using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using UnityEngine;

namespace PG.Persistence
{
    // Bölüm 1.14. File: [Header][Thumbnail][Section*], section = id | compressedLength | rawLength | Deflate(payload).
    public static class SaveSystem
    {
        public const int ManualSlots = 6;
        public const int AutoSlots = 3;
        public const int AutoSlotBase = 1000; // auto slots are AutoSlotBase + 1 .. AutoSlotBase + AutoSlots
        const int ThumbnailSize = 256;

        static readonly List<ISaveMigration> Migrations = new List<ISaveMigration>();

        public static string SlotsFolder => Path.Combine(Application.persistentDataPath, "saves");

        public static bool IsAutoSlot(int slot) => slot > AutoSlotBase;

        public static string PathFor(int slot) => Path.Combine(SlotsFolder,
            IsAutoSlot(slot) ? $"auto_{slot - AutoSlotBase}.pxgn" : $"slot_{slot}.pxgn");

        // The snapshot is taken now (tick boundary, main thread); compression and disk IO run in the background.
        public static Task SaveAsync(int slot, SimWorld state)
        {
            var snapshot = Snapshot.Take(state);
            string path = PathFor(slot);
            return Task.Run(() => Write(snapshot, path));
        }

        // Blocking variant for application quit.
        public static void Save(int slot, SimWorld state) => Write(Snapshot.Take(state), PathFor(slot));

        public static async Task<GameState> LoadAsync(int slot, ContentDB db)
        {
            string path = PathFor(slot);
            var doc = await Task.Run(() => ReadDocument(path, true));
            return Build(doc, db); // NativeArrays are created back on the main thread
        }

        // Blocking load (tools, diagnostics). Must run on the main thread.
        public static GameState Load(int slot, ContentDB db) => Build(ReadDocument(PathFor(slot), true), db);

        public static IReadOnlyList<SaveSlotInfo> ListSlots()
        {
            var list = new List<SaveSlotInfo>();
            for (int s = 1; s <= ManualSlots; s++) list.Add(Info(s));
            for (int s = 1; s <= AutoSlots; s++) list.Add(Info(AutoSlotBase + s));
            return list;
        }

        public static void Delete(int slot)
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
        }

        // Cyclic autosave: first free auto slot, else the oldest one.
        public static int NextAutoSlot()
        {
            int best = AutoSlotBase + 1;
            DateTime oldest = DateTime.MaxValue;
            for (int s = 1; s <= AutoSlots; s++)
            {
                string path = PathFor(AutoSlotBase + s);
                if (!File.Exists(path)) return AutoSlotBase + s;
                var time = File.GetLastWriteTimeUtc(path);
                if (time < oldest)
                {
                    oldest = time;
                    best = AutoSlotBase + s;
                }
            }
            return best;
        }

