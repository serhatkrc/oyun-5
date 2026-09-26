using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.2-5.4: where cities go, what they build, where buildings go and how borders grow.
    public static class CityPlanner
    {
        public const int SiteSamples = 64;
        public const int FoundingSearchZones = 2;  // 15 tiles ~ 2 zones around the group
        static readonly int[] HouseThresholds = { 8, 30, 80 }; // hall_1 / hall_2 / hall_3 population (Bölüm 5.4 table)

        // ---------------- founding ----------------

        // Zone scoring (Bölüm 5.2) around `center`; the bonfire origin must fit on buildable, unowned ground.
        public static bool BestFoundingZone(CivState civ, int2 center, out int zone, out int2 origin)
        {
            var map = civ.Map;
            zone = -1;
            origin = default;
            float best = float.MinValue;
            int bw = civ.Content.Buildings[civ.Ids.Bonfire].W, bh = civ.Content.Buildings[civ.Ids.Bonfire].H;
            int czx = center.x >> WorldMap.ZoneShift, czy = center.y >> WorldMap.ZoneShift;
            for (int zy = czy - FoundingSearchZones; zy <= czy + FoundingSearchZones; zy++)
                for (int zx = czx - FoundingSearchZones; zx <= czx + FoundingSearchZones; zx++)
                {
                    if (zx < 0 || zy < 0 || zx >= map.ZonesX || zy >= map.ZonesY) continue;
                    int z = zy * map.ZonesX + zx;
                    if (map.Zones[z].OwnerCity >= 0) continue;
                    float score = ZoneScore(civ, z, forFounding: true);
                    if (score <= best) continue;
                    // bonfire in the middle of the zone, nudged until it fits
                    if (!FitInZone(civ, z, bw, bh, -1, out int2 o)) continue;
                    best = score;
                    zone = z;
                    origin = o;
                }
            return zone >= 0;
        }

        static bool FitInZone(CivState civ, int z, int w, int h, int city, out int2 origin)
        {
            var map = civ.Map;
            int x0 = (z % map.ZonesX) << WorldMap.ZoneShift, y0 = (z / map.ZonesX) << WorldMap.ZoneShift;
            for (int dy = 0; dy + h <= WorldMap.ZoneSize; dy++)
                for (int dx = 0; dx + w <= WorldMap.ZoneSize; dx++)
                {
                    origin = new int2(x0 + (WorldMap.ZoneSize - w) / 2 + ((dx & 1) == 0 ? dx / 2 : -(dx + 1) / 2),
                                      y0 + (WorldMap.ZoneSize - h) / 2 + ((dy & 1) == 0 ? dy / 2 : -(dy + 1) / 2));
                    if (FootprintFree(civ, origin, w, h, city, margin: 0)) return true;
                }
            origin = default;
            return false;
        }

        // Claims up to `count` zones in total around `zone` (best land first).
        public static void ClaimAround(CivState civ, City city, int zone, int count)
        {
            var map = civ.Map;
            int zx = zone % map.ZonesX, zy = zone / map.ZonesX;
            var candidates = Candidates;
            candidates.Clear();
            for (int y = zy - 1; y <= zy + 1; y++)
                for (int x = zx - 1; x <= zx + 1; x++)
                {
                    if (x < 0 || y < 0 || x >= map.ZonesX || y >= map.ZonesY) continue;
                    int z = y * map.ZonesX + x;
                    if (z == zone || map.Zones[z].OwnerCity >= 0 || !MostlyLand(map, z)) continue;
                    candidates.Add((ZoneScore(civ, z, false), z));
                }
            candidates.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : a.z.CompareTo(b.z));
            for (int k = 0; k < candidates.Count && city.Zones.Count < count; k++) civ.ClaimZone(city, candidates[k].z);
        }

        static readonly List<(float score, int z)> Candidates = new List<(float score, int z)>(9);

        static bool MostlyLand(WorldMap map, int z) => map.Zones[z].LandTiles >= (WorldMap.ZoneSize * WorldMap.ZoneSize * 4) / 10;

        // Bölüm 5.2 land score of one zone.
        public static float ZoneScore(CivState civ, int z, bool forFounding)
        {
            var map = civ.Map;
            int x0 = (z % map.ZonesX) << WorldMap.ZoneShift, y0 = (z / map.ZonesX) << WorldMap.ZoneShift;
            int buildable = 0, food = 0, trees = 0, danger = 0;
            bool hills = false, coast = false;
            int hillLevel = HillLevel(map);
            for (int y = y0; y < y0 + WorldMap.ZoneSize; y++)
                for (int x = x0; x < x0 + WorldMap.ZoneSize; x++)
                {
                    int i = map.Index(x, y);
                    ushort f = map.Flags[i];
                    if ((f & (ushort)TileFlags.Buildable) != 0 && (f & (ushort)TileFlags.Water) == 0) buildable++;
                    ushort feat = map.Feature[i];
                    if (feat != 0)
                    {
                        byte kind = map.Tables.FeatureKind[feat];
                        if (kind == FeatureDef.KindTree) trees++;
                        if (civ.Units != null && civ.Units.FeatureNutrition[feat] > 0f) food++;
                    }
                    else if (map.Biome[i] != 0 && (f & (ushort)TileFlags.Buildable) != 0) food++; // fieldable
                    if ((f & (ushort)(TileFlags.Burning)) != 0 || map.Tables.Active[map.Ground[i]] != 0) danger++;
                    int level = map.Tables.Level[map.Ground[i]];
                    if (level >= hillLevel) hills = true;
                    if ((f & (ushort)TileFlags.Water) != 0) coast = true;
                }
            float score = 3f * buildable / 64f + 2f * math.min(64, food) / 64f + math.min(24, trees) / 24f + (hills ? 1f : 0f) + (coast ? 1f : 0f);
            if (danger > 0) score -= 3f;
            if (NearOtherCity(civ, z, 3)) score -= 5f;
            if (forFounding && buildable < 40) score -= 10f;
            return score;
        }

        public static int HillLevel(WorldMap map)
        {
            int hills = map.Content.Tiles.IdOrDefault("tile.hills");
            return hills >= 0 ? map.Tables.Level[hills] : int.MaxValue;
        }

        static bool NearOtherCity(CivState civ, int z, int zones)
        {
            var map = civ.Map;
            int zx = z % map.ZonesX, zy = z / map.ZonesX;
            for (int y = zy - zones; y <= zy + zones; y++)
                for (int x = zx - zones; x <= zx + zones; x++)
                {
                    if (x < 0 || y < 0 || x >= map.ZonesX || y >= map.ZonesY) continue;
                    if (map.Zones[y * map.ZonesX + x].OwnerCity >= 0) return true;
                }
            return false;
        }

        // ---------------- borders (Bölüm 5.3) ----------------

        public static void GrowBorders(CivState civ, City city, bool force = false)
        {
            int target = math.min(CivState.MaxZonesPerCity, 9 + city.Population / 3 + city.HallTier * 4);
            if (city.Zones.Count >= (force ? CivState.MaxZonesPerCity : target)) return;
            var map = civ.Map;
            int best = -1;
            float bestScore = float.MinValue;
            float2 centerZone = new float2(city.Center.x >> WorldMap.ZoneShift, city.Center.y >> WorldMap.ZoneShift);
            foreach (int z in city.Zones)
            {
                int zx = z % map.ZonesX, zy = z / map.ZonesX;
                for (int d = 0; d < 4; d++)
                {
                    int nx = zx + (d == 0 ? 1 : d == 1 ? -1 : 0), ny = zy + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (nx < 0 || ny < 0 || nx >= map.ZonesX || ny >= map.ZonesY) continue;
                    int n = ny * map.ZonesX + nx;
                    if (map.Zones[n].OwnerCity >= 0 || !MostlyLand(map, n)) continue;
                    float s = ZoneScore(civ, n, false) + 5f * (NearOtherCity(civ, n, 3) ? 1f : 0f) // own borders touch: undo the penalty
                              - 0.3f * math.distance(new float2(nx, ny), centerZone);
                    if (NearForeignCity(civ, n, city.Index, 2)) s -= 5f;
                    if (s > bestScore || (s == bestScore && n < best)) { bestScore = s; best = n; }
                }
            }
            if (best >= 0) civ.ClaimZone(city, best);
        }

        static bool NearForeignCity(CivState civ, int z, int self, int zones)
        {
            var map = civ.Map;
            int zx = z % map.ZonesX, zy = z / map.ZonesX;
            for (int y = zy - zones; y <= zy + zones; y++)
                for (int x = zx - zones; x <= zx + zones; x++)
                {
                    if (x < 0 || y < 0 || x >= map.ZonesX || y >= map.ZonesY) continue;
                    int owner = map.Zones[y * map.ZonesX + x].OwnerCity;
                    if (owner >= 0 && owner != self) return true;
                }
            return false;
        }

        // ---------------- planning (Bölüm 5.4) ----------------

        public static bool PlanOne(in SimContext ctx, CivState civ, City city, ref SimRandom rng)
        {
            var ids = civ.Ids;
            int pop = city.Population;

            // 1. centre upgrade
            int nextTier = city.HallTier + 1;
            if (nextTier <= 3 && pop >= HouseThresholds[nextTier - 1] && civ.CountBuildings(city, ids.HallByTier[nextTier]) == 0
                && TryBuild(ctx, civ, city, ids.HallByTier[nextTier], ref rng)) return true;

            // 2. housing; no room left -> claim more land right away (DECISIONS #58)
            if (pop - city.HousingCapacity + 4 > 0 && CountInProgress(civ, city, BuildingCategory.House) < 2)
            {
                int house = HouseFor(civ, city);
                if (house >= 0 && TryBuild(ctx, civ, city, house, ref rng)) return true;
                if (house >= 0 && civ.CanAfford(city, civ.Content.Buildings[house]) && RequirementsMet(civ, city, house)) GrowBorders(civ, city, force: true);
            }

            // 3. storage: whole store 80% full or the material share nearly used up
            int materials = 0;
            for (int r = 0; r < city.Stock.Length; r++) if (!civ.Content.Resources[r].IsFood) materials += city.Stock[r];
            if ((civ.StockTotal(city) > city.StorageCapacity * 0.8f || materials > city.StorageCapacity * CivState.MaterialShare * 0.9f))
            {
                if (TryBuild(ctx, civ, city, ids.Storage, ref rng)) return true;
                if (civ.CountBuildings(city, ids.Storage) < civ.Content.Buildings[ids.Storage].MaxPerCity) GrowBorders(civ, city, force: true);
            }

            // 4. food
            bool shortOfFood = city.FoodTotal < pop * 6;
            if (shortOfFood || civ.CountBuildings(city, ids.FarmShed) == 0)
            {
                if (TryBuild(ctx, civ, city, ids.FarmShed, ref rng)) return true;
                if (TryBuild(ctx, civ, city, ids.FishingHut, ref rng)) return true;
            }
            if (civ.CountBuildings(city, ids.FarmShed) >= 2 && TryBuild(ctx, civ, city, ids.Granary, ref rng)) return true;

            // 5. production
            if (civ.CountBuildings(city, ids.LumberCamp) == 0 && TryBuild(ctx, civ, city, ids.LumberCamp, ref rng)) return true;
            if (civ.CountBuildings(city, ids.Mine) == 0 && TryBuild(ctx, civ, city, ids.Mine, ref rng)) return true;
            if (city.StockOf(ids.Iron) > 0 && civ.CountBuildings(city, ids.Smithy) == 0 && TryBuild(ctx, civ, city, ids.Smithy, ref rng)) return true;
            if (civ.HasComplete(city, ids.Granary) && TryBuild(ctx, civ, city, ids.Windmill, ref rng)) return true;
            if (civ.HasComplete(city, ids.Windmill) && TryBuild(ctx, civ, city, ids.Bakery, ref rng)) return true;

            // 6. defence and wellbeing
            if (civ.CountBuildings(city, ids.Watchtower) < 1 + pop / 40 && TryBuild(ctx, civ, city, ids.Watchtower, ref rng)) return true;
            if (civ.CountBuildings(city, ids.Barracks) == 0 && pop >= 40 && TryBuild(ctx, civ, city, ids.Barracks, ref rng)) return true;
            if (civ.CountBuildings(city, ids.Well) < 1 + pop / 50 && TryBuild(ctx, civ, city, ids.Well, ref rng)) return true;
            if (pop > 40)
            {
                if (TryBuild(ctx, civ, city, ids.Market, ref rng)) return true;
                if (TryBuild(ctx, civ, city, ids.Inn, ref rng)) return true;
            }
            if (TryBuild(ctx, civ, city, ids.Graveyard, ref rng)) return true;
            return false;
        }

        // Best house the city may build: tent before hall_1 (needs leather), hut, house, manor by hall tier; beasts nest, hives hive.
        public static int HouseFor(CivState civ, City city)
        {
            var ids = civ.Ids;
            if (city.Style == ids.StyleInsect && ids.Hive >= 0) return ids.Hive;
            if (city.Style == ids.StyleBeast && ids.Nest >= 0) return ids.Nest;
            int[] byTier = { ids.Tent, ids.Hut, ids.House, ids.Manor };
            for (int t = math.min(3, (int)city.HallTier); t >= 0; t--)
            {
                int d = byTier[t];
                if (d < 0) continue;
                if (civ.CanAfford(city, civ.Content.Buildings[d]) && RequirementsMet(civ, city, d)) return d;
            }
            return byTier[math.min(3, (int)city.HallTier)];
        }

        static int CountInProgress(CivState civ, City city, BuildingCategory cat)
        {
            int n = 0;
            foreach (int b in city.Buildings)
            {
                var data = civ.Buildings[b];
                if (data.State == BuildingState.Construction && civ.Content.Buildings[data.Def].CategoryCode == cat) n++;
            }
            return n;
        }

        public static bool TryBuild(in SimContext ctx, CivState civ, City city, int def, ref SimRandom rng)
        {
            if (def < 0) return false;
            var d = civ.Content.Buildings[def];
            if (d.MaxPerCity > 0 && civ.CountBuildings(city, def) >= d.MaxPerCity) return false;
            if (!RequirementsMet(civ, city, def) || !civ.CanAfford(city, d)) return false;
            if (d.IsCenter && city.CenterBuilding >= 0)
            {
                // halls grow in place over the old centre (DECISIONS #58): the old one goes, the site takes its role at once
                if (!FindCenterSite(civ, city, def, out int2 at)) return false;
                civ.Pay(city, d);
                civ.Remove(city.CenterBuilding);
                city.CenterBuilding = civ.Place(def, at, city, ctx.Clock.Tick);
                city.ActiveConstructions++;
                return true;
            }
            if (!FindSite(civ, city, def, ref rng, out int2 origin)) return false;
            civ.Pay(city, d);
            civ.Place(def, origin, city, ctx.Clock.Tick);
            city.ActiveConstructions++;
            return true;
        }

        // Largest hall is 15x15 tiles: the centre keeps a square around it free so it can grow (DECISIONS #58).
        public const int CenterReserve = 15;

        // New hall footprint covering the old centre's, on the city's own buildable ground (roads may be built over).
        static bool FindCenterSite(CivState civ, City city, int def, out int2 best)
        {
            var d = civ.Content.Buildings[def];
            var old = civ.Buildings[city.CenterBuilding];
            var map = civ.Map;
            best = default;
            bool found = false;
            int bestDist = int.MaxValue;
            for (int oy = old.Origin.y + old.H - d.H; oy <= old.Origin.y; oy++)
                for (int ox = old.Origin.x + old.W - d.W; ox <= old.Origin.x; ox++)
                {
                    bool ok = true;
                    for (int y = oy - 1; y < oy + d.H + 1 && ok; y++)
                        for (int x = ox - 1; x < ox + d.W + 1 && ok; x++)
                        {
                            if (!map.InBounds(x, y)) { ok = false; break; }
                            int i = map.Index(x, y);
                            bool inside = x >= ox && x < ox + d.W && y >= oy && y < oy + d.H;
                            int b = map.Building[i];
                            if (b >= 0 && b != city.CenterBuilding) { ok = false; break; }
                            if (!inside || b == city.CenterBuilding) continue;
                            ushort f = map.Flags[i];
                            if ((f & (ushort)TileFlags.Buildable) == 0 || (f & (ushort)(TileFlags.Water | TileFlags.Burning)) != 0) ok = false;
                            else if (map.Zones[map.ZoneIndexOf(x, y)].OwnerCity != city.Index) ok = false;
                        }
                    if (!ok) continue;
                    // keep the hall centred on the old one
                    int dist = math.abs(2 * ox + d.W - (2 * old.Origin.x + old.W)) + math.abs(2 * oy + d.H - (2 * old.Origin.y + old.H));
                    if (dist < bestDist) { bestDist = dist; best = new int2(ox, oy); found = true; }
                }
            return found;
        }

        // True when the footprint overlaps the square kept free around the centre.
        static bool InCenterReserve(CivState civ, City city, int2 o, int w, int h)
        {
            if (city.CenterBuilding < 0) return false;
            var c = civ.Buildings[city.CenterBuilding];
            int cx = c.Origin.x + c.W / 2, cy = c.Origin.y + c.H / 2, r = CenterReserve / 2 + 1;
            return o.x < cx + r && o.x + w > cx - r && o.y < cy + r && o.y + h > cy - r;
        }

        public static bool RequirementsMet(CivState civ, City city, int def)
        {
            foreach (var r in civ.Content.Buildings[def].Requirements)
            {
                switch (r.Kind)
                {
                    case BuildingReq.Pop: if (city.Population < r.Value) return false; break;
                    case BuildingReq.Building: if (!civ.HasComplete(city, r.Value)) return false; break;
                    case BuildingReq.Coast: if (!CityTouches(civ, city, TileFlags.Water)) return false; break;
                    case BuildingReq.NearHills: if (!CityHasHills(civ, city)) return false; break;
                    case BuildingReq.NearForest: if (!CityHasTrees(civ, city)) return false; break;
                    case BuildingReq.BeastCiv: if (city.Style != civ.Ids.StyleBeast) return false; break;
                    case BuildingReq.HiveMind: if (city.Style != civ.Ids.StyleInsect) return false; break;
                    // livestock, flowers, herbs, languages, religions, capitals and war victories arrive in later chapters
                    default: return false;
                }
            }
            return true;
        }

        static bool CityTouches(CivState civ, City city, TileFlags flag)
        {
            var map = civ.Map;
            foreach (int z in city.Zones)
                if ((flag == TileFlags.Water ? map.Zones[z].WaterTiles : map.Zones[z].LandTiles) > 0) return true;
            return false;
        }

        static bool CityHasHills(CivState civ, City city)
        {
            var map = civ.Map;
            int hillLevel = HillLevel(map);
            foreach (int z in city.Zones)
            {
                int x0 = (z % map.ZonesX) << WorldMap.ZoneShift, y0 = (z / map.ZonesX) << WorldMap.ZoneShift;
                for (int y = y0; y < y0 + WorldMap.ZoneSize; y += 2)
                    for (int x = x0; x < x0 + WorldMap.ZoneSize; x += 2)
                        if (map.Tables.Level[map.Ground[map.Index(x, y)]] >= hillLevel) return true;
            }
            return false;
        }

        static bool CityHasTrees(CivState civ, City city)
        {
            var map = civ.Map;
            foreach (int z in city.Zones)
                if (map.Zones[z].TreeCount > 0) return true;
            return false;
        }

        // Bölüm 5.4 site search: 64 random origins in the city's zones; homes and stores near the centre, farms and mines outside.
        public static bool FindSite(CivState civ, City city, int def, ref SimRandom rng, out int2 best)
        {
            var d = civ.Content.Buildings[def];
            var ids = civ.Ids;
            var map = civ.Map;
            best = default;
            float bestScore = float.MinValue;
            bool outskirts = def == ids.FarmShed || def == ids.Mine || def == ids.LumberCamp || def == ids.FishingHut || def == ids.Pasture
                             || def == ids.Graveyard || def == ids.Watchtower;
            for (int s = 0; s < SiteSamples; s++)
            {
                int z = city.Zones[rng.Range(0, city.Zones.Count)];
                int ox = ((z % map.ZonesX) << WorldMap.ZoneShift) + rng.Range(-d.W + 1, WorldMap.ZoneSize);
                int oy = ((z / map.ZonesX) << WorldMap.ZoneShift) + rng.Range(-d.H + 1, WorldMap.ZoneSize);
                var origin = new int2(ox, oy);
                if (!FootprintFree(civ, origin, d.W, d.H, city.Index, margin: 1)) continue;
                if (!SiteRule(civ, def, origin, d) || InCenterReserve(civ, city, origin, d.W, d.H)) continue;
                float dist = math.distance(new float2(origin.x + d.W * 0.5f, origin.y + d.H * 0.5f), city.Center);
                float score = outskirts ? 0.3f * dist : -0.5f * dist;
                if (Flat(map, origin, d.W, d.H)) score += 2f;
                if (NearRoad(map, origin, d.W, d.H)) score += 2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = origin;
                }
            }
            return bestScore > float.MinValue;
        }

        // Type-specific siting (Bölüm 5.4): docks and fishing huts touch water, mines are within 3 tiles of hills.
        static bool SiteRule(CivState civ, int def, int2 o, BuildingDef d)
        {
            var ids = civ.Ids;
            var map = civ.Map;
            if (def == ids.FishingHut || def == ids.Docks) return Around(map, o, d.W, d.H, 1, AroundTest.Water);
            if (def == ids.Mine) return Around(map, o, d.W, d.H, 3, AroundTest.Hills);
            if (def == ids.LumberCamp) return Around(map, o, d.W, d.H, 4, AroundTest.Tree);
            return true;
        }

        enum AroundTest : byte { Water, Hills, Tree }

        static bool Around(WorldMap map, int2 o, int w, int h, int r, AroundTest test)
        {
            int hillLevel = test == AroundTest.Hills ? HillLevel(map) : 0;
            for (int y = o.y - r; y < o.y + h + r; y++)
                for (int x = o.x - r; x < o.x + w + r; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    if (x >= o.x && x < o.x + w && y >= o.y && y < o.y + h) continue;
                    int i = map.Index(x, y);
                    switch (test)
                    {
                        case AroundTest.Water: if ((map.Flags[i] & (ushort)TileFlags.Water) != 0) return true; break;
                        case AroundTest.Hills: if (map.Tables.Level[map.Ground[i]] >= hillLevel) return true; break;
                        default: if (map.Tables.FeatureKind[map.Feature[i]] == FeatureDef.KindTree) return true; break;
                    }
                }
            return false;
        }

        // Footprint on the city's buildable land with a free ring of `margin` tiles (paths between houses; DECISIONS #58).
        public static bool FootprintFree(CivState civ, int2 o, int w, int h, int city, int margin)
        {
            var map = civ.Map;
            for (int y = o.y - margin; y < o.y + h + margin; y++)
                for (int x = o.x - margin; x < o.x + w + margin; x++)
                {
                    bool inside = x >= o.x && x < o.x + w && y >= o.y && y < o.y + h;
                    if (!map.InBounds(x, y)) { if (inside) return false; continue; }
                    int i = map.Index(x, y);
                    if (map.Building[i] >= 0) return false;
                    if (!inside) continue;
                    ushort f = map.Flags[i];
                    // roads may be built over (the new building draws its own road; DECISIONS #58)
                    if ((f & (ushort)TileFlags.Buildable) == 0 || (f & (ushort)(TileFlags.Water | TileFlags.Burning | TileFlags.Reserved)) != 0) return false;
                    if (map.Zones[map.ZoneIndexOf(x, y)].OwnerCity != city) return false;
                }
            return true;
        }

        static bool Flat(WorldMap map, int2 o, int w, int h)
        {
            int level = map.Tables.Level[map.Ground[map.Index(o.x, o.y)]];
            for (int y = o.y; y < o.y + h; y++)
                for (int x = o.x; x < o.x + w; x++)
                    if (map.Tables.Level[map.Ground[map.Index(x, y)]] != level) return false;
            return true;
        }

        static bool NearRoad(WorldMap map, int2 o, int w, int h)
        {
            int y = o.y - 2;
            if (y < 0) return false;
            for (int x = o.x; x < o.x + w; x++)
                if (map.InBounds(x, y) && map.HasFlag(x, y, TileFlags.Road)) return true;
            return false;
        }
    }

    // Bölüm 5.3/5.10 on land: a crowded city with no room left sends a group of young adults to found a new village.
    public static class Settlers
    {
        public const int MinPopulation = 20, GroupMin = 6, GroupMax = 8;
        public const int MinDistance = 36, MaxDistance = 90;

        public static bool TrySend(in SimContext ctx, CivState civ, City city, ref SimRandom rng)
        {
            // Bölüm 5.10: crowded (pop > homes x 1.1) or out of land to claim
            bool crowded = city.Population > city.HousingCapacity * 1.1f || city.Zones.Count >= CivState.MaxZonesPerCity - 10;
            if (city.Population < MinPopulation || !crowded) return false;
            if (city.HallTier < 1) return false; // hunger is a reason to leave, not to stay
            var u = ctx.Units.Store;
            int adults = 0;
            foreach (int i in city.Residents) if ((AgeStage)u.Age[i] == AgeStage.Adult) adults++;
            if (adults < 3 * GroupMin) return false; // a group never takes more than a third of the workers (DECISIONS #55)
            if (!FindDestination(civ, city, ref rng, out int2 dest)) return false;
            int sent = 0, want = math.min(adults / 3, rng.Range(GroupMin, GroupMax + 1));
            foreach (int i in city.Residents)
            {
                if (sent >= want) break;
                if ((AgeStage)u.Age[i] != AgeStage.Adult || u.Uid[i] == city.LeaderUid) continue;
                u.City[i] = -1;
                u.Job[i] = 0;
                u.HomeBuilding[i] = -1;
                u.WorkBuilding[i] = -1;
                u.Task[i] = (byte)UnitTask.Migrate;
                u.Action[i] = 0;
                u.Timer[i] = 0;
                u.TargetTile[i] = dest + new int2(rng.Range(-3, 4), rng.Range(-3, 4));
                ctx.Units.ClearPath(i);
                sent++;
            }
            return sent > 0;
        }

        // A reachable, unowned, well-scored zone 36-90 tiles away on the same island.
        static bool FindDestination(CivState civ, City city, ref SimRandom rng, out int2 dest)
        {
            var map = civ.Map;
            dest = default;
            float best = float.MinValue;
            float radius = math.sqrt(city.Zones.Count) * WorldMap.ZoneSize * 0.6f; // rough city radius
            float min = math.max(MinDistance, radius + CivState.NoCityRadius), max = math.max(MaxDistance, min + 40f);
            for (int s = 0; s < 64; s++)
            {
                float angle = rng.Range(0f, 6.2831853f);
                float dist = rng.Range(min, max);
                int2 p = city.Center + (int2)math.round(new float2(math.cos(angle), math.sin(angle)) * dist);
                if (!map.InBounds(p.x, p.y) || !map.IsWalkable(p.x, p.y) || map.IsWater(p.x, p.y)) continue;
                if (!civ.Units.Paths.CanStand(p.x, p.y, Mobility.Land)) continue;
                if (!SameIsland(civ, city.Center, p)) continue;
                int z = map.ZoneIndexOf(p.x, p.y);
                if (map.Zones[z].OwnerCity >= 0 || SettlementSystem.CityWithin(civ, city.Species, p, CivState.NoCityRadius)) continue;
                float score = CityPlanner.ZoneScore(civ, z, true);
                if (score > best) { best = score; dest = p; }
            }
            return best > 0f;
        }

        static bool SameIsland(CivState civ, int2 a, int2 b) => civ.Units.Paths.SameIsland(a, b, Mobility.Land);
    }
}
