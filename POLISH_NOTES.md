# Deadline:4초 — 상용화 폴리시 작업 노트 (2026-10-01)

게임 규칙·판정 코드는 바꾸지 않고 외형·연출·사운드만 교체했다 (Spec §65–67). 모든 적용 단계는 에디터 메뉴로 다시 실행할 수 있다.

## 에디터 메뉴 (`Deadline 4 Sec`)

| 메뉴 | 하는 일 |
| --- | --- |
| Art/1–3, Run All Reaper Setup Steps | Mixamo 애니메이션 Humanoid 변환 → `Reaper.controller` 생성 → 세 씬의 Player에 Reaper 모델 장착 |
| Art/Apply Gothic Environment To Gameplay Scenes | 철로 바닥 셰이더, 밤하늘, 안개, 조명, 후처리, 배경 실루엣(`Environment`) |
| Art/Apply Gothic UI Skin To Scenes | 고딕 버튼·패널·타이머 프레임 스킨, HUD 배치 |
| Art/Polish Pickup Visuals | 발광 코인·선물상자, 아이콘이 보이는 파워업 구슬, 떠다니는 움직임. 크기(2026-10-02 확대): 아이콘 1.55m, 코인 지름 1m, 상자 1.6배, 획득 반경도 함께 키움. 다시 실행해도 크기가 누적되지 않음. 2026-10-11: 파워업은 2D 아이콘만 보이고 3D 구슬(`Visual`) 렌더러는 꺼짐 — 파워업만 다시 적용하려면 `Art/Apply Power-Up Pickup Icons` |
| Art/Convert Tripo GLBs In TripoRaw | `/TripoRaw/*.glb`를 1.2만 폴리곤 + 버텍스 컬러 프리팹으로 변환 (Reconvert = 덮어쓰기) |
| Art/Apply Enemy Visuals | 적 프리팹에 `EnemyVisual` 연결 (Tripo 프리팹이 있으면 사용, 없으면 절차적 외형) |
| Art/Apply Obstacle Visuals | 장애물 프리팹에 `ObstacleVisual` 연결 (모델을 판정 박스에 맞춰 늘림, 없으면 고딕 석재) |
| UI/Apply Title, Upgrade And Skin Layout | 타이틀(정면 클로즈업, 왼쪽 위 설정, 하단 SKIN/UPGRADE, 코인 숨김), 업그레이드(아이템·소비템만), 스킨 화면(왼쪽 캐릭터, 오른쪽 낫 카드) 배치. 아이콘은 `UI/AI/ai_*.png`가 있으면 우선 사용 |
| Art/Draw Menu Icons / Cut White Background From AI Icons | 메뉴 아이콘 SDF 생성 / OpenRouter 아이콘의 흰 배경 제거 |
| Course/Build Extended Pattern Library | 신규 패턴 12종 + 세트피스(시계탑 게이트, 고딕 아치, 화물칸 1·2량)를 생성하고 세 씬의 PatternSpawner에 등록. 다시 실행하면 같은 이름의 패턴을 덮어씀 |
| UI/Apply Mode Select | (2026-10-11 개편) 타이틀의 예전 `< DEADLINE >` 선택기를 지우고 전체화면 팝업 `TitlePanel/ModeSelectPopup`(비활성) 생성: 왼쪽 클래식·오른쪽 데드라인 아트(`Art/2D/ModeSelect_*.png`) + 데코 라인, 하단 패널(모드 이름 흰색 / 태그라인 핑크 / 설명) + SELECT 버튼. On Click: `ModeSelect_Btn` → `ModeSelectUI.Open`, 각 아트 → `ChooseClassic/ChooseDeadline`, SELECT → `Confirm`(모드 적용 후 타이틀로). 선택 안 된 쪽은 `UIGrayscale` 셰이더로 흑백+어둡게. 데드라인 아트의 터치 영역은 `ModeSplitHitArea`(대각선 경계 topX/bottomX)로 잘라 왼쪽 터치는 클래식으로 감. 문구는 ModeSelectUI 인스펙터에서 편집. 세 씬 적용(`ModeSelect_Btn`은 EndlessRun에만 있음) |
| Course/Build Train Patterns | 서브웨이 서퍼 스타일 전철: `Train`(멈춰있는 전철, 2.2×2.8×14m, 정면·측면 충돌 즉사, 지붕 착지 안전, 점프로 못 올라감) / `Train Ramp`(앞에 경사로 — 경사로는 Obstacle 밖 형제 오브젝트라 닿아도 안전, 지붕보다 0.15m 높게 끝나 지붕으로 내려섬) + 패턴 R(Easy 전철 사이 통로)·S(Medium 가운데 경사로 전철 + 양옆 전철)·T(Hard 전철 피하고 다른 레인 경사로 전철 오르기) 생성·세 씬 등록. 활성 씬이 저장된 상태에서 실행 |
| UI/Apply Result Screen Layout | 결과창 정보 계층(큰 SCORE → 거리/콤보/시간 3열 → NEW BEST → PERSONAL BEST → RUN REWARD → MAIN MENU \| RETRY 가로 버튼) + 뒤 어두운 오버레이 + `ResultPresenter` 연결. 기존 오브젝트 이름·참조·On Click 유지, ConfirmButton은 비활성(2026-10-09 OPEN REWARDS 제거 요청 — 상자는 두 버튼 모두 기존 상자 오픈 흐름을 먼저 거침). 세 씬 적용 |
| UI/Build Loading Scene | `Assets/Scenes/Loading.unity` 생성(키아트 + LOADING... + bg/fill/frame 게이지 + 랜덤 팁) 후 Build Settings 0번에 배치. 다시 실행해도 인스펙터에 적은 팁 목록은 유지 |
| UI/Apply Number Font To HUD | CallifontSharpie-Bold TMP 에셋 생성(`Resources/Fonts/CallifontSharpie-Bold SDF`) + HUD 숫자 라벨(타이머·점수·거리·코인·선물상자 수)에 `NumericLabel` 부착·크기 보정 |
| UI/Apply Gift Chest Opening | 선물상자 개봉 화면: 회전 선버스트 배경 + 3D 상자 뷰 + 보상 아이콘. 기존 텍스트/버튼 이름·연결은 유지, 배치만 화면 비율 기준으로 변경 |
| Audio/Assign Imported Sounds To Library | Casual Game Sounds / Demonic Music을 `SoundLibrary`에 배정, 모바일 임포트 설정 |
| Art/Apply Soul Pickup Model | (2026-10-11) Coin 프리팹의 외형을 `3DModel/Props/Soul.fbx`로 교체: 기존 원판 `Visual` 렌더러는 끄고 `FloatPivot/SoulModel`(높이 1.15m, 트리거 중앙 정렬, URP Unlit `Materials/PowerUps/Soul.mat`) 추가, 후광은 하늘색. 프리팹 이름·트리거·`CoinPickup`·패턴 배치·저장 키는 그대로. 화면 표기는 Coin → Soul(SOUL MAGNET, `SOUL +300`, 업그레이드 잔액, 스킨 가격, "Soul이 부족합니다!") |
| Audio/Assign Soul Pickup Sound | Soul 획득음을 `Sound/SFX/driken5482-retro-coin-4-236671`로 지정(SoundLibrary `coin` 슬롯). 0.7초 안에 연속으로 먹으면 한 번마다 반음씩 올라가 최대 12단계(1옥타브)에서 유지, 끊기면 원래 음높이로 — `CameraFeedbackController`의 전용 보이스 3개라 다른 효과음 피치는 안 바뀜 |
| Audio/Assign BGM | (2026-10-11) 타이틀 음악 = `Sound/BGM/BGM_MainScreen`, 인게임 음악 = `Sound/BGM/BGM_InGame` (SoundLibrary `titleMusic`/`runMusic`, 스트리밍). 전환·페이드·게임오버 덕킹은 기존 `MusicDirector` 그대로 |
| UI/Apply Leaderboard | (2026-10-14) `Resources/GooglePlay/GooglePlaySettings.asset`(없을 때만 생성 — 리더보드 ID 2개 입력) + `TitlePanel/LeaderboardPanel`(비활성: 어두운 배경·BackButton → `Close`, DEADLINE MODE / CLASSIC MODE 버튼 → `OpenDeadline/OpenClassic`, 모드별 로컬 최고점, 상태 문구). 타이틀 `lankingButton` On Click → `LeaderboardPanel.Open`. 세 씬 적용 |
| UI/Apply Achievements | (2026-10-14) 업적 시스템 씬/에셋 구성: `Resources/Achievements/AchievementCatalog.asset`(없을 때만 기본 28개로 생성, 이후 인스펙터 수정 유지), `Canvas/AchievementPopup`(상단 배너), `TitlePanel/AchievementPanel`(목록, 비활성). `achievementButton` On Click → `AchievementListUI.Open`, BackButton → `Close`. 세 씬 적용 |
| Debug/Log Achievement Progress · Reset Achievement Save · Reset Player Stats | 개발용(게임 UI에는 없음). 진행도 전체 출력 / 해제 기록 삭제 / 통계 삭제 |

