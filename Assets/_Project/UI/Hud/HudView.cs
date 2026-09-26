using System;
using System.Collections.Generic;
using PG.Core;
using PG.Powers;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 8.2 HUD, basic Faz 1 version: top bar (date, speed, step, menu), power bar with tabs, brush panel, toasts.
    // Faz 4: city count and civilized population (sum of the monthly city population caches) in the top bar.
    public sealed class HudView
    {
        static readonly string[] TabOrder = { "world", "civ", "creatures", "nature", "destruction", "other" };
        const int MaxToasts = 6;
        const float ToastSeconds = 4f;

        readonly IGameHost _host;
        readonly PowerTool _tool;
        readonly Label _date, _era, _civ;
        readonly Button[] _speed = new Button[GameClock.SpeedLevels.Length];
        readonly Button _super, _step;
        readonly VisualElement _tabs, _powers, _toasts;
        readonly Label _brushSize;
        readonly Button _shape;
        readonly List<string> _visibleTabs = new List<string>();
        readonly List<Button> _powerButtons = new List<Button>();
        readonly List<int> _powerIds = new List<int>();
        readonly List<(Label label, float until)> _toastItems = new List<(Label, float)>();
        int _tab;
        long _shownTick = -1;
        int _shownSpeed = -1;
        bool _shownSuper;
        int _shownCities = -1;

        public HudView(VisualElement layer, IGameHost host, PowerTool tool)
        {
            _host = host;
            _tool = tool;

            var top = new VisualElement();
            top.AddToClassList("pg-bar");
            top.AddToClassList("pg-topbar");
            layer.Add(top);

            _date = new Label();
            _date.AddToClassList("pg-label");
            _date.AddToClassList("pg-date");
            top.Add(_date);

            _era = new Label();
            _era.AddToClassList("pg-label");
            _era.AddToClassList("pg-date");
            top.Add(_era);

            _civ = new Label();
            _civ.AddToClassList("pg-label");
            _civ.AddToClassList("pg-civ");
            top.Add(_civ);

            for (int i = 0; i < _speed.Length; i++)
            {
                int index = i;
                _speed[i] = MakeButton(i == 0 ? Loc.T("ui.pause") : Loc.F("ui.speed_fmt", GameClock.SpeedLevels[i]), () =>
                {
                    var clock = _host.Sim?.Clock;
                    if (clock == null) return;
                    clock.SuperSpeed = false;
                    clock.SetSpeed(index);
                });
                top.Add(_speed[i]);
            }

            _super = MakeButton(Loc.T("ui.super_speed"), () =>
            {
                var clock = _host.Sim?.Clock;
                if (clock == null) return;
                clock.SuperSpeed = !clock.SuperSpeed;
                if (clock.SuperSpeed && clock.Paused) clock.TogglePause();
            });
            top.Add(_super);

            _step = MakeButton(Loc.T("ui.step"), () => _host.Sim?.Clock.StepOnce());
            top.Add(_step);

            var spacer = new VisualElement();
            spacer.AddToClassList("pg-spacer");
            top.Add(spacer);

            top.Add(MakeButton(Loc.T("ui.new_world"), () => NewWorldClicked?.Invoke()));
            top.Add(MakeButton(Loc.T("ui.save_load"), () => SavesClicked?.Invoke()));

            var bottom = new VisualElement();
            bottom.AddToClassList("pg-bottom");
            layer.Add(bottom);

            _tabs = new VisualElement();
            _tabs.AddToClassList("pg-row");
            bottom.Add(_tabs);

            var row = new VisualElement();
            row.AddToClassList("pg-row");
            bottom.Add(row);

            _powers = new VisualElement();
            _powers.AddToClassList("pg-row");
            _powers.style.flexGrow = 1;
            row.Add(_powers);

            var brush = new VisualElement();
            brush.AddToClassList("pg-row");
            row.Add(brush);
            var brushLabel = new Label(Loc.T("ui.brush_size"));
            brushLabel.AddToClassList("pg-label");
            brush.Add(brushLabel);
            brush.Add(MakeButton("-", () => ChangeBrush(-1)));
            _brushSize = new Label();
            _brushSize.AddToClassList("pg-label");
            brush.Add(_brushSize);
            brush.Add(MakeButton("+", () => ChangeBrush(1)));
            _shape = MakeButton("", ToggleShape);
            brush.Add(_shape);

            _toasts = new VisualElement { pickingMode = PickingMode.Ignore };
            _toasts.AddToClassList("pg-toasts");
            layer.Add(_toasts);

            RefreshBrush();
        }

        public event Action NewWorldClicked;
        public event Action SavesClicked;

        public void RebuildPowers()
        {
            var db = _host.Content;
            _visibleTabs.Clear();
            foreach (var tab in TabOrder)
            {
                for (int i = 0; i < db.Powers.Count; i++)
                {
                    var p = db.Powers[i];
                    if (p.Tab == tab && PowerOps.IsSupported(p, db))
                    {
                        _visibleTabs.Add(tab);
                        break;
                    }
                }
            }

            _tabs.Clear();
            for (int t = 0; t < _visibleTabs.Count; t++)
            {
                int index = t;
                var b = MakeButton(Loc.T("ui.tab." + _visibleTabs[t]), () => ShowTab(index));
                b.AddToClassList("pg-tab");
                _tabs.Add(b);
            }
            ShowTab(Mathf.Clamp(_tab, 0, Mathf.Max(0, _visibleTabs.Count - 1)));
        }

        public void SelectTab(int delta)
        {
            if (_visibleTabs.Count == 0) return;
            ShowTab((_tab + delta + _visibleTabs.Count) % _visibleTabs.Count);
        }

        void ShowTab(int index)
        {
            _tab = index;
            for (int t = 0; t < _tabs.childCount; t++) _tabs[t].EnableInClassList("pg-button--selected", t == index);

            _powers.Clear();
            _powerButtons.Clear();
            _powerIds.Clear();
            if (index >= _visibleTabs.Count) return;

            var db = _host.Content;
            string tab = _visibleTabs[index];
            for (int i = 0; i < db.Powers.Count; i++)
            {
                var p = db.Powers[i];
                if (p.Tab != tab || !PowerOps.IsSupported(p, db)) continue;
                int id = i;
                var b = MakeButton(Loc.Name(p), () =>
                {
                    _tool.SelectedPower = _tool.SelectedPower == id ? -1 : id;
                    RefreshSelection();
                });
                b.tooltip = p.Description;
                _powers.Add(b);
                _powerButtons.Add(b);
                _powerIds.Add(id);
            }
            RefreshSelection();
        }

        public void RefreshSelection()
        {
            for (int k = 0; k < _powerButtons.Count; k++)
                _powerButtons[k].EnableInClassList("pg-button--selected", _powerIds[k] == _tool.SelectedPower);
        }

        public void ChangeBrush(int delta)
        {
            _tool.ChangeSize(delta);
            RefreshBrush();
        }

        void ToggleShape()
        {
            _tool.Shape = _tool.Shape == BrushShape.Circle ? BrushShape.Square : BrushShape.Circle;
            RefreshBrush();
        }

        void RefreshBrush()
        {
            _brushSize.text = (_tool.Radius * 2 + 1).ToString();
            _shape.text = Loc.T(_tool.Shape == BrushShape.Circle ? "ui.shape.circle" : "ui.shape.square");
        }

        public void Toast(string text)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("pg-toast");
            _toasts.Add(label);
            _toastItems.Add((label, Time.unscaledTime + ToastSeconds));
            while (_toastItems.Count > MaxToasts) RemoveToast(0);
        }

        void RemoveToast(int index)
        {
            _toastItems[index].label.RemoveFromHierarchy();
            _toastItems.RemoveAt(index);
        }

        public void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _toastItems.Count - 1; i >= 0; i--)
                if (_toastItems[i].until <= now) RemoveToast(i);

            var clock = _host.Sim?.Clock;
            if (clock == null) return;
            long month = clock.Tick / SimConst.TicksPerMonth;
            var civ = _host.Sim.Civ;
            int cities = civ != null ? civ.AliveCities : 0;
            if (month != _shownTick || cities != _shownCities)
            {
                _shownCities = cities;
                _civ.text = Loc.F("ui.hud.civ_fmt", cities, CivilizedPopulation(civ));
            }
            if (month != _shownTick)
            {
                _shownTick = month;
                _date.text = Loc.F("ui.date_fmt", clock.Year + 1, clock.Month + 1);
                var era = _host.Sim.Nature.Era;
                string eraName = Loc.Name(_host.Content.Eras[era.CurrentEra]);
                _era.text = era.Frozen ? Loc.F("ui.era_frozen_fmt", eraName)
                                       : Loc.F("ui.era_fmt", eraName, Mathf.Max(0, era.StartedYear + era.DurationYears - clock.Year));
            }
            if (clock.SpeedIndex != _shownSpeed || clock.SuperSpeed != _shownSuper)
            {
                _shownSpeed = clock.SpeedIndex;
                _shownSuper = clock.SuperSpeed;
                for (int i = 0; i < _speed.Length; i++)
                    _speed[i].EnableInClassList("pg-button--selected", !clock.SuperSpeed && i == clock.SpeedIndex);
                _super.EnableInClassList("pg-button--selected", clock.SuperSpeed);
            }
            _step.SetEnabled(clock.Paused);
        }

        public void ResetCachedState()
        {
            _shownTick = -1;
            _shownSpeed = -1;
            _shownCities = -1;
        }

        static int CivilizedPopulation(PG.Sim.CivState civ)
        {
            if (civ == null) return 0;
            int n = 0;
            foreach (var city in civ.Cities)
                if (!city.Dead) n += city.Population;
            return n;
        }

        static Button MakeButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("pg-button");
            return b;
        }
    }
}
