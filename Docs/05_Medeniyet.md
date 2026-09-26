# BÖLÜM 5 — Medeniyet

Bu bölüm bittiğinde medeni birimler köy kurar, sınırlarını genişletir, bina yapar, iş bölümü yapar, tarım/madencilik/avcılık ile kaynak toplar, ekipman üretir, ticaret yapar ve gemilerle başka kıtalara göç eder.

Bağımlılık: Bölüm 3–4. Krallık ve siyaset Bölüm 6'dadır; bu bölümde her şehir geçici olarak kendi başına bir "krallık gibi" çalışır, Bölüm 6 bunu gerçek krallığa bağlar. İçerik: `buildings.json`, `building_styles.json`, `jobs.json`, `resources.json`, `equipment_types.json`, `materials.json`, `item_qualities.json`, `happiness_events.json`.

---

## 5.1 Şehir nesnesi

Şehir bir meta nesnedir (Bölüm 6.1 ortak alanlar). Şehre özgü alanlar:

```csharp
public struct CityData {
    public int   Kingdom;                 // −1 geçici (Bölüm 6)
    public int2  CenterTile;
    public int   CenterBuilding;          // bonfire/hall
    public byte  HallTier;                // 0 kamp ateşi, 1–3
    public int   Leader;                  // birim indeksi
    public NativeList<int> Zones;         // sahip olunan zone indeksleri
    public NativeList<int> Buildings;
    public NativeList<int> Residents;     // birim indeksleri (önbellek, aylık)
    public CityInventory Inventory;       // 5.5
    public int   Loyalty;                 // Bölüm 6
    public int   Gold;
    public short Happiness;               // sakinlerin ortalaması
    public int   FoundedYear;
    public ushort BuildingStyle;
    public JobQuotas Quotas;              // 5.6
    public int   NextPlanTick;
}
```

---

## 5.2 Şehir kurma

### Tetikleyici
Aylık `SettlementSystem`: `sapient` bayraklı ve şehri olmayan yetişkin birimler için:
- 20 tile yarıçapında aynı alt türden (veya `open_hearth` kültürüyle uyumlu) şehirsiz birim sayısı ≥ 6 **veya** birim bir sömürge gemisinden indi **veya** isyan/bölünme sonucu (Bölüm 6).
- Yakında (30 tile) aynı krallığın şehri yoksa ya da mevcut şehir dolu ise (`nüfus ≥ konut kapasitesi × 1.2`).

### Yer seçimi
Grup merkezinin 15 tile yarıçapındaki aday zone'lar puanlanır:
```
puan = 3 × inşa edilebilir tile oranı
     + 2 × (yiyecek feature'ı + tarla uygun tile) / 64
     + 1 × (ağaç sayısı) / 24
     + 1 × (tepe/dağ komşusu ? 1 : 0)         // maden
     + 1 × (kıyı ? 1 : 0)                      // iskele
     − 5 × (başka şehrin zone'una uzaklık < 3 zone ? 1 : 0)
     − 3 × (lav/yangın/goo var ? 1 : 0)
```
En yüksek puanlı zone'a kamp ateşi (`bld.bonfire`) yerleştirilir; zone ve çevresindeki inşa edilebilir zone'lardan en fazla 9'u şehre katılır. Grup üyeleri şehre sakin olarak yazılır, en yüksek `steward + diplo` puanlı yetişkin lider olur. `CityFoundedEvent`.

Bina stili: türün stili (`style.human` ...); evrimleşmiş hayvanlar `style.beast`, `sst.hive_mind` olanlar `style.insect`.

---

## 5.3 Sınır (zone) genişlemesi

Yıllık kontrol, şehir başına:
```
hedef zone sayısı = 9 + nüfus / 3 + HallTier × 4           // en fazla 120
eğer mevcut < hedef:
    aday = sınır komşusu, sahipsiz, en az %40 kara zone'lar
    puan = arazi puanı (5.2) − 0.3 × merkezden uzaklık(zone)
    en iyi aday → sahiplen
```
`cul.frontier_spirit` ve krallık trait'leri hedefi çarpar. Sahipsiz zone kalmazsa şehir sömürge düşünür (5.10). Terk edilmiş (sakinsiz) şehir 2 yıl sonra harabeye döner, zone'ları serbest kalır.

Zone sahipliği `ZoneData.OwnerCity`'ye yazılır ve overlay'i kirletir (`Dirty.Overlay`).

---

## 5.4 Binalar

