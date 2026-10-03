# -*- coding: utf-8 -*-
"""2차 기획 검산 (2차에서 새로 만든 스크립트. 1차에는 없음): fin_progress.py 를 여러 변형으로 동시에 돌려 핵심 지표를 한 표로 모은다.
실행: python fin_variants.py 묶음이름 [인원=2000]
  묶음: town(마을 체류·의뢰·상점 변형), curves(성장 곡선 비교), bounty(반복 의뢰 보상), seeds(난수 씨앗), gold(v3 골드 변형), all
  환경 변수를 비워 두면 fin_progress.py 기본값 = 2차 기획서 최종 채택안(체류 4/4/4/4.5·재도전 3, 의뢰 final, 반복 없음, 상점 7층, 골드)
  v3(2차 개정): 기본값에 골드(상점·마을 편의, 사용자 확정)가 켜졌다. 개정 전 2차 강화석 전용 상점은 GOLD=0 비교 행이다. gold 묶음은 골드 지표 열을 따로 보여 준다.
결과: fin_variants_<묶음>.txt
7칸 (장비-능력치-파밍-1차.md 12장 단계 5):
  - FIN_PROG=모듈 이름(기본 fin_progress)으로 돌릴 진행 시뮬레이션을 고른다. 비우면 예전과 같은 결과다.
    FIN_PROG=fin_progress7 로 기존 묶음을 돌리면 결과 파일은 fin7_variants_<묶음>.txt (V1·INH=naive 행은 7칸판이 지원하지 않아 빈 칸).
  - fin7 묶음: 2차(fin_progress.py, 손대지 않고 진단 고리만 걸어 같은 정의의 지표를 잼)와 7칸(fin_progress7.py) 드랍 ×1.0·×1.2를
    같은 씨앗으로 돌려 12장 단계 5 합격선 표와 13장 위험 1 손잡이 후보를 낸다. 결과: fin7_variants.txt
    실행: python fin_variants.py fin7 [인원=2000]"""
import sys, os, io, re, subprocess, json, math, statistics, random, importlib
from concurrent.futures import ThreadPoolExecutor
HERE = os.path.dirname(os.path.abspath(__file__))
GROUP = sys.argv[1] if len(sys.argv) > 1 else "town"
N = sys.argv[2] if len(sys.argv) > 2 else "2000"
PROG = os.environ.get("FIN_PROG", "fin_progress")   # 7칸: 돌릴 진행 시뮬레이션

TOWN = [  # (라벨, 환경 변수, 정책) — 기본값(빈 환경 변수)이 2차 기획서 최종 채택안이다
    ("1차 (던전 안 계승, 정비 3분)", dict(V1="1"), "c"), ("1차 (던전 안 계승, 정비 3분)", dict(V1="1"), "g"),
    ("마을에서만 계승, 체류 3분, 의뢰·상점 없음", dict(QUEST="0", SHOP="0", TOWN="3"), "c"),
    ("마을에서만 계승, 체류 3분, 의뢰·상점 없음", dict(QUEST="0", SHOP="0", TOWN="3"), "g"),
    ("마을에서만 계승, 대장간 추천 없음(단순 방식), 체류 3분, 의뢰·상점 없음", dict(QUEST="0", SHOP="0", TOWN="3", INH="naive"), "c"),
    ("+ 체류 예산 4/4/4/4.5·재도전 3, 의뢰·상점 없음", dict(QUEST="0", SHOP="0"), "c"),
    ("+ 체류 예산 4/4/4/4.5·재도전 3, 의뢰·상점 없음", dict(QUEST="0", SHOP="0"), "g"),
    ("+ 상점만", dict(QUEST="0"), "c"),
    ("**v3 최종 채택: 체류 예산 + 의뢰 보상표 12개(반복 없음) + 상점·편의(골드)**", dict(), "c"),   # v3: 기본값이 골드
    ("**v3 최종 채택: 체류 예산 + 의뢰 보상표 12개(반복 없음) + 상점·편의(골드)**", dict(), "g"),
    ("비교: 2차 강화석 전용 상점 (GOLD=0)", dict(GOLD="0"), "c"),   # v3
    ("비교: 2차 강화석 전용 상점 (GOLD=0)", dict(GOLD="0"), "g"),   # v3
    ("최종에서 대장간 추천만 뺌(단순 방식)", dict(INH="naive"), "c"),
    ("최종, 체류 3분 고정", dict(TOWN="3"), "c"),
    ("최종, 체류 3분 30초 고정", dict(TOWN="3.5"), "c"),
    ("최종, 체류 4분 고정", dict(TOWN="4"), "c"),
    ("최종, 체류 4분 30초 고정", dict(TOWN="4.5"), "c"),
    ("최종, 체류 5분 고정", dict(TOWN="5"), "c"),
    ("최종, 체류 = 시설 설계 일정 6/5/5/4·재도전 3", dict(TOWN="sched"), "c"),
    ("최종, 체류 = 시설 설계 일정 6/5/5/4·재도전 3", dict(TOWN="sched"), "g"),
    ("축소안 (주민 5명, 의뢰 10개 = 상인 의뢰 2개 뺌)", dict(QUEST="small"), "c"),
    ("축소안 (주민 5명, 의뢰 10개 = 상인 의뢰 2개 뺌)", dict(QUEST="small"), "g"),
    ("의뢰 = 검토 반영 전 통합표(반복 2석·6회, 상점 4층)", dict(QUEST="v2"), "c"),
    ("의뢰 = 검토 반영 전 통합표(반복 2석·6회, 상점 4층)", dict(QUEST="v2"), "g"),
    ("검토 반영 전 채택안 전체(통합표 + 체류 6/5/5/4)", dict(QUEST="v2", TOWN="sched"), "c"),
    ("검토 반영 전 채택안 전체(통합표 + 체류 6/5/5/4)", dict(QUEST="v2", TOWN="sched"), "g"),
    ("의뢰 = 시설 설계 Q01~Q10 (91석)", dict(QUEST="fac"), "c"),
    ("의뢰 = 마을 설계 크기 그대로 + 반복 C", dict(QUEST="town"), "c"),
    ("최종 + 마을 설계 장비 보장(정예 영웅+, 측량 희귀+)", dict(GUAR="1"), "c"),
    ("최종, 상점 끔", dict(SHOP="0"), "c"),
    ("최종, 진열 iLv = 최고 층 (상점 조정 규칙)", dict(SHOP_ILV="0"), "c"),
    ("최종, 상점 4층 도달 뒤 귀환에 열고 진열 iLv = 최고 층", dict(SHOP_UNLOCK="maxf4", SHOP_ILV="0"), "c"),
]
BOUNTY = [("반복 의뢰 없음" if b == "0" else "반복 의뢰 " + b + ("석" if b.isdigit() else "(작음 = 2 + 기준 층)") + (", 10층 전 상한 없음" if m == "0" else ", 10층 전 6회 상한"), dict(BOUNTY=b, BOUNTY_MAX=m), p)
          for b, m in (("0", "0"), ("2", "6"), ("2", "0"), ("3", "0"), ("5", "0"), ("C", "0")) for p in ("c", "g")]   # v2: 반복 의뢰 보상 민감도(최종 의뢰표 위에 켰을 때)
