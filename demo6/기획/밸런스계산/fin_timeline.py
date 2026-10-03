# -*- coding: utf-8 -*-
"""2차 기획 검산 (2차에서 새로 만든 스크립트. 1차에는 없음): 마을 이야기 시점표에 쓸 '마을 도착 시각'과 장면 배치 점검.
fin_progress.py 를 그대로 불러 같은 난수로 돌리고, 원정별 기록에서 마을 도착 시각을 계산한다. 환경 변수는 fin_progress.py 와 같음.
2차: 장면 배치 규칙(한 방문에 새 장면은 강제 1개 + 선택 1개까지, 나머지는 다음 방문으로)을 넣어
     방문별 새 장면 수, 처음 40분 중 마을 시간, 장면이 있는 방문 사이 가장 긴 공백을 센다.
     선택 장면은 플레이어가 '!'를 보면 늘 본다고 가정했다(장면이 가장 촘촘한 경우).
v3: 코드는 그대로다. 불러오는 fin_progress.py 기본값이 v3(골드: 상점·마을 편의, 2차 개정 확정)로 바뀌어 결과가 조금 달라진다.
    GOLD=0 이면 2차 강화석 전용 상점 비교로, 개정 전 2차와 같은 출력(fin_timeline_stone_c/g.txt).
7칸: FIN_PROG=모듈 이름(기본 fin_progress)으로 불러올 진행 시뮬레이션을 고른다(FIN_PROG=fin_progress7 = 장비 7칸판, 드랍 ×1.2 는 DROP_MULT=1.2).
    FIN_PROG 를 비우면 예전과 같은 출력이다. '첫 +8 도전'은 모든 부위 기준(7칸 무기·갑옷 기준은 fin_progress7 출력의 [7칸] 줄).
실행: python fin_timeline.py [인원=2000] [정책 c|g]"""
import sys, os, statistics, random, importlib
N = sys.argv[1] if len(sys.argv) > 1 else "2000"; POL = sys.argv[2] if len(sys.argv) > 2 else "c"
PROG = os.environ.get("FIN_PROG", "fin_progress")   # 7칸: 불러올 진행 시뮬레이션
sys.argv = [PROG + ".py", N, "1.27", POL, "1.00", "0.95", "1.24"]
F = importlib.import_module(PROG)
rng = random.Random(20261002)
res = [F.play(rng) for _ in range(int(N))]
med = statistics.median


def arrivals(per_run):
    """원정마다 (원정 번호, 마을 도착 분, 그 원정의 끝 층, 그때까지 가장 깊은 층, 사망 여부)."""
    out = []; t0 = 0.0; deep = 0
    for x in per_run:
        arr = t0 + x["mins"] - x["tmin"]; deep = max(deep, x["end"])
        out.append((x["run"], arr, x["end"], deep, x["died"])); t0 += x["mins"]
    return out


rows = []
def ev(name, pick):
    xs = []
    for r in res:
        a = arrivals(r[4]); hit = pick(r, a)
        if hit is not None: xs.append(hit)
    if not xs: rows.append(f"| {name} | — | — | 0% |"); return
    rows.append(f"| {name} | {med(h[0] for h in xs):.0f} | {med(h[1] for h in xs):.0f}분 (빠른10% {F.pct([h[1] for h in xs], .1):.0f}, 늦은10% {F.pct([h[1] for h in xs], .9):.0f}) | {len(xs) / len(res) * 100:.0f}% |")


def first(a, cond):
    for run, arr, end, deep, died in a:
        if cond(run, arr, end, deep, died): return (run, arr)
    return None


ev("첫 귀환 (원정 1 끝, 사망 포함)", lambda r, a: first(a, lambda *_: True))
ev("살아서 한 첫 귀환", lambda r, a: first(a, lambda run, arr, end, deep, died: not died))
for fl in (4, 6, 7, 8, 9):
    ev(f"{fl}층 첫 도달 뒤 마을 도착", lambda r, a, fl=fl: first(a, lambda run, arr, end, deep, died: deep >= fl))
def run_arr(a, k):
    return next(((run, arr) for run, arr, *_ in a if run == k), None)


