using PG.Sim;
using PG.World;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 1.11: one 64x64 layer per chunk in a Texture2DArray, all chunk quads in one mesh (one draw call).
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class MapRenderer : MonoBehaviour
    {
        public const string ShaderResource = "PG_MapChunk";
        static readonly int EraTintId = Shader.PropertyToID("_EraTint");

        [Tooltip("Chunk uploads per frame; visible chunks first.")]
        public int UploadBudget = 12;
        public Color32 SnowColor = new Color32(240, 244, 248, 255);
        public Color32 RoadColor = new Color32(150, 126, 92, 255);
        public Color32[] FirePalette =
        {
            new Color32(255, 214, 90, 255), new Color32(255, 150, 40, 255),
            new Color32(230, 80, 20, 255), new Color32(170, 30, 10, 255),
        };
        public Camera ViewCamera;

        WorldMap _map;
        RegionGraph _regions;
        Texture2DArray _texture;
        Mesh _mesh;
        Material _material;
        NativeArray<Color32> _tileColors, _biomeColors, _firePalette, _pixels;
        NativeArray<int> _chunkList, _islandOf;
        NativeArray<int> _zoneOwner;          // zone owners as last drawn (snapshot, compared against the map)
        NativeArray<Color32> _cityColors;
        int[] _candidates;
        bool _showRegions, _showCities = true;
        int _regionVersion = -1;
        CivState _civ;

        public int LastUploads { get; private set; }
        public int PendingChunks { get; private set; }
        public int SortingOrder { get; set; }

        public bool ShowRegions
        {
            get => _showRegions;
            set
            {
                if (_showRegions == value) return;
                _showRegions = value;
                _map?.MarkAllDirty(DirtyMask.Render);
            }
        }

        public bool ShowCityBorders
        {
            get => _showCities;
            set
            {
                if (_showCities == value) return;
                _showCities = value;
                _map?.MarkAllDirty(DirtyMask.Render);
            }
        }

        public void Bind(WorldMap map, RegionGraph regions, CivState civ = null)
        {
            Unbind();
            _map = map;
            _regions = regions;
            _civ = civ;
            var db = map.Content;

            _tileColors = new NativeArray<Color32>(db.Tiles.Count * 4, Allocator.Persistent);
            for (int t = 0; t < db.Tiles.Count; t++)
                for (int v = 0; v < 4; v++) _tileColors[t * 4 + v] = db.Tiles[t].ParsedColors[v];
            _biomeColors = new NativeArray<Color32>((db.Biomes.Count + 1) * 4, Allocator.Persistent);
            for (int b = 0; b < db.Biomes.Count; b++)
                for (int v = 0; v < 4; v++) _biomeColors[(b + 1) * 4 + v] = db.Biomes[b].ParsedColors[v];
            _firePalette = new NativeArray<Color32>(FirePalette, Allocator.Persistent);
            _pixels = new NativeArray<Color32>(UploadBudget * ChunkColorJob.PixelsPerChunk, Allocator.Persistent);
            _chunkList = new NativeArray<int>(UploadBudget, Allocator.Persistent);
            _islandOf = new NativeArray<int>(1, Allocator.Persistent);
            _zoneOwner = new NativeArray<int>(map.Zones.Length, Allocator.Persistent);
            for (int z = 0; z < map.Zones.Length; z++) _zoneOwner[z] = civ != null ? map.Zones[z].OwnerCity : -1;
            _cityColors = new NativeArray<Color32>(1, Allocator.Persistent);
            SyncCityColors();
            _candidates = new int[map.ChunkCount];

            _texture = new Texture2DArray(WorldMap.ChunkSize, WorldMap.ChunkSize, map.ChunkCount, TextureFormat.RGBA32, false, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "MapChunks",
            };

            var shader = Resources.Load<Shader>(ShaderResource);
            _material = new Material(shader) { name = "MapChunks" };
            _material.SetTexture("_MainTex", _texture);
            Shader.SetGlobalColor(EraTintId, Color.white);

            _mesh = BuildMesh(map);
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = _material;
            mr.sortingOrder = SortingOrder;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            map.MarkAllDirty(DirtyMask.Render);
            _regionVersion = -1;
        }

        public void Unbind()
        {
            _map = null;
            _regions = null;
            _civ = null;
            if (_zoneOwner.IsCreated) _zoneOwner.Dispose();
            if (_cityColors.IsCreated) _cityColors.Dispose();
            if (_tileColors.IsCreated) _tileColors.Dispose();
            if (_biomeColors.IsCreated) _biomeColors.Dispose();
            if (_firePalette.IsCreated) _firePalette.Dispose();
            if (_pixels.IsCreated) _pixels.Dispose();
            if (_chunkList.IsCreated) _chunkList.Dispose();
            if (_islandOf.IsCreated) _islandOf.Dispose();
            Release(_texture);
            Release(_mesh);
            Release(_material);
            _texture = null;
            _mesh = null;
            _material = null;
        }

        // Uploads everything now (thumbnails, first frame after load).
        public void FlushAll()
        {
            while (_map != null && _map.CountDirty(DirtyMask.Render) > 0) UploadDirty();
        }

        void LateUpdate()
        {
            if (_map == null) return;
            if (_showRegions && _regions != null && _regions.Version != _regionVersion)
            {
                _regionVersion = _regions.Version;
                _map.MarkAllDirty(DirtyMask.Render);
            }
            UploadDirty();
        }

        void UploadDirty()
        {
            if (_civ != null)
            {
                SyncCityColors();
                SyncZoneOwners();
            }
            int count = CollectDirty();
            LastUploads = count;
            if (count == 0) return;

            if (_showRegions) CopyIslands();

            var tables = _map.Tables;
            new ChunkColorJob
            {
                Width = _map.Width,
                Height = _map.Height,
                ChunksX = _map.ChunksX,
                ZonesX = _map.ZonesX,
                ShowRegions = _showRegions,
                ShowCities = _showCities && _civ != null,
                Snow = SnowColor,
                Road = RoadColor,
                ChunkList = _chunkList,
                Ground = _map.Ground,
                Biome = _map.Biome,
                Variant = _map.Variant,
                Fire = _map.Fire,
                Flags = _map.Flags,
                TileColors = _tileColors,
                BiomeColors = _biomeColors,
                FirePalette = _firePalette,
                ReliefLevel = tables.ReliefLevel,
                Material = tables.Material,
                ReliefShade = tables.ReliefShade,
                LandRegion = _regions.LandRegion,
                WaterRegion = _regions.WaterRegion,
                IslandOf = _islandOf,
                ZoneOwner = _zoneOwner,
                CityColors = _cityColors,
                Pixels = _pixels,
            }.Schedule(count * ChunkColorJob.PixelsPerChunk, 1024).Complete();

            for (int k = 0; k < count; k++)
            {
                int chunk = _chunkList[k];
                _texture.SetPixelData(_pixels, 0, chunk, k * ChunkColorJob.PixelsPerChunk);
                _map.ClearDirty(chunk, DirtyMask.Render);
            }
            _texture.Apply(false, false);
        }

        // City colours never change after founding; the array only grows with the city list.
        void SyncCityColors()
        {
            if (_civ == null) return;
            var cities = _civ.Cities;
            if (cities.Count > _cityColors.Length)
            {
                _cityColors.Dispose();
                _cityColors = new NativeArray<Color32>(Mathf.NextPowerOfTwo(cities.Count), Allocator.Persistent);
            }
            for (int c = 0; c < cities.Count; c++) _cityColors[c] = cities[c].Color;
        }

        // CivState marks a zone's chunk Render-dirty when its owner changes. A border line also depends on the
        // neighbouring zone, which may lie in another chunk: compare the zones of every dirty chunk with the snapshot
        // and also mark the chunks of the 4 neighbour zones of each changed zone.
        void SyncZoneOwners()
        {
            const int zonesPerChunk = WorldMap.ChunkSize / WorldMap.ZoneSize;
            int zonesX = _map.ZonesX, zonesY = _map.ZonesY;
            var zones = _map.Zones;
            int chunkCount = _map.ChunkCount;
            for (int chunk = 0; chunk < chunkCount; chunk++)
            {
                if ((_map.Chunks[chunk].Dirty & DirtyMask.Render) == 0) continue;
                int zx0 = (chunk % _map.ChunksX) * zonesPerChunk, zy0 = (chunk / _map.ChunksX) * zonesPerChunk;
                for (int zy = zy0; zy < zy0 + zonesPerChunk && zy < zonesY; zy++)
                    for (int zx = zx0; zx < zx0 + zonesPerChunk && zx < zonesX; zx++)
                    {
                        int z = zy * zonesX + zx;
                        int owner = zones[z].OwnerCity;
                        if (owner == _zoneOwner[z]) continue;
                        _zoneOwner[z] = owner;
                        MarkZoneChunk(zx - 1, zy, chunk);
                        MarkZoneChunk(zx + 1, zy, chunk);
                        MarkZoneChunk(zx, zy - 1, chunk);
                        MarkZoneChunk(zx, zy + 1, chunk);
                    }
            }
        }

        void MarkZoneChunk(int zx, int zy, int self)
        {
            if (zx < 0 || zy < 0 || zx >= _map.ZonesX || zy >= _map.ZonesY) return;
            int chunk = _map.ChunkIndexOf(zx << WorldMap.ZoneShift, zy << WorldMap.ZoneShift);
            if (chunk != self) _map.MarkChunkDirty(chunk, DirtyMask.Render);
        }

        // Visible dirty chunks first, then the rest, up to the upload budget.
        int CollectDirty()
        {
            int total = 0;
            for (int c = 0; c < _map.ChunkCount; c++)
                if ((_map.Chunks[c].Dirty & DirtyMask.Render) != 0) _candidates[total++] = c;
            PendingChunks = total;
            if (total == 0) return 0;

            GetVisibleChunkRect(out int minX, out int minY, out int maxX, out int maxY);
            int count = 0;
            for (int pass = 0; pass < 2 && count < UploadBudget; pass++)
            {
                for (int k = 0; k < total && count < UploadBudget; k++)
                {
                    int c = _candidates[k];
                    if (c < 0) continue;
                    int cx = c % _map.ChunksX, cy = c / _map.ChunksX;
                    bool visible = cx >= minX && cx <= maxX && cy >= minY && cy <= maxY;
                    if (pass == 0 && !visible) continue;
                    _chunkList[count++] = c;
                    _candidates[k] = -1;
                }
            }
            return count;
        }

        void GetVisibleChunkRect(out int minX, out int minY, out int maxX, out int maxY)
        {
            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                minX = minY = 0;
                maxX = _map.ChunksX - 1;
                maxY = _map.ChunksY - 1;
                return;
            }
            var pos = cam.transform.position;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            minX = Mathf.FloorToInt((pos.x - halfW) / WorldMap.ChunkSize);
            maxX = Mathf.FloorToInt((pos.x + halfW) / WorldMap.ChunkSize);
            minY = Mathf.FloorToInt((pos.y - halfH) / WorldMap.ChunkSize);
            maxY = Mathf.FloorToInt((pos.y + halfH) / WorldMap.ChunkSize);
        }

        void CopyIslands()
        {
            int cap = Mathf.Max(1, _regions.RegionCapacity);
            if (_islandOf.Length < cap)
            {
                _islandOf.Dispose();
                _islandOf = new NativeArray<int>(cap * 2, Allocator.Persistent);
            }
            for (int r = 0; r < _regions.RegionCapacity; r++) _islandOf[r] = _regions.IslandOfRegion(r);
        }

        static Mesh BuildMesh(WorldMap map)
        {
            int chunks = map.ChunkCount;
            var vertices = new Vector3[chunks * 4];
            var uvs = new Vector3[chunks * 4];
            var triangles = new int[chunks * 6];
            for (int c = 0; c < chunks; c++)
            {
                float x0 = (c % map.ChunksX) * WorldMap.ChunkSize, y0 = (c / map.ChunksX) * WorldMap.ChunkSize;
                float x1 = x0 + WorldMap.ChunkSize, y1 = y0 + WorldMap.ChunkSize;
                int v = c * 4;
                vertices[v] = new Vector3(x0, y0, 0f);
                vertices[v + 1] = new Vector3(x1, y0, 0f);
                vertices[v + 2] = new Vector3(x1, y1, 0f);
                vertices[v + 3] = new Vector3(x0, y1, 0f);
                uvs[v] = new Vector3(0f, 0f, c);
                uvs[v + 1] = new Vector3(1f, 0f, c);
                uvs[v + 2] = new Vector3(1f, 1f, c);
                uvs[v + 3] = new Vector3(0f, 1f, c);
                int t = c * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;
            }

            var mesh = new Mesh { name = "MapChunks", indexFormat = chunks * 4 > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.SetUVs(0, uvs);
            mesh.triangles = triangles;
            mesh.bounds = new Bounds(new Vector3(map.Width * 0.5f, map.Height * 0.5f, 0f), new Vector3(map.Width, map.Height, 1f));
            return mesh;
        }

        static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void OnDestroy() => Unbind();
    }
}
