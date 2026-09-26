# BÖLÜM 8 — Arayüz ve Oyuncu Araçları

Bu bölüm bittiğinde oyuncu dünyayı rahatça izleyip yönetebilir: güç çubuğu, inceleme pencereleri, meta pencereleri, harita overlay'leri, isim plakaları, grafikler, tarihçe, editörler, dünya kanunları, çağ saati, keşif ve başarımlar, birim kontrolü (possession), ayarlar ve mobil düzen.

Bağımlılık: Bölüm 1–7. Teknoloji: **Unity UI Toolkit** (UXML + USS). Dünya uzayındaki öğeler (isim plakası, arma) özel mesh ile çizilir, UI Toolkit ile değil.

---

## 8.1 Genel mimari

- **Tek yönlü veri akışı:** UI simülasyonu okur (`IReadOnlyWorldView` arayüzü üzerinden) ve yalnızca **komut** gönderir (`PowerCommand`, `EditCommand`, `LawCommand`). UI hiçbir simülasyon dizisine doğrudan yazmaz.
- **Güncelleme sıklığı:** Açık pencereler 4 kez/sn yenilenir (her frame değil). Değişmeyen alanlar yeniden çizilmez (değer önbelleği).
- **Yerelleştirme:** Tüm metinler `LocalizedString` bağlaması; içerik isimleri `Content` tablosundan (anahtar = içerik id'si).
- **Ölçek:** Ayarlardan UI ölçeği %75–%200; piksel font tam sayı katlarında ölçeklenir.
- **Tema:** Tek USS değişken dosyası (`theme.uss`); açık/koyu tema, renk körü dostu overlay paleti.

```
UI/
  Hud/            TopBar, PowerBar, BrushPanel, SpeedControls, NotificationFeed, Minimap
  Windows/        WindowManager, WindowBase, UnitWindow, MetaWindow<T>, StatsWindow, GraphsWindow, HistoryWindow,
                  LawsWindow, EraClockWindow, AchievementsWindow, SaveLoadWindow, SettingsWindow, WorldGenWindow
  Editors/        TraitEditorWindow<TSet>, GeneEditorWindow, ItemEditorWindow
  World/          Nameplates, BannerRenderer, SelectionOutline, BrushCursor, OverlayRenderer
  Possession/     PossessionController, PossessionHud
  Tooltips/       TooltipService
```

---

## 8.2 HUD

### Üst çubuk
Yıl/ay, çağ ikonu + kalan yıl (tıklanınca çağ saati), dünya nüfusu, krallık sayısı, hız düğmeleri (duraklat, ×1…×20, süper hız), tek adım, ayarlar menüsü.

### Güç çubuğu (alt)
- 6 sekme: Dünya, Medeniyetler, Yaratıklar, Doğa ve Afetler, Yıkım, Diğer. Her sekme yatay kaydırılabilir ikon ızgarası.
- İkon durumları: normal, seçili (çerçeve), kilitli (kilit + tooltip'te açma koşulu), keşfedilmemiş yaratık (siluet).
- **Favoriler şeridi:** Sağ tık / uzun basma ile ikon iğnelenir (en fazla 20). Profil dosyasında saklanır.
- **Arama kutusu:** İsimle güç arama (Ctrl+F).
- Seçili güç için **fırça paneli:** boyut (Bölüm 1.13 `BrushRadii`), şekil (daire/kare); sadece fırça ve düşürme tiplerinde görünür.
- Kısayollar: Q/E sekme değiştir, 1–7 hız, `[` `]` fırça boyutu, Esc iptal.

### Bildirim akışı (sağ kenar)
- `NotificationService` önemli olayları kuyruğa alır: savaş, kral ölümü, şehir kuruluşu/düşüşü, isyan, afet, efsanevi eşya, keşif, başarım, favori birim ölümü.
- Her kart: ikon + kısa metin + zaman; tıklayınca kamera oraya gider ve ilgili pencere açılır. Aynı türden olaylar 3 sn içinde gruplanır ("3 yeni şehir kuruldu"). Ekranda en fazla 6 kart; ayarlardan kategori bazında kapatılabilir.

### Mini harita (isteğe bağlı, sağ alt)
Chunk texture'larının 1/8 örneklemesi + krallık overlay'i; kamera dikdörtgeni; tıklayınca oraya git.

---

## 8.3 Pencere sistemi

```csharp
public abstract class WindowBase : VisualElement {
    public string WindowId; public bool Pinned;
    protected abstract void Refresh(IReadOnlyWorldView world);   // 4 Hz
    public virtual void OnTargetDied() { /* başlığa "ölü" rozeti, veriyi dondur */ }
}
public sealed class WindowManager { public T Open<T>(object target) where T : WindowBase; public void CloseTop(); public void CloseAll(); }
```
- Sürüklenebilir, en fazla 4 eşzamanlı pencere (fazlası en eskiyi kapatır, iğnelenmiş olanlar hariç).
- Aynı hedefin penceresi tekrar açılırsa öne gelir.
- Masaüstünde yüzen pencere; **mobilde tam ekran panel** (geri tuşu kapatır).
- Hedef ölünce pencere kapanmaz: başlıkta "Öldü (yıl, neden)" rozeti, son veriler gösterilir.

---

## 8.4 Birim inceleme penceresi

Sekmeler:
1. **Genel:** Büyütülmüş portre (sprite ×8, palet uygulanmış, ekipman görünür), isim (düzenlenebilir), tür/alt tür, yaş/evre, seviye/XP, can/mana/stamina çubukları, doygunluk/enerji/mutluluk, meslek, şehir/krallık/kültür/dil/din/klan bağlantıları (tıklanabilir), öldürme sayısı.
2. **İstatistik:** Tüm hesaplanmış statlar; üzerine gelince kaynak dökümü (tür +x, trait +y%, ekipman +z).
3. **Trait'ler:** İkon ızgarası, nadirlik çerçevesi; keşfedilmemiş trait "?" ve parıltı animasyonu.
4. **Ekipman:** 6 slot, eşya adı, kalite rengi, eşya geçmişi (yapan, eski sahipleri, öldürmeler).
5. **Aile:** Ebeveynler, eş, çocuklar (küçük portreler), klan ağacı düğmesi.
6. **Günlük:** Mutluluk olayları ve bu birimle ilgili tarihçe girişleri.
7. **Beyin:** Aktif nöronlar ve son düşünme puanları (çubuk grafik); nöron kapatma alt tür düzeyinde uyarısıyla.

Düğmeler: Takip et, Kontrol et (possession), Favori, Editörde aç, Kutsa/Lanetle (hızlı), Öldür.

---

## 8.5 Meta pencereleri

Tek jenerik şablon (`MetaWindow<T>`), meta türüne göre sekme seti:

| Sekme | İçerik | Hangi meta'lar |
|-------|--------|----------------|
| Genel | İsim, arma, renk, kuruluş, kurucu, üye sayısı, alt tür dağılımı (% çubuğu), trait'ler | Hepsi |
| Üyeler | Sıralanabilir liste (isim, yaş, seviye, meslek); sayfalı (50/sayfa) | Hepsi |
| Şehirler | Liste + nüfus + sadakat çubuğu | Krallık, kültür, din, dil |
| İlişkiler | Diğer krallıklara fikir (renkli çubuklar), savaşlar, ittifak | Krallık, ittifak |
| Savaşlar | Taraflar, **süre ve yaş ayrı sütun**, kayıplar, ele geçirilen şehirler | Krallık, savaş |
| İstatistik | Mini grafikler (nüfus, altın, asker) | Hepsi |
| Tarih | Bu meta'nın tarihçe girişleri | Hepsi |
| Kitaplar | Kitap listesi | Dil, kültür, din, şehir |

Haritadan meta seçimi: overlay açıkken bir bölgeye tıklamak o bölgenin meta'sını seçer (8.7).

---

## 8.6 Güç sekmeleri için meta kısayolları
Her meta türü için bir "hızlı bilgi" kartı: seçili meta'nın adı, arması, üye sayısı, favori düğmesi, pencereyi aç düğmesi. Diğer sekmesinde "Meta" alt bölümünde listelenir.

---

## 8.7 Harita overlay'leri

- **Modlar:** Krallık, Şehir, İttifak, Kültür, Dil, Din, Alt tür, Klan, Aile, Sıcaklık, Biyom, Yükseklik, Kaynak, Nüfus yoğunluğu, Hastalık. Birden fazla meta modu aynı anda açılabilir (çoklu geçiş); açık olanların renkleri dönüşümlü gösterilir (1 sn aralıkla) veya "sınırları çiz" moduyla üst üste.
- **Render:** Zone çözünürlüklü RGBA dokusu (`ZonesX × ZonesY`), `Dirty.Overlay` olan zone'lar güncellenir. Shader: dolgu %35 opaklık + meta sınırlarında 1 piksel kalın çizgi (komşu zone farklı meta ise).
- **Birim bazlı meta'lar** (kültür, din, alt tür...) için zone rengi = zone içindeki birimlerin çoğunluk meta'sı (aylık hesaplanır); birim yoksa sahip şehrin çoğunluğu.
- LOD 3'e geçince krallık overlay'i otomatik açılır (ayarlanabilir).

---

## 8.8 İsim plakaları ve armalar

- **Krallık/şehir plakaları:** LOD 1–3'te şehir merkezinin üstünde; arma + isim + nüfus. LOD 2+ yalnızca krallık adı (başkent üstünde, büyük). Çakışan plakalar öncelik sırasıyla gizlenir (başkent > büyük şehir > küçük şehir).
- **Birim plakaları:** LOD 0'da yalnızca kral, lider, favori ve seçili birim.
- Çizim: dünya uzayında, bitmap font atlasından tek mesh (her frame yeniden kurmak yerine plaka içeriği değişince). Görünür en fazla 300 plaka.
- İsim değiştirilince plaka 3 sn parlar.

---

## 8.9 Keşif sistemi ve başarımlar

### Profil verisi (dünyadan bağımsız)
`profile.json` (kalıcı klasörde): keşfedilmiş trait/gen/yaratık/eşya/plot id'leri, kazanılmış başarımlar, istatistik sayaçları (toplam patlatılan bomba vb.), favori güçler, ayarlar.

### Keşif
- `TraitDiscoveredEvent`, `SpeciesDiscoveredEvent` (ilk spawn/doğal oluşum), `GeneDiscoveredEvent`, `ItemQualityDiscoveredEvent`, `PlotDiscoveredEvent` → profile eklenir, bildirim "Yeni keşif: Ateş Soluğu".
- Editörlerde yalnızca keşfedilenler seçilebilir; keşfedilmemişler siluet/"?" olarak listelenir.
- **Keşif gözü** (`pw.eye`): açıkken haritada keşfedilmemiş trait taşıyan birimlerin üstünde parlayan "?" ikonu.

### Başarımlar
- `achievements.json` koşulları `AchievementConditionRegistry` ile koda eşlenir (sayaç tabanlı: "aynı anda 1.000 hasta", olay tabanlı: "bir krallık 500 yıl").
- Kontrol: olay tabanlılar olay anında, sayaç tabanlılar yılda bir.
- Kazanılınca bildirim + `unlocks` listesindeki güçlerin kilidi açılır. Steam build'inde Steam başarımıyla eşlenir (Bölüm 9).
- `law.forbidden_codex` açık dünyalarda keşif ve başarım kapalıdır (üst çubukta mor kitap ikonu uyarısı).

---

## 8.10 Editörler

### Jenerik trait editörü
`TraitEditorWindow<TSet>`; birim, alt tür, klan, kültür, din, dil, krallık için aynı şablon:
- Sol: mevcut trait'ler (kaldırmak için tıkla). Sağ: kategori sekmeli tüm trait'ler (arama + nadirlik filtresi); tıklayınca eklenir. Zıt trait eklenince eskisi kalkar (animasyonlu).
- Değişiklik `EditCommand` olarak simülasyona gider; hedefe o editörün **ilahi müdahale işareti** eklenir: birim `tr.divine_touch`, alt tür `sst.divine_breeding`, kültür/din/dil/klan için ilgili "düzenlendi" bayrağı (`MetaFlags.Edited`) ve pencere başlığında rozet.
- "Toplu uygula": alt tür editöründe yapılan değişiklik tüm üyelere; birim editöründe "tüm alt türe kopyala".
- Kayıtlı trait setleri (şablon): kaydet/yükle (profil dosyasında).

### Gen editörü
Kromozom ızgarası (satır = kromozom, sütun = slot). Gen paleti soldan sürükle-bırak; aktif sinerjiler kromozom üstünde vurgulanır ve bonus tooltip'te. Toplam stat değişimi canlı önizleme.

### Eşya editörü
Seçili birimin ekipman slotuna tip + malzeme + kalite seçip eşya oluşturur; isim verebilir.

---

## 8.11 Dünya kanunları, çağ saati, istatistik, grafik, tarihçe

- **Kanunlar penceresi:** `world_laws.json` gruplarına göre sekmeler (Medeniyet, Yaşam, Doğa, Genetik, Eğlence, Yasak). Anahtar/kaydırıcı; değişiklik `LawCommand`. "Tanrının adı" metin alanı (heykel plakasında görünür). Yasak kodeks sekmesi kilitliyken açma koşulunun ipucunu gösterir.
- **Çağ saati:** 8 slotlu daire (UI Toolkit `Painter2D` ile çizilir); slotlara çağ kartı sürükle-bırak; mevcut çağda ibre; "şimdi değiştir", "sabitle" düğmeleri; çağ açma/kapama listesi.
- **Dünya istatistikleri:** Toplam nüfus, tür sayıları, doğum/ölüm (yıllık), en büyük krallık, en uzun yaşayan birim, en çok öldüren, en eski krallık, toplam savaş, yok olan krallıklar.
- **Grafikler:** `Painter2D` çizgi grafik. Seri seçici: dünya serileri veya seçilen meta'lar (en fazla 8 karşılaştırma). Zaman aralığı: 10/50/100/1000 yıl. Seçimler meta yaşıyorsa korunur. PNG olarak kaydet.
- **Tarihçe:** Kronolojik, filtrelenebilir (tür, meta, yıl aralığı), metin arama. Her giriş tıklanabilir bağlantılar içerir.

---

## 8.12 Birim kontrolü (Possession)

- `pw.possess` ile birime tıklanır → kamera birime kilitlenir, zoom LOD 0'a iner, HUD değişir: can/stamina/mana çubukları, büyü düğmeleri, eşya ikonları.
- **Kontroller (masaüstü):** WASD hareket, sol tık saldırı (imleç yönüne; menzilli silahla hedefe mermi), 1–4 büyü, E konuş (önündeki birimle; kültür dönüştürme %50 — Bölüm 6.7), Space atılma (stamina 20), F binaya gir/çık, Esc bırak.
- **Mobil:** Sanal joystick + saldırı/büyü/konuşma düğmeleri.
- Birim AI'si kapalıdır (`UnitFlags.PlayerControlled`); simülasyon hızı ×1'e sabitlenir (ayarlanabilir). Gemideyken okla ateş edilebilir.
- Birim ölürse kontrol biter, "Öldün" kartı + birim penceresi açılır.
- İstatistik: possession ile yapılan öldürmeler (`ach.possessed`).

---

## 8.13 Meta kontrol

`pw.meta_control` ile bir krallık/din/kültür seçilir. Yan panel:
- **Krallık:** savaş ilan et (hedef seç), barış teklif et, ittifak kur/ayrıl, başkent değiştir, veraset adayı seç, ordu hedef şehri seç.
- **Din/kültür:** öncelikli yayılım hedef şehri.
Komutlar `MetaCommand` olarak gider; kontrol bırakılana kadar AI yalnızca boş kararları verir (Bölüm 6.14).

---

## 8.14 Diğer ekranlar

- **Ana menü:** Devam et (son kayıt), Yeni dünya, Yükle, Ayarlar, Başarımlar, Modlar, Çıkış.
- **Yeni dünya:** `WorldGenSettings` kaydırıcıları (Bölüm 1.10.5), şablon seçici, seed alanı, önizleme, PNG'den içe aktar.
- **Kaydet/Yükle:** Slot ızgarası (önizleme görseli, dünya adı, yıl, nüfus, tarih); yeniden adlandır, sil (onaylı), dışa/içe aktar (paylaşım dosyası).
- **Ayarlar:** Dil, UI ölçeği, tema, ses kanalları (ana/efekt/müzik/ortam), kenar kaydırma, bildirim kategorileri, otomatik kayıt aralığı, performans (birim LOD eşikleri, partikül yoğunluğu, maksimum FPS), hata raporu gönderimi (açık/kapalı), tuş atama.
- **Öğretici:** Özgün maskotlu 8 adımlı ilk oyun rehberi (kara çiz → hayvan ekle → insan ekle → zaman akıt → şehri incele → güç kullan → kanunları aç → kaydet). Her adım bir koşul bekler.

---

## 8.15 Tooltip ve erişilebilirlik

- `TooltipService`: 0,4 sn gecikme, fare takibi, mobilde uzun basma. Trait/güç/bina/eşya tooltip'leri JSON'daki açıklama + hesaplanmış efekt listesi.
- Tüm renk kodlu bilgiler ikon veya desenle de verilir (renk körlüğü).
- Klavyeyle pencere gezinme (Tab), ekran okuyucu etiketleri UI Toolkit `accessibility` alanlarıyla.

---

## 8.16 Testler ve kabul kriterleri

| # | Test | Kriter |
|---|------|--------|
| 1 | UI duman testi | Tüm pencereler açılıp kapanır, konsol hatası yok |
| 2 | 4 açık pencere + 5.000 birim | UI maliyeti < 2 ms/frame |
| 3 | Overlay değişimi | Mod değiştirmek < 50 ms |
| 4 | Editör | Trait ekle → stat anında değişir, ilahi işaret eklenir |
| 5 | Possession | Birimle 60 sn oyna; kontrol bırakınca AI kaldığı yerden devam eder |
| 6 | Mobil düzen (1080×2400 ve 720×1600) | Hiçbir öğe ekran dışında değil, dokunma alanları ≥ 44 px |
| 7 | Yerelleştirme | EN diline geçişte eksik anahtar yok (otomatik tarama) |

## 8.17 Görev listesi

| # | Görev | Süre |
|---|-------|------|
| 1 | UI çatısı, tema, WindowManager, tooltip | 2,5 gün |
| 2 | Üst çubuk, güç çubuğu, fırça paneli, favoriler, arama | 3 gün |
| 3 | Bildirim akışı, mini harita | 2 gün |
| 4 | Birim penceresi (7 sekme) | 3 gün |
| 5 | Jenerik meta penceresi | 3 gün |
| 6 | Overlay'ler ve haritadan seçim | 2,5 gün |
| 7 | İsim plakaları, armalar | 2 gün |
| 8 | Keşif + başarım + profil dosyası | 2,5 gün |
| 9 | Trait/gen/eşya editörleri | 4 gün |
| 10 | Kanunlar, çağ saati, istatistik, grafikler, tarihçe | 4 gün |
| 11 | Possession + meta kontrol | 3 gün |
| 12 | Menüler, yeni dünya, kaydet/yükle, ayarlar, öğretici | 4 gün |
| 13 | Mobil düzen, erişilebilirlik, yerelleştirme bağlama | 3 gün |
| 14 | Testler | 1,5 gün |
| | **Toplam** | **~40 gün** |
