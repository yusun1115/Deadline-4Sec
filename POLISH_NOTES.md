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
| Course/Build Air Route Patterns | (2026-10-16, Upper Platform.md) 공중 경로 패턴 5종 U1(진입)·U5(진입 Weave, Hard)·U2/U3(상층 달리기)·U4(복귀) 생성·세 씬 등록. 상층 덱은 `Upper Deck`(높이 8m, 폭 8.6m, FloorColor + `OneWayPlatform`) + 핑크 가장자리·거더(콜라이더 없음). 활성 씬이 저장된 상태에서 실행 |
| Course/Build Wall Run Patterns | (2026-10-18, Wall Run.md) Wall Run 패턴 2종 W1(왼쪽 진입)·W2(오른쪽 진입) 생성·세 씬 등록. 바닥 구멍(26~53m) 양옆에 벽 패널(시각용, 콜라이더 없음)·핑크 러닝 라인·바닥 끝 발광 립·구덩이 바닥, `SetPiece Wall Run` 안에 `WallRunSection`(WallRunEntry 트리거, WallRunExit, Left/RightWallAnchor, GroundExitAnchor, ReviveCheckpoint) + `FallDeathTrigger`. 활성 씬이 저장된 상태에서 실행 |
| Skins/Apply Weapon Skins | (2026-10-30) `Art/3DModel/Weapon`의 무기 모델 6종으로 스킨 적용: URP 머티리얼(`Art/Materials/WeaponSkins/Weapon_<이름>.mat`), 스킨 프리팹(`Prefab/WeaponSkins/WeaponSkin_<이름>.prefab`, 루트=손잡이 위치 + `WeaponSkinFx`), 상점 아이콘(`Art/2D/WeaponSkins/icon_weapon_<이름>.png`, 모델 렌더링), 카탈로그 이름·프리팹·색·아이콘 갱신. 씬은 건드리지 않음 |
| UI/Apply Skin Shop | (2026-10-30) 무기 스킨 상점: 상단 Soul 아이콘(`TextLeadingIcon`), 카드 290 높이로 재배치(무기 아이콘·이름·아이콘+숫자 가격·버튼·미리보기 테두리, 카드 본체=미리보기 버튼), "스킨 미리보기 입니다." 태그, 구매 확인 창(`PurchaseDialog`, 예/아니오). `SkinShopUI`를 SkinPanel에 붙임. 열린 씬이 이미 수정 중이면 저장하지 않음 |
| Character/Apply Reaper Rig Fixes | (2026-10-30) 머리에 묶인 후드 메시(`Art/3DModel/Generated/Hood_HeadBound.asset`) 생성 + 세 씬의 ReaperModel에 `ReaperRig` 추가(발 높이·후드·망토 물리·낫 잡기·왼손 IK). 열린 씬이 이미 수정 중이면 저장하지 않음 |
| Prepare Tutorial Course | 튜토리얼 코스 에셋(`Resources/Tutorial/TutorialCourse.asset`)에 실제 적·장애물 프리팹·바닥 머티리얼 연결. (2026-11-01) 화자·말풍선·스와이프 화살표·손 스프라이트(`Art/Tutorial`)도 연결, 단계 목록이 비어 있을 때만 기본 12단계로 채움(인스펙터에서 고친 문구 유지). 씬은 건드리지 않음 |
| UI/Apply Ranking Style To Windows | (2026-11-01) `ui_panel_gothic` / `ui_button_gothic`가 남은 창(설정·결과·부활·선물상자·업그레이드·업그레이드 팝업·튜토리얼 스킵 확인·SKIP·모드 선택 SELECT)을 랭킹/업적 스타일로 통일: 패널 = 어두운 자주색 + 핑크 외곽선 + 위/아래 핑크 포인트 바, 버튼 = 자주색 + 포인트색 외곽선·왼쪽 띠·글자(뒤로/아니오/포기/메인은 연핑크), 크기는 고딕 아트의 투명 여백만큼 축소(가로 92%, 세로 80%). 인게임 타이머 프레임은 그대로. 업그레이드 창은 업적 창 배치(넓은 프레임 + 화면 전체 스크롤 목록)로, 소모 아이템 칸은 목록 맨 아래 항목(스크롤해서 봄). 고딕 아트를 쓰는 예전 메뉴(UI Skin / Title·Upgrade 배치 / Mode Select)를 다시 실행했다면 이 메뉴도 다시 실행. 열린 씬이 이미 수정 중이면 저장하지 않음 |
| UI/Apply Settings Icon Buttons | (2026-11-04) 설정 화면: SOUND / VIBRATION을 나란히 놓인 정사각형 아이콘 버튼(190×190)으로 바꾸고 TUTORIAL·BACK은 아래 넓은 버튼. 아이콘은 `UI/icon_sound_on·off`, `icon_vibration_on·off`(없으면 `Art/Draw Setting Toggle Icons`로 먼저 그림 — 같은 이름의 PNG로 교체 가능), 버튼의 `SettingToggleIcon`이 저장된 설정에 따라 ON/OFF 아이콘으로 바꾸고 OFF는 흐리게. On Click(ToggleSound / ToggleVibration)과 버튼 이름은 그대로, 글자 라벨은 숨김. 세 씬 적용, 열린 씬이 이미 수정 중이면 저장하지 않음 |
| Course/Build Areas | (2026-11-06) Area 시스템 데이터: 전환 터널(`Decor_AreaTunnel`, `Pattern_AreaTransition_Tunnel`), 회복 패턴(`Pattern_V1_Landing`, `V2_SoulWalk`), 성당·심연 장애물 모델 각 3종, 바닥·덱·장식 재질, `Resources/Areas`의 Area 에셋과 카탈로그 생성. 씬은 건드리지 않음. 다시 실행해도 Area 에셋의 길이·간격·룩 값은 유지 |
| UI/Apply Result Screen Layout | 결과창 정보 계층(큰 SCORE → 거리/콤보/시간 3열 → NEW BEST → PERSONAL BEST → RUN REWARD → MAIN MENU \| RETRY 가로 버튼) + 뒤 어두운 오버레이 + `ResultPresenter` 연결. 기존 오브젝트 이름·참조·On Click 유지, ConfirmButton은 비활성(2026-10-09 OPEN REWARDS 제거 요청 — 상자는 두 버튼 모두 기존 상자 오픈 흐름을 먼저 거침). 세 씬 적용 |
| UI/Build Loading Scene | `Assets/Scenes/Loading.unity` 생성(키아트 + LOADING... + bg/fill/frame 게이지 + 랜덤 팁) 후 Build Settings 0번에 배치. 다시 실행해도 인스펙터에 적은 팁 목록은 유지 |
| UI/Apply Number Font To HUD | CallifontSharpie-Bold TMP 에셋 생성(`Resources/Fonts/CallifontSharpie-Bold SDF`) + HUD 숫자 라벨(타이머·점수·거리·코인·선물상자 수)에 `NumericLabel` 부착·크기 보정 |
| UI/Apply Gift Chest Opening | 선물상자 개봉 화면: 회전 선버스트 배경 + 3D 상자 뷰 + 보상 아이콘. 기존 텍스트/버튼 이름·연결은 유지, 배치만 화면 비율 기준으로 변경 |
| Audio/Assign Imported Sounds To Library | Casual Game Sounds / Demonic Music을 `SoundLibrary`에 배정, 모바일 임포트 설정 |
| Art/Apply Soul Pickup Model | (2026-10-11) Coin 프리팹의 외형을 `3DModel/Props/Soul.fbx`로 교체: 기존 원판 `Visual` 렌더러는 끄고 `FloatPivot/SoulModel`(높이 1.15m, 트리거 중앙 정렬, URP Unlit `Materials/PowerUps/Soul.mat`) 추가, 후광은 하늘색. 프리팹 이름·트리거·`CoinPickup`·패턴 배치·저장 키는 그대로. 화면 표기는 Coin → Soul(SOUL MAGNET, `SOUL +300`, 업그레이드 잔액, 스킨 가격, "Soul이 부족합니다!") |
| Audio/Assign Soul Pickup Sound | Soul 획득음을 `Sound/SFX/driken5482-retro-coin-4-236671`로 지정(SoundLibrary `coin` 슬롯). 0.7초 안에 연속으로 먹으면 한 번마다 반음씩 올라가 최대 12단계(1옥타브)에서 유지, 끊기면 원래 음높이로 — `CameraFeedbackController`의 전용 보이스 3개라 다른 효과음 피치는 안 바뀜 |
| Audio/Assign BGM | (2026-10-11) 타이틀 음악 = `Sound/BGM/BGM_MainScreen`, 인게임 음악 = `Sound/BGM/BGM_InGame` (SoundLibrary `titleMusic`/`runMusic`, 스트리밍). 전환·페이드·게임오버 덕킹은 기존 `MusicDirector` 그대로 |
| UI/Apply Currency Shop | (2026-10-15) `Resources/Shop/CurrencyShopCatalog.asset`(상품 4개, 없을 때만 생성) + `Canvas/CurrencyShopPanel`(비활성: Background, BackButton → `Close`, SoulBox, Card_1~4[BuyButton → `CurrencyShopCard.Buy`], StatusText, EffectLayer). 타이틀 `CurrencyshopButton` → `OpenFromTitle`, SkinPanel / UpgradePanel의 Soul 수량 오른쪽에 `CurrencyPlusBtn` 생성(수량·아이콘을 왼쪽으로 46 이동) → `OpenFromSkinShop` / `OpenFromUpgradeShop`. 아트 `Art/2D/CurrencyShop`. 세 씬 적용 |
| Debug/Soul Shop - Reset Today's Ad Views | 개발용. 오늘 광고 시청 횟수 초기화 |
| UI/Apply Daily Quest | (2026-10-15) `Resources/DailyQuests/DailyQuestCatalog.asset`(퀘스트 풀, 없을 때만 생성) + `TitlePanel/DailyQuestPopup`(비활성: Backdrop, Popup_Dailyquest[ResetTimerText, box_Dailyquest_1~3, Xbutton_Dailyquest → `Close`], CurrencyBox, RewardAnimationLayer). 타이틀 `QuestButton` On Click → `DailyQuestPopup.Open` + `DailyQuestButton`(RedDot). 아트 `Art/2D/Dailyquest`. 세 씬 적용 |
| Debug/Daily Quest - Log · Complete All · New Quests | 개발용. 오늘 퀘스트 출력 / 전부 완료(보상은 팝업에서) / 저장 삭제 후 새 퀘스트 |
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

