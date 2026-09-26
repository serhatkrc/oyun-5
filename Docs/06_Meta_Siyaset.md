# BÖLÜM 6 — Meta Nesneler ve Siyaset

Bu bölüm bittiğinde dünya kendi tarihini yazar: krallıklar kurulur, krallar ölür ve yerine geçen gelir, şehirler isyan eder, savaşlar ve ittifaklar olur, kültürler/diller/dinler yayılır ve bölünür, entrikalar çevrilir, kitaplar yazılır.

Bağımlılık: Bölüm 3–5. İçerik: `culture_traits.json`, `religion_traits.json`, `language_traits.json`, `clan_traits.json`, `kingdom_traits.json`, `plots.json`, `war_types.json`, `book_types.json`, `name_sets.json`.

---

## 6.1 Ortak meta altyapısı

```csharp
public enum MetaKind : byte { Kingdom, City, Alliance, Army, Clan, Family, Culture, Language, Religion, Subspecies, Plot, War }

public struct MetaHeader {
    public long   Uid;
    public MetaKind Kind;
    public int    NameId;              // isim havuzu
    public Color32 Color;
    public Banner Banner;              // 6.3
    public long   FoundedTick, DiedTick;   // DiedTick = 0 → yaşıyor
    public long   FounderUid;
    public int    MemberCount;         // aylık önbellek
    public TraitSet256 Traits;         // türe göre ilgili trait dosyası
    public MetaFlags Flags;            // Favorite, Edited, Dead, PlayerControlled
}

public sealed class MetaStore<T> where T : struct {
    public int Create(MetaKind kind, in T data, long founderUid);
    public ref MetaHeader Header(int index);
    public ref T Data(int index);
    public void MarkDead(int index, long tick);     // silinmez; tarih ve istatistik için kalır
    public IEnumerable<int> Alive();
}
```

### İstatistik geçmişi
Her meta nesne için aylık örnek: nüfus, (krallık/şehir için) şehir sayısı, altın, asker, alan. `StatSeries` her seri için 1.200 örneklik (100 yıl) halka tampon + her 10 yılın ortalamasını tutan uzun tampon (grafikler 1.000 yıla kadar gösterebilsin).

### Tür/alt tür dağılımı
Meta penceresinde üyelerin alt tür dağılımı (sayı ve %) gösterilir; aylık olarak üyeler taranarak `SpeciesBreakdown` (en fazla 8 kalem + "diğer") hesaplanır.

### Üyelik kuralları
- Yeni doğan bebek ebeveyninin (anne öncelikli) şehir, krallık, kültür, dil, din, klan ve ailesini alır.
- Spawn edilen birim 20 tile içinde aynı türden birim varsa onun kültür, dil ve dinini alır (rastgele meta patlamasını önler).
- Meta tutabilme alt tür bayraklarına bağlıdır (Bölüm 4.1): `can_hold_culture` yoksa kültür atanmaz vb.
- Oluşturulurken kültür, dil, klan ve din %40 ihtimalle 1–2 rastgele trait alır (biyom havuzları öncelikli: `biomes.json` `culturePool`, `languagePool`, `religionPool`).

---

## 6.2 İsim üretici

`name_sets.json` dil sesbilimi setlerini içerir. Her dil meta nesnesi bir `phonology` trait'i taşır (yoksa `lang.plain_speech`).

