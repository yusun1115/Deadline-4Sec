# 코어·시작 연출 회귀 검증

- Unity Editor: 6000.3.7f1
- 실행일: 2026-09-27 (KST)
- 테스트: `Assets/AcceptanceTests/`의 EditMode/PlayMode 혼합 테스트 29개 (코어·HUD 26개, 실제 씬 흐름 3개)
- 당시 결과: [acceptance-editor-results.xml](acceptance-editor-results.xml) — **29 통과, 0 실패**, Unity 종료 코드 0. 최신 전체 회귀는 문서 하단에 기록했다.
- C# 빌드: `dotnet build Assembly-CSharp.csproj --no-restore -v:q` — 오류 0개, 기존 SDK 어셈블리 버전 경고 2개

Unity Editor가 원본 프로젝트를 열고 있어서, 게임 스크립트·테스트·씬 변경을 동기화한 별도 검증 프로젝트에서 배치 테스트를 실행했다. 최종 실행은 Direct3D 11 렌더링을 포함한다. 런타임 테스트는 실제 물리 프레임에서 적 공격, 장애물 윗면 접촉, Near Miss, 스톰프, 슬램, 스와이프를 검사한다. 씬 테스트는 `EndlessRun`을 열어 Title→Ready? 연출→Go!, Settings, Result→Retry/Main Menu를 확인한다.

## 이번 수정의 검증 범위

- 고공 슬램 조건에서 머리 착지 → 스톰프 150점·바운스, 주변 적은 처치하지 않음.
- 머리 가장자리·한 번에 머리 아래로 내려가는 큰 이동량·겹친 적 → 가장 먼저 닿는 머리에서 발이 멈춤.
- 장애물 윗면이 적 머리보다 앞에 있으면 안전한 장애물 착지가 우선하며 처치·충격파·보상 없음.
- 적 머리와 무관한 맨 바닥 슬램 → 주변 적 2마리 광역 처치. 유효하지 않은 몸통 접촉은 사망.
- 숫자 카운트다운 없이 Ready?→Go!, 연출 중 자동 이동과 기록 정지, Go에서 거리 0부터 측정.
- Go 이전 코스 생성·유지, 첫 패턴 접근 여유와 4초 이내 후보 기회, 인게임 카메라에서 첫 위협이 보임.
- Title 재등장 없이 시작/재시도, 결과에서 Main Menu 복귀.

## 480×800 화면 렌더링

| 연출 단계 | 증거 |
| --- | --- |
| 캐릭터 측면에서 Ready? | [측면 화면](intro-ready-side.png) |
| 카메라 이동 중 | [전환 화면](intro-camera-transition.png) |
| Ready? 상태에서 앞 코스가 보임 | [코스 미리보기](intro-ready-approach.png) |
| 기존 인게임 뷰·Go!·4.00 타이머 | [Go 화면](intro-go.png) |

초기 렌더링 검증에서 프레임 지연이 연출을 건너뛸 수 있어 진행량을 제한했다. 화면 밖에 배치된 생존 타이머도 상단 기준으로 고정하고 최종 29개 테스트를 다시 통과했다.

이 결과는 `ACCEPTANCE.md`의 P0 전체 합격을 의미하지 않는다. 아래 고정 시드 경로 검증을 추가했으며, 다른 시드/설정의 연결과 Android 입력·실기기 검증은 남아 있다.

## GFCRedSpirit-Bold 폰트 적용 검증 (2026-09-27)

- 원본: `Assets/Font/GFCRedSpirit-Bold.ttf`. TMP SDF 자산도 이 원본만 참조하며 다른 폰트 fallback과 대체 굵기 자산은 비웠다.
- EndlessRun·GrayboxCourse·TestScene의 기존 UI Text/TMP 폰트 42개를 연결하고, 생성되는 메뉴·Near Miss는 `GameFont.Apply`로 동일한 자산을 사용한다. TMP 기본 폰트도 같은 자산이다.
- 폰트 변경 후 관련 씬 테스트 3개를 재실행해 **3 통과 / 0 실패**를 확인했다. 활성/비활성 UI와 생성된 텍스트의 원본 일치 및 타이머의 실제 글자 생성도 검사했다. 결과: [font-scene-results.xml](font-scene-results.xml).
- 런타임/에디터 C# 빌드 오류 0개. 위의 29개 코어 결과는 폰트 변경 전 전체 회귀 기록이며, 이번 변경은 관련 씬 3개를 재검증했다.
- 새 폰트의 줄 높이에 맞춰 생존 타이머 표시 영역과 글자 크기 자동 조절을 보완했다. 480×800 렌더링에서 타이머와 메뉴/결과의 잘림이 없음을 확인했다.

