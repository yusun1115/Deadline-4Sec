using System.Collections.Generic;
using UnityEngine;

namespace Deadline4Sec
{
    // Reaper model fixes (2026-10-30), on the Animator object (ReaperModel):
    //  - ground contact: the model stood ~0.17-0.2 m above the floor in every clip,
    //  - hood: uses a copy of the hood/cape mesh whose hood part is skinned to
    //    the Head bone (it only followed the chest before, so turning the head
    //    slid the face out of the hood),
    //  - cape: the three cape bone chains (no keys in the clips) swing as
    //    springs — gravity, a little of the run's inertia, body collision,
    //  - weapon grip: the scythe's shaft runs through the right fist (along the
    //    knuckle line, through the hollow of the fingers) instead of past it,
    //  - off hand: when an animation brings the left hand near the shaft, it
    //    closes onto the shaft (two-bone IK, blended by distance).
    // Runs after the Animator and PlayerCharacterAnimator (LateUpdate).
    [DefaultExecutionOrder(500)]
    [RequireComponent(typeof(Animator))]
    public sealed class ReaperRig : MonoBehaviour
    {
        [Header("Ground contact")]
        [SerializeField] private float groundOffset = -0.2f;

        [Header("Hood (head-bound copy of the hood mesh)")]
        [SerializeField] private SkinnedMeshRenderer hood;
        [SerializeField] private Mesh headBoundHood;

        [Header("Cape physics")]
        [SerializeField] private bool capePhysics = true;
        [SerializeField, Range(0f, 1f)] private float stiffness = 0.18f;
        [SerializeField, Range(0f, 1f)] private float damping = 0.14f;
        [SerializeField, Min(0f)] private float gravity = 9f;
        // Share of the runner's own movement the cape lags behind (0 = rides along).
        [SerializeField, Range(0f, 1f)] private float runInertia = 0.3f;
        [SerializeField, Range(0f, 120f)] private float maxSwingAngle = 70f;
        [SerializeField, Min(0f)] private float flutter = 1.2f;

        [Header("Weapon grip")]
        [SerializeField] private bool fixGrip = true;
        // Weapon node pose in the hand socket, measured on the idle fist in the
        // editor (ReaperRigSetup). Without it the grip is measured on the
        // first animated frame.
        [SerializeField] private bool hasStoredGrip;
        [SerializeField] private Vector3 storedGripPosition;
        [SerializeField] private Quaternion storedGripRotation = Quaternion.identity;

        [Header("Off hand on the shaft")]
        [SerializeField] private bool offHandIK = true;
        [SerializeField, Min(0.01f)] private float reach = 0.26f;
        [SerializeField, Min(0f)] private float fullAt = 0.1f;
        [SerializeField, Min(0f)] private float handGap = 0.15f;

        private sealed class Chain
        {
            public Transform[] Joints; // bones then the end
            public Quaternion[] Rest;
            public Vector3[] Pos, Prev;
            public float[] Length;
            public bool Ready;
        }

        private struct Sphere
        {
            public Transform A, B;
            public float Radius;
        }

        private Animator animator;
        private readonly List<Chain> chains = new List<Chain>();
        private readonly List<Sphere> spheres = new List<Sphere>();
        private Transform root;
        private Vector3 lastRoot;
        private bool hasLastRoot;
        private Transform weaponNode;
        private bool gripFixed;
        private float offHandWeight;
        private bool restCaptured;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            transform.localPosition += Vector3.up * groundOffset;
            if (hood != null && headBoundHood != null)
                hood.sharedMesh = headBoundHood;
            PlayerController player = GetComponentInParent<PlayerController>();
            root = player != null ? player.transform : transform;

            var all = new Dictionary<string, Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                all[t.name] = t;
            foreach (string side in new[] { "L", "C", "R" })
            {
                var joints = new List<Transform>();
                for (int i = 1; all.TryGetValue("Cape_" + side + i, out Transform bone); i++)
                    joints.Add(bone);
                if (joints.Count == 0)
                    continue;
                Transform last = joints[joints.Count - 1];
                if (last.childCount > 0)
                    joints.Add(last.GetChild(0));
                var chain = new Chain { Joints = joints.ToArray() };
                chain.Rest = new Quaternion[chain.Joints.Length];
                for (int i = 0; i < chain.Joints.Length; i++)
                    chain.Rest[i] = chain.Joints[i].localRotation;
                chains.Add(chain);
            }
            all.TryGetValue("tripo_node_29219f7e", out weaponNode);
        }

