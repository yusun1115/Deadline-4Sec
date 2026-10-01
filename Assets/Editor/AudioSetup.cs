using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Assigns the imported Casual Game Sounds / Demonic Dynamic Music clips to
    // the SoundLibrary and sets mobile-friendly import settings. Picks were made
    // from each clip's length, envelope and pitch contour; swap any slot in the
    // SoundLibrary inspector if a different take fits better.
    public static class AudioSetup
    {
        private const string SfxFolder = "Assets/Casual Game Sounds U6/CasualGameSounds/";
        private const string MusicFolder = "Assets/Demonic Dynamic Music pack/Assets/";
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
            library.coin = Sfx(27);         // bright ding
            library.powerUp = Sfx(8);       // rising power-up
            library.uiClick = Sfx(21);      // tight click
            library.titleMusic = Music("whispers.wav");
            library.runMusic = Music("demonic level 3 raw.wav");
            library.sfxVolume = 0.85f;
            library.musicVolume = 0.45f;
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
        }

        private static AudioClip Sfx(int number)
        {
            string path = SfxFolder + "DM-CGS-" + number.ToString("00") + ".wav";
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

        private static AudioClip Music(string file)
        {
            string path = MusicFolder + file;
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
