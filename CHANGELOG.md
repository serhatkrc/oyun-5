# Changelog

## [Unreleased] — Faz 4: Medeniyet temeli (05.1–05.8)

### Eklendi
- Content: `ResourceDef`, `BuildingDef` (hücre boyutu → tile, maliyet, ön koşul ayrıştırma), `BuildingStyleDef`, `JobDef`, `EquipmentTypeDef`, `MaterialDef`, `ItemQualityDef`, `HappinessEventDef`, `NameSets`; `buildings.json`'a `capacity` (DECISIONS #54).
- World: bina kaplaması (`PlaceBuilding`/`RemoveBuilding`, kapı tile'ı yürünebilir, `TileFlags.Door`), bina içinde bitki büyümez.
- Sim (`Sim/Civ`): `CivState` (şehirler, bina ve eşya depoları, isim havuzu), şehir kurma (6+ şehirsiz akıllı yetişkin, zone puanlaması), aylık şehir döngüsü (sakinler, ev atama, beslenme, bozulma, kıtlık, vergi, ekin büyümesi, mutluluk olayları, eşya dağıtımı, lider), meslek atama, planlayıcı (merkez yükseltmesi yerinde, konut, depo, yiyecek, üretim, savunma, refah), yıllık sınır büyümesi ve göçmen grupları, bina bakımı (yangın, sel), yollar, eşya üretimi (malzeme, kalite zarı, efsanevi isimler).
- Birim işleri: inşaatçı, çiftçi (tarla açma/ekme/hasat), oduncu/taşçı, toplayıcı, madenci, balıkçı, avcı, demirci, fırıncı, çoban, devriye; `Work` ve `Migrate` görevleri; ekipman etkileri stat formülünde.
- Persistence: bölüm 11 (şehirler, binalar, eşyalar, birimlerin şehir sütunları); yüklemede kaplamalar ve region'lar yeniden kurulur.
- Editör: `PixelGenesis/Civ Soak` (5.12 #1, #3–#8).
- UI: basit şehir penceresi — güç seçili değilken şehir zone'una ya da binaya tıklayınca açılır (tür, merkez ve kademe, kuruluş yılı, lider, nüfus/konut, mutluluk, altın, kıtlık, depo doluluğu, yiyecek, stok listesi, meslek sayısı/kotası, tür başına bina sayısı + inşaattakiler; 4 Hz, yalnızca okur; DECISIONS #61). Üst çubukta şehir sayısı ve medeni nüfus. F3'te şehir/bina/eşya sayıları, imleç altındaki zone'un şehri, bina (id, durum, ilerleme %, can, sakin) ve birimin şehri/mesleği/taşıdığı kaynak.
- Render: `BuildingRenderer` (bina sprite'ları `{stil}_{bina}_{durum}`, `human` stiline ve düz quad'a düşer; şehir renginde çatı paleti, yanma/terk tonu, kuzey→güney sıralama, yalnızca değişimde mesh kurulumu, uzak zoom'da gizli) ve harita üzerinde şehir toprakları: sahipli zone'larda hafif şehir rengi ve 1 tile'lık sınır çizgisi (DECISIONS #61). `SpriteLibrary.Exists` ve düz piksel girdisi.

### Değişti
- Ateşten kaçma nöronu; evi olan şehirliler sıcak/soğuktan korunur (#59). Şehirlerde hayvan kalabalık sınırı yerine konut sınırı.
- Yol bulma dolambaç kontrolü bölge merkezleri arasındaki mesafeye göre (büyük region köşelerinde yanlış `Unreachable`).

### Bilinen eksik
- 5.12 #2 (sömürge gemisi) Faz 5'te: gemiler, ticaret, göç 05.9–05.11.
- Bina ve sınır çizimi, şehir paneli: Unity'de görsel doğrulama bekliyor.

## [Unreleased] — Faz 3 tamamlama

### Düzeltildi
- Yumurtadaki birimler acıkmıyor (yumurtlayan türlerde açlık ölümleri; DECISIONS #50).
- Etoburlar görüşte av yokken yerinde sayıyordu: aç avcı artık kokuyla (48 tile) ava doğru ya da uzağa dolaşır; kovalama isabetle uzar, saldırı beklemesinde hedefe yapışık kalır; av, avın boyuyla doyurur (#52).
- Bebek evresindeki yavrular (ör. kurt) annelerinin yanında emzirilir; önceden avlanamadıkları için hepsi açlıktan ölüyordu (#53).
- Yol bulma: kısa hedeflerde düz çizgi kısayolu, uzun dolambaçlı kısa hedefler `Unreachable` (#51). Birim eylem sistemi 6,7 → 1,0 ms/tick (headless ölçüm).

### Eklendi
- `follow_herd` ve `pack_hunt` nöronları (alt tür trait'leri gelene kadar tüm hayvanlara), etçil varsayılan görüşü 14 (#52).
- Editör: `PixelGenesis/Unit Acceptance` (3.12 #4 avcı/av 5 seed, #5 açlık, #9 5.000 tick / 2.000 birim determinizm).
- Araç: `Tools/headless` (Unity olmadan derleme kontrolü ve simülasyon), `Tools/headless/make_meta.py`.

### Bilinen eksik
- 3.12 #4: kurtlar 5 seed'in hiçbirinde 30 yıl dayanmıyor (önce 3–7 yıl, şimdi 6–25 yıl). Darboğaz üreme hızı (12 ay gebelik, 1 yavru, eşlerin dağınıklığı).

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
