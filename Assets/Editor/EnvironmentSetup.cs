using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Deadline4Sec.Editor
{
    // Applies the gothic afterlife look (rail track floor, night sky, fog,
    // lighting, post-processing, background architecture) to gameplay scenes.
    // Visual only and safe to re-run.
    public static class EnvironmentSetup
    {
        private const string FloorMaterialPath = "Assets/Material/FloorColor.mat";
        private const string SkyMaterialPath = "Assets/Art/Materials/Environment/GothicSky.mat";
        private const string SilhouetteMaterialPath = "Assets/Art/Materials/Environment/Silhouette.mat";
        private const string EnvironmentName = "Environment";
        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/EndlessRun.unity",
            "Assets/Scenes/GrayboxCourse.unity",
            "Assets/Scenes/TestScene.unity",
        };

        public static readonly Color FogColor = new Color(0.2f, 0.14f, 0.27f);

        [MenuItem("Deadline 4 Sec/Art/Apply Gothic Environment To Gameplay Scenes")]
        public static void ApplyToScenes()
        {
            if (EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the open scene before applying the environment.");
            PrepareMaterials(out Material sky, out Material silhouette);
            ConfigureVolumeProfile();
            string original = EditorSceneManager.GetActiveScene().path;
            foreach (string path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path);
                ApplyToOpenScene(sky, silhouette);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(original))
                EditorSceneManager.OpenScene(original);
            AssetDatabase.SaveAssets();
        }

        private static void PrepareMaterials(out Material sky, out Material silhouette)
        {
            System.IO.Directory.CreateDirectory("Assets/Art/Materials/Environment");
            Material floor = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            Shader track = Shader.Find("Deadline4Sec/RailTrack");
            if (floor != null && track != null)
            {
                // Keep the FloorColor asset (Spec §45) and swap only its shader.
                floor.shader = track;
                floor.SetColor("_BaseColor", new Color(0.13f, 0.1f, 0.17f));
                floor.SetColor("_StoneLight", new Color(0.24f, 0.18f, 0.3f));
                floor.SetColor("_SleeperColor", new Color(0.09f, 0.06f, 0.1f));
                floor.SetColor("_RailColor", new Color(0.75f, 0.7f, 0.85f));
                floor.SetColor("_GlowColor", new Color(1f, 0.16f, 0.5f));
                EditorUtility.SetDirty(floor);
            }
            sky = LoadOrCreate(SkyMaterialPath, "Deadline4Sec/GothicSky");
            silhouette = LoadOrCreate(SilhouetteMaterialPath, "Deadline4Sec/Silhouette");
            silhouette.SetColor("_HazeColor", FogColor);
            silhouette.enableInstancing = true;
            EditorUtility.SetDirty(silhouette);
        }

        private static Material LoadOrCreate(string path, string shaderName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            material = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ApplyToOpenScene(Material sky, Material silhouette)
        {
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.4f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.26f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.08f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 320f;

            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional)
                    continue;
                light.color = new Color(0.92f, 0.82f, 1f);
                light.intensity = 1.5f;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.75f;
                light.transform.rotation = Quaternion.Euler(52f, 18f, 0f);
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.farClipPlane = 600f;
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(camera);
            }

            PatternSpawner spawner = UnityEngine.Object.FindFirstObjectByType<PatternSpawner>();
            PlayerController player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            float trackX = player != null ? player.transform.position.x
                : spawner != null ? spawner.transform.position.x : 0f;

            GameObject environment = GameObject.Find(EnvironmentName);
            if (environment == null)
                environment = new GameObject(EnvironmentName);
            environment.transform.position = new Vector3(trackX, 0f, 0f);
            // Re-add so tuned script defaults replace previously serialized values.
            EnvironmentScroller old = environment.GetComponent<EnvironmentScroller>();
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            EnvironmentScroller scroller = environment.AddComponent<EnvironmentScroller>();
            SerializedObject so = new SerializedObject(scroller);
            so.FindProperty("silhouetteMaterial").objectReferenceValue = silhouette;
            so.FindProperty("followTarget").objectReferenceValue = camera != null ? camera.transform : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureVolumeProfile()
        {
            foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                Configure(volume.sharedProfile);
            foreach (string guid in AssetDatabase.FindAssets("t:VolumeProfile SampleSceneProfile"))
                Configure(AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        private static void Configure(VolumeProfile profile)
        {
            if (profile == null)
                return;
            if (profile.TryGet(out MotionBlur blur))
                blur.active = false;
            Bloom bloom = Get<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(new Color(1f, 0.75f, 0.9f));
            bloom.highQualityFiltering.Override(false);
            Vignette vignette = Get<Vignette>(profile);
            vignette.color.Override(new Color(0.08f, 0.02f, 0.1f));
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            Tonemapping tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);
            ColorAdjustments color = Get<ColorAdjustments>(profile);
            color.contrast.Override(14f);
            color.saturation.Override(12f);
            color.postExposure.Override(0.15f);
            EditorUtility.SetDirty(profile);
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                if (AssetDatabase.Contains(profile))
                    AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }
    }
}
