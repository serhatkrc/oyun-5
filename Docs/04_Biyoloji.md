# BÖLÜM 4 — Biyoloji

Bu bölüm bittiğinde türler alt türlere ayrılır, genleri ve trait'leri kalıtılır ve mutasyona uğrar, izole popülasyonlar evrimleşir, monolit hayvanları medenileştirir, hastalıklar yayılır ve birimleri başka yaratıklara dönüştürür (zombi salgını dahil).

Bağımlılık: Bölüm 3. İçerik: `species.json`, `subspecies_traits.json`, `unit_traits.json`, `genes.json`, `gene_synergies.json`, `gene_rules.json`, `phenotypes.json`, `evolution_rules.json`, `metamorphoses.json`, `diseases.json`, `zombie_rules.json`.

---

## 4.1 Alt tür (Subspecies) nesnesi

Alt tür bir **meta nesnedir** (ortak meta altyapısı Bölüm 6.1'de; burada yalnızca biyolojik alanlar). Bölüm 6'dan önce yazılacağı için `MetaStore<SubspeciesData>` iskeleti bu bölümde kurulur, Bölüm 6 genişletir.

```csharp
public struct SubspeciesData {
    public ushort Species;              // üst tür
    public int    ParentSubspecies;     // −1 = kök
    public TraitSet256 Traits;          // subspecies_traits.json id'leri
    public GeneGenome Genome;           // 4.4
    public ushort Phenotype;            // phenotypes.json id'si
    public int    PaletteRow;           // render palet satırı (3.10)
    public ulong  DisabledNeurons;      // Beyin paneli maskesi
    public int    OriginIsland;         // izolasyon takibi için
    public int    FoundedYear;
    public byte   MonolithStage;        // 0–3
    public int    Population;           // önbellek, aylık güncellenir
}
```

### Oluşma yolları
| Yol | Kural |
|-----|-------|
| Spawn | Güçle doğan birim, 20 tile içinde aynı türden birim varsa onun alt türünü alır; yoksa türün varsayılan trait setiyle yeni kök alt tür |
| Worldgen | Her tür/ada çifti için bir kök alt tür |
| İzolasyon (4.6) | Ayrı adada 100 yıl yaşayan popülasyon %30 ihtimalle yeni alt tür |
| Biyom uyumu | Farklı biyomda doğan ilk nesil %10 ihtimalle yeni alt tür + biyomun `unitTraitPool`'undan değil, alt tür havuzundan 1 trait |
| Monolit (4.7) | Aşama atladığında |
| Editör | Oyuncu değiştirince `sst.divine_breeding` işareti eklenir |
| `law.mutant_box` | Her yeni alt türe 1–4 rastgele (açılmış) trait |

### Varsayılan trait setleri
Her tür için `species_defaults` tablosu (kodda değil, `species.json`'a eklenecek `defaultSubspeciesTraits` alanı; eklenene kadar aşağıdaki kurallarla türetilir):
- Diyet: `herb` → `sst.herbivore`, `carn` → `sst.carnivore`, `omni` → `sst.omnivore`, `brains` → `sst.brain_eater`.
- Üreme: `live` → `sst.live_birth`, `egg` → `sst.egg_laying`, `split` → `sst.fission`, `spore` → `sst.spore_birth`.
- Habitat: `amph` → `sst.amphibious`, `water` → `sst.aquatic`, `air` → `sst.flying`, `under` → `sst.burrowing`.
- Boy: `size` 1–5 → `sst.size_tiny` … `sst.size_huge`.
- Medeni türler (`civ`): ek olarak `sst.sapience`, `sst.tool_use`, `sst.speech_center`, `sst.advanced_memory`, `sst.abstract_thought`, `sst.planning`, `sst.fire_makers`.

### Yetenek bayrakları
Alt tür trait'lerindeki `flag:` anahtarları birimlerin yapabileceklerini belirler ve Bölüm 5–6 bunlara bakar:

| Bayrak | Gerekli trait | Açtığı şey |
|--------|---------------|-----------|
| `sapient` | `sst.sapience` | Şehir kurma, krallık |
| `can_use_items`, `can_build` | `sst.tool_use` | Ekipman, bina |
| `can_hold_culture` | `sst.advanced_memory` | Kültür üyeliği ve yayılımı |
| `can_speak`, `can_hold_language` | `sst.speech_center` | Dil, isimler, kitap |
| `can_hold_religion` | `sst.abstract_thought` | Din |
| `can_plot` | `sst.planning` | Plot başlatma/katılma |

---

## 4.2 Trait setleri

`TraitSet256`: 4 × `ulong`. `Has(id)`, `Add(id)`, `Remove(id)`, `Count()`, `ForEach`. Birim trait'leri (`unit_traits.json`, 118 adet) ve alt tür trait'leri (200 adet) ayrı kümelerdir. 256'yı aşarsa küme 8 × `ulong`'a genişletilir (tip değişikliği tek yerde).

**Zıtlık kuralı:** `opposite` alanı olan trait eklenirken zıttı varsa önce zıttı kaldırılır.

---

## 4.3 Birim trait kalıtımı (doğumda)

```
aday kümesi = anne.Traits ∪ baba.Traits
her aday t için:  rng.Chance(t.inheritChance) → ekle
yeni trait:        %3 ihtimalle nadirlik ağırlıklı rastgele (N 70, R 22, E 7, L 1); yalnızca keşfedilmiş veya doğal ortaya çıkabilen
biyom trait'i:     ilk nesil ise %20 ihtimalle biyomun unitTraitPool listesinden bir tane
ışınlanmış ebeveyn: yeni trait şansı ×5
en fazla 8 trait; fazlası nadirliği düşük olandan atılır
```
`inheritChance = 0` olan trait'ler (ör. `tr.infertile`, kazanılan trait'ler) kalıtılmaz.

**Kazanılan trait'ler** koşulla eklenir: `tr.veteran` (10 öldürme), `tr.kingslayer` (kral öldürme), `tr.survivor` (afetten sağ çıkma), `tr.hero` (100 öldürme + seviye 20), `tr.scarred` (can %10'un altına düşüp hayatta kalma), `tr.lame`/`tr.one_eye` (ağır yara %5).

