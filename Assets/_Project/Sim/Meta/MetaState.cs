using System;
using System.Collections.Generic;
using PG.Content;
using PG.Core;
using Unity.Mathematics;
using UnityEngine;

namespace PG.Sim
{
    // Bölüm 6.1-6.6: kingdoms, wars, armies and alliances of one world. Systems live in MetaSystems.cs.
    public sealed class MetaState
    {
        public readonly List<Kingdom> Kingdoms = new List<Kingdom>();
        public readonly List<War> Wars = new List<War>();
        public readonly List<Army> Armies = new List<Army>();
        public readonly List<Alliance> Alliances = new List<Alliance>();
        public readonly List<Caravan> Caravans = new List<Caravan>();
        public readonly List<Boat> Boats = new List<Boat>();
        public readonly WorldLaws Laws;
        public long NextUid = 1;
        public readonly ContentDB Content;
        readonly CivState _civ;
        readonly UnitWorld _units;
        bool[] _atWar = Array.Empty<bool>();
        int _warSize;

        public readonly int TrLoyal, TrTreacherous, TrPeaceful, TrBloodthirsty, TrHonest, TrDeceitful, TrAmbitious;
        public readonly int LawWars, LawRebellions, LawDiplomacy, LawMigration, LawColonization, LawPeaceful, LawChaos;

        public MetaState(ContentDB content, CivState civ, UnitWorld units, WorldLaws laws)
        {
            Content = content;
            _civ = civ;
            _units = units;
            units.Meta = this;
            Laws = laws;
            int T(string id) => content.UnitTraits.IdOrDefault(id);
            TrLoyal = T("tr.loyal"); TrTreacherous = T("tr.treacherous"); TrPeaceful = T("tr.peaceful"); TrBloodthirsty = T("tr.bloodthirsty");
            TrHonest = T("tr.honest"); TrDeceitful = T("tr.deceitful"); TrAmbitious = T("tr.ambitious");
            LawWars = laws.IndexOf("law.wars");
            LawRebellions = laws.IndexOf("law.kingdom_rebellions");
            LawDiplomacy = laws.IndexOf("law.diplomacy");
            LawMigration = laws.IndexOf("law.migration");
            LawColonization = laws.IndexOf("law.colonization");
            LawPeaceful = laws.IndexOf("law.peaceful_world");
            LawChaos = laws.IndexOf("law.chaos_world");
        }

        // Defaults for a save without the meta section: cities get their kingdoms back on the next monthly update.
        public void Reset()
        {
            Kingdoms.Clear(); Wars.Clear(); Armies.Clear(); Alliances.Clear(); Caravans.Clear(); Boats.Clear();
            AliveKingdoms = 0;
            foreach (var city in _civ.Cities) { city.Kingdom = -1; city.Loyalty = 50; }
            _units.Store.ResetMeta();
            _warSize = 0;
            _atWar = Array.Empty<bool>();
        }

        public CivState Civ => _civ;
        public UnitWorld Units => _units;
        public int AliveKingdoms { get; private set; }

        public int KingdomOfCity(int city) => city >= 0 ? _civ.Cities[city].Kingdom : -1;
        public int KingdomOfUnit(int unit) => KingdomOfCity(_units.Store.City[unit]);

        // Unit index of a resident of the kingdom with this uid, -1 when dead or gone.
        public int FindInKingdom(Kingdom k, long uid)
        {
            if (uid == 0) return -1;
            var u = _units.Store;
            foreach (int c in k.Cities)
                foreach (int i in _civ.Cities[c].Residents)
                    if (u.Uid[i] == uid && u.State[i] == UnitStore.StateAlive) return i;
            return -1;
        }

        // Warriors, guards and army soldiers fight wars on sight; everyone else only defends themselves (DECISIONS #63).
        public static bool IsCombatant(UnitStore u, CivIds ids, int i)
        {
            int job = u.Job[i] - 1;
            return u.ArmyOf[i] >= 0 || (job >= 0 && (job == ids.JobWarrior || job == ids.JobGuard));
        }

        // A walkable tile at the city's centre for island checks (a bonfire or hall origin is not walkable; halls use their door).
        public int2 Anchor(City city) => _units.Paths.NearestStandable(city.Center, Mobility.Land, out int2 t) ? t : city.Center;

