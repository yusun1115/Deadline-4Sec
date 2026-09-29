using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class GameExtrasAcceptanceTests
    {
        private static Type TypeOf(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance).Invoke(target, args);
        private static T Read<T>(object target, string name) =>
            (T)target.GetType().GetProperty(name).GetValue(target);
        private static object EnumValue(string type, string name) => Enum.Parse(TypeOf(type), name);
        private static readonly string[] SavedKeys = {
            "TotalCoins", "EquippedWeaponSkin", "EquippedConsumable", "Consumable_Shield_Count",
            "Consumable_Dash_Count", "ReviveCouponCount", "SkinOwned_NeonReaper",
            "SkinOwned_FrostMoon", "SkinOwned_BloodMoon", "SkinOwned_CyberDeath",
            "SkinOwned_Eclipse", "Currency_FrostShard", "Currency_BloodShard",
            "Currency_CyberCore", "Currency_EclipseFragment", "PendingGiftReward",
            "PendingGiftRewardAmount", "PendingGiftRewardCurrency", "PendingGiftRewardApplied" };

        private sealed class PrefsScope : IDisposable
        {
            private readonly Dictionary<string, int?> previous = new Dictionary<string, int?>();
            public PrefsScope()
            {
                foreach (string key in SavedKeys)
                {
                    previous[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                    PlayerPrefs.DeleteKey(key);
                }
                PlayerPrefs.Save();
            }
            public void Dispose()
            {
                foreach (var pair in previous)
                {
                    if (pair.Value.HasValue)
                        PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
                    else
                        PlayerPrefs.DeleteKey(pair.Key);
                }
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void CatalogGiftPrefabAndEditableScenesHaveExpectedContent()
        {
            Type catalogType = TypeOf("GameExtrasCatalog");
            ScriptableObject catalog = (ScriptableObject)Resources.Load("GameExtras/GameExtrasCatalog", catalogType);
            Assert.IsNotNull(catalog);
            Array skins = Read<Array>(catalog, "Skins");
            Assert.AreEqual(6, skins.Length);
            int[] counts = new int[7];
            for (int ticket = 0; ticket < 100; ticket++)
                counts[(int)Call(catalog, "RollRewardIndex", ticket)]++;
            CollectionAssert.AreEqual(new[] { 25, 10, 20, 8, 15, 15, 7 }, counts);
            GameObject gift = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Pickup/GiftBox.prefab");
            Assert.IsNotNull(gift);
            Assert.IsNotNull(gift.GetComponent(TypeOf("GiftBoxPickup")));
            Assert.IsTrue(gift.GetComponent<SphereCollider>().isTrigger);
            Assert.IsTrue(gift.GetComponent<Rigidbody>().isKinematic);
            GameObject pattern = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Pattern/Pattern_A_LaneAttack.prefab");
            Assert.IsNotNull(pattern.transform.Find("GiftBox_01"));
            foreach (string path in new[] { "Assets/Scenes/EndlessRun.unity",
                "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity" })
            {
                EditorSceneManager.OpenScene(path);
                Transform canvas = GameObject.Find("Canvas").transform;
                GameObject manager = GameObject.Find("GameManager");
                Assert.IsNotNull(manager.GetComponent(TypeOf("RunInventory")), path);
                Assert.IsNotNull(manager.GetComponent(TypeOf("GameExtrasUI")), path);
                Assert.AreEqual(6, canvas.Find("UpgradePanel").GetComponentsInChildren(
                    TypeOf("SkinShopRow"), true).Length, path);
                Assert.AreEqual(2, canvas.Find("UpgradePanel").GetComponentsInChildren(
                    TypeOf("ConsumableShopRow"), true).Length, path);
                Assert.IsNotNull(canvas.Find("RevivePanel/CouponButton"), path);
                Assert.IsNotNull(canvas.Find("GiftOpeningPanel/OpenButton"), path);
                Assert.IsNotNull(canvas.Find("GameplayUI/GiftBoxCountText"), path);
                Assert.IsNotNull(canvas.Find("ResultPanel/ConfirmButton"), path);
                Assert.IsNotNull(GameObject.Find("Player").transform.Find("WeaponVisual"), path);
                Assert.AreEqual("ConfirmResult", canvas.Find("ResultPanel/ConfirmButton")
                    .GetComponent<Button>().onClick.GetPersistentMethodName(0), path);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void SkinOwnershipCurrencyAndPendingRewardPersistWithoutReroll()
        {
            using (new PrefsScope())
            {
                PlayerPrefs.SetInt("TotalCoins", 30000);
                PlayerPrefs.SetInt("Currency_FrostShard", 30);
                GameObject go = new GameObject("Inventory Test");
                try
                {
                    Component wallet = go.AddComponent(TypeOf("CoinWallet"));
                    Component inventory = go.AddComponent(TypeOf("RunInventory"));
                    Call(wallet, "Awake");
                    Call(inventory, "Awake");
                    Assert.IsTrue((bool)Call(inventory, "TryBuyOrEquipSkin", 1));
                    Assert.AreEqual(22000, PlayerPrefs.GetInt("TotalCoins"));
                    Assert.IsTrue((bool)Call(inventory, "TryBuyOrEquipSkin", 2));
                    Assert.AreEqual(0, PlayerPrefs.GetInt("Currency_FrostShard"));
                    Assert.AreEqual(2, Read<int>(inventory, "EquippedSkinIndex"));
                    Assert.IsFalse((bool)Call(inventory, "TryBuyOrEquipSkin", 5));
                    Assert.AreEqual(2, Read<int>(inventory, "EquippedSkinIndex"));
                    PlayerPrefs.SetInt("PendingGiftReward", 4);
                    PlayerPrefs.SetInt("PendingGiftRewardAmount", 1);
                    PlayerPrefs.SetInt("PendingGiftRewardApplied", 0);
                    Assert.AreEqual("SHIELD +1", Call(inventory, "ApplyPendingReward"));
                    Assert.AreEqual("SHIELD +1", Call(inventory, "ApplyPendingReward"));
                    Assert.AreEqual(1, PlayerPrefs.GetInt("Consumable_Shield_Count"));
                    Assert.IsTrue(Read<bool>(inventory, "HasPendingReward"));
                    Call(inventory, "ClearPendingReward");
                    Assert.IsFalse(Read<bool>(inventory, "HasPendingReward"));
                    Assert.AreEqual(22000, Read<int>(wallet, "TotalCoins"));
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
        }

        [UnityTest]
        public IEnumerator ShieldDoesNotStopTimerAndDashIsNotInvulnerable()
        {
            using (new PrefsScope())
            {
                yield return new EnterPlayMode();
                GameObject go = new GameObject("Consumable Systems");
                Component timer = go.AddComponent(TypeOf("GameTimer"));
                go.AddComponent(TypeOf("CoinWallet"));
                Component inventory = go.AddComponent(TypeOf("RunInventory"));
                PlayerPrefs.SetInt("Consumable_Shield_Count", 1);
                Call(inventory, "EquipConsumable", EnumValue("ConsumableKind", "Shield"));
                Call(timer, "BeginRun");
                Assert.IsTrue((bool)Call(inventory, "TryUseEquipped"));
                Assert.IsTrue(Read<bool>(inventory, "ShieldActive"));
                Call(timer, "TriggerFatalContact");
                Assert.IsFalse(Read<bool>(timer, "IsGameOver"));
                Assert.IsFalse(Read<bool>(inventory, "ShieldActive"));
                Call(timer, "TriggerGameOver");
                Assert.IsTrue(Read<bool>(timer, "IsGameOver"), "Timer expiry must bypass Shield.");
                UnityEngine.Object.Destroy(go);

                GameObject dashGo = new GameObject("Dash Systems");
                Component dashTimer = dashGo.AddComponent(TypeOf("GameTimer"));
                dashGo.AddComponent(TypeOf("CoinWallet"));
                Component dashInventory = dashGo.AddComponent(TypeOf("RunInventory"));
                PlayerPrefs.SetInt("Consumable_Dash_Count", 1);
                Call(dashInventory, "EquipConsumable", EnumValue("ConsumableKind", "Dash"));
                Call(dashTimer, "BeginRun");
                Assert.IsTrue((bool)Call(dashInventory, "TryUseEquipped"));
                Assert.Greater(Read<float>(dashInventory, "DashBonusSpeed"), 0f);
                Call(dashTimer, "TriggerFatalContact");
                Assert.IsTrue(Read<bool>(dashTimer, "IsGameOver"), "Dash must not grant invulnerability.");
                UnityEngine.Object.Destroy(dashGo);
                yield return new ExitPlayMode();
            }
        }

        [UnityTest]
        public IEnumerator ShieldIgnoresTheSameLingeringContactButNotAnotherHazard()
        {
            using (new PrefsScope())
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                yield return new EnterPlayMode();
                GameObject runner = new GameObject("Shield Test Player");
                CharacterController body = runner.AddComponent<CharacterController>();
                body.center = new Vector3(0, 1, 0);
                ((MonoBehaviour)runner.AddComponent(TypeOf("PlayerController"))).enabled = false;
                GameObject systems = new GameObject("Shield Contact Systems");
                Component timer = systems.AddComponent(TypeOf("GameTimer"));
                systems.AddComponent(TypeOf("CoinWallet"));
                Component inventory = systems.AddComponent(TypeOf("RunInventory"));
                GameObject first = GameObject.CreatePrimitive(PrimitiveType.Cube);
                first.transform.position = new Vector3(0, 1, 0);
                first.AddComponent(TypeOf("Enemy"));
                GameObject sameEnemyZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sameEnemyZone.transform.SetParent(first.transform, false);
                sameEnemyZone.transform.localPosition = Vector3.zero;
                GameObject second = GameObject.CreatePrimitive(PrimitiveType.Cube);
                second.transform.position = new Vector3(0, 1, 0);
                PlayerPrefs.SetInt("Consumable_Shield_Count", 1);
                Call(inventory, "EquipConsumable", EnumValue("ConsumableKind", "Shield"));
                Call(timer, "BeginRun");
                Assert.IsTrue((bool)Call(inventory, "TryUseEquipped"));
                Call(timer, "TriggerFatalContactFrom", first.GetComponent<Collider>());
                Assert.IsFalse(Read<bool>(timer, "IsGameOver"));
                float until = Time.time + 0.4f;
                while (Time.time < until)
                    yield return null;
                Call(timer, "TriggerFatalContactFrom", first.GetComponent<Collider>());
                Assert.IsFalse(Read<bool>(timer, "IsGameOver"), "The same continuous hit must not kill again.");
                Call(timer, "TriggerFatalContactFrom", sameEnemyZone.GetComponent<Collider>());
                Assert.IsFalse(Read<bool>(timer, "IsGameOver"), "Body and attack zone of one enemy are one hit.");
                Call(timer, "TriggerFatalContactFrom", second.GetComponent<Collider>());
                Assert.IsTrue(Read<bool>(timer, "IsGameOver"), "A different hazard must remain fatal.");
                UnityEngine.Object.Destroy(first);
                UnityEngine.Object.Destroy(second);
                UnityEngine.Object.Destroy(systems);
                UnityEngine.Object.Destroy(runner);
                yield return new ExitPlayMode();
            }
        }

        [UnityTest]
        public IEnumerator DeathReviveResultAndGiftBoxFlow()
        {
            using (new PrefsScope())
            {
                PlayerPrefs.SetInt("ReviveCouponCount", 1);
                EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
                yield return new EnterPlayMode();
                GameObject manager = GameObject.Find("GameManager");
                Component flow = manager.GetComponent(TypeOf("GameFlowManager"));
                Component timer = manager.GetComponent(TypeOf("GameTimer"));
                Component inventory = manager.GetComponent(TypeOf("RunInventory"));
                Call(inventory, "BeginRun");
                Call(timer, "BeginRun");
                Call(timer, "TriggerFatalContact");
                float deadline = Time.realtimeSinceStartup + 3f;
                while (Read<object>(flow, "State").ToString() != "RevivePrompt" && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.AreEqual("RevivePrompt", Read<object>(flow, "State").ToString());
                Call(flow, "UseCouponRevive");
                Assert.AreEqual("Playing", Read<object>(flow, "State").ToString());
                ((MonoBehaviour)GameObject.Find("Player").GetComponent(TypeOf("PlayerController"))).enabled = false;
                Assert.AreEqual(0, PlayerPrefs.GetInt("ReviveCouponCount"));
                Assert.That(Read<float>(timer, "RemainingTime"), Is.EqualTo(4f).Within(0.05f));
                Call(inventory, "CollectGiftBox");
                // Protection expires before the second fatal hit.
                deadline = Time.realtimeSinceStartup + 2.5f;
                while (Read<bool>(inventory, "CollisionProtected") && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Call(timer, "TriggerFatalContact");
                deadline = Time.realtimeSinceStartup + 3f;
                while (Read<object>(flow, "State").ToString() != "Result" && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.AreEqual("Result", Read<object>(flow, "State").ToString());
                Call(flow, "ConfirmResult");
                Assert.AreEqual("GiftBoxes", Read<object>(flow, "State").ToString());
                Call(flow, "OpenOrContinueGiftBox");
                Assert.IsTrue(Read<bool>(inventory, "HasPendingReward"));
                Call(flow, "OpenOrContinueGiftBox");
                Assert.IsFalse(Read<bool>(inventory, "HasPendingReward"));
                yield return new ExitPlayMode();
            }
        }

        [UnityTest]
        public IEnumerator GiftPickupAndDoubleTapKeepTimerScoreAndSwipeRules()
        {
            using (new PrefsScope())
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                yield return new EnterPlayMode();
                GameObject systems = new GameObject("Input Test Systems");
                Component timer = systems.AddComponent(TypeOf("GameTimer"));
                systems.AddComponent(TypeOf("CoinWallet"));
                Component inventory = systems.AddComponent(TypeOf("RunInventory"));
                Component run = systems.AddComponent(TypeOf("RunManager"));
                GameObject runner = new GameObject("Input Test Player");
                Collider playerCollider = runner.AddComponent<CharacterController>();
                Component player = runner.AddComponent(TypeOf("PlayerController"));
                ((MonoBehaviour)player).enabled = false;
                GameObject inputObject = new GameObject("Input Reader");
                Component input = inputObject.AddComponent(TypeOf("MobileSwipeInput"));
                Call(timer, "BeginRun");
                float beforeTime = Read<float>(timer, "RemainingTime");
                GameObject gift = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefab/Pickup/GiftBox.prefab"));
                Call(gift.GetComponent(TypeOf("GiftBoxPickup")), "OnTriggerEnter", playerCollider);
                Assert.AreEqual(1, Read<int>(inventory, "CurrentRunGiftBoxes"));
                Assert.AreEqual(0, Read<int>(run, "CurrentScore"));
                Assert.That(Read<float>(timer, "RemainingTime"), Is.EqualTo(beforeTime).Within(0.02f));

                PlayerPrefs.SetInt("Consumable_Shield_Count", 1);
                Call(inventory, "EquipConsumable", EnumValue("ConsumableKind", "Shield"));
                Call(input, "BeginMouse", new Vector2(20, 20));
                Call(input, "EndMouse");
                Call(input, "BeginMouse", new Vector2(22, 21));
                Call(input, "EndMouse");
                Assert.AreEqual(0, PlayerPrefs.GetInt("Consumable_Shield_Count"));
                PlayerPrefs.SetInt("Consumable_Dash_Count", 1);
                Call(inventory, "EquipConsumable", EnumValue("ConsumableKind", "Dash"));
                Call(input, "BeginMouse", new Vector2(20, 20));
                Call(input, "EvaluateSwipe", new Vector2(150, 20));
                Call(input, "EndMouse");
                Call(input, "BeginMouse", new Vector2(20, 20));
                Call(input, "EvaluateSwipe", new Vector2(150, 20));
                Call(input, "EndMouse");
                Assert.AreEqual(1, PlayerPrefs.GetInt("Consumable_Dash_Count"));
                UnityEngine.Object.Destroy(gift);
                UnityEngine.Object.Destroy(inputObject);
                UnityEngine.Object.Destroy(runner);
                UnityEngine.Object.Destroy(systems);
                yield return new ExitPlayMode();
            }
        }
    }
}
