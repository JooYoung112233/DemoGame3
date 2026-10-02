"""Author integer pixel clusters and submit every cel to Pixel MCP / Aseprite.

No resampling of the reference: this is a new, native 128x160 pixel study.
The source is an editable coordinate drawing; Aseprite writes all art outputs.
"""
import argparse
import json
import math
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
WORKSPACE = ROOT.parents[2]
sys.path.insert(0, str(WORKSPACE / "tools" / "pixel-mcp"))
from mcp_probe import Client

W, H = 128, 160
P = {
    "ink": "#28232C", "deep": "#3C3034", "hair0": "#483231",
    "hair1": "#654438", "hair2": "#87583E", "hair3": "#AC794F",
    "hair4": "#CA9562", "skin0": "#AB6248", "skin1": "#D38A59",
    "skin2": "#F0B57B", "skin3": "#FFDA9C", "cloth0": "#353441",
    "cloth1": "#4A4852", "cloth2": "#66606A", "leather0": "#513834",
    "leather1": "#754A35", "leather2": "#98633E", "leather3": "#BA8350",
    "leather4": "#D8A66A", "cream0": "#A49478", "cream1": "#D1C2A0",
    "cream2": "#F0E2BB", "cream3": "#FFF2D2", "steel0": "#424C60",
    "steel1": "#64758A", "steel2": "#96ADB8", "steel3": "#CCDBD6",
    "steel4": "#F0EBDD", "gold0": "#82613A", "gold1": "#BA873E",
    "gold2": "#E7B953", "gold3": "#FFE49C", "shadow": "#24202B54",
}


def bresenham(a, b):
    x, y = map(round, a)
    x2, y2 = map(round, b)
    dx, dy = abs(x2-x), -abs(y2-y)
    sx, sy = 1 if x < x2 else -1, 1 if y < y2 else -1
    err = dx + dy
    while True:
        yield x, y
        if (x, y) == (x2, y2):
            break
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x += sx
        if e2 <= dx:
            err += dx
            y += sy


class Cel:
    def __init__(self, transform=None):
        self.pixels = {}
        self.transform = transform or (lambda x, y: (x, y))

    def point(self, x, y, color):
        x, y = round(x), round(y)
        if not (0 <= x < W and 0 <= y < H):
            raise ValueError((x, y))
        self.pixels[x, y] = P.get(color, color)

    def poly(self, points, color, outline=None):
        pts = [tuple(map(round, self.transform(x, y))) for x, y in points]
        # Scan conversion at pixel centres. All final painting is sent to MCP.
        for y in range(min(p[1] for p in pts), max(p[1] for p in pts) + 1):
            crossings = []
            cy = y + 0.5
            for a, b in zip(pts, pts[1:] + pts[:1]):
                if min(a[1], b[1]) <= cy < max(a[1], b[1]):
                    crossings.append(a[0] + (cy-a[1])*(b[0]-a[0])/(b[1]-a[1]))
            crossings.sort()
            for left, right in zip(crossings[::2], crossings[1::2]):
                for x in range(math.ceil(left - .5), math.ceil(right - .5)):
                    self.point(x, y, color)
        for a, b in zip(pts, pts[1:] + pts[:1]):
            for x, y in bresenham(a, b):
                self.point(x, y, outline or color)

    def line(self, points, color):
        pts = [self.transform(x, y) for x, y in points]
        for a, b in zip(pts, pts[1:]):
            for x, y in bresenham(a, b):
                self.point(x, y, color)

    def rect(self, x, y, width, height, color, outline=None):
        self.poly([(x,y),(x+width-1,y),(x+width-1,y+height-1),(x,y+height-1)], color, outline)

    def ellipse(self, cx, cy, rx, ry, color):
        pts = [(cx+rx*math.cos(i*math.tau/40), cy+ry*math.sin(i*math.tau/40)) for i in range(40)]
        self.poly(pts, color)

    def payload(self):
        return [{"x":x, "y":y, "color":color} for (x,y),color in sorted(self.pixels.items(), key=lambda p:(p[0][1],p[0][0]))]


