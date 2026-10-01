using UnityEngine;

namespace Deadline4Sec
{
    // One place to drop in final audio. Any empty slot falls back to the
    // procedural placeholder tone, so the game always has feedback.
    [CreateAssetMenu(menuName = "Deadline 4 Sec/Sound Library", fileName = "SoundLibrary")]
    public sealed class SoundLibrary : ScriptableObject
    {
        public const string ResourcePath = "Audio/SoundLibrary";

        [Header("Movement")]
        public AudioClip laneMove;
        public AudioClip jump;
        public AudioClip slide;
        public AudioClip fastFall;

        [Header("Combat")]
        public AudioClip homing;
        public AudioClip kill;
        public AudioClip stomp;
        public AudioClip groundSlam;
        public AudioClip nearMiss;

        [Header("Run state")]
        public AudioClip timerWarning;
        public AudioClip gameOver;
        public AudioClip coin;
        public AudioClip powerUp;

        [Header("UI")]
        public AudioClip uiClick;

        [Header("Music (looped)")]
        public AudioClip titleMusic;
        public AudioClip runMusic;

        [Header("Mix")]
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        [Range(0f, 0.3f)] public float pitchVariation = 0.06f;

        private static SoundLibrary cached;
        private static bool loaded;

        public static SoundLibrary Instance
        {
            get
            {
                if (!loaded)
                {
                    cached = Resources.Load<SoundLibrary>(ResourcePath);
                    loaded = true;
                }
                return cached;
            }
        }
    }
}
