# -*- coding: utf-8 -*-
"""7칸 검산 예시 (장비-능력치-파밍-1차.md 12장 단계 5, fin_examples.py 와 따로 둠).
 1) 8-5 비교 표시 예 3줄 (10층 기준 공격 1,462·체력 7,076·방어 1,600)
 2) 7장 상한 표 (모든 자리 iLv11 전설 +15, 굴림 100%, 옵션 최대) — 2차 3칸 최대와 나란히
 3) 3-2 시작 A 증가 (무기 고유 치명) · 단일 대상 계수
 4) 8-4 8자리 평균 강화석 (+5 / +8 / +10 / +12), 2차 3칸·배율 없음과 비교
 5) 시작 상태 S0: 7칸 vs 2차 fin_progress.S0
숫자 규칙·표는 fin_progress7.py 의 것을 그대로 불러 쓴다(같은 정의). 2차 값은 fin_progress.py 의 표를 불러 쓴다.
옵션 값에는 fin_progress7.OPT_MULT(기본 0.8, 12장 밸런스 결정 ①)를 곱한다. OPT_MULT=1 이면 결정 전 표다.
실행: python fin_examples7.py [강화 반복=200000]   (결과: fin7_examples_out.txt)"""
import sys, io, os, math, random, statistics, itertools
REPS = int(sys.argv[1]) if len(sys.argv) > 1 else 200000
sys.argv = ["fin_progress7.py", "1", "1.27", "c", "1.00", "0.95", "1.24"]
import fin_progress7 as F7
sys.argv = ["fin_progress.py", "1", "1.27", "c", "1.00", "0.95", "1.24"]
import fin_progress as F2
out = F7.sys.stdout
def rh(x): return int(math.floor(x + 0.5))
SK, IX, NS = F7.SK, F7.IX, F7.NS


def it7(part, kind, g, ilv, opts=(), lv=0, roll=1.0):
    it = F7.Item(part, kind, g, ilv, roll, [(k, v, 4, 1.0) for k, v in opts]); it.lv = lv
    return it


def abc(T, coef=1.49):
    """10배 전 단위 합계 벡터 T(SK 차례, 맨몸 빼고) → A·S·M (fin_progress7.stats 와 같은 식)."""
    atk = (10 + T[0]) * (1 + T[1]); hp = (200 + T[2]) * (1 + T[3]); df = T[4]
    crit = min(.5, .05 + T[5]); cd = min(3.0, 1.5 + T[6]); aspd = min(.3, T[7]); move = max(-.3, min(.3, T[8])); cdr = min(.4, T[9])
    A = atk * (1 + crit * (cd - 1)) * (0.65 * coef * (1 + aspd) / 1.49 + 0.35 * (1 + T[10]) / (1 - cdr)) * (1 + 0.2 * T[11])
    S = hp * (1 + min(df, 400) / 100) + min(.03, T[12]) * A * 10 + T[13] * 10 + T[14] * 5
    return dict(A=A, S=S, M=1 + move, atk=atk, hp=hp, df=df)


def comp(a, b): return 0.6 * (b["A"] / a["A"] - 1) + 0.3 * (b["S"] / a["S"] - 1) + 0.1 * (b["M"] / a["M"] - 1)
def mark(v, th): return "▲" if v >= th else ("▼" if v <= -th else "=")


# ---------------------------------------------------------------- 1) 8-5 비교 표시 예
print("## 1) 8-5 비교 표시 예 (10층 기준 공격 1,462·체력 7,076·방어 1,600)")
print("기준 빌드 (가): 문서 어림과 같은 단순 기준 — 장검, 치명 7%/160%, 공속·이동·스킬·재사용 0, 바꾸는 칸 말고는 그대로")
print("기준 빌드 (나): 7칸 드랍 ×1.0·옵션 ×0.8 시뮬레이션의 10층 첫 진입 중앙 — 치명 13.8%/191%, 공속 +7.5%, 재사용 8.2%, 스킬 피해 10.7%, 보스 피해 5.0%, 이동 +4.2% (fin7_progress_x10.txt [7칸] 줄)")
print("옛 가죽 장갑(고급 iLv6)의 옵션 1줄은 '치명 +3.0%'로 가정(새 장갑의 치명 줄과 같아 치명은 그대로, 바뀌는 것은 공속뿐). 굴림 100%.\n")
def base_T(kind):
    T = [0.0] * NS
    T[IX["atk"]] = 146.2 - 10; T[IX["hp"]] = 707.6 - 200; T[IX["def_"]] = 160.0
    if kind == "가":
        T[IX["crit"]] = .02; T[IX["critdmg"]] = .10
    else:
        T[IX["crit"]] = .088; T[IX["critdmg"]] = .41; T[IX["aspd"]] = .075; T[IX["cdr"]] = .082; T[IX["skill"]] = .107; T[IX["boss"]] = .05; T[IX["move"]] = .042
    return T
