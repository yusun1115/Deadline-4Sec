using UnityEngine;

namespace Deadline4Sec
{
    [CreateAssetMenu(menuName = "Deadline 4 Sec/Power-Up Balance")]
    public sealed class PowerUpBalance : ScriptableObject
    {
        [SerializeField] private int[] upgradeCosts = { 500, 1000, 2000, 4000, 8000, 15000 };
        [SerializeField] private float[] freezeClock = { 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 6f };
        [SerializeField] private float[] soulAmplifier = { 4f, 5f, 6f, 7f, 8f, 9f, 10f };
        [SerializeField] private float[] reaperRush = { 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f };
        [SerializeField] private float[] soulMagnet = { 4f, 5f, 6f, 7f, 8f, 9f, 10f };
        [SerializeField] private float[] comboSeal = { 3f, 4f, 5f, 6f, 7f, 8f, 9f };
        [SerializeField] private float[] timeHeartMaximum = { 4.5f, 5f, 5.5f, 6f, 6.5f, 7f, 8f };
        [SerializeField, Min(0f)] private float timeHeartDuration = 8f;
        [SerializeField, Min(1f)] private float reaperRushSpeedMultiplier = 1.6f;
        [SerializeField, Min(0.1f)] private float magnetRadius = 5f;
        [SerializeField, Min(0.1f)] private float magnetPullSpeed = 14f;

        public float ReaperRushSpeedMultiplier => reaperRushSpeedMultiplier;
        public float MagnetRadius => magnetRadius;
        public float MagnetPullSpeed => magnetPullSpeed;

        public int UpgradeCost(int currentLevel) => currentLevel >= 1 && currentLevel < 7
            ? upgradeCosts[currentLevel - 1] : 0;

        public float Duration(PowerUpType type, int level)
        {
            int index = Mathf.Clamp(level, 1, 7) - 1;
            switch (type)
            {
                case PowerUpType.FreezeClock: return freezeClock[index];
                case PowerUpType.SoulAmplifier: return soulAmplifier[index];
                case PowerUpType.ReaperRush: return reaperRush[index];
                case PowerUpType.SoulMagnet: return soulMagnet[index];
                case PowerUpType.ComboSeal: return comboSeal[index];
                case PowerUpType.TimeHeart: return timeHeartDuration;
                default: return 0f;
            }
        }

        public float TimeHeartMaximum(int level) => timeHeartMaximum[Mathf.Clamp(level, 1, 7) - 1];

        public string EffectDescription(PowerUpType type, int level)
        {
            if (type == PowerUpType.TimeHeart)
                return "TIMER MAX " + TimeHeartMaximum(level).ToString("0.0") + "s / " +
                    Duration(type, level).ToString("0.#") + "s";
            string effect = type == PowerUpType.FreezeClock ? "TIMER FREEZE" :
                type == PowerUpType.SoulAmplifier ? "SCORE x2" :
                type == PowerUpType.ReaperRush ? "INVINCIBLE RUSH" :
                type == PowerUpType.SoulMagnet ? "COIN MAGNET" : "COMBO HOLD";
            return effect + " / " + Duration(type, level).ToString("0.#") + "s";
        }

        private void OnValidate()
        {
            // Arrays intentionally stay editable as one small balance table.
            if (upgradeCosts == null || upgradeCosts.Length != 6 ||
                freezeClock == null || freezeClock.Length != 7 ||
                soulAmplifier == null || soulAmplifier.Length != 7 ||
                reaperRush == null || reaperRush.Length != 7 ||
                soulMagnet == null || soulMagnet.Length != 7 ||
                comboSeal == null || comboSeal.Length != 7 ||
                timeHeartMaximum == null || timeHeartMaximum.Length != 7)
                Debug.LogError("Power-Up Balance needs six upgrade costs and seven values per item.", this);
        }
    }
}
