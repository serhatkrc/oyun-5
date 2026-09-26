using PG.Content;
using PG.Core;
using Unity.Mathematics;

namespace PG.Sim
{
    // Bölüm 5.7: smiths make one item per 400 ticks; items go to the city's stock and are handed out monthly.
    public static class Crafting
    {
        public const int MaterialCost = 2;

        public static int Craft(CivState civ, City city, long makerUid, long tick, ref SimRandom rng)
        {
            var db = civ.Content;
            int type = PickType(civ, city, ref rng);
            if (type < 0) return -1;
            int material = PickMaterial(civ, city, db.EquipmentTypes[type]);
            if (material < 0) return -1;
            int res = db.Materials[material].Resource;
            if (res >= 0) city.Stock[res] -= MaterialCost;
            else if (civ.Ids.Wood >= 0 && city.StockOf(civ.Ids.Wood) >= MaterialCost) city.Stock[civ.Ids.Wood] -= MaterialCost;

            int quality = RollQuality(db, 0, ref rng);
            int nameId = -1;
            string qid = db.ItemQualities[quality].Id;
            if (qid == "q.unique" || qid == "q.legendary")
            {
                nameId = civ.Names.Count;
                civ.Names.Add(NameGen.Word(db.NameSets.SetIds.Count > 0 ? db.NameSets.Sets[db.NameSets.SetIds[0]] : new NameSets.Set(), ref rng));
            }
            int item = civ.CreateItem(new ItemData
            {
                Type = (ushort)type,
                Material = (ushort)material,
                Quality = (ushort)quality,
                NameId = nameId,
                CreatedTick = tick,
                MakerUid = makerUid,
                Owner = -1,
                City = city.Index,
            });
            civ.Units?.Events?.Publish(new ItemCraftedEvent(item, city.Index, (ushort)quality));
            return item;
        }

        // What the city lacks: a weapon for every warrior first, then armour, then anything (Bölüm 5.7).
        static int PickType(CivState civ, City city, ref SimRandom rng)
        {
            var db = civ.Content;
            int weapons = 0, armours = 0;
            for (int it = 0; it < civ.Items.Length; it++)
            {
                var item = civ.Items[it];
                if (item.Alive == 0 || item.City != city.Index || item.Owner >= 0) continue;
                if (db.EquipmentTypes[item.Type].SlotCode == EquipSlot.Weapon) weapons++;
                else armours++;
            }
            bool wantWeapon = weapons <= armours;
            int count = 0;
            for (int t = 0; t < db.EquipmentTypes.Count; t++)
            {
                var def = db.EquipmentTypes[t];
                if (!def.UsesMaterial) continue;
                if ((def.SlotCode == EquipSlot.Weapon) == wantWeapon) count++;
            }
            if (count == 0) return -1;
            int pick = rng.Range(0, count);
            for (int t = 0; t < db.EquipmentTypes.Count; t++)
            {
                var def = db.EquipmentTypes[t];
                if (!def.UsesMaterial || (def.SlotCode == EquipSlot.Weapon) != wantWeapon) continue;
                if (pick-- == 0) return t;
            }
            return -1;
        }

        // Highest tier material in stock; wood and bone need no special resource (DECISIONS #56).
        static int PickMaterial(CivState civ, City city, EquipmentTypeDef type)
        {
            var db = civ.Content;
            int best = -1;
            for (int m = 0; m < db.Materials.Count; m++)
            {
                var mat = db.Materials[m];
                bool available = mat.Resource >= 0 ? city.StockOf(mat.Resource) >= MaterialCost
                                                   : civ.Ids.Wood >= 0 && city.StockOf(civ.Ids.Wood) >= MaterialCost;
                if (!available) continue;
                if (best < 0 || mat.Tier > db.Materials[best].Tier) best = m;
            }
            return best;
        }

        // Quality from the baseChance table; each point of craft skill shifts the roll one grade up (Bölüm 5.7).
        public static int RollQuality(ContentDB db, int skill, ref SimRandom rng)
        {
            float total = 0f;
            for (int q = 0; q < db.ItemQualities.Count; q++) total += db.ItemQualities[q].BaseChance;
            float roll = rng.Value01() * total;
            int result = db.ItemQualities.Count - 1;
            for (int q = 0; q < db.ItemQualities.Count; q++)
            {
                roll -= db.ItemQualities[q].BaseChance;
                if (roll < 0f) { result = q; break; }
            }
            return math.min(db.ItemQualities.Count - 1, result + math.max(0, skill));
        }

        // Monthly: unowned items in the city's stock go to residents missing that slot, warriors and guards first.
        public static void HandOut(UnitWorld w, CivState civ, City city)
        {
            var u = w.Store;
            var db = civ.Content;
            for (int it = 0; it < civ.Items.Length; it++)
            {
                var item = civ.Items[it];
                if (item.Alive == 0 || item.Owner >= 0 || item.City != city.Index) continue;
                int slot = (int)db.EquipmentTypes[item.Type].SlotCode;
                int best = -1, bestRank = -1;
                foreach (int i in city.Residents)
                {
                    if (!JobAssigner.CanWork(u, i) || u.Equip[i * UnitStore.EquipSlots + slot] >= 0) continue;
                    int job = u.Job[i] - 1;
                    int rank = job == civ.Ids.JobWarrior ? 3 : job == civ.Ids.JobGuard ? 2 : job == civ.Ids.JobHunter ? 1 : 0;
                    if (rank > bestRank) { bestRank = rank; best = i; }
                }
                if (best < 0) continue;
                u.Equip[best * UnitStore.EquipSlots + slot] = it;
                u.StatsDirty[best] = 1;
                item.Owner = best;
                civ.Items[it] = item;
            }
        }
    }
}
