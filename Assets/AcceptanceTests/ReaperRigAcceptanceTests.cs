using System;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Deadline4Sec.AcceptanceTests
{
    // Reaper model fixes (2026-10-30): boots on the floor, hood bound to the
    // head, cape bones swinging, the shaft through the right fist.
    public sealed class ReaperRigAcceptanceTests
    {
        private static Type T(string name) => Type.GetType("Deadline4Sec." + name + ", Assembly-CSharp");

        [UnityTest]
        public IEnumerator ReaperStandsOnTheFloorWithHoodCapeAndGripFixed()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/EndlessRun.unity");
            yield return new EnterPlayMode();
            for (int i = 0; i < 30; i++)
                yield return null;
            Component player = (Component)UnityEngine.Object.FindFirstObjectByType(T("PlayerController"));
            Transform model = player.transform.Find("ReaperModel");
            Assert.IsNotNull(model.GetComponent(T("ReaperRig")), "ReaperRig on the model.");
            Animator animator = model.GetComponent<Animator>();

            // Boots on the floor (idle): lowest skinned vertex within a few cm.
            float floor = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(player.transform.position + Vector3.up, Vector3.down, 10f))
                if (!hit.collider.transform.IsChildOf(player.transform))
                    floor = Mathf.Max(floor, hit.point.y);
            SkinnedMeshRenderer body = model.Find("tripo_node_e38ca837").GetComponent<SkinnedMeshRenderer>();
            var baked = new Mesh();
            float lowest = float.PositiveInfinity;
            for (int frame = 0; frame < 60; frame++)
            {
                body.BakeMesh(baked, true);
                Matrix4x4 m = body.transform.localToWorldMatrix;
                foreach (Vector3 v in baked.vertices)
                    lowest = Mathf.Min(lowest, m.MultiplyPoint3x4(v).y);
                yield return null;
            }
            UnityEngine.Object.Destroy(baked);
            Assert.That(lowest - floor, Is.InRange(-0.06f, 0.06f), "Boots touch the floor in idle.");

            // Hood skinned to the head.
            SkinnedMeshRenderer hood = model.Find("Hood_Root").GetComponent<SkinnedMeshRenderer>();
            Assert.AreEqual("Hood_Root_HeadBound", hood.sharedMesh.name);
            int head = Array.FindIndex(hood.bones, b => b != null && b.name == "Head");
            int onHead = 0;
            foreach (BoneWeight w in hood.sharedMesh.boneWeights)
                if ((w.boneIndex0 == head && w.weight0 > 0.5f) || (w.boneIndex1 == head && w.weight1 > 0.5f))
                    onHead++;
            Assert.Greater(onHead, 300, "Hood vertices follow the head.");

            // Shaft through the right fist (close to the knuckles).
            Transform node = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == "tripo_node_29219f7e")
                    node = t;
            Vector3 knuckles = (animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).position +
                animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position +
                animator.GetBoneTransform(HumanBodyBones.RightLittleProximal).position) / 3f;
            Vector3 fromGrip = knuckles - node.position;
            float offShaft = (fromGrip - Vector3.Project(fromGrip, node.up)).magnitude;
            Assert.Less(offShaft, 0.05f, "Shaft passes through the fist.");
            Vector3 knuckleLine = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).position -
                animator.GetBoneTransform(HumanBodyBones.RightLittleProximal).position;
            float angle = Vector3.Angle(knuckleLine, node.up);
            Assert.That(Mathf.Min(angle, 180f - angle), Is.LessThan(15f), "Shaft along the knuckle line.");

            // Cape bones swing when the body moves (no keys in the clips): carry
            // the runner sideways a little each frame.
            Transform capeTip = null;
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (t.name == "Cape_C4")
                    capeTip = t;
            Quaternion before = capeTip.localRotation;
            float maxTurn = 0f;
            float end = Time.realtimeSinceStartup + 6f;
            int frames = 0;
            while (Time.realtimeSinceStartup < end && maxTurn < 5f)
            {
                player.transform.position += (frames++ % 40 < 20 ? Vector3.right : Vector3.left) * 0.06f;
                maxTurn = Mathf.Max(maxTurn, Quaternion.Angle(before, capeTip.localRotation));
                yield return null;
            }
            Assert.Greater(maxTurn, 5f, "Cape swings.");
            yield return new ExitPlayMode();
        }
    }
}
