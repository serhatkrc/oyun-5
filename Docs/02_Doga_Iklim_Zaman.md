# BÖLÜM 2 — Doğa, İklim ve Zaman

Bu bölüm bittiğinde dünya "canlı" görünür: biyomlar yayılır, ağaçlar büyür ve yanar, bulutlar geçip yağmur/kar/asit yağdırır, lav akar ve soğur, mevsimler ve çağlar değişir, otomatik afetler tetiklenir. Henüz birim yoktur (Bölüm 3).

Bağımlılık: Bölüm 1 (WorldMap, SimContext, EventBus, ContentDB, SimRandom). İçerik dosyaları: `biomes.json`, `features.json`, `clouds.json`, `eras.json`, `disasters.json`, `world_laws.json`.

---

## 2.1 İçerik tanım sınıfları

JSON alanları birebir C# sınıflarına eşlenir (Newtonsoft, `[JsonProperty]`). Yükleme sonrası metin referansları sayısal id'lere çözülür (`Resolve(ContentDB db)`).

```csharp
public sealed class BiomeDef : IContentDef {
    public string Id, Name, Effect, Description;
    public bool Special; public int GrowStrength; public float Temp, Moisture;
    public string[] Trees, Plants, Animals, LifeCloudCivs, UnitTraitPool, CulturePool, LanguagePool, ReligionPool;
    public string[] GroundColors;
    // çözülmüş:
    [JsonIgnore] public ushort[] TreeIds, PlantIds; [JsonIgnore] public int[] AnimalSpeciesIds;
    [JsonIgnore] public Color32[] Colors;
}
public sealed class FeatureDef : IContentDef { public string Id, Name, Kind, Note; public Dictionary<string,int> Yields; public bool Burnable; }
public sealed class CloudDef   : IContentDef { public string Id, Name, Effect; }
public sealed class EraDef     : IContentDef { public string Id, Name, Effects, Tint; public int MinYears, MaxYears, Rate, LoyaltyBonus, OpinionBonus, FertilityPct, TempShift, CloudIntervalMonths, BiomeGrowthBonus; }
public sealed class DisasterDef: IContentDef { public string Id, Name, Description; public int MinWorldAge, MinPopulation, CooldownYears; public float ChancePerYear; public Dictionary<string,float> EraMultipliers; }
```

**Davranış kodu:** JSON'daki `effect` alanı oyuncuya gösterilen metindir. Davranış, id ile eşlenen kod tablolarında durur (`BiomeEffectRegistry`, `CloudEffectRegistry`, `EraEffectRegistry`). Yeni bir davranış eklemek = tabloya bir satır + JSON'a açıklama. Mod'lar yalnızca var olan davranış kodlarını yeniden kullanabilir.

---

## 2.2 Genel doğa değiştiricileri (NatureModifiers)

Tüm doğa sistemleri tek bir global yapıyı okur. Çağ, mevsim ve dünya kanunları bu yapıyı ay başında günceller.

```csharp
public struct NatureModifiers {
    public float TempShift;          // çağ + mevsim, °C
    public float BiomeGrowthMul;     // 1 + era.BiomeGrowthBonus * 0.25 ; buz/kül çağında 0
    public float FireSpreadMul;      // kavurucu güneş 2, yağmur çağı 0.3
    public float PlantGrowthMul;     // yağmur çağı 1.5, kül çağı 0
    public float CloudIntervalMonths;
    public bool  GlobalRain;         // yağmur çağı
    public float FertilityMul;       // Bölüm 3 okur
}
```

---

## 2.3 Biyom yayılımı (BiomeSpreadSystem)

**Faz:** World. **Kanun:** `law.biome_spread`.

### Algoritma (zaman dilimli)
Her tick `SamplesPerTick = clamp(tileCount / 2000, 64, 1024)` adet rastgele kara tile'ı seçilir:

```
t = rastgele tile;  b = Biome[t];  if b == 0 → atla
n = rastgele 4-komşu;  if !canHaveBiome(n) → atla
nb = Biome[n]
if nb == b → atla
if nb == 0:            başarı şansı = 0.05 * BiomeGrowthMul          // boş toprağa yayılma
elif grow(b) == grow(nb): başarı şansı = 0.05 * BiomeGrowthMul
else:                  a = rng.Range(0, grow(b)+1); d = rng.Range(0, grow(nb)+1); başarı = a > d
başarı → SetBiome(n, b, ChangeSource.Nature)
```

