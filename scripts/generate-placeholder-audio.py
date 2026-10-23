#!/usr/bin/env python3
"""Generate original deterministic PCM placeholders using only Python's standard library.

No recordings, external samples, dependencies, or network access are used.
Run from any directory. Existing WAV outputs are regenerated; Unity metadata is untouched.
"""

from array import array
import math
from pathlib import Path
import random
import sys
import wave


RATE = 48000
OUTPUT = Path(__file__).resolve().parents[1] / "Assets/_Project/Audio/Placeholders"
TAU = 2 * math.pi


def write_clip(name, duration, signal, peak):
    values = [signal(index / RATE) for index in range(round(duration * RATE))]
    gain = peak / max(abs(value) for value in values)
    samples = array("h", (round(value * gain * 32767) for value in values))
    if sys.byteorder != "little":
        samples.byteswap()
    with wave.open(str(OUTPUT / name), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(samples.tobytes())
    print(f"{name}: {duration:g}s, mono, {RATE} Hz, 16-bit PCM")


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    rng = random.Random(31742)
    # Every frequency is an integer multiple of 1/8 Hz: the complete texture
    # and its slow amplitude modulation repeat continuously after eight seconds.
    texture = [(rng.randint(960, 14400) / 8, rng.uniform(0, TAU)) for _ in range(40)]

    def ambience(t):
        hum = (0.48 * math.sin(TAU * 50 * t)
               + 0.2 * math.sin(TAU * 100 * t)
               + 0.08 * math.sin(TAU * 150 * t))
        air = sum(math.sin(TAU * frequency * t + phase) for frequency, phase in texture) / 40
        return (hum + 0.32 * air) * (0.85 + 0.15 * math.cos(TAU * t / 8))

    def alarm(t):
        # Two smooth pulses per two-second loop, with a periodic pitch wobble.
        envelope = math.sin(math.pi * t) ** 8
        phase = TAU * 660 * t + 2 * math.sin(TAU * t)
        return envelope * (math.sin(phase) + 0.2 * math.sin(2 * phase))

    def confirmation(t):
        envelope = math.sin(math.pi * t / 0.32) ** 2
        return envelope * (math.sin(TAU * 880 * t) + 0.35 * math.sin(TAU * 1320 * t))

    write_clip("IndustrialAmbience.wav", 8, ambience, 0.55)
    write_clip("SecurityAlarm.wav", 2, alarm, 0.65)
    write_clip("Confirmation.wav", 0.32, confirmation, 0.6)


if __name__ == "__main__":
    main()
