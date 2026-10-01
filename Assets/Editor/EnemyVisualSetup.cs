using UnityEditor;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Adds EnemyVisual to the enemy prefabs and links a generated model when one
    // has been imported. Without a model the component builds the stylized
    // procedural look at runtime.
    public static class EnemyVisualSetup
    {
        private const string ModelFolder = "Assets/Art/3DModel/Enemies/";

        [MenuItem("Deadline 4 Sec/Art/Apply Enemy Visuals")]
        public static void Apply()
        {
            Setup("Assets/Prefab/Enemy/Enemy.prefab", FindModel("Enemy_ShadowImp"));
            Setup("Assets/Prefab/Enemy/EnemyAir.prefab", FindModel("Enemy_FallenAngel"));
            AssetDatabase.SaveAssets();
        }

        private static GameObject FindModel(string name)
        {
            // Converted Tripo models are saved as prefabs next to their mesh.
            return AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + name + ".prefab");
        }

        private static void Setup(string prefabPath, GameObject model)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                EnemyVisual visual = (root.TryGetComponent(out EnemyVisual existingEnemyVisual) ? existingEnemyVisual : root.AddComponent<EnemyVisual>());
                SerializedObject so = new SerializedObject(visual);
                so.FindProperty("modelPrefab").objectReferenceValue = model;
                so.FindProperty("modelYaw").floatValue = -90f;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"EnemyVisual on {prefabPath} model={(model != null ? model.name : "procedural")}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
