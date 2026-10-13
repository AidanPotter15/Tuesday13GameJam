using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace XIII.EditorTools
{
    // Menu: XIII > 1. Setup Project, then XIII > 2. Build WebGL.
    public static class XIIISetup
    {
        const string Root = "Assets/XIII";
        const string ResourcesDir = Root + "/Resources";
        const string ScenePath = Root + "/XIII.unity";
        const string BuildDir = "Builds/WebGL";

        [MenuItem("XIII/1. Setup Project", priority = 1)]
        public static void Setup()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreateMaterials();
            CreateScene();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[XIII] Setup complete. Press Play, or use XIII > 2. Build WebGL.");
        }

        [MenuItem("XIII/2. Build WebGL", priority = 2)]
        public static void BuildWebGL()
        {
            if (!File.Exists(ScenePath)) Setup();
            ConfigurePlayer();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildDir,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log("[XIII] WebGL build done: " + Path.GetFullPath(BuildDir) + "  (zip the CONTENTS of this folder for itch.io)");
                EditorUtility.RevealInFinder(BuildDir);
            }
            else
            {
                Debug.LogError("[XIII] WebGL build failed: " + report.summary.result + ". Is the WebGL Build Support module installed in Unity Hub?");
            }
        }

        [MenuItem("XIII/Open Game Scene", priority = 20)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) Setup();
            else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        static void CreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesDir)) AssetDatabase.CreateFolder(Root, "Resources");
            bool urp = GraphicsSettings.currentRenderPipeline != null;

            Shader lit = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            Shader unlit = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
            Shader text = Shader.Find("XIII/Text");
            if (lit == null || unlit == null) Debug.LogError("[XIII] Could not find the " + (urp ? "URP" : "Built-in") + " shaders.");

            WriteMaterial("XIII_Lit", lit);
            WriteMaterial("XIII_Unlit", unlit);
            if (text != null) WriteMaterial("XIII_Text", text);
            else Debug.LogWarning("[XIII] XIII/Text shader not found; signs will use the default font shader.");
        }

        static void WriteMaterial(string name, Shader shader)
        {
            if (shader == null) return;
            string path = ResourcesDir + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = shader;
                EditorUtility.SetDirty(existing);
                return;
            }
            AssetDatabase.CreateAsset(new Material(shader), path);
        }

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Set these in the saved scene too, so the build keeps the fog shader variants.
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.145f, 0.11f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.04f;
            RenderSettings.fogColor = new Color(0.04f, 0.033f, 0.024f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;

            new GameObject("XIII").AddComponent<XIIIGame>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "XIII";
            PlayerSettings.companyName = XIIIGame.Author;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            PlayerSettings.runInBackground = true;
            // Gzip + fallback works on itch.io without any server configuration.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
        }
    }
}