## 새 런타임 컴포넌트

- `PlayerCharacterAnimator` — PlayerController 상태/이벤트로 애니메이터를 즉시 크로스페이드. 이동·판정에 관여하지 않음.
- `EnvironmentScroller` — 절차적 고딕 배경(첨탑·기둥·사슬·배너·시계탑·구름바다)을 카메라 앞에서 재활용. 콜라이더 없음.
- `CombatParticles` — 스파크·영혼·먼지·베기 궤적·낫 트레일·속도선. 언스케일드 시간으로 히트스톱 중에도 재생. `CameraFeedbackController`가 자동 추가.
- `HudJuice` — 점수 팝업, 콤보 배율 색/펀치, 타이머 리셋 팝·위급 맥동. RunManager에 자동 부착.
- `SoundLibrary` (`Resources/Audio/SoundLibrary.asset`) — 빈 칸은 기존 절차음으로 대체. `MusicDirector`가 타이틀/런 BGM 크로스페이드.
- `EnemyVisual`, `ObstacleVisual`, `PickupFloat`, `WeaponSkinVisual`(텍스처 유지 틴트 방식).
- `TitleShowcaseCamera` — Title/Settings/Upgrade/Skin 상태에서 Reaper 정면 클로즈업. 런 시작 시 기존 Ready? 카메라 연출로 넘어감.
- `EnemyDeathFx` — 처치된 적의 외형을 떼어 번쩍임과 함께 날려 보냄(레인 공격은 옆, 호밍은 앞, 슬램은 바깥, 스톰프는 납작).
- GameFlowManager에 `Skins` 상태와 `OpenSkins`/`CloseSkins` 추가. 시작 연출(Ready) 중 애니메이션은 Run, 호밍은 FlyKick.
- `GiftChestPresenter` — GiftOpeningPanel에 부착. y=-500의 전용 무대(레이어 31)에 절차적 보물상자를 만들어 전용 카메라→RenderTexture→`ChestView`로 표시. 닫힘: 흔들림·점프, 탭: 0.4초 떨림 → 뚜껑 열림 + 별 파티클 + 선버스트 섬광 → 보상 아이콘이 솟아오름. 보상 지급은 기존 RunInventory 그대로이며 보상 문자열로 아이콘만 고른다(COIN/SHIELD/DASH/REVIVE/스킨 재화).
- `TitleOutro` (2026-10-04) — 시작 순간 타이틀 버튼은 즉시 사라지고 Titlelogo·TitleFrame은 위로, TitleFrame (1)은 아래로 InBack 곡선으로 날아감. 저장된 TitlePanel은 기존처럼 바로 비활성화하고 복사본만 움직인 뒤 삭제(테스트·Inspector 배치 보존). Retry 재시작 땐 생략. DOTween이 임포트되면 `Editor/DOTweenDefine`이 `DEADLINE_DOTWEEN` 심볼을 자동 추가해 DOTween으로 재생, 없으면 같은 곡선을 자체 재생. (2026-10-04 DOTween 임포트 완료 → 현재 DOTween 경로 사용. 검증용 복사본은 심볼이 저장된 ProjectSettings로 동기화해야 테스트 도중 재컴파일이 안 일어남.)
- `TitleShowcaseCamera` 핸드오프 — 타이틀 정면 클로즈업에서 Ready? 측면 카메라로 1.15초 동안 캐릭터 주위를 궤도로 돌며 넘어감(FOV 42→게임 FOV). 시작 프레임 히치에 건너뛰지 않도록 인트로와 같은 0.05초 클램프 시간 사용.
- `ComboBadge` (2026-10-04) — 오른쪽 아래 붓 터치 위에 등급 단어 + 손글씨 콤보 수. 1–9 Cool!(파랑) / 10–24 Great!(초록) / 25–49 Fantastic!(주황→빨강) / 50+ Unstoppable!(무지개 애니메이션). 크기 0.85배(2026-10-11, 이전 1.25배 — 캐릭터를 가리지 않게). 기존 `ComboText`는 숨김(RunManager는 그대로 갱신). 붓 이미지는 `Resources/HUD/combo_brush` 스프라이트가 있으면 사용, 없으면 절차 생성.
- 폰트 규칙 변경(2026-10-04, 2026-10-11 갱신): `NumericLabel`이 붙은 인게임 숫자 라벨만 HiKR-ExtraBold + 기울임(이탤릭, 이전엔 CallifontSharpie-Bold), 나머지는 GFCRedSpirit-Bold. `GameFont.Apply`가 마커를 보고 고르며 폰트 테스트도 같은 규칙으로 갱신.
- `PowerUpFx` (2026-10-05) — 아이템 6종의 획득/지속/종료 연출. PowerUpManager 상태만 읽고 규칙은 건드리지 않음(PowerUpManager 오브젝트에 자동 부착).
  - 빙결 시계: `쩡` + 푸른 플래시 + 타이머에서 얼음 파편 / 타이머 얼음막·결정·하늘색 숫자 + 화면 가장자리 성에 / 남은 0.7초부터 금이 가고 종료 시 `파삭` 파편.
  - 영혼 증폭기: 위로 퍼지는 금색 링 3개 + `x2 SCORE!` / Score·Combo 뒤 금빛 오라, 점수 팝업 1.6배 금색, 처치·니어미스 사운드 피치 +0.18 / 오라 빠른 수축.
  - 사신 폭주: FOV 펀치 + 에너지 폭발 링 / 진한 속도선, 잔상(베이크 메시), 몸·낫 트레일, FOV +3(속도 FOV가 이미 최대), 저음 드론 루프, 처치 시 대형 X자 슬래시 / 속도선·FOV 서서히 복귀.
  - 영혼 자석: 자기장 펄스 링 / 발밑 청록 링 + 안쪽으로 빨려드는 입자, 코인 비주얼이 곡선으로 휘며 날아옴(콜라이더 경로·획득 타이밍은 그대로) + Zip / 링 수축.
  - 콤보 봉인석: 콤보 배지에 사슬이 감기고 자물쇠 + `COMBO LOCK` / 콤보 타임아웃 게이지 보라색으로 정지 / 사슬이 끊어져 흩어짐.
  - 시간 심장: 심장 박동 2연타로 타이머 크게 펄스 / 타이머 프레임 붉은 발광·가장자리 맥동, 4초 초과 구간 숫자 분홍 / 마지막 큰 박동.
  - 사운드는 `SfxSynth` 합성 임시음. `SoundLibrary`의 Power-ups 슬롯에 실제 클립을 넣으면 그쪽 사용. 스프라이트는 `FxSprites` 절차 생성, `Resources/HUD/fx_*` 이미지가 있으면 대체.
