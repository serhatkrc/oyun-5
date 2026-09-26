# PixelGenesis — Teknik Tasarım ve Uygulama Planı (Genel İndeks)

> Tür: Piksel tabanlı sandbox tanrı simülatörü (WorldBox türü, özgün kimlikle).
> Motor: **Unity 6.3 LTS + C#** (Burst/Jobs/Collections). Tüm bölümler bu varsayıma göre yazıldı.
> Belge dili Türkçe, kod ve tanımlayıcılar İngilizce.

## Belgeler

İlk okunacak belge: `README_LLM.md` (okuma sırası, faz sırası, çelişki kuralları, bitti tanımı).

## Bölümler

| # | Bölüm | İçerik | Bağımlılık |
|---|-------|--------|------------|
| 1 | **Mimari, Çekirdek ve Dünya** | Proje yapısı, paketler, tick döngüsü, PRNG, ID sistemi, olay veriyolu, içerik veritabanı, tile/zone/chunk modeli, region/ada bağlantısı, harita üretimi, harita render'ı, kamera, terraform fırçaları, kayıt temeli | — |
| 2 | Doğa, İklim ve Zaman | Biyomlar ve yayılım, bitki/ağaç sistemi, sıcaklık, bulutlar/yağış, yangın, kar/buz/lav, takvim, çağlar ve çağ saati | 1 |
| 3 | Birim Çekirdeği | SoA birim deposu, hareket, hiyerarşik yol bulma, uzamsal hash, yaşam döngüsü, ihtiyaçlar, statü efektleri, savaş ve mermiler, nöron tabanlı yapay zekâ, görev/eylem sistemi, birim render'ı | 1, 2 |
| 4 | Biyoloji | Tür, alt tür, trait'ler, genler ve kromozom, fenotip/palet değiştirme, kalıtım ve mutasyon, evrim ve monolit, hastalıklar, metamorfoz | 3 |
| 5 | Medeniyet | Şehir kuruluşu, zone sahipliği, binalar ve inşaat, meslek dağıtımı, kaynak ve depo, tarım, ekipman üretimi, ticaret, gemiler, mutluluk | 3, 4 |
| 6 | Meta Nesneler ve Siyaset | MetaObject tabanı, krallık, lider seçimi, sadakat ve fikir, savaş, ordu, ittifak, kültür, dil ve isim üretici, din, klan, aile, plot, kitaplar | 5 |
| 7 | Tanrı Güçleri ve Afetler | Güç çerçevesi, fırça/düşürme/hedefli güçler, sözde-3D fizik, patlama sistemi, yıkım güçleri, otomatik afetler, hücresel otomatlar | 1–6 |
| 8 | Arayüz ve Oyuncu Araçları | HUD, pencere sistemi, inceleme pencereleri, harita overlay'leri, grafikler, tarihçe, editörler, keşif ve başarımlar, dünya kanunları, possession | 1–7 |
| 9 | Ses, Kayıt, Modding, Performans, Yayın | Ses sistemi, tam kayıt formatı ve migrasyon, mod yükleyici, profil çıkarma ve optimizasyon, test altyapısı, mobil port, yayın süreci | Tümü |


| Ek | Belge | İçerik |
|----|-------|--------|
| A | `EkA_Icerik_Katalogu.md` + `Data/` | Tüm oyun içeriği (JSON + okunabilir katalog) |
| B | `EkB_Gelistirme_Araclari_ve_Is_Akisi.md` | Araçlar, kurulum, git, CI, sanat hattı, testler |
| C | `EkC_Gorsel_Uretim_Rehberi.md` + `Tools/placeholder_art.py` | Görsel kurallar, boyutlar, üretim yolları, geçici sanat |
| – | `CLAUDE.md`, `DECISIONS.md` | Asistan kuralları, karar kaydı |

## Genel kurallar (tüm bölümler için geçerli)

1. **Simülasyon ≠ görsel.** Simülasyon verisi `NativeArray`/düz dizilerde tutulur ve MonoBehaviour'a bağlı değildir. Render bu veriyi okur.
2. **Veri odaklı içerik.** Türler, tile'lar, biyomlar, trait'ler, binalar ve güçler `StreamingAssets/Data/*.json` dosyalarında durur. Kodda yalnızca davranış bulunur.
3. **Determinizm.** Simülasyondaki tüm rastgelelik `SimRandom` akışlarından gelir. `UnityEngine.Random` ve `System.Random` simülasyonda yasaktır.
4. **Tick bütçesi.** Ağır sistemler zaman dilimli çalışır: her tick'te işin 1/N'i yapılır.
5. **Dirty flag'ler.** Değişen veri işaretlenir; render, region ve istatistik yalnızca işaretlenen kısmı yeniden hesaplar.
6. **Kimlikler.** Tüm varlıklara `EntityId(index, generation)` ile erişilir. Doğrudan nesne referansı saklanmaz.
