using System;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.WorldGen;
using Unity.Mathematics;

public static class CivDebug
{
    public static void Run(ContentDB db, int months)
    {
        var settings = new WorldGenSettings { Seed = 17, Size = MapSizePreset.Medium, Template = "wgt.pangea", ForestDensity = 0.4f };
        var map = WorldGenerator.Generate(settings, db);
        using var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed));
        sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
        sim.SpawnInitialAnimals();
        PG.EditorTools.CivSoak.SpawnGroup(sim, db.Species.IdOf("sp.human"), 10, PG.EditorTools.CivSoak.Meadow(map, new int2(map.Width / 2, map.Height / 2)), 6, 7);
        var u = sim.Units.Store;
        var civ = sim.Civ;
        var taskHist = new int[16]; var actHist = new System.Collections.Generic.Dictionary<string, int>();
        for (int m = 1; m <= months; m++)
        {
            for (int t = 0; t < SimConst.TicksPerMonth; t++)
            {
                try { sim.Tick(); } catch (Exception e) { Console.WriteLine("SIM EXCEPTION " + e); return; }
                for (int k = 0; k < u.Alive.Length; k++) { int i = u.Alive[k]; if (u.City[i] < 0) continue; taskHist[u.Task[i]]++;
                    if (u.Task[i] == (byte)UnitTask.Work && u.Job[i] > 0) { string key = $"{db.Jobs[u.Job[i] - 1].Id}:a{u.Action[i]}:op{u.WorkOp[i]}"; actHist[key] = actHist.TryGetValue(key, out int v) ? v + 1 : 1; } }
            }
            if (m == 600)
            {
                var c0 = civ.Cities[0]; var rr = new SimRandom(3, 3);
                foreach (var name in new[] { "bld.hut", "bld.storage", "bld.farm_shed", "bld.house", "bld.well" }) { int d = db.Buildings.IdOf(name);
                    bool site = CityPlanner.FindSite(civ, c0, d, ref rr, out int2 o); Console.WriteLine($"PLAN {name} req {CityPlanner.RequirementsMet(civ, c0, d)} afford {civ.CanAfford(c0, db.Buildings[d])} site {site} count {civ.CountBuildings(c0, d)} max {db.Buildings[d].MaxPerCity}"); }
                Console.WriteLine($"PLAN housing {c0.HousingCapacity} pop {c0.Population} constructions {c0.ActiveConstructions} house-for {db.Buildings[CityPlanner.HouseFor(civ, c0)].Id}");
                for (int yy = c0.Center.y + 20; yy >= c0.Center.y - 20; yy--) { var sb = new System.Text.StringBuilder(); for (int xx = c0.Center.x - 30; xx <= c0.Center.x + 30; xx++) { if (!map.InBounds(xx, yy)) { sb.Append(' '); continue; } int ii = map.Index(xx, yy); int ow = map.Zones[map.ZoneIndexOf(xx, yy)].OwnerCity; char ch = map.Building[ii] >= 0 ? 'B' : map.IsWater(xx, yy) ? '~' : !map.IsWalkable(xx, yy) ? '#' : (map.Flags[ii] & (ushort)PG.Content.TileFlags.Road) != 0 ? 'r' : map.Ground[ii] == civ.Ids.FieldTile ? 'f' : (map.Flags[ii] & (ushort)PG.Content.TileFlags.Buildable) == 0 ? 'x' : ow == c0.Index ? '.' : ' '; sb.Append(ch); } Console.WriteLine("   |" + sb); }
            }
            if (m == 99999)
            {
                foreach (var cc in civ.Cities) { if (cc.Dead) continue; foreach (int b in cc.Buildings) { var bd = civ.Buildings[b]; if (bd.State != BuildingState.Construction) continue;
                    var st = sim.Units.Paths.Request(cc.Center, bd.Door, Mobility.Land, out int hh, false); if (hh >= 0) sim.Units.Paths.Release(hh);
                    Console.WriteLine($"SITE city {cc.Name} {db.Buildings[bd.Def].Id} door {bd.Door} progress {bd.BuildProgress:0.00} doorWalk {map.IsWalkable(bd.Door.x, bd.Door.y)} belowWalk {map.IsWalkable(bd.Door.x, bd.Door.y - 1)} belowBld {map.Building[map.Index(bd.Door.x, bd.Door.y - 1)]} pathFromCenter {st}"); } }
                for (int k = 0; k < u.Alive.Length; k++) { int i = u.Alive[k]; if (u.City[i] < 0 || u.Job[i] == 0 || db.Jobs[u.Job[i] - 1].Id != "job.builder") continue; Console.WriteLine($"  builder u{i} task {(UnitTask)u.Task[i]} act {u.Action[i]} wb {u.WorkBuilding[i]} pos {u.Tile(i)} tgt {u.TargetTile[i]} fails {u.PathFails[i]} path {u.PathHandle[i]}"); }
            }
            if (m == 120000)
            {
                var c0 = civ.Cities[0];
                int cb = c0.CenterBuilding; var bd = civ.Buildings[cb]; var door = bd.Door;
                Console.WriteLine($"CENTER b{cb} origin {bd.Origin} {bd.W}x{bd.H} door {door} doorFlags {(PG.Content.TileFlags)map.Flags[map.Index(door.x, door.y)]} below {(PG.Content.TileFlags)map.Flags[map.Index(door.x, door.y - 1)]} bldBelow {map.Building[map.Index(door.x, door.y - 1)]} region {sim.Regions.RegionAt(door.x, door.y, PG.World.MoveClass.Land)} regionBelow {sim.Regions.RegionAt(door.x, door.y - 1, PG.World.MoveClass.Land)} dirtyRegions {map.CountDirty(PG.World.DirtyMask.Regions)}");
                for (int b = 0; b < civ.Buildings.Length; b++) { var x = civ.Buildings[b]; Console.WriteLine($"  bld {b} {db.Buildings[x.Def].Id} {x.State} origin {x.Origin} {x.W}x{x.H} door {x.Door}"); }
                for (int yy = door.y + 8; yy >= door.y - 10; yy--) { var sb = new System.Text.StringBuilder(); for (int xx = door.x - 16; xx <= door.x + 16; xx++) { int ii = map.Index(xx, yy); char ch = map.Building[ii] >= 0 ? ((map.Flags[ii] & (ushort)PG.Content.TileFlags.Door) != 0 ? 'D' : (char)('A' + map.Building[ii] % 26)) : !map.IsWalkable(xx, yy) ? '#' : map.IsWater(xx, yy) ? '~' : '.'; sb.Append(ch); } Console.WriteLine("   " + sb); }
                { var rr = new SimRandom(1, 1); int h1 = civ.Ids.Hall1; bool req = CityPlanner.RequirementsMet(civ, c0, h1); bool aff = civ.CanAfford(c0, db.Buildings[h1]); bool site = CityPlanner.FindSite(civ, c0, h1, ref rr, out int2 org);
                  int okAll = 0, own = 0, bld = 0, flag = 0; int W = db.Buildings[h1].W, H = db.Buildings[h1].H;
                  foreach (int z in c0.Zones) { int zx0 = (z % map.ZonesX) * 8, zy0 = (z / map.ZonesX) * 8; for (int oy = zy0 - H; oy < zy0 + 8; oy++) for (int ox = zx0 - W; ox < zx0 + 8; ox++) { if (CityPlanner.FootprintFree(civ, new int2(ox, oy), W, H, c0.Index, 1)) okAll++; } }
                  Console.WriteLine($"  hall_1 {W}x{H}: free origins {okAll}");
                  { int2 o = c0.Center + new int2(-12, -14); for (int yy = o.y; yy < o.y + H; yy++) for (int xx = o.x; xx < o.x + W; xx++) { int ii = map.Index(xx, yy); ushort f = map.Flags[ii]; if ((f & (ushort)PG.Content.TileFlags.Buildable) == 0 || (f & (ushort)(PG.Content.TileFlags.Water | PG.Content.TileFlags.Burning | PG.Content.TileFlags.Reserved | PG.Content.TileFlags.Road)) != 0 || map.Zones[map.ZoneIndexOf(xx, yy)].OwnerCity != c0.Index || map.Building[ii] >= 0) { Console.WriteLine($"   blocked at {xx},{yy}: {(PG.Content.TileFlags)f} owner {map.Zones[map.ZoneIndexOf(xx, yy)].OwnerCity} bld {map.Building[ii]}"); goto done; } } Console.WriteLine("   origin ok"); done:; }
                  for (int yy = c0.Center.y + 14; yy >= c0.Center.y - 14; yy--) { var sb = new System.Text.StringBuilder(); for (int xx = c0.Center.x - 20; xx <= c0.Center.x + 20; xx++) { int ii = map.Index(xx, yy); int ow = map.Zones[map.ZoneIndexOf(xx, yy)].OwnerCity; char ch = map.Building[ii] >= 0 ? 'B' : !map.IsWalkable(xx, yy) ? '#' : map.IsWater(xx, yy) ? '~' : (map.Flags[ii] & (ushort)PG.Content.TileFlags.Road) != 0 ? 'r' : (map.Flags[ii] & (ushort)PG.Content.TileFlags.Buildable) == 0 ? 'x' : map.Feature[ii] != 0 ? 't' : ow == c0.Index ? '.' : ' '; sb.Append(ch); } Console.WriteLine("   |" + sb); }
                  Console.WriteLine($"  hall_1: req {req} afford {aff} site {site} {org} pop {c0.Population} residents {c0.Residents.Count} zones {c0.Zones.Count} constructions {c0.ActiveConstructions} nextPlan {c0.NextPlanTick - sim.Clock.Tick}"); }
                var st = sim.Units.Paths.Request(new int2(134, 126), door, Mobility.Land, out int h, false);
                Console.WriteLine($"  request from (134,126): {st} regFrom {sim.Regions.RegionAt(134, 126, PG.World.MoveClass.Land)} same {sim.Regions.SameIsland(134, 126, door.x, door.y, PG.World.MoveClass.Land)} straight? searches {sim.Units.Paths.StatSearches} detour {sim.Units.Paths.StatDetour}");
                var rf = sim.Regions.GetRegion(sim.Regions.RegionAt(134, 126, PG.World.MoveClass.Land)); var rt = sim.Regions.GetRegion(16);
                Console.WriteLine($"  centers {rf.Center} {rt.Center} tiles {rf.TileCount} {rt.TileCount}");
                if (h >= 0) sim.Units.Paths.Release(h);
            }
            if (m >= 60 && m % 12 == 0)
            {
                foreach (var cc in civ.Cities) { if (cc.Dead) continue; int fields = 0, crops = 0, ripe = 0, sheds = 0, adults = 0, kids = 0, elders = 0;
                    foreach (int z in cc.Zones) { int x0 = (z % map.ZonesX) * 8, y0 = (z / map.ZonesX) * 8; for (int yy = y0; yy < y0 + 8; yy++) for (int xx = x0; xx < x0 + 8; xx++) { int ii = map.Index(xx, yy); if (map.Ground[ii] == civ.Ids.FieldTile) fields++; if (map.Feature[ii] == civ.Ids.WheatCrop) { crops++; if (map.GetFeatureStage(ii) >= 3) ripe++; } } }
                    foreach (int b in cc.Buildings) if (civ.Buildings[b].Def == civ.Ids.FarmShed && civ.Buildings[b].State == BuildingState.Complete) sheds++;
                    foreach (int i in cc.Residents) { var st = (AgeStage)u.Age[i]; if (st == AgeStage.Adult) adults++; else if (st == AgeStage.Elder) elders++; else kids++; }
                    var ft = new int[16]; foreach (int i in cc.Residents) if (u.Job[i] > 0 && db.Jobs[u.Job[i] - 1].Id == "job.farmer") ft[u.Task[i]]++;
                    Console.WriteLine("   farmer tasks " + string.Join(",", ft) + $"");
                    Console.WriteLine($"Y{m / 12} {cc.Name} pop {cc.Population} adults {adults} elders {elders} kids {kids} sheds {sheds} fields {fields} crops {crops} ripe {ripe} famine {cc.FamineMonths} food {cc.FoodTotal} hungry {cc.Residents.FindAll(r => u.Saturation[r] < 40).Count}"); }
            }
            if (m % 3 != 0) continue;
            Console.WriteLine($"m{m} {PG.EditorTools.CivSoak.Summary(sim)}");
            Console.WriteLine("  tasks " + string.Join(",", taskHist));
            var keys = new System.Collections.Generic.List<string>(actHist.Keys); keys.Sort();
            Console.WriteLine("  work " + string.Join(" ", keys.ConvertAll(k => k + "=" + actHist[k])));
            Array.Clear(taskHist, 0, taskHist.Length); actHist.Clear();
            foreach (var c in civ.Cities)
            {
                if (c.Dead) continue;
                Console.Write("  quotas:");
                for (int j = 0; j < db.Jobs.Count; j++) if (c.JobQuota[j] > 0 || c.JobCount[j] > 0) Console.Write($" {db.Jobs[j].Id.Substring(4)} {c.JobCount[j]}/{c.JobQuota[j]}");
                Console.WriteLine($" | storage {c.StorageCapacity} stock {civ.StockTotal(c)}");
            }
            for (int k = 0; k < u.Alive.Length && k < 400; k++) { int i = u.Alive[k]; if (u.City[i] < 0 || m % 6 != 0) continue;
                Console.WriteLine($"   u{i} job {(u.Job[i] > 0 ? db.Jobs[u.Job[i] - 1].Id : "-")} task {(UnitTask)u.Task[i]} act {u.Action[i]} op {u.WorkOp[i]} carry {u.CarryRes[i]}x{u.CarryAmount[i]} pos {u.Tile(i)} tgt {u.TargetTile[i]} sat {u.Saturation[i]} age {(AgeStage)u.Age[i]} fails {u.PathFails[i]}"); }
        }
    }
}
