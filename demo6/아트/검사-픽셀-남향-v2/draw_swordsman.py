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
WALK_FRAMES = 12
FRAME_MS = 80
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


def foot_state(phase):
    if phase is None:
        return 0., 0., True
    phase %= 1
    if phase <= .60:
        return 11 - 22*phase/.60, 0., True
    u=(phase-.60)/.40
    return -11+22*(u*u*(3-2*u)), 4.5*math.sin(math.pi*u), False


def project(y,z):
    return 138 + y*.52 - z*.84


def make_leg(cx, phase, sway, bob):
    """Two fixed-length leg segments, projected from a forward/up plane."""
    forward,lift,planted=foot_state(phase)
    hip_y,hip_z=0.,36.-bob/.84
    ankle_y,ankle_z=forward,3.+lift
    dy,dz=ankle_y-hip_y,ankle_z-hip_z
    distance=math.hypot(dy,dz)
    bend=math.sqrt(max(0,18**2-(distance/2)**2))
    knee_y=(hip_y+ankle_y)/2-dz/distance*bend
    knee_z=(hip_z+ankle_z)/2+dy/distance*bend
    hy=project(hip_y,hip_z)
    ky=project(knee_y,knee_z)
    ay=project(ankle_y,ankle_z)
    cy=project(knee_y,knee_z)-3
    side=-1 if cx<62 else 1
    hx=cx+sway
    kx=cx+sway*.5+side*.6
    ax=cx+side*.5
    toe_y=ay+6.5-lift*.16
    tx=ax+side*1.2
    c=Cel(lambda x,y:(cx+(x-cx)*1.18,y))
    c.poly([(hx-6,hy-8),(hx+6,hy-8),(kx+6,ky-2),(ax+4.5,ay+1),
            (ax-4.5,ay+1),(kx-6,ky-1)],"cloth0","ink")
    c.poly([(hx-4,hy-5),(hx+1,hy-5),(kx+2,ky),(ax+1,ay-1),(ax-3,ay-1),(kx-4,ky)],"cloth1")
    c.line([(hx-4,hy),(kx-3,ky-3)],"cloth2")
    # Knee strap wraps the trousers; it is not a black gap between separate parts.
    c.poly([(kx-5,ky-3),(kx+5,ky-4),(kx+5,ky-1),(kx-5,ky)],"leather1")
    c.line([(kx-4,ky-3),(kx+3,ky-4)],"leather3")
    # A single continuous boot silhouette joins the shaft, ankle and toe box.
    c.poly([(ax-5,cy),(ax-2,cy-2),(ax+4,cy-2),(ax+5,ay),
            (tx+6,toe_y-3),(tx+6,toe_y),(tx+3,toe_y+2),(tx-3,toe_y+2),
            (tx-6,toe_y),(ax-6,ay+1)],"leather1","ink")
    c.poly([(ax-4,cy+1),(ax+2,cy),(ax+3,ay-1),(tx+4,toe_y-3),
            (tx+3,toe_y),(tx-3,toe_y),(ax-4,ay+1)],"leather2")
    c.poly([(ax-3,cy+2),(ax,cy+1),(ax+1,ay-1),(tx-1,ay+2),
            (tx-4,ay+2)],"leather3")
    c.poly([(tx-3,toe_y-4),(tx+2,toe_y-5),(tx+4,toe_y-2),(tx+1,toe_y),(tx-3,toe_y-1)],"leather3")
    c.line([(tx-2,toe_y-4),(tx+1,toe_y-4)],"leather4")
    c.line([(tx-4,toe_y+1),(tx+2,toe_y+2),(tx+5,toe_y)],"cloth2")
    c.poly([(ax-5,cy-1),(ax+4,cy-2),(ax+5,cy+1),(ax-5,cy+2)],"leather0")
    c.line([(ax-4,cy-1),(ax+3,cy-2)],"leather3")
    c.line([(ax-4,cy+2),(ax+3,cy+1)],"leather2")
    if ay-cy>5:
        c.line([(ax-3,ay-2),(ax+2,ay-3)],"leather1")
    return c


