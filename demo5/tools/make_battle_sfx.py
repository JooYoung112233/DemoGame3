"""Battle sound effects for the field battle (demo5).

Synthesised from noise/sine sources so they can be regenerated and tuned.
They are placeholders with the right timing and weight, not final foley:
replace any WAV under Assets/Audio/Battle with recorded audio of the same
name, or reassign clips on ExpeditionBattlePanel > BattlePresentation.

Run:  python tools/make_battle_sfx.py
"""
import os
import wave

import numpy as np

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Audio", "Battle")
rng = np.random.default_rng(49)


def t_axis(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def noise(seconds):
    return rng.uniform(-1, 1, int(RATE * seconds))


def decay(seconds, tau, delay=0.0):
    t = t_axis(seconds)
    e = np.exp(-np.maximum(t - delay, 0) / tau)
    e[t < delay] = 0
    return e


def sweep(f0, f1, seconds, curve=3.0):
    t = t_axis(seconds)
    f = f1 + (f0 - f1) * np.exp(-t * curve / seconds * 3)
    return np.sin(2 * np.pi * np.cumsum(f) / RATE)


def lowpass(x, cutoff):
    cutoff = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    a = 1 - np.exp(-2 * np.pi * cutoff / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += a[i] * (x[i] - acc)
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def resonator(x, freq, q):
    """RBJ band-pass biquad (0 dB peak)."""
    w0 = 2 * np.pi * freq / RATE
    alpha = np.sin(w0) / (2 * q)
    b0, b2 = alpha, -alpha
    a0, a1, a2 = 1 + alpha, -2 * np.cos(w0), 1 - alpha
    b0, b2, a1, a2 = b0 / a0, b2 / a0, a1 / a0, a2 / a0
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        v = b0 * x[i] + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, x[i]
        y2, y1 = y1, v
        y[i] = v
    return y


def room(x, amount=0.3, darkness=2500):
    """A few early reflections: small concrete room, not a hall."""
    out = x.copy()
    wet = lowpass(x, darkness)
    for delay, gain in ((0.017, 0.5), (0.029, 0.38), (0.043, 0.3), (0.061, 0.22), (0.089, 0.14), (0.121, 0.09)):
        n = int(delay * RATE)
        out[n:] += wet[:-n] * gain * amount
    return out


def pad(x, seconds):
    n = int(RATE * seconds)
    return np.pad(x, (0, max(0, n - len(x))))[:n]


def at(x, start, seconds):
    return pad(np.concatenate([np.zeros(int(start * RATE)), x]), seconds)


def finish(x, peak=0.9, drive=1.0, tone=None):
    if drive != 1.0:
        x = np.tanh(x * drive) / np.tanh(drive)
    if tone:
        # Soften the fizz that the drive adds to noise layers.
        x = lowpass(lowpass(x, tone), tone)
    x = x - np.mean(x)
    fade = min(len(x), int(0.004 * RATE))
    x[:fade] *= np.linspace(0, 1, fade)
    x[-fade * 3:] *= np.linspace(1, 0, fade * 3)
    return x / (np.max(np.abs(x)) + 1e-9) * peak


def swing():
    s = 0.26
    t = t_axis(s)
    shape = np.sin(np.clip(t / s, 0, 1) * np.pi) ** 2.2
    cut = 700 + 2600 * np.sin(np.clip(t / s * 1.1, 0, 1) * np.pi)
    return finish(highpass(lowpass(noise(s), cut), 350) * shape, 0.55)


def impact():
    s = 0.32
    thud = sweep(120, 44, s, 4) * decay(s, 0.07)
    slap = highpass(lowpass(noise(s), 2600), 700) * decay(s, 0.022)
    click = lowpass(noise(s), 5000) * decay(s, 0.004)
    return finish(thud * 0.95 + slap * 0.55 + click * 0.3, 0.9, 1.8, 6000)


def critical():
    s = 0.5
    body = impact()
    crack = highpass(noise(s), 2200) * decay(s, 0.014)
    boom = sweep(70, 34, s, 3) * decay(s, 0.15)
    ring = (np.sin(2 * np.pi * 910 * t_axis(s)) + 0.7 * np.sin(2 * np.pi * 1373 * t_axis(s))) * decay(s, 0.07)
    return finish(pad(body, s) * 0.9 + crack * 0.6 + boom * 0.7 + ring * 0.12, 0.95, 2.2, 8000)


def gunshot():
    s = 0.75
    t = t_axis(s)
    transient = highpass(noise(s), 180) * decay(s, 0.011)
    body = lowpass(noise(s), 600 + 4800 * np.exp(-t / 0.05)) * decay(s, 0.07)
    thump = sweep(95, 38, s, 4) * decay(s, 0.085)
    shot = transient * 1.0 + body * 0.9 + thump * 0.85
    return finish(room(shot, 0.55, 2200), 0.95, 2.6, 9000)


def miss():
    s = 0.2
    t = t_axis(s)
    shape = np.sin(np.clip(t / s, 0, 1) * np.pi) ** 3
    whoosh = highpass(lowpass(noise(s), 5200), 1400) * shape
    return finish(whoosh, 0.42)


def bite():
    s = 0.36
    crunch = np.zeros(int(RATE * s))
    for start, gain in ((0.0, 1.0), (0.034, 0.75), (0.071, 0.55)):
        burst = resonator(noise(0.06), 1150, 1.6) * decay(0.06, 0.016)
        crunch += at(burst * gain, start, s)
    t = t_axis(s)
    saw = 2 * ((t * 82) % 1) - 1
    growl = lowpass(saw, 420) * decay(s, 0.1) * (0.7 + 0.3 * np.sin(2 * np.pi * 27 * t))
    thud = sweep(130, 60, s, 4) * decay(s, 0.05)
    return finish(crunch * 1.2 + growl * 0.45 + thud * 0.5, 0.85, 1.6, 6500)


def block():
    s = 0.24
    x = np.zeros(int(RATE * s))
    tick = noise(s) * decay(s, 0.003)
    for freq, q, gain in ((640, 9, 1.0), (1190, 11, 0.7), (1760, 12, 0.45)):
        x += resonator(tick, freq, q) * gain
    knock = sweep(170, 90, s, 4) * decay(s, 0.03)
    return finish(x * 1.6 + knock * 0.5 + tick * 0.2, 0.8, 1.4, 7000)


def fall():
    s = 0.7
    t = t_axis(s)
    rustle = highpass(lowpass(noise(s), 3200), 900) * np.clip(t / 0.2, 0, 1) * (t < 0.23)
    hit = sweep(85, 38, s - 0.22, 4) * decay(s - 0.22, 0.09) + lowpass(noise(s - 0.22), 1400) * decay(s - 0.22, 0.05) * 0.7
    bounce = sweep(95, 45, s - 0.35, 4) * decay(s - 0.35, 0.05) * 0.35
    x = rustle * 0.25 + at(hit, 0.22, s) + at(bounce, 0.35, s)
    return finish(room(x, 0.25, 1800), 0.8, 1.5, 5000)


def step():
    s = 0.13
    t = t_axis(s)
    knock = np.sin(2 * np.pi * 185 * t) * decay(s, 0.028)
    tap = np.sin(2 * np.pi * 960 * t) * decay(s, 0.011)
    tick = lowpass(noise(s), 4200) * decay(s, 0.004)
    return finish(knock * 0.8 + tap * 0.45 + tick * 0.5, 0.6, 1.3, 6000)


def turn():
    s = 0.16
    t = t_axis(s)
    shape = np.clip(t / 0.018, 0, 1) * np.exp(-np.maximum(t - 0.018, 0) / 0.032)
    flick = highpass(noise(s), 2400) * shape
    return finish(flick + np.sin(2 * np.pi * 1500 * t) * decay(s, 0.006) * 0.3, 0.4)


def growl():
    s = 1.05
    t = t_axis(s)
    pitch = 78 + 9 * np.sin(2 * np.pi * 1.7 * t) + 5 * np.sin(2 * np.pi * 5.3 * t)
    phase = np.cumsum(pitch) / RATE
    saw = 2 * (phase % 1) - 1
    rough = 0.65 + 0.35 * lowpass(noise(s), 30) * 3
    voice = saw * rough
    formants = resonator(voice, 520, 4) * 1.0 + resonator(voice, 1080, 6) * 0.55 + resonator(voice, 2400, 8) * 0.18
    breath = resonator(noise(s), 900, 1.2) * 0.25
    env = np.clip(t / 0.16, 0, 1) * np.clip((s - t) / 0.4, 0, 1)
    return finish(room((formants + breath) * env, 0.35, 1600), 0.7, 1.8)


def heal():
    s = 0.7
    t = t_axis(s)
    # cloth wrap: fluttering band of noise, then a soft two-note chime
    flutter = 0.55 + 0.45 * np.sin(2 * np.pi * 23 * t)
    wrap = highpass(lowpass(noise(s), 3400), 700) * flutter * np.clip(t / 0.03, 0, 1) * np.exp(-t / 0.16)
    chime = (np.sin(2 * np.pi * 784 * t) * decay(s, 0.28, 0.12) + 0.8 * np.sin(2 * np.pi * 1175 * t) * decay(s, 0.3, 0.22)
             + 0.25 * np.sin(2 * np.pi * 2350 * t) * decay(s, 0.12, 0.22))
    return finish(wrap * 0.5 + chime * 0.35, 0.6, 1.0, 7000)


SOUNDS = {
    "battle-swing": swing,
    "battle-impact": impact,
    "battle-critical": critical,
    "battle-gunshot": gunshot,
    "battle-miss": miss,
    "battle-bite": bite,
    "battle-block": block,
    "battle-fall": fall,
    "battle-step": step,
    "battle-turn": turn,
    "battle-growl": growl,
    "battle-heal": heal,
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
