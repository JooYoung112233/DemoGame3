# -*- coding: utf-8 -*-
"""2차 기획 검산: 성장 곡선 조합별 진행 시간 + 층 첫 진입 기준 장비로 본 처치 타수·한 번 맞을 때 잃는 체력.
모든 수치는 10배 정수 기준(공격력·체력·방어·몬스터). 방어 계산 상수 1000.
실행: python fin_floor.py [인원=800] [체력성장] [공격성장] [정책 c|g] [굴쥐 성장=1.24]
v2: 1차 복사본. 코드는 그대로이고, 불러오는 fin_progress.py 가 v2 규칙(마을에서만 계승, 마을 체류 일정, 의뢰, 상점)으로 바뀌었다.
    환경 변수(V1, TOWN, QUEST 등)는 fin_progress.py 와 같다. 기획 값: python fin_floor.py 2000 1.27 1.24 c 1.25
v3: 코드는 그대로다. 불러오는 fin_progress.py 기본값이 v3(골드, 2차 개정 확정)로 바뀌어 결과가 조금 달라진다.
    GOLD=0 이면 2차 강화석 전용 상점 비교로, 개정 전 2차와 같은 출력(fin_floor_127_124_stone_c/g.txt).
7칸: FIN_PROG=모듈 이름(기본 fin_progress)으로 불러올 진행 시뮬레이션을 고른다. FIN_PROG=fin_progress7 이면 장비 7칸판이고,
    끝에 3차 보스 체력(32,000 기준) 처치 시간 줄을 더 찍는다. FIN_PROG 를 비우면 예전과 같은 출력이다.
    7칸 기획 값: FIN_PROG=fin_progress7 python fin_floor.py 2000 1.27 1.24 c 1.25 (드랍 ×1.2 는 DROP_MULT=1.2)"""
import sys, io, os, statistics, random, math, importlib
N = sys.argv[1] if len(sys.argv) > 1 else "800"
HPG = sys.argv[2] if len(sys.argv) > 2 else "1.30"
ATG = sys.argv[3] if len(sys.argv) > 3 else "1.20"
POL = sys.argv[4] if len(sys.argv) > 4 else "c"
RATG = float(sys.argv[5]) if len(sys.argv) > 5 else 1.24
PROG = os.environ.get("FIN_PROG", "fin_progress")   # 7칸: 불러올 진행 시뮬레이션
sys.argv = [PROG + ".py", N, HPG, POL, "1.00", "0.95", ATG]
F = importlib.import_module(PROG)
out = F.sys.stdout
med = statistics.median
rng = random.Random(20261002)
res = [F.play(rng) for _ in range(int(N))]
def summary(key):
    xs = [r[0][key][1] for r in res if r[0][key] is not None]
    return med(xs) if xs else float("nan"), (sum(1 for m in xs if 105 <= m <= 165) / len(xs) * 100 if xs else 0), len(xs) / len(res) * 100
t10, inr, reach = summary("first10"); t5, _, _ = summary("first5")
b10 = [r[0]["boss10_first"] for r in res if r[0]["boss10_first"] is not None]
dpre = statistics.mean(r[0]["deaths_pre10"] for r in res)
print(f"# 체력 {HPG} / 공격 {ATG} / 정책 {POL} / 굴쥐 성장 {RATG}: 5층 보스 {t5:.0f}분, 10층 첫 처치 {t10:.0f}분 (도달 {reach:.0f}%, 1h45~2h45 안 {inr:.0f}%), 10층 첫 도전 승률 {sum(b10)/len(b10)*100:.0f}%, 10층 전 사망 {dpre:.1f}회")
hpm = lambda f: float(HPG) ** (f - 1); atm = lambda f: float(ATG) ** (f - 1)
print("| 층 | 기준 공격력 | 기준 체력 | 기준 방어 | 굴쥐 체력/물기 | 멧돼지 체력/돌진 | 궁수 체력/화살 | 타수 쥐/멧/궁 (장검) | 쥐 1타 확률 중앙/하위10% | 한 번 맞을 때 잃는 체력 쥐/돌진/화살 |")
print("|---|---|---|---|---|---|---|---|---|---|")
for f in range(1, 11):
    es = [r[1][f] for r in res if f in r[1]]
    atk = med(e["atk"] for e in es) * 10; lo = sorted(e["atk"] for e in es)[int(len(es) * .1)] * 10
    hp = med(e["hp"] for e in es) * 10; df = med(e["def_"] for e in es) * 10
    rat_hp = round(160 * RATG ** (f - 1)); rat_atk = round(60 * atm(f))
    boar_hp = round(900 * hpm(f)); boar_atk = round(300 * atm(f))
    arc_hp = round(400 * hpm(f)); arc_atk = round(100 * atm(f))
    hits = lambda h: math.ceil(h / atk)
    loss = lambda a: a * 1000 / (1000 + df) / hp * 100
    pr = F.p1(atk, 1.0, rat_hp); pl = F.p1(lo, 1.0, rat_hp)
    print(f"| {f} | {atk:.0f} | {hp:.0f} | {df:.0f} | {rat_hp} / {rat_atk} | {boar_hp} / {boar_atk} | {arc_hp} / {arc_atk} | {hits(rat_hp)}/{hits(boar_hp)}/{hits(arc_hp)} | {pr*100:.0f}% / {pl*100:.0f}% | {loss(rat_atk):.1f}% / {loss(boar_atk):.1f}% / {loss(arc_atk):.1f}% |")
