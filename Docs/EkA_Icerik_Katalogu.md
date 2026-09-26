# EK A — Oyun İçerik Kataloğu

Bu katalog `Data/` klasöründeki JSON dosyalarından otomatik üretildi. Oyun içerikleri bu JSON'lardan okur (Bölüm 1.7 ContentDB). İçerik eklemek/değiştirmek için JSON'u düzenle; bu katalog `catalog.py` ile yeniden üretilir.

**Efekt kısaltmaları:** `hp` can, `dmg` hasar, `armor` zırh, `speed` hız, `atkspd` saldırı hızı, `crit` kritik, `range` menzil, `intel` zekâ, `diplo` diplomasi, `warfare` savaşçılık, `steward` yönetim, `luck` şans, `neuron:x` AI'ye karar düğümü ekler, `flag:x` davranış bayrağı, `immune:x` bağışıklık, `spell:x` büyü verir, `onHit:x:n` vuruşta n% ihtimalle etki.

**Kural:** Bütün isimler özgündür; referans oyunun isim, sprite veya metinleri kullanılmaz.

## Özet

| Kategori | Dosya | Adet |
|---|---|---|
| Tile Tipleri | `tiles.json` | 18 |
| Harita Şablonları | `worldgen_templates.json` | 8 |
| Biyom Tablosu | `biome_table.json` | 1 |
| Biyomlar | `biomes.json` | 29 |
| Ağaç, Bitki ve Damarlar | `features.json` | 44 |
| Kaynaklar | `resources.json` | 29 |
| Bulutlar | `clouds.json` | 11 |
| Çağlar | `eras.json` | 10 |
| Otomatik Afetler | `disasters.json` | 17 |
| Türler ve Yaratıklar | `species.json` | 92 |
| Birim Trait'leri | `unit_traits.json` | 118 |
| Statü Efektleri | `status_effects.json` | 31 |
| Hastalıklar | `diseases.json` | 8 |
| Büyüler | `spells.json` | 28 |
| Ekipman Tipleri | `equipment_types.json` | 17 |
| Malzemeler | `materials.json` | 11 |
| Eşya Kaliteleri | `item_qualities.json` | 6 |
| Alt Tür Trait'leri | `subspecies_traits.json` | 200 |
| Genler | `genes.json` | 45 |
| Gen Sinerjileri | `gene_synergies.json` | 8 |
| Fenotipler | `phenotypes.json` | 50 |
| Evrim Kuralları | `evolution_rules.json` | 6 |
| Dönüşümler | `metamorphoses.json` | 13 |
| Binalar | `buildings.json` | 41 |
| Bina Stilleri | `building_styles.json` | 6 |
| Meslekler | `jobs.json` | 19 |
| Kültür Trait'leri | `culture_traits.json` | 78 |
| Din Trait'leri | `religion_traits.json` | 40 |
| Dil Trait'leri | `language_traits.json` | 25 |
| Klan Trait'leri | `clan_traits.json` | 28 |
| Krallık Trait'leri | `kingdom_traits.json` | 8 |
| Plotlar | `plots.json` | 16 |
| Savaş Türleri | `war_types.json` | 7 |
| Kitap Türleri | `book_types.json` | 10 |
| Mutluluk Olayları | `happiness_events.json` | 30 |
| Tanrı Güçleri | `powers.json` | 246 |
| Dünya Kanunları | `world_laws.json` | 49 |
| Başarımlar | `achievements.json` | 52 |
| **Toplam** | | **1455** |

Ek dosyalar: `zombie_rules.json` (zombi salgını kuralları), `name_sets.json` (dil bazlı isim üretici heceleri ve unvanlar), `gene_rules.json` (kromozom ve kalıtım kuralları).


---

# Dünya

## Tile Tipleri (18)

| id | name | level | walkable | water | moveCost | onRaiseBecomes | onLowerBecomes | note |
|---|---|---|---|---|---|---|---|---|
| tile.deep_ocean | Derin Okyanus | 0 | – | ✔ | 0 | tile.ocean |  | Sadece gemi; derin su yaratıkları |
| tile.ocean | Okyanus | 1 | – | ✔ | 0 | tile.shallow | tile.deep_ocean | Gemi rotası |
| tile.shallow | Sığ Su | 2 | ✔ | ✔ | 3 | tile.sand | tile.ocean | Yüzülebilir, yavaş yürünür; mercan biyomu alabilir |
| tile.sand | Kum | 3 | ✔ | – | 1.2 | tile.soil_low | tile.shallow | Kıyı; kaktüs/palmiye |
| tile.soil_low | Alçak Toprak | 4 | ✔ | – | 1 | tile.soil_high | tile.sand | Ana yaşam alanı |
| tile.soil_high | Yüksek Toprak | 5 | ✔ | – | 1 | tile.hills | tile.soil_low | Ana yaşam alanı |
| tile.hills | Tepe | 6 | ✔ | – | 2 | tile.mountain | tile.soil_high | Maden damarı çıkar |
| tile.mountain | Dağ | 7 | ✔ | – | 4 | tile.summit | tile.hills | Yavaş; zengin maden |
| tile.summit | Zirve | 8 | – | – | 0 |  | tile.mountain | Geçilmez; kar tutar |
| tile.lava_hot | Kızgın Lav | -1 | – | – | 0 |  |  | Değeni yakar; 300 tick sonra lava_mid |
| tile.lava_mid | Akan Lav | -1 | – | – | 0 |  |  | Yakar; 400 tick sonra lava_cool |
| tile.lava_cool | Soğuyan Lav | -1 | ✔ | – | 3 | tile.mountain |  | Yürünür, ısıtır; 600 tick sonra dağ/tepe |
| tile.ice | Buz | -1 | ✔ | – | 1.5 | tile.sand | tile.shallow | Donmuş su; sıcakta sığ suya döner |
| tile.field | Tarla | -1 | ✔ | – | 1 | tile.soil_high | tile.sand | Tarım; ekili hâlde buğday feature'ı taşır |
| tile.scorched | Yanık Toprak | -1 | ✔ | – | 1 | tile.soil_high | tile.sand | 300–900 tick sonra alçak toprağa döner |
| tile.pit | Çukur | -1 | – | – | 0 | tile.soil_low |  | Patlama kalıntısı; yağmurla dolup su olur |
| tile.goo | Yutan Balçık | -1 | – | – | 0 |  |  | Komşu her şeyi yiyerek yayılır (kanunla sınırlı) |
| tile.wasteland | Çorak Toprak | -1 | ✔ | – | 1.2 | tile.soil_high | tile.sand | Radyasyon izi; bitki çıkmaz, zamanla iyileşir |

## Harita Şablonları (8)

| id | name | landRatioDefault | edgeOcean |
|---|---|---|---|
| wgt.continents | Kıtalar |  | 0.06 |
| wgt.pangea | Tek Kıta |  | 0.06 |
| wgt.archipelago | Takımadalar | 0.3 | 0.06 |
| wgt.ring | Halka |  | 0.06 |
| wgt.lakes | Göller | 0.7 | 0.04 |
| wgt.flat_green | Düz Yeşillik |  | 0.03 |
| wgt.empty_ocean | Boş Okyanus |  | 0.06 |
| wgt.custom_image | Görüntüden |  | 0.06 |

## Biyom Tablosu (1)

| id | patches |
|---|---|
| bt.default | clover, mushroom, crystal, enchanted, candy, citrus, garlic, celestial |

## Biyomlar (29)

| id | name | growStrength | animals | effect | description |
|---|---|---|---|---|---|
| bio.grassland | Çayır | 5 | sheep, rabbit, cow, horse_wild |  | Dengeli, verimli ovalar. |
| bio.birch | Huşluk | 5 | deer, fox, rabbit |  | Beyaz gövdeli serin ormanlar. |
| bio.maple | Akçaağaç Koruluğu | 5 | deer, boar, fox |  | Kızıl yapraklı, bereketli korular. |
| bio.forest | Karışık Orman | 5 | wolf, bear, deer, boar |  | Sık ormanlar; av bol. |
| bio.jungle | Cengel | 6 | monkey, snake, frog, parrot |  | Sıcak, nemli, tehlikeli. |
| bio.swamp | Bataklık | 6 | frog, crocodile, snake, mosquito_swarm |  | Hastalık yayılımı +%50. |
| bio.savanna | Savan | 6 | buffalo, hyena, lion, rhino |  | Sıcak otlaklar. |
| bio.desert | Çöl | 6 | camel, scorpion, lizard | Su tile'ı buharlaşma şansı; yiyecek -%50 | Kum denizleri, vahalar. |
| bio.rocklands | Kayalık | 5 | goat, eagle, lizard | Maden damarı çıkma şansı x2 | Taşlı, madenli yaylalar. |
| bio.tundra | Tundra | 5 | penguin, seal, reindeer, polar_bear | Soğuk hasarı (korunaksızlara) | Donmuş düzlükler. |
| bio.snowpine | Karlı Çamlık | 5 | wolf, polar_bear, reindeer, owl | Kar örtüsü kalıcı | Karla kaplı ormanlar. |
| bio.flower | Çiçek Vadisi | 5 | butterfly, bee, rabbit | Mutluluk +2 (içinde yaşayanlara) | Rengârenk, huzurlu. |
| bio.clover | Yonca Tarlası | 5 | rabbit, sheep, cow | Şans +%5 | Uğurlu otlaklar. |
| bio.mushroom | Mantar Diyarı | 6 | shroomling, snail, beetle | Spor hastalığı riski | Dev mantarlar ve sporlar. |
| bio.crystal | Kristal Vadisi | 6 | crystal_beetle, snail | Büyü birimlerine mana +%20 | Işık kıran kristal ormanları. |
| bio.enchanted | Peri Ormanı | 6 | fairy, deer, butterfly | Yaralar 2x hızlı iyileşir | Işıldayan büyülü orman. |
| bio.corrupted | Kara Çürüme | 6 | rot_crawler, rat, crow | Mutluluk -3; delilik şansı | Bozulmuş, lanetli topraklar. |
| bio.infernal | Kor Diyarı | 6 | ember_imp, fire_elemental, salamander | Rastgele yangın çıkarır; tile yanmaz | Kızgın, yanan diyar. |
| bio.candy | Şekerleme Ülkesi | 6 | candy_golem, gummy_bear, rabbit | Yiyecek bolluğu; diş ağrısı (mutluluk dalgalanır) | Tatlı, tuhaf bir diyar. |
| bio.citrus | Turunç Bahçesi | 6 | parrot, bee, rabbit | Veba iyileşme şansı +%10 | Ekşi kokulu bahçeler. |
| bio.garlic | Sarımsaklık | 5 | rabbit, goat | Kan emici birimler giremez | Keskin kokulu tarlalar. |
| bio.celestial | Gök Bahçesi | 5 | sky_whale_calf, owl, fairy | Kutsama şansı; yıldırım çekmez | Bulut ağaçlı kutsal bahçe. |
| bio.ash | Kül Çölü | 6 | ash_crawler, crow, rat | Bitki çıkmaz; hastalık direnci -%20 | Yıkımın ardından kalan küller. |
| bio.timewarp | Zaman Kıvrımı | 5 | clock_crab, snail | Adım atanın 1 yıl yaşlanma şansı %10 | Zamanın eridiği tuhaf topraklar. |
| bio.void | Sessiz Boşluk | 5 | void_moth | Mana yenilenmez; sesler kısılır | Hiçliğin sızdığı alan. |
| bio.coral | Mercan Resifi | 5 | fish, crab, turtle, seal | Sadece sığ suda; balık x3 | Sığ denizlerde renkli resifler. |
| bio.volcanic | Volkanik Ova | 6 | salamander, fire_elemental, lizard | Obsidyen damarı; ara sıra lav baloncuğu | Kül ve bazalt ovaları. |
| bio.bone | Kemik Düzlüğü | 6 | skeleton, bone_crawler, crow | Gece iskelet doğma şansı | Eski savaşların kemik tarlası. |
| bio.honey | Bal Korusu | 5 | bee, bear, butterfly | Yiyecek +%30; arı sokması | Petek ağaçlı altın korular. |

## Ağaç, Bitki ve Damarlar (44)

| id | name | kind | yields | note |
|---|---|---|---|---|
| feat.tree_oak | Meşe | tree | res.wood:3 |  |
| feat.tree_birch | Huş | tree | res.wood:2 |  |
| feat.tree_maple | Akçaağaç | tree | res.wood:3 | Sonbahar renkli |
| feat.tree_pine | Çam | tree | res.wood:3 |  |
| feat.tree_snowpine | Karlı Çam | tree | res.wood:2 | Kar tutar |
| feat.tree_palm | Palmiye | tree | res.wood:2, res.fruit:1 | Kıyı |
| feat.tree_jungle | Dev Cengel Ağacı | tree | res.wood:4, res.fruit:1 |  |
| feat.tree_cypress | Bataklık Servisi | tree | res.wood:2 | Suda büyüyebilir |
| feat.tree_acacia | Akasya | tree | res.wood:2 | Savan |
| feat.cactus | Kaktüs | plant | res.fruit:1 | Dokunana hasar |
| feat.tree_dead | Kuru Ağaç | tree | res.wood:1 | Çorak biyomlar |
| feat.tree_giant_mushroom | Dev Mantar | tree | res.mushroom:3 |  |
| feat.crystal_spire | Kristal Sütun | tree | res.crystal:2 | Kesilmez, madencilikle alınır |
| feat.tree_fairy | Peri Ağacı | tree | res.wood:2, res.essence:1 | Işıldar |
| feat.tree_rot | Çürük Gövde | tree | res.wood:1 | Çevresine çürüme yayar |
| feat.tree_ember | Kor Ağacı | tree | res.wood:2 | Hiç sönmeyen kıvılcımlar |
| feat.tree_candy | Şeker Ağacı | tree | res.candy:2 |  |
| feat.tree_citrus | Turunç Ağacı | tree | res.wood:1, res.fruit:3 |  |
| feat.tree_cloud | Bulut Ağacı | tree | res.wood:1, res.essence:1 | Havada süzülür |
| feat.tree_ash | Kül Ağacı | tree | res.wood:1 |  |
| feat.tree_hourglass | Kum Saati Ağacı | tree | res.wood:1, res.essence:1 | Zaman Kıvrımı biyomu |
| feat.void_pillar | Boşluk Sütunu | tree | res.essence:2 |  |
| feat.coral | Mercan | plant | res.fish:1 | Sığ suda; balık çeker |
| feat.tree_bone | Kemik Ağacı | tree | res.bone:3 |  |
| feat.tree_honeycomb | Petek Ağacı | tree | res.honey:2, res.wood:1 | Arı üretir |
| feat.tree_volcanic | Bazalt Dikeni | tree | res.stone:2 |  |
| feat.grass_tuft | Ot Öbeği | plant |  | Otçullar yer |
| feat.flower | Çiçek | plant | res.herbs:1 | Arıları çeker |
| feat.berry_bush | Meyve Çalısı | plant | res.berries:2 |  |
| feat.reed | Saz | plant |  | Bataklık/kıyı |
| feat.wheat_crop | Buğday Başağı | crop | res.wheat:3 | Sadece tarla tile'ında |
| feat.herb_patch | Şifalı Ot Öbeği | plant | res.herbs:2 |  |
| feat.small_mushroom | Küçük Mantar | plant | res.mushroom:1 |  |
| feat.garlic_plant | Sarımsak | plant | res.herbs:1 | Kan emicileri iter |
| feat.ore_copper | Bakır Damarı | ore | res.copper:6 |  |
| feat.ore_iron | Demir Damarı | ore | res.iron:6 |  |
| feat.ore_silver | Gümüş Damarı | ore | res.silver:4 |  |
| feat.ore_gold | Altın Damarı | ore | res.gold:4 |  |
| feat.ore_skyiron | Gökdemir Damarı | ore | res.skyiron:3 | Meteor sonrası da oluşur |
| feat.ore_starore | Yıldız Cevheri Damarı | ore | res.starore:2 |  |
| feat.ore_obsidian | Obsidyen Kütlesi | ore | res.obsidian:4 | Lav soğuyunca |
| feat.rock | Kaya | ore | res.stone:4 |  |
| feat.salt_flat | Tuz Yatağı | ore | res.salt:4 | Çöl |
| feat.bones_pile | Kemik Yığını | ore | res.bone:3 | Savaş alanları |

## Kaynaklar (29)

| id | name | kind | nutrition | value | source |
|---|---|---|---|---|---|
| res.wood | Odun | material | 0 | 1 | Ağaç kesimi |
| res.stone | Taş | material | 0 | 1 | Tepe/dağ madenciliği |
| res.clay | Kil | material | 0 | 1 | Bataklık/nehir kıyısı |
| res.bone | Kemik | material | 0 | 1 | Avlanma, ölüler |
| res.leather | Deri | material | 0 | 2 | Avlanma |
| res.wool | Yün | material | 0 | 2 | Koyun/keçi kırkma |
| res.copper | Bakır | ore | 0 | 3 | Tepe damarları |
| res.iron | Demir | ore | 0 | 4 | Dağ damarları |
| res.silver | Gümüş | ore | 0 | 6 | Dağ damarları (nadir) |
| res.gold | Altın | currency | 0 | 8 | Dağ damarları, ticaret, vergi |
| res.skyiron | Gökdemir | ore | 0 | 14 | Meteor düşüş noktaları, zirve damarları |
| res.starore | Yıldız Cevheri | ore | 0 | 20 | Kristal ve gök biyomları (çok nadir) |
| res.obsidian | Obsidyen | ore | 0 | 10 | Soğumuş lav çevresi |
| res.crystal | Kristal | magic | 0 | 9 | Kristal biyomu |
| res.essence | Öz | magic | 0 | 12 | Büyülü biyomlar, ölü büyücüler |
| res.wheat | Buğday | food | 2 | 1 | Tarla |
| res.bread | Ekmek | food | 5 | 2 | Değirmen + fırın |
| res.meat | Et | food | 4 | 2 | Avlanma, hayvancılık |
| res.fish | Balık | food | 3 | 2 | Balıkçılık |
| res.berries | Yaban Meyvesi | food | 2 | 1 | Meyve çalıları |
| res.fruit | Meyve | food | 3 | 1 | Meyve ağaçları |
| res.mushroom | Mantar | food | 2 | 1 | Mantar biyomu, orman |
| res.honey | Bal | food | 4 | 3 | Arı kovanları, bal korusu |
| res.milk | Süt | food | 2 | 1 | İnek/keçi |
| res.eggs | Yumurta | food | 2 | 1 | Tavuk/kuş |
| res.candy | Şekerleme | food | 3 | 3 | Şekerleme ülkesi |
| res.herbs | Şifalı Ot | medicine | 1 | 3 | Çiçek vadisi, orman |
| res.salt | Tuz | trade | 0 | 3 | Çöl, kıyı |
| res.spice | Baharat | trade | 0 | 5 | Cengel, savan |

