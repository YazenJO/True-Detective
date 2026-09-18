using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TrueDetective.Data;

namespace TrueDetective.EditorTools
{
    /// <summary>
    /// One-time project wiring, run automatically the first time the editor loads the
    /// project: creates the Main scene, puts it in the build list, and points the
    /// mobile-facing player settings where they need to be.
    ///
    /// Everything here is also on the True Detective menu so it can be re-run by hand.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string DonePrefKey = "TrueDetective.SetupDone.v1";

        static ProjectSetup()
        {
            // defer: the asset database is not reliable inside a static constructor
            EditorApplication.delayCall += RunOnce;
        }

        private static void RunOnce()
        {
            if (SessionState.GetBool(DonePrefKey, false)) return;
            SessionState.SetBool(DonePrefKey, true);

            if (!File.Exists(ScenePath)) CreateScene();
            EnsureSceneInBuild();
            ApplyPlayerSettings();
        }

        [MenuItem("True Detective/Set Up Project", priority = 0)]
        public static void SetUpProject()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            EnsureSceneInBuild();
            ApplyPlayerSettings();
            Debug.Log("[TrueDetective] setup complete - open " + ScenePath + " and press Play.");
        }

        [MenuItem("True Detective/Open Main Scene", priority = 1)]
        public static void OpenMainScene()
        {
            if (!File.Exists(ScenePath)) CreateScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>
        /// An all-but-empty scene. Bootstrap spawns the game at runtime, so the scene
        /// deliberately holds nothing that could drift out of sync with the code.
        /// </summary>
        private static void CreateScene()
        {
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x14, 0x16, 0x1A, 0xFF);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGo.tag = "MainCamera";

            // present in the scene as well as via Bootstrap, so the object is visible
            // in the hierarchy and its case id can be changed in the Inspector
            var gameGo = new GameObject("TrueDetective");
            gameGo.AddComponent<TrueDetective.UI.Game>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log("[TrueDetective] created " + ScenePath);
        }

        private static void EnsureSceneInBuild()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == ScenePath))
            {
                // make sure it is enabled and first
                for (int i = 0; i < scenes.Count; i++)
                    if (scenes[i].path == ScenePath) scenes[i].enabled = true;
            }
            else
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "TrueDetectiveStudio";
            PlayerSettings.productName = "True Detective";

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Android, "com.truedetective.game");

            // IL2CPP + ARM64 is what Google Play requires for a real upload
            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------
        // case validation, so a broken case file is caught before Play
        // ------------------------------------------------------------------

        [MenuItem("True Detective/Validate Case Files", priority = 20)]
        public static void ValidateCases()
        {
            var files = Directory.Exists("Assets/Resources/Cases")
                ? Directory.GetFiles("Assets/Resources/Cases", "*.json")
                : new string[0];

            if (files.Length == 0)
            {
                Debug.LogWarning("[TrueDetective] no case files in Assets/Resources/Cases");
                return;
            }

            int ok = 0;
            foreach (var f in files)
            {
                string id = Path.GetFileNameWithoutExtension(f);
                var data = CaseLoader.Load(id);
                if (data != null)
                {
                    ok++;
                    Debug.Log("[TrueDetective] " + id + " OK — " +
                              data.locations.Length + " locations, " +
                              data.evidence.Length + " evidence, " +
                              data.report.slots.Length + " report slots");
                }
            }
            Debug.Log("[TrueDetective] validated " + ok + "/" + files.Length + " case files");
        }

        // ------------------------------------------------------------------
        // android build
        // ------------------------------------------------------------------

        [MenuItem("True Detective/Build Android APK", priority = 40)]
        public static void BuildAndroid()
        {
            EnsureSceneInBuild();
            ApplyPlayerSettings();

            Directory.CreateDirectory("Builds");
            string outPath = "Builds/TrueDetective.apk";

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log("[TrueDetective] APK built: " + Path.GetFullPath(outPath) +
                          "  (" + (summary.totalSize / (1024 * 1024)) + " MB)");
            else
                Debug.LogError("[TrueDetective] Android build " + summary.result +
                               " with " + summary.totalErrors + " errors");
        }
    }
}
