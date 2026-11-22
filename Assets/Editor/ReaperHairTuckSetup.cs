using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Hair under the hood (2026-11-22): in the title idle the Reaper turns her
    // back to the camera and strands of the back hair (part of the body mesh)
    // poked through the hood. Art/3DModel/Generated/Body_HairTucked.asset is a
    // copy of the body mesh where only the back / top hair vertices that stand
    // outside the hood shell are drawn in, just under it (the face, the front
    // locks and everything below the head stay as they are). ReaperRig puts it
    // on the body at run time, like the head-bound hood.
    // Measured over the idle clip (every 0.1 s, sampled in animation mode, the
    // scene untouched) against the head-bound hood: the back hair is partly
    // bound to the neck, so turning the head carried the hood past it. Each
    // vertex moves by the largest push any pose needs, through the inverse of
    // its own skinning in that pose.
    public static class ReaperHairTuckSetup
    {
        private const string MeshPath = "Assets/Art/3DModel/Generated/Body_HairTucked.asset";
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity", "Assets/Scenes/GrayboxCourse.unity", "Assets/Scenes/TestScene.unity",
        };
        private const float Margin = 0.02f;          // world metres under the hood surface
        private const float ConeDegrees = 9f;        // hood shell sampled around each direction
        private const float FrontLimit = 0.15f;      // directions facing forward more than this keep their hair

        [MenuItem("Deadline 4 Sec/Character/Tuck Hair Under Hood")]
        public static void Apply()
        {
            var open = EditorSceneManager.GetActiveScene();
            if (open.isDirty)
                throw new System.InvalidOperationException("Save the open scene first.");
            Mesh tucked = null;
            foreach (string path in ScenePaths)
            {
                bool isOpen = open.path == path;
                var scene = isOpen ? open : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                PlayerController player = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (player == null)
                        player = root.GetComponentInChildren<PlayerController>(true);
                Transform model = player != null ? player.transform.Find("ReaperModel") : null;
                SkinnedMeshRenderer body = model != null ? model.Find("tripo_node_e38ca837")?.GetComponent<SkinnedMeshRenderer>() : null;
                SkinnedMeshRenderer hood = model != null ? model.Find("Hood_Root")?.GetComponent<SkinnedMeshRenderer>() : null;
                if (body != null && hood != null && model.TryGetComponent(out ReaperRig rig))
                {
                    if (tucked == null)
                        tucked = BuildTuckedBody(model, body, hood);
                    var data = new SerializedObject(rig);
                    data.FindProperty("body").objectReferenceValue = body;
                    data.FindProperty("hairTuckedBody").objectReferenceValue = tucked;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                if (!isOpen)
                    EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
        }

        public static Mesh BuildTuckedBody(Transform model, SkinnedMeshRenderer body, SkinnedMeshRenderer hood)
        {
            Mesh source = body.sharedMesh;
            Transform[] bones = body.bones;
            int head = System.Array.FindIndex(bones, b => b != null && b.name == "Head");
            int neck = System.Array.FindIndex(bones, b => b != null && b.name == "Neck");
            if (head < 0 || neck < 0)
                throw new System.InvalidOperationException("Body bones Head / Neck not found.");
            // The hood as it is in play: the head-bound copy (ReaperRig).
            ReaperRig rig = model.GetComponent<ReaperRig>();
            Mesh hoodMesh = rig != null ? new SerializedObject(rig).FindProperty("headBoundHood").objectReferenceValue as Mesh : null;
            if (hoodMesh == null)
                hoodMesh = hood.sharedMesh;
            Transform[] hoodBones = hood.bones;
            int hoodHead = System.Array.FindIndex(hoodBones, b => b != null && b.name == "Head");

            Vector3[] vertices = source.vertices;
            BoneWeight[] weights = source.boneWeights;
            Matrix4x4[] bind = source.bindposes;
            Vector3[] hoodVertices = hoodMesh.vertices;
            BoneWeight[] hoodWeights = hoodMesh.boneWeights;
            Matrix4x4[] hoodBind = hoodMesh.bindposes;
            var shellIndex = new List<int>();
            for (int i = 0; i < hoodVertices.Length; i++)
                if (hoodHead < 0 || Weight(hoodWeights[i], hoodHead) >= 0.3f)
                    shellIndex.Add(i);
            // Candidates: the head's hair (any head / neck share).
            var hair = new List<int>();
            for (int i = 0; i < vertices.Length; i++)
                if (Weight(weights[i], head) + Weight(weights[i], neck) >= 0.2f)
                    hair.Add(i);

            var clips = new List<AnimationClip>();
            foreach (string path in new[] { "Assets/Art/Animation/Ani_idle.fbx" })
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                        clips.Add(clip);
            if (clips.Count == 0)
                throw new System.InvalidOperationException("Ani_idle clip not found.");

            var push = new Vector3[vertices.Length];
            var pushLength = new float[vertices.Length];
            float cone = Mathf.Cos(ConeDegrees * Mathf.Deg2Rad);
            Quaternion restHead = bones[head].rotation;
            Vector3 restForward = model.forward;
            bool started = !AnimationMode.InAnimationMode();
            if (started)
                AnimationMode.StartAnimationMode();
            int poses = 0;
            try
            {
                foreach (AnimationClip clip in clips)
                    for (float t = 0f; t <= clip.length; t += 0.1f)
                    {
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(model.gameObject, clip, t);
                        AnimationMode.EndSampling();
                        poses++;
                        Vector3 forward = bones[head].rotation * Quaternion.Inverse(restHead) * restForward;
                        // The skull's centre: the head-bound body vertices' centroid.
                        Vector3 centre = Vector3.zero;
                        int count = 0;
                        var world = new Dictionary<int, Vector3>(hair.Count);
                        foreach (int i in hair)
                        {
                            Vector3 p = Skin(weights[i], bones, bind).MultiplyPoint3x4(vertices[i]);
                            world[i] = p;
                            if (Weight(weights[i], head) >= 0.5f)
                            {
                                centre += p;
                                count++;
                            }
                        }
                        centre /= Mathf.Max(1, count);
                        var shellDir = new Vector3[shellIndex.Count];
                        var shellDist = new float[shellIndex.Count];
                        for (int k = 0; k < shellIndex.Count; k++)
                        {
                            int h = shellIndex[k];
                            Vector3 d = Skin(hoodWeights[h], hoodBones, hoodBind).MultiplyPoint3x4(hoodVertices[h]) - centre;
                            shellDist[k] = d.magnitude;
                            shellDir[k] = d / Mathf.Max(1e-5f, shellDist[k]);
                        }
                        foreach (int i in hair)
                        {
                            Vector3 d = world[i] - centre;
                            float r = d.magnitude;
                            if (r < 1e-4f)
                                continue;
                            Vector3 dir = d / r;
                            if (Vector3.Dot(dir, forward) > FrontLimit)
                                continue; // the face and the front locks
                            float inner = float.PositiveInfinity;
                            for (int k = 0; k < shellDir.Length; k++)
                                if (Vector3.Dot(dir, shellDir[k]) >= cone && shellDist[k] < inner)
                                    inner = shellDist[k];
                            if (float.IsPositiveInfinity(inner) || r <= inner - Margin)
                                continue;
                            Vector3 deltaWorld = dir * (inner - Margin - r);
                            // Back through this vertex's own skinning in this pose.
                            Vector3 delta = Skin(weights[i], bones, bind).inverse.MultiplyVector(deltaWorld);
                            if (delta.magnitude > pushLength[i])
                            {
                                pushLength[i] = delta.magnitude;
                                push[i] = delta;
                            }
                        }
                    }
            }
            finally
            {
                if (started)
                    AnimationMode.StopAnimationMode();
            }

            int moved = 0;
            float most = 0f;
            for (int i = 0; i < vertices.Length; i++)
                if (pushLength[i] > 0f)
                {
                    vertices[i] += push[i];
                    moved++;
                    most = Mathf.Max(most, pushLength[i]);
                }
            Mesh copy = Object.Instantiate(source);
            copy.name = "Body_HairTucked";
            copy.vertices = vertices;
            copy.RecalculateBounds();
            AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.CreateAsset(copy, MeshPath);
            Debug.Log($"Hair tucked under the hood: {moved} vertices moved (most {most:F4} mesh units) over {poses} idle poses.");
            return copy;
        }

        private static float Weight(BoneWeight w, int bone) =>
            (w.boneIndex0 == bone ? w.weight0 : 0f) + (w.boneIndex1 == bone ? w.weight1 : 0f) +
            (w.boneIndex2 == bone ? w.weight2 : 0f) + (w.boneIndex3 == bone ? w.weight3 : 0f);

        private static Matrix4x4 Skin(BoneWeight w, Transform[] bones, Matrix4x4[] bind)
        {
            Matrix4x4 m = Zero();
            Accumulate(ref m, bones[w.boneIndex0].localToWorldMatrix * bind[w.boneIndex0], w.weight0);
            Accumulate(ref m, bones[w.boneIndex1].localToWorldMatrix * bind[w.boneIndex1], w.weight1);
            Accumulate(ref m, bones[w.boneIndex2].localToWorldMatrix * bind[w.boneIndex2], w.weight2);
            Accumulate(ref m, bones[w.boneIndex3].localToWorldMatrix * bind[w.boneIndex3], w.weight3);
            m.m33 = 1f;
            return m;
        }

        private static Matrix4x4 Zero()
        {
            Matrix4x4 m = Matrix4x4.zero;
            return m;
        }

        private static void Accumulate(ref Matrix4x4 m, Matrix4x4 add, float weight)
        {
            if (weight <= 0f)
                return;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 4; c++)
                    m[r, c] += add[r, c] * weight;
        }
    }
}
