using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Currencyshop.md §40: rewarded ad +100 Soul only on success, 3 per local
    // day, dimmed when used up, back next day; debug IAP pays soul + bonus; the
    // three entries reach the same shop and Back returns where it came from;
    // one ad pays once. Soul, ad keys and the catalog's debug flags are restored.
    public sealed class CurrencyShopAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/EndlessRun.unity";
        private const string BackupKey = "Deadline4Sec.CurrencyShopTests.Backup";
        private static readonly string[] Keys = { "TotalCoins", "DailySoulAdDate", "DailySoulAdCount", "Deadline4Sec.TutorialCompleted" };

        private static Type T(string name)
        {
            Type type = Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
            Assert.IsNotNull(type, name);
            return type;
        }

        private static object S(string type, string member, params object[] args) =>
            T(type).GetMethod(member, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        private static object P(string type, string property) =>
            T(type).GetProperty(property, BindingFlags.Public | BindingFlags.Static).GetValue(null);

        private static ScriptableObject Catalog() => (ScriptableObject)P("CurrencyShopManager", "Catalog");
        private static void Set(string field, object value) => Catalog().GetType().GetField(field).SetValue(Catalog(), value);
        private static object Get(string field) => Catalog().GetType().GetField(field).GetValue(Catalog());
        private static int Soul() => (int)P("SoulBank", "TotalSoul");
        private static int AdsToday() => (int)P("CurrencyShopManager", "AdCountToday");
        private static int AdsLeft() => (int)P("CurrencyShopManager", "AdsRemaining");

        private static bool lastGranted;
        private static void OnAd(bool granted) => lastGranted = granted;
        private static void WatchAd() => S("CurrencyShopManager", "TryWatchSoulRewardAd", new Action<bool>(OnAd));

        private static void SetNow(DateTime now) =>
            T("DailyQuestManager").GetField("Now", BindingFlags.Public | BindingFlags.Static).SetValue(null, new Func<DateTime>(() => now));

        private static void RealClock() =>
            T("DailyQuestManager").GetField("Now", BindingFlags.Public | BindingFlags.Static).SetValue(null, new Func<DateTime>(() => DateTime.Now));

        [SetUp]
        public void SetUp()
        {
            if (UnityEditor.SessionState.GetString(BackupKey, "") == "")
            {
                var backup = new System.Text.StringBuilder();
                foreach (string key in Keys)
                    backup.Append(key).Append('\t').Append(!PlayerPrefs.HasKey(key) ? "-" : key == "DailySoulAdDate" ? "s" : "i")
                        .Append('\t').Append(key == "DailySoulAdDate" ? PlayerPrefs.GetString(key, "") : PlayerPrefs.GetInt(key, 0).ToString()).Append('\n');
                foreach (string field in new[] { "simulateRewardedAdForTesting", "simulatedAdSucceeds", "debugPurchaseInEditor" })
                    backup.Append("#").Append(field).Append('\t').Append(Get(field)).Append('\n');
                backup.Append("#simulatedResponseSeconds\t").Append(((float)Get("simulatedResponseSeconds")).ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
                UnityEditor.SessionState.SetString(BackupKey, backup.ToString());
            }
            PlayerPrefs.SetInt("TotalCoins", 1000);
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
            PlayerPrefs.DeleteKey("DailySoulAdDate");
            PlayerPrefs.DeleteKey("DailySoulAdCount");
            PlayerPrefs.Save();
            Set("simulateRewardedAdForTesting", true);
            Set("simulatedAdSucceeds", true);
            Set("debugPurchaseInEditor", true);
            Set("simulatedResponseSeconds", 0f);
        }

        [TearDown]
        public void TearDown()
        {
            RealClock();
            foreach (string line in UnityEditor.SessionState.GetString(BackupKey, "").Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 2)
                    continue;
                if (parts[0].StartsWith("#"))
                {
                    string field = parts[0].Substring(1);
                    if (field == "simulatedResponseSeconds")
                        Set(field, float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                    else
                        Set(field, parts[1] == "True");
                    continue;
                }
                if (parts.Length < 3)
                    continue;
                if (parts[1] == "-")
                    PlayerPrefs.DeleteKey(parts[0]);
                else if (parts[1] == "s")
                    PlayerPrefs.SetString(parts[0], parts[2]);
                else
                    PlayerPrefs.SetInt(parts[0], int.Parse(parts[2]));
            }
            UnityEditor.SessionState.EraseString(BackupKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void RewardedAdPaysOnlyOnSuccessThreeTimesADay()
        {
            Assert.AreEqual(3, AdsLeft());
            Set("simulatedAdSucceeds", false);
            WatchAd();
            Assert.IsFalse(lastGranted);
            Assert.AreEqual(1000, Soul(), "Closed early: no Soul.");
            Assert.AreEqual(0, AdsToday(), "Closed early: no count.");

            Set("simulatedAdSucceeds", true);
            WatchAd();
            Assert.IsTrue(lastGranted);
            Assert.AreEqual(1100, Soul());
            Assert.AreEqual(1, AdsToday());
            WatchAd();
            WatchAd();
            Assert.AreEqual(1300, Soul(), "Exactly +300 for three ads.");
            Assert.AreEqual(3, AdsToday());
            Assert.AreEqual(0, AdsLeft());
            WatchAd();
            Assert.IsFalse(lastGranted, "No fourth ad today.");
            Assert.AreEqual(1300, Soul());
        }

        [Test]
        public void AdCountSurvivesARestartAndResetsAtMidnight()
        {
            DateTime day = DateTime.Now.Date.AddHours(23).AddMinutes(50);
            SetNow(day);
            WatchAd();
            WatchAd();
            Assert.AreEqual(2, AdsToday());
            Assert.AreEqual(day.ToString("yyyy-MM-dd"), PlayerPrefs.GetString("DailySoulAdDate"));
            // "Restart": nothing is cached; the same day still reads 2.
            Assert.AreEqual(2, PlayerPrefs.GetInt("DailySoulAdCount"));
            Assert.AreEqual(2, AdsToday());

            SetNow(day.AddMinutes(15)); // next day 00:05
            Assert.AreEqual(0, AdsToday());
            Assert.AreEqual(3, AdsLeft());
            Assert.AreEqual(day.Date.AddDays(1).ToString("yyyy-MM-dd"), PlayerPrefs.GetString("DailySoulAdDate"));
        }

        [Test]
        public void DebugPurchasePaysSoulPlusBonus()
        {
            Array products = (Array)Catalog().GetType().GetField("products").GetValue(Catalog());
            object pack = null;
            foreach (object p in products)
                if ((int)p.GetType().GetField("bonusSoul").GetValue(p) > 0)
                    pack = p;
            Assert.IsNotNull(pack, "A product with a bonus exists.");
            int total = (int)pack.GetType().GetProperty("TotalSoul").GetValue(pack);
            Assert.AreEqual((int)pack.GetType().GetField("soulAmount").GetValue(pack) + (int)pack.GetType().GetField("bonusSoul").GetValue(pack), total);
            Type resultType = T("PurchaseResult");
            Delegate callback = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(resultType),
                typeof(CurrencyShopAcceptanceTests).GetMethod(nameof(OnPurchase), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(resultType));
            purchaseResult = null;
            S("CurrencyShopManager", "Purchase", pack, callback);
            Assert.AreEqual("Success", purchaseResult);
            Assert.AreEqual(1000 + total, Soul());
        }

        private static string purchaseResult;
        private static void OnPurchase<TResult>(TResult result) => purchaseResult = result.ToString();

        // Entries, Back targets, card texts, the dim and one payout per ad on the real scene.
        [UnityTest]
        public IEnumerator ShopOpensFromEveryEntryAndReturnsThere()
        {
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return Until(() => UnityEngine.Object.FindFirstObjectByType(T("GameFlowManager")) != null);
            Set("simulateRewardedAdForTesting", true);
            Set("simulatedAdSucceeds", true);
            Set("simulatedResponseSeconds", 0.3f);
            Component flow = (Component)UnityEngine.Object.FindFirstObjectByType(T("GameFlowManager"));
            Component shop = (Component)UnityEngine.Object.FindFirstObjectByType(T("CurrencyShopPanel"), FindObjectsInactive.Include);
            Transform canvas = GameObject.Find("Canvas").transform;

            yield return null;
            Assert.IsTrue(canvas.Find("TitlePanel/CurrencyshopButton/RedDot").gameObject.activeSelf, "Ad Soul left: title red dot on.");
            Click(canvas.Find("TitlePanel/CurrencyshopButton"));
            Assert.IsTrue(shop.gameObject.activeSelf);
            Assert.IsTrue(shop.transform.Find("Card_1/RedDot").gameObject.activeSelf, "Ad card red dot on.");
            Assert.IsFalse(shop.transform.Find("Card_2/RedDot").gameObject.activeSelf, "Only the ad card has one.");
            Assert.AreEqual("Title", Prop(shop, "PreviousShopContext").ToString());
            Assert.AreEqual(Soul().ToString(), Text(shop.transform.Find("SoulBox/AmountText")), "Top Soul = saved Soul.");
            Assert.AreEqual("X5,500", Text(shop.transform.Find("Card_3/AmountText")));
            Assert.AreEqual("+500", Text(shop.transform.Find("Card_3/BonusBadge/BonusText")));
            Assert.AreEqual("₩1,100", Text(shop.transform.Find("Card_2/BuyButton/PriceText")));
            Assert.AreEqual("FREE  3/3", Text(shop.transform.Find("Card_1/BuyButton/PriceText")));

            // One ad, two taps: one payout.
            int before = Soul();
            Click(shop.transform.Find("Card_1/BuyButton"));
            Click(shop.transform.Find("Card_1/BuyButton"));
            yield return Until(() => !(bool)P("CurrencyShopManager", "IsBusy"));
            Assert.AreEqual(before + 100, Soul());
            Click(shop.transform.Find("Card_1/BuyButton"));
            yield return Until(() => !(bool)P("CurrencyShopManager", "IsBusy"));
            Click(shop.transform.Find("Card_1/BuyButton"));
            yield return Until(() => !(bool)P("CurrencyShopManager", "IsBusy"));
            Assert.AreEqual(before + 300, Soul());
            Assert.IsTrue(shop.transform.Find("Card_1/DimOverlay").GetComponent<UnityEngine.UI.Image>().enabled, "Used up: dimmed.");
            Assert.IsFalse(Interactable(shop.transform.Find("Card_1/BuyButton")), "Used up: button off.");
            Assert.IsFalse(shop.transform.Find("Card_1/RedDot").gameObject.activeSelf, "Used up: card red dot off.");
            // No captured locals in lambdas (the closure would predate the domain reload).
            yield return Until(() => ShownSoulText() == Soul().ToString(), 3f);

            Click(shop.transform.Find("BackButton"));
            Assert.IsFalse(shop.gameObject.activeSelf);
            Assert.AreEqual("Title", Prop(flow, "State").ToString());
            Assert.IsFalse(canvas.Find("TitlePanel/CurrencyshopButton/RedDot").gameObject.activeSelf, "Used up: title red dot off.");

            flow.GetType().GetMethod("OpenUpgrades").Invoke(flow, null);
            Click(canvas.Find("UpgradePanel/CurrencyPlusBtn"));
            Assert.AreEqual("UpgradeShop", Prop(shop, "PreviousShopContext").ToString());
            Click(shop.transform.Find("BackButton"));
            Assert.AreEqual("Upgrades", Prop(flow, "State").ToString());
            Assert.IsTrue(canvas.Find("UpgradePanel").gameObject.activeSelf);
            Assert.AreEqual(Soul().ToString("N0", System.Globalization.CultureInfo.InvariantCulture),
                Text(canvas.Find("UpgradePanel/CoinBalanceText")), "Upgrade shop shows the new Soul at once.");

            flow.GetType().GetMethod("CloseUpgrades").Invoke(flow, null);
            flow.GetType().GetMethod("OpenSkins").Invoke(flow, null);
            Click(canvas.Find("SkinPanel/CurrencyPlusBtn"));
            Assert.AreEqual("SkinShop", Prop(shop, "PreviousShopContext").ToString());
            Click(shop.transform.Find("BackButton"));
            Assert.AreEqual("Skins", Prop(flow, "State").ToString());
            Assert.IsTrue(canvas.Find("SkinPanel").gameObject.activeSelf);
            yield return new ExitPlayMode();
        }

        private static string ShownSoulText()
        {
            var shop = (Component)UnityEngine.Object.FindFirstObjectByType(T("CurrencyShopPanel"), FindObjectsInactive.Include);
            return Text(shop.transform.Find("SoulBox/AmountText"));
        }

        private static object Prop(Component c, string name) => c.GetType().GetProperty(name).GetValue(c);

        private static string Text(Transform t)
        {
            Component text = t.GetComponent(Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro"));
            return (string)text.GetType().GetProperty("text").GetValue(text);
        }

        private static void Click(Transform t)
        {
            Component button = t.GetComponent("Button");
            var onClick = (UnityEngine.Events.UnityEvent)button.GetType().GetProperty("onClick").GetValue(button);
            onClick.Invoke();
        }

        private static bool Interactable(Transform t)
        {
            Component button = t.GetComponent("Button");
            return (bool)button.GetType().GetProperty("interactable").GetValue(button);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds = 6f)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < end)
            {
                System.Threading.Thread.Sleep(1);
                yield return null;
            }
            Assert.IsTrue(condition(), "Timed out waiting.");
        }
    }
}
