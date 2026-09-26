using System;
using PG.Content;
using PG.Core;
using PG.Sim;
using PG.WorldGen;
using Unity.Mathematics;

public static class WolfDebug
{
    public static void Run(ContentDB db, ulong seed)
    {
        var settings = new WorldGenSettings { Seed = seed, Size = MapSizePreset.Small, Template = "wgt.pangea", LandRatio = 0.5f };
        var map = WorldGenerator.Generate(settings, db);
        using var sim = new SimWorld(db, map, new SimRandomProvider(seed));
        sim.Nature.Laws.Set(sim.Units.LawAnimalSpawn, false, 0f);
        int sheep = db.Species.IdOf("sp.sheep"), wolf = db.Species.IdOf("sp.wolf");
        var u = sim.Units.Store;
        var rng = new SimRandom(seed, 5);
        int2 c = new int2(map.Width / 2, map.Height / 2);
        void Sp(int s, int n) { for (int k = 0, tries = 0; k < n && tries < 10000; tries++) { int x = c.x + rng.Range(-30, 31), y = c.y + rng.Range(-30, 31); if (!map.IsWalkable(x, y) || map.IsWater(x, y)) continue; u.Spawn(new UnitSpawnRequest { Species = s, Pos = new float2(x + .5f, y + .5f), AgeYears = db.Species[s].BaseStats[(int)StatId.Lifespan] * 0.3f, Sex = k % 2, Mother = EntityId.None, Father = EntityId.None, Subspecies = -1 }, 0, ref rng); k++; } }
        Sp(sheep, 50); Sp(wolf, 5);
        var causes = new int[16]; int births = 0, sheepKilled = 0;
        sim.Events.Subscribe<UnitDiedEvent>(e => { if (e.Species == wolf) causes[(int)e.Cause]++; else if (e.Species == sheep && e.Cause == DeathCause.Killed) sheepKilled++; });
        sim.Events.Subscribe<UnitBornEvent>(e => { if (e.Species == wolf) births++; });
        for (int month = 1; month <= 72; month++)
        {
            var tasks = new int[16]; int n = 0; float sat = 0, hp = 0; int wants = 0, preg = 0;
            for (int t = 0; t < SimConst.TicksPerMonth; t++)
            {
                sim.Tick();
                for (int k = 0; k < u.Alive.Length; k++) { int i = u.Alive[k]; if (u.Species[i] == wolf && u.State[i] == UnitStore.StateAlive) tasks[u.Task[i]]++; }
            }
            for (int k = 0; k < u.Alive.Length; k++) { int i = u.Alive[k]; if (u.Species[i] != wolf || u.State[i] != UnitStore.StateAlive) continue; n++; sat += u.Saturation[i]; hp += u.Hp[i]; if ((u.Wants[i] & 1) != 0) wants++; if (u.HasStatus(i, sim.Units.StPregnant)) preg++; }
            if (month % 3 == 0 || n == 0) Console.WriteLine($"m{month} wolves {n} sat {sat / math.max(1, n):0} hp {hp / math.max(1, n):0} wants {wants} preg {preg} births {births} sheepKilled {sheepKilled} deaths [{string.Join(",", causes)}] tasks [{string.Join(",", tasks)}]");
            if (false) for (int k = 0; k < u.Alive.Length; k++) { int i = u.Alive[k]; if (u.Species[i] != wolf) continue; var tl = u.Tile(i); Console.WriteLine($"   wolf {i} task {u.Task[i]} act {u.Action[i]} nextThink {u.NextThink[i] - sim.Clock.Tick} flags {u.Flags[i]:X} pos {u.Pos[i]} walk {map.IsWalkable(tl.x, tl.y)} water {map.IsWater(tl.x, tl.y)} z {u.Z[i]} age {u.Age[i]} sight {u.Stat(i, StatId.Sight)}"); }
            if (n == 0) break;
        }
        Console.WriteLine("tasks enum: " + string.Join(",", Enum.GetNames(typeof(UnitTask))) + " | causes: " + string.Join(",", Enum.GetNames(typeof(DeathCause))));
    }
}
