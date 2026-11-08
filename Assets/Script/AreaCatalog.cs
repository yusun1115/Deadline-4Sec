using UnityEngine;

namespace Deadline4Sec
{
    // The areas of a run, in travel order (the last one leads back to the first),
    // and the section that connects them. Loaded by PatternSpawner from
    // Resources/Areas/AreaCatalog, so no scene holds its own copy of the setup.
    public sealed class AreaCatalog : ScriptableObject
    {
        public const string ResourcePath = "Areas/AreaCatalog";

        public AreaDefinition[] areas;
        [Tooltip("Connects any two areas (a CoursePattern with an AreaTransitionSection).")]
        public CoursePattern transitionPattern;

        [Header("Debug")]
        [Tooltip("Logs each area change and the patterns chosen for it.")]
        public bool showPatternDebugLogs;

        public bool IsUsable => areas != null && areas.Length > 0 && areas[0] != null && transitionPattern != null;

        public AreaDefinition After(AreaDefinition area)
        {
            int index = System.Array.IndexOf(areas, area);
            return areas[(index + 1) % areas.Length];
        }
    }
}