        public bool SameIsland(City a, City b) => _units.Paths.SameIsland(Anchor(a), Anchor(b), Mobility.Land);

        public bool ArmyActive(int army) => army >= 0 && army < Armies.Count && Armies[army].State != ArmyState.Disbanded;

        // Bölüm 5.9 arrival: goods go into the market city's stock, gold to the sender, +2 opinion via the yearly trade count.
        public void DeliverCaravan(int unit, long tick)
        {
            var u = _units.Store;
            for (int n = 0; n < Caravans.Count; n++)
            {
                var c = Caravans[n];
                if (c.Unit != unit || c.UnitUid != u.Uid[unit]) continue;
                var from = _civ.Cities[c.From];
                var to = _civ.Cities[c.To];
                int res = u.CarryRes[unit], amount = u.CarryAmount[unit];
                if (!to.Dead && res >= 0 && amount > 0)
                {
                    _civ.AddStock(to, res, amount);
                    var def = Content.Resources[res];
                    float profit = 0f;
                    if (from.Kingdom >= 0) profit = TraitPct(Kingdoms[from.Kingdom], "gold") / 100f;
                    if (!from.Dead) from.Gold += (int)math.round(math.max(1, def.Value) * amount * 1.3f * (1f + profit));
                    AddTrade(to.Kingdom, from.Kingdom);
                    AddTrade(from.Kingdom, to.Kingdom);
                }
                u.CarryRes[unit] = -1;
                u.CarryAmount[unit] = 0;
                Caravans.RemoveAt(n);
                return;
            }
        }

        // Units of kingdoms at war attack each other on sight (UnitWorld.Hostile).
        public bool WarHostile(int a, int b)
        {
            var u = _units.Store;
            int ca = u.City[a], cb = u.City[b];
            if (ca < 0 || cb < 0 || ca == cb) return false;
            if (!IsCombatant(u, _civ.Ids, a) || !IsCombatant(u, _civ.Ids, b)) return false;
            return AtWar(_civ.Cities[ca].Kingdom, _civ.Cities[cb].Kingdom);
        }

        // Years of active war this kingdom is in right now (war weariness, Bölüm 6.4).
        public float WarYears(int k, long tick)
        {
            float years = 0f;
            foreach (var w in Wars)
                if (w.Active && (w.OnAttackerSide(k) || w.OnDefenderSide(k))) years += w.Duration(tick) / (float)SimConst.TicksPerYear;
            return years;
        }

        public void AddTrade(int receiver, int sender)
        {
            if (receiver < 0 || sender < 0 || receiver == sender) return;
            var t = Kingdoms[receiver].Trade;
            while (t.Count <= sender) t.Add(0);
            t[sender]++;
        }

        // ---------------- kingdoms ----------------

        public Kingdom CreateKingdom(City capital, long kingUid, long tick, ref SimRandom rng, Kingdom parent = null)
        {
            var k = new Kingdom
            {
                Index = Kingdoms.Count,
                Uid = NextUid++,
                Species = capital.Species,
                FoundedTick = tick,
                FounderUid = kingUid,
                KingUid = kingUid,
                Capital = capital.Index,
                Banner = parent != null ? parent.Banner.Recolor(ref rng) : Banner.Random(ref rng),
            };
            k.Root = NameGen.Word(Content.NameSets.SetIds.Count > 0 ? Content.NameSets.Sets[Content.NameSets.SetIds[0]] : new NameSets.Set(), ref rng);
            k.Color = Color.HSVToRGB(rng.Value01(), 0.7f, 0.85f);
            // doc 6.1: a new meta object may start with a random trait (40%)
            if (Content.KingdomTraits.Count > 0 && rng.Chance(0.4f)) k.Traits = new[] { rng.Range(0, Content.KingdomTraits.Count) };
            Kingdoms.Add(k);
            AliveKingdoms++;
            foreach (var other in Kingdoms)
            {
                other.SetOpinion(k.Index, other == parent ? -60 : 0);
                while (other.Trade.Count < Kingdoms.Count) other.Trade.Add(0);
            }
            k.SetOpinion(k.Index, 100);
            if (parent != null) k.SetOpinion(parent.Index, -60);
            MoveCity(capital, k.Index);
            Rename(k);
            GrowWarMatrix();
            return k;
        }

