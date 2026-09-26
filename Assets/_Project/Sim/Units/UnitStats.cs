using PG.Content;
using Unity.Collections;
using Unity.Mathematics;

namespace PG.Sim
{
    // Equipment lookup for the stat formula; implemented by the civ item store (Bölüm 5.7).
    public interface IItemSource
    {
        bool TryGetEffect(int item, out EquipmentTypeDef type, out float multiplier);
    }

    // Bölüm 3.2: final = (base + sum add) * (1 + sum pct / 100), then age stage and level multipliers and floors.
    public static class UnitStats
    {
        public const int MaxLevel = 30;

        public static AgeStage StageFor(SpeciesDef sp, float ageYears)
        {
            float lifespan = sp.BaseStats[(int)StatId.Lifespan];
            if (lifespan <= 0f) return AgeStage.Adult;
            float t = ageYears / lifespan;
            return t < 0.08f ? AgeStage.Baby : t < 0.18f ? AgeStage.Child : t <= 0.8f ? AgeStage.Adult : AgeStage.Elder;
        }

        public static void Recompute(UnitStore u, int i)
        {
            var content = u.Content;
            var sp = content.Species[u.Species[i]];
            int o = i * UnitStore.StatCount;
            var stats = u.Stats;

            // add[] in the output slots, pct accumulated in a stack buffer
            var pct = new FixedList512Bytes<float>();
            for (int s = 0; s < UnitStore.StatCount; s++)
            {
                stats[o + s] = sp.BaseStats[s];
                pct.Add(0f);
            }

            uint flags = (uint)sp.Flags;
            var traits = u.Traits[i];
            if (!traits.IsEmpty)
                for (int t = 0; t < content.UnitTraits.Count; t++)
                {
                    if (!traits.Has(t)) continue;
                    var def = content.UnitTraits[t];
                    flags |= (uint)def.Flags;
                    foreach (var m in def.Mods)
                    {
                        stats[o + (int)m.Stat] += m.Add;
                        pct[(int)m.Stat] += m.Pct;
                    }
                }
            u.BaseFlags[i] = flags;

            for (int s = 0; s < UnitStore.StatusSlots; s++)
            {
                int id = u.StatusId[i * UnitStore.StatusSlots + s] - 1;
                if (id < 0) continue;
                var def = content.StatusEffects[id];
                flags |= (uint)def.Flags;
                foreach (var m in def.Mods)
                {
                    stats[o + (int)m.Stat] += m.Add;
                    pct[(int)m.Stat] += m.Pct;
                }
            }
            u.Flags[i] = flags;

            // equipment (Bölüm 5.7): baseEffects x material x quality
            if (u.Items != null)
                for (int s = 0; s < UnitStore.EquipSlots; s++)
                {
                    int item = u.Equip[i * UnitStore.EquipSlots + s];
                    if (item < 0 || !u.Items.TryGetEffect(item, out var type, out float mul)) continue;
                    for (int k = 0; k < UnitStore.StatCount; k++)
                    {
                        stats[o + k] += type.AddStats[k] * mul;
                        pct[k] += type.PctStats[k] * mul;
                    }
                }

            for (int s = 0; s < UnitStore.StatCount; s++) stats[o + s] *= 1f + pct[s] / 100f;

            switch ((AgeStage)u.Age[i])
            {
                case AgeStage.Baby: Mul(stats, o, StatId.Dmg, 0.3f); Mul(stats, o, StatId.Hp, 0.3f); Mul(stats, o, StatId.Speed, 0.6f); break;
                case AgeStage.Child: Mul(stats, o, StatId.Dmg, 0.6f); Mul(stats, o, StatId.Hp, 0.6f); break;
                case AgeStage.Elder: Mul(stats, o, StatId.Dmg, 0.8f); Mul(stats, o, StatId.Speed, 0.8f); stats[o + (int)StatId.Intel] += 1f; break;
            }

            int level = math.min((int)u.Level[i], MaxLevel) - 1;
            if (level > 0)
            {
                Mul(stats, o, StatId.Hp, 1f + 0.03f * level);
                Mul(stats, o, StatId.Dmg, 1f + 0.02f * level);
            }

            stats[o + (int)StatId.Hp] = math.max(1f, stats[o + (int)StatId.Hp]);
            stats[o + (int)StatId.Armor] = math.max(0f, stats[o + (int)StatId.Armor]);
            stats[o + (int)StatId.Size] = math.max(1f, stats[o + (int)StatId.Size]);
            stats[o + (int)StatId.AtkSpd] = math.max(0.1f, stats[o + (int)StatId.AtkSpd]);
            if (sp.BaseStats[(int)StatId.Speed] > 0f) stats[o + (int)StatId.Speed] = math.max(0.05f, stats[o + (int)StatId.Speed]);
            u.StatsDirty[i] = 0;
        }

        static void Mul(NativeArray<float> stats, int o, StatId s, float f) => stats[o + (int)s] *= f;
    }
}