        static SaveSlotInfo Info(int slot)
        {
            var info = new SaveSlotInfo { Slot = slot, IsAuto = IsAutoSlot(slot) };
            string path = PathFor(slot);
            if (!File.Exists(path)) return info;
            try
            {
                var doc = ReadDocument(path, false);
                info.Exists = true;
                info.FormatVersion = doc.FormatVersion;
                info.GameVersion = doc.GameVersion;
                info.CreatedUtc = new DateTime(doc.CreatedUtc, DateTimeKind.Utc);
                info.WorldName = doc.WorldName;
                info.Year = doc.Year;
                info.Population = doc.Population;
                info.SizeX = doc.SizeX;
                info.SizeY = doc.SizeY;
                info.ThumbnailPng = doc.ThumbnailPng;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] Unreadable slot {slot}: {e.Message}");
            }
            return info;
        }

        // ---------------- Writing ----------------

        sealed class Snapshot
        {
            public string WorldName, GameVersion;
            public long CreatedUtc, Tick;
            public int SpeedIndex, Year, Population, Width, Height;
            public ulong Seed;
            public ulong[] RngState, RngInc;
            public byte[] Ground, Biome, Variant, Fire, FeatureState, Thumbnail, NatureRaw, LawsRaw, UnitsRaw, CivRaw, MetaRaw;
            public ushort[] Flags, Feature;
            public ZoneData[] Zones;
            public string[] TileKeys, BiomeKeys, FeatureKeys;

            public static Snapshot Take(SimWorld s)
            {
                var map = s.World;
                var db = s.Content;
                var snap = new Snapshot
                {
                    WorldName = s.WorldName ?? "",
                    GameVersion = Application.version,
                    CreatedUtc = DateTime.UtcNow.Ticks,
                    Tick = s.Clock.Tick,
                    SpeedIndex = s.Clock.SpeedIndex,
                    Year = s.Clock.Year,
                    Population = s.Population,
                    Width = map.Width,
                    Height = map.Height,
                    Seed = s.Rng.WorldSeed,
                    RngState = new ulong[SimRandomProvider.StreamCount],
                    RngInc = new ulong[SimRandomProvider.StreamCount],
                    Ground = map.Ground.ToArray(),
                    Biome = map.Biome.ToArray(),
                    Variant = map.Variant.ToArray(),
                    Fire = map.Fire.ToArray(),
                    Flags = map.Flags.ToArray(),
                    Feature = map.Feature.ToArray(),
                    FeatureState = map.FeatureState.ToArray(),
                    Zones = map.Zones.ToArray(),
                    NatureRaw = NatureSave.WriteNature(s),
                    LawsRaw = NatureSave.WriteLaws(s),
                    UnitsRaw = UnitSave.Write(s),
                    CivRaw = CivSave.Write(s),
                    MetaRaw = MetaSave.Write(s),
                    TileKeys = Keys(db.Tiles),
                    BiomeKeys = Keys(db.Biomes),
                    FeatureKeys = Keys(db.Features),
                };
                for (int i = 0; i < SimRandomProvider.StreamCount; i++)
                {
                    var r = s.Rng.GetStream(i);
                    snap.RngState[i] = r.State;
                    snap.RngInc[i] = r.Increment;
                }
                snap.Thumbnail = RenderThumbnail(map, db);
                return snap;
            }

            static string[] Keys<T>(Registry<T> registry) where T : ContentDef
            {
                var keys = new string[registry.Count];
                for (int i = 0; i < keys.Length; i++) keys[i] = registry[i].Id;
                return keys;
            }
        }

        static byte[] RenderThumbnail(WorldMap map, ContentDB db)
        {
            var pixels = new Color32[ThumbnailSize * ThumbnailSize];
            for (int py = 0; py < ThumbnailSize; py++)
            {
                int y = py * map.Height / ThumbnailSize;
                for (int px = 0; px < ThumbnailSize; px++)
                {
                    int x = px * map.Width / ThumbnailSize;
                    int i = map.Index(x, y);
                    int v = map.Variant[i] & 3;
                    byte b = map.Biome[i];
                    pixels[py * ThumbnailSize + px] = b != 0 ? db.Biomes[b - 1].ParsedColors[v] : db.Tiles[map.Ground[i]].ParsedColors[v];
                }
            }
            var tex = new Texture2D(ThumbnailSize, ThumbnailSize, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(pixels);
                tex.Apply(false);
                return tex.EncodeToPNG();
            }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(tex);
                else UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static void Write(Snapshot s, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            using (var fs = File.Create(tmp))
            using (var w = new BinaryWriter(fs, Encoding.UTF8))
            {
                w.Write(Encoding.ASCII.GetBytes(SaveFormat.Magic));
                w.Write(SaveFormat.FormatVersion);
                w.Write(s.GameVersion ?? "");
                w.Write(s.CreatedUtc);
                w.Write(s.WorldName);
                w.Write(s.Year);
                w.Write(s.Population);
                w.Write(s.Width);
                w.Write(s.Height);
                w.Write(s.Thumbnail.Length);
                w.Write(s.Thumbnail);

                WriteSection(w, SaveFormat.SectionContentMap, b =>
                {
                    WriteKeys(b, s.TileKeys);
                    WriteKeys(b, s.BiomeKeys);
                    WriteKeys(b, s.FeatureKeys);
                });
                WriteSection(w, SaveFormat.SectionClock, b =>
                {
                    b.Write(s.Tick);
                    b.Write(s.SpeedIndex);
                });
                WriteSection(w, SaveFormat.SectionRng, b =>
                {
                    b.Write(s.Seed);
                    b.Write(s.RngState.Length);
                    for (int i = 0; i < s.RngState.Length; i++)
                    {
                        b.Write(s.RngState[i]);
                        b.Write(s.RngInc[i]);
                    }
                });
                WriteSection(w, SaveFormat.SectionWorldTiles, b =>
                {
                    b.Write(s.Width);
                    b.Write(s.Height);
                    var indices = new int[WorldMap.ChunkSize * WorldMap.ChunkSize];
                    int chunksX = s.Width / WorldMap.ChunkSize, chunksY = s.Height / WorldMap.ChunkSize;
                    for (int c = 0; c < chunksX * chunksY; c++)
                    {
                        ChunkIndices(c, chunksX, s.Width, indices);
                        Rle.WriteBytes(b, s.Ground, indices);
                        Rle.WriteBytes(b, s.Biome, indices);
                        Rle.WriteBytes(b, s.Variant, indices);
                        Rle.WriteUShorts(b, s.Flags, indices);
                        Rle.WriteBytes(b, s.Fire, indices);
                        Rle.WriteUShorts(b, s.Feature, indices);
                    }
                });
                WriteSection(w, SaveFormat.SectionWorldZones, b =>
                {
                    b.Write(s.Zones.Length);
                    for (int z = 0; z < s.Zones.Length; z++)
                    {
                        b.Write(s.Zones[z].OwnerCity);
                        b.Write(s.Zones[z].TemperatureC);
                        b.Write(s.Zones[z].DominantBiome);
                    }
                });
                WriteSection(w, SaveFormat.SectionFeatureState, b =>
                {
                    var indices = new int[WorldMap.ChunkSize * WorldMap.ChunkSize];
                    int chunksX = s.Width / WorldMap.ChunkSize, chunksY = s.Height / WorldMap.ChunkSize;
                    for (int c = 0; c < chunksX * chunksY; c++)
                    {
                        ChunkIndices(c, chunksX, s.Width, indices);
                        Rle.WriteBytes(b, s.FeatureState, indices);
                    }
                });
                WriteSection(w, SaveFormat.SectionZoneClimate, b =>
                {
                    b.Write(s.Zones.Length);
                    for (int z = 0; z < s.Zones.Length; z++) b.Write(s.Zones[z].BaseTemperatureC);
                });
                WriteSection(w, SaveFormat.SectionNature, b => b.Write(s.NatureRaw));
                WriteSection(w, SaveFormat.SectionLaws, b => b.Write(s.LawsRaw));
                WriteSection(w, SaveFormat.SectionUnits, b => b.Write(s.UnitsRaw));
                WriteSection(w, SaveFormat.SectionCiv, b => b.Write(s.CivRaw));
                WriteSection(w, SaveFormat.SectionMeta, b => b.Write(s.MetaRaw));
            }

            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    File.Delete(path);
                }
            }
            File.Move(tmp, path);
        }

        static void WriteSection(BinaryWriter w, int id, Action<BinaryWriter> body)
        {
            byte[] raw;
            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms, Encoding.UTF8, true)) body(bw);
                raw = ms.ToArray();
            }

            byte[] packed;
            using (var ms = new MemoryStream())
            {
                using (var deflate = new DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) deflate.Write(raw, 0, raw.Length);
                packed = ms.ToArray();
            }

            w.Write(id);
            w.Write(packed.Length);
            w.Write(raw.Length);
            w.Write(packed);
        }

        static void WriteKeys(BinaryWriter w, string[] keys)
        {
            w.Write(keys.Length);
            for (int i = 0; i < keys.Length; i++) w.Write(keys[i]);
        }

        static void ChunkIndices(int chunk, int chunksX, int width, int[] indices)
        {
            int x0 = chunk % chunksX * WorldMap.ChunkSize, y0 = chunk / chunksX * WorldMap.ChunkSize, k = 0;
            for (int y = 0; y < WorldMap.ChunkSize; y++)
                for (int x = 0; x < WorldMap.ChunkSize; x++)
                    indices[k++] = (y0 + y) * width + x0 + x;
        }

        // ---------------- Reading ----------------

        static SaveDocument ReadDocument(string path, bool withSections)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Save file not found", path);
            using (var fs = File.OpenRead(path))
            using (var r = new BinaryReader(fs, Encoding.UTF8))
            {
                string magic = Encoding.ASCII.GetString(r.ReadBytes(4));
                if (magic != SaveFormat.Magic) throw new InvalidDataException("Not a PixelGenesis save file");
                var doc = new SaveDocument
                {
                    FormatVersion = r.ReadInt32(),
                    GameVersion = r.ReadString(),
                    CreatedUtc = r.ReadInt64(),
                    WorldName = r.ReadString(),
                    Year = r.ReadInt32(),
                    Population = r.ReadInt32(),
                    SizeX = r.ReadInt32(),
                    SizeY = r.ReadInt32(),
                };
                doc.ThumbnailPng = r.ReadBytes(r.ReadInt32());
                if (!withSections) return doc;

                while (fs.Position < fs.Length)
                {
                    int id = r.ReadInt32();
                    int packedLength = r.ReadInt32();
                    int rawLength = r.ReadInt32();
                    byte[] packed = r.ReadBytes(packedLength);
                    var raw = new byte[rawLength];
                    using (var ms = new MemoryStream(packed))
                    using (var inflate = new DeflateStream(ms, CompressionMode.Decompress))
                    {
                        int read = 0;
                        while (read < rawLength)
                        {
                            int n = inflate.Read(raw, read, rawLength - read);
                            if (n <= 0) throw new InvalidDataException($"Truncated section {id}");
                            read += n;
                        }
                    }
                    doc.Sections[id] = raw;
                }
                return doc;
            }
        }

        static GameState Build(SaveDocument doc, ContentDB db)
        {
            if (doc.FormatVersion > SaveFormat.FormatVersion)
                throw new InvalidDataException($"Save format {doc.FormatVersion} is newer than this game ({SaveFormat.FormatVersion})");
            for (int v = doc.FormatVersion; v < SaveFormat.FormatVersion; v++)
            {
                var migration = Migrations.Find(m => m.FromVersion == v);
                if (migration == null) throw new InvalidDataException($"No migration from save format {v}");
                migration.Migrate(doc);
            }

            var state = new GameState { WorldName = doc.WorldName };

            byte[] tileRemap, biomeRemap;
            ushort[] featureRemap;
            using (var r = Reader(doc, SaveFormat.SectionContentMap))
            {
                byte soilFallback = (byte)db.TileByLevel[Math.Min(4, db.MaxLevel)];
                string[] tiles = ReadKeys(r), biomes = ReadKeys(r), features = ReadKeys(r);

                tileRemap = new byte[256];
                for (int i = 0; i < tileRemap.Length; i++) tileRemap[i] = soilFallback;
                for (int i = 0; i < tiles.Length && i < 256; i++)
                {
                    int id = db.Tiles.IdOrDefault(tiles[i]);
                    if (id >= 0) tileRemap[i] = (byte)id;
                    else state.Warnings.Add($"tile '{tiles[i]}' -> {db.Tiles[soilFallback].Id}");
                }

                biomeRemap = new byte[256]; // map values: 0 = none, index + 1 otherwise
                for (int i = 0; i < biomes.Length && i < 255; i++)
                {
                    int id = db.Biomes.IdOrDefault(biomes[i]);
                    if (id >= 0) biomeRemap[i + 1] = (byte)(id + 1);
                    else state.Warnings.Add($"biome '{biomes[i]}' -> none");
                }

                featureRemap = new ushort[features.Length + 1];
                for (int i = 0; i < features.Length; i++)
                {
                    int id = db.Features.IdOrDefault(features[i]);
                    if (id >= 0) featureRemap[i + 1] = (ushort)(id + 1);
                    else state.Warnings.Add($"feature '{features[i]}' -> none");
                }
            }

            state.Clock = new GameClock();
            using (var r = Reader(doc, SaveFormat.SectionClock))
            {
                state.Clock.SetTick(r.ReadInt64());
                state.Clock.SetSpeed(r.ReadInt32());
            }

            using (var r = Reader(doc, SaveFormat.SectionRng))
            {
                state.Rng = new SimRandomProvider(r.ReadUInt64());
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    var restored = SimRandom.FromState(r.ReadUInt64(), r.ReadUInt64());
                    if (i < SimRandomProvider.StreamCount) state.Rng.SetStream(i, restored);
                }
            }

            WorldMap map = null;
            try
            {
                using (var r = Reader(doc, SaveFormat.SectionWorldTiles))
                {
                    int w = r.ReadInt32(), h = r.ReadInt32();
                    map = new WorldMap(w, h, db);
                    int n = w * h;
                    var ground = new byte[n];
                    var biome = new byte[n];
                    var variant = new byte[n];
                    var fire = new byte[n];
                    var flags = new ushort[n];
                    var feature = new ushort[n];
                    var indices = new int[WorldMap.ChunkSize * WorldMap.ChunkSize];
                    for (int c = 0; c < map.ChunkCount; c++)
                    {
                        ChunkIndices(c, map.ChunksX, w, indices);
                        Rle.ReadBytes(r, ground, indices);
                        Rle.ReadBytes(r, biome, indices);
                        Rle.ReadBytes(r, variant, indices);
                        Rle.ReadUShorts(r, flags, indices);
                        Rle.ReadBytes(r, fire, indices);
                        Rle.ReadUShorts(r, feature, indices);
                    }

                    for (int i = 0; i < n; i++)
                    {
                        ground[i] = tileRemap[ground[i]];
                        biome[i] = biomeRemap[biome[i]];
                        feature[i] = feature[i] < featureRemap.Length ? featureRemap[feature[i]] : (ushort)0;
                    }
                    map.Ground.CopyFrom(ground);
                    map.Biome.CopyFrom(biome);
                    map.Variant.CopyFrom(variant);
                    map.Fire.CopyFrom(fire);
                    map.Flags.CopyFrom(flags);
                    map.Feature.CopyFrom(feature);
                }

                using (var r = Reader(doc, SaveFormat.SectionWorldZones))
                {
                    int count = r.ReadInt32();
                    for (int z = 0; z < count; z++)
                    {
                        int owner = r.ReadInt32();
                        short temperature = r.ReadInt16();
                        byte dominant = r.ReadByte();
                        if (z >= map.Zones.Length) continue;
                        var zone = map.Zones[z];
                        zone.OwnerCity = owner;
                        zone.TemperatureC = temperature;
                        zone.DominantBiome = biomeRemap[dominant];
                        map.Zones[z] = zone;
                    }
                }

                if (doc.Sections.ContainsKey(SaveFormat.SectionFeatureState))
                    using (var r = Reader(doc, SaveFormat.SectionFeatureState))
                    {
                        var featureState = new byte[map.TileCount];
                        var indices = new int[WorldMap.ChunkSize * WorldMap.ChunkSize];
                        for (int c = 0; c < map.ChunkCount; c++)
                        {
                            ChunkIndices(c, map.ChunksX, map.Width, indices);
                            Rle.ReadBytes(r, featureState, indices);
                        }
                        map.FeatureState.CopyFrom(featureState);
                    }
                else
                    for (int i = 0; i < map.TileCount; i++) // pre-Bölüm 2 save: grown features with full resource
                        if (map.Feature[i] != 0) map.FeatureState[i] = (byte)((map.Tables.FeatureResource[map.Feature[i]] << 2) | 2);

                for (int z = 0; z < map.Zones.Length; z++)
                {
                    var zone = map.Zones[z];
                    zone.BaseTemperatureC = zone.TemperatureC; // fallback when the climate section is missing
                    map.Zones[z] = zone;
                }
                if (doc.Sections.ContainsKey(SaveFormat.SectionZoneClimate))
                    using (var r = Reader(doc, SaveFormat.SectionZoneClimate))
                    {
                        int count = r.ReadInt32();
                        for (int z = 0; z < count; z++)
                        {
                            short baseTemp = r.ReadInt16();
                            if (z >= map.Zones.Length) continue;
                            var zone = map.Zones[z];
                            zone.BaseTemperatureC = baseTemp;
                            map.Zones[z] = zone;
                        }
                    }

                map.RebuildDerivedData();
            }
            catch
            {
                map?.Dispose();
                throw;
            }

            state.World = map;
            doc.Sections.TryGetValue(SaveFormat.SectionNature, out state.NatureRaw);
            doc.Sections.TryGetValue(SaveFormat.SectionLaws, out state.LawsRaw);
            doc.Sections.TryGetValue(SaveFormat.SectionUnits, out state.UnitsRaw);
            doc.Sections.TryGetValue(SaveFormat.SectionCiv, out state.CivRaw);
            doc.Sections.TryGetValue(SaveFormat.SectionMeta, out state.MetaRaw);
            return state;
        }

        static BinaryReader Reader(SaveDocument doc, int section)
        {
            if (!doc.Sections.TryGetValue(section, out var raw)) throw new InvalidDataException($"Missing save section {section}");
            return new BinaryReader(new MemoryStream(raw), Encoding.UTF8);
        }

        static string[] ReadKeys(BinaryReader r)
        {
            var keys = new string[r.ReadInt32()];
            for (int i = 0; i < keys.Length; i++) keys[i] = r.ReadString();
            return keys;
        }
    }
}
