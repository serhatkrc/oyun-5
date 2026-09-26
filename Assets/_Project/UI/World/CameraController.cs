using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PG.UI
{
    [Serializable]
    public sealed class CameraSettings
    {
        public float MinOrthoSize = 6f;      // closest: ~12 tiles on screen
        public float MaxOrthoFactor = 0.6f;  // farthest: map height * 0.6
        public float ZoomStep = 1.15f;       // per wheel notch
        public float ZoomLerp = 12f;         // smooth zoom speed
        public float PanSpeedKeys = 1.2f;    // screen heights per second
        public float EdgePanMargin = 8f;     // px
        public bool EdgePan = true;
        public float PanInertia = 6f;        // damping after drag release
        public float OverscrollTiles = 32f;  // how far past the map edge the view may go
    }

    // Bölüm 1.12: zoom to cursor, keyboard/edge/drag pan with inertia, pixel snapping, LOD broadcast.
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        public CameraSettings Settings = new CameraSettings();

        Camera _cam;
        Vector2 _pos;
        float _ortho, _targetOrtho;
        Vector2 _velocity;
        bool _dragging;
        Vector2 _grabWorld;
        float _pinchStart;
        int _mapWidth, _mapHeight;

        public int Lod { get; private set; } = -1;
        public bool IsDragging => _dragging;
        public event Action<int> LodChanged;

        public Camera Camera => _cam != null ? _cam : _cam = GetComponent<Camera>();

        public void Bind(int mapWidth, int mapHeight)
        {
            _mapWidth = mapWidth;
            _mapHeight = mapHeight;
            var cam = Camera;
            cam.orthographic = true;
            _pos = new Vector2(mapWidth * 0.5f, mapHeight * 0.5f);
            _targetOrtho = _ortho = Mathf.Clamp(mapHeight * 0.5f, Settings.MinOrthoSize, MaxOrtho);
            _velocity = Vector2.zero;
            Apply();
        }

        float MaxOrtho => Mathf.Max(Settings.MinOrthoSize, _mapHeight * Settings.MaxOrthoFactor);

        public Vector2 ScreenToWorld(Vector2 screen)
        {
            var w = Camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -Camera.transform.position.z));
            return new Vector2(w.x, w.y);
        }

        // Called by UiRoot each frame. dragWithLeft: space held while the left button drives panning.
        public void Tick(GameInput input, bool pointerOverUi, bool dragWithLeft, float dt)
        {
            if (_mapWidth == 0) return;
            Vector2 pointer = input.Point.ReadValue<Vector2>();

            // Zoom: wheel over the map, +/- keys, pinch.
            float wheel = pointerOverUi ? 0f : input.Zoom.ReadValue<float>();
            if (Mathf.Abs(wheel) > 0.01f) _targetOrtho *= wheel > 0 ? 1f / Settings.ZoomStep : Settings.ZoomStep;
            if (input.ZoomIn.WasPressedThisFrame()) _targetOrtho /= Settings.ZoomStep;
            if (input.ZoomOut.WasPressedThisFrame()) _targetOrtho *= Settings.ZoomStep;
            HandleTouch(ref pointer);
            _targetOrtho = Mathf.Clamp(_targetOrtho, Settings.MinOrthoSize, MaxOrtho);

            Vector2 before = ScreenToWorld(pointer);
            _ortho = Mathf.Lerp(_ortho, _targetOrtho, 1f - Mathf.Exp(-Settings.ZoomLerp * dt));
            Camera.orthographicSize = _ortho;
            Vector2 after = ScreenToWorld(pointer);
            _pos += before - after; // the world point under the cursor stays put

            // Drag pan: middle button, or space + left button.
            bool dragHeld = input.DragPan.IsPressed() || dragWithLeft;
            if (dragHeld && !_dragging && !pointerOverUi)
            {
                _dragging = true;
                _grabWorld = ScreenToWorld(pointer);
                _velocity = Vector2.zero;
            }
            if (_dragging)
            {
                if (dragHeld)
                {
                    Vector2 delta = _grabWorld - ScreenToWorld(pointer);
                    _pos += delta;
                    if (dt > 0f) _velocity = Vector2.Lerp(_velocity, delta / dt, 0.5f);
                    Camera.transform.position = new Vector3(_pos.x, _pos.y, Camera.transform.position.z);
                }
                else
                {
                    _dragging = false;
                }
            }
            else
            {
                _pos += _velocity * dt;
                _velocity *= Mathf.Exp(-Settings.PanInertia * dt);
            }

            // Keyboard and edge pan.
            Vector2 keys = input.Pan.ReadValue<Vector2>();
            if (Settings.EdgePan && !_dragging && Application.isFocused && Mouse.current != null)
            {
                Vector2 m = Mouse.current.position.ReadValue();
                if (m.x >= 0 && m.y >= 0 && m.x <= Screen.width && m.y <= Screen.height)
                {
                    if (m.x < Settings.EdgePanMargin) keys.x -= 1f;
                    else if (m.x > Screen.width - Settings.EdgePanMargin) keys.x += 1f;
                    if (m.y < Settings.EdgePanMargin) keys.y -= 1f;
                    else if (m.y > Screen.height - Settings.EdgePanMargin) keys.y += 1f;
                }
            }
            if (keys.sqrMagnitude > 0f) _pos += Vector2.ClampMagnitude(keys, 1f) * (Settings.PanSpeedKeys * 2f * _ortho * dt);

            float o = Settings.OverscrollTiles;
            _pos.x = Mathf.Clamp(_pos.x, -o, _mapWidth + o);
            _pos.y = Mathf.Clamp(_pos.y, -o, _mapHeight + o);
            Apply();
        }

        void HandleTouch(ref Vector2 pointer)
        {
            var ts = Touchscreen.current;
            if (ts == null) return;
            int active = 0;
            Vector2 a = default, b = default;
            foreach (var touch in ts.touches)
            {
                if (!touch.press.isPressed) continue;
                if (active == 0) a = touch.position.ReadValue();
                else if (active == 1) b = touch.position.ReadValue();
                active++;
            }
            if (active < 2)
            {
                _pinchStart = 0f;
                return;
            }

            float dist = Vector2.Distance(a, b);
            pointer = (a + b) * 0.5f;
            if (_pinchStart > 0f && dist > 1f) _targetOrtho *= _pinchStart / dist;
            _pinchStart = dist;
        }

        // Pixel snapping: the transform sits on a screen pixel; the unsnapped position keeps accumulating.
        void Apply()
        {
            float ppu = Screen.height / (2f * _ortho);
            var snapped = ppu > 0f ? new Vector2(Mathf.Round(_pos.x * ppu) / ppu, Mathf.Round(_pos.y * ppu) / ppu) : _pos;
            var t = Camera.transform;
            t.position = new Vector3(snapped.x, snapped.y, t.position.z);
            Camera.orthographicSize = _ortho;

            float visibleTiles = 2f * _ortho;
            int lod = visibleTiles < 60f ? 0 : visibleTiles < 180f ? 1 : visibleTiles < 450f ? 2 : 3;
            if (lod != Lod)
            {
                Lod = lod;
                LodChanged?.Invoke(lod);
            }
        }
    }
}
