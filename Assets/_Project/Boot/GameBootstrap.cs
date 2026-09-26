using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PG.Content;
using PG.Core;
using PG.Persistence;
using PG.Powers;
using PG.Render;
using PG.Sim;
using PG.UI;
using PG.WorldGen;
using UnityEngine;

namespace PG.Boot
{
    // Wires every layer together: content -> world -> sim -> renderer/camera/UI. Scene: GameBootstrap.
    public sealed class GameBootstrap : MonoBehaviour, IGameHost
    {
        const int AutosaveEveryYears = 10;

        public WorldGenSettings DefaultWorld = new WorldGenSettings();
        public Color BackgroundColor = new Color32(12, 22, 40, 255);

        SimulationRunner _runner;
        UiRoot _ui;
        CameraController _camera;
        int _lastYear;
        bool _autosaving;

        public ContentDB Content { get; private set; }
        public SimWorld Sim { get; private set; }
        public PowerCommandQueue Commands { get; } = new PowerCommandQueue();
        public MapRenderer Renderer { get; private set; }
        FeatureRenderer _features;
        NatureRenderer _nature;
        UnitRenderer _units;
        public int TicksLastSecond => _runner != null ? _runner.TicksLastSecond : 0;

        public event Action WorldChanged;

        void Awake()
        {
            var args = CommandLine.Parse(Environment.GetCommandLineArgs());
            if (!LoadContent()) return;
            Loc.Init(Content);
            ApplyArgs(args);

            if (args.DeterminismTicks > 0)
            {
                if (DefaultWorld.Seed == 0) DefaultWorld.Seed = 42;
                bool ok = Diagnostics.DeterminismCheck(Content, DefaultWorld, args.DeterminismTicks);
                Finish(ok ? 0 : 1);
                return;
            }
            if (args.Headless)
            {
                if (DefaultWorld.Seed == 0) DefaultWorld.Seed = 42;
                Diagnostics.Headless(Content, DefaultWorld, args.Years);
                Finish(0);
                return;
            }

            SetupScene();
            StartNewWorld(DefaultWorld.Clone());
        }

        bool LoadContent()
        {
            try
            {
                Content = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), ModFolders());
                return true;
            }
            catch (ContentLoadException e)
            {
                Debug.LogError(e.Message);
                UiRoot.ShowFatal("İçerik verisinde hata var / Content data has errors", e.Errors);
                if (Application.isBatchMode) Application.Quit(1);
                enabled = false;
                return false;
            }
        }

        // StreamingAssets/Mods/<mod>/Data, applied in folder-name order.
        static IReadOnlyList<string> ModFolders()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "Mods");
            if (!Directory.Exists(root)) return Array.Empty<string>();
            var mods = new List<string>(Directory.GetDirectories(root));
            mods.Sort(StringComparer.Ordinal);
            return mods;
        }

        void ApplyArgs(CommandLine args)
        {
            if (args.Seed != 0) DefaultWorld.Seed = args.Seed;
            if (!string.IsNullOrEmpty(args.Template)) DefaultWorld.Template = args.Template;
            if (!string.IsNullOrEmpty(args.Size) && Enum.TryParse(args.Size, true, out MapSizePreset size)) DefaultWorld.Size = size;
        }

        static void Finish(int exitCode)
        {
            if (Application.isBatchMode) Application.Quit(exitCode);
        }

        void SetupScene()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.transform.position = new Vector3(0f, 0f, -10f);
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            if (!cam.TryGetComponent(out _camera)) _camera = cam.gameObject.AddComponent<CameraController>();

            var mapGo = new GameObject("Map", typeof(MeshFilter), typeof(MeshRenderer));
            Renderer = mapGo.AddComponent<MapRenderer>();
            Renderer.ViewCamera = cam;
            _features = new GameObject("Features").AddComponent<FeatureRenderer>();
            _features.ViewCamera = cam;
            _nature = new GameObject("Nature").AddComponent<NatureRenderer>();
            _nature.ViewCamera = cam;
            _units = new GameObject("Units").AddComponent<UnitRenderer>();
            _units.ViewCamera = cam;

            _runner = gameObject.AddComponent<SimulationRunner>();

            var uiGo = new GameObject("UI");
            _ui = uiGo.AddComponent<UiRoot>();
            _ui.Init(this, _camera);
        }

        public void StartNewWorld(WorldGenSettings settings)
        {
            var map = WorldGenerator.Generate(settings, Content);
            var template = Content.WorldGenTemplates.TryGet(settings.Template, out var t) ? t : Content.WorldGenTemplates[0];
            string name = $"{Loc.Name(template)} #{settings.Seed % 100000}";
            var sim = new SimWorld(Content, map, new SimRandomProvider(settings.Seed), null, name);
            if (settings.SpawnAnimals) sim.SpawnInitialAnimals();
            ReplaceSim(sim);
        }

        public Task SaveToSlot(int slot) => SaveSystem.SaveAsync(slot, Sim);

        public async Task LoadSlot(int slot)
        {
            var state = await SaveSystem.LoadAsync(slot, Content);
            var sim = new SimWorld(Content, state.World, state.Rng, state.Clock, state.WorldName);
            state.ApplyTo(sim);
            ReplaceSim(sim);
            if (state.Warnings.Count > 0 && _ui != null) _ui.Toast(Loc.T("ui.load_warnings") + "\n" + string.Join("\n", state.Warnings));
        }

        void ReplaceSim(SimWorld sim)
        {
            var old = Sim;
            Commands.Clear();
            sim.Pipeline.Add(new PowerCommandSystem(Commands, Content));
            new DisasterExecutor(sim.Events, Commands, Content, sim.Clock); // lives as long as the sim's event bus
            Sim = sim;
            _lastYear = sim.Clock.Year;
            if (Renderer != null) Renderer.Bind(sim.World, sim.Regions);
            if (_features != null) _features.Bind(sim.World);
            if (_nature != null) _nature.Bind(sim.Nature);
            if (_units != null) _units.Bind(sim);
            if (_runner != null) _runner.Bind(sim);
            old?.Dispose();
            WorldChanged?.Invoke();
        }

        void Update()
        {
            if (Sim == null) return;
            int year = Sim.Clock.Year;
            if (year == _lastYear) return;
            _lastYear = year;
            if (year > 0 && year % AutosaveEveryYears == 0) Autosave();
        }

        async void Autosave()
        {
            if (_autosaving) return;
            _autosaving = true;
            try
            {
                await SaveSystem.SaveAsync(SaveSystem.NextAutoSlot(), Sim);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _autosaving = false;
            }
        }

        void OnApplicationQuit()
        {
            if (Sim == null || Application.isBatchMode) return;
            try
            {
                SaveSystem.Save(SaveSystem.NextAutoSlot(), Sim);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void OnDestroy()
        {
            if (Renderer != null) Renderer.Unbind();
            if (_features != null) _features.Unbind();
            if (_nature != null) _nature.Unbind();
            if (_units != null) _units.Unbind();
            Sim?.Dispose();
            Sim = null;
        }
    }
}