```
kelime(set, rng):
    şablon = rastgele(set.pattern)            // ör. "CVCV"
    her harf için: C → rastgele(onset), V → rastgele(vowel)
    %40 ihtimalle sona coda
    backwards → ters çevir;  silent_signs → sembol + sayı
isim(birim)   = kelime (+ %30 ikinci kelime)
isim(şehir)   = kelime + (%30 ek: "ia", "heim", "gar", "ova")
isim(krallık) = kök + " " + unvan  (unvan: rulerTitles, şehir sayısına göre)  veya kök + "ya"
isim(klan)    = kök + "oğulları" benzeri soy eki (dile göre)
isim(din)     = kök + " Yolu" / " Tarikatı";  isim(kültür) = kök + "lı"
```
- İsimler bir `NamePool` içinde saklanır (`NameId` → string); aynı isim tekrar üretilirse sonuna Roma rakamı eklenir (II, III) — krallar ve hanedan için.
- `lang.loanwords` olan diller komşu dillerin hecelerinden %20 alır.
- Oyuncunun değiştirdiği isimler `MetaFlags.Edited` ve 3 saniyelik parlama efekti (Bölüm 8).

---

## 6.3 Arma (Banner) üretimi

```csharp
public struct Banner { public byte Shape, Icon, BgColor, FgColor, Pattern; }
```
- 12 kalkan/bayrak şekli, 64 ikon (özgün piksel ikonlar: kule, taç, kurt başı, güneş, yaprak, çekiç...), 16 renk, 6 desen.
- Kültür trait'i ikon eğilimini etkiler (`war_drums` → silah ikonları, `farmers` → başak).
- İsyan/bölünmeyle doğan krallık, ana krallığın şeklini korur ama renkleri değiştirir.
- Render: 16×16 piksel doku, `BannerAtlas`'ta önbelleklenir.

---

## 6.4 Krallık

```csharp
public struct KingdomData {
    public int King, Heir;                  // birim indeksleri
    public int Capital;                     // şehir
    public NativeList<int> Cities;
    public int Culture, Religion, Language; // resmî (başkentin çoğunluğu)
    public int Alliance;
    public ushort RulerTitleIndex;
    public int  Treasury;
    public long LastWarEndTick;
    public NativeHashMap<int, short> Opinion;   // diğer krallık → fikir (−100..100)
}
```

### Kuruluş
Bölüm 5'te kurulan her şehir, bağlı olduğu krallık yoksa kendi krallığını kurar (kurucu lider kral olur). Sömürgeler kurucu şehrin krallığına bağlıdır.

### Kral ve veraset
Kral öldüğünde (veya tahttan indirildiğinde) kültürün `succ_*` trait'i uygulanır:

| Trait | Seçim |
|-------|-------|
| `succ_primogeniture` (varsayılan) | Kralın en büyük yaşayan evladı; yoksa klanın en yaşlısı |
| `succ_election` | Krallıkta en yüksek diplomasi |
| `succ_strongest` | En yüksek savaşçılık; aday eşitse düello |
| `succ_wisest` | En yüksek zekâ |
| `succ_elders` | Başkentteki en yaşlı yetişkin |
| `succ_lot` | Şehir liderleri arasından rastgele |
| `succ_matrilineal` | En büyük kız evlat |
| `succ_youngest` | En küçük yetişkin evlat |

Aday yoksa başkentin lideri kral olur. Veraset sırasında 1 yıl için `Loyalty −10` (tüm şehirler); aday tartışmalıysa (iki güçlü aday) `plot.usurpation` başlatma şansı.

### Şehir sadakati (aylık)
```
sadakat = 50
        + kral.diplo × 3 + (loyal trait +15 / treacherous −15)
        + (başkent ise +100)
        − uzaklık(başkent, şehir) / 8                      // zone cinsinden
        + (aynı kültür ? 10 : −15) + (aynı din ? 10 : −10) + (aynı alt tür ? 5 : −10)
        + şehir mutluluğu / 5
        − savaş yorgunluğu (her aktif savaş yılı −3, en fazla −20)
        + çağ.LoyaltyBonus + kültür/krallık trait'leri
        + şehir lideri kralın akrabasıysa +10
```
Sadakat < 0 ve `law.kingdom_rebellions` açık → aylık %5 ihtimalle `plot.rebellion` (6.10) veya doğrudan isyan (lider `ambitious` ise).