- `MomentFx` (2026-10-05) — 순간 연출 3종. GameFlowManager 오브젝트에 자동 부착, 상태만 읽음.
  - Near Miss: 장애물 쪽 화면 가장자리 청록 Edge Flash + 짧은 속도선, `NEAR MISS!`(RunManager 라벨, 텍스트를 `NEAR MISS!`로 변경 — 점수는 HudJuice 팝업)를 장애물 쪽으로 이동·팝, 타이머 4.00 튕김, 기존 swish + 짧은 성공음. 카메라는 장애물 반대쪽으로 아주 약한 좌우 Impulse(CameraFeedbackController, 0.07m/0.18초). 장애물 위치는 `Obstacle`이 `PlayNearMiss(player, hazard)`로 전달.
  - Timer 0초 직전: 남은 1.5초부터 화면 가장자리 어두운 붉은 Pulse/Vignette, 타이머 확대 + 점점 빨라지는 맥동, 1초 이하 붉은 발광, 0.42→0.11초 간격 Tick + 격박 Heartbeat. 빙결 중엔 꺼짐. (HudJuice의 기존 1초 이하 맥동을 대체)
  - 충돌 사망: 흰 Impact Flash 0.2초, 강한 1회 Shake, 0.1초 정지 → 0.2배 슬로우 → 0.6초에 정상 속도, 채도·노출 다운(런타임 Volume, 부활/재시작 시 복귀), 강한 Hit + 저음 Impact. 시간 초과 사망은 붉은 플래시·약한 흔들림의 가벼운 버전. 구분은 `GameTimer.EndedByContact`(읽기 전용).
  - 사망 시 HUD는 `deathHudHold`(0.35초) 동안 멈춘 채 남았다가 Death/Revive로 전환(GameFlowManager).
