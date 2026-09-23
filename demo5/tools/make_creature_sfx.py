"""Creature sound effects for the field battle (demo5).

Placeholder foley for the twelve paper-standee anomaly creatures, synthesised
from noise/sine sources with the helpers of make_battle_sfx.py so they share
its loudness, fades and small concrete-room colour. Timing and weight are meant
to be right; the sounds themselves are not final: replace any WAV under
Assets/Audio/Battle/Creatures with recorded audio of the same name.

make_battle_sfx.py is only imported, never run, so its WAVs and its random
stream stay untouched. This script draws from its own generator (seed 1207).

Run:  python tools/make_creature_sfx.py
"""
import os
import sys
import wave

import numpy as np

sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from make_battle_sfx import (RATE, at, decay, finish, highpass, lowpass, pad,  # noqa: E402
                             resonator, room, sweep, t_axis)

OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Audio", "Battle", "Creatures"))
rng = np.random.default_rng(1207)
TAU = 2 * np.pi


# ---------------------------------------------------------------- local helpers

def noise(seconds):
    return rng.uniform(-1, 1, int(RATE * seconds))


def norm(x):
    return x / (np.max(np.abs(x)) + 1e-9)


def band(x, lo, hi):
    return highpass(lowpass(x, hi), lo)


def env(seconds, attack, release):
    t = t_axis(seconds)
    return np.clip(t / attack, 0, 1) * np.clip((seconds - t) / release, 0, 1)


def stick_slip(rate, jitter=0.2, spread=0.5):
    """Friction impulses, one per stick-slip cycle at a wandering rate (Hz per sample)."""
    x = np.zeros(len(rate))
    t = rng.uniform(0, 0.5) / rate[0]
    while True:
        n = int(t * RATE)
        if n >= len(x):
            return x
        x[n] = 1 - spread * rng.uniform()
        t += (1 + jitter * rng.uniform(-1, 1)) / rate[n]


def sparse(seconds, density, lo=0.3, hi=1.0):
    """Random single-sample events; density is events per second (scalar or per sample)."""
    n = int(RATE * seconds)
    density = np.broadcast_to(np.asarray(density, dtype=float), (n,))
    hits = rng.uniform(size=n) < density / RATE
    return hits * rng.uniform(lo, hi, n) * rng.choice((-1.0, 1.0), n)


def grains(events, tau=0.0015):
    """Turn single-sample events into tiny decaying noise ticks."""
    k = int(tau * 6 * RATE)
    kernel = rng.uniform(-1, 1, k) * np.exp(-np.arange(k) / (tau * RATE))
    return np.convolve(events, kernel)[:len(events)]


def bubble(freq, rise, tau, seconds):
    """Water drop / bubble: a short sine whose pitch climbs as it surfaces."""
    t = t_axis(seconds)
    f = freq * (1 + (rise - 1) * (1 - np.exp(-t / (tau * 1.5))))
    shape = np.clip(t / 0.001, 0, 1) * np.exp(-t / tau) * np.clip((seconds - t) / 0.01, 0, 1)
    return np.sin(TAU * np.cumsum(f) / RATE) * shape


# ---------------------------------------------------------------- door-bearer / stair

def creak():
    s = 0.62
    t = t_axis(s)
    u = t / s
    # heavy hinge: slips start slow, catch up as the slab swings, then sag
    rate = 34 + 62 * np.sin(np.pi * u) ** 1.4 + 9 * np.sin(TAU * 2.3 * t)
    slips = stick_slip(rate, 0.16, 0.45)
    wood = (0.6 * norm(resonator(slips, 150, 2.5)) + norm(resonator(slips, 330, 5))
            + 0.7 * norm(resonator(slips, 760, 7)) + 0.3 * norm(resonator(slips, 1350, 9)))
    hinge = norm(resonator(slips, 1880, 28)) * 0.1
    grit = norm(band(noise(s), 400, 2000)) * 0.12
    x = (norm(wood) + hinge + grit) * env(s, 0.07, 0.14) * (0.6 + 0.4 * np.sin(np.pi * u))
    return finish(room(x, 0.3, 2200), 0.8, 1.3, 5000)


