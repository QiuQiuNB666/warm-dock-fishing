#!/usr/bin/env python3
"""Phase 5 test: web_check.py [web_dir=unity/Build/Web] [seconds=60]. Serves the WebGL build with http.server,
opens it in headless Edge (index.html is served with a hook tagging console.error / uncaught errors, because
Edge logs every page console level as INFO; the build on disk is untouched), then asserts SCENE-READY logged exactly once, 0 console errors, Build/ <= 60MB.
Prints one line WEB PASS / WEB FAIL: <first reason>."""
import functools, http.server, os, re, shutil, subprocess, sys, threading, time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EDGE = r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
PROFILE, LOG = r"D:\tmp\edge", r"D:\tmp\edge.log"
PORT, LIMIT_MB = 8765, 60
HOOK = (b"<script>(e=>{console.error=function(...a){e.apply(console,['WEBCHECK-ERROR',...a])};"
        b"addEventListener('error',x=>console.log('WEBCHECK-ERROR',x.message));"
        b"addEventListener('unhandledrejection',x=>console.log('WEBCHECK-ERROR',String(x.reason)))})(console.error)</script>")


class Handler(http.server.SimpleHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def do_GET(self):
        if self.path.split("?")[0] not in ("/", "/index.html"):
            return super().do_GET()
        body = open(os.path.join(self.directory, "index.html"), "rb").read().replace(b"<head>", b"<head>" + HOOK, 1)
        self.send_response(200)
        self.send_header("Content-Type", "text/html")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


def kill(p):
    subprocess.run(["taskkill", "/PID", str(p.pid), "/T", "/F"], capture_output=True)


def check(web, secs):
    build = os.path.join(web, "Build")
    if not os.path.isfile(os.path.join(web, "index.html")) or not os.path.isdir(build):
        return f"no build in {web}"
    mb = sum(os.path.getsize(os.path.join(build, f)) for f in os.listdir(build)) / 2**20
    if mb > LIMIT_MB:
        return f"Build/ {mb:.1f}MB > {LIMIT_MB}MB"
    shutil.rmtree(PROFILE, ignore_errors=True)
    server = http.server.ThreadingHTTPServer(("127.0.0.1", PORT), functools.partial(Handler, directory=web))
    threading.Thread(target=server.serve_forever, daemon=True).start()
    try:
        with open(LOG, "w", encoding="utf-8") as log:
            edge = subprocess.Popen([EDGE, "--headless=new", f"--user-data-dir={PROFILE}", "--enable-logging=stderr", "--v=1",
                                     "--enable-unsafe-swiftshader", "--no-first-run", f"http://127.0.0.1:{PORT}/index.html"],
                                    stdout=log, stderr=log)
            time.sleep(secs)
            kill(edge)
    finally:
        server.shutdown()
    console = [l for l in open(LOG, encoding="utf-8", errors="replace") if re.search(r":CONSOLE[:(]", l)]
    ready = sum("SCENE-READY" in l for l in console)
    errors = [l.strip() for l in console if "WEBCHECK-ERROR" in l]
    if ready != 1:
        return f"SCENE-READY x{ready} ({len(console)} console lines)"
    if errors:
        return f"{len(errors)} console errors, first: {errors[0][:200]}"
    print(f"build {mb:.1f}MB")
    return None


if __name__ == "__main__":
    a = sys.argv[1:]
    err = check(a[0] if a else os.path.join(ROOT, "unity", "Build", "Web"), int(a[1]) if len(a) > 1 else 60)
    print("WEB PASS" if err is None else f"WEB FAIL: {err}")
    sys.exit(0 if err is None else 1)
