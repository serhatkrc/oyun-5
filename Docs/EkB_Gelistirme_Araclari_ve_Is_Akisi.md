# EK B — Geliştirme Araçları ve İş Akışı

Bu belge, PixelGenesis'in hangi araçlarla kodlanacağını, her aracın nasıl kurulacağını ve günlük işin nasıl yürüyeceğini anlatır. Sıralama kurulum sırasıdır: yukarıdan aşağı giderek sıfırdan çalışan bir geliştirme ortamı kurarsın.

---

## 3.1 Araç haritası

| Alan | Araç | Ne için | Ücret |
|------|------|---------|-------|
| Motor | **Unity 6.3 LTS** | Oyun motoru, editör, build | Ücretsiz (Personal lisans; gelir eşiği için güncel lisans şartlarına bak) |
| Kod editörü | **JetBrains Rider** (önerilen) veya **VS Code** + C# Dev Kit + Unity eklentisi | C# yazma, hata ayıklama, refactor | Rider: ticari olmayan kullanımda ücretsiz; VS Code ücretsiz |
| Yapay zekâ asistanı | **Claude Code** | Faz faz kod yazdırma, test, refactor | Claude aboneliği veya API |
| Sürüm kontrolü | **Git** + **Git LFS** | Kod ve büyük dosyalar (png, wav) | Ücretsiz |
| Depo ve proje yönetimi | **GitHub** (repo, Issues, Projects, Actions) | Kod barındırma, görev panosu, CI | Özel depo ücretsiz; CI dakikaları kotalı |
| Sürekli entegrasyon | **GitHub Actions** + **GameCI** | Otomatik test ve build | Ücretsiz (kota dahilinde) |
| Piksel sanat | **Aseprite** (alternatif: Pixelorama, LibreSprite) | Sprite, animasyon, tile paletleri | Aseprite ücretli, alternatifler ücretsiz |
| Ses efekti | **jsfxr / ChipTone** + **Audacity** | Retro efekt üretimi ve düzenleme | Ücretsiz |
| Müzik | **BeepBox** veya **LMMS** | Chiptune / ambient müzik | Ücretsiz |
| Yazı tipi | Açık lisanslı (OFL) piksel fontlar | Türkçe karakter destekli UI fontu | Ücretsiz |
| İçerik üretimi | **Python 3.11+** | `Data/_generator` betikleri, doğrulama, katalog | Ücretsiz |
| Veri düzenleme | VS Code + JSON Schema | JSON içerikleri hatasız düzenleme | Ücretsiz |
| Profil çıkarma | Unity Profiler, Memory Profiler, Profile Analyzer, Burst Inspector, Frame Debugger | Performans ve bellek | Unity ile gelir |
| Test | Unity Test Framework (NUnit) + Performance Testing paketi | Birim, entegrasyon ve performans testleri | Unity ile gelir |
| Hata raporlama | Sentry Unity SDK (veya Unity Cloud Diagnostics) | Oyuncu çökmelerini toplama | Ücretsiz katman var |
| Dağıtım | Steamworks (Steamworks.NET), itch.io + butler, Google Play Console, App Store Connect | Yayın | Platform kayıt ücretleri (güncel ücretleri kontrol et) |

> **Sürüm notu:** Bölüm 1'de "Unity 6000.0 LTS" yazıyordu. Unity 6.0 LTS'in desteği Ekim 2026'da bitiyor; 6.3 LTS ise Aralık 2027'ye kadar destekleniyor. Bu yüzden proje **Unity 6.3 LTS** ile başlatılmalı. Paket sürümleri de 6.3 ile gelen doğrulanmış sürümlerden seçilir.

---

## 3.2 Donanım ve işletim sistemi

