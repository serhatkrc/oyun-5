using System.Collections.Generic;
using PG.Content;
using PG.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 2.4 render: trees, plants and veins as atlas quads, one mesh per chunk, rebuilt when the chunk is Features-dirty.
    public sealed class FeatureRenderer : MonoBehaviour
    {
        public const int SortingOrder = 10;
        static readonly float[] StageScale = { 0.4f, 0.7f, 1f, 1f };
        static readonly Color32 OldTint = new Color32(200, 196, 190, 255);

        [Tooltip("Chunk mesh rebuilds per frame; visible chunks first.")]
        public int RebuildBudget = 16;
        [Tooltip("Features are hidden when zoomed out further than this orthographic size (LOD 3).")]
        public float HideAboveOrthoSize = 160f;
        public Camera ViewCamera;

        WorldMap _map;
        SpriteLibrary _sprites;
        Material _material;
        Rect[] _uv;       // by feature map value
        Vector2[] _size;
        MeshFilter[] _chunks;
        Transform _root;
        readonly List<int> _dirty = new List<int>();
        readonly List<Vector3> _v = new List<Vector3>(4096);
        readonly List<Vector2> _t = new List<Vector2>(4096);
        readonly List<Color32> _c = new List<Color32>(4096);
        readonly List<int> _i = new List<int>(6144);

        public int LastRebuilds { get; private set; }

        public void Bind(WorldMap map)
        {
            Unbind();
            _map = map;
            var db = map.Content;
            if (_sprites == null)
            {
                var ids = new List<string>(db.Features.Count);
                for (int f = 0; f < db.Features.Count; f++) ids.Add(db.Features[f].Id);
                _sprites = SpriteLibrary.Load("features", ids);
                _material = new Material(Resources.Load<Shader>("PG_Sprite")) { name = "Features", mainTexture = _sprites.Atlas };
            }
            _uv = new Rect[db.Features.Count + 1];
            _size = new Vector2[db.Features.Count + 1];
            for (int f = 0; f < db.Features.Count; f++)
            {
                _sprites.TryGet(db.Features[f].Id, out var e);
                _uv[f + 1] = e.Uv;
                _size[f + 1] = e.Size;
            }

            _root = new GameObject("FeatureChunks").transform;
            _root.SetParent(transform, false);
            _chunks = new MeshFilter[map.ChunkCount];
            for (int c = 0; c < map.ChunkCount; c++)
            {
                var go = new GameObject("Chunk" + c);
                go.transform.SetParent(_root, false);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = new Mesh { name = "Features" + c };
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _material;
                mr.sortingOrder = SortingOrder;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                _chunks[c] = mf;
            }
            map.MarkAllDirty(DirtyMask.Features);
        }

        public void Unbind()
        {
            _map = null;
            if (_chunks != null)
                foreach (var mf in _chunks)
                    if (mf != null) Release(mf.sharedMesh);
            _chunks = null;
            if (_root != null) Release(_root.gameObject);
            _root = null;
        }

        public void FlushAll()
        {
            while (_map != null && _map.CountDirty(DirtyMask.Features) > 0) Rebuild(int.MaxValue);
        }

        void LateUpdate()
        {
            if (_map == null) return;
            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            bool show = cam == null || !cam.orthographic || cam.orthographicSize <= HideAboveOrthoSize;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            Rebuild(RebuildBudget);
        }

        void Rebuild(int budget)
        {
            _dirty.Clear();
            for (int c = 0; c < _map.ChunkCount; c++)
                if ((_map.Chunks[c].Dirty & DirtyMask.Features) != 0) _dirty.Add(c);
            if (_dirty.Count == 0)
            {
                LastRebuilds = 0;
                return;
            }

            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            if (cam != null && cam.orthographic && budget < _dirty.Count)
            {
                Vector3 p = cam.transform.position;
                _dirty.Sort((a, b) => Distance(a, p).CompareTo(Distance(b, p)));
            }

            int n = Mathf.Min(budget, _dirty.Count);
            for (int k = 0; k < n; k++) BuildChunk(_dirty[k]);
            LastRebuilds = n;
        }

        float Distance(int chunk, Vector3 p)
        {
            float cx = (chunk % _map.ChunksX + 0.5f) * WorldMap.ChunkSize - p.x;
            float cy = (chunk / _map.ChunksX + 0.5f) * WorldMap.ChunkSize - p.y;
            return cx * cx + cy * cy;
        }

        void BuildChunk(int chunk)
        {
            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear();
            int x0 = (chunk % _map.ChunksX) * WorldMap.ChunkSize, y0 = (chunk / _map.ChunksX) * WorldMap.ChunkSize;
            var feature = _map.Feature;
            var state = _map.FeatureState;
            var white = new Color32(255, 255, 255, 255);

            // Top rows first so sprites further south draw over the ones behind them.
            for (int y = y0 + WorldMap.ChunkSize - 1; y >= y0; y--)
            {
                for (int x = x0; x < x0 + WorldMap.ChunkSize; x++)
                {
                    int i = y * _map.Width + x;
                    ushort f = feature[i];
                    if (f == 0) continue;
                    int stage = state[i] & 3;
                    byte kind = _map.Tables.FeatureKind[f];
                    float scale = kind == FeatureDef.KindOre ? 1f : StageScale[stage];
                    Vector2 size = _size[f] * scale;
                    Rect uv = _uv[f];
                    float left = x + 0.5f - size.x * 0.5f, bottom = y;
                    int v = _v.Count;
                    _v.Add(new Vector3(left, bottom, 0f));
                    _v.Add(new Vector3(left + size.x, bottom, 0f));
                    _v.Add(new Vector3(left + size.x, bottom + size.y, 0f));
                    _v.Add(new Vector3(left, bottom + size.y, 0f));
                    _t.Add(new Vector2(uv.xMin, uv.yMin));
                    _t.Add(new Vector2(uv.xMax, uv.yMin));
                    _t.Add(new Vector2(uv.xMax, uv.yMax));
                    _t.Add(new Vector2(uv.xMin, uv.yMax));
                    var tint = stage == 3 && kind == FeatureDef.KindTree ? OldTint : white;
                    _c.Add(tint); _c.Add(tint); _c.Add(tint); _c.Add(tint);
                    _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
                    _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
                }
            }

            var mesh = _chunks[chunk].sharedMesh;
            mesh.Clear();
            mesh.indexFormat = _v.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _t);
            mesh.SetColors(_c);
            mesh.SetTriangles(_i, 0);
            mesh.bounds = new Bounds(new Vector3(x0 + WorldMap.ChunkSize * 0.5f, y0 + WorldMap.ChunkSize * 0.5f, 0f),
                                     new Vector3(WorldMap.ChunkSize + 16f, WorldMap.ChunkSize + 16f, 1f));
            _map.ClearDirty(chunk, DirtyMask.Features);
        }

        static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void OnDestroy()
        {
            Unbind();
            _sprites?.Dispose();
            Release(_material);
        }
    }
}