## 일일 퀘스트 (2026-10-15, DailyQuest.md)

- **흐름**: 게임플레이 → `PlayerStats.Recorded`/런 값 → `DailyQuestManager`(진행·완료) → 타이틀 `QuestButton` RedDot → 팝업 열기 → 완료 퀘스트 자동 보상(연출) → Claimed 저장 → RedDot 갱신.
- **스크립트(`Assets/Script/DailyQuest/`)**: `DailyQuestDefinition`(데이터+enum), `DailyQuestCatalog`(ScriptableObject 풀, 기본 19개), `DailyQuestManager`(정적: 생성·저장·자정 리셋·진행·`ClaimReward`), `DailyQuestBoxView`, `DailyQuestButton`(RedDot, 1초마다 날짜 확인), `DailyQuestPopup`(DOTween 연출). 에디터 `DailyQuestSetup`, 테스트 `DailyQuestAcceptanceTests`. 기존 스크립트 수정 없음.
- **선택**: 풀을 섞어 서로 다른 ID 3개, 같은 종류(예: 처치 25/50)는 하루에 하나만(풀이 모자라면 허용).
- **진행**: Counter/수집 퀘스트는 `PlayerStats.Recorded`의 증가분만 더함(생성 이후 행동만, 평생 누적값 미사용). 콤보/거리는 런 중 최고값. Deadline/Classic 모두 집계(`modeRestriction`으로 모드 전용 가능). 통계는 PlayerStats가 한 번만 기록하므로 업적·퀘스트 중복 카운트 없음.
- **자정**: 기기 로컬 날짜. 앱 시작·버튼 표시·팝업 열기·통계 기록 때와 1초마다 저장 날짜와 비교 → 다르면 새 퀘스트(미수령 보상은 소멸). 날짜가 과거로 가면 경고 로그. 남은 시간 = 다음 00:00 - 현재.
- **보상**: `ClaimReward`에서 재화 지급(`CoinWallet.CreditTotalCoins`)과 Claimed 저장을 함께 하고 즉시 `PlayerPrefs.Save` → 팝업에서 모든 미수령 보상을 먼저 지급한 뒤 연출(박스 Dim → Soul 박스 오른쪽에서 등장 → 아이콘 곡선 비행 → 펀치 + 카운트업, 퀘스트별 순차). 연출 중 X 버튼 비활성.
- **저장 키**: `DailyQuest_CurrentDate`(yyyy-MM-dd), `DailyQuest{1~3}_ID/_Progress/_Completed/_Claimed`.