- Büyüme gücü değerleri `biomes.json` → `growStrength` (5 veya 6).
- Titanik haritada ×1 hızda yaklaşık 20 örnek/tick × 20 tick = saniyede 20.000 deneme: biyom sınırları dakikalar içinde gözle görülür şekilde kayar ama patlamaz.
- `BiomeGrowthMul == 0` ise sistem hiç çalışmaz.

### Tohum gücü
`pw.seed_<biome>` düştüğü noktada yarıçap 2 alanı hemen boyar ve oraya bir **Tohum** kaydı bırakır (`SeedData{tile, biome, remainingYears=5}`). Tohum aktifken her tick etrafında yarıçap 6 içinde ek 4 deneme yapar ve başarı şansı 3 kat olur. Böylece ekilen biyom kısa sürede kök salar.

### Biyom efektleri (kod tablosu)
| Biyom | Kod | Davranış |
|-------|-----|----------|
| `bio.infernal` | `RandomFire` | Ayda zone başına %2: rastgele tile'da ateş; bu biyomun tile'ları yanmaz (`Burnable=false` geçersiz kılar) |
| `bio.swamp` | `DiseaseBoost` | Bölüm 4 hastalık bulaşma ×1.5 |
| `bio.desert` | `Evaporate` | Yılda bir, çöle komşu sığ su tile'ı %3 ihtimalle kuma döner; yiyecek verimi ×0.5 |
| `bio.rocklands` | `OreBoost` | Maden damarı çıkma ×2 |
| `bio.tundra`, `bio.snowpine` | `Cold` | Zone sıcaklığı −8 °C; `SnowCover` kalıcı |
| `bio.flower` | `Happy` | İçindeki birimlere +2 mutluluk (Bölüm 5) |
| `bio.clover` | `Luck` | Şans +5 |
| `bio.mushroom` | `Spores` | Yılda %1 spor hastalığı başlangıcı |
| `bio.crystal` | `ManaBoost` | Mana +%20 |
| `bio.enchanted` | `Healing` | İyileşme ×2 |
| `bio.corrupted` | `Corruption` | Mutluluk −3; ayda %0.5 delilik |
| `bio.garlic` | `Repel` | `sp.bloodsucker` giremez (yol bulma maliyeti = geçilmez) |
| `bio.celestial` | `Holy` | Yıldırım düşmez; kutsama şansı |
| `bio.ash` | `NoPlants` | Bitki büyümez |
| `bio.timewarp` | `Aging` | Adım atan birime %10 ihtimalle +1 yaş |
| `bio.void` | `ManaVoid` | Mana yenilenmez |
| `bio.coral` | `FishBoost` | Sadece sığ suya uygulanabilir; balık ×3 |
| `bio.volcanic` | `LavaBubble` | Yılda zone başına %1 küçük lav baloncuğu |
| `bio.bone` | `NightSkeleton` | Gece (ay içi ikinci yarı) %0.2 iskelet doğuşu |
| `bio.honey` | `FoodBoost` | Yiyecek +%30 |

Birimlere etki eden efektler (mutluluk, mana, yaşlanma) Bölüm 3'teki sistemler tarafından `Biome[tile]` okunarak uygulanır; bu tablo tek kaynak olarak `BiomeEffectRegistry` içinde yaşar.

---

## 2.4 Ağaç, bitki ve damarlar (Feature sistemi)

### Veri
Bölüm 1'deki `Feature` (ushort, feature id + 1; 0 = yok) dizisine ek olarak:

| Dizi | Tip | Anlam |
|------|-----|-------|
| `FeatureState` | `byte` | Bit 0–1: büyüme evresi (0 fidan, 1 genç, 2 olgun, 3 yaşlı); bit 2–7: kalan kaynak miktarı (0–63) |

### Büyüme
`FeatureGrowthSystem` her tick `SamplesPerTick` kadar rastgele tile inceler:

