using System;
using System.Collections.Generic;
using PG.Core;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Sim
{
    // Bölüm 6.3: drawn later from shape/icon/colour/pattern indices (Bölüm 8 banner atlas).
    public struct Banner
    {
        public byte Shape, Icon, BgColor, FgColor, Pattern;
        public const int Shapes = 12, Icons = 64, Colors = 16, Patterns = 6;

        public static Banner Random(ref SimRandom rng)
        {
            var b = new Banner
            {
                Shape = (byte)rng.Range(0, Shapes),
                Icon = (byte)rng.Range(0, Icons),
                BgColor = (byte)rng.Range(0, Colors),
                Pattern = (byte)rng.Range(0, Patterns),
            };
            b.FgColor = (byte)((b.BgColor + rng.Range(3, Colors - 2)) % Colors); // never the background colour
            return b;
        }

        // Rebels keep the shape of their old banner with new colours (Bölüm 6.3).
        public Banner Recolor(ref SimRandom rng)
        {
            var b = this;
            b.BgColor = (byte)((BgColor + rng.Range(1, Colors)) % Colors);
            b.FgColor = (byte)((b.BgColor + rng.Range(3, Colors - 2)) % Colors);
            return b;
        }
    }

    [Flags]
    public enum MetaFlags : byte { None = 0, Favorite = 1, Edited = 2, Dead = 4, PlayerControlled = 8 }

    // Bölüm 6.4. Few per world; a managed class like City.
    public sealed class Kingdom
    {
        public int Index;
        public long Uid;
        public string Name;
        public string Root;               // name root (titles change with the city count)
        public Color32 Color;
        public Banner Banner;
        public ushort Species;
        public long FoundedTick, DiedTick;
        public long FounderUid;
        public MetaFlags Flags;
        public long KingUid, HeirUid;
        public int Capital = -1;
        public readonly List<int> Cities = new List<int>();
        public int Alliance = -1;
        public int RulerTitle;
        public int Treasury;
        public long LastWarEndTick = long.MinValue / 2;
        public long SuccessionTick = long.MinValue / 2; // loyalty -10 for a year after a new king
        public readonly List<short> Opinion = new List<short>(); // by kingdom index, -100..100
        public int[] Traits = Array.Empty<int>();                 // KingdomTraitDef indices
        public int KingChanges;
        public long NextArmyTick;
        public readonly List<short> Trade = new List<short>();   // caravans received from each kingdom this year (Bölüm 5.9)
        // monthly caches (derived, not saved)
        public int Population, Soldiers, KingIndex = -1;
        public float Power;

        public bool Dead => (Flags & MetaFlags.Dead) != 0;

        public short OpinionOf(int other) => other < Opinion.Count ? Opinion[other] : (short)0;

        public void SetOpinion(int other, int value)
        {
            while (Opinion.Count <= other) Opinion.Add(0);
            Opinion[other] = (short)math.clamp(value, -100, 100);
        }
    }

    // Bölüm 5.9: one merchant carrying surplus to a market city.
    public sealed class Caravan
    {
        public long UnitUid;
        public int Unit, From, To;
        public long StartTick;
    }

    public enum BoatType : byte { Fishing, Trade, Transport, War }
    public enum BoatState : byte { Sailing, Done, Sunk }

    // Bölüm 5.10. Only transports sail for now (colonisation); the other types come with the ship UI (DECISIONS #64).
    public sealed class Boat
    {
        public int Index;
        public BoatType Type;
        public BoatState State;
        public float2 Pos;
        public int City, Kingdom;
        public float Hp;
        public readonly List<int> Passengers = new List<int>();
        public readonly List<long> PassengerUids = new List<long>();
        public int2 Destination;   // water tile next to the landing site
        public int2 Landing;       // land tile where the colony is founded
        public int PathHandle = -1, PathStep;
        public long LaunchTick;
    }

    public enum WarResult : byte { Ongoing, Peace, AttackerWon, DefenderWon, Vanished }

    // Bölüm 6.5
    public sealed class War
    {
        public int Index;
        public long Uid;
        public ushort Type;                  // war_types.json
        public int Attacker, Defender;
        public readonly List<int> AttackerSide = new List<int>(), DefenderSide = new List<int>();
        public long StartTick, EndTick;
        public int CasualtiesA, CasualtiesD;
        public readonly List<int> CapturedCities = new List<int>();
        public int TargetCity = -1;
        public WarResult Result;

        public bool Active => Result == WarResult.Ongoing;
        public long Duration(long now) => (Active ? now : EndTick) - StartTick;

        public bool OnAttackerSide(int k) => AttackerSide.Contains(k);
        public bool OnDefenderSide(int k) => DefenderSide.Contains(k);
    }

    public enum ArmyState : byte { Gathering, Marching, Sieging, Returning, Disbanded }

    public sealed class Army
    {
        public int Index;
        public int Kingdom, War, HomeCity;
        public long CaptainUid;
        public readonly List<int> Soldiers = new List<int>(); // unit indices (validated each update by uid)
        public readonly List<long> SoldierUids = new List<long>();
        public int2 RallyTile;
        public int TargetCity = -1;
        public ArmyState State;
        public long StateTick;
        public int StartSize;
    }

    public sealed class Alliance
    {
        public int Index;
        public long Uid;
        public string Name;
        public readonly List<int> Kingdoms = new List<int>();
        public int Leader = -1;
        public bool Dead;
    }

    public readonly struct KingdomFoundedEvent : ISimEvent
    {
        public readonly int Kingdom, City;
        public KingdomFoundedEvent(int kingdom, int city) { Kingdom = kingdom; City = city; }
    }

    public readonly struct KingChangedEvent : ISimEvent
    {
        public readonly int Kingdom;
        public readonly long OldKing, NewKing;
        public KingChangedEvent(int kingdom, long oldKing, long newKing) { Kingdom = kingdom; OldKing = oldKing; NewKing = newKing; }
    }

    public readonly struct RebellionEvent : ISimEvent
    {
        public readonly int City, OldKingdom, NewKingdom;
        public RebellionEvent(int city, int oldKingdom, int newKingdom) { City = city; OldKingdom = oldKingdom; NewKingdom = newKingdom; }
    }

    public readonly struct WarDeclaredEvent : ISimEvent
    {
        public readonly int War, Attacker, Defender;
        public WarDeclaredEvent(int war, int attacker, int defender) { War = war; Attacker = attacker; Defender = defender; }
    }

    public readonly struct WarEndedEvent : ISimEvent
    {
        public readonly int War;
        public readonly WarResult Result;
        public WarEndedEvent(int war, WarResult result) { War = war; Result = result; }
    }

    public readonly struct CityCapturedEvent : ISimEvent
    {
        public readonly int City, From, To;
        public CityCapturedEvent(int city, int from, int to) { City = city; From = from; To = to; }
    }

    public readonly struct KingdomFellEvent : ISimEvent
    {
        public readonly int Kingdom;
        public KingdomFellEvent(int kingdom) { Kingdom = kingdom; }
    }
}
