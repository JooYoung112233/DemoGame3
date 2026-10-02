# -*- coding: utf-8 -*-
"""최종 기획 검산 (임시): 강화표 안5(+13~15 수정)에 '연속 하락 2번 뒤 확정'을 더할지 비교.
실행: python fin_enhance.py [반복=40000]"""
import random, statistics, sys, io, math
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
N = int(sys.argv[1]) if len(sys.argv) > 1 else 40000
BASE = {1:(100,0,0,2),2:(95,0,1,2),3:(90,0,1,3),4:(85,0,1,3),5:(80,0,1,4),6:(70,10,2,5),7:(60,10,2,6),
        8:(55,10,3,10),9:(50,10,3,12),10:(45,10,3,14),11:(40,10,3,18),12:(35,10,3,22),
        13:(30,15,3,30),14:(25,15,3,36),15:(20,15,3,42)}
FIX5 = dict(BASE); FIX5.update({13:(40,15,2,46),14:(35,15,2,59),15:(30,15,2,72)})
SAFE = 7; BONUS = 20
def climb(rng, T, start, goal, streak_ceiling):
    lv = start; best = start; gauge = {}; cost = 0; tries = 0; drops = 0; streak = 0; max_streak = 0
    while lv < goal:
        t = lv + 1; base, inc, ceil_, st = T[t]
        f = gauge.get(t, 0)
        if (ceil_ and f >= ceil_) or (streak_ceiling and streak >= streak_ceiling):
            p = 100
        else:
            p = min(100, base + inc * f + (BONUS if t <= best else 0))
        cost += st; tries += 1
        if rng.random() * 100 < p:
            lv = t; gauge[t] = 0; best = max(best, t); streak = 0
        else:
            gauge[t] = f + 1
            if lv > SAFE:
                lv -= 1; drops += 1; streak += 1; max_streak = max(max_streak, streak)
    return cost, tries, drops, max_streak
def q(xs, p):
    xs = sorted(xs); return xs[min(len(xs)-1, int(math.ceil(p*len(xs)))-1)]
print("| 안 | 0→+15 강화석 평균/운 나쁜 10%/1% | 0→+15 시도 평균 | 0→+15 하락 평균 | +12→+15 강화석 평균/운 나쁜 10% | +12→+15 3연속 하락 겪는 비율 | 4연속 |")
print("|---|---|---|---|---|---|---|")
for name, T, sc in (("초안", BASE, 0), ("초안 + 연속 하락 2번 뒤 확정", BASE, 2), ("안5", FIX5, 0), ("안5 + 연속 하락 2번 뒤 확정", FIX5, 2), ("안5 + 연속 하락 3번 뒤 확정", FIX5, 3)):
    rng = random.Random(7)
    a = [climb(rng, T, 0, 15, sc) for _ in range(N)]
    b = [climb(rng, T, 12, 15, sc) for _ in range(N)]
    c = [x[0] for x in a]; bc = [x[0] for x in b]
    print(f"| {name} | {statistics.mean(c):.0f} / {q(c,.9)} / {q(c,.99)} | {statistics.mean(x[1] for x in a):.1f} | {statistics.mean(x[2] for x in a):.1f} | "
          f"{statistics.mean(bc):.0f} / {q(bc,.9)} | {sum(1 for x in b if x[3]>=3)/N*100:.0f}% | {sum(1 for x in b if x[3]>=4)/N*100:.0f}% |")