### Boyut birimi
`buildings.json` içindeki `size` alanı **hücre** cinsindendir: **1 hücre = 3×3 tile**. Örn. `house` 3x2 → kaplama 9×6 tile. Sprite genişliği kaplama genişliğine eşittir; yüksekliği kaplama + çatı payıdır (EK C 6). Sur (`bld.wall`) 1x1 = 3×3 tile'lık segmenttir.

### Veri
```csharp
public struct BuildingData {
    public ushort Def;             // buildings.json id
    public int2   Origin;          // sol-alt tile
    public byte   W, H;          // tile cinsinden (hücre × 3)
    public int    City;
    public float  Hp, MaxHp;
    public float  BuildProgress;   // 0–1; 1 = tamam
    public byte   Tier;            // ev kademesi gibi yükseltmeler
    public ushort Style;
    public int    Residents;       // ev ise kapasite kullanımı
    public BuildingFlags Flags;    // Burning, Ruin, Abandoned, Upgrading
}
```
Kaplama: bina tile'larının `Building[]` dizisine bina indeksi yazılır, tile `Reserved` bayrağı alır, `Walkable` kalkar (kapı tile'ı hariç: en alt ortadaki tile yürünebilir kalır). Region yeniden hesaplanır.

### Planlayıcı (CityPlanner)
Her şehir 120 tick'te bir planlar. İhtiyaç puanları:

| İhtiyaç | Formül | Karşılayan bina |
|---------|--------|-----------------|
| Konut | `nüfus − konut kapasitesi + 4` | tent → hut → house → manor (kademeye göre) |
| Depo | depolanan / depo kapasitesi > 0.8 | storage, granary |
| Yiyecek | yıllık tüketim > üretim | farm_shed, fishing_hut, pasture, beehive |
| Merkez yükseltme | nüfus eşiği (8 / 30 / 80) | hall_1 → hall_2 → hall_3 |
| Savunma | savaşta veya komşu düşman | watchtower, wall, gate, barracks |
| Üretim | demir var ve demirhane yok | smithy, mine, lumber_camp |
| Kültür | dil/din var | library, temple, school, graveyard |
| Refah | nüfus > 40 | market, inn, healers_house, well |

- `requires` alanı çözümlenir: `pop:30` (nüfus), `hall_2` (bina var), `din`/`dil` (şehrin dini/dili var), `kıyı`, `hills/mountain yakın`, `orman yakın`, `evcil hayvan`.
- `maxPerCity` sınırı uygulanır. Kaynak yoksa bina plan kuyruğunda bekler (en fazla 3 aktif inşaat).

### Yer bulma
```
aday tile'lar: şehir zone'larındaki inşa edilebilir, footprint'i tamamen boş olan konumlar (örnekleme: 64 rastgele aday)
puan = −uzaklık(merkez) × 0.5  (konut, depo)   /  +uzaklık × 0.3  (çiftlik, maden: kenarda)
     + yol komşuluğu 2 + düzlük (footprint içi seviye farkı 0 ise) 2
     + türe özel: iskele → su komşusu zorunlu; maden → tepe/dağ 3 tile içinde zorunlu; değirmen → tahıl ambarına yakın
en iyi aday → bina yerleştir (BuildProgress = 0)
```

### İnşaat
İnşaatçılar (`job.builder`) depodan maliyeti taşır (her taşıma en fazla 5 birim), sonra `BuildProgress += 0.01 × buildSpeed` / tick. Tamamlanınca `BuildingCompletedEvent`. Yükseltme (ev kademesi, merkez) aynı süreçle yapılır; bina bu sırada çalışmaya devam eder.

### Hasar ve yıkım
Can 0 → `BuildingDestroyedEvent`, tile'lar `bld.ruins` olur (5 yıl sonra silinir), sakinler evsiz kalır. Yanan bina tick başına 4 can kaybeder (Bölüm 2.8). Kuşatmada birimler binaya `dmg × 0.5` vurur (surlara `siege_masters` ile ×2).

### Yollar
Bina tamamlanınca kapı tile'ından şehir merkezine A\* (yol tile'ları maliyet 0.5, diğerleri 1; su yasak) çizilir; güzergâhtaki tile'lara `Road` bayrağı yazılır. Yol hareket hızını ×1.3 artırır.

---

## 5.5 Kaynaklar ve envanter