**Keşif kancası:** Bir trait dünyada ilk kez doğal yoldan ortaya çıktığında `TraitDiscoveredEvent` yayınlanır (Bölüm 8.9 keşif sistemi).

---

## 4.4 Genler

### Veri
Genom alt tür düzeyindedir (tüm üyeler paylaşır). `gene_rules.json`:
- Kromozom sayısı varsayılan 2 (en fazla 4), kromozom başına 6 slot (en fazla 10).

```csharp
public struct GeneGenome { public byte ChromosomeCount, SlotsPerChromosome; public FixedList64Bytes<ushort> Slots; } // gene id, 0 = boş dizi
```

### Etki hesabı
1. Her slottaki genin efekti toplanır (3.2 formülündeki "genler" kaynağı).
2. **Sinerji:** `gene_synergies.json`'daki her sinerji için, gerekli genler aynı kromozomda **ardışık slotlarda (sıra fark etmez)** duruyorsa bonus eklenir.
3. `gene.chimera`: doğumda %5 rastgele birim trait'i.

### Mutasyon ve kayma
- Her yıl alt tür başına `0.02 × mutasyonÇarpanı` ihtimalle bir mutasyon işlemi: `swap_gene` (iki slotu değiştir), `replace_random` (rastgele gen), `duplicate` (komşu slota kopya), `delete_to_empty`.
- `mutasyonÇarpanı` = `law.mutation` kaydırıcısı × (`sst.mutable` 2 / `sst.stable_genome` 0.2) × (`era.glimmer` 1.5) × (ışınlanmış üye oranı × 5 + 1).
- Birikmiş mutasyon sayısı 5'i aşan ve nüfusu 30+ olan alt tür, yıllık %20 ihtimalle ikiye bölünür: rastgele yarısı yeni alt türe geçer.
- `law.gene_chaos`: her doğumda genom karıştırılır (alt türün kopyası yerine birim bazında geçici genom — bu kanun açıkken genom birim düzeyine iner; `GeneGenome` birim deposunda opsiyonel alan olarak tutulur).

---

## 4.5 Fenotip ve renk

- Alt tür oluşurken fenotip seçimi: doğduğu biyomun `biomeBias` içerdiği fenotipler ×4 ağırlık, diğerleri ×1.
- **Palet üretimi:** `baseColor` → HSL. 8 gri seviyeye karşılık 8 renk: açıklık 0.15'ten 0.85'e doğrusal, doygunluk sabit. Desen (`plain`, `spotted`, `striped`, `ringed`, `shaded`) sprite'ta ayrı bir maske katmanıyla uygulanır (shader desen dokusunu 4×4 tekrar eder, `shaded` için dikey gradyan).
- Palet satırları `PaletteAtlas` (256 satır) içinde havuzlanır; alt tür ölünce satır serbest kalır.

