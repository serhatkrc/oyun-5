using System.Collections.Generic;
using System.IO;
using System.Text;
using PG.Content;
using PG.Sim;
using Unity.Mathematics;

namespace PG.Persistence
{
    // Sections 8 (nature) and 9 (laws): content referenced by id so mods and data edits survive (Bölüm 1.14).
    // Optional sections: older saves without them load with defaults.
    static class NatureSave
    {
        public static byte[] WriteNature(SimWorld sim)
        {
            var n = sim.Nature;
            var db = sim.Content;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(n.WindAngle);
                w.Write(n.WindSpeed);
                w.Write(n.NextCloudMonth);

                w.Write(n.Clouds.Length);
                for (int i = 0; i < n.Clouds.Length; i++)
                {
                    var c = n.Clouds[i];
                    w.Write(db.Clouds[c.Type].Id);
                    w.Write(c.Pos.x);
                    w.Write(c.Pos.y);
                    w.Write(c.Radius);
                    w.Write(c.LifetimeTicks);
                    w.Write(c.NextDropTick);
                }

                w.Write(n.Seeds.Length);
                for (int i = 0; i < n.Seeds.Length; i++)
                {
                    var s = n.Seeds[i];
                    w.Write(s.Tile);
                    w.Write(s.Biome == 0 ? "" : db.Biomes[s.Biome - 1].Id);
                    w.Write(s.RemainingYears);
                }

                w.Write(n.Lava.Length);
                for (int i = 0; i < n.Lava.Length; i++)
                {
                    var l = n.Lava[i];
                    w.Write(l.Tile);
                    w.Write(l.DueTick);
                    w.Write(l.BaseLevel);
                }

                var era = n.Era;
                w.Write(db.Eras[era.CurrentEra].Id);
                w.Write(db.Eras[era.PreviousEra].Id);
                w.Write(era.StartedYear);
                w.Write(era.DurationYears);
                w.Write(era.CurrentSlot);
                w.Write(era.Frozen);
                w.Write(era.TintBlend);
                for (int s = 0; s < EraState.SlotCount; s++) w.Write(era.ClockSlots[s] == 0 ? "" : db.Eras[era.ClockSlots[s] - 1].Id);
                w.Write(db.Eras.Count);
                for (int i = 0; i < db.Eras.Count; i++)
                {
                    w.Write(db.Eras[i].Id);
                    w.Write(era.Enabled[i]);
                }

                w.Write(db.Disasters.Count);
                for (int i = 0; i < db.Disasters.Count; i++)
                {
                    w.Write(db.Disasters[i].Id);
                    w.Write(n.DisasterLastYear[i]);
                }

                w.Write(n.ZoneAshMonths.Length);
                for (int z = 0; z < n.ZoneAshMonths.Length; z++) w.Write(n.ZoneAshMonths[z]);

                // Processing order matters for determinism after load, so the lists are stored as they are.
                w.Write(n.Burning.Length);
                for (int i = 0; i < n.Burning.Length; i++) w.Write(n.Burning[i]);
                w.Write(n.Goo.Length);
                for (int i = 0; i < n.Goo.Length; i++) w.Write(n.Goo[i]);
                w.Flush();
                return ms.ToArray();
            }
        }