- 인게임 HUD 배치(2026-10-05, 메뉴 `UI/Apply In-Game HUD Layout`) — 오른쪽 상단에 점수·코인·선물상자 순서의 라운드 알약(`ui_hud_pill.png`, 9-slice) + 작은 코인/상자 아이콘(`icon_gift_small.png` 절차 생성). 라벨은 숫자만 표시(RunManager/UpgradeMenu/GameExtrasUI 문자열 변경), 거리·소모아이템·활성 아이템 목록은 숨김. 타이머 숫자는 프레임 중앙에 오도록 -24(2026-10-11 HiKR 폰트 기준, 이전 -33). 기존 라벨 이름·경로·참조는 유지하고 알약/아이콘은 형제 오브젝트로 배치.
  - 다시 실행해도 그리기 순서가 알약 → 아이콘 → 라벨로 유지됨(2026-10-05 수정: 재실행 시 알약이 글자를 덮던 문제).
  - 선물상자를 모두 열면 'ALL BOXES OPENED' 확인 창 없이 바로 다음 화면으로 넘어감(GameFlowManager.OpenOrContinueGiftBox).
  - `HudStats`: 선물상자 0개면 알약 숨김 → 첫 상자 획득 시 오른쪽에서 슬라이드 인. 획득 위치에서 상자 아이콘이 곡선으로 날아가 도착할 때 카운트 +1(펀치 + 짧은 효과음). 코인 아이콘 펀치. 상자 획득 이벤트는 `GiftBoxPickup.Collected`.
