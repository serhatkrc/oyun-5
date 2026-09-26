# BÖLÜM 3 — Birim Çekirdeği

Bu bölüm bittiğinde haritada binlerce hayvan yaşar: doğar, büyür, yer, uyur, çiftleşir, avlanır, savaşır, yaşlanıp ölür. Birimler yol bulur, adaları ayırt eder, yüzer veya uçar. Medeniyet yoktur (Bölüm 5), ama tüm medeni davranışların üzerine kurulacağı birim altyapısı eksiksizdir.

Bağımlılık: Bölüm 1–2. İçerik: `species.json`, `unit_traits.json`, `status_effects.json`, `spells.json`.

---

## 3.1 Birim deposu (UnitStore, SoA)

Tüm birimler tek depoda. Her alan ayrı `NativeArray`; kapasite 8.192'den başlar, dolunca ikiye katlanır. Aktif birimler `NativeList<int> Alive` listesinde tutulur (ölü yuvalar listede olmaz).

| Grup | Alan | Tip | Not |
|------|------|-----|-----|
| Kimlik | `Uid` | `long` | Kalıcı kimlik (tarihçe) |
| | `Species`, `Subspecies` | `ushort`, `int` | Alt tür = meta indeksi (Bölüm 4) |
| | `Sex` | `byte` | 0 dişi, 1 erkek, 2 yok |
| | `BirthTick` | `long` | Yaş = (şimdi − doğum) / ticksPerYear |
| | `NameId` | `int` | İsim havuzundaki indeks (Bölüm 6 dil sistemi) |
| Konum | `Pos` | `float2` | Tile koordinatı (alt-tile hassasiyetli) |
| | `Z`, `VZ` | `float` | Sözde-3D yükseklik ve dikey hız (Bölüm 7) |
| | `Facing` | `byte` | Sol/sağ |
| Durum | `Hp`, `Mana`, `Stamina` | `float` | |
| | `Saturation` | `byte` | 0–100 doygunluk |
| | `Energy` | `byte` | 0–100 uyku ihtiyacı |
| | `Happiness` | `sbyte` | −100..100 |
| | `Xp`, `Level` | `int`, `byte` | |
| | `Kills` | `ushort` | |
| Hesaplanan | `Stats` | `UnitStats` | Önbellek (3.2) |
| | `StatsDirty` | `bool` | |
| Özellik | `Traits` | `TraitSet` (4 × `ulong` = 256 bit) | `unit_traits.json` id'leri |
| | `Statuses` | `StatusSlots` (sabit 8 slot) | 3.6 |
| | `Flags` | `UnitFlags` (uint) | Uçar, yüzer, ölümsüz, oyuncu kontrolünde, uyuyor... |
| AI | `Task`, `ActionIndex`, `ActionTimer` | `ushort`, `byte`, `short` | 3.8 |
| | `Target` | `EntityId` | Birim/bina hedefi |
| | `TargetTile` | `int2` | |
| | `PathHandle` | `int` | 3.4 |
| | `NextThinkTick` | `int` | |
| Sosyal | `Mother`, `Father`, `Mate` | `EntityId` | |
| | `Family`, `Clan`, `City`, `Kingdom`, `Culture`, `Language`, `Religion`, `Army` | `int` | Meta indeksleri (−1 yok); Bölüm 5–6 |
| | `Job` | `ushort` | Bölüm 5 |
| Ekipman | `Equip` | `EquipSlots` (6 × `int` item id) | Bölüm 5 |
| | `CarryRes`, `CarryAmount` | `ushort`, `ushort` | Taşınan kaynak |
| Hastalık | `Disease`, `DiseaseProgress` | `ushort`, `ushort` | Bölüm 4 |

```csharp
public sealed class UnitStore : IDisposable {
    public EntityId Spawn(in UnitSpawnRequest req);          // tür, alt tür, konum, yaş, ebeveynler, meta'lar
    public void Kill(EntityId id, DeathCause cause, EntityId killer);  // olay yayınlar, yuvayı tick sonunda serbest bırakır
    public bool IsAlive(EntityId id);
    public ref UnitStats GetStats(int index);                 // dirty ise yeniden hesaplar
}
public enum DeathCause : byte { OldAge, Killed, Starved, Disease, Burned, Drowned, Frozen, Explosion, Crushed, Divine, Transformed, Void }
```

