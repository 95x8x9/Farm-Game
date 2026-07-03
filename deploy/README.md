# Production deployment

The production branch contains the API source, deployment configuration, and the
Unity WebGL release artifact under `deploy/web`.

## Publish from Windows

Use the dedicated deployment worktree and keep it clean.

```powershell
cd C:\Users\gsh\Naver_Cloud_project\Farm-Game-production
.\deploy\publish.ps1
```

The script merges `origin/main`, creates a Unity WebGL release build, copies it
to `deploy/web`, commits it, and pushes `codex/deploy-production`.

`Server/package-lock.json` is committed on the deployment branch so the server
can use `npm ci`. Refresh and review it separately whenever dependencies in
`Server/package.json` change.

## One-time server bootstrap

The repository is expected at `/opt/farm-game/repo` and must be owned by the
`farmgame` system user. `/etc/farm-game.env` remains outside Git.

```bash
runuser -u farmgame -- git -C /opt/farm-game/repo fetch origin codex/deploy-production
runuser -u farmgame -- git -C /opt/farm-game/repo switch --track origin/codex/deploy-production
bash /opt/farm-game/repo/deploy/update-server.sh
```

If the local branch already exists, use:

```bash
runuser -u farmgame -- git -C /opt/farm-game/repo switch codex/deploy-production
bash /opt/farm-game/repo/deploy/update-server.sh
```

## Routine server update

After publishing a new deployment commit:

```bash
cd /opt/farm-game/repo
bash deploy/update-server.sh
```

Run the same command on both `farm-web1` and `farm-web2`. Set `SERVER_NAME`
appropriately in each server's `/etc/farm-game.env`.
