# Farm Game

가속 시스템과 실시간 성장을 결합한 2D 농장 게임 팀 프로젝트입니다. Unity Web 빌드를 우선 대상으로 개발합니다.

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

웹 배포 파일은 소스 브랜치에 커밋하지 않습니다. 추후 CI 또는 별도 배포 단계에서 생성합니다.

## Git에 포함하는 항목

- `Assets/`와 모든 `.meta`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `ProjectSettings/`
- 협업 문서와 저장소 설정

`Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, `Builds`, IDE 생성 파일은 제외합니다.
