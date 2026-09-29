# 스킨·선물 상자·소비 아이템·부활 시스템

## 하이어라키에서 직접 수정할 곳

`Assets/Scenes/EndlessRun.unity`를 열어 아래 오브젝트를 선택한다. 같은 구조가 `GrayboxCourse.unity`, `TestScene.unity`에도 저장되어 있다.

| 화면/오브젝트 | 하이어라키 경로 | 직접 수정할 내용 |
| --- | --- | --- |
| 무기 외형 | `Player/WeaponVisual/Staff`, `Player/WeaponVisual/Blade` | 플레이스홀더 메시의 크기·위치. 최종 모델로 교체 가능 |
| 스킨·소비 아이템 상점 | `Canvas/UpgradePanel/ItemScrollView/Viewport/Content` | 기존 6종 업그레이드 아래 스킨 6행, 소비 아이템 2행의 배치·색·문구 |
| 부활창 | `Canvas/RevivePanel` | 문구·색·버튼·카운트다운 위치 |
| 선물 상자 개봉창 | `Canvas/GiftOpeningPanel` | 상자 그림, 보상 텍스트, 열기/확인 버튼 |
| 결과 확인 | `Canvas/ResultPanel/ConfirmButton` | 결과에서 상자 개봉으로 넘어가는 버튼 |
| 런 HUD | `Canvas/GameplayUI/GiftBoxCountText`, `ConsumableStatusText` | 상자 수와 장착 아이템 표시 |
| 선물 상자 배치 | `Assets/Prefab/Pattern/Pattern_A_LaneAttack.prefab`, `Pattern_E_MixedRisk.prefab`의 `GiftBox_01` | 위치·수량. 다른 패턴에도 `GiftBox.prefab`을 직접 끌어다 놓을 수 있음 |

게임 내 새 텍스트는 `GameFont.Apply`를 거쳐 `GFCRedSpirit-Bold`를 사용한다. `GameExtrasSetup`은 최초 배치/누락 오브젝트 보강용이며, 존재하는 UI의 스타일은 다시 만들지 않는다.

## 데이터와 저장

- `Assets/Resources/GameExtras/GameExtrasCatalog.asset`: 스킨 ID·표시명·코인 가격·전용 재화·가격·머티리얼, 선물 상자 보상 종류·수량·확률을 Inspector에서 수정한다. 스킨 6종(기본 포함), 보상 가중치 `25/10/20/8/15/15/7`의 합계는 100이다. 스킨은 무기 외형 머티리얼만 바꾸며 게임 수치를 변경하지 않는다.
- `Assets/Art/Materials/WeaponSkins`: 교체 가능한 플레이스홀더 머티리얼. `Player/WeaponVisual`의 렌더러 둘에 장착 스킨 머티리얼을 적용한다.
- `Assets/Prefab/Pickup/GiftBox.prefab`: Trigger SphereCollider와 Kinematic Rigidbody가 있는 수집 프리팹. 획득은 `CurrentRunGiftBoxes`만 올리며 타이머·점수·콤보를 바꾸지 않는다.
- `RunInventory`: 구매·장착·보유 수량·선물 보상·부활권·런 전용 상태를 관리한다. `GameManager` Inspector에서 Catalog, Wallet, Weapon Visual 참조를 확인할 수 있다.
- `GameExtrasUI`: 저장된 UI의 참조만 연결하고 수량·상태 문구를 갱신한다. 버튼은 씬에 영구 리스너로 연결되어 있다.

영구 `PlayerPrefs` 키: `TotalCoins`, `SkinOwned_<SkinId>`, `EquippedWeaponSkin`, `Currency_FrostShard`, `Currency_BloodShard`, `Currency_CyberCore`, `Currency_EclipseFragment`, `Consumable_Shield_Count`, `Consumable_Dash_Count`, `EquippedConsumable`, `ReviveCouponCount`. 인덱스는 Catalog의 스킨 배열 순서를 쓰므로, 출시 후 순서를 바꿀 때는 저장 데이터 이전이 필요하다. `CurrentRunGiftBoxes`·현재 런 점수/코인·부활 사용 여부는 새 런에서 초기화한다.

상자를 여는 순간 선택한 보상은 `PendingGiftReward`, `PendingGiftRewardAmount`, `PendingGiftRewardCurrency`, `PendingGiftRewardApplied`에 먼저 기록한다. 지급과 적용 표시를 함께 저장하고, 앱을 다시 열면 미완료 보상을 개봉 화면에서 재표시한다. 다음 상자로 넘어갈 때 보류 키를 제거하므로 같은 상자를 무한 재추첨할 수 없다. 전용 재화는 미구매 스킨에 필요한 종류만 뽑고, 모두 구매했으면 코인 300개로 바꾼다.

