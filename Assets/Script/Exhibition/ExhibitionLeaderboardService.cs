using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Deadline4Sec
{
    [Serializable]
    public sealed class ExhibitionScoreEntry
    {
        public string nickname;
        public int score;
        public long timestamp;
    }

    [Serializable]
    public sealed class ExhibitionLeaderboardData
    {
        public List<ExhibitionScoreEntry> entries = new List<ExhibitionScoreEntry>();
    }

    // The exhibition's own ranking: a JSON file on this device, no network.
    // No link to GooglePlayService. One line per nickname (case and outer
    // spaces ignored), best score kept; ties: the earlier registration first.
    // Saved right after each registration, the previous file kept as a backup.
    public static class ExhibitionLeaderboardService
    {
        public const string FileName = "exhibition_ranking.json";
        public const string BackupName = "exhibition_ranking_backup.json";

        private static ExhibitionLeaderboardData data;
        private static string folderOverride;

        public static string Folder => folderOverride ?? Application.persistentDataPath;
        public static string FilePath => Path.Combine(Folder, FileName);
        public static string BackupPath => Path.Combine(Folder, BackupName);

        public static IReadOnlyList<ExhibitionScoreEntry> Entries
        {
            get
            {
                Load();
                return data.entries;
            }
        }

        // Tests keep their files apart (null: the real folder). Forces a reload.
        public static void UseFolderForTests(string folder)
        {
            folderOverride = folder;
            data = null;
        }

        public static void Reload() => data = null;

        public static string Normalize(string nickname) => (nickname ?? "").Trim();

        public static bool SameName(string a, string b) =>
            string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);

        // Empty when fine; otherwise the reason, for the entry screen.
        public static string Validate(string nickname)
        {
            ExhibitionSettings settings = ExhibitionMode.Settings;
            string name = Normalize(nickname);
            if (name.Length == 0)
                return "닉네임을 입력해 주세요";
            if (name.Length < settings.nicknameMinLength || name.Length > settings.nicknameMaxLength)
                return $"{settings.nicknameMinLength}~{settings.nicknameMaxLength}자로 입력해 주세요";
            foreach (char c in name)
                if (char.IsControl(c) || char.IsWhiteSpace(c))
                    return "공백 없이 입력해 주세요";
            if (settings.bannedWords != null)
                foreach (string banned in settings.bannedWords)
                    if (!string.IsNullOrWhiteSpace(banned) &&
                        name.IndexOf(banned.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                        return "사용할 수 없는 닉네임이에요";
            return "";
        }

        // Register → memory → sort → save. Returns the 1-based rank of the name.
        public static int Register(string nickname, int score)
        {
            Load();
            string name = Normalize(nickname);
            ExhibitionScoreEntry entry = data.entries.Find(e => SameName(e.nickname, name));
            if (entry == null)
            {
                entry = new ExhibitionScoreEntry { nickname = name, score = score, timestamp = Now() };
                data.entries.Add(entry);
            }
            else if (score > entry.score)
            {
                entry.score = score;
                entry.timestamp = Now();
            }
            Sort();
            int keep = Mathf.Max(10, ExhibitionMode.Settings.keepEntries);
            if (data.entries.Count > keep)
                data.entries.RemoveRange(keep, data.entries.Count - keep);
            Save();
            return RankOf(name);
        }

        // 1-based; 0 when the name is not (or no longer) on the board.
        public static int RankOf(string nickname)
        {
            Load();
            int index = data.entries.FindIndex(e => SameName(e.nickname, nickname));
            return index + 1;
        }

        public static ExhibitionScoreEntry Find(string nickname)
        {
            Load();
            return data.entries.Find(e => SameName(e.nickname, nickname));
        }

        // Admin only: the whole exhibition board, file and backup.
        public static void ClearExhibitionLeaderboard()
        {
            data = new ExhibitionLeaderboardData();
            TryDelete(FilePath);
            TryDelete(BackupPath);
            Debug.Log("Exhibition ranking cleared.");
        }

        private static void Load()
        {
            if (data != null)
                return;
            data = TryRead(FilePath) ?? TryRead(BackupPath) ?? new ExhibitionLeaderboardData();
            if (data.entries == null)
                data.entries = new List<ExhibitionScoreEntry>();
            data.entries.RemoveAll(e => e == null || string.IsNullOrWhiteSpace(e.nickname));
            Sort();
        }

        private static ExhibitionLeaderboardData TryRead(string path)
        {
            if (!File.Exists(path))
                return null;
            try
            {
                ExhibitionLeaderboardData read = JsonUtility.FromJson<ExhibitionLeaderboardData>(File.ReadAllText(path));
                if (read == null || read.entries == null)
                    throw new InvalidDataException("no entries");
                return read;
            }
            catch (Exception exception)
            {
                Debug.LogError("Exhibition ranking file unreadable (" + path + "): " + exception.Message);
                return null;
            }
        }

        // Written next to the file first; the old file becomes the backup.
        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(FilePath))
                    File.Copy(FilePath, BackupPath, true);
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
                File.Move(temp, FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogError("Exhibition ranking could not be saved: " + exception.Message);
            }
        }

        private static void Sort() => data.entries.Sort((a, b) =>
            a.score != b.score ? b.score.CompareTo(a.score) : a.timestamp.CompareTo(b.timestamp));

        private static long nowCounter;

        // Milliseconds; strictly increasing so same-millisecond ties keep order.
        private static long Now()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            nowCounter = Math.Max(nowCounter + 1, now);
            return nowCounter;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogError("Could not delete " + path + ": " + exception.Message);
            }
        }
    }
}