## Bulutlar (11)

| id | name | effect |
|---|---|---|
| cloud.rain | Yağmur Bulutu | Yangın söndürür, bitki büyütür, lavı soğutur |
| cloud.snow | Kar Bulutu | Kar örtüsü, suyu dondurur |
| cloud.acid | Asit Bulutu | Tile'ı bir seviye eritir, birimlere hasar |
| cloud.lava | Lav Bulutu | Kızgın lav damlatır |
| cloud.life | Yaşam Bulutu | Biyomun hayvanlarını/uygarlıklarını doğurur |
| cloud.storm | Fırtına Bulutu | Rastgele yıldırım düşürür |
| cloud.ash | Kül Bulutu | Güneşi keser: sıcaklık -5, bitki büyümesi durur |
| cloud.blessing | Kutsal Bulut | Altındakilere 'Kutsanmış' statüsü |
| cloud.plague | Veba Sisi | Altındakilere veba bulaştırma şansı |
| cloud.rot | Çürük Bulutu | Altındakilere çürük ısırık bulaştırır |
| cloud.candy | Şeker Yağmuru | Şekerleme düşürür, yiyecek verir |

## Çağlar (10)

| id | name | minYears | maxYears | fertilityPct | tempShift | effects |
|---|---|---|---|---|---|---|
| era.dawn | Şafak Çağı | 30 | 60 | 120 | 0 | Barış olasılığı +%30; yeni şehir kurma hızı + |
| era.scorch | Kavurucu Güneş | 30 | 50 | 90 | 15 | Kuraklık, yangın şansı x2, kar varlıkları yanar |
| era.rain | Yağmur Çağı | 30 | 50 | 110 | -5 | Küresel yağmur: yangınlar söner, bitki patlaması |
| era.ice | Buz Devri | 30 | 70 | 70 | -25 | Su donar, biyom yayılımı durur, açlık artar |
| era.shadow | Gölge Çağı | 30 | 60 | 90 | -5 | Canavar doğuşu x3, gece iskeletleri |
| era.blood | Kan Çağı | 30 | 50 | 100 | 0 | Savaş/isyan olasılığı x2, plot ilerlemesi +%50 |
| era.glimmer | Işıltı Çağı | 30 | 60 | 100 | 0 | Büyü gücü +%50, yeni din kurma şansı x2, trait mutasyon + |
| era.ash | Kül Çağı | 30 | 50 | 60 | -10 | Mutluluk -5, kıtlık, biyom yayılımı durur |
| era.pale | Solgun Çağ | 30 | 50 | 80 | 0 | Hastalık bulaşma x2, veba sisleri |
| era.harvest | Hasat Çağı | 30 | 60 | 130 | 5 | Tarım verimi x1.5, ticaret + |

## Otomatik Afetler (17)

| id | name | minWorldAge | cooldownYears | chancePerYear | description |
|---|---|---|---|---|---|
| dis.meteor | Göktaşı Düşüşü | 5 | 10 | 0.04 | Rastgele noktaya göktaşı; çukur + gökdemir damarı |
| dis.earthquake | Deprem | 10 | 15 | 0.03 | Çatlak hattı: tile seviyeleri kayar, binalar hasar alır |
| dis.tornado | Hortum | 5 | 8 | 0.05 | Birimleri ve nesneleri savurur, 60–200 tick sürer |
| dis.volcano | Yanardağ Uyanışı | 20 | 25 | 0.02 | Dağda patlama, lav akışı, kül bulutu |
| dis.tsunami | Dev Dalga | 30 | 30 | 0.015 | Kıyıya su dalgası: sığ su istilası, bina yıkımı |
| dis.famine | Kıtlık | 15 | 20 | 0.03 | Bölgede tarım verimi -%80, 3–6 yıl |
| dis.plague | Salgın | 20 | 25 | 0.03 | Kalabalık bir şehirde veba başlar |
| dis.demon_raid | Kor İblisi Akını | 40 | 40 | 0.015 | Kor Diyarı yoksa bile yarık açılır, iblisler çıkar |
| dis.dark_mages | Kara Büyücü Ayaklanması | 30 | 30 | 0.02 | 3–6 kara büyücü belirir |
| dis.dragon | Ejderha Uyanışı | 50 | 60 | 0.01 | Dağ zirvesinden ejderha doğar |
| dis.sky_visitors | Gökten Gelenler | 60 | 60 | 0.008 | Gökten gemiler iner, birimleri kaçırır |
| dis.ice_storm | Buz Fırtınası | 10 | 15 | 0.02 | Geniş alanda donma |
| dis.heat_wave | Sıcak Dalgası | 10 | 15 | 0.02 | Su buharlaşır, yangın şansı x5 |
| dis.locusts | Çekirge Sürüsü | 15 | 20 | 0.02 | Tarlaları yer |
| dis.flood | Sel | 10 | 12 | 0.03 | Alçak toprakları su basar |
| dis.zombie_outbreak | Ölü Salgını | 40 | 50 | 0.012 | Mezarlık/kemik düzlüğünden ölüler kalkar |
| dis.bandits | Haydut Çetesi | 20 | 15 | 0.03 | Kanunsuzlardan oluşan bağımsız grup doğar |


---

# Canlılar

## Türler ve Yaratıklar (92)

| id | name | category | diet | stats | reproduction | note |
|---|---|---|---|---|---|---|
| sp.human | İnsan | civ | omni | hp:100, damage:12, armor:0, speed:1, lifespan:70, size:2 | live | Dengeli; her biyoma uyum sağlar, hızlı çoğalır |
| sp.elf | Elf | civ | herb | hp:90, damage:11, armor:0, speed:1.15, lifespan:300, size:2 | live | Uzun ömürlü, iyi okçu, yavaş çoğalır |
| sp.dwarf | Cüce | civ | omni | hp:130, damage:13, armor:5, speed:0.85, lifespan:200, size:2 | live | Madenci ve demirci; dağlara yerleşir |
| sp.orc | Ork | civ | carn | hp:140, damage:16, armor:2, speed:1, lifespan:60, size:3 | live | Savaşçı; çok hızlı çoğalır, saldırgan |
| sp.wolf | Kurt | evo | carn | hp:60, damage:10, armor:0, speed:1.3, lifespan:20, size:2 | live | Sürü halinde avlanır |
| sp.bear | Ayı | evo | omni | hp:180, damage:20, armor:3, speed:0.9, lifespan:30, size:4 | live | Bal ve balık sever |
| sp.cat | Kedi | evo | carn | hp:30, damage:6, armor:0, speed:1.4, lifespan:18, size:1 | live | Fare avcısı |
| sp.dog | Köpek | evo | carn | hp:45, damage:8, armor:0, speed:1.3, lifespan:15, size:1 | live | Medenilere evcilleşir |
| sp.rabbit | Tavşan | evo | herb | hp:15, damage:1, armor:0, speed:1.6, lifespan:8, size:1 | live | Çok hızlı çoğalır |
| sp.sheep | Koyun | evo | herb | hp:40, damage:2, armor:0, speed:0.9, lifespan:12, size:2 | live | Yün verir |
| sp.cow | İnek | evo | herb | hp:90, damage:4, armor:1, speed:0.8, lifespan:20, size:3 | live | Süt ve et |
| sp.chicken | Tavuk | evo | omni | hp:12, damage:1, armor:0, speed:1, lifespan:8, size:1 | egg | Yumurta |
| sp.frog | Kurbağa | evo | carn | hp:20, damage:3, armor:0, speed:1.2, lifespan:10, size:1 | egg | Böcek yer; bataklıkta çoğalır |
| sp.rat | Sıçan | evo | omni | hp:12, damage:2, armor:0, speed:1.4, lifespan:4, size:1 | live | Veba taşır |
| sp.monkey | Maymun | evo | herb | hp:40, damage:5, armor:0, speed:1.4, lifespan:25, size:2 | live | Zeki; evrime yatkın |
| sp.penguin | Penguen | evo | carn | hp:30, damage:3, armor:0, speed:0.9, lifespan:20, size:1 | egg | Soğukta yaşar, iyi yüzer |
| sp.fox | Tilki | evo | carn | hp:30, damage:6, armor:0, speed:1.5, lifespan:12, size:1 | live | Kurnaz avcı |
| sp.crab | Yengeç | evo | omni | hp:35, damage:5, armor:6, speed:0.8, lifespan:15, size:1 | egg | Kabuklu |
| sp.scorpion | Akrep | evo | carn | hp:30, damage:8, armor:4, speed:1, lifespan:10, size:1 | egg | Zehirli iğne |
| sp.lizard | Kertenkele | evo | carn | hp:25, damage:4, armor:2, speed:1.3, lifespan:12, size:1 | egg | Sıcağa dayanıklı |
| sp.crocodile | Timsah | evo | carn | hp:160, damage:22, armor:6, speed:0.8, lifespan:60, size:4 | egg | Pusu avcısı |
| sp.bee | Arı | evo | herb | hp:5, damage:2, armor:0, speed:1.5, lifespan:2, size:1 | egg | Kovan kurar; bal üretir |
| sp.ant | Karınca | evo | omni | hp:6, damage:2, armor:1, speed:1.2, lifespan:3, size:1 | egg | Koloni halinde çalışır |
| sp.beetle | Böcek | evo | herb | hp:20, damage:3, armor:5, speed:0.8, lifespan:5, size:1 | egg | Sert kabuk |
| sp.snake | Yılan | evo | carn | hp:25, damage:7, armor:0, speed:1.1, lifespan:15, size:1 | egg |  |
| sp.turtle | Kaplumbağa | evo | herb | hp:80, damage:3, armor:12, speed:0.4, lifespan:120, size:2 | egg | Çok uzun ömür |
| sp.deer | Geyik | evo | herb | hp:50, damage:5, armor:0, speed:1.5, lifespan:15, size:2 | live |  |
| sp.boar | Yaban Domuzu | evo | omni | hp:70, damage:9, armor:2, speed:1.2, lifespan:15, size:2 | live |  |
| sp.goat | Keçi | evo | herb | hp:40, damage:5, armor:0, speed:1.2, lifespan:15, size:2 | live | Dağlara tırmanır |
| sp.owl | Baykuş | evo | carn | hp:20, damage:4, armor:0, speed:1.3, lifespan:20, size:1 | egg |  |
| sp.crow | Karga | evo | omni | hp:12, damage:2, armor:0, speed:1.4, lifespan:15, size:1 | egg | Zeki; ölüleri izler |
| sp.eagle | Kartal | evo | carn | hp:30, damage:8, armor:0, speed:1.8, lifespan:25, size:2 | egg |  |
| sp.parrot | Papağan | evo | herb | hp:12, damage:2, armor:0, speed:1.5, lifespan:40, size:1 | egg | Taklitçi; dil öğrenmeye yatkın |
| sp.seal | Fok | evo | carn | hp:70, damage:6, armor:2, speed:0.7, lifespan:25, size:2 | live |  |
| sp.hyena | Sırtlan | evo | carn | hp:55, damage:9, armor:0, speed:1.4, lifespan:15, size:2 | live |  |
| sp.lion | Aslan | evo | carn | hp:120, damage:18, armor:1, speed:1.4, lifespan:15, size:3 | live |  |
| sp.rhino | Gergedan | evo | herb | hp:220, damage:20, armor:10, speed:0.9, lifespan:40, size:4 | live | Hücum eder |
| sp.buffalo | Manda | evo | herb | hp:150, damage:12, armor:4, speed:0.9, lifespan:20, size:3 | live |  |
| sp.camel | Deve | evo | herb | hp:100, damage:5, armor:1, speed:1, lifespan:40, size:3 | live |  |
| sp.reindeer | Ren Geyiği | evo | herb | hp:70, damage:6, armor:1, speed:1.3, lifespan:15, size:2 | live |  |
| sp.polar_bear | Kutup Ayısı | evo | carn | hp:200, damage:22, armor:4, speed:0.9, lifespan:25, size:4 | live |  |
| sp.snail | Salyangoz | evo | herb | hp:15, damage:1, armor:6, speed:0.2, lifespan:5, size:1 | egg |  |
| sp.salamander | Semender | evo | carn | hp:40, damage:6, armor:2, speed:1, lifespan:20, size:1 | egg | Ateşte yaşar |
| sp.horse_wild | Yaban Atı | evo | herb | hp:90, damage:6, armor:0, speed:1.9, lifespan:25, size:3 | live | Evcilleşince binek |
| sp.fish | Balık | animal | herb | hp:8, damage:0, armor:0, speed:1.2, lifespan:4, size:1 | egg | Besin kaynağı |
| sp.butterfly | Kelebek | animal | herb | hp:3, damage:0, armor:0, speed:1, lifespan:1, size:1 | meta | Tırtıldan dönüşür |
| sp.caterpillar | Tırtıl | animal | herb | hp:5, damage:0, armor:0, speed:0.3, lifespan:1, size:1 | egg | Metamorfoz ile kelebeğe |
| sp.piranha | Pirana | animal | carn | hp:10, damage:6, armor:0, speed:1.6, lifespan:5, size:1 | egg | Suya düşeni parçalar |
| sp.mosquito_swarm | Sivrisinek Sürüsü | animal | carn | hp:8, damage:1, armor:0, speed:1.4, lifespan:1, size:1 | egg | Hastalık yayar |
| sp.dragon | Ejderha | monster | carn | hp:3000, damage:120, armor:40, speed:1.2, lifespan:1000, size:5 | egg | Şehirleri yakar, altın biriktirir |
| sp.sandworm | Kum Kurdu | monster | carn | hp:1500, damage:60, armor:20, speed:1, lifespan:300, size:5 | split | Yerin altından çıkıp yutar |
| sp.kraken | Derin Canavarı | monster | carn | hp:2000, damage:70, armor:20, speed:0.8, lifespan:500, size:5 | egg | Gemileri batırır |
| sp.ember_imp | Kor İblisi | monster | carn | hp:90, damage:14, armor:2, speed:1.2, lifespan:100, size:2 | summon | Yarıklardan çıkar |
| sp.rot_crawler | Çürük Sürüngen | monster | carn | hp:60, damage:10, armor:2, speed:1, lifespan:40, size:2 | split |  |
| sp.candy_golem | Şeker Golemi | monster | herb | hp:250, damage:15, armor:15, speed:0.6, lifespan:200, size:3 | split |  |
| sp.gummy_bear | Jöle Ayı | monster | herb | hp:60, damage:5, armor:5, speed:1, lifespan:20, size:2 | split |  |
| sp.shroomling | Mantarcık | monster | herb | hp:40, damage:5, armor:0, speed:0.9, lifespan:15, size:1 | spore | Spor hastalığıyla dönüşenler |
| sp.crystal_beetle | Kristal Böcek | monster | herb | hp:60, damage:6, armor:15, speed:0.8, lifespan:30, size:1 | egg |  |
| sp.ash_crawler | Kül Sürüngeni | monster | carn | hp:70, damage:11, armor:4, speed:1, lifespan:30, size:2 | egg |  |
| sp.bone_crawler | Kemik Örümceği | monster | carn | hp:80, damage:12, armor:5, speed:1.2, lifespan:50, size:2 | egg |  |
| sp.clock_crab | Saat Yengeci | monster | herb | hp:50, damage:6, armor:10, speed:0.8, lifespan:999, size:1 | egg | Yaşlanmaz |
| sp.void_moth | Boşluk Güvesi | monster | herb | hp:20, damage:3, armor:0, speed:1.3, lifespan:10, size:1 | egg | Manayı emer |
| sp.flesh_mound | Et Yığını | monster | carn | hp:200, damage:15, armor:0, speed:0.5, lifespan:50, size:3 | tumor | Tümör hastalığından doğar |
| sp.devourer | Yutucu Kütle | monster | omni | hp:150, damage:12, armor:5, speed:0.7, lifespan:999, size:3 | assimilate | Değdiği birimi kendine dönüştürür |
| sp.slime | Balçık | monster | omni | hp:40, damage:5, armor:0, speed:0.6, lifespan:30, size:1 | split | İkiye bölünerek çoğalır |
| sp.walking_tree | Yürüyen Ağaç | magic | herb | hp:400, damage:25, armor:15, speed:0.4, lifespan:800, size:4 | seed | Ormanları korur |
| sp.fairy | Peri | magic | herb | hp:20, damage:3, armor:0, speed:1.6, lifespan:500, size:1 | egg | Çiçekleri büyütür |
| sp.sky_whale_calf | Gök Balinası Yavrusu | magic | herb | hp:600, damage:5, armor:5, speed:0.5, lifespan:300, size:5 | live | Gökte süzülür |
| sp.skeleton | İskelet | undead | none | hp:60, damage:10, armor:2, speed:1, lifespan:0, size:2 | summon | Ölü diriltmeyle doğar |
| sp.zombie | Zombi | undead | brains | hp:80, damage:10, armor:0, speed:0.6, lifespan:0, size:2 | infect | Isırdığını dönüştürür |
| sp.zombie_runner | Koşucu Zombi | undead | brains | hp:55, damage:9, armor:0, speed:1.5, lifespan:0, size:2 | infect | Taze dönüşmüş; hızlı ama çabuk çürür (ömür 3 yıl) |
| sp.zombie_brute | İri Zombi | undead | brains | hp:320, damage:26, armor:6, speed:0.5, lifespan:0, size:4 | infect | İri birimlerden (ork, ayı, dev) dönüşür; kapıları kırar |
| sp.zombie_bloater | Şişkin Zombi | undead | brains | hp:120, damage:4, armor:0, speed:0.4, lifespan:0, size:3 | infect | Ölünce patlar; çevresine çürük bulutu yayar |
| sp.zombie_crawler | Sürünen Zombi | undead | brains | hp:30, damage:6, armor:0, speed:0.3, lifespan:0, size:1 | infect | Bacaksız; pusuda bekler, yüzebilir |
| sp.zombie_beast | Zombi Hayvan | undead | brains | hp:0, damage:0, armor:0, speed:0, lifespan:0, size:0 | infect | Enfekte hayvan: kendi türünün istatistikleri x0.8, hız x0.7, çürük paleti |
| sp.zombie_dragon | Çürük Ejderha | undead | brains | hp:2500, damage:90, armor:30, speed:1, lifespan:0, size:5 | infect | Ölen ejderha dönüşürse; ateş yerine çürük nefes |
| sp.zombie_lord | Zombi Efendisi | undead | brains | hp:600, damage:30, armor:8, speed:0.9, lifespan:0, size:3 | none | Büyük sürülerde 500 zombi başına bir tane doğar; sürüyü yönetir |
| sp.ghost | Hayalet | undead | none | hp:50, damage:8, armor:0, speed:1.2, lifespan:0, size:2 | summon | Duvarlardan geçer |
| sp.necromancer | Ölü Çağırıcı | undead | omni | hp:150, damage:10, armor:2, speed:1, lifespan:300, size:2 | none | Mezarlıklarda güçlenir |
| sp.bloodsucker | Kan Emici | undead | blood | hp:140, damage:16, armor:2, speed:1.3, lifespan:0, size:2 | infect | Sarımsaktan kaçar |
| sp.fire_elemental | Ateş Elementali | elemental | none | hp:120, damage:18, armor:0, speed:1.2, lifespan:0, size:2 | none | Soğuk çağlarda erir |
| sp.water_elemental | Su Elementali | elemental | none | hp:140, damage:12, armor:0, speed:1, lifespan:0, size:2 | none | Yangın söndürür |
| sp.earth_golem | Toprak Golemi | elemental | none | hp:400, damage:30, armor:25, speed:0.5, lifespan:0, size:4 | none | Yavaş ama durdurulamaz |
| sp.snowman | Kardan Adam | elemental | none | hp:40, damage:4, armor:0, speed:0.8, lifespan:0, size:2 | none | Sıcakta erir |
| sp.storm_spirit | Fırtına Ruhu | elemental | none | hp:90, damage:20, armor:0, speed:1.6, lifespan:0, size:2 | none |  |
| sp.dark_mage | Kara Büyücü | magic | omni | hp:120, damage:8, armor:0, speed:1, lifespan:200, size:2 | none | Köyleri yakar |
| sp.white_mage | Ak Büyücü | magic | omni | hp:120, damage:8, armor:0, speed:1, lifespan:200, size:2 | none | Yaralıları iyileştirir |
| sp.druid | Druid | magic | herb | hp:110, damage:8, armor:0, speed:1, lifespan:250, size:2 | none |  |
| sp.sky_visitor | Gök Ziyaretçisi | special | none | hp:200, damage:20, armor:10, speed:1.8, lifespan:999, size:2 | none | Gemilerle iner |
| sp.colossus | Mekanik Dev | special | none | hp:5000, damage:150, armor:60, speed:0.8, lifespan:0, size:5 | none | Oyuncunun yönetebildiği dev savaş makinesi |
| sp.bandit | Haydut | special | omni | hp:100, damage:14, armor:2, speed:1.1, lifespan:60, size:2 | live | Kanunsuz insanlar; köy yağmalar |
| sp.cultist | Tarikatçı | special | omni | hp:90, damage:9, armor:0, speed:1, lifespan:60, size:2 | live | Karanlık ritüeller |

