# Farm Game

가속 시스템과 실시간 성장을 결합한 2D 농장 게임 팀 프로젝트입니다. Unity Web 빌드를 우선 대상으로 개발합니다.

## 바로 실행하기

[운영 서버에서 게임 실행](https://educs242.ai-startpoint.com/)

운영 환경은 NCP Load Balancer 뒤의 두 Web 서버가 Unity WebGL 정적 파일과 Node.js API를 제공하고, 별도의 Cloud DB for MySQL을 사용합니다. Unity Editor를 설치하지 않아도 최신 웹 브라우저에서 실행할 수 있습니다.

## 고정 개발 환경

- Unity Editor: `6000.3.10f1`
- Template: Universal 2D
- Render Pipeline: Universal Render Pipeline `17.3.0` / 2D Renderer
- Target Platform: Web
- IDE: Visual Studio Code + Microsoft Unity extension
- Version Control: Git + Git LFS

Unity 버전은 `ProjectSettings/ProjectVersion.txt`, 패키지 버전은 `Packages/packages-lock.json`이 기준입니다. 팀 합의 없이 Unity Editor 또는 패키지 버전을 올리지 않습니다.

## 최초 설치

1. Unity Hub에서 Unity `6000.3.10f1`을 설치합니다.
2. 해당 Editor의 모듈에서 `Web Build Support`를 설치합니다.
3. Git과 Git LFS, Visual Studio Code를 설치합니다.
4. VS Code에서 Microsoft의 `Unity` 확장을 설치합니다.
5. 저장소를 복제하고 LFS 파일을 내려받습니다.

```powershell
git clone https://github.com/95x8x9/Farm-Game.git
cd Farm-Game
git lfs install
git lfs pull
```

Unity Hub에서 `Add project from disk`를 선택해 복제한 폴더를 엽니다. `Library`는 저장소에 포함하지 않으며 최초 실행 시 각 컴퓨터에서 다시 생성됩니다.

## Unity 협업 설정

프로젝트에는 다음 설정이 적용되어 있습니다.

- `Version Control / Mode`: Visible Meta Files
- `Editor / Asset Serialization`: Force Text
- `Editor / Enter Play Mode Options`: 활성화

에셋을 이동하거나 이름을 바꿀 때는 파일 탐색기가 아니라 Unity의 Project 창을 사용합니다. 모든 에셋과 폴더의 `.meta` 파일을 함께 커밋해야 합니다.

## 개발 흐름

`main`에 직접 개발하지 않고 작업 브랜치에서 Pull Request를 만듭니다.

```powershell
git switch main
git pull --ff-only
git switch -c feature/farm-grid

# 작업 후
git add <변경한 파일>
git commit -m "feat: add farm grid"
git push -u origin feature/farm-grid
```

브랜치 이름은 `feature/`, `fix/`, `chore/`, `docs/` 접두사를 사용합니다. 자세한 협업 규칙은 [CONTRIBUTING.md](CONTRIBUTING.md)를 확인합니다.

## Web 빌드

Unity 메뉴에서 다음 명령을 사용할 수 있습니다.

- `Farm Game > Build > Web > Development`
- `Farm Game > Build > Web > Release`

빌드 결과는 `Builds/Web`에 생성되며 Git에는 포함하지 않습니다. Unity Editor를 닫은 상태에서는 다음 명령으로도 릴리스 빌드를 만들 수 있습니다.

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe" `
  -batchmode -quit `
  -projectPath "$PWD" `
  -executeMethod FarmGame.Editor.WebBuildCommand.BuildRelease `
  -logFile "Logs/web-build.log"
```

웹 배포 파일은 용량이 크고 빌드 결과물에 해당하므로 소스 브랜치에는 커밋하지 않습니다.

## NCP 운영 배포

운영 배포는 `main`을 서버에서 직접 빌드하지 않습니다. 전용 브랜치인
`codex/deploy-production`에 검증된 소스와 WebGL 산출물을 만든 뒤, 각 Web
서버가 해당 브랜치를 내려받는 방식으로 진행합니다.

배포 구성과 스크립트의 세부 설명은
[배포 브랜치의 deploy/README.md](https://github.com/95x8x9/Farm-Game/blob/codex/deploy-production/deploy/README.md)에서도
확인할 수 있습니다.

### 운영 구성

- 공개 주소: `https://educs242.ai-startpoint.com/`
- Web 서버: `farm-web1`, `farm-web2` (Rocky Linux 8.10)
- Web/API: Apache HTTP Server + Node.js 22 + systemd
- 데이터베이스: NCP Cloud DB for MySQL의 `farmdb`
- 로드밸런서 헬스 체크: HTTP `GET /health`
- 배포 브랜치: `codex/deploy-production`
- 애플리케이션 경로: `/opt/farm-game/repo`
- 비밀 환경파일: `/etc/farm-game.env` (Git에 커밋하지 않음)
- 정적 파일 링크: `/var/www/farm-game-current`
- 릴리스 보관 경로: `/var/www/farm-game-releases`

서버 주소, DB 비밀번호, JWT 비밀키 등의 인증정보는 README, 이슈, PR, 채팅,
커밋에 기록하지 않습니다.

### 최초 서버 준비

각 Web 서버에는 Apache, Git, rsync, Node.js 22가 설치되어 있어야 합니다.
애플리케이션 전용 계정과 디렉터리를 만든 뒤 배포 브랜치를 복제합니다.

```bash
useradd --system --create-home \
  --home-dir /opt/farm-game \
  --shell /sbin/nologin farmgame

mkdir -p /opt/farm-game
chown farmgame:farmgame /opt/farm-game

runuser -u farmgame -- git clone \
  --branch codex/deploy-production \
  https://github.com/95x8x9/Farm-Game.git \
  /opt/farm-game/repo
```

API가 사용하는 환경파일을 서버마다 생성하고 root만 읽을 수 있게 설정합니다.
실제 값은 비밀 저장소 또는 서버 관리자에게 전달받아 입력합니다.

```bash
install -o root -g root -m 600 /dev/null /etc/farm-game.env
vi /etc/farm-game.env
```

```dotenv
PORT=3000
NODE_ENV=production

DB_HOST=<CLOUD_DB_PRIVATE_ENDPOINT>
DB_PORT=3306
DB_USER=<APPLICATION_DB_USER>
DB_PASSWORD=<APPLICATION_DB_PASSWORD>
DB_NAME=farmdb
DB_SSL=false

JWT_SECRET=<LONG_RANDOM_VALUE>
JWT_EXPIRES_IN=7d

CORS_ORIGINS=https://educs242.ai-startpoint.com
SERVER_NAME=farm-web1
```

```bash
chown root:root /etc/farm-game.env
chmod 600 /etc/farm-game.env
```

두 Web 서버는 동일한 DB 설정과 `JWT_SECRET`을 사용해야 합니다. 단,
`SERVER_NAME`은 각각 `farm-web1`, `farm-web2`로 설정합니다. 애플리케이션은
원격 Cloud DB를 사용하므로 Web 서버의 로컬 MariaDB는 사용하지 않습니다.

최초 배포도 일반 업데이트와 같은 스크립트로 실행합니다.

```bash
cd /opt/farm-game/repo
bash deploy/update-server.sh
```

이 스크립트는 다음 작업을 수행합니다.

1. 배포 브랜치를 fast-forward 방식으로 업데이트합니다.
2. `npm ci --omit=dev`와 API 테스트를 실행합니다.
3. WebGL 파일을 버전별 릴리스 디렉터리에 복사합니다.
4. Apache와 systemd 설정을 설치합니다.
5. 완성된 릴리스로 심볼릭 링크를 원자적으로 교체합니다.
6. API를 재시작하고 헬스 체크를 재시도합니다.
7. 최근 릴리스 세 개만 남깁니다.

### 새 버전 발행

PR을 `main`에 머지한 뒤 Windows의 전용 배포 작업 폴더에서 실행합니다.
Unity Editor는 닫아 두는 편이 안전합니다.

```powershell
cd C:\path\to\Farm-Game-production
.\deploy\publish.ps1
```

스크립트는 `origin/main`을 배포 브랜치에 병합하고 Unity WebGL Release 빌드를
만든 뒤, 결과를 `deploy/web`에 복사하여 커밋하고 푸시합니다. 빌드가 실패하면
`Logs/web-build.log`를 확인합니다.

### 두 Web 서버 업데이트

로드밸런서 서비스가 유지되도록 `farm-web1`과 `farm-web2`를 한 대씩 순서대로
업데이트합니다. 한 서버의 검증을 마친 후 다음 서버로 이동합니다.

```bash
cd /opt/farm-game/repo
bash deploy/update-server.sh
```

각 서버에서 다음 항목을 확인합니다.

```bash
curl --fail http://127.0.0.1/health
curl -I http://127.0.0.1/Build/Web.data.br
curl -I http://127.0.0.1/Build/Web.framework.js.br
curl -I http://127.0.0.1/Build/Web.wasm.br
```

정상 기준은 다음과 같습니다.

- `/health`: HTTP 200, `db` 값이 `connected`
- `.data.br`: HTTP 200, `Content-Encoding: br`
- `.framework.js.br`: HTTP 200, JavaScript Content-Type
- `.wasm.br`: HTTP 200, `Content-Type: application/wasm`
- `systemctl is-active farm-game httpd`: 두 서비스 모두 `active`

두 서버를 모두 업데이트한 뒤 공개 주소에서도 `/health`와 WebGL 로딩을 여러 번
확인합니다. 한 서버만 업데이트하면 로드밸런서가 이전 서버로 연결할 때 간헐적인
404 또는 `Unknown data format` 오류가 발생할 수 있습니다.

### 롤백

배포 스크립트는 최근 릴리스 세 개를 보관합니다. 문제가 생기면 서버별로 이전
릴리스 디렉터리를 확인하고 `farm-game-current` 링크를 이전 버전으로 교체합니다.

```bash
ls -lt /var/www/farm-game-releases

ln -sfn /var/www/farm-game-releases/<previous-release> \
  /var/www/farm-game-current.next
mv -Tf /var/www/farm-game-current.next /var/www/farm-game-current
systemctl reload httpd
```

API 소스까지 롤백해야 한다면 배포 브랜치에서 정상 커밋을 되돌린 새 커밋을 만든
뒤 다시 발행합니다. 서버 저장소를 `git reset --hard`로 직접 되돌리지 않습니다.

### 장애 확인

```bash
systemctl status farm-game httpd --no-pager -l
journalctl -u farm-game -n 100 --no-pager
apachectl configtest
readlink -f /var/www/farm-game-current
```

- `502` 또는 `503`: API 재시작 중일 수 있으므로 잠시 후 `/health`를 다시 확인합니다.
- `.br` 파일 404: 현재 릴리스 링크와 `Build` 디렉터리를 확인합니다.
- `Unknown data format`: 두 Web 서버가 같은 배포 커밋인지 확인합니다.
- Apache 응답에 `X-Powered-By: Express`가 붙은 정적 파일 404: `/` 전체를 Node로
  전달하는 구형 `ProxyPass` 설정이 남아 있는지 확인합니다.
- `npm audit` 경고: 배포 중 `npm audit fix --force`를 실행하지 말고 별도 PR에서
  의존성 호환성과 테스트를 검증한 뒤 업데이트합니다.

### GitHub Pages 정적 배포(선택)

GitHub Pages가 필요한 경우 별도의 `gh-pages` 브랜치 루트에 WebGL 빌드 결과를 올려 정적 미리보기로 사용할 수 있습니다. 운영 배포는 위의 NCP 절차를 따릅니다.

배포 순서는 다음과 같습니다.

1. Unity에서 `Farm Game > Build > Web > Release`를 실행합니다.
2. `Builds/Web`의 결과물과 `index.html`, `TemplateData`를 배포용 폴더에 복사합니다.
3. Brotli 압축 파일(`.br`)을 사용하는 경우 서버가 `Content-Encoding: br` 헤더를 보내야 합니다. GitHub Pages에 직접 배포할 때는 압축을 해제하고 `index.html`의 파일 경로도 함께 수정합니다.
4. 배포용 파일을 `gh-pages` 브랜치에 커밋하고 푸시합니다.
5. [공개 주소](https://95x8x9.github.io/Farm-Game/)에서 로딩과 한글 표시를 확인합니다.

WebGL 빌드는 `file://`로 직접 열면 브라우저 보안 정책 때문에 실행되지 않을 수 있습니다. 로컬 테스트도 간단한 HTTP 서버를 사용합니다.

```powershell
cd Builds/Web
python -m http.server 8000
```

그다음 브라우저에서 `http://localhost:8000`을 엽니다.

## Git에 포함하는 항목

- `Assets/`와 모든 `.meta`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `ProjectSettings/`
- 협업 문서와 저장소 설정

`Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, `Builds`, IDE 생성 파일은 제외합니다.
