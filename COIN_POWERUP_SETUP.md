# 코인·파워업·업그레이드 편집 안내

## 씬에서 직접 수정할 위치

`Assets/Scenes/EndlessRun.unity`를 연 뒤 Hierarchy에서 아래 오브젝트를 편집한다. `GrayboxCourse.unity`와 `TestScene.unity`에도 같은 구조가 저장되어 있다.

| 용도 | Hierarchy 경로 |
| --- | --- |
| 메인 화면 보유 코인 | `Canvas/TitlePanel/CoinBalanceText` |
| 메인 화면 하단 버튼 | `Canvas/TitlePanel/UpgradeButton` 및 자식 `Label` |
| 업그레이드 화면 배경·제목 | `Canvas/UpgradePanel` 및 `HeadingText` |
| 업그레이드 화면 보유 코인 | `Canvas/UpgradePanel/CoinBalanceText` |
| 스크롤 가능한 6종 카드 | `Canvas/UpgradePanel/ItemScrollView/Viewport/Content/*Row` |
| 업그레이드 화면 뒤로 | `Canvas/UpgradePanel/BackButton` |
| 플레이 중 런 코인·효과 시간 | `Canvas/GameplayUI/RunCoinText`, `Canvas/GameplayUI/ActivePowerUpsText` |
| 코인·아이템 관리 컴포넌트 | `GameManager`의 `CoinWallet`, `PowerUpManager`, `UpgradeMenu` |

버튼의 `Button > On Click()` 연결은 씬에 저장되어 있다. `UpgradeButton`은 `GameFlowManager.OpenUpgrades`, `BackButton`은 `CloseUpgrades`, 각 카드의 버튼은 해당 `PowerUpUpgradeRow.Upgrade`를 호출한다. 텍스트·색·크기·위치는 Inspector에서 바꿀 수 있다. 새 텍스트는 GFCRedSpirit-Bold SDF를 사용한다.

## 코인과 아이템 프리팹 배치

1. Project 창에서 `Assets/Prefab/Pattern/`의 패턴 프리팹을 연다.
2. `Assets/Prefab/Pickup/Coin.prefab` 또는 아래 6종 프리팹을 패턴 루트의 자식으로 드래그한다. `Assets/Prefab/Pattern/Pattern_A`~`E`에는 예시 배치가 이미 저장되어 있다.
3. Scene 뷰에서 레인 X는 왼쪽 `-2.5`, 가운데 `0`, 오른쪽 `2.5`를 기준으로 위치를 조정한다. 점프 경로는 Y를 높여 배치한다.
4. 코인은 루트 `CoinPickup > Amount`로 획득량을 바꾼다. 콜라이더의 `Is Trigger`와 Rigidbody의 `Is Kinematic`을 유지한다.
5. 아이템 프리팹 루트의 `PowerUpPickup > Item Type`을 선택한다. 6종 프리팹은 각각 이미 설정돼 있다. 동일한 공통 구조를 복제해 다른 위치에도 배치할 수 있다.

| 프리팹 | Item Type / 효과 |
| --- | --- |
| `FreezeClock_Pickup` | `FreezeClock` / 타이머 감소 정지 |
| `SoulAmplifier_Pickup` | `SoulAmplifier` / 점수 x2 |
| `ReaperRush_Pickup` | `ReaperRush` / 빠른 무적 질주 |
| `SoulMagnet_Pickup` | `SoulMagnet` / 주변 코인 흡인 |
| `ComboSeal_Pickup` | `ComboSeal` / 콤보 만료 정지 |
| `TimeHeart_Pickup` | `TimeHeart` / 일시적 최대 타이머 확장 |

픽업은 기존 적/장애물/기회 간격 검사를 대신하지 않는다. 위험 판정 경로와 겹치지 않는지 패턴을 실제 플레이로 확인한다.

## 밸런스와 참조