## Birim Trait'leri (118)

| id | name | group | rarity | effects | opposite |
|---|---|---|---|---|---|
| tr.strong | Güçlü | body | normal | dmg +20% | tr.weak |
| tr.weak | Cılız | body | normal | dmg -20% | tr.strong |
| tr.swift | Çevik | body | normal | speed +20% | tr.sluggish |
| tr.sluggish | Hantal | body | normal | speed -20% | tr.swift |
| tr.giant | Dev Cüsse | body | rare | hp +50%, speed -10%, size +1 | tr.tiny |
| tr.tiny | Ufak Tefek | body | normal | hp -25%, dodge +10, size -1 | tr.giant |
| tr.tough | Dayanıklı | body | normal | hp +25% | tr.fragile |
| tr.fragile | Kırılgan | body | normal | hp -25% | tr.tough |
| tr.thick_skin | Kalın Deri | body | normal | armor +10 |  |
| tr.agile | Sıçrayışlı | body | normal | dodge +12 |  |
| tr.long_life | Uzun Ömürlü | body | rare | lifespan +50% | tr.short_life |
| tr.short_life | Kısa Ömürlü | body | normal | lifespan -40% | tr.long_life |
| tr.fertile | Doğurgan | body | normal | fertility +40% | tr.infertile |
| tr.infertile | Kısır | body | normal | fertility -100% | tr.fertile |
| tr.attractive | Alımlı | body | normal | mateChance +30%, diplo +1 | tr.ugly |
| tr.ugly | Çirkin | body | normal | mateChance -30% | tr.attractive |
| tr.genius | Dâhi | mind | rare | intel +5, xp +30% | tr.dim |
| tr.dim | Kalın Kafalı | mind | normal | intel -3, xp -20% | tr.genius |
| tr.wise | Bilge | mind | rare | intel +3, diplo +2 |  |
| tr.curious | Meraklı | mind | normal | xp +10%, neuron:explore |  |
| tr.forgetful | Unutkan | mind | normal | xp -15% |  |
| tr.strategist | Stratejist | mind | rare | warfare +4 |  |
| tr.orator | Hatip | mind | rare | diplo +4 |  |
| tr.steward | Kâhya | mind | rare | steward +4 |  |
| tr.bookworm | Kitap Kurdu | mind | normal | readBonus +100%, neuron:read_book | tr.illiterate |
| tr.illiterate | Okuma Bilmez | mind | normal | readBonus -100% | tr.bookworm |
| tr.fast_learner | Çabuk Kavrayan | mind | normal | xp +25% |  |
| tr.stubborn | İnatçı | mind | normal | convertResist +50% |  |
| tr.ambitious | Hırslı | char | normal | plotChance +50%, warfare +1 | tr.content |
| tr.content | Kanaatkâr | char | normal | happiness +2, plotChance -50% | tr.ambitious |
| tr.peaceful | Barışsever | char | normal | warChance -50%, opinion +10 | tr.bloodthirsty |
| tr.bloodthirsty | Kana Susamış | char | normal | dmg +10%, warChance +50% | tr.peaceful |
| tr.greedy | Açgözlü | char | normal | taxRate +20%, loyalty -5 | tr.generous |
| tr.generous | Cömert | char | normal | loyalty +10, opinion +5 | tr.greedy |
| tr.honest | Dürüst | char | normal | opinion +10 | tr.deceitful |
| tr.deceitful | Hilekâr | char | normal | plotSpeed +30%, opinion -10 | tr.honest |
| tr.brave | Yürekli | char | normal | fleeThreshold -50% | tr.coward |
| tr.coward | Korkak | char | normal | fleeThreshold +100% | tr.brave |
| tr.loyal | Sadık | char | normal | loyalty +15 | tr.treacherous |
| tr.treacherous | Hain | char | normal | loyalty -15, plotChance +30% | tr.loyal |
| tr.cruel | Zalim | char | normal | fear +20, happinessOthers -2 | tr.merciful |
| tr.merciful | Merhametli | char | normal | spareEnemy +50% | tr.cruel |
| tr.zealous | Bağnaz | char | normal | faith +50%, tolerance -50% | tr.skeptic |
| tr.skeptic | Şüpheci | char | normal | faith -50% | tr.zealous |
| tr.cheerful | Neşeli | char | normal | happiness +3 | tr.gloomy |
| tr.gloomy | Karamsar | char | normal | happiness -3 | tr.cheerful |
| tr.romantic | Âşık Ruhlu | char | normal | mateChance +50% | tr.loner |
| tr.loner | Yalnız Kurt | char | normal | neuron:wander, mateChance -40% | tr.romantic |
| tr.wanderer | Gezgin | char | normal | neuron:migrate, speed +5% | tr.homebody |
| tr.homebody | Ev Kuşu | char | normal | happiness +2, neuron:stay_home | tr.wanderer |
| tr.eagle_eye | Şahin Gözü | skill | normal | range +2, crit +10 |  |
| tr.blademaster | Kılıç Ustası | skill | rare | dmg +15%, atkspd +15% |  |
| tr.shield_wall | Kalkan Duvarı | skill | normal | armor +8, block +15 |  |
| tr.master_builder | Usta Eller | skill | normal | buildSpeed +50% |  |
| tr.green_thumb | Yeşil Parmak | skill | normal | farmYield +50% |  |
| tr.miner | Maden Burnu | skill | normal | mineYield +50% |  |
| tr.angler | Olta Ustası | skill | normal | fishYield +50% |  |
| tr.hunter | İzci | skill | normal | huntYield +50%, speed +5% |  |
| tr.sailor | Denizci | skill | normal | boatSpeed +30% |  |
| tr.smith | Demir Döven | skill | rare | craftQuality +1 |  |
| tr.healer | Şifacı | skill | rare | spell:heal |  |
| tr.trader | Tüccar Ruhu | skill | normal | tradeProfit +30% |  |
| tr.runner | Maratoncu | skill | normal | stamina +50%, speed +10% |  |
| tr.swimmer | Balık Gibi | skill | normal | flag:swim, swimSpeed +50% |  |
| tr.climber | Dağ Keçisi | skill | normal | mountainCost -60% |  |
| tr.iron_stomach | Demir Mide | skill | normal | hungerRate -30%, immune:food_poison | tr.glutton |
| tr.glutton | Obur | skill | normal | hungerRate +40% | tr.iron_stomach |
| tr.night_owl | Gece Kuşu | skill | normal | nightBonus +20% |  |
| tr.fire_breath | Ateş Soluğu | power | epic | spell:fire_breath, immune:burning |  |
| tr.frost_touch | Buz Dokunuşu | power | epic | onHit:freeze:10 |  |
| tr.venom | Zehirli Isırık | power | rare | onHit:poison:30 |  |
| tr.regeneration | Yenilenme | power | epic | regen +2 |  |
| tr.immortal | Ölümsüz | power | legendary | lifespan 0, flag:no_aging |  |
| tr.flight | Kanatlı | power | epic | flag:fly |  |
| tr.teleporter | Işınlanan | power | epic | spell:teleport, neuron:teleport_home |  |
| tr.stormcaller | Şimşek Çağıran | power | epic | spell:lightning |  |
| tr.necro | Ölü Fısıltısı | power | epic | spell:raise_dead |  |
| tr.mage_blood | Büyü Kanı | power | rare | mana +50, manaRegen +50% |  |
| tr.holy | Kutsal Işık | power | epic | spell:heal, dmgVsUndead +100% |  |
| tr.shadowstep | Gölge Adımı | power | epic | dodge +25, stealth +1 |  |
| tr.earthshaker | Yer Sarsan | power | epic | spell:quake_stomp |  |
| tr.vampiric | Kan Emici | power | epic | lifesteal +20%, weak:garlic |  |
| tr.thorns | Diken Kabuk | power | rare | reflect +15% |  |
| tr.volatile | Patlak | power | rare | onDeath:explode:small |  |
| tr.berserker | Cinnet Öfkesi | power | rare | lowHpDmg +50% |  |
| tr.courage_aura | Cesaret Aurası | power | epic | aura:morale:+20 |  |
| tr.blessed | Kutsanmış | power | rare | luck +10, hp +10% | tr.cursed |
| tr.cursed | Lanetli | power | rare | luck -10, happiness -2 | tr.blessed |
| tr.blind | Kör | cond | normal | range -3, crit -20 | tr.eagle_eye |
| tr.lame | Topal | cond | normal | speed -35% |  |
| tr.one_eye | Tek Göz | cond | normal | range -1 |  |
| tr.deaf | Sağır | cond | normal | ambushResist -30% |  |
| tr.sickly | Hastalıklı | cond | normal | diseaseResist -50% | tr.immune |
| tr.mad | Deli | cond | normal | flag:attack_all |  |
| tr.scarred | Yara İzli | cond | normal | fear +10 |  |
| tr.sleepless | Uykusuz | cond | normal | sleepNeed -50%, happiness -1 |  |
| tr.weak_immunity | Zayıf Bağışıklık | cond | normal | diseaseResist -30% | tr.immune |
| tr.immune | Bağışık | cond | rare | immune:plague, immune:infection | tr.sickly |
| tr.heatproof | Ateşe Dayanıklı | cond | normal | immune:heat, burnDmg -70% |  |
| tr.coldproof | Soğuğa Dayanıklı | cond | normal | immune:cold |  |
| tr.poisonproof | Zehre Dayanıklı | cond | normal | immune:poison |  |
| tr.waterborn | Su Doğumlu | cond | rare | flag:breathe_water, flag:swim |  |
| tr.chosen_one | Seçilmiş | legend | legendary | allStats +30%, luck +20 |  |
| tr.hero | Destan Kahramanı | legend | legendary | dmg +30%, hp +30%, aura:morale:+30 |  |
| tr.divine_touch | İlahi Dokunuş | legend | legendary | flag:edited |  |
| tr.plague_bearer | Salgın Taşıyıcı | legend | rare | carry:plague, immune:plague |  |
| tr.undead_curse | Ölümsüz Laneti | legend | epic | onDeath:rise_zombie |  |
| tr.golden_heart | Altın Yürek | legend | epic | happinessOthers +3, loyalty +20 |  |
| tr.kingslayer | Kral Katili | legend | epic | fear +30, warfare +2 |  |
| tr.survivor | Hayatta Kalan | legend | rare | hp +15%, fleeSuccess +30% |  |
| tr.veteran | Kıdemli | legend | rare | dmg +10%, armor +5 |  |
| tr.lucky | Şanslı | legend | rare | luck +15 | tr.unlucky |
| tr.unlucky | Talihsiz | legend | normal | luck -15 | tr.lucky |
| tr.moonborn | Ay Çocuğu | legend | epic | nightBonus +40%, mana +30 |  |
| tr.stormborn | Fırtına Doğumlu | legend | epic | immune:lightning, speed +15% |  |
| tr.zombie_hunter | Ölü Avcısı | skill | rare | dmgVsUndead +60%, infectResist +30% |  |
| tr.rot_resistant | Çürüğe Dirençli | cond | rare | infectResist +60% |  |
| tr.hollow | Oyuk | cond | normal | flag:undead, flag:no_needs, happiness 0 |  |

## Statü Efektleri (31)

| id | name | durationMonths | effect | note |
|---|---|---|---|---|
| st.burning | Yanıyor | 10 | hp:-3/tick | Suya girince/yağmurda biter; yanabilir tile'ı tutuşturur |
| st.frozen | Donmuş | 8 | flag:stunned | Hareket ve saldırı yok; ateş hasarı çözer |
| st.poisoned | Zehirlenmiş | 15 | hp:-1/tick | Şifacı ve kutsal ışık temizler |
| st.slowed | Yavaşlamış | 6 | speed:-50% |  |
| st.stunned | Sersem | 3 | flag:stunned |  |
| st.shielded | Kalkanlı | 20 | dmgTaken:-60% | Büyü kalkanı |
| st.sleeping | Uyuyor | 0 | flag:asleep | Enerji dolar; saldırıya açık |
| st.pregnant | Hamile | 12 | speed:-20% | Süre sonunda doğum |
| st.in_egg | Yumurtada | 10 | flag:immobile | Yumurta kırılınca yavru |
| st.cocoon | Kozada | 15 | flag:immobile | Metamorfoz |
| st.blessed | Kutsanmış | 30 | luck:+20/regen:+1 |  |
| st.cursed | Lanetli | 30 | luck:-20/happiness:-3 |  |
| st.inspired | İlham Almış | 20 | allStats:+20%/plotChance:+100% | Lider güçlenir |
| st.enraged | Öfkeli | 8 | dmg:+30%/armor:-5 |  |
| st.afraid | Korkmuş | 6 | flag:flee |  |
| st.drunk | Sarhoş | 6 | speed:-20%/happiness:+3 |  |
| st.starving | Açlıktan Ölüyor | 0 | hp:-1/tick/happiness:-5 | Doygunluk 0 iken |
| st.exhausted | Bitkin | 0 | speed:-30% | Stamina 0 |
| st.wet | Islak | 5 | fireResist:+50%/coldDmg:+30% |  |
| st.irradiated | Işınlanmış | 40 | hp:-1/20tick/mutation:+1 | Atom sonrası |
| st.possessed | Kontrol Ediliyor | 0 | flag:player_controlled | Oyuncu bu birimde |
| st.invisible | Görünmez | 10 | flag:untargetable |  |
| st.charmed | Büyülenmiş | 10 | flag:ally_of_caster |  |
| st.rooted | Sarmaşıkla Bağlı | 5 | flag:immobile |  |
| st.haste | Hızlanmış | 10 | speed:+50%/atkspd:+30% |  |
| st.weakened | Zayıflamış | 10 | dmg:-30% |  |
| st.mourning | Yasta | 30 | happiness:-4 | Yakını öldü |
| st.in_love | Âşık | 40 | happiness:+4 |  |
| st.celebrating | Kutlama | 10 | happiness:+5 | Savaş zaferi, festival |
| st.turning | Dönüşüyor | 2 | hp:-1/20tick/speed:-20% | Süre sonunda zombi; iyileştirilebilir |
| st.homesick | Sıla Hasreti | 20 | happiness:-2 | Uzak şehre göç |

## Hastalıklar (8)

| id | name | contagion | lethality | transformsInto | note |
|---|---|---|---|---|---|
| dis_plague | Kara Veba | 0.25 | 0.35 |  | Kalabalık şehirlerde hızla yayılır |
| dis_rotbite | Çürük Isırık | 0.9 | 0.0 | sp.zombie | Isırılan ölünce zombi olur; ölmeden de 2 yılda dönüşür |
| dis_fleshgrowth | Et Büyümesi | 0.05 | 0.1 | sp.flesh_mound | Tedavi edilmezse et yığınına dönüşür |
| dis_spores | Mantar Sporu | 0.15 | 0.05 | sp.shroomling | Mantar biyomlarında; mantarcığa dönüşür |
| dis_black_madness | Kara Cinnet | 0.1 | 0.0 |  | Herkese saldırır; kutsal ışık iyileştirir |
| dis_marsh_fever | Bataklık Humması | 0.2 | 0.1 |  | Yavaşlık ve halsizlik |
| dis_rabies | Kuduz | 0.5 | 0.4 |  | Hayvanlardan geçer; saldırganlık |
| dis_coughing_sickness | Öksürük Sayrılığı | 0.3 | 0.03 |  | Hafif; kışın artar |

## Büyüler (28)

