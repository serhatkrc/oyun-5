# BÖLÜM 9 — Ses, Kayıt, Modding, Performans ve Yayın

Bu bölüm oyunu "bitmiş ürün" hâline getirir: ses ve müzik, tam kayıt formatı ve sürüm geçişleri, mod desteği, performans bütçeleri ve optimizasyon, test planı, platform yayınları.

Bağımlılık: Tümü. Araç kurulumları için EK B.

---

## 9.1 Ses sistemi

### Veri (`Data/sounds.json` — oluşturulacak)
```json
{ "id": "snd.explosion_small", "files": ["sfx/expl_s_1.wav", "sfx/expl_s_2.wav"], "volume": 0.8,
  "pitch": [0.9, 1.1], "maxInstances": 4, "cooldownMs": 60, "category": "sfx", "worldSpace": true, "minLod": 0, "maxLod": 2 }
```

### AudioManager
- **Havuz:** 32 `AudioSource` (2D). Dünya sesleri stereo pan ile konumlanır (kamera merkezine göre yatay konum), ses seviyesi zoom'a göre (`LOD 0: 1.0 … LOD 3: 0.2`) ve ekrandan uzaklığa göre azalır.
- **Kısıtlar:** Ses başına `maxInstances` ve `cooldownMs`; global eşzamanlı 24 ses. 5.000 birimlik savaşta ses patlaması olmaz — aynı sesin fazla istekleri birleşik "kalabalık" varyantına düşer (`snd.battle_crowd`).
- **Kategoriler:** Master, SFX, UI, Ambient, Music (ayarlardan ayrı ayrı).
- Simülasyon ses tetiklemez; olaylar (`UnitDiedEvent`, `ExplosionEvent`, `BuildingCompletedEvent`...) `SoundEventMap` üzerinden sese dönüştürülür. Böylece headless modda ses kodu hiç çalışmaz.

### Ortam sesi
Kamera görüş alanının baskın içeriğine göre katmanlar: okyanus, orman, şehir uğultusu (görünür şehir nüfusuna orantılı), yangın, yağmur, rüzgâr. Katmanlar 2 sn'lik geçişlerle karışır.

### Müzik
Her çağ için bir parça + "sakin/gergin" iki katman. Gerginlik = görünür alandaki savaş/afet yoğunluğu; katmanlar arasında 4 sn geçiş. Çağ değişince 8 sn çapraz geçiş.

### Hedef ses listesi (asgari)
Güç başına 1 ses (fırçalar ortak "boyama" sesi), patlama ×4 boyut, yıldırım, deprem uğultusu, hortum, yağmur, ateş, su sıçraması, kılıç/ok/büyü vuruşları, ölüm (tür grubuna göre 6 varyant), doğum, bina tamamlandı, bina yıkıldı, çan (savaş ilanı), boru (zafer), UI tıklama/açma/kapama/bildirim, başarım jingle'ı.

---

## 9.2 Tam kayıt formatı

Bölüm 1.14'teki dosya yapısı korunur; bölüm listesi tamamlanır:

| Id | Bölüm | İçerik |
|----|-------|--------|
| 1 | `ContentMap` | Her kategori için metin anahtarı ↔ sayısal id |
| 2 | `Clock` | Tick, hız |
| 3 | `Rng` | Tüm akış durumları |
| 4 | `WorldTiles` | Chunk başına RLE: Ground, Biome, Variant, Flags, Feature, FeatureState, Fire |
| 5 | `WorldZones` | ZoneData (sahip, sıcaklık, baseTemp) |
| 6 | `Nature` | Bulutlar, lav zamanlayıcıları, tohumlar, rüzgâr, çağ durumu, afet son tetikleri, dünya nesneleri (monolit, yem, gayzer, yarık, otomatlar) |
| 7 | `Units` | Birim deposu (yalnız yaşayanlar, SoA sütunları ayrı ayrı sıkıştırılır), cesetler, yumurtalar |
| 8 | `Items` | Eşyalar (sahipli ve yerdekiler) |
| 9 | `Buildings` | Binalar |
| 10 | `Boats`, `Projectiles`, `Drops` | Geçici nesneler |
| 11 | `Meta` | Her MetaStore (header + data + trait setleri), ölü meta'lar dahil |
| 12 | `Stats` | İstatistik serileri |
| 13 | `History` | Tarihçe girişleri |
| 14 | `Names` | NamePool |
| 15 | `Laws` | Kanun değerleri, tanrının adı |
| 16 | `Paths` | Kaydedilmez (yüklemede birimler yeniden ister) |

**Kurallar**
- Tüm çapraz referanslar `EntityId` değil **indeks + generation** olarak yazılır; yüklemede depo aynı yuva düzeniyle yeniden kurulur (serbest yuvalar dahil). Böylece referans çevirisi gerekmez.
- Dizi sütunları ayrı MessagePack blokları (hızlı `MemoryMarshal` kopyası), ardından LZ4.
- Hedef: 5.000 birimli Titanik dünya < 8 MB, kaydetme < 1 sn (arka planda), yükleme < 3 sn.