| | Asgari | Önerilen |
|--|--------|----------|
| CPU | 6 çekirdek | 8+ çekirdek (Burst job'ları çekirdek sayısıyla ölçeklenir) |
| RAM | 16 GB | 32 GB |
| Disk | 50 GB SSD | 100 GB NVMe (Library klasörü ve build'ler) |
| GPU | DirectX 11 / Metal destekli | Önemli değil (2D oyun) |
| İşletim sistemi | Windows 10/11, macOS 13+ veya Ubuntu 22.04+ | Windows (en geniş build hedefi); iOS build için macOS şart |

Test cihazları: en az bir orta seviye Android telefon ve bir düşük seviye dizüstü. Performans hedefleri (Bölüm 1.16) bu cihazlarda ölçülür.

---

## 3.3 Kurulum adımları

### Adım 1 — Unity Hub ve motor
1. Unity Hub'ı resmi siteden kur, Unity hesabıyla giriş yap.
2. **Installs → Install Editor → Unity 6.3 LTS** (en son yama sürümü).
3. Modüller: Windows Build Support (IL2CPP), Mac Build Support (Mono + IL2CPP), Linux Build Support (IL2CPP), Android Build Support (OpenJDK + SDK/NDK ile birlikte), iOS Build Support (yalnızca macOS'ta), dokümantasyon.
4. Rider kullanılacaksa Visual Studio modülünü işaretleme.

### Adım 2 — Kod editörü
**Rider (önerilen):** Kur, Unity eklentisi hazır gelir. Unity'de **Edit → Preferences → External Tools → External Script Editor = Rider**. "Generate .csproj files" altında Embedded packages, Local packages ve Registry packages kutularını işaretle (paket koduna gidebilmek için).

**VS Code (alternatif):** C# Dev Kit ve Unity eklentilerini kur. Unity'de Visual Studio Editor paketi yüklü olmalı; External Script Editor = VS Code.

Her iki durumda da depo köküne `.editorconfig` koy (Adım 7).

### Adım 3 — Git ve Git LFS
```bash
git --version          # 2.40+
git lfs install        # makine başına bir kez
```
Unity YAML dosyalarında çakışmaları çözmek için Unity'nin kendi birleştirme aracını tanıt (`.gitconfig`):
```ini
[merge]
    tool = unityyamlmerge
[mergetool "unityyamlmerge"]
    trustExitCode = false
    cmd = '<UnityKurulumYolu>/Editor/Data/Tools/UnityYAMLMerge' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
```

### Adım 4 — Projeyi oluşturma
1. Unity Hub → **New project → Universal 2D** şablonu → ad: `PixelGenesis`.
2. **Edit → Project Settings → Editor:** Version Control Mode = *Visible Meta Files*, Asset Serialization Mode = *Force Text*.
3. **Player:** Scripting Backend = IL2CPP, API Compatibility = .NET Standard 2.1, *Allow 'unsafe' Code* = açık, Active Input Handling = *Input System Package (New)*.
4. **Quality:** VSync açık (editörde), Anti-aliasing kapalı (piksel keskinliği için).
5. **Graphics / URP 2D Renderer:** Pixel Perfect Camera bileşeni kamerada kullanılacak; Post-processing kapalı.

### Adım 5 — Paketler
Package Manager'dan ya da doğrudan `Packages/manifest.json` üzerinden eklenir.

| Paket | Kaynak | Not |
|-------|--------|-----|
| Burst, Collections, Mathematics | Unity Registry | Simülasyon çekirdeği |
| Input System | Unity Registry | Klavye/fare/dokunmatik |
| Localization | Unity Registry | TR + EN metin tabloları |
| Test Framework, Performance Testing | Unity Registry | Testler ve benchmark |
| Memory Profiler, Profile Analyzer | Unity Registry | Yalnızca geliştirmede |
| Newtonsoft Json | Unity Registry (`com.unity.nuget.newtonsoft-json`) | İçerik yükleme |
| NuGetForUnity | Git URL (GitHub) | NuGet paketlerini Unity'ye çekmek için |
| MessagePack-CSharp | NuGetForUnity veya resmi Unity paketi | İkili kayıt formatı |
| K4os.Compression.LZ4 | NuGetForUnity | Kayıt sıkıştırma |
| FastNoiseLite | Tek `.cs` dosyası, `Assets/_Project/ThirdParty/` altına kopyalanır | Gürültü üretimi (MIT lisansı `CREDITS.md`'ye) |
| Steamworks.NET | Git URL | Yalnızca Steam build'inde (Faz 11) |

Kural: her üçüncü taraf paket `CREDITS.md`'ye lisansıyla birlikte yazılır ve sürümü sabitlenir (git URL'lerinde `#vX.Y.Z` etiketi kullanılır).

### Adım 6 — Klasör ve assembly yapısı
Bölüm 1.2'deki klasörleri oluştur; her birine sağ tık → **Create → Scripting → Assembly Definition**. Referansları diyagramdaki oklara göre ver:

| Assembly | Referanslar | Ek ayar |
|----------|-------------|---------|
| `PG.Core` | Collections, Mathematics, Burst | Allow unsafe |
| `PG.Content` | PG.Core, Newtonsoft | |
| `PG.World` | PG.Core, PG.Content, Collections, Mathematics, Burst | Allow unsafe |
| `PG.WorldGen` | PG.Core, PG.Content, PG.World, Burst | |
| `PG.Sim` | PG.Core, PG.Content, PG.World, Burst | |
| `PG.Powers` | + PG.Sim | |
| `PG.Render` | PG.Core, PG.World, PG.Sim, URP | |
| `PG.Persistence` | PG.Core, PG.Content, PG.World, PG.Sim, MessagePack, LZ4 | |
| `PG.UI` | hepsi, Localization, Input System | |
| `PG.Boot` | hepsi | |
| `PG.Tests.EditMode` / `PG.Tests.PlayMode` | hepsi + test paketleri | "Test Assemblies" işaretli |

Bir assembly yanlışlıkla üst katmana referans verirse derleme hata verir. Bu, mimariyi korumanın en ucuz yoludur.

### Adım 7 — Depo dosyaları
Depo kökünde şu dosyalar bulunur:

```
PixelGenesis/
├─ Assets/ Packages/ ProjectSettings/      ← Unity
├─ Data/                                   ← içerik JSON'ları + _generator (EK A)
├─ Docs/                                   ← 00_, 01_, 03_ ... plan belgeleri, EkA kataloğu
├─ Tools/                                  ← yardımcı betikler (aseprite dışa aktarma, build)
├─ .github/workflows/                      ← CI tanımları
├─ .gitignore  .gitattributes  .editorconfig
├─ CLAUDE.md                               ← Claude Code için proje kuralları (3.5)
├─ DECISIONS.md  CREDITS.md  CHANGELOG.md  README.md
```

`Data/` klasörü Unity projesine `StreamingAssets/Data` olarak bağlanır: build öncesi bir editör betiği kopyalar (sembolik link Windows'ta sorun çıkarır).

**`.gitignore`:** GitHub'ın resmi Unity şablonu kullanılır (`Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`, `*.csproj`, `*.sln` hariç tutulur).

**`.gitattributes` (LFS):**
```
*.png filter=lfs diff=lfs merge=lfs -text
*.aseprite filter=lfs diff=lfs merge=lfs -text
*.wav filter=lfs diff=lfs merge=lfs -text
*.ogg filter=lfs diff=lfs merge=lfs -text
*.ttf filter=lfs diff=lfs merge=lfs -text
*.unity merge=unityyamlmerge eol=lf
*.prefab merge=unityyamlmerge eol=lf
*.asset merge=unityyamlmerge eol=lf
*.cs text eol=lf
*.json text eol=lf
```

**`.editorconfig`:** 4 boşluk girinti, LF satır sonu, `private` alanlar `_camelCase`, public üyeler `PascalCase`, `var` serbest; Rider bu dosyayı otomatik uygular.

---

## 3.4 Git iş akışı

- **Dal modeli:** `main` her zaman derlenir ve testleri geçer. Her iş `feature/<faz>-<kısa-ad>` dalında yapılır (ör. `feature/f1-region-system`). Hata düzeltmeleri `fix/<kısa-ad>`.
- **Commit mesajı:** Conventional Commits. Örnek: `feat(world): add incremental region rebuild`, `fix(worldgen): clamp sea level percentile`, `content(biomes): add coral reef`, `perf(render): batch chunk uploads`.
- **Pull request:** Tek geliştirici olsan da PR aç. CI yeşil olmadan birleştirme. PR açıklamasında: ne yapıldı, hangi kabul kriterine karşılık geliyor, performans etkisi.
- **Etiketleme:** Her faz sonunda `v0.<faz>.0` etiketi (`v0.1.0` = Faz 1 bitti). Oynanabilir ara sürümler `v0.1.1` gibi.
- **Görev panosu:** GitHub Projects'te her faz bir *Milestone*. Bölüm 1.17'deki görev listesi gibi tablolar Issue olarak açılır; sütunlar: Backlog → Bu hafta → Yapılıyor → İncelemede → Bitti.

---

## 3.5 Claude Code ile çalışma yöntemi

Claude Code, depo içinde komut satırından çalışan bir kodlama asistanıdır. Kurulum ve güncel gereksinimler için resmi dokümantasyona bak: https://docs.claude.com/en/docs/claude-code/overview

### 3.5.1 CLAUDE.md
Depo kökündeki `CLAUDE.md` dosyası her oturumda asistana okunur. İçeriği kısa ve kural odaklı tutulur:

```markdown
# PixelGenesis — proje kuralları
- Motor: Unity 6.3 LTS, C#. Mimari: Docs/00_Genel_Plan.md ve Docs/01_Mimari_Cekirdek_Dunya.md.
- Katman kuralı: Core ← Content ← World ← Sim ← Powers ← UI. Render ve Persistence yalnızca okur.
- Simülasyon verisi NativeArray/SoA'da; MonoBehaviour'a simülasyon mantığı yazma.
- Rastgelelik yalnızca SimRandom akışlarıyla; UnityEngine.Random yasak.
- İçerik JSON'da (Data/). Kodda içerik sabiti yazma.
- Her yeni sistem için EditMode testi yaz. Testleri çalıştırmadan işi bitmiş sayma.
- Tick içinde heap ayırma yapma (GC 0 hedefi).
- Emin olmadığın tasarım kararında seçenekleri sun, kararı DECISIONS.md'ye yaz.
- Oyuncuya görünen metinler Localization anahtarıyla; kod yorumları İngilizce.
```

### 3.5.2 Görev verme düzeni
1. **Bir oturum = bir görev.** Bölüm 1.17'deki tablodan tek satır seç (ör. "Region/ada sistemi").
2. Görevi verirken ilgili bölüm numarasını belirt: *"Docs/01 dosyasındaki 1.9 bölümünü uygula. Önce plan yaz, onayımdan sonra kodla."*
3. Asistan önce planı yazar; sen okuyup onaylarsın. Plan bölümdeki API imzalarıyla uyuşmuyorsa düzelttirirsin.
4. Kod yazılır, ardından testler yazılır ve komut satırından çalıştırılır (3.7.2).
5. Değişikliği (diff) sen incelersin. Özellikle bakılacaklar: katman kuralı ihlali, tick içinde `new`/LINQ, `UnityEngine.Random` kullanımı, testsiz kod.
6. Onaylanan iş commit edilir, PR açılır.

### 3.5.3 Editörle bağlantı (isteğe bağlı)
Topluluk tarafından geliştirilmiş "Unity MCP" sunucuları, asistanın Unity editöründe sahne/konsol okumasına izin verir. Resmi bir araç değildir; kullanmadan önce güncelliğini ve güvenliğini kontrol et. Kullanmasan da iş akışı eksiksiz çalışır: asistan kodu yazar, sen Unity'de derlenmesini ve sahneyi kontrol edersin, konsol hatasını asistana yapıştırırsın.

### 3.5.4 İçerik işleri
Yeni trait/tür/bina eklemek gibi içerik işlerinde asistana `Data/_generator/` betiklerini düzenlet, ardından `python validate.py` ve `python catalog.py` çalıştırt. JSON'u elle düzenlemek yerine betiği düzenlemek, katalog ve doğrulamanın senkron kalmasını sağlar.

---

## 3.6 Sanat ve ses üretim hattı

### 3.6.1 Piksel ölçeği
Bölüm 1'deki kurala göre **1 tile = 1 dünya birimi = 1 sprite pikseli**. Bu yüzden tüm sprite'lar **Pixels Per Unit = 1** ile içe aktarılır. Bir insan sprite'ı ~4×6 piksel, bir ejderha ~16×12 piksel olur.

### 3.6.2 Aseprite dosya düzeni
```
Art/Source/units/human.aseprite     ← katmanlar: body, clothes(krallık rengi), hair, item
Art/Source/buildings/human_house.aseprite
Art/Source/features/trees.aseprite
Art/Source/ui/icons_powers.aseprite
```
- **Etiketler (tags)** animasyonları tanımlar: `idle`, `walk`, `attack`, `swim`, `sleep`, `death`, `baby_walk`, `old_walk`.
- **Renk kuralı:** Birim sprite'ları **indeksli gri tonla** çizilir (ör. 0–7 arası 8 gri seviye). Gerçek renkler shader'da palet dokusundan gelir. Böylece fenotip, krallık rengi ve zombi paleti tek sprite ile yapılır (palette swap).

### 3.6.3 Dışa aktarma
`Tools/export_art.py` betiği her `.aseprite` dosyasını Aseprite'ın komut satırı modu ile sprite sheet + JSON olarak dışa aktarır:
```bash
aseprite -b Art/Source/units/human.aseprite --sheet Assets/_Project/Art/units/human.png --data Assets/_Project/Art/units/human.json --format json-array --list-tags
```
Unity tarafında bir `AssetPostprocessor` bu JSON'u okuyup animasyon kare listelerini (`UnitAnimationSet` ScriptableObject) üretir.

### 3.6.4 Unity içe aktarma ayarları (Preset olarak kaydedilir)
| Ayar | Değer |
|------|-------|
| Texture Type | Sprite (2D and UI) |
| Pixels Per Unit | 1 |
| Filter Mode | Point (no filter) |
| Compression | None |
| Generate Mip Maps | Kapalı |
| Sprite Mode | Multiple (sheet) |

Tüm birim ve bina sprite'ları **Sprite Atlas** içinde toplanır (birim atlası, bina atlası, UI atlası). Bu, Bölüm 1.11'deki "tek draw call'a yakın" hedefi için şarttır.

### 3.6.5 Ses
- Efektler jsfxr/ChipTone'da üretilir, Audacity'de normalize edilir (−1 dB tepe), `.wav` 44.1 kHz mono olarak `Art/Audio/sfx/` altına konur. Unity'de "Decompress On Load" (kısa efektler).
- Müzik BeepBox/LMMS'te yapılır, `.ogg` olarak "Streaming" ayarıyla içe aktarılır. Her çağ için bir müzik katmanı.
- Ses tanımları da veridir: `Data/sounds.json` (id, dosyalar, rastgele perde aralığı, eşzamanlı sınır).

### 3.6.6 Font
Türkçe karakterleri (ç, ğ, ı, İ, ö, ş, ü) içeren, OFL lisanslı bir piksel font seçilir. Unity'de TextCore/UI Toolkit font asset'i oluşturulur; karakter setine Türkçe harfler açıkça eklenir. Lisans `CREDITS.md`'ye yazılır.

---

## 3.7 Test altyapısı

### 3.7.1 Test türleri
| Tür | Klasör | Örnek | Ne zaman çalışır |
|-----|--------|-------|------------------|
| EditMode birim testi | `Tests/EditMode` | PCG32 dağılımı, `SetGround` bayrak güncellemesi, region artımlı = tam | Her commit (CI) |
| PlayMode entegrasyon | `Tests/PlayMode` | 10 insan → 100 yıl → şehir kuruldu mu | Her PR |
| Determinizm | `Tests/PlayMode` | Aynı seed ile iki koşu, 1000 tick hash karşılaştırma | Her PR |
| Performans | `Tests/Performance` | Titanik harita üretim süresi, 5.000 birimle tick süresi | Gece (nightly) |
| İçerik | `Data/_generator/validate.py` | Kırık referans, çift id | Her commit |

### 3.7.2 Komut satırından çalıştırma
Unity'nin batch modu ile testler editör açılmadan çalıştırılabilir; Claude Code da bu komutu kullanır:
```bash
Unity -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testResults Logs/editmode.xml
Unity -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults Logs/playmode.xml
```
Not: Aynı proje editörde açıkken batch modu çalışmaz; ya editörü kapat ya da testleri editörün Test Runner penceresinden çalıştır.

### 3.7.3 Headless simülasyon
`PG.Boot` içine `--headless --years 1000 --seed 42` argümanlarını kabul eden bir başlatıcı eklenir. Render olmadan simülasyonu koşturur ve sonunda istatistik raporu (nüfus, krallık sayısı, bellek) yazar. Uzun süreli "sağlık" testleri (Bölüm 1.16 ve genel plan) bununla yapılır.

---

## 3.8 Sürekli entegrasyon (GitHub Actions + GameCI)

GameCI, Unity projelerini GitHub Actions üzerinde test etmek ve build almak için hazır eylemler sunar. Unity lisansının CI'da etkinleştirilmesi gerekir; bunun adımları GameCI dokümantasyonunda anlatılır ve lisans bilgileri GitHub **Secrets** içinde saklanır (asla depoya yazılmaz).

`.github/workflows/ci.yml` aşamaları:

| Aşama | Tetikleyici | İş |
|-------|-------------|----|
| 1. İçerik | her push | `python Data/_generator/validate.py` — hata varsa dur |
| 2. EditMode testleri | her push | GameCI test runner |
| 3. PlayMode testleri | PR | GameCI test runner + determinizm testi |
| 4. Build | `main`'e birleşince | Windows ve Linux build'i, artifact olarak saklanır |
| 5. Performans | gece | Performance Testing sonuçları, önceki güne göre %10 kötüleşmede uyarı |

`Library/` klasörü CI önbelleğine alınır; aksi hâlde her çalıştırmada içe aktarma dakikalar sürer.

---

## 3.9 Profil çıkarma ve performans rutini

- **Her faz sonunda** Bölüm 1.16 tablosundaki ölçümler tekrarlanır ve sonuçlar `Docs/perf_log.md`'ye yazılır.
- **Unity Profiler:** Deep Profile yerine `ProfilerMarker` işaretçileri kullan (Bölüm 1.15); Deep Profile sonuçları bozar.
- **Burst Inspector:** Job'ların gerçekten Burst ile derlendiğini ve vektörleştiğini kontrol et.
- **Memory Profiler:** Her fazda bir anlık görüntü; `NativeArray` sızıntıları için editörde *Leak Detection = Full Stack Traces* açık.
- **Profile Analyzer:** İki build'in frame sürelerini karşılaştırmak için.
- **GC kontrolü:** Profiler'da "GC Alloc" sütunu tick sırasında 0 olmalı.

---

## 3.10 Yerelleştirme

- Unity Localization paketi, iki dil: `tr` (varsayılan) ve `en`.
- **String Table** koleksiyonları: `UI`, `Content` (tüm içerik isimleri ve açıklamaları), `History` (tarihçe olay şablonları).
- İçerik JSON'undaki `name` alanı Türkçe kaynak metindir; bir editör betiği bunları `Content` tablosuna anahtar = içerik id'si (`tr.strong`, `bio.coral`) olacak şekilde aktarır. İngilizce çeviriler bu tablo üzerinden eklenir.
- Prosedürel isimler (birim, şehir, krallık) çevrilmez; dil sisteminden gelir.

---

## 3.11 Hata raporlama ve oyuncu geri bildirimi

- Sentry Unity SDK: çökme ve yakalanmamış istisnalar; build sürümü ve dünya boyutu etiket olarak eklenir. Kişisel veri gönderilmez, oyuncuya ayarlarda kapatma seçeneği verilir.
- Oyun içi "Hata bildir" butonu: son 200 log satırı + isteğe bağlı kayıt dosyasını zip'ler, oyuncunun kendisinin göndermesi için klasörü açar.
- Discord/Steam topluluk sayfası geri bildirim kanalı olarak kullanılır (Faz 11 sonrası).

---

## 3.12 Build ve yayın süreci

| Hedef | Nasıl | Not |
|-------|-------|-----|
| Windows / Linux / macOS | Unity **Build Profiles**, IL2CPP | macOS için notarization gerekir (Apple geliştirici hesabı) |
| Steam | Steamworks.NET + SteamPipe (`steamcmd` ile yükleme) | Başarımlar, Workshop (mod paylaşımı), bulut kayıt |
| itch.io | `butler push Build/win pixelgenesis/game:windows` | Erken erişim/demo için hızlı kanal |
| Android | IL2CPP, ARM64, AAB çıktısı, Google Play Console | Varsayılan harita boyutu küçük; dokunmatik UI |
| iOS | Xcode projesi çıkarılır, macOS'ta derlenir, App Store Connect | Apple geliştirici üyeliği gerekir |

**Sürüm numarası:** `0.<faz>.<yama>` Early Access'e kadar; çıkışta `1.0.0`. Build numarası CI çalıştırma numarasından gelir ve oyun içinde köşede gösterilir.

**Yayın kontrol listesi:** tüm testler yeşil → performans tablosu hedefte → kayıt migrasyon testi (önceki sürümün kayıtları yükleniyor) → CHANGELOG güncel → CREDITS güncel → build'ler test cihazlarında denendi → etiket at → yükle.

---

## 3.13 Günlük çalışma döngüsü

1. `main`'i çek, yeni dal aç.
2. Panodan bir görev al; Claude Code oturumunu o görevle başlat.
3. Plan → onay → kod → test (3.5.2).
4. Unity'de oyunu çalıştır, görsel/oynanış kontrolü yap; debug overlay (F3) ile sayılara bak.
5. Gerekirse içerik: betiği düzenle, `validate.py` + `catalog.py` çalıştır.
6. Commit, push, PR; CI yeşilse birleştir.
7. Haftada bir: profil ölçümü, `perf_log.md` güncellemesi, panonun temizlenmesi.

---

## 3.14 Faz 1 için kurulum kontrol listesi

- [ ] Unity 6.3 LTS + modüller kurulu
- [ ] Rider/VS Code Unity ile bağlı, `.csproj` üretiliyor
- [ ] Git + LFS kurulu, UnityYAMLMerge tanımlı
- [ ] Proje *Universal 2D* şablonuyla oluşturuldu, Force Text + Visible Meta açık
- [ ] Paketler yüklü, sürümler sabit, `CREDITS.md` yazıldı
- [ ] Assembly tanımları diyagrama göre kuruldu, derleme temiz
- [ ] `.gitignore`, `.gitattributes`, `.editorconfig`, `CLAUDE.md`, `DECISIONS.md` depoda
- [ ] `Data/` klasörü ve `_generator` depoda; `validate.py` çalışıyor
- [ ] GitHub Actions: içerik doğrulama + EditMode testleri yeşil
- [ ] Sprite içe aktarma Preset'i ve atlaslar hazır (ilk tile paleti ile)
- [ ] İlk boş sahne `GameBootstrap` açılıyor, F3 debug overlay çalışıyor
