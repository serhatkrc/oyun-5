"""PixelGenesis geçici (placeholder) sanat üreticisi.
Data/*.json dosyalarından her tür, bina, feature, güç, trait ve statü için
EK C'deki boyut ve indeks kurallarına uygun geçici sprite'lar üretir.
Kullanım: python Tools/placeholder_art.py  (depo kökünden)
Çıktı:   Art/Placeholder/{units,buildings,features,icons}/*.png + *.json, preview.png, asset_list.csv
"""
import json, os, csv, hashlib, math
from PIL import Image, ImageDraw

def _norm(fn):
    def w(self, xy, *a, **k):
        if len(xy) == 4 and not isinstance(xy[0], tuple):
            x0, y0, x1, y1 = xy; xy = [min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1)]
        return fn(self, xy, *a, **k)
    return w
for _n in ("rectangle", "ellipse", "rounded_rectangle"):  # küçük sprite'larda ters koordinatlara karşı
    setattr(ImageDraw.ImageDraw, _n, _norm(getattr(ImageDraw.ImageDraw, _n)))

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(ROOT, "Data")
OUT = os.path.join(ROOT, "Art", "Placeholder")
def load(n): return json.load(open(os.path.join(DATA, n + ".json"), encoding="utf-8"))["items"]
def h(s, n): return int(hashlib.md5(s.encode()).hexdigest(), 16) % n

# ---- İndeks kodlaması (EK C 4): gri = indeks * 28, alfa 0 = boş
OUTLINE, SHADOW, BASE, LIGHT, CLOTH_D, CLOTH, ACCENT, SHINE = range(1, 9)
def g(i): return (i * 28, i * 28, i * 28, 255)

# ---- Animasyon tanımı (EK C 5)
ANIMS = [("idle", 2, 400), ("walk", 4, 120), ("attack", 3, 100), ("swim", 2, 250), ("sleep", 1, 1000), ("death", 3, 150)]

BUG = {"bee", "ant", "beetle", "scorpion", "crystal_beetle", "void_moth", "butterfly", "caterpillar", "snail", "mosquito_swarm", "crab", "clock_crab", "bone_crawler"}
SERPENT = {"snake", "sandworm"}
BLOB = {"slime", "flesh_mound", "devourer", "candy_golem", "gummy_bear", "shroomling"}
BIPED_CATS = {"civ", "undead", "magic", "special", "elemental"}

def body_plan(sp):
    k = sp["id"][3:]
    if k in BLOB: return "blob"
    if k in SERPENT: return "serpent"
    if k in BUG: return "bug"
    if sp["habitat"] == "water" or k in ("fish", "piranha", "kraken"): return "fish"
    if sp["habitat"] == "air" and sp["category"] not in BIPED_CATS: return "bird"
    if k in ("dragon", "zombie_dragon"): return "dragon"
    if sp["category"] in BIPED_CATS or k in ("monkey", "penguin", "walking_tree", "zombie_beast") and k != "zombie_beast": return "biped"
    return "quad"

SIZES = {  # plan -> boy(1..5) -> (w,h)
 "biped": {1: (3, 5), 2: (4, 7), 3: (5, 8), 4: (7, 10), 5: (12, 14)},
 "quad":  {1: (4, 3), 2: (6, 4), 3: (8, 5), 4: (10, 7), 5: (16, 10)},
 "bird":  {1: (4, 3), 2: (6, 4), 3: (8, 5), 4: (10, 6), 5: (18, 10)},
 "bug":   {1: (3, 2), 2: (4, 3), 3: (5, 3), 4: (7, 4), 5: (9, 5)},
 "fish":  {1: (3, 2), 2: (5, 3), 3: (7, 4), 4: (10, 5), 5: (16, 9)},
 "serpent": {1: (5, 2), 2: (6, 2), 3: (8, 3), 4: (12, 4), 5: (20, 6)},
 "blob":  {1: (3, 3), 2: (4, 4), 3: (6, 5), 4: (8, 7), 5: (12, 10)},
 "dragon": {5: (20, 14), 4: (16, 12), 3: (12, 9), 2: (8, 6), 1: (6, 4)},
}

