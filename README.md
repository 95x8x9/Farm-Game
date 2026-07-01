# Farm Game

가속 시스템과 실시간 성장을 결합한 2D 농장 게임 팀 프로젝트입니다. Unity Web 빌드를 우선 대상으로 개발합니다.

## 바로 실행하기

[GitHub Pages에서 게임 실행](https://95x8x9.github.io/Farm-Game/)

배포된 WebGL 버전은 Unity Editor를 설치하지 않아도 최신 웹 브라우저에서 실행할 수 있습니다. 게임 실행 파일은 GitHub Pages가 정적 파일로 제공하므로 별도의 백엔드 서버는 필요하지 않습니다. 현재 저장 데이터는 브라우저의 IndexedDB에 보관되며, 브라우저 데이터 삭제 또는 다른 기기·브라우저 사용 시 공유되지 않습니다.

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

### GitHub Pages 배포

현재 공개 버전은 별도의 `gh-pages` 브랜치 루트에 WebGL 빌드 결과를 올려 배포합니다. GitHub Pages는 정적 웹 서버 역할만 하며 게임 로직과 저장은 브라우저에서 실행됩니다.

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
