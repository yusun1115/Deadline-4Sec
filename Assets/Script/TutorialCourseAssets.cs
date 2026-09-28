using UnityEngine;

namespace Deadline4Sec
{
    public sealed class TutorialCourseAssets : ScriptableObject
    {
        public GameObject groundEnemy;
        public GameObject airEnemy;
        public GameObject jumpObstacle;
        public GameObject slideObstacle;
        public GameObject laneObstacle;
        public Material floorMaterial;
        public bool IsValid => groundEnemy != null && airEnemy != null &&
            jumpObstacle != null && slideObstacle != null && laneObstacle != null && floorMaterial != null;
    }
}
