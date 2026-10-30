using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Weapon skins (2026-10-30): the renamed catalog, one weapon model per
    // skin held exactly where the built-in scythe is, a particle aura on every
    // skin except the default, the swing-trail colour, and the shop icons.
    public sealed class WeaponSkinAcceptanceTests
    {
        private static readonly string[] Names = { "NEON REAPER", "STARLIGHT", "FROST BITE", "BLOOD MOON", "CYBER DEATH", "ECLIPSE" };
        private static readonly string[] Ids = { "DefaultScythe", "NeonReaper", "FrostMoon", "BloodMoon", "CyberDeath", "Eclipse" };

        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static object Field(object target, string name) => target.GetType().GetField(name).GetValue(target);
        private static Array Skins() => (Array)T("GameExtrasCatalog").GetProperty("Skins")
            .GetValue(Resources.Load("GameExtras/GameExtrasCatalog", T("GameExtrasCatalog")));

        [Test]
        public void CatalogHasTheRenamedSkinsWithModelsEffectsAndIcons()
        {
            Array skins = Skins();
            Assert.AreEqual(6, skins.Length);
            for (int i = 0; i < skins.Length; i++)
            {
                object skin = skins.GetValue(i);
                Assert.AreEqual(Ids[i], Field(skin, "id"), "Ids stay (saves).");
                Assert.AreEqual(Names[i], Field(skin, "displayName"));
                Assert.IsNotNull(Field(skin, "icon"), Names[i] + " icon");
                var prefab = (GameObject)Field(skin, "weaponPrefab");
                if (i == 0)
                {
                    Assert.IsNull(prefab, "The default keeps the built-in scythe, no effect.");
                    continue;
                }
                Assert.IsNotNull(prefab, Names[i] + " model");
                Assert.IsNotNull(prefab.GetComponentInChildren<MeshRenderer>(), Names[i] + " renders");
                Component fx = prefab.GetComponent(T("WeaponSkinFx"));
                Assert.IsNotNull(fx, Names[i] + " effect");
                Assert.AreNotEqual("None", fx.GetType().GetProperty("Kind").GetValue(fx).ToString(), Names[i] + " has an effect");
                Material material = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                Assert.AreEqual("Universal Render Pipeline/Unlit", material.shader.name, "URP material");
                Assert.IsNotNull(material.GetTexture("_BaseMap"), Names[i] + " texture");
            }
        }

        [UnityTest]
        public IEnumerator EquippedSkinReplacesTheHeldScytheAndShowsItsEffect()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            yield return null;
            int saved = PlayerPrefs.GetInt("EquippedWeaponSkin", 0);
            try
            {
                Component inventory = (Component)UnityEngine.Object.FindFirstObjectByType(T("RunInventory"));
                MethodInfo apply = inventory.GetType().GetMethod("ApplySkinVisual", BindingFlags.Instance | BindingFlags.NonPublic);
                Component player = (Component)UnityEngine.Object.FindFirstObjectByType(T("PlayerController"));
                Renderer builtIn = null;
                foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
                    if (t.parent != null && t.parent.name == "WeaponSocket" && t.GetComponent<MeshRenderer>() != null)
                        builtIn = t.GetComponent<MeshRenderer>();
                Assert.IsNotNull(builtIn, "Built-in scythe under the hand socket.");
                Bounds held = builtIn.bounds;
                PropertyInfo accent = T("WeaponSkinVisual").GetProperty("CurrentAccent");

                for (int i = 1; i < 6; i++)
                {
                    PlayerPrefs.SetInt("EquippedWeaponSkin", i);
                    apply.Invoke(inventory, null);
                    yield return null;
                    yield return null;
                    Assert.IsFalse(builtIn.enabled, Names[i] + ": built-in scythe hidden");
                    Assert.AreEqual(1, CountSkinWeapons(builtIn.transform), Names[i] + ": one skin weapon in the hand");
                    Transform weapon = SkinWeapon(builtIn.transform);
                    Renderer model = weapon.GetComponentInChildren<MeshRenderer>();
                    Assert.IsTrue(model.enabled);
                    // Same hand, same size: the skin sits where the scythe was.
                    Assert.Less(Vector3.Distance(model.bounds.center, held.center), 0.6f, Names[i] + " in the hand");
                    Assert.That(model.bounds.size.magnitude / held.size.magnitude, Is.InRange(0.6f, 1.4f), Names[i] + " size");
                    ParticleSystem[] systems = weapon.GetComponentsInChildren<ParticleSystem>();
                    Assert.GreaterOrEqual(systems.Length, 3, Names[i] + " effect layers");
                    foreach (ParticleSystem system in systems)
                        Assert.IsTrue(system.isPlaying, Names[i] + " " + system.name + " playing");
                    Color skinColour = (Color)Field(Skins().GetValue(i), "accentColor");
                    Assert.AreEqual(skinColour, (Color)accent.GetValue(null), Names[i] + " trail colour");
                }

                PlayerPrefs.SetInt("EquippedWeaponSkin", 0);
                apply.Invoke(inventory, null);
                yield return null;
                Assert.IsTrue(builtIn.enabled, "Default: the built-in scythe again.");
                Assert.AreEqual(0, CountSkinWeapons(builtIn.transform), "Skin weapon removed.");
            }
            finally
            {
                PlayerPrefs.SetInt("EquippedWeaponSkin", saved);
                PlayerPrefs.Save();
            }
            yield return new ExitPlayMode();
        }

        // Skin shop (2026-10-30): card tap previews in the hand with the tag,
        // BUY asks first (아니오 buys nothing, 예 buys + equips), short Soul offers
        // the currency shop, a short skin currency locks the card, and leaving
        // the screen shows the equipped skin again. Prefs are restored.
        [UnityTest]
        public IEnumerator SkinShopPreviewsConfirmsAndSendsShortSoulToTheCurrencyShop()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            yield return null;
            string[] keys = { "TotalCoins", "EquippedWeaponSkin", "SkinOwned_NeonReaper", "SkinOwned_FrostMoon", "SkinOwned_BloodMoon",
                "SkinOwned_CyberDeath", "SkinOwned_Eclipse", "Currency_FrostShard", "Currency_BloodShard", "Currency_CyberCore",
                "Currency_EclipseFragment" };
            var saved = new System.Collections.Generic.Dictionary<string, int?>();
            foreach (string key in keys)
                saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            try
            {
                foreach (string key in keys)
                    if (key.StartsWith("SkinOwned_") || key.StartsWith("Currency_"))
                        PlayerPrefs.DeleteKey(key);
                PlayerPrefs.SetInt("EquippedWeaponSkin", 0);
                PlayerPrefs.SetInt("Currency_FrostShard", 30);      // FROST BITE: buyable (currency only)
                PlayerPrefs.SetInt("Currency_EclipseFragment", 25); // ECLIPSE: currency ok, Soul short
                Component wallet = (Component)UnityEngine.Object.FindFirstObjectByType(T("CoinWallet"));
                int coins = (int)wallet.GetType().GetProperty("TotalCoins").GetValue(wallet);
                wallet.GetType().GetMethod("TrySpend").Invoke(wallet, new object[] { coins, false });

                Component flow = (Component)UnityEngine.Object.FindFirstObjectByType(T("GameFlowManager"));
                float guard = Time.realtimeSinceStartup + 6f;
                while (flow.GetType().GetProperty("State").GetValue(flow).ToString() != "Title" && Time.realtimeSinceStartup < guard)
                    yield return null;
                flow.GetType().GetMethod("OpenSkins").Invoke(flow, null);
                yield return null;
                yield return null;
                Component shop = (Component)UnityEngine.Object.FindFirstObjectByType(T("SkinShopUI"));
                Assert.IsNotNull(shop, "Skin shop screen open.");
                Component Row(int index)
                {
                    foreach (Component row in shop.GetComponentsInChildren(T("SkinShopRow"), true))
                        if ((int)row.GetType().GetProperty("SkinIndex").GetValue(row) == index)
                            return row;
                    return null;
                }
                string State(int index) { Component row = Row(index); return row.GetType().GetProperty("State").GetValue(row).ToString(); }
                object Read(Component c, string p) => c.GetType().GetProperty(p).GetValue(c);
                GameObject Tag() => (GameObject)shop.GetType().GetField("previewTag", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shop);
                Transform Hand()
                {
                    Component player = (Component)UnityEngine.Object.FindFirstObjectByType(T("PlayerController"));
                    foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
                        if (t.name.StartsWith("WeaponSkin_"))
                            return t;
                    return null;
                }

                Assert.AreEqual("Equipped", State(0));
                Assert.AreEqual("Buyable", State(2), "FROST BITE: currency 30 / 30.");
                Assert.AreEqual("Locked", State(3), "BLOOD MOON: currency short.");
                Assert.AreEqual("SoulShort", State(5), "ECLIPSE: Soul short only.");
                Assert.AreEqual("SoulShort", State(1), "STARLIGHT: Soul short.");
                UnityEngine.UI.Button LockedButton = (UnityEngine.UI.Button)Row(3).GetType().GetField("actionButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Row(3));
                Assert.IsFalse(LockedButton.interactable, "Locked card: BUY off.");

                // Tap = preview in the hand + tag; nothing is equipped.
                Row(1).GetType().GetMethod("Preview").Invoke(Row(1), null);
                yield return null;
                Assert.AreEqual(1, Read(shop, "PreviewIndex"));
                Assert.IsTrue(Tag().activeSelf, "Preview tag shown.");
                Assert.IsNotNull(Hand(), "Previewed weapon in the hand.");
                Assert.AreEqual("WeaponSkin_STARLIGHT", Hand().name);
                Assert.AreEqual(0, PlayerPrefs.GetInt("EquippedWeaponSkin"), "Preview does not equip.");

                // BUY asks first; 아니오 buys nothing.
                MethodInfo action = Row(2).GetType().GetMethod("BuyOrEquip");
                action.Invoke(Row(2), null);
                Assert.IsTrue((bool)Read(shop, "DialogOpen"), "Purchase question.");
                Component message = (Component)shop.GetType().GetField("dialogMessage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(shop);
                string Message() => (string)message.GetType().GetProperty("text").GetValue(message);
                Assert.AreEqual("구매하시겠습니까?", Message());
                Assert.AreEqual(2, Read(shop, "PreviewIndex"), "The weapon being bought is shown.");
                shop.GetType().GetMethod("ConfirmNo").Invoke(shop, null);
                Assert.IsFalse((bool)Read(shop, "DialogOpen"));
                Assert.AreEqual(0, PlayerPrefs.GetInt("SkinOwned_FrostMoon", 0), "아니오: nothing bought.");
                // 예 buys and equips.
                action.Invoke(Row(2), null);
                shop.GetType().GetMethod("ConfirmYes").Invoke(shop, null);
                yield return null;
                Assert.AreEqual(1, PlayerPrefs.GetInt("SkinOwned_FrostMoon", 0), "예: bought.");
                Assert.AreEqual(2, PlayerPrefs.GetInt("EquippedWeaponSkin"), "Bought skin equipped.");
                Assert.AreEqual("Equipped", State(2));
                Assert.AreEqual("Owned", State(0));
                Assert.AreEqual(-1, Read(shop, "PreviewIndex"));
                Assert.IsFalse(Tag().activeSelf);

                // Short Soul: offer the currency shop; 예 opens it.
                Row(5).GetType().GetMethod("BuyOrEquip").Invoke(Row(5), null);
                Assert.IsTrue((bool)Read(shop, "DialogOpen"));
                StringAssert.Contains("Soul이 부족합니다", Message());
                shop.GetType().GetMethod("ConfirmYes").Invoke(shop, null);
                yield return null;
                Component currencyShop = (Component)UnityEngine.Object.FindFirstObjectByType(T("CurrencyShopPanel"));
                Assert.IsNotNull(currencyShop, "Currency shop opened.");
                Assert.IsTrue(currencyShop.gameObject.activeInHierarchy);
                Assert.AreEqual(0, PlayerPrefs.GetInt("SkinOwned_Eclipse", 0), "Nothing bought.");
                currencyShop.GetType().GetMethod("Close").Invoke(currencyShop, null);

                // Tapping an owned card equips it at once (no preview).
                Row(0).GetType().GetMethod("Preview").Invoke(Row(0), null);
                yield return null;
                Assert.AreEqual(0, PlayerPrefs.GetInt("EquippedWeaponSkin"), "Owned card: worn straight away.");
                Assert.AreEqual(-1, Read(shop, "PreviewIndex"));
                Assert.IsFalse(Tag().activeSelf);
                Assert.AreEqual("Equipped", State(0));
                Row(2).GetType().GetMethod("Preview").Invoke(Row(2), null);
                yield return null;
                Assert.AreEqual(2, PlayerPrefs.GetInt("EquippedWeaponSkin"));
                Assert.AreEqual("WeaponSkin_FROSTBITE", Hand() != null ? Hand().name : "none");

                // Leaving the screen shows the equipped skin (no preview left).
                Row(1).GetType().GetMethod("Preview").Invoke(Row(1), null);
                yield return null;
                Assert.AreEqual("WeaponSkin_STARLIGHT", Hand() != null ? Hand().name : "none", "Not owned: preview.");
                flow.GetType().GetMethod("CloseSkins").Invoke(flow, null);
                yield return null;
                yield return null;
                Assert.AreEqual("WeaponSkin_FROSTBITE", Hand() != null ? Hand().name : "none", "Equipped skin back in the hand.");
            }
            finally
            {
                foreach (var pair in saved)
                    if (pair.Value.HasValue)
                        PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
                    else
                        PlayerPrefs.DeleteKey(pair.Key);
                PlayerPrefs.Save();
            }
            yield return new ExitPlayMode();
        }

        private static int CountSkinWeapons(Transform slot)
        {
            int count = 0;
            foreach (Transform child in slot)
                if (child.name.StartsWith("WeaponSkin_"))
                    count++;
            return count;
        }

        private static Transform SkinWeapon(Transform slot)
        {
            foreach (Transform child in slot)
                if (child.name.StartsWith("WeaponSkin_"))
                    return child;
            return null;
        }
    }
}