Ölüm anında birim hemen yok edilmez: `Kill` bir kuyruğa yazar, yuva Faz 7'de (Events Flush) serbest bırakılır. Böylece aynı tick içindeki diğer sistemler tutarlı veri okur. Ölü birim yerinde **ceset** bırakabilir (`CorpseStore`: tile, tür, uid, zaman; 2 yıl sonra kemik yığını) — zombi ve ölü diriltme bunu kullanır.

---

## 3.2 İstatistik hesaplama

### Stat kimlikleri
JSON'daki efekt anahtarları (`hp`, `dmg`, `armor`, `speed`, `atkspd`, `crit`, `range`, `dodge`, `lifespan`, `fertility`, `intel`, `diplo`, `warfare`, `steward`, `luck`, `mana`, `manaRegen`, `regen`, `stamina`, `hungerRate`, `sleepNeed`, `sight`, `size`, `knockbackResist`, `diseaseResist`, `spellPower`, `xp`, ...) yükleme sırasında `StatId` enum'una çevrilir. Tanınmayan anahtarlar **davranış anahtarı** sayılır ve `BehaviorKeyRegistry`'ye gider (ör. `buildSpeed`, `farmYield` — ilgili sistemler bunları okur). Hiçbir sistemin tanımadığı anahtar yükleme sırasında uyarı üretir.

### Formül
```
final(stat) = (base + Σ add) × (1 + Σ pct / 100)
base   = türün stats değeri (species.json)
kaynaklar (sırasıyla toplanır): alt tür trait'leri, genler (alt tür), birim trait'leri, ekipman, statüler, yaş evresi çarpanı, biyom/çağ çarpanları
```
- Yaş evresi çarpanları: bebek ×0.3 hasar/can, çocuk ×0.6, yetişkin ×1, yaşlı ×0.8 hasar & ×0.8 hız.
- Alt sınırlar: can ≥ 1, hız ≥ 0.05 (0 yalnızca `unmoving`), zırh ≥ 0.
- `StatsDirty` şu durumlarda işaretlenir: trait/statü/ekipman değişimi, yaş evresi geçişi, alt tür değişimi. Hesap tembeldir (ilk okumada).

---

## 3.3 Uzamsal indeks

- Hücre boyutu = zone (8×8 tile).
- `NativeParallelMultiHashMap<int, int> UnitsByCell` her tick Faz 3 başında paralel job ile **baştan kurulur** (5.000 birimde < 0,2 ms).
- Sorgular:
```csharp
public static class SpatialQuery {
    public static int FindNearest(in SpatialCtx c, float2 pos, float radius, UnitFilter filter);  // en yakın uyan birim
    public static int CollectInRadius(in SpatialCtx c, float2 pos, float radius, UnitFilter f, NativeList<int> outList);
}
public struct UnitFilter { public ulong SpeciesMask; public bool Enemy, Ally, Living, Undead, Edible; public int ExcludeIndex; }
```
- Binalar ve feature'lar için ayrı indeksler: `BuildingsByZone` (Bölüm 5), feature'lar zaten tile dizisinde.

---

## 3.4 Yol bulma

### Katmanlar
1. **Ada kontrolü:** Başlangıç ve hedef tile'ın `IslandId`'si (hareket sınıfına göre) farklıysa istek anında `Unreachable` döner. Uçan birimler için yol bulma yapılmaz (düz çizgi + küçük sapma).
2. **Region A\*:** Bölüm 1.9'daki region grafiğinde A\*. Sezgisel = region merkezleri arası öklid mesafesi. Kenar maliyeti = merkezler arası mesafe × ortalama hareket maliyeti.
3. **Yerel A\*:** Bulunan region koridoru içinde tile düzeyinde A\* (8 yön, köşe kesme yok). Sadece koridordaki region'lar açık sayılır; arama alanı küçük kalır.

