# Deadline:4초 — 상용화 폴리시 작업 노트 (2026-10-01)

게임 규칙·판정 코드는 바꾸지 않고 외형·연출·사운드만 교체했다 (Spec §65–67). 모든 적용 단계는 에디터 메뉴로 다시 실행할 수 있다.

## 에디터 메뉴 (`Deadline 4 Sec`)

| 메뉴 | 하는 일 |
| --- | --- |
| Art/1–3, Run All Reaper Setup Steps | Mixamo 애니메이션 Humanoid 변환 → `Reaper.controller` 생성 → 세 씬의 Player에 Reaper 모델 장착 |
| Art/Apply Gothic Environment To Gameplay Scenes | 철로 바닥 셰이더, 밤하늘, 안개, 조명, 후처리, 배경 실루엣(`Environment`) |
| Art/Apply Gothic UI Skin To Scenes | 고딕 버튼·패널·타이머 프레임 스킨, HUD 배치 |
| Art/Polish Pickup Visuals | 발광 코인·선물상자, 아이콘이 보이는 파워업 구슬, 떠다니는 움직임. 크기(2026-10-02 확대): 아이콘 1.55m, 코인 지름 1m, 상자 1.6배, 획득 반경도 함께 키움. 다시 실행해도 크기가 누적되지 않음 |
| Art/Convert Tripo GLBs In TripoRaw | `/TripoRaw/*.glb`를 1.2만 폴리곤 + 버텍스 컬러 프리팹으로 변환 (Reconvert = 덮어쓰기) |
| Art/Apply Enemy Visuals | 적 프리팹에 `EnemyVisual` 연결 (Tripo 프리팹이 있으면 사용, 없으면 절차적 외형) |
| Art/Apply Obstacle Visuals | 장애물 프리팹에 `ObstacleVisual` 연결 (모델을 판정 박스에 맞춰 늘림, 없으면 고딕 석재) |
| UI/Apply Title, Upgrade And Skin Layout | 타이틀(정면 클로즈업, 왼쪽 위 설정, 하단 SKIN/UPGRADE, 코인 숨김), 업그레이드(아이템·소비템만), 스킨 화면(왼쪽 캐릭터, 오른쪽 낫 카드) 배치. 아이콘은 `UI/AI/ai_*.png`가 있으면 우선 사용 |
| Art/Draw Menu Icons / Cut White Background From AI Icons | 메뉴 아이콘 SDF 생성 / OpenRouter 아이콘의 흰 배경 제거 |
| Course/Build Extended Pattern Library | 신규 패턴 12종 + 세트피스(시계탑 게이트, 고딕 아치, 화물칸 1·2량)를 생성하고 세 씬의 PatternSpawner에 등록. 다시 실행하면 같은 이름의 패턴을 덮어씀 |
| UI/Apply Number Font To HUD | CallifontSharpie-Bold TMP 에셋 생성(`Resources/Fonts/CallifontSharpie-Bold SDF`) + HUD 숫자 라벨(타이머·점수·거리·코인·선물상자 수)에 `NumericLabel` 부착·크기 보정 |
| UI/Apply Gift Chest Opening | 선물상자 개봉 화면: 회전 선버스트 배경 + 3D 상자 뷰 + 보상 아이콘. 기존 텍스트/버튼 이름·연결은 유지, 배치만 화면 비율 기준으로 변경 |
| Audio/Assign Imported Sounds To Library | Casual Game Sounds / Demonic Music을 `SoundLibrary`에 배정, 모바일 임포트 설정 |

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
- `ComboBadge` (2026-10-04) — 오른쪽 아래 붓 터치 위에 등급 단어 + 손글씨 콤보 수. 1–9 Cool!(파랑) / 10–24 Great!(초록) / 25–49 Fantastic!(주황→빨강) / 50+ Unstoppable!(무지개 애니메이션). 기존 `ComboText`는 숨김(RunManager는 그대로 갱신). 붓 이미지는 `Resources/HUD/combo_brush` 스프라이트가 있으면 사용, 없으면 절차 생성.
- 폰트 규칙 변경(2026-10-04 사용자 요청): `NumericLabel`이 붙은 인게임 숫자 라벨만 CallifontSharpie-Bold, 나머지는 GFCRedSpirit-Bold. `GameFont.Apply`가 마커를 보고 고르며 폰트 테스트도 같은 규칙으로 갱신.
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
- 인게임 HUD 배치(2026-10-05, 메뉴 `UI/Apply In-Game HUD Layout`) — 오른쪽 상단에 점수·코인·선물상자 순서의 라운드 알약(`ui_hud_pill.png`, 9-slice) + 작은 코인/상자 아이콘(`icon_gift_small.png` 절차 생성). 라벨은 숫자만 표시(RunManager/UpgradeMenu/GameExtrasUI 문자열 변경), 거리·소모아이템·활성 아이템 목록은 숨김. 타이머 숫자는 프레임 중앙에 오도록 -33. 기존 라벨 이름·경로·참조는 유지하고 알약/아이콘은 형제 오브젝트로 배치.
  - 다시 실행해도 그리기 순서가 알약 → 아이콘 → 라벨로 유지됨(2026-10-05 수정: 재실행 시 알약이 글자를 덮던 문제).
  - 선물상자를 모두 열면 'ALL BOXES OPENED' 확인 창 없이 바로 다음 화면으로 넘어감(GameFlowManager.OpenOrContinueGiftBox).
  - `HudStats`: 선물상자 0개면 알약 숨김 → 첫 상자 획득 시 오른쪽에서 슬라이드 인. 획득 위치에서 상자 아이콘이 곡선으로 날아가 도착할 때 카운트 +1(펀치 + 짧은 효과음). 코인 아이콘 펀치. 상자 획득 이벤트는 `GiftBoxPickup.Collected`.
- `ComboBadge` 게이지 — 콤보 타임아웃 남은 비율(`RunManager.ComboTimeLeft01`, 읽기 전용 추가).
- `Dev/DevAutoPilot` — 에디터 전용 자동 플레이(시각 QA용). 빌드에 포함되지 않음.

## 코스 (2026-10-01 확장)

- 패턴 17종(기존 A–E + F–Q). 난이도: Easy F·G·H·I / Medium J·K·L·M / Hard N·O·P·Q.
- 화물칸(`Train Wagon`, `Train Wagon Double`)은 폭 8.6m·높이 1m의 Jump 장애물이다. 앞면은 충돌 사망, 지붕은 착지·달리기 안전. 루트가 앞면에 있어 거리 판정이 앞면 기준.
- 세트피스(시계탑 게이트, 고딕 아치)는 콜라이더 없는 장식이다.
- 패턴 안 아이템/상자는 후보일 뿐이다: `PatternSpawner`의 `powerUpChance`(0.3)·`giftBoxChance`(0.18)·`maxPowerUpsPerPattern`(1)로 생성 시 남길지 결정한다. 코인은 항상 나온다. 별도 시드 스트림이라 고정 시드의 코스 순서는 그대로다.
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