### Sürüm geçişi
- `formatVersion` her değişiklikte +1; `ISaveMigration` zinciri (Bölüm 1.14.3).
- Silinmiş içerik: `ContentMap`'te karşılığı olmayan anahtar → yedek (bilinmeyen trait silinir, bilinmeyen tür `sp.human`'a değil **silinir**; bilinmeyen biyom 0). Kayıp listesi yükleme sonrası pencerede gösterilir.
- CI testi: her sürümden bir örnek kayıt `Tests/SaveFixtures/` altında saklanır ve her build'de yüklenir.

### Profil dosyası
`profile.json` (düz JSON): keşifler, başarımlar, favoriler, ayarlar, trait şablonları. Kayıt dosyalarından bağımsızdır; bozulursa yedeğinden (`profile.bak`) yüklenir.

---

## 9.3 Modding

### Klasör ve manifest
```
Mods/<mod_id>/
  manifest.json
  Data/*.json          (yeni içerik veya yamalar)
  Art/*.png + *.json   (sprite sheet + Aseprite verisi)
  Audio/*.wav|ogg
  Localization/tr.json, en.json
  Code/*.dll           (isteğe bağlı, yalnızca masaüstü)
```
```json
{ "id": "author.more_biomes", "name": "Daha Fazla Biyom", "version": "1.2.0", "gameVersion": ">=0.9.0",
  "dependencies": ["author.core_lib>=1.0"], "loadAfter": [], "description": "...", "hasCode": false }
```

### Yükleme sırası
1. Oyun verisi → 2. modlar, bağımlılık grafiğine göre topolojik sırayla (çakışmada `loadAfter`) → 3. ContentValidator.
- **Yama anlamı:** Aynı `id` tekrar gelirse alanlar üzerine yazılır; listeler için `"+trees": [...]` (ekle) ve `"-trees": [...]` (çıkar) sözdizimi desteklenir.
- Mod'lar yeni **davranış kodu** ekleyemez (yalnızca kayıtlı efekt anahtarları, nöronlar, işleyiciler); kod gerektiren mod'lar `Code/` DLL'i ile kendi kayıtlarını yapar.
- Mod listesi kayıt dosyasına yazılır; eksik mod ile yüklemede uyarı.

### Kod modları (yalnızca masaüstü)
- `IModEntry { void OnLoad(ModApi api); }`; `ModApi` kayıt noktaları: nöron, eylem, güç işleyicisi, efekt anahtarı, olay dinleyici, UI paneli.
- Varsayılan **kapalı**; açarken güvenlik uyarısı ("kod modları bilgisayarınızda kod çalıştırır"). IL2CPP build'lerinde çalışmaz — kod modları için ayrı Mono build veya Lua gibi betik dili değerlendirilir; karar Faz 11'de `DECISIONS.md`'ye yazılır.

### Steam Workshop
Mod klasörü yükleme aracı (oyun içi "Modlarım → Yayınla"), abone olunan modlar otomatik `Mods/` altına iner. Harita paylaşımı da Workshop öğesi olarak (kayıt dosyası + önizleme).

---

## 9.4 Performans bütçeleri

Hedef makine: 8 çekirdek, 16 GB; Titanik harita, 5.000 birim, 20 krallık, ×1 hız.

| Sistem | Bütçe (ms/tick, ana iş parçacığı) | Not |
|--------|-----------------------------------|-----|
| World (yangın, lav, biyom, feature) | 1,5 | Zaman dilimli |
| Climate + clouds | 0,3 | Sıcaklık aylık |
| Units-Think | 1,5 | Paralel job'lar, ana iş parçacığında bekleme süresi |
| Units-Act (hareket, savaş, eylem) | 3,0 | |
| Pathfinding | 1,0 | 64 istek/tick |
| Civ | 1,0 | Şehir başına 10 tick'te bir |
| Meta | 1,0 | Aylık işler 60 tick'e yayılır |
| Events + history | 0,3 | |
| Region rebuild | 0,5 | 4 chunk/tick |
| **Toplam simülasyon** | **≤ 10** | ×2 hızda frame başına 2 tick → 20 ms |
| Render (harita + birim + UI) | ≤ 5 | |

### Kurallar
- Tick içinde **sıfır heap ayırma**. LINQ, `foreach` üzerinde boxing, string birleştirme, lambda yakalama yasak.
- Ağır döngüler `[BurstCompile]` job; ana iş parçacığı yalnızca çakışma çözümü ve olay dağıtımı yapar.
- Aylık/yıllık işler tek tick'e yığılmaz: `AmortizedScheduler` işi 60 tick'e böler.
- **Simülasyon LOD'u (isteğe bağlı, ×10 üstü hızlar):** Kamera görüşü dışındaki hayvanlar 2 kat seyrek düşünür; şehir planlayıcı aralığı 2 kat.
- Headless benchmark sahnesi CI'da gece çalışır; bütçe aşımı %10'dan fazlaysa uyarı.