| id | name | manaCost | cooldownTicks | range | effect |
|---|---|---|---|---|---|
| spell.fireball | Ateş Topu | 20 | 60 | 6 | Alan hasarı 30, tutuşturur |
| spell.fire_breath | Ateş Soluğu | 0 | 40 | 3 | Koni şeklinde yangın |
| spell.lightning | Şimşek | 25 | 80 | 8 | Tek hedef 60, zincirlenir |
| spell.heal | İyileştirme | 15 | 40 | 4 | Can +40 |
| spell.mass_heal | Toplu Şifa | 40 | 200 | 4 | Çevredeki müttefikler +30 |
| spell.shield | Büyü Kalkanı | 15 | 120 | 4 | Kalkanlı statüsü |
| spell.raise_dead | Ölü Diriltme | 30 | 120 | 5 | Yakındaki cesetlerden iskelet; çağıran medeniyse iskelet onun şehrine katılır |
| spell.teleport | Işınlanma | 20 | 200 | 30 | Rastgele ya da ev şehrine |
| spell.curse | Lanet | 15 | 90 | 6 | Lanetli statüsü |
| spell.bless | Kutsama | 15 | 90 | 6 | Kutsanmış statüsü |
| spell.freeze | Dondurma | 20 | 80 | 6 | Donmuş statüsü, alan |
| spell.poison_cloud | Zehir Bulutu | 25 | 120 | 5 | Alan zehirlenmesi |
| spell.summon_imp | İblis Çağırma | 40 | 300 | 4 | 2 kor iblisi |
| spell.meteor_call | Göktaşı Çağrısı | 80 | 1200 | 20 | Din büyüsü: hedefe küçük göktaşı |
| spell.quake_stomp | Yer Sarsıntısı | 30 | 150 | 2 | Çevredekileri sersemletir |
| spell.entangle | Sarmaşık | 15 | 80 | 6 | Kök salmış statüsü |
| spell.grow | Büyüme | 10 | 60 | 6 | Çevrede ağaç/bitki |
| spell.haste | Hızlandırma | 15 | 90 | 5 | Hızlanmış statüsü |
| spell.weaken | Zayıflatma | 15 | 90 | 6 | Zayıflamış statüsü |
| spell.charm | Büyüleme | 30 | 200 | 5 | Hedef geçici müttefik |
| spell.invisibility | Görünmezlik | 25 | 240 | 0 | Kendine |
| spell.rain_call | Yağmur Duası | 40 | 600 | 0 | Üstünde yağmur bulutu |
| spell.abduct | Kaçırma Işını | 0 | 60 | 6 | Birimi gemiye alır |
| spell.holy_smite | Kutsal Darbe | 30 | 120 | 6 | Ölümsüzlere 3x hasar |
| spell.rot_breath | Çürük Nefes | 20 | 120 | 4 | Koni: çürük ısırık bulaştırır |
| spell.cure_rot | Çürük Arındırma | 30 | 240 | 3 | Dönüşmekte olan birimi iyileştirir |
| spell.acid_spit | Asit Tükürüğü | 10 | 60 | 4 | Zırhı eritir: zırh -5, 10 hasar/sn |
| spell.inspire | İlham Verme | 30 | 300 | 6 | Müttefiklere ilham |

## Ekipman Tipleri (17)

| id | name | slot | baseEffects |
|---|---|---|---|
| eq.sword | Kılıç | weapon | dmg +8, atkspd 0 |
| eq.axe | Balta | weapon | dmg +11, atkspd -10% |
| eq.spear | Mızrak | weapon | dmg +7, range +1 |
| eq.bow | Yay | weapon | dmg +5, range +6 |
| eq.crossbow | Arbalet | weapon | dmg +9, range +5, atkspd -25% |
| eq.hammer | Savaş Çekici | weapon | dmg +13, knockback +50%, atkspd -20% |
| eq.dagger | Hançer | weapon | dmg +4, crit +10, atkspd +30% |
| eq.staff | Asa | weapon | mana +30, spellPower +20% |
| eq.club | Sopa | weapon | dmg +4 |
| eq.trident | Üç Dişli Zıpkın | weapon | dmg +7, range +1, waterDmg +30% |
| eq.sling | Sapan | weapon | dmg +3, range +4 |
| eq.helmet | Miğfer | helmet | armor +3 |
| eq.armor | Zırh | armor | armor +8, speed -5% |
| eq.shield | Kalkan | shield | armor +4, block +15 |
| eq.boots | Çizme | boots | speed +5% |
| eq.ring | Yüzük | ring | luck +5 |
| eq.amulet | Muska | amulet | mana +10, diseaseResist +10% |

## Malzemeler (11)

| id | name | multiplier | tier | special |
|---|---|---|---|---|
| mat.wood | Ahşap | 0.6 | 0 |  |
| mat.bone | Kemik | 0.7 | 0 |  |
| mat.copper | Bakır | 0.9 | 1 |  |
| mat.bronze | Tunç | 1.0 | 2 | Bakır+kalay |
| mat.iron | Demir | 1.2 | 3 |  |
| mat.steel | Çelik | 1.45 | 4 | Demir+kömür |
| mat.silver | Gümüş | 1.2 | 4 | Ölümsüzlere +%50 hasar |
| mat.obsidian | Obsidyen | 1.5 | 5 | Kritik +%10, kırılgan |
| mat.skyiron | Gökdemir | 1.8 | 6 | Hafif: hız cezası yok |
| mat.crystal | Kristal | 1.4 | 6 | Büyü gücü +%30 |
| mat.starore | Yıldız Çeliği | 2.3 | 8 | Efsanevi silahların malzemesi |

## Eşya Kaliteleri (6)

| id | name | multiplier | baseChance |
|---|---|---|---|
| q.crude | Kaba | 0.7 | 0.3 |
| q.common | Sıradan | 1.0 | 0.45 |
| q.fine | İyi | 1.2 | 0.15 |
| q.masterwork | Usta İşi | 1.45 | 0.07 |
| q.unique | Eşsiz | 1.8 | 0.025 |
| q.legendary | Efsanevi | 2.5 | 0.005 |


---

# Biyoloji

## Alt Tür Trait'leri (200)

| id | name | group | effects |
|---|---|---|---|
| sst.herbivore | Otçul | diet | diet:plant |
| sst.carnivore | Etçil | diet | diet:meat, dmg +5% |
| sst.omnivore | Hepçil | diet | diet:any |
| sst.insectivore | Böcekçil | diet | diet:insect |
| sst.piscivore | Balıkçıl | diet | diet:fish, flag:fish_hunt |
| sst.scavenger | Leşçil | diet | diet:corpse, immune:food_poison |
| sst.nectar | Nektarcı | diet | diet:flower |
| sst.lithophage | Taş Yiyen | diet | diet:stone |
| sst.photosynthesis | Fotosentez | diet | hungerRate -70%, needsSun +1 |
| sst.brain_eater | Beyin Yiyen | diet | diet:brain |
| sst.fungivore | Mantarcıl | diet | diet:mushroom |
| sst.bloodfeeder | Kan İçen | diet | diet:blood, lifesteal +10% |
| sst.cannibal | Yamyam | diet | diet:same_species, happinessOthers -3 |
| sst.fasting | Az Yiyen | diet | hungerRate -40% |
| sst.live_birth | Canlı Doğum | repro | repro:live |
| sst.egg_laying | Yumurtlama | repro | repro:egg |
| sst.budding | Tomurcuklanma | repro | repro:bud, fertility +30% |
| sst.fission | İkiye Bölünme | repro | repro:split |
| sst.spore_birth | Sporla Üreme | repro | repro:spore |
| sst.parthenogenesis | Eşeysiz Üreme | repro | flag:no_mate_needed |
| sst.twins | İkiz Eğilimi | repro | twinChance +30% |
| sst.litter | Kalabalık Batın | repro | litterSize:3-6 |
| sst.single_offspring | Tek Yavru | repro | litterSize +1, childSurvival +30% |
| sst.fast_gestation | Kısa Gebelik | repro | gestation -50% |
| sst.long_gestation | Uzun Gebelik | repro | gestation +100%, childStats +10% |
| sst.seasonal_breeding | Mevsimsel Üreme | repro | breedMonths:spring |
| sst.monogamous | Tek Eşli | repro | flag:lifelong_mate, happiness +2 |
| sst.polygamous | Çok Eşli | repro | fertility +25% |
| sst.fast_maturity | Erken Olgunlaşma | life | maturity -50% |
| sst.slow_maturity | Geç Olgunlaşma | life | maturity +80%, xp +15% |
| sst.long_lived_kind | Uzun Ömürlü Soy | life | lifespan +60% |
| sst.short_lived_kind | Kısa Ömürlü Soy | life | lifespan -50%, fertility +30% |
| sst.ageless | Yaşlanmayan | life | flag:no_aging |
| sst.metamorphic | Başkalaşımcı | life | flag:metamorphosis |
| sst.elder_wisdom | Yaşlı Bilgeliği | life | elderIntel +3 |
| sst.youth_vigor | Genç Dinçliği | life | youngSpeed +20% |
| sst.hibernation | Kış Uykusu | life | winterSleep +1, hungerRate -50% |
| sst.rebirth | Küllerden Doğuş | life | onDeath:rebirth:10% |
| sst.primitive_brain | İlkel Beyin | brain | intel -2, neuron:instinct |
| sst.advanced_memory | Gelişmiş Hafıza | brain | flag:can_hold_culture |
| sst.speech_center | Konuşma Merkezi | brain | flag:can_speak, flag:can_hold_language |
| sst.abstract_thought | Soyut Düşünce | brain | flag:can_hold_religion |
| sst.tool_use | Alet Kullanımı | brain | flag:can_use_items, flag:can_build |
| sst.planning | Planlama | brain | flag:can_plot |
| sst.pattern_seek | Örüntü Arayıcı | brain | bookWrite +50% |
| sst.empathy | Empati | brain | happinessOthers +1, spareEnemy +20% |
| sst.hive_mind | Kovan Zihni | brain | flag:shared_mind, loyalty +30 |
| sst.instinct_driven | İçgüdüsel | brain | neuron:instinct, intel -1 |
| sst.curious_minds | Meraklı Zihinler | brain | neuron:explore, xp +10% |
| sst.dream_walkers | Rüya Gezginleri | brain | sleepXp +50% |
| sst.sapience | Bilinç | brain | flag:sapient |
| sst.mimicry | Taklitçilik | brain | learnFromOthers +50% |
| sst.pack_hunter | Sürü Avcısı | social | packDmg +20%, neuron:pack_hunt |
| sst.solitary | Yalnız | social | flag:no_family, dmg +10% |
| sst.herd | Sürü Güdüsü | social | neuron:follow_herd, fleeTogether +1 |
| sst.colony | Koloni | social | flag:colony, buildSpeed +30% |
| sst.territorial | Bölgeci | social | neuron:guard_territory, aggro +30% |
| sst.nomadic | Göçebe | social | neuron:migrate, flag:no_permanent_city |
| sst.xenophile | Yabancı Sever | social | tolerance +50% |
| sst.xenophobe | Yabancı Düşmanı | social | tolerance -50% |
| sst.matriarchal | Ana Soylu | social | leaderRule:female |
| sst.patriarchal | Baba Soylu | social | leaderRule:male |
| sst.egalitarian | Eşitlikçi | social | happiness +1, loyalty +5 |
| sst.hierarchical | Hiyerarşik | social | warfare +1, happinessLow -1 |
| sst.communal_care | Ortak Bakım | social | childSurvival +30% |
| sst.peace_seeking | Barışçıl Soy | social | warChance -40% |
| sst.cold_adapted | Soğuğa Uyumlu | climate | immune:cold |
| sst.heat_adapted | Sıcağa Uyumlu | climate | immune:heat |
| sst.arid_adapted | Kuraklığa Uyumlu | climate | thirst -60% |
| sst.wet_adapted | Neme Uyumlu | climate | swampCost -50% |
| sst.altitude | Yüksek Rakım | climate | mountainCost -50% |
| sst.cave_dweller | Mağara Sakini | climate | flag:lives_in_mountains |
| sst.nocturnal | Gececi | climate | activeAt:night, nightBonus +20% |
| sst.diurnal | Gündüzcü | climate | activeAt:day |
| sst.sun_sensitive | Güneş Hassası | climate | sunDmg +1 |
| sst.storm_hardy | Fırtınaya Dayanıklı | climate | immune:lightning |
| sst.toxic_tolerant | Zehre Toleranslı | climate | immune:poison |
| sst.radiation_tolerant | Radyasyona Dayanıklı | climate | immune:radiation |
| sst.swimmer_kind | Yüzücü | move | flag:swim |
| sst.aquatic | Suda Yaşar | move | flag:water_only, flag:breathe_water |
| sst.amphibious | Amfibi | move | flag:swim, flag:breathe_water |
| sst.flying | Uçucu | move | flag:fly |
| sst.gliding | Süzülen | move | fallDmg 0, jumpRange +3 |
| sst.burrowing | Kazıcı | move | flag:burrow |
| sst.climbing | Tırmanıcı | move | mountainCost -60% |
| sst.fast_runner | Koşucu | move | speed +25% |
| sst.slow_mover | Ağır | move | speed -30%, armor +5 |
| sst.jumper | Sıçrayıcı | move | flag:jump |
| sst.migratory | Göçmen | move | neuron:seasonal_migrate |
| sst.hover | Havada Süzülen | move | flag:hover |
| sst.keen_sight | Keskin Görüş | sense | sight +4 |
| sst.keen_smell | Keskin Koku | sense | trackRange +8 |
| sst.echolocation | Yankı Algısı | sense | flag:see_in_dark |
| sst.thermal_vision | Isıl Görüş | sense | flag:see_invisible |
| sst.tremorsense | Titreşim Algısı | sense | ambushResist +50% |
| sst.poor_sight | Zayıf Görüş | sense | sight -3 |
| sst.sixth_sense | Altıncı His | sense | dodge +10 |
| sst.magic_sense | Büyü Algısı | sense | spellResist +20% |
| sst.fur | Kürk | cover | coldResist +30% |
| sst.thick_fur | Kalın Kürk | cover | immune:cold, heatDmg +30% |
| sst.scales | Pullu | cover | armor +5 |
| sst.shell | Kabuklu | cover | armor +12, speed -15% |
| sst.exoskeleton | Dış İskelet | cover | armor +8 |
| sst.feathers | Tüylü | cover | coldResist +20% |
| sst.bare_skin | Çıplak Deri | cover | speed +5%, armor -2 |
| sst.slime_coat | Balçık Kaplı | cover | grabResist +100% |
| sst.bark_skin | Kabuk Deri | cover | armor +6, fireDmg +50% |
| sst.stone_skin | Taş Deri | cover | armor +15, speed -20% |
| sst.spines | Dikenli | cover | reflect +15% |
| sst.camouflage | Kamuflaj | cover | stealth +1 |
| sst.toxic_skin | Zehirli Deri | cover | onHitTaken:poison |
| sst.regenerating_tissue | Yenilenen Doku | cover | regen +1 |
| sst.thick_bones | Kalın Kemik | cover | hp +15%, knockbackResist +50% |
| sst.hollow_bones | Kof Kemik | cover | speed +10%, hp -10% |
| sst.claws | Pençe | attack | dmg +4 |
| sst.fangs | Sivri Diş | attack | dmg +3, bleed +1 |
| sst.horns | Boynuz | attack | chargeDmg +50% |
| sst.tusks | Fildişi Diş | attack | dmg +5 |
| sst.stinger | İğne | attack | onHit:poison:20 |
| sst.venom_glands | Zehir Bezi | attack | onHit:poison:40 |
| sst.acid_spit | Asit Tükürüğü | attack | spell:acid_spit |
| sst.fire_glands | Ateş Bezi | attack | spell:fire_breath |
| sst.frost_breath | Buz Nefesi | attack | spell:freeze |
| sst.tail_whip | Kuyruk Kamçısı | attack | aoeHit +1 |
| sst.crushing_jaw | Ezici Çene | attack | armorPierce +30% |
| sst.charge | Hücum | attack | neuron:charge |
| sst.ambusher | Pusucu | attack | firstStrikeDmg +100% |
| sst.electric_organ | Elektrik Organı | attack | onHit:stun:5 |
| sst.fast_metabolism | Hızlı Metabolizma | metab | hungerRate +40%, speed +10% |
| sst.slow_metabolism | Yavaş Metabolizma | metab | hungerRate -40%, speed -10% |
| sst.fat_reserves | Yağ Deposu | metab | starveTime +100% |
| sst.cold_blooded | Soğukkanlı | metab | coldSlow +1, hungerRate -30% |
| sst.warm_blooded | Sıcakkanlı | metab | coldResist +20% |
| sst.efficient_digestion | Verimli Sindirim | metab | foodValue +30% |
| sst.weak_stomach | Zayıf Mide | metab | foodPoisonChance +30% |
| sst.water_storage | Su Deposu | metab | immune:drought |
| sst.high_stamina | Yüksek Dayanım | metab | stamina +50% |
| sst.low_stamina | Düşük Dayanım | metab | stamina -40% |
| sst.plague_resist | Veba Direnci | immune | diseaseResist +50% |
| sst.disease_prone | Hastalığa Yatkın | immune | diseaseResist -40% |
| sst.infection_immune | Çürüğe Bağışık | immune | immune:rotbite |
| sst.spore_immune | Spora Bağışık | immune | immune:spores |
| sst.madness_resist | Cinnet Direnci | immune | immune:black_madness |
| sst.fast_healing | Hızlı İyileşme | immune | regenOutOfCombat +100% |
| sst.antivenom | Panzehirli Kan | immune | immune:poison |
| sst.curse_ward | Lanet Kalkanı | immune | immune:cursed |
| sst.size_tiny | Minik Boy | size | size +1 |
| sst.size_small | Küçük Boy | size | size +2 |
| sst.size_medium | Orta Boy | size | size +3 |
| sst.size_large | İri Boy | size | size +4 |
| sst.size_huge | Devasa Boy | size | size +5 |
| sst.dimorphism | Eşey Farkı | size | flag:sex_dimorphism |
| sst.artistic | Sanatçı Ruh | calling | happiness +1, bookWrite +20% |
| sst.musical | Müzikal | calling | festivalHappiness +50% |
| sst.builders | İnşaatçı Soy | calling | buildSpeed +40% |
| sst.miners_kind | Madenci Soy | calling | mineYield +40% |
| sst.sailors_kind | Denizci Soy | calling | boatSpeed +30%, colonizeChance +30% |
| sst.farmers_kind | Çiftçi Soy | calling | farmYield +40% |
| sst.warrior_kind | Savaşçı Soy | calling | warfare +2, dmg +5% |
| sst.mystic_kind | Mistik Soy | calling | mana +30, faith +30% |
| sst.traders_kind | Tüccar Soy | calling | tradeProfit +40% |
| sst.scholars_kind | Âlim Soy | calling | intel +2 |
| sst.magic_affinity | Büyü Yatkınlığı | arcane | spellPower +30% |
| sst.anti_magic | Büyü Sağırı | arcane | spellResist +60%, flag:no_spells |
| sst.radiant | Işıyan | arcane | lightRadius +3, dmgVsUndead +30% |
| sst.shadow_born | Gölge Doğumlu | arcane | nightBonus +30%, stealth +1 |
| sst.elemental_fire | Ateş Özü | arcane | immune:burning, onHit:burn:10 |
| sst.elemental_water | Su Özü | arcane | flag:breathe_water, fireResist +50% |
| sst.elemental_earth | Toprak Özü | arcane | armor +8 |
| sst.elemental_air | Hava Özü | arcane | speed +15%, flag:hover |
| sst.undying | Ölmeyen | arcane | reviveOnce +1 |
| sst.soul_bond | Ruh Bağı | arcane | mateDeath:mourning_x2, pairDmg +15% |
| sst.time_touched | Zaman Dokunmuş | arcane | agingRandom +1 |
| sst.void_touched | Boşluk Dokunmuş | arcane | manaDrainAura +1 |
| sst.crystal_body | Kristal Beden | arcane | armor +10, flag:shatter_on_death |
| sst.plant_body | Bitkisel Beden | arcane | hungerRate -50%, fireDmg +50% |
| sst.gold_digest | Altın Sindiren | arcane | diet:gold |
| sst.divine_chosen | Tanrı Seçkini | arcane | luck +15, blessChance +50% |
| sst.mutable | Değişken Genom | arcane | mutation +100% |
| sst.stable_genome | Sabit Genom | arcane | mutation -80% |
| sst.pure | Saf Kan | arcane | allStats +5%, mutation -50% |
| sst.hybrid_vigor | Melez Gücü | arcane | hybridBonus +15% |
| sst.builds_nests | Yuva Kurar | behavior | neuron:build_nest |
| sst.food_hoarder | Yiyecek Biriktirir | behavior | neuron:hoard_food |
| sst.gift_giver | Hediye Verir | behavior | neuron:give_gift, opinion +5 |
| sst.grave_keepers | Ölülerini Gömer | behavior | neuron:bury_dead, happiness +1 |
| sst.stargazers | Yıldız Gözlemcisi | behavior | neuron:stargaze, intel +1 |
| sst.dancers | Dansçılar | behavior | neuron:dance, happiness +2 |
| sst.fire_makers | Ateş Yakar | behavior | neuron:make_fire, coldResist +20% |
| sst.domesticators | Evcilleştirici | behavior | neuron:tame_animal |
| sst.story_tellers | Hikâye Anlatıcı | behavior | neuron:tell_story, xpShare +10% |
| sst.raiders | Yağmacı | behavior | neuron:raid, lootBonus +50% |
| sst.shiny_collectors | Parlak Toplayıcı | behavior | neuron:collect_shiny |
| sst.sun_greeters | Güneşe Selam | behavior | neuron:greet_sun, faith +10% |
| sst.divine_breeding | İlahi Islah | mark | flag:edited |
| sst.uplifted | Yükseltilmiş | mark | flag:uplifted |
| sst.unmoving | Kıpırtısız | mark | speed 0 |
| sst.antimatter_core | Karşı Madde Özü | mark | onDeath:explode:huge |
| sst.monolith_touched | Monolit Dokunuşu | mark | xp +20%, mutation +30% |
| sst.first_of_kind | İlk Soy | mark | flag:founder_lineage |

