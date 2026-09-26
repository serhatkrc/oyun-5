using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 6.5 / 5.9 unit tasks: soldiers follow their army's orders, merchants walk caravans to market.
    public sealed partial class UnitActSystem
    {
        // The army system sets TargetTile (formation slot); the soldier walks there and holds until the army moves on.
        bool March(int i, float dt)
        {
            var u = _w.Store;
            var meta = _w.Meta;
            if (meta == null || !meta.ArmyActive(u.ArmyOf[i])) return true;
            int2 at = u.Tile(i);
            if (math.all(at == u.TargetTile[i])) return false;
            MoveTo(i, u.TargetTile[i], dt, out bool failed);
            if (failed && ++u.PathFails[i] > 3)
            {
                u.PathFails[i] = 0;
                return true; // think again; the army system drops a soldier who cannot follow once the army leaves
            }
            return false;
        }

        bool Caravan(int i, float dt, long tick)
        {
            var u = _w.Store;
            bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
            if (failed && u.PathFails[i] < 3) { u.PathFails[i]++; return false; }
            if (arrived || math.distancesq(u.Pos[i], (float2)u.TargetTile[i]) < 9f) { _w.Meta?.DeliverCaravan(i, tick); return true; }
            return failed || ++u.Timer[i] > 4800;
        }
    }
}
