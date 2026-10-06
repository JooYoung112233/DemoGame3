"""Turn a captured frame folder (frame-###.png + times.txt from the Verify* recorders) into a review GIF.

Frame timing follows the real capture timestamps, so the GIF plays at game speed.
Run:  python tools/make_battle_gif.py <frame folder> <out.gif> [width]
"""
import os
import sys

from PIL import Image


def make(folder, out, width=None, hold_last=0.6):
    names = sorted(n for n in os.listdir(folder) if n.startswith("frame-") and n.endswith(".png"))
    if not names:
        raise SystemExit("no frames in " + folder)
    times = [float(t) for t in open(os.path.join(folder, "times.txt"), encoding="utf-8").read().split()]
    frames = []
    for name in names:
        im = Image.open(os.path.join(folder, name)).convert("RGB")
        if width and im.width != width:
            im = im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
        frames.append(im)
    durations = [max(20, round((times[i + 1] - times[i]) * 1000)) for i in range(len(times) - 1)] + [round(hold_last * 1000)]
    # One shared adaptive palette keeps colours steady from frame to frame.
    sample = Image.new("RGB", (frames[0].width, frames[0].height * 3))
    for k, i in enumerate((0, len(frames) // 2, len(frames) - 1)):
        sample.paste(frames[i], (0, frames[0].height * k))
    palette = sample.quantize(colors=255, method=Image.MEDIANCUT)
    paletted = [f.quantize(palette=palette, dither=Image.NONE) for f in frames]
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    paletted[0].save(out, save_all=True, append_images=paletted[1:], duration=durations[: len(paletted)], loop=0, optimize=True, disposal=1)
    return len(frames), sum(durations) / 1000, os.path.getsize(out)


if __name__ == "__main__":
    folder, out = sys.argv[1], sys.argv[2]
    width = int(sys.argv[3]) if len(sys.argv) > 3 else None
    n, seconds, size = make(folder, out, width)
    print(f"{out}: {n} frames, {seconds:.1f}s, {size / 1024:.0f} KB")