def swap(T, old, new):
    c0 = F7.contrib(old, old.lv); c1 = F7.contrib(new, new.lv)
    return [t - a + b for t, a, b in zip(T, c0, c1)]
EX = [("판금 장화 일반 iLv8 → 판금 장화 고급 iLv9 (체력+ 99)", it7(4, "판금 장화", 0, 8), it7(4, "판금 장화", 1, 9, [("hp", 9.9)]), "+0.9% / =", 0.01),
      ("판금 장화 일반 iLv8 → 가죽 장화 고급 iLv9 (이동 +6%)", it7(4, "판금 장화", 0, 8), it7(4, "가죽 장화", 1, 9, [("move", .06)]), "+1.1% / ▲", 0.01),
      ("가죽 장갑 고급 iLv6 → 사슬 장갑 희귀 iLv9 (공속 +4.6%, 치명 +3.0%, 고유 공속 +3%)", it7(3, "가죽 장갑", 1, 6, [("crit", .03)]),
       it7(3, "사슬 장갑", 2, 9, [("aspd", .046), ("crit", .03)]), "+4.2% / ▲", 0.01)]
print("| 바꾸기 | 방어 / 체력 / 이동 변화 | 빌드 | 공격 지수 | 생존 지수 | 종합 | 표시 (작은 칸 문턱 1%) | 문턱 3%였다면 | 문서 어림 (종합 / 표시) |")
print("|---|---|---|---|---|---|---|---|---|")
for name, old, new, doc, th in EX:
    c0 = F7.contrib(old, 0); c1 = F7.contrib(new, 0)
    dd = f"방어 {(c1[IX['def_']] - c0[IX['def_']]) * 10:+.0f} / 체력 {(c1[IX['hp']] - c0[IX['hp']]) * 10:+.0f} / 이동 {(c1[IX['move']] - c0[IX['move']]) * 100:+.0f}%p"
    for kind in ("가", "나"):
        T0 = base_T(kind); a = abc(T0); b = abc(swap(T0, old, new)); v = comp(a, b)
        print(f"| {name} | {dd} | ({kind}) | {(b['A'] / a['A'] - 1) * 100:+.1f}% | {(b['S'] / a['S'] - 1) * 100:+.1f}% | {v * 100:+.1f}% | {mark(v, th)} | {mark(v, 0.03)} | {doc} |")

# ---------------------------------------------------------------- 2) 7장 상한 표
print(f"\n## 2) 7장 상한 점검 (모든 자리 iLv11 전설 +15, 굴림 100%, 옵션 = 전설 최대(위끝 × 1.5 × 옵션 세기 {F7.OPT_MULT:g}, 고정값은 × iLv11 배율 2.5))")
LV = 15; G = 4; IL = 11
MULT = F7.ilv_mult(IL) * F7.GMULT[G] * (1 + F7.CUM[LV] / 100)
def opt_max(part, k):
    best = 0.0
    for kk, lo, hi, flat, w in F7.POOL[part]:
        if kk == k or (k == "def_" and kk == "def"): best = max(best, hi * F7.OPTM[G] * F7.OPT_MULT * (F7.ilv_mult(IL) if flat else 1))   # 옵션 세기 ×0.8(12장 밸런스 결정 ①)
    return best
def uniq_max(part, k, kinds=None):
    return max((F7.KINDS[part][kd][1].get(k, 0.0) for kd in (kinds or F7.KIND_LIST[part])), default=0.0)
def stat_max7(k, weapon=None):
    """한 능력치의 7칸 최대: 자리마다 그 능력치에 가장 유리한 종류의 고유 + 그 부위 옵션 최대(반지 두 자리)."""
    tot = 0.0
    for p, slots in enumerate(F7.PART_SLOTS):
        kinds = (weapon,) if (p == 0 and weapon) else None
        tot += (uniq_max(p, k, kinds) + opt_max(p, k)) * len(slots)
    return tot
