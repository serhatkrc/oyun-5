using System;
using System.Collections.Generic;
using System.Globalization;
using PG.Content;
using PG.World;
using PG.WorldGen;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 1.10.5: generation screen. Sliders bind to WorldGenSettings; a quarter-resolution preview follows edits.
    public sealed class WorldGenWindow
    {
        const float PreviewDelay = 0.3f;

        readonly IGameHost _host;
        readonly VisualElement _root;
        readonly DropdownField _template, _size;
        readonly TextField _seed, _image;
        readonly Image _preview;
        readonly List<(Slider slider, Func<float> get, Action<float> set)> _sliders = new List<(Slider, Func<float>, Action<float>)>();
        readonly List<string> _templateIds = new List<string>();
        WorldGenSettings _s = new WorldGenSettings();
        Texture2D _previewTex;
        float _previewDue = -1f;

        public WorldGenWindow(VisualElement layer, IGameHost host)
        {
            _host = host;
            _root = new VisualElement();
            _root.AddToClassList("pg-window");
            _root.style.display = DisplayStyle.None;
            layer.Add(_root);

            var title = new Label(Loc.T("ui.new_world"));
            title.AddToClassList("pg-window-title");
            _root.Add(title);

            var body = new VisualElement();
            body.AddToClassList("pg-worldgen-body");
            _root.Add(body);

            var fields = new VisualElement();
            fields.AddToClassList("pg-worldgen-fields");
            body.Add(fields);

            var db = host.Content;
            var names = new List<string>();
            for (int i = 0; i < db.WorldGenTemplates.Count; i++)
            {
                _templateIds.Add(db.WorldGenTemplates[i].Id);
                names.Add(Loc.Name(db.WorldGenTemplates[i]));
            }
            _template = new DropdownField(Loc.T("ui.template"), names, 0);
            _template.RegisterValueChangedCallback(_ => OnTemplateChanged());
            fields.Add(_template);

            var sizes = new List<string>();
            for (int i = 0; i < MapSizes.Count; i++)
            {
                var preset = (MapSizePreset)i;
                sizes.Add($"{Loc.T(MapSizes.LocKey(preset))} ({MapSizes.TilesFor(preset)})");
            }
            _size = new DropdownField(Loc.T("ui.size"), sizes, (int)MapSizePreset.Medium);
            _size.RegisterValueChangedCallback(_ =>
            {
                _s.Size = (MapSizePreset)_size.index;
                SchedulePreview();
            });
            fields.Add(_size);

            var seedRow = new VisualElement();
            seedRow.AddToClassList("pg-row");
            _seed = new TextField(Loc.T("ui.seed")) { style = { flexGrow = 1 } };
            _seed.RegisterValueChangedCallback(e =>
            {
                if (ulong.TryParse(e.newValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed) && seed != 0)
                {
                    _s.Seed = seed;
                    SchedulePreview();
                }
            });
            seedRow.Add(_seed);
            var randomize = new Button(Randomize) { text = Loc.T("ui.randomize") };
            randomize.AddToClassList("pg-button");
            seedRow.Add(randomize);
            fields.Add(seedRow);

            AddSlider(fields, "ui.land_ratio", 0.10f, 0.85f, () => _s.LandRatio, v => _s.LandRatio = v);
            AddSlider(fields, "ui.mountain_ratio", 0f, 0.25f, () => _s.MountainRatio, v => _s.MountainRatio = v);
            AddSlider(fields, "ui.hill_ratio", 0f, 0.30f, () => _s.HillRatio, v => _s.HillRatio = v);
            AddSlider(fields, "ui.roughness", 0f, 1f, () => _s.Roughness, v => _s.Roughness = v);
            AddSlider(fields, "ui.forest_density", 0f, 1f, () => _s.ForestDensity, v => _s.ForestDensity = v);
            AddSlider(fields, "ui.biome_variety", 0f, 1f, () => _s.BiomeVariety, v => _s.BiomeVariety = v);
            AddSlider(fields, "ui.ore_density", 0f, 1f, () => _s.OreDensity, v => _s.OreDensity = v);
            AddSlider(fields, "ui.temperature", 0f, 1f, () => _s.Temperature, v => _s.Temperature = v);
            AddSlider(fields, "ui.moisture", 0f, 1f, () => _s.Moisture, v => _s.Moisture = v);

            _image = new TextField(Loc.T("ui.image_path"));
            _image.RegisterValueChangedCallback(e =>
            {
                _s.ImagePath = e.newValue;
                SchedulePreview();
            });
            fields.Add(_image);

            _preview = new Image { scaleMode = ScaleMode.ScaleToFit };
            _preview.AddToClassList("pg-preview");
            body.Add(_preview);

            var buttons = new VisualElement();
            buttons.AddToClassList("pg-row");
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 8;
            _root.Add(buttons);
            var cancel = new Button(Close) { text = Loc.T("ui.cancel") };
            cancel.AddToClassList("pg-button");
            buttons.Add(cancel);
            var generate = new Button(Generate) { text = Loc.T("ui.generate") };
            generate.AddToClassList("pg-button");
            generate.AddToClassList("pg-button--selected");
            buttons.Add(generate);
        }

        public bool IsOpen => _root.style.display == DisplayStyle.Flex;

        public void Open()
        {
            if (_s.Seed == 0) _s.Seed = WorldGenerator.RandomSeed();
            SyncFields();
            _root.style.display = DisplayStyle.Flex;
            SchedulePreview();
        }

        public void Close()
        {
            _root.style.display = DisplayStyle.None;
            _previewDue = -1f;
        }

        public void Update()
        {
            if (!IsOpen || _previewDue < 0f || Time.unscaledTime < _previewDue) return;
            _previewDue = -1f;
            RenderPreview();
        }

        void AddSlider(VisualElement parent, string key, float min, float max, Func<float> get, Action<float> set)
        {
            var slider = new Slider(Loc.T(key), min, max) { showInputField = true };
            slider.RegisterValueChangedCallback(e =>
            {
                set(e.newValue);
                SchedulePreview();
            });
            parent.Add(slider);
            _sliders.Add((slider, get, set));
        }

        void OnTemplateChanged()
        {
            int index = Mathf.Max(0, _template.index);
            _s.ApplyTemplateDefaults(_host.Content.WorldGenTemplates[index]);
            SyncFields();
            SchedulePreview();
        }

        void SyncFields()
        {
            int t = _templateIds.IndexOf(_s.Template);
            if (t >= 0) _template.SetValueWithoutNotify(_template.choices[t]);
            _size.SetValueWithoutNotify(_size.choices[(int)_s.Size]);
            _seed.SetValueWithoutNotify(_s.Seed.ToString(CultureInfo.InvariantCulture));
            _image.SetValueWithoutNotify(_s.ImagePath ?? "");
            _image.style.display = _s.Template == "wgt.custom_image" ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var (slider, get, _) in _sliders) slider.SetValueWithoutNotify(get());
        }

        // UI-side randomness (not simulation): picks a new seed and slider values within their ranges.
        void Randomize()
        {
            _s.Seed = WorldGenerator.RandomSeed();
            var rng = new System.Random((int)(_s.Seed & 0x7FFFFFFF));
            foreach (var (slider, _, set) in _sliders)
                set(slider.lowValue + (float)rng.NextDouble() * (slider.highValue - slider.lowValue));
            SyncFields();
            SchedulePreview();
        }

        void Generate()
        {
            Close();
            _host.StartNewWorld(_s.Clone());
            _s.Seed = WorldGenerator.RandomSeed(); // next world starts from a fresh seed
        }

        void SchedulePreview() => _previewDue = Time.unscaledTime + PreviewDelay;

        void RenderPreview()
        {
            var settings = _s.Clone();
            settings.PreviewDivisor = 4;
            WorldMap map = null;
            try
            {
                map = WorldGenerator.Generate(settings, _host.Content);
                if (_previewTex == null || _previewTex.width != map.Width)
                {
                    if (_previewTex != null) UnityEngine.Object.Destroy(_previewTex);
                    _previewTex = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                }
                _previewTex.SetPixels32(Colors(map, _host.Content));
                _previewTex.Apply(false);
                _preview.image = _previewTex;
            }
            catch (Exception e)
            {
                _preview.image = null;
                Debug.LogWarning($"[WorldGen] Preview failed: {e.Message}");
            }
            finally
            {
                map?.Dispose();
            }
        }

        static Color32[] Colors(WorldMap map, ContentDB db)
        {
            var pixels = new Color32[map.TileCount];
            for (int i = 0; i < pixels.Length; i++)
            {
                int v = map.Variant[i] & 3;
                byte b = map.Biome[i];
                pixels[i] = b != 0 ? db.Biomes[b - 1].ParsedColors[v] : db.Tiles[map.Ground[i]].ParsedColors[v];
            }
            return pixels;
        }
    }
}
