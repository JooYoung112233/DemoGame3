# -*- coding: utf-8 -*-
"""2차 기획 검산: 실제 원정 수입(강화석) — 시작 층·끝 방식별 중앙값, 10층 첫 처치 전후 분당 수입, 9-5 필요 원정 수.
1차 fin_income.py 복사본. v2: '정비 화면 분해' → '마을 분해', '의뢰' 열 추가, 시간은 마을 체류 포함. 환경 변수는 fin_progress.py 와 같음.
v3: 골드 수입표(원정 종류별 골드, 의뢰 골드, 그 방문의 골드 지출)를 더했다(기본값 = 골드, 2차 개정 확정).
    GOLD=0 이면 2차 강화석 전용 상점 비교로, 개정 전 2차와 같은 출력(fin_income_stone_c/g.txt).
실행: python fin_income.py [인원=1500] [정책 c|g]"""
import sys, statistics, random
N = sys.argv[1] if len(sys.argv) > 1 else "1500"; POL = sys.argv[2] if len(sys.argv) > 2 else "c"
sys.argv = ["fin_progress.py", N, "1.27", POL, "1.00", "0.95", "1.24"]
import fin_progress as F
rng = random.Random(20261002)
res = [F.play(rng) for _ in range(int(N))]
runs = [x for r in res for x in r[4]]
med = statistics.median
rows = {}
print("| 원정 종류 | 횟수 | 수입 | 직접 / 던전 안 분해 / 마을 분해 / 의뢰(평균) | 시간 (마을 포함) | 분당 |")
print("|---|---|---|---|---|---|")
def show(key, name, xs, qk="quest"):   # v2: 의뢰 열, 마을 체류 포함 시간. qk="quest_pre" 면 10층 첫 처치 뒤에 받은 의뢰는 뺌
    if not xs: print(f"| {name} | 0 | — | — | — | — |"); return
    tot = [x["direct"] + x["dis"] + x["town"] + x[qk] for x in xs]; mins = [x["mins"] for x in xs]
    rows[key] = med(tot)
    print(f"| {name} | {len(xs)} | {med(tot):.0f}석 | {med(x['direct'] for x in xs):.0f} / {med(x['dis'] for x in xs):.0f} / {med(x['town'] for x in xs):.0f} / {statistics.mean(x[qk] for x in xs):.1f} | "
          f"{med(mins):.1f}분 | {sum(tot) / sum(mins):.1f}석 |")
show("a", "1~4층 시작, 5층 전 귀환", [x for x in runs if x["start"] <= 4 and x["end"] < 5 and not x["died"]])
show("b", "1층 시작, 5층 보스까지", [x for x in runs if x["start"] == 1 and x["end"] >= 5 and not x["died"]])
show("c", "6~9층 시작, 10층 전 귀환", [x for x in runs if 6 <= x["start"] <= 9 and x["end"] < 10 and not x["died"]])
show("d", "6층 시작, 10층 완주", [x for x in runs if x["start"] == 6 and x["end"] == 10 and not x["died"]])
show("e", "사망한 원정", [x for x in runs if x["died"]])
show("f", "10층 첫 처치 전 전체", [x for x in runs if x["pre10"]], "quest_pre")
show("g", "10층 첫 처치 뒤 전체", [x for x in runs if not x["pre10"]])
pre = [x for x in runs if x["pre10"]]
if pre:
    q = sum(x["quest_pre"] for x in pre); tot = sum(x["direct"] + x["dis"] + x["town"] + x["quest_pre"] for x in pre)
    print(f"\n10층 첫 처치 전 수입 구성 (합계 기준): 직접 {sum(x['direct'] for x in pre) / tot * 100:.0f}%, 던전 안 분해 {sum(x['dis'] for x in pre) / tot * 100:.0f}%, "
          f"마을 분해 {sum(x['town'] for x in pre) / tot * 100:.0f}%, 의뢰 {q / tot * 100:.1f}%, "
          + ("상점 지출 0.0% (v3: 상점은 골드만 받음)" if F.GOLD else f"상점 지출 {sum(x['shop'] for x in pre) / tot * 100:.1f}%"))   # v3
if F.GOLD:   # v3: 골드 수입표. 사냥 = 몬스터·정예·상자·보스 골드(사망 손실 뒤), 의뢰 = 그 방문에 받은 의뢰 골드
    grows = {}
    print("\n| 원정 종류 (골드) | 횟수 | 골드 수입 | 사냥·상자·정예·보스 / 의뢰(평균) | 그 방문 골드 지출(평균) | 분당 골드 |")
    print("|---|---|---|---|---|---|")
    def gshow(key, name, xs, qk="g_q"):
        if not xs: print(f"| {name} | 0 | — | — | — | — |"); return
        tot = [x["gold"] + x[qk] for x in xs]; mins = [x["mins"] for x in xs]; grows[key] = med(tot)
        print(f"| {name} | {len(xs)} | {med(tot):.0f} | {med(x['gold'] for x in xs):.0f} / {statistics.mean(x[qk] for x in xs):.0f} | {statistics.mean(x['g_sp'] for x in xs):.0f} | {sum(tot) / sum(mins):.0f} |")
    gshow("a", "1~4층 시작, 5층 전 귀환", [x for x in runs if x["start"] <= 4 and x["end"] < 5 and not x["died"]])
    gshow("b", "1층 시작, 5층 보스까지", [x for x in runs if x["start"] == 1 and x["end"] >= 5 and not x["died"]])
    gshow("c", "6~9층 시작, 10층 전 귀환", [x for x in runs if 6 <= x["start"] <= 9 and x["end"] < 10 and not x["died"]])
    gshow("d", "6층 시작, 10층 완주", [x for x in runs if x["start"] == 6 and x["end"] == 10 and not x["died"]])
    gshow("e", "사망한 원정", [x for x in runs if x["died"]])
    gshow("f", "10층 첫 처치 전 전체", [x for x in runs if x["pre10"]], "g_q_pre")
    gshow("g", "10층 첫 처치 뒤 전체", [x for x in runs if not x["pre10"]])
    gin = sum(x["gold"] + x["g_q_pre"] for x in pre); gsp = sum(x["g_sp"] for x in runs if x["pre10_visit"])
    print(f"\n10층 첫 처치 전 골드 (합계 기준): 사냥·상자·정예·보스 {sum(x['gold'] for x in pre) / gin * 100:.0f}%, 의뢰 {sum(x['g_q_pre'] for x in pre) / gin * 100:.0f}%. "
          f"10층 첫 처치 전 방문의 지출은 그때까지 수입의 {gsp / gin * 100:.0f}%")
# v2: 9-5 표 (장비 1개 기준, 필요한 원정 수). 구간 비용은 fin_tables_out.txt 의 누적 평균을 반올림한 값
SEG = [("+0 → +5", 16), ("+5 → +7", 16), ("+7 → +10", 92), ("+10 → +12", 154), ("+12 → +13", 131), ("+13 → +14", 197), ("+14 → +15", 286)]
if all(k in rows for k in ("a", "c", "d")):
    a, c, d = round(rows["a"]), round(rows["c"]), round(rows["d"])
    print(f"\n| 구간 | 평균 강화석 | 1~4층 중간 귀환 ({a}석) | 6층 시작 중간 귀환 ({c}석) | 6~10층 완주 ({d}석) |")
    print("|---|---|---|---|---|")
    for name, cost in SEG:
        print(f"| {name} | {cost} | {cost / a:.2f} | {cost / c:.2f} | {cost / d:.2f} |")
