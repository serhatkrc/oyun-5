# PixelGenesis — proje kuralları

Her oturumda önce `Docs/README_LLM.md` dosyasını oku; belge haritası, faz sırası ve çelişki kuralları oradadır.

- Motor: Unity 6.3 LTS, C#. Mimari ve API'ler: `Docs/01_Mimari_Cekirdek_Dunya.md` ve ilgili bölüm belgesi.
- Katmanlar: Core ← Content ← World ← (WorldGen, Sim) ← Powers ← UI. Render ve Persistence yalnızca okur. Üst katmana referans verme.
- Simülasyon verisi NativeArray/SoA'da; MonoBehaviour'a simülasyon mantığı yazma.
- Rastgelelik yalnızca SimRandom akışlarıyla; UnityEngine.Random / System.Random / Time.time simülasyonda yasak.
- UI simülasyona yazmaz; komut kuyruğu (PowerCommand, EditCommand, LawCommand, MetaCommand) kullanır.
- Tick içinde heap ayırma yok: LINQ, boxing, string birleştirme, lambda yakalama yok.
- İçerik `Data/*.json`'dan okunur; kodda içerik sabiti yok. İçerik eklerken `Data/_generator` betiklerini düzenle, `validate.py` ve `catalog.py` çalıştır.
- Sprite'lar id ile yüklenir; gerçek sanat yoksa `Art/Placeholder` kullanılır. Boyut ve renk kuralları: `Docs/EkC_Gorsel_Uretim_Rehberi.md`.
- Belge ile JSON id'si çelişirse JSON geçerlidir.
- Önce plan yaz, onay al, sonra kodla. Her yeni sistem için EditMode testi yaz ve çalıştır.
- Belgelerde olmayan kararları `DECISIONS.md`'ye yaz.
- Oyuncuya görünen metinler Localization anahtarıyla; kod ve yorumlar İngilizce.
- Referans oyunun isim, sprite, metin ve seslerini kullanma; her şey özgün.
