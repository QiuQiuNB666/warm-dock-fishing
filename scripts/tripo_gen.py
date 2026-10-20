#!/usr/bin/env python3
"""Phase 1: Tripo API v3 text-to-model (+ animate_rig -> animate_retarget for character) -> assets_raw/<name>.glb.
Usage: tripo_gen.py [names...] [--dry-run]   (names default: rock boat cabin character)
Exit: 0 ok / 1 item failed after 3 tries / 2 BLOCKED (401/402/429, 2x 5xx/timeout in a row)."""
import json, os, subprocess, sys, time, urllib.error, urllib.request

API = "https://openapi.tripo3d.com/v3"  # v2 sunsets 2026-11-01; v3 = one endpoint per task type, no "type" field
PATHS = {"text_to_model": "/generation/text-to-model", "animate_rig": "/animations/rig",
         "animate_retarget": "/animations/retarget"}
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SUFFIX = "stylized low-poly game asset, warm storybook palette, soft turquoise sea, clean topology, no text, no watermark"
MODEL_VERSION = "v2.5-20250123"  # API default; rig needs >= v2.0-20240919
ITEMS = {
    "rock": ("a weathered coastal rock", 3000),
    "boat": ("a small wooden rowing boat, empty hull interior", 5000),
    "cabin": ("a small seaside wooden cabin", 8000),
    "character": ("a fisherman wearing a straw hat, full body, A-pose", 10000),
}
ANIMS = ["preset:idle", "preset:walk"]
DRY = "--dry-run" in sys.argv


class Blocked(Exception):
    pass


def bodies(name):
    """Request chain for one item; '$prev' is replaced by the previous task_id. "type" only picks the v3 path."""
    desc, faces = ITEMS[name]
    chain = [{"type": "text_to_model", "model_version": MODEL_VERSION, "prompt": f"{desc}, {SUFFIX}",
              "face_limit": faces, "texture": True, "pbr": True}]
    if name == "character":
        chain += [{"type": "animate_rig", "input": "$prev", "out_format": "glb",
                   "rig_type": "biped", "spec": "tripo"},
                  {"type": "animate_retarget", "input": "$prev", "out_format": "glb",
                   "animations": ANIMS, "animate_in_place": True, "bake_animation": True}]
    return chain


_fails = 0  # consecutive 5xx/timeouts across the whole run


def call(method, url, key, body=None, raw=False):
    global _fails
    req = urllib.request.Request(url, method=method, data=json.dumps(body).encode() if body else None,
                                 headers={"Content-Type": "application/json"} if raw else
                                 {"Content-Type": "application/json", "Authorization": f"Bearer {key}"})
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            data = r.read()
    except urllib.error.HTTPError as e:
        if e.code in (401, 402, 429):
            raise Blocked(f"tripo HTTP {e.code}")
        if e.code >= 500:
            _fails += 1
            if _fails >= 2:
                raise Blocked(f"tripo 2x 5xx ({e.code})")
        raise
    except (urllib.error.URLError, TimeoutError) as e:
        _fails += 1
        if _fails >= 2:
            raise Blocked(f"tripo 2x timeout ({e})")
        raise
    _fails = 0
    if raw:
        return data
    j = json.loads(data)
    if j.get("code") != 0:
        msg = json.dumps(j)[:200]
        if "balance" in msg.lower() or "credit" in msg.lower():
            raise Blocked(f"tripo balance: {msg}")
        raise RuntimeError(msg)
    return j["data"]


def run_task(key, body):
    path, body = PATHS[body["type"]], {k: v for k, v in body.items() if k != "type"}
    tid = call("POST", API + path, key, body)["task_id"]
    while True:
        time.sleep(5)
        d = call("GET", f"{API}/tasks/{tid}", key)
        st = d.get("status")
        if st == "success":
            return tid, d.get("output", {})
        if st not in ("queued", "running"):
            raise RuntimeError(f"task {tid} {path} status={st}")


def gen(key, name):
    out = os.path.join(ROOT, "assets_raw", f"{name}.glb")
    prev, output = None, {}
    for body in bodies(name):
        body = {k: (prev if v == "$prev" else v) for k, v in body.items()}
        if DRY:
            print(json.dumps({"POST": API + PATHS[body["type"]], "body": {k: v for k, v in body.items() if k != "type"}},
                             ensure_ascii=False))
            prev = f"<{body['type']}-task-id>"
            continue
        prev, output = run_task(key, body)
    if DRY:
        return
    url = output.get("pbr_model") or output.get("model")
    if not url:
        raise RuntimeError(f"{name}: no model url in output keys {list(output)}")
    data = call("GET", url, key, raw=True)
    if data[:4] != b"glTF":
        raise RuntimeError(f"{name}: download is not glb")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "wb") as f:
        f.write(data)
    git = os.environ.get("GIT", "git")
    subprocess.run([git, "-C", ROOT, "add", out], check=True)
    subprocess.run([git, "-C", ROOT, "commit", "-qm", f"phase-1 {name}"], check=True)
    print(f"{name}: {len(data)} bytes")


def main():
    names = [a for a in sys.argv[1:] if not a.startswith("--")] or list(ITEMS)
    key = "" if DRY else open(os.path.expanduser("~/.tripo_key")).read().strip()
    for name in names:
        for attempt in range(1, 4):
            try:
                gen(key, name)
                break
            except Blocked as e:
                print(f"BLOCKED: {e}")
                return 2
            except Exception as e:
                print(f"{name} try {attempt} failed: {e}")
        else:
            return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
