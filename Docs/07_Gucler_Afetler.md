# BÖLÜM 7 — Tanrı Güçleri ve Afetler

Bu bölüm bittiğinde `powers.json`'daki 246 gücün hepsi çalışır: fırçalar, gökten düşen nesneler, spawn'lar, hedefli güçler, patlamalar, doğal felaketler, hücresel otomatlar. Otomatik afetler de aynı işleyicileri kullanır.

Bağımlılık: Bölüm 1–6. İçerik: `powers.json`, `disasters.json`, `clouds.json`, `achievements.json` (kilitler).

---

## 7.1 Güç çatısı

### Tanım
```csharp
public sealed class PowerDef : IContentDef {
    public string Id, Name, Tab, Type, UnlockedBy, Description;
    public PowerParams Params;               // mods() çıktısı: add/pct/flags + düz anahtarlar
}
public enum PowerType { Brush, Drop, Spawn, Target, Window, Toggle }
```

### İşleyici eşlemesi
Her gücün davranışı parametrelerindeki **ana anahtardan** seçilen bir işleyiciyle çalışır. Aynı işleyici birçok gücü karşılar:

| Ana anahtar | İşleyici | Güçler (örnek) |
|-------------|----------|----------------|
| `op` | `TileBrushHandler` (Bölüm 1.13) | soil, sand, raise, sponge, road, field, eraser, extinguish, ice, cool_lava, zone_add |
| `spawn:biome_tree` / `spawn:feat.*` | `FeatureDropHandler` | tree, bush, ore_* |
| `spawn:seed` | `BiomeSeedHandler` (Bölüm 2.3) | seed_* |
| `species` | `UnitSpawnHandler` | spawn_* |
| `spawn:cloud.*` | `CloudSpawnHandler` | cloud_* |
| `spawn:village` | `VillageSpawnHandler` | found_village |
| `spawn:tornado`, `spawn:volcano`, `spawn:geyser`, `spawn:monolith`, `spawn:lure` | `WorldObjectHandler` | tornado, volcano, geyser, monolith, golden_brain |
| `explosion` | `ExplosionDropHandler` | bomb, dynamite, atomic, antimatter, meteor, fireball, cluster |
| `fire`, `freeze`, `meltTile`, `dmg`, `lightning` | `ElementalDropHandler` | fire, napalm, ice_bomb, acid, lightning, storm |
| `infect`, `cure` | `DiseaseHandler` | plague, rotbite, spores, madness, divine_light, zombie_serum |
| `target` + `opinion`/`loyalty`/`makeKing`... | `MetaTargetHandler` | friendship, spite, crown, rebellion_spark, capital |
| `status` | `StatusTargetHandler` | inspire, blessing, curse, bless_city |
| `grab` | `MagnetHandler` | magnet |
| `quake`, `wave`, `flood`, `pull`, `ray` | `CataclysmHandlers` | earthquake, tsunami, flood, black_hole, heat_ray |
| `automaton` | `AutomatonHandler` | life_game, ant |
| `poke`, `knockback` | `PhysicsHandler` | finger, shockwave, stomp |
| `window`, `camera`, `possess`, `open`, `control`, `highlight` | UI katmanı (Bölüm 8) | stats, follow, possess, eye |

```csharp
public interface IPowerHandler {
    bool CanApply(in PowerContext c);                 // kilit, hedef türü, kanun
    void Begin(in PowerContext c);                    // tıklama başı
    void Apply(in PowerContext c, int2 tile);         // her uygulama (fırça: her stamp)
    void End(in PowerContext c);
}
public struct PowerContext { public ushort PowerId; public BrushSpec Brush; public int2 CursorTile; public int TargetUnit, TargetCity, TargetKingdom; public SimContext Sim; }
```

### Komut kuyruğu (determinizm)
Oyuncu girdisi simülasyonu doğrudan değiştirmez. UI → `PowerCommand{powerId, tile, brush, target, tick}` kuyruğa yazılır; simülasyon Faz 0'da (Input Commands) sırayla uygular. Böylece tekrar oynatma (replay) ve determinizm korunur. Kaydedilen komut günlüğü (isteğe bağlı) hata raporlarında kullanılır.