| 화면 | 증거 |
| --- | --- |
| Title | [타이틀](font-title.png) |
| Settings | [설정](font-settings.png) |
| Go / HUD | [인게임 폰트와 타이머](intro-go.png) |
| Result | [결과](font-result.png) |

## 실제 코스 연결 재현 (2026-09-27)

`CourseReplayAcceptanceTests`는 EndlessRun 씬·원본 패턴 프리팹·실제 물리 판정을 사용한다. 플레이어의 공개 입력 진입점만 호출하며 적 처치, 타이머 초기화, 위치/속도 변경을 테스트에서 주입하지 않는다.

| 조건 | 결과 | 실제 보상 | 증거 |
| --- | --- | --- | --- |
| 시드 12345, 60FPS, 60.02초 | 통과, Score 72,600 / Distance 1,009.5m | 161회, 최대 공백 1.72초 | [결과 XML](course-replay-60fps-results.xml), [입력·패턴 로그](course-replay.txt) |
| 시드 12345, 30FPS, 60.03초 | 통과, Score 71,200 / Distance 995.5m | 158회, 최대 공백 1.73초 | [결과 XML](course-replay-30fps-results.xml), [입력·패턴 로그](course-replay-30fps.txt) |

- 패턴 A–E, Ground→Air→Stomp 연계, Easy/Medium/Hard 구간을 실제로 통과했다. 같은 프리팹 연속 생성·같은 카테고리 3회 반복·해금 전 난이도 진입이 없었다.
- 실패/수정 루프: 예상 도착 시간만으로 Hard를 해금하면 호밍으로 43.88초에 Hard에 진입했다. 실제 런 시간으로 해금하도록 수정한 뒤 첫 Hard 진입은 60FPS에서 51.12초, 30FPS에서 51.70초였다.
- 혼합 패턴의 낮은 공중 바운스 이후 지상 적은 높은 옆 통과로 회피하고, 레인 장애물은 공중에서도 옆 입력으로 피할 수 있었다. 점프 뒤 슬라이드 바는 공중 아래 입력→빠른 착지→슬라이드로 통과한다.
- 주요 Inspector 값: 자동 전진 12→최대 16m/s, 증가 0.06m/s², 레인 이동 22m/s, 호밍 탐색 10m, 슬라이드 높이 비율 0.1, 슬램 최소 높이 6.5m. 이번 재현을 위해 이 값을 변경하지 않았다.
- 이는 위 시드/설정에서 정상 입력 경로가 존재한다는 증거다. 모든 시드, 사람의 조작감, Android 프레임 시간·성능을 합격시킨 결과는 아니다. 이후 이펙트 추가의 판정 회귀는 별도로 기록한다.

## 전투 VFX와 회귀 (2026-09-27)

- 실제 레인 입력 처치로 이펙트가 생성되는 것을 확인했다. 50회 연속 효과 요청에도 렌더러 20개를 재사용하고, 충돌체가 없으며 효과 만료·게임 종료·씬 종료 때 정상 정리된다.
- 초기 씬 종료 검사에서 파괴된 렌더러를 정리하려는 예외를 발견해 Unity 객체 유효성 검사를 추가했다. 수정 후 코어·HUD 26개 + 실제 씬/폰트/피드백 4개, **30 통과 / 0 실패**. [결과 XML](feedback-regression-results.xml).
- Direct3D 11, 480×800: [실제 레인 처치 링](feedback-lane-kill.png), [슬램 링 표시 샘플](feedback-slam-ring.png). 슬램 스크린샷은 시각 효과를 직접 호출한 표시 샘플이며, 맨 바닥/머리 판정은 코어 물리 테스트에서 별도로 검증한다.
- 최종 화면 샘플을 보완한 뒤 관련 테스트 1개를 다시 실행해 **1 통과 / 0 실패**를 확인했다. 가속 시뮬레이션의 게임 시간과 VFX 만료의 비배율 실제 시간을 분리해 검사한다. [최종 샘플·풀·종료 재검증](visual-feedback-final-results.xml). 기존 30개 회귀와 별도 재실행 기록이다.
- 사운드/진동 설정 코드와 카메라 피드백을 유지했다. Android Manifest 진동 권한·APK 내용은 빌드 후 검사한다. 실기기 진동·성능은 검증하지 않았다.