`Assets/Resources/PowerUps/PowerUpBalance.asset` 하나에서 `Upgrade Costs`, 6종 레벨별 지속시간, `Time Heart Maximum`, `Time Heart Duration`, `Reaper Rush Speed Multiplier`, `Magnet Radius`, `Magnet Pull Speed`를 수정한다. 배열 순서는 Lv.1에서 Lv.7까지다. 비용 배열은 현재 레벨 Lv.1→Lv.2부터 Lv.6→Lv.7 순서로 `500, 1000, 2000, 4000, 8000, 15000`이다.

`GameManager > PowerUpManager`의 Inspector 참조는 다음과 같다.

| 필드 | 드래그할 오브젝트 |
| --- | --- |
| `Balance` | `Assets/Resources/PowerUps/PowerUpBalance.asset` |
| `Game Timer` | 같은 `GameManager`의 `GameTimer` 컴포넌트 |
| `Wallet` | 같은 `GameManager`의 `CoinWallet` 컴포넌트 |
| `Player` | Hierarchy의 플레이어 `PlayerController` 컴포넌트 |

`GameManager > UpgradeMenu`의 `Title Coin Text`, `Upgrade Coin Text`, `Gameplay Coin Text`, `Active Effects Text`, `Rows[0..5]`도 씬에 연결되어 있다. UI를 복제하거나 다른 씬으로 옮길 때 이 참조와 `GameFlowManager > Upgrade Panel`을 확인한다. 씬 자동 준비 메뉴는 `Deadline 4 Sec > Update Title And Tutorial Menus`이며, 기존 오브젝트가 있으면 재사용한다.

## 저장 데이터와 게임 오버

PlayerPrefs 키는 보유 코인 `TotalCoins`, 아이템 레벨 `ItemLevel_FreezeClock`, `ItemLevel_SoulAmplifier`, `ItemLevel_ReaperRush`, `ItemLevel_SoulMagnet`, `ItemLevel_ComboSeal`, `ItemLevel_TimeHeart`다. 기본 레벨은 모두 1, 최대는 7이다. 플레이 중 획득 코인은 `CurrentRunCoins`에만 쌓이고, 게임 오버에서 한 번 `TotalCoins`로 옮겨 저장한다. 재시도는 런 코인과 효과를 초기화하고 보유 코인·레벨은 유지한다.

## PC 확인 순서

1. 타이틀 상단 `COIN`과 하단 `UPGRADES` 버튼을 확인한다. 버튼을 눌러 6종 카드와 뒤로 가기를 확인한다.
2. 테스트를 빠르게 진행하려면 Play 모드에서 `GameManager > CoinWallet` 컴포넌트 메뉴의 `Debug/Add 1000 Total Coins`를 실행한다. 이 명령은 Unity Editor 전용이며 실제 PlayerPrefs 보유 코인을 늘린다.
3. 업그레이드 비용 차감, 레벨/효과 갱신, 코인 부족 시 실패, Lv.7 MAX 잠금을 확인한다. Play 모드를 다시 시작한 후에도 값이 유지되는지 확인한다.
4. 일반 런에서 코인을 먹고 HUD 런 코인 증가, 점수·콤보·타이머 불변을 확인한다. 게임 오버 후 타이틀로 돌아와 보유 코인 합산, 재시도 시 런 코인 0을 확인한다.
5. 각 프리팹을 패턴에 배치해 효과/남은 시간, 같은 종류 재획득 시 지속시간 갱신, 다른 종류의 동시 사용을 확인한다.
6. Freeze 중 적 처치/점수 진행, Amplifier와 콤보 배율, Rush 중 적/장애물과 타이머 0 사망, Magnet 범위 안팎, Seal 종료 후 콤보 만료, Heart 종료 후 4초 복귀를 확인한다.
7. 게임 오버·재시도 이후 모든 아이템 효과가 사라지고 Stomp·Slam·장애물 윗면 판정이 기존대로 유지되는지 확인한다.

PC 자동 결과는 `Verification/coin-powerup-results.xml`에 있다. Android 실기기 설치·입력·성능은 아직 검증하지 않았다.