> Medenileşme şartı: `sapience` + `tool_use`. Kültür tutabilmek için `advanced_memory`, dil için `speech_center`, din için `abstract_thought`, plot kurabilmek için `planning` gerekir. `canAppearRandomly=false` olanlar yalnızca editör veya özel olaylarla gelir.

## Genler (45)

| id | name | effects |
|---|---|---|
| gene.vitality | Canlılık | hp +10% |
| gene.might | Kuvvet | dmg +8% |
| gene.haste | Çabukluk | speed +6% |
| gene.guard | Muhafız | armor +3 |
| gene.focus | Odak | crit +5 |
| gene.reach | Erim | range +1 |
| gene.persistence | Süreklilik | lifespan +12% |
| gene.brood | Döl | fertility +15% |
| gene.mind | Zihin | intel +1 |
| gene.charm | Cazibe | diplo +1 |
| gene.battle | Cenk | warfare +1 |
| gene.order | Düzen | steward +1 |
| gene.mana | Mana | mana +15 |
| gene.mending | Onarım | regen +0.5 |
| gene.stamina | Soluk | stamina +20% |
| gene.appetite | Tokluk | hungerRate -12% |
| gene.rest | Dinçlik | sleepNeed -20% |
| gene.hardiness | Sertlik | diseaseResist +15% |
| gene.warmth | Sıcaklık | heatResist +25% |
| gene.chill | Serinlik | coldResist +25% |
| gene.growth | Büyüme | size +0.5, hp +5% |
| gene.dwarfism | Küçülme | size -0.5, dodge +5 |
| gene.calm | Sükûnet | happiness +1 |
| gene.rage | Hiddet | dmg +12%, happiness -1 |
| gene.luck | Talih | luck +5 |
| gene.night | Gece | nightBonus +10% |
| gene.day | Gün | dayBonus +10% |
| gene.fin | Yüzgeç | swimSpeed +30% |
| gene.grip | Kavrama | mountainCost -20% |
| gene.keen | Keskinlik | sight +2 |
| gene.bones | Kemik | knockbackResist +25% |
| gene.hide | Post | armor +2, coldResist +10% |
| gene.blood | Kan | bleedResist +50% |
| gene.nerve | Sinir | atkspd +8% |
| gene.heart | Yürek | fleeThreshold -20% |
| gene.lungs | Ciğer | breathHold +100% |
| gene.liver | Karaciğer | poisonResist +40% |
| gene.mutagen | Mutajen | mutation +50% |
| gene.stabilizer | Dengeleyici | mutation -50% |
| gene.empty | Boş Dizi |  |
| gene.junk | Hurda Dizi | hp -3% |
| gene.dominant | Baskın | inheritWeight +50% |
| gene.recessive | Çekinik | inheritWeight -50% |
| gene.ancient | Kadim | xp +15%, lifespan +5% |
| gene.chimera | Kimera | randomTraitOnBirth +5% |

## Gen Sinerjileri (8)

| id | name | requires | bonus |
|---|---|---|---|
| syn.titan | Titan Uyumu | growth, vitality, bones | hp +20%, size +1 |
| syn.hunter_line | Avcı Soyu | haste, keen, focus | crit +10, speed +5% |
| syn.sage_line | Bilge Soyu | mind, ancient, persistence | intel +2, lifespan +10% |
| syn.berserk_line | Cinnet Soyu | rage, nerve, heart | dmg +15% |
| syn.sea_line | Deniz Soyu | fin, lungs, chill | flag:breathe_water |
| syn.frost_line | Ayaz Soyu | chill, hide, stamina | immune:cold |
| syn.ember_line | Kor Soyu | warmth, blood, rage | immune:burning |
| syn.golden_line | Altın Soy | luck, charm, calm | luck +15, diplo +2 |

## Fenotipler (50)

| id | name | baseColor | pattern | biomeBias |
|---|---|---|---|---|
| ph.sand_plain | Kum Düz | #D8C08A | plain | desert, savanna |
| ph.sand_spotted | Kum Benekli | #D8C08A | spotted | desert, savanna |
| ph.sand_striped | Kum Çizgili | #D8C08A | striped | desert, savanna |
| ph.sand_ringed | Kum Halkalı | #D8C08A | ringed | desert, savanna |
| ph.sand_shaded | Kum Gölgeli | #D8C08A | shaded | desert, savanna |
| ph.ash_plain | Kül Düz | #8A8580 | plain | ash, volcanic |
| ph.ash_spotted | Kül Benekli | #8A8580 | spotted | ash, volcanic |
| ph.ash_striped | Kül Çizgili | #8A8580 | striped | ash, volcanic |
| ph.ash_ringed | Kül Halkalı | #8A8580 | ringed | ash, volcanic |
| ph.ash_shaded | Kül Gölgeli | #8A8580 | shaded | ash, volcanic |
| ph.night_plain | Gece Düz | #2E2B3A | plain | void, corrupted |
| ph.night_spotted | Gece Benekli | #2E2B3A | spotted | void, corrupted |
| ph.night_striped | Gece Çizgili | #2E2B3A | striped | void, corrupted |
| ph.night_ringed | Gece Halkalı | #2E2B3A | ringed | void, corrupted |
| ph.night_shaded | Gece Gölgeli | #2E2B3A | shaded | void, corrupted |
| ph.snow_plain | Kar Düz | #EEF2F4 | plain | tundra, snowpine |
| ph.snow_spotted | Kar Benekli | #EEF2F4 | spotted | tundra, snowpine |
| ph.snow_striped | Kar Çizgili | #EEF2F4 | striped | tundra, snowpine |
| ph.snow_ringed | Kar Halkalı | #EEF2F4 | ringed | tundra, snowpine |
| ph.snow_shaded | Kar Gölgeli | #EEF2F4 | shaded | tundra, snowpine |
| ph.crimson_plain | Kızıl Düz | #A23A2E | plain | infernal, maple |
| ph.crimson_spotted | Kızıl Benekli | #A23A2E | spotted | infernal, maple |
| ph.crimson_striped | Kızıl Çizgili | #A23A2E | striped | infernal, maple |
| ph.crimson_ringed | Kızıl Halkalı | #A23A2E | ringed | infernal, maple |
| ph.crimson_shaded | Kızıl Gölgeli | #A23A2E | shaded | infernal, maple |
| ph.honey_plain | Bal Düz | #D9A441 | plain | honey, flower |
| ph.honey_spotted | Bal Benekli | #D9A441 | spotted | honey, flower |
| ph.honey_striped | Bal Çizgili | #D9A441 | striped | honey, flower |
| ph.honey_ringed | Bal Halkalı | #D9A441 | ringed | honey, flower |
| ph.honey_shaded | Bal Gölgeli | #D9A441 | shaded | honey, flower |
| ph.olive_plain | Zeytin Düz | #6E7A3A | plain | swamp, forest |
| ph.olive_spotted | Zeytin Benekli | #6E7A3A | spotted | swamp, forest |
| ph.olive_striped | Zeytin Çizgili | #6E7A3A | striped | swamp, forest |
| ph.olive_ringed | Zeytin Halkalı | #6E7A3A | ringed | swamp, forest |
| ph.olive_shaded | Zeytin Gölgeli | #6E7A3A | shaded | swamp, forest |
| ph.sea_plain | Deniz Düz | #3E7FA8 | plain | coral |
| ph.sea_spotted | Deniz Benekli | #3E7FA8 | spotted | coral |
| ph.sea_striped | Deniz Çizgili | #3E7FA8 | striped | coral |
| ph.sea_ringed | Deniz Halkalı | #3E7FA8 | ringed | coral |
| ph.sea_shaded | Deniz Gölgeli | #3E7FA8 | shaded | coral |
| ph.violet_plain | Menekşe Düz | #7C5AA6 | plain | enchanted, timewarp, mushroom |
| ph.violet_spotted | Menekşe Benekli | #7C5AA6 | spotted | enchanted, timewarp, mushroom |
| ph.violet_striped | Menekşe Çizgili | #7C5AA6 | striped | enchanted, timewarp, mushroom |
| ph.violet_ringed | Menekşe Halkalı | #7C5AA6 | ringed | enchanted, timewarp, mushroom |
| ph.violet_shaded | Menekşe Gölgeli | #7C5AA6 | shaded | enchanted, timewarp, mushroom |
| ph.gold_plain | Altın Düz | #E0C04A | plain | celestial, citrus |
| ph.gold_spotted | Altın Benekli | #E0C04A | spotted | celestial, citrus |
| ph.gold_striped | Altın Çizgili | #E0C04A | striped | celestial, citrus |
| ph.gold_ringed | Altın Halkalı | #E0C04A | ringed | celestial, citrus |
| ph.gold_shaded | Altın Gölgeli | #E0C04A | shaded | celestial, citrus |

## Evrim Kuralları (6)

| id | name | stage | grants | note |
|---|---|---|---|---|
| evo.monolith_stage1 | Monolit — Uyanış | 1 | curious_minds, tool_use | Çevredeki hayvanlara 20 yılda bir |
| evo.monolith_stage2 | Monolit — Söz | 2 | speech_center, advanced_memory | Aşama 1'i geçmiş alt türler |
| evo.monolith_stage3 | Monolit — Bilinç | 3 | sapience, abstract_thought, planning | Tür medeni olur; ilk köy kurulur |
| evo.isolation_drift | İzolasyon Kayması | 0 |  | 100 yıl izole popülasyon: %30 ihtimalle yeni alt tür + 1–2 trait |
| evo.biome_adapt | Biyom Uyumu | 0 |  | Yeni biyomda doğan ilk nesil biyomun alt tür havuzundan trait alabilir |
| evo.radiation_mutation | Radyasyon Mutasyonu | 0 |  | Işınlanmış statüsü doğumda mutasyon oranını x5 yapar |

## Dönüşümler (13)

| id | from | to | trigger | note |
|---|---|---|---|---|
| meta.caterpillar_to_butterfly | sp.caterpillar | sp.butterfly | age:1 | Kozada 3 ay |
| meta.tadpole_frog | sp.frog | sp.frog | age:0.5 | Yavru formu suda |
| meta.corpse_to_zombie | * | sp.zombie | dis_rotbite | Ölüm anında |
| meta.animal_to_zombie_beast | *animal | sp.zombie_beast | dis_rotbite | Hayvanlar |
| meta.big_to_brute | *size>=4 | sp.zombie_brute | dis_rotbite | İri birimler |
| meta.fresh_to_runner | * | sp.zombie_runner | dis_rotbite:%25 | Taze ölülerin %25'i |
| meta.dragon_to_rot | sp.dragon | sp.zombie_dragon | dis_rotbite | Ejderha |
| meta.runner_decay | sp.zombie_runner | sp.zombie_crawler | age:3 | Koşucu çürüyünce |
| meta.growth_to_mound | * | sp.flesh_mound | dis_fleshgrowth | Hastalık sonu |
| meta.spores_to_shroom | * | sp.shroomling | dis_spores | Hastalık sonu |
| meta.mage_to_necro | sp.dark_mage | sp.necromancer | age:150 | Yaşlı kara büyücü |
| meta.tree_awaken | feat.tree_oak | sp.walking_tree | era.glimmer | Işıltı çağında 500 yaşındaki meşe uyanabilir |
| meta.slime_merge | sp.slime | sp.slime | merge:4 | 4 balçık birleşip büyür |


---

# Medeniyet ve Meta

## Binalar (41)

| id | name | category | size | hp | cost | requires | function |
|---|---|---|---|---|---|---|---|
| bld.bonfire | Kamp Ateşi | center | 2x2 | 100 |  |  | Şehrin ilk çekirdeği; ısınma, toplanma |
| bld.hall_1 | Köy Meydanı | center | 3x3 | 400 | res.wood:20 | pop:8 | Şehir merkezi; bayrak, vergi toplanır |
| bld.hall_2 | Kasaba Konağı | center | 4x4 | 900 | res.wood:30, res.stone:30 | pop:30/hall_1 | Sınır yarıçapı +, lider konutu |
| bld.hall_3 | Şehir Sarayı | center | 5x5 | 2000 | res.stone:60, res.gold:30 | pop:80/hall_2 | Başkent olabilir; ordu kurar |
| bld.tent | Çadır | house | 2x2 | 60 | res.wood:2, res.leather:2 |  | 2 kişilik barınak |
| bld.hut | Kulübe | house | 2x2 | 120 | res.wood:6 | hall_1 | 4 kişi |
| bld.house | Ev | house | 3x2 | 250 | res.wood:8, res.stone:6 | hall_2 | 6 kişi; mutluluk +1 |
| bld.manor | Konak | house | 3x3 | 450 | res.stone:14, res.gold:4 | hall_3 | 10 kişi; mutluluk +2 |
| bld.storage | Ambar | economy | 3x2 | 200 | res.wood:10 | hall_1 | Kaynak deposu; toplayıcılar buraya taşır |
| bld.granary | Tahıl Ambarı | economy | 2x3 | 200 | res.wood:8, res.stone:4 | farm | Yiyecek bozulması -%50 |
| bld.windmill | Değirmen | economy | 2x2 | 180 | res.wood:12, res.stone:4 | granary | Buğday → un |
| bld.bakery | Fırın | economy | 2x2 | 160 | res.stone:8 | windmill | Un → ekmek |
| bld.well | Kuyu | economy | 1x1 | 150 | res.stone:6 | hall_1 | Yangın söndürme hızı; kuraklıkta su |
| bld.mine | Maden Ocağı | economy | 3x3 | 300 | res.wood:10 | hills/mountain yakın | Taş ve cevher çıkarır |
| bld.lumber_camp | Kereste Kampı | economy | 2x2 | 150 | res.wood:4 | orman yakın | Odun verimi +%30 |
| bld.smithy | Demirhane | economy | 2x2 | 250 | res.stone:10, res.iron:4 | hall_2 | Silah/zırh üretir |
| bld.farm_shed | Tarla Barakası | economy | 2x2 | 120 | res.wood:6 | hall_1 | Etrafına tarla açar |
| bld.pasture | Ağıl | economy | 4x3 | 120 | res.wood:10 | evcil hayvan | Hayvan besler: süt, yün, et |
| bld.beehive | Arı Kovanı | economy | 1x1 | 40 | res.wood:2 | çiçek yakın | Bal üretir |
| bld.market | Pazar Yeri | economy | 4x3 | 300 | res.wood:12, res.stone:8 | pop:40 | Ticaret kervanı, altın |
| bld.inn | Han | social | 3x3 | 300 | res.wood:14, res.stone:6 | pop:30 | Göçmen çeker, mutluluk +2 |
| bld.stable | Ahır | military | 3x2 | 200 | res.wood:10 | pop:40/hall_2 | Binek hayvanları; süvari |
| bld.barracks | Kışla | military | 4x3 | 500 | res.stone:20, res.wood:10 | hall_2 | Asker eğitir, ordu kapasitesi |
| bld.watchtower | Gözetleme Kulesi | military | 1x1 | 400 | res.stone:10 | hall_2 | Menzilli otomatik savunma |
| bld.wall | Sur | military | 1x1 | 300 | res.stone:2 | hall_2 | Geçilmez; kuşatmada yıkılır |
| bld.gate | Kapı | military | 2x1 | 500 | res.stone:6, res.wood:4 | wall | Dostlara açık |
| bld.docks | İskele | naval | 3x2 | 250 | res.wood:12 | kıyı | Balıkçı ve nakliye gemileri |
| bld.fishing_hut | Balıkçı Barakası | naval | 2x2 | 120 | res.wood:6 | kıyı | Balık verimi |
| bld.shipyard | Tersane | naval | 4x3 | 400 | res.wood:30 | docks/pop:50 | Savaş ve sömürge gemileri |
| bld.library | Kütüphane | culture | 3x3 | 350 | res.wood:10, res.stone:14 | dil/pop:30 | Kitap saklar; okuma |
| bld.school | Mektep | culture | 3x2 | 250 | res.wood:12 | library | Çocuklara XP |
| bld.temple | Tapınak | culture | 3x3 | 500 | res.stone:20 | din | Din yayılımı, rahip, büyü |
| bld.grand_temple | Büyük Tapınak | culture | 5x5 | 1500 | res.stone:60, res.gold:20 | temple/başkent | Din merkezi; kutsal büyüler |
| bld.healers_house | Şifahane | culture | 3x2 | 300 | res.wood:10, res.stone:8 | herbs | Hastalık iyileşmesi x2 |
| bld.graveyard | Mezarlık | culture | 3x3 | 100 | res.stone:4 | pop:20 | Ölüler gömülür; yas süresi kısalır |
| bld.statue_god | Yaratıcı Heykeli | monument | 2x2 | 800 | res.stone:30, res.gold:10 | hall_3 | Oyuncuya adanmış; mutluluk +3 (yarıçap) |
| bld.monument | Zafer Anıtı | monument | 2x2 | 800 | res.stone:30 | savaş zaferi | Sadakat +10 |
| bld.pyre | Ölü Yakma Ocağı | culture | 2x2 | 150 | res.wood:10, res.stone:4 | pop:30 | Cesetleri yakar: şehirde zombi kalkışı olmaz |
| bld.nest | Yuva | house | 2x2 | 80 | res.wood:2 | hayvan kökenli uygarlık | Evrimleşmiş hayvanların ilk evi |
| bld.hive | Kovan | house | 3x3 | 200 | res.wood:4, res.honey:2 | kovan zihni | Arı/karınca uygarlığı evi |
| bld.ruins | Harabe | ruin | * | 50 |  |  | Yıkılan binadan kalır; zamanla kaybolur |