- `LoadingScreen` (2026-10-09) — 첫 실행 로딩. 게이지는 실제 작업에 연동: 0–15% 합성 사운드·이펙트 스프라이트·폰트 미리 생성, 15–75% EndlessRun 비동기 로드(additive), 75–95% 활성화(Awake), 95–100% 첫 프레임(Start) 후 0.25초 페이드아웃하고 로딩 씬 언로드. 게이지는 실제 진행도 이하로만 부드럽게 따라감(`barSpeed`). 팁은 `LoadingCanvas`의 LoadingScreen › Tips에서 편집, 실행마다 랜덤. 로딩 카메라는 MainCamera 태그 없음(게임 씬 스크립트가 자기 카메라를 찾도록).
- `ResultPresenter` (2026-10-09, gameover.md) — 결과창 연출 전용. 오버레이(검정 0.6) + 프레임 팝(0.85→1.03→1.00, `resultAppearDuration` 0.23s / `resultOvershootScale`) → GAME OVER → 점수 카운트업(`scoreCountDuration` 0.65s, ease-out, 틱 사운드) → 3열 기록 → NEW BEST(카운트 끝난 뒤, 기록 갱신 시에만 팝+사운드) → 개인 최고 → 보상 → 버튼 순. 약 1.4초에 버튼 활성(그 전엔 터치 막힘), 화면 탭 시 즉시 최종 상태. 버튼은 상자 유무와 무관하게 MAIN MENU | RETRY. 화면이 짧으면 프레임 자동 축소. 사운드 훅 4개(open/scoreTick/newBest/button) — 비우면 임시 합성음/무음.
  - GameFlowManager는 값만 채우고(라벨 문구 제거) `Prepare/Play` 호출. 기록 저장·보상·흐름 로직은 그대로.
  - 사망 원인: `GameTimer.LastDeathReason`(Collision / TimerExpired). 충돌 = 흰분홍 플래시 0.1s + 강한 흔들림 + 0.08s 히트스톱 → 슬로우. 타이머 0 = 플래시·흔들림 없음, 0.00 타이머 두 번 펀치 + 붉은 발광, 세상이 0.25배까지 느려졌다 복귀(전원 꺼지는 느낌). 둘 다 채도·밝기 다운(MomentFx 런타임 Volume).
- Classic Mode (2026-10-10, ClassicMode.md) — 같은 씬·같은 콘텐츠, 4초 Survival Timer만 끔.
  - `GameModeState`(GameMode.cs): `CurrentGameMode`(메뉴 선택, 기본 Deadline, 세션 동안 유지·앱 실행/Play마다 Deadline으로 초기화) / `CurrentRunMode`(StartGame에서 고정, 튜토리얼은 항상 Deadline). `IsDeadlineMode/IsClassicMode/IsClassicRun`.
  - 분기 위치: `GameTimer.SetUntimed`(GameFlowManager.BeginFlow에서 호출) 한 곳이 카운트다운·리셋(처치/Near Miss)·경고·Time Heart·타임아웃·타이머 UI를 모두 끔. `PatternSpawner.ThinOutPickups`가 스폰 시 Freeze Clock/Time Heart 제거(난수는 그대로 소비), `PowerUpManager.Activate` 안전장치.
  - 기록 키: Deadline = 기존 `Deadline4Sec.BestScore/BestDistance/BestCombo`(기존 저장·PC 영속성 프로브 그대로, 마이그레이션 불필요), Classic = 같은 이름 + `_Classic`. `RecordKeys`.
  - Result: "DEADLINE SCORE / DEADLINE BEST" 또는 "CLASSIC ..." 캡션, NEW BEST는 해당 모드 기록끼리만 비교.
  - 향후 연결점: `GameFlowManager.RunFinished`(static event, `RunRecord` — Mode/Score/Distance/BestCombo/RunTime/NewBest) → 리더보드 제출·업적 판정은 여기서 Mode로 분기.
  - 테스트: `ClassicModeAcceptanceTests` 2개(타이머 없음+점수·사망 유지+타이머 아이템 제외 / 모드별 기록 분리).