        // Bölüm 6.2: root + ruler title by city count, or root + "ya" for the plainest realms.
        public void Rename(Kingdom k)
        {
            var titles = Content.NameSets.RulerTitles;
            int t = 0;
            for (int i = 0; i < titles.Count; i++) if (k.Cities.Count >= titles[i].MinCities) t = i;
            k.RulerTitle = t;
            k.Name = titles.Count > 0 ? k.Root + " " + titles[t].Title : k.Root + "ya";
        }

        public void MoveCity(City city, int kingdom)
        {
            int old = city.Kingdom;
            if (old == kingdom) return;
            if (old >= 0) Kingdoms[old].Cities.Remove(city.Index);
            city.Kingdom = kingdom;
            if (kingdom >= 0 && !Kingdoms[kingdom].Cities.Contains(city.Index)) Kingdoms[kingdom].Cities.Add(city.Index);
            _civ.MarkCityDirty(city);
        }

        public void KillKingdom(Kingdom k, long tick, EventBus events)
        {
            if (k.Dead) return;
            k.Flags |= MetaFlags.Dead;
            k.DiedTick = tick;
            AliveKingdoms--;
            foreach (var w in Wars)
                if (w.Active && (w.Attacker == k.Index || w.Defender == k.Index)) EndWar(w, w.Attacker == k.Index ? WarResult.DefenderWon : WarResult.AttackerWon, tick, events);
            if (k.Alliance >= 0) LeaveAlliance(k);
            foreach (var a in Armies) if (a.Kingdom == k.Index) a.State = ArmyState.Disbanded;
            events?.Publish(new KingdomFellEvent(k.Index));
        }

        public float TraitPct(Kingdom k, string key)
        {
            float v = 0f;
            foreach (int t in k.Traits) v += Content.KingdomTraits[t].Pct(key);
            return v;
        }

        // ---------------- wars ----------------

        public bool AtWar(int a, int b) => a >= 0 && b >= 0 && a != b && a < _warSize && b < _warSize && _atWar[a * _warSize + b];

        public bool InAnyWar(int k)
        {
            foreach (var w in Wars) if (w.Active && (w.OnAttackerSide(k) || w.OnDefenderSide(k))) return true;
            return false;
        }

        void GrowWarMatrix()
        {
            if (Kingdoms.Count <= _warSize) return;
            int size = math.max(16, math.ceilpow2(Kingdoms.Count));
            _warSize = size;
            _atWar = new bool[size * size];
            RebuildWarMatrix();
        }

        public void RebuildWarMatrix()
        {
            if (_warSize < Kingdoms.Count) { GrowWarMatrix(); return; }
            Array.Clear(_atWar, 0, _atWar.Length);
            foreach (var w in Wars)
            {
                if (!w.Active) continue;
                foreach (int a in w.AttackerSide)
                    foreach (int d in w.DefenderSide)
                    {
                        _atWar[a * _warSize + d] = true;
                        _atWar[d * _warSize + a] = true;
                    }
            }
        }

        public War DeclareWar(int attacker, int defender, int type, long tick, EventBus events)
        {
            var w = new War { Index = Wars.Count, Uid = NextUid++, Type = (ushort)math.max(0, type), Attacker = attacker, Defender = defender, StartTick = tick };
            w.AttackerSide.Add(attacker);
            w.DefenderSide.Add(defender);
            // allies join the side of their member (Bölüm 6.6)
            JoinAllies(w, attacker, w.AttackerSide, defender);
            JoinAllies(w, defender, w.DefenderSide, attacker);
            Wars.Add(w);
            RebuildWarMatrix();
            events?.Publish(new WarDeclaredEvent(w.Index, attacker, defender));
            return w;
        }