ev("5층 보스 첫 처치 (던전 안)", lambda r, a: r[0]["first5"])
ev("5층 보스 첫 처치 뒤 마을 도착", lambda r, a: run_arr(a, r[0]["first5"][0]) if r[0]["first5"] else None)
ev("10층 첫 진입 (던전 안)", lambda r, a: (r[1][10]["run"], r[1][10]["time"]) if 10 in r[1] else None)
ev("10층 보스 첫 처치 뒤 마을 도착", lambda r, a: run_arr(a, r[0]["first10"][0]) if r[0]["first10"] else None)
ev("두 번째 귀환 (훈련장 열림)", lambda r, a: (a[1][0], a[1][1]) if len(a) > 1 else None)
def by_field(r, a, key, n):
    for x, (run, arr, *_rest) in zip(r[4], a):
        if x[key] >= n: return (run, arr)
    return None
ev("명패 3개를 맡긴 귀환 (등불지기 1단계)", lambda r, a: by_field(r, a, "tags", 3))
ev("명패 5개를 맡긴 귀환", lambda r, a: by_field(r, a, "tags", 5))
ev("10층 보스에게 처음 진 뒤 마을 도착", lambda r, a: by_field(r, a, "b10loss", 1))
ev("10층 보스에게 두 번째 진 뒤 마을 도착", lambda r, a: by_field(r, a, "b10loss", 2))
ev("첫 +8 도전 (마을 출발 시각 기준, 1차와 같은 기준)", lambda r, a: (0, r[0]["risk"]) if r[0]["risk"] else None)
ev("첫 마을 계승 (도착 시각)", lambda r, a: r[0]["firstinh"])
ev("첫 전설", lambda r, a: r[0]["firstleg"])
# ---------------------------------------------------------------- 2차: 장면 배치 점검
# (id, 강제 여부, 우선(0 = 그 방문의 사건에 붙은 장면), 조건)
def qd(r, qid, run):
    q = r[0]["stat"]["qtime"].get(qid); return q is not None and q[0] <= run
SCENES = [
    ("smith1", True, 0, lambda st, sn: st["v"] >= 1),
    ("kid1", False, 1, lambda st, sn: st["v"] >= 1),
    ("trainer1", False, 0, lambda st, sn: st["v"] >= 2),
    ("gate2", False, 1, lambda st, sn: st["deep"] >= 4),
    ("lamp1", False, 1, lambda st, sn: st["tags"] >= 3),
    ("trainer2", False, 1, lambda st, sn: st["q"]("q.trainer_windblade") and "trainer1" in sn),
    ("winch", True, 0, lambda st, sn: st["boss5"]),
    ("keeper1", False, 0, lambda st, sn: st["boss5"]),
    ("kid2", False, 1, lambda st, sn: st["q"]("q.kid_chalk") and "kid1" in sn),
    ("lamp2", False, 1, lambda st, sn: st["tags"] >= 5 and "keeper1" in sn and "lamp1" in sn),
    ("merchant1", False, 0, lambda st, sn: st["deep"] >= 7),
    ("kid_arrow7", False, 1, lambda st, sn: st["tags"] >= 7 and "kid2" in sn),
    ("keeper2", False, 1, lambda st, sn: st["tags"] >= 7 and "keeper1" in sn),
    ("gate3", False, 1, lambda st, sn: st["deep"] >= 8),
    ("merchant2", False, 1, lambda st, sn: st["deep"] >= 8 and "merchant1" in sn),
    ("kid_arrow8", False, 1, lambda st, sn: st["tags"] >= 8 and "gate3" in sn and "kid_arrow7" in sn),
    ("trainer3", False, 1, lambda st, sn: st["q"]("q.trainer_elites") and "trainer2" in sn),
    ("merchant3", False, 1, lambda st, sn: st["q"]("q.merchant_survey") and "merchant2" in sn),
    ("kid_arrow9", False, 1, lambda st, sn: st["tags"] >= 9 and "kid_arrow8" in sn),
    ("trainer_ogre", False, 0, lambda st, sn: st["b10loss"] >= 1),
    ("smith_hammer", False, 0, lambda st, sn: st["b10loss"] >= 2),
    ("finale", True, 0, lambda st, sn: st["fin"]),
]
first3 = [[], [], []]; elig1 = []; gaps = []; last_before = []; town40 = []; n40 = []; seen_pre = []; per_visit = []
for r in res:
    a = arrivals(r[4]); f5 = r[0]["first5"][0] if r[0]["first5"] else 999; f10 = r[0]["first10"][0] if r[0]["first10"] else None
    seen = set(); queue = []; scene_t = []; t40 = 0.0; c40 = 0
    for i, (x, (run, arr, end, deep, died)) in enumerate(zip(r[4], a)):
        st = dict(v=i + 1, deep=x["maxf"], tags=x["tags"], b10loss=x["b10loss"], boss5=run >= f5, fin=(f10 == run),
                  q=lambda qid, run=run: qd(r, qid, run))
        for sid, forced, pri, cond in SCENES:
            if sid in seen or any(q[0] == sid for q in queue): continue
            if cond(st, seen): queue.append((sid, forced, pri, i))
        if i == 0: elig1.append(len(queue))
        play = [q for q in queue if q[1]]
        opt = sorted([q for q in queue if not q[1]], key=lambda q: (q[2], q[3]))
        if opt: play.append(opt[0])
        for q in play: seen.add(q[0]); queue.remove(q)
        if i < 3: first3[i].append(len(play))
        if play and (f10 is None or run <= f10): scene_t.append(arr)
        t40 += max(0.0, min(40.0, arr + x["tmin"]) - max(0.0, arr)) if arr < 40 else 0.0
        if arr < 40: c40 += len(play)
        if f10 == run: break
    town40.append(t40); n40.append(c40); seen_pre.append(len(seen))
    if len(scene_t) >= 2:
        g = [b - a_ for a_, b in zip(scene_t, scene_t[1:])]; gaps.append(max(g))
        last_before.append(scene_t[-2])
