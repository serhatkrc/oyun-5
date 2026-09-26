using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PG.Render
{
    // Loads sprites by content id from StreamingAssets/Art/{Final,Placeholder}/<category>/<name>.png (EkC; 1 pixel = 1 tile)
    // and packs them into one point-filtered atlas. Missing art gets a small generated block so the game still runs.
    public sealed class SpriteLibrary
    {
        public struct Entry
        {
            public Rect Uv;
            public Vector2 Size; // pixels = tiles
        }

        public Texture2D Atlas { get; private set; }
        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        public static string ArtRoot => Path.Combine(Application.streamingAssetsPath, "Art");

        // ids like "feat.tree_oak" -> <category>/tree_oak.png
        public static SpriteLibrary Load(string category, IReadOnlyList<string> ids)
        {
            var lib = new SpriteLibrary();
            var textures = new Texture2D[ids.Count];
            for (int i = 0; i < ids.Count; i++) textures[i] = LoadOne(category, ids[i]);

            lib.Atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = category + "Atlas" };
            var rects = lib.Atlas.PackTextures(textures, 1, 2048, false);
            lib.Atlas.filterMode = FilterMode.Point;
            for (int i = 0; i < ids.Count; i++)
            {
                lib._entries[ids[i]] = new Entry { Uv = rects[i], Size = new Vector2(textures[i].width, textures[i].height) };
                Object.DestroyImmediate(textures[i]);
            }
            return lib;
        }

        public bool TryGet(string id, out Entry entry) => _entries.TryGetValue(id, out entry);

        static Texture2D LoadOne(string category, string id)
        {
            string name = id.Substring(id.IndexOf('.') + 1);
            foreach (string tier in new[] { "Final", "Placeholder" })
            {
                string path = Path.Combine(ArtRoot, tier, category, name + ".png");
                if (!File.Exists(path)) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
                Object.DestroyImmediate(tex);
            }
            return Fallback(id);
        }

        static Texture2D Fallback(string id)
        {
            var tex = new Texture2D(3, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            uint h = (uint)id.GetHashCode();
            var c = new Color32((byte)(80 + (h & 127)), (byte)(80 + ((h >> 8) & 127)), (byte)(60 + ((h >> 16) & 63)), 255);
            var pixels = new Color32[9];
            for (int i = 0; i < 9; i++) pixels[i] = (i == 0 || i == 2) ? new Color32(0, 0, 0, 0) : c;
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        public void Dispose()
        {
            if (Atlas == null) return;
            if (Application.isPlaying) Object.Destroy(Atlas);
            else Object.DestroyImmediate(Atlas);
            Atlas = null;
        }
    }
}
