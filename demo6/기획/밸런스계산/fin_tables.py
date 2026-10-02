# -*- coding: utf-8 -*-
"""최종 기획 검산 (임시): 최종 강화표(안5 + 연속 하락 2번 뒤 확정)의 단계별 평균 비용, 환급표, 계승 비용표.
실행: python fin_tables.py [반복=200000]"""
import random, statistics, sys, io, math
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
N = int(sys.argv[1]) if len(sys.argv) > 1 else 200000
T = {1:(100,0,0,2),2:(95,0,1,2),3:(90,0,1,3),4:(85,0,1,3),5:(80,0,1,4),6:(70,10,2,5),7:(60,10,2,6),
     8:(55,10,3,10),9:(50,10,3,12),10:(45,10,3,14),11:(40,10,3,18),12:(35,10,3,22),
     13:(40,15,2,46),14:(35,15,2,59),15:(30,15,2,72)}
SAFE, BONUS, STREAK = 7, 20, 2
def q(xs, p):
    xs = sorted(xs); return xs[min(len(xs)-1, int(math.ceil(p*len(xs)))-1)]
rng = random.Random(2026)
first_cost = {t: [] for t in range(1, 16)}   # 처음 오를 때(그 단계 최고 기록 갱신까지) 비용
cum = {t: [] for t in range(1, 16)}; cumT = {t: [] for t in range(1, 16)}; cumD = {t: [] for t in range(1, 16)}
for _ in range(N):
    lv = 0; best = 0; gauge = {}; cost = 0; tries = 0; drops = 0; streak = 0; seg = 0
    while lv < 15:
        t = lv + 1; base, inc, ceil_, st = T[t]
        f = gauge.get(t, 0)
        p = 100 if ((ceil_ and f >= ceil_) or streak >= STREAK) else min(100, base + inc * f + (BONUS if t <= best else 0))
        cost += st; seg += st; tries += 1
        if rng.random() * 100 < p:
            lv = t; gauge[t] = 0; streak = 0
            if t > best:
                best = t; first_cost[t].append(seg); seg = 0
                cum[t].append(cost); cumT[t].append(tries); cumD[t].append(drops)
        else:
            gauge[t] = f + 1
            if lv > SAFE: lv -= 1; drops += 1; streak += 1
print("| 목표 | 처음 오를 때 평균 강화석 | 0부터 누적 평균 | 누적 운 나쁜 10% | 누적 1% | 누적 평균 시도 | 누적 평균 하락 | 환급(20% 반올림) | 계승 비용(20% 올림) |")
print("|---|---|---|---|---|---|---|---|---|")
for t in range(1, 16):
    m = statistics.mean(cum[t])
    print(f"| +{t} | {statistics.mean(first_cost[t]):.1f} | {m:.1f} | {q(cum[t], .9)} | {q(cum[t], .99)} | {statistics.mean(cumT[t]):.2f} | {statistics.mean(cumD[t]):.2f} | {int(round(0.2*m))} | {math.ceil(0.2*m) if t < 15 else '-'} |")
# 되찾기 비용: 한 칸 잃은 상태(+t-1, best=t)에서 +t 까지
print("\n한 칸 되찾기 평균 강화석 (best = 목표, 게이지 0, 연속 하락 0):")
for t in (10, 12, 13, 14, 15):
    xs = []
    for _ in range(40000):
        lv = t - 1; best = t; gauge = {}; cost = 0; streak = 0
        while lv < t:
            tt = lv + 1; base, inc, ceil_, st = T[tt]; f = gauge.get(tt, 0)
            p = 100 if ((ceil_ and f >= ceil_) or streak >= STREAK) else min(100, base + inc * f + (BONUS if tt <= best else 0))
            cost += st
            if rng.random() * 100 < p: lv = tt; gauge[tt] = 0; streak = 0
            else:
                gauge[tt] = f + 1
                if lv > SAFE: lv -= 1; streak += 1
        xs.append(cost)
    xs2 = []
    for _ in range(40000):
        lv = t - 1; best = t - 1; gauge = {}; cost = 0; streak = 0
        while lv < t:
            tt = lv + 1; base, inc, ceil_, st = T[tt]; f = gauge.get(tt, 0)
            p = 100 if ((ceil_ and f >= ceil_) or streak >= STREAK) else min(100, base + inc * f + (BONUS if tt <= best else 0))
            cost += st
            if rng.random() * 100 < p: lv = tt; gauge[tt] = 0; streak = 0; best = max(best, tt)
            else:
                gauge[tt] = f + 1
                if lv > SAFE: lv -= 1; streak += 1
        xs2.append(cost)
    print(f"+{t}: 되찾기 적용 {statistics.mean(xs):.0f}석 / 처음 오르기 {statistics.mean(xs2):.0f}석")
# 단일 단계 기대 시도(하락 무시, 되찾기 없음) — 테스트 기대값 계산법 예시
def single(t):
    base, inc, ceil_, st = T[t]; qq = 1.0; e = 0.0; f = 0
    while True:
        e += qq; p = 100 if (ceil_ and f >= ceil_) else min(100, base + inc * f)
        if p >= 100: break
        qq *= (1 - p / 100); f += 1
    return e
print("\n단일 단계 기대 시도(하락·되찾기 무시):", {t: round(single(t), 4) for t in (8, 10, 13, 14, 15)})
