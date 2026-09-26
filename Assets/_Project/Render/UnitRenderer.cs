using System;
using System.Collections.Generic;
using System.IO;
using PG.Content;
using PG.Sim;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 3.10: units as palette-mapped sprite quads in one dynamic mesh, rebuilt per frame for the visible area only.
    // Sheets are 4 columns x 6 rows (idle, walk, attack, swim, sleep, death) as written by Tools/placeholder_art.py.
    // ponytail: dynamic mesh instead of RenderMeshInstanced; fine for thousands of quads, switch when profiling says so.
    public sealed class UnitRenderer : MonoBehaviour
    {
        public const int SortingOrder = 20;
        public const int Columns = 4, Rows = 6;
        static readonly int[] FrameCount = { 2, 4, 3, 2, 1, 3 };
        static readonly float[] FrameMs = { 400f, 120f, 100f, 250f, 1000f, 150f };
        const int AnimIdle = 0, AnimWalk = 1, AnimAttack = 2, AnimSwim = 3, AnimSleep = 4;

        [Tooltip("Above this orthographic size units draw as 1-tile dots (LOD 2); features/clouds hide at a similar zoom.")]
        public float DotAboveOrthoSize = 120f;
        public float HideAboveOrthoSize = 400f;
        public Camera ViewCamera;

        SimWorld _sim;
        Texture2D _atlas, _palette;
        Material _material;
        Mesh _mesh;
        MeshRenderer _renderer;
        Rect[] _sheet;         // per species
        Vector2[] _frameSize;  // per species, pixels
        Rect _dot;
        int _paletteRows, _projectileRow;
        readonly List<Vector3> _v = new List<Vector3>(8192);
        readonly List<Vector2> _t = new List<Vector2>(8192);
        readonly List<Vector2> _r = new List<Vector2>(8192);
        readonly List<Color32> _c = new List<Color32>(8192);
        readonly List<int> _i = new List<int>(12288);
        int[] _order = Array.Empty<int>();
        float[] _keys = Array.Empty<float>();

        public int LastDrawn { get; private set; }

        public void Bind(SimWorld sim)
        {
            _sim = sim;
            if (_material != null) return;
            var db = sim.Content;
            Build(db);
            _material = new Material(Resources.Load<Shader>("PG_Unit")) { name = "Units" };
            _material.SetTexture("_MainTex", _atlas);
            _material.SetTexture("_Palette", _palette);
            _mesh = new Mesh { name = "Units", indexFormat = IndexFormat.UInt32 };
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.sortingOrder = SortingOrder;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        public void Unbind() => _sim = null;

        void Build(ContentDB db)
        {
            int n = db.Species.Count;
            var textures = new Texture2D[n + 1];
            for (int s = 0; s < n; s++) textures[s] = LoadSheet(db.Species[s].Id);
            var dot = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            dot.SetPixel(0, 0, new Color32(84, 84, 84, 255)); // index 3: body main color
            dot.Apply();
            textures[n] = dot;

            _atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "UnitAtlas" };
            var rects = _atlas.PackTextures(textures, 1, 4096, false);
            _atlas.filterMode = FilterMode.Point;
            _atlas.wrapMode = TextureWrapMode.Clamp;
            _sheet = new Rect[n];
            _frameSize = new Vector2[n];
            for (int s = 0; s < n; s++)
            {
                _sheet[s] = rects[s];
                _frameSize[s] = new Vector2(textures[s].width / (float)Columns, textures[s].height / (float)Rows);
            }
            _dot = rects[n];
            foreach (var t in textures) Release(t);

            // Palette rows: one per species + projectiles.
            _paletteRows = n + 1;
            _projectileRow = n;
            _palette = new Texture2D(8, _paletteRows, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "UnitPalette" };
            for (int s = 0; s < n; s++) WriteRow(_palette, s, BaseColor(db.Species[s]));
            WriteRow(_palette, _projectileRow, new Color(0.35f, 0.3f, 0.25f));
            _palette.Apply();
        }

        static Texture2D LoadSheet(string id)
        {
            string name = id.Substring(id.IndexOf('.') + 1);
            foreach (string tier in new[] { "Final", "Placeholder" })
            {
                string path = Path.Combine(SpriteLibrary.ArtRoot, tier, "units", name + ".png");
                if (!File.Exists(path)) continue;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(File.ReadAllBytes(path))) return tex;
                Release(tex);
            }
            // missing art: a 3x3 body block in every frame
            var fallback = new Texture2D(Columns * 3, Rows * 3, TextureFormat.RGBA32, false);
            var px = new Color32[fallback.width * fallback.height];
            for (int k = 0; k < px.Length; k++) px[k] = new Color32(84, 84, 84, 255);
            fallback.SetPixels32(px);
            fallback.Apply();
            return fallback;
        }

        // Placeholder colouring by species keyword, otherwise a hue from the id (real phenotypes arrive in Bölüm 4).
        static Color BaseColor(SpeciesDef sp)
        {
            string id = sp.Id;
            (string key, Color c)[] table =
            {
                ("sheep", new Color(0.92f, 0.9f, 0.85f)), ("cow", new Color(0.55f, 0.4f, 0.3f)), ("wolf", new Color(0.5f, 0.5f, 0.52f)),
                ("bear", new Color(0.4f, 0.28f, 0.18f)), ("fox", new Color(0.85f, 0.45f, 0.15f)), ("rabbit", new Color(0.75f, 0.68f, 0.58f)),
                ("deer", new Color(0.62f, 0.44f, 0.28f)), ("boar", new Color(0.35f, 0.27f, 0.22f)), ("chicken", new Color(0.95f, 0.93f, 0.88f)),
                ("frog", new Color(0.35f, 0.65f, 0.3f)), ("snake", new Color(0.4f, 0.55f, 0.25f)), ("crocodile", new Color(0.3f, 0.45f, 0.28f)),
                ("human", new Color(0.87f, 0.7f, 0.56f)), ("elf", new Color(0.93f, 0.85f, 0.7f)), ("dwarf", new Color(0.8f, 0.6f, 0.48f)),
                ("orc", new Color(0.45f, 0.6f, 0.3f)), ("zombie", new Color(0.5f, 0.62f, 0.45f)), ("skeleton", new Color(0.9f, 0.88f, 0.8f)),
                ("dragon", new Color(0.7f, 0.2f, 0.15f)), ("fairy", new Color(0.9f, 0.6f, 0.9f)), ("slime", new Color(0.4f, 0.8f, 0.4f)),
                ("imp", new Color(0.85f, 0.3f, 0.1f)), ("bee", new Color(0.95f, 0.8f, 0.2f)), ("crow", new Color(0.2f, 0.2f, 0.25f)),
            };
            foreach (var (key, c) in table)
                if (id.Contains(key)) return c;
            uint h = (uint)id.GetHashCode();
            return Color.HSVToRGB((h % 360) / 360f, 0.45f, 0.7f);
        }

        static void WriteRow(Texture2D palette, int row, Color body)
        {
            palette.SetPixel(0, row, body * 0.35f);           // 1 outline
            palette.SetPixel(1, row, body * 0.7f);            // 2 body shadow
            palette.SetPixel(2, row, body);                   // 3 body
            palette.SetPixel(3, row, Color.Lerp(body, Color.white, 0.3f)); // 4 light
            palette.SetPixel(4, row, new Color(0.3f, 0.34f, 0.45f)); // 5 cloth shadow (kingdom colour later)
            palette.SetPixel(5, row, new Color(0.42f, 0.48f, 0.62f)); // 6 cloth
            palette.SetPixel(6, row, new Color(0.95f, 0.85f, 0.3f)); // 7 accent
            palette.SetPixel(7, row, new Color(0.96f, 0.96f, 0.92f)); // 8 shine
        }

        void LateUpdate()
        {
            if (_sim == null || _mesh == null) return;
            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            float ortho = cam != null && cam.orthographic ? cam.orthographicSize : 50f;
            _renderer.enabled = ortho <= HideAboveOrthoSize;
            if (!_renderer.enabled) return;
            Rebuild(cam, ortho > DotAboveOrthoSize);
        }

        public void Rebuild(Camera cam, bool dots)
        {
            var u = _sim.Units.Store;
            float2 min = new float2(-1e6f), max = new float2(1e6f);
            if (cam != null && cam.orthographic)
            {
                var p = cam.transform.position;
                float h = cam.orthographicSize + 12f, w = cam.orthographicSize * cam.aspect + 12f;
                min = new float2(p.x - w, p.y - h);
                max = new float2(p.x + w, p.y + h);
            }

            int n = u.Alive.Length;
            if (_order.Length < n)
            {
                _order = new int[math.ceilpow2(n)];
                _keys = new float[_order.Length];
            }
            int visible = 0;
            for (int k = 0; k < n; k++)
            {
                int i = u.Alive[k];
                float2 pos = u.Pos[i];
                if (math.any(pos < min) || math.any(pos > max) || u.Boat[i] >= 0) continue; // passengers are inside their boat
                _order[visible] = i;
                _keys[visible] = -pos.y; // north first, so southern units overlap them
                visible++;
            }
            Array.Sort(_keys, _order, 0, visible);

            _v.Clear(); _t.Clear(); _r.Clear(); _c.Clear(); _i.Clear();
            float time = Time.time * 1000f;
            for (int k = 0; k < visible; k++)
            {
                int i = _order[k];
                int species = u.Species[i];
                float row = (species + 0.5f) / _paletteRows;
                float2 pos = u.Pos[i];
                var tint = Tint(u, i);
                if (dots)
                {
                    Quad(new Vector2(pos.x - 0.5f, pos.y - 0.5f), new Vector2(1f, 1f), _dot, false, row, tint);
                    continue;
                }
                int anim = Anim(u, i);
                int frame = (int)(time / FrameMs[anim] + i * 7) % FrameCount[anim];
                Vector2 size = _frameSize[species];
                var sheet = _sheet[species];
                float fw = sheet.width / Columns, fh = sheet.height / Rows;
                var uv = new Rect(sheet.xMin + frame * fw, sheet.yMax - (anim + 1) * fh, fw, fh);
                float scale = u.Age[i] == (byte)AgeStage.Baby ? 0.6f : u.Age[i] == (byte)AgeStage.Child ? 0.8f : 1f;
                Vector2 s = size * scale;
                Quad(new Vector2(pos.x - s.x * 0.5f, pos.y - 0.3f + u.Z[i]), s, uv, u.Facing[i] == 1, row, tint);
            }

            var proj = _sim.Units.Projectiles;
            float prow = (_projectileRow + 0.5f) / _paletteRows;
            for (int k = 0; k < proj.Length; k++)
            {
                var p = proj[k];
                Quad(new Vector2(p.Pos.x - 0.5f, p.Pos.y - 0.5f + p.Z), new Vector2(1f, 1f), _dot, false, prow, new Color32(255, 255, 255, 255));
            }

            // Bölüm 5.10 boats: a hull and a mast until ship sprites exist (EkC)
            var meta = _sim.Meta;
            if (meta != null)
                foreach (var b in meta.Boats)
                {
                    if (b.State != BoatState.Sailing || math.any(b.Pos < min) || math.any(b.Pos > max)) continue;
                    Quad(new Vector2(b.Pos.x - 2f, b.Pos.y - 0.8f), new Vector2(4f, 1.4f), _dot, false, prow, new Color32(122, 84, 48, 255));
                    Quad(new Vector2(b.Pos.x - 0.2f, b.Pos.y + 0.6f), new Vector2(0.4f, 2.6f), _dot, false, prow, new Color32(92, 64, 40, 255));
                    Quad(new Vector2(b.Pos.x - 1.2f, b.Pos.y + 1.2f), new Vector2(1.4f, 1.6f), _dot, false, prow, new Color32(236, 230, 212, 255));
                }

            _mesh.Clear();
            _mesh.SetVertices(_v);
            _mesh.SetUVs(0, _t);
            _mesh.SetUVs(1, _r);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_i, 0);
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1f));
            LastDrawn = visible;
        }

        int Anim(UnitStore u, int i)
        {
            if (u.Has(i, UnitFlags.Asleep)) return AnimSleep;
            int2 t = u.Tile(i);
            var map = _sim.World;
            if (!u.Has(i, UnitFlags.Fly) && map.InBounds(t.x, t.y) && !map.IsWalkable(t.x, t.y)) return AnimSwim;
            var task = (UnitTask)u.Task[i];
            if ((task == UnitTask.Attack || task == UnitTask.Hunt) && u.PathHandle[i] < 0 && u.AttackCooldown[i] > 0) return AnimAttack;
            bool moving = u.PathHandle[i] >= 0 || task == UnitTask.Flee || task == UnitTask.Hunt || task == UnitTask.Attack ||
                          task == UnitTask.Follow || task == UnitTask.Mate || task == UnitTask.GoLand;
            return moving ? AnimWalk : AnimIdle;
        }

        Color32 Tint(UnitStore u, int i)
        {
            var w = _sim.Units;
            if (w.StFrozen >= 0 && u.HasStatus(i, w.StFrozen)) return new Color32(170, 210, 255, 255);
            if (w.StBurning >= 0 && u.HasStatus(i, w.StBurning)) return (Time.frameCount / 4 + i) % 2 == 0 ? new Color32(255, 170, 90, 255) : new Color32(255, 120, 60, 255);
            if (u.Has(i, UnitFlags.Untargetable)) return new Color32(255, 255, 255, 110);
            return new Color32(255, 255, 255, 255);
        }

        void Quad(Vector2 origin, Vector2 size, Rect uv, bool flip, float row, Color32 color)
        {
            int v = _v.Count;
            _v.Add(new Vector3(origin.x, origin.y, 0f));
            _v.Add(new Vector3(origin.x + size.x, origin.y, 0f));
            _v.Add(new Vector3(origin.x + size.x, origin.y + size.y, 0f));
            _v.Add(new Vector3(origin.x, origin.y + size.y, 0f));
            float u0 = flip ? uv.xMax : uv.xMin, u1 = flip ? uv.xMin : uv.xMax;
            _t.Add(new Vector2(u0, uv.yMin));
            _t.Add(new Vector2(u1, uv.yMin));
            _t.Add(new Vector2(u1, uv.yMax));
            _t.Add(new Vector2(u0, uv.yMax));
            var r = new Vector2(row, 0f);
            _r.Add(r); _r.Add(r); _r.Add(r); _r.Add(r);
            _c.Add(color); _c.Add(color); _c.Add(color); _c.Add(color);
            _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
            _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
        }

        void OnDestroy()
        {
            Release(_material);
            Release(_atlas);
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
