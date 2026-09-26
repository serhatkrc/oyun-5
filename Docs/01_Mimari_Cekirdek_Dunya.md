# BÖLÜM 1 — Mimari, Çekirdek Sistemler ve Dünya

Bu bölüm bittiğinde elinde şunlar olacak: prosedürel üretilmiş bir dünya haritası, bu haritada kaydırma ve yakınlaştırma yapabilen bir kamera, fırçayla toprak/su/dağ çizip silebilme, zamanı durdurup hızlandırabilme ve dünyayı kaydedip geri yükleyebilme. Birim yok; ama sonraki bütün bölümlerin üzerine oturacağı temel eksiksiz olacak.

---

## 1.1 Teknoloji ve paketler

| Paket | Sürüm (asgari) | Kullanım |
|-------|----------------|----------|
| Unity | 6.3 LTS | Motor (6.0 LTS desteği Ekim 2026'da bitiyor) |
| Universal RP (2D Renderer) | 17.x | Render hattı |
| `com.unity.burst` | 1.8 | Ağır döngülerin native derlenmesi |
| `com.unity.collections` | 2.4 | `NativeArray`, `NativeList`, `NativeParallelMultiHashMap` |
| `com.unity.mathematics` | 1.3 | `int2`, `float2`, `math.*` (Burst uyumlu) |
| `com.unity.inputsystem` | 1.8 | Klavye/fare/dokunmatik |
| `com.unity.nuget.newtonsoft-json` | 3.2 | İçerik JSON'larının okunması |
| MessagePack-CSharp | 2.5 | İkili kayıt formatı |
| K4os.Compression.LZ4 | 1.3 | Kayıt sıkıştırma |
| FastNoiseLite (tek dosya, MIT) | 1.1 | Gürültü üretimi |

**Neden Unity Entities (DOTS ECS) değil?** Birim sayısı 5–20 bin arasında. Kendi SoA depomuz + Jobs bu ölçek için yeterli; ayrıca kayıt formatı, mod desteği ve hata ayıklama üzerinde tam kontrol sağlar. Entities; editör akışı, kayıt/yükleme ve mod API'si konusunda ciddi karmaşıklık ekler. Karar `DECISIONS.md` dosyasına yazılır.

**Hedef platform ayarları:** IL2CPP, .NET Standard 2.1, `Allow unsafe code = true` (bazı hızlı kopyalamalar için), Incremental GC açık.

---

## 1.2 Proje ve assembly yapısı

Her klasörün kendi `.asmdef` dosyası olur. Böylece derleme süresi kısalır ve bağımlılık yönü zorla korunur (alt katman üst katmanı göremez).

```
Assets/
  _Project/
    Core/            PG.Core.asmdef          (bağımlılık: yok)
    Content/         PG.Content.asmdef       (Core)
    World/           PG.World.asmdef         (Core, Content)
    WorldGen/        PG.WorldGen.asmdef      (Core, Content, World)
    Sim/             PG.Sim.asmdef           (Core, Content, World)        ← Bölüm 2–6
    Powers/          PG.Powers.asmdef        (Core, Content, World, Sim)   ← Bölüm 7
    Render/          PG.Render.asmdef        (Core, World, Sim)
    UI/              PG.UI.asmdef            (hepsi)                        ← Bölüm 8
    Persistence/     PG.Persistence.asmdef   (Core, Content, World, Sim)
    Boot/            PG.Boot.asmdef          (hepsi) → GameBootstrap sahnesi
    Tests/           PG.Tests.asmdef         (hepsi, test only)
  StreamingAssets/
    Data/            tiles.json, biomes.json, worldgen_templates.json, ...
    Mods/
```

Bağımlılık kuralı: **Core ← Content ← World ← Sim ← Powers ← UI**. Render hiçbir şeye yazmaz, yalnızca okur.

---

## 1.3 Çekirdek: zaman ve ana döngü

### 1.3.1 Sabitler

```csharp
public static class SimConst
{
    public const int   TicksPerSecond   = 20;   // x1 hızda saniyede 20 tick
    public const int   TicksPerMonth    = 60;   // x1'de 1 ay = 3 sn
    public const int   MonthsPerYear    = 12;   // x1'de 1 yıl = 36 sn
    public const float TickDt           = 1f / TicksPerSecond;
    public const int   MaxTicksPerFrame = 40;   // spiral-of-death koruması
}
```

Hız kademeleri: `{ 0, 1, 2, 3, 5, 10, 20 }` ve ek olarak **Süper Hız** modu. Süper hızda render yalnızca 4 karede bir yapılır ve tick'ler frame başına 16 ms bütçe dolana kadar koşar.

### 1.3.2 GameClock

```csharp
public sealed class GameClock
{
    public long  Tick { get; private set; }
    public int   Month => (int)(Tick / SimConst.TicksPerMonth % SimConst.MonthsPerYear);
    public int   Year  => (int)(Tick / (SimConst.TicksPerMonth * SimConst.MonthsPerYear));
    public int   SpeedIndex { get; private set; } = 1;   // SpeedLevels dizisinde indeks
    public bool  Paused => SpeedLevels[SpeedIndex] == 0;
    public bool  SuperSpeed { get; set; }

    public static readonly int[] SpeedLevels = { 0, 1, 2, 3, 5, 10, 20 };

    public void SetSpeed(int index);           // UI ve klavye (1–7 tuşları) çağırır
    public void TogglePause();                 // Space
    public void StepOnce();                    // duraklatılmışken "." tuşu: tam 1 tick
    internal void AdvanceTick() => Tick++;
    public bool IsMonthStart => Tick % SimConst.TicksPerMonth == 0;
    public bool IsYearStart  => Tick % (SimConst.TicksPerMonth * SimConst.MonthsPerYear) == 0;
}
```

### 1.3.3 Ana döngü (accumulator deseni)

```csharp
public sealed class SimulationRunner : MonoBehaviour
{
    float _accumulator;

    void Update()
    {
        var clock = Game.Clock;
        if (clock.Paused && !_stepRequested) { Game.Render.Interpolation = 0; return; }

        int ticksToRun;
        if (clock.SuperSpeed) {
            ticksToRun = RunUntilBudget(msBudget: 16f);          // bütçe dolana kadar
        } else {
            _accumulator += Time.unscaledDeltaTime * GameClock.SpeedLevels[clock.SpeedIndex];
            ticksToRun = Mathf.Min((int)(_accumulator / SimConst.TickDt), SimConst.MaxTicksPerFrame);
            _accumulator -= ticksToRun * SimConst.TickDt;
            if (ticksToRun == SimConst.MaxTicksPerFrame) _accumulator = 0; // yetişemiyorsak birikeni at
        }
        for (int i = 0; i < ticksToRun; i++) Game.Sim.Tick();
        Game.Render.Interpolation = _accumulator / SimConst.TickDt;     // 0..1 arası
    }
}
```

### 1.3.4 Tick içi sıralama (SimulationPipeline)

Her tick aşağıdaki sabit sırayla çalışır. Sıra determinizm için değiştirilmez; yeni sistemler bir **faz** içine eklenir.

| Faz | İçerik | Bölüm |
|-----|--------|-------|
| 0. Input Commands | Oyuncu güç komutları kuyruktan uygulanır | 7 |
| 1. World | Tile değişiklikleri, yangın, lav, biyom yayılımı (zaman dilimli) | 1, 2 |
| 2. Climate | Sıcaklık, bulutlar, yağış | 2 |
| 3. Units-Think | AI karar (paralel, sadece okuma) | 3 |
| 4. Units-Act | Hareket, savaş, eylem sonuçları (tek iş parçacığı yazma) | 3 |
| 5. Civ | Şehir, inşaat, meslek dağıtımı (şehir başına 10 tick'te bir) | 5 |
| 6. Meta | Krallık, diplomasi, plotlar (aylık) | 6 |
| 7. Events Flush | Olay kuyruğu dağıtılır, tarihçeye yazılır | 1 |
| 8. Region Rebuild | Dirty region'lar yeniden hesaplanır (bütçeli) | 1 |

```csharp
public interface ISimSystem
{
    SimPhase Phase { get; }
    int Order { get; }                 // aynı faz içinde sıralama
    void Tick(in SimContext ctx);
}
```

`SimContext` sistemlere dünya, saat, rastgelelik akışı ve olay veriyolu referanslarını tek yerde verir:

```csharp
public readonly ref struct SimContext
{
    public readonly WorldMap  World;
    public readonly GameClock Clock;
    public readonly EventBus  Events;
    public readonly ContentDB Content;
    public readonly SimRandomProvider Rng;
}
```

---

## 1.4 Çekirdek: deterministik rastgelelik

Algoritma: **PCG32** (hızlı, küçük durum, iyi dağılım). Her sistem kendi akışını alır; böylece bir sisteme yeni bir rastgele çağrı eklemek diğer sistemlerin sonuçlarını kaydırmaz.

```csharp
public struct SimRandom
{
    ulong _state; ulong _inc;

    public SimRandom(ulong seed, ulong stream) { _state = 0; _inc = (stream << 1) | 1; NextUInt(); _state += seed; NextUInt(); }

    public uint NextUInt() {
        ulong old = _state;
        _state = old * 6364136223846793005UL + _inc;
        uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
    }
    public int   Range(int minIncl, int maxExcl) => minIncl + (int)(NextUInt() % (uint)(maxExcl - minIncl));
    public float Value01()                        => (NextUInt() >> 8) * (1f / 16777216f);
    public bool  Chance(float p)                  => Value01() < p;
    public int   WeightedIndex(ReadOnlySpan<float> weights);   // ağırlıklı seçim (AI, çağ seçimi)
}

public sealed class SimRandomProvider
{
    readonly ulong _worldSeed;
    readonly Dictionary<int, SimRandom> _streams = new();
    public ref SimRandom Get(RngStream s);   // RngStream: WorldGen, Biome, Fire, Weather, UnitAI, Combat, Civ, Meta, Disasters, Names, Powers
}
```

Paralel job'larda: her birimin rastgele durumu `seed ^ unitIndex ^ tick` ile türetilen yerel bir `SimRandom` olur. Paylaşılan akış kullanılmaz.

Kayıt dosyasına tüm akışların `_state` değeri yazılır.

---

## 1.5 Çekirdek: kimlik sistemi

```csharp
public readonly struct EntityId : IEquatable<EntityId>
{
    public readonly int Index;        // depo içindeki yuva
    public readonly int Generation;   // yuva yeniden kullanıldıkça artar
    public static readonly EntityId None = new(-1, 0);
    public bool IsNone => Index < 0;
}

public sealed class IdPool
{
    int[] _generations; Stack<int> _free; int _count;
    public EntityId Allocate();
    public void Release(EntityId id);           // generation++ yapar, yuvayı serbest bırakır
    public bool IsAlive(EntityId id) => id.Index >= 0 && _generations[id.Index] == id.Generation;
}
```

Birim, bina, şehir, krallık ve diğer meta nesneler ayrı `IdPool`'lar kullanır. Ölmüş bir birimin id'si başka birine verilse bile eski referans `IsAlive` kontrolünde geçersiz çıkar.

Ek olarak her varlığın **kalıcı sayısal kimliği** (`long Uid`, artan sayaç) vardır. Tarihçe ve istatistik kayıtları `Uid` kullanır, çünkü `EntityId` yuvaları yeniden kullanılır.

---

## 1.6 Çekirdek: olay veriyolu

Simülasyon sistemleri birbirini doğrudan çağırmaz; olay yayınlar. Olaylar tick boyunca kuyrukta birikir ve Faz 7'de dağıtılır.

```csharp
public interface ISimEvent { }

public readonly struct TileChangedEvent : ISimEvent { public readonly int X, Y; public readonly TileTypeId Old, New; }
public readonly struct ChunkDirtyEvent  : ISimEvent { public readonly int ChunkIndex; public readonly DirtyMask Mask; }

public sealed class EventBus
{
    public void Publish<T>(in T evt) where T : struct, ISimEvent;             // tick içinde kuyruğa ekler
    public void Subscribe<T>(Action<T> handler) where T : struct, ISimEvent;
    internal void Flush();                                                     // Faz 7
}
```

Kurallar:
- Tip başına ayrı `List<T>` kuyruğu tutulur (struct kutulama olmaz).
- Olay işleyicisi yeni olay yayınlarsa bu olay **bir sonraki tick'te** işlenir. Bu, sonsuz zincirleri engeller.
- Tile başına olay yerine, fırça gibi toplu değişikliklerde `ChunkDirtyEvent` kullanılır.

---

## 1.7 İçerik veritabanı (ContentDB)

### 1.7.1 Yükleme

1. `StreamingAssets/Data/*.json` dosyaları okunur, ardından `Mods/*/Data/*.json` dosyaları (mod sırasıyla).
2. Her kayıt metin anahtarına sahiptir (`"id": "tile.sand"`). Yükleme sırasında her kategoriye 0'dan başlayan **sayısal id** atanır (`TileTypeId = byte`, `BiomeId = byte`, `TraitId = ushort`...).
3. Metin anahtarı → sayısal id eşlemesi kayıt dosyasına yazılır. Böylece mod eklenip çıkarıldığında eski kayıt doğru eşlenir.
4. Aynı anahtar ikinci kez gelirse (mod), alanlar **üzerine yazılır** (patch).

```csharp
public sealed class ContentDB
{
    public Registry<TileTypeDef> Tiles;
    public Registry<BiomeDef>    Biomes;      // Bölüm 2
    public Registry<WorldGenTemplateDef> WorldGenTemplates;
    // ... sonraki bölümlerde genişler

    public static ContentDB LoadAll(string basePath, IReadOnlyList<string> modPaths);
}

public sealed class Registry<T> where T : IContentDef
{
    public T this[int id] { get; }
    public int  IdOf(string key);             // bulunamazsa açıklayıcı hata
    public bool TryGet(string key, out T def);
    public int  Count { get; }
}
```

### 1.7.2 Doğrulama

Yükleme sonunda `ContentValidator` çalışır: eksik referans (var olmayan biyoma işaret eden tile), çakışan anahtar, aralık dışı değer. Hatalar tek listede toplanır ve açılışta gösterilir; oyun hatalı veriyle başlamaz.

---

## 1.8 Dünya veri modeli

### 1.8.1 Hiyerarşi ve boyutlar

| Birim | Boyut | Amaç |
|-------|-------|------|
| Tile | 1×1 | Temel hücre |
| Zone | 8×8 tile | Şehir sahipliği, sıcaklık, görünürlük, overlay |
| Chunk | 64×64 tile (8×8 zone) | Render yükleme birimi, region hesabı, kayıt bölümü |

Harita boyutu her zaman 64'ün katıdır.

| Ön ayar | Tile | Chunk | Tile sayısı |
|---------|------|-------|-------------|
| Çok Küçük | 128×128 | 2×2 | 16 K |
| Küçük | 192×192 | 3×3 | 37 K |
| Orta | 256×256 | 4×4 | 65 K |
| Büyük | 384×384 | 6×6 | 147 K |
| Çok Büyük | 512×512 | 8×8 | 262 K |
| Devasa | 768×768 | 12×12 | 590 K |
| Titanik | 1024×1024 | 16×16 | 1 M |

### 1.8.2 Tile depolama (SoA)

Her alan ayrı bir düz dizidir. İndeks: `i = y * Width + x`.

| Dizi | Tip | Bayt/tile | Açıklama |
|------|-----|-----------|----------|
| `Ground` | `byte` (TileTypeId) | 1 | Temel katman tipi |
| `Biome` | `byte` (BiomeId, 0 = yok) | 1 | Biyom üst katmanı |
| `Flags` | `ushort` | 2 | Bit alanı (aşağıda) |
| `Variant` | `byte` | 1 | Renk varyantı 0–3 (gürültüden, sabit) |
| `Fire` | `byte` | 1 | Yangın şiddeti 0–255 (Bölüm 2) |
| `Feature` | `ushort` | 2 | Ağaç/bitki/kaynak türü id (0 = yok) (Bölüm 2) |
| `Building` | `int` | 4 | Üzerindeki bina indeksi (−1 = yok) (Bölüm 5) |
| `Region` | `int` | 4 | Region id (bağlantı) |

Toplam ≈ 16 bayt/tile → Titanik haritada ≈ 16 MB. Kabul edilebilir.

```csharp
[Flags]
public enum TileFlags : ushort
{
    None        = 0,
    Walkable    = 1 << 0,   // tile tipinden türetilir, önbellek
    Water       = 1 << 1,
    DeepWater   = 1 << 2,
    Burnable    = 1 << 3,
    Buildable   = 1 << 4,
    SnowCover   = 1 << 5,   // kar örtüsü (katman değil, bayrak)
    Frozen      = 1 << 6,   // donmuş su
    Burning     = 1 << 7,
    Road        = 1 << 8,
    Wall        = 1 << 9,
    Irradiated  = 1 << 10,  // atom bombası sonrası (Bölüm 7)
    Blessed     = 1 << 11,
    Reserved    = 1 << 12,  // bina temeli için ayrılmış
    // 13–15 boş
}
```

### 1.8.3 Tile tipleri

Temel katman **sıralı seviyeler** şeklindedir. "Yükselt" ve "Alçalt" fırçaları `Level` değerini ±1 değiştirir.

| Level | Anahtar | Yürünebilir | Su | İnşa | Hareket maliyeti |
|-------|---------|-------------|----|------|------------------|
| 0 | `tile.deep_ocean` | – | ✔ (derin) | – | – |
| 1 | `tile.ocean` | – | ✔ | – | – |
| 2 | `tile.shallow` | ✔ (yavaş) | ✔ | – | 3 |
| 3 | `tile.sand` | ✔ | – | ✔ | 1.2 |
| 4 | `tile.soil_low` | ✔ | – | ✔ | 1 |
| 5 | `tile.soil_high` | ✔ | – | ✔ | 1 |
| 6 | `tile.hills` | ✔ | – | – | 2 |
| 7 | `tile.mountain` | ✔ (yavaş) | – | – | 4 |
| 8 | `tile.summit` | – | – | – | – |

Seviyesiz (özel) tipler: `tile.lava_hot`, `tile.lava_mid`, `tile.lava_cool` (soğuyunca `tile.mountain` veya `tile.hills` olur), `tile.ice`, `tile.field` (tarla), `tile.scorched` (yanık toprak), `tile.pit` (çukur), `tile.goo` (yayılan madde), `tile.wasteland`.

**Tile tanımı (JSON):**

```json
{
  "id": "tile.soil_low",
  "level": 4,
  "walkable": true,
  "water": false,
  "buildable": true,
  "burnable": false,
  "moveCost": 1.0,
  "canHaveBiome": true,
  "colors": ["#5C8A3A", "#588636", "#62903F", "#557F33"],
  "reliefShade": 0.06,
  "onLowerBecomes": "tile.sand",
  "onRaiseBecomes": "tile.soil_high",
  "tags": ["land", "fertile"]
}
```

- `colors`: 4 varyant; tile'ın `Variant` değeri hangisinin kullanılacağını seçer. Bu, düz alanlarda doğal bir "doku" hissi verir.
- `canHaveBiome`: `false` olan tile'a biyom uygulanırsa biyom silinir (ör. kum, dağ).
- `reliefShade`: kuzey komşusu daha alçaksa renk bu oranda açılır, daha yüksekse koyulaşır (Bölüm 1.11).

`TileTypeDef` yüklenince bayraklar önceden hesaplanır (`DerivedFlags`); `SetGround` çağrısında `Flags` alanının tip kaynaklı bitleri tek işlemle güncellenir.

### 1.8.4 Zone ve Chunk yapıları

```csharp
public struct ZoneData
{
    public int   OwnerCity;       // -1 = sahipsiz (Bölüm 5)
    public short TemperatureC;    // Bölüm 2
    public ushort LandTiles;      // istatistik önbelleği
    public ushort WaterTiles;
    public byte  DominantBiome;
}

public struct ChunkData
{
    public DirtyMask Dirty;       // Render | Regions | Stats | Save
    public int FirstRegion;       // bu chunk'a ait region listesinin başı
    public int RegionCount;
}

[Flags] public enum DirtyMask : byte { None = 0, Render = 1, Regions = 2, Stats = 4, Save = 8, Overlay = 16 }
```

### 1.8.5 WorldMap API

```csharp
public sealed class WorldMap : IDisposable
{
    public int Width { get; }  public int Height { get; }
    public int ChunksX => Width >> 6;  public int ChunksY => Height >> 6;
    public int ZonesX  => Width >> 3;  public int ZonesY  => Height >> 3;

    public NativeArray<byte>   Ground;   // ... (1.8.2 tablosundaki tüm diziler)

    // --- Okuma ---
    public bool InBounds(int x, int y);
    public int  Index(int x, int y) => y * Width + x;
    public TileTypeId GetGround(int x, int y);
    public bool IsWalkable(int x, int y);
    public bool IsWater(int x, int y);
    public int  ZoneIndexOf(int x, int y) => (y >> 3) * ZonesX + (x >> 3);
    public int  ChunkIndexOf(int x, int y) => (y >> 6) * ChunksX + (x >> 6);

    // --- Yazma (tek kapı: tüm değişiklikler buradan geçer) ---
    public void SetGround(int x, int y, TileTypeId type, ChangeSource src);
    public void SetBiome (int x, int y, BiomeId biome, ChangeSource src);
    public void SetFlag  (int x, int y, TileFlags flag, bool value);
    public void RaiseLevel(int x, int y, ChangeSource src);   // Level+1 (onRaiseBecomes)
    public void LowerLevel(int x, int y, ChangeSource src);   // Level-1 (onLowerBecomes)

    // --- Toplu yazma (fırça) ---
    public TileEditBatch BeginBatch(ChangeSource src);       // using ile kullanılır, sonunda dirty işler
}

public enum ChangeSource : byte { WorldGen, Power, Nature, Unit, Building, Load }
```

`SetGround` içinde sırasıyla yapılanlar:
1. Eski tip ile yeni tip aynıysa çık.
2. `Ground[i]` yazılır, `Flags` içindeki tip kaynaklı bitler yenilenir.
3. Yeni tip `canHaveBiome = false` ise `Biome[i] = 0`.
4. Yeni tip su ise `Fire[i] = 0`, `Feature[i]` içindeki kara bitkileri silinir (Bölüm 2 kuralları).
5. Chunk'ın `Dirty |= Render | Regions | Stats | Save`.
6. Yürünebilirlik değiştiyse region sistemi o chunk'ı yeniden hesap kuyruğuna alır.
7. `ChangeSource` = `WorldGen` veya `Load` değilse `TileChangedEvent` yayınlanır. Toplu işlemde tile başına olay yerine chunk başına tek `ChunkDirtyEvent` yayınlanır.

---

## 1.9 Region ve ada (bağlantı) sistemi

Yol bulma Bölüm 3'te ayrıntılanacak; ancak region verisi tile değişikliklerine doğrudan bağlı olduğu için burada kurulur.

### 1.9.1 Tanımlar

- **Region:** Tek bir chunk içinde, 4 yönlü komşulukla birbirine bağlı ve aynı **hareket sınıfındaki** tile kümesi.
- Hareket sınıfları: `Land` (yürünebilir kara), `Water` (gemi ve yüzücüler için su), `Shallow` hem kara hem su sınıfına sayılabilir (iki ayrı region katmanı tutulur).
- **Ada (Island):** Birbirine bağlı region'ların kümesi. İki tile aynı adada değilse aralarında kara yolu yoktur.

### 1.9.2 Veri

```csharp
public struct RegionData
{
    public int   Chunk;
    public MoveClass Class;
    public int   TileCount;
    public int2  Center;          // yaklaşık merkez (yol bulma sezgiseli için)
    public int   IslandId;
    public int   FirstEdge, EdgeCount;   // komşu region kenar listesi
}
public enum MoveClass : byte { Land, Water }
```

### 1.9.3 Algoritma

**Chunk'ı yeniden hesaplama** (`RebuildChunkRegions(int chunk)`):
1. Chunk'ın eski region'larını serbest bırak.
2. 64×64 alanda, her hareket sınıfı için taşma doldurma (flood fill, 4 yön, yığın tabanlı; özyineleme yok) yap ve yeni region id'leri ata.
3. Chunk kenarlarında karşı chunk'taki tile'ların region'larıyla kenar (edge) oluştur. Komşu chunk'ların da edge listesi güncellenir.
4. Etkilenen region'ların ada kimliklerini geçersiz işaretle.

**Ada hesaplama:** Geçersiz işaretlenen tüm region'lar üzerinde union-find (yol sıkıştırmalı). Ada sayısı değiştiğinde `IslandsChangedEvent` yayınlanır (Bölüm 3'teki yol önbellekleri buna göre temizlenir).

**Bütçe:** Faz 8'de tick başına en fazla **4 chunk** yeniden hesaplanır. Toplu değişiklik (büyük bomba) daha fazla chunk'ı kirletirse iş birkaç tick'e yayılır. Bu sürede birimler eski veriyle hareket eder; yol üzerinde yürünemeyen tile'a gelince yol yeniden istenir.

**Doğrulama testi:** Rastgele 1000 terraform işleminden sonra tam harita flood fill ile artımlı sonuç karşılaştırılır; ada eşlemeleri aynı olmalıdır.

---

## 1.10 Harita üretimi (WorldGen)

### 1.10.1 Ayarlar

```csharp
[Serializable]
public sealed class WorldGenSettings
{
    public ulong  Seed;                         // 0 ise rastgele
    public MapSizePreset Size = MapSizePreset.Medium;
    public string Template = "wgt.continents";  // worldgen_templates.json'dan
    public float  LandRatio      = 0.45f;       // 0.10–0.85  kara/toplam hedefi
    public float  MountainRatio  = 0.08f;       // 0.00–0.25  karanın dağ oranı
    public float  HillRatio      = 0.12f;       // 0.00–0.30
    public float  Roughness      = 0.50f;       // 0–1  kıyı girintisi (domain warp gücü)
    public float  ForestDensity  = 0.50f;       // 0–1
    public float  BiomeVariety   = 0.50f;       // 0–1  kaç farklı biyom
    public float  OreDensity     = 0.50f;       // 0–1
    public float  Temperature    = 0.50f;       // 0 soğuk – 1 sıcak (global kayma)
    public float  Moisture       = 0.50f;
    public bool   SpawnAnimals   = true;        // Bölüm 3'te doldurulur
    public bool   SpawnCivs      = false;
}
```

### 1.10.2 Şablon tanımı

```json
{
  "id": "wgt.continents",
  "mask": { "type": "multiBlob", "count": [2, 4], "radius": [0.25, 0.4], "falloffPower": 2.2 },
  "noise": { "octaves": 6, "frequencyMul": 1.0, "lacunarity": 2.0, "gain": 0.5 },
  "warp":  { "amplitude": 0.08, "frequencyMul": 2.0 },
  "edgeOcean": 0.06
}
```

| Şablon | Maske tipi | Açıklama |
|--------|-----------|----------|
| `wgt.continents` | `multiBlob` 2–4 | 2–4 büyük kıta |
| `wgt.pangea` | `radial` | Ortada tek büyük kara |
| `wgt.archipelago` | `none` + yüksek frekans | Çok sayıda ada; `LandRatio` varsayılanı 0.30 |
| `wgt.ring` | `annulus` | Halka şeklinde kıta, ortada iç deniz |
| `wgt.lakes` | `inverse` | Kara ağırlıklı, içinde göller |
| `wgt.flat_green` | `full` | Tamamen düz alçak toprak + çayır |
| `wgt.empty_ocean` | `empty` | Sadece derin okyanus (sıfırdan çizmek için) |
| `wgt.custom_image` | `image` | PNG'den içe aktarma (1.10.6) |

### 1.10.3 Adımlar ve formüller

Tüm adımlar `IJobParallelFor` + Burst ile satır bazlı paralel çalışır. Titanik haritada hedef toplam süre < 1,5 sn.

**Adım 1 — Temel yükseklik (float `h[x,y]`, 0..1):**

```
u, v        = x / W, y / H                                  // 0..1 normalize
(wu, wv)    = (u, v) + warpAmp * (noiseW1(u,v), noiseW2(u,v)) // domain warp
base        = FBM(wu, wv; octaves, freq0 = 3.0 * frequencyMul, lacunarity, gain)   // -1..1 → 0..1
h           = base * mask(u, v)
h          *= edgeFade(u, v, edgeOcean)                     // harita kenarı her zaman okyanus
```

- `FBM`: FastNoiseLite OpenSimplex2, oktav toplamı `Σ gain^k * noise(freq * lacunarity^k)` ve sonra normalize.
- `mask` fonksiyonları:
  - `radial`: `m = 1 - smoothstep(0, R, dist(u,v, 0.5,0.5))^falloffPower`
  - `multiBlob`: rastgele N merkez (Poisson ile birbirinden ≥ 0.3 uzak), `m = max_k radial_k`
  - `annulus`: `m = 1 - |dist - Rmid| / width` (0'ın altı 0'a kırpılır)
  - `inverse`: `m = 1 - 0.6 * radialNoise`
- `edgeFade`: kenardan `edgeOcean` oranındaki mesafe içinde `smoothstep` ile 0'a iner.

**Adım 2 — Deniz seviyesi (yüzdelik ile):** `h` dizisinin histogramı (1024 kova) çıkarılır. `seaLevel = percentile(h, 1 - LandRatio)`. Böylece hangi şablon olursa olsun kara oranı hedefi tutar.

**Adım 3 — Seviyelere dönüştürme:** Kara ve su ayrı yüzdeliklerle bölünür.

| Koşul | Tip |
|-------|-----|
| `h < percentile_su(0.55)` | derin okyanus |
| `h < percentile_su(0.85)` | okyanus |
| `h < seaLevel` | sığ su |
| kara yüzdeliği < 0.06 | kum (kıyı) |
| < `1 - MountainRatio - HillRatio - 0.25` | alçak toprak |
| < `1 - MountainRatio - HillRatio` | yüksek toprak |
| < `1 - MountainRatio` | tepe |
| < `1 - MountainRatio * 0.15` | dağ |
| üstü | zirve |

**Adım 4 — Temizlik (hücresel otomat, 2 geçiş):** Bir tile'ın 8 komşusundan en az 6'sı farklı tek bir tipse, tile o tipe dönüştürülür. Tek pikselli adacıklar ve delikler kaybolur. Ayrıca 20 tile'dan küçük su kütleleri (iç göller) kalabilir, ancak 3 tile'dan küçük kara parçaları silinir.

**Adım 5 — Kıyı düzeltmesi:** Kara ile derin okyanus doğrudan komşuysa araya en az 1 sığ su tile'ı eklenir. Kum yalnızca suya komşu kara tile'larında kalır.

**Adım 6 — Nem ve sıcaklık alanları (0..1):**
```
moisture    = FBM2(u,v, octaves 4, freq 2.0) + 0.25 * coastProximity   → Moisture ayarıyla kaydır, kırp
temperature = 1 - |v - 0.5| * 1.6            // ekvator sıcak, kutuplar soğuk
            - 0.35 * max(0, level - 4) / 4   // yükseklik soğutur
            + 0.15 * FBM3(u,v)
            + (Temperature - 0.5)             // global ayar
```
`coastProximity`: sudan uzaklık alanından türetilir (iki geçişli mesafe dönüşümü, chamfer 3-4).

**Adım 7 — Biyom ataması:** Sadece `canHaveBiome` tile'lara. `(sıcaklık, nem)` çiftinden `biome_table.json` içindeki 6×6 ızgarayla seçilir. `BiomeVariety` düşükse yalnızca "temel" etiketli biyomlar kullanılır; yüksekse özel biyomlar (kristal, mantar, büyülü) rastgele **yama** olarak eklenir: `count = round(BiomeVariety * chunks * 0.4)` adet yama, her biri 150–800 tile, flood-fill ile büyütülür.

Örnek tablo (sıcaklık satır, nem sütun; tüm biyomlar Bölüm 2'de tanımlanır):

| T \ N | 0–0.17 | –0.33 | –0.5 | –0.67 | –0.83 | –1 |
|------|--------|-------|------|-------|-------|----|
| 0–0.17 | tundra | tundra | tundra | kar ormanı | kar ormanı | kar ormanı |
| –0.33 | kaya | çayır | huş | huş | kar ormanı | bataklık |
| –0.5 | kaya | çayır | çayır | orman | orman | bataklık |
| –0.67 | savan | çayır | çiçek | orman | akçaağaç | bataklık |
| –0.83 | çöl | savan | savan | orman | cengel | cengel |
| –1 | çöl | çöl | savan | cengel | cengel | cengel |

**Adım 8 — Varyant:** `Variant[i] = hash(x, y, seed) & 3`, ardından düşük frekanslı gürültüyle kümelenir (yan yana piksellerin aynı varyantı tutma olasılığı artar). Bu, "tuz-biber" görüntüsü yerine hafif lekeli bir doku verir.

**Adım 9 — Kaynaklar ve bitkiler:** Bölüm 2'de tanımlanan `FeatureDef` kayıtlarına göre yerleştirilir. Bu bölümde yalnızca iskelet: tepe/dağ tile'larında maden damarları için **Poisson disk** örneklemesi (min uzaklık = `lerp(18, 6, OreDensity)` tile), her noktada 3–9 tile'lık damar.

**Adım 10 — Başlangıç canlıları:** Bölüm 3 hazır olduğunda doldurulur.

### 1.10.4 WorldGen API

```csharp
public static class WorldGenerator
{
    public static WorldMap Generate(WorldGenSettings s, ContentDB db, IProgress<float> progress = null);
    public static JobHandle GenerateAsync(WorldGenSettings s, ContentDB db, out WorldMap result);
}
```

Üretim ayrı iş parçacığında çalışır; UI'da ilerleme çubuğu gösterilir (her adım bir yüzde aralığına karşılık gelir).

### 1.10.5 Üretim ekranı parametreleri

UI kaydırıcıları doğrudan `WorldGenSettings` alanlarına bağlanır. "Rastgele" butonu her alanı kendi aralığında rastgele seçer. "Önizleme" 1/4 çözünürlükte üretim yapar (Titanik için bile < 150 ms).

### 1.10.6 PNG'den içe aktarma

- Görüntü harita boyutuna **en yakın komşu** yöntemiyle ölçeklenir.
- Her piksel, tüm tile ve biyom renklerinin ortalamasına en yakın renge (CIE76, Lab uzayında) eşlenir.
- Tanınmayan renkler: parlaklığa göre seviye tahmini (koyu mavi → derin okyanus, beyaz → zirve).

---

## 1.11 Harita render'ı

### 1.11.1 Yaklaşım

- Her chunk için **64×64 `Texture2D`** (RGBA32, `FilterMode.Point`, mipmap yok) ve bir quad.
- Quad'lar tek bir `Mesh` içinde birleşir (tek draw call); texture'lar `Texture2DArray` içinde katman olarak tutulur. Böylece Titanik haritada bile 256 chunk tek draw call ile çizilir.
- Chunk'ın `Dirty & Render` bayrağı varsa CPU'da renk dizisi yeniden hesaplanır ve `SetPixelData` + `Apply(false)` ile GPU'ya yüklenir.
- **Yükleme bütçesi:** frame başına en fazla **12 chunk**. Kamera görüş alanındaki chunk'lar önceliklidir.

### 1.11.2 Piksel rengi hesabı (Burst job)

```
tip      = Ground[i]
renk     = TileColors[tip][Variant[i]]
if Biome[i] != 0:   renk = BiomeColors[Biome[i]][Variant[i]]      // biyom zemini
if SnowCover:       renk = lerp(renk, kar_rengi, 0.85)
if Burning:         renk = ateş paleti[Fire[i] >> 6]
if Road:            renk = yol rengi
// kabartma (relief) gölgesi
dNorth   = Level(tip) - Level(Ground[i - Width])
renk    *= 1 + clamp(dNorth, -2, 2) * reliefShade
// alfa kanalı → shader için malzeme kodu
a        = water ? 1 : (lava ? 2 : 0)    // 0..255 aralığına ölçeklenir
```

### 1.11.3 Shader (URP 2D, HLSL)

- Alfa kanalı `1` olan pikseller su: `sin(time*1.5 + worldPos.x*0.7 + worldPos.y*0.4)` ile renk parlaklığı ±%6 oynatılır ve 8×8 hücrede rastgele parlama pikselleri çıkar (`hash(cell, floor(time*2))`).
- Alfa `2` olan pikseller lav: yavaş renk titreşimi ve emisyon.
- Çağ renk filtresi (Bölüm 2) shader'a `_EraTint` global değişkeniyle verilir.
- Overlay katmanı (krallık bölgeleri vb.) ayrı bir zone çözünürlüklü texture ile üst üste harmanlanır (Bölüm 8).

### 1.11.4 Katman sırası (sorting)

| Sıra | Katman |
|------|--------|
| 0 | Harita chunk'ları |
| 10 | Tile özellikleri (ağaç, bitki, kaynak) |
| 20 | Binalar |
| 30 | Yerdeki birimler |
| 40 | Mermiler, partiküller |
| 50 | Havadaki birimler (uçanlar, fırlatılanlar), düşen nesneler |
| 60 | Bulutlar |
| 70 | Zone overlay |
| 80 | İsim plakaları ve armalar (dünya uzayında UI) |

Sözde-3D yükseklik (Z) olan nesneler ekranda `y + z` konumunda çizilir; gölgeleri `y` konumunda, katman 29'da ayrı çizilir (Bölüm 7).

---

## 1.12 Kamera

### 1.12.1 Parametreler

```csharp
[Serializable]
public sealed class CameraSettings
{
    public float MinOrthoSize   = 6f;      // en yakın: ekranda ~12 tile yükseklik
    public float MaxOrthoFactor = 0.6f;    // en uzak: haritanın yüksekliği * 0.6
    public float ZoomStep       = 1.15f;   // tekerlek başına çarpan
    public float ZoomLerp       = 12f;     // yumuşak zoom hızı
    public float PanSpeedKeys   = 1.2f;    // ekran yüksekliği/sn
    public float EdgePanMargin  = 8f;      // px, kenar kaydırma (ayarlardan kapatılabilir)
    public float PanInertia     = 6f;      // sürükle-bırak sonrası sönümleme
    public float OverscrollTiles = 32f;    // harita dışına çıkma payı
}
```

### 1.12.2 Davranışlar

- **İmlece doğru zoom:** Zoom sırasında imleç altındaki dünya noktası ekranda sabit kalır. Formül: `camPos += (cursorWorldBefore - cursorWorldAfter)`.
- **Piksel mükemmelliği:** Kamera konumu, ekran pikseline hizalanır (`round(pos * pixelsPerUnit) / pixelsPerUnit`). Böylece zoomda kayma titremesi olmaz.
- **Sürükleme:** Orta tuş ya da boşluk + sol tuş (fırça aktifken sol tuş fırça içindir). Dokunmatikte iki parmak kaydırma ve pinch zoom.
- **Takip modu:** `Follow(EntityId)` çağrılırsa kamera hedefe `lerp` ile yaklaşır; kullanıcı kaydırırsa takip biter.
- **LOD seviyesi** ortografik boyuttan türetilir ve diğer sistemlere yayınlanır:

| LOD | Ekranda görünen tile yüksekliği | Görünen |
|-----|--------------------------------|---------|
| 0 Yakın | < 60 | Tüm birim detayları, isim etiketleri, eşya pikselleri |
| 1 Orta | 60–180 | Birimler (basit), şehir isimleri |
| 2 Uzak | 180–450 | Birimler nokta; krallık isimleri ve armalar |
| 3 Harita | > 450 | Birimler gizli; overlay otomatik açılır (ayarlanabilir) |

### 1.12.3 Girdi eylemleri (Input System)

| Eylem | Klavye/Fare | Dokunmatik |
|-------|-------------|------------|
| `Pan` | WASD / oklar / orta tuş sürükleme | 2 parmak kaydırma |
| `Zoom` | Tekerlek, `+`/`-` | Pinch |
| `UsePower` | Sol tuş (basılı tutulabilir) | Tek parmak |
| `CancelPower` | Sağ tuş, `Esc` | Güç ikonuna tekrar dokunma |
| `Pause` | Space | HUD butonu |
| `Speed1..7` | 1–7 tuşları | HUD |
| `Step` | `.` | – |
| `BrushSizeUp/Down` | `]` / `[` | HUD kaydırıcı |

---

## 1.13 İlk güçler: terraform fırçaları

Bölüm 7'de tam güç çerçevesi kurulacak. Burada, sonradan bu çerçeveye taşınacak biçimde temel fırça altyapısı yazılır.

### 1.13.1 Fırça tanımı

```csharp
public enum BrushShape : byte { Circle, Square }

public readonly struct BrushSpec
{
    public readonly BrushShape Shape;
    public readonly int  Radius;           // 0 = tek tile
    public readonly float Density;         // 1 = tüm tile'lar, <1 = rastgele serpme (ağaç ekme gibi)
}

public static readonly int[] BrushRadii = { 0, 1, 2, 3, 5, 8, 12, 20, 32 };
```

### 1.13.2 Darbe (stamp) ve vuruş (stroke)

- **Stamp:** Merkez `(cx, cy)` etrafında şekle göre tile'ları dolaşır. Daire için `dx² + dy² <= r² + r` (kenarları yumuşatır).
- **Stroke:** Fare hızlı hareket edince iki frame arasındaki konumlar boşluk bırakmasın diye önceki ve şimdiki merkez arası Bresenham çizgisi çizilir ve `max(1, r/2)` adım aralığıyla stamp uygulanır.
- **Uygulama sıklığı:** Basılı tutuldukça fırça tipine göre `ApplyIntervalTicks` (ör. yükselt/alçalt 3 tick, doğrudan boyama her frame).

```csharp
public interface ITileBrushOp
{
    void Apply(TileEditBatch batch, int x, int y, ref SimRandom rng);
}

public sealed class SetGroundOp   : ITileBrushOp { public TileTypeId Type; }
public sealed class RaiseOp       : ITileBrushOp { }         // RaiseLevel
public sealed class LowerOp       : ITileBrushOp { }
public sealed class SpongeOp      : ITileBrushOp { }         // su → kum/alçak toprak, sığ öncelikli
public sealed class EraserOp      : ITileBrushOp { }         // bina/bitki/biyom sil, zemini korur

public static class BrushExecutor
{
    public static void Stroke(WorldMap map, BrushSpec brush, int2 from, int2 to,
                              ITileBrushOp op, ref SimRandom rng, ChangeSource src = ChangeSource.Power);
}
```

### 1.13.3 Ilk güç seti

| Güç | Op | Not |
|-----|----|-----|
| Toprak | `SetGroundOp(soil_low)` | Su üzerine uygulanırsa önce sığa, sonra kuma, sonra toprağa geçer (her uygulamada 1 kademe) |
| Kum | `SetGroundOp(sand)` | |
| Tepe | `RaiseOp` sınırı `hills` | |
| Dağ | `RaiseOp` sınırı `summit` | |
| Sığ su / Su / Derin su | `LowerOp` sınırı ilgili seviye | |
| Sünger | `SpongeOp` | |
| Silgi | `EraserOp` | |

**Kademeli uygulama kuralı:** Toprak ve su güçleri tile'ı hedef tipe doğrudan değil, **her uygulamada bir seviye** yaklaştırır. Oyuncu fırçayı basılı tuttukça dağ yavaş yavaş yükselir veya deniz yavaş yavaş derinleşir. Bu, türün kendine özgü "yoğurma" hissini verir.

### 1.13.4 İmleç önizlemesi

Fırça alanı, dünya üzerinde yarı saydam bir piksel çerçeve olarak çizilir (tek `LineRenderer` yerine basit bir mesh). Etkilenecek tile'lar Bölüm 7'deki güç çerçevesinde renklendirilir.

---

## 1.14 Kayıt temeli (Persistence v1)

Tam format Bölüm 9'da genişletilecek. Bu bölümde dünya ve saat kaydedilir.

### 1.14.1 Dosya yapısı

```
[Header]    magic "PXGN" | formatVersion:int | gameVersion:string | createdUtc:long
            | worldName:string | year:int | population:int | sizeX:int | sizeY:int
[Thumbnail] PNG baytları (256×256 mini harita)
[Section*]  sectionId:int | compressedLength:int | rawLength:int | LZ4(MessagePack(payload))
```

Bölümler (Section): `ContentMap` (metin anahtarı ↔ sayısal id tabloları), `Clock`, `Rng`, `WorldTiles`, `WorldZones`. Sonraki bölümlerde `Units`, `Buildings`, `Meta`, `History`, `Stats`, `Laws` eklenir.

### 1.14.2 Tile sıkıştırması

Her chunk ayrı paketlenir. `Ground`, `Biome`, `Variant`, `Flags` dizileri **RLE** (run-length) ile kodlanır, ardından tüm bölüm LZ4 ile sıkıştırılır. Tipik bir Titanik harita yaklaşık 1,5–3 MB tutar.

`Region` dizisi kaydedilmez; yüklemeden sonra baştan hesaplanır.

### 1.14.3 Sürümleme

```csharp
public interface ISaveMigration { int FromVersion { get; } void Migrate(SaveDocument doc); }
```

Yükleme: dosyanın `formatVersion`'ı güncelden küçükse migrasyonlar sırayla uygulanır. `ContentMap` sayesinde silinmiş içerik anahtarları yedek değere (ör. bilinmeyen biyom → `0`) eşlenir ve bir uyarı listesinde gösterilir.

### 1.14.4 API

```csharp
public static class SaveSystem
{
    public static string SlotsFolder { get; }                     // Application.persistentDataPath/saves
    public static Task SaveAsync(int slot, GameState state);      // simülasyon kopyası alınır, yazma arka planda
    public static Task<GameState> LoadAsync(int slot, ContentDB db);
    public static IReadOnlyList<SaveSlotInfo> ListSlots();        // header + thumbnail okunur, gövde okunmaz
    public static void Delete(int slot);                          // kullanıcı onayıyla
}
```

**Tutarlılık:** Kayıt, bir tick sınırında alınan anlık kopya (snapshot) üzerinden yapılır. `NativeArray`'ler `CopyTo` ile kopyalanır; ağır sıkıştırma işi ana döngüyü bekletmez. Dosya önce `.tmp` olarak yazılır, sonra atomik olarak yeniden adlandırılır.

**Otomatik kayıt:** Her 10 oyun yılında bir ve çıkışta, döngüsel 3 slota.

---

## 1.15 Hata ayıklama araçları (ilk günden)

- **Debug overlay** (F3): FPS, tick/sn, tick süresi (ms, sistem başına), dirty chunk sayısı, region sayısı, ada sayısı, imleç altındaki tile bilgisi (tip, biyom, bayraklar, zone, chunk, region, ada).
- **Region görselleştirme** (F4): her region farklı renkte, ada sınırları kalın.
- **Determinizm kontrolü:** `--determinism-check 1000` komut satırı argümanıyla oyun aynı seed'le iki kez 1000 tick koşturulur ve her 100 tick'te dünya dizilerinin hash'i (xxHash64) karşılaştırılır.
- **Profiler işaretçileri:** Her `ISimSystem.Tick` çağrısı `ProfilerMarker` ile sarılır.

---

## 1.16 Bölüm 1 testleri ve kabul kriterleri

| # | Test | Kriter |
|---|------|--------|
| 1 | Aynı seed + ayar ile iki üretim | Bayt bayt aynı `Ground` ve `Biome` dizileri |
| 2 | 7 şablon × 7 boyut üretimi | Hepsi hatasız; kara oranı hedefin ±%3'ü içinde |
| 3 | Titanik harita üretim süresi | < 1,5 sn (orta seviye 8 çekirdekli CPU) |
| 4 | 1000 rastgele fırça işlemi sonrası region | Artımlı sonuç = tam yeniden hesap sonucu |
| 5 | Kaydet → yükle → kaydet | İki dosyanın gövde hash'leri aynı |
| 6 | Titanik harita, sürekli kaydırma + zoom | ≥ 60 FPS, frame başına GC ayırması 0 bayt |
| 7 | Büyük fırça (r=32) ile sürekli boyama | Frame süresi < 16 ms, render gecikmesi ≤ 2 frame |
| 8 | Hız ×20 + Süper hız | Tick'ler kesintisiz; UI donmuyor |
| 9 | İçerik doğrulayıcı | Bozuk JSON'da açılışta anlaşılır hata listesi |

---

## 1.17 Görev listesi (uygulama sırası)

| # | Görev | Tahmini süre |
|---|-------|--------------|
| 1 | Proje kurulumu, paketler, asmdef yapısı, `GameBootstrap` sahnesi | 0,5 gün |
| 2 | `SimConst`, `GameClock`, `SimulationRunner`, `ISimSystem` hattı | 1 gün |
| 3 | `SimRandom`, `SimRandomProvider`, birim testleri | 0,5 gün |
| 4 | `IdPool`, `EntityId`, `EventBus` | 1 gün |
| 5 | `ContentDB`, `Registry<T>`, JSON yükleyici, doğrulayıcı, `tiles.json` | 1,5 gün |
| 6 | `WorldMap` SoA dizileri ve yazma API'si, `TileEditBatch` | 1,5 gün |
| 7 | Region/ada sistemi + görselleştirme + doğrulama testi | 2,5 gün |
| 8 | WorldGen adımları 1–8 (Burst job'ları), şablonlar, ayar ekranı | 3 gün |
| 9 | Chunk texture render'ı, `Texture2DArray`, su/lav shader'ı | 2 gün |
| 10 | Kamera, LOD yayını, girdi eylemleri | 1,5 gün |
| 11 | Fırça altyapısı ve ilk terraform güçleri, geçici güç çubuğu | 2 gün |
| 12 | Kayıt v1, slot listesi, otomatik kayıt | 2 gün |
| 13 | Debug overlay, determinizm kontrolü, profil işaretçileri | 1 gün |
| 14 | Test seti ve performans ölçümü | 1,5 gün |
| | **Toplam** | **~22 iş günü (tek geliştirici)** |

---

**Sonraki bölüm:** Bölüm 2 — Doğa, İklim ve Zaman. İçerik: biyom tanımları ve yayılma algoritması, ağaç/bitki (`Feature`) sistemi ve render'ı, zone sıcaklık hesabı, bulut varlıkları ve yağış türleri, yangın yayılımı ve söndürme, kar/buz/lav döngüleri, takvim olayları, 10 çağın parametreleri ve çağ saati.
