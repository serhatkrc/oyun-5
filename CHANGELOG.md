# Changelog

## [Unreleased] — Faz 3: Birim çekirdeği

### Eklendi
- Content: `SpeciesDef`, `UnitTraitDef`, `StatusEffectDef`, `SpellDef`; `StatId` eşlemesi (bilinmeyen anahtarlar davranış anahtarı), `UnitFlags`, diyet/habitat/üreme türleri, statü efekt ayrıştırıcı, biyom hayvan listeleri.
- Sim: SoA `UnitStore` (yuva yeniden kullanımı + nesil, ertelenmiş ölüm, cesetler), stat formülü (trait, statü, yaş evresi, seviye), zone hücreli deterministik uzamsal indeks, `PathService` (ada kontrolü → region A* koridoru → tile A*, bütçe, yol havuzu), nöron → görev → eylem AI (kaç, saldır, avlan, beslen/otla, uyu, çiftleş, takip/sürü, dinlen, karaya çık, büyü), hareket (tile maliyeti, yüzme, itme/düşme), yakın dövüş + mermiler + XP/seviye/veteran, statü sistemi, açlık/enerji/stamina, yaşlanma, canlı/yumurta/bölünme üremesi, nüfus sınırı ve yerel kalabalık, sıcaklık/lav/yangın/boğulma hasarı, 20+ büyü, hayvan doğuşu (başlangıç + yıllık).
- Powers: yaratık/tür doğurma güçleri; afetlere nüfus bilgisi.
- Render: palet shader'lı birim sprite'ları (4×6 sayfa, animasyon, yön, yaş ölçeği, statü tonu), LOD noktaları, mermiler.
- Persistence: bölüm 10 (birimler, yollar, mermiler; tür/trait/statü eşlemesi).
- UI: F3 panelinde doğa ve birim bilgisi, imleç altındaki birim.
- Editör: soak'a ekosistem ve 5.000 birim performans senaryoları, `PixelGenesis/Feed Debug`.

## [Unreleased] — Faz 2: Doğa, iklim ve zaman

### Eklendi
- Content: `CloudDef`, `EraDef`, `DisasterDef`, `WorldLawDef` + kayıtlar (clouds, eras, disasters, world_laws); tile doğa kuralları (donma/erime, yanık, iyileşme, lav soğuma/akış/söndürme, balçık), feature `aquatic`/`burnsInto`, biyom `effectCode`/`tempOffset`/`snowCover`/`waterOnly`/`fireproof`; `BiomeEffect` ve `CloudEffect` kod tabloları.
- World: `FeatureState` (evre + kaynak), zone ağaç/bitki sayaçları ve temel sıcaklık, `DirtyMask.Features`, `SetFire`, aktif tile kuyruğu, su/kara uyumlu biyom ve feature kuralları.
- Sim: `NatureState` (değiştiriciler, rüzgâr, bulutlar, tohumlar, yanan/lav/balçık listeleri, çağ saati, kanunlar), biyom yayılımı + tohum, feature büyümesi, yangın (rüzgârlı yayılım), lav akışı/soğuma, balçık, yavaş dönüşümler, aylık Burst zone iklimi (mevsim, donma, kar, çöl buharlaşması, volkan baloncuğu, kor kıvılcımı), rüzgâr, bulut doğuşu/hareketi/yağışı, küresel yağmur, çağ sistemi (8 slotlu saat, ağırlıklı seçim, tint geçişi), yıllık afet kararı + `DisasterRequestEvent`.
- WorldGen: başlangıç bitki örtüsü (orman yoğunluğu), zone temel sıcaklığı, maden damarlarına kaynak.
- Powers: ağaç/çalı/çiçek/maden/buğday ekme, biyom tohumları, ateş, kar, lav, balçık, bulut güçleri, söndür/dondur/lav soğut fırçaları, `DisasterExecutor`.
- Render: `SpriteLibrary` (id ile PNG, atlas), chunk başına `FeatureRenderer` (evre ölçeği, LOD 3'te gizli), bulut + gölge katmanları, global çağ rengi, `PG_Sprite` shader.
- UI: üst çubukta çağ ve kalan süre; çağ değişimi ve afet bildirimleri.
- Persistence: isteğe bağlı bölümler 6–9 (FeatureState, zone iklimi, doğa, kanunlar); yükleme sonrası birebir devam.
- Editör: `PixelGenesis/Nature Soak` (uzun koşu, buz çağı, rüzgârlı yangın), snapshot'a feature/bulut ve yakın görünüm; DataSync `Art/` PNG'lerini de aynalar.
- Araç: `Tools/placeholder_art.py` feature'lara türe uygun geçici renkler verir.

## [Unreleased] — Faz 1: Dünya çekirdeği

### Eklendi
- Core: `SimConst`, `GameClock` (hız kademeleri, duraklatma, tek adım), PCG32 `SimRandom` + akış başına `SimRandomProvider`, `EntityId`/`IdPool`, tip başına kuyruklu `EventBus`.
- Content: `ContentDB.LoadAll` (StreamingAssets/Data + Mods yaması), `Registry<T>`, `ContentValidator`; tiles, biomes, features, powers, worldgen_templates, biome_table, ui_strings.
- World: SoA `WorldMap` (tek yazma kapısı, dirty maskeleri, `TileEditBatch`), `RegionGraph` (chunk içi region'lar, chunk arası kenarlar, union-find adalar, tick başına 4 chunk bütçe).
- WorldGen: Burst job'larıyla 9 adım (yükseklik + maske + domain warp, yüzdelik deniz seviyesi, seviyeler, hücresel temizlik, kıyı, nem/sıcaklık, biyom tablosu + özel yamalar, varyant, maden damarları), 8 şablon, PNG'den içe aktarma.
- Sim: `SimulationPipeline` (9 faz, ProfilerMarker), `SimWorld`, olay dağıtımı ve region yeniden hesap sistemleri.
- Powers: fırça (daire/kare, 9 boyut), stamp/stroke (Bresenham), `powers.json` op'larından fırça işlemleri, `PowerCommand` kuyruğu.
- Render: chunk başına 64×64 katmanlı `Texture2DArray`, tek mesh/tek draw call, Burst renk job'ı (kabartma gölgesi, kar/yangın/yol), su/lav animasyonlu URP 2D shader, F4 region/ada görünümü.
- UI: UI Toolkit HUD (tarih, hız, süper hız, tek adım, güç çubuğu, fırça paneli, bildirimler), yeni dünya penceresi (önizlemeli), kayıt penceresi, F3 debug overlay; kamera (imlece zoom, kaydırma, sürükleme, ataletli, piksel hizalama, LOD).
- Persistence: kayıt v1 (başlık + küçük resim + sıkıştırılmış bölümler, chunk RLE, ContentMap eşleme), 6 slot + döngüsel 3 otomatik kayıt (10 yılda bir ve çıkışta).
- Boot: `GameBootstrap` sahnesi, `SimulationRunner`, `--determinism-check`, `--headless --years`.
- Editör: `PixelGenesis/Sync Data`, `PixelGenesis/Run Diagnostics`.
