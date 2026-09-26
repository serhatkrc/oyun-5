using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.6 job task chains, run inside the unit act phase. One pass = choose a target, walk there, work, carry home.
    // The step lives in Action (choose / move / work / deliver), what is being done in WorkOp, the target in TargetTile/WorkBuilding.
    public sealed partial class UnitActSystem
    {
        const byte ActChoose = 0, ActMove = 1, ActWork = 2, ActDeliver = 100, ActDeliverMove = 101, ActHunt = 50;

        const byte OpBuild = 1, OpHarvest = 2, OpPlant = 3, OpTill = 4, OpChop = 5, OpGather = 6, OpMine = 7, OpFish = 8,
                   OpSmith = 9, OpBake = 10, OpHerd = 11, OpPatrol = 12, OpHunt = 13, OpQuarry = 14;

        // a month is only 60 ticks: one trip must feed a family for months, so hands carry 20 (DECISIONS #55)
        public const int FieldRadius = 8, MaxFieldsPerShed = 16, TreeSearch = 32, FoodSearch = 32, HuntSearch = 32, CarryMax = 20;
        public const int ChopTicks = 20, HarvestTicks = 40, PlantTicks = 30, TillTicks = 40, GatherTicks = 30, MineTicks = 60,
                         FishTicks = 80, QuarryTicks = 40, SmithTicks = 400, BakeTicks = 200, HerdTicks = 300, PatrolIdle = 60, HuntGiveUp = 300;
        public const float BuildPerTick = 0.01f;
        public const int HarvestMul = 3; // a field feeds nine months of one mouth per harvest (DECISIONS #55)

        bool Work(int i, float dt, long tick, ref SimRandom rng, ref SimRandom combatRng)
        {
            var u = _w.Store;
            var civ = _w.Civ;
            if (civ == null || u.City[i] < 0) return true;
            var city = civ.Cities[u.City[i]];
            if (city.Dead) return true;
            if (u.Job[i] == 0)
            {
                Unload(civ, city, i);
                return true;
            }

            // a hungry worker eats from the food in hand
            if (u.CarryAmount[i] > 0 && u.Saturation[i] < 30 && u.CarryRes[i] >= 0)
            {
                var food = civ.Content.Resources[u.CarryRes[i]];
                if (food.IsFood && CivMonthlySystem.Edible(food, u.SpeciesOf(i).Diet))
                {
                    u.CarryAmount[i]--;
                    u.Saturation[i] = (byte)math.min(100, u.Saturation[i] + food.Nutrition * 10);
                    if (u.CarryAmount[i] == 0) u.CarryRes[i] = -1;
                }
            }
            if (u.CarryAmount[i] >= CarryMax && u.Action[i] < ActDeliver && u.Action[i] != ActWork) u.Action[i] = ActDeliver;
            switch (u.Action[i])
            {
                case ActChoose:
                    if (!Choose(civ, city, i, ref rng))
                    {
                        if (u.CarryAmount[i] == 0) return true;
                        u.Action[i] = ActDeliver; // nothing more to do: bring home what we have
                        return false;
                    }
                    if (u.Action[i] == ActChoose) u.Action[i] = ActMove; // hunters switch to the chase themselves
                    return false;
                case ActMove:
                {
                    bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
                    if (failed) return true;
                    if (!arrived) return false;
                    u.Action[i] = ActWork;
                    u.Timer[i] = (short)WorkTicks(civ, city, i);
                    return false;
                }
                case ActWork:
                    return DoWork(civ, city, i, tick, ref rng);
                case ActHunt:
                    return Hunt(civ, city, i, dt, tick, ref combatRng);
                case ActDeliver:
                {
                    int store = civ.NearestStore(city, u.Pos[i]);
                    if (store < 0) { Drop(i); return true; }
                    u.TargetTile[i] = civ.Buildings[store].Door;
                    u.Action[i] = ActDeliverMove;
                    return false;
                }
                default:
                {
                    bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
                    if (failed) { Drop(i); return true; }
                    if (!arrived) return false;
                    civ.AddStock(city, u.CarryRes[i], u.CarryAmount[i]);
                    Drop(i);
                    return true;
                }
            }
        }

        // Lost the job while carrying: the load still reaches the stock (no carry is stranded).
        public static void Unload(CivState civ, City city, int i)
        {
            var u = civ.Units.Store;
            if (u.CarryAmount[i] > 0) civ.AddStock(city, u.CarryRes[i], u.CarryAmount[i]);
            u.CarryRes[i] = -1;
            u.CarryAmount[i] = 0;
        }

        void Drop(int i)
        {
            _w.Store.CarryRes[i] = -1;
            _w.Store.CarryAmount[i] = 0;
        }

        void Carry(int i, int res, int amount)
        {
            var u = _w.Store;
            if (res < 0 || amount <= 0) return;
            if (u.CarryAmount[i] > 0 && u.CarryRes[i] != res) return; // one kind at a time (callers deliver first)
            if (u.CarryRes[i] != res) { u.CarryRes[i] = (short)res; u.CarryAmount[i] = 0; }
            u.CarryAmount[i] = (byte)math.min(255, u.CarryAmount[i] + amount);
        }

        // Picks what to do next for the unit's job; false = nothing to do now.
        bool Choose(CivState civ, City city, int i, ref SimRandom rng)
        {
            var u = _w.Store;
            var ids = civ.Ids;
            int job = u.Job[i] - 1;
            u.WorkOp[i] = 0;
            if (job == ids.JobBuilder) return ChooseBuild(civ, city, i);
            if (job == ids.JobFarmer) return ChooseFarm(civ, city, i);
            if (job == ids.JobLumberjack) return ChooseTree(civ, city, i);
            if (job == ids.JobGatherer) return ChooseFood(civ, city, i);
            if (job == ids.JobMiner) return AtBuilding(civ, city, i, ids.Mine, OpMine);
            if (job == ids.JobFisher) return ChooseCoast(civ, city, i);
            if (job == ids.JobSmith) return AtBuilding(civ, city, i, ids.Smithy, OpSmith);
            if (job == ids.JobBaker) return AtBuilding(civ, city, i, ids.Bakery, OpBake);
            if (job == ids.JobHerder) return AtBuilding(civ, city, i, ids.Pasture, OpHerd);
            if (job == ids.JobHunter) return ChoosePrey(civ, city, i);
            return ChoosePatrol(civ, city, i, ref rng);
        }

        int WorkTicks(CivState civ, City city, int i)
        {
            switch (_w.Store.WorkOp[i])
            {
                case OpHarvest: return HarvestTicks;
                case OpPlant: return PlantTicks;
                case OpTill: return TillTicks;
                case OpChop: return civ.HasComplete(city, civ.Ids.LumberCamp) ? (int)(ChopTicks / 1.3f) : ChopTicks;
                case OpQuarry: return QuarryTicks;
                case OpGather: return GatherTicks;
                case OpMine: return MineTicks;
                case OpFish: return FishTicks;
                case OpSmith: return SmithTicks;
                case OpBake: return BakeTicks;
                case OpHerd: return HerdTicks;
                case OpPatrol: return PatrolIdle;
                default: return 200; // building: re-think after a while
            }
        }

        bool DoWork(CivState civ, City city, int i, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            var map = _w.Map;
            var ids = civ.Ids;
            int2 t = u.TargetTile[i];
            if (u.WorkOp[i] == OpBuild)
            {
                int b = u.WorkBuilding[i];
                if (b < 0 || civ.Buildings[b].State != BuildingState.Construction) return true;
                var data = civ.Buildings[b];
                data.BuildProgress += BuildPerTick;
                data.Hp = math.max(data.Hp, data.MaxHp * data.BuildProgress);
                civ.Buildings[b] = data;
                if (data.BuildProgress >= 1f)
                {
                    civ.Complete(b, tick, _w.Events);
                    return true;
                }
                return --u.Timer[i] <= 0;
            }
            if (--u.Timer[i] > 0) return false;
            int ti = map.InBounds(t.x, t.y) ? map.Index(t.x, t.y) : 0;
            switch (u.WorkOp[i])
            {
                case OpHarvest:
                    if (map.Feature[ti] == ids.WheatCrop && map.GetFeatureStage(ti) >= 3)
                    {
                        map.SetFeature(t.x, t.y, 0, ChangeSource.Unit);
                        Carry(i, ids.Wheat, HarvestMul * Yield(civ, ids.WheatCrop, ids.Wheat, 3));
                    }
                    break;
                case OpTill:
                    if (civ.ZoneOwner(t.x, t.y) < 0) civ.ClaimZone(city, map.ZoneIndexOf(t.x, t.y)); // DECISIONS #57
                    if (map.Feature[ti] != 0)
                    {
                        byte kind = map.Tables.FeatureKind[map.Feature[ti]];
                        if (kind == FeatureDef.KindTree && city.StockOf(ids.Wood) < CivState.WoodTarget) civ.AddStock(city, ids.Wood, 2);
                        if (kind == FeatureDef.KindPlant || kind == FeatureDef.KindTree) map.SetFeature(t.x, t.y, 0, ChangeSource.Unit);
                    }
                    if (map.Feature[ti] == 0 && map.Building[ti] < 0 && ids.FieldTile != 0) map.SetGround(t.x, t.y, ids.FieldTile, ChangeSource.Unit);
                    goto case OpPlant;
                case OpPlant:
                    if (map.Ground[ti] == ids.FieldTile && map.Feature[ti] == 0) map.SetFeature(t.x, t.y, ids.WheatCrop, 0, ChangeSource.Unit);
                    break;
                case OpChop:
                    if (map.Tables.FeatureKind[map.Feature[ti]] == FeatureDef.KindTree)
                    {
                        int left = map.GetFeatureResource(ti) - 1;
                        Carry(i, ids.Wood, 2);
                        if (left <= 0) map.SetFeature(t.x, t.y, 0, ChangeSource.Unit);
                        else map.SetFeatureResource(t.x, t.y, left);
                        if (left > 0 && u.CarryAmount[i] < CarryMax)
                        {
                            u.Timer[i] = (short)WorkTicks(civ, city, i);
                            return false; // keep chopping this tree
                        }
                    }
                    break;
                case OpQuarry:
                    if (map.Feature[ti] == _w.RockFeature)
                    {
                        int left = map.GetFeatureResource(ti) - 1;
                        Carry(i, ids.Stone, 3);
                        if (left <= 0) map.SetFeature(t.x, t.y, 0, ChangeSource.Unit);
                        else map.SetFeatureResource(t.x, t.y, left);
                        if (left > 0 && u.CarryAmount[i] < CarryMax)
                        {
                            u.Timer[i] = QuarryTicks;
                            return false;
                        }
                    }
                    break;
                case OpGather:
                {
                    ushort f = map.Feature[ti];
                    if (f == 0 || _w.FeatureNutrition[f] <= 0f) break;
                    var def = civ.Content.Features[f - 1];
                    foreach (var kv in def.Yields)
                    {
                        int res = civ.Content.Resources.IdOrDefault(kv.Key);
                        if (res < 0 || !civ.Content.Resources[res].IsFood) continue;
                        Carry(i, res, 2 * math.max(1, (int)kv.Value));
                        break;
                    }
                    int left = map.GetFeatureResource(ti) - 1;
                    if (left > 0) map.SetFeatureResource(t.x, t.y, left);
                    else if (map.Tables.FeatureKind[f] != FeatureDef.KindTree) map.SetFeature(t.x, t.y, 0, ChangeSource.Unit);
                    else map.SetFeatureResource(t.x, t.y, 0);
                    if (left > 0 && u.CarryAmount[i] < CarryMax)
                    {
                        u.Timer[i] = (short)WorkTicks(civ, city, i);
                        return false; // keep picking this bush
                    }
                    break;
                }
                case OpMine:
                    if (MineOnce(civ, i) && u.CarryAmount[i] < CarryMax)
                    {
                        u.Timer[i] = (short)WorkTicks(civ, city, i);
                        return false;
                    }
                    break;
                case OpFish:
                    Carry(i, ids.Fish, 3);
                    if (u.CarryAmount[i] < CarryMax)
                    {
                        u.Timer[i] = (short)WorkTicks(civ, city, i);
                        return false;
                    }
                    break;
                case OpSmith:
                    Crafting.Craft(civ, city, u.Uid[i], tick, ref rng);
                    return true;
                case OpBake:
                    if (city.StockOf(ids.Wheat) >= 2 && ids.Bread >= 0)
                    {
                        city.Stock[ids.Wheat] -= 2;
                        civ.AddStock(city, ids.Bread, 1);
                    }
                    return true;
                case OpHerd:
                    civ.AddStock(city, ids.Milk, 2);
                    civ.AddStock(city, ids.Wool, 1);
                    return true;
                case OpPatrol:
                    return true;
            }
            // full hands go home; otherwise look for the next field / tree / bush (Choose delivers when nothing is left)
            u.Action[i] = u.CarryAmount[i] >= CarryMax ? ActDeliver : ActChoose;
            return false;
        }

        int Yield(CivState civ, ushort feature, int res, int fallback)
        {
            var def = civ.Content.Features[feature - 1];
            if (def.Yields != null && def.Yields.TryGetValue(civ.Content.Resources[res].Id, out int v)) return math.max(1, v);
            return fallback;
        }

        // ---------------- target choice ----------------

        bool ChooseBuild(CivState civ, City city, int i)
        {
            var u = _w.Store;
            int best = -1;
            float bestD = float.MaxValue;
            foreach (int b in city.Buildings)
            {
                var data = civ.Buildings[b];
                if (data.State != BuildingState.Construction) continue;
                float d = math.distancesq(u.Pos[i], data.Door) - (b == u.WorkBuilding[i] ? 1e6f : 0f); // stick with the site
                if (d < bestD) { bestD = d; best = b; }
            }
            if (best < 0) return false;
            u.WorkBuilding[i] = best;
            u.WorkOp[i] = OpBuild;
            u.TargetTile[i] = civ.Buildings[best].Door;
            return true;
        }

        bool ChooseFarm(CivState civ, City city, int i)
        {
            var u = _w.Store;
            var map = _w.Map;
            var ids = civ.Ids;
            int shed = PickBuilding(civ, city, i, ids.FarmShed);
            if (shed < 0 || ids.WheatCrop == 0) return false;
            int2 c = civ.Buildings[shed].Door;
            int2 ripe = new int2(-1), empty = new int2(-1), soil = new int2(-1);
            int fields = 0;
            for (int r = 1; r <= FieldRadius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        int owner = civ.ZoneOwner(x, y);
                        if (owner != city.Index && (owner >= 0 || city.Zones.Count >= CivState.MaxZonesPerCity)) continue; // fields may claim free land
                        int ti = map.Index(x, y);
                        if (map.Ground[ti] == ids.FieldTile)
                        {
                            fields++;
                            if (map.Feature[ti] == ids.WheatCrop && map.GetFeatureStage(ti) >= 3) { if (ripe.x < 0) ripe = new int2(x, y); }
                            else if (map.Feature[ti] == 0 && empty.x < 0) empty = new int2(x, y);
                            continue;
                        }
                        if (soil.x >= 0) continue;
                        ushort f = map.Flags[ti];
                        if ((f & (ushort)TileFlags.Buildable) == 0 || (f & (ushort)(TileFlags.Water | TileFlags.Road | TileFlags.Reserved)) != 0) continue;
                        // bushes, flowers and trees are cleared for the field; ore and crops stay (DECISIONS #57)
                        byte fk = map.Tables.FeatureKind[map.Feature[ti]];
                        if (map.Building[ti] >= 0 || (map.Feature[ti] != 0 && fk != FeatureDef.KindPlant && fk != FeatureDef.KindTree)) continue;
                        soil = new int2(x, y);
                    }
            // harvest first, then grow the farm to its full size (tilling plants too), then replant
            if (ripe.x >= 0) { u.TargetTile[i] = ripe; u.WorkOp[i] = OpHarvest; return true; }
            if (soil.x >= 0 && fields < MaxFieldsPerShed) { u.TargetTile[i] = soil; u.WorkOp[i] = OpTill; return true; }
            if (empty.x >= 0) { u.TargetTile[i] = empty; u.WorkOp[i] = OpPlant; return true; }
            return false;
        }

        // Spread workers over the city's buildings of a type: the unit's own if still valid, else by index.
        int PickBuilding(CivState civ, City city, int i, int def)
        {
            var u = _w.Store;
            int own = u.WorkBuilding[i];
            if (own >= 0 && civ.Buildings[own].State == BuildingState.Complete && civ.Buildings[own].Def == def && civ.Buildings[own].City == city.Index) return own;
            int count = civ.CountBuildings(city, def, false);
            if (count == 0) return -1;
            int pick = i % count, n = 0;
            foreach (int b in city.Buildings)
            {
                var data = civ.Buildings[b];
                if (data.Def != def || data.State != BuildingState.Complete) continue;
                if (n++ == pick) { u.WorkBuilding[i] = b; return b; }
            }
            return -1;
        }

        bool AtBuilding(CivState civ, City city, int i, int def, byte op)
        {
            int b = PickBuilding(civ, city, i, def);
            if (b < 0) return false;
            _w.Store.TargetTile[i] = civ.Buildings[b].Door;
            _w.Store.WorkOp[i] = op;
            return true;
        }

        public const int QuarryBelow = 40;

        // Lumberjacks double as quarrymen while the city has no mine and little stone (DECISIONS #56).
        bool ChooseTree(CivState civ, City city, int i)
        {
            if (city.StockOf(civ.Ids.Wood) >= CivState.WoodTarget && city.StockOf(civ.Ids.Stone) >= QuarryBelow) return false; // enough: storage room is for food
            if (civ.Ids.Stone >= 0 && city.StockOf(civ.Ids.Stone) < QuarryBelow && !civ.HasComplete(city, civ.Ids.Mine)
                && city.StockOf(civ.Ids.Wood) >= 20 && Spiral(i, TreeSearch, SpiralRock, 0, out int2 rock))
            {
                _w.Store.TargetTile[i] = rock;
                _w.Store.WorkOp[i] = OpQuarry;
                return true;
            }
            if (city.StockOf(civ.Ids.Wood) >= CivState.WoodTarget || !Spiral(i, TreeSearch, SpiralTree, 0, out int2 at)) return false;
            _w.Store.TargetTile[i] = at;
            _w.Store.WorkOp[i] = OpChop;
            return true;
        }

        bool ChooseFood(CivState civ, City city, int i)
        {
            if (!Spiral(i, FoodSearch, SpiralFood, civ.Ids.WheatCrop, out int2 at)) return false;
            _w.Store.TargetTile[i] = at;
            _w.Store.WorkOp[i] = OpGather;
            return true;
        }

        bool ChooseCoast(CivState civ, City city, int i)
        {
            var u = _w.Store;
            var map = _w.Map;
            int hut = PickBuilding(civ, city, i, civ.Ids.FishingHut);
            if (hut < 0) return false;
            int2 c = civ.Buildings[hut].Door;
            for (int r = 1; r <= 8; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (!map.InBounds(x, y) || !map.IsWalkable(x, y) || map.IsWater(x, y)) continue;
                        if (!WaterNext(map, x, y)) continue;
                        u.TargetTile[i] = new int2(x, y);
                        u.WorkOp[i] = OpFish;
                        return true;
                    }
            return false;
        }

        static bool WaterNext(WorldMap map, int x, int y) =>
            (map.InBounds(x + 1, y) && map.IsWater(x + 1, y)) || (map.InBounds(x - 1, y) && map.IsWater(x - 1, y)) ||
            (map.InBounds(x, y + 1) && map.IsWater(x, y + 1)) || (map.InBounds(x, y - 1) && map.IsWater(x, y - 1));

        bool ChoosePatrol(CivState civ, City city, int i, ref SimRandom rng)
        {
            if (city.Zones.Count == 0) return false;
            var map = _w.Map;
            int z = city.Zones[rng.Range(0, city.Zones.Count)];
            int2 p = new int2(((z % map.ZonesX) << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize),
                              ((z / map.ZonesX) << WorldMap.ZoneShift) + rng.Range(0, WorldMap.ZoneSize));
            if (!_w.Paths.CanStand(p.x, p.y, Mobility.Land)) return false;
            _w.Store.TargetTile[i] = p;
            _w.Store.WorkOp[i] = OpPatrol;
            return true;
        }

        // Hunters go after wild, non-sapient animals they can beat (Bölüm 5.6 hunter).
        bool ChoosePrey(CivState civ, City city, int i)
        {
            var u = _w.Store;
            int prey = _w.Nearest(i, HuntSearch, _isGame);
            if (prey < 0) return false;
            u.Target[i] = u.IdOf(prey);
            u.WorkOp[i] = OpHunt;
            u.Timer[i] = 0;
            u.Action[i] = ActHunt;
            return true;
        }

        bool IsGame(int self, int other)
        {
            var u = _w.Store;
            var sp = u.SpeciesOf(other);
            if (sp.IsCiv || sp.IsMonster || u.Has(other, UnitFlags.Undead) || u.Has(other, UnitFlags.Untargetable)) return false;
            if (u.City[other] >= 0 || u.Species[other] == u.Species[self]) return false;
            return _w.Power(other) < _w.Power(self) * 1.5f;
        }

        bool Hunt(CivState civ, City city, int i, float dt, long tick, ref SimRandom rng)
        {
            var u = _w.Store;
            var target = u.Target[i];
            if (!u.IsAlive(target))
            {
                if (u.WorkOp[i] == OpHunt + 100)
                {
                    // the kill: meat to carry, the hide goes straight into the stock
                    Carry(i, civ.Ids.Meat, math.max(2, 4 * (int)_w.Content.Species[u.Species[target.Index]].BaseStats[(int)StatId.Size]));
                    civ.AddStock(city, civ.Ids.Leather, 1);
                    u.Action[i] = ActDeliver;
                    return false;
                }
                return true;
            }
            int t = target.Index;
            if (++u.Timer[i] > HuntGiveUp) return true;
            float range = math.max(1f, u.Stat(i, StatId.Range));
            float dist = math.distance(u.Pos[i], u.Pos[t]);
            if (dist <= range + 0.5f)
            {
                _w.ClearPath(i);
                if (u.AttackCooldown[i] > 0)
                {
                    if (range <= 1.5f && dist > 0.9f) _w.StepToward(i, u.Pos[t], dt);
                    return false;
                }
                u.AttackCooldown[i] = (short)math.max(2, (int)(20f / u.Stat(i, StatId.AtkSpd)));
                if (range > 1.5f) _w.LaunchProjectile(i, u.Pos[t], u.Stat(i, StatId.Dmg), 0f, false);
                else _w.MeleeHit(i, t, ref rng, tick);
                u.Timer[i] = 0;
                if (u.State[t] != UnitStore.StateAlive) u.WorkOp[i] = OpHunt + 100;
                return false;
            }
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

        // One extraction; false when the hands already hold something else.
        bool MineOnce(CivState civ, int i)
        {
            var u = _w.Store;
            var map = _w.Map;
            int b = u.WorkBuilding[i];
            int2 c = b >= 0 ? civ.Buildings[b].Door : u.Tile(i);
            // an ore vein within 10 tiles gives ore, else the mine gives stone
            for (int r = 0; r <= 10; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        int ti = map.Index(x, y);
                        ushort f = map.Feature[ti];
                        if (f == 0 || map.Tables.FeatureKind[f] != FeatureDef.KindOre || map.GetFeatureResource(ti) <= 0) continue;
                        var def = civ.Content.Features[f - 1];
                        foreach (var kv in def.Yields)
                        {
                            int res = civ.Content.Resources.IdOrDefault(kv.Key);
                            if (res < 0) continue;
                            if (u.CarryAmount[i] > 0 && u.CarryRes[i] != res) continue;
                            Carry(i, res, 2);
                            int left = map.GetFeatureResource(ti) - 1;
                            if (left > 0) map.SetFeatureResource(x, y, left);
                            else map.SetFeature(x, y, 0, ChangeSource.Unit);
                            return true;
                        }
                    }
            if (u.CarryAmount[i] > 0 && u.CarryRes[i] != civ.Ids.Stone) return false;
            Carry(i, civ.Ids.Stone, 3);
            return true;
        }

        const byte SpiralTree = 0, SpiralFood = 1, SpiralRock = 2;

        // Ring search around the unit for the nearest standable tile holding a grown tree (SpiralTree) or wild food (SpiralFood).
        bool Spiral(int i, int radius, byte mode, ushort exclude, out int2 found)
        {
            var map = _w.Map;
            var u = _w.Store;
            int2 c = u.Tile(i);
            var mob = PathService.MobilityOf(u, i);
            for (int r = 0; r <= radius; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (math.max(math.abs(dx), math.abs(dy)) != r) continue;
                        int x = c.x + dx, y = c.y + dy;
                        if (!map.InBounds(x, y)) continue;
                        int ti = map.Index(x, y);
                        ushort f = map.Feature[ti];
                        if (f == 0 || f == exclude || map.GetFeatureResource(ti) <= 0) continue;
                        bool ok = mode == SpiralTree ? map.Tables.FeatureKind[f] == FeatureDef.KindTree && map.GetFeatureStage(ti) >= 2
                                : mode == SpiralRock ? f == _w.RockFeature
                                : _w.FeatureNutrition[f] > 0f;
                        if (!ok || !_w.Paths.CanStand(x, y, mob)) continue;
                        found = new int2(x, y);
                        return true;
                    }
            found = default;
            return false;
        }

        // Settlers walk to their new home site (Settlers.TrySend); the settlement system founds the village there.
        bool Migrate(int i, float dt)
        {
            var u = _w.Store;
            bool arrived = MoveTo(i, u.TargetTile[i], dt, out bool failed);
            if (failed && u.PathFails[i] < 3) { u.PathFails[i]++; return false; }
            return arrived || failed || ++u.Timer[i] > 2400;
        }
    }
}
