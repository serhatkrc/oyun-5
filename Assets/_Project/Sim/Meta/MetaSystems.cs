using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using PG.World;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 6.4 monthly: kingdom membership, capitals, succession, loyalty and rebellions, opinion, alliances, titles.
    public sealed class KingdomSystem : ISimSystem
    {
        public const int MonthOffset = 20;              // after the civ monthly update (tick % 60 == 0)
        public const float LoyaltyRate = 0.1f, OpinionRate = 0.05f, RebellionChance = 0.05f;
        public const int WarMemoryYears = 50;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 10;

        bool[] _border = Array.Empty<bool>();
        int _borderSize;

        public void Tick(in SimContext ctx)
        {
            if (ctx.Clock.Tick % SimConst.TicksPerMonth != MonthOffset) return;
            var meta = ctx.Units.Meta;
            if (meta == null) return;
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            Monthly(ctx, meta, ref rng);
        }

        public void Monthly(in SimContext ctx, MetaState meta, ref SimRandom rng)
        {
            long tick = ctx.Clock.Tick;
            AssignCities(ctx, meta, ref rng);
            foreach (var k in meta.Kingdoms)
            {
                if (k.Dead) continue;
                if (k.Cities.Count == 0) { meta.KillKingdom(k, tick, ctx.Events); continue; }
                if (k.Capital < 0 || meta.Civ.Cities[k.Capital].Dead || meta.Civ.Cities[k.Capital].Kingdom != k.Index) meta.PickCapital(k);
                Succession(ctx, meta, k);
                Caches(meta, k);
                int title = k.RulerTitle;
                meta.Rename(k);
                if (title != k.RulerTitle) meta.Civ.MarkCityDirty(meta.Civ.Cities[k.Capital]);
            }
            BuildBorders(meta);
            var era = ctx.Nature.CurrentEra;
            // loyalty; rebellions after the loop so the kingdom list is not changed while walking it
            var civ = meta.Civ;
            for (int c = 0; c < civ.Cities.Count; c++)
            {
                var city = civ.Cities[c];
                if (city.Dead || city.Kingdom < 0) continue;
                int target = LoyaltyTarget(meta, city, era.LoyaltyBonus, tick);
                city.Loyalty = Converge(city.Loyalty, target, LoyaltyRate);
                var k = meta.Kingdoms[city.Kingdom];
                if (city.Loyalty >= 0 || k.Capital == city.Index || (k.Flags & MetaFlags.PlayerControlled) != 0) continue;
                if (!meta.Laws.IsOn(meta.LawRebellions)) continue;
                float chance = RebellionChance * (1f - meta.TraitPct(k, "rebellionResist") / 100f);
                if (rng.Chance(chance)) Rebel(ctx, meta, city, ref rng);
            }
            Opinions(meta, era.OpinionBonus, tick);
            Alliances(meta, ref rng);
        }

        // ---------------- membership ----------------

        // Bölüm 6.4 "Kuruluş": a city without a kingdom joins the one its settlers came from, otherwise founds its own.
        static void AssignCities(in SimContext ctx, MetaState meta, ref SimRandom rng)
        {
            var civ = meta.Civ;
            var u = ctx.Units.Store;
            for (int c = 0; c < civ.Cities.Count; c++)
            {
                var city = civ.Cities[c];
                if (city.Dead)
                {
                    if (city.Kingdom >= 0) meta.MoveCity(city, -1);
                    continue;
                }
                if (city.Kingdom >= 0) continue;
                int origin = -1;
                foreach (int i in city.Residents)
                {
                    int o = u.Origin[i];
                    if (o >= 0 && o < meta.Kingdoms.Count && !meta.Kingdoms[o].Dead && meta.Kingdoms[o].Species == city.Species) { origin = o; break; }
                }
                foreach (int i in city.Residents) u.Origin[i] = -1;
                if (origin >= 0)
                {
                    meta.MoveCity(city, origin);
                    city.Loyalty = 60;
                }
                else
                {
                    var k = meta.CreateKingdom(city, city.LeaderUid, ctx.Clock.Tick, ref rng);
                    city.Loyalty = 100;
                    ctx.Events.Publish(new KingdomFoundedEvent(k.Index, city.Index));
                }
            }
        }

        // ---------------- succession (Bölüm 6.4 "Kral ve veraset") ----------------

        static void Succession(in SimContext ctx, MetaState meta, Kingdom k)
        {
            var u = ctx.Units.Store;
            int king = meta.FindInKingdom(k, k.KingUid);
            if (king >= 0)
            {
                k.KingIndex = king;
                int heir = Heir(meta, k, king);
                k.HeirUid = heir >= 0 ? u.Uid[heir] : 0;
                return;
            }
            // the king is gone: heir by primogeniture (the default succ_* rule until cultures exist, Bölüm 6.7),
            // otherwise the capital's leader, otherwise the oldest adult of the capital
            int next = meta.FindInKingdom(k, k.HeirUid);
            if (next < 0 && k.Capital >= 0) next = CivMonthlySystem.LeaderIndex(ctx.Units, meta.Civ.Cities[k.Capital]);
            if (next < 0 && k.Capital >= 0) next = Eldest(u, meta.Civ.Cities[k.Capital]);
            long old = k.KingUid;
            k.KingUid = next >= 0 ? u.Uid[next] : 0;
            k.KingIndex = next;
            k.HeirUid = 0;
            if (old == k.KingUid) return;
            k.KingChanges++;
            k.SuccessionTick = ctx.Clock.Tick;
            int hapKingDied = meta.Content.HappinessEvents.IdOrDefault("hap.king_died");
            if (old != 0)
                foreach (int c in k.Cities)
                    foreach (int i in meta.Civ.Cities[c].Residents) Happy.Add(u, i, hapKingDied, meta.Content);
            ctx.Events.Publish(new KingChangedEvent(k.Index, old, k.KingUid));
        }

        // Eldest living child of the king inside the kingdom.
        public static int Heir(MetaState meta, Kingdom k, int king)
        {
            var u = meta.Units.Store;
            var id = u.IdOf(king);
            int best = -1;
            long bestBirth = long.MaxValue;
            foreach (int c in k.Cities)
                foreach (int i in meta.Civ.Cities[c].Residents)
                {
                    if (u.Mother[i] != id && u.Father[i] != id) continue;
                    if (u.BirthTick[i] < bestBirth) { bestBirth = u.BirthTick[i]; best = i; }
                }
            return best;
        }

        static int Eldest(UnitStore u, City city)
        {
            int best = -1;
            long bestBirth = long.MaxValue;
            foreach (int i in city.Residents)
            {
                var stage = (AgeStage)u.Age[i];
                if (stage != AgeStage.Adult && stage != AgeStage.Elder) continue;
                if (u.BirthTick[i] < bestBirth) { bestBirth = u.BirthTick[i]; best = i; }
            }
            return best;
        }

        // Bölüm 6.5 "Güç = asker sayısı × ortalama seviye × ekipman çarpanı + kule sayısı × 5"; a tenth of the people count too so
        // a kingdom without barracks is not powerless (DECISIONS #63).
        static void Caches(MetaState meta, Kingdom k)
        {
            var civ = meta.Civ;
            var u = meta.Units.Store;
            var ids = civ.Ids;
            int pop = 0, soldiers = 0, towers = 0;
            float levels = 0f;
            foreach (int c in k.Cities)
            {
                var city = civ.Cities[c];
                pop += city.Population;
                towers += civ.CountBuildings(city, ids.Watchtower, false);
                foreach (int i in city.Residents)
                {
                    int job = u.Job[i] - 1;
                    if (job != ids.JobWarrior && job != ids.JobGuard) continue;
                    soldiers++;
                    levels += u.Level[i] + (u.Equip[i * UnitStore.EquipSlots] >= 0 ? 1f : 0f);
                }
            }
            k.Population = pop;
            k.Soldiers = soldiers;
            k.Power = levels + towers * 5f + pop * 0.1f;
        }

        // ---------------- loyalty (Bölüm 6.4 "Şehir sadakati") ----------------

        public struct LoyaltyInputs
        {
            public float KingDiplo, DistanceTiles, Happiness, WarYears;
            public bool HasKing, KingLoyal, KingTreacherous, IsCapital, SameCulture, SameReligion, SameSpecies, NewKing, LeaderIsRelative;
            public int EraBonus;
        }

        // The Bölüm 6.4 formula; distance counts in zones (tiles / 8).
        public static int LoyaltyFormula(in LoyaltyInputs x)
        {
            float v = 50f;
            if (x.HasKing)
            {
                v += x.KingDiplo * 3f;
                if (x.KingLoyal) v += 15f;
                if (x.KingTreacherous) v -= 15f;
            }
            if (x.IsCapital) v += 100f;
            else v -= x.DistanceTiles / WorldMap.ZoneSize;
            v += x.SameCulture ? 10f : -15f;
            v += x.SameReligion ? 10f : -10f;
            v += x.SameSpecies ? 5f : -10f;
            v += x.Happiness / 5f;
            v -= math.min(20f, 3f * x.WarYears);
            v += x.EraBonus;
            if (x.NewKing) v -= 10f;
            if (x.HasKing && x.LeaderIsRelative) v += 10f;
            return (int)math.round(math.clamp(v, -200f, 300f));
        }

        public static int LoyaltyTarget(MetaState meta, City city, int eraBonus, long tick)
        {
            var k = meta.Kingdoms[city.Kingdom];
            var u = meta.Units.Store;
            int king = k.KingIndex >= 0 && u.IsAlive(k.KingIndex) && u.Uid[k.KingIndex] == k.KingUid ? k.KingIndex : -1;
            // Cultures and religions arrive in Faz 7; until then each species counts as one culture and one faith (DECISIONS #63).
            bool same = city.Species == k.Species;
            var x = new LoyaltyInputs
            {
                HasKing = king >= 0,
                KingDiplo = king >= 0 ? u.Stat(king, StatId.Diplo) : 0f,
                KingLoyal = king >= 0 && HasTrait(meta, king, meta.TrLoyal),
                KingTreacherous = king >= 0 && HasTrait(meta, king, meta.TrTreacherous),
                IsCapital = k.Capital == city.Index,
                DistanceTiles = k.Capital >= 0 ? math.distance((float2)city.Center, (float2)meta.Civ.Cities[k.Capital].Center) : 0f,
                SameCulture = same, SameReligion = same, SameSpecies = same,
                Happiness = city.Happiness,
                WarYears = meta.WarYears(k.Index, tick),
                EraBonus = eraBonus,
                NewKing = tick - k.SuccessionTick < SimConst.TicksPerYear,
                LeaderIsRelative = king >= 0 && city.LeaderUid != 0 && IsRelative(meta, k, king, city.LeaderUid),
            };
            return LoyaltyFormula(x);
        }

        static bool HasTrait(MetaState meta, int unit, int trait) => trait >= 0 && meta.Units.Store.Traits[unit].Has(trait);

        static bool IsRelative(MetaState meta, Kingdom k, int king, long uid)
        {
            var u = meta.Units.Store;
            if (u.Uid[king] == uid) return true;
            var id = u.IdOf(king);
            bool Rel(EntityId e) => u.IsAlive(e) && u.Uid[e.Index] == uid;
            if (Rel(u.Mother[king]) || Rel(u.Father[king]) || Rel(u.Mate[king])) return true;
            int other = meta.FindInKingdom(k, uid);
            return other >= 0 && (u.Mother[other] == id || u.Father[other] == id);
        }

        public static int Converge(int value, int target, float rate)
        {
            if (value == target) return value;
            int step = (int)math.round((target - value) * rate);
            if (step == 0) step = target > value ? 1 : -1;
            return value + step;
        }

        // Bölüm 6.4: the city breaks away as a new kingdom (banner recoloured) and its old kingdom goes to war over it.
        public static Kingdom Rebel(in SimContext ctx, MetaState meta, City city, ref SimRandom rng)
        {
            var parent = meta.Kingdoms[city.Kingdom];
            long tick = ctx.Clock.Tick;
            var rebel = meta.CreateKingdom(city, city.LeaderUid, tick, ref rng, parent);
            city.Loyalty = 60;
            ctx.Events.Publish(new RebellionEvent(city.Index, parent.Index, rebel.Index));
            ctx.Events.Publish(new KingdomFoundedEvent(rebel.Index, city.Index));
            if (meta.Laws.IsOn(meta.LawWars) && !meta.Laws.IsOn(meta.LawPeaceful))
                meta.DeclareWar(parent.Index, rebel.Index, meta.Content.WarTypes.IdOrDefault("war.rebellion"), tick, ctx.Events);
            return rebel;
        }

        // ---------------- opinion (Bölüm 6.4 "Fikir") ----------------

        void BuildBorders(MetaState meta)
        {
            int n = meta.Kingdoms.Count;
            if (_borderSize < n)
            {
                _borderSize = math.max(16, math.ceilpow2(n));
                _border = new bool[_borderSize * _borderSize];
            }
            else Array.Clear(_border, 0, _border.Length);
            var map = meta.Civ.Map;
            var cities = meta.Civ.Cities;
            int K(int zone)
            {
                int c = map.Zones[zone].OwnerCity;
                return c >= 0 ? cities[c].Kingdom : -1;
            }
            for (int zy = 0; zy < map.ZonesY; zy++)
                for (int zx = 0; zx < map.ZonesX; zx++)
                {
                    int z = zy * map.ZonesX + zx;
                    int a = K(z);
                    if (a < 0) continue;
                    if (zx + 1 < map.ZonesX) Mark(a, K(z + 1));
                    if (zy + 1 < map.ZonesY) Mark(a, K(z + map.ZonesX));
                }
        }

        void Mark(int a, int b)
        {
            if (b < 0 || a == b) return;
            _border[a * _borderSize + b] = true;
            _border[b * _borderSize + a] = true;
        }

        public bool Borders(int a, int b) => a < _borderSize && b < _borderSize && _border[a * _borderSize + b];

        void Opinions(MetaState meta, int eraBonus, long tick)
        {
            var ks = meta.Kingdoms;
            for (int a = 0; a < ks.Count; a++)
            {
                if (ks[a].Dead) continue;
                for (int b = 0; b < ks.Count; b++)
                {
                    if (a == b || ks[b].Dead) continue;
                    int target = OpinionTarget(meta, ks[a], ks[b], eraBonus, tick);
                    ks[a].SetOpinion(b, Converge(ks[a].OpinionOf(b), target, OpinionRate));
                }
            }
        }

        public int OpinionTarget(MetaState meta, Kingdom a, Kingdom b, int eraBonus, long tick)
        {
            if (meta.Laws.IsOn(meta.LawChaos)) return -100;
            float v = 0f;
            // same species stands for same culture (15) + same faith (20) + same kind (10) until Faz 7; other kinds are rivals (DECISIONS #63)
            v += a.Species == b.Species ? 45f : -15f;
            if (Borders(a.Index, b.Index)) v -= 10f;
            if (a.Alliance >= 0 && a.Alliance == b.Alliance) v += 40f;
            if (CommonEnemy(meta, a.Index, b.Index)) v += 20f;
            long since = tick - WarMemoryYears * (long)SimConst.TicksPerYear;
            foreach (var w in meta.Wars)
            {
                if (w.StartTick < since) continue;
                bool opposed = (w.OnAttackerSide(a.Index) && w.OnDefenderSide(b.Index)) || (w.OnDefenderSide(a.Index) && w.OnAttackerSide(b.Index));
                if (opposed) v -= 15f;
            }
            v += math.min(20, 2 * (b.Index < a.Trade.Count ? a.Trade[b.Index] : 0));
            int king = a.KingIndex;
            var u = meta.Units.Store;
            if (king >= 0 && u.IsAlive(king) && u.Uid[king] == a.KingUid)
            {
                if (HasTrait(meta, king, meta.TrPeaceful)) v += 10f;
                if (HasTrait(meta, king, meta.TrBloodthirsty)) v -= 10f;
                if (HasTrait(meta, king, meta.TrHonest)) v += 10f;
                if (HasTrait(meta, king, meta.TrDeceitful)) v -= 10f;
            }
            foreach (int t in a.Traits) v += meta.Content.KingdomTraits[t].Add("opinion");
            v += eraBonus;
            return (int)math.round(math.clamp(v, -100f, 100f));
        }

        static bool CommonEnemy(MetaState meta, int a, int b)
        {
            for (int c = 0; c < meta.Kingdoms.Count; c++)
                if (c != a && c != b && meta.AtWar(a, c) && meta.AtWar(b, c)) return true;
            return false;
        }

        // ---------------- alliances (Bölüm 6.6) ----------------

        // Plots (Bölüm 6.11) form alliances later; until then two friendly kingdoms that share a border or an enemy may ally
        // (DECISIONS #63). Members who fall out (< -30) leave.
        void Alliances(MetaState meta, ref SimRandom rng)
        {
            var ks = meta.Kingdoms;
            foreach (var k in ks)
            {
                if (k.Dead || k.Alliance < 0) continue;
                foreach (int o in meta.Alliances[k.Alliance].Kingdoms)
                    if (o != k.Index && k.OpinionOf(o) < -30) { meta.LeaveAlliance(k); break; }
            }
            if (!meta.Laws.IsOn(meta.LawDiplomacy)) return;
            for (int a = 0; a < ks.Count; a++)
            {
                if (ks[a].Dead || ks[a].Alliance >= 0) continue;
                for (int b = a + 1; b < ks.Count; b++)
                {
                    if (ks[b].Dead || ks[b].Alliance >= 0 || meta.AtWar(a, b)) continue;
                    if (ks[a].OpinionOf(b) < 60 || ks[b].OpinionOf(a) < 60) continue;
                    if (!Borders(a, b) && !CommonEnemy(meta, a, b)) continue;
                    if (!rng.Chance(0.02f)) continue;
                    meta.CreateAlliance(a, b);
                    break;
                }
            }
        }
    }

    // Bölüm 6.5 yearly: peace checks, then war declarations.
    public sealed class WarSystem : ISimSystem
    {
        public const int YearOffset = 45;
        public const int CooldownYears = 10;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 20;

        readonly List<int> _order = new List<int>();

        public void Tick(in SimContext ctx)
        {
            if (ctx.Clock.Tick % SimConst.TicksPerYear != YearOffset) return;
            var meta = ctx.Units.Meta;
            if (meta == null) return;
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            Yearly(ctx, meta, ref rng);
        }

        public void Yearly(in SimContext ctx, MetaState meta, ref SimRandom rng)
        {
            long tick = ctx.Clock.Tick;
            var era = ctx.Nature.CurrentEra;
            bool peaceful = meta.Laws.IsOn(meta.LawPeaceful) || !meta.Laws.IsOn(meta.LawWars);
            bool dawn = HasCode(era, "PeaceBoost");
            for (int w = 0; w < meta.Wars.Count; w++)
            {
                var war = meta.Wars[w];
                if (!war.Active) continue;
                if (peaceful) { meta.EndWar(war, WarResult.Peace, tick, ctx.Events); continue; }
                float years = war.Duration(tick) / (float)SimConst.TicksPerYear;
                float losses = math.max(Loss(meta, war.CasualtiesA, war.AttackerSide), Loss(meta, war.CasualtiesD, war.DefenderSide));
                float chance = 0.05f + years * 0.02f + losses * 0.3f + (dawn ? 0.3f : 0f);
                if (KingDiedThisYear(meta, war, tick)) chance *= 2f;
                if (rng.Chance(chance)) meta.EndWar(war, WarResult.Peace, tick, ctx.Events);
            }
            foreach (var k in meta.Kingdoms) k.Trade.Clear();
            if (peaceful) return;

            float eraMul = HasCode(era, "WarBoost") ? 2f : 1f;
            // a fixed shuffle per year so the first kingdom is not always the aggressor
            _order.Clear();
            for (int k = 0; k < meta.Kingdoms.Count; k++) _order.Add(k);
            for (int k = _order.Count - 1; k > 0; k--)
            {
                int j = rng.Range(0, k + 1);
                (_order[k], _order[j]) = (_order[j], _order[k]);
            }
            foreach (int a in _order)
            {
                var k = meta.Kingdoms[a];
                if (k.Dead || (k.Flags & MetaFlags.PlayerControlled) != 0 || k.Capital < 0) continue;
                if (tick - k.LastWarEndTick < CooldownYears * (long)SimConst.TicksPerYear) continue;
                if (Attacking(meta, a)) continue;
                int target = BestTarget(meta, k, out float score);
                if (target < 0) continue;
                float chance = Sigmoid((score - 40f) / 15f) * eraMul * (1f + meta.TraitPct(k, "warChance") / 100f);
                if (!rng.Chance(chance)) continue;
                var war = meta.DeclareWar(a, target, meta.Content.WarTypes.IdOrDefault("war.conquest"), tick, ctx.Events);
                war.TargetCity = meta.Kingdoms[target].Capital;
            }
        }

        public static float Sigmoid(float x) => 1f / (1f + math.exp(-x));

        static bool HasCode(EraDef era, string code)
        {
            if (era.EffectCodes == null) return false;
            foreach (var c in era.EffectCodes) if (c == code) return true;
            return false;
        }

        static float Loss(MetaState meta, int casualties, List<int> side)
        {
            int pop = 0;
            foreach (int k in side) pop += meta.Kingdoms[k].Population;
            return casualties / (float)math.max(1, pop + casualties);
        }

        static bool KingDiedThisYear(MetaState meta, War war, long tick)
        {
            long since = tick - SimConst.TicksPerYear;
            return meta.Kingdoms[war.Attacker].SuccessionTick > since || meta.Kingdoms[war.Defender].SuccessionTick > since;
        }

        static bool Attacking(MetaState meta, int k)
        {
            foreach (var w in meta.Wars) if (w.Active && w.Attacker == k) return true;
            return false;
        }

        // Bölüm 6.5: candidates are kingdoms we dislike (< -20) that we can walk to.
        public static int BestTarget(MetaState meta, Kingdom k, out float bestScore)
        {
            bestScore = float.MinValue;
            int best = -1;
            var civ = meta.Civ;
            var u = meta.Units.Store;
            float2 home = civ.Cities[k.Capital].Center;
            bool bloodthirsty = k.KingIndex >= 0 && u.IsAlive(k.KingIndex) && meta.TrBloodthirsty >= 0 && u.Traits[k.KingIndex].Has(meta.TrBloodthirsty);
            foreach (var o in meta.Kingdoms)
            {
                if (o.Dead || o.Index == k.Index || o.Capital < 0 || meta.AtWar(k.Index, o.Index)) continue;
                int opinion = k.OpinionOf(o.Index);
                if (opinion >= -20) continue;
                if (!Reachable(meta, k, o, out float dist)) continue;
                float score = -opinion + 30f * (k.Power / math.max(1f, o.Power) - 1f) + (bloodthirsty ? 20f : 0f) - dist / 10f;
                if (score > bestScore) { bestScore = score; best = o.Index; }
            }
            return best;
        }

        // Closest pair of cities on the same island (ships come with colonisation, Bölüm 5.10).
        static bool Reachable(MetaState meta, Kingdom a, Kingdom b, out float dist)
        {
            dist = float.MaxValue;
            var civ = meta.Civ;
            foreach (int ca in a.Cities)
                foreach (int cb in b.Cities)
                {
                    var x = civ.Cities[ca];
                    var y = civ.Cities[cb];
                    float d = math.distance((float2)x.Center, (float2)y.Center);
                    if (d < dist && meta.SameIsland(x, y)) dist = d;
                }
            return dist < float.MaxValue;
        }
    }

    // Bölüm 6.5 "Ordular" / "Şehir fethi": every 20 ticks armies gather, march, besiege and come home.
    public sealed class ArmySystem : ISimSystem
    {
        public const int Every = 20;
        public const int MinSoldiers = 3;
        public const float SiegeRadius = 14f, CaptureRadius = 5f, DefendRadius = 10f;
        // a year is 36 s and people walk about a tile a second: a march across a medium map takes years (DECISIONS #63)
        public const int GatherTicks = 2 * SimConst.TicksPerMonth, MarchTicks = 5 * SimConst.TicksPerYear, SiegeTicks = SimConst.TicksPerYear;
        public const float SiegeDamagePerSoldier = 2f;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 30;

        readonly List<int> _candidates = new List<int>();

        public void Tick(in SimContext ctx)
        {
            if (ctx.Clock.Tick % Every != 7) return;
            Step(ctx);
        }

        public void Step(in SimContext ctx)
        {
            var meta = ctx.Units.Meta;
            if (meta == null || meta.Wars.Count == 0) return;
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            long tick = ctx.Clock.Tick;
            for (int w = 0; w < meta.Wars.Count; w++)
            {
                var war = meta.Wars[w];
                if (!war.Active) continue;
                foreach (int k in war.AttackerSide) TryForm(ctx, meta, war, k, true);
                foreach (int k in war.DefenderSide) TryForm(ctx, meta, war, k, false);
            }
            for (int a = 0; a < meta.Armies.Count; a++)
            {
                var army = meta.Armies[a];
                if (army.State == ArmyState.Disbanded) continue;
                UpdateArmy(ctx, meta, army, tick, ref rng);
            }
        }

        static bool HasArmy(MetaState meta, int kingdom, int war)
        {
            foreach (var a in meta.Armies)
                if (a.Kingdom == kingdom && a.War == war && (a.State == ArmyState.Gathering || a.State == ArmyState.Marching || a.State == ArmyState.Sieging))
                    return true;
            return false;
        }

        void TryForm(in SimContext ctx, MetaState meta, War war, int kingdom, bool attacker)
        {
            var k = meta.Kingdoms[kingdom];
            long tick = ctx.Clock.Tick;
            if (k.Dead || tick < k.NextArmyTick || HasArmy(meta, kingdom, war.Index)) return;
            k.NextArmyTick = tick + SimConst.TicksPerMonth;
            // defenders go on the offensive only once the attacker has been held off for a year
            if (!attacker && war.Duration(tick) < SimConst.TicksPerYear) return;
            var enemies = attacker ? war.DefenderSide : war.AttackerSide;
            if (!PickTarget(meta, k, enemies, out int home, out int target)) return;
            var civ = meta.Civ;
            var u = meta.Units.Store;
            var homeCity = civ.Cities[home];
            _candidates.Clear();
            foreach (int c in k.Cities)
            {
                var city = civ.Cities[c];
                if (city.Dead || math.distance((float2)city.Center, (float2)homeCity.Center) > 80f) continue;
                foreach (int i in city.Residents)
                    if (u.Job[i] - 1 == civ.Ids.JobWarrior && u.ArmyOf[i] < 0 && u.State[i] == UnitStore.StateAlive) _candidates.Add(i);
            }
            // keep a third of the warriors home to defend (Bölüm 6.5 leaves the split open; DECISIONS #63)
            int take = _candidates.Count - _candidates.Count / 3;
            take = (int)(take * (1f + meta.TraitPct(k, "armySize") / 100f));
            take = math.min(take, _candidates.Count);
            if (take < MinSoldiers) return;
            var army = new Army
            {
                Index = meta.Armies.Count, Kingdom = kingdom, War = war.Index, HomeCity = home, TargetCity = target,
                RallyTile = homeCity.Center, State = ArmyState.Gathering, StateTick = tick,
            };
            // captain = best warfare (Bölüm 6.5)
            int captain = -1;
            float bestWar = float.MinValue;
            for (int n = 0; n < take; n++)
            {
                int i = _candidates[n];
                army.Soldiers.Add(i);
                army.SoldierUids.Add(u.Uid[i]);
                u.ArmyOf[i] = army.Index;
                float s = u.Stat(i, StatId.Warfare) + u.Level[i];
                if (s > bestWar) { bestWar = s; captain = i; }
            }
            army.CaptainUid = captain >= 0 ? u.Uid[captain] : 0;
            army.StartSize = army.Soldiers.Count;
            meta.Armies.Add(army);
            k.NextArmyTick = tick + 2 * SimConst.TicksPerMonth;
        }

        // Nearest, weakest enemy city on our island; home = our city closest to it.
        static bool PickTarget(MetaState meta, Kingdom k, List<int> enemies, out int home, out int target)
        {
            home = target = -1;
            var civ = meta.Civ;
            float best = float.MaxValue;
            foreach (int e in enemies)
            {
                var ek = meta.Kingdoms[e];
                if (ek.Dead) continue;
                foreach (int ec in ek.Cities)
                {
                    var enemyCity = civ.Cities[ec];
                    if (enemyCity.Dead) continue;
                    int defenders = Defenders(meta, enemyCity, float.MaxValue);
                    foreach (int oc in k.Cities)
                    {
                        var own = civ.Cities[oc];
                        if (own.Dead) continue;
                        float d = math.distance((float2)own.Center, (float2)enemyCity.Center) + defenders * 10f;
                        if (d >= best || !meta.SameIsland(own, enemyCity)) continue;
                        best = d;
                        home = oc;
                        target = ec;
                    }
                }
            }
            return target >= 0;
        }

        // Living warriors, guards and soldiers of the city's kingdom near its centre (all of them when radius is infinite).
        public static int Defenders(MetaState meta, City city, float radius)
        {
            var u = meta.Units.Store;
            var ids = meta.Civ.Ids;
            int n = 0;
            float r2 = radius * radius;
            foreach (int c in meta.Kingdoms[city.Kingdom].Cities)
                foreach (int i in meta.Civ.Cities[c].Residents)
                {
                    if (u.State[i] != UnitStore.StateAlive || !MetaState.IsCombatant(u, ids, i)) continue;
                    if (radius < float.MaxValue && math.distancesq(u.Pos[i], (float2)city.Center + 0.5f) > r2) continue;
                    n++;
                }
            return n;
        }

        void UpdateArmy(in SimContext ctx, MetaState meta, Army army, long tick, ref SimRandom rng)
        {
            var u = meta.Units.Store;
            var civ = meta.Civ;
            var war = meta.Wars[army.War];
            // drop the fallen and those who left the army's kingdom or job
            for (int n = army.Soldiers.Count - 1; n >= 0; n--)
            {
                int i = army.Soldiers[n];
                bool ok = u.State[i] == UnitStore.StateAlive && u.Uid[i] == army.SoldierUids[n] && u.ArmyOf[i] == army.Index
                          && meta.KingdomOfUnit(i) == army.Kingdom && u.Job[i] - 1 == civ.Ids.JobWarrior;
                if (ok) continue;
                bool died = u.Uid[i] != army.SoldierUids[n] || u.State[i] != UnitStore.StateAlive;
                if (died)
                {
                    if (war.OnAttackerSide(army.Kingdom)) war.CasualtiesA++;
                    else war.CasualtiesD++;
                }
                else if (u.ArmyOf[i] == army.Index) u.ArmyOf[i] = -1;
                army.Soldiers.RemoveAt(n);
                army.SoldierUids.RemoveAt(n);
            }
            var target = army.TargetCity >= 0 ? civ.Cities[army.TargetCity] : null;
            bool targetValid = target != null && !target.Dead && target.Kingdom >= 0 && meta.AtWar(army.Kingdom, target.Kingdom);
            bool active = army.State == ArmyState.Gathering || army.State == ArmyState.Marching || army.State == ArmyState.Sieging;
            if (active && (!war.Active || !targetValid || army.Soldiers.Count == 0 || army.Soldiers.Count < army.StartSize * 0.3f))
                SetState(army, ArmyState.Returning, tick);
            var home = civ.Cities[army.HomeCity];
            switch (army.State)
            {
                case ArmyState.Gathering:
                {
                    int near = Steer(meta, army, army.RallyTile, 2);
                    if (near * 10 >= army.Soldiers.Count * 7 || tick - army.StateTick > GatherTicks) SetState(army, ArmyState.Marching, tick);
                    break;
                }
                case ArmyState.Marching:
                {
                    Steer(meta, army, target.Center, 2);
                    if (NearCount(u, army, target.Center, SiegeRadius) > 0) SetState(army, ArmyState.Sieging, tick);
                    else if (tick - army.StateTick > MarchTicks) SetState(army, ArmyState.Returning, tick);
                    break;
                }
                case ArmyState.Sieging:
                {
                    Steer(meta, army, target.Center, 1);
                    int inside = NearCount(u, army, target.Center, SiegeRadius);
                    if (inside > 0) Siege(ctx, meta, target, inside, ref rng);
                    if (!target.Dead && Defenders(meta, target, DefendRadius) == 0 && NearCount(u, army, target.Center, CaptureRadius) > 0)
                    {
                        Conquer(ctx, meta, army, war, target, tick);
                        SetState(army, ArmyState.Returning, tick);
                    }
                    else if (tick - army.StateTick > SiegeTicks) SetState(army, ArmyState.Returning, tick);
                    break;
                }
                case ArmyState.Returning:
                {
                    int2 back = home.Dead ? army.RallyTile : home.Center;
                    int near = Steer(meta, army, back, 3);
                    if (near >= army.Soldiers.Count || tick - army.StateTick > MarchTicks) Disband(meta, army);
                    break;
                }
            }
        }

        static void SetState(Army army, ArmyState state, long tick)
        {
            army.State = state;
            army.StateTick = tick;
        }

        // Formation around the goal (Bölüm 6.5); returns how many soldiers are already within 8 tiles.
        static int Steer(MetaState meta, Army army, int2 goal, int spacing)
        {
            var u = meta.Units.Store;
            var map = meta.Civ.Map;
            int near = 0;
            for (int n = 0; n < army.Soldiers.Count; n++)
            {
                int i = army.Soldiers[n];
                int2 t = goal + new int2(n % 5 - 2, n / 5 % 5 - 2) * spacing;
                t = math.clamp(t, 0, new int2(map.Width - 1, map.Height - 1));
                if (!math.all(u.TargetTile[i] == t) && (UnitTask)u.Task[i] == UnitTask.March) meta.Units.ClearPath(i);
                u.TargetTile[i] = t;
                if (math.distancesq(u.Pos[i], (float2)goal) <= 64f) near++;
            }
            return near;
        }

        static int NearCount(UnitStore u, Army army, int2 at, float radius)
        {
            int n = 0;
            float r2 = radius * radius;
            foreach (int i in army.Soldiers) if (math.distancesq(u.Pos[i], (float2)at + 0.5f) <= r2) n++;
            return n;
        }

        // Soldiers inside the city wear its buildings down (Bölüm 6.5 "binalara saldırır").
        static void Siege(in SimContext ctx, MetaState meta, City city, int soldiers, ref SimRandom rng)
        {
            var civ = meta.Civ;
            if (city.Buildings.Count == 0) return;
            int b = city.Buildings[rng.Range(0, city.Buildings.Count)];
            var data = civ.Buildings[b];
            if (data.State != BuildingState.Complete && data.State != BuildingState.Construction) return;
            if (b == city.CenterBuilding) return; // the hall changes hands with the city
            float wall = 1f + meta.TraitPct(meta.Kingdoms[city.Kingdom], "wallHp") / 100f;
            data.Hp -= soldiers * SiegeDamagePerSoldier * Every / wall / SimConst.TicksPerSecond;
            civ.Buildings[b] = data;
            if (data.Hp <= 0f) civ.Destroy(b, ctx.Clock.Tick, ctx.Events, leaveRuins: true);
        }

        public static void Conquer(in SimContext ctx, MetaState meta, Army army, War war, City city, long tick)
        {
            int from = city.Kingdom;
            if (city.Population == 0) meta.Civ.KillCity(city, tick, ctx.Events); // nobody to rule: razed (Bölüm 6.5)
            else meta.CaptureCity(city, army.Kingdom, war, tick, ctx.Events);
            if (from >= 0 && meta.Kingdoms[from].Cities.Count == 0 || (from >= 0 && AllDead(meta, meta.Kingdoms[from])))
                meta.KillKingdom(meta.Kingdoms[from], tick, ctx.Events);
        }

        static bool AllDead(MetaState meta, Kingdom k)
        {
            foreach (int c in k.Cities) if (!meta.Civ.Cities[c].Dead) return false;
            return true;
        }

        public static void Disband(MetaState meta, Army army)
        {
            var u = meta.Units.Store;
            for (int n = 0; n < army.Soldiers.Count; n++)
            {
                int i = army.Soldiers[n];
                if (u.Uid[i] == army.SoldierUids[n] && u.ArmyOf[i] == army.Index) u.ArmyOf[i] = -1;
            }
            army.Soldiers.Clear();
            army.SoldierUids.Clear();
            army.State = ArmyState.Disbanded;
        }
    }

    // Bölüm 5.9 trade (yearly) and 5.11 migration (yearly), plus caravan bookkeeping every month.
    public sealed class TradeMigrationSystem : ISimSystem
    {
        public const int YearOffset = 90;
        public const int CaravanMax = 30;
        public const float MigrateChance = 0.1f;
        public const int UnhappyBelow = -30;

        public SimPhase Phase => SimPhase.Meta;
        public int Order => 40;

        public void Tick(in SimContext ctx)
        {
            var meta = ctx.Units.Meta;
            if (meta == null) return;
            long tick = ctx.Clock.Tick;
            if (tick % SimConst.TicksPerMonth == MonthOffset) CheckCaravans(ctx, meta);
            if (tick % SimConst.TicksPerYear != YearOffset) return;
            ref var rng = ref ctx.Rng.Get(RngStream.Meta);
            SendCaravans(ctx, meta, ref rng);
            if (meta.Laws.IsOn(meta.LawMigration)) Migrate(ctx, meta, ref rng);
        }

        const int MonthOffset = 25;

        static bool Friendly(MetaState meta, int a, int b)
        {
            if (a < 0 || b < 0) return false;
            if (a == b) return true;
            var ka = meta.Kingdoms[a];
            return !meta.AtWar(a, b) && ((ka.Alliance >= 0 && ka.Alliance == meta.Kingdoms[b].Alliance) || ka.OpinionOf(b) > 20);
        }

        static void SendCaravans(in SimContext ctx, MetaState meta, ref SimRandom rng)
        {
            var civ = meta.Civ;
            var u = meta.Units.Store;
            int market = civ.Ids.Market;
            if (market < 0) return;
            foreach (var from in civ.Cities)
            {
                if (from.Dead || from.Kingdom < 0 || !civ.HasComplete(from, market)) continue;
                City best = null;
                float bestD = 200f;
                foreach (var to in civ.Cities)
                {
                    if (to == from || to.Dead || !civ.HasComplete(to, market) || !Friendly(meta, from.Kingdom, to.Kingdom)) continue;
                    float d = math.distance((float2)from.Center, (float2)to.Center);
                    if (d >= bestD || !meta.SameIsland(from, to)) continue;
                    best = to;
                    bestD = d;
                }
                if (best == null) continue;
                int res = Surplus(civ, from);
                if (res < 0) continue;
                int merchant = -1;
                foreach (int i in from.Residents)
                {
                    if (!JobAssigner.CanWork(u, i) || u.ArmyOf[i] >= 0 || u.Job[i] - 1 == civ.Ids.JobLeader || u.CarryAmount[i] > 0) continue;
                    merchant = i;
                    break;
                }
                if (merchant < 0) continue;
                int amount = math.min(CaravanMax, from.StockOf(res) / 2);
                if (amount <= 0) continue;
                from.Stock[res] -= amount;
                u.CarryRes[merchant] = (short)res;
                u.CarryAmount[merchant] = (byte)amount;
                u.Task[merchant] = (byte)UnitTask.Caravan;
                u.Action[merchant] = 0;
                u.Timer[merchant] = 0;
                u.TargetTile[merchant] = best.Center;
                meta.Units.ClearPath(merchant);
                meta.Caravans.Add(new Caravan { UnitUid = u.Uid[merchant], Unit = merchant, From = from.Index, To = best.Index, StartTick = ctx.Clock.Tick });
            }
        }

        // The resource the city holds most of beyond a small reserve.
        static int Surplus(CivState civ, City city)
        {
            int best = -1, bestAmount = 20;
            for (int r = 0; r < city.Stock.Length; r++)
                if (city.Stock[r] > bestAmount) { bestAmount = city.Stock[r]; best = r; }
            return best;
        }

        // Arrival is handled by the unit (UnitActSystem.Caravan); here the lost ones are written off.
        static void CheckCaravans(in SimContext ctx, MetaState meta)
        {
            var u = meta.Units.Store;
            for (int n = meta.Caravans.Count - 1; n >= 0; n--)
            {
                var c = meta.Caravans[n];
                bool alive = u.State[c.Unit] == UnitStore.StateAlive && u.Uid[c.Unit] == c.UnitUid;
                if (alive && (UnitTask)u.Task[c.Unit] == UnitTask.Caravan) continue;
                if (!alive)
                {
                    int a = meta.Civ.Cities[c.From].Kingdom, b = meta.Civ.Cities[c.To].Kingdom;
                    if (a >= 0 && b >= 0 && a != b)
                    {
                        meta.Kingdoms[a].SetOpinion(b, meta.Kingdoms[a].OpinionOf(b) - 5);
                        meta.Kingdoms[b].SetOpinion(a, meta.Kingdoms[b].OpinionOf(a) - 5);
                    }
                }
                meta.Caravans.RemoveAt(n);
            }
        }

        // Bölüm 5.11: unhappy or starving residents may move to a happier city of their kingdom.
        static void Migrate(in SimContext ctx, MetaState meta, ref SimRandom rng)
        {
            var civ = meta.Civ;
            var u = meta.Units.Store;
            int homesick = ctx.Content.StatusEffects.IdOrDefault("st.homesick");
            foreach (var from in civ.Cities)
            {
                if (from.Dead || from.Kingdom < 0 || (from.Happiness >= UnhappyBelow && from.FamineMonths == 0)) continue;
                City best = null;
                float bestScore = from.Happiness;
                foreach (int c in meta.Kingdoms[from.Kingdom].Cities)
                {
                    var to = civ.Cities[c];
                    if (to == from || to.Dead || to.Population >= to.HousingCapacity) continue;
                    float score = to.Happiness * (civ.HasComplete(to, civ.Ids.Inn) ? 1.5f : 1f) - (to.FamineMonths > 0 ? 50 : 0);
                    if (score <= bestScore || !meta.SameIsland(from, to)) continue;
                    best = to;
                    bestScore = score;
                }
                if (best == null) continue;
                for (int n = from.Residents.Count - 1; n >= 0; n--)
                {
                    int i = from.Residents[n];
                    if (u.Uid[i] == from.LeaderUid || u.ArmyOf[i] >= 0 || !rng.Chance(MigrateChance)) continue;
                    UnitActSystem.Unload(civ, from, i);
                    u.City[i] = best.Index;
                    u.Job[i] = 0;
                    u.HomeBuilding[i] = -1;
                    u.WorkBuilding[i] = -1;
                    u.Task[i] = (byte)UnitTask.Migrate;
                    u.Action[i] = 0;
                    u.Timer[i] = 0;
                    u.TargetTile[i] = best.Center;
                    meta.Units.ClearPath(i);
                    if (homesick >= 0) meta.Units.AddStatus(i, homesick);
                }
            }
        }
    }
}
