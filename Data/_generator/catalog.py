import json,os
from lib import OUT
ORDER=[("Dünya",["tiles","worldgen_templates","biome_table","biomes","features","resources","clouds","eras","disasters"]),
("Canlılar",["species","unit_traits","status_effects","diseases","spells","equipment_types","materials","item_qualities"]),
("Biyoloji",["subspecies_traits","genes","gene_synergies","phenotypes","evolution_rules","metamorphoses"]),
("Medeniyet ve Meta",["buildings","building_styles","jobs","culture_traits","religion_traits","language_traits","clan_traits","kingdom_traits","plots","war_types","book_types","happiness_events"]),
("Tanrı Araçları",["powers","world_laws","achievements"])]
COLS={"tiles":["id","name","level","walkable","water","moveCost","onRaiseBecomes","onLowerBecomes","note"],"worldgen_templates":["id","name","landRatioDefault","edgeOcean"],"biome_table":["id","patches"],"biomes":["id","name","growStrength","animals","effect","description"],
"features":["id","name","kind","yields","note"],"resources":["id","name","kind","nutrition","value","source"],"clouds":["id","name","effect"],
"eras":["id","name","minYears","maxYears","fertilityPct","tempShift","effects"],"disasters":["id","name","minWorldAge","cooldownYears","chancePerYear","description"],
"species":["id","name","category","diet","stats","reproduction","note"],"unit_traits":["id","name","group","rarity","effects","opposite"],
"status_effects":["id","name","durationMonths","effect","note"],"diseases":["id","name","contagion","lethality","transformsInto","note"],
"spells":["id","name","manaCost","cooldownTicks","range","effect"],"equipment_types":["id","name","slot","baseEffects"],"materials":["id","name","multiplier","tier","special"],
"item_qualities":["id","name","multiplier","baseChance"],"subspecies_traits":["id","name","group","effects"],"genes":["id","name","effects"],
"gene_synergies":["id","name","requires","bonus"],"phenotypes":["id","name","baseColor","pattern","biomeBias"],"evolution_rules":["id","name","stage","grants","note"],
"metamorphoses":["id","from","to","trigger","note"],"buildings":["id","name","category","size","hp","cost","requires","function"],
"building_styles":["id","name","description"],"jobs":["id","name","task","requiresBuilding"],"culture_traits":["id","name","group","effects"],
"religion_traits":["id","name","group","effects"],"language_traits":["id","name","group","effects"],"clan_traits":["id","name","effects"],
"kingdom_traits":["id","name","effects"],"plots":["id","name","initiator","conditions","minParticipants","durationMonths","outcome"],
"war_types":["id","name"],"book_types":["id","name","readerBonus"],"happiness_events":["id","name","value"],
"powers":["id","name","type","unlockedBy","description"],"world_laws":["id","name","group","default"],"achievements":["id","name","condition","unlocks"]}
TITLES={}
def fmt(v):
    if isinstance(v,bool): return "✔" if v else "–"
    if v is None: return ""
    if isinstance(v,list): return ", ".join(x.split(".",1)[-1] if isinstance(x,str) else str(x) for x in v)
    if isinstance(v,dict):
        parts=[]
        for k,x in v.items():
            if k=="add": parts+= [f"{a} {'+' if b>0 else ''}{b:g}" for a,b in x.items()]
            elif k=="pct": parts+= [f"{a} {'+' if b>0 else ''}{b:g}%" for a,b in x.items()]
            elif isinstance(x,list): parts+= [f"{k[:-1]}:{i}" for i in x]
            elif isinstance(x,dict): parts+= [f"{a}={b}" for a,b in x.items()]
            else: parts.append(f"{k}:{x:g}" if isinstance(x,(int,float)) else f"{k}:{x}")
        return ", ".join(parts)
    return str(v).replace("|","/")
