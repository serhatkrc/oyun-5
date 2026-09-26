using System;
using System.Collections.Generic;
using PG.Core;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Sim
{
    public enum BuildingState : byte { Free, Construction, Complete, Ruin }

    [Flags]
    public enum BuildingFlags : byte { None = 0, Burning = 1, Abandoned = 2 }

    // Bölüm 5.4. Stored in BuildingStore by index; the index is written into WorldMap.Building for the footprint.
    public struct BuildingData
    {
        public long Uid;
        public ushort Def;
        public int2 Origin;        // bottom-left tile
        public byte W, H;          // tiles
        public int2 Door;          // walkable tile of the footprint (bottom middle)
        public int City;           // -1 = none (ruins keep their last city for history)
        public float Hp, MaxHp;
        public float BuildProgress; // 0-1, 1 = done
        public BuildingState State;
        public BuildingFlags Flags;
        public ushort Style;
        public short Residents;
        public long StateTick;     // tick of the last state change (ruins fade after 5 years)
    }

    // Bölüm 5.7
    public struct ItemData
    {
        public long Uid;
        public ushort Type, Material, Quality;
        public int NameId;         // -1 = no name (common items)
        public int Kills;
        public long CreatedTick;
        public long MakerUid;
        public int Owner;          // unit index, -1 = in a city stock / on the ground
        public int City;           // stock city while unowned, -1 = on the ground
        public int2 Tile;          // on the ground
        public byte Alive;
    }

    // Bölüm 5.1: cities are few (tens to hundreds); a managed class keeps the code simple. Saved field by field (CivSave).
    public sealed class City
    {
        public int Index;
        public long Uid;
        public string Name;
        public ushort Species;
        public int Kingdom = -1;           // Bölüm 6
        public int2 Center;
        public int CenterBuilding = -1;
        public byte HallTier;              // 0 bonfire, 1-3 halls
        public long LeaderUid;
        public readonly List<int> Zones = new List<int>();
        public readonly List<int> Buildings = new List<int>();
        public readonly List<int> Residents = new List<int>(); // unit indices, rebuilt monthly
        public int[] Stock;                // by resource index
        public int Gold;
        public short Happiness;
        public int FoundedYear;
        public ushort Style;
        public Color32 Color;
        public long NextPlanTick;
        public int FamineMonths;
        public bool Dead;
        public long DiedTick;
        public long EmptySinceTick = -1;
        // monthly caches
        public int Population;
        public int HousingCapacity;
        public int StorageCapacity;
        public int[] JobQuota;              // by JobDef index
        public int[] JobCount;
        public int FoodTotal;               // nutrition units in stock
        public int ActiveConstructions;

        public int StockOf(int res) => res >= 0 && res < Stock.Length ? Stock[res] : 0;
    }

    public readonly struct CityFoundedEvent : ISimEvent
    {
        public readonly int City;
        public readonly long FounderUid;
        public CityFoundedEvent(int city, long founderUid) { City = city; FounderUid = founderUid; }
    }

    public readonly struct CityDiedEvent : ISimEvent
    {
        public readonly int City;
        public CityDiedEvent(int city) { City = city; }
    }

    public readonly struct BuildingCompletedEvent : ISimEvent
    {
        public readonly int Building, City;
        public readonly ushort Def;
        public BuildingCompletedEvent(int building, int city, ushort def) { Building = building; City = city; Def = def; }
    }

    public readonly struct BuildingDestroyedEvent : ISimEvent
    {
        public readonly int Building, City;
        public readonly ushort Def;
        public BuildingDestroyedEvent(int building, int city, ushort def) { Building = building; City = city; Def = def; }
    }

    public readonly struct FamineEvent : ISimEvent
    {
        public readonly int City;
        public FamineEvent(int city) { City = city; }
    }

    public readonly struct ItemCraftedEvent : ISimEvent
    {
        public readonly int Item, City;
        public readonly ushort Quality;
        public ItemCraftedEvent(int item, int city, ushort quality) { Item = item; City = city; Quality = quality; }
    }
}
