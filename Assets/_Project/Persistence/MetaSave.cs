using System.Collections.Generic;
using System.IO;
using System.Text;
using PG.Sim;

namespace PG.Persistence
{
    // Section 12 (Bölüm 6): kingdoms, wars, armies, alliances, caravans, city loyalty and the meta columns of the unit store.
    // Content references (kingdom traits, war types) are written as string ids.
    static class MetaSave
    {
        public static byte[] Write(SimWorld sim)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                sim.Meta.Write(w);
                w.Flush();
                return ms.ToArray();
            }
        }

        // Runs after the civ section. A save without it: kingdoms are founded again on the next monthly update.
        public static void Apply(byte[] raw, SimWorld sim, List<string> warnings)
        {
            if (raw == null)
            {
                sim.Meta.Reset();
                return;
            }
            using (var r = new BinaryReader(new MemoryStream(raw), Encoding.UTF8))
                sim.Meta.Read(r);
        }
    }
}