### Optimizasyon kontrol listesi (sorun çıktığında sırayla)
1. Profiler'da en pahalı `ProfilerMarker`'ı bul.
2. Burst Inspector: job gerçekten Burst mi, vektörleşiyor mu?
3. Veri erişimi: SoA sütunları sıralı mı okunuyor? Rastgele erişimi indeksle sırala.
4. Sıklık: bu iş her tick mi gerekiyor? Zaman dilimine böl.
5. Kapsam: tüm harita yerine aktif küme (yanan tile'lar, dirty chunk'lar) mi dolaşılıyor?
6. Son çare: yaklaşık çözüm (daha az örnek, daha kaba ızgara).

---

## 9.5 Test planı (bütünleşik)

| Katman | Kapsam hedefi | Araç |
|--------|---------------|------|
| Birim testleri | Çekirdek formüller, veri yapıları, kurallar (%80 satır kapsamı Core/World/Sim) | Unity Test Framework EditMode |
| Senaryo testleri | Her bölümün "Testler ve kabul kriterleri" tablosu | PlayMode + headless başlatıcı |
| Determinizm | 10.000 tick, 3 seed, tüm sistemler | PlayMode |
| Kayıt uyumluluğu | Her sürümün fixture kaydı | EditMode |
| Performans | 9.4 bütçeleri | Performance Testing paketi, gece |
| Uzun koşu | 1.000 yıl süper hız, bellek ve meta sayısı sınırlı | Headless, haftalık |
| İçerik | Referans bütünlüğü | `validate.py`, her commit |
| UI duman | Tüm pencereler | PlayMode |
| Manuel | Oynanış kontrol listesi (aşağıda) | İnsan |

**Manuel oynanış kontrol listesi (her faz sonunda):** Yeni dünya oluştur → her sekmeden 3 güç kullan → 200 yıl izle → rastgele bir krallığın tarihini oku → bir birimi kontrol et → kaydet, çık, yükle → aynı yerden devam ettiğini doğrula.

---

## 9.6 Yayın hazırlığı

### Early Access kapsamı (öneri)
Faz 1–10 tamam + Faz 11'in ses, öğretici, yerelleştirme (TR/EN) kısımları. Modding ve mobil Early Access sonrasına bırakılabilir.

### Steam entegrasyonu
- Steamworks.NET: başlatma (`SteamAPI.Init` başarısızsa oyun Steam'siz çalışır), başarımlar (`achievements.json` id → Steam API adı eşleme tablosu), bulut kayıt (profil + son 3 kayıt), Workshop (9.3), zengin durum ("Yıl 340 — 12 krallık").
- Mağaza varlıkları: kapsül görseller, 5+ ekran görüntüsü, 30–60 sn fragman (oyun içi kayıt modu: UI gizle + sinematik kamera).

### Mobil
- Android: IL2CPP ARM64, AAB; varsayılan harita Orta; birim sınırı 2.000; partikül yoğunluğu düşük; dokunmatik UI düzeni (Bölüm 8).
- iOS: aynı ayarlar; dosya paylaşımı için kayıt klasörü Dosyalar uygulamasına açılır (`UIFileSharingEnabled`).
- Pil: arka planda simülasyon duraklatılır; FPS sınırı 30/60 seçilebilir.

### Sürüm yönetimi
`0.<faz>.<yama>` Early Access'e kadar; yayın kontrol listesi EK B 3.12'de. Her sürümde oyun içi "Yenilikler" penceresi `CHANGELOG.md`'den üretilir.

---

## 9.7 Görev listesi

| # | Görev | Süre |
|---|-------|------|
| 1 | Ses sistemi, olay→ses eşleme, ortam, müzik katmanları | 4 gün |
| 2 | Ses üretimi (asgari liste) | 5 gün (sanat) |
| 3 | Tam kayıt formatı, fixture testleri, profil dosyası | 4 gün |
| 4 | Mod yükleyici, yama sözdizimi, sprite/ses/yerelleştirme modları | 4 gün |
| 5 | Kod modları kararı ve uygulaması | 3 gün |
| 6 | Performans turu (9.4 bütçeleri) | 5 gün |
| 7 | Test altyapısının tamamlanması, uzun koşu | 3 gün |
| 8 | Steam entegrasyonu + Workshop | 4 gün |
| 9 | Mobil port ve dokunmatik ayarları | 8 gün |
| 10 | Mağaza varlıkları, fragman modu | 3 gün |
| | **Toplam** | **~43 gün** |
