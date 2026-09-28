using System;
using System.IO;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Two-process PC verification. The second process always restores the
    // original preferences; a failed first test restores them immediately.
    public static class PcPersistenceProbe
    {
        [Serializable] private sealed class Entry
        {
            public string key;
            public bool exists, isFloat;
            public int integer;
            public float number;
        }
        [Serializable] private sealed class Snapshot { public Entry[] entries; }
        private static readonly string[] Keys = {
            "Deadline4Sec.BestScore", "Deadline4Sec.BestDistance", "Deadline4Sec.BestCombo",
            "Deadline4Sec.SoundEnabled", "Deadline4Sec.VibrationEnabled"
        };
        private static string Root
        {
            get
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Path.GetFileName(root) == "ValidationProject" ? Path.GetDirectoryName(root) : root;
            }
        }
        private static string Baseline => Path.Combine(Root, "Verification", "pc-prefs-baseline.json");
        private static string Expected => Path.Combine(Root, "Verification", "pc-prefs-expected.json");

        public static void Prepare()
        {
            Require(!File.Exists(Baseline), "Restore the previous persistence probe before starting another.");
            Directory.CreateDirectory(Path.GetDirectoryName(Baseline));
            File.WriteAllText(Baseline, JsonUtility.ToJson(Capture(), true));
            bool sound = GamePreferences.SoundEnabled, vibration = GamePreferences.VibrationEnabled;
            for (int i = 0; i < 3; i++) PlayerPrefs.DeleteKey(Keys[i]);
            GamePreferences.SoundEnabled = !sound;
            GamePreferences.VibrationEnabled = !vibration;
            PlayerPrefs.Save();
        }

        public static void CaptureExpected(float distance)
        {
            Require(PlayerPrefs.GetInt(Keys[0]) == 100, "An actual public-input kill must save best score 100.");
            Require(PlayerPrefs.GetInt(Keys[2]) == 1, "The actual kill must save best combo 1.");
            Require(distance > 0f && Mathf.Abs(PlayerPrefs.GetFloat(Keys[1]) - distance) < 0.001f,
                "The actual run distance must be saved.");
            File.WriteAllText(Expected, JsonUtility.ToJson(Capture(), true));
        }

        public static void VerifyAndRestore()
        {
            bool passed = false;
            try
            {
                Require(File.Exists(Baseline) && File.Exists(Expected), "Missing first-process persistence snapshot.");
                Snapshot expected = JsonUtility.FromJson<Snapshot>(File.ReadAllText(Expected));
                Require(Matches(expected), "Preferences changed across the Unity process restart.");
                passed = true;
            }
            finally
            {
                Restore();
                File.WriteAllText(Path.Combine(Root, "Verification", "pc-persistence.txt"),
                    DateTime.UtcNow.ToString("O") + "\n" + (passed ? "PASS" : "FAIL") +
                    ": best score, best distance, best combo, sound and vibration settings across two Unity editor processes.\n" +
                    "Original preference keys/values restored. Android app persistence was not tested.\n");
            }
        }

        public static void Restore()
        {
            if (!File.Exists(Baseline)) return;
            Snapshot baseline = JsonUtility.FromJson<Snapshot>(File.ReadAllText(Baseline));
            foreach (Entry entry in baseline.entries)
                if (!entry.exists) PlayerPrefs.DeleteKey(entry.key);
                else if (entry.isFloat) PlayerPrefs.SetFloat(entry.key, entry.number);
                else PlayerPrefs.SetInt(entry.key, entry.integer);
            PlayerPrefs.Save();
            Require(Matches(baseline), "Original preferences were not restored.");
            File.Delete(Baseline);
            if (File.Exists(Expected)) File.Delete(Expected);
        }

        private static Snapshot Capture()
        {
            Snapshot snapshot = new Snapshot { entries = new Entry[Keys.Length] };
            for (int i = 0; i < Keys.Length; i++) snapshot.entries[i] = new Entry {
                key = Keys[i], exists = PlayerPrefs.HasKey(Keys[i]), isFloat = i == 1,
                integer = i == 1 ? 0 : PlayerPrefs.GetInt(Keys[i]),
                number = i == 1 ? PlayerPrefs.GetFloat(Keys[i]) : 0f
            };
            return snapshot;
        }
        private static bool Matches(Snapshot snapshot)
        {
            foreach (Entry entry in snapshot.entries)
                if (PlayerPrefs.HasKey(entry.key) != entry.exists || entry.exists && (entry.isFloat ?
                    Mathf.Abs(PlayerPrefs.GetFloat(entry.key) - entry.number) >= 0.001f :
                    PlayerPrefs.GetInt(entry.key) != entry.integer)) return false;
            return true;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