- `ComboBadge` 게이지 — 콤보 타임아웃 남은 비율(`RunManager.ComboTimeLeft01`, 읽기 전용 추가).
- `Dev/DevAutoPilot` — 에디터 전용 자동 플레이(시각 QA용). 빌드에 포함되지 않음.

## 업적 / 플레이어 통계 (2026-10-14, Achievement.md 1단계)

흐름: 게임플레이 → `PlayerStats`(기록만) → `AchievementManager`(판정·해제·저장·팝업 큐) → `AchievementPopup`. Google Play Games는 아직 연결하지 않음.

- **스크립트(신규, `Assets/Script/Achievements/`)**: `PlayerStats`(정적, Lifetime + `RunStats`), `PlayerStatsRecorder`(기존 이벤트 → PlayerStats, GameFlowManager 오브젝트에 런타임 부착), `AchievementDefinition`(데이터 + enum), `AchievementCatalog`(ScriptableObject, 기본 목록), `AchievementManager`(정적), `AchievementPopup`, `AchievementListUI`, `AchievementRow`. 에디터: `AchievementSetup`. 테스트: `AchievementAcceptanceTests`.
- **수정(한두 줄 훅만)**: `GameFlowManager`(런 시작 `BeginRun`, 사망, 쿠폰 부활), `PlayerController`(Ground Slam 처치 1마리마다), `RunInventory`(스킨 구매, 실드 사용/방어, 대시 사용, 선물상자 개봉 — 새로 뽑을 때만, 스킨 소유 판정 static 공유), `PowerUpManager`(업그레이드 성공, 레벨 static 공유), `PowerUpPickup`(파워업 획득).
- **기존 이벤트 재사용**: `PlayerController.EnemyKilled`(Enemy.TryKill 뒤라 1킬 1회), `SlamResolved`, `RunManager.NearMissRecorded`(장애물당 1회), `GameTimer.TimerReset`(리셋 직전 남은 시간 전달 → `LowestTimerBeforeReset`), `CoinWallet` 런 획득량 증가분, `GiftBoxPickup.Collected`, `GameFlowManager.RunFinished`(런 종료).
- **Run vs Lifetime**: 게임플레이 통계는 `BeginRun`~`EndRun` 사이에서만 집계(튜토리얼·타이틀 제외). 메타(업그레이드·스킨·선물상자 개봉)는 항상 집계. RunStats는 런 시작 시 0.
- **Global / DeadlineOnly**: DeadlineOnly Counter/OneShot은 Deadline 전용 통계(`TotalDeadlineNearMisses`, `TotalDeadlineTimerResets`)만 읽고 Classic에서는 이 값이 오르지 않음(PlayerStats). DeadlineOnly RunCondition은 Deadline 런 중에만 검사(`AchievementManager.CanProgress`).
- **Air Combo**: Homing Dash로 공중 적을 처치할 때마다 `Run.AirChain`+1, 플레이어가 땅·지붕에 닿으면(Recorder가 `IsGrounded` 확인) 0. 최고값 `BestAirChain`.
- **Ground Slam**: 처치 1마리마다 `TotalEnemyKills`/`TotalGroundSlamKills`+1(지상/공중 구분), 착지 1회마다 `TotalGroundSlams`+1, 한 번에 잡은 최대 수 `Run.BestSlamKills`.
- **진행도 소스**: Soul 업적은 누적 획득량(`TotalCoinsCollected`, 소비해도 안 줄어듦). 스킨·업그레이드 업적은 실제 저장 상태(소유 스킨 수, 아이템 레벨)를 읽어 기존 세이브도 인정. 해제 여부만 따로 저장.
- **저장 키**: `Stats_<PlayerStat 이름>`(25개) + `Stats_LowestTimerBeforeReset`, `AchievementUnlocked_<ID>`. 통계는 메모리에 쌓고 런 종료·업그레이드·스킨 구매·상자 개봉·업적 해제·앱 일시정지/종료 때 저장.
- **팝업**: `Canvas/AchievementPopup`(CanvasGroup, 터치 안 받음) → `Body`(Edge, Icon, Header "ACHIEVEMENT UNLOCKED", NameText, DescriptionText). 타이머·점수 알약 아래, 2.6초(카탈로그 `popupSeconds`). 큐는 `AchievementManager`에 있어 동시에 여러 개 해제돼도 하나씩, 씬 재시작에도 유지.
- **목록**: `TitlePanel/AchievementPanel` — Backdrop, Frame, ListScrollView/Viewport/Content/RowTemplate(StateIcon, NameText, TagText "DEADLINE", DescriptionText, ProgressBar/Fill, ProgressText), HeadingText, CountIcon, CountText, BackButton. 숨김 업적은 해제 전 "???", Counter만 진행 바.
- **디버그**: 카탈로그 `showAchievementDebugLogs` 켜면 통계 변화 / `Achievement Progress: ID n / m` / `Achievement Unlocked: ID` 로그. 메뉴 `Deadline 4 Sec/Debug/...`로 진행도 출력·초기화.
- **Google Play 연결 위치**: `AchievementManager.UnlockAchievement` → `ReportToPlatform`(지금은 비어 있음), ID는 정의의 `googlePlayId`.
- **Daily Quest 재사용**: `PlayerStats.Recorded(stat, amount, mode)` 이벤트와 `PlayerStats.Run` 값.
- 이름 조정: `ACH_COIN_10000` "코인 사냥꾼" → "소울 사냥꾼", 설명은 Soul 표기(10-11 명칭 변경 반영). 숨김 업적 `ACH_NO_POWERUP_60` 포함.

