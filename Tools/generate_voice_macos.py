#!/usr/bin/env python3
"""
Genera le tracce vocali italiane con la voce integrata di macOS (GRATIS, offline).

Ripiego a costo zero quando ElevenLabs non e' disponibile (es. piano free che blocca le
voci via API). Legge gli STESSI testi di voice_lines.json e produce gli STESSI nomi file
(<chiave>.mp3) in Assets/Resources/Voice/, quindi e' perfettamente intercambiabile con
generate_voice.py: quando passi a ElevenLabs, ri-lanci quello e i file si sovrascrivono.

Richiede: comando `say` (nativo macOS) + `ffmpeg`.

Uso:
    python3 Tools/generate_voice_macos.py
    python3 Tools/generate_voice_macos.py --voice Alice --force
    python3 Tools/generate_voice_macos.py --only find_cubo,find_cilindro

Voci italiane disponibili: `say -v '?' | grep it_IT`
"""

import argparse
import json
import os
import subprocess
import sys
import tempfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
LINES_FILE = os.path.join(HERE, "voice_lines.json")
OUT_DIR = os.path.join(REPO, "Assets", "Resources", "Voice")
DEFAULT_VOICE = "Alice"  # voce italiana chiara di macOS


def check_tool(name):
    if subprocess.run(["which", name], capture_output=True).returncode != 0:
        sys.exit(f"ERRORE: '{name}' non trovato. Serve per generare/convertire l'audio.")


def synth(text, voice, out_mp3):
    # say -> AIFF, poi ffmpeg -> MP3 44.1kHz mono (leggero e universale in Unity).
    with tempfile.NamedTemporaryFile(suffix=".aiff", delete=False) as tmp:
        aiff = tmp.name
    try:
        subprocess.run(["say", "-v", voice, "-o", aiff, text], check=True)
        subprocess.run(
            ["ffmpeg", "-y", "-loglevel", "error", "-i", aiff,
             "-ar", "44100", "-ac", "1", "-codec:a", "libmp3lame", "-q:a", "4", out_mp3],
            check=True,
        )
    finally:
        if os.path.exists(aiff):
            os.remove(aiff)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--voice", default=DEFAULT_VOICE)
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--only", default="")
    args = ap.parse_args()

    check_tool("say")
    check_tool("ffmpeg")

    if not os.path.exists(LINES_FILE):
        sys.exit(f"ERRORE: manca {LINES_FILE}")
    with open(LINES_FILE, "r", encoding="utf-8") as f:
        lines = json.load(f)

    only = {k.strip() for k in args.only.split(",") if k.strip()}
    if only:
        lines = {k: v for k, v in lines.items() if k in only}

    os.makedirs(OUT_DIR, exist_ok=True)
    print(f"Voce macOS: {args.voice}  |  Tracce: {len(lines)}")

    done = 0
    for key, text in lines.items():
        out = os.path.join(OUT_DIR, f"{key}.mp3")
        if os.path.exists(out) and not args.force:
            print(f"  = {key} (gia' presente, salto; usa --force per rigenerare)")
            continue
        synth(text, args.voice, out)
        done += 1
        print(f"  + {key}  ->  {os.path.relpath(out, REPO)}  ({os.path.getsize(out)} byte)")

    print(f"Fatto. Generate {done} tracce in {os.path.relpath(OUT_DIR, REPO)}.")
    print("Apri Unity per importarle, poi avvia la scena Level1: il gioco parlera'.")


if __name__ == "__main__":
    main()