### İstek sistemi
```csharp
public struct PathRequest { public int Unit; public int2 From, To; public MoveClass Class; public byte Priority; }
public sealed class PathService {
    public int Request(in PathRequest r);           // handle döner
    public PathStatus Status(int handle);           // Pending, Ready, Unreachable, Failed
    public ReadOnlySpan<int2> GetPath(int handle);
    public void Release(int handle);
}
```
- **Bütçe:** Tick başına en fazla 64 istek çözülür (Burst job, paralel). Kuyrukta öncelik: oyuncu kontrolündeki birim > savaş > kaçma > iş > gezinme.
- **Önbellek:** (başlangıç region'ı, hedef region'ı) çifti için region koridoru 120 tick önbellekte tutulur. `IslandsChangedEvent` veya ilgili chunk'ın region sürümü değişirse geçersizleşir.
- **Yol bellek havuzu:** Yollar tek bir büyük `NativeList<int2>` içinde dilimler olarak durur; serbest bırakılan dilimler yeniden kullanılır. Yol uzunluğu en fazla 512 adım; daha uzunsa ara hedefle bölünür.
- **Hedef tile yürünemez olursa** (bina, su) hedefin en yakın yürünebilir komşusu seçilir.
- **Yüzücüler:** `flag:swim` olan birimler için sığ su kara sınıfına dahil edilir; derin su için `MoveClass.Water` kullanılır ve birim yüzme animasyonuna geçer.

### Takip
Birim yolda ilerlerken sıradaki tile yürünemez hâle geldiyse (terraform, bina) yolu bırakıp yeniden ister. Aynı hedefe 3 başarısız denemeden sonra görev iptal edilir.

---

## 3.5 Hareket

```
hız (tile/sn) = Stats.speed × 1.5 / tileMoveCost × (yüzüyorsa 0.5) × (yol varsa 1.3)
Pos += normalize(sıradaki_tile − Pos) × hız × TickDt
```
- Aynı tile'da 4'ten fazla birim varsa hafif itme (separation) uygulanır; birimler üst üste yığılmaz ama kilitlenmez.
- `Z > 0` iken yatay hareket fizik tarafından yapılır (fırlatılma; Bölüm 7). Yere düşünce `VZ` hızına göre düşme hasarı: `max(0, VZ − 8) × 5`.
- Derin suya düşen, yüzemeyen birim `Stamina` tükenince boğulur.

---

## 3.6 Statü efektleri