## 리더보드 / Google Play Games (2026-10-14, leaderboard.md)

- **구조**: `GooglePlayService`(정적, 유일한 Google Play 창구: `Authenticate`, `IsAuthenticated`, `SubmitScore(mode, score)`, `ShowLeaderboard(mode)`, `ReportAchievement(id)`, 모드→ID 매핑 `GetLeaderboardId`). 업적의 `AchievementManager.ReportToPlatform`도 여기로 연결해 인증을 공유.
- **플러그인**: Google Play Games Plugin 2.3.0 + EDM4U 1.2.182 설치(2026-10-14). Unity 6의 Import Package 창이 이 패키지에서 NullReferenceException(`PackageImportTreeView`, 상위 폴더 항목 누락)으로 멈춰서, 패키지 파일을 .meta(GUID 그대로)와 함께 직접 복사해 설치함(내장된 구버전 `current-build/…2.2.0.unitypackage`만 제외). `GooglePlayGamesDefine`(에디터)이 `Google.Play.Games` 어셈블리가 있으면 Android에 `DEADLINE_GPGS`를 자동 추가 → 실제 API(`PlayGamesPlatform`)는 Android 빌드에서만 컴파일, 에디터는 모의(Mock, 로그만), Windows 빌드는 플러그인 없이 그대로.
- **ID**: `GooglePlaySettings` 에셋의 `deadlineLeaderboardId`, `classicLeaderboardId`(Play Console 값 또는 GPGSIds 상수 복사). 디버그: `showLeaderboardDebugLogs`, `useMockLeaderboardInEditor`, `simulateSignedOutInEditor`(로그인 실패 흉내).
- **제출 시점**: `LeaderboardRunSubmitter`(GameFlowManager 오브젝트에 런타임 부착)가 `GameFlowManager.RunFinished`에서 제출 — 부활 제안을 거절/소진한 뒤 로컬 최고 기록 저장 직후 1회만 발생하므로 첫 사망·부활 중간 점수는 안 나감. `hasSubmittedLeaderboardScore`는 다음 런이 진행 중(Ready/Playing/Dying/RevivePrompt)일 때만 해제 → 런당 최대 1회, Retry 시 이전 점수 재제출 없음. 모드는 RunRecord(=CurrentRunMode).
- **정책**: Google Play 리더보드는 플레이어별 최고점만 유지하므로 매 런 최종 점수를 그대로 제출(로컬 최고와 비교 안 함). 0점 이하는 제출 안 함. 로그인 안 됨/ID 없음/실패는 로그만 남기고 게임 진행에 영향 없음. 로컬 Best 저장과는 서로 독립.

## 코스 (2026-10-01 확장)