def draw_frame(plan, w, h, anim, f, seed):
    im = Image.new("RGBA", (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    bob = 1 if (anim == "walk" and f % 2) else 0
    if anim == "death":
        rot = f  # yere yatma: gövdeyi basıklaştır
        hh = max(2, h - rot * (h // 3)); d.rectangle([0, h - hh, w - 1, h - 1], fill=g(SHADOW))
        d.rectangle([1, h - hh + 1, w - 2, h - 2], fill=g(BASE)); return im
    if anim == "sleep":
        d.rectangle([0, h - max(2, h // 2), w - 1, h - 1], fill=g(BASE)); d.point((w - 2, h - max(2, h // 2)), fill=g(ACCENT)); return im
    if plan == "biped":
        head = max(2, w // 2); hx = (w - head) // 2
        d.rectangle([hx, 0 + bob, hx + head - 1, head - 1 + bob], fill=g(LIGHT))           # baş
        d.point((hx + head - 1, 1 + bob), fill=g(ACCENT))                                  # göz
        d.rectangle([0, head + bob, w - 1, h - 3], fill=g(CLOTH))                          # gövde (krallık rengi)
        d.line([0, head + bob, 0, h - 3], fill=g(CLOTH_D))
        legs = [(1, w - 2), (0, w - 1), (1, w - 2), (2, w - 3)][f % 4] if anim == "walk" else (1, w - 2)
        d.line([legs[0], h - 2, legs[0], h - 1], fill=g(SHADOW)); d.line([legs[1], h - 2, legs[1], h - 1], fill=g(SHADOW))
        if anim == "attack": d.line([w - 1, head + bob - f, w - 1, head + 2 + bob], fill=g(SHINE))  # silah
        if anim == "swim": im = im.crop((0, 0, w, h)); ImageDraw.Draw(im).rectangle([0, h - 3, w - 1, h - 1], fill=(0, 0, 0, 0))
    elif plan in ("quad", "dragon"):
        bh = max(2, h - 2)
        d.rectangle([0, 1 + bob, w - 3, bh], fill=g(BASE)); d.line([0, 1 + bob, w - 3, 1 + bob], fill=g(LIGHT))
        d.rectangle([w - 3, 0 + bob, w - 1, 2 + bob], fill=g(LIGHT)); d.point((w - 1, 1 + bob), fill=g(ACCENT))
        step = f % 4 if anim == "walk" else 0
        for i, x in enumerate([1, w - 4]):
            off = (1 if (step + i) % 2 else 0)
            d.line([x + off, bh + 1, x + off, h - 1], fill=g(SHADOW))
        if plan == "dragon":
            wing = 1 if f % 2 else 0
            d.polygon([(w // 3, 1), (w // 2, -2 + wing + 3), (2 * w // 3, 1)], fill=g(CLOTH))
    elif plan == "bird":
        up = f % 2 if anim in ("walk", "idle") else 0
        d.rectangle([w // 3, h // 3, 2 * w // 3, h - 1], fill=g(BASE))
        wy = 0 if up else h // 3
        d.line([0, wy, w // 3, h // 3], fill=g(LIGHT)); d.line([w - 1, wy, 2 * w // 3, h // 3], fill=g(LIGHT))
        d.point((2 * w // 3, h // 3), fill=g(ACCENT))
    elif plan == "fish":
        d.ellipse([0, 0, w - 2, h - 1], fill=g(BASE)); d.point((w - 3, h // 2 - 1 if h > 2 else 0), fill=g(ACCENT))
        t = h // 2 + (1 if f % 2 else 0); d.line([w - 2, t - 1, w - 1, t], fill=g(SHADOW))
    elif plan == "bug":
        d.rectangle([0, 0, w - 1, h - 1], fill=g(BASE)); d.line([0, 0, w - 1, 0], fill=g(LIGHT)); d.point((w - 1, 0), fill=g(ACCENT))
    elif plan == "serpent":
        for x in range(w):
            y = int((h - 1) / 2 + math.sin((x + f * 2) / 2.0) * (h - 1) / 2)
            d.point((x, y), fill=g(BASE if x < w - 1 else LIGHT))
    elif plan == "blob":
        squish = 1 if f % 2 else 0
        d.ellipse([0, squish, w - 1, h - 1], fill=g(BASE)); d.point((w // 2, h // 3 + squish), fill=g(ACCENT))
        d.line([1, 1 + squish, w // 2, 1 + squish], fill=g(SHINE))
    return im

def unit_sheet(sp):
    plan = body_plan(sp); size = max(1, min(5, sp["stats"]["size"] or 2))
    w, h = SIZES[plan][size]
    cols = max(n for _, n, _ in ANIMS)
    sheet = Image.new("RGBA", (w * cols, h * len(ANIMS)), (0, 0, 0, 0))
    frames, tags, idx = [], [], 0
    for r, (name, n, dur) in enumerate(ANIMS):
        tags.append({"name": name, "from": idx, "to": idx + n - 1, "direction": "forward"})
        for f in range(n):
            fr = draw_frame(plan, w, h, name, f, sp["id"])
            sheet.paste(fr, (f * w, r * h), fr)
            frames.append({"filename": f"{name}_{f}", "frame": {"x": f * w, "y": r * h, "w": w, "h": h}, "duration": dur}); idx += 1
    meta = {"frames": frames, "meta": {"frameTags": tags, "size": {"w": sheet.width, "h": sheet.height}, "plan": plan, "placeholder": True}}
    return sheet, meta, (w, h), plan

# ---- Binalar (EK C 6): 1 hücre = 3x3 tile/piksel
STYLES = {"human": ("pitched", 5), "elf": ("round", 3), "dwarf": ("flat", 7), "orc": ("spike", 2), "beast": ("mound", 4), "insect": ("hex", 6)}
def building_sprite(b, style, state):
    cw, ch = [int(x) for x in b["size"].split("x")] if "x" in b["size"] else (2, 2)
    w, fh = cw * 3, ch * 3; roof = max(2, fh // 2); H = fh + roof
    im = Image.new("RGBA", (w, H), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    shape, _ = STYLES[style]
    if state == "ruin":
        for x in range(0, w, 2): d.line([x, H - 1 - h(b["id"] + str(x), 4), x, H - 1], fill=g(SHADOW))
        return im
    d.rectangle([0, roof, w - 1, H - 1], fill=g(BASE)); d.rectangle([0, roof, w - 1, H - 1], outline=g(OUTLINE))
    dx = w // 2; d.rectangle([dx - 1, H - 3, dx, H - 1], fill=g(SHADOW))                    # kapı
    if w >= 6: d.point((2, roof + 2), fill=g(SHINE)); d.point((w - 3, roof + 2), fill=g(SHINE))  # pencere
    if shape == "pitched": d.polygon([(0, roof), (w // 2, 0), (w - 1, roof)], fill=g(CLOTH))
    elif shape == "round": d.ellipse([0, 0, w - 1, roof * 2], fill=g(CLOTH))
    elif shape == "flat": d.rectangle([0, roof - 2, w - 1, roof], fill=g(LIGHT))
    elif shape == "spike":
        for x in range(0, w, 3): d.line([x, roof, x + 1, 0], fill=g(CLOTH))
    elif shape == "mound": d.ellipse([0, 1, w - 1, H - 1], fill=g(BASE)); d.rectangle([dx - 1, H - 3, dx, H - 1], fill=g(OUTLINE))
    elif shape == "hex":
        d.polygon([(w // 4, 0), (3 * w // 4, 0), (w - 1, H // 2), (3 * w // 4, H - 1), (w // 4, H - 1), (0, H // 2)], fill=g(CLOTH))
    if state == "construction":
        for x in range(0, w, 3): d.line([x, 0, x, H - 1], fill=g(LIGHT))
        for y in range(0, H, 3): d.line([0, y, w - 1, y], fill=g(LIGHT))
    if b["category"] == "center": d.line([w - 1, 0, w - 1, roof], fill=g(OUTLINE)); d.point((w - 2, 0), fill=g(CLOTH_D))  # bayrak
    return im

# ---- Feature'lar (RGB, biyom rengiyle çizilir)
# Readable placeholder hues (degrees, saturation) by id keyword; everything else: green foliage with a small hash shift.
FEATURE_HUES = [("candy", 330, 0.6), ("ember", 20, 0.8), ("volcanic", 15, 0.25), ("snowpine", 150, 0.25), ("dead", 30, 0.35),
                ("ash", 30, 0.05), ("crystal", 185, 0.6), ("rot", 280, 0.35), ("bone", 45, 0.25), ("honey", 42, 0.75),
                ("cloud", 200, 0.15), ("void", 265, 0.5), ("fairy", 300, 0.45), ("hourglass", 40, 0.45), ("mushroom", 12, 0.55),
                ("coral", 350, 0.6), ("flower", 320, 0.6), ("berry", 350, 0.55), ("garlic", 60, 0.2), ("cactus", 110, 0.45),
                ("salt", 200, 0.05), ("wheat", 48, 0.7), ("reed", 85, 0.45), ("gold", 48, 0.8), ("silver", 210, 0.1),
                ("copper", 25, 0.65), ("iron", 15, 0.3), ("obsidian", 270, 0.3), ("skyiron", 200, 0.55), ("starore", 55, 0.9)]

def feature_hue(fid):
    for key, hue, sat in FEATURE_HUES:
        if key in fid: return hue, sat
    return 100 + h(fid, 40), 0.45

def feature_sprite(f):
    k = f["kind"]; c, sat = feature_hue(f["id"])
    col = tuple(int(v) for v in hsl(c / 360, sat, 0.35)) + (255,)
    lite = tuple(int(v) for v in hsl(c / 360, sat, 0.55)) + (255,)
    if k == "tree":
        im = Image.new("RGBA", (5, 7), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
        d.line([2, 4, 2, 6], fill=(92, 64, 40, 255)); d.ellipse([0, 0, 4, 4], fill=col); d.point((1, 1), fill=lite)
    elif k == "ore":
        im = Image.new("RGBA", (3, 3), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
        d.rectangle([0, 1, 2, 2], fill=(110, 110, 110, 255)); d.point((1, 1), fill=lite); d.point((2, 0), fill=lite)
    else:
        im = Image.new("RGBA", (3, 3), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
        d.point((1, 2), fill=col); d.point((0, 1), fill=col); d.point((2, 1), fill=col); d.point((1, 0), fill=lite)
    return im

def hsl(hh, s, l):
    import colorsys; r, g_, b = colorsys.hls_to_rgb(hh, l, s); return (r * 255, g_ * 255, b * 255)

# ---- 3x5 piksel font (ikon harfleri)
FONT = {c: v for c, v in zip("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", [
 "111101111101101","110101110101110","111100100100111","110101101101110","111100110100111","111100110100100","111100101101111","101101111101101",
 "111010010010111","001001001101111","101110100110101","100100100100111","101111111101101","111101101101101","111101101101111","111101111100100",
 "111101101111001","111101110101101","111100111001111","111010010010010","101101101101111","101101101101010","101101111111101","101101010101101",
 "101101111010010","111001010100111","111101101101111","010110010010111","111001111100111","111001111001111","101101111001001","111100111001111",
 "111100111101111","111001001001001","111101111101111","111101111001111"])}
def text(d, s, x, y, col):
    for i, ch in enumerate(s.upper()[:3]):
        bits = FONT.get(ch)
        if not bits: continue
        for j, b in enumerate(bits):
            if b == "1": d.point((x + i * 4 + j % 3, y + j // 3), fill=col)

TAB_COL = {"world": (86, 142, 60), "civ": (186, 120, 60), "creatures": (180, 90, 120), "nature": (60, 120, 180), "destruction": (190, 60, 50), "other": (110, 110, 130)}
RAR_COL = {"normal": (140, 140, 140), "rare": (70, 130, 210), "epic": (150, 80, 200), "legendary": (230, 170, 40)}
def icon(size, label, bg, frame=None):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=2, fill=bg + (255,), outline=(frame or (30, 30, 30)) + (255,))
    words = [w for w in label.replace("_", " ").split() if w]
    lab = "".join(w[0] for w in words[:3]) if len(words) > 1 else label[:3]
    n = min(3, len(lab)); tw = n * 4 - 1
    text(d, lab, (size - tw) // 2, (size - 5) // 2, (250, 250, 250, 255)); return im

PAL = [(0, 0, 0, 0), (30, 24, 28), (70, 52, 44), (120, 90, 70), (170, 132, 100), (40, 60, 120), (70, 100, 180), (230, 60, 40), (250, 240, 200)]
def colorize(im):
    out = Image.new("RGBA", im.size, (0, 0, 0, 0)); px, po = im.load(), out.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g_, b, a = px[x, y]
            if a: po[x, y] = PAL[min(8, max(1, round(r / 28)))] + (255,)
    return out

def main():
    for sub in ("units", "buildings", "features", "icons/powers", "icons/traits", "icons/status"):
        os.makedirs(os.path.join(OUT, sub), exist_ok=True)
    rows, previews = [], []
    for sp in load("species"):
        sheet, meta, (w, h_), plan = unit_sheet(sp)
        base = os.path.join(OUT, "units", sp["id"][3:])
        sheet.save(base + ".png"); json.dump(meta, open(base + ".json", "w"), indent=1)
        rows.append(["unit", sp["id"], sp["name"], f"{w}x{h_}", plan, "idle2 walk4 attack3 swim2 sleep1 death3", 3])
        previews.append(colorize(sheet.crop((0, h_, w, h_ * 2))))
    blds = load("buildings")
    for style in STYLES:
        for b in blds:
            if b["id"] == "bld.ruins": continue
            for state in ("construction", "complete", "ruin"):
                im = building_sprite(b, style, state)
                im.save(os.path.join(OUT, "buildings", f"{style}_{b['id'][4:]}_{state}.png"))
            rows.append(["building", b["id"], b["name"], f"{im.width}x{im.height}", style, "construction complete ruin", 4])
    for f in load("features"):
        im = feature_sprite(f); im.save(os.path.join(OUT, "features", f["id"][5:] + ".png"))
        rows.append(["feature", f["id"], f["name"], f"{im.width}x{im.height}", f["kind"], "evre 0-3 (ölçek)", 2])
    for p in load("powers"):
        icon(16, p["id"][3:], TAB_COL[p["tab"]]).save(os.path.join(OUT, "icons/powers", p["id"][3:] + ".png"))
        rows.append(["icon_power", p["id"], p["name"], "16x16", p["tab"], "-", 1])
    for fn, frame_key in [("unit_traits", "rarity"), ("subspecies_traits", None), ("culture_traits", None), ("religion_traits", None),
                          ("language_traits", None), ("clan_traits", None), ("kingdom_traits", None), ("genes", None)]:
        for t in load(fn):
            fr = RAR_COL.get(t.get("rarity", "normal"))
            icon(12, t["id"].split(".", 1)[1], tuple(int(v) for v in hsl(h(fn, 360) / 360, 0.35, 0.3)), fr).save(
                os.path.join(OUT, "icons/traits", t["id"].replace(".", "_") + ".png"))
            rows.append(["icon_trait", t["id"], t["name"], "12x12", fn, "-", 9])
    for s in load("status_effects"):
        icon(7, s["id"][3:4], (60, 60, 60)).save(os.path.join(OUT, "icons/status", s["id"][3:] + ".png"))
        rows.append(["icon_status", s["id"], s["name"], "7x7", "-", "-", 3])
    with open(os.path.join(OUT, "asset_list.csv"), "w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh); w.writerow(["kategori", "id", "ad", "boyut_px", "tip/stil", "animasyon/durum", "ilk_gereken_faz"]); w.writerows(rows)
    # önizleme: ilk 40 birimin yürüme karesi, 6x büyütülmüş
    sc, pad = 8, 6; sel = previews[:48]; cols = 12
    cw = max(p.width for p in sel) * sc + pad; ch = max(p.height for p in sel) * sc + pad
    pv = Image.new("RGBA", (cw * cols, ch * math.ceil(len(sel) / cols)), (46, 52, 40, 255))
    for i, p in enumerate(sel):
        big = p.resize((p.width * sc, p.height * sc), Image.NEAREST); pv.paste(big, ((i % cols) * cw + 2, (i // cols) * ch + 2), big)
    pv.save(os.path.join(OUT, "preview_units.png"))
    bp = [building_sprite(b, s, "complete") for s in STYLES for b in blds if b["id"] in ("bld.hut", "bld.house", "bld.hall_2", "bld.temple")]
    bw = max(i.width for i in bp) * 4 + 6; bh = max(i.height for i in bp) * 4 + 6
    pv2 = Image.new("RGBA", (bw * 4, bh * len(STYLES)), (46, 52, 40, 255))
    for i, im in enumerate(bp):
        c = colorize(im).resize((im.width * 4, im.height * 4), Image.NEAREST); pv2.paste(c, ((i % 4) * bw + 3, (i // 4) * bh + 3), c)
    pv2.save(os.path.join(OUT, "preview_buildings.png"))
    print(len(rows), "varlık üretildi")

if __name__ == "__main__":
    main()