def stat_max2(k, weapon=None):
    """2차 3칸 최대(fin_progress 표): 무기·방어구·장신구 옵션 최대 + 장신구 종류 고유."""
    tot = 0.0
    for slot in range(3):
        for kk, lo, hi, flat, w in F2.POOL[slot]:
            if kk == k: tot += hi * 1.5 * (F7.ilv_mult(IL) if flat else 1)
    if k == "crit": tot += max(a.get("u_crit", 0) for a in F2.ACC.values())
    if k == "cdr": tot += max(a.get("u_cdr", 0) for a in F2.ACC.values())
    if k == "move": tot += max(a.get("u_move", 0) for a in F2.ARMOR.values()) + max(a.get("u_move", 0) for a in F2.ACC.values())
    return tot
plate_def = sum(rh(F7.KINDS[p][k][0]["def_"] * MULT) for p, k in ((1, "판금 갑옷"), (2, "판금 투구"), (3, "판금 장갑"), (4, "판금 장화")))
def_opt = stat_max7("def_") * 10
atk7 = {}
for wk in F7.KIND_LIST[0]:
    rings = [("쇠 반지", 25), ("핏빛 반지", 20), ("송곳 반지", 20)]
    flat = (10 + rh(100 * MULT) / 10 + 2 * rh(25 * MULT) / 10 + stat_max7("atk")) * (1 + stat_max7("atk_pct"))
    atk7[wk] = flat * 10
crit_ring_flat = (10 + rh(100 * MULT) / 10 + 2 * rh(20 * MULT) / 10 + stat_max7("atk")) * (1 + stat_max7("atk_pct")) * 10
rows = [
    ("치명 확률", f"{(.05 + stat_max7('crit')) * 100:.1f}% (장검 {(.05 + stat_max7('crit', '장검')) * 100:.1f}, 대검 {(.05 + stat_max7('crit', '대검')) * 100:.1f})", "50%",
     f"{(.05 + stat_max2('crit')) * 100:.0f}%", .05 + stat_max7("crit") <= .5),
    ("치명 피해", f"{(1.5 + stat_max7('critdmg')) * 100:.0f}% (장검 {(1.5 + stat_max7('critdmg', '장검')) * 100:.0f}, 쌍검 {(1.5 + stat_max7('critdmg', '쌍검')) * 100:.0f})", "300%",
     f"{(1.5 + stat_max2('critdmg')) * 100:.0f}%", 1.5 + stat_max7("critdmg") <= 3.0 + 1e-9),
    ("공격 속도", f"+{stat_max7('aspd') * 100:.1f}% (초당 장검 {1.40 * (1 + stat_max7('aspd')):.2f} · 대검 {0.97 * (1 + stat_max7('aspd')):.2f} · 쌍검 {1.54 * (1 + stat_max7('aspd')):.2f}회)", "+30%",
     f"+{stat_max2('aspd') * 100:.0f}%", stat_max7("aspd") <= .3),
    ("이동 속도", f"+{stat_max7('move') * 100:.1f}% (판금 한 벌이면 {-.03 * 2 * 100:.0f}%)", "+30%", f"+{stat_max2('move') * 100:.0f}%", stat_max7("move") <= .3),
    ("스킬 재사용 감소", f"{stat_max7('cdr') * 100:.1f}%", "40%", f"{stat_max2('cdr') * 100:.0f}%", stat_max7("cdr") <= .4),
    ("체력 흡수", f"{stat_max7('leech') * 100:.1f}%", "3%", f"{stat_max2('leech') * 100:.1f}%", stat_max7("leech") <= .03),
    ("방어", f"판금 한 벌 {plate_def} + 방어+ 옵션 {def_opt:.0f} = {plate_def + def_opt:.0f}", "4,000", f"{rh(240 * MULT) + stat_max2('def') * 10:.0f}", plate_def + def_opt <= 4000),
    ("스킬 피해", f"{stat_max7('skill') * 100:.1f}%", "없음", f"{stat_max2('skill') * 100:.0f}%", True),
    ("보스 피해", f"{stat_max7('boss') * 100:.1f}%", "없음", f"{stat_max2('boss') * 100:.0f}%", True),
    ("공격력%", f"{stat_max7('atk_pct') * 100:.1f}%", "없음", f"{stat_max2('atk_pct') * 100:.1f}%", True),
    ("체력%", f"{stat_max7('hp_pct') * 100:.1f}%", "없음", f"{stat_max2('hp_pct') * 100:.0f}%", True),
    ("공격력+ 옵션", f"{stat_max7('atk') * 10:.0f}", "없음", f"{stat_max2('atk') * 10:.0f}", True),
    ("체력+ 옵션", f"{stat_max7('hp') * 10:.0f}", "없음", f"{stat_max2('hp') * 10:.0f}", True),
    ("초당 재생 / 처치 시 회복", f"{stat_max7('regen') * 10:.0f} / {stat_max7('onkill') * 10:.0f}", "없음", f"{stat_max2('regen') * 10:.0f} / {stat_max2('onkill') * 10:.0f}", True),
    ("공격력", f"{atk7['장검']:.0f} (치명 반지 둘이면 {crit_ring_flat:.0f})", "없음", f"{(10 + rh(100 * MULT) / 10 + rh(60 * MULT) / 10 + stat_max2('atk')) * (1 + stat_max2('atk_pct')) * 10:.0f}", True),
]
print("| 능력치 | 7칸 최대 | 상한 | 2차 최대 (fin_progress 표로 계산) | 판정 |")
print("|---|---|---|---|---|")
for nm, v7, cap, v2, ok in rows:
    print(f"| {nm} | {v7} | {cap} | {v2} | {'통과' if ok else '**넘음**'} |")
