using UnityEngine;

namespace Deadline4Sec
{
    // Whether this build is the exhibition build (see ExhibitionSettings).
    public static class ExhibitionMode
    {
        private static ExhibitionSettings settings;
        private static bool? overrideActive;

        public static ExhibitionSettings Settings
        {
            get
            {
                if (settings == null)
                    settings = Resources.Load<ExhibitionSettings>(ExhibitionSettings.ResourcePath);
                if (settings == null)
                    settings = ScriptableObject.CreateInstance<ExhibitionSettings>();
                return settings;
            }
        }

        public static bool IsActive => overrideActive ?? Settings.exhibitionMode;


        // Tests switch the mode without touching the asset (null: the asset decides).
        public static void OverrideForTests(bool? active) => overrideActive = active;
    }
}