def make_arm(side, swing, bob, sway, roll):
    shoulder=(35 if side<0 else 88.5,77)
    sx,sy=shoulder[0]+sway,shoulder[1]+bob+side*roll*.8
    ex=sx+side*(5.7+abs(swing)*.12)
    ey=sy+14+swing*.4
    wx=ex+side*1.4-swing*.1
    wy=ey+12+swing*.42
    c=Cel()
    # Continuous shoulder/elbow/wrist shell, with an elbow bulge and wrist taper.
    c.poly([(sx-6,sy-3),(sx+4,sy-4),(sx+7,sy+1),(ex+5.5,ey-2),
            (ex+5,ey+3),(wx+4,wy+2),(wx-4,wy+2),(ex-5,ey+3),
            (ex-5.5,ey-1),(sx-7,sy+3)],"skin1","ink")
    c.poly([(sx-4,sy),(sx+3,sy-1),(ex+3,ey-2),(ex+2,ey+3),
            (wx+2,wy),(wx-3,wy),(ex-3,ey+1)],"skin2")
    c.line([(ex-3,ey-1),(ex-2,ey+2),(wx-2,wy-2)],"skin3")
    c.poly([(sx-6,sy-3),(sx+4,sy-4),(sx+7,sy+1),(ex+5,ey-5),
            (ex+3,ey-1),(ex-4,ey),(ex-6,ey-5),(sx-7,sy+3)],"cloth1","ink")
    c.poly([(sx-4,sy-1),(sx+1,sy-2),(ex+1,ey-5),(ex-3,ey-4)],"cloth2")
    c.poly([(ex-5,ey-4),(ex+4,ey-5),(ex+3,ey-1),(ex-3,ey)],"cloth0")
    # Bracer overlaps skin and glove. Inner joins use material shadows, not black rings.
    c.poly([(ex-4,ey+4),(ex+4,ey+3),(wx+4,wy),(wx+3,wy+4),
            (wx-4,wy+3),(wx-5,wy-1)],"leather1","ink")
    c.poly([(ex-3,ey+5),(ex,ey+4),(wx+1,wy),(wx,wy+3),(wx-3,wy+2)],"leather3")
    c.line([(ex-3,ey+6),(ex+3,ey+5)],"leather2")
    c.line([(wx-3,wy-1),(wx+3,wy-2)],"leather0")
    # Draw the glove in coordinates aligned to the palm; the sword uses this same anchor.
    gx,gy=wx,wy+4
    def hand_poly(points,color,outline=None):
        c.poly([(gx+x,gy+y) for x,y in points],color,outline)
    if side<0:
        hand_poly([(-3,-4),(2,-4),(4,-1),(4,3),(2,7),(-2,8),(-5,5),(-5,0)],"leather1","ink")
        hand_poly([(-3,-2),(1,-3),(2,0),(1,5),(-2,6),(-3,4)],"leather2")
        hand_poly([(-3,-2),(-1,-2),(0,2),(-2,3)],"leather3")
        hand_poly([(2,-1),(5,0),(4,4),(2,4),(1,2)],"leather2","leather0")
        for x in [-2,0]:
            c.line([(gx+x,gy+4),(gx+x,gy+6)],"leather0")
    else:
        hand_poly([(-4,-4),(2,-4),(5,-1),(5,4),(2,6),(-3,5),(-5,2)],"leather1","ink")
        hand_poly([(-3,-2),(1,-3),(3,-1),(3,3),(0,4),(-3,2)],"leather2")
        c.line([(gx-2,gy-2),(gx+1,gy-2)],"leather3")
        # Knuckles curl across the grip and the thumb crosses back toward the wrist.
        c.line([(gx+1,gy),(gx+4,gy+1)],"leather0")
        c.line([(gx+1,gy+2),(gx+4,gy+3)],"leather0")
        hand_poly([(-4,-1),(-2,-2),(1,0),(0,2),(-2,2)],"leather3","leather1")
    return c,(gx,gy)