def make_leg(cx, advance, lift, sway):
    c = Cel()
    hx = cx + sway
    ax = cx + (1 if cx < 62 else -1) * round(lift/3)
    ay = 124 + advance - lift
    knee_y = 112 + advance*.4 - lift*.7
    c.poly([(hx-7,99),(hx+7,99),(hx+8,knee_y),(ax+6,ay-5),(ax-6,ay-4),(hx-8,knee_y)], "cloth0", "ink")
    c.poly([(hx-6,103),(hx,104),(ax+1,ay-5),(ax-5,ay-4)], "cloth1")
    c.poly([(hx-8,108),(hx+7,108),(hx+8,112+advance*.3),(hx-7,114+advance*.3)], "leather1", "ink")
    c.line([(hx-6,110),(hx+5,110)], "leather3")
    # The cuff and top-facing toe change spacing as the knee lifts.
    toe = 12 - round(lift/2)
    c.poly([(ax-7,ay-9),(ax+5,ay-11),(ax+7,ay-3),(ax+9,ay+5),
            (ax+7,ay+toe),(ax+2,ay+toe+3),(ax-6,ay+toe+2),(ax-9,ay+7)], "leather1", "ink")
    c.poly([(ax-6,ay-7),(ax+3,ay-9),(ax+5,ay-2),(ax+6,ay+7),
            (ax+3,ay+toe),(ax-4,ay+toe),(ax-7,ay+6)], "leather2")
    c.poly([(ax-5,ay-4),(ax-1,ay-5),(ax+2,ay+4),(ax,ay+8),(ax-5,ay+6)], "leather3")
    c.line([(ax-7,ay+toe-1),(ax-3,ay+toe+2),(ax+3,ay+toe+2),(ax+7,ay+toe-1)], "cloth2")
    c.poly([(ax-8,ay-10),(ax+5,ay-12),(ax+7,ay-7),(ax-7,ay-5)], "leather0", "ink")
    c.line([(ax-6,ay-9),(ax+3,ay-11)], "leather3")
    c.line([(ax-6,ay-5),(ax+4,ay-7)], "leather2")
    c.line([(ax-6,ay),(ax+4,ay-2)], "leather1")
    return c


def make_arm(side, swing, bob, sway, roll):
    # Segment-aware deformation changes the elbow and wrist; no raster stretching.
    shoulder_x = 31 if side == -1 else 92
    wrist_x = 23 if side == -1 else 100
    wrist_y = 108 + swing
    def warp(x,y):
        t = max(0,min(1,(y-77)/31))
        return x+sway+side*round(abs(swing)*.18)*t, y+bob+roll*side*(1-t)+swing*t
    c=Cel(warp)
    if side == -1:
        points=[(27,73),(39,78),(35,89),(30,98),(31,107),(28,114),(22,116),(17,112),(17,105),(21,96),(23,84)]
        c.poly(points,"leather1","ink")
        c.poly([(28,77),(35,81),(31,90),(25,98),(22,97),(24,85)],"cloth1")
        c.poly([(23,94),(31,96),(29,104),(24,108),(19,105)],"leather2")
        c.poly([(20,105),(25,103),(29,107),(27,112),(23,113),(20,110)],"leather2")
        c.poly([(20,104),(23,103),(24,109),(22,110)],"leather3")
        c.line([(21,95),(29,97),(30,101)],"leather0")
        c.line([(20,100),(27,102)],"leather3")
        c.line([(25,108),(26,111)],"leather0")
        c.line([(28,81),(32,83)],"cloth2")
    else:
        c.poly([(86,74),(97,78),(100,88),(105,98),(107,107),(105,113),(100,116),(94,112),(93,104),(94,98),(90,88)],"leather1","ink")
        c.poly([(88,76),(95,79),(98,88),(94,93),(89,88)],"cloth1")
        c.poly([(95,93),(103,97),(105,102),(101,106),(95,103)],"leather2")
        c.poly([(95,104),(101,103),(105,108),(102,112),(98,112)],"leather2")
        c.line([(97,96),(103,99)],"leather3")
        c.line([(96,101),(104,104)],"leather0")
        c.line([(97,107),(100,105),(103,106)],"leather3")
        c.line([(101,109),(103,110)],"leather0")
        c.line([(92,78),(95,83)],"cloth2")
    return c, (wrist_x+sway+side*round(abs(swing)*.18), wrist_y+bob)


