using System;
using System.Collections.Generic;
using System.IO;
using PG.Content;
using PG.World;
using UnityEngine;

namespace PG.WorldGen
{
    // Bölüm 1.10.6: PNG -> tiles. Nearest-neighbour scaling, nearest palette color in CIE Lab (CIE76).
    public static class ImageImporter
    {
        const float UnknownColorDistance = 30f;

        struct Entry
        {
            public byte Tile;
            public byte Biome;
            public Vector3 Lab;
        }

        public static void Apply(string path, WorldMap map, ContentDB db)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new FileNotFoundException("Map image not found", path);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!tex.LoadImage(File.ReadAllBytes(path))) throw new InvalidDataException($"Not a readable image: {path}");
                var pixels = tex.GetPixels32();
                int iw = tex.width, ih = tex.height;
                var palette = BuildPalette(db);
                var cache = new Dictionary<int, int>();

                for (int y = 0; y < map.Height; y++)
                {
                    int sy = Math.Min(ih - 1, y * ih / map.Height);
                    for (int x = 0; x < map.Width; x++)
                    {
                        int sx = Math.Min(iw - 1, x * iw / map.Width);
                        var c = pixels[sy * iw + sx];
                        int key = (c.r << 16) | (c.g << 8) | c.b;
                        if (!cache.TryGetValue(key, out int match))
                        {
                            match = Nearest(palette, ToLab(c));
                            cache[key] = match;
                        }

                        int i = map.Index(x, y);
                        if (match >= 0)
                        {
                            map.Ground[i] = palette[match].Tile;
                            map.Biome[i] = palette[match].Biome;
                        }
                        else
                        {
                            map.Ground[i] = GuessByBrightness(c, db);
                            map.Biome[i] = 0;
                        }
                    }
                }
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(tex);
                else UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static List<Entry> BuildPalette(ContentDB db)
        {
            var list = new List<Entry>();
            for (int i = 0; i < db.Tiles.Count; i++)
                list.Add(new Entry { Tile = (byte)i, Biome = 0, Lab = ToLab(Average(db.Tiles[i].ParsedColors)) });

            byte biomeGround = (byte)db.TileByLevel[4];
            for (int i = 0; i < db.Biomes.Count; i++)
            {
                var b = db.Biomes[i];
                if (b.Special) continue;
                list.Add(new Entry { Tile = biomeGround, Biome = b.MapValue, Lab = ToLab(Average(b.ParsedColors)) });
            }
            return list;
        }

        static int Nearest(List<Entry> palette, Vector3 lab)
        {
            int best = -1;
            float bestDist = UnknownColorDistance * UnknownColorDistance;
            for (int k = 0; k < palette.Count; k++)
            {
                float d = (palette[k].Lab - lab).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = k;
                }
            }
            return best;
        }

        // Unknown colors: bluish -> water by darkness, otherwise land level by brightness (white = summit).
        static byte GuessByBrightness(Color32 c, ContentDB db)
        {
            float l = ToLab(c).x;
            bool bluish = c.b > c.r + 10 && c.b >= c.g;
            int level;
            if (bluish) level = l < 25f ? 0 : l < 40f ? 1 : 2;
            else level = l > 85f ? 8 : l > 70f ? 7 : l > 55f ? 6 : 4;
            return (byte)db.TileByLevel[level];
        }

        static Color32 Average(Color32[] colors)
        {
            int r = 0, g = 0, b = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                r += colors[i].r;
                g += colors[i].g;
                b += colors[i].b;
            }
            int n = Math.Max(1, colors.Length);
            return new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
        }

        static Vector3 ToLab(Color32 c)
        {
            float r = Linear(c.r / 255f), g = Linear(c.g / 255f), b = Linear(c.b / 255f);
            float x = (r * 0.4124f + g * 0.3576f + b * 0.1805f) / 0.95047f;
            float y = r * 0.2126f + g * 0.7152f + b * 0.0722f;
            float z = (r * 0.0193f + g * 0.1192f + b * 0.9505f) / 1.08883f;
            float fx = F(x), fy = F(y), fz = F(z);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        static float Linear(float v) => v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);

        static float F(float t) => t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;
    }
}