        public static byte[] WriteLaws(SimWorld sim)
        {
            var laws = sim.Nature.Laws;
            var db = sim.Content;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(laws.Count);
                for (int i = 0; i < laws.Count; i++)
                {
                    w.Write(db.Laws[i].Id);
                    w.Write(laws.IsOn(i));
                    w.Write(laws.Value(i));
                }
                w.Flush();
                return ms.ToArray();
            }
        }

        public static void ApplyNature(byte[] raw, SimWorld sim, List<string> warnings)
        {
            if (raw == null) return;
            var n = sim.Nature;
            var db = sim.Content;
            using (var r = new BinaryReader(new MemoryStream(raw), Encoding.UTF8))
            {
                n.WindAngle = r.ReadSingle();
                n.WindSpeed = r.ReadSingle();
                n.NextCloudMonth = r.ReadInt64();

                n.Clouds.Clear();
                int clouds = r.ReadInt32();
                for (int i = 0; i < clouds; i++)
                {
                    string id = r.ReadString();
                    var c = new CloudData { Pos = new float2(r.ReadSingle(), r.ReadSingle()), Radius = r.ReadSingle(), LifetimeTicks = r.ReadInt32(), NextDropTick = r.ReadInt32() };
                    int type = db.Clouds.IdOrDefault(id);
                    if (type < 0) { warnings.Add($"cloud '{id}' -> removed"); continue; }
                    c.Type = (ushort)type;
                    if (n.Clouds.Length < NatureState.MaxClouds) n.Clouds.Add(c);
                }

                n.Seeds.Clear();
                int seeds = r.ReadInt32();
                for (int i = 0; i < seeds; i++)
                {
                    int tile = r.ReadInt32();
                    string biome = r.ReadString();
                    byte years = r.ReadByte();
                    int b = db.Biomes.IdOrDefault(biome);
                    if (b >= 0 && tile >= 0 && tile < sim.World.TileCount)
                        n.Seeds.Add(new SeedData { Tile = tile, Biome = (byte)(b + 1), RemainingYears = years });
                }

                // Lava timers replace the defaults RebuildFromMap derived; cells whose tile is no longer lava drop out on the next step.
                int lava = r.ReadInt32();
                if (lava > 0) n.Lava.Clear();
                for (int i = 0; i < lava; i++)
                {
                    int tile = r.ReadInt32();
                    long due = r.ReadInt64();
                    byte baseLevel = r.ReadByte();
                    if (tile < 0 || tile >= sim.World.TileCount) continue;
                    n.Lava.Add(new LavaCell { Tile = tile, DueTick = due, BaseLevel = baseLevel, Type = sim.World.Ground[tile] });
                }

                var era = n.Era;
                int current = db.Eras.IdOrDefault(r.ReadString()), previous = db.Eras.IdOrDefault(r.ReadString());
                era.StartedYear = r.ReadInt32();
                era.DurationYears = r.ReadInt32();
                era.CurrentSlot = r.ReadInt32() % EraState.SlotCount;
                era.Frozen = r.ReadBoolean();
                era.TintBlend = r.ReadSingle();
                if (current >= 0) era.CurrentEra = current;
                else warnings.Add("era -> default");
                era.PreviousEra = previous >= 0 ? previous : era.CurrentEra;
                for (int s = 0; s < EraState.SlotCount; s++)
                {
                    string id = r.ReadString();
                    era.ClockSlots[s] = id.Length == 0 ? 0 : db.Eras.IdOrDefault(id) + 1; // unknown era -> empty slot
                }
                int eras = r.ReadInt32();
                for (int i = 0; i < eras; i++)
                {
                    int e = db.Eras.IdOrDefault(r.ReadString());
                    bool on = r.ReadBoolean();
                    if (e >= 0) era.Enabled[e] = on;
                }
                n.Mods = NatureModifiers.From(db.Eras[era.CurrentEra]);

                int disasters = r.ReadInt32();
                for (int i = 0; i < disasters; i++)
                {
                    int d = db.Disasters.IdOrDefault(r.ReadString());
                    int year = r.ReadInt32();
                    if (d >= 0) n.DisasterLastYear[d] = year;
                }

                int zones = r.ReadInt32();
                for (int z = 0; z < zones; z++)
                {
                    byte months = r.ReadByte();
                    if (z < n.ZoneAshMonths.Length) n.ZoneAshMonths[z] = months;
                }

                ReadTiles(r, n.Burning, sim.World.TileCount);
                ReadTiles(r, n.Goo, sim.World.TileCount);
            }
        }

        static void ReadTiles(BinaryReader r, Unity.Collections.NativeList<int> list, int tileCount)
        {
            list.Clear();
            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int tile = r.ReadInt32();
                if (tile >= 0 && tile < tileCount) list.Add(tile);
            }
        }

        public static void ApplyLaws(byte[] raw, SimWorld sim)
        {
            if (raw == null) return;
            var laws = sim.Nature.Laws;
            using (var r = new BinaryReader(new MemoryStream(raw), Encoding.UTF8))
            {
                int count = r.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    int law = sim.Content.Laws.IdOrDefault(r.ReadString());
                    bool on = r.ReadBoolean();
                    float value = r.ReadSingle();
                    laws.Set(law, on, value); // unknown laws are ignored (Set checks the range)
                }
            }
        }
    }
}