def make_sword(hand, sway_angle):
    c=Cel()
    angle=math.radians(21+sway_angle)
    d=(math.sin(angle),math.cos(angle)); n=(d[1],-d[0])
    def pos(across,along):
        return hand[0]+n[0]*across+d[0]*along, hand[1]+n[1]*across+d[1]*along
    def poly(points,color,outline=None):
        c.poly([pos(x,y) for x,y in points],color,outline)
    poly([(-2,-11),(2,-11),(3,-8),(2,8),(-2,8),(-3,-8)],"cloth0","ink")
    for y in [-6,-3,0,3]:
        c.line([pos(-1,y),pos(1,y+1)],"cloth2")
    poly([(-3,-12),(0,-14),(3,-12),(3,-9),(0,-7),(-3,-9)],"gold1","ink")
    poly([(-2,-11),(0,-13),(2,-11),(0,-9)],"gold3")
    poly([(-4,11),(4,11),(4,35),(0,44),(-4,35)],"steel1","ink")
    poly([(-3,12),(0,13),(0,40),(-3,34)],"steel3")
    poly([(0,13),(3,12),(3,34),(0,40)],"steel2")
    c.line([pos(-2,14),pos(-2,32),pos(0,39)],"steel4")
    poly([(-9,6),(-6,5),(-4,7),(5,7),(7,5),(10,7),(9,12),(6,13),(4,11),(-5,11),(-7,13),(-10,11)],"gold1","ink")
    poly([(-8,7),(-6,7),(-5,9),(5,9),(7,7),(9,8),(8,10),(-8,10)],"gold2")
    c.line([pos(-7,7),pos(-6,7)],"gold3")
    poly([(-4,7),(4,7),(4,11),(-4,11)],"steel1","ink")
    c.line([pos(-3,8),pos(3,8)],"steel3")
    return c


