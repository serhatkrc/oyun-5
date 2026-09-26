using System;
using System.Collections.Generic;
using PG.Content;
using PG.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 5.4 / EkC 6: buildings as gray-index sprite quads in one mesh, recoloured through a palette texture
    // (PG_Unit shader). Sprite id = "{style}_{building}_{state}" in Art/<tier>/buildings; a missing style falls back to
    // the human style, then to a plain footprint quad. The mesh is rebuilt only when the building signature changes.
    public sealed class BuildingRenderer : MonoBehaviour
    {
        public const int SortingOrder = 15; // above features (10), below units (20)
        const string Category = "buildings";
        const int States = 3;               // construction, complete, ruin
        static readonly string[] StateNames = { "construction", "complete", "ruin" };
        static readonly Color32 White = new Color32(255, 255, 255, 255);
        static readonly Color32 BurningTint = new Color32(255, 150, 80, 255);
        static readonly Color32 AbandonedTint = new Color32(185, 185, 185, 255);
        static readonly Color32 SolidConstructionTint = new Color32(255, 255, 255, 150);
        static readonly Color Wood = new Color(0.6f, 0.47f, 0.33f);

        [Tooltip("Buildings are hidden when zoomed out further than this orthographic size (like features, LOD 3).")]
        public float HideAboveOrthoSize = 160f;
        public Camera ViewCamera;

        SimWorld _sim;
        SpriteLibrary _sprites;
        Material _material;
        Mesh _mesh;
        MeshRenderer _renderer;
        Texture2D _palette;
        Rect _solid;
        Rect[] _uv;          // resolved sprite per [style, building, state]; width 0 = none (plain quad)
        Vector2[] _size;
        int _buildingCount, _styleCount;
        int _paletteRows, _paletteCities = -1;
        ulong _signature;
        bool _force;
        readonly List<Vector3> _v = new List<Vector3>(4096);
        readonly List<Vector2> _t = new List<Vector2>(4096);
        readonly List<Vector2> _r = new List<Vector2>(4096);
        readonly List<Color32> _c = new List<Color32>(4096);
        readonly List<int> _i = new List<int>(6144);
        int[] _order = Array.Empty<int>();
        long[] _keys = Array.Empty<long>();

        public int LastDrawn { get; private set; }
        public int Rebuilds { get; private set; }

        public void Bind(SimWorld sim)
        {
            _sim = sim;
            _force = true;
            _paletteCities = -1;
            if (_material != null) return;
            Build(sim.Content);
            _material = new Material(Resources.Load<Shader>("PG_Unit")) { name = "Buildings" };
            _material.SetTexture("_MainTex", _sprites.Atlas);
            _mesh = new Mesh { name = "Buildings", indexFormat = IndexFormat.UInt32 };
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.sortingOrder = SortingOrder;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        public void Unbind()
        {
            _sim = null;
            if (_mesh != null) _mesh.Clear();
            LastDrawn = 0;
        }

        // Loads every existing {style}_{building}_{state} sprite and resolves the per-style fallbacks once per content.
        void Build(ContentDB db)
        {
            _styleCount = db.BuildingStyles.Count;
            _buildingCount = db.Buildings.Count;
            var styleNames = new string[_styleCount];
            var buildingNames = new string[_buildingCount];
            for (int s = 0; s < _styleCount; s++) styleNames[s] = ShortName(db.BuildingStyles[s].Id);
            for (int b = 0; b < _buildingCount; b++) buildingNames[b] = ShortName(db.Buildings[b].Id);

            var ids = new List<string>(_styleCount * _buildingCount * States);
            for (int s = 0; s < _styleCount; s++)
                for (int b = 0; b < _buildingCount; b++)
                    for (int st = 0; st < States; st++)
                    {
                        string id = SpriteId(styleNames[s], buildingNames[b], st);
                        if (SpriteLibrary.Exists(Category, id)) ids.Add(id);
                    }
            // index 3 (gray 84) = body main colour, so the plain quad takes the material colour of its palette row
            _sprites = SpriteLibrary.Load(Category, ids, new Color32(84, 84, 84, 255));
            _sprites.TryGet(SpriteLibrary.SolidId, out var solid);
            _solid = solid.Uv;

            int human = db.BuildingStyles.IdOrDefault("style.human");
            _uv = new Rect[_styleCount * _buildingCount * States];
            _size = new Vector2[_uv.Length];
            for (int s = 0; s < _styleCount; s++)
                for (int b = 0; b < _buildingCount; b++)
                    for (int st = 0; st < States; st++)
                    {
                        int slot = (s * _buildingCount + b) * States + st;
                        if (_sprites.TryGet(SpriteId(styleNames[s], buildingNames[b], st), out var e) ||
                            (human >= 0 && _sprites.TryGet(SpriteId(styleNames[human], buildingNames[b], st), out e)))
                        {
                            _uv[slot] = e.Uv;
                            _size[slot] = e.Size;
                        }
                    }
        }

        static string ShortName(string id) => id.Substring(id.IndexOf('.') + 1);
        static string SpriteId(string style, string building, int state) => style + "_" + building + "_" + StateNames[state];

        void LateUpdate()
        {
            if (_sim == null || _mesh == null) return;
            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            bool show = cam == null || !cam.orthographic || cam.orthographicSize <= HideAboveOrthoSize;
            _renderer.enabled = show;
            if (!show) return;

            var civ = _sim.Civ;
            if (civ.Cities.Count != _paletteCities)
            {
                RebuildPalette(civ);
                _force = true;
            }
            ulong signature = Signature(civ);
            if (!_force && signature == _signature) return;
            _signature = signature;
            _force = false;
            Rebuild(civ);
        }

        // Cheap change signal: everything the mesh depends on, hashed over the building list (no allocation).
        // A counter in CivState would miss flag toggles (burning) that systems write straight into the list.
        static ulong Signature(CivState civ)
        {
            var list = civ.Buildings;
            ulong h = 14695981039346656037UL;
            int n = list.Length;
            for (int b = 0; b < n; b++)
            {
                var d = list[b];
                if (d.State == BuildingState.Free) continue;
                h = Mix(h, (ulong)b);
                h = Mix(h, (ulong)d.State | ((ulong)d.Flags << 8) | ((ulong)d.Def << 16) | ((ulong)d.Style << 32) | ((ulong)(uint)d.City << 48));
                h = Mix(h, (ulong)(uint)d.Origin.x | ((ulong)(uint)d.Origin.y << 32));
                h = Mix(h, (ulong)d.W | ((ulong)d.H << 8));
            }
            return Mix(h, (ulong)n);
        }

        static ulong Mix(ulong h, ulong v) => (h ^ v) * 1099511628211UL;

        // Rows: one per style (cityless buildings, ruins) + one per city (roof and flag in the city colour, EkC 4.1).
        void RebuildPalette(CivState civ)
        {
            var db = civ.Content;
            int cities = civ.Cities.Count;
            int rows = _styleCount + cities;
            if (_palette == null || rows > _paletteRows)
            {
                int capacity = Mathf.NextPowerOfTwo(Mathf.Max(16, rows));
                Release(_palette);
                _palette = new Texture2D(8, capacity, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "BuildingPalette",
                };
                _paletteRows = capacity;
                _material.SetTexture("_Palette", _palette);
            }
            for (int s = 0; s < _styleCount; s++)
            {
                Color accent = db.BuildingStyles[s].AccentColor;
                WriteRow(s, MaterialColor(accent), accent);
            }
            for (int c = 0; c < cities; c++)
            {
                var city = civ.Cities[c];
                Color accent = city.Style < _styleCount ? (Color)db.BuildingStyles[city.Style].AccentColor : Wood;
                WriteRow(_styleCount + c, MaterialColor(accent), city.Color);
            }
            _palette.Apply(false, false);
            _paletteCities = cities;
        }

        static Color MaterialColor(Color accent) => Color.Lerp(accent, Wood, 0.55f);

        void WriteRow(int row, Color body, Color roof)
        {
            _palette.SetPixel(0, row, body * 0.35f);                            // 1 outline
            _palette.SetPixel(1, row, body * 0.7f);                             // 2 shadow
            _palette.SetPixel(2, row, body);                                    // 3 material
            _palette.SetPixel(3, row, Color.Lerp(body, Color.white, 0.3f));     // 4 material light
            _palette.SetPixel(4, row, roof * 0.7f);                             // 5 roof shadow
            _palette.SetPixel(5, row, roof);                                    // 6 roof / flag
            _palette.SetPixel(6, row, new Color(0.3f, 0.22f, 0.16f));           // 7 door / window frame
            _palette.SetPixel(7, row, new Color(0.98f, 0.9f, 0.62f));           // 8 window light
        }

        void Rebuild(CivState civ)
        {
            var list = civ.Buildings;
            int n = list.Length;
            if (_order.Length < n)
            {
                _order = new int[Mathf.NextPowerOfTwo(n)];
                _keys = new long[_order.Length];
            }
            int count = 0;
            for (int b = 0; b < n; b++)
            {
                var d = list[b];
                if (d.State == BuildingState.Free) continue;
                _order[count] = b;
                _keys[count] = ((long)-d.Origin.y << 32) + d.Origin.x; // north first, so southern roofs overlap them
                count++;
            }
            Array.Sort(_keys, _order, 0, count);

            _v.Clear(); _t.Clear(); _r.Clear(); _c.Clear(); _i.Clear();
            for (int k = 0; k < count; k++)
            {
                var d = list[_order[k]];
                int state = d.State == BuildingState.Construction ? 0 : d.State == BuildingState.Ruin ? 2 : 1;
                int style = d.Style < _styleCount ? d.Style : 0;
                int slot = d.Def < _buildingCount ? (style * _buildingCount + d.Def) * States + state : -1;

                bool ownRow = d.City >= 0 && d.City < civ.Cities.Count && d.State != BuildingState.Ruin;
                int row = ownRow ? _styleCount + d.City : style;
                float v = (row + 0.5f) / _paletteRows;

                Color32 tint = (d.Flags & BuildingFlags.Burning) != 0 ? BurningTint
                             : (d.Flags & BuildingFlags.Abandoned) != 0 ? AbandonedTint : White;

                if (slot >= 0 && _size[slot].x > 0f)
                {
                    // 1 px = 1 tile: sprite width matches the footprint; the roof margin rises above Origin.y + H.
                    Vector2 size = _size[slot];
                    float scale = d.W / size.x;
                    Quad(new Vector2(d.Origin.x, d.Origin.y), new Vector2(d.W, size.y * scale), _uv[slot], v, tint);
                }
                else
                {
                    var uv = new Rect(_solid.center, Vector2.zero);
                    if (d.State == BuildingState.Construction) tint.a = SolidConstructionTint.a;
                    float h = d.State == BuildingState.Ruin ? Mathf.Max(1f, d.H * 0.5f) : d.H;
                    Quad(new Vector2(d.Origin.x, d.Origin.y), new Vector2(d.W, h), uv, v, tint);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_v);
            _mesh.SetUVs(0, _t);
            _mesh.SetUVs(1, _r);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_i, 0);
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1f));
            LastDrawn = count;
            Rebuilds++;
        }

        void Quad(Vector2 origin, Vector2 size, Rect uv, float row, Color32 color)
        {
            int v = _v.Count;
            _v.Add(new Vector3(origin.x, origin.y, 0f));
            _v.Add(new Vector3(origin.x + size.x, origin.y, 0f));
            _v.Add(new Vector3(origin.x + size.x, origin.y + size.y, 0f));
            _v.Add(new Vector3(origin.x, origin.y + size.y, 0f));
            _t.Add(new Vector2(uv.xMin, uv.yMin));
            _t.Add(new Vector2(uv.xMax, uv.yMin));
            _t.Add(new Vector2(uv.xMax, uv.yMax));
            _t.Add(new Vector2(uv.xMin, uv.yMax));
            var r = new Vector2(row, 0f);
            _r.Add(r); _r.Add(r); _r.Add(r); _r.Add(r);
            _c.Add(color); _c.Add(color); _c.Add(color); _c.Add(color);
            _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
            _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
        }

        void OnDestroy()
        {
            _sprites?.Dispose();
            Release(_material);
            Release(_palette);
            Release(_mesh);
        }

        static void Release(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
