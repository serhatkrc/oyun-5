# EK C — Görsel Üretim Rehberi (Sanat Kitabı)

Bu belge oyunun tüm görsellerinin **neyle, hangi kurallarla, hangi boyutta ve hangi sırayla** hazırlanacağını tanımlar. Hem çizimi yapacak kişi (sen, tuttuğun bir sanatçı) hem de kodu yazan LLM için tek referanstır. Üretim hattının teknik kısmı (Aseprite dışa aktarma, Unity içe aktarma) EK B 3.6'dadır; bu belge onu tamamlar.

---

## 1. Hangi görseller çizilir, hangileri çizilmez

| Görsel | Nasıl üretilir | Çizim gerekir mi |
|--------|---------------|------------------|
| Harita zemini (tile'lar, biyomlar, su, lav) | Kod: `tiles.json` / `biomes.json` renkleri + varyant gürültüsü + shader (Bölüm 1.11) | **Hayır** |
| Overlay'ler, sınır çizgileri, sıcaklık haritası | Kod (Bölüm 8.7) | Hayır |
| Armalar | Kod birleştirir; parçaları (şekil, ikon) çizilir | Parçalar evet |
| Fenotip renkleri, krallık renkleri, zombi görünümü | Palet değiştirme (shader) | Hayır — tek gri sprite yeter |
| Birimler (92 tür) | Sprite sheet | **Evet** |
| Binalar (40 tip × 6 stil × 3 durum) | Sprite | **Evet** |
| Ağaç, bitki, damar (44) | Sprite | Evet |
| Güç ikonları (246), trait/gen ikonları (542), statü ikonları (31) | İkon | Evet (öncelikli olanlar) |
| Efektler (patlama, ateş, duman, yağmur, kan, büyü) | Küçük sprite + partikül sistemi | Evet |
| Arayüz çerçeveleri, düğmeler, font | 9-dilim sprite + OFL piksel font | Evet (az sayıda) |
| Mağaza/menü illüstrasyonları, logo | Büyük illüstrasyon | Yayın öncesi |

Çizilecek toplam öğe sayısı büyüktür (~1.200 dosya). Bu yüzden **önce geçici sanatla oyun tamamen çalışır hâle getirilir**, gerçek sanat faz faz yerine konur (bölüm 11).

---

## 2. Araçlar

| Araç | Kullanım | Not |
|------|----------|-----|
| **Aseprite** (önerilen) | Tüm sprite, animasyon, ikon | Ücretli; komut satırı dışa aktarma otomasyonu için gerekli |
| Pixelorama / LibreSprite | Ücretsiz alternatif | Dışa aktarma otomasyonu sınırlı; sprite sheet + JSON elle dışa aktarılır |
| `Tools/placeholder_art.py` | Geçici sanat üretimi | Pakette hazır (bölüm 10) |
| Lospec (palet kütüphanesi) | Hazır palet ilhamı | Kendi paletini `.gpl` olarak kaydet |
| Krita / Photopea | Mağaza illüstrasyonları, logo | Piksel sanat için değil |
| Unity Sprite Editor + Sprite Atlas | İçe aktarma, atlas | EK B 3.6.4 |

### Aseprite kurulumu (bir kez)
1. **Palet:** `Art/Palettes/master.gpl` (bölüm 4.3) yükle; *Preferences → Color → Default palette* olarak ayarla.
2. **Birim/bina için ikinci palet:** `Art/Palettes/index8.gpl` — 8 gri tonu (bölüm 4.1). Birim ve bina dosyaları **yalnızca bu paletle** çizilir.
3. **Izgara:** *View → Grid Settings* 1×1 (tile ızgarası); binalar için 3×3 (hücre).
4. **Şablon dosyalar:** `Art/Source/_templates/unit_biped_s2.aseprite` gibi; doğru boyut, katmanlar ve etiketler hazır. Yeni birim bu şablondan kopyalanır.

---

## 3. Sanat yönü

- **Bakış açısı:** Yukarıdan bakan harita, üzerinde **önden-yandan (3/4) görünen** figürler. Birimler sağa bakar; sola gidişte kod yatay çevirir (ayrı sola bakan kare çizilmez).
- **Işık:** Sol üstten. Her şeklin sol-üst kenarı bir ton açık, sağ-alt kenarı bir ton koyu.
- **Kontur:** Birimlerde **siyah kontur yok** (çok küçükler, kontur okunurluğu bozar); en koyu ton (indeks 1) yalnızca ayrım gereken yerde. Binalarda ve büyük canavarlarda (boy ≥ 4) 1 piksel koyu kontur.
- **Okunurluk kuralı:** Her birim, 1× ölçekte (1 piksel = 1 ekran pikseli, en uzak zoom LOD 1) **siluetinden tanınmalı**. Detay değil siluet önce gelir: kulak, boynuz, kuyruk, kanat gibi tek bir ayırt edici çıkıntı.
- **Renk dili:** Canlılar sıcak ve doygun, zemin daha soluk. Krallık rengi yalnızca kıyafet/bayrak piksellerinde (indeks 5–6). Tehlikeli şeyler (lav, ateş, büyü) en parlak renkler.
- **Anti-aliasing yok, bulanıklık yok, gradyan yok.** Titreşim (dithering) yalnızca 16 pikselden büyük yüzeylerde.
- **Özgünlük:** Referans oyunun sprite'larına bakarak kopyalama yapılmaz. Tür karakterleri (insan, ork vb.) genel fantastik arketiplerdir; oranlar, kafa-gövde oranı, kıyafet biçimi ve bina mimarisi kendi tasarımımızdır. Çizim öncesi her grup için 3–4 kavram eskizi yapılıp stil sabitlenir.

---

## 4. Renk ve palet sistemi

### 4.1 İndeksli gri kodlama (birimler ve binalar)
Birim ve bina sprite'ları gerçek renkle değil, **8 gri tonla** çizilir. Oyunda shader her tonu paletten gelen gerçek renge çevirir (Bölüm 3.10, 4.5).

| İndeks | Gri değeri (R=G=B) | Anlamı | Rengi nereden gelir |
|--------|--------------------|--------|---------------------|
| 0 | alfa = 0 | Boş | — |
| 1 | 28 | Kontur / en koyu | Fenotip paleti |
| 2 | 56 | Gövde gölge | Fenotip paleti |
| 3 | 84 | Gövde ana renk | Fenotip paleti |
| 4 | 112 | Gövde açık / ten | Fenotip paleti |
| 5 | 140 | Kıyafet gölge | **Krallık rengi** (koyu) |
| 6 | 168 | Kıyafet / bayrak / çatı | **Krallık rengi** |
| 7 | 196 | Vurgu: göz, boynuz, büyü | Türün vurgu rengi |
| 8 | 224 | Parlama / metal / pencere ışığı | Sabit açık ton |

**Kural:** Bu 8 değerin dışında hiçbir renk kullanılmaz. Aseprite'ta `index8.gpl` paleti seçili çalışılırsa hata yapılmaz. Dışa aktarma betiği (bölüm 9) başka değer bulursa hata verir.

**Binalarda** indeks 1–4 stilin malzeme rengi (ahşap, taş, deri), 5–6 çatı/bayrak (krallık rengi), 7 kapı/pencere çerçevesi, 8 ışık.

**Özel durum paletleri** (kod üretir, çizim gerekmez): zombi (yeşil-gri ten, kırmızı göz), donmuş (açık mavi), yanıyor (turuncu titreşim), hayalet (yarı saydam), ilahi (altın).

### 4.2 Gerçek renkli çizilenler
Ağaç/bitki/damar (feature), ikonlar, efektler ve arayüz **master paletle** gerçek renkli çizilir.

### 4.3 Master palet
64 renklik tek palet (`Art/Palettes/master.gpl`). İçeriği:
- 8 nötr (siyahtan beyaza)
- Her biri 5 tonluk 10 rampa: yeşil (bitki), kahve (ahşap/toprak), gri-mavi (taş), mavi (su), kırmızı (ateş/kan), turuncu (lav), sarı (altın/ışık), mor (büyü), pembe (şeker/çiçek), camgöbeği (kristal)
- 6 özel vurgu

Palet sanatçı tarafından ilk hafta sabitlenir ve sonra değiştirilmez. Zemin renkleri (`tiles.json`, `biomes.json`) de bu paletten seçilecek şekilde güncellenir.

---

## 5. Birimler

### 5.1 Boyutlar (piksel = tile)
Tür boyutu `species.json` → `stats.size` (1–5), gövde planı türden türetilir (`Tools/placeholder_art.py` → `body_plan`).

| Gövde planı | Boy 1 | Boy 2 | Boy 3 | Boy 4 | Boy 5 | Örnek |
|-------------|-------|-------|-------|-------|-------|-------|
| İki ayaklı (biped) | 3×5 | 4×7 | 5×8 | 7×10 | 12×14 | insan, elf, zombi, büyücü |
| Dört ayaklı (quad) | 4×3 | 6×4 | 8×5 | 10×7 | 16×10 | kurt, koyun, ayı |
| Kuş / uçan | 4×3 | 6×4 | 8×5 | 10×6 | 18×10 | kartal, peri, gök balinası |
| Böcek | 3×2 | 4×3 | 5×3 | 7×4 | 9×5 | arı, akrep, yengeç |
| Balık | 3×2 | 5×3 | 7×4 | 10×5 | 16×9 | balık, kraken |
| Yılan | 5×2 | 6×2 | 8×3 | 12×4 | 20×6 | yılan, kum kurdu |
| Kütle (blob) | 3×3 | 4×4 | 6×5 | 8×7 | 12×10 | balçık, et yığını |
| Ejderha | — | — | 12×9 | 16×12 | 20×14 | ejderha, çürük ejderha |

Yaş evreleri ayrı çizilmez: bebek ×0.6, çocuk ×0.8 kodla ölçeklenir. Yaşlılar için tek fark gri saç pikseli; bu da kodla (indeks 4'ün bir pikselini açarak) yapılır.

### 5.2 Animasyonlar

| Etiket | Kare | Kare süresi | Açıklama |
|--------|------|-------------|----------|
| `idle` | 2 | 400 ms | Nefes: gövde 1 piksel iner-kalkar |
| `walk` | 4 | 120 ms | Bacak döngüsü; gövde 1 piksel zıplar |
| `attack` | 3 | 100 ms | Hazırlan – vur – geri çekil; silah pikseli öne |
| `swim` | 2 | 250 ms | Alt yarı gizli (suyun içinde), baş sallanır |
| `sleep` | 1 | — | Yatay duruş |
| `death` | 3 | 150 ms | Devrilme; son kare yerde kalır (ceset) |

Uçanlar için `walk` = kanat çırpma. Yılanlar için `walk` = dalga hareketi. Kütleler için `walk` = basılıp açılma.

### 5.3 Aseprite katmanları (birim)
`body` (indeks 1–4), `clothes` (5–6, yalnızca medeniler), `hair` (4), `accent` (7: göz, boynuz), `item_anchor` (dışa aktarılmayan yardımcı katman: silah tutma noktası, 1 piksel). Ekipman kodla çizilir: silah 1–3 piksellik çizgi, tutma noktasından çıkar.

### 5.4 Dosya adı
`Art/Source/units/<id>.aseprite` → dışa aktarım `Assets/_Project/Art/units/<id>.png` + `.json`. `<id>` = `species.json` id'sinin `sp.` sonrası (ör. `zombie_runner`). Zombi varyantları kendi sprite'ına sahiptir; yalnızca `zombie_beast` kaynak hayvanın sprite'ını zombi paletiyle kullanır.

---

## 6. Binalar

### 6.1 Boyut
`buildings.json` → `size` **hücre** cinsinden, **1 hücre = 3×3 tile/piksel** (DECISIONS #13).

- Sprite genişliği = hücre genişliği × 3.
- Sprite yüksekliği = hücre yüksekliği × 3 + **çatı payı** (hücre yüksekliği × 3 / 2, en az 2).
- Taban (en alt satırlar) kaplama alanıdır; çatı payı arkadaki tile'ların üstüne taşar.
- Kapı her zaman alt kenarın ortasında (yürünebilir kapı tile'ı; Bölüm 5.4).

Örnek: `house` 3x2 → 9×6 kaplama → sprite 9×9.

### 6.2 Stiller
| Stil | Silüet imzası | Malzeme (indeks 1–4) | Krallık rengi (5–6) |
|------|---------------|----------------------|---------------------|
| `style.human` | Sivri üçgen çatı | Kerpiç, ahşap | Kiremit çatı |
| `style.elf` | Yuvarlak, kubbe çatı, ağaç gövdesi | Açık ahşap, yaprak | Yaprak çatı süsü |
| `style.dwarf` | Düz, kalın taş, alçak; kapı kemerli | Kesme taş | Kapı üstü sancak |
| `style.orc` | Kazıklı sivri çatı, eğri | Deri, koyu ahşap, kemik | Çadır örtüsü |
| `style.beast` | Toprak tümsek | Toprak, dal | Tepe bayrağı |
| `style.insect` | Altıgen petek | Balmumu, çamur | Petek kenarı |

### 6.3 Durumlar ve kademeler
Her bina için 3 durum çizilir: `construction` (iskele + yarım gövde), `complete`, `ruin` (yıkık duvar parçaları). Hasar (%50 altı) kodla çatlak pikselleriyle gösterilir. Ev kademeleri (`tent` → `hut` → `house` → `manor`) ve merkez kademeleri (`hall_1..3`) ayrı binalardır, ayrı çizilir.

Toplam: 40 bina × 6 stil = 240 bina (her biri 3 durum). **Öncelik:** önce `style.human` ile tüm binalar, sonra diğer stiller yalnızca en sık görülen 12 bina (ev kademeleri, merkezler, depo, tarla barakası, kule, tapınak, iskele), geri kalanlar insan stilinden palet ile türetilir.

### 6.4 Dosya adı
`Art/Source/buildings/<stil>/<bina_id>.aseprite`; etiketler `construction`, `complete`, `ruin` (tek dosyada 3 kare).

---

## 7. Ağaçlar, bitkiler, damarlar

| Tür | Boyut | Kare | Not |
|-----|-------|------|-----|
| Ağaç | 5×7 (olgun) | 1 + rüzgârda sallanma için 2. kare | Evreler kodla ölçeklenir: fidan ×0.4, genç ×0.7, olgun ×1, yaşlı ×1 + koyu ton |
| Dev ağaçlar (cengel, dev mantar) | 7×9 | 1–2 | |
| Bitki / çalı / çiçek | 3×3 | 1 | Meyveli ve boş hâli (çalı) ayrı kare |
| Ekin | 3×3 | 4 (evreler) | Filiz → yeşil → sarı → hasat |
| Damar / kaya | 3×3 | 1 | Cevher rengi 1–2 piksel parıltı |
| Mercan | 3×3 | 2 | Su altında, yarı saydam |

Ağaçlar biyomun renklerine uyacak şekilde gerçek renkli çizilir; aynı ağaç birden fazla biyomda kullanılıyorsa (meşe) renk kaydırma shader'la yapılır.

---

## 8. İkonlar, arayüz ve efektler

### 8.1 İkon boyutları
| Tür | Boyut | Adet | Çerçeve |
|-----|-------|------|---------|
| Güç ikonu | 16×16 | 246 | Sekme renginde arka plan (Dünya yeşil, Medeniyet turuncu, Yaratık pembe, Doğa mavi, Yıkım kırmızı, Diğer gri) |
| Birim trait'i | 12×12 | 118 | Nadirlik çerçevesi: gri / mavi / mor / altın |
| Alt tür, kültür, din, dil, klan, krallık trait'i, gen | 12×12 | 424 | Kategori renginde kenar |
| Statü (birim üstü) | 7×7 | 31 | Çerçevesiz, 1 piksel kontur |
| Kaynak | 8×8 | 29 | |
| Meslek | 8×8 | 19 | |
| Arma şekilleri | 16×16 | 12 | Gri (renk kodla) |
| Arma ikonları | 10×10 | 64 | Tek renk (kodla boyanır) |

**Kural:** İkon tek bir nesneyi yan ya da önden gösterir; yazı yok. Aynı aileden ikonlar ortak bir biçim dili kullanır (ör. tüm ateş güçleri aynı alev şeklini paylaşır, üzerine küçük fark eklenir).

**Öncelik:** 246 güç ikonunun hepsi erken gerekir; ama 29 biyom tohumu tek bir "tohum" ikonunun biyom rengiyle boyanmış hâlidir, 80+ yaratık spawn ikonu ise **birimin kendi sprite'ının ikon çerçevesine yerleştirilmiş** hâlidir — kodla üretilir. Böylece gerçekten çizilecek güç ikonu sayısı ~110'a iner. Trait ikonlarında da benzer şekilde temel sembol seti (~80 sembol) + kategori rengi yaklaşımı kullanılır.

### 8.2 Arayüz
- **Font:** OFL lisanslı, Türkçe karakter destekli piksel font (EK B 3.6.6). Ana boyut 8 px yükseklik, başlık 12 px.
- **Pencere çerçevesi:** 9-dilim sprite, 24×24 kaynak, 8 piksel köşe. Açık ve koyu tema için iki set.
- **Düğme:** 3 durum (normal, üzerine gelme, basılı) × 9-dilim.
- **Kaydırma çubuğu, sekme, onay kutusu, kaydırıcı, tooltip kutusu:** her biri 9-dilim.
- Tüm UI 1×, 2×, 3× tam sayı ölçeklerde net görünmeli (UI ölçek ayarı).

### 8.3 Efektler
| Efekt | Boyut | Kare |
|-------|-------|------|
| Patlama (küçük/orta/büyük) | 8×8 / 16×16 / 32×32 | 6 |
| Şok halkası | kod (daire çizimi) | — |
| Ateş (tile üstü) | 3×4 | 3 |
| Duman | 4×4 | 4 |
| Kıvılcım, toz, kan damlası, su sıçraması | 1–2 px partikül | 1 |
| Büyü mermileri (ateş topu, şimşek, iyileştirme ışığı, zehir) | 3×3 – 5×5 | 2–4 |
| Yağmur / kar / asit damlası | 1×2 | 1 |
| Bulut | 12×8 – 24×12 lekeler | 3 varyant |
| Işınlanma, kutsama, lanet parıltısı | 5×7 | 4 |
| Monolit nabzı | 16×16 halka | 6 |

---

## 9. Dışa aktarma ve içe aktarma

1. Çizim bitince `python Tools/export_art.py` (EK B 3.6.3) tüm `.aseprite` dosyalarını sheet + JSON olarak dışa aktarır.
2. Betik kontrol eder: birim/bina dosyalarında 8 gri değer dışında renk var mı; boyut bu belgedeki tabloya uyuyor mu; zorunlu etiketler (`idle`, `walk`...) var mı. Hata varsa dosya adıyla raporlar.
3. Unity `AssetPostprocessor` sprite'ları Preset'le içe aktarır (PPU 1, Point, sıkıştırmasız) ve atlaslara ekler: `Atlas_Units`, `Atlas_Buildings`, `Atlas_Features`, `Atlas_Icons`, `Atlas_UI`, `Atlas_FX`.
4. Kod sprite'ları **id ile** bulur (`SpriteLibrary.GetUnit("sp.zombie")`). Gerçek sprite yoksa `Art/Placeholder` altındakine düşer. Böylece gerçek sanat parça parça eklenebilir; hiçbir şey kırılmaz.

---

## 10. Geçici sanat (placeholder)

`Tools/placeholder_art.py` pakette hazırdır ve çalıştırıldığında:
- 92 birim sprite sheet'i (6 animasyon, doğru boyut ve gri indeks kurallarıyla) + Aseprite formatında JSON,
- 240 bina (6 stil × 40 tip, her biri 3 durum),
- 44 feature,
- 246 güç ikonu, 542 trait/gen ikonu, 31 statü ikonu (kısaltma harfli, renk kodlu),
- `asset_list.csv` (tüm varlıkların boyut, tip ve ilk gerektiği faz listesi — sanatçı iş listesi olarak kullanılır),
- `preview_units.png` ve `preview_buildings.png` önizlemeleri üretir.

Geçici sprite'lar bu belgedeki boyut ve indeks kurallarına uyar; bu yüzden gerçek sanat aynı dosya adıyla konunca kodda hiçbir şey değişmez. Yeni içerik eklenince betik yeniden çalıştırılır.

---

## 11. Üretim yolları (çizim bilgin yoksa)

| Yol | Nasıl | Artı | Dikkat |
|-----|-------|------|--------|
| **A. Kendin çiz** | Aseprite + bu belge + şablonlar | Ücretsiz, tam kontrol | Bu ölçekte (1–16 px) piksel sanat öğrenilebilir; önce 10 birimle stil denemesi |
| **B. Piksel sanatçı tut** | Bu belge + `asset_list.csv` + kavram eskizleri iş tanımı olur | Tutarlı, profesyonel | Paket paket ödeme (ör. önce 10 birim + 6 bina deneme işi); tüm hakların devri sözleşmede yazılı olmalı |
| **C. Hazır varlık paketi** | Piksel varlık mağazaları | Hızlı | Lisansın ticari kullanıma ve değiştirmeye izin verdiğini kontrol et; farklı paketler stil uyumsuzluğu yaratır; gri indeks kuralına dönüştürme gerekir |
| **D. Yapay zekâ görsel üreticileri** | Kavram, renk ve kompozisyon fikri için | Hızlı fikir | 3–16 piksellik sprite'larda doğrudan kullanılabilir sonuç nadirdir; çıktı her durumda elle yeniden çizilmeli. Aracın kullanım şartlarını ve mağaza politikalarını (Steam yapay zekâ içerik beyanı) kontrol et |
| **E. Prosedürel** | Kod | Sınırsız varyasyon | Armalar, fenotip desenleri, ağaç varyantları, patlama halkaları için ideal; ana karakterler için değil |

**Önerilen karma:** Geçici sanatla oyunu bitir → A veya B ile birimler ve binalar → E ile armalar/efektler → ikonlar için temel sembol seti (A/B) + kod birleştirme.

---

## 12. Üretim sırası (fazlara göre)

| Faz | Gerekli gerçek sanat | Adet (yaklaşık) |
|-----|----------------------|-----------------|
| 1 | Master palet, fırça imleci, temel UI çerçevesi, font | 10 |
| 2 | Ağaç/bitki/damar, bulut, ateş, duman, yağmur | 60 |
| 3 | 12 temel hayvan (koyun, kurt, ayı, tavşan, geyik, tavuk, inek, balık, kuş, akrep, yılan, kurbağa) | 12 sheet |
| 4 | 4 medeni tür + `style.human` tüm binalar + kaynak/meslek ikonları | 4 sheet + 40 bina + 48 ikon |
| 5 | Kalan 3 stil (elf, cüce, ork) öncelikli binalar, arma parçaları, silah pikselleri | 36 bina + 76 parça |
| 6 | Güç ikonları (~110 çizim), patlama/büyü efektleri | 130 |
| 7–9 | Kalan hayvanlar, canavarlar, ölümsüzler, zombi varyantları, trait ikon sembolleri | 76 sheet + 80 sembol |
| 10 | Tüm UI setleri, portre çerçeveleri, çağ saati görselleri | 40 |
| 11–12 | Menü illüstrasyonu, logo, mağaza görselleri, fragman | — |

---

## 13. Kalite kontrol listesi (her teslimde)

- [ ] Boyut tablodakiyle aynı (dışa aktarma betiği doğrular)
- [ ] Birim/bina yalnızca 8 gri değer kullanıyor
- [ ] 1× ölçekte, LOD 1'de siluet tanınıyor (oyun içinde kontrol)
- [ ] Krallık rengi yalnızca kıyafet/bayrak/çatı piksellerinde
- [ ] Animasyon etiketleri eksiksiz, kare süreleri tablodaki gibi
- [ ] Sağa bakıyor; sol-üst ışık kuralına uyuyor
- [ ] Zemin üstünde (çayır, kum, kar, su) okunaklı
- [ ] Referans oyundan kopya yok; kavram eskiziyle uyumlu
- [ ] Dosya adı id ile eşleşiyor; eski geçici sprite'ın yerine geçtiği oyunda görüldü