사용자 요청에 따라 현재 검증 범위는 **PC 검증과 APK 제작**이다. Android 설치·터치·저장 복원·진동·기기 성능 항목은 미검증으로 유지한다.

## 인터랙티브 튜토리얼과 PC 회귀 (2026-09-27)

- 최초 타이틀 탭 또는 설정의 Tutorial에서 10단계 실제 연습을 시작한다. 이동, 점프, 슬라이드, 레인 처치, 4초 회복, 비접촉 Near Miss, 호밍, 빠른 낙하, Stomp, 맨 바닥 Slam을 수행해야 진행한다. 실패한 단계만 재시도하며 연습 기록은 최고 기록에 저장하지 않는다.
- 첫 경로에서 마지막 슬램 착지점과 대상 간격이 멀어 광역 처치가 없었다. [실패 입력 기록](tutorial-failure-slam.txt), [첫 실패 결과](tutorial-failure-results.xml). 바닥 거리 탐지와 슬램 발동은 정상이라 연습 대상 Z만 23.5→21.5m로 조정했다. 게임의 슬램 최소 높이/반경과 머리 우선 규칙은 유지했다.
- 안내 입력만으로 30/60FPS에서 10단계를 모두 완료하고 새 일반 런의 Ready?→Go!로 진입했다. 실패/재시도·메뉴 복귀·최고 기록 보존도 통과했다. 처치/타이머/위치를 테스트가 주입하지 않으며, 단계의 준비·재배치는 실제 튜토리얼이 수행한다. [60FPS 입력 기록](tutorial-replay.txt), [30FPS 입력 기록](tutorial-replay-30fps.txt).
- 기존 코어·HUD·폰트·메뉴·시작·VFX·시드 12345 코스와 튜토리얼 3개를 합쳐 **35 통과 / 0 실패**. [회귀 결과](tutorial-regression-results.xml). 이는 검증 조건에서의 기능 증거이며 신규 플레이어의 설명 없는 이해도를 입증하지 않는다.
- Direct3D 11, 480×800: [첫 단계](tutorial-step-1.png), [Stomp 준비](tutorial-step-9.png), [Slam 준비](tutorial-step-10.png), [완료 화면](tutorial-complete.png). 실제 입력·타이머·판정은 위 로그/XML로 확인한다.

## 장애물 프리팹과 추가 코스 경로 (2026-09-27)

- 원본 Jump Obstacle / Slide Obstacle / Lane Blocker 각각의 앞면 접촉, 레인 공격 중 옆면 접촉, 고공 Slam 윗면 착지와 전진 이탈의 **9조건**을 통과했다. 앞면/옆면은 사망, 윗면은 생존·점수/콤보 없음·바닥 충격파 없음이며 장애물이 유지된다. 윗면 검사는 수직 접촉을 분리한 뒤 12m/s 전진을 재개하는 별도 물리 설정이다.
- 시드 1, 60FPS, 60.02초: 실제 보상 159회, 최대 공백 1.72초, Score 72,200 / Distance 1,009.2m. [입력 기록](course-replay-seed-1.txt).
- 위 두 검사는 [첫 추가 검증 결과](extended-core-first-results.xml)에서 통과했다. 같은 실행의 3분 경로는 137초까지 정상 진행한 뒤 검증기의 고정 프레임 상한에 걸렸다. 반복 히트 스톱의 실제 대기 프레임 때문에 상한을 요청 길이에 비례하도록 수정했다. [상한 실패 입력 기록](course-replay-long-first.txt).
- 같은 시드 98765 / 30FPS / 180.01초 재실행 **통과**: 실제 보상 516회, 최대 공백 1.73초, Score 234,800 / Distance 3,367.9m. 최대 자동 전진 속도에서 패턴 A–E 연결·반복 금지·난도 해금을 유지했다. [최종 결과](long-course-results.xml), [입력 기록](course-replay-long.txt).
- 새 코스 경로도 정상 공개 입력만 사용한다. 적 처치·타이머 초기화·속도 변경·텔레포트를 주입하지 않는다. APK/Android 성능 측정이나 모든 시드/설정의 보장을 의미하지 않는다.