## Soul 상점 (2026-10-15, Currencyshop.md)

- **구조**: 상점은 Canvas 맨 위에 덮는 화면. Title/Skin/Upgrade 화면을 닫지 않고 위에 열기 때문에 Back(`Close`)은 덮개만 닫고 들어온 화면으로 그대로 복귀. 진입 경로는 `PreviousShopContext`(`ShopEntrySource.Title/SkinShop/UpgradeShop`)에 기록.
- **스크립트(`Assets/Script/Shop/`)**: `SoulBank`(Soul 지급 공통 창구 `GrantSoul(amount, SoulSource)`, 기존 `CoinWallet`/`TotalCoins` 저장 그대로 사용), `CurrencyShopCatalog`(상품 데이터 + 개발용 스위치), `ShopServices`(`RewardedAdService.Show`, `SoulPurchaseService.Purchase` — SDK 연결 지점), `CurrencyShopManager`(광고 일일 제한·날짜 리셋·지급·중복 방지), `CurrencyShopCard`, `CurrencyShopPanel`. 에디터 `CurrencyShopSetup`, 테스트 `CurrencyShopAcceptanceTests`. 수정: `DailyQuestManager.ClaimReward`가 `SoulBank.GrantSoul` 사용.
- **상품**: `CurrencyShopProduct`(productId, productType RewardedAd/IAP, soulAmount, bonusSoul, displayPrice, iapProductId, icon). 지급량 = soulAmount + bonusSoul, 카드 표시(X5,500 / +500)도 같은 값에서 계산.
- **광고**: 하루 `adDailyLimit`(3)회, 광고 성공 콜백에서만 Soul 지급 + 횟수 +1 + 저장. 실패/취소는 변화 없음. 요청 중 `isProcessingRewardAd`로 재요청 차단, 콜백은 한 번만 처리. 날짜는 일일 퀘스트와 같은 시계(`DailyQuestManager.Now`)·같은 형식으로 `DailySoulAdDate`와 비교 → 다르면 `DailySoulAdCount` 0. 소진 시 카드 Dim + 버튼 비활성 + "FREE 0/3".
- **SDK 없음**: 광고 SDK·IAP 미설치. `simulateRewardedAdForTesting`(기본 false)는 에디터/개발 빌드에서만 컴파일, `debugPurchaseInEditor`는 에디터에서만 → 릴리스 빌드는 광고 "불가", 구매 "실패"로 처리되어 무료 지급 불가.
- **연출(DOTween)**: 카드 순차 등장(스케일 0.9→1 + 페이드), 보상 시 카드 아이콘이 상단 Soul 박스로 곡선 비행 → 아이콘 펀치 → 숫자 카운트업, "+100 SOUL" 문구 페이드.
- **Red Dot**: 오늘 광고 보상이 남아 있으면 타이틀 `CurrencyshopButton/RedDot`(`CurrencyShopButton`, 1초마다 날짜 확인)과 광고 카드 `Card_1/RedDot` 표시, 3회 모두 쓰면 해제, 자정 후 다시 표시.
- **저장 키**: `TotalCoins`(기존 Soul), `DailySoulAdDate`, `DailySoulAdCount`.

## 코스 (2026-10-01 확장)

- 패턴 17종(기존 A–E + F–Q). 난이도: Easy F·G·H·I / Medium J·K·L·M / Hard N·O·P·Q.
- 화물칸(`Train Wagon`, `Train Wagon Double`)은 폭 8.6m·높이 1m의 Jump 장애물이다. 앞면은 충돌 사망, 지붕은 착지·달리기 안전. 루트가 앞면에 있어 거리 판정이 앞면 기준.
- 세트피스(시계탑 게이트, 고딕 아치)는 콜라이더 없는 장식이다.
- 패턴 안 아이템/상자는 후보일 뿐이다: `PatternSpawner`의 `powerUpChance`(0.3)·`giftBoxChance`(0.18)·`maxPowerUpsPerPattern`(1)로 생성 시 남길지 결정한다. 코인은 항상 나온다. 별도 시드 스트림이라 고정 시드의 코스 순서는 그대로다.
- 전철(2026-10-10): `Obstacle.ObstacleType.RampTrain` 추가(경사로 전철 라벨). 전철의 Near Miss 좌우 여유는 0.35m — 옆 레인을 달리는 것만으로는 Near Miss가 아니고, 정면 직전에 레인을 바꿔 스칠 때만 인정. 리플레이 봇 규칙상 단독 전철은 왼쪽(-1) 레인 또는 좌우 쌍으로, 지붕 위엔 공중 적만, 지붕에서 떨어진 뒤 지상 적은 착지 지점(최고 속도 ~18m) 이후에 배치. Reaper Rush는 지붕도 통과하므로 지붕 위에 두지 않음.
- 기존 코스 특성 메모: Air로 끝나는 패턴(C·D·J·L·M·O·P) 뒤엔 Air/Any 입장 패턴만 올 수 있어, 한 번 공중 패턴 흐름에 들어가면 지상 패턴(전철 포함)으로 돌아오지 않았음. 2026-10-16부터 공중 경로 진입(U1·U5)이 Any 입장이라 이 흐름에서도 나오고, 복귀(U4)가 Ground로 끝나 고리가 끊김.
- 배치 규칙(리플레이 봇 기준): 연속 점프 금지(점프 체공 ~8m), 지상 적은 인접 레인으로 ≥5m 간격, 높은 공중 콤보 직후에는 Ground 입장 패턴만.

## Air Route + Upper Platform (2026-10-16, Upper Platform.md)

