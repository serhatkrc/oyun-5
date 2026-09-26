using System;
using System.Collections.Generic;
using PG.Core;
using PG.Sim;
using PG.World;

namespace PG.Persistence
{
    public static class SaveFormat
    {
        public const string Magic = "PXGN";
        public const int FormatVersion = 1;

        public const int SectionContentMap = 1;
        public const int SectionClock = 2;
        public const int SectionRng = 3;
        public const int SectionWorldTiles = 4;
        public const int SectionWorldZones = 5;
        // Optional sections (Bölüm 2): missing in older files -> defaults, so FormatVersion stays 1.
        public const int SectionFeatureState = 6;
        public const int SectionZoneClimate = 7;
        public const int SectionNature = 8;
        public const int SectionLaws = 9;
        public const int SectionUnits = 10; // Bölüm 3
        public const int SectionCiv = 11;   // Bölüm 5: cities, buildings, items
        public const int SectionMeta = 12;  // Bölüm 6: kingdoms, wars, armies, alliances
        // Later chapters: History, Stats
    }

    public sealed class SaveSlotInfo
    {
        public int Slot;
        public bool Exists;
        public bool IsAuto;
        public int FormatVersion;
        public string GameVersion;
        public DateTime CreatedUtc;
        public string WorldName;
        public int Year;
        public int Population;
        public int SizeX, SizeY;
        public byte[] ThumbnailPng;
    }

    // Result of a load; the game session turns it into a SimWorld.
    public sealed class GameState
    {
        public string WorldName;
        public GameClock Clock;
        public SimRandomProvider Rng;
        public WorldMap World;
        public readonly List<string> Warnings = new List<string>();
        internal byte[] NatureRaw, LawsRaw, UnitsRaw, CivRaw, MetaRaw;

        // Call once the SimWorld exists: restores nature state and world laws.
        public void ApplyTo(SimWorld sim)
        {
            NatureSave.ApplyLaws(LawsRaw, sim);
            NatureSave.ApplyNature(NatureRaw, sim, Warnings);
            UnitSave.Apply(UnitsRaw, sim, Warnings);
            CivSave.Apply(CivRaw, sim, Warnings);
            MetaSave.Apply(MetaRaw, sim, Warnings);
        }
    }

    // Parsed file for migrations: header fields + raw (decompressed) sections.
    public sealed class SaveDocument
    {
        public int FormatVersion;
        public string GameVersion;
        public long CreatedUtc;
        public string WorldName;
        public int Year, Population, SizeX, SizeY;
        public byte[] ThumbnailPng;
        public readonly Dictionary<int, byte[]> Sections = new Dictionary<int, byte[]>();
    }

    public interface ISaveMigration
    {
        int FromVersion { get; }
        void Migrate(SaveDocument doc);
    }
}