## 이전 Android 개발 APK 기록 (코인·아이템 반영, 2026-09-28)

- 당시 Unity 6000.3.7f1에서 Development + Script Debugging, IL2CPP / ARM64, `EndlessRun` 씬을 빌드했다. 코인·6종 파워업 프리팹, 업그레이드 UI, 기존 타이틀/튜토리얼이 포함됐다. 최종 Unity 로그의 `Build Finished, Result: Success`를 확인했다.
- 당시 APK 기록은 **124,292,014 bytes**다. 첫 패키징에서 발생한 약 90MB의 ZIP 빈 공간은 검증 프로젝트의 생성된 Gradle 출력만 정리해 재패키징했다. 해당 파일은 아래 2026-09-29 빌드로 교체됐다.
- `apksigner verify --verbose`에서 v2 서명 검증 성공, 서명자 1명. `aapt`에서 최소 API 25, 대상 API 36, `arm64-v8a`, `android.permission.VIBRATE`를 확인했다. 현재 패키지 ID는 `com.UnityTechnologies.com.unity.template.urpblank`다.
- 당시 BuildPipeline은 성공했으나 직후 증거 JSON을 쓰는 단계에서 일시적인 Win32 IO 1224 오류로 배치 명령은 종료 코드 1을 반환했다. 당시 APK 자체는 별도 서명·패키지 검사를 통과했다.
- 당시 원본/검증 프로젝트의 `Assets` 313개 파일 해시가 일치했다. 현재 해시 파일은 아래 2026-09-29 빌드로 갱신됐다.
- 당시 APK는 Android 기기에 설치하거나 실행하지 않았다. 빌드/서명/내용 검사는 실기기 Acceptance 통과 증거가 아니다.

## PC 저장의 프로세스 재실행 검증 (2026-09-27)

- 첫 Unity 프로세스에서 정상 레인 입력으로 실제 적을 처치해 100점·콤보 1을 획득했다. GameOver의 실제 결과 저장 경로로 최고 점수/거리/콤보를 저장하고 사운드/진동 설정을 변경한 뒤 프로세스를 종료했다.
- 별도 Unity 프로세스에서 5개 값을 확인해 일치했고, 검사 전의 키 존재 여부와 값을 모두 복원했다. 임시 baseline/expected 파일도 정리했다. [재실행 결과](pc-persistence.txt), [첫 프로세스·콤보 검사 결과: 2 통과](pc-persistence-results.xml).
- 같은 실행에서 콤보 5/10/20의 x2/x3/x4 배율과 생존 타이머 독립, 게임 오버 후 점수 정지를 확인했다. 기존 회귀 35개와 구분되는 추가 검사다.
- `ActualKillResultLeavesSavedRecordsForASecondProcess`는 두 프로세스를 연속 실행해야 하므로 Explicit 검사다. 일반 전체 테스트에는 포함하지 않는다. 정확한 메서드를 선택해 실행한 후 새 Unity에서 `-executeMethod Deadline4Sec.Editor.PcPersistenceProbe.VerifyAndRestore`를 실행한다. 중단되면 `PcPersistenceProbe.Restore`로 원래 값을 복원한다.

## 씬에서 직접 편집 가능한 타이틀 (2026-09-27)

