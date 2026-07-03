#!/usr/bin/env bash
set -Eeuo pipefail

BRANCH="${DEPLOY_BRANCH:-codex/deploy-production}"
APP_DIR="${APP_DIR:-/opt/farm-game/repo}"
WEB_RELEASES="${WEB_RELEASES:-/var/www/farm-game-releases}"
WEB_CURRENT="${WEB_CURRENT:-/var/www/farm-game-current}"
SERVICE_NAME="farm-game"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script as root: sudo bash deploy/update-server.sh" >&2
  exit 1
fi

if [[ ! -f /etc/farm-game.env ]]; then
  echo "/etc/farm-game.env is missing." >&2
  exit 1
fi

if [[ ! -f "${APP_DIR}/deploy/web/index.html" ]]; then
  echo "deploy/web/index.html is missing. Publish a WebGL build first." >&2
  exit 1
fi

runuser -u farmgame -- git -C "${APP_DIR}" fetch origin "${BRANCH}"

current_branch="$(runuser -u farmgame -- git -C "${APP_DIR}" branch --show-current)"
if [[ "${current_branch}" != "${BRANCH}" ]]; then
  echo "Expected branch ${BRANCH}, found ${current_branch}." >&2
  echo "Switch to the deployment branch during the one-time bootstrap." >&2
  exit 1
fi

runuser -u farmgame -- git -C "${APP_DIR}" pull --ff-only origin "${BRANCH}"
runuser -u farmgame -- npm --prefix "${APP_DIR}/Server" ci --omit=dev
runuser -u farmgame -- npm --prefix "${APP_DIR}/Server" test

release_id="$(date +%Y%m%d%H%M%S)-$(runuser -u farmgame -- git -C "${APP_DIR}" rev-parse --short HEAD)"
release_dir="${WEB_RELEASES}/${release_id}"

install -d -m 0755 "${WEB_RELEASES}" "${release_dir}"
rsync -a --delete "${APP_DIR}/deploy/web/" "${release_dir}/"
chown -R root:apache "${release_dir}"
find "${release_dir}" -type d -exec chmod 0755 {} +
find "${release_dir}" -type f -exec chmod 0644 {} +

install -m 0644 \
  "${APP_DIR}/deploy/apache/farm-game.conf" \
  /etc/httpd/conf.d/farm-game.conf
rm -f /etc/httpd/conf.d/farm-game-api.conf

install -m 0644 \
  "${APP_DIR}/deploy/systemd/farm-game.service" \
  /etc/systemd/system/farm-game.service

if command -v restorecon >/dev/null 2>&1; then
  restorecon -RF "${WEB_RELEASES}" >/dev/null
fi
if command -v setsebool >/dev/null 2>&1; then
  setsebool -P httpd_can_network_connect 1
fi

apachectl configtest

# Publish the complete release in one filesystem operation so browsers never
# observe a partially copied Unity build.
ln -sfn "${release_dir}" "${WEB_CURRENT}.next"
mv -Tf "${WEB_CURRENT}.next" "${WEB_CURRENT}"

systemctl daemon-reload
systemctl enable "${SERVICE_NAME}" httpd >/dev/null
systemctl restart "${SERVICE_NAME}"
systemctl reload httpd

curl --fail --silent --show-error http://127.0.0.1/health
echo

mapfile -t old_releases < <(
  find "${WEB_RELEASES}" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %p\n' \
    | sort -rn \
    | tail -n +4 \
    | cut -d' ' -f2-
)
for old_release in "${old_releases[@]}"; do
  rm -rf -- "${old_release}"
done

echo "Deployment complete: ${BRANCH}"