```csharp
public struct CityInventory { public FixedList128Bytes<ResAmount> Items; public int Capacity; }  // ResAmount{ushort res; int amount}
```
- Kapasite = depo sayısı × 200 + merkez kademesi × 100. Aşan kaynak toplanmaz.
- **Tüketim (aylık):** Her sakin `1` besin birimi tüketir; en yüksek besinli yiyecekten başlanır (ekmek 5 > et 4 > ...). Diyete uymayan yiyecek yenmez.
- **Bozulma:** Yiyecek ayda %4 bozulur (tahıl ambarı varsa %2). Tuz varsa et/balık bozulması yarıya iner.
- **Kıtlık:** Yiyecek biterse sakinler `hap.hungry` ve açlık; 3 ay sürerse `FamineEvent` ve göç eğilimi.
- **Vergi/altın:** Aylık `sakin × 0.1 × (1 + steward/20)` altın; `greedy` lider +%20, mutluluk −.

---

## 5.6 Meslekler ve iş dağıtımı

Aylık `JobAssigner`, şehir başına:
1. Kotaları hesapla (`jobs.json` → `quotaRule` kodu):
   - builder: `max(1, nüfus/8)` + aktif inşaat başına 1
   - farmer: tarla sayısı / 4; gatherer: yiyecek < 2 yıllık ihtiyaç ise `nüfus/10`
   - lumberjack: odun < `100 + planlanan maliyet`; miner: maden başına 4 (bina `jobSlots`)
   - fisher, herder, smith, baker, priest, scholar, healer, trader, sailor: ilgili bina `jobSlots`
   - warrior: barışta `nüfus × 0.05`, savaşta `nüfus × 0.2` (kışla kapasitesiyle sınırlı); guard: kule başına 1
2. İşsiz yetişkinlere öncelik sırasıyla ata: yiyecek > inşaat > savunma > üretim > kültür. Trait eşleşmesi tercih edilir (`green_thumb` → farmer, `miner` → miner, `blademaster` → warrior).
3. Fazla işçileri boşalt (en düşük uygunluk puanlıdan).

Medeni nöron `work` puanı: işi varsa 40 (gece −20); görevi `Job` türüne göre seçilir:

| Meslek | Görev zinciri |
|--------|---------------|
| builder | depodan malzeme al → inşaat alanına git → inşa et |
| farmer | boş tarla bul → ek (60 tick) → olgunlaşınca biç (`feat.wheat_crop` evre 3) → depoya taşı |
| lumberjack | olgun ağaç → kes (kaynak −1/40 tick) → taşı |
| miner | maden binasına gir (görünmez olur) → 200 tick → taş/cevher çıkar (en yakın damarın kaynağından) |
| gatherer | meyve çalısı/mantar/ot → topla → taşı |
| fisher | iskele/kıyı → balık tut (200 tick, `bio.coral` ×3) veya tekneyle açığa çık |
| hunter | yenilebilir hayvan bul → avla → et+deri taşı |
| smith | depodan cevher+odun → demirhane → eşya üret (5.7) |
| priest / scholar / healer | Bölüm 6 ve 4.8 davranışları |

---

## 5.7 Eşya üretimi

```csharp
public struct ItemData { public long Uid; public ushort Type, Material, Quality; public int NameId; public int Kills; public long CreatedTick; public long MakerUid; public int Owner; }
```
- Demirci 400 tick'te bir eşya üretir. Eşya tipi: şehrin ihtiyacı (asker silahsızsa silah, zırhsızsa zırh).
- Malzeme: depodaki en yüksek kademeli uygun malzeme (`materials.json` tier). Ahşap/kemik malzemesiz de yapılabilir.
- Kalite zarı: `item_qualities.json` `baseChance` dağılımı; demircinin `craftQuality` puanı başına dağılım bir kademe yukarı kayar.
- `q.unique` ve `q.legendary` eşyalara dil sisteminden isim verilir ve tarihçeye yazılır; sahibinin öldürdükleri eşyaya işlenir. Sahip ölünce eşya yere düşer (tile üstünde `DroppedItem`), başka birim alabilir.
- Final etki: `eq.baseEffects × material.multiplier × quality.multiplier`.

---

## 5.8 Mutluluk

- Birim başına son 8 olayın halkalı tamponu (`HappinessLog`: olay id, değer, kalan ay 6). Mutluluk = Σ aktif olay değerleri + trait/biyom/çağ sabitleri, −100..100 arası.
- Olay listesi `happiness_events.json`. Örnek tetikleyiciler: ev tamamlandı → sakinlere `hap.new_home`; aile üyesi öldü → yakınlarına `hap.family_died`; savaş kazanıldı → krallığa `hap.war_won`.
- Şehir mutluluğu sakinlerin ortalamasıdır. −40 altı: üretim ×0.7, göç ve isyan eğilimi (Bölüm 6); +40 üstü: doğurganlık ×1.2.

---

## 5.9 Ticaret

