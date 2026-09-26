using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.World;
using PG.WorldGen;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace PG.EditorTools
{
    // Bölüm 6.15 acceptance checks for kingdoms and war (#1-#4, #8) plus meta save/load.
    // Batch: Unity -batchmode -projectPath . -executeMethod PG.EditorTools.MetaSoak.Run -logFile Logs/meta.log
    public static class MetaSoak
    {
        [MenuItem("PixelGenesis/Meta Soak")]
        public static void Run()
        {
            int failures = 0;
            try
            {
                DataSync.Sync();
                var db = ContentDB.LoadAll(Path.Combine(Application.streamingAssetsPath, "Data"), null);
                failures += Loyalty();
                failures += Succession(db);
                failures += Conquest(db);
                failures += Performance(db);
                failures += SaveLoad(db);
                failures += Colony(db);
                failures += World(db, 200);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
            }
            Debug.Log(failures == 0 ? "[MetaSoak] ALL OK" : $"[MetaSoak] {failures} failure(s)");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        static readonly string[] Species = { "sp.human", "sp.elf", "sp.dwarf", "sp.orc" };

        // Four civilised species, one per quarter of a single continent.
        static SimWorld FourSpecies(ContentDB db, ulong seed, MapSizePreset size = MapSizePreset.Medium)
        {
            var settings = new WorldGenSettings { Seed = seed, Size = size, Template = "wgt.pangea", ForestDensity = 0.4f };
            var map = WorldGenerator.Generate(settings, db);
            var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "Meta");
            sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
            sim.SpawnInitialAnimals();
            for (int s = 0; s < Species.Length; s++)
            {
                int2 at = new int2(map.Width * (1 + 2 * (s % 2)) / 4, map.Height * (1 + 2 * (s / 2)) / 4);
                CivSoak.SpawnGroup(sim, db.Species.IdOf(Species[s]), 12, CivSoak.Meadow(map, at), 6, 11 + (ulong)s);
            }
            return sim;
        }

        // #1: 4 species on one continent for 200 years -> >= 1 war, >= 1 rebellion, >= 1 king change (plots come in Faz 7).
        public static int World(ContentDB db, int years, ulong seed = 31)
        {
            using (var sim = FourSpecies(db, seed))
            {
                int wars = 0, rebellions = 0, kingChanges = 0, captures = 0, fallen = 0, founded = 0, razed = 0;
                sim.Events.Subscribe<WarDeclaredEvent>(e => wars++);
                sim.Events.Subscribe<RebellionEvent>(e => rebellions++);
                sim.Events.Subscribe<KingChangedEvent>(e => { if (e.OldKing != 0) kingChanges++; });
                sim.Events.Subscribe<CityCapturedEvent>(e => captures++);
                sim.Events.Subscribe<KingdomFellEvent>(e => fallen++);
                sim.Events.Subscribe<KingdomFoundedEvent>(e => founded++);
                sim.Events.Subscribe<CityDiedEvent>(e => razed++);
                var sw = Stopwatch.StartNew();
                var line = new StringBuilder();
                for (int year = 1; year <= years; year++)
                {
                    for (int t = 0; t < SimConst.TicksPerYear; t++) sim.Tick();
                    if (year % 20 == 0)
                        line.Append($"\n  y{year}: people {CivSoak.CivPopulation(sim)} cities {sim.Civ.AliveCities} kingdoms {sim.Meta.AliveKingdoms} " +
                                    $"wars {wars} (active {ActiveWars(sim.Meta)}) rebellions {rebellions} kings {kingChanges} captured {captures} fallen {fallen} | {Kingdoms(sim)}");
                }
                bool ok = wars >= 1 && rebellions >= 1 && kingChanges >= 1;
                Debug.Log($"[MetaSoak] world {years}y seed {seed} ({sw.Elapsed.TotalSeconds:0.0} s):{line}\n  -> kingdoms founded {founded}, wars {wars}, rebellions {rebellions}, " +
                          $"king changes {kingChanges}, cities captured {captures}, cities died {razed} -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        static int ActiveWars(MetaState meta)
        {
            int n = 0;
            foreach (var w in meta.Wars) if (w.Active) n++;
            return n;
        }

        public static string Kingdoms(SimWorld sim)
        {
            var sb = new StringBuilder();
            int shown = 0;
            foreach (var k in sim.Meta.Kingdoms)
            {
                if (k.Dead || shown++ >= 6) continue;
                sb.Append($"[{k.Name} {sim.Content.Species[k.Species].Id.Substring(3)} c{k.Cities.Count} p{k.Population} s{k.Soldiers} pw{k.Power:0} ");
                int worst = 100, loyal = 100;
                for (int o = 0; o < sim.Meta.Kingdoms.Count; o++)
                    if (o != k.Index && !sim.Meta.Kingdoms[o].Dead) worst = math.min(worst, k.OpinionOf(o));
                foreach (int c in k.Cities) loyal = math.min(loyal, sim.Civ.Cities[c].Loyalty);
                sb.Append($"op{worst} loy{loyal}] ");
            }
            return sb.ToString();
        }

        // #3: the loyalty formula with known inputs.
        public static int Loyalty()
        {
            int fails = 0;
            void Check(string name, in KingdomSystem.LoyaltyInputs x, int expected)
            {
                int got = KingdomSystem.LoyaltyFormula(x);
                if (got != expected) { fails++; Debug.Log($"[MetaSoak] loyalty {name}: {got} != {expected}"); }
            }
            // capital, same everything, king diplo 4 and loyal, happiness 20, era +10 -> 50 + 12 + 15 + 100 + 25 + 4 + 10 = 216
            Check("capital", new KingdomSystem.LoyaltyInputs
            {
                HasKing = true, KingDiplo = 4, KingLoyal = true, IsCapital = true, SameCulture = true, SameReligion = true, SameSpecies = true,
                Happiness = 20, EraBonus = 10,
            }, 216);
            // conquered foreign city 160 tiles away, 3 war years, new king, unhappy -> 50 - 20 - 15 - 10 - 10 - 8 - 9 - 15 - 10 = -47
            Check("foreign", new KingdomSystem.LoyaltyInputs
            {
                HasKing = true, DistanceTiles = 160, Happiness = -40, WarYears = 3, EraBonus = -15, NewKing = true,
            }, -47);
            // war weariness caps at -20; relative leader +10; treacherous king -15 -> 50 - 15 - 4 + 25 - 20 + 10 = 46
            Check("weary", new KingdomSystem.LoyaltyInputs
            {
                HasKing = true, KingTreacherous = true, DistanceTiles = 32, SameCulture = true, SameReligion = true, SameSpecies = true,
                WarYears = 12, LeaderIsRelative = true,
            }, 46);
            Debug.Log($"[MetaSoak] loyalty formula: {(fails == 0 ? "ok" : "FAIL")}");
            return fails == 0 ? 0 : 1;
        }

        // A small world with one founded city per group; returns the cities.
        static List<City> Villages(SimWorld sim, ContentDB db, params (string species, int2 at, int count)[] groups)
        {
            var cities = new List<City>();
            var group = new List<int>();
            ref var rng = ref sim.Rng.Get(RngStream.Civ);
            foreach (var g in groups)
            {
                int species = db.Species.IdOf(g.species);
                int before = sim.Units.Store.HighWater;
                CivSoak.SpawnGroup(sim, species, g.count, CivSoak.Meadow(sim.World, g.at), 6, (ulong)(before + 3));
                var u = sim.Units.Store;
                group.Clear();
                for (int i = before; i < u.HighWater; i++) if (u.State[i] == UnitStore.StateAlive) group.Add(i);
                // adults first so the leader is one of them
                var city = SettlementSystem.Found(sim.Context, sim.Civ, group, ref rng);
                if (city != null) cities.Add(city);
            }
            CivMonthlySystem.RefreshCaches(sim.Units, sim.Civ);
            return cities;
        }

        static SimWorld Blank(ContentDB db, ulong seed, MapSizePreset size = MapSizePreset.Medium)
        {
            var settings = new WorldGenSettings { Seed = seed, Size = size, Template = "wgt.pangea", ForestDensity = 0.3f };
            var map = WorldGenerator.Generate(settings, db);
            var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "MetaTest");
            sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
            return sim;
        }

        // #2 (primogeniture, the rule until cultures carry succ_* traits): the king's eldest child inherits; without children
        // the capital's leader does.
        public static int Succession(ContentDB db)
        {
            using (var sim = Blank(db, 41))
            {
                var map = sim.World;
                var cities = Villages(sim, db, ("sp.human", new int2(map.Width / 2, map.Height / 2), 12));
                var ks = new KingdomSystem();
                ref var rng = ref sim.Rng.Get(RngStream.Meta);
                ks.Monthly(sim.Context, sim.Meta, ref rng);
                if (cities.Count == 0 || sim.Meta.Kingdoms.Count == 0) { Debug.Log("[MetaSoak] succession: no kingdom -> FAIL"); return 1; }
                var k = sim.Meta.Kingdoms[0];
                var u = sim.Units.Store;
                int king = sim.Meta.FindInKingdom(k, k.KingUid);
                // two children of the king; the elder must inherit
                int elder = Child(sim, king, 20f, cities[0]);
                int younger = Child(sim, king, 10f, cities[0]);
                CivMonthlySystem.RefreshCaches(sim.Units, sim.Civ);
                ks.Monthly(sim.Context, sim.Meta, ref rng);
                u.Kill(king, DeathCause.OldAge);
                u.FlushDeaths(sim.Events, sim.Clock.Tick, sim.World.Width);
                CivMonthlySystem.RefreshCaches(sim.Units, sim.Civ);
                ks.Monthly(sim.Context, sim.Meta, ref rng);
                bool heirOk = k.KingUid == u.Uid[elder] && k.KingChanges == 1;
                // the new king dies without children -> the capital's leader (or eldest adult) takes the crown
                long leader = cities[0].LeaderUid;
                u.Kill(elder, DeathCause.OldAge);
                u.FlushDeaths(sim.Events, sim.Clock.Tick, sim.World.Width);
                CivMonthlySystem.RefreshCaches(sim.Units, sim.Civ);
                ks.Monthly(sim.Context, sim.Meta, ref rng);
                bool fallbackOk = k.KingUid != 0 && k.KingUid != u.Uid[elder] && k.KingChanges == 2 &&
                                  (k.KingUid == leader || sim.Meta.FindInKingdom(k, k.KingUid) >= 0);
                bool ok = heirOk && fallbackOk && younger >= 0;
                Debug.Log($"[MetaSoak] succession: eldest child inherits {heirOk}, leader fallback {fallbackOk} -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        static int Child(SimWorld sim, int parent, float ageYears, City city)
        {
            var u = sim.Units.Store;
            var rng = new SimRandom(99, (ulong)ageYears);
            var id = u.Spawn(new UnitSpawnRequest
            {
                Species = u.Species[parent], Pos = u.Pos[parent], AgeYears = ageYears, Sex = 0,
                Mother = EntityId.None, Father = u.IdOf(parent), Subspecies = -1,
            }, sim.Clock.Tick, ref rng);
            u.City[id.Index] = city.Index;
            return id.Index;
        }

        // #4: an undefended city changes hands; a city without people is razed.
        public static int Conquest(ContentDB db)
        {
            using (var sim = Blank(db, 43))
            {
                var map = sim.World;
                int2 c = new int2(map.Width / 2, map.Height / 2);
                var cities = Villages(sim, db, ("sp.orc", c, 12), ("sp.human", c + new int2(60, 0), 12), ("sp.elf", c + new int2(0, 60), 8));
                if (cities.Count < 3) { Debug.Log($"[MetaSoak] conquest: only {cities.Count} villages -> FAIL"); return 1; }
                var meta = sim.Meta;
                var ks = new KingdomSystem();
                ref var rng = ref sim.Rng.Get(RngStream.Meta);
                ks.Monthly(sim.Context, meta, ref rng);
                var orcs = cities[0];
                var humans = cities[1];
                var elves = cities[2];
                int ko = orcs.Kingdom, kh = humans.Kingdom, ke = elves.Kingdom;
                var u = sim.Units.Store;
                // the elves leave their village
                foreach (int i in elves.Residents) u.City[i] = -1;
                CivMonthlySystem.RefreshCaches(sim.Units, sim.Civ);
                var war = meta.DeclareWar(ko, kh, db.WarTypes.IdOrDefault("war.conquest"), sim.Clock.Tick, sim.Events);
                var war2 = meta.DeclareWar(ko, ke, db.WarTypes.IdOrDefault("war.conquest"), sim.Clock.Tick, sim.Events);
                // no defenders: nobody of the humans is a warrior or guard
                foreach (int i in humans.Residents) u.Job[i] = 0;
                bool captured = Besiege(sim, orcs, humans, war, ko);
                bool razed = Besiege(sim, orcs, elves, war2, ko);
                bool ok = captured && humans.Kingdom == ko && humans.Loyalty == 0 && razed && elves.Dead && meta.Kingdoms[ke].Dead;
                Debug.Log($"[MetaSoak] conquest: captured {captured} (city now kingdom {humans.Kingdom}, orcs {ko}), empty city razed {elves.Dead}, " +
                          $"kingdom fell {meta.Kingdoms[ke].Dead} -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        // Puts three warriors of `from` on the target's centre as a besieging army and steps the army system.
        static bool Besiege(SimWorld sim, City from, City target, War war, int kingdom)
        {
            var meta = sim.Meta;
            var u = sim.Units.Store;
            var army = new Army { Index = meta.Armies.Count, Kingdom = kingdom, War = war.Index, HomeCity = from.Index, TargetCity = target.Index, State = ArmyState.Sieging, StateTick = sim.Clock.Tick };
            foreach (int i in from.Residents)
            {
                if (army.Soldiers.Count >= 3 || !JobAssigner.CanWork(u, i) || u.Uid[i] == from.LeaderUid) continue;
                u.Job[i] = (byte)(sim.Civ.Ids.JobWarrior + 1);
                u.ArmyOf[i] = army.Index;
                u.Pos[i] = (float2)target.Center + 0.5f;
                army.Soldiers.Add(i);
                army.SoldierUids.Add(u.Uid[i]);
            }
            army.StartSize = army.Soldiers.Count;
            meta.Armies.Add(army);
            var system = new ArmySystem();
            system.Step(sim.Context);
            return target.Dead || target.Kingdom == kingdom;
        }

        // #8: 20 kingdoms and 5,000 people -> the monthly meta update stays under 15 ms.
        public static int Performance(ContentDB db)
        {
            using (var sim = Blank(db, 47, MapSizePreset.Huge))
            {
                var map = sim.World;
                // random land sites at least 48 tiles apart until there are 20 villages
                var cities = new List<City>();
                var sites = new List<int2>();
                var pick = new SimRandom(47, 1);
                for (int n = 0; n < 20000 && cities.Count < 20; n++)
                {
                    int2 at = new int2(pick.Range(20, map.Width - 20), pick.Range(20, map.Height - 20));
                    if (!map.IsWalkable(at.x, at.y) || map.IsWater(at.x, at.y) || !map.HasFlag(at.x, at.y, TileFlags.Buildable)) continue;
                    bool far = true;
                    foreach (var s in sites) if (math.distance((float2)s, (float2)at) < 48f) far = false;
                    if (!far) continue;
                    sites.Add(at);
                    cities.AddRange(Villages(sim, db, (Species[cities.Count % 4], at, 260)));
                }
                var meta = sim.Meta;
                var ks = new KingdomSystem();
                var ws = new WarSystem();
                var army = new ArmySystem();
                ref var rng = ref sim.Rng.Get(RngStream.Meta);
                ks.Monthly(sim.Context, meta, ref rng);
                Debug.Log($"[MetaSoak] perf setup: {cities.Count} villages, {sim.Civ.AliveCities} alive cities, {meta.Kingdoms.Count} kingdoms ({meta.AliveKingdoms} alive)");
                // everyone hates everyone: wars, armies and the full opinion matrix are exercised
                foreach (var a in meta.Kingdoms)
                    foreach (var b in meta.Kingdoms)
                        if (a != b) a.SetOpinion(b.Index, -100);
                ws.Yearly(sim.Context, meta, ref rng);
                var u = sim.Units.Store;
                foreach (var city in cities)
                {
                    int n = 0;
                    foreach (int i in city.Residents) if (JobAssigner.CanWork(u, i) && n++ < 40) u.Job[i] = (byte)(sim.Civ.Ids.JobWarrior + 1);
                }
                var times = new List<double>();
                for (int m = 0; m < 12; m++)
                {
                    var sw = Stopwatch.StartNew();
                    ks.Monthly(sim.Context, meta, ref rng);
                    army.Step(sim.Context);
                    times.Add(sw.Elapsed.TotalMilliseconds);
                }
                var ys = Stopwatch.StartNew();
                ws.Yearly(sim.Context, meta, ref rng);
                double yearly = ys.Elapsed.TotalMilliseconds;
                times.Sort();
                double median = times[times.Count / 2];
                bool ok = meta.AliveKingdoms >= 20 && sim.Units.Store.Count >= 5000 && median < 15.0;
                Debug.Log($"[MetaSoak] performance: {meta.AliveKingdoms} kingdoms, {sim.Units.Store.Count} units, {ActiveWars(meta)} wars, {meta.Armies.Count} armies: " +
                          $"monthly meta {median:0.00} ms (worst {times[times.Count - 1]:0.00}), yearly wars {yearly:0.00} ms -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        // 5.12 #2: a crowded island city ships 6-8 settlers to another island; they found a village of the same kingdom.
        public static int Colony(ContentDB db)
        {
            // seed 63: most archipelago land is joined by walkable shallows; this one has separate islands
            var settings = new WorldGenSettings { Seed = 63, Size = MapSizePreset.Medium, Template = "wgt.archipelago", ForestDensity = 0.3f };
            var map = WorldGenerator.Generate(settings, db);
            using (var sim = new SimWorld(db, map, new SimRandomProvider(settings.Seed), null, "Colony"))
            {
                sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
                // the largest island's grassland near a coast
                var cities = Villages(sim, db, ("sp.human", CivSoak.Meadow(map, new int2(map.Width / 2, map.Height / 2)), 24));
                if (cities.Count == 0) { Debug.Log("[MetaSoak] colony: no village -> FAIL"); return 1; }
                var city = cities[0];
                var meta = sim.Meta;
                ref var rng = ref sim.Rng.Get(RngStream.Meta);
                new KingdomSystem().Monthly(sim.Context, meta, ref rng);
                // nearest open-sea tile stands in for the docks' port
                int2 port = default;
                bool found = false;
                for (int r = 1; r < 80 && !found; r++)
                    for (int dy = -r; dy <= r && !found; dy++)
                        for (int dx = -r; dx <= r && !found; dx++)
                        {
                            int2 p = city.Center + new int2(dx, dy);
                            if (math.max(math.abs(dx), math.abs(dy)) != r || !map.InBounds(p.x, p.y) || !map.IsWater(p.x, p.y)) continue;
                            if (!sim.Units.Paths.SameIsland(p, new int2(1, 1), Mobility.Water)) continue;
                            port = p;
                            found = true;
                        }
                Boat boat = null;
                for (int attempt = 0; attempt < 8 && boat == null; attempt++)
                    if (found && BoatSystem.FindIsland(meta, city, port, ref rng, out int2 landing, out int2 shore))
                        boat = BoatSystem.Launch(sim.Context, meta, city, port, landing, shore, ref rng);
                if (boat == null)
                {
                    bool island = BoatSystem.FindIsland(meta, city, port, ref rng, out int2 l2, out int2 s2);
                    Debug.Log($"[MetaSoak] colony: no voyage (port {port} found {found}, island {island} {l2} shore {s2}, residents {city.Residents.Count}) -> FAIL");
                    return 1;
                }
                int kingdom = city.Kingdom, passengers = boat.Passengers.Count;
                City colony = null;
                int t = 0;
                for (; t < 8 * SimConst.TicksPerYear && colony == null; t++)
                {
                    sim.Tick();
                    if (t % 60 != 59) continue;
                    foreach (var c in sim.Civ.Cities)
                        if (c != city && !c.Dead && c.Kingdom == kingdom && math.distance((float2)c.Center, (float2)boat.Landing) < 40f) colony = c;
                }
                bool ok = colony != null && boat.State == BoatState.Done;
                Debug.Log($"[MetaSoak] colony: boat with {passengers} settlers {boat.State}, colony {(colony != null ? colony.Name : "none")} after {t / (float)SimConst.TicksPerYear:0.0} years, " +
                          $"same kingdom {colony != null} -> {(ok ? "ok" : "FAIL")}");
                return ok ? 0 : 1;
            }
        }

        // Kingdoms, wars and loyalty survive save/load and the worlds keep running identically.
        public static int SaveLoad(ContentDB db)
        {
            const int slot = 996;
            using (var sim = Blank(db, 53))
            {
                var map = sim.World;
                int2 c = new int2(map.Width / 2, map.Height / 2);
                var cities = Villages(sim, db, ("sp.orc", c, 14), ("sp.human", c + new int2(50, 0), 14));
                for (int t = 0; t < 2 * SimConst.TicksPerYear; t++) sim.Tick();
                var meta = sim.Meta;
                if (meta.Kingdoms.Count >= 2) meta.DeclareWar(0, 1, db.WarTypes.IdOrDefault("war.conquest"), sim.Clock.Tick, sim.Events);
                for (int t = 0; t < 3 * SimConst.TicksPerMonth + 13; t++) sim.Tick();
                PG.Persistence.SaveSystem.Save(slot, sim);
                var state = PG.Persistence.SaveSystem.Load(slot, db);
                using (var loaded = new SimWorld(db, state.World, state.Rng, state.Clock, state.WorldName))
                {
                    state.ApplyTo(loaded);
                    bool before = Hash(sim) == Hash(loaded);
                    for (int i = 0; i < 900; i++) { sim.Tick(); loaded.Tick(); }
                    bool after = Hash(sim) == Hash(loaded);
                    PG.Persistence.SaveSystem.Delete(slot);
                    bool ok = before && after && meta.Kingdoms.Count >= 2 && meta.Wars.Count >= 1 && state.Warnings.Count == 0;
                    Debug.Log($"[MetaSoak] save/load with {meta.Kingdoms.Count} kingdoms, {meta.Wars.Count} wars, {meta.Armies.Count} armies: equal after load {before}, " +
                              $"after 900 ticks {after}, warnings {state.Warnings.Count} -> {(ok ? "ok" : "FAIL")}");
                    return ok ? 0 : 1;
                }
            }
        }

        public static ulong Hash(SimWorld sim)
        {
            ulong h = CivSoak.Hash(sim);
            void Mix(long v) => h = (h ^ (ulong)v) * 1099511628211UL;
            var meta = sim.Meta;
            foreach (var k in meta.Kingdoms)
            {
                Mix(k.Uid); Mix(k.KingUid); Mix(k.Capital); Mix(k.Cities.Count); Mix((long)k.Flags); Mix(k.KingChanges);
                foreach (short o in k.Opinion) Mix(o);
            }
            foreach (var w in meta.Wars) { Mix(w.Attacker); Mix(w.Defender); Mix((long)w.Result); Mix(w.CasualtiesA); Mix(w.CasualtiesD); }
            foreach (var a in meta.Armies) { Mix((long)a.State); Mix(a.Soldiers.Count); Mix(a.TargetCity); }
            foreach (var c in sim.Civ.Cities) { Mix(c.Kingdom); Mix(c.Loyalty); }
            var u = sim.Units.Store;
            for (int k = 0; k < u.Alive.Length; k++) { int i = u.Alive[k]; Mix(u.ArmyOf[i]); Mix(u.Origin[i]); }
            return h;
        }
    }
}
