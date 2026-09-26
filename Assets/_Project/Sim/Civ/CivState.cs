using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Sim
{
    // Content ids the civ systems use every tick (-1 when a mod removed them).
    public sealed class CivIds
    {
        public readonly int Bonfire, Hall1, Hall2, Hall3, Tent, Hut, House, Manor, Nest, Hive, Storage, Granary, FarmShed, Mine,
                            LumberCamp, Smithy, FishingHut, Windmill, Bakery, Pasture, Barracks, Watchtower, Well, Market, Inn, Ruins,
                            Graveyard, Docks;
        public readonly int Wood, Stone, Wheat, Bread, Meat, Fish, Berries, Fruit, Mushroom, Leather, Gold, Iron, Copper, Bone,
                            Milk, Wool, Honey, Herbs, Clay;
        public readonly int JobBuilder, JobGatherer, JobFarmer, JobLumberjack, JobMiner, JobFisher, JobHunter, JobHerder, JobSmith,
                            JobBaker, JobWarrior, JobGuard, JobLeader;
        public readonly int HapNewHome, HapHungry, HapHomeless, HapFamilyDied, HapChildBorn, HapAteWell;
        public readonly ushort WheatCrop;
        public readonly byte FieldTile;
        public readonly int[] HallByTier;   // 0 bonfire, 1-3 halls
        public readonly int StyleHuman, StyleBeast, StyleInsect;

        public CivIds(ContentDB db)
        {
            int B(string id) => db.Buildings.IdOrDefault(id);
            int R(string id) => db.Resources.IdOrDefault(id);
            int J(string id) => db.Jobs.IdOrDefault(id);
            int H(string id) => db.HappinessEvents.IdOrDefault(id);
            Bonfire = B("bld.bonfire"); Hall1 = B("bld.hall_1"); Hall2 = B("bld.hall_2"); Hall3 = B("bld.hall_3");
            Tent = B("bld.tent"); Hut = B("bld.hut"); House = B("bld.house"); Manor = B("bld.manor"); Nest = B("bld.nest"); Hive = B("bld.hive");
            Storage = B("bld.storage"); Granary = B("bld.granary"); FarmShed = B("bld.farm_shed"); Mine = B("bld.mine");
            LumberCamp = B("bld.lumber_camp"); Smithy = B("bld.smithy"); FishingHut = B("bld.fishing_hut"); Windmill = B("bld.windmill");
            Bakery = B("bld.bakery"); Pasture = B("bld.pasture"); Barracks = B("bld.barracks"); Watchtower = B("bld.watchtower");
            Well = B("bld.well"); Market = B("bld.market"); Inn = B("bld.inn"); Ruins = B("bld.ruins"); Graveyard = B("bld.graveyard");
            Docks = B("bld.docks");
            HallByTier = new[] { Bonfire, Hall1, Hall2, Hall3 };
            Wood = R("res.wood"); Stone = R("res.stone"); Wheat = R("res.wheat"); Bread = R("res.bread"); Meat = R("res.meat");
            Fish = R("res.fish"); Berries = R("res.berries"); Fruit = R("res.fruit"); Mushroom = R("res.mushroom"); Leather = R("res.leather");
            Gold = R("res.gold"); Iron = R("res.iron"); Copper = R("res.copper"); Bone = R("res.bone"); Milk = R("res.milk"); Wool = R("res.wool");
            Honey = R("res.honey"); Herbs = R("res.herbs"); Clay = R("res.clay");
            JobBuilder = J("job.builder"); JobGatherer = J("job.gatherer"); JobFarmer = J("job.farmer"); JobLumberjack = J("job.lumberjack");
            JobMiner = J("job.miner"); JobFisher = J("job.fisher"); JobHunter = J("job.hunter"); JobHerder = J("job.herder");
            JobSmith = J("job.smith"); JobBaker = J("job.baker"); JobWarrior = J("job.warrior"); JobGuard = J("job.guard"); JobLeader = J("job.leader");
            HapNewHome = H("hap.new_home"); HapHungry = H("hap.hungry"); HapHomeless = H("hap.homeless"); HapFamilyDied = H("hap.family_died");
            HapChildBorn = H("hap.child_born"); HapAteWell = H("hap.ate_well");
            WheatCrop = db.Features.TryGet("feat.wheat_crop", out var wheat) ? wheat.MapValue : (ushort)0;
            int field = db.Tiles.IdOrDefault("tile.field");
            FieldTile = field >= 0 ? (byte)field : (byte)0;
            StyleHuman = db.BuildingStyles.IdOrDefault("style.human");
            StyleBeast = db.BuildingStyles.IdOrDefault("style.beast");
            StyleInsect = db.BuildingStyles.IdOrDefault("style.insect");
        }
    }

    // Bölüm 5: cities, buildings and items of one world. Systems (CivSystems.cs) and unit work (CivWork.cs) act on it.
    public sealed class CivState : IDisposable, IItemSource
    {
        public const int MaxZonesPerCity = 120;
        public const int FoundingRadius = 20, FoundingGroup = 6, NoCityRadius = 30;
        public const int BaseStorage = 60;             // DECISIONS #55: a bonfire village can store a little
        public const int StoragePerBuilding = 200, StoragePerTier = 100;
        public const int RuinYears = 5, AbandonYears = 2;
        public const int WoodTarget = 100; // wood kept in stock; beyond it storage is left for food (DECISIONS #55)

        public readonly List<City> Cities = new List<City>();
        public NativeList<BuildingData> Buildings;
        public NativeList<ItemData> Items;
        NativeList<int> _freeBuildings, _freeItems;
        public long NextUid = 1;
        public readonly CivIds Ids;
        public readonly ContentDB Content;
        public readonly List<string> Names = new List<string>();   // NamePool (Bölüm 6.2): NameId -> text
        readonly WorldMap _map;
        UnitWorld _units;

        public CivState(WorldMap map, ContentDB content)
        {
            _map = map;
            Content = content;
            Ids = new CivIds(content);
            Buildings = new NativeList<BuildingData>(256, Allocator.Persistent);
            Items = new NativeList<ItemData>(64, Allocator.Persistent);
            _freeBuildings = new NativeList<int>(32, Allocator.Persistent);
            _freeItems = new NativeList<int>(32, Allocator.Persistent);
        }

        public void Attach(UnitWorld units)
        {
            _units = units;
            units.Store.Items = this;
            units.Civ = this;
        }

        public WorldMap Map => _map;
        public UnitWorld Units => _units;
        public int AliveCities { get; private set; }

        public City CityOf(int unit) => _units.Store.City[unit] >= 0 ? Cities[_units.Store.City[unit]] : null;
        public BuildingDef DefOf(int building) => Content.Buildings[Buildings[building].Def];

        // ---------------- cities ----------------

        public City CreateCity(int species, int2 center, long tick, int year, ref SimRandom rng)
        {
            var sp = Content.Species[species];
            var city = new City
            {
                Index = Cities.Count,
                Uid = NextUid++,
                Species = (ushort)species,
                Center = center,
                FoundedYear = year,
                Stock = new int[Content.Resources.Count],
                JobQuota = new int[Content.Jobs.Count],
                JobCount = new int[Content.Jobs.Count],
                Style = (ushort)math.max(0, StyleFor(sp)),
                NextPlanTick = tick + rng.Range(0, 120),
            };
            city.Name = CityName(ref rng);
            float hue = rng.Value01();
            city.Color = Color.HSVToRGB(hue, 0.65f, 0.9f);
            Cities.Add(city);
            AliveCities++;
            return city;
        }

        int StyleFor(SpeciesDef sp)
        {
            int own = Content.BuildingStyles.IdOrDefault("style." + sp.Id.Substring(sp.Id.IndexOf('.') + 1));
            if (own >= 0) return own;
            return Ids.StyleBeast >= 0 ? Ids.StyleBeast : Ids.StyleHuman;
        }

        public void KillCity(City city, long tick, EventBus events)
        {
            if (city.Dead) return;
            city.Dead = true;
            city.DiedTick = tick;
            AliveCities--;
            foreach (int z in city.Zones) SetZoneOwner(z, -1);
            city.Zones.Clear();
            var u = _units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.City[i] != city.Index) continue;
                u.City[i] = -1;
                u.Job[i] = 0;
                u.HomeBuilding[i] = -1;
                u.WorkBuilding[i] = -1;
            }
            for (int b = city.Buildings.Count - 1; b >= 0; b--)
            {
                int bi = city.Buildings[b];
                if (Buildings[bi].State != BuildingState.Free) Destroy(bi, tick, events, true);
            }
            events?.Publish(new CityDiedEvent(city.Index));
        }

        // Name from the plain phonology set; languages (Bölüm 6.7) will pick their own set later.
        public string CityName(ref SimRandom rng)
        {
            var sets = Content.NameSets;
            if (sets.SetIds.Count == 0) return "City " + (Cities.Count + 1);
            var set = sets.Sets[sets.SetIds[0]];
            string word = NameGen.Word(set, ref rng);
            if (rng.Chance(0.3f)) word += NameGen.CitySuffixes[rng.Range(0, NameGen.CitySuffixes.Length)];
            return word;
        }

        // ---------------- zones ----------------

        public void ClaimZone(City city, int zone)
        {
            if (_map.Zones[zone].OwnerCity >= 0) return;
            SetZoneOwner(zone, city.Index);
            city.Zones.Add(zone);
        }

        public void ReleaseZone(City city, int zone)
        {
            if (_map.Zones[zone].OwnerCity != city.Index) return;
            SetZoneOwner(zone, -1);
            city.Zones.Remove(zone);
        }

        void SetZoneOwner(int zone, int owner)
        {
            var z = _map.Zones[zone];
            z.OwnerCity = owner;
            _map.Zones[zone] = z;
            int zx = zone % _map.ZonesX, zy = zone / _map.ZonesX;
            _map.MarkChunkDirty(_map.ChunkIndexOf(zx << WorldMap.ZoneShift, zy << WorldMap.ZoneShift), DirtyMask.Overlay | DirtyMask.Render | DirtyMask.Save);
        }

        public int ZoneOwner(int x, int y) => _map.InBounds(x, y) ? _map.Zones[_map.ZoneIndexOf(x, y)].OwnerCity : -1;

        // ---------------- buildings ----------------

        // Footprint fully inside the city's zones (or unowned for the first bonfire), on dry buildable ground, nothing built.
        public bool CanPlace(int def, int2 origin, int city)
        {
            var d = Content.Buildings[def];
            for (int y = origin.y; y < origin.y + d.H; y++)
                for (int x = origin.x; x < origin.x + d.W; x++)
                {
                    if (!_map.InBounds(x, y)) return false;
                    int i = _map.Index(x, y);
                    ushort f = _map.Flags[i];
                    if ((f & (ushort)TileFlags.Buildable) == 0 || (f & (ushort)(TileFlags.Water | TileFlags.Burning | TileFlags.Reserved)) != 0) return false;
                    if (_map.Building[i] >= 0) return false;
                    int owner = _map.Zones[_map.ZoneIndexOf(x, y)].OwnerCity;
                    if (owner != city) return false;
                }
            return true;
        }

        public int Place(int def, int2 origin, City city, long tick, bool complete = false)
        {
            var d = Content.Buildings[def];
            int index;
            if (_freeBuildings.Length > 0)
            {
                index = _freeBuildings[_freeBuildings.Length - 1];
                _freeBuildings.RemoveAt(_freeBuildings.Length - 1);
            }
            else
            {
                index = Buildings.Length;
                Buildings.Add(default);
            }
            var door = new int2(origin.x + d.W / 2, origin.y);
            Buildings[index] = new BuildingData
            {
                Uid = NextUid++,
                Def = (ushort)def,
                Origin = origin,
                W = (byte)d.W,
                H = (byte)d.H,
                Door = door,
                City = city != null ? city.Index : -1,
                MaxHp = d.Hp,
                Hp = complete ? d.Hp : math.max(1f, d.Hp * 0.1f),
                BuildProgress = complete ? 1f : 0f,
                State = complete ? BuildingState.Complete : BuildingState.Construction,
                Style = city != null ? city.Style : (ushort)0,
                StateTick = tick,
            };
            _map.PlaceBuilding(index, origin, d.W, d.H, door);
            city?.Buildings.Add(index);
            return index;
        }

        // Construction finished: full hp; roads; a new hall replaces the old centre.
        public void Complete(int b, long tick, EventBus events)
        {
            var data = Buildings[b];
            data.BuildProgress = 1f;
            data.State = BuildingState.Complete;
            data.Hp = data.MaxHp;
            data.StateTick = tick;
            Buildings[b] = data;
            var def = Content.Buildings[data.Def];
            if (data.City >= 0)
            {
                var city = Cities[data.City];
                if (def.IsCenter)
                {
                    int old = city.CenterBuilding;
                    city.CenterBuilding = b;
                    city.HallTier = (byte)math.max(0, Array.IndexOf(Ids.HallByTier, (int)data.Def));
                    city.Center = data.Door;
                    if (old >= 0 && old != b && Buildings[old].State != BuildingState.Free) Remove(old);
                }
                else if (city.CenterBuilding >= 0) DrawRoad(data.Door, Buildings[city.CenterBuilding].Door);
            }
            events?.Publish(new BuildingCompletedEvent(b, data.City, data.Def));
        }

        // Hp 0, terraform or a dead city: the footprint becomes ruins (Bölüm 5.4), residents lose their home.
        public void Destroy(int b, long tick, EventBus events, bool leaveRuins)
        {
            var data = Buildings[b];
            if (data.State == BuildingState.Free) return;
            ClearOccupants(b);
            int city = data.City;
            ushort def = data.Def;
            if (data.State == BuildingState.Ruin || !leaveRuins || Ids.Ruins < 0)
            {
                Remove(b);
            }
            else
            {
                data.State = BuildingState.Ruin;
                data.StateTick = tick;
                data.Hp = Content.Buildings[Ids.Ruins].Hp;
                data.Flags = BuildingFlags.None;
                Buildings[b] = data;
                _map.MarkChunkDirty(_map.ChunkIndexOf(data.Origin.x, data.Origin.y), DirtyMask.Render | DirtyMask.Save);
                if (city >= 0)
                {
                    var c = Cities[city];
                    c.Buildings.Remove(b);
                    if (c.CenterBuilding == b) c.CenterBuilding = -1;
                }
            }
            events?.Publish(new BuildingDestroyedEvent(b, city, def));
        }

        // Frees the slot and the footprint.
        public void Remove(int b)
        {
            var data = Buildings[b];
            if (data.State == BuildingState.Free) return;
            ClearOccupants(b);
            _map.RemoveBuilding(b, data.Origin, data.W, data.H);
            if (data.City >= 0 && data.City < Cities.Count)
            {
                var c = Cities[data.City];
                c.Buildings.Remove(b);
                if (c.CenterBuilding == b) c.CenterBuilding = -1;
            }
            data.State = BuildingState.Free;
            Buildings[b] = data;
            _freeBuildings.Add(b);
        }

        void ClearOccupants(int b)
        {
            if (_units == null) return;
            var u = _units.Store;
            for (int k = 0; k < u.Alive.Length; k++)
            {
                int i = u.Alive[k];
                if (u.HomeBuilding[i] == b) u.HomeBuilding[i] = -1;
                if (u.WorkBuilding[i] == b) u.WorkBuilding[i] = -1;
            }
        }

        // Bölüm 5.4 roads: path from the door to the centre; road tiles speed walkers up (x1.3).
        void DrawRoad(int2 from, int2 to)
        {
            if (_units == null) return;
            var status = _units.Paths.Request(from, to, Mobility.Land, out int handle, countBudget: false);
            if (status != PathStatus.Ready) return;
            int n = _units.Paths.Length(handle);
            for (int k = 0; k < n; k++)
            {
                int2 p = _units.Paths.Point(handle, k);
                if (_map.Building[_map.Index(p.x, p.y)] >= 0) continue;
                _map.SetFlag(p.x, p.y, TileFlags.Road, true);
            }
            _units.Paths.Release(handle);
        }

        public int CountBuildings(City city, int def, bool includeConstruction = true)
        {
            int n = 0;
            foreach (int b in city.Buildings)
            {
                var data = Buildings[b];
                if (data.Def != def) continue;
                if (data.State == BuildingState.Complete || (includeConstruction && data.State == BuildingState.Construction)) n++;
            }
            return n;
        }

        public bool HasComplete(City city, int def) => def >= 0 && CountBuildings(city, def, false) > 0;

        // Nearest completed building of the city that accepts deliveries (storage, granary, centre).
        public int NearestStore(City city, float2 from)
        {
            int best = -1;
            float bestD = float.MaxValue;
            foreach (int b in city.Buildings)
            {
                var data = Buildings[b];
                bool center = Content.Buildings[data.Def].IsCenter;
                if (data.State != BuildingState.Complete && !(center && data.State == BuildingState.Construction)) continue;
                if (data.Def != Ids.Storage && data.Def != Ids.Granary && !center) continue;
                float d = math.distancesq(from, data.Door);
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        // ---------------- stock ----------------

        public int StockTotal(City city)
        {
            int n = 0;
            for (int r = 0; r < city.Stock.Length; r++) n += city.Stock[r];
            return n;
        }

        // Adds up to the storage capacity; returns what was stored. Materials may fill at most 60% so there is always room
        // for food (DECISIONS #55).
        public const float MaterialShare = 0.6f;

        public int AddStock(City city, int res, int amount)
        {
            if (res < 0 || amount <= 0) return 0;
            int total = StockTotal(city);
            int room = math.max(0, city.StorageCapacity - total);
            if (!Content.Resources[res].IsFood)
            {
                int materials = 0;
                for (int r = 0; r < city.Stock.Length; r++) if (!Content.Resources[r].IsFood) materials += city.Stock[r];
                room = math.min(room, math.max(0, (int)(city.StorageCapacity * MaterialShare) - materials));
            }
            int stored = math.min(room, amount);
            city.Stock[res] += stored;
            return stored;
        }

        public bool CanAfford(City city, BuildingDef def)
        {
            for (int k = 0; k < def.CostRes.Length; k++)
                if (city.Stock[def.CostRes[k]] < def.CostAmount[k]) return false;
            return true;
        }

        public void Pay(City city, BuildingDef def)
        {
            for (int k = 0; k < def.CostRes.Length; k++) city.Stock[def.CostRes[k]] -= def.CostAmount[k];
        }

        // ---------------- items (Bölüm 5.7) ----------------

        public int CreateItem(in ItemData item)
        {
            int index;
            if (_freeItems.Length > 0)
            {
                index = _freeItems[_freeItems.Length - 1];
                _freeItems.RemoveAt(_freeItems.Length - 1);
            }
            else
            {
                index = Items.Length;
                Items.Add(default);
            }
            var data = item;
            data.Uid = NextUid++;
            data.Alive = 1;
            Items[index] = data;
            return index;
        }

        public void DestroyItem(int item)
        {
            var data = Items[item];
            if (data.Alive == 0) return;
            data.Alive = 0;
            Items[item] = data;
            _freeItems.Add(item);
        }

        public bool TryGetEffect(int item, out EquipmentTypeDef type, out float multiplier)
        {
            type = null;
            multiplier = 0f;
            if (item < 0 || item >= Items.Length || Items[item].Alive == 0) return false;
            var data = Items[item];
            type = Content.EquipmentTypes[data.Type];
            multiplier = Content.Materials[data.Material].Multiplier * Content.ItemQualities[data.Quality].Multiplier;
            return true;
        }

        // ---------------- save / load ----------------

        public void Write(System.IO.BinaryWriter w)
        {
            w.Write(NextUid);
            w.Write(Cities.Count);
            foreach (var c in Cities)
            {
                w.Write(c.Uid); w.Write(c.Name ?? ""); w.Write(c.Species); w.Write(c.Kingdom); w.Write(c.Center.x); w.Write(c.Center.y);
                w.Write(c.CenterBuilding); w.Write(c.HallTier); w.Write(c.LeaderUid); WriteInts(w, c.Zones); WriteInts(w, c.Buildings);
                w.Write(c.Stock.Length); foreach (int s in c.Stock) w.Write(s);
                w.Write(c.Gold); w.Write(c.Happiness); w.Write(c.FoundedYear); w.Write(c.Style);
                w.Write(c.Color.r); w.Write(c.Color.g); w.Write(c.Color.b); w.Write(c.Color.a);
                w.Write(c.NextPlanTick); w.Write(c.FamineMonths); w.Write(c.Dead); w.Write(c.DiedTick); w.Write(c.EmptySinceTick);
            }
            WriteList(w, Buildings);
            WriteList(w, _freeBuildings);
            WriteList(w, Items);
            WriteList(w, _freeItems);
            w.Write(Names.Count);
            foreach (var n in Names) w.Write(n);
        }

        // resMap: saved resource index -> current index (-1 removed); buildingMap / typeMap / materialMap / qualityMap likewise.
        public void Read(System.IO.BinaryReader r, int[] resMap, int[] buildingMap, int[] styleMap, int[] typeMap, int[] materialMap, int[] qualityMap)
        {
            Cities.Clear();
            AliveCities = 0;
            NextUid = r.ReadInt64();
            int count = r.ReadInt32();
            for (int k = 0; k < count; k++)
            {
                var c = new City { Index = k };
                c.Uid = r.ReadInt64(); c.Name = r.ReadString(); c.Species = r.ReadUInt16(); c.Kingdom = r.ReadInt32();
                c.Center = new int2(r.ReadInt32(), r.ReadInt32()); c.CenterBuilding = r.ReadInt32(); c.HallTier = r.ReadByte();
                c.LeaderUid = r.ReadInt64(); ReadInts(r, c.Zones); ReadInts(r, c.Buildings);
                c.Stock = new int[Content.Resources.Count];
                int n = r.ReadInt32();
                for (int s = 0; s < n; s++)
                {
                    int v = r.ReadInt32();
                    int to = s < resMap.Length ? resMap[s] : -1;
                    if (to >= 0) c.Stock[to] += v;
                }
                c.Gold = r.ReadInt32(); c.Happiness = r.ReadInt16(); c.FoundedYear = r.ReadInt32();
                int style = r.ReadUInt16(); c.Style = (ushort)math.max(0, style < styleMap.Length ? styleMap[style] : 0);
                c.Color = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                c.NextPlanTick = r.ReadInt64(); c.FamineMonths = r.ReadInt32(); c.Dead = r.ReadBoolean(); c.DiedTick = r.ReadInt64();
                c.EmptySinceTick = r.ReadInt64();
                c.JobQuota = new int[Content.Jobs.Count];
                c.JobCount = new int[Content.Jobs.Count];
                Cities.Add(c);
                if (!c.Dead) AliveCities++;
            }
            ReadList(r, Buildings);
            ReadList(r, _freeBuildings);
            ReadList(r, Items);
            ReadList(r, _freeItems);
            Names.Clear();
            int names = r.ReadInt32();
            for (int k = 0; k < names; k++) Names.Add(r.ReadString());

            for (int b = 0; b < Buildings.Length; b++)
            {
                var data = Buildings[b];
                if (data.State == BuildingState.Free) continue;
                int def = data.Def < buildingMap.Length ? buildingMap[data.Def] : -1;
                data.Def = (ushort)math.max(0, def < 0 ? Ids.Ruins : def);
                data.Style = (ushort)math.max(0, data.Style < styleMap.Length ? styleMap[data.Style] : 0);
                Buildings[b] = data;
                _map.PlaceBuilding(b, data.Origin, data.W, data.H, data.Door);
            }
            for (int it = 0; it < Items.Length; it++)
            {
                var data = Items[it];
                if (data.Alive == 0) continue;
                data.Type = (ushort)math.max(0, data.Type < typeMap.Length ? typeMap[data.Type] : 0);
                data.Material = (ushort)math.max(0, data.Material < materialMap.Length ? materialMap[data.Material] : 0);
                data.Quality = (ushort)math.max(0, data.Quality < qualityMap.Length ? qualityMap[data.Quality] : 0);
                Items[it] = data;
            }
            foreach (var c in Cities)
                foreach (int z in c.Zones) SetZoneOwner(z, c.Index);
        }

        static void WriteInts(System.IO.BinaryWriter w, List<int> list)
        {
            w.Write(list.Count);
            foreach (int v in list) w.Write(v);
        }

        static void ReadInts(System.IO.BinaryReader r, List<int> list)
        {
            list.Clear();
            int n = r.ReadInt32();
            for (int k = 0; k < n; k++) list.Add(r.ReadInt32());
        }

        static void WriteList<T>(System.IO.BinaryWriter w, NativeList<T> list) where T : unmanaged
        {
            w.Write(list.Length);
            w.Write(System.Runtime.InteropServices.MemoryMarshal.AsBytes(list.AsArray().AsReadOnlySpan()));
        }

        static void ReadList<T>(System.IO.BinaryReader r, NativeList<T> list) where T : unmanaged
        {
            int n = r.ReadInt32();
            list.Resize(n, NativeArrayOptions.ClearMemory);
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(list.AsArray().AsSpan());
            if (r.Read(bytes) != bytes.Length) throw new System.IO.EndOfStreamException("Truncated civ list");
        }

        public void Dispose()
        {
            if (Buildings.IsCreated) Buildings.Dispose();
            if (Items.IsCreated) Items.Dispose();
            if (_freeBuildings.IsCreated) _freeBuildings.Dispose();
            if (_freeItems.IsCreated) _freeItems.Dispose();
        }
    }

    // Bölüm 6.2 word generator (languages pick their own phonology set in Bölüm 6.7).
    public static class NameGen
    {
        public static readonly string[] CitySuffixes = { "ia", "heim", "gar", "ova" };

        public static string Word(NameSets.Set set, ref SimRandom rng)
        {
            if (set.Pattern.Length == 0 || set.Onset.Length == 0 || set.Vowel.Length == 0) return "Anon";
            string pattern = set.Pattern[rng.Range(0, set.Pattern.Length)];
            var sb = new System.Text.StringBuilder(12);
            foreach (char ch in pattern)
            {
                if (ch == 'C') sb.Append(set.Onset[rng.Range(0, set.Onset.Length)]);
                else if (ch == 'V') sb.Append(set.Vowel[rng.Range(0, set.Vowel.Length)]);
            }
            if (set.Coda.Length > 0 && rng.Chance(0.4f)) sb.Append(set.Coda[rng.Range(0, set.Coda.Length)]);
            if (sb.Length == 0) return "Anon";
            sb[0] = char.ToUpperInvariant(sb[0]);
            return sb.ToString();
        }
    }
}