        void JoinAllies(War w, int member, List<int> side, int enemy)
        {
            int a = Kingdoms[member].Alliance;
            if (a < 0) return;
            foreach (int k in Alliances[a].Kingdoms)
            {
                if (k == member || side.Contains(k) || Kingdoms[k].Dead) continue;
                if (w.AttackerSide.Contains(k) || w.DefenderSide.Contains(k)) continue;
                if (Kingdoms[k].OpinionOf(enemy) < 0) side.Add(k);
            }
        }

        public void EndWar(War w, WarResult result, long tick, EventBus events)
        {
            if (!w.Active) return;
            w.Result = result;
            w.EndTick = tick;
            foreach (int k in w.AttackerSide) Kingdoms[k].LastWarEndTick = tick;
            foreach (int k in w.DefenderSide) Kingdoms[k].LastWarEndTick = tick;
            foreach (var a in Armies) if (a.War == w.Index && a.State != ArmyState.Disbanded) a.State = ArmyState.Returning;
            RebuildWarMatrix();
            events?.Publish(new WarEndedEvent(w.Index, result));
        }

        // Bölüm 6.5 conquest: the city changes hands; people stay; loyalty starts from 0.
        public void CaptureCity(City city, int to, War war, long tick, EventBus events)
        {
            int from = city.Kingdom;
            MoveCity(city, to);
            city.Loyalty = 0;
            war?.CapturedCities.Add(city.Index);
            var u = _units.Store;
            foreach (int i in city.Residents)
            {
                Happy.Add(u, i, Content.HappinessEvents.IdOrDefault("hap.city_captured"), Content);
                u.ArmyOf[i] = -1;
            }
            events?.Publish(new CityCapturedEvent(city.Index, from, to));
            var k = from >= 0 ? Kingdoms[from] : null;
            if (k != null && k.Capital == city.Index) PickCapital(k);
        }

        // Bölüm 6.5: when the capital falls the largest remaining city takes over.
        public void PickCapital(Kingdom k)
        {
            int best = -1, bestPop = -1;
            foreach (int c in k.Cities)
            {
                var city = _civ.Cities[c];
                if (city.Dead) continue;
                if (city.Population > bestPop) { bestPop = city.Population; best = c; }
            }
            k.Capital = best;
        }

        // ---------------- alliances ----------------

        public Alliance CreateAlliance(int a, int b)
        {
            var al = new Alliance { Index = Alliances.Count, Uid = NextUid++, Leader = a };
            al.Kingdoms.Add(a);
            al.Kingdoms.Add(b);
            al.Name = Kingdoms[a].Root + "-" + Kingdoms[b].Root;
            Kingdoms[a].Alliance = al.Index;
            Kingdoms[b].Alliance = al.Index;
            Alliances.Add(al);
            return al;
        }

        public void LeaveAlliance(Kingdom k)
        {
            if (k.Alliance < 0) return;
            var al = Alliances[k.Alliance];
            al.Kingdoms.Remove(k.Index);
            k.Alliance = -1;
            if (al.Kingdoms.Count <= 1)
            {
                foreach (int o in al.Kingdoms) Kingdoms[o].Alliance = -1;
                al.Kingdoms.Clear();
                al.Dead = true;
            }
            else if (al.Leader == k.Index) al.Leader = al.Kingdoms[0];
        }

        // ---------------- save / load ----------------