def build_pose(frame=None):
    # Contact, down, passing, up; repeated with the other supporting foot.
    if frame is None:
        a,b,la,lb,bob,sway,roll = 0,0,0,0,0,0,0
        arm_l=arm_r=0
    else:
        advances=[7,3,-2,-7,-8,-4,1,6]
        lifts=[0,0,0,1,2,5,5,2]
        a,b=advances[frame],advances[(frame+4)%8]
        la,lb=lifts[frame],lifts[(frame+4)%8]
        bob=[0,1,0,-1,0,1,0,-1][frame]
        sway=[-1,-1,0,0,1,1,0,0][frame]
        roll=[-1,-1,0,1,1,1,0,-1][frame]
        arm_l=round(-a*.55);arm_r=round(-b*.3)
    def body(x,y):
        return x+sway, y+bob+round((x-62)*roll/45)
    shadow=Cel(); shadow.ellipse(62,140,32,7,"shadow")
    layers={"00 Contact shadow":shadow,
            "01 Left leg and boot":make_leg(49,a,la,sway),
            "02 Right leg and boot":make_leg(76,b,lb,sway)}
    c=Cel(body)
    c.poly([(41,74),(53,69),(73,69),(86,74),(90,93),(86,111),(73,116),(63,112),(53,117),(38,111),(35,93)],"leather1","ink")
    c.poly([(42,76),(82,76),(86,92),(79,105),(63,107),(41,102),(38,89)],"leather2")
    c.poly([(42,81),(56,85),(52,101),(43,103),(40,92)],"leather3")
    c.poly([(79,82),(86,90),(84,108),(73,113),(65,109),(67,100)],"leather0")
    c.poly([(79,90),(84,94),(82,107),(74,111),(67,109),(69,100)],"leather1")
    c.poly([(40,101),(57,103),(60,111),(52,115),(40,109)],"leather2")
    c.line([(41,106),(50,111),(55,111)],"leather3")
    c.line([(62,106),(63,113)],"ink")
    c.poly([(39,74),(51,69),(73,68),(87,75),(83,86),(65,93),(48,88),(39,81)],"cloth0","ink")
    c.poly([(43,75),(53,71),(75,71),(84,76),(79,83),(63,88),(49,84)],"cloth1")
    c.line([(45,83),(60,89),(71,88)],"cloth2")
    layers["03 Leather coat and undershirt"]=c
    armleft,_=make_arm(-1,arm_l,bob,sway,roll)
    armright,hand=make_arm(1,arm_r,bob,sway,roll)
    layers["04 Sword - replaceable"]=make_sword(hand, 0 if frame is None else roll*2)
    layers["05 Left arm and glove"]=armleft
    layers["06 Right arm and glove"]=armright
    c=Cel(body)
    c.poly([(39,98),(62,101),(84,96),(85,102),(63,108),(38,104)],"leather0","ink")
    c.line([(40,99),(61,103),(82,98)],"leather3")
    c.poly([(34,92),(41,92),(47,98),(45,110),(39,113),(33,108)],"leather1","ink")
    c.poly([(35,93),(40,94),(44,98),(39,103),(34,100)],"leather3","leather0")
    c.rect(38,99,2,3,"gold2")
    c.poly([(57,100),(68,99),(71,101),(70,108),(58,110),(55,108),(55,102)],"gold1","ink")
    c.poly([(58,102),(67,101),(68,103),(67,106),(58,107)],"leather0")
    c.line([(57,103),(57,101),(68,100)],"gold3")
    c.line([(58,108),(68,107),(69,103)],"gold2")
    layers["07 Belt buckle and pouch"]=c
    c=Cel(body)
    c.poly([(26,56),(37,52),(44,55),(49,66),(44,81),(35,87),(22,81),(17,69),(18,63)],"steel0","ink")
    c.poly([(26,58),(37,54),(42,56),(46,66),(39,76),(31,79),(22,74),(19,66)],"steel1")
    c.poly([(26,58),(36,55),(38,65),(32,72),(23,68)],"steel2")
    c.poly([(27,57),(35,55),(34,59),(24,64),(21,67),(20,64)],"steel3")
    c.poly([(38,57),(43,58),(46,66),(40,74),(34,71),(39,66)],"steel2")
    c.line([(35,56),(38,64),(34,70),(27,73)],"steel3")
    c.line([(22,76),(33,83),(40,80)],"steel1")
    for x,y in [(20,72),(35,82)]:
        c.poly([(x-2,y-1),(x,y-3),(x+3,y-1),(x+3,y+2),(x,y+3),(x-2,y+1)],"gold1","ink")
        c.rect(x-1,y-1,3,3,"gold2")
        c.rect(x,y-2,2,2,"gold3")
    layers["08 Left pauldron - replaceable"]=c
    c=Cel(body)
    c.poly([(39,68),(44,64),(55,70),(65,74),(77,69),(84,64),(88,70),(85,80),(76,86),(64,91),(49,87),(42,80)],"cream1","ink")
    c.poly([(42,69),(46,68),(56,74),(65,78),(77,74),(83,68),(85,71),(81,79),(65,86),(51,82),(44,77)],"cream2")
    c.poly([(42,69),(45,67),(49,72),(59,77),(58,80),(48,77),(44,74)],"cream3")
    c.poly([(76,81),(83,79),(84,90),(82,106),(74,105),(72,93)],"cream1","ink")
    c.poly([(77,83),(81,82),(81,91),(79,103),(75,102),(75,94)],"cream2")
    c.line([(77,86),(77,98)],"cream3")
    c.line([(76,104),(80,104)],"cream0")
    layers["09 Cream scarf"]=c
    # Head: top plane dominates; the face is foreshortened below the fringe.
    head=Cel(lambda x,y:(x+sway,y+bob))
    head.poly([(41,55),(45,51),(78,51),(84,56),(83,64),(77,73),(69,80),(61,82),(53,77),(46,70)],"skin1","ink")
    head.poly([(45,55),(79,55),(80,63),(73,73),(63,79),(54,74),(47,66)],"skin2")
    head.poly([(49,62),(55,65),(63,69),(69,64),(77,61),(74,70),(64,77),(56,73)],"skin3")
    head.poly([(39,56),(42,54),(45,58),(45,64),(42,65),(39,62)],"skin1","ink")
    head.poly([(81,55),(84,53),(87,55),(86,61),(83,65),(80,61)],"skin1","ink")
    head.line([(41,58),(43,61)],"skin3")
    head.line([(84,55),(85,58),(83,61)],"skin3")
    head.line([(49,68),(52,69),(55,69)],"hair0")
    head.line([(69,68),(74,66)],"hair0")
    head.poly([(50,70),(55,71),(54,73),(51,73)],"ink")
    head.poly([(69,70),(73,69),(73,72),(70,73)],"ink")
    head.line([(63,73),(62,75),(64,76)],"skin1")
    head.line([(61,79),(64,80),(66,79)],"skin0")
    layers["10 Face and ears"]=head
    c=Cel(lambda x,y:(x+sway,y+bob))
    c.poly([(29,47),(33,37),(28,38),(37,29),(31,25),(42,24),(40,17),(54,20),
            (52,12),(67,14),(77,18),(82,25),(85,19),(91,28),(90,34),(98,39),(92,44),
            (97,53),(91,51),(91,60),(85,57),(81,65),(77,62),(69,70),(62,65),
            (59,71),(53,65),(47,69),(44,63),(38,65),(38,58),(31,60),(32,50),(26,52)],"hair0","ink")
    # Clustered locks: large planes, selective highlights, no per-frame noise.
    c.poly([(54,14),(65,16),(76,21),(80,30),(78,39),(70,31),(65,23)],"hair2")
    c.poly([(55,15),(65,17),(71,21),(74,29),(67,24),(64,20)],"hair3")
    c.poly([(43,20),(53,23),(61,31),(67,41),(55,38),(48,31),(46,26)],"hair2")
    c.poly([(44,21),(53,25),(58,31),(51,29)],"hair3")
    c.poly([(34,27),(42,27),(48,31),(40,35),(33,35),(39,31)],"hair1")
    c.poly([(35,27),(41,28),(45,30),(39,32)],"hair3")
    c.poly([(35,37),(44,32),(52,34),(60,40),(47,41),(36,46),(29,49)],"hair2")
    c.poly([(36,37),(44,34),(50,35),(53,37),(43,38)],"hair3")
    c.poly([(36,47),(49,42),(56,42),(50,50),(43,54),(35,57)],"hair1")
    c.poly([(38,46),(48,43),(53,43),(47,47),(40,51)],"hair2")
    c.poly([(46,48),(54,44),(61,46),(57,53),(50,61),(46,65),(43,58)],"hair2")
    c.poly([(48,48),(54,46),(57,46),(54,51),(47,56)],"hair3")
    c.poly([(57,48),(63,43),(68,46),(65,54),(59,67),(54,61)],"hair1")
    c.poly([(60,48),(63,47),(63,52),(58,61),(58,55)],"hair2")
    c.poly([(63,45),(68,38),(73,43),(77,50),(76,59),(69,66),(64,59)],"hair1")
    c.poly([(67,43),(70,42),(73,47),(72,52),(66,57)],"hair2")
    c.poly([(74,41),(81,36),(86,38),(86,46),(89,55),(82,52),(77,49)],"hair2")
    c.poly([(77,40),(82,38),(85,39),(84,43),(80,43)],"hair3")
    c.poly([(84,25),(85,22),(89,29),(87,36),(83,39),(83,31)],"hair1")
    c.poly([(85,25),(87,29),(85,34)],"hair3")
    c.poly([(87,36),(92,37),(95,40),(88,42),(83,42)],"hair2")
    c.line([(88,37),(92,39)],"hair3")
    c.poly([(84,46),(90,46),(93,51),(88,49),(87,48)],"hair1")
    c.poly([(76,51),(81,49),(82,53),(80,61),(76,58)],"hair2")
    c.poly([(49,40),(58,35),(65,36),(70,41),(63,42),(56,45),(50,45)],"hair3")
    c.poly([(52,40),(59,37),(64,38),(58,41)],"hair4")
    c.line([(60,18),(66,20)],"hair4")
    c.line([(38,38),(42,36)],"hair4")
    layers["11 Brown hair"]=c
    return layers