out=["# EK A — Oyun İçerik Kataloğu\n",
"Bu katalog `Data/` klasöründeki JSON dosyalarından otomatik üretildi. Oyun içerikleri bu JSON'lardan okur (Bölüm 1.7 ContentDB). İçerik eklemek/değiştirmek için JSON'u düzenle; bu katalog `catalog.py` ile yeniden üretilir.\n",
"**Efekt kısaltmaları:** `hp` can, `dmg` hasar, `armor` zırh, `speed` hız, `atkspd` saldırı hızı, `crit` kritik, `range` menzil, `intel` zekâ, `diplo` diplomasi, `warfare` savaşçılık, `steward` yönetim, `luck` şans, `neuron:x` AI'ye karar düğümü ekler, `flag:x` davranış bayrağı, `immune:x` bağışıklık, `spell:x` büyü verir, `onHit:x:n` vuruşta n% ihtimalle etki.\n",
"**Kural:** Bütün isimler özgündür; referans oyunun isim, sprite veya metinleri kullanılmaz.\n","## Özet\n","| Kategori | Dosya | Adet |","|---|---|---|"]
data={}
for grp,files in ORDER:
    for f in files:
        d=json.load(open(f"{OUT}/{f}.json",encoding="utf-8")); data[f]=d
from lib import REG
import importlib
titles={}
for grp,files in ORDER:
    for f in files: pass
T={"tiles":"Tile Tipleri","worldgen_templates":"Harita Şablonları","biome_table":"Biyom Tablosu","biomes":"Biyomlar","features":"Ağaç, Bitki ve Damarlar","resources":"Kaynaklar","clouds":"Bulutlar","eras":"Çağlar","disasters":"Otomatik Afetler",
"species":"Türler ve Yaratıklar","unit_traits":"Birim Trait'leri","status_effects":"Statü Efektleri","diseases":"Hastalıklar","spells":"Büyüler","equipment_types":"Ekipman Tipleri",
"materials":"Malzemeler","item_qualities":"Eşya Kaliteleri","subspecies_traits":"Alt Tür Trait'leri","genes":"Genler","gene_synergies":"Gen Sinerjileri","phenotypes":"Fenotipler",
"evolution_rules":"Evrim Kuralları","metamorphoses":"Dönüşümler","buildings":"Binalar","building_styles":"Bina Stilleri","jobs":"Meslekler","culture_traits":"Kültür Trait'leri",
"religion_traits":"Din Trait'leri","language_traits":"Dil Trait'leri","clan_traits":"Klan Trait'leri","kingdom_traits":"Krallık Trait'leri","plots":"Plotlar","war_types":"Savaş Türleri",
"book_types":"Kitap Türleri","happiness_events":"Mutluluk Olayları","powers":"Tanrı Güçleri","world_laws":"Dünya Kanunları","achievements":"Başarımlar"}
total=0
for grp,files in ORDER:
    for f in files: out.append(f"| {T[f]} | `{f}.json` | {data[f]['count']} |"); total+=data[f]['count']
out.append(f"| **Toplam** | | **{total}** |\n")
out.append("Ek dosyalar: `zombie_rules.json` (zombi salgını kuralları), `name_sets.json` (dil bazlı isim üretici heceleri ve unvanlar), `gene_rules.json` (kromozom ve kalıtım kuralları).\n")
n=0
for grp,files in ORDER:
    out.append(f"\n---\n\n# {grp}\n")
    for f in files:
        n+=1; items=data[f]["items"]; cols=COLS[f]
        out.append(f"## {T[f]} ({len(items)})\n")
        if f=="powers":
            for tab,tn in [("world","Dünya Yaratma"),("civ","Medeniyetler"),("creatures","Yaratıklar"),("nature","Doğa ve Afetler"),("destruction","Yıkım"),("other","Diğer")]:
                sub=[i for i in items if i["tab"]==tab]; out.append(f"### {tn} ({len(sub)})\n")
                out.append("| "+" | ".join(cols)+" |"); out.append("|"+"---|"*len(cols))
                for it in sub: out.append("| "+" | ".join(fmt(it.get(c)) for c in cols)+" |")
                out.append("")
            continue
        out.append("| "+" | ".join(cols)+" |"); out.append("|"+"---|"*len(cols))
        for it in items: out.append("| "+" | ".join(fmt(it.get(c)) for c in cols)+" |")
        out.append("")
        if f=="subspecies_traits": out.append("> Medenileşme şartı: `sapience` + `tool_use`. Kültür tutabilmek için `advanced_memory`, dil için `speech_center`, din için `abstract_thought`, plot kurabilmek için `planning` gerekir. `canAppearRandomly=false` olanlar yalnızca editör veya özel olaylarla gelir.\n")
open(os.path.join(OUT,"..","Docs","EkA_Icerik_Katalogu.md"),"w",encoding="utf-8").write("\n".join(out))
print(total)
