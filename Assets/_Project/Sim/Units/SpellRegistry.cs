using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    public enum SpellTarget : byte { Enemy, Self, Ally, Area }

    // Bölüm 3.9: spell behaviour by id (spells.json holds cost, cooldown, range, player text).
    // Spells of later chapters (rot_breath, cure_rot, abduct, meteor_call) are known but do nothing yet.
    public static class SpellRegistry
    {
        public static SpellTarget TargetOf(string id)
        {
            switch (id)
            {
                case "spell.heal": case "spell.shield": case "spell.invisibility": case "spell.haste": case "spell.teleport": return SpellTarget.Self;
                case "spell.mass_heal": case "spell.inspire": case "spell.rain_call": case "spell.grow": case "spell.raise_dead": case "spell.summon_imp": case "spell.quake_stomp": return SpellTarget.Area;
                case "spell.bless": return SpellTarget.Ally;
                default: return SpellTarget.Enemy;
            }
        }

        // Is it worth casting now? (heal only when hurt, raise_dead only near corpses...)
        public static bool Useful(UnitWorld w, int caster, SpellDef spell, int enemy)
        {
            var u = w.Store;
            switch (spell.Id)
            {
                case "spell.heal": case "spell.mass_heal": return u.Hp[caster] < u.Stat(caster, StatId.Hp) * 0.6f;
                case "spell.shield": case "spell.invisibility": case "spell.haste": case "spell.quake_stomp": case "spell.teleport": return enemy >= 0;
                case "spell.raise_dead": return w.Store.Corpses.Length > 0 && enemy >= 0;
                case "spell.rain_call": return w.Map.Fire[w.Map.Index(u.Tile(caster).x, u.Tile(caster).y)] > 0;
                case "spell.grow": case "spell.inspire": case "spell.bless": return true;
                case "spell.summon_imp": return enemy >= 0;
                case "spell.rot_breath": case "spell.cure_rot": case "spell.abduct": case "spell.meteor_call": return false;
                default: return enemy >= 0 && math.distance(u.Pos[caster], u.Pos[enemy]) <= spell.Range + 0.5f;
            }
        }

        public static void Cast(UnitWorld w, NatureState nature, int caster, SpellDef spell, int enemy, long tick, ref SimRandom rng)
        {
            var u = w.Store;
            var map = w.Map;
            float power = 1f + u.Stat(caster, StatId.SpellPower) / 100f;
            float2 at = enemy >= 0 ? u.Pos[enemy] : u.Pos[caster];
            switch (spell.Id)
            {
                case "spell.heal": w.Heal(caster, 40f * power); break;
                case "spell.mass_heal": ForAllies(w, caster, 4f, a => w.Heal(a, 30f * power)); break;
                case "spell.fireball": w.LaunchProjectile(caster, at, 30f * power, 1.5f, true); break;
                case "spell.lightning":
                    if (enemy >= 0) w.Damage(enemy, 60f * power, caster, DeathCause.Killed, tick);
                    Ignite(nature, at, 200);
                    break;
                case "spell.fire_breath":
                    if (enemy >= 0) w.Damage(enemy, 20f * power, caster, DeathCause.Burned, tick);
                    w.AddStatus(enemy, w.StBurning);
                    Ignite(nature, at, 150);
                    break;
                case "spell.holy_smite": if (enemy >= 0) w.Damage(enemy, (w.IsUndead(enemy) ? 90f : 30f) * power, caster, DeathCause.Divine, tick); break;
                case "spell.acid_spit": if (enemy >= 0) w.Damage(enemy, 10f * power, caster, DeathCause.Killed, tick); break;
                case "spell.shield": w.AddStatus(caster, w.StShielded); break;
                case "spell.invisibility": w.AddStatus(caster, w.StInvisible); break;
                case "spell.haste": w.AddStatus(caster, w.StHaste); break;
                case "spell.bless": w.AddStatus(caster, w.StBlessed); break;
                case "spell.curse": w.AddStatus(enemy, w.StCursed); break;
                case "spell.weaken": w.AddStatus(enemy, w.StWeakened); break;
                case "spell.entangle": w.AddStatus(enemy, w.StRooted); break;
                case "spell.charm": w.AddStatus(enemy, w.StCharmed); break;
                case "spell.freeze": ForEnemies(w, caster, at, 2f, e => w.AddStatus(e, w.StFrozen)); break;
                case "spell.poison_cloud": ForEnemies(w, caster, at, 2.5f, e => w.AddStatus(e, w.StPoisoned)); break;
                case "spell.quake_stomp": ForEnemies(w, caster, u.Pos[caster], 3f, e => w.AddStatus(e, w.StStunned)); break;
                case "spell.inspire": ForAllies(w, caster, 6f, a => w.AddStatus(a, w.StInspired)); break;
                case "spell.rain_call":
                    int rain = nature.Content.Clouds.IdOrDefault("cloud.rain");
                    nature.SpawnCloud(rain, u.Pos[caster], ref rng);
                    break;
                case "spell.grow":
                    for (int k = 0; k < 6; k++)
                    {
                        int2 t = u.Tile(caster) + new int2(rng.Range(-4, 5), rng.Range(-4, 5));
                        if (map.InBounds(t.x, t.y)) FeatureGrowth.TryGrow(nature, t.x, t.y, ref rng);
                    }
                    break;
                case "spell.teleport":
                    for (int k = 0; k < 16; k++)
                    {
                        int2 t = u.Tile(caster) + new int2(rng.Range(-30, 31), rng.Range(-30, 31));
                        if (!w.Paths.CanStand(t.x, t.y, PathService.MobilityOf(u, caster))) continue;
                        u.Pos[caster] = (float2)t + 0.5f;
                        w.ClearPath(caster);
                        break;
                    }
                    break;
                case "spell.summon_imp": Summon(w, caster, "sp.ember_imp", 2, tick, ref rng); break;
                case "spell.raise_dead": RaiseDead(w, caster, tick, ref rng); break;
            }
        }

        static void Ignite(NatureState nature, float2 at, byte fire)
        {
            int2 t = (int2)math.floor(at);
            if (nature.World.InBounds(t.x, t.y)) nature.Ignite(t.x, t.y, fire);
        }

        // ponytail: lambdas allocate, but spells are rare (cooldowns of seconds); fine outside the hot loops.
        static void ForEnemies(UnitWorld w, int caster, float2 at, float radius, System.Action<int> act)
        {
            var u = w.Store;
            w.Index.CellRange(at, radius, out int x0, out int y0, out int x1, out int y1);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cy * w.Index.CellsX + cx;
                    for (int k = w.Index.CellStart[cell]; k < w.Index.CellStart[cell + 1]; k++)
                    {
                        int o = w.Index.Sorted[k];
                        if (o == caster || u.State[o] != UnitStore.StateAlive || u.Species[o] == u.Species[caster]) continue;
                        if (math.distance(u.Pos[o], at) <= radius) act(o);
                    }
                }
        }

        static void ForAllies(UnitWorld w, int caster, float radius, System.Action<int> act)
        {
            var u = w.Store;
            float2 at = u.Pos[caster];
            w.Index.CellRange(at, radius, out int x0, out int y0, out int x1, out int y1);
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cy * w.Index.CellsX + cx;
                    for (int k = w.Index.CellStart[cell]; k < w.Index.CellStart[cell + 1]; k++)
                    {
                        int o = w.Index.Sorted[k];
                        if (u.State[o] != UnitStore.StateAlive || u.Species[o] != u.Species[caster]) continue;
                        if (math.distance(u.Pos[o], at) <= radius) act(o);
                    }
                }
        }

        public static void Summon(UnitWorld w, int caster, string species, int count, long tick, ref SimRandom rng)
        {
            int sp = w.Content.Species.IdOrDefault(species);
            if (sp < 0) return;
            for (int k = 0; k < count && w.Store.Count < UnitWorld.HardUnitCap; k++)
                w.Store.Spawn(new UnitSpawnRequest
                {
                    Species = sp, Pos = w.Store.Pos[caster] + new float2(rng.Range(-1.5f, 1.5f), rng.Range(-1.5f, 1.5f)),
                    AgeYears = 1f, Sex = -1, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1,
                }, tick, ref rng);
        }

        static void RaiseDead(UnitWorld w, int caster, long tick, ref SimRandom rng)
        {
            int sp = w.Content.Species.IdOrDefault("sp.skeleton");
            if (sp < 0) return;
            var corpses = w.Store.Corpses;
            int2 me = w.Store.Tile(caster);
            int raised = 0;
            for (int k = corpses.Length - 1; k >= 0 && raised < 3; k--)
            {
                int tile = corpses[k].Tile;
                int2 t = new int2(tile % w.Map.Width, tile / w.Map.Width);
                if (math.distance(t, me) > 5f) continue;
                w.Store.Spawn(new UnitSpawnRequest
                {
                    Species = sp, Pos = (float2)t + 0.5f, AgeYears = 1f, Sex = 2,
                    Mother = EntityId.None, Father = EntityId.None, Subspecies = -1,
                }, tick, ref rng);
                corpses.RemoveAt(k);
                raised++;
            }
        }
    }
}
