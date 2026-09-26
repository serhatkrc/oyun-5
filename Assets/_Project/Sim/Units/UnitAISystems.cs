using System;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Phase 3 start: spatial index, path budget, per-species counts.
    public sealed class UnitIndexSystem : ISimSystem
    {
        public SimPhase Phase => SimPhase.UnitsThink;
        public int Order => 0;

        public void Tick(in SimContext ctx)
        {
            var w = ctx.Units;
            w.Paths.BeginTick();
            w.Index.Rebuild(w.Store);
            Array.Clear(w.SpeciesCount, 0, w.SpeciesCount.Length);
            var alive = w.Store.Alive;
            for (int k = 0; k < alive.Length; k++) w.SpeciesCount[w.Store.Species[alive[k]]]++;
        }
    }

    // Bölüm 3.8: utility neurons pick a task; units think every 10 ticks (4 in combat, 40 asleep).
    public sealed class UnitThinkSystem : ISimSystem
    {
        public const int ThinkNormal = 10, ThinkCombat = 4, ThinkSleep = 40;
        public const float MateRadius = 24f, HerdRadius = 40f, HerdNear = 10f;
        public const int HuntBelow = 60;
        public const float WorkScore = 40f, WorkInterrupt = 60f, CivMateScore = 45f;
        public const int CivForageBelow = 20;

        UnitWorld _w;
        readonly Func<int, int, bool> _isThreat, _isEnemy, _isPrey, _isMate, _isKin, _isKinHunting;

        public UnitThinkSystem()
        {
            _isThreat = IsThreat;
            _isEnemy = IsEnemy;
            _isPrey = IsPrey;
            _isMate = IsMate;
            _isKin = IsKin;
            _isKinHunting = IsKinHunting;
        }

        public SimPhase Phase => SimPhase.UnitsThink;
        public int Order => 10;

        public void Tick(in SimContext ctx)
        {
            _w = ctx.Units;
            var u = _w.Store;
            long tick = ctx.Clock.Tick;
            ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
            var alive = u.Alive;
            for (int k = 0; k < alive.Length; k++)
            {
                int i = alive[k];
                if (u.State[i] != UnitStore.StateAlive || u.NextThink[i] > tick) continue;
                if (u.Has(i, UnitFlags.Stunned) || u.Has(i, UnitFlags.Immobile) || u.Has(i, UnitFlags.PlayerControlled) || u.Boat[i] >= 0)
                {
                    u.NextThink[i] = tick + ThinkNormal;
                    continue;
                }
                Think(i, tick, ref rng);
            }
        }

        void Think(int i, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            var sp = u.SpeciesOf(i);
            var current = (UnitTask)u.Task[i];
            float sight = u.Stat(i, StatId.Sight);
            float hpRatio = u.Hp[i] / u.Stat(i, StatId.Hp);

            var best = UnitTask.Wander;
            float bestScore = 10f;
            var bestTarget = EntityId.None;
            int2 fireAt = new int2(-1, -1);

            void Consider(UnitTask task, float score, EntityId target, ref SimRandom r)
            {
                if (score < bestScore || (score == bestScore && !r.Chance(0.5f))) return;
                best = task;
                bestScore = score;
                bestTarget = target;
            }

            // flee from fire: burning ground within 2 tiles (Bölüm 2.8 fires; DECISIONS #59)
            if (!u.Has(i, UnitFlags.Fly) && NearFire(i, out int2 fire))
            {
                Consider(UnitTask.Flee, FireFleeScore, EntityId.None, ref rng);
                if (best == UnitTask.Flee && bestTarget == EntityId.None) fireAt = fire;
            }

            // flee from stronger threats
            int threat = u.Has(i, UnitFlags.Afraid) || hpRatio < 0.9f || !sp.IsMonster ? _w.Nearest(i, sight, _isThreat) : -1;
            if (threat >= 0) Consider(UnitTask.Flee, u.Has(i, UnitFlags.Afraid) ? 300f : 120f * (1.3f - hpRatio) * u.Stat(i, StatId.FleeThreshold), u.IdOf(threat), ref rng);

            // fight back / attack on sight
            var attacker = u.LastAttacker[i];
            if (u.IsAlive(attacker) && tick - u.LastAttackedTick[i] < 100 && math.distance(u.Pos[i], u.Pos[attacker.Index]) < sight * 1.5f)
                Consider(UnitTask.Attack, 90f, attacker, ref rng);
            int enemy = _w.Nearest(i, sight, _isEnemy);
            if (enemy >= 0) Consider(UnitTask.Attack, 70f, u.IdOf(enemy), ref rng);

            bool needs = !u.Has(i, UnitFlags.NoNeeds);
            var stage = (AgeStage)u.Age[i];
            // eat below 70; hunters go after live prey only when hunger > 40 (Bölüm 3.8.2 `hunt`)
            // city workers leave feeding to the city (monthly stock) until they are really starving (DECISIONS #55)
            bool cityFed = u.City[i] >= 0 && u.Job[i] != 0 && u.Saturation[i] > CivForageBelow;
            if (needs && !cityFed && u.Saturation[i] < (sp.Diet == Diet.Carn ? HuntBelow : 70))
            {
                float hunger = (100 - u.Saturation[i]) * 1.2f * (u.Saturation[i] == 0 ? 2f : 1f);
                bool eatsPlants = sp.Diet == Diet.Herb || sp.Diet == Diet.Omni;
                int prey = sp.Diet != Diet.Herb && stage != AgeStage.Baby ? _w.Nearest(i, sight, _isPrey) : -1;
                // pack_hunt (Bölüm 3.8.2): join a kin already chasing prey, +50 (every hunter for now; subspecies traits come in Bölüm 4)
                if (sp.Diet == Diet.Carn && stage != AgeStage.Baby)
                {
                    int packmate = _w.Nearest(i, sight, _isKinHunting);
                    if (packmate >= 0) Consider(UnitTask.Hunt, hunger + 50f, u.Target[packmate], ref rng);
                }
                if (prey >= 0 && !eatsPlants) Consider(UnitTask.Hunt, hunger * 1.1f, u.IdOf(prey), ref rng);
                else if (prey >= 0 && u.Saturation[i] < 25) Consider(UnitTask.Hunt, hunger, u.IdOf(prey), ref rng);
                else Consider(UnitTask.FindFood, hunger, EntityId.None, ref rng);
            }
            if (needs && u.Energy[i] < 40) Consider(UnitTask.Sleep, 100 - u.Energy[i], EntityId.None, ref rng);

            if ((u.Wants[i] & 1) != 0 && stage == AgeStage.Adult)
            {
                int mate = _w.Nearest(i, MateRadius, _isMate);
                // city folk court between jobs instead of dropping them (DECISIONS #55)
                if (mate >= 0) Consider(UnitTask.Mate, u.City[i] >= 0 ? CivMateScore : 75f, u.IdOf(mate), ref rng);
                else
                {
                    // herd cohesion: drift toward the nearest of the kind so partners can meet
                    int kin = _w.Nearest(i, HerdRadius, _isKin);
                    if (kin >= 0) Consider(UnitTask.Follow, 30f, u.IdOf(kin), ref rng);
                }
            }
            // follow_herd (Bölüm 3.8.2): until subspecies traits exist (Bölüm 4) every non-monster animal keeps loosely to its kind;
            // checked every third think to keep the wide query cheap (DECISIONS #52)
            else if (!sp.IsMonster && stage != AgeStage.Baby && (u.NextThink[i] / ThinkNormal + i) % 3 == 0
                     && _w.Nearest(i, HerdNear, _isKin) < 0)
            {
                int kin = _w.Nearest(i, HerdRadius, _isKin);
                if (kin >= 0) Consider(UnitTask.Follow, 15f, u.IdOf(kin), ref rng);
            }
            if (stage == AgeStage.Baby && u.IsAlive(u.Mother[i]) && math.distance(u.Pos[i], u.Pos[u.Mother[i].Index]) > 3f)
                Consider(UnitTask.Follow, 60f, u.Mother[i], ref rng);
            if (u.Stamina[i] < 20f) Consider(UnitTask.Rest, 45f, EntityId.None, ref rng);

            var mob = PathService.MobilityOf(u, i);
            int2 tile = u.Tile(i);
            if (!_w.Paths.CanStand(tile.x, tile.y, mob)) Consider(UnitTask.GoLand, 150f, EntityId.None, ref rng);
            else if (mob == Mobility.Amphibious && _w.Map.IsWater(tile.x, tile.y) && u.Stamina[i] < 30f && sp.Habitat != Habitat.Water)
                Consider(UnitTask.GoLand, 80f, EntityId.None, ref rng);

            if (ReadySpell(_w, i, enemy, out _, out _)) Consider(UnitTask.Cast, 65f, enemy >= 0 ? u.IdOf(enemy) : EntityId.None, ref rng);

            // work (Bölüm 5.6): city residents with a job; hunger, sleep and danger still win
            if (u.City[i] >= 0 && u.Job[i] != 0 && _w.Civ != null && JobAssigner.CanWork(u, i)) Consider(UnitTask.Work, WorkScore, EntityId.None, ref rng);

            // war (Bölüm 6.5): soldiers of a marching army follow it; fights on the way and real hunger still win
            if (u.ArmyOf[i] >= 0 && _w.Meta != null && _w.Meta.ArmyActive(u.ArmyOf[i])) Consider(UnitTask.March, MarchScore, EntityId.None, ref rng);

            // Tasks in progress keep running unless something clearly more urgent came up.
            bool lowPriority = current == UnitTask.None || current == UnitTask.Wander || current == UnitTask.Rest || current == UnitTask.Follow;
            // work is kept against small wishes (a snack, a stroll), not against real hunger, tiredness or danger (DECISIONS #55)
            float needed = current == UnitTask.Work ? WorkInterrupt : current == UnitTask.March ? MarchInterrupt : 90f;
            bool switchTask = lowPriority ? best != current || current == UnitTask.None : bestScore >= needed && best != current;
            if (switchTask) StartTask(i, best, bestTarget);
            if (switchTask && best == UnitTask.Flee && bestTarget == EntityId.None && fireAt.x >= 0) u.TargetTile[i] = fireAt; // flee from here

            bool combat = best == UnitTask.Attack || best == UnitTask.Hunt || best == UnitTask.Flee;
            int interval = (UnitTask)u.Task[i] == UnitTask.Sleep ? ThinkSleep : combat ? ThinkCombat : ThinkNormal;
            u.NextThink[i] = tick + interval + ((tick + i) % 3);
        }

        void StartTask(int i, UnitTask task, EntityId target)
        {
            var u = _w.Store;
            u.Task[i] = (byte)task;
            u.Action[i] = 0;
            u.Timer[i] = 0;
            u.Target[i] = target;
            u.PathFails[i] = 0;
            _w.ClearPath(i);
            if (task != UnitTask.Sleep && _w.StSleeping >= 0) u.RemoveStatus(i, _w.StSleeping);
        }

        // First castable spell in slot order (species spells, then trait spells), with the slot that holds its cooldown.
        public static bool ReadySpell(UnitWorld w, int i, int enemy, out int spell, out int slot)
        {
            spell = -1;
            var u = w.Store;
            var sp = u.SpeciesOf(i);
            slot = 0;
            for (int k = 0; k < sp.SpellIds.Length && slot < UnitStore.SpellSlots; k++, slot++)
                if (Check(w, i, slot, sp.SpellIds[k], enemy)) { spell = sp.SpellIds[k]; return true; }
            var traits = u.Traits[i];
            if (traits.IsEmpty) return false;
            for (int t = 0; t < w.Content.UnitTraits.Count && slot < UnitStore.SpellSlots; t++)
            {
                if (!traits.Has(t)) continue;
                foreach (int s in w.Content.UnitTraits[t].SpellIds)
                {
                    if (slot >= UnitStore.SpellSlots) break;
                    if (Check(w, i, slot, s, enemy)) { spell = s; return true; }
                    slot++;
                }
            }
            return false;
        }

        static bool Check(UnitWorld w, int i, int slot, int spell, int enemy)
        {
            var u = w.Store;
            if (u.SpellCooldown[i * UnitStore.SpellSlots + slot] > 0) return false;
            var def = w.Content.Spells[spell];
            if (u.Mana[i] < def.ManaCost) return false;
            return SpellRegistry.Useful(w, i, def, enemy);
        }

        public const float FireFleeScore = 110f;
        public const float MarchScore = 65f, MarchInterrupt = 66f;

        bool NearFire(int i, out int2 at)
        {
            var map = _w.Map;
            int2 c = _w.Store.Tile(i);
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = c.x + dx, y = c.y + dy;
                    if (!map.InBounds(x, y) || (map.Flags[map.Index(x, y)] & (ushort)TileFlags.Burning) == 0) continue;
                    at = new int2(x, y);
                    return true;
                }
            at = default;
            return false;
        }

        bool IsThreat(int self, int other) =>
            (_w.Hostile(other, self) || _w.Prey(other, self)) && _w.Power(other) > _w.Power(self) * 1.2f;

        bool IsEnemy(int self, int other) => _w.Hostile(self, other) && !IsThreat(self, other);

        bool IsPrey(int self, int other) => _w.Prey(self, other) && _w.Power(other) < _w.Power(self) * 1.5f;

        bool IsKin(int self, int other) => _w.Store.Species[self] == _w.Store.Species[other];

        bool IsKinHunting(int self, int other)
        {
            var u = _w.Store;
            return u.Species[self] == u.Species[other] && (UnitTask)u.Task[other] == UnitTask.Hunt && u.IsAlive(u.Target[other]);
        }

        bool IsMate(int self, int other)
        {
            var u = _w.Store;
            if (u.Species[self] != u.Species[other] || (AgeStage)u.Age[other] != AgeStage.Adult) return false;
            if (u.SpeciesOf(self).Reproduction == Reproduction.Split) return false;
            if (u.Sex[self] == u.Sex[other] || u.Sex[other] == 2) return false;
            int female = u.Sex[self] == 0 ? self : other;
            return _w.StPregnant < 0 || !u.HasStatus(female, _w.StPregnant);
        }
    }

    // Phase 4: runs the current task's actions and moves the unit.
    public sealed partial class UnitActSystem : ISimSystem
    {
        public const int ChaseGiveUpTicks = 240; // without landing a hit (DECISIONS #52)
        public const int SeekGiveUpTicks = 900;  // walking to a mate / parent / herd across the map
        public const int FoodSearchRadius = 10;
        public const int GrazeSaturation = 12;
        public const int FarFoodRadius = 32;
        public const int SleepEnergyPerTick = 1;
        public const int KillMealPerSize = 45;
        public const float ScentRadius = 48f;

        readonly Func<int, int, bool> _isPrey, _isGame;

        public UnitActSystem()
        {
            _isPrey = (self, other) => _w.Prey(self, other);
            _isGame = IsGame;
        }
       

        UnitWorld _w;
        NatureState _nature;

        public SimPhase Phase => SimPhase.UnitsAct;
        public int Order => 0;

        public void Tick(in SimContext ctx)
        {
            _w = ctx.Units;
            _nature = ctx.Nature;
            var u = _w.Store;
            long tick = ctx.Clock.Tick;
            ref var rng = ref ctx.Rng.Get(RngStream.UnitAI);
            ref var combatRng = ref ctx.Rng.Get(RngStream.Combat);
            float dt = SimConst.TickDt;
            int count = u.Alive.Length; // units spawned during the loop act next tick
            for (int k = 0; k < count; k++)
            {
                int i = u.Alive[k];
                if (u.State[i] != UnitStore.StateAlive) continue;
                if (u.AttackCooldown[i] > 0) u.AttackCooldown[i]--;
                for (int s = 0; s < UnitStore.SpellSlots; s++)
                    if (u.SpellCooldown[i * UnitStore.SpellSlots + s] > 0) u.SpellCooldown[i * UnitStore.SpellSlots + s]--;
                if (u.Has(i, UnitFlags.Stunned) || u.Boat[i] >= 0) continue; // passengers ride (Bölüm 5.10)

                bool done;
                switch ((UnitTask)u.Task[i])
                {
                    case UnitTask.Wander: done = Wander(i, dt, ref rng); break;
                    case UnitTask.FindFood: done = FindFood(i, dt, ref rng); break;
                    case UnitTask.Hunt:
                    case UnitTask.Attack: done = Fight(i, dt, tick, ref combatRng); break;
                    case UnitTask.Flee: done = Flee(i, dt); break;
                    case UnitTask.Sleep: done = Sleep(i); break;
                    case UnitTask.Mate: done = Mate(i, dt, tick, ref rng); break;
                    case UnitTask.Follow: done = Follow(i, dt); break;
                    case UnitTask.Rest: done = u.Stamina[i] >= 60f; break;
                    case UnitTask.GoLand: done = GoLand(i, dt); break;
                    case UnitTask.Cast: done = Cast(i, tick, ref combatRng); break;
                    case UnitTask.Work: done = Work(i, dt, tick, ref rng, ref combatRng); break;
                    case UnitTask.Migrate: done = Migrate(i, dt); break;
                    case UnitTask.March: done = March(i, dt); break;
                    case UnitTask.Caravan: done = Caravan(i, dt, tick); break;
                    default: done = false; break;
                }
                if (done)
                {
                    u.Task[i] = (byte)UnitTask.None;
                    u.Action[i] = 0;
                    _w.ClearPath(i);
                    if (u.NextThink[i] > tick + 1) u.NextThink[i] = tick + 1;
                }
            }
        }

        // Starts or continues a path to `dest`; true when arrived. Failure ends the task.
        bool MoveTo(int i, int2 dest, float dt, out bool failed)
        {
            var u = _w.Store;
            failed = false;
            if (u.PathHandle[i] < 0)
            {
                if (math.all(u.Tile(i) == dest)) return _w.StepToward(i, (float2)dest + 0.5f, dt);
                var r = _w.StartPath(i, dest);
                if (r == MoveResult.Failed) { failed = true; return false; }
                if (r == MoveResult.Waiting) return false;
            }
            var m = _w.FollowPath(i, dt);
            if (m == MoveResult.Failed) failed = true;
            return m == MoveResult.Arrived;
        }

        bool Wander(int i, float dt, ref SimRandom rng)
        {
            var u = _w.Store;
            switch (u.Action[i])
            {
                case 0:
                    var mob = PathService.MobilityOf(u, i);
                    int2 here = u.Tile(i);
                    bool landLover = mob == Mobility.Fly && u.SpeciesOf(i).Habitat != Habitat.Water; // fliers roam over land
                    for (int t = 0; t < 8; t++)
                    {
                        int2 d = here + new int2(rng.Range(-6, 7), rng.Range(-6, 7));
                        if (!_w.Paths.CanStand(d.x, d.y, mob)) continue;
                        if (landLover && (!_w.Map.InBounds(d.x, d.y) || _w.Map.IsWater(d.x, d.y)) && t < 7) continue;
                        u.TargetTile[i] = d;
                        u.Action[i] = 1;
                        return false;
                    }
                    return true;
                case 1:
                    bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
                    if (failed) return true;
                    if (arrived)
                    {
                        u.Action[i] = 2;
                        u.Timer[i] = (short)rng.Range(20, 61);
                    }
                    return false;
                default:
                    return --u.Timer[i] <= 0;
            }
        }

        bool FindFood(int i, float dt, ref SimRandom rng)
        {
            var u = _w.Store;
            var sp = u.SpeciesOf(i);
            switch (u.Action[i])
            {
                case 0:
                    if ((sp.Diet == Diet.Herb || sp.Diet == Diet.Omni) && FindPlant(i, out int2 plant))
                    {
                        u.TargetTile[i] = plant;
                        u.Action[i] = 1;
                        return false;
                    }
                    if (sp.Diet != Diet.Herb && FindCorpse(i, out int2 corpse))
                    {
                        u.TargetTile[i] = corpse;
                        u.Action[i] = 3;
                        return false;
                    }
                    if ((sp.Diet == Diet.Herb || sp.Diet == Diet.Omni) && CanGraze(u.Tile(i)))
                    {
                        u.TargetTile[i] = u.Tile(i);
                        u.Action[i] = 2;
                        u.Timer[i] = 20;
                        return false;
                    }
                    // nothing close: head for vegetated ground further away (sampled)
                    var mob = PathService.MobilityOf(u, i);
                    if (sp.Diet == Diet.Herb || sp.Diet == Diet.Omni)
                    {
                        for (int t = 0; t < 24; t++)
                        {
                            int2 d = u.Tile(i) + new int2(rng.Range(-FarFoodRadius, FarFoodRadius + 1), rng.Range(-FarFoodRadius, FarFoodRadius + 1));
                            if (!CanGraze(d) || !_w.Paths.CanStand(d.x, d.y, mob)) continue;
                            u.TargetTile[i] = d;
                            u.Action[i] = 1;
                            return false;
                        }
                    }
                    // still nothing (hunters without prey in sight): roam far as a Wander, so spotting prey or food
                    // interrupts it at the next think; hunters head for prey they can scent (DECISIONS #52)
                    bool overLand = mob == Mobility.Fly && sp.Habitat != Habitat.Water;
                    if (sp.Diet != Diet.Herb)
                    {
                        int scent = _w.Nearest(i, ScentRadius, _isPrey);
                        if (scent >= 0 && _w.Paths.NearestStandable(u.Tile(scent), mob, out int2 near))
                        {
                            u.Task[i] = (byte)UnitTask.Wander;
                            u.TargetTile[i] = near;
                            u.Action[i] = 1;
                            return false;
                        }
                    }
                    for (int t = 0; t < 12; t++)
                    {
                        int2 d = u.Tile(i) + new int2(rng.Range(-FarFoodRadius, FarFoodRadius + 1), rng.Range(-FarFoodRadius, FarFoodRadius + 1));
                        if (!_w.Paths.CanStand(d.x, d.y, mob)) continue;
                        if (overLand && (!_w.Map.InBounds(d.x, d.y) || _w.Map.IsWater(d.x, d.y))) continue;
                        u.Task[i] = (byte)UnitTask.Wander;
                        u.TargetTile[i] = d;
                        u.Action[i] = 1;
                        return false;
                    }
                    return true;
                case 1:
                case 3:
                    bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
                    if (failed) return true;
                    if (arrived)
                    {
                        u.Action[i]++;
                        u.Timer[i] = 20;
                    }
                    return false;
                case 2:
                    if (--u.Timer[i] > 0) return false;
                    EatPlant(i, u.TargetTile[i]);
                    return true;
                default:
                    if (--u.Timer[i] > 0) return false;
                    EatCorpse(i, u.TargetTile[i]);
                    return true;
            }
        }

        bool FindPlant(int i, out int2 found)
        {
            var map = _w.Map;
            var u = _w.Store;
            int2 c = u.Tile(i);
            var mob = PathService.MobilityOf(u, i);
            for (int r = 0; r <= FoodSearchRadius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        ushort f = map.Feature[map.Index(x, y)];
                        if (f == 0 || _w.FeatureNutrition[f] <= 0f || !_w.Paths.CanStand(x, y, mob)) continue;
                        found = new int2(x, y);
                        return true;
                    }
            found = default;
            return false;
        }

        // Grazing: plant eaters get a little from any vegetated biome ground without using up a feature (DECISIONS #43).
        bool CanGraze(int2 tile)
        {
            var map = _w.Map;
            if (!map.InBounds(tile.x, tile.y)) return false;
            byte b = map.Biome[map.Index(tile.x, tile.y)];
            if (b == 0 || map.Tables.BiomeEffectCode[b] == (byte)BiomeEffect.NoPlants) return false;
            var biome = _w.Content.Biomes[b - 1];
            return biome.PlantValues.Length > 0 || biome.TreeValues.Length > 0;
        }

        bool FindCorpse(int i, out int2 found)
        {
            var u = _w.Store;
            var corpses = u.Corpses;
            float best = FoodSearchRadius * FoodSearchRadius;
            found = default;
            bool any = false;
            for (int k = 0; k < corpses.Length; k++)
            {
                int t = corpses[k].Tile;
                var p = new int2(t % _w.Map.Width, t / _w.Map.Width);
                float d = math.distancesq(p, u.Pos[i]);
                if (d >= best) continue;
                best = d;
                found = p;
                any = true;
            }
            return any;
        }

        void EatPlant(int i, int2 tile)
        {
            var map = _w.Map;
            var u = _w.Store;
            int ti = map.Index(tile.x, tile.y);
            ushort f = map.Feature[ti];
            float food = _w.FeatureNutrition[f];
            if (food <= 0f)
            {
                if (CanGraze(tile)) u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + GrazeSaturation);
                return;
            }
            u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + food * 10f);
            int res = map.GetFeatureResource(ti) - 1;
            byte kind = map.Tables.FeatureKind[f];
            if (res > 0) map.SetFeatureResource(tile.x, tile.y, res);
            else if (kind != FeatureDef.KindTree) map.SetFeature(tile.x, tile.y, 0, ChangeSource.Unit);
        }

        void EatCorpse(int i, int2 tile)
        {
            var u = _w.Store;
            int ti = tile.y * _w.Map.Width + tile.x;
            var corpses = u.Corpses;
            for (int k = 0; k < corpses.Length; k++)
            {
                if (corpses[k].Tile != ti) continue;
                corpses.RemoveAt(k);
                u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + 40);
                return;
            }
        }

        bool Fight(int i, float dt, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            var target = u.Target[i];
            if (!u.IsAlive(target))
            {
                // the meal scales with the prey's size (its slot is still readable: deaths flush at the end of the tick) (DECISIONS #52)
                if ((UnitTask)u.Task[i] == UnitTask.Hunt && u.Action[i] == 1)
                    u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + KillMealPerSize * (int)u.SpeciesOf(target.Index).BaseStats[(int)StatId.Size] + 20);
                return true;
            }
            int t = target.Index;
            if (++u.Timer[i] > ChaseGiveUpTicks) return true;

            float range = math.max(1f, u.Stat(i, StatId.Range));
            float dist = math.distance(u.Pos[i], u.Pos[t]);
            if (dist <= range + 0.5f)
            {
                _w.ClearPath(i);
                if (u.Pos[t].x != u.Pos[i].x) u.Facing[i] = (byte)(u.Pos[t].x < u.Pos[i].x ? 1 : 0);
                if (u.AttackCooldown[i] > 0)
                {
                    // stay on the target between blows instead of letting it walk out of reach (DECISIONS #52)
                    if (range <= 1.5f && dist > 0.9f) _w.StepToward(i, u.Pos[t], dt);
                    return false;
                }
                u.AttackCooldown[i] = (short)math.max(2, (int)(20f / u.Stat(i, StatId.AtkSpd)));
                if (range > 1.5f) _w.LaunchProjectile(i, u.Pos[t], u.Stat(i, StatId.Dmg), 0f, false);
                else _w.MeleeHit(i, t, ref rng, tick);
                u.Timer[i] = 0; // landing hits is progress: give up only after a chase without contact (DECISIONS #52)
                if (u.State[t] != UnitStore.StateAlive) u.Action[i] = 1; // killed: hunters eat
                return false;
            }

            // Chase: steer directly when close and the way is clear, otherwise path (re-planned when the target drifts).
            var mob = PathService.MobilityOf(u, i);
            float2 dir = (u.Pos[t] - u.Pos[i]) / math.max(dist, 1e-4f);
            int2 ahead = (int2)math.floor(u.Pos[i] + dir * 0.6f);
            if (dist < 8f && _w.Paths.CanStand(ahead.x, ahead.y, mob))
            {
                _w.ClearPath(i);
                _w.StepToward(i, u.Pos[t], dt);
                return false;
            }
            int2 goal = u.Tile(t);
            if (u.PathHandle[i] >= 0 && math.distance(u.TargetTile[i], goal) > 3f) _w.ClearPath(i);
            MoveTo(i, goal, dt, out bool failed);
            return failed && u.PathFails[i] >= 3;
        }

        bool Flee(int i, float dt)
        {
            var u = _w.Store;
            if (u.Action[i] == 0)
            {
                var threat = u.Target[i];
                // no unit to run from: the fire tile left in TargetTile by the think step
                float2 away = u.IsAlive(threat) ? u.Pos[i] - u.Pos[threat.Index] : u.Pos[i] - ((float2)u.TargetTile[i] + 0.5f);
                float len = math.length(away);
                away = len > 1e-3f ? away / len : new float2(1f, 0f);
                int2 dest = (int2)math.floor(u.Pos[i] + away * 12f);
                dest = math.clamp(dest, 0, new int2(_w.Map.Width - 1, _w.Map.Height - 1));
                if (!_w.Paths.NearestStandable(dest, PathService.MobilityOf(u, i), out dest)) return true;
                u.TargetTile[i] = dest;
                u.Action[i] = 1;
            }
            bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
            return arrived || failed;
        }

        bool Sleep(int i)
        {
            var u = _w.Store;
            if (u.Action[i] == 0)
            {
                if (_w.StSleeping >= 0) u.AddStatus(i, _w.StSleeping, -1);
                u.Action[i] = 1;
            }
            if (_w.StSleeping >= 0 && !u.HasStatus(i, _w.StSleeping)) return true; // woken up
            // +1 energy per tick (doc: +0.3); a month is only 60 ticks, so the doc rate kept animals asleep most of the time (DECISIONS #42)
            u.Energy[i] = (byte)math.min(100, u.Energy[i] + SleepEnergyPerTick);
            if (u.Energy[i] < 100) return false;
            if (_w.StSleeping >= 0) u.RemoveStatus(i, _w.StSleeping);
            return true;
        }

        bool Mate(int i, float dt, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            var mate = u.Target[i];
            if (!u.IsAlive(mate) || (u.Wants[i] & 1) == 0) return true;
            int m = mate.Index;
            if (u.Action[i] == 0)
            {
                if (math.distance(u.Pos[i], u.Pos[m]) > 1.2f)
                {
                    SeekUnit(i, m, dt);
                    return ++u.Timer[i] > SeekGiveUpTicks;
                }
                u.Action[i] = 1;
                u.Timer[i] = 60;
                return false;
            }
            if (--u.Timer[i] > 0) return false;
            int mother = u.Sex[i] == 0 ? i : m, father = mother == i ? m : i;
            UnitLife.Conceive(_w, mother, father, tick, ref rng);
            u.Wants[i] &= 0xFE;
            u.Wants[m] &= 0xFE;
            return true;
        }

        bool Follow(int i, float dt)
        {
            var u = _w.Store;
            var target = u.Target[i];
            if (!u.IsAlive(target)) return true;
            if (math.distance(u.Pos[i], u.Pos[target.Index]) <= 2f) return true;
            SeekUnit(i, target.Index, dt);
            return ++u.Timer[i] > SeekGiveUpTicks;
        }

        // Walk to another unit: straight when close and clear, else a path to its tile (re-planned when it drifts away).
        void SeekUnit(int i, int other, float dt)
        {
            var u = _w.Store;
            float dist = math.distance(u.Pos[i], u.Pos[other]);
            var mob = PathService.MobilityOf(u, i);
            float2 dir = (u.Pos[other] - u.Pos[i]) / math.max(dist, 1e-4f);
            int2 ahead = (int2)math.floor(u.Pos[i] + dir * 0.6f);
            if (dist < 6f && _w.Paths.CanStand(ahead.x, ahead.y, mob))
            {
                _w.ClearPath(i);
                _w.StepToward(i, u.Pos[other], dt);
                return;
            }
            int2 goal = u.Tile(other);
            if (u.PathHandle[i] >= 0 && math.distance(u.TargetTile[i], goal) > 4f) _w.ClearPath(i);
            MoveTo(i, goal, dt, out _);
        }

        bool GoLand(int i, float dt)
        {
            var u = _w.Store;
            if (u.Action[i] == 0)
            {
                int2 c = u.Tile(i);
                bool found = false;
                for (int r = 1; r <= 16 && !found; r++)
                    for (int dy = -r; dy <= r && !found; dy++)
                        for (int dx = -r; dx <= r && !found; dx++)
                        {
                            if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                            int x = c.x + dx, y = c.y + dy;
                            if (!_w.Map.InBounds(x, y) || !_w.Map.IsWalkable(x, y) || _w.Map.IsWater(x, y)) continue;
                            u.TargetTile[i] = new int2(x, y);
                            found = true;
                        }
                if (!found) return true;
                u.Action[i] = 1;
            }
            // straight swim: the path finder has no water class for walkers
            return _w.StepToward(i, (float2)u.TargetTile[i] + 0.5f, dt) || ++u.Timer[i] > ChaseGiveUpTicks;
        }

        bool Cast(int i, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            int enemy = u.IsAlive(u.Target[i]) ? u.Target[i].Index : -1;
            if (!UnitThinkSystem.ReadySpell(_w, i, enemy, out int spell, out int slot)) return true;
            var def = _w.Content.Spells[spell];
            u.Mana[i] -= def.ManaCost;
            u.SpellCooldown[i * UnitStore.SpellSlots + slot] = (ushort)math.max(1, def.CooldownTicks);
            SpellRegistry.Cast(_w, _nature, i, def, enemy, tick, ref rng);
            return true;
        }
    }
}
