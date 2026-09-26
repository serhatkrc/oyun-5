import json, os
OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
os.makedirs(OUT, exist_ok=True)
REG = {}

def mods(s):
    """'hp:+20|dmg:+15%|flag:fly' -> dict"""
    d = {}
    if not s: return d
    for p in s.split("|"):
        p = p.strip()
        if not p: continue
        k, v = p.split(":", 1)
        if k in ("flag", "tag", "grant", "immune", "neuron", "spell", "event"):
            d.setdefault(k + "s", []).append(v)
        elif v.endswith("%") and v[:-1].lstrip("+-").replace(".","",1).isdigit():
            d.setdefault("pct", {})[k] = float(v[:-1])
        else:
            try: d.setdefault("add", {})[k] = float(v)
            except ValueError: d[k] = v
    return d

def ids(s):
    return [x.strip() for x in s.split(",") if x.strip()] if s else []

def save(name, items, title, cols):
    for it in items:
        assert "id" in it, it
    keys = [i["id"] for i in items]
    dup = {k for k in keys if keys.count(k) > 1}
    assert not dup, (name, dup)
    REG[name] = (title, cols, items)
    with open(f"{OUT}/{name}.json", "w", encoding="utf-8", newline="\n") as f:
        json.dump({"category": name, "count": len(items), "items": items}, f, ensure_ascii=False, indent=1)
    print(f"{name}: {len(items)}")