| Durum | Kural |
|-------|-------|
| Feature var, evre < 3 | `PlantGrowthMul * 0.02` ihtimalle evre +1 |
| Feature yok, tile biyomlu, `law.tree_growth` açık | Biyomun `trees` listesinden: `0.002 * PlantGrowthMul * yoğunlukCarpanı` ihtimalle fidan; `plants` listesinden `0.004` ihtimalle bitki |
| Zone yoğunluk sınırı | Zone başına en fazla 24 ağaç + 16 bitki (sayaç `ZoneData.FeatureCount`) |
| Tile su olursa | Kara feature'ları silinir (`feat.coral` ve `feat.reed` hariç) |
| Tile ateş alırsa | Yanabilir feature → `feat.tree_dead`, ikinci yanışta silinir |
| Kaynak 0'a inerse | Ağaç: kütük kalır, 2 yıl sonra silinir. Çalı: 1 yıl sonra yeniden dolar. Damar: silinir |

Ekin (`feat.wheat_crop`) yalnızca `tile.field` üzerinde yaşar ve Bölüm 5'teki çiftçiler tarafından ekilip biçilir.

### Render
Feature'lar sprite olarak çizilir (Bölüm 1.11 katman 10). Chunk başına `FeatureRenderBatch` (konum + sprite indeksi + evre) tutulur; chunk `Dirty.Render` olduğunda yeniden kurulur. Görünmeyen chunk'lar çizilmez. LOD 3'te feature'lar çizilmez; zemin rengi biyom rengini zaten taşır.

---

## 2.5 Sıcaklık ve mevsimler

### Zone sıcaklığı (aylık)
```
T(zone) = baseTemp(zone)              // worldgen sıcaklık alanının zone ortalaması → −20..40 °C'ye ölçeklenir, kayıtta saklanır
        + mods.TempShift               // çağ.tempShift + mevsim
        + biyomEtkisi                   // tundra −8, infernal +15, volcanic +8
        + lavYakınlığı                  // zone içinde lav tile'ı varsa +10
        + yangınEtkisi                  // zone içinde yanan tile sayısı × 0.2 (en fazla +10)
Mevsim   = 10 * sin(2π * (ay / 12)) * enlemÇarpanı      // law.seasons kapalıysa 0; ekvatorda 0.3, kutuplarda 1
```
`ZoneData.TemperatureC` (short) bu değeri tutar. Hesap tek Burst job'ıdır, ay başında çalışır.

### Eşikler
| Koşul | Sonuç |
|-------|-------|
| T < −5 ve tile sığ su | Ay başına %20 → `Frozen` bayrağı + `tile.ice` |
| T > 2 ve `tile.ice` | %30 → sığ suya döner |
| T < 0 ve (kar bulutu veya tundra/karlı biyom) | `SnowCover = true` |
| T > 5 | `SnowCover` kaldırılır (%50/ay) |
| T > 35 | Birimlere sıcaklık hasarı (Bölüm 3; `immune:heat` hariç) |
| T < −10 | Soğuk hasarı (`immune:cold` hariç) |
| `law.eternal_summer` | Mevsim terimi +10 sabit |
| `law.eternal_winter` | Mevsim terimi −15 sabit |

---

## 2.6 Rüzgâr

Global bir vektör: `WindDir` (açı) ve `WindSpeed` (0–1). Her ay açı ±20° rastgele yürür, hız 0.2–0.8 arasında salınır. Bulutlar ve yangın yayılımı rüzgârı kullanır.

---

## 2.7 Bulutlar

### Veri
```csharp
public struct CloudData {
    public float2 Pos;  public float Radius;      // 6–14 tile
    public ushort Type;  public int LifetimeTicks; // 600–2400
    public int NextDropTick;
}
```
`CloudStore` basit bir `NativeList<CloudData>`; en fazla 64 bulut.

### Doğuş
- Her `CloudIntervalMonths` ayda (çağdan gelir) rastgele bir su zone'unun üstünde 1–3 bulut. Tip seçimi: sıcaklık < 0 ise kar, aksi hâlde %85 yağmur, %10 fırtına, %5 kül (kül çağında %40).
- Özel bulutlar (asit, lav, yaşam, kutsal, veba, çürük, şeker) yalnızca güçlerle veya afetlerle doğar.
- `law.clouds` kapalıysa doğal doğuş olmaz.

