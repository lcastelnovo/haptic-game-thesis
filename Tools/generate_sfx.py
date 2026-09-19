#!/usr/bin/env python3
"""Genera i suoni non verbali del Level 2 (labirinto) con ffmpeg.

Come per le voci, i suoni NON si fanno a mano: qui c'e' la ricetta, cosi' si possono
rigenerare identici e ritoccare senza indovinare che cosa era stato usato.

    python3 Tools/generate_sfx.py                # genera solo i mancanti
    python3 Tools/generate_sfx.py --force        # rigenera tutto
    python3 Tools/generate_sfx.py --only dwell_tone reading_ready

Criteri di design (il gioco e' per persone non vedenti, i suoni sono l'interfaccia):
  - tutto non verbale e breve: la voce ha gia' il suo canale, questi sono segnali
  - ogni segnale e' distinguibile dagli altri per TIMBRO, non solo per altezza:
    chi ascolta tutto il tempo non deve fare confronti di intonazione
  - attenzione ai loop: l'mp3 aggiunge padding all'encoder, quindi un clip che cicla
    fa un clic a ogni giro. Il tono della sosta e' piu' lungo della sosta massima
    (non cicla mai), e il loop del fuori percorso e' WAV, che e' gapless
"""

import argparse
import subprocess
import sys
from pathlib import Path

OUT_DIR = Path(__file__).resolve().parent.parent / "Assets" / "Audio" / "Level2"
SR = 44100


def cycles(freq: float, seconds: float) -> float:
    """Durata arrotondata a un numero intero di cicli: loop senza clic."""
    n = max(1, round(freq * seconds))
    return n / freq


# nome -> (filtro ffmpeg, durata)
def build_specs():
    # Sosta sulla piastrella: tono tenue, il pitch lo rampa Unity da 1 a 2 (330 -> 660
    # Hz). Dura piu' della sosta massima ammessa dal profilo (6 s), cosi' non cicla mai
    # e il padding dell'mp3 non si sente.
    dwell = (f"sine=frequency=330:sample_rate={SR}:duration=6.5,"
             f"afade=t=in:st=0:d=0.02,volume=0.22")

    # Fine sosta: la lettura e' valida. Blip cristallino, diverso da tutto il resto.
    ready = (f"sine=frequency=1318:sample_rate={SR}:duration=0.10,"
             f"afade=t=out:st=0.04:d=0.06,volume=0.5")

    # Condizione 'audio': esito del bivio. Due note, sale = giusto, scende = sbagliato.
    up = ("sine=frequency=523:sample_rate=%d:duration=0.14,afade=t=out:st=0.10:d=0.04[a];"
          "sine=frequency=784:sample_rate=%d:duration=0.22,afade=t=out:st=0.12:d=0.10[b];"
          "[a][b]concat=n=2:v=0:a=1,volume=0.45" % (SR, SR))
    down = ("sine=frequency=784:sample_rate=%d:duration=0.14,afade=t=out:st=0.10:d=0.04[a];"
            "sine=frequency=415:sample_rate=%d:duration=0.28,afade=t=out:st=0.12:d=0.16[b];"
            "[a][b]concat=n=2:v=0:a=1,volume=0.45" % (SR, SR))

    # Vicolo cieco: tonfo sordo e basso, senza intonazione riconoscibile.
    dead = (f"sine=frequency=110:sample_rate={SR}:duration=0.30,"
            f"afade=t=out:st=0.03:d=0.27,volume=0.6")

    # Fuori percorso: loop insistente ma non allarmante, deve poter restare acceso
    # anche per parecchi secondi senza diventare intollerabile.
    off_d = cycles(196, 2.0)
    off = (f"sine=frequency=196:sample_rate={SR}:duration={off_d},"
           f"tremolo=f=4:d=0.8,volume=0.20")

    return {
        "dwell_tone": dwell,
        "reading_ready": ready,
        "branch_up": up,
        "branch_down": down,
        "dead_end": dead,
        "off_track_loop": off,
    }


# Questi ciclano all'infinito: WAV, perche' l'mp3 non e' gapless.
LOOPING = {"off_track_loop"}


def generate(name: str, filt: str, force: bool) -> bool:
    ext = "wav" if name in LOOPING else "mp3"
    out = OUT_DIR / f"{name}.{ext}"
    if out.exists() and not force:
        print(f"  = {out.name} (gia' presente)")
        return False
    cmd = ["ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
           "-filter_complex", filt, "-ac", "1", "-ar", str(SR)]
    cmd += ["-c:a", "pcm_s16le"] if ext == "wav" else ["-b:a", "128k"]
    cmd += [str(out)]
    subprocess.run(cmd, check=True)
    print(f"  + {out.name}")
    return True


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--force", action="store_true", help="rigenera anche i file gia' presenti")
    ap.add_argument("--only", nargs="+", metavar="NOME", help="genera solo questi suoni")
    args = ap.parse_args()

    specs = build_specs()
    if args.only:
        sconosciuti = [n for n in args.only if n not in specs]
        if sconosciuti:
            print(f"Nomi sconosciuti: {', '.join(sconosciuti)}", file=sys.stderr)
            print(f"Disponibili: {', '.join(specs)}", file=sys.stderr)
            return 2
        specs = {n: specs[n] for n in args.only}

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Suoni in {OUT_DIR}")
    fatti = sum(generate(n, f, args.force) for n, f in specs.items())
    print(f"Generati {fatti} file su {len(specs)}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