        private void Start()
        {
            if (animator == null || !animator.isHuman)
                return;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null)
                chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            float torso = hips != null && chest != null ? Vector3.Distance(hips.position, chest.position) : 0.5f;
            AddSphere(HumanBodyBones.Hips, HumanBodyBones.Hips, torso * 0.42f);
            AddSphere(HumanBodyBones.Spine, HumanBodyBones.Chest, torso * 0.36f);
            AddSphere(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, torso * 0.24f);
            AddSphere(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, torso * 0.24f);
            AddSphere(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, torso * 0.2f);
            AddSphere(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, torso * 0.2f);
        }

        private void AddSphere(HumanBodyBones a, HumanBodyBones b, float radius)
        {
            Transform ta = animator.GetBoneTransform(a), tb = animator.GetBoneTransform(b);
            if (ta != null && tb != null)
                spheres.Add(new Sphere { A = ta, B = tb, Radius = radius });
        }

        private void LateUpdate()
        {
            if (animator == null || !animator.isHuman)
                return;
            // The Animator rewrites every skeleton transform (the weapon node
            // included) each frame, so the grip pose is applied every frame.
            if (fixGrip)
                FixGrip();
            if (offHandIK)
                SolveOffHand();
            if (capePhysics)
                SimulateCape();
        }

        // ---------------- weapon grip ----------------

        // The shaft along the knuckle line through the hollow between the
        // knuckles and the curled fingertips: the stored idle measurement, or
        // measured once on the first animated frame.
        private void FixGrip()
        {
            if (weaponNode == null || weaponNode.parent == null)
                return;
            if (!gripFixed)
            {
                gripFixed = true;
                if (hasStoredGrip)
                {
                    gripPosition = storedGripPosition;
                    gripRotation = storedGripRotation;
                }
                else if (!MeasureGrip(animator, weaponNode, out gripPosition, out gripRotation))
                {
                    gripPosition = weaponNode.localPosition;
                    gripRotation = weaponNode.localRotation;
                }
            }
            weaponNode.localPosition = gripPosition;
            weaponNode.localRotation = gripRotation;
        }

        private Vector3 gripPosition;
        private Quaternion gripRotation = Quaternion.identity;

        // The shaft along the knuckle line, through the hollow between the
        // knuckles and the curled fingertips (socket space). Shared with the
        // editor setup, which measures it on the idle pose.
        public static bool MeasureGrip(Animator animator, Transform weaponNode, out Vector3 position, out Quaternion rotation)
        {
            position = weaponNode.localPosition;
            rotation = weaponNode.localRotation;
            Transform socket = weaponNode.parent;
            Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            Transform middleTip = animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal);
            Transform indexTip = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal);
            if (socket == null || index == null || middle == null || little == null || middleTip == null || indexTip == null)
                return false;
            Vector3 knuckles = socket.InverseTransformPoint((index.position + middle.position + little.position) / 3f);
            Vector3 tips = socket.InverseTransformPoint((middleTip.position + indexTip.position) * 0.5f);
            Vector3 axisNow = weaponNode.localRotation * Vector3.up;
            Vector3 axis = (socket.InverseTransformPoint(index.position) - socket.InverseTransformPoint(little.position)).normalized;
            if (Vector3.Dot(axis, axisNow) < 0f)
                axis = -axis;
            rotation = Quaternion.FromToRotation(axisNow, axis) * weaponNode.localRotation;
            position = Vector3.Lerp(knuckles, tips, 0.5f);
            return true;
        }

        // ---------------- off hand ----------------

        private void SolveOffHand()
        {
            Transform upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform index = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            Transform little = animator.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            if (upper == null || lower == null || hand == null || index == null || little == null || weaponNode == null)
                return;
            Vector3 grip = weaponNode.position;
            Vector3 shaft = weaponNode.up.normalized;
            Vector3 palm = Vector3.Lerp(hand.position, (index.position + little.position) * 0.5f, 0.6f);
            Vector3 fromGrip = palm - grip;
            float along = Vector3.Dot(fromGrip, shaft);
            float distance = (fromGrip - shaft * along).magnitude;
            float target = Mathf.Clamp01(Mathf.InverseLerp(reach, fullAt, distance));
            if (along < -0.9f || along > 1.2f)
                target = 0f;
            offHandWeight = Mathf.MoveTowards(offHandWeight, target, Time.deltaTime * 6f);
            if (offHandWeight <= 0.001f)
                return;

            // Next to the right hand, never on it.
            if (Mathf.Abs(along) < handGap)
                along = along >= 0f ? handGap : -handGap;
            Vector3 onShaft = grip + shaft * along;
            Vector3 handTarget = hand.position + (onShaft - palm);
            Quaternion upperBefore = upper.rotation, lowerBefore = lower.rotation, handBefore = hand.rotation;
            SolveTwoBone(upper, lower, hand, handTarget);
            // Fist across the shaft: knuckle line along it.
            Vector3 knuckleLine = index.position - little.position;
            Vector3 along2 = Vector3.Dot(knuckleLine, shaft) >= 0f ? shaft : -shaft;
            hand.rotation = Quaternion.FromToRotation(knuckleLine, along2) * hand.rotation;

            float w = offHandWeight;
            upper.rotation = Quaternion.Slerp(upperBefore, upper.rotation, w);
            lower.rotation = Quaternion.Slerp(lowerBefore, lower.rotation, w);
            hand.rotation = Quaternion.Slerp(handBefore, hand.rotation, w);
        }

        // Analytic two-bone IK (keeps the current bend plane).
        private static void SolveTwoBone(Transform a, Transform b, Transform c, Vector3 target)
        {
            Vector3 aPos = a.position, bPos = b.position, cPos = c.position;
            float ab = Vector3.Distance(aPos, bPos), bc = Vector3.Distance(bPos, cPos);
            float d = Mathf.Clamp(Vector3.Distance(aPos, target), 0.01f, ab + bc - 0.001f);
            Vector3 axis = Vector3.Cross(cPos - aPos, bPos - aPos);
            if (axis.sqrMagnitude < 1e-8f)
                axis = Vector3.Cross(cPos - aPos, Vector3.up);
            axis.Normalize();
            float bendNow = Vector3.Angle(aPos - bPos, cPos - bPos);
            float bendWant = Mathf.Acos(Mathf.Clamp((ab * ab + bc * bc - d * d) / (2f * ab * bc), -1f, 1f)) * Mathf.Rad2Deg;
            b.rotation = Quaternion.AngleAxis(bendWant - bendNow, axis) * b.rotation;
            a.rotation = Quaternion.FromToRotation(c.position - aPos, target - aPos) * a.rotation;
        }

        // ---------------- cape ----------------

        private void SimulateCape()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            // Rest = the pose the Animator writes (taken on the first frame);
            // reset to it each frame so the springs never accumulate.
            if (!restCaptured)
            {
                restCaptured = true;
                foreach (Chain chain in chains)
                    for (int i = 0; i < chain.Joints.Length; i++)
                        chain.Rest[i] = chain.Joints[i].localRotation;
            }
            foreach (Chain chain in chains)
                for (int i = 0; i < chain.Joints.Length; i++)
                    chain.Joints[i].localRotation = chain.Rest[i];
            if (dt <= 0f)
                return;

            Vector3 rootNow = root.position;
            Vector3 move = hasLastRoot ? rootNow - lastRoot : Vector3.zero;
            bool teleported = move.sqrMagnitude > 4f;
            lastRoot = rootNow;
            hasLastRoot = true;
            Vector3 carry = move * (1f - runInertia);
            float stiff = 1f - Mathf.Pow(1f - stiffness, dt * 60f);
            float keep = Mathf.Pow(1f - damping, dt * 60f);
            Vector3 back = -root.forward;
            float t = Time.time;

            foreach (Chain chain in chains)
            {
                int n = chain.Joints.Length;
                if (!chain.Ready || teleported)
                {
                    chain.Pos = new Vector3[n];
                    chain.Prev = new Vector3[n];
                    chain.Length = new float[n];
                    for (int i = 0; i < n; i++)
                        chain.Pos[i] = chain.Prev[i] = chain.Joints[i].position;
                    for (int i = 1; i < n; i++)
                        chain.Length[i] = Vector3.Distance(chain.Pos[i], chain.Pos[i - 1]);
                    chain.Ready = true;
                    continue;
                }

                chain.Pos[0] = chain.Prev[0] = chain.Joints[0].position;
                for (int i = 1; i < n; i++)
                {
                    chain.Pos[i] += carry;
                    chain.Prev[i] += carry;
                    Vector3 rest = chain.Joints[i].position;
                    Vector3 velocity = (chain.Pos[i] - chain.Prev[i]) * keep;
                    chain.Prev[i] = chain.Pos[i];
                    float wave = Mathf.PerlinNoise(t * 1.7f + i * 0.31f, chain.GetHashCode() * 0.001f) - 0.5f;
                    Vector3 force = Vector3.down * gravity + back * (flutter * (0.6f + wave)) * i;
                    chain.Pos[i] += velocity + force * dt * dt;
                    chain.Pos[i] = Vector3.Lerp(chain.Pos[i], rest, stiff);

                    Vector3 segment = chain.Pos[i] - chain.Pos[i - 1];
                    Vector3 restSegment = rest - chain.Joints[i - 1].position;
                    float angle = Vector3.Angle(segment, restSegment);
                    if (angle > maxSwingAngle)
                        segment = Vector3.Slerp(restSegment.normalized, segment.normalized, maxSwingAngle / angle);
                    chain.Pos[i] = chain.Pos[i - 1] + segment.normalized * chain.Length[i];

                    foreach (Sphere sphere in spheres)
                    {
                        Vector3 centre = (sphere.A.position + sphere.B.position) * 0.5f;
                        Vector3 offset = chain.Pos[i] - centre;
                        float r = sphere.Radius;
                        if (offset.sqrMagnitude < r * r && offset.sqrMagnitude > 1e-8f)
                            chain.Pos[i] = centre + offset.normalized * r;
                    }
                    chain.Pos[i] = chain.Pos[i - 1] + (chain.Pos[i] - chain.Pos[i - 1]).normalized * chain.Length[i];
                }

                // Point each bone at its simulated child (top-down, so children follow).
                for (int i = 0; i < n - 1; i++)
                {
                    Vector3 current = chain.Joints[i + 1].position - chain.Joints[i].position;
                    Vector3 wanted = chain.Pos[i + 1] - chain.Joints[i].position;
                    if (current.sqrMagnitude > 1e-8f && wanted.sqrMagnitude > 1e-8f)
                        chain.Joints[i].rotation = Quaternion.FromToRotation(current, wanted) * chain.Joints[i].rotation;
                }
            }
        }
    }
}
