using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Deadline4Sec.AcceptanceTests
{
    // The exhibition admin's whole-save reset. It really wipes this machine's
    // editor PlayerPrefs, so it only runs when named on purpose: export
    // HKCU\Software\Unity\UnityEditor\DefaultCompany\Deadline-4Sec first and
    // import it again afterwards.
    [Explicit("Wipes the editor PlayerPrefs")]
    public sealed class ExhibitionWipeTests
    {
        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");
        private static object S(Type type, string member, params object[] args)
        {
            MethodInfo method = type.GetMethod(member, BindingFlags.Public | BindingFlags.Static);
            if (method != null)
                return method.Invoke(null, args);
            return type.GetProperty(member, BindingFlags.Public | BindingFlags.Static).GetValue(null);
        }

        private string folder;

        // Batch runs without a filter run [Explicit] tests too (2026-11-13): this
        // one also needs DEADLINE_ALLOW_PREFS_WIPE=1 in the environment.
        [SetUp]
        public void UseTempFolder()
        {
            if (Environment.GetEnvironmentVariable("DEADLINE_ALLOW_PREFS_WIPE") != "1")
                Assert.Ignore("Set DEADLINE_ALLOW_PREFS_WIPE=1 (and back up the PlayerPrefs) to run.");
            folder = Path.Combine(Path.GetTempPath(), "deadline-exhibition-" + Guid.NewGuid().ToString("N"));
            S(T("ExhibitionLeaderboardService"), "UseFolderForTests", folder);
        }

        [TearDown]
        public void Restore()
        {
            S(T("ExhibitionLeaderboardService"), "UseFolderForTests", new object[] { null });
            if (folder != null && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        // Wipes this machine's editor save: runs only when asked for by name
        // (export the PlayerPrefs registry key first and import it afterwards).
        [Test]
        public void FullResetWipesTheSave()
        {
            S(T("ExhibitionLeaderboardService"), "Register", "NUNI", 100);
            PlayerPrefs.SetInt("TotalCoins", 777);
            PlayerPrefs.SetInt("Currency_FrostShard", 9);
            PlayerPrefs.SetInt("ItemLevel_SoulMagnet", 5);
            PlayerPrefs.SetInt("Deadline4Sec.TutorialCompleted", 1);
            Type stats = T("PlayerStats");
            object stat = Enum.GetValues(T("PlayerStat")).GetValue(0);
            PlayerPrefs.SetInt((string)stats.GetMethod("KeyOf").Invoke(null, new[] { stat }), 42);
            stats.GetMethod("ReloadFromSave").Invoke(null, null);
            Assert.AreEqual(42, (int)stats.GetMethod("Get").Invoke(null, new[] { stat }));

            T("ExhibitionUI").GetMethod("WipeSavedData").Invoke(null, null);
            foreach (string key in new[] { "TotalCoins", "Currency_FrostShard", "ItemLevel_SoulMagnet", "Deadline4Sec.TutorialCompleted" })
                Assert.IsFalse(PlayerPrefs.HasKey(key), key + " wiped");
            Assert.AreEqual(0, (int)stats.GetMethod("Get").Invoke(null, new[] { stat }), "Stats read again from the empty save.");
            Assert.AreEqual(0, ((System.Collections.ICollection)S(T("ExhibitionLeaderboardService"), "Entries")).Count, "The exhibition ranking too.");
        }
    }
}