def make_sword(hand, sway_angle):
    c=Cel()
    angle=math.radians(18+sway_angle)
    d=(math.sin(angle),math.cos(angle)); n=(d[1],-d[0])
    def pos(across,along):
        return hand[0]+n[0]*across+d[0]*along, hand[1]+n[1]*across+d[1]*along
    def poly(points,color,outline=None):
        c.poly([pos(x,y) for x,y in points],color,outline)
    poly([(-2,-9),(2,-9),(2,9),(-2,9)],"cloth0","ink")
    for y in [-6,-3,0,3,6]:
        c.line([pos(-1,y),pos(1,y+1)],"cloth2")
    poly([(-3,-10),(0,-12),(3,-10),(3,-8),(0,-6),(-3,-8)],"gold1","ink")
    poly([(-2,-10),(0,-11),(2,-10),(0,-8)],"gold3")
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
    if frame is None:
        phase=None
        bob,sway,roll,head_bob,head_sway=0,0,0,0,0
        arm_l=arm_r=0
    else:
        phase=frame/WALK_FRAMES
        bob=.8*math.cos(math.tau*2*(phase-.10))
        sway=-.8*math.sin(math.tau*(phase+.12))
        roll=-math.cos(math.tau*phase)
        head_bob=.65*math.cos(math.tau*2*(phase-.13))
        head_sway=-.6*math.sin(math.tau*(phase+.09))
        arm_l=-3.6*math.cos(math.tau*(phase-.05))
        arm_r=2.3*math.cos(math.tau*(phase-.08))
    def body(x,y):
        return x+sway+(y-92)*roll*.012, y+bob+(x-62)*roll*.017
    shadow=Cel(); shadow.ellipse(62,138,27,5,"#24202B28")
    for cx,p in [(50,phase),(75,None if phase is None else phase+.5)]:
        forward,lift,planted=foot_state(p)
        shadow.ellipse(cx,138+forward*.52+3,8,2.5,"#211E2948" if planted else "#211E2920")
    layers={"00 Contact shadow":shadow,
            "01 Left leg and boot":make_leg(50,phase,sway,bob),
            "02 Right leg and boot":make_leg(75,None if phase is None else phase+.5,sway,bob)}
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
    layers["04 Sword - replaceable"]=make_sword(hand, 0 if frame is None else 1.5*math.cos(math.tau*(phase-.10)))
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
    c=Cel(lambda x,y:body(36+(x-36)*.92,72+(y-72)*.86+1))
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
    c.poly([(41,69),(44,65),(48,67),(57,74),(65,77),(76,72),
            (81,66),(84,65),(87,70),(85,78),(79,84),(66,89),
            (57,88),(48,84),(43,77)],"cream1","leather0")
    c.poly([(43,69),(46,68),(53,74),(63,80),(70,79),(80,74),
            (83,68),(85,70),(83,76),(76,81),(66,85),(56,84),(48,80)],"cream2")
    c.poly([(43,69),(46,69),(48,73),(54,77),(54,80),(48,77),(45,74)],"cream3")
    c.poly([(54,82),(64,84),(73,82),(77,81),(72,85),(64,87),(58,86)],"cream0")
    c.line([(54,82),(62,84),(67,83)],"cream1")
    flutter=0 if phase is None else .85*math.sin(math.tau*(phase-.17))
    def tail(x,y):
        return x+flutter*max(0,(y-81)/24),y
    def tailpoly(points,color,outline=None):
        c.poly([tail(x,y) for x,y in points],color,outline)
    tailpoly([(76,80),(82,79),(83,86),(81,95),(82,103),(78,105),(73,103),(73,96),(75,89)],"cream1","leather0")
    tailpoly([(78,82),(80,82),(80,89),(78,95),(79,102),(75,101),(75,95),(77,90)],"cream2")
    c.line([tail(78,85),tail(77,93),tail(78,98)],"cream3")
    c.line([tail(74,102),tail(77,103),tail(80,102)],"cream0")
    layers["09 Cream scarf"]=c
    # Head: top plane dominates; the face is foreshortened below the fringe.
    head=Cel(lambda x,y:(x+head_sway,y+head_bob))
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
    c=Cel(lambda x,y:(x+head_sway,y+head_bob))
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
    count=WALK_FRAMES if walk else 1
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
        call("set_frame_duration",sprite_path=sprite,frame_number=1,duration_ms=FRAME_MS if walk else 500)
        for _ in range(count-1):
            call("add_frame",sprite_path=sprite,duration_ms=FRAME_MS)
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