- **흐름**: 공중 적 호밍 연쇄로 상승 → 상층 덱 착지 → 상층 달리기 1~2개 → 덱 끝에서 낙하해 지상 복귀. 연쇄를 놓치면 그대로 아래 경로(지상)로 진행 — 아래 경로에도 Soul·적·장애물이 있어 손해만 있고 죽지 않음.
- **두 층 패턴**: 패턴 하나에 지상(기존 바닥)과 덱(`CoursePattern.UpperDeckHeight` = 8m)이 함께 있음. 덱 높이가 항상 같아서 패턴 사이 Y 연결 계산이 필요 없음. `TravelState.Upper` 추가: Upper는 Upper하고만 연결(Any도 안 됨). 진입 = Any→Upper, 상층 = Upper→Upper, 복귀 = Upper→Ground. `PatternCategory.AirRoute` 추가.
- **연쇄 배치**: 공중 적은 레인을 번갈아 5m 간격으로 상승, 마지막 적은 덱보다 1m 위·덱 1.5m 앞 → 그 킬의 바운스로 덱에 착지. 놓쳤을 때는 마지막으로 잡은 적의 레인(다음 적 아래가 아님)으로 떨어지고, 덱 앞면(7.5m) 아래로 지나갈 수 있는 높이.
- **스포너(`PatternSpawner`, 인스펙터 Air Route)**: `airRouteStartsAtSeconds`(25) 이후, 일반 패턴 `groundPatternsBetweenAirRoutesMin~Max`(5~8)개마다 진입 패턴, 상층 `upperPatternsMin~Max`(1~2)개 후 복귀. 연속 공중 경로 없음. 상층 중에는 난이도·카테고리 연속 규칙을 보지 않음. 공중 경로는 별도 난수(`fixedSeed + 104729`)를 써서 첫 공중 경로 전까지 기존 시드의 코스 순서가 그대로.
- **`OneWayPlatform`**: 덱은 위에서만 단단함. 발이 덱 윗면보다 아래면 플레이어와 충돌 무시 → 아래 경로에서 점프·스톰프·Ground Slam 바운스가 덱 밑면/앞면에 걸리지 않고, 위에서 내려오면 착지. 레이캐스트(Ground Slam 높이 계산 등)는 덱을 그대로 봄.
- **카메라(`CameraFeedbackController`, Vertical follow)**: 착지한 바닥 높이를 기준으로, 플레이어가 기준+`verticalFollowThreshold`(2.2m) 위로 올라가면 따라 올라가고, 기준보다 아래로 내려가면 바로 따라 내려감(`verticalFollowMaxDropLag` 0.8m 이내). 일반 점프(1.6m)는 데드존 안이라 카메라가 안 움직임. `verticalFollowSpeed`(5)로 부드럽게.
- **모드**: 호밍 킬마다 기존 규칙대로 Deadline 타이머 리셋. Classic도 같은 경로. 플레이어 컨트롤·입력·호밍 코드는 변경 없음.
- **테스트 봇**: 리플레이 봇(`CourseReplayAcceptanceTests.Drive`)과 `DevAutoPilot`은 발밑 바닥 높이(`FloorUnder`)를 기준으로 같은 층의 위협만 봄(덱 위 적·장애물은 아래 경로에서 무시, 그 반대도). `AirRouteAcceptanceTests` 7종: 패턴 구조, 스포너 규칙, Deadline/Classic 상승·덱 플레이, 연쇄 실패·빠른 낙하 후 아래 경로, 복귀 낙하·카메라.

## Wall Run (2026-10-18, Wall Run.md)

- **흐름**: 지상 → 바닥이 끊기는 구간 → 진입 창(WallRunEntry) 안에서 벽 방향으로 좌/우 입력 → 벽 부착 → 좌/우 입력으로 반대 벽 전환하며 벽 장애물 회피 → WallRunExit에서 일반 상태로 → 벽 쪽 레인에 착지. 자동 부착 없음. 입력이 없거나 반대 방향이거나 창이 끝난 뒤면 그대로 떨어져 `FallDeathTrigger` → `GameTimer.DeathReason.Fall`.
- **상태**: `PlayerController.MovementState { Normal, WallRun }` 추가(새 컨트롤러/입력 없음). WallRun 중엔 중력·점프·슬라이드/빠른 낙하/슬램·호밍 OFF, 좌/우 = 벽 전환(같은 방향·전환 중 입력 무시). 좌/우 입력 우선순위: WallRun 전환 > 진입 창의 부착 > 일반 레인 이동 → 한 번의 스와이프가 레인 이동과 부착을 동시에 하지 않음. 키보드(A/D/←/→)·스와이프 모두 `RequestMoveLeft/Right` 경유라 같은 분기.
- **이동**: 부착 0.2초(`wallAttachDuration`)·전환 0.28초(`wallSwitchDuration`) 동안 SmoothStep으로 앵커 라인(X, 발 높이 0.8m)까지 이동하면서 앞으로는 계속 달림(`wallRunSpeedMultiplier` 1 × 현재 달리기 속도, +Z). 벽 장애물은 기존 `Obstacle`(Lane Blocker) → 충돌 즉사·Shield 1회 방어·Near Miss(늦게 피하면 타이머 리셋) 그대로.
- **기울기**: `wallRunTiltAngle`(16°, 벽 쪽으로) — 모델은 `PlayerCharacterAnimator`, 무기는 PlayerController가 같은 피벗으로 회전. 전환 중 중간에 똑바로 섰다가 반대로 기욺. 전용 애니메이션 없음(Run 상태 사용, 나중에 WallRun_Left/Right로 교체 가능).
- **카메라**: Wall Run 중 FOV +4(`wallRunFovBoost`), 부착 시 FOV 펀치, 전환 시 옆으로 살짝 밀림. 카메라 롤 없음. 낙사 때는 카메라가 최대 3.5m(`fallDeathCameraLag`) 위에 남고 캐릭터가 떨어져 내려감.
- **낙사 연출**: 히트 스톱·플래시·타이머 펀치·슬로모션 없음(`MomentFx`), 충돌음 대신 바람 소리 플레이스홀더(`SoundLibrary.fallWind` 슬롯, 비면 합성 노이즈), 사망 포즈 대신 계속 낙하. Shield는 낙사를 막지 않음(접촉 사망만 방어하는 기존 정책).
- **부활**: 구간(체크포인트~착지 지점) 안에서 죽으면 `ReviveCheckpoint`(진입 창 직전 지상, 가운데 레인)에서 부활. 낙사·벽 장애물 사망 모두.
- **Deadline**: 벽 위에 있는 동안(부착~출구) 타이머 멈춤(2026-10-18 요청, `GameTimer.Tick`), 출구 후 다시 감소. 멈춘 동안 숫자가 보라색(`GameTimer.wallPausedTimeColor`)이 되고 타이머 뒤 은은한 보라 빛 + 아래 "❚❚ PAUSE" 표시(`MomentFx`), 마지막 1.5초 압박 연출(틱·심장박동·붉은 비네트)은 멈춤. 부착·전환은 타이머를 리셋하지 않음. 진입 창 직전 적(12m)·착지 후 적(59.5m)이 앞뒤 리셋 기회. Classic은 같은 패턴.
- **스포너(인스펙터 Wall Run)**: `wallRunStartsAtSeconds`(45) 이후, `patternsBetweenWallRunsMin~Max`(6~10)개마다, 특수 구간(공중 경로·Wall Run) 뒤 일반 패턴 `normalPatternsAfterSpecial`(3)개 보장. 공중 경로와 동시에 차례가 되면 더 오래 기다린 쪽 먼저. Wall Run은 Ground 입장이라 공중 패턴 흐름(Air 종료) 뒤엔 못 오므로, 차례가 된 뒤엔 일반 패턴을 지상 종료 패턴 위주로 골라 지상을 유지. 별도 난수(`fixedSeed + 15485863`)라 첫 Wall Run 전까지 기존 시드 코스 그대로. 바닥은 구멍 앞/뒤 두 조각으로 생성.
- **튜토리얼 훅**: `WallRunSection.EntryReached`(정적 이벤트, 구간마다 1회) — 나중에 "← SWIPE" 표시 연결용.
- **테스트 봇**: 리플레이 봇·`DevAutoPilot`이 진입 창에서 벽 방향 입력, 벽 위에선 앞 장애물 반대쪽으로 전환. `WallRunAcceptanceTests` 16종.
- **실기기 확인 필요**: Android 스와이프 판정·부착·전환·낙사·카메라(명세 §75)는 기기에서 직접 확인해야 함.

