using PG.Sim;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Owns the UI Toolkit panel and routes input: hotkeys -> clock/tool, pointer -> camera and PowerTool.
    public sealed class UiRoot : MonoBehaviour
    {
        const string ThemeResource = "PG_Theme";
        const string StyleResource = "PG_Hud";

        IGameHost _host;
        GameInput _input;
        CameraController _camera;
        BrushCursor _cursor;
        readonly PowerTool _tool = new PowerTool();
        VisualElement _root;
        HudView _hud;
        WorldGenWindow _worldGen;
        SaveLoadWindow _saves;
        DebugOverlay _debug;
        bool _spaceUsedForDrag;

        public PowerTool Tool => _tool;

        public void Init(IGameHost host, CameraController camera)
        {
            _host = host;
            _camera = camera;
            _input = new GameInput();

            _root = CreatePanel(gameObject);
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("pg-layer");
            _root.Add(layer);

            _hud = new HudView(layer, host, _tool);
            _debug = new DebugOverlay(layer);
            _worldGen = new WorldGenWindow(layer, host);
            _saves = new SaveLoadWindow(layer, host, Toast);
            _hud.NewWorldClicked += () =>
            {
                _saves.Close();
                _worldGen.Open();
            };
            _hud.SavesClicked += () =>
            {
                _worldGen.Close();
                _saves.Open();
            };

            var cursorGo = new GameObject("BrushCursor", typeof(MeshFilter), typeof(MeshRenderer));
            cursorGo.transform.SetParent(transform, false);
            _cursor = cursorGo.AddComponent<BrushCursor>();
            _cursor.Hide();

            host.WorldChanged += OnWorldChanged;
            OnWorldChanged();
        }

        public void Toast(string text) => _hud?.Toast(text);

        void OnWorldChanged()
        {
            var sim = _host.Sim;
            if (sim == null) return;
            _camera.Bind(sim.World.Width, sim.World.Height);
            _hud.RebuildPowers();
            _hud.ResetCachedState();
            sim.Events.Subscribe<EraChangedEvent>(OnEraChanged);
            sim.Events.Subscribe<DisasterRequestEvent>(OnDisaster);
        }

        void OnEraChanged(EraChangedEvent e) => Toast(Loc.F("ui.era_changed_fmt", Loc.Name(_host.Content.Eras[e.NewEra])));

        void OnDisaster(DisasterRequestEvent e) => Toast(Loc.F("ui.disaster_fmt", Loc.Name(_host.Content.Disasters[e.Disaster])));

        void Update()
        {
            if (_host?.Sim == null) return;
            var clock = _host.Sim.Clock;
            float dt = Time.unscaledDeltaTime;
            Vector2 pointer = _input.Point.ReadValue<Vector2>();
            bool overUi = IsPointerOverUi(pointer);
            bool windowOpen = _worldGen.IsOpen || _saves.IsOpen;
            bool typing = IsTyping();

            if (!typing)
            {
                for (int i = 0; i < _input.Speed.Length; i++)
                {
                    if (!_input.Speed[i].WasPressedThisFrame()) continue;
                    clock.SuperSpeed = false;
                    clock.SetSpeed(i);
                }
                if (_input.Step.WasPressedThisFrame()) clock.StepOnce();
                if (_input.BrushSizeUp.WasPressedThisFrame()) _hud.ChangeBrush(1);
                if (_input.BrushSizeDown.WasPressedThisFrame()) _hud.ChangeBrush(-1);
                if (_input.TabPrev.WasPressedThisFrame()) _hud.SelectTab(-1);
                if (_input.TabNext.WasPressedThisFrame()) _hud.SelectTab(1);
                if (_input.DebugOverlay.WasPressedThisFrame()) _debug.Visible = !_debug.Visible;
                if (_input.RegionOverlay.WasPressedThisFrame() && _host.Renderer != null)
                    _host.Renderer.ShowRegions = !_host.Renderer.ShowRegions;
                if (_input.CancelPower.WasPressedThisFrame())
                {
                    if (windowOpen)
                    {
                        _worldGen.Close();
                        _saves.Close();
                    }
                    else
                    {
                        _tool.Cancel();
                        _hud.RefreshSelection();
                    }
                }

                // Space: tap = pause, hold + left drag = pan.
                if (_input.PanModifier.WasPressedThisFrame()) _spaceUsedForDrag = false;
                if (_input.PanModifier.IsPressed() && _input.UsePower.IsPressed()) _spaceUsedForDrag = true;
                if (_input.PanModifier.WasReleasedThisFrame() && !_spaceUsedForDrag) clock.TogglePause();
            }

            bool spaceDrag = !typing && _input.PanModifier.IsPressed() && _input.UsePower.IsPressed();
            _camera.Tick(_input, overUi || windowOpen, spaceDrag, dt);

            Vector2 world = _camera.ScreenToWorld(pointer);
            var tile = new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
            bool held = _input.UsePower.IsPressed() && !spaceDrag && !windowOpen && !_camera.IsDragging;
            _tool.Update(_host, tile, held, _input.UsePower.WasPressedThisFrame(), overUi);

            var map = _host.Sim.World;
            if (_tool.Active && !overUi && !windowOpen && map.InBounds(tile.x, tile.y)) _cursor.Show(tile, _tool.Radius, _tool.Shape);
            else _cursor.Hide();

            _hud.Update();
            _worldGen.Update();
            _debug.Update(_host, world);
        }

        bool IsPointerOverUi(Vector2 screen)
        {
            var panel = _root?.panel;
            if (panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return panel.Pick(p) != null;
        }

        bool IsTyping()
        {
            var focused = _root?.panel?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
        }

        static VisualElement CreatePanel(GameObject host)
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "PG_Panel";
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResource);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            var doc = host.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            var root = doc.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            var style = Resources.Load<StyleSheet>(StyleResource);
            if (style != null) root.styleSheets.Add(style);
            return root;
        }

        // Content failed to load: nothing else can run. Localization is unavailable, so the header comes from the caller.
        public static void ShowFatal(string header, IReadOnlyList<string> errors)
        {
            var go = new GameObject("FatalError");
            var root = CreatePanel(go);
            var panel = new ScrollView();
            panel.AddToClassList("pg-fatal");
            root.Add(panel);
            var title = new Label(header);
            title.AddToClassList("pg-window-title");
            panel.Add(title);
            foreach (var e in errors)
            {
                var line = new Label("• " + e);
                line.AddToClassList("pg-fatal-text");
                panel.Add(line);
            }
        }

        void OnDestroy()
        {
            if (_host != null) _host.WorldChanged -= OnWorldChanged;
            _input?.Dispose();
        }
    }
}