---

## 4.6 İzolasyon ve doğal evrim

Yıllık `EvolutionSystem`:
```
her alt tür s için (nüfus ≥ 20):
    ada dağılımını çıkar (üyelerin IslandId sayımı)
    s.OriginIsland dışındaki bir adada ≥ 20 üye varsa:
        izolasyonYılı[s, ada]++
        izolasyonYılı ≥ 100 ve rng.Chance(0.3) → o adadaki üyelerle yeni alt tür
             yeni alt türe 1–2 rastgele alt tür trait'i (biyom havuzundan öncelikli)
```
`law.evolution` kapalıysa çalışmaz.

---

## 4.7 Monolit

Monolit bir dünya nesnesidir (`MonolithData{tile, radius=20, nextPulseYear}`), `pw.monolith` ile yerleştirilir.

Her 20 yılda bir **nabız**:
```
yarıçaptaki her evrimleşebilir (canEvolve) alt tür için:
    aşama = s.MonolithStage + 1 (en fazla 3)
    evolution_rules.json'daki evo.monolith_stage{aşama}.grants trait'lerini ekle
    aşama 3'te: tür medeni sayılır → yarıçaptaki üyelerin etrafında ilk şehir kurulma denemesi (Bölüm 5)
                    ilk kez olan tür için ach.evolution
    görsel: monolit parlar, birimler üzerinde ışık partikülü
```
Bir monolitin etkilediği alt türe `sst.monolith_touched` eklenir.

---

## 4.8 Hastalıklar

### Veri
Birim başına tek aktif hastalık (`Disease`, `DiseaseProgress` 0–65535). İkinci hastalık birincinin yerine geçmez (bağışıklık sistemi zayıf olsa bile).

### Bulaşma yolları (`spread` alanı)
| Yol | Mekanik |
|-----|---------|
| `contact` | Her ay, hasta birimin 1,5 tile yakınındaki her birime `contagion × çarpan` şans |
| `bite` | Hasta birimin her vuruşunda `contagion` şans (zombide `bitePerHit` 0.35) |
| `rat` | `sp.rat` ve `tr.plague_bearer` taşıyıcıdır; kendileri hastalanmaz, temasla bulaştırır |
| `cloud` | Veba sisi / çürük bulutu damlası (Bölüm 2.7) |
| `air` | Sadece biyom kaynaklı (mantar) — o biyomdaki birimlere aylık küçük şans |
| `mosquito` | `sp.mosquito_swarm` ısırığı |
| `magic` | Güç veya büyü |

Çarpan = `(1 − diseaseResist)` × biyom (bataklık 1.5) × çağ (`era.pale` 2) × şehir kalabalığı (şehirde yaşıyorsa `1 + nüfus/200`, en fazla 3). `immune:<hastalık>` veya `tr.immune` → 0.

### Seyir (aylık)
```
progress += 65535 / durationMonths
ölüm şansı (aylık) = lethality / max(1, durationMonths) × (şifahane varsa 0.5) × (şifacı tedavisi 0.5)
iyileşme şansı = 0.1 × (1 + diseaseResist) × (Turunç Bahçesi +0.1)
progress dolarsa: transformsInto varsa dönüşüm (4.9), yoksa iyileşir ve 5 yıl bağışık olur
```
Kutsal ışık (güç) veya `spell.cure_rot` / `spell.heal` (hastalığa %20) iyileştirir.

### Salgın takibi
Dünya genelinde hastalık başına aktif vaka sayısı istatistiğe yazılır (grafik + `ach.plague_lord`).

---

## 4.9 Dönüşümler ve zombi salgını