### Kilitler
`unlockedBy` başarımı kazanılmamışsa güç çubukta kilit ikonuyla görünür ve uygulanamaz. `law.forbidden_codex` açıkken tüm kilitler açıktır (o dünyada başarım kazanılmaz).

---

## 7.2 Düşen nesneler (sözde-3D)

```csharp
public struct DropData { public ushort Power; public float2 Pos; public float Z, VZ; public int Seed; public float Delay; }
```
- Başlangıç: `Z = 40` (tile), `VZ = 0`; yerçekimi `g = 60 tile/s²`. Yere düşme süresi ≈ 1,15 sn — bu gecikme türün imza hissidir.
- Düşerken sprite + küçülen gölge; `Z` ≤ 0 olunca işleyicinin `Apply` metodu çağrılır.
- `dynamite`: yere düştükten sonra `Delay = 30` tick fitil animasyonu.
- `cluster`: yere değince 8 alt düşüş (yarıçap 10 içinde rastgele, `Z = 8`).
- Basılı tutulan düşme güçleri her 6 tick'te bir yeni damla üretir (fırça boyutu içinde rastgele konum).
- En fazla 512 aktif düşüş.

---

## 7.3 Patlama sistemi

```csharp
public struct ExplosionSpec { public float Radius, Damage, Knockback, FireChance; public byte TileEffect; public bool Irradiate, DeleteTiles; }
```
| Boyut | Yarıçap | Hasar (merkez) | İtme | Tile etkisi | Yangın |
|-------|---------|----------------|------|-------------|--------|
| `small` | 4 | 120 | 6 | Merkez %50 `pit`, halka `scorched` | %30 |
| `medium` | 10 | 400 | 10 | Merkez seviye −2, halka `scorched` | %40 |
| `huge` | 40 | 5.000 | 20 | Merkez 1/3: seviye −3, `wasteland` + `Irradiated`; dış halka `scorched` | %60 |
| `void` | 60 | ∞ | 0 | Yarıçap içi tüm tile → derin okyanus; birim/bina/feature silinir | — |

Uygulama (tek seferlik, Burst job ile tile halkası; birimler uzamsal sorguyla):
```
her tile t (mesafe d ≤ R):
    f = 1 − d/R
    tile etkisi f eşiklerine göre (merkez f>0.66, orta f>0.33, dış)
    binaya hasar = Damage × f × 2
    feature: f>0.33 → sil, aksi hâlde yanabilirse tutuştur
her birim u (d ≤ R):
    hasar = Damage × f² × (1 − zırh azaltması × 0.5)
    itme: u.VZ += Knockback × f;  yatay hız = normalize(u − merkez) × Knockback × f
    Irradiate → st.irradiated
ekran sarsıntısı: şiddet ∝ R, kamera uzaklığıyla azalır
partikül: halka şok dalgası + duman + kıvılcım (havuzlu)
```
Büyük patlamalar tile değişikliklerini tek `TileEditBatch` içinde yapar (region yeniden hesabı bütçeli dağılır, Bölüm 1.9).

---

## 7.4 Fırlatılan birim fiziği

`Z > 0` olan birimler `PhysicsSystem` tarafından güncellenir: `VZ −= g × dt`, yatay hız sürtünmesiz uygulanır, suya düşerse sıçrama efekti, yere çarpınca düşme hasarı (Bölüm 3.5). Fırlatılan birim havadayken eylem yapmaz. `pw.finger`: tıklanan birime `VZ = 25` ve rastgele yatay 4–8.

---

## 7.5 Özel işleyiciler

