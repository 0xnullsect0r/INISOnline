#!/usr/bin/env python3
"""Generate the game's original, royalty-free audio (SFX + a looping ambient pad).

Everything here is synthesized from scratch, so the audio is fully original and free of
third-party licensing. Run from the repo root:  python3 tools/gen-audio.py
Outputs 16-bit mono WAVs under game/audio/sfx and game/audio/music.
"""
import math, os, struct, wave, random

RATE = 22050
SFX_DIR = "game/audio/sfx"
MUSIC_DIR = "game/audio/music"


def write_wav(path, samples):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        frames = bytearray()
        for s in samples:
            v = int(max(-1.0, min(1.0, s)) * 32767)
            frames += struct.pack("<h", v)
        w.writeframes(frames)
    print(f"  {path}  ({len(samples)/RATE:.2f}s)")


def env(i, n, attack=0.01, release=0.3):
    t = i / RATE
    dur = n / RATE
    a = min(1.0, t / attack) if attack > 0 else 1.0
    r = min(1.0, (dur - t) / release) if release > 0 else 1.0
    return max(0.0, a) * max(0.0, r)


def tone(freq, dur, amp=0.5, attack=0.005, release=0.25, kind="sine"):
    n = int(dur * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        if kind == "sine":
            v = math.sin(2 * math.pi * freq * t)
        elif kind == "square":
            v = 1.0 if math.sin(2 * math.pi * freq * t) >= 0 else -1.0
        elif kind == "tri":
            v = 2 / math.pi * math.asin(math.sin(2 * math.pi * freq * t))
        else:
            v = 0.0
        out.append(v * amp * env(i, n, attack, release))
    return out


def noise(dur, amp=0.4, attack=0.005, release=0.2):
    n = int(dur * RATE)
    return [(random.uniform(-1, 1)) * amp * env(i, n, attack, release) for i in range(n)]


def mix(*tracks):
    n = max(len(t) for t in tracks)
    out = [0.0] * n
    for t in tracks:
        for i, v in enumerate(t):
            out[i] += v
    return out


def seq(*tracks):
    out = []
    for t in tracks:
        out += t
    return out


def main():
    random.seed(7)
    # --- SFX ---
    write_wav(f"{SFX_DIR}/click.wav", tone(1200, 0.05, 0.4, 0.001, 0.045))
    write_wav(f"{SFX_DIR}/draw.wav", noise(0.13, 0.35, 0.02, 0.11))
    write_wav(f"{SFX_DIR}/place.wav", mix(tone(180, 0.16, 0.5, 0.001, 0.15),
                                          noise(0.05, 0.15, 0.001, 0.05)))
    write_wav(f"{SFX_DIR}/chime.wav", mix(tone(660, 0.45, 0.28, 0.01, 0.4),
                                          tone(990, 0.45, 0.2, 0.04, 0.4)))
    write_wav(f"{SFX_DIR}/error.wav", tone(140, 0.25, 0.4, 0.005, 0.2, "square"))
    notes = [523.25, 659.25, 783.99, 1046.5]
    write_wav(f"{SFX_DIR}/victory.wav",
              seq(*[tone(f, 0.2, 0.3, 0.01, 0.18) for f in notes]))

    # --- Music: a slow Celtic-ish ambient pad (A minor-ish chord with gentle LFOs) ---
    dur = 8.0
    n = int(dur * RATE)
    voices = [(110.0, 0.18), (164.81, 0.14), (220.0, 0.12), (277.18, 0.10)]
    pad = [0.0] * n
    for vi, (freq, amp) in enumerate(voices):
        lfo = 0.12 + 0.04 * vi
        for i in range(n):
            t = i / RATE
            mod = 0.7 + 0.3 * math.sin(2 * math.pi * lfo * t + vi)
            pad[i] += math.sin(2 * math.pi * freq * t) * amp * mod
    # Soft global fade in/out so the loop is seamless.
    fade = int(0.5 * RATE)
    for i in range(fade):
        k = i / fade
        pad[i] *= k
        pad[n - 1 - i] *= k
    write_wav(f"{MUSIC_DIR}/menu_ambient.wav", pad)


if __name__ == "__main__":
    main()