CURVES = []
for hp, at in (("1.25", "1.20"), ("1.27", "1.20"), ("1.29", "1.20"), ("1.30", "1.20"), ("1.26", "1.24"), ("1.27", "1.24"), ("1.28", "1.24"), ("1.26", "1.26")):
    for p in ("c", "g"):
        CURVES.append((f"{hp} / {at}", dict(), p, hp, at))
SEEDS = [(f"씨앗 {sd}", dict(SEED=sd), p) for sd in ("11", "22", "33") for p in ("c", "g")]   # 2차: 난수 씨앗 안정성
GOLDV = []   # v3: 골드 변형 (라벨, 환경 변수, 정책 목록). 기본값이 골드라 GOLD=0 행만 비교용
for label, env, pols in (
        ("**v3 최종 (골드 쓰기 base: 칸 늘리기는 살 수 있으면, 구경 새로고침 1번)**", dict(), "cg"),
        ("비교: 2차 강화석 전용 상점 (GOLD=0)", dict(GOLD="0"), "cg"),
        ("쓰기 reserve: 첫 새로고침도 다음 칸 늘리기 값을 남길 때만", dict(GSPEND="reserve"), "cg"),
        ("쓰기 smart: 새로고침은 오를 부위가 있을 때만", dict(GSPEND="smart"), "cg"),
        ("쓰기 always: 새로고침을 늘 2번", dict(GSPEND="always"), "c"),
        ("쓰기 save: 칸 늘리기를 사지 않음", dict(GSPEND="save"), "c"),
        ("쓰기 none: 진열 구매만", dict(GSPEND="none"), "c"),
        ("골드 출처 x1.33 (예전 2차 골드 비교 경로 값)", dict(GSCALE="1.333"), "cg"),
        ("골드 출처 x0.67", dict(GSCALE="0.667"), "cg"),
        ("의뢰 골드 없음", dict(QGOLD="0"), "c"),
        ("새로고침 방문당 1번", dict(REROLL="1"), "c"),
        ("장비 팔기: 일반 +0 (10골드)", dict(SELL="3"), "cg"),
        ("장비 팔기: 일반·고급 +0 (10 / 20골드)", dict(SELL="1"), "cg"),
        ("장비 팔기: 일반~영웅 +0", dict(SELL="2"), "c"),
        ("상점 5층 보스 첫 처치 뒤 귀환(창고와 같은 방문)", dict(SHOP_UNLOCK="first5"), "cg"),
        ("상점 4층 도달 뒤 귀환", dict(SHOP_UNLOCK="maxf4"), "cg"),
        ("상점 7층 + 진열 iLv = 최고 층", dict(SHOP_ILV="0"), "c")):
    for p in pols:
        GOLDV.append((label, env, p))
GROUPS = dict(town=TOWN, bounty=BOUNTY, curves=CURVES, seeds=SEEDS, gold=GOLDV)


def run(spec):
    label, env, pol = spec[:3]; hp = spec[3] if len(spec) > 3 else "1.27"; at = spec[4] if len(spec) > 4 else "1.24"
    e = dict(os.environ); e.update(env); e["PYTHONIOENCODING"] = "utf-8"
    out = subprocess.run([sys.executable, os.path.join(HERE, PROG + ".py"), N, hp, pol, "1.00", "0.95", at], env=e, capture_output=True).stdout.decode("utf-8")   # 7칸: FIN_PROG
    return label, pol, out


def g(pat, s, i=1, d="—"):
    m = re.search(pat, s)
    return m.group(i) if m else d