- EndlessRun·GrayboxCourse·TestScene에 `Canvas/TitlePanel`과 `SettingsPanel`, `ResultPanel/MainMenuButton`을 저장했다. TitleText·SubtitleText·버튼 Label은 Inspector에서 편집하며, 버튼 기능은 저장된 On Click에 연결되어 있다. 편집 안내: [TITLE_EDITING.md](../TITLE_EDITING.md).
- 런타임의 메뉴 생성과 CanvasScaler/Retry 배치 덮어쓰기를 제거했다. 런타임은 표시 상태, 설정의 ON/OFF 값, 클릭 피드백을 처리한다. GFCRedSpirit-Bold 폰트 규칙은 유지한다.
- 새 편집 검사는 세 씬의 저장된 구조/On Click 연결을 검사하고, 제목 문구·폰트 크기·색·버튼 위치·배경 색·CanvasScaler 값을 수정/저장/재열기/Play/Settings 왕복/일반 런/결과의 Main Menu/Play 종료 후 비교했다. 타이틀 중복 생성도 없었다. 이 2개 검사와 메뉴/튜토리얼 6개가 통과했다.
- 첫 실행은 **8 통과 / 1 실패**였다. 실패는 기존 테스트가 이제 Inspector에 연결하기 위해 공개된 ToggleSound 메서드를 NonPublic 반사로 찾던 부분이었다. [첫 결과](editable-menu-first-results.xml). 테스트를 실제 Sound/Vibration Button의 On Click 호출로 수정하고 해당 시작/설정/Ready?→Go! 검사를 다시 실행해 **1 통과 / 0 실패**를 확인했다. [수정 후 결과](editable-menu-flow-results.xml). 두 실행으로 서로 다른 관련 검사 **9개**를 확인했으며 전체 Acceptance 재검증을 의미하지 않는다.
- 480×800 타이틀/설정/시작 화면을 렌더링해 확인했다. 새 타이틀 편집과 메뉴 흐름 검증은 PC 범위이며 Android 실행은 포함하지 않는다.
- 변경된 씬/메뉴를 포함한 APK를 다시 제작하고 서명·최소/대상 API·VIBRATE 권한·ARM64를 확인했다. 주요 소스 70개가 원본/검증 프로젝트와 일치하며 APK와 소스 해시를 갱신했다. 기기 설치/실행은 미실행이다.
- Windows Unity 에디터 저장 증거이며 Android 앱의 설치/재실행 저장 검증을 대신하지 않는다. 전체 남은 조건은 [PC Acceptance 범위 점검](pc-acceptance-audit.md)에 기록했다.

## 타이틀 탭·튜토리얼 Skip (2026-09-28)

- 사용자가 편집한 `Titlelogo`, `TitleFrame`, `TitleFrame (1)` 이미지는 유지했다. 타이틀의 Start/Tutorial 버튼을 제거하고 `Tap to start` 텍스트와 화면 전체의 투명한 탭 영역을 씬에 저장했다. 프레임/로고 Image의 Raycast Target을 꺼서 화면 탭이 가로막히지 않게 했다. [현재 480×800 타이틀](font-title.png).
- 완료/스킵 기록이 없는 첫 타이틀 탭은 튜토리얼로 간다. 기록이 있으면 기존 Ready?→Go! 일반 런으로 간다. Settings에는 진동 아래 Tutorial 버튼을 추가해 재연습할 수 있다.
- 연습 우측 상단 Skip과 예/아니오 확인창을 세 씬에 저장했다. 확인 중 시간·이동·입력·단계 전환이 멈춘다. 아니오는 현재 단계로 돌아가고 예는 완료 플래그를 저장한 뒤 새 일반 런으로 연결한다. 한글 텍스트와 GFCRedSpirit-Bold 사용을 [확인창 렌더링](tutorial-skip-confirmation.png)에서 확인했다.
- 첫 관련 실행에서 기존 타이틀 편집 검사가 사용자가 제거한 `TitleText`를 가정해 **9 통과 / 2 실패**했다. 테스트를 실제 이미지 타이틀 구조에 맞춘 뒤 [메뉴 4개 통과](tap-menu-recheck-results.xml). 이후 장식 이미지가 탭을 가로채는 것을 발견해 Raycast Target을 수정하고, [실제 중앙/설정 탭 Raycast 포함 4개 통과](tap-raycast-results.xml)했다. 다른 메뉴·씬 흐름·튜토리얼 7개는 [첫 실행 결과](tap-tutorial-results.xml)에서 통과했다.
- 검증 프로젝트에 사용자 로고/프레임 원본 PNG가 누락된 상태의 첫 APK는 최종 산출물로 채택하지 않았다. 원본 이미지와 `.meta` 5개를 동기화하고 [타이틀·첫 탭 렌더링 2개를 재검증](tap-art-results.xml)했다. Android 실기기 입력은 아직 확인하지 않았다.
- 이미지가 포함된 당시 APK를 다시 빌드해 서명 v2, ARM64, 최소 API 25·대상 API 36, VIBRATE 권한을 확인했다. 이후 코인·아이템을 포함한 APK 검증은 위 Android 섹션에 기록했다. 설치/기기 실행은 사용자 범위에 따라 수행하지 않았다.

