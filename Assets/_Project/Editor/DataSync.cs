using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PG.EditorTools
{
    // Mirrors <repo>/Data/*.json into Assets/StreamingAssets/Data.
    // A copy instead of a symlink because symlinks are unreliable on Windows (EkB 3.3 step 7).
    [InitializeOnLoad]
    public sealed class DataSync : IPreprocessBuildWithReport
    {
        static DataSync()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => Sync();

        [MenuItem("PixelGenesis/Sync Data")]
        public static void Sync()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            int changed = Mirror(Path.Combine(root, "Data"), Path.Combine(Application.streamingAssetsPath, "Data"));
            foreach (string tier in new[] { "Placeholder", "Final" })
                changed += MirrorTree(Path.Combine(root, "Art", tier), Path.Combine(Application.streamingAssetsPath, "Art", tier), "*.png");
            if (changed == 0) return;
            AssetDatabase.Refresh();
            Debug.Log($"[DataSync] {changed} file(s) updated in StreamingAssets/Data and Art");
        }

        // Copies top-level *.json from src to dst when content differs and deletes dst *.json missing in src.
        // Subfolders (_generator, _schemas) are tooling, not runtime data, so they are skipped.
        // Returns the number of files copied or deleted.
        public static int Mirror(string src, string dst)
        {
            if (!Directory.Exists(src)) throw new DirectoryNotFoundException($"Data folder not found: {src}");
            Directory.CreateDirectory(dst);

            int changed = 0;
            foreach (string s in Directory.GetFiles(src, "*.json"))
            {
                string d = Path.Combine(dst, Path.GetFileName(s));
                if (File.Exists(d) && SameContent(s, d)) continue;
                File.Copy(s, d, true);
                changed++;
            }

            foreach (string d in Directory.GetFiles(dst, "*.json"))
            {
                if (File.Exists(Path.Combine(src, Path.GetFileName(d)))) continue;
                File.Delete(d);
                if (File.Exists(d + ".meta")) File.Delete(d + ".meta");
                changed++;
            }

            return changed;
        }

        // Recursive mirror for runtime art (sprites are loaded by id at runtime, Bölüm 2.4 / EkC). Missing src = nothing to do.
        public static int MirrorTree(string src, string dst, string pattern)
        {
            if (!Directory.Exists(src)) return 0;
            int changed = 0;
            foreach (string s in Directory.GetFiles(src, pattern, SearchOption.AllDirectories))
            {
                string d = Path.Combine(dst, Path.GetRelativePath(src, s));
                if (File.Exists(d) && SameContent(s, d)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(d));
                File.Copy(s, d, true);
                changed++;
            }
            if (!Directory.Exists(dst)) return changed;
            foreach (string d in Directory.GetFiles(dst, pattern, SearchOption.AllDirectories))
            {
                if (File.Exists(Path.Combine(src, Path.GetRelativePath(dst, d)))) continue;
                File.Delete(d);
                if (File.Exists(d + ".meta")) File.Delete(d + ".meta");
                changed++;
            }
            return changed;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) Sync();
        }

        static bool SameContent(string a, string b)
        {
            if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
            return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
        }
    }
}