| Güç | Algoritma |
|-----|-----------|
| **Hortum** | Dünya nesnesi; rastgele yürüyüş (rüzgâr yönüne %60 eğilimli), hız 3 tile/sn, 600 tick. Yarıçap 4 içindeki birimler ve düşen nesneler havaya kalkar (`VZ` += 3/tick, dönme), 6 tile dışarı savrulur. Ağaçları söker (feature → düşen nesne), binalara 10 hasar/tick. |
| **Deprem** | Tıklanan noktadan iki yöne rastgele yürüyüşle çatlak hattı (uzunluk 30–60). Hat üzerindeki tile'lar bir seviye aşağı (%50) veya yukarı (%20); hat çevresindeki 8 tile yarıçapında binalara 150 hasar, birimlere `st.stunned`. Kamera sarsıntısı 2 sn. |
| **Tsunami** | En yakın derin sudan tıklanan kıyıya doğru dalga cephesi (genişlik 40). Cephe her 4 tick ilerler; geçtiği kara tile'ları (seviye ≤ 5) 300 tick boyunca sığ suya çevirir, sonra eski hâline döner (orijinal tipler geçici haritada saklanır). Binalara 300 hasar, birimleri iter ve suya sürükler. |
| **Sel** | Yarıçap 25 içindeki alçak toprak/kum tile'ları 600 tick sığ su olur, sonra geri döner. Tarlalar yok olur. |
| **Yanardağ** | Tıklanan yerde 3 tick'te bir seviye yükselen tepe → dağ → zirve; sonra 400 tick boyunca tepeden `lava_hot` damlaları (her 10 tick) ve `cloud.ash`. Bitince soğuyan lav + `feat.ore_obsidian`. |
| **Gayzer** | Kalıcı nesne; her 300 tick buhar fışkırtır: yarıçap 2 birimleri fırlatır, yangın söndürür. |
| **Göktaşı** | `medium` patlama + merkezde `feat.ore_skyiron` damarı. |
| **Yıldırım** | Tek tile: 150 hasar, `st.stunned` 10, %60 yangın; `stormborn`/`immune:lightning` etkilenmez; `bio.celestial` üstüne düşmez. |
| **Kara delik** | 400 tick boyunca yarıçap 25: birimler ve düşen nesneler merkeze çekilir (ivme ∝ 1/d), merkeze ulaşanlar yok olur (`DeathCause.Void`). Tile'lar merkeze yakınlıkla `pit` olur. `ach.world_eater` sayacı. |
| **Isı ışını** | Fırça gibi sürüklenir; imleç altındaki tile'a 40 hasar/tick, tile seviyesini 20 tick'te bir düşürür (dağı eritir), yangın. |
| **Yutan balçık** | `tile.goo` bırakır; yayılım Bölüm 2.10 kurallarıyla (`law.goo_spread`). |
| **Mıknatıs** | Basılıyken fırça alanındaki en fazla 50 birimi tutar (`Z = 3`, imleci takip eder); bırakınca düşerler (yüksekten bırakılırsa düşme hasarı). |
| **Dürtme / şok dalgası / ezme** | Fizik: tek birime yüksek itme / halka itme / yarıçap 2'de anında ölüm + bina ezme. |
| **Çağır (spawn)** | Fırça boyutu kadar birim (yarıçap 0 → 1 birim, 1 → 3, 2 → 6...). Meta atama kuralı: 20 tile içinde aynı tür varsa onun meta'ları (Bölüm 6.1). Medeni türler başlangıçta yetişkin, hayvanlar rastgele yaş. |
| **Köy kur** | Seçili son medeni tür için 8 yetişkin + kamp ateşi + 2 çadır; şehir kurma yordamını (Bölüm 5.2) zorla çalıştırır. |
| **Monolit** | Bölüm 4.7 nesnesi. |
| **Altın beyin** | Kalıcı yem nesnesi (10 yıl); `brains` diyetli birimlerin `lure_golden_brain` nöronu en yüksek puanı verir. |
| **Arınma serumu / kutsal ışık** | Fırça alanındaki birimlerde hastalık, `st.turning`, delilik, lanet temizlenir; kutsal ışık zombilere 200 kutsal hasar verir. |
| **Ölüleri kaldır** | Fırça alanındaki cesetleri (Bölüm 3.1) zombi olarak kaldırır (Bölüm 4.9 varyant kuralı). |

---

## 7.6 Hücresel otomatlar

