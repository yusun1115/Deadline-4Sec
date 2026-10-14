using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Assigns the imported Casual Game Sounds clips and the Sound/BGM music to
    // the SoundLibrary and sets mobile-friendly import settings. Picks were made
    // from each clip's length, envelope and pitch contour; swap any slot in the
    // SoundLibrary inspector if a different take fits better.
    public static class AudioSetup
    {
        private const string SfxFolder = "Assets/Casual Game Sounds U6/CasualGameSounds/";
        private const string LibraryPath = "Assets/Resources/Audio/SoundLibrary.asset";

        [MenuItem("Deadline 4 Sec/Audio/Assign Imported Sounds To Library")]
        public static void Assign()
        {
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.laneMove = Sfx(47);     // short airy swish
            library.jump = Sfx(7);          // quick rising blip
            library.slide = Sfx(19);        // soft noise swell
            library.fastFall = Sfx(39);     // falling whistle
            library.homing = Sfx(41);       // fast rising zip
            library.kill = Sfx(3);          // punchy hit
            library.stomp = Sfx(14);        // low thump
            library.groundSlam = Sfx(9);    // long low boom
            library.nearMiss = Sfx(34);     // bright swish
            library.timerWarning = Sfx(15); // steady beep
            library.gameOver = Sfx(24);     // descending fail
            library.coin = SoulClip();      // Soul pickup (2026-10-11: retro coin)
            library.powerUp = Sfx(8);       // rising power-up
            library.uiClick = Sfx(21);      // tight click
            library.titleMusic = MusicAt(MainScreenBgm);
            library.runMusic = MusicAt(InGameBgm);
            library.sfxVolume = 0.85f;
            library.musicVolume = 0.45f;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        // Soul pickup sound only; the other slots are left as they are.
        [MenuItem("Deadline 4 Sec/Audio/Assign Soul Pickup Sound")]
        public static void AssignSoulSound()
        {
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
                throw new System.InvalidOperationException(LibraryPath + " is missing.");
            library.coin = SoulClip();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        // Title and in-game music (2026-10-11: Sound/BGM).
        private const string MainScreenBgm = "Assets/Sound/BGM/BGM_MainScreen.mp3";
        private const string InGameBgm = "Assets/Sound/BGM/BGM_InGame.mp3";

        [MenuItem("Deadline 4 Sec/Audio/Assign BGM")]
        public static void AssignBgm()
        {
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(LibraryPath);
            if (library == null)
                throw new System.InvalidOperationException(LibraryPath + " is missing.");
            library.titleMusic = MusicAt(MainScreenBgm);
            library.runMusic = MusicAt(InGameBgm);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        private static AudioClip SoulClip() => Sfx("Assets/Sound/SFX/driken5482-retro-coin-4-236671.mp3");

        private static AudioClip Sfx(int number) => Sfx(SfxFolder + "DM-CGS-" + number.ToString("00") + ".wav");

        private static AudioClip Sfx(string path)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                importer.forceToMono = true;
                importer.loadInBackground = false;
                AudioImporterSampleSettings s = importer.defaultSampleSettings;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = true;
                importer.defaultSampleSettings = s;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static AudioClip MusicAt(string path)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                importer.loadInBackground = true;
                AudioImporterSampleSettings s = importer.defaultSampleSettings;
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.55f;
                s.preloadAudioData = false;
                importer.defaultSampleSettings = s;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