## Bina Stilleri (6)

| id | name | description |
|---|---|---|
| style.human | İnsan | Kerpiç/ahşap duvar, kiremit kırmızısı çatı, taş temeller |
| style.elf | Elf | Ağaç gövdelerine oyulmuş evler, yaprak çatılar, kıvrımlı hatlar |
| style.dwarf | Cüce | Kesme taş, dağa gömülü kapılar, bakır süslemeler |
| style.orc | Ork | Deri gerilmiş kazık çadırlar, kemik süsler, koyu ahşap |
| style.beast | Evrimleşmiş Hayvan | Toprak ve dal yuvalar; türün rengine göre tonlanır |
| style.insect | Böcek Uygarlığı | Balmumu ve çamur petekler, tüneller |

## Meslekler (19)

| id | name | task | requiresBuilding |
|---|---|---|---|
| job.builder | İnşaatçı | Bina yapar/onarır |  |
| job.gatherer | Toplayıcı | Meyve, ot, mantar toplar |  |
| job.farmer | Çiftçi | Tarla eker/biçer | bld.farm_shed |
| job.lumberjack | Oduncu | Ağaç keser |  |
| job.miner | Madenci | Taş/cevher çıkarır | bld.mine |
| job.fisher | Balıkçı | Kıyıdan/tekneden balık | bld.fishing_hut |
| job.hunter | Avcı | Hayvan avlar |  |
| job.herder | Çoban | Hayvan besler | bld.pasture |
| job.smith | Demirci | Ekipman üretir | bld.smithy |
| job.baker | Fırıncı | Ekmek yapar | bld.bakery |
| job.trader | Tüccar | Şehirler arası ticaret | bld.market |
| job.warrior | Asker | Savaşır, devriye | bld.barracks |
| job.guard | Muhafız | Kule/kapıda nöbet | bld.watchtower |
| job.priest | Rahip | Dua, din yayar, iyileştirir | bld.temple |
| job.scholar | Âlim | Kitap yazar, okur | bld.library |
| job.healer | Şifacı | Hastaları iyileştirir | bld.healers_house |
| job.sailor | Gemici | Gemi kullanır | bld.docks |
| job.leader | Şehir Lideri | Şehri yönetir |  |
| job.king | Kral | Krallığı yönetir |  |

## Kültür Trait'leri (78)

| id | name | group | effects |
|---|---|---|---|
| cul.succ_primogeniture | Büyük Evlat | succession | leader:eldest_child |
| cul.succ_election | Seçimle | succession | leader:highest_diplo |
| cul.succ_strongest | En Güçlü | succession | leader:highest_warfare, neuron:duel_for_crown |
| cul.succ_wisest | En Bilge | succession | leader:highest_intel |
| cul.succ_elders | Yaşlılar Meclisi | succession | leader:oldest |
| cul.succ_lot | Kura | succession | leader:random |
| cul.succ_matrilineal | Ana Soydan | succession | leader:eldest_daughter |
| cul.succ_youngest | Küçük Evlat | succession | leader:youngest_child |
| cul.war_drums | Savaş Davulları | war | warChance +30%, morale +10 |
| cul.iron_fist | Demir Yumruk | war | rebellionResist +40%, happiness -1 |
| cul.honor_duels | Düello Onuru | war | neuron:duel |
| cul.siege_masters | Kuşatma Ustaları | war | wallDmg +100% |
| cul.shield_brothers | Kalkan Kardeşleri | war | armorInGroup +5 |
| cul.horse_lords | Atlı Beyler | war | mountedUnits +1, speed +20% |
| cul.archers_pride | Okçu Gururu | war | bowPreference +1, range +1 |
| cul.no_retreat | Geri Çekilmez | war | fleeThreshold -100% |
| cul.spare_the_weak | Zayıfa Dokunmaz | war | noCivilianKill +1, opinion +10 |
| cul.raid_culture | Akıncılar | war | neuron:raid, lootBonus +50% |
| cul.expansionists | Yayılmacılar | society | newCityChance +50% |
| cul.isolationists | İçe Kapanık | society | newCityChance -50%, opinion -10 |
| cul.nomads | Göçebeler | society | flag:mobile_city |
| cul.sea_folk | Deniz Halkı | society | colonizeChance +60%, boatSpeed +20% |
| cul.open_hearth | Açık Ocak | society | tolerance:other_species:+100% |
| cul.pure_blood | Soy Arıklığı | society | tolerance:other_species:-100% |
| cul.melting_pot | Harman | society | mixedSpeciesHappiness +2 |
| cul.hospitality | Misafirperver | society | migrantChance +50% |
| cul.frontier_spirit | Öncü Ruh | society | borderGrowth +30% |
| cul.city_lovers | Kent Sevdalısı | society | maxCitySize +30% |
| cul.villagers | Köy Hayatı | society | maxCitySize -30%, happiness +1 |
| cul.great_walls | Sur Kültürü | society | buildWalls +1 |
| cul.farmers | Toprak Ehli | economy | farmYield +30% |
| cul.harvest_feast | Hasat Şöleni | economy | yearlyFestival +1, happiness +2 |
| cul.hunters | Avcı Gelenekleri | economy | huntYield +40% |
| cul.spice_road | Baharat Yolu | economy | tradeProfit +40% |
| cul.stone_carvers | Taş Oymacıları | economy | stoneBuildings +1, buildingHp +30% |
| cul.artisans | Zanaatkârlar | economy | craftQuality +1 |
| cul.merchants | Loncalar | economy | gold +30% |
| cul.miners_guild | Maden Loncası | economy | mineYield +40% |
| cul.fishers | Balıkçı Köyleri | economy | fishYield +40% |
| cul.herders | Çobanlar | economy | herdYield +40% |
| cul.scholars | Âlimler | knowledge | bookWrite +50% |
| cul.reading_lovers | Okur Halk | knowledge | readChance +100% |
| cul.oral_tradition | Sözlü Gelenek | knowledge | xpShare +20%, bookWrite -50% |
| cul.ancestral_knowledge | Atalardan Bilgi | knowledge | newbornGetsHalfBestParentAttr +1 |
| cul.library_keepers | Kitap Bekçileri | knowledge | bookDecay -80% |
| cul.astronomers | Yıldızbilimciler | knowledge | meteorWarning +1 |
| cul.inventors | Mucitler | knowledge | buildingUpgradeSpeed +30% |
| cul.book_burners | Kitap Yakıcılar | knowledge | burnForeignBooks +1 |
| cul.mystics | Gizemciler | knowledge | spellPower +15% |
| cul.healers_guild | Şifacılar Ocağı | knowledge | diseaseCure +40% |
| cul.elders_voice | Yaşlılara Hürmet | family | elderHappiness +3, elderIntel +1 |
| cul.youth_cult | Gençlik Kültü | family | youngXp +30% |
| cul.ancestor_halls | Ata Ocakları | family | deadMourning -50%, faith +10% |
| cul.large_families | Kalabalık Aileler | family | fertility +30% |
| cul.small_families | Çekirdek Aile | family | fertility -20%, childStats +10% |
| cul.arranged_marriage | Görücü Usulü | family | clanStrength +20% |
| cul.free_love | Serbest Gönül | family | mateChance +30% |
| cul.orphan_care | Yetim Koruma | family | childSurvival +30% |
| cul.twin_blessing | İkiz Uğuru | family | twinHappiness +5 |
| cul.burial_rites | Defin Ritüelleri | family | buildGraveyard +1, happiness +1 |
| cul.forest_keepers | Orman Bekçileri | nature | treeCutting -60%, elfOpinion +10 |
| cul.swamp_dwellers | Bataklık Halkı | nature | swampHappiness +2 |
| cul.animal_friends | Hayvan Dostları | nature | huntYield -50%, tameChance +50% |
| cul.tree_cutters | Baltacılar | nature | woodYield +40% |
| cul.beast_tamers | Canavar Terbiyecileri | nature | tameMonsters +1 |
| cul.fire_keepers | Ateş Bekçileri | nature | fireSpreadInCity -60% |
| cul.mountain_folk | Dağ Halkı | nature | mountainHappiness +2 |
| cul.true_roots | Köklere Sadakat | special | convertResist +100%, survivesKingdomFall +1 |
| cul.festivals | Şenlikler | special | monthlyFestivalChance +10% |
| cul.stoic | Metanet | special | happinessSwing -50% |
| cul.superstitious | Batıl İnançlı | special | eraEffects:x1.5 |
| cul.pacifist_creed | Barış Yemini | special | warChance -80% |
| cul.blood_feud | Kan Davası | special | revengeWar +100% |
| cul.gift_economy | Hediye Ekonomisi | special | opinion +15, gold -20% |
| cul.bureaucracy | Bürokrasi | special | loyalty +15, buildSpeed -10% |
| cul.tattoo_marks | Dövme Gelenekleri | special | fear +10, cosmetic:tattoo |
| cul.tower_architecture | Kule Mimarisi | special | towerRange +2, cosmetic:spires |
| cul.corpse_burners | Ölü Yakıcılar | special | burnCorpses +1, zombieRise -90% |

## Din Trait'leri (40)

| id | name | group | effects |
|---|---|---|---|
| rel.green_mother | Yeşil Ana | deity | spell:grow, farmYield +20% |
| rel.serpent_coil | Yılan Halkası | deity | onHit:poison:20 |
| rel.drowned_god | Batık Tanrı | deity | flag:swim, boatSafety +50% |
| rel.sun_eye | Güneş Gözü | deity | spell:bless, immune:heat |
| rel.deep_hammer | Derin Çekiç | deity | craftQuality +1, mineYield +20% |
| rel.frost_mother | Ayaz Ana | deity | spell:freeze, immune:cold |
| rel.spore_choir | Spor Korosu | deity | immune:spores, spell:poison_cloud |
| rel.prism | Prizma | deity | spellPower +25%, mana +20 |
| rel.moon_veil | Ay Peçesi | deity | nightBonus +25%, spell:invisibility |
| rel.hollow_king | Oyuk Kral | deity | spell:raise_dead |
| rel.eternal_flame | Sönmez Ateş | deity | spell:fireball, immune:burning |
| rel.star_choir | Yıldız Korosu | deity | spell:meteor_call |
| rel.endless_wheel | Sonsuz Çark | deity | onDeath:rebirth:5% |
| rel.null | Hiçlik | deity | spellResist +50%, flag:no_spells |
| rel.hive_mind | Kovan | deity | loyalty +25, plotChance -50% |
| rel.missionaries | Misyonerler | doctrine | conversion +50% |
| rel.holy_war | Kutsal Savaş | doctrine | religiousWarChance +60% |
| rel.pilgrimage | Hac | doctrine | neuron:pilgrimage, happiness +2 |
| rel.monasticism | Manastır | doctrine | priestXp +50%, fertility -10% |
| rel.offerings | Adak | doctrine | sacrificeFood +1, blessChance +20% |
| rel.fasting | Oruç | doctrine | hungerRate -10%, faith +10% |
| rel.temple_builders | Tapınakçılar | doctrine | templeBuildSpeed +50% |
| rel.healing_hands | Şifa Elleri | doctrine | spell:heal |
| rel.prophecy | Kehanet | doctrine | disasterWarning +1 |
| rel.devotion | Fedakârlık | doctrine | fleeThreshold -50% |
| rel.paradise | Cennet İnancı | doctrine | deathMourning -50% |
| rel.reincarnation | Ruh Göçü | doctrine | xpInheritance +10% |
| rel.ancestor_worship | Ata Kültü | doctrine | clanLoyalty +20% |
| rel.idol_makers | Put Ustaları | doctrine | buildStatues +1, happiness +1 |
| rel.iconoclasm | Put Kırıcılar | doctrine | destroyForeignStatues +1 |
| rel.tolerance | Hoşgörü | doctrine | foreignReligionOpinion +20 |
| rel.inquisition | Engizisyon | doctrine | convertOthersForce +1, happinessOthers -2 |
| rel.divine_kings | Tanrı Krallar | doctrine | kingLoyalty +30 |
| rel.rain_dancers | Yağmur Dansçıları | doctrine | spell:rain_call |
| rel.relic_keepers | Emanet Bekçileri | doctrine | legendaryItemChance +50% |
| rel.charity | Sadaka | doctrine | poorHappiness +2, gold -10% |
| rel.celibate_priests | Evlenmeyen Rahipler | doctrine | priestSpellPower +30% |
| rel.holy_animals | Kutsal Hayvanlar | doctrine | huntYield -80%, animalFriend +1 |
| rel.smiting | Göksel Ceza | doctrine | spell:holy_smite |
| rel.rot_purge | Arınma Ayini | doctrine | spell:cure_rot, dmgVsUndead +30% |

## Dil Trait'leri (25)

| id | name | group | effects |
|---|---|---|---|
| lang.plain_speech | Sade Konuşma | phonology |  |
| lang.soft_vowels | Yumuşak Ünlüler | phonology |  |
| lang.poetic | Şiirsel | phonology |  |
| lang.drumming | Davul Ritmi | phonology |  |
| lang.guttural | Gırtlaksı | phonology |  |
| lang.sand_whisper | Kum Fısıltısı | phonology |  |
| lang.hard_consonants | Sert Ünsüzler | phonology |  |
| lang.chime | Çan Sesi | phonology |  |
| lang.hissing | Tıslamalı | phonology |  |
| lang.backwards | Tersine | phonology |  |
| lang.silent_signs | İşaret Dili | phonology |  |
| lang.bubbling | Fokurtulu | phonology |  |
| lang.buzzing | Vızıltılı | phonology |  |
| lang.complex_grammar | Karmaşık Dilbilgisi | grammar | bookPower +30%, learnTime +50% |
| lang.simple_grammar | Sade Dilbilgisi | grammar | spread +30% |
| lang.eternal_text | Kalıcı Yazı | grammar | bookDecay -100% |
| lang.confusing_semantics | Muğlak Anlam | grammar | bookPower -20%, diplo -1 |
| lang.many_dialects | Çok Lehçeli | grammar | splitChance +100% |
| lang.written_script | Yazı Sistemi | grammar | flag:can_write |
| lang.runic | Runik Yazı | grammar | flag:can_write, enchantChance +10% |
| lang.pictographic | Resim Yazısı | grammar | flag:can_write, bookWrite -30% |
| lang.loanwords | Alıntı Sözcükler | grammar | adoptWords +1 |
| lang.sacred_tongue | Kutsal Dil | grammar | faith +20% |
| lang.trade_tongue | Ticaret Dili | grammar | tradeProfit +20%, spread +20% |
| lang.whistled | Islık Dili | grammar | commRange +100% |

## Klan Trait'leri (28)

| id | name | effects |
|---|---|---|
| clan.iron_blood | Demir Kan | hp +10% |
| clan.wise_line | Bilgeler Soyu | intel +1 |
| clan.cursed_line | Lanetli Soy | luck -10, traitChance:cursed |
| clan.royal_blood | Kraliyet Kanı | leaderChance +50% |
| clan.long_lived_line | Uzun Ömürlüler | lifespan +20% |
| clan.fertile_line | Bereketli Ocak | fertility +25% |
| clan.hunter_line | Avcılar | huntYield +30% |
| clan.sea_line | Denizciler | boatSpeed +20% |
| clan.smith_line | Demirciler | craftQuality +1 |
| clan.mage_line | Büyücüler | mana +30, spellChance +20% |
| clan.healer_line | Şifacılar | traitChance:healer |
| clan.traitor_line | Hainler Soyu | loyalty -20 |
| clan.loyal_line | Sadıklar | loyalty +20 |
| clan.schemer_line | Entrikacılar | plotChance +50% |
| clan.peacemaker_line | Arabulucular | diplo +2 |
| clan.zealot_line | Bağnazlar | faith +50% |
| clan.beauty_line | Güzeller | traitChance:attractive |
| clan.giant_line | İriler | traitChance:giant |
| clan.swift_line | Çevikler | speed +10% |
| clan.lucky_line | Talihliler | luck +10 |
| clan.unlucky_line | Kara Bahtlılar | luck -10 |
| clan.feud_line | Kan Davalılar | revengeChance +100% |
| clan.founder_line | Kurucu Soy | newCityLeaderChance +50% |
| clan.nomad_line | Göçerler | migrateChance +50% |
| clan.scholar_line | Kalemşorlar | bookWrite +40% |
| clan.beast_line | Canavar Kanı | dmg +10%, diplo -1 |
| clan.shadow_line | Gölgeler | stealth +1, plotSpeed +30% |
| clan.blessed_line | Kutlular | traitChance:blessed |