### Hareket ve yağış
- Konum her tick `WindDir * WindSpeed * 0.05` tile ilerler.
- Her 4 tick'te bir, yarıçap içinde `Radius * 0.6` adet rastgele tile'a **damla** uygulanır.

| Bulut | Damlanın etkisi |
|-------|-----------------|
| `cloud.rain` | Yangın söner, lav bir evre soğur, bitki büyüme denemesi, birimlere `st.wet` |
| `cloud.snow` | `SnowCover`, sığ suya donma denemesi |
| `cloud.acid` | %15 ihtimalle tile seviyesi −1 (`LowerLevel`), birime 5 hasar, feature silinir |
| `cloud.lava` | %5 ihtimalle `tile.lava_hot` |
| `cloud.life` | %2 ihtimalle biyomun hayvanı; `law.life_cloud_civs` açıksa `lifeCloudCivs` listesinden medeni birim |
| `cloud.storm` | %1 yıldırım (Bölüm 7 yıldırım işleyicisi) |
| `cloud.ash` | Zone sıcaklığı −5, bitki büyümesi durur (zone bayrağı 1 ay) |
| `cloud.blessing` | Birime `st.blessed` |
| `cloud.plague` | Birime veba bulaşma denemesi (Bölüm 4) |
| `cloud.rot` | Birime çürük ısırık denemesi (Bölüm 4) |
| `cloud.candy` | %3 `feat.tree_candy` fidanı; birime +10 doygunluk |

`GlobalRain` açıkken (yağmur çağı) bulutlara ek olarak her tick haritada 32 rastgele tile yağmur damlası alır.

### Render
Bulutlar yarı saydam piksel lekeleri olarak katman 60'ta çizilir; gölgesi yerde katman 29'da (bulut konumundan +3 tile güneydoğuya kaymış). LOD 3'te gizlenir.

---

## 2.8 Yangın

### Veri
`Fire[tile]` şiddettir (0–255). Aktif yangınlar `NativeHashSet<int> BurningTiles` içinde tutulur; sistem sadece bu kümeyi dolaşır (tüm haritayı değil).

### Tick (her 2 tick'te bir)
```
her yanan tile t için:
    yakıt = (Feature[t] yanabilir ? 2 : 0) + (Building[t] >= 0 ? 3 : 0) + (tile yanabilir/tarla ? 1 : 0)
    Fire[t] += yakıt*6 − 10                   // yakıt yoksa söner
    Fire[t] = min(Fire[t], 255)
    if Fire[t] <= 0 → söndür (Fire=0, Burning=false); yanan şey varsa tile → tile.scorched
    her 4 komşu n için:
        yanabilirse ve yanmıyorsa:
           p = 0.08 * FireSpreadMul * (1 + 0.8 * dot(rüzgâr, yön(n−t))) * (Fire[t]/255)
           rng.Chance(p) → tutuştur(n, 80)
    Building[t] varsa → binaya 4 hasar (Bölüm 5)
    Feature ağaçsa → kaynak −1; 0 olunca kuru ağaç
    tile üzerindeki birimler → st.burning (Bölüm 3)
```
- Su, buz, kum, dağ ve zirve tile'ları yanmaz. Yağmur damlası `Fire[t] = 0` yapar.
- `law.fire_spread` kapalıysa komşuya yayılma yapılmaz (tile kendi yakıtını tüketip söner).
- Tek anda aktif yangın üst sınırı: 50.000 tile. Aşılırsa yeni tutuşmalar reddedilir.
- Yanık toprak (`tile.scorched`) 300–900 tick sonra `tile.soil_low`'a döner (2.10).

### Render
Yanan tile rengi Bölüm 1.11.2'deki ateş paletinden gelir; ayrıca yanan tile'ların %10'unda duman partikülü (havuzlu, en fazla 2.000).

---

## 2.9 Lav

- Aktif lav tile'ları `NativeHashMap<int, ushort> LavaTimers` içinde (tile → kalan tick).
- `tile.lava_hot` 300 tick → `lava_mid`; `lava_mid` 400 tick → `lava_cool`; `lava_cool` 600 tick → çevresinin ortalama seviyesine göre `tile.hills` veya `tile.mountain`. `law.lava_cooling` kapalıysa zamanlayıcılar durur.
- **Akış:** Her 6 tick'te `lava_hot` ve `lava_mid` tile'ı, seviyesi kendisinden düşük (veya su olan) rastgele bir komşuya %25 ihtimalle akar. Suya akarsa ikisi de `tile.lava_cool` olur ve buhar partikülü çıkar.
- Lav tile'ına giren birim anında `st.burning` ve 20 hasar/tick alır (`immune:heat` hariç). Komşu yanabilir tile'lar tutuşur.
- `lava_cool` çevresinde (%2) `feat.ore_obsidian` oluşur.