```csharp
public struct StatusSlots { public ushort Id0..Id7; public int Remaining0..Remaining7; }   // sabit boyut, heap yok
```
- Uygulama: aynı statü varsa süre yenilenir (en uzunu), yoksa boş slota yazılır; slot yoksa süresi en kısa olanın yerine geçer.
- Her tick süreler azalır; tick etkileri (`hp:-3/tick` gibi) `StatusEffectRegistry`'deki kod ile uygulanır.
- Süre birimi JSON'da ay (`durationMonths`); 0 = koşula bağlı (ör. `st.starving` doygunluk 0 olduğu sürece).
- Görsel: statü ikonları birim üstünde (LOD 0'da), yanma/donma için palet efekti.

---

## 3.7 Yaşam döngüsü ve ihtiyaçlar

### Yaş evreleri
| Evre | Ömür oranı | Not |
|------|-----------|-----|
| Bebek | < %8 | Hareket yavaş, ebeveyni takip eder, çalışmaz |
| Çocuk | %8–18 | Oynar, öğrenir (XP ×1.5), savaşmaz |
| Yetişkin | %18–80 | Tüm eylemler |
| Yaşlı | > %80 | Hız ve hasar ×0.8; zekâ +1 |

Ömür sonrasında her yıl ölüm şansı: `0.1 + 0.15 × (yaş/ömür − 1) × 10` (en fazla 0.9). Ömür 0 olan türler (ölümsüzler, elementaller) yaşlanmaz.

### Açlık, uyku, stamina
| İhtiyaç | Azalma | Karşılama |
|---------|--------|-----------|
| Doygunluk | Ayda `8 × hungerRate` | Diyete uygun yiyecek: yiyeceğin `nutrition × 10` |
| Enerji | Ayda `12 × sleepNeed`; gece ×1.5 | Uyku: tick başına +0.3, evde ×2 |
| Stamina | Koşma/yüzme/savaşta tick başına −0.2 | Dinlenince +0.5 |

Doygunluk 0 → `st.starving`; 3 ay sürerse ölüm. Diyet eşlemesi: `herb` → bitki/meyve/buğday; `carn` → et/balık, canlı av; `omni` → hepsi; `brains` → canlı birim (zombi); `none` → ihtiyaç yok.

### Üreme
Aylık kontrol (yetişkin, doygunluk > 60, mutluluk > −20, `law.reproduction` açık):
```
şans = 0.08 × fertility × çağ.FertilityMul × (nüfus sınırı yakınsa 0.2)
```
| Tip | Mekanik |
|-----|---------|
| `live` | Eş gerekir (aynı tür, karşı cins, yakın). Dişiye `st.pregnant`; süre sonunda 1 yavru (`litter` trait'leriyle 1–6) |
| `egg` | Eş gerekir; yumurta nesnesi tile'a bırakılır (`st.in_egg` birimi), 3 ay sonra çatlar |
| `split` | Eş gerekmez; birim ikiye bölünür, canı paylaşılır |
| `spore`, `tumor`, `infect`, `assimilate` | Hastalık/dönüşüm yoluyla (Bölüm 4) |
| `summon`, `seed`, `none` | Doğal üreme yok |

Yavru: tür ve alt tür ebeveynlerden, trait'ler kalıtım kurallarıyla (Bölüm 4.3), meta'lar ebeveynden (Bölüm 6).

### Nüfus sınırı
`law.population_cap` açıksa toplam birim sınırı (varsayılan 5.000; kaydırıcı 100–20.000). Tür başına ayrıca `max(200, sınır × 0.25)` yumuşak sınırı: aşılınca doğurganlık ×0.2.

---

## 3.8 Yapay zekâ: nöron → görev → eylem

### 3.8.1 Düşünme sıklığı
Birim her tick düşünmez. `NextThinkTick` ile kademeli: normal 10 tick'te bir, savaşta 4, uyurken 40. Aynı tick'te düşünen birimler indeks kaydırmasıyla dağıtılır (`(tick + index) % aralık`).

### 3.8.2 Nöronlar (Utility AI)
Her nöron bir **puan** üretir; en yüksek puanlı (eşitlikte rastgele) nöronun görevi seçilir. Nöronlar kodda kayıtlıdır; hangi nöronların aktif olduğu tür + alt tür trait'leri + birim trait'lerinden gelen `neuron:x` anahtarlarıyla belirlenir. Oyuncu Beyin panelinden alt tür bazında nöron kapatabilir (bit maskesi).

```csharp
public interface INeuron { ushort Id { get; } float Score(in ThinkCtx c, int unit); ushort TaskId { get; } }
```
Performans için gerçek uygulama, Burst job içinde `switch(neuronId)` ile çalışır; arayüz yalnızca kayıt ve editör içindir.

**Çekirdek nöronlar (Bölüm 3):**

| Nöron | Puan (özet) | Görev |
|-------|-------------|-------|
| `eat` | `(100 − doygunluk) × 1.2`; açlıkta ×2 | `task.find_food` |
| `sleep` | `(100 − enerji)`; gece +20 | `task.sleep` |
| `flee` | Yakında güçlü düşman: `tehdit × (1 − can oranı) × fleeThreshold` | `task.flee` |
| `attack` | Yakında düşman/av; etçil ve açsa ×1.5 | `task.attack` |
| `hunt` | Etçil, açlık > 40, av görüş alanında | `task.hunt` |
| `mate` | Üreme koşulları + eş yakında | `task.mate` |
| `follow_parent` | Bebekse sabit 60 | `task.follow` |
| `follow_herd` | `herd` / `pack` trait'i, sürüden uzak | `task.follow` |
| `wander` | Taban 10 | `task.wander` |
| `explore` | `curious` trait'i | `task.explore` |
| `return_home` | Evi/yuvası varsa ve uzaktaysa | `task.go_home` |
| `rest` | Stamina < 20 | `task.rest` |
| `swim_to_land` | Suda ve yorgun | `task.go_land` |
| `cast_spell` | Büyüsü hazır ve hedef uygun | `task.cast` |
| `teleport_home` | `neuron:teleport_home` + uzakta + tehlike | `task.teleport` |
| `pack_hunt` | `pack_hunter`: sürü üyesi saldırıyorsa +50 | `task.attack` |
| `guard_territory` | `territorial`: bölgeye yabancı girdi | `task.attack` |
| `migrate` | `nomadic`/`migratory`: mevsim veya kıtlık | `task.migrate` |
| `build_nest` | `builds_nests`: yuvası yok | `task.build_nest` |
| `bury_dead` | `grave_keepers`: yakında ceset | `task.bury` |

Bölüm 5–6 medeni nöronları ekler (`work`, `go_to_city`, `talk`, `read_book`, `pray`, `trade`, `plot`, `join_army` ...).

### 3.8.3 Görevler ve eylemler
Görev = eylem dizisi. Eylem = küçük durum makinesi. Birim sadece `Task`, `ActionIndex`, `ActionTimer`, `Target` tutar.

```csharp
public enum ActionResult : byte { Running, Done, Failed }
public interface IAction { ActionResult Tick(ref ActCtx c, int unit); }
```
| Görev | Eylemler |
|-------|----------|
| `task.find_food` | `find_food_target` → `move_to` → `eat` |
| `task.hunt` | `pick_prey` → `chase` → `melee` (öldürünce) → `eat_corpse` |
| `task.attack` | `move_to_target` → `melee`/`ranged` (tekrar) |
| `task.flee` | `pick_flee_point` (tehdit tersine, 12 tile) → `move_to` |
| `task.sleep` | `find_sleep_spot` → `move_to` → `sleep` |
| `task.mate` | `move_to_mate` → `mate` (60 tick) |
| `task.wander` | `pick_random_near` (6 tile) → `move_to` → `idle` (20–60 tick) |

Eylem `Failed` dönerse görev biter ve birim hemen yeniden düşünür.

---

## 3.9 Savaş

### Hedefleme
Düşmanlık kararı `HostilityMatrix` üzerinden: tür × tür varsayılanları (etçil → av, canavar → herkes, ölümsüz → canlı), üzerine Bölüm 6 krallık ilişkileri eklenir. Görüş (`sight`, varsayılan 8 tile) içinde en yüksek `tehdit / mesafe` puanlı düşman seçilir.

### Yakın dövüş
```
saldırı aralığı (tick) = 20 / atkspd
isabet: rng > hedef.dodge / 100
hasar = dmg × rng(0.85, 1.15) × (kritik ? 2 : 1)
azaltma = armor / (armor + 50)                   // 50 zırh = %50 azaltma
son hasar = max(1, hasar × (1 − azaltma))
itme: hedef.VZ += 2 × (dmg / hedefBoyut) × (1 − knockbackResist); yatay itme yönü saldırgandan dışa
```
Vuruş efektleri (`onHit:poison:30` → %30 ihtimalle zehir) vuruş sonrası işlenir.

### Menzilli ve mermiler
```csharp
public struct Projectile { public float2 From, To, Pos; public float Z, T, Duration; public ushort Type; public int Owner; public float Damage; public byte Flags; }
```
- Parabolik yay: `Z = sin(π × t) × yükseklik`, yükseklik mesafeyle orantılı. Hedef konumu atışta sabitlenir (öngörü yok); hedef kaçarsa ıskalar.
- `ProjectileStore` en fazla 4.096 mermi; havuzlu.
- Ateş topu, asit gibi alan mermileri çarpışmada küçük patlama çağırır (Bölüm 7 patlama sistemi).

### Deneyim ve seviye
Öldürme XP'si = hedefin seviyesi × 10 + 5. Seviye eşiği: `100 × seviye^1.5`. Seviye başına can +%3, hasar +%2 (en fazla 30. seviye). 10 öldürmede `tr.veteran`, 100'de `ach.slayer` kontrolü.

### Büyüler
`SpellSlots`: birim başına en fazla 4 büyü (trait, tür, din kaynaklı). Her birinin bekleme sayacı. `cast_spell` nöronu büyü hazır ve hedef menzildeyken puan verir. Büyü etkileri `SpellRegistry` kod tablosunda (`spells.json` id'leriyle eşlenir).

---

## 3.10 Birim render'ı

- **Veri akışı:** Faz sonunda render thread'i için `UnitRenderBuffer` (konum, Z, sprite kare indeksi, palet satırı, yön, ölçek, renk tint) bir Burst job ile doldurulur. Render bu tamponu okur; simülasyon verisine doğrudan dokunmaz.
- **Çizim:** Tek bir quad mesh + `Graphics.RenderMeshInstanced` (sprite atlası UV'si ve palet satırı instance verisi olarak). Görünüm alanı dışındakiler tamponda yer almaz (culling job'da).
- **Palet değiştirme:** Sprite'lar 8 gri seviye indeksli çizilir (EK B 3.6.2). Palet dokusu 256×8: her satır bir palet. Palet satırı = alt tür fenotipi + krallık rengi (kıyafet pikselleri için ayrı 2 indeks) + özel durum (zombi, donmuş, yanıyor).
- **Animasyon:** Kare = `(tick / kareSüresi) % kareSayısı`; eylem tipine göre animasyon seti (`idle`, `walk`, `attack`, `swim`, `sleep`, `death`). Yaş evresine göre ölçek (bebek 0.6, çocuk 0.8).
- **LOD:** 0'da tam sprite + ekipman pikseli + statü ikonu; 1'de sprite; 2'de 1×1 renkli nokta (tür rengi); 3'te çizilmez.
- **Gölge:** `Z > 0.1` olan birimlere katman 29'da gölge.

---

## 3.11 Olaylar

`UnitBornEvent{uid, species, parentUids}`, `UnitDiedEvent{uid, cause, killerUid, tile}`, `UnitLevelUpEvent`, `UnitTraitGainedEvent`, `UnitAteEvent` (istatistik). Tarihçe yalnızca "önemli" birimleri kaydeder (lider, favori, 10+ öldürme, efsanevi trait).

---

## 3.12 Testler ve kabul kriterleri

| # | Test | Kriter |
|---|------|--------|
| 1 | Stat formülü: bilinen trait + ekipman kombinasyonu | Beklenen değer (birim testi) |
| 2 | İki ada, köprü yok | Karşı adaya istek `Unreachable`, 0 A\* düğümü açılır |
| 3 | 1.000 rastgele yol isteği, Titanik harita | Ortalama < 0,3 ms, %99'u < 2 ms |
| 4 | 50 koyun + 5 kurt, kapalı ada, 100 yıl | Popülasyonlar salınır; ikisi de 30 yılda sıfırlanmaz (5 seed'in en az 4'ünde) |
| 5 | Açlık | Yiyeceksiz alanda koyunlar ~12 ay içinde açlıktan ölür |
| 6 | Savaş formülü | 50 zırhlı hedefe hasar yarıya iner |
| 7 | 5.000 birim, ×1 hız | Birim sistemleri toplamı < 6 ms/tick; GC 0 |
| 8 | Terraform ile yol üstüne dağ | Birim 1 saniye içinde yeni yol bulur |
| 9 | Determinizm, 5.000 tick, 2.000 birim | Birim konum ve can hash'leri aynı |

## 3.13 Görev listesi

| # | Görev | Süre |
|---|-------|------|
| 1 | `UnitStore`, spawn/kill, ceset deposu | 2 gün |
| 2 | Stat sistemi, `StatId` eşlemesi, `BehaviorKeyRegistry` | 2 gün |
| 3 | Uzamsal indeks ve sorgular | 1 gün |
| 4 | Yol bulma servisi (region A\* + yerel A\* + önbellek) | 4 gün |
| 5 | Hareket, yüzme, itme | 1,5 gün |
| 6 | Statü sistemi | 1 gün |
| 7 | Yaşam döngüsü, ihtiyaçlar, üreme | 2,5 gün |
| 8 | Nöron/görev/eylem çatısı + çekirdek nöronlar | 4 gün |
| 9 | Savaş, mermiler, XP, büyü çatısı | 3 gün |
| 10 | Birim render'ı, palet değiştirme, animasyon, LOD | 3 gün |
| 11 | Hayvan spawn (worldgen adım 10 + güç) | 0,5 gün |
| 12 | Testler, performans ayarı | 2,5 gün |
| | **Toplam** | **~27 gün** |
