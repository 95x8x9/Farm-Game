# 협업 가이드

## 작업 시작

1. Unity에서 모든 변경을 저장합니다.
2. 브랜치를 바꾸기 전 Unity Editor를 종료합니다.
3. `main`을 최신 상태로 만든 뒤 작업 브랜치를 생성합니다.

```powershell
git switch main
git pull --ff-only
git switch -c feature/short-description
```

## 브랜치와 커밋

- `feature/`: 기능
- `fix/`: 버그 수정
- `chore/`: 환경 및 도구
- `docs/`: 문서

커밋은 `feat:`, `fix:`, `chore:`, `docs:`, `test:` 접두사를 사용하고 한 가지 목적만 담습니다.

## Unity 파일 규칙

- `.meta` 파일을 삭제하거나 누락하지 않습니다.
- 에셋 이동과 이름 변경은 Unity Project 창에서 수행합니다.
- `Library`, `Temp`, `Obj`, `Logs`, `UserSettings`, 빌드 결과를 커밋하지 않습니다.
- 씬과 프리팹은 텍스트 병합이 가능하지만 같은 파일을 동시에 편집하지 않는 것을 우선합니다.
- 작업을 시작하기 전에 담당 Scene과 Prefab을 팀 채널에 공유합니다.
- 공통 Scene에는 조립만 하고 기능별 오브젝트는 Prefab으로 분리합니다.
- 충돌 난 `.unity`, `.prefab`, `.asset` 파일에 무조건 ours/theirs를 적용하지 않습니다.

Unity에는 Scene과 Prefab을 위한 `UnityYAMLMerge`가 포함되어 있습니다. 충돌이 복잡하면 작업자끼리 변경 내용을 확인한 뒤 한 사람이 Unity Editor에서 다시 조립합니다.

## Git LFS

PSD, PSB, Aseprite, Blender, FBX, 원본 WAV 및 영상은 Git LFS로 관리합니다. 새로운 대형 바이너리 확장자를 추가하기 전에 `.gitattributes`에 LFS 규칙을 먼저 등록합니다.

```powershell
git lfs install
git lfs pull
git lfs status
```

## Pull Request

- `main`에 직접 푸시하지 않습니다.
- 최소 한 명의 리뷰를 받습니다.
- 리뷰 대화를 모두 해결합니다.
- 변경한 Scene, Prefab, ProjectSettings를 PR 본문에 명시합니다.
- Unity Console 오류가 없는지 확인합니다.
- Web Development 빌드가 성공하는지 확인합니다.
- 승인 후 Squash merge합니다.

## Unity 버전 변경

Unity Editor 또는 패키지 업그레이드는 별도 PR로 진행합니다. 업그레이드 PR에는 영향받은 에셋, 마이그레이션 결과, Web 빌드 결과를 기록합니다.