def thud():
    s = 0.6
    boom = sweep(80, 30, s, 3.5) * decay(s, 0.17)
    body = sweep(165, 95, s, 4) * decay(s, 0.05)
    slab = norm(resonator(noise(s), 360, 2.5) * decay(s, 0.03)) + 0.6 * norm(resonator(noise(s), 840, 3.5) * decay(s, 0.018))
    click = lowpass(noise(s), 3200) * decay(s, 0.0025)
    x = boom + body * 0.45 + norm(slab) * 0.55 + norm(click) * 0.25
    return finish(room(x, 0.4, 1800), 0.95, 1.9, 5000)


def collide():
    s = 0.3
    knock = sweep(170, 85, s, 4) * decay(s, 0.04)
    thump = lowpass(noise(s), 700) * decay(s, 0.028)
    wall = resonator(noise(s), 240, 2) * decay(s, 0.035)
    flesh = band(noise(s), 300, 1400) * decay(s, 0.01)
    x = knock + norm(thump) * 0.6 + norm(wall) * 0.5 + norm(flesh) * 0.3
    return finish(room(x, 0.25, 1600), 0.85, 1.5, 4000)


def listen():
    s = 0.72
    t = t_axis(s)
    u = t / s
    hiss = band(noise(s), 300 + 650 * u ** 1.5, 1000 + 2000 * u ** 1.5)
    air = noise(s)
    breath = norm(resonator(air, 600, 1.3)) * (1 - u) + norm(resonator(air, 900, 1.3)) * u
    crackle = band(grains(sparse(s, 15 + 110 * u ** 2), 0.0012), 600, 3000)
    phase = TAU * np.cumsum(290 + 40 * u) / RATE
    tone = np.sin(phase) + np.sin(phase * 1.013)
    x = norm(hiss) * 0.6 + norm(breath) * 0.35 + norm(crackle) * 0.18 + norm(tone) * 0.08
    x *= u ** 1.8 * np.clip((s - t) / 0.09, 0, 1)
    return finish(room(x, 0.2, 2400), 0.5, 1.0, 4500)


# ---------------------------------------------------------------- long-arm

def slam():
    s = 0.78
    hit_at = 0.19
    w = 0.21
    tw = t_axis(w)
    shape = np.clip(tw / w, 0, 1) ** 2.2 * np.clip((w - tw) / 0.02, 0, 1)
    whoosh = highpass(lowpass(noise(w), 2400 - 1700 * tw / w), 220) * shape
    h = s - hit_at
    boom = sweep(95, 33, h, 4) * decay(h, 0.14)
    crack = lowpass(noise(h), 2200) * decay(h, 0.016)
    floor = resonator(noise(h), 210, 2.2) * decay(h, 0.05)
    grit = band(noise(h), 900, 2800) * env(h, 0.015, h) * decay(h, 0.06)
    hit = boom + norm(crack) * 0.5 + norm(floor) * 0.45 + norm(grit) * 0.08
    x = pad(norm(whoosh) * 0.45, s) + at(hit, hit_at, s)
    return finish(room(x, 0.5, 1900), 0.95, 1.9, 5500)


# ---------------------------------------------------------------- chair creature

def clack():
    s = 0.42
    x = np.zeros(int(RATE * s))
    for start, gain, f in ((0.0, 1.0, 1.0), (0.037, 0.7, 1.08), (0.069, 0.85, 0.93), (0.115, 0.55, 1.04)):
        d = 0.08
        tick = noise(d) * decay(d, 0.002)
        leg = (norm(resonator(tick, 760 * f, 9)) + 0.6 * norm(resonator(tick, 1330 * f, 11))
               + 0.22 * norm(resonator(tick, 2150 * f, 13)) + 0.4 * np.sin(TAU * 205 * f * t_axis(d)) * decay(d, 0.018))
        x += at(norm(leg) * gain, start, s)
    h = s - 0.12
    thump = sweep(150, 68, h, 4) * decay(h, 0.045) + norm(lowpass(noise(h), 600) * decay(h, 0.02)) * 0.4
    x += at(norm(thump) * 0.35, 0.12, s)
    return finish(room(x, 0.22, 2200), 0.85, 1.3, 6000)