### Fikir (krallıklar arası, aylık)
```
fikir = önceki × 0.95 + hedef × 0.05          // yavaş yakınsama
hedef = (aynı kültür 15) + (aynı din 20 / farklı din ve iki tarafta holy_war −30) + (aynı tür 10 / pure_blood farklı tür −30)
      + (sınır komşusu −10) + (ittifak 40) + (ortak düşman 20) + (kraliyet evliliği 30)
      + (geçmiş savaş: son 50 yıldaki her savaş −15) + (ticaret, yıllık kervan başına +2, en fazla 20)
      + kral trait'leri (peaceful +10, bloodthirsty −10, honest +10, deceitful −10) + çağ.OpinionBonus
```

---

## 6.5 Savaş

### Savaş ilanı (yıllık, krallık başına)
```
if law.wars kapalı → çık
adaylar = fikir < −20 olan ve kara/deniz yoluyla ulaşılabilen krallıklar
her aday için puan = −fikir + 30 × (bizimGüç / onlarınGücü − 1) + (bloodthirsty/war_drums +20) − uzaklık/10
şans = sigmoid((puan − 40)/15) × çağ çarpanı (kan çağı ×2) × kültür (pacifist_creed ×0.2)
seçilen hedefe savaş → WarData oluştur
```
Güç = asker sayısı × ortalama seviye × ekipman çarpanı + kule sayısı × 5.

```csharp
public struct WarData { public ushort Type; public int Attacker, Defender; public NativeList<int> AttackerSide, DefenderSide; public long StartTick, EndTick; public int CasualtiesA, CasualtiesD; public NativeList<int> CapturedCities; public int TargetCity; public WarResult Result; }
```
Savaş türleri `war_types.json`: fetih (varsayılan), isyan (isyanla), din (`holy_war`/`plot.crusade`), bağımsızlık (bölünme), intikam (`blood_feud`), veraset (taht plotu), herkese karşı (`law.chaos_world` veya delirmiş kral).

**Süre ve yaş ayrı tutulur:** `süre = (EndTick veya şimdi) − StartTick`; `yaş = şimdi − StartTick` (UI karşılaştırması için).