- **Hayat Oyunu:** Ayrı bir bit ızgarası (tile çözünürlüğünde, yalnızca fırçayla boyanan bölgelerin sınır kutusunda). Her 4 tick'te bir Conway kuralı (B3/S23) bir Burst job'la uygulanır. Canlı hücreler haritada pembe piksel olarak çizilir; üzerindeki birimlere 5 hasar/adım, binalara 2. Canlı hücre sayısı 0 olunca kutu silinir. En fazla 200.000 canlı hücre.
- **Tur Karıncası (Langton):** Karınca nesnesi; her tick: bulunduğu tile'ın "otomat bayrağı" kapalıysa sağa dön, açıksa sola; bayrağı çevir; ileri git. Bayraklı tile'lar zemin rengini değiştirir (görsel), 10.000 adımdan sonra "otoyol" deseni ortaya çıkar. En fazla 16 karınca.

---

## 7.7 Otomatik afet yürütücüsü

`DisasterExecutor` (Powers katmanı) `DisasterRequestEvent`'leri dinler ve eşlenen işleyiciyi çalıştırır:

| Afet | İşleyici |
|------|----------|
| `dis.meteor` | Göktaşı düşüşü |
| `dis.earthquake`, `dis.tsunami`, `dis.volcano`, `dis.flood`, `dis.tornado` | 7.5'teki ilgili işleyici |
| `dis.famine` | Şehir zone'larında 3–6 yıl tarım verimi ×0.2 bayrağı |
| `dis.plague` | Şehirde 5 rastgele sakine veba |
| `dis.zombie_outbreak` | Mezarlık/kemik düzlüğünden 20–40 zombi |
| `dis.demon_raid` | Yarık nesnesi + 10–30 kor iblisi (3 yıl boyunca dalgalar) |
| `dis.dark_mages`, `dis.dragon`, `dis.sky_visitors`, `dis.bandits` | İlgili türlerden spawn |
| `dis.ice_storm`, `dis.heat_wave` | Geniş alanda sıcaklık ±25 bayrağı (1 yıl) |
| `dis.locusts` | Tarlalar ve bitkiler 60 tile yarıçapta yok |

Her afet bildirim akışına ("Kuzey kıyısında deprem!" + konuma git butonu) ve tarihçeye yazılır.

---

## 7.8 Testler ve kabul kriterleri

| # | Test | Kriter |
|---|------|--------|
| 1 | Güç duman testi | 246 gücün her biri test haritasında 1 kez uygulanır; istisna yok |
| 2 | Atom bombası şehir üstünde | Şehir yok olur, radyasyon alanı kalır, 50 yılda kısmen iyileşir |
| 3 | Karşı madde | Yarıçap içinde tek bir kara tile'ı ve birim kalmaz |
| 4 | 20 eşzamanlı büyük patlama | Frame < 33 ms; region yeniden hesabı birkaç tick'e yayılır |
| 5 | Replay | Aynı komut günlüğü + seed → aynı dünya hash'i |
| 6 | Kilitler | Kilitli güç uygulanamaz; yasak kodeks açınca uygulanır ve başarım kazanılmaz |
| 7 | Tsunami | Geri çekilince tile'lar eski tiplerine döner |

## 7.9 Görev listesi

| # | Görev | Süre |
|---|-------|------|
| 1 | Güç tanımı, işleyici kaydı, komut kuyruğu, kilitler | 2 gün |
| 2 | Düşüş sistemi + render | 1,5 gün |
| 3 | Patlama sistemi + partiküller + kamera sarsıntısı | 2,5 gün |
| 4 | Fizik (fırlatma, düşme) | 1 gün |
| 5 | Spawn, köy, bulut, monolit, yem işleyicileri | 1,5 gün |
| 6 | Doğa afetleri (hortum, deprem, tsunami, sel, yanardağ, gayzer, yıldırım) | 4 gün |
| 7 | Yıkım güçleri (kara delik, ısı ışını, balçık, mıknatıs, dürtme) | 2 gün |
| 8 | Hastalık ve meta hedefli güçler | 1,5 gün |
| 9 | Hücresel otomatlar | 1,5 gün |
| 10 | Afet yürütücüsü | 1 gün |
| 11 | Testler | 1,5 gün |
| | **Toplam** | **~20 gün** |