def scrape():
    s = 0.3
    t = t_axis(s)
    u = t / s
    rate = 80 + 95 * np.sin(np.pi * u)
    slips = stick_slip(rate, 0.25, 0.5)
    wood = norm(resonator(slips, 440, 6)) + 0.7 * norm(resonator(slips, 1020, 8)) + 0.25 * norm(resonator(slips, 1720, 10))
    rasp = band(noise(s), 600, 2400) * (0.5 + 0.5 * np.sin(TAU * np.cumsum(rate) / RATE))
    x = (norm(wood) + norm(rasp) * 0.3) * env(s, 0.025, 0.09) * (0.7 + 0.3 * np.sin(np.pi * u))
    return finish(room(x, 0.2, 2200), 0.7, 1.2, 5000)


# ---------------------------------------------------------------- crawler

def dash():
    s = 0.3
    t = t_axis(s)
    arc = np.sin(np.pi * np.clip(t / s, 0, 1))
    whoosh = highpass(lowpass(noise(s), 320 + 1100 * arc), 110) * arc ** 2
    claws = np.zeros(len(t))
    for base in (0.03, 0.085, 0.14, 0.195, 0.245):
        for lag, g in ((0.0, 1.0), (0.016, 0.7)):
            d = 0.03
            tk = noise(d) * decay(d, 0.0015)
            c = norm(resonator(tk, rng.uniform(1900, 2500), 5)) + 0.6 * norm(resonator(tk, rng.uniform(800, 1100), 4))
            c += 0.35 * np.sin(TAU * 170 * t_axis(d)) * decay(d, 0.01)
            claws += at(norm(c) * g * rng.uniform(0.6, 1.0), base + lag + rng.uniform(-0.006, 0.006), s)
    x = norm(whoosh) + norm(lowpass(claws, 3800)) * 0.45
    return finish(x, 0.8, 1.2, 5000)


def snarl():
    s = 0.4
    t = t_axis(s)
    pitch = 90 + 16 * np.exp(-t / 0.07) + 6 * np.sin(TAU * 6.3 * t)
    saw = 2 * ((np.cumsum(pitch) / RATE) % 1) - 1
    rattle = 0.6 + 0.4 * np.sin(TAU * 29 * t) * (0.6 + 0.4 * np.clip(lowpass(noise(s), 60) * 5, -1, 1))
    voice = saw * rattle
    throat = norm(resonator(voice, 470, 4)) + 0.55 * norm(resonator(voice, 960, 6)) + 0.14 * norm(resonator(voice, 2100, 8))
    breath = norm(resonator(noise(s), 820, 1.1))
    growl_env = np.clip(t / 0.03, 0, 1) * np.clip((0.29 - t) / 0.1, 0, 1)
    pant_env = np.clip((t - 0.25) / 0.025, 0, 1) * np.exp(-np.maximum(t - 0.275, 0) / 0.035)
    x = norm(throat) * growl_env + breath * (0.22 * growl_env + 0.35 * pant_env)
    return finish(room(x, 0.3, 1600), 0.75, 1.7, 4500)


# ---------------------------------------------------------------- laundry sheet

def flap():
    s = 0.42
    snap_at = 0.085
    t = t_axis(s)
    pre = lowpass(noise(s), 1100) * np.clip(t / snap_at, 0, 1) ** 2 * np.exp(-np.maximum(t - snap_at, 0) / 0.012)
    h = s - snap_at
    th = t_axis(h)
    crack = lowpass(noise(h), 2600) * decay(h, 0.007)
    whump = sweep(120, 52, h, 4) * decay(h, 0.07)
    wet = resonator(noise(h), 320, 2.5) * decay(h, 0.032)
    flutter = band(noise(h), 250, 1500) * (0.5 + 0.5 * np.sin(TAU * 23 * th)) ** 2 * np.clip(th / 0.02, 0, 1) * decay(h, 0.08)
    hit = norm(crack) * 0.5 + whump * 0.9 + norm(wet) * 0.6 + norm(flutter) * 0.35
    x = norm(pre) * 0.25 + at(hit, snap_at, s)
    return finish(room(x, 0.25, 1800), 0.85, 1.6, 4500)


# ---------------------------------------------------------------- transformer / fuse box