es10 = [r[1][10] for r in res if 10 in r[1]]; es5 = [r[1][5] for r in res if 5 in r[1]]
atk10 = med(e["atk"] for e in es10) * 10; atk5 = med(e["atk"] for e in es5) * 10
hp10 = med(e["hp"] for e in es10) * 10; df10 = med(e["def_"] for e in es10) * 10
hp5 = med(e["hp"] for e in es5) * 10; df5 = med(e["def_"] for e in es5) * 10
for base in (16000, 17000, 18000):
    for r5 in (0.55, 0.6):
        b10hp = round(base * hpm(10)); b5hp = round(base * r5 * hpm(5))
        print(f"보스 기본 체력 {base}, 5층 비율 {r5}: 10층 {b10hp} → {b10hp / (atk10 * 1.76):.0f}초, 5층 {b5hp} → {b5hp / (atk5 * 1.76):.0f}초")
for batk in (300, 320, 340):
    a10 = batk * atm(10); a5 = batk * 0.85 * atm(5)
    l = lambda a, m, hp, df: a * m * 1000 / (1000 + df) / hp * 100
    print(f"보스 기본 공격 {batk}: 10층 공격 {a10:.0f} 내려찍기/돌진/낙석 {l(a10,2.5,hp10,df10):.0f}%/{l(a10,1.5,hp10,df10):.0f}%/{l(a10,1.2,hp10,df10):.0f}%, 5층 공격 {a5:.0f} 내려찍기 {l(a5,2.5,hp5,df5):.0f}%")
if hasattr(F, "report7"):   # 7칸: 3차 부록 보스(체력 = 32,000 × 1.27^(층 − 1), 5층 × 0.55, 처치 시간 = 체력 ÷ (공격력 × 1.76))
    b10 = med(e["B"] for e in es10) * 10; b5 = med(e["B"] for e in es5) * 10
    cm10 = med(e["cm"] for e in es10); cm5 = med(e["cm"] for e in es5)
    h10 = round(32000 * hpm(10)); h5 = round(32000 * 0.55 * hpm(5))
    print(f"7칸, 3차 보스 체력 10층 {h10} / 5층 {h5}: 치명 없는 3차 식 {h10 / (atk10 * 1.76):.0f}초 / {h5 / (atk5 * 1.76):.0f}초 "
          f"(10층 공격력 중앙 {atk10:.0f}, 3차 기준 1,462 → 107초). 기대 치명 배율 중앙 10층 {cm10:.3f} / 5층 {cm5:.3f}, "
          f"보스전 지수 B(치명·공속·스킬·재사용·보스 피해 포함) 중앙 10층 {b10:.0f} / 5층 {b5:.0f} → 체력 ÷ (B × 1.76) {h10 / (b10 * 1.76):.0f}초 / {h5 / (b5 * 1.76):.0f}초. "
          f"합격선 비교(2차 대비 고친 107초)는 fin7_variants.txt")