        public void Write(System.IO.BinaryWriter w)
        {
            w.Write(NextUid);
            w.Write(Kingdoms.Count);
            foreach (var k in Kingdoms)
            {
                w.Write(k.Uid); w.Write(k.Name ?? ""); w.Write(k.Root ?? ""); w.Write(k.Color.r); w.Write(k.Color.g); w.Write(k.Color.b); w.Write(k.Color.a);
                w.Write(k.Banner.Shape); w.Write(k.Banner.Icon); w.Write(k.Banner.BgColor); w.Write(k.Banner.FgColor); w.Write(k.Banner.Pattern);
                w.Write(k.Species); w.Write(k.FoundedTick); w.Write(k.DiedTick); w.Write(k.FounderUid); w.Write((byte)k.Flags);
                w.Write(k.KingUid); w.Write(k.HeirUid); w.Write(k.Capital); Ints(w, k.Cities); w.Write(k.Alliance); w.Write(k.RulerTitle);
                w.Write(k.Treasury); w.Write(k.LastWarEndTick); w.Write(k.SuccessionTick); w.Write(k.KingChanges);
                w.Write(k.Opinion.Count); foreach (short o in k.Opinion) w.Write(o);
                w.Write(k.Traits.Length); foreach (int t in k.Traits) w.Write(Content.KingdomTraits[t].Id);
                w.Write(k.NextArmyTick);
                w.Write(k.Trade.Count); foreach (short v in k.Trade) w.Write(v);
            }
            w.Write(Wars.Count);
            foreach (var x in Wars)
            {
                w.Write(x.Uid); w.Write(Content.WarTypes.Count > x.Type ? Content.WarTypes[x.Type].Id : ""); w.Write(x.Attacker); w.Write(x.Defender);
                Ints(w, x.AttackerSide); Ints(w, x.DefenderSide); w.Write(x.StartTick); w.Write(x.EndTick); w.Write(x.CasualtiesA); w.Write(x.CasualtiesD);
                Ints(w, x.CapturedCities); w.Write(x.TargetCity); w.Write((byte)x.Result);
            }
            w.Write(Armies.Count);
            foreach (var a in Armies)
            {
                w.Write(a.Kingdom); w.Write(a.War); w.Write(a.HomeCity); w.Write(a.CaptainUid); Ints(w, a.Soldiers);
                w.Write(a.SoldierUids.Count); foreach (long uid in a.SoldierUids) w.Write(uid);
                w.Write(a.RallyTile.x); w.Write(a.RallyTile.y); w.Write(a.TargetCity); w.Write((byte)a.State); w.Write(a.StateTick); w.Write(a.StartSize);
            }
            w.Write(Alliances.Count);
            foreach (var al in Alliances) { w.Write(al.Uid); w.Write(al.Name ?? ""); Ints(w, al.Kingdoms); w.Write(al.Leader); w.Write(al.Dead); }
            w.Write(Caravans.Count);
            foreach (var c in Caravans) { w.Write(c.UnitUid); w.Write(c.Unit); w.Write(c.From); w.Write(c.To); w.Write(c.StartTick); }
            w.Write(Boats.Count);
            foreach (var b in Boats)
            {
                w.Write((byte)b.Type); w.Write((byte)b.State); w.Write(b.Pos.x); w.Write(b.Pos.y); w.Write(b.City); w.Write(b.Kingdom); w.Write(b.Hp);
                Ints(w, b.Passengers); w.Write(b.PassengerUids.Count); foreach (long uid in b.PassengerUids) w.Write(uid);
                w.Write(b.Destination.x); w.Write(b.Destination.y); w.Write(b.Landing.x); w.Write(b.Landing.y);
                w.Write(b.PathHandle); w.Write(b.PathStep); w.Write(b.LaunchTick);
            }
            // per-city loyalty (City lives in the civ section, whose layout stays fixed)
            w.Write(_civ.Cities.Count);
            foreach (var city in _civ.Cities) w.Write(city.Loyalty);
            _units.Store.WriteMeta(w);
        }

