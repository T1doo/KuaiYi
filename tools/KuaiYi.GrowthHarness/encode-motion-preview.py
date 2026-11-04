"""Encode authentic WPF PNG samples; no geometry-generated/interpolated frames.

Usage: python encode-motion-preview.py <capture-directory> <new-output-directory>
Optional review utility; requires Pillow. It is not a product dependency.
"""
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

source, target = map(lambda value: Path(value).resolve(), sys.argv[1:3])
data = json.loads((source / "frames.json").read_text(encoding="utf-8-sig"))
assert data["TerminalCaptured"] and len(data["Frames"]) >= 3
frames = data["Frames"]
left, top = min(f["Card"]["Left"] for f in frames), min(f["Card"]["Top"] for f in frames)
right, bottom = max(f["Card"]["Right"] for f in frames), max(f["Card"]["Bottom"] for f in frames)
size = (right - left + 32, bottom - top + 58)
target.mkdir(parents=True, exist_ok=True)
names = ["opening.gif", "terminal.png", "frames.json", "contact-sheet.png"]
if any((target / name).exists() for name in names):
    raise FileExistsError("Existing review outputs must not be overwritten")
font_path = Path("C:/Windows/Fonts/segoeui.ttf")
font = ImageFont.truetype(str(font_path), 12) if font_path.exists() else ImageFont.load_default()
images, durations = [], []
for index, frame in enumerate(frames):
    board = Image.new("RGB", size, "#E9EDF3")
    with Image.open(source / frame["File"]) as raw:
        assert raw.size == (frame["Card"]["Width"], frame["Card"]["Height"])
        board.paste(raw, (frame["Card"]["Left"] - left + 16, frame["Card"]["Top"] - top + 16), raw)
    ImageDraw.Draw(board).text((16, size[1] - 25), "Actual WPF frame samples - no interpolation - not DWM video", fill="#536174", font=font)
    images.append(board)
    durations.append(max(10, min(1000, round(frames[index+1]["Milliseconds"]-frame["Milliseconds"]))) if index+1<len(frames) else 1200)
images[0].save(target / "opening.gif", save_all=True, append_images=images[1:], duration=durations, loop=0, disposal=2, optimize=False)
for name, original in [("terminal.png", source / frames[-1]["File"]), ("frames.json", source / "frames.json")]:
    with (target / name).open("xb") as output:
        output.write(original.read_bytes())
sheet = Image.new("RGB", (size[0]*2, size[1]*2), "white")
for slot, index in enumerate(sorted(set([0, len(images)//3, 2*len(images)//3, len(images)-1]))):
    sheet.paste(images[index], ((slot%2)*size[0], (slot//2)*size[1]))
sheet.save(target / "contact-sheet.png")
print(json.dumps({"Frames":len(frames),"SampleMilliseconds":sum(durations[:-1]),"FinalHoldMilliseconds":1200,"Output":str(target)}))