## 무기 스킨 (2026-10-30)

- **이름 변경**: DEFAULT SCYTHE → NEON REAPER(기본), NEON REAPER → STARLIGHT, FROST MOON → FROST BITE. BLOOD MOON·CYBER DEATH·ECLIPSE 그대로. 스킨 id(`DefaultScythe`, `NeonReaper`, `FrostMoon`…)는 그대로라 보유·장착 저장 데이터 유지.
- **모델 교체**: `WeaponSkinVisual.Apply(Skin)` — 손에 붙은 기본 낫 노드(`tripo_node_29219f7e`, 애니메이션이 직접 움직이는 노드) 밑에 스킨 프리팹을 붙이고 기본 낫 렌더러만 숨김(스윙 잔상 등 붙어 있는 것은 그대로 따라감). 프리팹 원점 = 손잡이(낫: 자루의 아래 1/3 지점, BLOOD MOON 검: 손잡이 13% 지점), 기본 낫과 같은 축이라 같은 손 위치. 기본 스킨(NEON REAPER)은 같은 모델이라 기본 낫 그대로, 이펙트 없음.
- **이펙트(`WeaponSkinFx`)**: 런타임 절차 생성 파티클(공용 `FxAdditive` 셰이더), 모든 스킨에 스킨 색 빛무리 + 두 겹: STARLIGHT 금빛 반짝임·별빛 팝 / FROST BITE 가라앉는 냉기 안개·얼음 반짝임 / BLOOD MOON 떨어지는 핏방울·올라가는 붉은 불씨 / CYBER DEATH 깜빡이는 데이터 조각·옆으로 튀는 글리치 / ECLIPSE 보랏빛 오라·금빛 코로나 불꽃. 러너 기준 공간이라 달려도 화면 뒤로 흘러가지 않고 스윙 때만 뒤로 남음.
- **색**: 카탈로그 `accentColor` → 스윙 잔상 색(`CombatParticles`, `WeaponSkinVisual.AccentChanged`), 기본 스킨이 아니면 처치 불꽃에 스킨 색 추가.
- **상점**: 카드 아이콘이 각 무기 렌더링 아이콘으로 바뀜(미보유는 흐리게). 왼쪽 미리보기 캐릭터는 장착한 무기·이펙트를 그대로 보여줌.
- **모델 원본 머티리얼**: FBX에 연결된 `weapon_mat/tripo_mat_*.mat`은 Built-in 파티클 셰이더라 URP에서 안 그려져 사용하지 않음(별도 URP 머티리얼 생성).
- **테스트**: `WeaponSkinAcceptanceTests` 2종(카탈로그 이름·모델·이펙트·아이콘 / 장착 시 손 위치·크기·이펙트 재생·잔상 색·기본 복귀).

## 무기 스킨 상점 (2026-10-30)

- **상단 재화**: Soul 숫자 왼쪽에 Soul 아이콘(`icon_coin_gold`), 자릿수가 바뀌어도 숫자 바로 왼쪽에 붙음(`TextLeadingIcon`).
- **가격**: 글자 대신 아이콘 + 숫자. Soul은 `[아이콘] 8,000`(부족하면 빨강), 스킨 재화는 `[재화 아이콘] 18 / 30`(보유 수가 부족하면 빨강, 충분하면 초록). 둘 다 필요한 카드(ECLIPSE)는 두 줄. 위치는 런타임 `SkinShopRow.PlacePrice`로 아이콘+숫자를 가운데 정렬.
- **카드 상태(`SkinShopRow.CardState`)**: 장착(Equipped) 핑크 테두리 + 맥동 글로우, "EQUIPPED"(비활성) / 보유(Owned) 흰·보라 테두리, 보라 "EQUIP" / 구매 가능(Buyable) 밝은 핑크 "BUY"(살짝 맥동) / Soul 부족(SoulShort) 어두운 "BUY"(누르면 재화 상점 안내) / 잠김(Locked, 스킨 재화 부족) 회색 "BUY"(비활성). 버튼은 꽉 찬 둥근 사각형(`FxSprites.RoundedBox`).
- **카드 테마(`SkinCardTheme`, 런타임)**: 테두리 안쪽 선 + 아이콘 뒤 빛 + 작은 움직임만 — STARLIGHT 금빛 글로우·반짝임 / FROST BITE 서리 테두리·눈송이 / BLOOD MOON 붉은 균열·불씨 / CYBER DEATH 초록 스캔라인·모서리 브래킷·깜빡이는 픽셀 / ECLIPSE 보라 이중 테두리·금빛 링·모서리 다이아몬드. 기본(NEON REAPER) 카드는 깔끔하게 둠.
- **보유 카드 탭**: 이미 가진 스킨 카드를 누르면 미리보기 없이 바로 장착(2026-10-31).
- **미리보기**: 아직 없는 스킨 카드를 누르면 캐릭터가 그 무기를 들고(이펙트 포함) 상단에 "스킨 미리보기 입니다." 표시, 미리보는 카드엔 흰 테두리. 다시 누르거나 장착 중인 카드를 누르면 해제. 화면을 나가면 장착 스킨으로 복귀(`RunInventory.PreviewSkin / ShowEquippedSkin`).
- **구매**: BUY → 그 무기를 미리보기로 보여주며 "구매하시겠습니까?" 예/아니오 → 예: 구매 + 장착. Soul 부족 → "Soul이 부족합니다. 재화 상점으로 이동할까요?" 예/아니오 → 예: 재화 상점(`CurrencyShopPanel.OpenFromSkinShop`, Back으로 스킨 상점 복귀). Soul이 바뀌면 카드 상태 자동 갱신.
- **상점 무대(`SkinShopStage`, 런타임)**: 스킨 화면 동안 캐릭터 뒤에 어두운 보라 그라데이션 벽, 회전하는 핑크/보라 링 조명, 발밑 원형 플랫폼(핑크 테두리) + 어두운 바닥. 나가면 숨김.
- **헤더**: 카드 목록 위 "SCYTHE" 제목은 비움(2026-10-30).
- **테스트**: `WeaponSkinAcceptanceTests.SkinShopPreviewsConfirmsAndSendsShortSoulToTheCurrencyShop`.

