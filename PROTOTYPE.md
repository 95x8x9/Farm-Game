# Farm Prototype

## 실행

1. Unity `6000.3.10f1`에서 프로젝트를 연다.
2. `Assets/_Project/Scenes/FarmScene.unity`를 연다.
3. Play 버튼을 누른다.

## 조작

- 회색 밭 클릭: 밭 구매 (`100원`)
- 구매한 빈 밭 클릭: 밀 심기 (`10원`)
- 파란 표시가 있는 밭 클릭: 물주기 타이밍 게임 시작
- 타이밍 게임: 초록 구간에서 클릭 또는 `Space`
- 노란 작물 클릭: 수확 (`20원`)
- `R`: 저장 데이터 초기화

밀은 물주기에 성공한 뒤 60초 동안 자란다. 저장 데이터에는 남은 시간이 아니라 UTC 완료 시각이 기록되므로 게임을 종료해도 성장이 이어진다.

## 저장 구조

`IGameRepository`가 저장소 경계를 제공하며 현재 구현은 `PlayerPrefsGameRepository`이다. Web 빌드에서는 브라우저 IndexedDB에 JSON이 저장된다. 서버 DB를 연결할 때는 이 구현을 API 기반 저장소로 교체한다.

## 씬 다시 생성

Unity 메뉴에서 `Farm Game > Prototype > Rebuild Farm Scene`을 실행한다. 이 작업은 `FarmScene`과 `Wheat.asset`을 프로토타입 기본값으로 다시 만든다.
