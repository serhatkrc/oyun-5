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
        int[] _candidates;
        bool _showRegions;
        int _regionVersion = -1;

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

        public void Bind(WorldMap map, RegionGraph regions)
        {
            Unbind();
            _map = map;
            _regions = regions;
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
                ShowRegions = _showRegions,
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