### Ordular
```csharp
public struct ArmyData { public int Kingdom, Captain; public NativeList<int> Soldiers; public int2 RallyTile; public int TargetCity; public ArmyState State; }  // Gathering, Marching, Sieging, Returning
```
- Savaş başında saldıran krallık, sınıra en yakın şehirlerinde ordu kurar: kaptan = en yüksek savaşçılık; askerler `job.warrior` birimleri.
- Hedef şehir: düşmanın en yakın ve en zayıf savunmalı şehri. Ordu toplanma noktasından (`RallyTile`) hedefe yürür; askerlerin hedef tile'ı kaptanın etrafında formasyon ofsetidir.
- Sefer bitince (şehir alındı veya ordu %30'un altına düştü) geri döner.

### Şehir fethi
- Saldıran askerler şehir zone'larına girip binalara ve savunuculara saldırır.
- Şehir merkezinin 10 tile içinde **savunan krallığa ait yaşayan savaşçı kalmadıysa** ve saldırgan birim merkezdeyse şehir el değiştirir: şehir saldırgan krallığa geçer, sakinler kalır (kültür/din değişmez), sadakat 0'dan başlar, `hap.city_captured`.
- Şehrin içinde hiç sakin yoksa ve ele geçirilemiyorsa şehir **yok edilir** (harabe).
- Başkent düşerse yeni başkent en büyük şehir olur; kral esir alınamaz, ya kaçar ya ölür.

### Barış
Yıllık kontrol: `şans = 0.05 + savaşYılı × 0.02 + kayıp oranı × 0.3 + (dawn çağı 0.3)`; saldırganın ele geçirdiği şehirler onda kalır. `plot.peace_treaty` barışı hemen yapar. Kralın ölümü de barış şansını ×2 yapar. Barış sonrası 10 yıl yeniden savaş ilanı yasak.

---

## 6.6 İttifak

```csharp
public struct AllianceData { public NativeList<int> Kingdoms; public int Leader; }
```
- Kuruluş: `plot.alliance` veya `pw.friendship`. Üyeler ortak düşmana karşı savaşa otomatik katılır (üyenin fikri savaşan düşmana < 0 ise).
- Üyeler arası fikir < −30 olursa üye ayrılır; tek üye kalırsa ittifak dağılır.

---

## 6.7 Kültür

```csharp
public struct CultureData { public int OriginKingdom; public int Generation; public int ParentCulture; }
```
- **Oluşma:** Yeni şehir kurulurken kurucuların kültürü; kültürü yoksa yeni kültür (biyom havuzundan trait). Plotla bölünme (`plot.culture_split`).
- **Yayılım:**
  - Konuşma (`talk` nöronu): iki birim konuşunca, dinleyenin alt türünde `advanced_memory` varsa ve kültürü `true_roots` değilse %5 ihtimalle konuşanın kültürüne geçer (oyuncu possession ile konuşursa %50).
  - Uyku sırasında din/kültür propagandası (rahipler, `missionaries`): uyuyan birimlere %50 ihtimal (aynı koşullar).
  - Fetih sonrası: yeni krallığın kültürü 20 yılda şehirde yavaşça yayılır (yıllık %5 dönüşüm).
- **Efekt uygulaması:** Kültür trait'lerinin efektleri üyelere (birim statları) ve şehir/krallık kararlarına (veraset, savaş şansı, bina tercihleri) uygulanır. Hangi anahtarın hangi sistemi etkilediği `BehaviorKeyRegistry` belgelerinde listelenir.
- `cul.ancestral_knowledge`: yeni doğana her ebeveynin en yüksek öğrenilmiş sivil statının yarısı eklenir.
- `cul.true_roots`: dönüşüme %100 direnç; krallık yıkılsa bile üyeler kültürü korur.

---

## 6.8 Dil

```csharp
public struct LanguageData { public ushort Phonology; public int ParentLanguage; public int WrittenWorks; }
```
- Konuşma merkezi (`speech_center`) olan alt türlerde kültürle birlikte doğar.
- **Lehçe ayrılığı:** Dil 3+ şehre yayılmış ve bir şehir diğer konuşanlardan 40+ zone uzaktaysa yıllık %2 (`many_dialects` ×2) `plot.language_split` otomatik başlar.
- Yazı: `written_script`, `runic`, `pictographic` trait'lerinden biri yoksa kitap yazılamaz (dil ilk yazı trait'ini 50 yıl sonra %30 ile kazanır).
- İsim üretici bu dili kullanır (6.2).

---

## 6.9 Din

```csharp
public struct ReligionData { public int Founder; public int HolyCity; public NativeList<int> Temples; public ushort DeityTrait; }
```
- **Oluşma:** `plot.new_religion` ile veya bir kültürün ilk tapınağı yapılırken (soyut düşünce varsa). İlk trait bir tanrı arketipi (`rel.green_mother` ...; biyom havuzu öncelikli) + 0–2 doktrin.
- **Büyü sistemi:** Din trait'lerindeki `spell:` anahtarları inananlara büyü slotu verir — rahiplere tam güç, sıradan inananlara %30 şans ve ×0.5 güç. Örn. `rel.star_choir` → `spell.meteor_call` (bekleme 1200 tick); `rel.hollow_king` → `spell.raise_dead`.
- **Yayılım:** Rahip vaazı (tapınak yarıçapı 20, yıllık %10 dönüşüm; `missionaries` ×1.5), konuşma (kültürle aynı kurallar), `plot.conversion` (sadece plotçunun kendi şehri).
- **Din savaşı:** `holy_war` doktrinli krallık, farklı dinli komşuya karşı savaş puanına +30.
- `inquisition`: farklı dindeki kendi vatandaşlarını zorla dönüştürür (yıllık %20), mutluluk −.

