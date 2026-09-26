using System.Collections.Generic;
using PG.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace PG.Render
{
    // Bölüm 2.7 / 2.12 render: cloud blobs (layer 60) with ground shadows (layer 29), and the global era tint.
    public sealed class NatureRenderer : MonoBehaviour
    {
        public const int CloudOrder = 60, ShadowOrder = 29;
        public const int BlobVariants = 3, BlobW = 24, BlobH = 12;
        static readonly int EraTintId = Shader.PropertyToID("_EraTint");

        [Range(0f, 1f)] public float EraTintStrength = 0.35f;
        public float CloudAlpha = 0.8f, ShadowAlpha = 0.22f;
        public Vector2 ShadowOffset = new Vector2(3f, -3f);
        public float HideAboveOrthoSize = 160f;
        public Camera ViewCamera;

        NatureState _nature;
        Texture2D _blobs;
        Material _material;
        Mesh _cloudMesh, _shadowMesh;
        MeshRenderer _cloudRenderer, _shadowRenderer;
        readonly List<Vector3> _v = new List<Vector3>(256);
        readonly List<Vector2> _t = new List<Vector2>(256);
        readonly List<Color32> _c = new List<Color32>(256);
        readonly List<int> _i = new List<int>(384);

        public void Bind(NatureState nature)
        {
            _nature = nature;
            if (_material != null) return;
            _blobs = MakeBlobs();
            _material = new Material(Resources.Load<Shader>("PG_Sprite")) { name = "Clouds", mainTexture = _blobs };
            _material.SetFloat("_Soft", 1f);
            _cloudMesh = new Mesh { name = "Clouds" };
            _shadowMesh = new Mesh { name = "CloudShadows" };
            _cloudRenderer = MakeLayer("Clouds", _cloudMesh, CloudOrder);
            _shadowRenderer = MakeLayer("CloudShadows", _shadowMesh, ShadowOrder);
        }

        public void Unbind()
        {
            _nature = null;
            Shader.SetGlobalColor(EraTintId, Color.white);
        }

        MeshRenderer MakeLayer(string name, Mesh mesh, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _material;
            mr.sortingOrder = order;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mr;
        }

        void LateUpdate()
        {
            if (_nature == null) return;
            UpdateTint();

            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            bool show = cam == null || !cam.orthographic || cam.orthographicSize <= HideAboveOrthoSize;
            _cloudRenderer.enabled = _shadowRenderer.enabled = show;
            if (!show) return;
            Build(_cloudMesh, Vector2.zero, CloudAlpha, false);
            Build(_shadowMesh, ShadowOffset, ShadowAlpha, true);
        }

        void UpdateTint()
        {
            var era = _nature.Era;
            var content = _nature.Content;
            Color from = content.Eras[era.PreviousEra].ParsedTint, to = content.Eras[era.CurrentEra].ParsedTint;
            Color tint = Color.Lerp(from, to, era.TintBlend);
            Shader.SetGlobalColor(EraTintId, Color.Lerp(Color.white, tint, EraTintStrength));
        }

        void Build(Mesh mesh, Vector2 offset, float alpha, bool shadow)
        {
            _v.Clear(); _t.Clear(); _c.Clear(); _i.Clear();
            var clouds = _nature.Clouds;
            var content = _nature.Content;
            for (int k = 0; k < clouds.Length; k++)
            {
                var cloud = clouds[k];
                float w = cloud.Radius * 2f, h = cloud.Radius;
                float x = cloud.Pos.x + offset.x - w * 0.5f, y = cloud.Pos.y + offset.y - h * 0.5f;
                int variant = k % BlobVariants;
                float u0 = (float)variant / BlobVariants, u1 = (float)(variant + 1) / BlobVariants;
                Color32 col = shadow ? new Color32(0, 0, 0, 255) : content.Clouds[cloud.Type].ParsedColor;
                col.a = (byte)(alpha * 255f);
                int v = _v.Count;
                _v.Add(new Vector3(x, y, 0f));
                _v.Add(new Vector3(x + w, y, 0f));
                _v.Add(new Vector3(x + w, y + h, 0f));
                _v.Add(new Vector3(x, y + h, 0f));
                _t.Add(new Vector2(u0, 0f)); _t.Add(new Vector2(u1, 0f)); _t.Add(new Vector2(u1, 1f)); _t.Add(new Vector2(u0, 1f));
                _c.Add(col); _c.Add(col); _c.Add(col); _c.Add(col);
                _i.Add(v); _i.Add(v + 2); _i.Add(v + 1);
                _i.Add(v); _i.Add(v + 3); _i.Add(v + 2);
            }
            mesh.Clear();
            mesh.SetVertices(_v);
            mesh.SetUVs(0, _t);
            mesh.SetColors(_c);
            mesh.SetTriangles(_i, 0);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(1e6f, 1e6f, 1f));
        }

        // Three pixel blobs side by side: overlapping ellipses, hard edges (pixel art).
        static Texture2D MakeBlobs()
        {
            int w = BlobW * BlobVariants;
            var tex = new Texture2D(w, BlobH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "CloudBlobs" };
            var pixels = new Color32[w * BlobH];
            var rng = new System.Random(7); // render-only decoration, not simulation
            for (int b = 0; b < BlobVariants; b++)
            {
                for (int e = 0; e < 4; e++)
                {
                    bool core = e == 0; // one centered ellipse keeps every blob solid in the middle
                    float cx = core ? BlobW * 0.5f : rng.Next(6, BlobW - 6), cy = core ? BlobH * 0.5f : rng.Next(4, BlobH - 4);
                    float rx = core ? 8 : rng.Next(4, 8), ry = core ? 4 : rng.Next(3, 5);
                    for (int y = 0; y < BlobH; y++)
                        for (int x = 0; x < BlobW; x++)
                        {
                            float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                            if (dx * dx + dy * dy <= 1f) pixels[y * w + b * BlobW + x] = new Color32(255, 255, 255, 255);
                        }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        void OnDestroy()
        {
            Unbind();
            if (_material == null) return;
            Release(_material);
            Release(_blobs);
            Release(_cloudMesh);
            Release(_shadowMesh);
        }

        static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }
    }
}