def row(label, pol, s):
    t10 = g(r"10층 보스 첫 처치: 도달 \d+%, 원정 중앙 (\d+), 시간 중앙 (\d+)분.*?안 (\d+)%", s, 2)
    r10 = g(r"10층 보스 첫 처치: 도달 \d+%, 원정 중앙 (\d+)", s)
    in10 = g(r"1h45~2h45 안 (\d+)%", s)
    t5 = g(r"5층 보스 첫 처치: 도달 \d+%, 원정 중앙 \d+, 시간 중앙 (\d+)분", s)
    ep = g(r"첫 영웅\(보라\) 획득: 도달 \d+%, 원정 중앙 \d+, 시간 중앙 (\d+)분", s)
    lg = g(r"첫 전설 획득: 도달 \d+%, 원정 중앙 \d+, 시간 중앙 (\d+)분", s)
    rk = g(r"\+8 첫 시도 중앙 (\d+)분", s); dr = g(r"첫 하락 중앙 (\d+)분", s); d60 = g(r"60분 안 첫 하락 (\d+)%", s)
    w10 = g(r"10층 보스 첫 도전 승률 (\d+)%", s); tr = g(r"강화 시도 평균 ([\d.]+)", s)
    nt = g(r"원정 9~12: .*?강화 (\d+)%", s)
    q = g(r"의뢰 강화석 중앙 (\d+)석 \(평균 ([\d.]+), 늦은 사람 90% (\d+)", s, 0, "")
    qm = g(r"의뢰 강화석 중앙 (\d+)석", s, 1, "0"); q90 = g(r"늦은 사람 90% (\d+)", s, 1, "0"); qs = g(r"의뢰 비중 평균 ([\d.]+)%", s, 1, "0"); qo = g(r"95석을 넘은 사람 ([\d.]+)%", s, 1, "0")
    tv = g(r"방문당 평균 ([\d.]+)분", s, 1, "3.00")
    return (f"| {label} | {'신중' if pol == 'c' else '밀어붙임'} | {t10}분 (원정 {r10}) | {in10}% | {t5}분 | {ep}분 | {lg}분 | {rk} / {dr}분 ({d60}%) | {w10}% | {tr} | {nt}% | "
            f"{qm} / {q90}석 ({qs}%, 95석 초과 {qo}%) | {tv}분 |")


def grow(label, pol, s):   # v3: 골드 지표 행
    t10 = g(r"10층 보스 첫 처치: 도달 \d+%, 원정 중앙 \d+, 시간 중앙 (\d+)분", s); in10 = g(r"1h45~2h45 안 (\d+)%", s)
    d60 = g(r"60분 안 첫 하락 (\d+)%", s); lg = g(r"첫 전설 획득: 도달 \d+%, 원정 중앙 \d+, 시간 중앙 (\d+)분", s); tr = g(r"강화 시도 평균 ([\d.]+)", s)
    st = g(r"총수입 중앙 (\d+)석", s)
    gi = g(r"골드 수입\(10층 첫 처치 순간까지\) 중앙 (\d+)", s); lr = g(r"번 골드의 중앙 (\d+)%", s); l30 = g(r"30% 이하인 사람 (\d+)%", s)
    pv = g(r"마을 방문당 ([\d.]+)회 \(진열", s); rr = g(r"새로고침 ([\d.]+), 창고", s); pp = g(r"0.5회 이상인 사람 (\d+)%", s)
    po = g(r"쓴 골드 \d+ \((\d+)%\)", s); sl = g(r"총수입의 평균 ([\d.]+)%", s, 1, "—")
    return (f"| {label} | {'신중' if pol == 'c' else '밀어붙임'} | {t10}분 | {in10}% | {d60}% | {lg}분 | {tr} | {st}석 | {gi} | {lr}% ({l30}%) | {pv}회 (새로고침 {rr}, 0.5회 이상 {pp}%) | {po}% | {sl}% |")