## Krallık Trait'leri (8)

| id | name | effects |
|---|---|---|
| kt.seafaring | Denizci Krallık | colonizeChance +50% |
| kt.militarist | Militarist | armySize +30% |
| kt.mercantile | Tüccar Krallık | gold +30% |
| kt.theocracy | Teokrasi | faith +30%, templeBonus +1 |
| kt.isolationist | Yalnızcı | opinion -15, rebellionResist +20% |
| kt.conquerors | Fetihçi | warChance +40% |
| kt.scholarly | Bilgin Krallık | bookWrite +30% |
| kt.fortress | Kale Krallığı | wallHp +50% |

## Plotlar (16)

| id | name | initiator | conditions | minParticipants | durationMonths | outcome |
|---|---|---|---|---|---|---|
| plot.rebellion | İsyan | city_leader | loyalty<30, ambitious_or_inspired | 3 | 24 | Şehir bağımsız krallık olur |
| plot.new_religion | Yeni Din Kurma | any | intel>=6, abstract_thought, zealous_or_inspired | 2 | 36 | Yeni din; kurucu peygamber olur |
| plot.culture_split | Kültür Ayrılığı | leader | intel>=6, 2 civil>=2, ambitious_or_inspired | 3 | 36 | Kültür ikiye bölünür |
| plot.language_split | Lehçe Ayrılığı | any | dil 3+ şehre yayılmış, uzak şehir | 2 | 48 | Yeni dil doğar |
| plot.alliance | İttifak Kurma | king | diplo>=6, ortak düşman | 1 | 18 | İttifak meta nesnesi |
| plot.war_declaration | Savaş Kışkırtma | king_or_general | warfare>=5, opinion<-20 | 2 | 12 | Savaş ilanı |
| plot.assassination | Suikast | any | deceitful_or_cruel, hedef lider | 2 | 12 | Hedef ölür; yakalanırsa infaz |
| plot.usurpation | Taht Gaspı | noble | ambitious, warfare>=6 | 4 | 24 | Kral devrilir |
| plot.conversion | Din Değiştirme | priest | kendi şehri | 1 | 12 | Şehir halkı plotçunun dinine döner (sadece kendi şehri) |
| plot.new_clan | Klan Kurma | any | ambitious, çocuk>=3 | 1 | 12 | Yeni klan |
| plot.secession | Bağımsızlık | city_leader | farklı kültür/din, loyalty<40 | 3 | 24 | Birden fazla şehir yeni krallık |
| plot.royal_marriage | Kraliyet Evliliği | king | opinion>10 | 1 | 6 | Fikir +30, ittifak şansı |
| plot.reform | Reform | king | intel>=7 | 2 | 36 | Kültür trait'lerinden biri değişir |
| plot.crusade | Kutsal Sefer | high_priest | holy_war doktrini | 3 | 18 | Farklı dinli krallığa din savaşı |
| plot.peace_treaty | Barış Antlaşması | king | savaş>=5 yıl, diplo>=5 | 1 | 6 | Savaş biter |
| plot.dark_ritual | Karanlık Ritüel | any | cultist_or_cursed, mage_blood | 3 | 12 | İblis çağırma veya ölü diriltme felaketi |

## Savaş Türleri (7)

| id | name |
|---|---|
| war.conquest | Fetih Savaşı |
| war.rebellion | İsyan Savaşı |
| war.religious | Din Savaşı |
| war.independence | Bağımsızlık Savaşı |
| war.revenge | İntikam Savaşı |
| war.succession | Veraset Savaşı |
| war.world_war | Herkese Karşı |

## Kitap Türleri (10)

| id | name | readerBonus |
|---|---|---|
| book.history | Tarih | intel:+1 |
| book.poetry | Şiir | happiness:+2 |
| book.science | İlim | intel:+2 |
| book.warfare | Savaş Sanatı | warfare:+1 |
| book.scripture | Kutsal Metin | faith:+20% |
| book.law | Kanunname | steward:+1 |
| book.medicine | Tıp | diseaseResist:+10% |
| book.craft | Zanaat | 5 |
| book.travel | Seyahatname | speed:+3% |
| book.forbidden | Yasak Bilgi | mana:+20, happiness:-2 |

## Mutluluk Olayları (30)

| id | name | value |
|---|---|---|
| hap.new_home | Yeni ev | 4 |
| hap.festival | Şenlik | 5 |
| hap.married | Evlilik | 6 |
| hap.child_born | Çocuk doğdu | 5 |
| hap.ate_well | İyi yemek | 1 |
| hap.hungry | Açlık | -3 |
| hap.homeless | Evsizlik | -4 |
| hap.family_died | Aile üyesi öldü | -8 |
| hap.friend_died | Arkadaşı öldü | -4 |
| hap.war_won | Savaş zaferi | 4 |
| hap.war_lost | Savaş yenilgisi | -5 |
| hap.city_captured | Şehri ele geçirildi | -10 |
| hap.king_died | Kral öldü | -3 |
| hap.plague_nearby | Yakında salgın | -3 |
| hap.read_book | Kitap okudu | 2 |
| hap.prayed | Dua etti | 1 |
| hap.new_king_liked | Sevilen kral | 3 |
| hap.heavy_tax | Ağır vergi | -2 |
| hap.disaster | Afet yaşadı | -6 |
| hap.blessed_by_god | Tanrı kutsadı | 6 |
| hap.cursed_by_god | Tanrı lanetledi | -6 |
| hap.foreign_rule | Yabancı yönetim | -3 |
| hap.same_faith_king | Aynı dinden kral | 2 |
| hap.promoted | Terfi | 3 |
| hap.gift_received | Hediye aldı | 2 |
| hap.insulted | Hakarete uğradı | -2 |
| hap.won_duel | Düello kazandı | 4 |
| hap.saw_miracle | Mucize gördü | 5 |
| hap.statue_nearby | Heykel yakında | 1 |
| hap.era_change | Çağ değişimi | 0 |


---

# Tanrı Araçları

## Tanrı Güçleri (246)

### Dünya Yaratma (54)

| id | name | type | unlockedBy | description |
|---|---|---|---|---|
| pw.soil | Toprak | brush |  | Suyu kademeli karaya çevirir |
| pw.sand | Kum | brush |  |  |
| pw.hills | Tepe | brush |  |  |
| pw.mountain | Dağ | brush |  |  |
| pw.shallow | Sığ Su | brush |  |  |
| pw.water | Su | brush |  |  |
| pw.deep_water | Derin Su | brush |  |  |
| pw.raise | Yükselt | brush |  | Bir kademe |
| pw.lower | Alçalt | brush |  | Bir kademe |
| pw.sponge | Sünger | brush |  | Suyu kurutur |
| pw.road | Yol | brush |  |  |
| pw.field | Tarla | brush |  |  |
| pw.eraser | Silgi | brush |  | Bina, bitki, biyom siler |
| pw.tree | Ağaç Ek | drop |  | Biyomun ağacını eker |
| pw.bush | Çalı Ek | drop |  |  |
| pw.flowers | Çiçek Ek | drop |  |  |
| pw.mushrooms | Mantar Ek | drop |  |  |
| pw.wheat | Buğday Ek | drop |  | Sadece tarlada |
| pw.rock | Kaya | drop |  |  |
| pw.ore_copper | Bakır Damarı | drop |  |  |
| pw.ore_iron | Demir Damarı | drop |  |  |
| pw.ore_silver | Gümüş Damarı | drop |  |  |
| pw.ore_gold | Altın Damarı | drop |  |  |
| pw.ore_skyiron | Gökdemir Damarı | drop | ach.first_mine |  |
| pw.ore_starore | Yıldız Cevheri | drop | ach.first_mine |  |
| pw.seed_grassland | Çayır Tohumu | drop |  | Düştüğü yerde Çayır biyomu başlatır |
| pw.seed_birch | Huşluk Tohumu | drop |  | Düştüğü yerde Huşluk biyomu başlatır |
| pw.seed_maple | Akçaağaç Koruluğu Tohumu | drop |  | Düştüğü yerde Akçaağaç Koruluğu biyomu başlatır |
| pw.seed_forest | Karışık Orman Tohumu | drop |  | Düştüğü yerde Karışık Orman biyomu başlatır |
| pw.seed_jungle | Cengel Tohumu | drop |  | Düştüğü yerde Cengel biyomu başlatır |
| pw.seed_swamp | Bataklık Tohumu | drop |  | Düştüğü yerde Bataklık biyomu başlatır |
| pw.seed_savanna | Savan Tohumu | drop |  | Düştüğü yerde Savan biyomu başlatır |
| pw.seed_desert | Çöl Tohumu | drop |  | Düştüğü yerde Çöl biyomu başlatır |
| pw.seed_rocklands | Kayalık Tohumu | drop |  | Düştüğü yerde Kayalık biyomu başlatır |
| pw.seed_tundra | Tundra Tohumu | drop |  | Düştüğü yerde Tundra biyomu başlatır |
| pw.seed_snowpine | Karlı Çamlık Tohumu | drop |  | Düştüğü yerde Karlı Çamlık biyomu başlatır |
| pw.seed_flower | Çiçek Vadisi Tohumu | drop |  | Düştüğü yerde Çiçek Vadisi biyomu başlatır |
| pw.seed_clover | Yonca Tarlası Tohumu | drop |  | Düştüğü yerde Yonca Tarlası biyomu başlatır |
| pw.seed_mushroom | Mantar Diyarı Tohumu | drop |  | Düştüğü yerde Mantar Diyarı biyomu başlatır |
| pw.seed_crystal | Kristal Vadisi Tohumu | drop |  | Düştüğü yerde Kristal Vadisi biyomu başlatır |
| pw.seed_enchanted | Peri Ormanı Tohumu | drop |  | Düştüğü yerde Peri Ormanı biyomu başlatır |
| pw.seed_corrupted | Kara Çürüme Tohumu | drop |  | Düştüğü yerde Kara Çürüme biyomu başlatır |
| pw.seed_infernal | Kor Diyarı Tohumu | drop |  | Düştüğü yerde Kor Diyarı biyomu başlatır |
| pw.seed_candy | Şekerleme Ülkesi Tohumu | drop |  | Düştüğü yerde Şekerleme Ülkesi biyomu başlatır |
| pw.seed_citrus | Turunç Bahçesi Tohumu | drop |  | Düştüğü yerde Turunç Bahçesi biyomu başlatır |
| pw.seed_garlic | Sarımsaklık Tohumu | drop |  | Düştüğü yerde Sarımsaklık biyomu başlatır |
| pw.seed_celestial | Gök Bahçesi Tohumu | drop |  | Düştüğü yerde Gök Bahçesi biyomu başlatır |
| pw.seed_ash | Kül Çölü Tohumu | drop |  | Düştüğü yerde Kül Çölü biyomu başlatır |
| pw.seed_timewarp | Zaman Kıvrımı Tohumu | drop | ach.biome_master | Düştüğü yerde Zaman Kıvrımı biyomu başlatır |
| pw.seed_void | Sessiz Boşluk Tohumu | drop | ach.biome_master | Düştüğü yerde Sessiz Boşluk biyomu başlatır |
| pw.seed_coral | Mercan Resifi Tohumu | drop | ach.biome_master | Düştüğü yerde Mercan Resifi biyomu başlatır |
| pw.seed_volcanic | Volkanik Ova Tohumu | drop | ach.biome_master | Düştüğü yerde Volkanik Ova biyomu başlatır |
| pw.seed_bone | Kemik Düzlüğü Tohumu | drop | ach.biome_master | Düştüğü yerde Kemik Düzlüğü biyomu başlatır |
| pw.seed_honey | Bal Korusu Tohumu | drop | ach.biome_master | Düştüğü yerde Bal Korusu biyomu başlatır |

### Medeniyetler (19)

| id | name | type | unlockedBy | description |
|---|---|---|---|---|
| pw.spawn_human | İnsan | spawn |  | Dengeli; her biyoma uyum sağlar, hızlı çoğalır |
| pw.spawn_elf | Elf | spawn |  | Uzun ömürlü, iyi okçu, yavaş çoğalır |
| pw.spawn_dwarf | Cüce | spawn |  | Madenci ve demirci; dağlara yerleşir |
| pw.spawn_orc | Ork | spawn |  | Savaşçı; çok hızlı çoğalır, saldırgan |
| pw.friendship | Dostluk | target |  | İki krallığı barıştırır, ittifak şansı |
| pw.spite | Kin | target |  | İki krallığı savaşa sürükler |
| pw.inspire | İlham | target |  |  |
| pw.rebellion_spark | İsyan Kıvılcımı | target |  | Şehir isyan eder |
| pw.crown | Taç | target | ach.kingmaker | Birimi krallığının kralı yapar |
| pw.relocate | Sancak Değiştir | target |  | Birimi başka krallığa geçirir |
| pw.magnet | Kutsal Mıknatıs | target |  | Birimleri tut-taşı-bırak |
| pw.zone_add | Sınır Ekle | brush |  | Şehir sınırına zone ekler |
| pw.zone_remove | Sınır Kaldır | brush |  |  |
| pw.bless_city | Şehri Kutsa | target |  |  |
| pw.curse_city | Şehri Lanetle | target |  |  |
| pw.capital | Başkent Yap | target |  |  |
| pw.found_village | Köy Kur | spawn |  | Seçili türden hazır küçük köy |
| pw.migrants | Göçmen Dalgası | target |  |  |
| pw.festival | Şenlik Başlat | target |  |  |

### Yaratıklar (87)

| id | name | type | unlockedBy | description |
|---|---|---|---|---|
| pw.spawn_wolf | Kurt | spawn |  | Sürü halinde avlanır |
| pw.spawn_bear | Ayı | spawn |  | Bal ve balık sever |
| pw.spawn_cat | Kedi | spawn |  | Fare avcısı |
| pw.spawn_dog | Köpek | spawn |  | Medenilere evcilleşir |
| pw.spawn_rabbit | Tavşan | spawn |  | Çok hızlı çoğalır |
| pw.spawn_sheep | Koyun | spawn |  | Yün verir |
| pw.spawn_cow | İnek | spawn |  | Süt ve et |
| pw.spawn_chicken | Tavuk | spawn |  | Yumurta |
| pw.spawn_frog | Kurbağa | spawn |  | Böcek yer; bataklıkta çoğalır |
| pw.spawn_rat | Sıçan | spawn |  | Veba taşır |
| pw.spawn_monkey | Maymun | spawn |  | Zeki; evrime yatkın |
| pw.spawn_penguin | Penguen | spawn |  | Soğukta yaşar, iyi yüzer |
| pw.spawn_fox | Tilki | spawn |  | Kurnaz avcı |
| pw.spawn_crab | Yengeç | spawn |  | Kabuklu |
| pw.spawn_scorpion | Akrep | spawn |  | Zehirli iğne |
| pw.spawn_lizard | Kertenkele | spawn |  | Sıcağa dayanıklı |
| pw.spawn_crocodile | Timsah | spawn |  | Pusu avcısı |
| pw.spawn_bee | Arı | spawn |  | Kovan kurar; bal üretir |
| pw.spawn_ant | Karınca | spawn |  | Koloni halinde çalışır |
| pw.spawn_beetle | Böcek | spawn |  | Sert kabuk |
| pw.spawn_snake | Yılan | spawn |  |  |
| pw.spawn_turtle | Kaplumbağa | spawn |  | Çok uzun ömür |
| pw.spawn_deer | Geyik | spawn |  |  |
| pw.spawn_boar | Yaban Domuzu | spawn |  |  |
| pw.spawn_goat | Keçi | spawn |  | Dağlara tırmanır |
| pw.spawn_owl | Baykuş | spawn |  |  |
| pw.spawn_crow | Karga | spawn |  | Zeki; ölüleri izler |
| pw.spawn_eagle | Kartal | spawn |  |  |
| pw.spawn_parrot | Papağan | spawn |  | Taklitçi; dil öğrenmeye yatkın |
| pw.spawn_seal | Fok | spawn |  |  |
| pw.spawn_hyena | Sırtlan | spawn |  |  |
| pw.spawn_lion | Aslan | spawn |  |  |
| pw.spawn_rhino | Gergedan | spawn |  | Hücum eder |
| pw.spawn_buffalo | Manda | spawn |  |  |
| pw.spawn_camel | Deve | spawn |  |  |
| pw.spawn_reindeer | Ren Geyiği | spawn |  |  |
| pw.spawn_polar_bear | Kutup Ayısı | spawn |  |  |
| pw.spawn_snail | Salyangoz | spawn |  |  |
| pw.spawn_salamander | Semender | spawn |  | Ateşte yaşar |
| pw.spawn_horse_wild | Yaban Atı | spawn |  | Evcilleşince binek |
| pw.spawn_fish | Balık | spawn |  | Besin kaynağı |
| pw.spawn_butterfly | Kelebek | spawn |  | Tırtıldan dönüşür |
| pw.spawn_piranha | Pirana | spawn |  | Suya düşeni parçalar |
| pw.spawn_mosquito_swarm | Sivrisinek Sürüsü | spawn |  | Hastalık yayar |
| pw.spawn_dragon | Ejderha | spawn | ach.dragon_age | Şehirleri yakar, altın biriktirir |
| pw.spawn_sandworm | Kum Kurdu | spawn |  | Yerin altından çıkıp yutar |
| pw.spawn_kraken | Derin Canavarı | spawn |  | Gemileri batırır |
| pw.spawn_ember_imp | Kor İblisi | spawn |  | Yarıklardan çıkar |
| pw.spawn_rot_crawler | Çürük Sürüngen | spawn |  |  |
| pw.spawn_candy_golem | Şeker Golemi | spawn |  |  |
| pw.spawn_gummy_bear | Jöle Ayı | spawn |  |  |
| pw.spawn_shroomling | Mantarcık | spawn |  | Spor hastalığıyla dönüşenler |
| pw.spawn_crystal_beetle | Kristal Böcek | spawn |  |  |
| pw.spawn_ash_crawler | Kül Sürüngeni | spawn |  |  |
| pw.spawn_bone_crawler | Kemik Örümceği | spawn |  |  |
| pw.spawn_clock_crab | Saat Yengeci | spawn |  | Yaşlanmaz |
| pw.spawn_void_moth | Boşluk Güvesi | spawn |  | Manayı emer |
| pw.spawn_flesh_mound | Et Yığını | spawn |  | Tümör hastalığından doğar |
| pw.spawn_devourer | Yutucu Kütle | spawn | ach.mad_scientist | Değdiği birimi kendine dönüştürür |
| pw.spawn_slime | Balçık | spawn |  | İkiye bölünerek çoğalır |
| pw.spawn_walking_tree | Yürüyen Ağaç | spawn |  | Ormanları korur |
| pw.spawn_fairy | Peri | spawn |  | Çiçekleri büyütür |
| pw.spawn_sky_whale_calf | Gök Balinası Yavrusu | spawn |  | Gökte süzülür |
| pw.spawn_skeleton | İskelet | spawn |  | Ölü diriltmeyle doğar |
| pw.spawn_zombie | Zombi | spawn |  | Isırdığını dönüştürür |
| pw.spawn_zombie_runner | Koşucu Zombi | spawn |  | Taze dönüşmüş; hızlı ama çabuk çürür (ömür 3 yıl) |
| pw.spawn_zombie_brute | İri Zombi | spawn |  | İri birimlerden (ork, ayı, dev) dönüşür; kapıları kırar |
| pw.spawn_zombie_bloater | Şişkin Zombi | spawn |  | Ölünce patlar; çevresine çürük bulutu yayar |
| pw.spawn_zombie_crawler | Sürünen Zombi | spawn |  | Bacaksız; pusuda bekler, yüzebilir |
| pw.spawn_zombie_beast | Zombi Hayvan | spawn |  | Enfekte hayvan: kendi türünün istatistikleri x0.8, hız x0.7, çürük paleti |
| pw.spawn_zombie_dragon | Çürük Ejderha | spawn |  | Ölen ejderha dönüşürse; ateş yerine çürük nefes |
| pw.spawn_zombie_lord | Zombi Efendisi | spawn |  | Büyük sürülerde 500 zombi başına bir tane doğar; sürüyü yönetir |
| pw.spawn_ghost | Hayalet | spawn |  | Duvarlardan geçer |
| pw.spawn_necromancer | Ölü Çağırıcı | spawn |  | Mezarlıklarda güçlenir |
| pw.spawn_bloodsucker | Kan Emici | spawn |  | Sarımsaktan kaçar |
| pw.spawn_fire_elemental | Ateş Elementali | spawn |  | Soğuk çağlarda erir |
| pw.spawn_water_elemental | Su Elementali | spawn |  | Yangın söndürür |
| pw.spawn_earth_golem | Toprak Golemi | spawn |  | Yavaş ama durdurulamaz |
| pw.spawn_snowman | Kardan Adam | spawn |  | Sıcakta erir |
| pw.spawn_storm_spirit | Fırtına Ruhu | spawn |  |  |
| pw.spawn_dark_mage | Kara Büyücü | spawn |  | Köyleri yakar |
| pw.spawn_white_mage | Ak Büyücü | spawn |  | Yaralıları iyileştirir |
| pw.spawn_druid | Druid | spawn |  |  |
| pw.spawn_sky_visitor | Gök Ziyaretçisi | spawn | ach.star_gazer | Gemilerle iner |
| pw.spawn_colossus | Mekanik Dev | spawn | ach.destroyer | Oyuncunun yönetebildiği dev savaş makinesi |
| pw.spawn_bandit | Haydut | spawn |  | Kanunsuz insanlar; köy yağmalar |
| pw.spawn_cultist | Tarikatçı | spawn |  | Karanlık ritüeller |