---

## 6.10 Klan ve aile

- **Aile:** Eşler + çocukları. Hayvanlarda **sürü** olarak işler (sürü lideri en yaşlı yetişkin). Çocuklar yetişkin olup evlenince yeni aile kurar.
- **Klan:** Medenilerde ortak ataya bağlı soy. Kurucu, `plot.new_clan` ile veya bir kralın soyundan otomatik (kral olan her birim klanı yoksa yeni klan kurar). Klan reisi = en yaşlı üye. Klan trait'leri üyelere uygulanır (`clan.royal_blood` → lider seçilme ×1.5).
- Klanın tarihçesi: klandan çıkan kralların listesi (`ach.dynasty`).

---

## 6.11 Plotlar (entrikalar)

```csharp
public struct PlotData { public ushort Def; public int Initiator; public NativeList<int> Members; public float Progress; public int TargetMeta; public long StartTick; public PlotState State; }
```

### Başlatma (yıllık, `law.plots` açık)
Her aday birim (lider, kral, rahip, soylu; `can_plot` bayrağı) için `plots.json`'daki plotlar koşul listesiyle değerlendirilir. Koşul dili basit anahtarlardır; `PlotConditionRegistry` her anahtarı koda eşler:

| Koşul anahtarı | Anlamı |
|----------------|--------|
| `intel>=6`, `diplo>=6`, `warfare>=5` | Sivil stat eşiği |
| `2 civil>=2` | En az iki sivil stat ≥ 2 |
| `ambitious_or_inspired` | `tr.ambitious` veya `st.inspired` |
| `loyalty<30` | Şehrinin sadakati |
| `opinion<-20` | Hedef krallığa fikir |
| `abstract_thought` | Alt tür trait'i |
| `kendi şehri`, `hedef lider`, `ortak düşman` ... | Bağlamsal koşullar |

Başlatma şansı: `0.05 × (ambitious 2) × (deceitful plotSpeed) × (kan çağı 1.5)`.