## 코인·6종 파워업·업그레이드 (2026-09-28)

- 코인과 6종 픽업을 패턴 A–E에 수동 배치했다. `GameManager`의 `CoinWallet`, `PowerUpManager`, `UpgradeMenu`와 Inspector에서 편집 가능한 `PowerUpBalance.asset`이 런 중 효과·정산·영구 레벨을 관리한다. 하이어라키의 `Canvas/TitlePanel/CoinBalanceText`·`UpgradeButton`과 `Canvas/UpgradePanel`에서 화면을 직접 편집할 수 있다. [편집/배치 안내](../COIN_POWERUP_SETUP.md).
- [신규 PC 테스트 5개](coin-powerup-results.xml) 통과: 프리팹·세 씬의 UI/버튼 참조, 코인 정산·비용/레벨 저장·MAX, 타이머 정지/확장, 점수 x2, 질주 적 처치·장애물 무시, 콤보 유지, 자석 수집, 업그레이드 화면 왕복. [480×800 업그레이드 화면](upgrade-menu.png)을 렌더링했다.
- [전체 회귀 48개](coin-powerup-regression-results.xml) 통과: 기존 코어 조작·장애물 윗면·Stomp/Slam·패턴·튜토리얼/메뉴 테스트를 포함한다. 최종 변경인 Lv.7 `MAX` 버튼 표기와 Editor 전용 코인 테스트 메뉴는 신규 5개 재실행으로 확인했다.
- 코인/아이템의 실제 플레이 배치 난이도와 Android 터치·성능·저장 복원은 실기기에서 추가 확인이 필요하다.

## 무기 스킨·선물 상자·소비 아이템·부활 (2026-09-29)

- [편집/구현 안내](../GAME_EXTRAS_SETUP.md)에 세 씬의 하이어라키, 보상표/스킨 데이터 자산, 저장 키와 Inspector 참조를 정리했다. `GameExtrasCatalog.asset`의 선물 상자 가중치 합계는 100이며 초기 분포는 25/10/20/8/15/15/7이다.
- [최종 코드의 신규 PC Acceptance 6개](game-extras-results.xml) 통과: 스킨 구매·장착/저장, 보상표와 중복 지급 방지, 선물 상자 수집의 타이머·점수 독립, 더블 탭과 Swipe 분리, Shield의 동일 적/장애물 연속 접촉 방어와 타이머 0, Dash 치명타, 사망·부활·결과·개봉 전환. 최신 충돌 변경과 직접 관련된 [장애물 회귀 1개](game-extras-obstacle-regression-results.xml), [적 전면 충돌 2개](game-extras-core-extra-results.xml)도 통과했다. 보완 직전 코드의 [전체 Unity 회귀](game-extras-regression-results.xml)는 55개 통과/실패 0개/명시적 두 프로세스 검사 1개 건너뜀이다.
- 최종 Android 개발 APK는 `Builds/Android/Deadline4Sec-Development.apk`(214,511,887 bytes). Unity 6000.3.7f1 BuildReport `Succeeded`, 오류 0개, 경고 3개. [빌드 JSON](android-build.json)·[APK SHA-256](android-apk-sha256.txt)·[빌드 스냅샷 SHA-256](android-source-sha256.txt)에 기록했다. APK 서명 v2, ARM64, 최소 API 25·대상 API 36, VIBRATE 권한을 검사했다. 원본과 검증 복제본의 기능 관련 자산 351개 및 `ProjectSettings.asset` 해시는 일치한다. 작업 중 별도로 추가된 `Assets/Art/2D/Generated`의 미연결 이미지 일부는 검증 스냅샷에 포함하지 않았다.
- Android 기기 설치·실행·터치 입력·진동·저장 복원·성능 검사는 사용자 지정 범위에 따라 수행하지 않았다.