def unpack(result):
    if "structuredContent" in result:
        return result["structuredContent"]
    for item in result.get("content",[]):
        if item.get("type")=="text":
            return json.loads(item["text"])
    raise ValueError(result)


def run(walk=False):
    name="south-walk" if walk else "south-idle"
    count=8 if walk else 1
    frames=[build_pose(i if walk else None) for i in range(count)]
    colors=sorted({c for frame in frames for cel in frame.values() for c in cel.pixels.values() if len(c)==7})
    events=[]
    (ROOT/"review").mkdir(exist_ok=True)
    (ROOT/"frames").mkdir(exist_ok=True)
    with Client() as client:
        def call(tool,**args):
            value=unpack(client.call(tool,**args))
            events.append({"tool":tool,"result":value})
            return value
        sprite=call("create_canvas",width=W,height=H,color_mode="rgb")["file_path"].replace("\\","/")
        for layer in frames[0]:
            call("add_layer",sprite_path=sprite,layer_name=layer)
        call("delete_layer",sprite_path=sprite,layer_name="Layer 1")
        call("set_palette",sprite_path=sprite,colors=colors)
        call("set_frame_duration",sprite_path=sprite,frame_number=1,duration_ms=110 if walk else 500)
        for _ in range(count-1):
            call("add_frame",sprite_path=sprite,duration_ms=110)
        for f, layers in enumerate(frames,1):
            for layer,cel in layers.items():
                call("draw_pixels",sprite_path=sprite,layer_name=layer,frame_number=f,pixels=cel.payload())
            call("export_sprite",sprite_path=sprite,output_path=(ROOT/"frames"/f"{name}-{f:02}.png").as_posix(),format="png",frame_number=f)
            print(f"{name}: painted and exported frame {f}/{count}",flush=True)
        call("create_tag",sprite_path=sprite,tag_name="walk_south" if walk else "idle_south",from_frame=1,to_frame=count,direction="forward")
        native=(ROOT/f"{name}.aseprite").as_posix()
        call("save_as",sprite_path=sprite,output_path=native)
        info=call("get_sprite_info",sprite_path=native)
        call("export_sprite",sprite_path=native,output_path=(ROOT/f"{name}.png").as_posix(),format="png",frame_number=1)
        if walk:
            call("export_sprite",sprite_path=native,output_path=(ROOT/f"{name}.gif").as_posix(),format="gif",frame_number=0)
            call("export_spritesheet",sprite_path=native,output_path=(ROOT/f"{name}-sheet.png").as_posix(),layout="horizontal",padding=0,include_json=True)
        # Exact integer nearest-neighbour inspection copies; not new source resolution.
        preview=(ROOT/"review"/f"{name}-4x.aseprite").as_posix()
        call("save_as",sprite_path=native,output_path=preview)
        call("scale_sprite",sprite_path=preview,scale_x=4,scale_y=4,algorithm="nearest")
        call("export_sprite",sprite_path=preview,output_path=(ROOT/"review"/f"{name}-4x.png").as_posix(),format="png",frame_number=1)
        if walk:
            call("export_sprite",sprite_path=preview,output_path=(ROOT/"review"/f"{name}-4x.gif").as_posix(),format="gif",frame_number=0)
        (ROOT/f"{name}-mcp-log.json").write_text(json.dumps({"info":info,"events":events},indent=2),encoding="utf-8")
        print(json.dumps(info,ensure_ascii=False),flush=True)


if __name__=="__main__":
    parser=argparse.ArgumentParser()
    parser.add_argument("--walk",action="store_true")
    args=parser.parse_args()
    run(args.walk)
