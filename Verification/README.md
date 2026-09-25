# P0 코어 회귀 검증

- Unity Editor: 6000.3.7f1
- 실행일: 2026-09-26 (KST)
- 테스트: `Assets/AcceptanceTests/`의 EditMode/PlayMode 혼합 테스트 26개 (코어·HUD 23개, 실제 씬 흐름 3개)
- 최신 결과: [acceptance-editor-results.xml](acceptance-editor-results.xml) — 26 통과, 0 실패
- C# 빌드: `dotnet build Assembly-CSharp.csproj --no-restore -v:q` — 오류 0개

Unity Editor가 원본 프로젝트를 열고 있어서, 동일한 `Assets`, `Packages`, `ProjectSettings`를 복제한 별도 검증 프로젝트에서 배치 테스트를 실행했다. 런타임 테스트는 실제 물리 프레임에서 적 공격, 장애물 윗면 접촉, Near Miss, 스톰프, 슬램, 스와이프를 검사한다. 씬 테스트는 `EndlessRun`을 열어 Title→GO, Settings, Result→Retry/Main Menu를 확인한다.

이 결과는 `ACCEPTANCE.md`의 P0 전체 합격을 의미하지 않는다. 빌드 씬의 전체 패턴 연결 플레이, Android 입력과 실기기 검증은 아직 남아 있다.