---

## 2.10 Yavaş dönüşümler (TileRecoverySystem)

Her tick 256 rastgele tile örneklenir:

| Tile | Kural |
|------|-------|
| `tile.scorched` | 1/600 ihtimalle `soil_low` (biyomsuz) |
| `tile.wasteland` | 1/5000 ihtimalle `soil_low`; `Irradiated` bayrağı 1/3000 ile kalkar |
| `tile.pit` | Komşusu su ise 1/50 → sığ su; aksi hâlde yağmur damlasıyla 1/20 |
| `tile.goo` | `law.goo_spread` açıksa her tick ek 16 goo tile'ı işlenir: rastgele komşu → goo (su hariç); kapalıysa goo durağandır |

---

## 2.11 Takvim ve olay kancaları

`GameClock` ay/yıl başında şu olayları yayınlar: `MonthStartedEvent`, `YearStartedEvent`. Aylık ve yıllık sistemler kendi tick'lerinde `ctx.Clock.IsMonthStart` kontrol etmek yerine bu olaylara abone olmaz; **Tick içinde saat kontrolü yapar** (olaylar bir sonraki tick'te dağıtıldığı için gecikme istenmez). Olaylar yalnızca UI ve tarihçe içindir.

---

## 2.12 Çağlar (EraSystem)

### Veri
```csharp
public sealed class EraState {
    public ushort CurrentEra;
    public int    StartedYear, DurationYears;     // min–max arasında rastgele
    public ushort[] ClockSlots = new ushort[8];   // 0 = boş slot
    public int    CurrentSlot;
    public bool[] Enabled;                        // era id → açık mı
    public bool   Frozen;                         // oyuncu çağı sabitledi
    public float  TintBlend;                      // 0..1 geçiş
}
```

### Çağ saati
- 8 slotlu dairesel sıra. Yeni haritada slotlar varsayılan sırayla dolar: Şafak, Hasat, Yağmur, Kan, Gölge, Işıltı, Kavurucu Güneş, Buz (Solgun ve Kül boş slot seçiminde havuzdan gelir).
- Çağ biterken: `CurrentSlot = (CurrentSlot+1) % 8`. Slot doluysa ve o çağ açıksa o seçilir. Slot boşsa **açık çağlar havuzundan `rate` ağırlıklı** seçim yapılır; aynı çağ arka arkaya gelmez.
- Oyuncu slotlara çağ sürükleyebilir, çağı anında değiştirebilir (`EraSystem.ForceEra(id)`), süreyi sabitleyebilir (`Frozen`).

### Uygulama
Çağ değişince `NatureModifiers` ve Bölüm 5–6'nın okuduğu `SocietyModifiers` (sadakat, fikir, savaş şansı çarpanları) güncellenir. Ekran rengi `era.tint` değerine 2 oyun yılı boyunca `TintBlend` ile geçer (shader `_EraTint`). Çağa özel davranışlar `EraEffectRegistry` tablosundadır:

| Çağ | Kod davranışı |
|-----|---------------|
| `era.dawn` | Savaşta barış şansı +%30 (Bölüm 6) |
| `era.scorch` | `FireSpreadMul=2`, kardan varlıklar hasar alır, zombiler çürür |
| `era.rain` | `GlobalRain=true`, `PlantGrowthMul=1.5`, `FireSpreadMul=0.3` |
| `era.ice` | `BiomeGrowthMul=0`, ateş elementalleri hasar alır |
| `era.shadow` | Canavar doğuşu ×3, `bio.bone` iskelet doğuşu ×5 |
| `era.blood` | Savaş/isyan şansı ×2, plot ilerlemesi +%50 |
| `era.glimmer` | Büyü gücü +%50, yeni din şansı ×2, mutasyon +%50 |
| `era.ash` | `BiomeGrowthMul=0`, `PlantGrowthMul=0`, kül bulutu oranı %40 |
| `era.pale` | Hastalık bulaşma ×2 |
| `era.harvest` | Tarım verimi ×1.5 |

