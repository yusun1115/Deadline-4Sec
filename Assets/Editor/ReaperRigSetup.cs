using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Reaper model fixes (2026-10-30):
    //  - Art/3DModel/Generated/Hood_HeadBound.asset: a copy of the hood/cape mesh
    //    whose hood part (above the neck) is skinned to the Head bone, blending
    //    back into the cape bones toward the neck,
    //  - ReaperRig on ReaperModel in the three gameplay scenes (ground offset,
    //    hood mesh, cape springs, weapon grip, off-hand IK — see ReaperRig).
    // The open scene is changed in place (left for you to save if it was
    // already dirty); the other gameplay scenes are opened, saved and closed.
    public static class ReaperRigSetup
    {
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        private const string ModelPath = "Assets/Art/3DModel/Reaper.fbx";
        private const string HoodPath = "Assets/Art/3DModel/Generated/Hood_HeadBound.asset";

        [MenuItem("Deadline 4 Sec/Character/Apply Reaper Rig Fixes")]
        public static void Apply()
        {
            var open = EditorSceneManager.GetActiveScene();
            bool openWasDirty = open.isDirty;
            Mesh hoodMesh = null;
            foreach (string path in ScenePaths)
            {
                bool isOpen = open.path == path;
                var scene = isOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                PlayerController player = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (player == null)
                        player = root.GetComponentInChildren<PlayerController>(true);
                Transform model = player != null ? player.transform.Find("ReaperModel") : null;
                if (model != null)
                {
                    SkinnedMeshRenderer hood = model.Find("Hood_Root")?.GetComponent<SkinnedMeshRenderer>();
                    if (hood != null && hoodMesh == null)
                        hoodMesh = BuildHeadBoundHood(hood);
                    ReaperRig rig = model.TryGetComponent(out ReaperRig existing) ? existing : model.gameObject.AddComponent<ReaperRig>();
                    var data = new SerializedObject(rig);
                    data.FindProperty("hood").objectReferenceValue = hood;
                    data.FindProperty("headBoundHood").objectReferenceValue = hoodMesh;
                    data.FindProperty("groundOffset").floatValue = -0.2f;
                    if (MeasureIdleGrip(model, out Vector3 gripPosition, out Quaternion gripRotation))
                    {
                        data.FindProperty("hasStoredGrip").boolValue = true;
                        data.FindProperty("storedGripPosition").vector3Value = gripPosition;
                        data.FindProperty("storedGripRotation").quaternionValue = gripRotation;
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!(isOpen && openWasDirty))
                        EditorSceneManager.SaveScene(scene);
                }
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
        }

        // The grip measured on the idle clip's first frame (fists closed); the
        // pose is sampled in animation mode, so the scene is left untouched.
        private static bool MeasureIdleGrip(Transform model, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            Animator animator = model.GetComponent<Animator>();
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Animation/Ani_idle.fbx").OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview"));
            Transform node = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "tripo_node_29219f7e");
            if (animator == null || idle == null || node == null)
                return false;
            bool started = !AnimationMode.InAnimationMode();
            if (started)
                AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(model.gameObject, idle, 0f);
                AnimationMode.EndSampling();
                bool ok = ReaperRig.MeasureGrip(animator, node, out position, out rotation);
                Debug.Log($"Reaper grip: {position:F5} {rotation.eulerAngles:F1} (node was {node.localPosition:F5} {node.localEulerAngles:F1})");
                return ok;
            }
            finally
            {
                if (started)
                    AnimationMode.StopAnimationMode();
            }
        }

        // Head weight rises from 0 at the neck joint to 1 a little above it
        // (measured along neck → head); the rest of each vertex keeps its cape bones.
        private static Mesh BuildHeadBoundHood(SkinnedMeshRenderer hood)
        {
            Mesh source = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().First(m => m.name == "Hood_Root");
            Transform[] bones = hood.bones;
            int head = System.Array.FindIndex(bones, b => b != null && b.name == "Head");
            int neck = System.Array.FindIndex(bones, b => b != null && b.name == "Neck");
            if (head < 0 || neck < 0)
                throw new System.InvalidOperationException("Hood bones Head / Neck not found.");
            Matrix4x4[] bind = source.bindposes;
            Vector3 headPos = bind[head].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 neckPos = bind[neck].inverse.MultiplyPoint3x4(Vector3.zero);
            Vector3 up = headPos - neckPos;
            float length = up.magnitude;
            up /= length;

            Mesh copy = Object.Instantiate(source);
            copy.name = "Hood_Root_HeadBound";
            Vector3[] vertices = source.vertices;
            BoneWeight[] weights = source.boneWeights;
            int bound = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                float h = Vector3.Dot(vertices[i] - neckPos, up) / length;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.9f, h));
                if (t <= 0f)
                    continue;
                bound++;
                BoneWeight w = weights[i];
                var influence = new Dictionary<int, float>();
                void Add(int index, float value)
                {
                    if (value <= 0f)
                        return;
                    influence.TryGetValue(index, out float now);
                    influence[index] = now + value;
                }
                Add(w.boneIndex0, w.weight0 * (1f - t));
                Add(w.boneIndex1, w.weight1 * (1f - t));
                Add(w.boneIndex2, w.weight2 * (1f - t));
                Add(w.boneIndex3, w.weight3 * (1f - t));
                Add(head, t);
                var top = influence.OrderByDescending(p => p.Value).Take(4).ToList();
                float sum = top.Sum(p => p.Value);
                var result = new BoneWeight();
                for (int k = 0; k < top.Count; k++)
                {
                    float value = top[k].Value / sum;
                    switch (k)
                    {
                        case 0: result.boneIndex0 = top[k].Key; result.weight0 = value; break;
                        case 1: result.boneIndex1 = top[k].Key; result.weight1 = value; break;
                        case 2: result.boneIndex2 = top[k].Key; result.weight2 = value; break;
                        case 3: result.boneIndex3 = top[k].Key; result.weight3 = value; break;
                    }
                }
                weights[i] = result;
            }
            copy.boneWeights = weights;

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(HoodPath));
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(HoodPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                Object.DestroyImmediate(copy);
                copy = existing;
            }
            else
                AssetDatabase.CreateAsset(copy, HoodPath);
            Debug.Log($"Hood mesh: {bound} of {vertices.Length} vertices follow the head.");
            return copy;
        }
    }
}