def zap():
    s = 0.46
    t = t_axis(s)
    mains = np.tanh(3.5 * np.sin(TAU * 60 * t)) + 0.4 * np.sin(TAU * 120 * t)
    mains = lowpass(mains, 1300) * (0.3 + 0.7 * np.exp(-t / 0.12)) * np.clip((s - t) / 0.16, 0, 1)
    sparks = band(grains(sparse(s, 1500 * np.exp(-t / 0.06) + 50 * np.exp(-t / 0.3)), 0.0012), 700, 4200)
    arc = band(noise(s), 800, 3200) * np.exp(-t / 0.06) * (0.5 + 0.5 * np.sin(TAU * 120 * t)) ** 2
    snap = lowpass(noise(s), 4000) * decay(s, 0.004)
    x = norm(mains) * 0.6 + norm(sparks) * 0.6 + norm(arc) * 0.35 + norm(snap) * 0.7
    x *= np.clip(t / 0.002, 0, 1)
    return finish(room(x, 0.2, 2600), 0.9, 1.6, 6500)


def hum():
    s = 0.6
    t = t_axis(s)
    u = t / s
    phase = TAU * np.cumsum(52 + 40 * u ** 1.4) / RATE
    k = 1.5 + 4 * u
    buzz = np.tanh(k * np.sin(phase)) / np.tanh(k) + 0.35 * np.sin(2 * phase)
    buzz = lowpass(buzz, 450 + 1500 * u)
    whine = np.sin(8 * phase) * u ** 2
    crackle = band(grains(sparse(s, 6 + 60 * u ** 2), 0.001), 800, 3500)
    level = (0.12 + 0.88 * u ** 1.3) * np.clip((s - t) / 0.045, 0, 1)
    x = (norm(buzz) + norm(whine) * 0.06 + norm(crackle) * 0.22) * level
    return finish(x, 0.6, 1.2, 6000)


# ---------------------------------------------------------------- moth swarm

def flutter():
    s = 0.5
    t = t_axis(s)
    x = np.zeros(len(t))
    for _ in range(5):
        rate = rng.uniform(26, 44)
        wobble = 0.8 * np.sin(TAU * rng.uniform(2, 5) * t + rng.uniform(0, TAU))
        beats = (0.5 + 0.5 * np.sin(TAU * rate * t + rng.uniform(0, TAU) + wobble)) ** 4
        wing = band(noise(s), 450, 2600) + 0.5 * band(noise(s), 120, 450)
        x += wing * beats * rng.uniform(0.5, 1.0)
    rustle = band(noise(s), 1400, 3200) * (0.5 + 0.5 * np.sin(TAU * 9 * t))
    x = lowpass(norm(x) + norm(rustle) * 0.12, 3000) * env(s, 0.06, 0.15)
    return finish(room(x, 0.15, 2500), 0.6, 1.0, 5000)


# ---------------------------------------------------------------- drowned / sink

def splash():
    s = 0.45
    t = t_axis(s)
    slap = sweep(160, 70, s, 4) * decay(s, 0.028)
    smack = band(noise(s), 300, 2400) * decay(s, 0.009)
    spray = band(noise(s), 900, 3400) * np.clip(t / 0.012, 0, 1) * np.exp(-t / 0.07)
    spray *= np.clip(0.3 + np.abs(lowpass(noise(s), 180)) * 8, 0, 1.5)
    gurgle = np.zeros(len(t))
    for i in range(9):
        start = 0.06 + i * 0.035 + rng.uniform(-0.01, 0.01)
        b = bubble(rng.uniform(260, 700), rng.uniform(1.3, 1.8), rng.uniform(0.012, 0.028), 0.1)
        gurgle += at(b * rng.uniform(0.5, 1.0) * np.exp(-start / 0.22), start, s)
    x = norm(slap) * 0.7 + norm(smack) * 0.6 + norm(spray) * 0.45 + norm(gurgle) * 0.45
    return finish(room(x, 0.3, 2400), 0.85, 1.4, 6000)


def drip():
    s = 0.5
    t = t_axis(s)
    x = np.zeros(len(t))
    for start, f, g in ((0.012, 1150, 1.0), (0.195, 1420, 0.65), (0.335, 990, 0.8)):
        d = bubble(f, 1.7, 0.011, 0.08) + 0.12 * norm(lowpass(noise(0.08), 3000) * decay(0.08, 0.0012))
        x += at(d * g, start, s)
    for start, f in ((0.05, 430), (0.235, 470), (0.37, 400)):
        x += at(bubble(f, 1.25, 0.03, 0.12) * 0.22, start, s)
    lap = lowpass(noise(s), 480) * (0.5 + 0.5 * np.sin(TAU * 5 * t)) * np.clip(t / 0.05, 0, 1) * np.exp(-t / 0.2)
    x += norm(lap) * 0.12
    return finish(room(x, 0.45, 2600), 0.6, 1.0, 6000)


