using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Deadline4Sec.AcceptanceTests
{
    public sealed class CoinPowerUpAcceptanceTests
    {
        private static Type TypeOf(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance).Invoke(target, args);
        private static T Read<T>(object target, string property) =>
            (T)target.GetType().GetProperty(property).GetValue(target);
        private static object Item(string name) => Enum.Parse(TypeOf("PowerUpType"), name);
        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(target);
        private static IEnumerator AdvanceGameSeconds(float seconds)
        {
            float until = Time.time + seconds;
            int frames = 0;
            while (Time.time < until && frames++ < 10000)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.Less(frames, 10000, "Game time did not advance in the test runner.");
        }

        [Test]
        public void BalancePickupsAndSavedMenusAreAuthorable()
        {
            Type balanceType = TypeOf("PowerUpBalance");
            Assert.IsNotNull(balanceType);
            ScriptableObject balance = (ScriptableObject)Resources.Load("PowerUps/PowerUpBalance", balanceType);
            Assert.IsNotNull(balance);
            Assert.AreEqual(500, Call(balance, "UpgradeCost", 1));
            Assert.AreEqual(15000, Call(balance, "UpgradeCost", 6));
            Assert.AreEqual(6f, (float)Call(balance, "Duration", Item("FreezeClock"), 7));
            Assert.AreEqual(8f, (float)Call(balance, "TimeHeartMaximum", 7));

            GameObject coin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Pickup/Coin.prefab");
            Assert.IsNotNull(coin);
            Assert.IsNotNull(coin.GetComponent(TypeOf("CoinPickup")));
            Assert.IsTrue(coin.GetComponent<SphereCollider>().isTrigger);
            Assert.IsTrue(coin.GetComponent<Rigidbody>().isKinematic);
            foreach (string name in Enum.GetNames(TypeOf("PowerUpType")))
            {
                GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefab/Pickup/" + name + "_Pickup.prefab");
                Assert.IsNotNull(pickup, name);
                Assert.IsNotNull(pickup.GetComponent(TypeOf("PowerUpPickup")));
                Assert.IsTrue(pickup.GetComponent<SphereCollider>().isTrigger);
                Assert.IsTrue(pickup.GetComponent<Rigidbody>().isKinematic);
            }

            foreach (string path in new[] { "Assets/Scenes/EndlessRun.unity",
                "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity" })
            {
                EditorSceneManager.OpenScene(path);
                GameObject flow = GameObject.Find("GameManager");
                Assert.IsNotNull(flow, path);
                Assert.IsNotNull(flow.GetComponent(TypeOf("CoinWallet")), path);
                Assert.IsNotNull(flow.GetComponent(TypeOf("PowerUpManager")), path);
                Assert.IsNotNull(flow.GetComponent(TypeOf("UpgradeMenu")), path);
                Transform canvas = GameObject.Find("Canvas").transform;
                Assert.IsNotNull(canvas.Find("TitlePanel/CoinBalanceText"), path);
                Assert.IsNotNull(canvas.Find("TitlePanel/UpgradeButton"), path);
                Assert.IsNotNull(canvas.Find("UpgradePanel/CoinBalanceText"), path);
                Assert.AreEqual(6, canvas.Find("UpgradePanel").GetComponentsInChildren(
                    TypeOf("PowerUpUpgradeRow"), true).Length, path);
                foreach (Component row in canvas.Find("UpgradePanel").GetComponentsInChildren(
                    TypeOf("PowerUpUpgradeRow"), true))
                    Assert.AreEqual("Upgrade", row.transform.Find("UpgradeButton")
                        .GetComponent<Button>().onClick.GetPersistentMethodName(0), path);
                Button open = canvas.Find("TitlePanel/UpgradeButton").GetComponent<Button>();
                Assert.AreEqual("OpenUpgrades", open.onClick.GetPersistentMethodName(0), path);
                Button close = canvas.Find("UpgradePanel/BackButton").GetComponent<Button>();
                Assert.AreEqual("CloseUpgrades", close.onClick.GetPersistentMethodName(0), path);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator TitleOpensAnEditableUpgradeScreenAndReturns()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            yield return null;
            Transform canvas = GameObject.Find("Canvas").transform;
            Component flow = GameObject.Find("GameManager").GetComponent(TypeOf("GameFlowManager"));
            Button open = canvas.Find("TitlePanel/UpgradeButton").GetComponent<Button>();
            open.onClick.Invoke();
            Assert.AreEqual("Upgrades", Read<object>(flow, "State").ToString());
            Assert.IsTrue(canvas.Find("UpgradePanel").gameObject.activeSelf);
            Assert.IsFalse(canvas.Find("TitlePanel").gameObject.activeSelf);
            Component firstRow = canvas.Find("UpgradePanel/ItemScrollView/Viewport/Content/FreezeClockRow")
                .GetComponent(TypeOf("PowerUpUpgradeRow"));
            Assert.IsNotNull(firstRow);
            Component effectLabel = firstRow.transform.Find("EffectText").GetComponent(
                Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro"));
            Assert.IsFalse(string.IsNullOrEmpty(Read<string>(effectLabel, "text")));
            SceneFlowAcceptanceTests.CaptureIntroFrame(Camera.main, "upgrade-menu");
            canvas.Find("UpgradePanel/BackButton").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("Title", Read<object>(flow, "State").ToString());
            Assert.IsTrue(canvas.Find("TitlePanel").gameObject.activeSelf);
            yield return new ExitPlayMode();
        }

        [Test]
        public void CoinBankingAndUpgradeSaveArePersistentAndBounded()
        {
            const string coinKey = "TotalCoins";
            const string levelKey = "ItemLevel_FreezeClock";
            bool hadCoins = PlayerPrefs.HasKey(coinKey);
            bool hadLevel = PlayerPrefs.HasKey(levelKey);
            int oldCoins = PlayerPrefs.GetInt(coinKey);
            int oldLevel = PlayerPrefs.GetInt(levelKey);
            GameObject go = null;
            try
            {
                PlayerPrefs.SetInt(coinKey, 1000);
                PlayerPrefs.SetInt(levelKey, 1);
                go = new GameObject("Wallet Test");
                Component wallet = go.AddComponent(TypeOf("CoinWallet"));
                Call(wallet, "Awake");
                Call(wallet, "BeginRun");
                Call(wallet, "Collect", 3);
                Assert.AreEqual(3, Read<int>(wallet, "CurrentRunCoins"));
                Call(wallet, "BankRunCoins");
                Call(wallet, "BankRunCoins");
                Assert.AreEqual(1003, Read<int>(wallet, "TotalCoins"));
                Assert.AreEqual(1003, PlayerPrefs.GetInt(coinKey));
                go.AddComponent(TypeOf("GameTimer"));
                Component manager = go.AddComponent(TypeOf("PowerUpManager"));
                Call(manager, "Awake");
                Assert.IsTrue((bool)Call(manager, "TryUpgrade", Item("FreezeClock")));
                Assert.AreEqual(2, PlayerPrefs.GetInt(levelKey));
                Assert.AreEqual(503, PlayerPrefs.GetInt(coinKey));
                Assert.IsFalse((bool)Call(manager, "TryUpgrade", Item("FreezeClock")));
                Assert.AreEqual(2, PlayerPrefs.GetInt(levelKey));
                PlayerPrefs.SetInt(levelKey, 7);
                Assert.IsFalse((bool)Call(manager, "TryUpgrade", Item("FreezeClock")));
                Assert.AreEqual(503, PlayerPrefs.GetInt(coinKey));
            }
            finally
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
                if (hadCoins) PlayerPrefs.SetInt(coinKey, oldCoins);
                else PlayerPrefs.DeleteKey(coinKey);
                if (hadLevel) PlayerPrefs.SetInt(levelKey, oldLevel);
                else PlayerPrefs.DeleteKey(levelKey);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator FreezeAmplifierAndTimeHeartKeepCoreTimerAndScoreRules()
        {
            yield return new EnterPlayMode();
            GameObject systems = new GameObject("Power-Up Test Systems");
            Component timer = systems.AddComponent(TypeOf("GameTimer"));
            systems.AddComponent(TypeOf("CoinWallet"));
            Component manager = systems.AddComponent(TypeOf("PowerUpManager"));
            Component run = systems.AddComponent(TypeOf("RunManager"));
            Call(timer, "BeginRun");
            yield return null;
            Call(manager, "Activate", Item("FreezeClock"));
            float frozenAt = Read<float>(timer, "RemainingTime");
            yield return AdvanceGameSeconds(0.15f);
            Assert.That(Read<float>(timer, "RemainingTime"), Is.EqualTo(frozenAt).Within(0.02f));
            Call(manager, "Activate", Item("SoulAmplifier"));
            Call(run, "RecordNearMiss");
            Assert.AreEqual(300, Read<int>(run, "CurrentScore"));
            Call(manager, "Activate", Item("TimeHeart"));
            Assert.AreEqual(4.5f, Read<float>(timer, "MaximumTime"));
            Assert.AreEqual(4.5f, Read<float>(timer, "RemainingTime"));
            Call(timer, "ResetTimer");
            Assert.AreEqual(4.5f, Read<float>(timer, "RemainingTime"));
            Call(manager, "ResetRunEffects");
            Assert.AreEqual(4f, Read<float>(timer, "MaximumTime"));
            Assert.AreEqual(4f, Read<float>(timer, "RemainingTime"));
            yield return AdvanceGameSeconds(0.1f);
            Assert.Less(Read<float>(timer, "RemainingTime"), 4f);
            UnityEngine.Object.Destroy(systems);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RushMagnetAndComboSealResolveAndCleanUp()
        {
            yield return new EnterPlayMode();
            GameObject systems = new GameObject("Power-Up Integration Systems");
            Component timer = systems.AddComponent(TypeOf("GameTimer"));
            Component wallet = systems.AddComponent(TypeOf("CoinWallet"));
            Component manager = systems.AddComponent(TypeOf("PowerUpManager"));
            Component run = systems.AddComponent(TypeOf("RunManager"));
            GameObject runner = new GameObject("Power-Up Test Runner");
            CharacterController playerCollider = runner.AddComponent<CharacterController>();
            Component player = runner.AddComponent(TypeOf("PlayerController"));
            ((MonoBehaviour)player).enabled = false;
            SetField(manager, "player", player);
            SetField(manager, "playerCollider", playerCollider);
            SetField(run, "playerController", player);
            GameObject hazard = new GameObject("Rush Obstacle");
            BoxCollider obstacleCollider = hazard.AddComponent<BoxCollider>();
            hazard.AddComponent(TypeOf("Obstacle"));
            hazard.transform.position = new Vector3(0, 1, 5);
            Call(timer, "BeginRun");
            yield return null;

            Call(manager, "Activate", Item("ReaperRush"));
            Assert.IsTrue(Physics.GetIgnoreCollision(playerCollider, obstacleCollider));
            Assert.That(Read<float>(manager, "SpeedMultiplier"), Is.GreaterThan(1f));
            GameObject target = new GameObject("Rush Enemy");
            target.transform.position = new Vector3(0, 1, 2);
            target.AddComponent<BoxCollider>();
            Component enemy = target.AddComponent(TypeOf("Enemy"));
            Call(player, "TryReaperRushKill", enemy);
            Assert.IsTrue(Read<bool>(enemy, "IsKilled"));
            Assert.AreEqual(100, Read<int>(run, "CurrentScore"));
            Call(manager, "ResetRunEffects");
            Assert.IsFalse(Physics.GetIgnoreCollision(playerCollider, obstacleCollider));
            Assert.AreEqual(1f, Read<float>(manager, "SpeedMultiplier"));

            SetField(run, "comboTimeout", 0.2f);
            Call(run, "RecordNearMiss");
            int combo = Read<int>(run, "CurrentCombo");
            Call(manager, "Activate", Item("ComboSeal"));
            yield return AdvanceGameSeconds(0.3f);
            Assert.AreEqual(combo, Read<int>(run, "CurrentCombo"));
            Call(manager, "ResetRunEffects");
            yield return AdvanceGameSeconds(0.25f);
            Assert.AreEqual(0, Read<int>(run, "CurrentCombo"),
                "running=" + Read<bool>(timer, "IsRunning") +
                " runTime=" + Read<float>(run, "CurrentRunTime") +
                " sealed=" + Read<bool>(manager, "ComboSealed") +
                " elapsed=" + (Time.time - Field<float>(run, "lastSuccessTime")));

            GameObject coin = new GameObject("Magnet Coin");
            coin.transform.position = new Vector3(3.5f, 1f, 0);
            SphereCollider coinCollider = coin.AddComponent<SphereCollider>();
            coinCollider.isTrigger = true;
            Rigidbody body = coin.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            coin.AddComponent(TypeOf("CoinPickup"));
            int scoreBeforeCoin = Read<int>(run, "CurrentScore");
            Call(manager, "Activate", Item("SoulMagnet"));
            yield return AdvanceGameSeconds(0.35f);
            Assert.AreEqual(1, Read<int>(wallet, "CurrentRunCoins"));
            Assert.AreEqual(scoreBeforeCoin, Read<int>(run, "CurrentScore"));
            UnityEngine.Object.Destroy(coin);
            UnityEngine.Object.Destroy(hazard);
            UnityEngine.Object.Destroy(runner);
            UnityEngine.Object.Destroy(systems);
            yield return new ExitPlayMode();
        }
    }
}
