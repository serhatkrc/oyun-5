using System.Collections.Generic;
using PG.Powers;
using UnityEngine;

namespace PG.UI
{
    // Bölüm 1.13.4: outline of the tiles the brush will touch, as a line mesh (no LineRenderer).
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BrushCursor : MonoBehaviour
    {
        public const string ShaderResource = "PG_Line";
        public Color OutlineColor = new Color(1f, 1f, 1f, 0.85f);

        Mesh _mesh;
        Material _material;
        int _radius = -1;
        BrushShape _shape;
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _indices = new List<int>();

        void Awake()
        {
            _mesh = new Mesh { name = "BrushCursor" };
            _mesh.MarkDynamic();
            _material = new Material(Resources.Load<Shader>(ShaderResource)) { name = "BrushCursor" };
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = GetComponent<MeshRenderer>();
            mr.sharedMaterial = _material;
            mr.sortingOrder = 90; // above the map and world UI
        }

        public void Show(Vector2Int tile, int radius, BrushShape shape)
        {
            if (radius != _radius || shape != _shape) Rebuild(radius, shape);
            transform.position = new Vector3(tile.x, tile.y, -1f);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // Boundary edges of the brush tile set, in tile-local coordinates (tile (0,0) spans 0..1).
        void Rebuild(int radius, BrushShape shape)
        {
            _radius = radius;
            _shape = shape;
            _vertices.Clear();
            _colors.Clear();
            _indices.Clear();
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (!Brushes.Contains(shape, radius, dx, dy)) continue;
                    if (!Inside(dx - 1, dy)) Segment(dx, dy, dx, dy + 1);
                    if (!Inside(dx + 1, dy)) Segment(dx + 1, dy, dx + 1, dy + 1);
                    if (!Inside(dx, dy - 1)) Segment(dx, dy, dx + 1, dy);
                    if (!Inside(dx, dy + 1)) Segment(dx, dy + 1, dx + 1, dy + 1);
                }
            }
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetIndices(_indices, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
        }

        bool Inside(int dx, int dy) =>
            dx >= -_radius && dx <= _radius && dy >= -_radius && dy <= _radius && Brushes.Contains(_shape, _radius, dx, dy);

        void Segment(float x0, float y0, float x1, float y1)
        {
            _indices.Add(_vertices.Count);
            _vertices.Add(new Vector3(x0, y0, 0f));
            _indices.Add(_vertices.Count);
            _vertices.Add(new Vector3(x1, y1, 0f));
            _colors.Add(OutlineColor);
            _colors.Add(OutlineColor);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
