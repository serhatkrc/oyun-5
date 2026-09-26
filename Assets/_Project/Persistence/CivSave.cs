using System.Collections.Generic;
using System.IO;
using System.Text;
using PG.Content;
using PG.Sim;

namespace PG.Persistence
{
    // Section 11 (Bölüm 5): cities, buildings, items, the name pool and the civ columns of the unit store, with id maps.
    static class CivSave
    {
        public static byte[] Write(SimWorld sim)
        {
            var db = sim.Content;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                Keys(w, db.Resources);
                Keys(w, db.Buildings);
                Keys(w, db.BuildingStyles);
                Keys(w, db.EquipmentTypes);
                Keys(w, db.Materials);
                Keys(w, db.ItemQualities);
                Keys(w, db.Jobs);
                Keys(w, db.HappinessEvents);
                sim.Civ.Write(w);
                sim.Units.Store.WriteCiv(w);
                w.Flush();
                return ms.ToArray();
            }
        }

        // Runs after the unit section (store slots exist). A save without this section: nobody belongs to a city.
        public static void Apply(byte[] raw, SimWorld sim, List<string> warnings)
        {
            var u = sim.Units.Store;
            if (raw == null)
            {
                u.ResetCiv();
                return;
            }
            var db = sim.Content;
            using (var r = new BinaryReader(new MemoryStream(raw), Encoding.UTF8))
            {
                var res = Remap(r, db.Resources, warnings);
                var buildings = Remap(r, db.Buildings, warnings);
                var styles = Remap(r, db.BuildingStyles, warnings);
                var types = Remap(r, db.EquipmentTypes, warnings);
                var materials = Remap(r, db.Materials, warnings);
                var qualities = Remap(r, db.ItemQualities, warnings);
                var jobs = Remap(r, db.Jobs, warnings);
                var happiness = Remap(r, db.HappinessEvents, warnings);
                sim.Civ.Read(r, res, buildings, styles, types, materials, qualities);
                u.ReadCiv(r);
                for (int i = 0; i < u.HighWater; i++)
                {
                    if (u.State[i] == UnitStore.StateFree) continue;
                    int job = u.Job[i] - 1;
                    u.Job[i] = (byte)(job >= 0 && job < jobs.Length && jobs[job] >= 0 ? jobs[job] + 1 : 0);
                    int carry = u.CarryRes[i];
                    if (carry >= 0) u.CarryRes[i] = (short)(carry < res.Length ? res[carry] : -1);
                    if (u.CarryRes[i] < 0) u.CarryAmount[i] = 0;
                    for (int s = 0; s < UnitStore.HapSlots; s++)
                    {
                        int k = i * UnitStore.HapSlots + s;
                        int ev = u.HapEvent[k] - 1;
                        u.HapEvent[k] = (ushort)(ev >= 0 && ev < happiness.Length && happiness[ev] >= 0 ? happiness[ev] + 1 : 0);
                    }
                    u.StatsDirty[i] = 1; // equipment effects
                }
            }
            CivMonthlySystem.RefreshCaches(sim.Units, sim.Civ);
            // footprints were written back into the map: walkable areas changed
            sim.Regions.RebuildAll();
            sim.Events.ClearPending();
        }

        static void Keys<T>(BinaryWriter w, Registry<T> registry) where T : ContentDef
        {
            w.Write(registry.Count);
            for (int i = 0; i < registry.Count; i++) w.Write(registry[i].Id);
        }

        static int[] Remap<T>(BinaryReader r, Registry<T> registry, List<string> warnings) where T : ContentDef
        {
            var map = new int[r.ReadInt32()];
            for (int i = 0; i < map.Length; i++)
            {
                string key = r.ReadString();
                map[i] = registry.IdOrDefault(key);
                if (map[i] < 0) warnings.Add($"{registry.Category} '{key}' -> removed");
            }
            return map;
        }
    }
}
