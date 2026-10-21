"""Read-only inventory of project roots and nested Git repositories; output stays local."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import unicodedata
from urllib.parse import urlsplit, urlunsplit

SKIP = {"node_modules", "venv", ".venv", "build", "dist", "bin", "obj", "versions", "Library", "packages", "research", "Interception-source"}

def slug(value):
    text = unicodedata.normalize("NFKD", re.sub(r"^\d+[_ ]*", "", value))
    text = text.encode("ascii", "ignore").decode().lower()
    return re.sub(r"[^a-z0-9]+", "-", text).strip("-") or "projekt"

def category(name):
    text = slug(name)
    if any(word in text for word in ["esp", "arduino", "zigbee", "svetylka", "light-saber", "irrem", "lora"]): return "esp32"
    if any(word in text for word in ["homeassistant", "ha-local", "mybrowser"]): return "homeassistant"
    if "zephyr" in text: return "android"
    if any(word in text for word in ["wifi-anal", "sony", "prehled-pro-videa", "obrazkova-galerie"]): return "pc"
    if "web" in text: return "web"
    return "review"

def remote(path):
    result = subprocess.run(["git", "-C", str(path), "config", "--get", "remote.origin.url"], capture_output=True, text=True, encoding="utf-8", errors="replace")
    value = result.stdout.strip()
    if "://" in value:
        parsed = urlsplit(value)
        value = urlunsplit((parsed.scheme, parsed.hostname or "", parsed.path, "", ""))
    return value

def scan(root):
    projects = []
    for folder in sorted(root.iterdir()):
        if not folder.is_dir() or folder.name.startswith(".") or folder.name == "00_AppStore": continue
        repositories = []
        for current, dirs, files in os.walk(folder):
            path = Path(current)
            if ".git" in dirs or ".git" in files:
                origin = remote(path)
                repositories.append({"path": path.relative_to(root).as_posix(), "origin": origin, "owner": "own" if "github.com/Jirkas-prog/" in origin else "external-or-local"})
                dirs[:] = []
            else:
                dirs[:] = [d for d in dirs if d not in SKIP and not d.startswith(".") and len(path.relative_to(folder).parts) < 3]
        projects.append({"folder": folder.name, "suggestedRepository": slug(folder.name), "suggestedCategory": category(folder.name), "reviewRequired": True, "repositories": repositories})
    return projects

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    rows = scan(args.root.resolve())
    out = Path(__file__).resolve().parents[1] / "local"
    out.mkdir(exist_ok=True)
    (out / "project-inventory.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    lines = ["# Lokální inventura projektů", "", "Automatický návrh kategorií a názvů, nikoli schválení ke zveřejnění. Zdrojové složky nebyly přesunuty. Výzkumné klony, závislosti a archivy verzí se neprocházejí.", "", "| Složka | Navržená kategorie | Navržený název repozitáře | Git |", "| --- | --- | --- | --- |"]
    for row in rows:
        repos = "<br>".join(r["origin"] or "lokální Git" for r in row["repositories"]) or "nenalezen"
        lines.append(f'| {row["folder"]} | {row["suggestedCategory"]} | {row["suggestedRepository"]} | {repos} |')
    (out / "PROJECTS.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f'{len(rows)} složek, {sum(len(r["repositories"]) for r in rows)} repozitářů. Výstup: {out / "PROJECTS.md"}')