### İlerleme (aylık)
```
katılımcı toplama: başlatıcı 20 tile içindeki uygun birimleri davet eder (fikir/sadakat/trait uyumuna göre %20)
ilerleme += Σ (katılımcı.intel + katılımcı.diplo) × 0.5 / durationMonths / 10     // yaklaşık durationMonths'ta dolar
minParticipants altında ilerleme durur
başlatıcı ölürse: en güçlü katılımcı devralır (%50) ya da plot çöker
```
Dolunca `outcome` kodu çalışır (`PlotOutcomeRegistry`): yeni krallık, yeni din/kültür/dil/klan, savaş ilanı, suikast (hedef ölür; %30 yakalanma → plotçu idam), gasp (kral değişir), dönüşüm, ittifak, barış, reform (kültür trait'i değişir), karanlık ritüel (iblis çağırma veya ölü diriltme felaketi). Tarihçeye yazılır.

---

## 6.12 Kitaplar ve bilgi

```csharp
public struct BookData { public long Uid; public ushort Type; public int Language, Culture, Religion; public long AuthorUid; public int NameId; public int Library; public long WrittenTick; public byte Condition; }
```
- **Yazma:** `job.scholar` veya `pattern_seek` trait'li medeniler, kütüphanesi olan şehirde yıllık %10 (`bookWrite` çarpanlarıyla). Tür: kültür/din/yazar trait'ine göre (`scholars` → İlim, `holy` → Kutsal Metin).
- **Okuma:** `read_book` nöronu (boş zamanda, `bookworm` ×2). Okuyan birim `book_types.json`'daki bonusu kalıcı küçük stat olarak alır (aynı türden en fazla 3 kitap etkisi); dil biliyor olmalı. `hap.read_book`.
- **Yıpranma:** Yıllık `Condition −1` (`eternal_text` 0, `library_keepers` ×0.2); 0 olunca yok olur. Kütüphane yanarsa içindeki kitaplar yok olur. `book_burners` kültürü fethedilen şehirdeki yabancı kitapları yakar.

---

## 6.13 Tarihçe (World History)

```csharp
public struct HistoryEntry { public long Tick; public ushort Template; public long A, B, C; public int Extra; }  // Uid parametreleri
```
- Şablonlar yerelleştirme anahtarıdır: `history.kingdom_founded` → "{A} krallığı {B} tarafından kuruldu".
- Kaydedilen olaylar: krallık/şehir/din/kültür/dil/klan kuruluşu ve ölümü, kral değişimi, savaş başlangıç/bitiş, şehir fethi, isyan, plot sonucu, büyük afet, efsanevi eşya, ilk keşifler, favori birim ölümleri.
- Meta başına indeks (o meta'yla ilgili girişlerin listesi) — meta penceresindeki "Tarih" sekmesi için.
- Bellek: en fazla 200.000 giriş; aşılınca en eski "önemsiz" girişler (tek birimli) silinir.

---

## 6.14 Meta kontrol ve oyuncu müdahaleleri

- `pw.friendship` / `pw.spite`: iki krallığın fikrini ±100 yapar (sonra doğal olarak yakınsar).
- `pw.crown`: birim krallığının kralı olur; eski kral soylu olarak kalır.
- `pw.rebellion_spark`: şehir sadakati −100.
- **Meta kontrol** (`pw.meta_control`, Bölüm 8): Oyuncu seçtiği krallığın savaş hedefini, barışı, ittifakı, başkenti ve veraset adayını belirler; seçtiği dinin/kültürün hedef şehrini işaretler (yayılım o şehre odaklanır). Kontrol edilen meta `MetaFlags.PlayerControlled` taşır; AI yalnızca oyuncunun boş bıraktığı kararları verir.

---

## 6.15 Testler ve kabul kriterleri

| # | Test | Kriter |
|---|------|--------|
| 1 | 4 medeni tür aynı kıtada, 200 yıl | ≥ 1 savaş, ≥ 1 isyan, ≥ 1 plot sonucu, ≥ 1 kral değişimi |
| 2 | Veraset kuralları | Her `succ_*` için birim testi |
| 3 | Sadakat formülü | Bilinen girdilerle beklenen değer |
| 4 | Fetih | Savunmasız şehir el değiştirir; sakinsiz şehir yok edilir |
| 5 | Dil bölünmesi | Uzak şehirlerde 200 yılda yeni dil (5 seed'in 3'ü) |
| 6 | İsim üretici | 10.000 isim, çakışma < %2, tüm karakterler font setinde |
| 7 | Tarihçe | Kaydet/yükle sonrası girişler aynı |
| 8 | 20 krallık, 5.000 birim | Meta sistemleri aylık tick < 15 ms (tek tick'e yayılmış iş bütçesiyle) |

## 6.16 Görev listesi

| # | Görev | Süre |
|---|-------|------|
| 1 | `MetaStore` tamamlama, istatistik serileri, dağılım | 2 gün |
| 2 | İsim üretici + `NamePool` | 1,5 gün |
| 3 | Arma üretimi ve atlas | 1,5 gün |
| 4 | Krallık, veraset, sadakat, fikir | 3 gün |
| 5 | Savaş ilanı, ordular, kuşatma, fetih, barış | 5 gün |
| 6 | İttifaklar | 1 gün |
| 7 | Kültür yayılımı ve efektleri | 2 gün |
| 8 | Dil, lehçe, yazı | 1,5 gün |
| 9 | Din, tapınak, din büyüleri | 2,5 gün |
| 10 | Aile, sürü, klan | 1,5 gün |
| 11 | Plot çatısı + 16 plot sonucu | 4 gün |
| 12 | Kitaplar, kütüphane | 1,5 gün |
| 13 | Tarihçe | 1,5 gün |
| 14 | Testler | 2,5 gün |
| | **Toplam** | **~31 gün** |