### Genel dönüşüm
`metamorphoses.json`: `from` (tür id'si veya `*` joker; `*animal`, `*size>=4` gibi filtreler), `to`, `trigger` (`age:x` yıl, `dis_*` hastalık sonu, `era.*` çağ, `merge:n`). Dönüşümde birim **yeni türde yeniden doğar**: aynı konum, `Uid` korunur, isim korunur, can yeni türün tam canı, trait'ler korunur (türe uymayanlar silinir), `TransformedEvent{uid, fromSpecies, toSpecies}`.

### Zombi kuralları (`zombie_rules.json`)
- **Isırık:** Zombi vuruşunda %35, öldürücü vuruşta %90 `dis_rotbite`. Hasta birime `st.turning` (2 ay).
- **Dönüşüm varyantı:** boy ≥ 4 → `sp.zombie_brute`; ejderha → `sp.zombie_dragon`; hayvan → `sp.zombie_beast` (kendi sprite'ı, zombi paleti, istatistik ×0.8, hız ×0.7); diğerleri %25 `sp.zombie_runner`, %65 `sp.zombie`, %10 `sp.zombie_bloater`.
- **Ölü kalkışı:** Hasta birim ölürse 40 tick sonra cesetten kalkar. `law.zombie_apocalypse` açıksa **her** ceset (hastalıksız da) %100 kalkar; kapalıyken yalnızca hastalıklılar. Şehrinde `bld.pyre` veya `cul.corpse_burners` varsa cesetler yakılır ve kalkış olmaz.
- **Meta kaybı:** Dönüşen birim tüm meta'larını bırakır, `tr.hollow` alır. İnceleme penceresi eski kimliğini gösterir.
- **İhtiyaç yok:** Zombiler yemez, uyumaz, çoğalmaz.
- **Nöronlar:** `hunt_living` (görüş 10, gece +3), `follow_horde` (5+ zombi), `wander_to_noise` (savaş/patlama 25 tile), `attack_buildings` (içinde canlı varsa), `lure_golden_brain` (altın beyin varsa en yüksek puan).
- **Sürü:** 50+ zombi kümesi (`HordeSystem`, aylık kümeleme — zone bazlı bağlı bileşen) en yakın şehri hedef seçer; kümedeki tüm zombilerin hedef tile'ı o şehrin merkezi olur. 500 zombide bir `sp.zombie_lord` doğar ve sürünün merkezinde kalır.
- **Zayıflıklar:** Ateş ×2, kutsal ×3, gümüş ×1.5 hasar; `era.scorch` sırasında her 60 tick −1 can; sürünen hariç derin suda boğulurlar.
- **Çürüme:** `law.zombie_decay` açıksa zombi 10 yılda kemik yığınına döner; koşucu 3 yılda sürünen zombiye.
- **Şişkin:** Ölünce yarıçap 3 çürük bulutu damlası (tek seferlik) — `dis_rotbite` şansı %50.

---

## 4.10 Testler ve kabul kriterleri

| # | Test | Kriter |
|---|------|--------|
| 1 | Trait kalıtımı, 10.000 doğum simülasyonu | Gözlenen oranlar `inheritChance`'in ±%2'si |
| 2 | Zıt trait ekleme | Zıttı otomatik kalkar |
| 3 | Gen sinerjisi | Ardışık genlerde bonus var, araya boş gen girince yok |
| 4 | İki adaya bölünmüş kurt popülasyonu, 300 yıl | En az bir yeni alt tür (5 seed'in 3'ünde) |
| 5 | Monolit + 30 maymun, 60 yıl | Maymunlar medeni olur ve şehir kurma denemesi yapar (Bölüm 5 varsa) |
| 6 | 300 kişilik şehirde veba | Vaka eğrisi yükselir ve düşer; nüfusun %20–60'ı etkilenir |
| 7 | 1 zombi, 200 kişilik surlu olmayan köy | 5 yıl içinde köy düşer (law varsayılan) |
| 8 | Aynı senaryo, `cul.corpse_burners` | Salgın yavaşlar; ölüler kalkmaz |
| 9 | Dönüşüm kimliği | Dönüşen birimin `Uid` ve ismi korunur |

## 4.11 Görev listesi

| # | Görev | Süre |
|---|-------|------|
| 1 | `MetaStore` iskeleti + `SubspeciesData`, oluşma yolları | 2 gün |
| 2 | `TraitSet256`, zıtlık, kazanılan trait'ler | 1 gün |
| 3 | Kalıtım + keşif kancası | 1 gün |
| 4 | Gen genomu, sinerji, mutasyon, bölünme | 2,5 gün |
| 5 | Fenotip ve palet atlası | 1,5 gün |
| 6 | İzolasyon evrimi + monolit | 2 gün |
| 7 | Hastalık sistemi (bulaşma, seyir, tedavi) | 2,5 gün |
| 8 | Dönüşüm çatısı + zombi kuralları + sürü | 3 gün |
| 9 | Testler | 1,5 gün |
| | **Toplam** | **~17 gün** |