- Pazarı olan şehirler yıllık olarak aynı krallıktaki veya dost/müttefik krallıklardaki pazarlı şehirlerle **kervan** gönderir: 1 tüccar birimi, fazlası olan kaynağı (en fazla 30 birim) taşır.
- Varış: `değer × 1.3 × (1 + tradeProfit)` altın kazanılır, kaynak karşı depoya eklenir. Karşılıklı fikir +2 (Bölüm 6).
- Kervan yolda öldürülürse yük kaybolur, fikir −5.
- Kıta aşırı ticaret: ticaret gemisiyle (5.10).

---

## 5.10 Gemiler

```csharp
public struct BoatData { public ushort Type; public float2 Pos; public int City; public float Hp; public FixedList32Bytes<int> Passengers; public ushort CargoRes; public int CargoAmount; public int2 Destination; public int PathHandle; public BoatState State; }
public enum BoatType : byte { Fishing, Trade, Transport, War }
```
- **Üretim:** İskele → balıkçı teknesi (kapasite 1) ve nakliye (8 yolcu). Tersane → ticaret gemisi ve savaş gemisi (20 can × tier, menzilli saldırı).
- **Yol bulma:** `MoveClass.Water` region grafiği. Liman noktası = iskelenin su tarafındaki tile.
- **Sömürge kararı (yıllık):** Şehir nüfusu > konut × 1.1, sahipsiz zone kalmadı ve krallıkta `law.colonization` açık → hedef: aynı su adasına kıyısı olan, en yakın 3 zone'u sahipsiz kara adası. 6–8 gönüllü (genç yetişkin, ailesiz öncelikli) biner, varınca yeni şehir kurulur (5.2), aynı krallığa bağlı.
- **Batma:** Can 0 → yolcular suya düşer (yüzme kuralları); savaş gemileri düşman gemilerine ve kıyıdaki birimlere saldırır.
- Tooltip: yolcu sayısı, tür ikonları, yük.

---

## 5.11 Göç

- Mutluluk < −30 veya kıtlıkta sakinler yıllık %10 ihtimalle ayrılır: aynı krallıkta daha mutlu bir şehre (yürüyerek/gemiyle) ya da `hospitality` kültürlü yabancı şehre (`law.migration`).
- `bld.inn` olan şehirler göçmen çekimi ×1.5. Göçmen 1 yıl `st.homesick` taşır.

---

## 5.12 Testler ve kabul kriterleri

| # | Test | Kriter |
|---|------|--------|
| 1 | 10 insan, boş çayır, ×5 hız, 100 yıl | Şehir kurulur, merkez en az `hall_2`, nüfus ≥ 60, en az 1 yeni şehir |
| 2 | Aynı senaryo, kıyı adası | Sömürge gemisiyle başka adaya şehir |
| 3 | Yiyecek kaynağı olmayan kaya adası | Kıtlık olayı, göç veya nüfus düşüşü |
| 4 | Bina yerleşimi | Hiçbir bina su/dağ/başka bina üstüne kurulmaz; iskele hep kıyıda |
| 5 | Terraform ile şehir altını su yap | Binalar yıkılır, sakinler evsiz kalır, şehir yeniden inşa etmeye çalışır |
| 6 | Demirci 100 eşya | Kalite dağılımı tabloyla uyumlu (±%3) |
| 7 | 10 şehir, 3.000 medeni birim | Medeniyet sistemleri < 3 ms/tick (ortalama) |
| 8 | Kaydet/yükle ortasında inşaat | İnşaat kaldığı yerden devam eder |

## 5.13 Görev listesi

| # | Görev | Süre |
|---|-------|------|
| 1 | `CityData`, `BuildingData`, depolar, kaplama | 2 gün |
| 2 | Şehir kurma + yer puanlama | 1,5 gün |
| 3 | Zone genişlemesi, terk ve harabe | 1 gün |
| 4 | Planlayıcı + yer bulma + inşaat + yükseltme | 4 gün |
| 5 | Yol çizimi | 1 gün |
| 6 | Envanter, tüketim, bozulma, kıtlık, vergi | 1,5 gün |
| 7 | Meslek kotası ve atama + meslek görevleri | 4 gün |
| 8 | Eşya üretimi, düşen eşyalar, efsanevi isimler | 2 gün |
| 9 | Mutluluk olay sistemi | 1 gün |
| 10 | Ticaret kervanları | 1,5 gün |
| 11 | Gemiler, su yol bulma, sömürge, göç | 3,5 gün |
| 12 | Bina render'ı (stil, kademe, iskele sprite'ı, hasar/yangın), sakin görünürlüğü | 2 gün |
| 13 | Testler | 2 gün |
| | **Toplam** | **~28 gün** |
