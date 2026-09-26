#!/usr/bin/env python3
"""Creates missing Unity .meta files (random GUID) for new files/folders under Assets/, so they are tracked in git
before the project is opened in Unity. Unity keeps a meta it finds; it only generates one when it is missing."""
import os, uuid, sys
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets")
made = []
for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if not d.startswith(".") and d not in ("StreamingAssets",)]
    for d in dirnames:
        m = os.path.join(dirpath, d + ".meta")
        if not os.path.exists(m):
            open(m, "w").write(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
            made.append(m)
    for f in filenames:
        if f.endswith(".meta") or f.startswith("."): continue
        m = os.path.join(dirpath, f + ".meta")
        if os.path.exists(m): continue
        open(m, "w").write(f"fileFormatVersion: 2\nguid: {uuid.uuid4().hex}")
        made.append(m)
for m in made: print("created", os.path.relpath(m, os.path.join(ROOT, "..")))
