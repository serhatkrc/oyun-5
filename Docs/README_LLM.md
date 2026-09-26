# PixelGenesis — LLM Başlangıç Belgesi (ÖNCE BUNU OKU)

Sen bu projeyi kodlayacak yapay zekâ asistanısın. Bu belge; hangi dosyayı ne zaman okuyacağını, belgeler çeliştiğinde hangisinin geçerli olduğunu, nasıl çalışacağını ve bir işin ne zaman "bitti" sayılacağını tanımlar.

## 1. Proje özeti

PixelGenesis, piksel tabanlı bir sandbox tanrı simülatörüdür (WorldBox türünde, **özgün isim, sanat ve metinlerle**). Oyuncu dünyayı şekillendirir, canlı yaratır; birimler kendiliğinden medeniyet, krallık, kültür, din ve savaş üretir. Motor: **Unity 6.3 LTS, C#, Burst/Jobs/Collections**. Hedef: 5.000+ birim, 60 FPS, deterministik simülasyon.

## 2. Belge haritası

| Dosya | Ne zaman oku | İçerik |
|-------|--------------|--------|
| `README_LLM.md` | Her oturumun başında | Bu belge |
| `CLAUDE.md` | Her oturum (otomatik) | Kısa kurallar |
| `00_Genel_Plan.md` | İlk oturum | Bölüm haritası, genel kurallar |
| `01_Mimari_Cekirdek_Dunya.md` | Her zaman (temel) | Proje yapısı, tick, PRNG, ID, olaylar, ContentDB, dünya, region, worldgen, render, kamera, fırça, kayıt v1 |
| `02_Doga_Iklim_Zaman.md` | Faz 2 | Biyomlar, bitkiler, sıcaklık, bulut, yangın, lav, çağlar, afet kararı |
| `03_Birim_Cekirdegi.md` | Faz 3 | Birim deposu, statlar, yol bulma, AI, savaş, render |
| `04_Biyoloji.md` | Faz 8 (iskeleti Faz 3–4'te) | Alt tür, trait kalıtımı, gen, fenotip, evrim, hastalık, zombi |
| `05_Medeniyet.md` | Faz 4 | Şehir, bina, meslek, ekonomi, eşya, ticaret, gemi |
| `06_Meta_Siyaset.md` | Faz 5 ve 7 | Meta altyapısı, isimler, krallık, savaş, kültür, dil, din, plot, kitap, tarihçe |
| `07_Gucler_Afetler.md` | Faz 6 | Güç çatısı, patlamalar, afetler, otomatlar |
| `08_Arayuz_Oyuncu_Araclari.md` | Faz 10 (temel HUD Faz 1'den itibaren) | UI Toolkit, pencereler, overlay, editörler, keşif, possession |
| `09_Ses_Kayit_Mod_Performans_Yayin.md` | Faz 11–12 | Ses, tam kayıt, modding, performans bütçeleri, yayın |
| `EkA_Icerik_Katalogu.md` | İçerik gerektiğinde | Tüm oyun içeriğinin okunabilir listesi |
| `EkB_Gelistirme_Araclari_ve_Is_Akisi.md` | Kurulum ve iş akışı | Araçlar, git, CI, sanat hattı, test komutları |
| `EkC_Gorsel_Uretim_Rehberi.md` | Render, sprite yükleme ve sanat işlerinde | Sprite boyutları, gri indeks kodlaması, animasyonlar, bina hücre ölçeği, ikonlar, üretim sırası |
| `Tools/placeholder_art.py`, `Art/Placeholder/` | Faz 1'den itibaren | Geçici sprite'lar; gerçek sanat yoksa kod bunlara düşer |
| `Data/*.json` | Kod içerik okurken | Oyun verisi (tek doğru kaynak) |
| `Data/zombie_rules.json`, `gene_rules.json`, `name_sets.json` | İlgili sistemde | Kural verileri |
| `Data/_schemas/*.schema.json` | JSON düzenlerken / yükleyici yazarken | Veri şemaları |
| `Data/_generator/*.py` | İçerik eklerken | İçerik üretici, `validate.py`, `catalog.py` |
| `DECISIONS.md` | Tasarım kararı verirken | Alınmış kararlar; yenilerini sen eklersin |

> `godsim_oyun_gelistirme_promptu.md` ilk taslak prompttur; **geçersizdir**, kullanma. Tüm bilgileri güncel belgelerde bulunur.

## 3. Faz sırası (uygulama)

Bölüm numarası ile faz numarası aynı değildir. Uygulama sırası `00_Genel_Plan.md`'deki bağımlılıklara göre şudur:

| Faz | Kapsam | Belgeler |
|-----|--------|----------|
| 1 | Dünya çekirdeği | 01 |
| 2 | Doğa ve zaman | 02 |
| 3 | Birimler (hayvanlar) + alt tür iskeleti | 03, 04.1–04.3 |
| 4 | Medeniyet temeli | 05.1–05.8 |
| 5 | Krallık ve savaş | 06.1–06.6, 05.9–05.11 |
| 6 | Güçler ve afetler | 07 |
| 7 | Kültür, dil, din, klan, plot, kitap, tarihçe | 06.7–06.13 |
| 8 | Biyoloji derinliği | 04.4–04.9 |
| 9 | Karakter derinliği (trait seti, ekipman, büyüler, beyin paneli) | 03.8–03.9, 05.7 |
| 10 | Tanrı araçları ve UI | 08 |
| 11 | Cila: ses, öğretici, yerelleştirme, performans, modding | 09 |
| 12 | İçerik genişletme ve yayın | EkA, 09.6 |

Her faz sonunda oyun **çalışır ve oynanabilir** olmalıdır. Bir fazın UI'ı gerekiyorsa (ör. Faz 3'te birim penceresi) 08'deki şablonun basit hâli yazılır, Faz 10'da tamamlanır.

## 4. Çelişki çözümü (öncelik sırası)

1. Kullanıcının o oturumdaki açık talimatı
2. `DECISIONS.md`'deki kayıtlı kararlar
3. İlgili bölüm belgesi (01–09) — sayısal değerler, API imzaları, algoritmalar
4. `Data/*.json` — içerik kimlikleri ve değerleri (belge ile JSON id'si çelişirse **JSON geçerlidir**)
5. `00_Genel_Plan.md` genel kuralları
6. `EkB` araç ve iş akışı önerileri

Belgelerde bir boşluk veya çelişki bulursan: en basit, genişletilebilir çözümü seç, `DECISIONS.md`'ye yaz, kullanıcıya özetle.

## 5. Çalışma yöntemi

1. Kullanıcı bir görev verir (genellikle bölüm sonundaki görev tablosundan bir satır).
2. **Önce plan yaz:** dokunacağın dosyalar, yeni sınıflar ve imzalar, testler, riskler. Kullanıcı onaylamadan kod yazma.
3. Kodu yaz. Belgedeki API imzalarına uy; değiştirmen gerekiyorsa gerekçesini yaz ve `DECISIONS.md`'ye ekle.
4. Testleri yaz ve çalıştır (EK B 3.7.2 komutları). Kırmızı test bırakma.
5. Değişiklik özetini ver: ne yapıldı, hangi kabul kriteri karşılandı, bilinen eksikler, performans ölçümü (varsa).

## 6. Değiştirilemez kurallar

- Katman sırası: **Core ← Content ← World ← (WorldGen, Sim) ← Powers ← UI**; Render ve Persistence yalnızca okur; Boot her şeyi bağlar. Üst katmana referans verme.
- Simülasyon verisi SoA / `NativeArray`; simülasyon mantığı MonoBehaviour'da olmaz.
- Rastgelelik yalnızca `SimRandom` akışlarından; `UnityEngine.Random`, `System.Random`, `Time.time` simülasyonda yasak.
- UI simülasyona doğrudan yazmaz; komut kuyruğu kullanır.
- Tick içinde heap ayırma yok (GC 0).
- İçerik kodda sabit değildir; `Data/` JSON'larından okunur. Yeni içerik `_generator` betiklerine eklenir, `validate.py` ve `catalog.py` çalıştırılır.
- Oyuncuya görünen metinler yerelleştirme anahtarıyla; kod ve yorumlar İngilizce.
- **Özgünlük:** Referans oyunun isimleri, sprite'ları, metinleri, sesleri kullanılmaz.

## 7. "Bitti" tanımı (Definition of Done)

Bir görev ancak şunların hepsi doğruysa bitmiştir:
- [ ] Derleme uyarısız (en azından yeni uyarı yok)
- [ ] İlgili bölümün kabul kriterlerinden bu göreve düşenler geçiyor
- [ ] Yeni kod için EditMode testleri var ve yeşil
- [ ] Determinizm testi hâlâ yeşil
- [ ] Tick içinde GC ayırması yok (Profiler kontrolü)
- [ ] Kayıt/yükleme yeni veriyi kapsıyor (ilgiliyse)
- [ ] `DECISIONS.md` ve gerekiyorsa `CHANGELOG.md` güncel

## 8. Sözlük

| Terim | Anlamı |
|-------|--------|
| Tile | Haritanın en küçük hücresi; 1 tile = 1 dünya birimi = 1 sprite pikseli |
| Zone | 8×8 tile; şehir sahipliği ve overlay birimi |
| Chunk | 64×64 tile; render ve kayıt birimi |
| Region | Chunk içinde bağlı yürünebilir (veya su) tile kümesi |
| Ada (Island) | Birbirine bağlı region'lar; yol bulmanın ilk filtresi |
| Tick | Simülasyon adımı; ×1 hızda saniyede 20 |
| SoA | Structure of Arrays; her alan ayrı dizi |
| Meta nesne | Krallık, şehir, kültür, dil, din, klan, aile, ittifak, ordu, alt tür, plot, savaş |
| Nöron | AI karar düğümü (puan üretir, görev seçer) |
| Görev / Eylem | Nöronun seçtiği iş / görevi oluşturan küçük durum makinesi |
| Trait | Birim, alt tür, kültür vb. üzerindeki özellik; etkileri `effects` alanında |
| Fenotip | Alt türün renk/desen görünümü; palet değiştirme ile uygulanır |
| Possession | Oyuncunun bir birimi doğrudan kontrol etmesi |
| Keşif | İçeriğin ilk doğal ortaya çıkışıyla profil koleksiyonuna eklenmesi |
| Yasak Kodeks | Tek dünyada tüm kilitleri açan kanun; başarımları kapatır |
| Çağ saati | Çağların sırasını belirleyen 8 slotlu döngü |
| İlahi işaret | Editörle değiştirilen nesneye eklenen "düzenlendi" trait/bayrağı |