## 캐릭터 보정 (2026-10-30, `ReaperRig`)

- **발 높이**: 모든 애니메이션에서 신발 바닥이 지면보다 ~0.17~0.2m 떠 있었음 → 모델을 0.2m 내림(`groundOffset`, 대기 시 지면 ±3cm, 달리기 최저점 살짝 닿음).
- **후드**: 후드+망토 메시가 망토 뼈(Cape_Root, 가슴에 고정)에만 묶여 있어 머리를 돌리면 얼굴이 후드에서 빠져나옴 → 목 위 정점을 머리뼈에 묶은 메시 복사본으로 교체(2282개 중 959개, 목 근처는 망토 뼈와 섞어 자연스럽게 이어짐). 원본 FBX는 그대로.
- **망토 물리**: 망토 뼈 3줄(L/C/R × 4마디, 애니메이션 키 없음)에 스프링 시뮬레이션 — 중력, 달리기 관성 일부(`runInertia` 0.3), 약한 펄럭임, 최대 흔들림 각도(70°), 엉덩이·몸통·다리 구체 충돌. 텔레포트(부활 등) 땐 초기화. 인스펙터에서 `stiffness`, `damping`, `gravity`, `runInertia`, `maxSwingAngle`, `flutter` 조절.
- **낫 잡기**: 낫 자루가 주먹 옆으로 지나가 손에서 어긋나 보였음(자루 축과 손가락 관절선이 51° 차이) → 자루를 손가락 관절선 방향으로, 주먹 안쪽(관절과 굽힌 손끝 사이)을 지나도록 무기 노드 위치·각도 보정(대기 애니메이션 첫 프레임 주먹으로 에디터에서 측정해 저장, 애니메이터가 매 프레임 스켈레톤을 다시 쓰므로 매 프레임 적용). 스킨 무기도 같은 노드에 붙어 함께 적용.
- **왼손**: 애니메이션에서 왼손이 자루 가까이(26cm 이내) 오면 자루를 쥐도록 2본 IK(거리에 따라 부드럽게 섞임, 오른손과 15cm 간격).
- **테스트**: `ReaperRigAcceptanceTests`(발 지면 접촉, 후드 머리 바인딩, 자루-주먹 정렬, 망토 움직임).

## 튜토리얼 온보딩 (2026-11-01, Tutorial.md)

- **구조**: 기존 `TutorialController`(10개 실전 레슨, 실제 입력·처치 이벤트로 완료)는 그대로 두고, 앞에 브리핑(InfoOnly) 2단계를 붙임. 연출은 새 `TutorialUIController`(TutorialHUD에 런타임 생성), 단계 데이터는 `TutorialStep`(stepId, kind, title, dialogueText, followUpText, highlightTarget, dimBackground, requiredInputType, showSwipeGuide, autoAdvance, nextDelay, completeCondition).
- **편집**: `TutorialCourse.asset`의 `steps`(문구·강조·가이드·딜레이)와 `presentation`(크기·위치·타이핑 속도·dim·펄스·손 이동 거리·성공 문구). 레슨 10개는 코스 배치 순서대로 `completeCondition`이 맞아야 하며, 순서가 어긋나면 경고 후 기본 단계를 씀.
- **화자**: 왼쪽 위 `tutorial_ex`가 왼쪽에서 OutBack으로 등장(0.26초) → 0.1초 뒤 `tutorial_ex_bubble`이 왼쪽 기준 ScaleX로 펼쳐짐 → 대사가 타이핑(초당 40자, 0이면 페이드). 단계가 바뀌면 말풍선이 살짝 튀고 새 대사를 타이핑.
- **브리핑**: 실제 타이머(4.00 정지)가 펄스 + 핑크 외곽 발광, 나머지 화면은 dim(GameplayUI 맨 앞에 깔려 타이머만 그 위로 그려짐). 아무 곳이나 탭 → 타이핑 완료 → 다음, 또는 대사가 끝나고 `nextDelay`초 뒤 자동 진행.
- **스와이프 가이드**: 하단 중앙. 레슨이 그 순간 요구하는 입력(`PromptInput`)을 따라 위쪽 화살표 스프라이트를 회전(좌 90°, 우 -90°, 하 180°)하고 방향으로 살짝 흔들림, 손이 화살표 옆에서 같은 방향으로 스와이프(페이드인 → 이동 → 페이드아웃 루프). 에디터/PC에서는 위에 키 안내(A / LEFT 등).
- **배치(2026-11-01 수정)**: 말풍선이 캐릭터 위·오른쪽에 있고 꼬리(왼쪽 아래)가 캐릭터를 가리킴(`portraitPosition` / `bubblePosition`).
- **입력 대기 정지(`pauseForInput`)**: 스와이프할 타이밍(`PromptInput`)이 오면 게임을 멈추고(`timeScale` 0, 가이드 표시) 그 방향 스와이프만 받아 다시 진행. 다른 방향은 무시(`PlayerController.Request*` → `GameFlowManager.TryTutorialInput` → `TutorialController.TryConsumeInput`). 점프·슬라이드 안내는 장애물과 같은 레인일 때, 레인 공격은 적이 바로 오른쪽 레인일 때만. 시간이 멈춘 프레임에는 플레이어 이동 갱신을 건너뜀(0 이동이 '공중'으로 판정돼 점프가 씹히던 문제).
- **실패 → 자동 재시도**: TRY AGAIN 버튼 없이 화면(말풍선 제외)이 살짝 어두워졌다가(`failFadeAlpha` 0.8, 0.25초 + 0.55초) 같은 단계를 자동 재시작. 잘못된 조작으로 장애물/목표 적을 지나쳐 버리면(옆으로 피함, 너무 이른 처치 등) 4초를 기다리지 않고 0.25초 뒤 바로 실패 처리.
- **성공/완료**: 성공 시 화면 중앙 NICE! / GOOD! / GREAT! 팝(니어 미스 단계 "NEAR MISS!", 타이머 단계 "4.00 RESET!" — 단계별 `successText`) 후 `nextDelay`(0.6초) 뒤 다음 단계. 완료 대사도 말풍선으로.
- **HUD**: 튜토리얼 동안 `hiddenHud` 목록(점수·콤보·Soul·선물상자·파워업 표시)만 CanvasGroup으로 숨김 — NEAR MISS 문구·점수 팝업·콤보 배지 같은 피드백은 보임. 하단 버튼은 SKIP(오른쪽 아래)만(MENU 제거). 종료/SKIP 시 `Cleanup`으로 전부 원복(씬도 다시 로드됨).
- **니어 미스 단계(11-01 수정)**: 장애물을 러너와 같은 레인 정중앙(x 2.5)에 두고, 장애물 앞까지 0.75m(연습 속도 6m/s에서 니어 미스가 되는 0.45~1.0m 사이)일 때만 정지·왼쪽 안내. 미리 피하면 니어 미스가 아니라 실패 → 자동 재시도.
- **공중 적 연결(11-01 수정)**: 공중 처치 직후(`PlayerController.IsAirComboReady`) 다음 적이 옆 레인이면 그쪽 방향 스와이프(오른쪽/왼쪽)로 안내·정지(실제 연결 조작), 같은 줄 위쪽이면 위. AIR HOMING은 위 → 오른쪽, STOMP는 위 → 왼쪽 → 아래. 점프 직후의 호밍 위 스와이프도 정지 대기.
- **공중 적 한 번에(11-05)**: 땅에서 공중 적을 향해 위로 스와이프하면 점프 후 호밍 공격이 저절로 이어짐(두 번째 위 스와이프·정지 없음). 공중 처치 후 이어지는 적(옆 레인 → 옆 스와이프, 같은 줄 위 → 위 스와이프)은 그대로 한 번씩.
- **끝(11-05)**: START RUN·SKIP 버튼 없음. 마지막 대사가 다 나온 뒤 `completeHold`(1.8초) → 검은 화면으로 페이드아웃(`endFadeOut` 0.6초) → 타이틀로 돌아와 페이드인(`endFadeIn` 0.5초, `ScreenFade`).
- **줄바꿈**: TMP Settings의 Modern Hangul Line Breaking을 켬(한국어가 단어 중간에서 끊기지 않음, 게임 전체 적용).
- **테스트**: `TutorialAcceptanceTests.SpeakerBriefsTimerThenSwipeGuideFollowsPromptAndSkipRestoresHud`, `HeldLessonTakesOnlyItsSwipeAndAWrongMoveRetriesByItself` + 레슨 10개 재생 테스트(브리핑은 탭, 모든 레슨이 정지 대기를 거침).