        public void Read(System.IO.BinaryReader r)
        {
            Kingdoms.Clear(); Wars.Clear(); Armies.Clear(); Alliances.Clear(); Caravans.Clear(); Boats.Clear();
            AliveKingdoms = 0;
            NextUid = r.ReadInt64();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var k = new Kingdom { Index = i };
                k.Uid = r.ReadInt64(); k.Name = r.ReadString(); k.Root = r.ReadString(); k.Color = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                k.Banner = new Banner { Shape = r.ReadByte(), Icon = r.ReadByte(), BgColor = r.ReadByte(), FgColor = r.ReadByte(), Pattern = r.ReadByte() };
                k.Species = r.ReadUInt16(); k.FoundedTick = r.ReadInt64(); k.DiedTick = r.ReadInt64(); k.FounderUid = r.ReadInt64(); k.Flags = (MetaFlags)r.ReadByte();
                k.KingUid = r.ReadInt64(); k.HeirUid = r.ReadInt64(); k.Capital = r.ReadInt32(); ReadInts(r, k.Cities); k.Alliance = r.ReadInt32(); k.RulerTitle = r.ReadInt32();
                k.Treasury = r.ReadInt32(); k.LastWarEndTick = r.ReadInt64(); k.SuccessionTick = r.ReadInt64(); k.KingChanges = r.ReadInt32();
                int o = r.ReadInt32(); for (int j = 0; j < o; j++) k.Opinion.Add(r.ReadInt16());
                int t = r.ReadInt32(); var traits = new List<int>();
                for (int j = 0; j < t; j++) { int id = Content.KingdomTraits.IdOrDefault(r.ReadString()); if (id >= 0) traits.Add(id); }
                k.Traits = traits.ToArray();
                k.NextArmyTick = r.ReadInt64();
                int tr = r.ReadInt32(); for (int j = 0; j < tr; j++) k.Trade.Add(r.ReadInt16());
                Kingdoms.Add(k);
                if (!k.Dead) AliveKingdoms++;
            }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var x = new War { Index = i };
                x.Uid = r.ReadInt64(); x.Type = (ushort)math.max(0, Content.WarTypes.IdOrDefault(r.ReadString())); x.Attacker = r.ReadInt32(); x.Defender = r.ReadInt32();
                ReadInts(r, x.AttackerSide); ReadInts(r, x.DefenderSide); x.StartTick = r.ReadInt64(); x.EndTick = r.ReadInt64(); x.CasualtiesA = r.ReadInt32(); x.CasualtiesD = r.ReadInt32();
                ReadInts(r, x.CapturedCities); x.TargetCity = r.ReadInt32(); x.Result = (WarResult)r.ReadByte();
                Wars.Add(x);
            }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var a = new Army { Index = i };
                a.Kingdom = r.ReadInt32(); a.War = r.ReadInt32(); a.HomeCity = r.ReadInt32(); a.CaptainUid = r.ReadInt64(); ReadInts(r, a.Soldiers);
                int s = r.ReadInt32(); for (int j = 0; j < s; j++) a.SoldierUids.Add(r.ReadInt64());
                a.RallyTile = new int2(r.ReadInt32(), r.ReadInt32()); a.TargetCity = r.ReadInt32(); a.State = (ArmyState)r.ReadByte(); a.StateTick = r.ReadInt64(); a.StartSize = r.ReadInt32();
                Armies.Add(a);
            }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var al = new Alliance { Index = i };
                al.Uid = r.ReadInt64(); al.Name = r.ReadString(); ReadInts(r, al.Kingdoms); al.Leader = r.ReadInt32(); al.Dead = r.ReadBoolean();
                Alliances.Add(al);
            }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
                Caravans.Add(new Caravan { UnitUid = r.ReadInt64(), Unit = r.ReadInt32(), From = r.ReadInt32(), To = r.ReadInt32(), StartTick = r.ReadInt64() });
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                var b = new Boat { Index = i };
                b.Type = (BoatType)r.ReadByte(); b.State = (BoatState)r.ReadByte(); b.Pos = new float2(r.ReadSingle(), r.ReadSingle());
                b.City = r.ReadInt32(); b.Kingdom = r.ReadInt32(); b.Hp = r.ReadSingle();
                ReadInts(r, b.Passengers); int p = r.ReadInt32(); for (int j = 0; j < p; j++) b.PassengerUids.Add(r.ReadInt64());
                b.Destination = new int2(r.ReadInt32(), r.ReadInt32()); b.Landing = new int2(r.ReadInt32(), r.ReadInt32());
                b.PathHandle = r.ReadInt32(); b.PathStep = r.ReadInt32(); b.LaunchTick = r.ReadInt64();
                Boats.Add(b);
            }
            n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                int loyalty = r.ReadInt32();
                if (i < _civ.Cities.Count) _civ.Cities[i].Loyalty = loyalty;
            }
            _units.Store.ReadMeta(r);
            _warSize = 0;
            GrowWarMatrix();
            RebuildWarMatrix();
        }

        static void Ints(System.IO.BinaryWriter w, List<int> list)
        {
            w.Write(list.Count);
            foreach (int v in list) w.Write(v);
        }

        static void ReadInts(System.IO.BinaryReader r, List<int> list)
        {
            list.Clear();
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) list.Add(r.ReadInt32());
        }
    }
}
