using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Deadline4Sec.Editor
{
    // Replaces the graybox player with the rigged Reaper model in every gameplay
    // scene. Gameplay colliders stay on the PlayerController root; the model is a
    // visual child only. Safe to run repeatedly.
    public static class ReaperCharacterSetup
    {
        private const string ModelPath = "Assets/Art/3DModel/Reaper.fbx";
        private const string AnimationFolder = "Assets/Art/Animation";
        private const string ControllerPath = "Assets/Art/Animation/Reaper.controller";
        private const string ModelChildName = "ReaperModel";
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        // State name -> (clip file, loop, mirror, speed)
        private static readonly (string state, string file, bool loop, bool mirror, float speed)[] States =
        {
            (PlayerCharacterAnimator.IdleState, "Ani_idle", true, false, 1f),
            (PlayerCharacterAnimator.RunState, "Ani_Run", true, false, 1.15f),
            (PlayerCharacterAnimator.JumpState, "Ani_Jump", false, false, 1.6f),
            (PlayerCharacterAnimator.FallState, "Ani_FallingIdle", true, false, 1f),
            (PlayerCharacterAnimator.FastFallState, "Ain_JumpingDown", false, false, 1.4f),
            (PlayerCharacterAnimator.SlideState, "Ani_RunningSlide", false, false, 1.3f),
            (PlayerCharacterAnimator.AttackRightState, "Ani_SideAttack", false, false, 1.8f),
            (PlayerCharacterAnimator.AttackLeftState, "Ani_SideAttack", false, true, 1.8f),
            (PlayerCharacterAnimator.HomingState, "Ani_FlyKick", false, false, 1.8f),
            (PlayerCharacterAnimator.DieState, "Ani_Die", false, false, 1.2f),
        };

        private static readonly HashSet<string> LoopingFiles = new HashSet<string>
            { "Ani_idle", "Ani_Run", "Ani_FallingIdle" };

        [MenuItem("Deadline 4 Sec/Art/1. Import Reaper Animations As Humanoid")]
        public static void ImportAnimations()
        {
            foreach (string path in AnimationPaths())
            {
                ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(path);
                string file = Path.GetFileNameWithoutExtension(path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                foreach (ModelImporterClipAnimation clip in clips)
                {
                    clip.name = file;
                    clip.loopTime = LoopingFiles.Contains(file);
                    clip.loopPose = clip.loopTime;
                    // Gameplay owns all movement: bake root motion into the pose.
                    // The Mixamo idle slowly turns ~80 degrees. Leaving its root rotation
                    // un-baked sends that turn to (ignored) root motion, so the menu
                    // close-up keeps facing the camera.
                    clip.lockRootRotation = file != "Ani_idle";
                    clip.keepOriginalOrientation = true;
                    clip.lockRootHeightY = true;
                    clip.keepOriginalPositionY = true;
                    clip.heightFromFeet = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalPositionXZ = false;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            Debug.Log("Reaper animations imported as Humanoid.");
        }

        [MenuItem("Deadline 4 Sec/Art/2. Build Reaper Animator Controller")]
        public static AnimatorController BuildController()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            Vector3 position = new Vector3(300f, 0f, 0f);
            foreach (var entry in States)
            {
                AnimationClip clip = LoadClip(entry.file);
                AnimatorState state = machine.AddState(entry.state, position);
                state.motion = clip;
                state.mirror = entry.mirror;
                state.speed = entry.speed;
                state.writeDefaultValues = true;
                position += new Vector3(0f, 60f, 0f);
                if (entry.state == PlayerCharacterAnimator.IdleState)
                    machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Reaper animator controller built at " + ControllerPath);
            return controller;
        }

        [MenuItem("Deadline 4 Sec/Art/3. Apply Reaper To Gameplay Scenes")]
        public static void ApplyToScenes()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before applying the Reaper model.");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? BuildController();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                throw new InvalidOperationException("Missing " + ModelPath);
            string original = EditorSceneManager.GetActiveScene().path;
            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (player == null)
                    continue;
                ApplyToPlayer(player, model, controller);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(original))
                EditorSceneManager.OpenScene(original);
        }

        [MenuItem("Deadline 4 Sec/Art/Run All Reaper Setup Steps")]
        public static void RunAll()
        {
            ImportAnimations();
            BuildController();
            ApplyToScenes();
        }

        private static void ApplyToPlayer(PlayerController player, GameObject modelAsset, AnimatorController controller)
        {
            Transform existing = player.transform.Find(ModelChildName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.transform);
            model.name = ModelChildName;
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Transform t = model.transform;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            t.localPosition = Vector3.zero;

            // Fit the model to the CharacterController capsule: feet on its bottom,
            // height slightly above it so the cartoon proportions read on mobile.
            CharacterController body = player.GetComponent<CharacterController>();
            Bounds bounds = RendererBounds(model);
            float targetHeight = body != null ? body.height * 1.1f : 2f;
            float scale = bounds.size.y > 0.001f ? targetHeight / bounds.size.y : 1f;
            t.localScale = Vector3.one * scale;
            bounds = RendererBounds(model);
            float feetY = body != null
                ? player.transform.TransformPoint(body.center).y - body.height * 0.5f
                : player.transform.position.y;
            t.position += Vector3.up * (feetY - bounds.min.y);

            Animator animator = model.GetComponent<Animator>();
            if (animator == null)
                animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar != null)
                animator.avatar = avatar;

            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (renderer is SkinnedMeshRenderer skinned)
                    skinned.updateWhenOffscreen = true;
            }

            Transform socket = FindDeep(t, "WeaponSocket");
            PlayerCharacterAnimator driver = model.GetComponent<PlayerCharacterAnimator>()
                ?? model.AddComponent<PlayerCharacterAnimator>();
            SerializedObject driverData = new SerializedObject(driver);
            driverData.FindProperty("player").objectReferenceValue = player;
            driverData.FindProperty("weaponPivot").objectReferenceValue = socket;
            driverData.ApplyModifiedPropertiesWithoutUndo();

            HideGrayboxVisuals(player, model);
            RewireWeaponSkin(player, model, socket);
            LogRenderers(model, socket);
        }

        private static void HideGrayboxVisuals(PlayerController player, GameObject model)
        {
            MeshRenderer rootRenderer = player.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
                rootRenderer.enabled = false;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(model.transform))
                    continue;
                // Keep VFX like trails/particles; hide graybox meshes only.
                if (renderer is MeshRenderer)
                    renderer.enabled = false;
            }
        }

        private static void RewireWeaponSkin(PlayerController player, GameObject model, Transform socket)
        {
            Transform weaponRoot = player.transform.Find("WeaponVisual");
            if (weaponRoot == null)
                return;
            WeaponSkinVisual visual = weaponRoot.GetComponent<WeaponSkinVisual>();
            if (visual == null)
                return;
            // The Tripo scythe is a rigid MeshRenderer parented under WeaponSocket.
            List<Renderer> weaponRenderers = model.GetComponentsInChildren<Renderer>(true)
                .Where(r => socket != null && (r.transform.IsChildOf(socket) ||
                    (r is SkinnedMeshRenderer s && IsWeaponMesh(s, socket))))
                .ToList();
            if (weaponRenderers.Count == 0)
            {
                Debug.LogWarning("No Reaper mesh is skinned to WeaponSocket; skin materials keep the graybox scythe.", player);
                return;
            }
            SerializedObject data = new SerializedObject(visual);
            SerializedProperty renderers = data.FindProperty("weaponRenderers");
            renderers.arraySize = weaponRenderers.Count;
            for (int i = 0; i < weaponRenderers.Count; i++)
            {
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = weaponRenderers[i];
                // Skins tint through emission, so the keyword must be on in the asset.
                Material material = weaponRenderers[i].sharedMaterial;
                if (material != null && !material.IsKeywordEnabled("_EMISSION"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    EditorUtility.SetDirty(material);
                }
            }
            GameExtrasCatalog catalog = Resources.Load<GameExtrasCatalog>("GameExtras/GameExtrasCatalog");
            if (catalog != null && catalog.Skins.Length > 0)
                data.FindProperty("untintedSkin").objectReferenceValue = catalog.Skins[0].weaponMaterial;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // A mesh is the weapon when most of its vertex weight sits on WeaponSocket
        // or its children.
        private static bool IsWeaponMesh(SkinnedMeshRenderer renderer, Transform socket)
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null || renderer.bones == null || renderer.bones.Length == 0)
                return false;
            BoneWeight[] weights = mesh.boneWeights;
            if (weights.Length == 0)
                return false;
            int onSocket = 0;
            foreach (BoneWeight w in weights)
            {
                Transform bone = w.boneIndex0 < renderer.bones.Length ? renderer.bones[w.boneIndex0] : null;
                if (bone != null && bone.IsChildOf(socket))
                    onSocket++;
            }
            return onSocket > weights.Length / 2;
        }

        private static void LogRenderers(GameObject model, Transform socket)
        {
            foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Debug.Log($"Reaper renderer '{r.name}' verts={r.sharedMesh?.vertexCount} " +
                          $"bounds={r.bounds.size} weapon={(socket != null && IsWeaponMesh(r, socket))} " +
                          $"materials={string.Join(",", r.sharedMaterials.Select(m => m != null ? m.name : "null"))}");
        }

        private static Bounds RendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);
            return bounds;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
                return root;
            foreach (Transform child in root)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static IEnumerable<string> AnimationPaths() =>
            AssetDatabase.FindAssets("t:Model", new[] { AnimationFolder })
                .Select(AssetDatabase.GUIDToAssetPath);

        private static AnimationClip LoadClip(string file)
        {
            string path = AnimationPaths().FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == file);
            if (path == null)
                throw new InvalidOperationException("Missing animation file " + file);
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null)
                throw new InvalidOperationException("No clip in " + path);
            return clip;
        }
    }
}
