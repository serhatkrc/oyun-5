using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    public enum UnitTask : byte { None, Wander, FindFood, Hunt, Attack, Flee, Sleep, Mate, Follow, Rest, GoLand, Cast }

    public enum MoveResult : byte { Moving, Arrived, Waiting, Failed }

    public struct Projectile
    {
        public float2 From, To, Pos;
        public float Z, T, Duration;
        public int Owner;          // unit index
        public long OwnerUid;
        public float Damage;
        public float Area;         // > 0: splash radius
        public byte Ignite;        // 1 = sets tiles on fire at impact
    }

    // Everything units need besides the map: store, spatial index, paths, projectiles, cached content ids and shared rules.
    public sealed class UnitWorld : IDisposable
    {
        public const int HardUnitCap = 20000;
        public const int MaxProjectiles = 4096;

        public readonly UnitStore Store;
        public readonly SpatialIndex Index;
        public readonly PathService Paths;
        public NativeList<Projectile> Projectiles;
        public readonly int[] SpeciesCount;
        readonly WorldMap _map;
        readonly ContentDB _content;

        // cached content ids (-1 when missing)
        public readonly int StStarving, StSleeping, StPregnant, StInEgg, StBurning, StWet, StFrozen, StStunned, StShielded,
                            StBlessed, StCursed, StHaste, StWeakened, StRooted, StInvisible, StCharmed, StInspired, StPoisoned, StAfraid;
        public readonly int TrVeteran;
        public readonly int LawAging, LawHunger, LawReproduction, LawPopulationCap, LawAnimalSpawn;
        public readonly float[] FeatureNutrition; // by feature map value, 0 = not food for plant eaters
        public readonly ushort BonesFeature;

        public UnitWorld(WorldMap map, RegionGraph regions, ContentDB content, WorldLaws laws)
        {
            _map = map;
            _content = content;
            Store = new UnitStore(content);
            Index = new SpatialIndex(map);
            Paths = new PathService(map, regions);
            Projectiles = new NativeList<Projectile>(64, Allocator.Persistent);
            SpeciesCount = new int[content.Species.Count];

            int S(string id) => content.StatusEffects.IdOrDefault(id);
            StStarving = S("st.starving"); StSleeping = S("st.sleeping"); StPregnant = S("st.pregnant"); StInEgg = S("st.in_egg");
            StBurning = S("st.burning"); StWet = S("st.wet"); StFrozen = S("st.frozen"); StStunned = S("st.stunned");
            StShielded = S("st.shielded"); StBlessed = S("st.blessed"); StCursed = S("st.cursed"); StHaste = S("st.haste");
            StWeakened = S("st.weakened"); StRooted = S("st.rooted"); StInvisible = S("st.invisible"); StCharmed = S("st.charmed");
            StInspired = S("st.inspired"); StPoisoned = S("st.poisoned"); StAfraid = S("st.afraid");
            TrVeteran = content.UnitTraits.IdOrDefault("tr.veteran");
            LawAging = laws.IndexOf("law.aging");
            LawHunger = laws.IndexOf("law.hunger");
            LawReproduction = laws.IndexOf("law.reproduction");
            LawPopulationCap = laws.IndexOf("law.population_cap");
            LawAnimalSpawn = laws.IndexOf("law.animal_spawn");
            BonesFeature = content.Features.TryGet("feat.bones_pile", out var bones) ? bones.MapValue : (ushort)0;

            FeatureNutrition = new float[content.Features.Count + 1];
            for (int f = 0; f < content.Features.Count; f++)
            {
                var def = content.Features[f];
                float food = 0f;
                if (def.Yields != null)
                    foreach (var kv in def.Yields)
                        if (kv.Key == "res.fruit" || kv.Key == "res.berries" || kv.Key == "res.wheat" || kv.Key == "res.herbs" ||
                            kv.Key == "res.mushroom" || kv.Key == "res.honey" || kv.Key == "res.candy" || kv.Key == "res.fish")
                            food += kv.Value;
                if (def.KindCode == FeatureDef.KindPlant || def.KindCode == FeatureDef.KindCrop) food = math.max(food, 1f); // grazing
                FeatureNutrition[def.MapValue] = food;
            }
        }

        public WorldMap Map => _map;
        public ContentDB Content => _content;

        // Status durations are months (status_effects.json); "/tick" damage means per second (DECISIONS #41).
        public static int StatusTicks(StatusEffectDef def) => def.DurationMonths <= 0 ? -1 : def.DurationMonths * SimConst.TicksPerMonth;

        public void AddStatus(int unit, int status)
        {
            if (status < 0) return;
            Store.AddStatus(unit, status, StatusTicks(_content.StatusEffects[status]));
        }

        // --- relations ---

        public bool IsUndead(int i) => Store.Has(i, UnitFlags.Undead);

        // a wants to attack b on sight (monsters, undead, attack_all). Hunting for food is Prey().
        public bool Hostile(int a, int b)
        {
            if (a == b) return false;
            var sa = Store.SpeciesOf(a);
            var sb = Store.SpeciesOf(b);
            if (Store.Has(a, UnitFlags.Charmed) || Store.Has(b, UnitFlags.Untargetable)) return false;
            if (Store.Has(a, UnitFlags.AttackAll)) return true;
            if (Store.Species[a] == Store.Species[b]) return false;
            if (sa.IsMonster && !sb.IsMonster) return true;
            if (IsUndead(a) && !IsUndead(b)) return true;
            return false;
        }

        public bool Prey(int hunter, int prey)
        {
            if (hunter == prey || Store.Species[hunter] == Store.Species[prey] || Store.Has(prey, UnitFlags.Untargetable)) return false;
            var diet = Store.SpeciesOf(hunter).Diet;
            if (diet != Diet.Carn && diet != Diet.Omni && diet != Diet.Brains && diet != Diet.Blood) return false;
            if (IsUndead(prey)) return false;
            if (diet == Diet.Omni && Store.SpeciesOf(prey).Diet != Diet.Herb) return false;
            return Store.Stat(prey, StatId.Size) <= Store.Stat(hunter, StatId.Size) + 0.5f;
        }

        public float Power(int i) => Store.Hp[i] * math.max(1f, Store.Stat(i, StatId.Dmg));

        // --- queries (Bölüm 3.3) ---

        public int Nearest(int self, float radius, Func<int, int, bool> accept)
        {
            float2 p = Store.Pos[self];
            Index.CellRange(p, radius, out int x0, out int y0, out int x1, out int y1);
            int best = -1;
            float bestD = radius * radius;
            for (int cy = y0; cy <= y1; cy++)
                for (int cx = x0; cx <= x1; cx++)
                {
                    int cell = cy * Index.CellsX + cx;
                    for (int k = Index.CellStart[cell]; k < Index.CellStart[cell + 1]; k++)
                    {
                        int o = Index.Sorted[k];
                        if (o == self || Store.State[o] != UnitStore.StateAlive) continue;
                        float d = math.distancesq(p, Store.Pos[o]);
                        if (d > bestD || (d == bestD && o > best)) continue;
                        if (!accept(self, o)) continue;
                        best = o;
                        bestD = d;
                    }
                }
            return best;
        }

        // --- combat (Bölüm 3.9) ---

        public bool MeleeHit(int attacker, int target, ref SimRandom rng, long tick)
        {
            if (!rng.Chance(1f - math.saturate(Store.Stat(target, StatId.Dodge) / 100f))) return false;
            float dmg = Store.Stat(attacker, StatId.Dmg) * rng.Range(0.85f, 1.15f);
            if (rng.Chance(Store.Stat(attacker, StatId.Crit) / 100f)) dmg *= 2f;
            Damage(target, dmg, attacker, DeathCause.Killed, tick);

            // knockback: up and away from the attacker
            float size = Store.Stat(target, StatId.Size);
            float kb = 2f * (Store.Stat(attacker, StatId.Dmg) / math.max(1f, size * 10f)) * (1f - math.saturate(Store.Stat(target, StatId.KnockbackResist)));
            Store.VZ[target] += math.min(kb, 6f);

            // onHit statuses from attacker traits
            var traits = Store.Traits[attacker];
            if (!traits.IsEmpty)
                for (int t = 0; t < _content.UnitTraits.Count; t++)
                {
                    if (!traits.Has(t)) continue;
                    var def = _content.UnitTraits[t];
                    if (def.OnHitStatus >= 0 && rng.Chance(def.OnHitChance)) AddStatus(target, def.OnHitStatus);
                }
            float steal = Store.Stat(attacker, StatId.Lifesteal);
            if (steal > 0f) Heal(attacker, dmg * steal / 100f);
            return true;
        }

        public void Damage(int target, float amount, int attacker, DeathCause cause, long tick)
        {
            if (Store.State[target] != UnitStore.StateAlive) return;
            float armor = Store.Stat(target, StatId.Armor);
            float final = math.max(1f, amount * (1f - armor / (armor + 50f)) * Store.Stat(target, StatId.DmgTaken));
            Store.Hp[target] -= final;
            if (attacker >= 0)
            {
                Store.LastAttacker[target] = Store.IdOf(attacker);
                Store.LastAttackedTick[target] = tick;
            }
            if (StSleeping >= 0 && Store.HasStatus(target, StSleeping)) Store.RemoveStatus(target, StSleeping);
            if (Store.Hp[target] > 0f) return;

            Store.Kill(target, cause, attacker);
            if (attacker >= 0 && Store.State[attacker] == UnitStore.StateAlive) GainKill(attacker, target);
        }

        public void Heal(int i, float amount) => Store.Hp[i] = math.min(Store.Stat(i, StatId.Hp), Store.Hp[i] + amount);

        void GainKill(int killer, int victim)
        {
            Store.Kills[killer] = (ushort)math.min(ushort.MaxValue, Store.Kills[killer] + 1);
            GainXp(killer, Store.Level[victim] * 10 + 5);
            if (Store.Kills[killer] == 10 && TrVeteran >= 0)
            {
                var t = Store.Traits[killer];
                t.Set(TrVeteran, true);
                Store.Traits[killer] = t;
                Store.StatsDirty[killer] = 1;
            }
        }

        public void GainXp(int i, float amount)
        {
            Store.Xp[i] += (int)(amount * (1f + Store.Stat(i, StatId.Xp) / 100f));
            while (Store.Level[i] < UnitStats.MaxLevel && Store.Xp[i] >= 100f * math.pow(Store.Level[i], 1.5f))
            {
                Store.Level[i]++;
                Store.StatsDirty[i] = 1;
                Events?.Publish(new UnitLevelUpEvent(Store.Uid[i], Store.Level[i]));
            }
        }

        public EventBus Events;

        public bool LaunchProjectile(int owner, float2 to, float damage, float area, bool ignite)
        {
            if (Projectiles.Length >= MaxProjectiles) return false;
            float2 from = Store.Pos[owner];
            float dist = math.distance(from, to);
            Projectiles.Add(new Projectile
            {
                From = from, To = to, Pos = from, Owner = owner, OwnerUid = Store.Uid[owner],
                Duration = math.max(0.2f, dist / 12f), Damage = damage, Area = area, Ignite = (byte)(ignite ? 1 : 0),
            });
            return true;
        }

        // --- movement (Bölüm 3.5) ---

        public MoveResult StartPath(int i, int2 dest)
        {
            var u = Store;
            Paths.Release(u.PathHandle[i]);
            u.PathHandle[i] = -1;
            u.PathStep[i] = 0;
            var status = Paths.Request(u.Tile(i), dest, PathService.MobilityOf(u, i), out int handle);
            switch (status)
            {
                case PathStatus.Ready:
                    u.PathHandle[i] = handle;
                    u.TargetTile[i] = Paths.Point(handle, Paths.Length(handle) - 1);
                    return MoveResult.Moving;
                case PathStatus.OverBudget: return MoveResult.Waiting;
                default:
                    u.PathFails[i]++;
                    return MoveResult.Failed;
            }
        }

        public void ClearPath(int i)
        {
            Paths.Release(Store.PathHandle[i]);
            Store.PathHandle[i] = -1;
            Store.PathStep[i] = 0;
        }

        // Follows the unit's path one tick; re-requests when the next tile became impassable.
        public MoveResult FollowPath(int i, float dt)
        {
            var u = Store;
            int h = u.PathHandle[i];
            if (h < 0) return MoveResult.Arrived;
            int len = Paths.Length(h);
            if (u.PathStep[i] >= len)
            {
                ClearPath(i);
                return MoveResult.Arrived;
            }
            var mob = PathService.MobilityOf(u, i);
            int2 next = Paths.Point(h, u.PathStep[i]);
            if (!Paths.CanStand(next.x, next.y, mob))
            {
                var dest = Paths.Point(h, len - 1);
                if (u.PathFails[i] >= 3) return MoveResult.Failed;
                u.PathFails[i]++;
                return StartPath(i, dest);
            }
            if (StepToward(i, (float2)next + 0.5f, dt))
            {
                u.PathStep[i]++;
                if (u.PathStep[i] >= len)
                {
                    ClearPath(i);
                    u.PathFails[i] = 0;
                    return MoveResult.Arrived;
                }
            }
            return MoveResult.Moving;
        }

        // Moves toward a point; true when reached this tick.
        public bool StepToward(int i, float2 point, float dt)
        {
            var u = Store;
            if (u.Has(i, UnitFlags.Immobile) || u.Has(i, UnitFlags.Stunned) || u.Z[i] > 0.05f) return false;
            float2 pos = u.Pos[i];
            float2 d = point - pos;
            float dist = math.length(d);
            float speed = SpeedAt(i, pos) * dt;
            if (dist <= speed || dist < 1e-4f)
            {
                u.Pos[i] = point;
                return true;
            }
            u.Pos[i] = pos + d / dist * speed;
            if (math.abs(d.x) > 0.01f) u.Facing[i] = (byte)(d.x < 0f ? 1 : 0);
            return false;
        }

        // tiles / second = speed * 1.5 / moveCost * (swimming 0.5)
        public float SpeedAt(int i, float2 pos)
        {
            var u = Store;
            float speed = u.Stat(i, StatId.Speed) * 1.5f;
            if (u.Has(i, UnitFlags.Fly)) return speed;
            int2 t = (int2)math.floor(pos);
            if (!_map.InBounds(t.x, t.y)) return speed;
            int ti = _map.Index(t.x, t.y);
            ushort flags = _map.Flags[ti];
            if ((flags & (ushort)TileFlags.Walkable) == 0) return speed * 0.5f * u.Stat(i, StatId.SwimSpeed); // swimming
            float cost = math.max(0.1f, _map.Tables.MoveCost[_map.Ground[ti]]);
            if ((flags & (ushort)TileFlags.Road) != 0) speed *= 1.3f;
            return speed / cost;
        }

        public void Dispose()
        {
            Store.Dispose();
            Index.Dispose();
            Paths.Dispose();
            if (Projectiles.IsCreated) Projectiles.Dispose();
        }
    }
}