print(f"(문서 7장 옵션 ×0.8 뒤: 치명 27.4% / 치명 피해 288% / 공속 +19.8% / 이동 +19.6% / 재사용 20% / 흡수 1.5%(코드는 줄마다 반올림, 여기는 1.44%) / 방어 3,705 / 스킬 24.4% / 보스 19.4% / 공격력% 16.8% / 체력% 9.6% / 공격력+ 300 / 체력+ 1,620 / 36·240 / 공격력 2,976. ×0.8 전 어림: 치명 31% / 치명 피해 300% / 공속 +24% / 이동 +22% / 재사용 23% / 흡수 1.8% / 방어 3,774 / 스킬 28% / 보스 23% / 공격력% 21% / 체력% 12% / 공격력+ 375 / 체력+ 2,025 / 45·300 / 공격력 3,174)")

# 끝 빌드 공격 지수 A: 자리마다 종류·옵션 3줄을 A가 가장 커지게 고름(좌표 오르기). 2차도 같은 방식.
def endA7(weapon):
    parts = list(range(7)); choice = {}
    def opts_of(p): return [c for c in itertools.combinations(range(len(F7.POOL[p])), 3)]
    def T_of(ch):
        T = [0.0] * NS
        for (p, si), (kind, oc) in ch.items():
            base, uniq = F7.KINDS[p][kind]
            for k, x in base.items(): T[IX[k]] += rh(x * MULT) / 10
            for k, x in uniq.items(): T[IX[k]] += x
            for oi in oc:
                kk, lo, hi, flat, w = F7.POOL[p][oi]; T[IX[kk]] += hi * F7.OPTM[G] * F7.OPT_MULT * (F7.ilv_mult(IL) if flat else 1)
        return T
    for p, slots in enumerate(F7.PART_SLOTS):
        for si in range(len(slots)): choice[(p, si)] = ((weapon if p == 0 else F7.KIND_LIST[p][0]), opts_of(p)[0])
    for _ in range(6):
        changed = False
        for key in list(choice):
            p = key[0]; best = None; bv = -1
            for kind in ((weapon,) if p == 0 else F7.KIND_LIST[p]):
                for oc in opts_of(p):
                    ch = dict(choice); ch[key] = (kind, oc); v = abc(T_of(ch), F7.WEAPON[weapon])["A"]
                    if v > bv + 1e-12: best, bv = (kind, oc), v
            if best != choice[key]: choice[key] = best; changed = True
        if not changed: break
    T = T_of(choice); r = abc(T, F7.WEAPON[weapon])
    crit = min(.5, .05 + T[IX["crit"]]); cd = min(3.0, 1.5 + T[IX["critdmg"]])
    return r["A"], 1 + crit * (cd - 1)
