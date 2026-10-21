"""Exercises the real Rust host protocol and a local package in an isolated profile."""
import json
import os
from pathlib import Path
import queue
import subprocess
import tempfile
import threading

ROOT = Path(__file__).resolve().parents[1]

with tempfile.TemporaryDirectory(prefix="jirka-host-check-") as profile:
    catalog = ROOT / "local/catalog.local.json"
    if not catalog.exists():
        catalog = ROOT / "catalog/catalog.json"
    Path(profile, "settings.json").write_text(json.dumps({"source": str(catalog)}), encoding="utf-8")
    process = subprocess.Popen([str(ROOT / "dist/windows/engine/JirkaStore.Host.exe"), "--data-root", profile],
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8",
                               creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    events = queue.Queue()
    def read():
        for line in process.stdout:
            events.put(json.loads(line))
    threading.Thread(target=read, daemon=True).start()
    def until(predicate):
        while True:
            event = events.get(timeout=90)
            if predicate(event):
                return event
    def idle():
        return until(lambda e: e["type"] == "snapshot" and not e["busy"])
    def send(**command):
        process.stdin.write(json.dumps(command) + "\n")
        process.stdin.flush()
    try:
        start = idle()
        assert len(start["apps"]) >= 11
        wifi = next(e for e in start["apps"] if e["app"]["id"] == "wifi-scope")
        if wifi["app"]["kind"] == "portable-zip":
            send(command="install", id="wifi-scope")
            installed = idle()
            assert next(e for e in installed["apps"] if e["app"]["id"] == "wifi-scope")["state"] == "installed"
            pointer = Path(profile, "apps/wifi-scope/installed.json")
            previous = pointer.read_bytes()
            send(command="install", id="wifi-scope")
            assert "novější" in until(lambda e: e["type"] == "error")["message"]
            idle()
            assert pointer.read_bytes() == previous
        send(command="source", source=str(Path(profile, "missing.json")))
        until(lambda e: e["type"] == "error")
        assert len(idle()["apps"]) == len(start["apps"])
        send(command="unknown")
        until(lambda e: e["type"] == "error")
        idle()
        print("PASS: host catalogue, state, install, downgrade, failure preservation, invalid command, clean exit")
    finally:
        process.stdin.close()
        try:
            process.wait(timeout=20)
        except subprocess.TimeoutExpired:
            process.kill()
            raise
        assert process.returncode == 0, process.stderr.read()
