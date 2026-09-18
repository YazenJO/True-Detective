using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace TrueDetective.EditorTools
{
    /// <summary>
    /// Imports TextMeshPro's essential resources if the project does not have them yet.
    ///
    /// TMP keeps its settings in an asset the uGUI package ships as a .unitypackage rather
    /// than as part of the package itself. Until that is imported, TMP_Settings.instance is
    /// null and the first TMP_FontAsset.CreateFontAsset call throws a NullReferenceException
    /// deep inside TMP, with nothing pointing at the real cause. Unity normally handles this
    /// with a popup the user has to notice and accept; doing it here means the project works
    /// on a fresh clone without that step.
    /// </summary>
    [InitializeOnLoad]
    public static class TMPResources
    {
        private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string GuardKey = "TrueDetective.TMPImportAttempted";

        static TMPResources()
        {
            EditorApplication.delayCall += EnsureImported;
        }

        [MenuItem("True Detective/Import TextMeshPro Resources", priority = 10)]
        public static void ImportMenu()
        {
            SessionState.SetBool(GuardKey, false);
            EnsureImported(force: true);
        }

        private static void EnsureImported() { EnsureImported(false); }

        private static void EnsureImported(bool force)
        {
            if (!force)
            {
                // one attempt per editor session, or a failed import retries in a loop
                if (SessionState.GetBool(GuardKey, false)) return;
                SessionState.SetBool(GuardKey, true);
                if (HasResources()) return;
            }

            string pkg = FindEssentialPackage();
            if (string.IsNullOrEmpty(pkg))
            {
                Debug.LogError(
                    "[TrueDetective] could not find 'TMP Essential Resources.unitypackage'. " +
                    "Import it by hand from Window > TextMeshPro > Import TMP Essential Resources.");
                return;
            }

            Debug.Log("[TrueDetective] importing TextMeshPro essential resources from:\n" + pkg);
            // interactive:false - no dialog, just bring everything in
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
        }

        private static bool HasResources()
        {
            if (File.Exists(SettingsPath)) return true;
            // the settings asset can also live elsewhere if it was moved
            return TMP_Settings.instance != null;
        }

        /// <summary>
        /// Finds the shipped .unitypackage. It lives in the resolved uGUI package, whose
        /// folder name carries a hash, so the path is resolved rather than hard-coded.
        /// </summary>
        private static string FindEssentialPackage()
        {
            const string leaf = "Package Resources/TMP Essential Resources.unitypackage";

            // 1. wherever the package manager actually resolved com.unity.ugui.
            // Fully qualified: UnityEditor also has an unrelated PackageInfo type.
            var info = UnityEditor.PackageManager.PackageInfo
                       .FindForAssetPath("Packages/com.unity.ugui/package.json");
            if (info != null && !string.IsNullOrEmpty(info.resolvedPath))
            {
                string p = Path.Combine(info.resolvedPath, leaf);
                if (File.Exists(p)) return p;
            }

            // 2. the project's own package cache
            string cache = Path.Combine(Directory.GetCurrentDirectory(), "Library/PackageCache");
            if (Directory.Exists(cache))
            {
                foreach (var dir in Directory.GetDirectories(cache, "com.unity.ugui*"))
                {
                    string p = Path.Combine(dir, leaf);
                    if (File.Exists(p)) return p;
                }
            }

            // 3. the copy bundled inside the editor install
            string builtIn = Path.Combine(
                EditorApplication.applicationContentsPath,
                "Resources/PackageManager/BuiltInPackages/com.unity.ugui/" + leaf);
            if (File.Exists(builtIn)) return builtIn;

            return null;
        }
    }
}
