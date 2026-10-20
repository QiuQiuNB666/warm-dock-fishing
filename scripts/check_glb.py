#!/usr/bin/env python3
"""Phase 1 test: check_glb.py [dir=assets_raw]. Every glb has glTF header + parsable JSON chunk;
rock/boat/cabin present; character = character.glb or character_*.glb with >=2 animations in total; total <50MB."""
import glob, json, os, struct, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def glb_json(path):
    with open(path, "rb") as f:
        head = f.read(20)
        magic, _, _, clen, ctype = struct.unpack("<4sIIII", head)
        if magic != b"glTF" or ctype != 0x4E4F534A:  # 'JSON'
            raise ValueError("bad header")
        return json.loads(f.read(clen))


def check(d):
    files = sorted(glob.glob(os.path.join(d, "*.glb")))
    names = {os.path.basename(p)[:-4] for p in files}
    missing = [n for n in ("rock", "boat", "cabin") if n not in names]
    chars = [p for p in files if os.path.basename(p).startswith("character")]
    if not chars:
        missing.append("character")
    if missing:
        return f"missing {missing}"
    anims = 0
    for p in files:
        try:
            j = glb_json(p)
        except Exception as e:
            return f"{os.path.basename(p)}: {e}"
        if p in chars:
            anims += len(j.get("animations", []))
    if anims < 2:
        return f"character animations={anims} (<2)"
    mb = sum(os.path.getsize(p) for p in files) / 2**20
    if mb >= 50:
        return f"total {mb:.1f}MB >= 50MB"
    return None


if __name__ == "__main__":
    err = check(sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "assets_raw"))
    print("CHECK-GLB " + (f"FAIL: {err}" if err else "PASS"))
    sys.exit(1 if err else 0)
