using System.Text;
using PG.Content;
using PG.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace PG.UI
{
    // Bölüm 1.15 F3 overlay: developer diagnostics (English on purpose; not player-facing content).
    public sealed class DebugOverlay
    {
        const float RefreshSeconds = 0.25f;

        readonly Label _label;
        readonly StringBuilder _sb = new StringBuilder(1024);
        float _next;
        float _fps;

        public DebugOverlay(VisualElement layer)
        {
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList("pg-debug");
            _label.style.display = DisplayStyle.None;
            layer.Add(_label);
        }

        public bool Visible
        {
            get => _label.style.display == DisplayStyle.Flex;
            set => _label.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void Update(IGameHost host, Vector2 cursorWorld)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0f) _fps = Mathf.Lerp(_fps, 1f / dt, 0.1f);
            if (!Visible || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + RefreshSeconds;

            var sim = host.Sim;
            if (sim == null) return;
            var map = sim.World;
            var regions = sim.Regions;
            var pipeline = sim.Pipeline;

            _sb.Clear();
            _sb.Append("FPS ").Append(_fps.ToString("0")).Append("   ticks/s ").Append(host.TicksLastSecond)
               .Append("   tick ").Append(pipeline.LastTickMs.ToString("0.000")).Append(" ms\n");
            for (int i = 0; i < pipeline.Count; i++)
                _sb.Append("  ").Append(pipeline[i].Phase).Append(' ').Append(pipeline[i].GetType().Name).Append(' ')
                   .Append(pipeline.LastMs(i).ToString("0.000")).Append(" ms\n");
            _sb.Append("dirty chunks: render ").Append(map.CountDirty(DirtyMask.Render))
               .Append("  regions ").Append(map.CountDirty(DirtyMask.Regions)).Append('\n');
            _sb.Append("regions ").Append(regions.RegionCount).Append("   islands ").Append(regions.IslandCount).Append('\n');
            _sb.Append("map ").Append(map.Width).Append('x').Append(map.Height).Append("   seed ").Append(sim.Seed).Append('\n');
            var nature = sim.Nature;
            _sb.Append("era ").Append(nature.CurrentEra.Id).Append("  wind ").Append((nature.WindAngle * Mathf.Rad2Deg).ToString("0")).Append("° ")
               .Append(nature.WindSpeed.ToString("0.00")).Append("  clouds ").Append(nature.Clouds.Length).Append("  burning ").Append(nature.Burning.Length)
               .Append("  lava ").Append(nature.Lava.Length).Append('\n');
            var units = sim.Units;
            _sb.Append("units ").Append(units.Store.Count).Append("  corpses ").Append(units.Store.Corpses.Length)
               .Append("  projectiles ").Append(units.Projectiles.Length).Append("  paths/tick ").Append(units.Paths.SolvedThisTick).Append('\n');

            int x = Mathf.FloorToInt(cursorWorld.x), y = Mathf.FloorToInt(cursorWorld.y);
            if (map.InBounds(x, y))
            {
                var db = sim.Content;
                int i = map.Index(x, y);
                byte biome = map.Biome[i];
                ushort feature = map.Feature[i];
                _sb.Append("tile (").Append(x).Append(',').Append(y).Append(") ").Append(db.Tiles[map.Ground[i]].Id)
                   .Append("  variant ").Append(map.Variant[i]).Append('\n');
                _sb.Append("biome ").Append(biome == 0 ? "-" : db.Biomes[biome - 1].Id)
                   .Append("  feature ").Append(feature == 0 ? "-" : db.Features[feature - 1].Id).Append('\n');
                _sb.Append("flags ").Append(((TileFlags)map.Flags[i]).ToString()).Append('\n');
                _sb.Append("zone ").Append(map.ZoneIndexOf(x, y)).Append("  ").Append(map.Zones[map.ZoneIndexOf(x, y)].TemperatureC).Append("°C")
                   .Append("  chunk ").Append(map.ChunkIndexOf(x, y)).Append('\n');
                _sb.Append("region land ").Append(regions.RegionAt(x, y, MoveClass.Land))
                   .Append(" water ").Append(regions.RegionAt(x, y, MoveClass.Water))
                   .Append("  island land ").Append(regions.IslandAt(x, y, MoveClass.Land))
                   .Append(" water ").Append(regions.IslandAt(x, y, MoveClass.Water));
                AppendUnitUnderCursor(sim, cursorWorld);
            }
            _label.text = _sb.ToString();
        }

        void AppendUnitUnderCursor(PG.Sim.SimWorld sim, Vector2 cursor)
        {
            var u = sim.Units.Store;
            int best = -1;
            float bestD = 2.25f;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                float d = (new Vector2(u.Pos[i].x, u.Pos[i].y) - cursor).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) return;
            _sb.Append("\nunit #").Append(u.Uid[best]).Append(' ').Append(u.SpeciesOf(best).Id)
               .Append("  age ").Append(u.AgeYears(best, sim.Clock.Tick).ToString("0.0")).Append(' ').Append((PG.Sim.AgeStage)u.Age[best])
               .Append("  hp ").Append(u.Hp[best].ToString("0")).Append('/').Append(u.Stat(best, PG.Content.StatId.Hp).ToString("0"))
               .Append("\n  task ").Append((PG.Sim.UnitTask)u.Task[best]).Append('.').Append(u.Action[best])
               .Append("  sat ").Append(u.Saturation[best]).Append("  energy ").Append(u.Energy[best])
               .Append("  stamina ").Append(u.Stamina[best].ToString("0")).Append("  lvl ").Append(u.Level[best]).Append("  kills ").Append(u.Kills[best]);
        }
    }
}