---

## 2.13 Otomatik afetler (DisasterSystem)

**Katman sorunu:** Afetlerin davranışı (patlama, deprem, dalga) Powers katmanında yazılır; Sim katmanı Powers'ı göremez. Çözüm: `DisasterSystem` (Sim) yalnızca karar verir ve `DisasterRequestEvent{disasterId, tile, power}` yayınlar; Powers katmanındaki `DisasterExecutor` bu olayı dinler ve ilgili güç işleyicisini çalıştırır (Bölüm 7).

### Karar (yıl başında)
```
eğer law.auto_disasters == 0 → çık
her afet d için:
    worldAge < d.minWorldAge veya nüfus < d.minPopulation → atla
    yıl − sonTetik[d] < d.cooldownYears → atla
    p = d.chancePerYear * (d.eraMultipliers[mevcutÇağ] ?? 1) * lawSlider   // lawSlider: 0,1,2,3 → 0, 1, 2, 3.5
    rng.Chance(p) → konum seç → DisasterRequestEvent → sonTetik[d] = yıl
```
Konum seçimi afete göre: `meteor` rastgele kara; `volcano` en yüksek dağ zone'u; `tsunami` en kalabalık kıyı şehri yakını; `plague`, `famine`, `zombie_outbreak`, `bandits` en kalabalık şehir; `dragon` rastgele zirve; `demon_raid` Kor Diyarı varsa orası, yoksa rastgele kara.

Tarihçeye ve bildirim akışına yazılır (Bölüm 8).

---

## 2.14 Testler ve kabul kriterleri

| # | Test | Kriter |
|---|------|--------|
| 1 | Tek biyom tohumu, boş düz dünya, ×10 hız, 50 yıl | Biyom haritanın %30–70'ini kaplar, sonra yavaşlar |
| 2 | İki eşit güçlü biyom yan yana, 200 yıl | Sınır dalgalanır, hiçbiri diğerini tamamen yok etmez (tolerans %15) |
| 3 | Orman + ateş, rüzgâr sabit | Yangın rüzgâr yönünde en az 2 kat hızlı ilerler |
| 4 | Yağmur bulutu yanan ormanın üstünde | Bulut altındaki yangınların %90'ı 200 tick'te söner |
| 5 | Lav tile'ı dağ yamacında | Aşağı akar, 1300 tick sonra tamamı kayaya döner |
| 6 | Buz çağı zorla | Sığ suların %60'ı 3 yılda donar; biyom yayılımı 0 |
| 7 | Çağ saati 1000 yıl | Hiçbir çağ arka arkaya gelmez; süreler min–max içinde |
| 8 | Afet kanunu 3, 500 yıl | Her afet cooldown kuralına uyar; tarihçe kayıtları tutarlı |
| 9 | Performans: Titanik, 20.000 yanan tile | Yangın sistemi < 2 ms/tick |
| 10 | Determinizm | Aynı seed ile 5000 tick sonrası `Biome`, `Feature`, `Fire` hash'leri aynı |

## 2.15 Görev listesi

| # | Görev | Süre |
|---|-------|------|
| 1 | İçerik sınıfları + çözümleme (biome/feature/cloud/era/disaster) | 1 gün |
| 2 | `NatureModifiers`, `BiomeEffectRegistry` | 0,5 gün |
| 3 | Biyom yayılımı + tohum | 1,5 gün |
| 4 | Feature büyüme, yoğunluk, render batch | 2 gün |
| 5 | Zone sıcaklığı, mevsimler, donma/kar | 1,5 gün |
| 6 | Rüzgâr + bulutlar + yağış efektleri | 2 gün |
| 7 | Yangın sistemi | 1,5 gün |
| 8 | Lav akışı ve soğuma, yavaş dönüşümler | 1,5 gün |
| 9 | Çağ sistemi, çağ saati, tint geçişi | 2 gün |
| 10 | Afet karar sistemi + `DisasterRequestEvent` | 1 gün |
| 11 | Testler | 1,5 gün |
| | **Toplam** | **~16 gün** |