srt = sorted(gaps)
print(f"# 마을 도착 시각 (인원 {N}, 정책 {'신중' if POL == 'c' else '밀어붙임'}, {F.TOWN} 체류, 의뢰 {F.QUEST})")
print("| 시점 | 원정 중앙 | 마을 도착 시각 중앙 | 겪은 비율 |")
print("|---|---|---|---|")
print("\n".join(rows))
print(f"\n# 장면 배치 점검 (한 방문에 강제 1 + 선택 1, 선택 장면은 늘 본다고 가정, 장면 {len(SCENES)}개)")
print(f"첫 귀환에 볼 수 있게 된 장면 평균 {statistics.mean(elig1):.1f}개 → 실제로 본 장면 방문 1/2/3: {statistics.mean(first3[0]):.1f} / {statistics.mean(first3[1]):.1f} / {statistics.mean(first3[2]):.1f}개")
print(f"처음 40분 중 마을에 있던 시간 평균 {statistics.mean(town40):.1f}분 ({statistics.mean(town40) / 40 * 100:.0f}%), 그동안 본 장면 평균 {statistics.mean(n40):.1f}개")
print(f"10층 첫 처치까지 장면이 있는 방문 사이 가장 긴 공백: 중앙 {med(gaps):.0f}분, 늦은 10% {F.pct(gaps, .9):.0f}분, 25분 이하 {sum(1 for g in gaps if g <= 25) / len(gaps) * 100:.0f}%")
print(f"마무리 직전 마지막 장면 방문 시각 중앙 {med(last_before):.0f}분, 마무리까지 본 장면 평균 {statistics.mean(seen_pre):.1f}개")
qa = {}
for r in res:
    f10 = r[0]["first10"][0] if r[0]["first10"] else 999
    for x in r[4]:
        if x["run"] <= f10: qa.setdefault(min(x["run"], 13), []).append(x["qact"])
print("원정 시작 때 진행 중인 의뢰 수(명패 제외, 10층 첫 처치 원정까지) 평균: " + ", ".join(f"원정 {k}{'+' if k == 13 else ''} {statistics.mean(v):.1f}" for k, v in sorted(qa.items())))
allv = [v for vs in qa.values() for v in vs]
print(f"전체 평균 {statistics.mean(allv):.1f}개, 0개인 원정 {sum(1 for v in allv if v == 0) / len(allv) * 100:.0f}%, 4개 이상 {sum(1 for v in allv if v >= 4) / len(allv) * 100:.0f}%")
