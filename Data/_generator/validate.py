import json,glob,os
from lib import OUT
D={os.path.basename(f)[:-5]:json.load(open(f,encoding="utf-8")) for f in glob.glob(OUT+"/*.json")}
ID=set()
for k,v in D.items():
    if "items" in v: ID|={i["id"] for i in v["items"]}
err=[]
def chk(x,ctx):
    if x and x not in ID: err.append(f"{ctx}: {x}")
for t in D["tiles"]["items"]:
    for k in ("onRaiseBecomes","onLowerBecomes","freezeTo","meltTo","onBurnBecomes","recoverTo","decayTo","decayToIfHigh","quenchTo","decayFeature"):
        chk(t.get(k),t["id"]+"."+k)
    if len(t["colors"])!=4: err.append(f"{t['id']}: colors must have 4 variants")
for ft in D["features"]["items"]:
    chk(ft.get("burnsInto"),ft["id"]+".burnsInto")
for e in D["eras"]["items"]:
    if not (e["minYears"]<=e["maxYears"]): err.append(f"{e['id']}: minYears > maxYears")
slots=[e["defaultSlot"] for e in D["eras"]["items"] if e["defaultSlot"]>=0]
if len(slots)!=len(set(slots)) or any(x>7 for x in slots): err.append("eras: defaultSlot must be unique 0..7")
for w in D["worldgen_templates"]["items"]:
    chk(w.get("flatTile"),w["id"]+".flatTile"); chk(w.get("flatBiome"),w["id"]+".flatBiome")
for bt in D["biome_table"]["items"]:
    if len(bt["grid"])!=6 or any(len(r)!=6 for r in bt["grid"]): err.append(f"{bt['id']}: grid must be 6x6")
    for r in bt["grid"]:
        for x in r: chk(x,bt["id"]+".grid")
    for x in bt["patches"]: chk(x,bt["id"]+".patches")
for b in D["biomes"]["items"]:
    for f in ("trees","plants","animals","lifeCloudCivs","unitTraitPool","culturePool","languagePool","religionPool"):
        for x in b[f]: chk(x,b["id"]+"."+f)
for s in D["species"]["items"]:
    for g in s["abilities"].get("grants",[]): chk("tr."+g,s["id"])
    for g in s["abilities"].get("spells",[]): chk("spell."+g,s["id"])
for f in ("unit_traits","subspecies_traits","religion_traits","clan_traits","culture_traits"):
    for t in D[f]["items"]:
        for g in t["effects"].get("spells",[]): chk("spell."+g,t["id"])
        if t.get("opposite"): chk(t["opposite"],t["id"])
for d in D["disasters"]["items"]:
    for e in d["eraMultipliers"]: chk(e,d["id"])
    chk(d.get("power"),d["id"]+".power")
    if d["placement"].startswith("biomeOrLand:"): chk(d["placement"].split(":",1)[1],d["id"]+".placement")
for c in D["clouds"]["items"]:
    chk(c.get("dropTile"),c["id"]+".dropTile"); chk(c.get("dropFeature"),c["id"]+".dropFeature")
for p in D["powers"]["items"]:
    if p["unlockedBy"]: chk(p["unlockedBy"],p["id"])
    for k in ("species",):
        if k in p["params"]: chk(p["params"][k],p["id"])
for e in D["evolution_rules"]["items"]:
    for g in e["grants"]: chk(g,e["id"])
for m in D["metamorphoses"]["items"]:
    if not m["from"].startswith("*"): chk(m["from"],m["id"])
    chk(m["to"],m["id"])
for s in D["gene_synergies"]["items"]:
    for g in s["requires"]: chk(g,s["id"])
print("\n".join(err) if err else "Tüm referanslar geçerli"); print(len(err),"hata")
