#!/usr/bin/env python3
"""
Genera le tracce vocali italiane del gioco con ElevenLabs (bake OFFLINE).

Legge i testi da voice_lines.json (chiave -> frase) e salva un MP3 per chiave in
Assets/Resources/Voice/<chiave>.mp3, dove il gioco li carica per nome via NarrationManager.

La chiave API NON e' nel codice: si legge da variabile d'ambiente o da un file locale
gitignorato. Non viene mai stampata.

Uso:
    # opzione A: variabile d'ambiente
    export ELEVENLABS_API_KEY="sk_..."
    python3 Tools/generate_voice.py

    # opzione B: file locale (gitignorato)
    echo "sk_..." > Tools/.elevenlabs.key
    python3 Tools/generate_voice.py

Opzioni:
    --force            rigenera anche le tracce gia' esistenti
    --voice <id>       voice id ElevenLabs (default: env ELEVENLABS_VOICE_ID o Rachel)
    --model <id>       model id (default: eleven_multilingual_v2, ottimo per l'italiano)
    --only <chiavi>    genera solo alcune chiavi, separate da virgola
"""

import argparse
import json
import os
import sys
import urllib.request
import urllib.error

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
LINES_FILE = os.path.join(HERE, "voice_lines.json")
OUT_DIR = os.path.join(REPO, "Assets", "Resources", "Voice")
KEY_FILE = os.path.join(HERE, ".elevenlabs.key")

# Voce di default: "Rachel" (premade multilingua). Per una voce italiana dedicata
# imposta ELEVENLABS_VOICE_ID o passa --voice <id> dalla tua Voice Library.
DEFAULT_VOICE = "21m00Tcm4TlvDq8ikWAM"
DEFAULT_MODEL = "eleven_multilingual_v2"
OUTPUT_FORMAT = "mp3_44100_128"


def read_api_key():
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if key:
        return key
    if os.path.exists(KEY_FILE):
        with open(KEY_FILE, "r", encoding="utf-8") as f:
            return f.read().strip()
    sys.exit(
        "ERRORE: nessuna API key. Imposta ELEVENLABS_API_KEY oppure crea Tools/.elevenlabs.key."
    )


def synth(text, api_key, voice_id, model_id):
    url = (
        f"https://api.elevenlabs.io/v1/text-to-speech/{voice_id}"
        f"?output_format={OUTPUT_FORMAT}"
    )
    payload = json.dumps({
        "text": text,
        "model_id": model_id,
        "voice_settings": {"stability": 0.5, "similarity_boost": 0.75},
    }).encode("utf-8")
    req = urllib.request.Request(url, data=payload, method="POST")
    req.add_header("xi-api-key", api_key)
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "audio/mpeg")
    with urllib.request.urlopen(req, timeout=60) as resp:
        return resp.read()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true")
    ap.add_argument("--voice", default=os.environ.get("ELEVENLABS_VOICE_ID", DEFAULT_VOICE))
    ap.add_argument("--model", default=DEFAULT_MODEL)
    ap.add_argument("--only", default="")
    args = ap.parse_args()

    if not os.path.exists(LINES_FILE):
        sys.exit(f"ERRORE: manca {LINES_FILE}")
    with open(LINES_FILE, "r", encoding="utf-8") as f:
        lines = json.load(f)

    only = {k.strip() for k in args.only.split(",") if k.strip()}
    if only:
        lines = {k: v for k, v in lines.items() if k in only}

    api_key = read_api_key()
    os.makedirs(OUT_DIR, exist_ok=True)

    print(f"Voce: {args.voice}  |  Modello: {args.model}  |  Tracce: {len(lines)}")
    done = 0
    for key, text in lines.items():
        out = os.path.join(OUT_DIR, f"{key}.mp3")
        if os.path.exists(out) and not args.force:
            print(f"  = {key} (gia' presente, salto; usa --force per rigenerare)")
            continue
        try:
            audio = synth(text, api_key, args.voice, args.model)
        except urllib.error.HTTPError as e:
            body = e.read().decode("utf-8", "ignore")
            sys.exit(f"ERRORE HTTP {e.code} su '{key}': {body}")
        except urllib.error.URLError as e:
            sys.exit(f"ERRORE di rete su '{key}': {e.reason}")
        with open(out, "wb") as fo:
            fo.write(audio)
        done += 1
        print(f"  + {key}  ->  {os.path.relpath(out, REPO)}  ({len(audio)} byte)")

    print(f"Fatto. Generate {done} tracce in {os.path.relpath(OUT_DIR, REPO)}.")
    print("Apri Unity per importarle, poi avvia la scena Level1: il gioco parlera'.")


if __name__ == "__main__":
    main()
