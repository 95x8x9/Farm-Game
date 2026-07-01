# Farm Prototype

## 실행

1. Unity `6000.3.10f1`에서 프로젝트를 연다.
2. `Assets/_Project/Scenes/FarmScene.unity`를 연다.
3. Play 버튼을 누른다.

## 조작

- 회색 밭 클릭: 밭 구매 (`100원`)
- 구매한 빈 밭 클릭: 밀 심기 (`10원`)
- 자라는 작물 클릭: 남은 횟수가 있으면 물주기 타이밍 게임 시작
- 타이밍 게임: 초록 구간에서 클릭 또는 `Space`
- 노란 작물 클릭: 수확 (`20원`)
- `R`: 저장 데이터 초기화

작물은 심는 즉시 타이머가 시작되어 물을 주지 않아도 자란다. 물주기에 성공하면 남은 시간이 60초, 실패하면 30초 줄어들며 두 경우 모두 물주기 횟수를 한 번 사용한다. 성장 시간이 `n`분인 작물에는 최대 `n`번 물을 줄 수 있다. 저장 데이터에는 남은 시간이 아니라 UTC 완료 시각이 기록되므로 게임을 종료해도 성장이 이어진다.

## 저장 구조

`IGameRepository`가 저장소 경계를 제공하며 현재 구현은 `PlayerPrefsGameRepository`이다. Web 빌드에서는 브라우저 IndexedDB에 JSON이 저장된다. 서버 DB를 연결할 때는 이 구현을 API 기반 저장소로 교체한다.

## 씬 다시 생성

Unity 메뉴에서 `Farm Game > Prototype > Rebuild Farm Scene`을 실행한다. 이 작업은 `FarmScene`과 `Wheat.asset`을 프로토타입 기본값으로 다시 만든다.

## Web 빌드 환경

Unity가 설치되지 않은 환경에서는 아래 GitHub Pages 주소로 빌드된 게임을 바로 실행할 수 있다.

[https://95x8x9.github.io/Farm-Game/](https://95x8x9.github.io/Farm-Game/)

별도의 백엔드 서버는 필요하지 않지만 WebGL 파일을 전달할 정적 웹 서버는 필요하다. 공개 환경에서는 GitHub Pages가 이 역할을 담당한다. 빌드와 배포 절차는 [README.md](README.md#github-pages-배포)를 참고한다.