def endA2(weapon):
    aps, wm = F2.WEAPON[weapon]; best = (0, 0)
    for acc in F2.ACC:
        for o0 in itertools.combinations(range(len(F2.POOL[0])), 3):
            for o2 in itertools.combinations(range(len(F2.POOL[2])), 3):
                st = dict(atk=10 + rh(100 * MULT) / 10 + rh(F2.ACC[acc].get("atk", 0) * 10 * MULT) / 10, atk_pct=0, crit=.05 + F2.ACC[acc].get("u_crit", 0), critdmg=1.5, aspd=0,
                          cdr=F2.ACC[acc].get("u_cdr", 0) + .04 * 1.5, skill=0, boss=0)   # 방어구 옵션 중 A 에 닿는 것은 재사용 감소뿐이라 그 줄을 늘 고름
                for slot, oc in ((0, o0), (2, o2)):
                    for oi in oc:
                        kk, lo, hi, flat, w = F2.POOL[slot][oi]
                        if kk in st: st[kk] += hi * 1.5 * (F7.ilv_mult(IL) if flat else 1)
                crit = min(.5, st["crit"]); cd = min(3.0, st["critdmg"]); APS = min(2.5, aps * (1 + st["aspd"])); cdr = min(.4, st["cdr"])
                A = st["atk"] * (1 + st["atk_pct"]) * (1 + crit * (cd - 1)) * (0.65 * APS * wm / 1.5 + 0.35 * (1 + st["skill"]) / (1 - cdr)) * (1 + 0.2 * st["boss"])
                if A > best[0]: best = (A, 1 + crit * (cd - 1))
    return best
print("\n| 끝 빌드 (A 가 가장 큰 종류·옵션) | 7칸 A | 기대 치명 배율 | 2차 3칸 A | 기대 치명 배율 | 7칸 ÷ 2차 |")
print("|---|---|---|---|---|---|")
for wk in F7.KIND_LIST[0]:
    a7, c7 = endA7(wk); a2, c2 = endA2(wk)
    print(f"| {wk} | {a7:.0f} | {c7:.3f} | {a2:.0f} | {c2:.3f} | {(a7 / a2 - 1) * 100:+.1f}% |")
print("(문서 7장 어림: 2차 대비 쌍검 +6~10%, 장검 +12~14%, 대검 +19%, 기대 치명 배율 1.27~1.46)")

# ---------------------------------------------------------------- 3) 3-2 시작 A 증가
print("\n## 3) 3-2 시작 A 증가 (시작 상태에서 무기만 바꿈, 공격 200)")
print("| 무기 | 치명 (고유 포함) | 기대 치명 배율 (2차 1.025) | 시작 A 증가: 치명만 | 계수 식 차이까지 (2차 휘두르기 × 타 배율 ÷ 1.5 → 콤보 계수 ÷ 1.49) | 단일 대상 계수 치명 뺌 → 치명 포함 (× 배율 ÷ 1.025) |")
print("|---|---|---|---|---|---|")
for wk in F7.KIND_LIST[0]:
    u = F7.KINDS[0][wk][1]; c = .05 + u.get("crit", 0); cd = 1.5 + u.get("critdmg", 0); cm = 1 + c * (cd - 1)
    coef = F7.WEAPON[wk]; aps, wm = F2.WEAPON[wk]
    a7 = 20 * cm * (0.65 * coef / 1.49 + 0.35); a2 = 20 * 1.025 * (0.65 * aps * wm / 1.5 + 0.35)
    print(f"| {wk} | {c * 100:.0f}% / {cd * 100:.0f}% | {cm:.4f} | {(cm / 1.025 - 1) * 100:+.1f}% | {(a7 / a2 - 1) * 100:+.1f}% | {coef:.2f} → {coef * cm / 1.025:.2f} |")
print("(문서 3-2: 장검 +1.7%, 대검 +2.4%, 쌍검 +0.2%; 3-1: 1.49 → 1.51, 1.45 → 1.49, 1.59 → 1.60)")

# ---------------------------------------------------------------- 4) 8-4 8자리 평균 강화석
print(f"\n## 4) 8-4 강화석: 부위별 시도 비용과 8자리를 모두 올리는 평균 강화석 (강화표 안5 + 연속 하락 2번 뒤 확정, 반복 {REPS:,})")
print("| 부위 (배율) | +1~+15 시도 비용 |")
print("|---|---|")
for p in range(7):
    print(f"| {F7.PARTS[p]} ({F7.COST_PM[p]}‰) | {', '.join(str(F7.enh_cost(p, t)) for t in range(1, 16))} |")