## Near Miss 판정 / Ground Slam 착지 (2026-11-01)

- **Near Miss = 마지막 순간 회피**: 기존 조건(장애물 주변 near 구역을 몸 전체가 지나가고 닿지 않음)에 더해, 장애물 앞 `lastMomentDistance`(기본 2.6m) 안에서 장애물과 정면으로 겹치는(좌우·높이) 충돌 코스에 있었어야 함(`Obstacle.HeadingInto`). 옆 레인으로 그냥 지나가거나(예: 전철 옆 레인 통과) 미리 피하면 Near Miss가 아님. 테스트 `ClosePassAwardsOneNearMiss`(막판 회피 = 인정), `StraightClosePassIsNotANearMiss`(옆 통과 = 불인정).
- **Ground Slam 반동 제거**: 충격파 판정 후 튀어 오르지 않고 바로 착지해 계속 달림(`groundSlamBouncePower` 삭제, 씬에 남은 값은 무시됨). 공중 콤보 상태도 해제.

## 런 초반 도움말 (2026-11-11, 플레이 테스트 피드백)

- **타겟 표시**: 매 판 처음 만나는 적 10마리에 표시. `PlayerController.TryGetKillWindow`가 실제 공격 판정 박스·속도·지속시간과 적의 공격 범위(옆/아래)로 "지금 스와이프하면 처치되는지"와 "몇 초 뒤 열리는지"를 계산. 옆 레인 공격의 실제 처치 구간이 0.2초 남짓이라, 열리기 0.75초 전부터 크고 흐리게 나타나 줄어들고, 처치 가능한 동안 작고 선명하게 깜빡임. 지원: 옆 레인 공격(지상 적), 점프 공격·호밍(공중 적), 급강하 스톰프·그라운드 슬램.
- **Wall Run 안내**: 매 판 첫 Wall Run 구간의 진입 범위 안에서 튜토리얼 스와이프 안내(`tutorial_swipe_arrow`, `tutorial_swipe_hand`)를 열린 벽 방향으로 표시. 붙거나 범위를 벗어나면 끝.
- 위치: `RunAssistGuide`(GameFlowManager에 자동 부착, 튜토리얼에서는 꺼짐), 표시 그림 `FxSprites.Reticle`/`ReticleOutline`(Resources/HUD/fx_reticle.png를 넣으면 그 그림 사용). 테스트: `RunAssistAcceptanceTests`(표시 첫/중간/마지막 프레임 스와이프는 처치, 직전은 빗나감), 코스 리플레이(10마리, 첫 Wall Run 안내).

## Area / Biome (2026-11-06, AreaBiome,Pacing.md)