- 패턴 17종(기존 A–E + F–Q). 난이도: Easy F·G·H·I / Medium J·K·L·M / Hard N·O·P·Q.
- 화물칸(`Train Wagon`, `Train Wagon Double`)은 폭 8.6m·높이 1m의 Jump 장애물이다. 앞면은 충돌 사망, 지붕은 착지·달리기 안전. 루트가 앞면에 있어 거리 판정이 앞면 기준.
- 세트피스(시계탑 게이트, 고딕 아치)는 콜라이더 없는 장식이다.
- 패턴 안 아이템/상자는 후보일 뿐이다: `PatternSpawner`의 `powerUpChance`(0.3)·`giftBoxChance`(0.18)·`maxPowerUpsPerPattern`(1)로 생성 시 남길지 결정한다. 코인은 항상 나온다. 별도 시드 스트림이라 고정 시드의 코스 순서는 그대로다.
- 전철(2026-10-10): `Obstacle.ObstacleType.RampTrain` 추가(경사로 전철 라벨). 전철의 Near Miss 좌우 여유는 0.35m — 옆 레인을 달리는 것만으로는 Near Miss가 아니고, 정면 직전에 레인을 바꿔 스칠 때만 인정. 리플레이 봇 규칙상 단독 전철은 왼쪽(-1) 레인 또는 좌우 쌍으로, 지붕 위엔 공중 적만, 지붕에서 떨어진 뒤 지상 적은 착지 지점(최고 속도 ~18m) 이후에 배치. Reaper Rush는 지붕도 통과하므로 지붕 위에 두지 않음.
- 기존 코스 특성 메모: Air로 끝나는 패턴(C·D·J·L·M·O·P) 뒤엔 Air/Any 입장 패턴만 올 수 있어, 한 번 공중 패턴 흐름에 들어가면 지상 패턴(전철 포함)으로 돌아오지 않음. 지상 복귀용 Air→Ground 패턴이 있으면 해결됨(미적용).
- 배치 규칙(리플레이 봇 기준): 연속 점프 금지(점프 체공 ~8m), 지상 적은 인접 레인으로 ≥5m 간격, 높은 공중 콤보 직후에는 Ground 입장 패턴만.

## Tripo 사용 기록

- MCP Tripo 연동은 `face_limit`를 보내지 않아 모델이 약 140만 폴리곤 GLB(.fbx 확장자)로 온다. 원본은 `/TripoRaw`(git 제외)에 두고 변환기로 처리한다.
- 아틀라스 UV가 잘게 쪼개져 있어 텍스처를 유지한 채 줄이면 번진다. 그래서 베이스 컬러를 버텍스 컬러로 구운 뒤 줄이고 `Deadline4Sec/VertexColorLit` 셰이더를 쓴다.
- 생성 중 도메인 리로드(컴파일·Play 진입)가 일어나면 작업이 끊기고 크레딧만 차감된다. 생성 중에는 에디터를 건드리지 않는다.
- 사용량: 적 2종(중단 2회 포함 4회), 장애물 3종 — 회당 약 20 크레딧.

## 검증 메모

- 2026-10-01 패턴 17종 확장 후 전체 회귀 **53 통과 / 1 실패**(Explicit PcPersistence). 리플레이 4종(시드 12345 60FPS/30FPS, 시드 1, 시드 98765 180초) 모두 통과. 리플레이 조건은 'A–E 전부 등장'에서 '서로 다른 패턴 6개 이상'으로 변경. 기존 E는 배치를 유지하고 입장만 Ground로 좁힘.

- 2026-10-01 업그레이드 화면 개편(전체 화면 목록, 레벨 칸, 현재→다음 수치, 코인/업그레이드 아이콘, 하단 소비 아이템 상자, 성공/코인 부족 팝업) 후 메뉴 관련 4개 클래스 **19/19 통과**.

- 2026-10-01 메뉴 개편 후: 전체 54개 중 52 통과. `SceneFlow.TitleStartWaitsForGoBeforeRunning`은 타이틀 카메라를 인게임 기준으로 저장하던 가정이 정면 클로즈업과 충돌해 기준을 카메라 리그(`baseLocalPosition/Rotation`)로 바꿨고, 메뉴 관련 4개 클래스 재실행 **19/19 통과**. 남은 실패는 Explicit `PcPersistence`.

- 2026-10-01 최종 배치 회귀(EditMode, `Deadline4Sec.AcceptanceTests`): **53 통과 / 1 실패**. 실패는 Explicit 2-프로세스 검사 `PcPersistenceAcceptanceTests`가 커밋된 `Verification/pc-prefs-baseline.json`을 보고 "이전 검사를 먼저 복원하라"며 시작을 거부한 것으로, 게임 코드와 무관하다.
- 배치 테스트는 에디터와 같은 PlayerPrefs를 쓰므로, 실행 후 에디터의 보유 코인·최고 기록이 테스트 값으로 바뀔 수 있다. 테스트가 덮어쓴 `Verification/` 증거 파일은 git으로 되돌렸다.

- `RuntimeAcceptanceTests.SideLaneAttackKillsOneGroundEnemy`는 실시간 프레임 길이에 민감하다. 배치 실행에서 큰 프레임 끊김이 있으면 측면 공격 존이 레인 공격보다 먼저 판정돼 간헐적으로 실패한다. 같은 코드 재실행에서 26/26 통과.
- `PcPersistenceAcceptanceTests.ActualKillResultLeavesSavedRecordsForASecondProcess`는 Explicit 2-프로세스 검사라 전체 실행에서 제외해야 한다.
