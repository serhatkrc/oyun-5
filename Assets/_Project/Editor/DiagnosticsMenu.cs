using System;
using System.Diagnostics;
using System.IO;
using PG.Boot;
using PG.Content;
using PG.Core;
using PG.Persistence;
using PG.Render;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PG.EditorTools
{
    // Bölüm 1.16 checks without play mode: every template/size generates, land ratio, determinism, save round trip.
    // Batch: Unity -batchmode -projectPath . -executeMethod PG.EditorTools.DiagnosticsMenu.Run -logFile Logs/diag.log
    public static class DiagnosticsMenu
    {
        const int DiagnosticSlot = 900;

        [MenuItem("PixelGenesis/Run Diagnostics")]
        public static void Run()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                failures += GenerateAll(db);
                if (!Diagnostics.DeterminismCheck(db, new WorldGenSettings { Seed = 42, Size = MapSizePreset.Small }, 1000)) failures++;
                failures += SaveRoundTrip(db);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }

            Debug.Log(failures == 0 ? "[Diagnostics] ALL OK" : $"[Diagnostics] {failures} failure(s)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // Renders a generated world (map, features, clouds, era tint) into Logs/snapshot*.png (needs a graphics device).
        [MenuItem("PixelGenesis/Render Snapshot")]
        public static void RenderSnapshot()
        {
            int failures = 0;
            GameObject camGo = null, mapGo = null, featGo = null, natureGo = null, unitGo = null;
            RenderTexture rt = null;
            SimWorld sim = null;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                var map = WorldGenerator.Generate(new WorldGenSettings { Seed = 2026, Size = MapSizePreset.Medium }, db);
                sim = new SimWorld(db, map, new SimRandomProvider(2026));
                sim.SpawnInitialAnimals();
                var rng = new SimRandom(3, 3);
                for (int k = 0; k < 4; k++)
                    sim.Nature.SpawnCloud(k % 2, new Unity.Mathematics.float2(60 + k * 45, 80 + k * 30), ref rng);
                for (int i = 0; i < 120; i++) sim.Tick();
                var regions = sim.Regions;

                camGo = new GameObject("SnapshotCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = map.Height * 0.5f;
                cam.transform.position = new Vector3(map.Width * 0.5f, map.Height * 0.5f, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;

                mapGo = new GameObject("SnapshotMap", typeof(MeshFilter), typeof(MeshRenderer));
                var renderer = mapGo.AddComponent<MapRenderer>();
                renderer.ViewCamera = cam;
                renderer.Bind(map, regions);
                renderer.FlushAll();
                featGo = new GameObject("SnapshotFeatures");
                var features = featGo.AddComponent<FeatureRenderer>();
                features.ViewCamera = cam;
                features.HideAboveOrthoSize = 1e6f;
                features.Bind(map);
                features.FlushAll();
                natureGo = new GameObject("SnapshotNature");
                var nature = natureGo.AddComponent<NatureRenderer>();
                nature.ViewCamera = cam;
                nature.HideAboveOrthoSize = 1e6f;
                nature.Bind(sim.Nature);
                nature.SendMessage("LateUpdate");
                unitGo = new GameObject("SnapshotUnits");
                var units = unitGo.AddComponent<UnitRenderer>();
                units.ViewCamera = cam;
                units.Bind(sim);
                units.Rebuild(cam, true);

                rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                Capture(cam, rt, "Logs/snapshot.png");

                // Zoomed view on the densest land chunk: sprite scale and stages.
                int best = 0, bestCount = -1;
                for (int c = 0; c < map.ChunkCount; c++)
                {
                    int count = 0, x0 = (c % map.ChunksX) * WorldMap.ChunkSize, y0 = (c / map.ChunksX) * WorldMap.ChunkSize;
                    for (int y = y0; y < y0 + WorldMap.ChunkSize; y++)
                        for (int x = x0; x < x0 + WorldMap.ChunkSize; x++)
                            if (map.Feature[map.Index(x, y)] != 0) count++;
                    if (count > bestCount) { bestCount = count; best = c; }
                }
                cam.orthographicSize = 24f;
                cam.transform.position = new Vector3((best % map.ChunksX + 0.5f) * WorldMap.ChunkSize, (best / map.ChunksX + 0.5f) * WorldMap.ChunkSize, -10f);
                Capture(cam, rt, "Logs/snapshot_zoom.png");

                // Units close up: centre on the densest unit cluster.
                var u = sim.Units.Store;
                if (u.Count > 0)
                {
                    var p = u.Pos[u.Alive[u.Count / 2]];
                    cam.orthographicSize = 16f;
                    cam.transform.position = new Vector3(p.x, p.y, -10f);
                    units.Rebuild(cam, false);
                    Capture(cam, rt, "Logs/snapshot_units.png");
                }

                cam.orthographicSize = map.Height * 0.5f;
                cam.transform.position = new Vector3(map.Width * 0.5f, map.Height * 0.5f, -10f);
                renderer.ShowRegions = true;
                renderer.FlushAll();
                Capture(cam, rt, "Logs/snapshot_regions.png");
                cam.targetTexture = null;
                renderer.Unbind();
                features.Unbind();
                nature.Unbind();
                Debug.Log($"[Snapshot] Saved Logs/snapshot.png, snapshot_zoom.png, snapshot_regions.png (regions {regions.RegionCount}, islands {regions.IslandCount}, clouds {sim.Nature.Clouds.Length}, units {sim.Units.Store.Count})");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            finally
            {
                if (mapGo != null) UnityEngine.Object.DestroyImmediate(mapGo);
                if (featGo != null) UnityEngine.Object.DestroyImmediate(featGo);
                if (natureGo != null) UnityEngine.Object.DestroyImmediate(natureGo);
                if (unitGo != null) UnityEngine.Object.DestroyImmediate(unitGo);
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (rt != null) UnityEngine.Object.DestroyImmediate(rt);
                sim?.Dispose();
            }
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static void Capture(Camera cam, RenderTexture rt, string path)
        {
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static int GenerateAll(ContentDB db)
        {
            int failures = 0;
            for (int t = 0; t < db.WorldGenTemplates.Count; t++)
            {
                var template = db.WorldGenTemplates[t];
                if (template.Mask.Type == "image") continue; // needs a user PNG
                for (int s = 0; s < MapSizes.Count; s++)
                {
                    var settings = new WorldGenSettings { Seed = 1234 + (ulong)s, Size = (MapSizePreset)s };
                    settings.ApplyTemplateDefaults(template);
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        using (var map = WorldGenerator.Generate(settings, db))
                        {
                            double ms = sw.Elapsed.TotalMilliseconds;
                            int water = 0;
                            for (int i = 0; i < map.TileCount; i++)
                                if ((map.Flags[i] & (ushort)TileFlags.Water) != 0) water++;
                            float land = 1f - water / (float)map.TileCount;
                            bool heightBased = template.Mask.Type != "full" && template.Mask.Type != "empty";
                            bool ratioOk = !heightBased || Mathf.Abs(land - settings.LandRatio) <= 0.03f;
                            string line = $"[WorldGen] {template.Id} {settings.Size} {map.Width}x{map.Height}: {ms:0} ms, land {land:0.000} (target {settings.LandRatio:0.00})";
                            if (ratioOk) Debug.Log(line);
                            else Debug.LogWarning(line + " outside ±0.03");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[WorldGen] {template.Id} {settings.Size} failed: {e}");
                        failures++;
                    }
                }
            }
            return failures;
        }

        static int SaveRoundTrip(ContentDB db)
        {
            var settings = new WorldGenSettings { Seed = 77, Size = MapSizePreset.Medium };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "Diagnostics"))
            {
                sim.SpawnInitialAnimals();
                for (int i = 0; i < 250; i++) sim.Tick();
                ulong hash = sim.World.ComputeHash() ^ Diagnostics.UnitHash(sim);
                long tick = sim.Clock.Tick;
                var rng = sim.Rng.GetStream(0);

                SaveSystem.Save(DiagnosticSlot, sim);
                var state = SaveSystem.Load(DiagnosticSlot, db);
                using (var loaded = new SimWorld(db, state.World, state.Rng, state.Clock, state.WorldName))
                {
                    state.ApplyTo(loaded);
                    bool ok = (loaded.World.ComputeHash() ^ Diagnostics.UnitHash(loaded)) == hash && loaded.Clock.Tick == tick &&
                              loaded.Rng.GetStream(0).State == rng.State && loaded.Regions.IslandCount == sim.Regions.IslandCount &&
                              state.Warnings.Count == 0;
                    // Continuing both worlds must stay identical (nature lists, era, clouds restored in order).
                    for (int i = 0; i < 600; i++) { sim.Tick(); loaded.Tick(); }
                    bool same = sim.World.ComputeHash() == loaded.World.ComputeHash() && Diagnostics.UnitHash(sim) == Diagnostics.UnitHash(loaded);
                    if (!same) Debug.LogError("[Save] Worlds diverge after load");
                    ok &= same;
                    long size = new FileInfo(SaveSystem.PathFor(DiagnosticSlot)).Length;
                    SaveSystem.Delete(DiagnosticSlot);
                    if (ok) Debug.Log($"[Save] Round trip OK ({size / 1024} KB, {map.Width}x{map.Height}, units {sim.Units.Store.Count})");
                    else Debug.LogError("[Save] Round trip mismatch");
                    return ok ? 0 : 1;
                }
            }
        }
    }
}