# ---------------------------------------------------------------- concrete stair

def crumble():
    s = 0.86
    t = t_axis(s)
    boom = sweep(85, 32, s, 3.5) * decay(s, 0.13)
    crack = lowpass(noise(s), 3000) * decay(s, 0.01)
    grit = grains(sparse(s, 2600 * np.exp(-t / 0.05)), 0.002)
    crunch = norm(resonator(grit, 650, 3)) + 0.7 * norm(resonator(grit, 1250, 3.5)) + 0.3 * norm(lowpass(grit, 3200))
    rubble = np.zeros(len(t))
    for _ in range(34):
        start = 0.03 + min(rng.exponential(0.17), 0.72)
        d = 0.06
        chunk = noise(d) * decay(d, rng.uniform(0.003, 0.009))
        piece = norm(resonator(chunk, rng.uniform(350, 1800), rng.uniform(3, 7)))
        if rng.uniform() < 0.35:
            piece += 0.6 * np.sin(TAU * rng.uniform(110, 190) * t_axis(d)) * decay(d, 0.018)
        rubble += at(piece * rng.uniform(0.3, 1.0) * np.exp(-start / 0.3), start, s)
    rumble = lowpass(noise(s), 180) * np.clip(t / 0.01, 0, 1) * np.exp(-t / 0.28)
    sand = band(noise(s), 1100, 3000) * np.clip(t / 0.05, 0, 1) * np.exp(-t / 0.22)
    x = (norm(boom) * 0.9 + norm(crack) * 0.4 + norm(crunch) * 0.7 + norm(rubble) * 0.6
         + norm(rumble) * 0.5 + norm(sand) * 0.1)
    return finish(room(x, 0.45, 2000), 0.95, 1.7, 5500)


# ---------------------------------------------------------------- radio

def static():
    s = 0.7
    t = t_axis(s)
    u = t / s
    hiss = band(noise(s), 300, 3300) * np.clip(0.55 + lowpass(noise(s), 35) * 14, 0.1, 1.3)
    crackle = band(grains(sparse(s, 140), 0.0015), 500, 3500)
    f = 700 + 28 * np.sin(TAU * 2.6 * t) + 11 * np.sin(TAU * 7.1 * t + 1.0) - 45 * u
    ph = TAU * np.cumsum(f) / RATE
    tone = np.sin(ph) + 0.55 * np.sin(ph * 1.012) + 0.18 * np.sin(2 * ph)
    tone = lowpass(tone * (0.6 + 0.4 * np.sin(TAU * 3.7 * t)), 2000)
    level = np.clip(t / 0.012, 0, 1) * (0.7 + 0.3 * np.exp(-t / 0.06)) * np.clip((s - t) / 0.14, 0, 1)
    x = (norm(hiss) * 0.55 + norm(crackle) * 0.35 + norm(tone) * 0.4) * level
    return finish(x, 0.75, 1.3, 5500)


# ---------------------------------------------------------------- shopping cart

def rattle():
    s = 0.6
    t = t_axis(s)
    rate = 36 + 7 * np.sin(TAU * 1.3 * t)
    ticks = stick_slip(rate, 0.35, 0.6)
    wheel = (0.7 * norm(resonator(ticks, 820, 7)) + norm(resonator(ticks, 1650, 10))
             + 0.5 * norm(resonator(ticks, 2550, 14)) + 0.4 * norm(resonator(ticks, 330, 4)))
    seams = np.zeros(len(t))
    for start in (0.045, 0.1, 0.24, 0.295, 0.435, 0.49):
        d = 0.08
        knock = sweep(170, 90, d, 4) * decay(d, 0.018) + 0.5 * norm(resonator(noise(d) * decay(d, 0.002), 900, 5))
        jingle = norm(resonator(noise(d) * decay(d, 0.002), 2300, 20))
        seams += at((knock + 0.2 * jingle) * rng.uniform(0.6, 1.0), start + rng.uniform(-0.008, 0.008), s)
    rumble = lowpass(noise(s), 260) * (0.6 + 0.4 * np.sin(TAU * np.cumsum(rate) / RATE))
    x = (norm(wheel) * 0.45 + norm(seams) * 0.7 + norm(rumble) * 0.3) * env(s, 0.04, 0.1)
    return finish(room(x, 0.3, 2400), 0.8, 1.3, 5500)