### Doğa ve Afetler (41)

| id | name | type | unlockedBy | description |
|---|---|---|---|---|
| pw.cloud_rain | Yağmur Bulutu | spawn |  | Yangın söndürür, bitki büyütür, lavı soğutur |
| pw.cloud_snow | Kar Bulutu | spawn |  | Kar örtüsü, suyu dondurur |
| pw.cloud_acid | Asit Bulutu | spawn |  | Tile'ı bir seviye eritir, birimlere hasar |
| pw.cloud_lava | Lav Bulutu | spawn |  | Kızgın lav damlatır |
| pw.cloud_life | Yaşam Bulutu | spawn |  | Biyomun hayvanlarını/uygarlıklarını doğurur |
| pw.cloud_storm | Fırtına Bulutu | spawn |  | Rastgele yıldırım düşürür |
| pw.cloud_ash | Kül Bulutu | spawn |  | Güneşi keser: sıcaklık -5, bitki büyümesi durur |
| pw.cloud_blessing | Kutsal Bulut | spawn |  | Altındakilere 'Kutsanmış' statüsü |
| pw.cloud_plague | Veba Sisi | spawn | ach.plague_lord | Altındakilere veba bulaştırma şansı |
| pw.cloud_rot | Çürük Bulutu | spawn |  | Altındakilere çürük ısırık bulaştırır |
| pw.cloud_candy | Şeker Yağmuru | spawn |  | Şekerleme düşürür, yiyecek verir |
| pw.fire | Ateş | drop |  | Tutuşturur |
| pw.extinguish | Söndür | brush |  |  |
| pw.snow | Kar | drop |  |  |
| pw.ice | Buz | brush |  | Suyu dondurur |
| pw.lava | Lav | drop |  |  |
| pw.cool_lava | Lav Soğut | brush |  |  |
| pw.tornado | Hortum | spawn |  |  |
| pw.earthquake | Deprem | target |  | Çatlak hattı oluşturur |
| pw.tsunami | Dev Dalga | target |  |  |
| pw.volcano | Yanardağ | spawn |  | Yükselir, lav püskürtür |
| pw.geyser | Gayzer | spawn |  | Sıcak su fışkırtır |
| pw.meteor | Göktaşı | drop |  |  |
| pw.lightning | Yıldırım | drop |  |  |
| pw.flood | Sel | target |  |  |
| pw.plague | Veba | target | ach.plague_lord |  |
| pw.rotbite | Çürük Isırık | target |  |  |
| pw.fleshgrowth | Et Büyümesi | target |  |  |
| pw.spores | Spor | target |  |  |
| pw.madness | Cinnet | target |  |  |
| pw.zombie_serum | Arınma Serumu | brush |  | Dönüşmekte olanları kurtarır (zombileri değil) |
| pw.raise_dead | Ölüleri Kaldır | brush | ach.zombie_world | Alandaki cesetler zombi olur |
| pw.divine_light | Kutsal Işık | brush |  | Hastalık, cinnet ve çürüğü temizler |
| pw.monolith | Monolit | spawn | ach.first_civ | Çevredeki hayvanları evrimleştirir |
| pw.golden_brain | Altın Beyin | spawn |  | Zombileri çeker |
| pw.fertile_rain | Bereket | brush |  |  |
| pw.drought | Kuraklık | brush |  |  |
| pw.heat | Isı Dalgası | brush |  |  |
| pw.cold | Ayaz | brush |  |  |
| pw.blessing | Kutsama | target |  |  |
| pw.curse | Lanet | target |  |  |

### Yıkım (16)

| id | name | type | unlockedBy | description |
|---|---|---|---|---|
| pw.bomb | Bomba | drop |  |  |
| pw.dynamite | Dinamit | drop |  | Fitil yanar |
| pw.napalm | Napalm | drop |  |  |
| pw.cluster | Parça Bombası | drop |  |  |
| pw.atomic | Atom Bombası | drop | ach.destroyer |  |
| pw.antimatter | Karşı Madde | drop | ach.world_eater | Her şeyi siler |
| pw.heat_ray | Isı Işını | brush |  |  |
| pw.black_hole | Kara Delik | spawn | ach.world_eater |  |
| pw.fireball | Ateş Topu | drop |  |  |
| pw.goo | Yutan Balçık | drop | ach.mad_scientist | Kanunla sınırlandırılabilir |
| pw.ice_bomb | Buz Bombası | drop |  |  |
| pw.storm | Şimşek Fırtınası | target |  |  |
| pw.acid | Asit Damlası | drop |  |  |
| pw.finger | Dürtme Parmağı | target |  | Birimi fırlatır |
| pw.stomp | Ezme | drop |  |  |
| pw.shockwave | Şok Dalgası | drop |  |  |

### Diğer (29)

| id | name | type | unlockedBy | description |
|---|---|---|---|---|
| pw.life_game | Hayat Oyunu | brush |  | Hücreler tile boyar, birimleri ezer |
| pw.ant | Tur Karıncası | spawn |  |  |
| pw.fireworks | Havai Fişek | drop |  |  |
| pw.confetti | Konfeti | drop |  |  |
| pw.inspect | İncele | target |  |  |
| pw.follow | Takip Et | target |  |  |
| pw.possess | Ruh Girişi | target | ach.first_civ | Birimi doğrudan kontrol et |
| pw.eye | Keşif Gözü | toggle |  |  |
| pw.stats | Dünya İstatistikleri | window |  |  |
| pw.graphs | Grafikler | window |  |  |
| pw.history | Dünya Tarihi | window |  |  |
| pw.laws | Dünya Kanunları | window |  |  |
| pw.era_clock | Çağ Saati | window |  |  |
| pw.ed_unit | Birim Editörü | window |  |  |
| pw.ed_subspecies | Alt Tür Editörü | window |  |  |
| pw.ed_genes | Gen Editörü | window | ach.gene_splicer |  |
| pw.ed_clan | Klan Editörü | window |  |  |
| pw.ed_culture | Kültür Editörü | window |  |  |
| pw.ed_religion | Din Editörü | window |  |  |
| pw.ed_language | Dil Editörü | window |  |  |
| pw.ed_kingdom | Krallık Editörü | window |  |  |
| pw.ed_item | Eşya Editörü | window |  |  |
| pw.meta_control | Meta Kontrol | target | ach.puppet_master | Bir krallığı/dini yönet |
| pw.save | Kaydet | window |  |  |
| pw.load | Yükle | window |  |  |
| pw.achievements | Başarımlar | window |  |  |
| pw.settings | Ayarlar | window |  |  |
| pw.tutorial | Öğretici | window |  |  |
| pw.new_world | Yeni Dünya | window |  |  |

## Dünya Kanunları (49)

| id | name | group | default |
|---|---|---|---|
| law.kingdom_rebellions | Krallık İsyanları | civ | ✔ |
| law.wars | Savaşlar | civ | ✔ |
| law.diplomacy | Diplomasi | civ | ✔ |
| law.royal_marriages | Kraliyet Evlilikleri | civ | ✔ |
| law.migration | Göç | civ | ✔ |
| law.colonization | Denizaşırı Sömürge | civ | ✔ |
| law.plots | Entrikalar | civ | ✔ |
| law.new_religions | Yeni Dinler | civ | ✔ |
| law.culture_splits | Kültür Ayrılıkları | civ | ✔ |
| law.borders_grow | Sınır Büyümesi | civ | ✔ |
| law.item_crafting | Eşya Üretimi | civ | ✔ |
| law.book_writing | Kitap Yazımı | civ | ✔ |
| law.mixed_species_cities | Karma Tür Şehirleri | civ | ✔ |
| law.animal_spawn | Hayvan Doğal Doğuşu | life | ✔ |
| law.monster_spawn | Canavar Doğuşu | life | ✔ |
| law.aging | Yaşlanma | life | ✔ |
| law.hunger | Açlık | life | ✔ |
| law.reproduction | Üreme | life | ✔ |
| law.disease | Hastalık Yayılımı | life | ✔ |
| law.evolution | Doğal Evrim | life | ✔ |
| law.metamorphosis | Dönüşümler | life | ✔ |
| law.population_cap | Nüfus Sınırı | life | – |
| law.undead_rising | Ölülerin Kalkışı | life | ✔ |
| law.zombie_apocalypse | Kıyamet Salgını (her ölü zombi olarak kalkar) | life | – |
| law.zombie_decay | Zombi Çürümesi (zombiler 10 yılda dağılır) | life | ✔ |
| law.zombie_hordes | Zombi Sürüleri (zombiler toplanıp şehirlere yürür) | life | ✔ |
| law.biome_spread | Biyom Yayılımı | nature | ✔ |
| law.tree_growth | Ağaç Büyümesi | nature | ✔ |
| law.fire_spread | Yangın Yayılımı | nature | ✔ |
| law.clouds | Bulutlar | nature | ✔ |
| law.auto_disasters | Doğal Afetler | nature | ✔ |
| law.seasons | Mevsimler | nature | ✔ |
| law.lava_cooling | Lav Soğuması | nature | ✔ |
| law.goo_spread | Balçık Yayılımı | nature | – |
| law.eternal_summer | Sonsuz Yaz | nature | – |
| law.eternal_winter | Sonsuz Kış | nature | – |
| law.mutation | Mutasyon | genetics | ✔ |
| law.mutant_box | Mutant Kutusu (yeni alt türe 1–4 rastgele trait) | genetics | – |
| law.gene_chaos | Gen Kaosu (her doğumda gen karışır) | genetics | – |
| law.life_cloud_civs | Yaşam Bulutundan Uygarlık | genetics | – |
| law.uplift_all | Herkes Bilinçli | genetics | – |
| law.pure_lines | Saf Soylar (mutasyon yok) | genetics | – |
| law.laughing_death | Kahkahalı Ölüm (ölen birim patlar ve konfeti saçar) | fun | – |
| law.tiny_world | Minik Dünya (tüm birimler küçük) | fun | – |
| law.giants | Devler Diyarı | fun | – |
| law.peaceful_world | Barış Dünyası (saldırı yok) | fun | – |
| law.chaos_world | Kaos (herkes herkese düşman) | fun | – |
| law.god_name | Tanrının Adı (metin alanı) | fun | – |
| law.forbidden_codex | Yasak Kodeks — tüm içeriklerin kilidi açılır, bu dünyada başarım kapanır | forbidden | – |

## Başarımlar (52)

| id | name | condition | unlocks |
|---|---|---|---|
| ach.first_life | İlk Nefes | İlk canlıyı yarat |  |
| ach.first_civ | İlk Köy | İlk şehir kurulsun | pw.monolith, pw.possess |
| ach.first_mine | Derinlerde | İlk maden ocağı | pw.ore_skyiron, pw.ore_starore |
| ach.population_1k | Kalabalık | Dünya nüfusu 1.000 |  |
| ach.population_10k | Mahşer | Dünya nüfusu 10.000 |  |
| ach.kingmaker | Kral Yapıcı | 5 farklı krallık kur/taçlandır | pw.crown |
| ach.empire | İmparatorluk | Tek krallık 25 şehir |  |
| ach.ancient_kingdom | Kadim Taht | Bir krallık 500 yıl yaşasın |  |
| ach.world_war | Dünya Savaşı | Aynı anda 5 savaş |  |
| ach.peace_age | Altın Barış | 100 yıl hiç savaş olmasın |  |
| ach.plague_lord | Vebanın Efendisi | Aynı anda 1.000 hasta | pw.plague, pw.cloud_plague |
| ach.zombie_world | Ölüler Dünyası | 500 zombi |  |
| ach.dragon_age | Ejderha Çağı | Doğal bir ejderha uyansın | pw.spawn_dragon |
| ach.destroyer | Yok Edici | Tek güçle 100 bina yık | pw.atomic, pw.spawn_colossus |
| ach.world_eater | Dünya Yiyen | Karanın %90'ını yok et | pw.antimatter, pw.black_hole |
| ach.mad_scientist | Çılgın Bilgin | Alt tür editörüyle 10 değişiklik | pw.goo, pw.spawn_devourer |
| ach.gene_splicer | Gen Terzisi | 5 gen sinerjisi keşfet | pw.ed_genes |
| ach.biome_master | Biyom Ustası | Tüm normal biyomlar aynı dünyada | özel biyom tohumları |
| ach.star_gazer | Göğe Bakan | Yıldızbilimciler kültürü 3 krallıkta | pw.spawn_sky_visitor |
| ach.puppet_master | Kukla Ustası | Bir krallığı 50 yıl meta kontrolle yönet | pw.meta_control |
| ach.hero_born | Destan | Bir birim 'Destan Kahramanı' olsun |  |
| ach.slayer | Canavar Avcısı | Tek birim 100 öldürme |  |
| ach.old_one | Yaşlı Kurt | Bir birim 500 yaşına ulaşsın |  |
| ach.prophet | Peygamber | Plotla yeni din kurulsun |  |
| ach.babel | Babil | Aynı anda 10 dil |  |
| ach.librarian | Kütüphaneci | Dünyada 500 kitap |  |
| ach.master_smith | Usta Demirci | Efsanevi bir eşya üretilsin |  |
| ach.sailor | Yedi Deniz | Başka kıtaya sömürge |  |
| ach.evolution | Evrim | Bir hayvan türü medenileşsin |  |
| ach.uplift_all | Hayvan Çiftliği | 5 farklı hayvan uygarlığı |  |
| ach.frozen_world | Buz Küre | Suyun %80'i donsun |  |
| ach.fire_world | Kor Küre | Aynı anda 5.000 tile yansın |  |
| ach.possessed | Beden Değiştiren | Possession ile 50 öldürme |  |
| ach.rebel_king | Asi Kral | İsyanla kurulan krallık başkentini alsın |  |
| ach.assassin | Gölgedeki El | Suikastle kral ölsün |  |
| ach.dynasty | Hanedan | Aynı klandan 10 kral |  |
| ach.mixed_city | Harman Şehri | Tek şehirde 4 farklı tür |  |
| ach.undead_king | Ölü Kral | Bir ölümsüz kral olsun |  |
| ach.fairy_tale | Masal | Peri + ejderha + yürüyen ağaç aynı dünyada |  |
| ach.all_eras | Çağlar Boyu | 10 çağın hepsi doğal yaşansın |  |
| ach.apocalypse | Kıyamet | Tek yılda 3 farklı afet |  |
| ach.genesis | Yaratılış | Boş okyanustan 1.000 nüfuslu dünya |  |
| ach.ice_to_fire | Buzdan Ateşe | Buz devrinden hemen sonra kavurucu güneş |  |
| ach.pacifist | Barış Güvercini | Barış Yemini kültürü 200 yıl |  |
| ach.heretic | Sapkın | Din bölünmesi 5 kez |  |
| ach.collector | Koleksiyoncu | 100 trait keşfet |  |
| ach.completionist | Her Şeyi Bilen | Tüm trait'leri keşfet |  |
| ach.god_named | Adım Anılsın | Tanrı heykeli 10 şehirde |  |
| ach.colossus_war | Dev Savaşı | Mekanik Dev 1.000 birim yensin |  |
| ach.last_city | Son Kale | Dünyada zombiler 10:1 çoğunluktayken bir şehir 20 yıl dayansın |  |
| ach.cure_found | Tedavi | 1.000 birim dönüşümden kurtarılsın |  |
| ach.cellular | Hücresel | Hayat Oyunu 1 şehri yok etsin |  |
