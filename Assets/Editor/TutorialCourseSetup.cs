using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    public static class TutorialCourseSetup
    {
        [MenuItem("Deadline 4 Sec/Prepare Tutorial Course")]
        public static void Prepare()
        {
            const string folder = "Assets/Resources/Tutorial";
            const string path = folder + "/TutorialCourse.asset";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/Resources", "Tutorial");
            TutorialCourseAssets assets = AssetDatabase.LoadAssetAtPath<TutorialCourseAssets>(path);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<TutorialCourseAssets>();
                AssetDatabase.CreateAsset(assets, path);
            }
            assets.groundEnemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/Enemy.prefab");
            assets.airEnemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Enemy/EnemyAir.prefab");
            assets.jumpObstacle = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Obstacle/Jump Obstacle.prefab");
            assets.slideObstacle = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Obstacle/Slide Obstacle.prefab");
            assets.laneObstacle = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Obstacle/Lane Blocker.prefab");
            assets.floorMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/FloorColor.mat");
            if (!assets.IsValid)
                throw new System.InvalidOperationException("Tutorial requires the production enemy, obstacle and floor assets.");
            // Speaker and swipe guide art (2026-11-01); steps keep any edits.
            assets.portrait = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tutorial/tutorial_ex.png");
            assets.bubble = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tutorial/tutorial_ex_bubble.png");
            assets.swipeArrow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tutorial/tutorial_swipe_arrow.png");
            assets.swipeHand = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Tutorial/tutorial_swipe_hand.png");
            if (assets.presentation == null)
                assets.presentation = new TutorialPresentation();
            if (assets.steps == null || assets.steps.Length == 0)
                assets.steps = TutorialStep.CreateDefaults();
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            Debug.Log("Tutorial course uses the production enemy/obstacle prefabs.");
        }
    }
}