def clang():
    s = 0.35
    t = t_axis(s)
    ring = sum(g * np.sin(TAU * f * t) * decay(s, tau)
               for f, tau, g in ((380, 0.11, 1.0), (947, 0.07, 0.7), (1530, 0.05, 0.45), (2210, 0.035, 0.28), (3020, 0.022, 0.12)))
    wires = np.zeros(len(t))
    for start, g in ((0.0, 1.0), (0.032, 0.45), (0.066, 0.3), (0.105, 0.18)):
        d = 0.1
        tk = noise(d) * decay(d, 0.0015)
        w = sum(norm(resonator(tk, f * rng.uniform(0.98, 1.02), 18)) for f in (1180, 1215, 1690, 1745))
        wires += at(norm(w) * g, start, s)
    thud = sweep(150, 70, s, 4) * decay(s, 0.03)
    hit = lowpass(noise(s), 3500) * decay(s, 0.003)
    x = norm(ring) * 0.7 + norm(wires) * 0.55 + norm(thud) * 0.5 + norm(hit) * 0.3
    return finish(room(x, 0.3, 2600), 0.85, 1.4, 6500)


# ---------------------------------------------------------------- neighbours through the wall

VOWELS = {"a": (700, 1150, 2400), "o": (470, 820, 2450), "u": (330, 740, 2300), "e": (440, 1650, 2450), "uh": (520, 1250, 2400)}


def mumble(s, f0, start, vowels, syll, drift):
    """One muffled voice: a glottal saw through vowel formants that slide syllable by syllable."""
    t = t_axis(s)
    pitch = f0 * (1 + 0.07 * np.sin(TAU * 1.3 * t + drift) + 0.025 * np.sin(TAU * 4.7 * t))
    src = lowpass(2 * ((np.cumsum(pitch) / RATE) % 1) - 1, 1600) + 0.06 * noise(s)
    pos = np.clip((t - start) * syll - 0.5, 0, len(vowels) - 1)
    out = np.zeros(len(t))
    for k, (f1, f2, f3) in enumerate(vowels):
        w = np.clip(1 - np.abs(pos - k), 0, 1)
        out += w * (norm(resonator(src, f1, 5)) + 0.5 * norm(resonator(src, f2, 7)) + 0.12 * norm(resonator(src, f3, 9)))
    phase = np.clip((t - start) * syll + 0.15 * np.sin(TAU * 1.7 * t + drift), 0, None)
    beats = 0.3 + 0.7 * np.sin(np.pi * (phase % 1)) ** 0.8
    end = start + len(vowels) / syll
    return out * beats * np.clip((t - start) / 0.04, 0, 1) * np.clip((end - t) / 0.06, 0, 1)


def murmur():
    s = 0.6
    v = VOWELS
    a = mumble(s, 112, 0.0, [v["o"], v["uh"], v["u"], v["a"]], 7.4, 0.4)
    b = mumble(s, 88, 0.12, [v["a"], v["u"], v["e"]], 6.8, 2.1)
    x = lowpass(norm(a) * 0.8 + norm(b), 1500)
    return finish(room(x, 0.35, 1500), 0.6, 1.2, 3000)


SOUNDS = {
    "creature-creak": creak,
    "creature-thud": thud,
    "creature-collide": collide,
    "creature-listen": listen,
    "creature-slam": slam,
    "creature-clack": clack,
    "creature-scrape": scrape,
    "creature-dash": dash,
    "creature-snarl": snarl,
    "creature-flap": flap,
    "creature-zap": zap,
    "creature-hum": hum,
    "creature-flutter": flutter,
    "creature-splash": splash,
    "creature-drip": drip,
    "creature-crumble": crumble,
    "creature-static": static,
    "creature-rattle": rattle,
    "creature-clang": clang,
    "creature-murmur": murmur,
}


def write(name, data):
    path = os.path.join(OUT, name + ".wav")
    pcm = (np.clip(data, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())
    return path


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for name, make in SOUNDS.items():
        data = make()
        print(f"{write(name, data)}  {len(data) / RATE:.2f}s")