T = {t: (F7.S[t][1], F7.S[t][2], F7.S[t][3]) for t in range(1, 16)}
rng = random.Random(2026); TARG = (5, 8, 10, 12)
COSTS = [[F7.enh_cost(p, t) if t else 0 for t in range(16)] for p in range(7)] + [[F7.S[t][4] if t else 0 for t in range(16)]]   # 마지막 = 배율 없음
accn = {k: [0] * 16 for k in TARG}   # 목표에 처음 닿을 때까지 단계별 시도 횟수 합(비용은 끝에 부위별로 곱함)
for _ in range(REPS):
    lv = 0; best = 0; gauge = {}; streak = 0; n = [0] * 16
    while lv < 12:
        t = lv + 1; base, inc, ceil_ = T[t]; f = gauge.get(t, 0)
        p = 100 if ((ceil_ and f >= ceil_) or streak >= F7.STREAK) else min(100, base + inc * f + (F7.BONUS if t <= best else 0))
        n[t] += 1
        if rng.random() * 100 < p:
            lv = t; gauge[t] = 0; streak = 0
            if t > best:
                best = t
                if t in accn:
                    a_ = accn[t]
                    for i in range(1, t + 1): a_[i] += n[i]
        else:
            gauge[t] = f + 1
            if lv > F7.SAFE: lv -= 1; streak += 1
avg = {k: [sum(accn[k][t] * COSTS[i][t] for t in range(16)) / REPS for i in range(8)] for k in TARG}
print("\n| 목표 | 무기 | 갑옷 | 투구 | 장갑 | 장화 | 반지 (자리마다) | 목걸이 | 8자리 합 | 2차 3칸 (무기 비용 × 3) | 7칸 ÷ 2차 | 배율 없이 8자리 | 문서 어림 |")
print("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
DOC = {5: "약 59석 (47석, 1.25배)", 8: "약 161석 (144석, 1.11배)", 10: "약 389석 (369석, 1.05배), 배율 없으면 984석", 12: "약 852석 (833석, 1.02배)"}
for k in TARG:
    a = avg[k]; s8 = sum(a[p] * len(F7.PART_SLOTS[p]) for p in range(7)); s3 = a[0] * 3; nm = a[7] * 8
    print(f"| +{k} | {a[0]:.1f} | {a[1]:.1f} | {a[2]:.1f} | {a[3]:.1f} | {a[4]:.1f} | {a[5]:.1f} | {a[6]:.1f} | {s8:.0f} | {s3:.0f} | {s8 / s3:.2f}배 | {nm:.0f} ({nm / s3:.1f}배) | {DOC[k]} |")

# ---------------------------------------------------------------- 5) 시작 상태 S0
s7, s2 = F7.S0, F2.S0
print("\n## 5) 시작 상태 S0 (10배 전 단위, 진행 시뮬레이션의 능력 비율 R 기준점)")
print("| 항목 | 7칸 (장검 + 가죽 한 벌, 반지·목걸이 빈 자리) | 2차 (장검 + 가죽 갑옷) | 같은지 |")
print("|---|---|---|---|")
for k, nm in (("atk", "공격력"), ("hp", "체력"), ("def_", "방어"), ("S", "생존 지수 S"), ("M", "이동 지수 M"), ("A", "공격 지수 A")):
    a, b = s7[k], s2[k]
    print(f"| {nm} | {a:.4f} | {b:.4f} | {'같음' if abs(a - b) < 1e-9 else f'{(a / b - 1) * 100:+.2f}% (장검 고유 치명 7%/160%, 3-2)'} |")
print(f"| 시작 R (각자 S0 로 나눔) | {F7.ratio(s7, 1):.4f} | {F2.ratio(s2, 1):.4f} | {'같음' if abs(F7.ratio(s7, 1) - F2.ratio(s2, 1)) < 1e-9 else '다름'} |")
print(f"| 치명 확률 / 치명 피해 | {s7['crit'] * 100:.0f}% / {s7['cd'] * 100:.0f}% | {s2['crit'] * 100:.0f}% / {s2['cd'] * 100:.0f}% | 장검 고유 |")
print(f"| 이동 | {5.0 * s7['M']:.2f} | {5.0 * s2['M']:.2f} | {'같음' if abs(s7['M'] - s2['M']) < 1e-9 else '다름'} |")
