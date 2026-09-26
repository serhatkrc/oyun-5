using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using PG.Content;
using PG.Sim;
using Unity.Collections;

namespace PG.Persistence
{
    // Section 10: units (raw store arrays + lists), their paths and projectiles, with id maps for species/traits/statuses.
    static class UnitSave
    {
        public static byte[] Write(SimWorld sim)
        {
            var db = sim.Content;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                Keys(w, db.Species);
                Keys(w, db.UnitTraits);
                Keys(w, db.StatusEffects);
                sim.Units.Store.Write(w);
                sim.Units.Paths.Write(w);
                var p = sim.Units.Projectiles;
                w.Write(p.Length);
                w.Write(MemoryMarshal.AsBytes(p.AsArray().AsReadOnlySpan()));
                w.Flush();
                return ms.ToArray();
            }
        }

        public static void Apply(byte[] raw, SimWorld sim, List<string> warnings)
        {
            if (raw == null) return;
            var db = sim.Content;
            using (var r = new BinaryReader(new MemoryStream(raw), Encoding.UTF8))
            {
                var species = Remap(r, db.Species, warnings);
                var traits = Remap(r, db.UnitTraits, warnings);
                var statuses = Remap(r, db.StatusEffects, warnings);
                var speciesMap = new ushort[species.Length];
                for (int i = 0; i < species.Length; i++) speciesMap[i] = (ushort)(species[i] < 0 ? 0 : species[i]);
                sim.Units.Store.Read(r, speciesMap, traits, statuses);
                sim.Units.Paths.Read(r);
                var p = sim.Units.Projectiles;
                p.Resize(r.ReadInt32(), NativeArrayOptions.ClearMemory);
                var bytes = MemoryMarshal.AsBytes(p.AsArray().AsSpan());
                if (r.Read(bytes) != bytes.Length) throw new EndOfStreamException("Truncated projectiles");
            }
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