## 입력·충돌·화면 흐름

`MobileSwipeInput`은 한 손가락의 짧은 탭 두 번을 0.32초 안에 받으면 장착 아이템을 사용한다. 이동 거리가 작은 탭만 후보로 삼고, Swipe 또는 UI 버튼 입력은 탭 연속성을 지운다. PC에서는 마우스 두 번 클릭으로 확인할 수 있다. `Shield`는 적/장애물의 다음 치명 접촉 한 번을 막지만 타이머 0에는 적용되지 않는다. `Dash`는 Inspector의 거리/시간으로 전방 속도를 잠시 더하고, 기존 충돌·공격 판정은 유지한다. 효과가 끝나면 현재 자동 달리기 속도로 돌아온다.

적/장애물 치명타는 `GameTimer.TriggerFatalContactFrom()`으로 충돌 Collider를 전달한다. 여기서 부활 직후 보호 또는 방패를 검사한 뒤 기존 게임 오버를 호출한다. 방패가 깨진 직후 같은 적의 몸체/공격 구역 또는 같은 장애물과 접촉이 이어지는 동안은 중복 사망을 막지만, 다른 위험은 즉시 치명적이다. 타이머 0은 `TriggerGameOver()`를 직접 호출한다. 사망하면 `GameFlowManager`의 `Dying`에서 카메라 피드백과 임시 넘어짐 포즈를 0.75초 보여 주고, 첫 사망은 `RevivePrompt`로 이동한다. 쿠폰 부활은 수량을 1 차감하고 타이머를 4.00으로 복구하며 1.75초 동안 재사망만 막는다. 이 보호는 적 처치·점수를 제공하지 않는다. 광고 버튼은 `Rewarded Ad not implemented yet` 로그만 출력하며 SDK 연결은 없다. 두 번째 사망 또는 4초 만료/포기는 결과 화면으로 간다.

결과의 `CONFIRM`은 상자 0개면 타이틀로, 1개 이상이면 `GiftOpeningPanel`로 이동한다. 상자는 한 번 눌러 보상을 받고 다시 눌러 다음 상자로 간다. 모두 열면 `CONFIRM`으로 타이틀에 복귀한다. 기존 `RETRY`와 `MAIN MENU`도 상자를 획득했으면 먼저 개봉하도록 연결했다.

## 코드 위치

신규: `GameExtrasCatalog.cs`, `RunInventory.cs`, `WeaponSkinVisual.cs`, `GiftBoxPickup.cs`, `GameExtrasUI.cs`, `SkinShopRow.cs`, `ConsumableShopRow.cs`, `GameExtrasSetup.cs`, `GameExtrasAcceptanceTests.cs`.

수정: `CoinWallet.cs`(상자 코인 지급), `GameTimer.cs`(접촉 치명타/부활), `PlayerController.cs`(접촉·사망 포즈), `EnemyAttackZone.cs`(접촉 치명타), `RunManager.cs`(대시 추가 속도), `MobileSwipeInput.cs`(더블 탭), `GameFlowManager.cs`(사망·부활·상자 화면).

## PC 검증 순서

1. 타이틀의 `UPGRADES`에서 스킨 구매/장착, 소비 아이템 장착/해제, 재실행 후 저장 확인.
2. 패턴의 선물 상자 획득 시 HUD 수만 증가하고 타이머·점수·콤보는 유지되는지 확인.
3. 방패를 장착하고 두 번 탭 → 적/장애물 한 번 방어, 타이머 0은 사망하는지 확인.
4. 대시를 장착하고 두 번 탭 → 순간 전진 후 정상 속도 복귀, 충돌 시 사망하는지 확인.
5. 사망 연출 → 4초 부활창 → 쿠폰 소비와 4.00초 복귀, 보호 시간, 두 번째 사망 결과 이동 확인.
6. 쿠폰 없이 광고 버튼을 누르면 로그만 출력되는지 확인. 포기/4초 만료도 결과로 가야 한다.
7. 상자 없는 결과 확인은 타이틀, 상자 있는 결과 확인은 순차 개봉과 보상 저장 후 타이틀로 가는지 확인.
8. `Verification/game-extras-results.xml`과 전체 회귀 결과를 확인한다. Android 실기기 조작·성능은 현재 범위 밖이며 개발 APK까지만 제작한다.