def main():
    specs = GROUPS.get(GROUP) if GROUP != "all" else TOWN + BOUNTY + CURVES + SEEDS
    with ThreadPoolExecutor(max_workers=min(24, len(specs))) as ex:
        outs = list(ex.map(run, specs))
    lines = [f"# fin_variants {GROUP}, 인원 {N}, 보스 R50 5층 0.95 / 10층 1.00, 체력/공격 성장 1.27/1.24 (curves 묶음은 표의 값)",
             "| 변형 | 정책 | 10층 첫 처치 | 105~165분 안 | 5층 보스 | 첫 영웅 | 첫 전설 | 첫 +8 도전 / 첫 하락 (60분 안) | 10층 첫 도전 승률 | 방문당 강화 시도(1~8) | 눈에 띄는 강화 9~12 | 10층 전 의뢰 중앙 / 늦은 90% (비중) | 10층 전 방문당 체류 |",
             "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
    if GROUP == "gold":   # v3: 골드 묶음은 골드 열
        lines = [f"# fin_variants gold (v3), 인원 {N}, 체력/공격 성장 1.27/1.24. 남은 골드 = 10층 첫 처치 순간(지갑 + 손에 든 이번 원정 골드) ÷ 그때까지 번 골드",
                 "| 변형 | 정책 | 10층 첫 처치 | 105~165분 안 | 60분 안 첫 하락 | 첫 전설 | 방문당 강화 시도(1~8) | 10층 전 강화석 총수입 | 10층 전 골드 수입 | 남은 골드 중앙 (30% 이하인 사람) | 10층 전 골드 구매/방문 | 10층 뒤 방문 지출/수입 | 팔기로 포기한 강화석 |",
                 "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for label, pol, out in outs:
        lines.append(grow(label, pol, out) if GROUP == "gold" else row(label, pol, out))
    txt = "\n".join(lines)
    name = f"fin_variants_{GROUP}.txt" if PROG == "fin_progress" else (f"fin7_variants_{GROUP}.txt" if PROG == "fin_progress7" else f"{PROG}_variants_{GROUP}.txt")   # 7칸
    io.open(os.path.join(HERE, name), "w", encoding="utf-8").write(txt + "\n")
    sys.stdout.buffer.write((txt + "\n").encode("utf-8"))


# ================================================================ 7칸: fin7 묶음 (장비 문서 12장 단계 5 합격선)
def v7_window(per_run):
    """'7층 도달 뒤 귀환'(maxf >= 7 이 된 첫 마을 방문)의 (도착 시각, 방문 끝 시각). fin_progress7.v7_window 와 같은 정의."""
    t0 = 0.0
    for x in per_run:
        t1 = t0 + x["mins"]
        if x["maxf"] >= 7: return (t1 - x["tmin"], t1)
        t0 = t1
    return None


def drop_vs_v7(res, key):
    inc = exc = n = 0; arr = []
    for r in res:
        w = v7_window(r[4])
        if w is None: continue
        n += 1; arr.append(w[0]); d = r[0][key]
        if d is not None and d <= w[1] + 1e-9: inc += 1
        if d is not None and d < w[0] - 1e-9: exc += 1
    return (inc / n if n else 0.0, exc / n if n else 0.0, statistics.median(arr) if arr else float("nan"))


def probe2(n, pol):
    """2차 fin_progress.py 를 고치지 않고 돌리며, 진단 고리(stats·ratio·make_item·p_boss_win 감싸기와 호출한 play 의 지역 변수 읽기)로
    fin_progress7 의 [7칸] 지표와 같은 정의의 값을 잰다. 난수 소비는 바꾸지 않으므로 fin_progress.py 결과와 같은 판이다."""
    sys.argv = ["fin_progress.py", n, "1.27", pol, "1.00", "0.95", "1.24"]
    F = importlib.import_module("fin_progress")
    o_stats, o_ratio, o_make, o_pbw = F.stats, F.ratio, F.make_item, F.p_boss_win
    CUR = {}

    def reset():
        CUR.clear(); CUR.update(rec={}, runs={}, parts=[0, 0, 0], drops=0, bossbag={5: [], 10: []}, full_picks=0)

    def stats_w(eq, lvo=None):
        st = o_stats(eq, lvo)
        boss = sum(v for it in eq if it is not None for k, v in it.opts if k == "boss")
        st["B"] = st["A"] / (1 + 0.2 * boss) * (1 + boss); st["cm"] = 1 + st["crit"] * (st["cd"] - 1)
        return st

    def run_rec(L):
        r = CUR["runs"].get(L["run"])
        if r is None:
            r = CUR["runs"][L["run"]] = dict(pre=L["ev"]["first10"] is None, full=False, bagmax=len(L["bag"]), kept=len(L["bag"]))
        return r

    def ratio_w(st, f):
        fr = sys._getframe(1)
        if fr.f_code.co_name == "play":
            L = fr.f_locals; run_rec(L)
            if f not in L["entry"]: CUR["rec"][f] = st   # 층 첫 진입 기록을 만드는 호출이 마지막으로 남는다
        return o_ratio(st, f)

    def make_w(rng, slot, g, ilv):
        fr = sys._getframe(1)
        if fr.f_code.co_name == "play":
            L = fr.f_locals
            if rng is L["rng"]:   # 던전 드랍만(상점은 rng_shop, 의뢰 상자는 give_box)
                r = run_rec(L); bag = L["bag"]; r["bagmax"] = max(r["bagmax"], len(bag))
                if len(bag) >= 40 + 5 * L["caps"]["bag"]:
                    r["full"] = True
                    if r["pre"]: CUR["full_picks"] += 1
                if L["ev"]["first10"] is None: CUR["parts"][slot] += 1; CUR["drops"] += 1
        return o_make(rng, slot, g, ilv)

    def pbw_w(R, f):
        fr = sys._getframe(1)
        if fr.f_code.co_name == "play":
            L = fr.f_locals; r = run_rec(L)
            if r["pre"]: CUR["bossbag"][f].append(len(L["bag"]))
        return o_pbw(R, f)

    F.stats, F.ratio, F.make_item, F.p_boss_win = stats_w, ratio_w, make_w, pbw_w
    rng = random.Random(int(os.environ.get("SEED", "20261002")))
    res = []; extra = []
    for _ in range(int(n)):
        reset(); r = F.play(rng); res.append(r)
        e10 = CUR["rec"].get(10) if 10 in r[1] else None
        runs = [x for x in CUR["runs"].values() if x["pre"]]
        extra.append(dict(B10=e10["B"] if e10 else None, cm10=e10["cm"] if e10 else None, runs=len(runs), full=sum(1 for x in runs if x["full"]),
                          bagmax=[x["bagmax"] for x in runs], kept=[x["kept"] for x in runs], bossbag=CUR["bossbag"], parts=list(CUR["parts"]),
                          drops=CUR["drops"], full_picks=CUR["full_picks"]))
    med = statistics.median; N_ = len(res)
    Rm = {}; atk = {}; hp = {}; df = {}
    for f in range(1, 11):
        es = [r[1][f] for r in res if f in r[1]]
        if es: Rm[f] = med(e["R"] for e in es); atk[f] = med(e["atk"] for e in es); hp[f] = med(e["hp"] for e in es); df[f] = med(e["def_"] for e in es)
    t10 = [r[0]["first10"][1] for r in res if r[0]["first10"]]
    an_inc, an_exc, v7arr = drop_vs_v7(res, "drop")
    drop_any = [r[0]["drop"] or 999 for r in res]
    runs = sum(x["runs"] for x in extra); bm = [b for x in extra for b in x["bagmax"]]; kp = [b for x in extra for b in x["kept"]]
    bb5 = [b for x in extra for b in x["bossbag"][5]]; bb10 = [b for x in extra for b in x["bossbag"][10]]
    parts = [statistics.mean(x["parts"][s] for x in extra) for s in range(3)]
    B10 = [x["B10"] for x in extra if x["B10"] is not None]; cm10 = [x["cm10"] for x in extra if x["cm10"] is not None]
    m = dict(prog="2차", drop_mult=1.0, pol=pol, n=N_, t10=med(t10) if t10 else None, t10_in=sum(1 for v in t10 if 105 <= v <= 165) / max(1, len(t10)),
             t10_reach=len(t10) / N_, R=Rm, atk=atk, hp=hp, df=df, B10=med(B10) if B10 else None, cm10=med(cm10) if cm10 else None, atk10=atk.get(10),
             drop_any_inc=an_inc, drop_any_exc=an_exc, v7arr=v7arr, d60_any=sum(1 for x in drop_any if x <= 60) / N_,
             full_runs=sum(x["full"] for x in extra) / max(1, runs), bagmax=statistics.mean(bm), bagmax99=F.pct(bm, .99),
             bossbag5=statistics.mean(bb5) if bb5 else None, bossbag10=statistics.mean(bb10) if bb10 else None,
             kept=statistics.mean(kp), kept_max=max(kp), parts=parts, drops=sum(parts),
             S0=dict(A=F.S0["A"], S=F.S0["S"], M=F.S0["M"]), R0=o_ratio(F.S0, 1))
    sys.stdout.buffer.write(("#METRICS2 " + json.dumps(m, ensure_ascii=False) + "\n").encode("utf-8"))


# (라벨, 환경 변수, 정책, 종류) 종류 = "2" 2차 진단 / "7" 7칸판. 환경 변수의 _BOSS_HP 는 보스 체력 배율(run7 참고).
FIN7 = [("2차 (fin_progress.py 그대로)", dict(), "c", "2"), ("2차 (fin_progress.py 그대로)", dict(), "g", "2"),
        ("7칸 드랍 ×1.0", dict(DROP_MULT="1.0"), "c", "7"), ("7칸 드랍 ×1.0", dict(DROP_MULT="1.0"), "g", "7"),
        ("7칸 드랍 ×1.2", dict(DROP_MULT="1.2"), "c", "7"), ("7칸 드랍 ×1.2", dict(DROP_MULT="1.2"), "g", "7"),
        ("민감도: 7칸 ×1.0, R 기준 = 2차 시작 A(RNORM=v2)", dict(DROP_MULT="1.0", RNORM="v2"), "c", "7"),
        ("민감도: 7칸 ×1.0, 상점 문턱 모두 5%(SHOP_TH=flat)", dict(DROP_MULT="1.0", SHOP_TH="flat"), "c", "7"),
        ("민감도: 7칸 ×1.0, 씨앗 11", dict(DROP_MULT="1.0", SEED="11"), "c", "7"),
        ("민감도: 2차, 씨앗 11", dict(SEED="11"), "c", "2")]
C12 = dict(CRIT_GLV="10,20", CRIT_RING="5,10", WPN_CRIT="10,0,20")
# 13장 위험 1 손잡이 차례: ① 장갑·반지 치명 옵션 범위 → ② 무기 고유 치명 → ③ 보스 체력(마지막). 두 정책을 같이 돌린다.
KNOBS = [("손잡이 ① 장갑 치명 10~20‰ · 반지 치명 5~10‰", dict(DROP_MULT="1.0", CRIT_GLV="10,20", CRIT_RING="5,10")),
         ("손잡이 ② 무기 고유 치명 장검 +10‰ · 쌍검 +20‰", dict(DROP_MULT="1.0", WPN_CRIT="10,0,20")),
         ("손잡이 ①+②", dict(DROP_MULT="1.0", **C12)),
         ("손잡이 ③ 보스 체력 ×1.15", dict(DROP_MULT="1.0", _BOSS_HP="1.15")),
         ("손잡이 ③ 보스 체력 ×1.20", dict(DROP_MULT="1.0", _BOSS_HP="1.20")),
         ("손잡이 ①+② + ③ 보스 체력 ×1.15", dict(DROP_MULT="1.0", _BOSS_HP="1.15", **C12)),
         ("드랍 ×1.2 + ③ 보스 체력 ×1.30", dict(DROP_MULT="1.2", _BOSS_HP="1.30")),
         # 13장 밖(원인 나눠 보기·참고 후보). 기획 값이 아니다.
         ("진단: 부위 문턱 모두 30‰(2차 +3%와 같음)", dict(DROP_MULT="1.0", THRESH_FLAT="0.03")),
         ("진단: 부위 문턱 모두 30‰ + ③ 보스 체력 ×1.10", dict(DROP_MULT="1.0", THRESH_FLAT="0.03", _BOSS_HP="1.10")),
         ("진단: 작은 칸 문턱 20‰(30,20,20,20,20,20,20)", dict(DROP_MULT="1.0", THRESH_PM="30,20,20,20,20,20,20")),
         ("진단: 빈 자리 채우기 끔", dict(DROP_MULT="1.0", NO_FILL="1")),
         ("진단: 모든 옵션 값 ×0.8", dict(DROP_MULT="1.0", OPT_MULT="0.8"))]
FIN7 += [(lab, env, pol, "7") for lab, env in KNOBS for pol in ("c", "g")]


def run7(spec):
    """spec 의 env 에서 '_BOSS_HP'(보스 체력 배율 k)는 환경 변수가 아니다: 보스 승률의 R50 을 k^0.6 배(R 의 공격 몫 지수 0.6)로 올리고,
    보스 처치 시간 판정에 k 를 곱한다(13장 위험 1 손잡이 ③)."""
    label, env, pol, kind = spec
    env = dict(env); k = float(env.pop("_BOSS_HP", "1"))
    e = dict(os.environ); e.update(env); e["PYTHONIOENCODING"] = "utf-8"; e["METRICS_JSON"] = "1"
    if kind == "2":
        cmd = [sys.executable, os.path.join(HERE, "fin_variants.py"), "_probe2", N, pol]
    else:
        cmd = [sys.executable, os.path.join(HERE, "fin_progress7.py"), N, "1.27", pol, f"{1.00 * k ** 0.6:.4f}", f"{0.95 * k ** 0.6:.4f}", "1.24"]
    p = subprocess.run(cmd, env=e, capture_output=True)
    out = p.stdout.decode("utf-8")
    m = re.search(r"^#METRICS[27] (.*)$", out, re.M)
    if not m: raise SystemExit(f"fin7 {label} {pol} 실패:\n{p.stderr.decode('utf-8', 'replace')[-2000:]}")
    d = json.loads(m.group(1)); d["boss_hp"] = k
    for kk in ("R", "atk", "hp", "df"): d[kk] = {int(a): b for a, b in d[kk].items()}
    return label, pol, kind, d, out


BOSS_BASE = 107.0   # 3차 부록: 10층 보스 275,032 ÷ (장비 기준 공격력 1,462 × 1.76) = 107초 (2차 장비, 치명은 식에 없음)
def boar_loss(d, f): return round(300 * 1.24 ** (f - 1)) * 1000 / (1000 + d["df"][f] * 10) / (d["hp"][f] * 10) * 100


def boss_time(d, base):
    """10층 보스 처치 시간(초). 2차 대비 고친 값 = 107 × (2차 10층 첫 진입 보스전 지수 B 중앙 ÷ 이 판의 B 중앙).
    B = 공격력 × 기대 치명 배율 × [0.65 × 계수 × (1 + 공속) ÷ 1.49 + 0.35 × (1 + 스킬) ÷ (1 − 재사용)] × (1 + 보스 피해)."""
    return BOSS_BASE * base["B10"] / d["B10"] * d.get("boss_hp", 1.0)


def fin7_report(outs, n):
    by = {(lab, pol): d for lab, pol, kind, d, out in outs}
    v2c = by[("2차 (fin_progress.py 그대로)", "c")]; v2g = by[("2차 (fin_progress.py 그대로)", "g")]
    L = [f"# fin_variants fin7 (장비 7칸, 장비-능력치-파밍-1차.md 12장 단계 5), 인원 {n}, 체력/공격 성장 1.27/1.24, 보스 R50 5층 0.95 / 10층 1.00, 씨앗 20261002",
         "# 2차 = fin_progress.py(v3 기본값) 그대로 돌리고 진단 고리로 같은 정의의 지표를 잼. 7칸 = fin_progress7.py. 같은 씨앗·같은 인원.",
         ""]
    def judge(ok): return "통과" if ok else "**벗어남**"
    def col(d, d2c, d2g, dg):
        """한 판(×1.0 또는 ×1.2)의 합격선 항목 (값, 합격 여부)."""
        rr = {f: d["R"][f] / d2c["R"][f] for f in range(1, 11) if f in d["R"] and f in d2c["R"]}
        lo_f = min(rr, key=rr.get); hi_f = max(rr, key=rr.get)
        r_ok = all(0.95 <= v <= 1.05 for v in rr.values())
        bl = {f: boar_loss(d, f) for f in range(1, 11) if f in d["hp"]}
        b_ok = all(11.0 <= v <= 14.0 for v in bl.values()); blo = min(bl, key=bl.get); bhi = max(bl, key=bl.get)
        bt = boss_time(d, d2c); bt_ok = 100 <= bt <= 120
        tc, tg = d["t10"], dg["t10"]; t_ok = abs(tc / 144 - 1) <= 0.05 and abs(tg / 124 - 1) <= 0.05
        dr = d["drop_wa_inc"]; dr_ok = dr >= 0.80
        fd = (d["full_runs"] - d2c["full_runs"]) * 100; f_ok = fd <= 10
        dm = d["drop_mult"]; w, gl, bo = d["parts"][0], d["parts"][3], d["parts"][4]
        p_ok = abs(w / (40 * dm) - 1) <= 0.15 and abs(gl / (8.9 * dm) - 1) <= 0.15 and abs(bo / (8.9 * dm) - 1) <= 0.15
        s_ok = abs(d["S0"]["S"] - d2c["S0"]["S"]) < 1e-9 and abs(d["S0"]["M"] - d2c["S0"]["M"]) < 1e-9 and abs(d["R0"] - d2c["R0"]) < 1e-9
        return [
            (f"{min(rr.values()):.3f} ({lo_f}층) ~ {max(rr.values()):.3f} ({hi_f}층)", judge(r_ok)),
            (f"{min(bl.values()):.1f}% ({blo}층) ~ {max(bl.values()):.1f}% ({bhi}층)", judge(b_ok)),
            (f"{bt:.0f}초 (B {d['B10'] * 10:.0f} / 2차 {d2c['B10'] * 10:.0f}, 기대 치명 {d['cm10']:.3f} / 2차 {d2c['cm10']:.3f})", judge(bt_ok)),
            (f"신중 {tc:.0f}분 ({(tc / 144 - 1) * 100:+.0f}%) / 밀어붙임 {tg:.0f}분 ({(tg / 124 - 1) * 100:+.0f}%)", judge(t_ok)),
            (f"{dr * 100:.0f}% (그 방문 전 {d['drop_wa_exc'] * 100:.0f}%, 귀환 도착 중앙 {d['v7arr']:.0f}분; 모든 부위 {d['drop_any_inc'] * 100:.0f}%)", judge(dr_ok)),
            (f"{d['full_runs'] * 100:.1f}% (2차 {d2c['full_runs'] * 100:.1f}%, {fd:+.1f}%p), 원정 최대 칸 평균 {d['bagmax']:.1f} / 2차 {d2c['bagmax']:.1f}, 출발 때 남긴 장비 {d['kept']:.2f} / 2차 {d2c['kept']:.2f}", judge(f_ok)),
            (f"무기 {w:.1f} · 장갑 {gl:.1f} · 장화 {bo:.1f} (기준 {40 * dm:.0f} / {8.9 * dm:.1f} / {8.9 * dm:.1f} ±15%)", judge(p_ok)),
            (f"S {d['S0']['S']:.2f} = 2차 {d2c['S0']['S']:.2f}, M {d['S0']['M']:.2f} = {d2c['S0']['M']:.2f}, 시작 R {d['R0']:.3f} = {d2c['R0']:.3f} (A {(d['S0']['A'] / d2c['S0']['A'] - 1) * 100:+.2f}%: 장검 고유 치명, 3-2 예상 +1.7%)", judge(s_ok)),
        ]
    x10 = col(by[("7칸 드랍 ×1.0", "c")], v2c, v2g, by[("7칸 드랍 ×1.0", "g")])
    x12 = col(by[("7칸 드랍 ×1.2", "c")], v2c, v2g, by[("7칸 드랍 ×1.2", "g")])
    names = ["R 비 (층 첫 진입 R 중앙, 7칸 ÷ 2차, 1~10층)", "돌진 한 번 잃는 체력 (층 첫 진입 중앙 장비, 1~10층)", "10층 보스 처치 시간 (2차 대비 고친 107초)",
             "2장 시간표 10층 첫 처치 (144 / 124분)", "'7층 도달 뒤 귀환' 때까지 첫 하락 (무기·갑옷 기준)", "가방이 찬 채로 주운 원정 비율 (2차 대비)",
             "10층 첫 처치 전 부위별 장비 수 (사람당)", "시작 S0·시작 R (2차와 같은지)"]
    lines_ = ["0.95~1.05", "11~14%", "100~120초", "±5%", "80% 이상", "2차 + 10%p 이하", "무기 약 40, 장갑·장화 약 9 (×배율)", "같음"]
    L += ["## 합격선 비교 (신중 정책 기준, 시간표만 두 정책)", "| 항목 | 합격선 | 7칸 ×1.0 | 판정 | 7칸 ×1.2 | 판정 |", "|---|---|---|---|---|---|"]
    for i, nm in enumerate(names):
        L.append(f"| {nm} | {lines_[i]} | {x10[i][0]} | {x10[i][1]} | {x12[i][0]} | {x12[i][1]} |")
    # 층별 상세
    L += ["", "## 층별 상세 (층 첫 진입 중앙. 공격력·체력·방어는 10배 단위, 신중 정책)",
          "| 층 | R 2차 | R 7칸 ×1.0 (비) | R 7칸 ×1.2 (비) | 공격력 2차 / ×1.0 / ×1.2 | 체력 2차 / ×1.0 / ×1.2 | 방어 2차 / ×1.0 / ×1.2 | 돌진 잃는 체력 2차 / ×1.0 / ×1.2 |",
          "|---|---|---|---|---|---|---|---|"]
    a, b = by[("7칸 드랍 ×1.0", "c")], by[("7칸 드랍 ×1.2", "c")]
    for f in range(1, 11):
        if f not in v2c["R"] or f not in a["R"] or f not in b["R"]: continue
        L.append(f"| {f} | {v2c['R'][f]:.3f} | {a['R'][f]:.3f} ({a['R'][f] / v2c['R'][f]:.3f}) | {b['R'][f]:.3f} ({b['R'][f] / v2c['R'][f]:.3f}) | "
                 f"{v2c['atk'][f] * 10:.0f} / {a['atk'][f] * 10:.0f} / {b['atk'][f] * 10:.0f} | {v2c['hp'][f] * 10:.0f} / {a['hp'][f] * 10:.0f} / {b['hp'][f] * 10:.0f} | "
                 f"{v2c['df'][f] * 10:.0f} / {a['df'][f] * 10:.0f} / {b['df'][f] * 10:.0f} | {boar_loss(v2c, f):.1f}% / {boar_loss(a, f):.1f}% / {boar_loss(b, f):.1f}% |")
    # 손잡이 후보 (13장 위험 1 차례 + 13장 밖 진단)
    def boss_doc(k): return f"보스 기본 체력 32,000 → {32000 * k:,.0f} (10층 275,032 → {275032 * k:,.0f}, 5층 45,785 → {45785 * k:,.0f})"
    DOCV = {"7칸 드랍 ×1.0": "(문서 값 그대로)",
            "손잡이 ① 장갑 치명 10~20‰ · 반지 치명 5~10‰": "5-3 장갑 치명 확률 20~40‰ → 10~20‰(전설 최대 60 → 30‰), 반지 10~20‰ → 5~10‰(30 → 15‰)",
            "손잡이 ② 무기 고유 치명 장검 +10‰ · 쌍검 +20‰": "4-3 무기 고유 치명 확률 장검 +20 → +10‰(7% → 6%), 쌍검 +40 → +20‰(9% → 7%), 대검 0 그대로",
            "손잡이 ①+②": "① 과 ② 함께",
            "손잡이 ③ 보스 체력 ×1.15": boss_doc(1.15), "손잡이 ③ 보스 체력 ×1.20": boss_doc(1.20),
            "손잡이 ①+② + ③ 보스 체력 ×1.15": "① + ② + " + boss_doc(1.15),
            "드랍 ×1.2 + ③ 보스 체력 ×1.30": "결정 1 ×1.2 + " + boss_doc(1.30)}
    L += ["", "## 손잡이 후보 (드랍 ×1.0 기준. 13장 위험 1 차례 ① → ② → ③, 그 아래는 13장 밖 원인 나눠 보기 — 기획 값 아님)",
          "보스 체력 배율 k 는 시뮬레이션에서 보스 승률의 R50 을 k^0.6 배(5층·10층 모두)로 올리고, 보스 처치 시간에 k 를 곱해 넣었다.",
          "| 후보 | 문서에 넣을 값 | 10층 첫 처치 신중 / 밀어붙임 (144 / 124분 ±5%) | 10층 보스 (100~120초) | R 비 (0.95~1.05) | '7층 귀환'까지 첫 하락 무기·갑옷 (80%) | 돌진 최대 (14%) | 시간표·보스·R 비 셋 다 |",
          "|---|---|---|---|---|---|---|---|"]
    for lab in ["7칸 드랍 ×1.0"] + [k for k, _ in KNOBS]:
        if (lab, "c") not in by or (lab, "g") not in by: continue
        dc, dg = by[(lab, "c")], by[(lab, "g")]
        tc, tg = dc["t10"], dg["t10"]; t_ok = abs(tc / 144 - 1) <= 0.05 and abs(tg / 124 - 1) <= 0.05
        bt = boss_time(dc, v2c); b_ok = 100 <= bt <= 120
        rr = [dc["R"][f] / v2c["R"][f] for f in range(1, 11) if f in dc["R"] and f in v2c["R"]]; r_ok = all(0.95 <= v <= 1.05 for v in rr)
        bl = max(boar_loss(dc, f) for f in range(1, 11) if f in dc["hp"])
        L.append(f"| {lab} | {DOCV.get(lab, '—')} | {tc:.0f}분 ({(tc / 144 - 1) * 100:+.0f}%) / {tg:.0f}분 ({(tg / 124 - 1) * 100:+.0f}%) {'통과' if t_ok else '**벗어남**'} | "
                 f"{bt:.0f}초 {'통과' if b_ok else '**벗어남**'} | {min(rr):.3f}~{max(rr):.3f} {'통과' if r_ok else '**벗어남**'} | "
                 f"{dc['drop_wa_inc'] * 100:.0f}% {'통과' if dc['drop_wa_inc'] >= 0.8 else '**벗어남**'} | {bl:.1f}% {'통과' if bl <= 14 else '**벗어남**'} | "
                 f"{'**셋 다 통과**' if (t_ok and b_ok and r_ok) else '아님'} |")
    # 모든 판 요약
    L += ["", "## 판별 요약 (2차 기준 값과 같은 정의)",
          "| 판 | 정책 | 10층 첫 처치 (1h45~2h45 안) | R 비 최소~최대 | 돌진 최소~최대 | 10층 보스 | '7층 귀환'까지 첫 하락 무기·갑옷 / 모든 부위 | 60분 안 첫 하락 무기·갑옷 / 모든 부위 | 가방 찬 원정 | 출발 때 남긴 장비 | 10층 전 장비 수 (무기/장갑/장화) |",
          "|---|---|---|---|---|---|---|---|---|---|---|"]
    for lab, pol, kind, d, out in outs:
        base = v2c if pol == "c" else v2g
        if lab.endswith("씨앗 11") and ("민감도: 2차, 씨앗 11", "c") in by: base = by[("민감도: 2차, 씨앗 11", "c")] if kind == "7" else base
        rr = [d["R"][f] / base["R"][f] for f in range(2, 11) if f in d["R"] and f in base["R"]]
        bl = [boar_loss(d, f) for f in range(1, 11) if f in d["hp"]]
        bt = boss_time(d, base) if pol == "c" else float("nan")
        wa = f"{d['drop_wa_inc'] * 100:.0f}%" if "drop_wa_inc" in d else "—"
        d60 = f"{d['d60_wa'] * 100:.0f}%" if "d60_wa" in d else "—"
        pp = d["parts"]
        pstr = f"{pp[0]:.1f} / {pp[3]:.1f} / {pp[4]:.1f}" if len(pp) == 7 else f"(3칸) 무기 {pp[0]:.1f} · 방어구 {pp[1]:.1f} · 장신구 {pp[2]:.1f}"
        L.append(f"| {lab} | {'신중' if pol == 'c' else '밀어붙임'} | {d['t10']:.0f}분 ({d['t10_in'] * 100:.0f}%) | {min(rr):.3f}~{max(rr):.3f} | {min(bl):.1f}~{max(bl):.1f}% | "
                 f"{'—' if math.isnan(bt) else f'{bt:.0f}초'} | {wa} / {d['drop_any_inc'] * 100:.0f}% | {d60} / {d['d60_any'] * 100:.0f}% | {d['full_runs'] * 100:.1f}% | {d['kept']:.2f} | {pstr} |")
    return L


def main7():
    outs = []
    with ThreadPoolExecutor(max_workers=min(24, len(FIN7))) as ex:
        outs = list(ex.map(run7, FIN7))
    L = fin7_report(outs, N)
    txt = "\n".join(L)
    io.open(os.path.join(HERE, "fin7_variants.txt"), "w", encoding="utf-8").write(txt + "\n")
    sys.stdout.buffer.write((txt + "\n").encode("utf-8"))


if __name__ == "__main__":
    if GROUP == "_probe2":   # 7칸: fin7 묶음이 부르는 2차 진단(사람이 직접 쓰지 않음)
        probe2(N, sys.argv[3] if len(sys.argv) > 3 else "c")
    elif GROUP == "fin7":
        main7()
    else:
        main()