- **현재 단계**: Phase 1~5 완료(Area 데이터, Soul Railway 등록, Transition, Broken Cathedral + Wall Run, Clockwork Abyss + Air Route). 순서는 철도 → 성당 → 심연 → 철도 순환. Phase 6~8(Intensity, Pacing Block, 후보 필터링)도 적용.
- **Air Route 연결**: 심연의 특수 풀(U1·U5 진입, U2·U3 상층, U4 복귀). 앞에 일반 2개, 이후 3~5개 간격으로 등장. 상층에 있는 동안은 Transition이 나오지 않고, 복귀 후에 나옴. 상층 패턴은 지역 패턴 수에 세지 않음.
- **패턴에 든 재질**: 상층 덱·벽 패널(`floorMaterial`), 덱 가장자리(`deckEdgeMaterial`), 아치·게이트(`decorMaterial`)를 스폰할 때 지역 재질로 교체.
- **관리 위치**: `AreaDirector`(PatternSpawner가 런타임에 추가). `SpawnArea` = 새 패턴을 만드는 지역(터널이 스폰될 때 바뀜), `CurrentArea` = 플레이어가 있는 지역(카메라가 터널 안에 들어오면 바뀜).
- **데이터**: `Resources/Areas/AreaCatalog`(순서, 전환 패턴, `showPatternDebugLogs`) → `Area_*`(`AreaDefinition`: 패턴 풀, 특수 풀, `minPatterns/maxPatterns`, 특수 구간 간격, 룩, 바닥, 장애물 스킨). 씬에 설정을 두지 않음. Spawner의 `areaCatalog`를 비우면 카탈로그를 불러오고, 런타임에 null로 두면 예전처럼 `patternPrefabs` 전체로 동작(규칙 테스트가 이 방식을 씀).
- **Transition**: Area의 패턴 수(8~12, 특수 구간을 아직 못 봤으면 최대 3개 더)를 채우면 터널이 나옴. 터널은 양쪽 벽·천장으로 주변을 가리고 끝의 포털이 앞쪽 코스를 가림. 카메라가 입구에서 6m 들어오면 하늘·안개·조명·배경을 교체(0.6초 블렌드). 포털은 이 교체를 가리는 역할만 하고, 교체 후 카메라가 8m→18m 지점을 지나는 동안 가운데부터 열림(`revealFrom/revealTo`, 셰이더 `AreaPortal`). 플레이어가 출구 약 19m 전에 있을 때 완전히 열려 다음 위험물이 보임. 나오면 지역 이름을 2초 표시. 양옆 레인 적 2마리로 Deadline 리셋 기회 유지.
- **배경**: `EnvironmentScroller`가 "현재/다음" 두 개까지만 유지. 다음 배경은 터널 스폰 때 비활성으로 만들고, 교체 때 이전 배경과 메시·재질을 삭제. 테마 메시는 `EnvironmentScroller.Themes.cs`.
- **장애물 스킨**: 판정 박스는 그대로, `ObstacleVisual`이 스폰 중인 Area의 모델을 박스에 맞춰 늘림(석상/잔해/부서진 아치, 현재는 절차적 메시). 전철 패턴은 성당 풀에서 제외.
- **Wall Run 연결**: 성당의 특수 풀. 시간 조건(45초) 대신 Area 안 패턴 수로 등장(앞에 일반 2개, 이후 3~5개 간격). 나오기 전까지는 지상 출구 패턴만 골라 Wall Run으로 이어지게 함.
- **회복 패턴이 필요했던 이유**: 공중 패턴은 출구가 전부 Air이고 지상으로 돌아오는 길이 Air Route뿐이었음. `V1/V2`는 어디서든 들어와 지상으로 끝남. Stomp 반동 체공(최대 약 28m)을 고려해 첫 적을 28m 뒤에 둠.
- **공중 처치 직후 의문사 수정(2026-11-07)**: 지상 적 정면 공격 범위는 일반 점프로 못 넘도록 머리 위로 높게 서 있음(바닥 위 약 3.24m). `Pattern_E_MixedRisk`처럼 공중 적 3.5m 뒤에 지상 적이 있으면 공중 처치 직후 머리 위를 날고 있는데도 이 범위에 닿아 죽었음. `EnemyAttackZone.frontClearAbove`(1.75): 발이 범위 바닥보다 이만큼 위면 안전(바닥 위 약 2m). 공중 처치 뒤 착지 전(`IsAirComboReady`)에는 `afterAirKillClearAbove`(0.5, 바닥 위 약 0.74m)까지 낮춤. 높이만으로는 12m/s에서 내려오는 궤적이 적을 지나기 전에 선을 넘어 여전히 죽었음. 적 몸통도 공중 처치 뒤 이미 지나친 적이면 스치기만 한 것으로 봄(30fps에서 뒤쪽 모서리에 걸렸음). 일반 점프 최고점(1.68m)은 공중 처치가 아니라 그대로 죽음. 값은 프리팹에 저장(기본값만 바꾸면 임포트 캐시 때문에 반영되지 않음). 테스트: `AirKillOverGroundEnemyAcceptanceTests`.
- **Pacing**: 패턴마다 `CoursePattern.intensity`(1~5), Area마다 `AreaDefinition.pacingBlocks`(첫 블록이 그 지역의 대표 곡선이자 시작). `PacingDirector`가 목표 강도를 정하고 `PatternSpawner`가 후보 중 가장 가까운 강도를 고름. 난이도 등급은 잠금 해제 용도로만 남음(20초 전 Easy, 45초 전 Hard 없음). 최근 3개 프리팹 제외, 강도 4 이상 연속 2개까지, 5 다음 5 금지, 목표가 3 미만일 때는 특수 구간 대기. 값은 PatternSpawner의 Pacing 항목과 `AreaCatalog.showPatternDebugLogs`(선택 로그, 간격 초과 조합 포함). 테스트: `PacingAcceptanceTests`.
- **빌드 첫 판에서만 적 처치 예외(2026-11-09)**: 빌드는 `Loading.unity`에서 시작하고 게임 씬을 Additive로 불러옴. 그동안 활성 씬이 Loading이라 게임이 Awake·Start·sceneLoaded에서 만든 `new GameObject`가 로딩 씬에 들어갔다가 함께 사라졌음. 처치마다 `EnemyDeathFx`에서 예외가 나 뒤쪽 리스너(튜토리얼, HUD 등)가 호출되지 않았음. 에디터와 Retry 후에는 재현되지 않음. `LoadingScreen`이 언로드 전에 자기 것이 아닌 오브젝트를 게임 씬으로 옮김. 테스트: `LoadingSceneAcceptanceTests`.
- **쉬운 패턴이 최고 속도로 돌아옴**: 예전엔 45초 뒤 Easy가 안 나와서 몰랐던 문제. `B_JumpSlide`가 점프 장애물로 끝나고 뒤가 6m뿐이라 16m/s에서 착지 전에 다음 패턴 장애물이 와서 봇이 죽음(B→Q). 길이 22→28m. Easy 패턴의 체감 강도가 최고 속도에선 숫자보다 높을 수 있으니 플레이로 확인 필요.
- **테스트**: `AreaAcceptanceTests`(카탈로그, 코스 순환, 터널 안 전환·배경 누적 없음·스킨). 코스 리플레이가 터널을 포함해 3분 주행, 리셋 간격 최대 3.47초.

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
