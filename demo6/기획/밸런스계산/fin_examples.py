# -*- coding: utf-8 -*-
"""최종 기획 검산 (임시): 10배 정수 단위의 예시 값 (장비 비교 예시, 전설 +15 무기, 계승·분해 예시, 몬스터 표 반올림).
실행: python fin_examples.py"""
import sys, io, math
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
def rnd(x): return int(math.floor(x + 0.5))   # 0.5 는 올림
CUM = [0, 5, 10, 15, 20, 25, 32, 39, 49, 59, 69, 82, 95, 113, 131, 149]
GM = [1.00, 1.25, 1.55, 1.90, 2.30]; OPTM = [0, 1.00, 1.15, 1.30, 1.50]
def ilv(i): return 1 + 0.15 * (i - 1)
WEAPON = {"장검": (1.5, 1, 1.00), "대검": (1.1, 1, 1.32), "쌍검": (1.45, 2, 0.55)}
def scale(g, il, lv, roll=1.0): return ilv(il) * GM[g] * roll * (1 + CUM[lv] / 100)
def stats(w, armor, acc):
    atk = 100 + rnd(100 * scale(*w["s"])) + acc["atk"]; atk_pct = sum(v for k, v in w["opts"] + acc["opts"] if k == "atk_pct")
    atk += sum(v for k, v in w["opts"] + acc["opts"] if k == "atk")
    crit = 0.05 + sum(v for k, v in w["opts"] + acc["opts"] if k == "crit") + acc.get("u_crit", 0)
    cd = 1.5 + sum(v for k, v in w["opts"] + acc["opts"] if k == "critdmg")
    aspd = sum(v for k, v in w["opts"] + acc["opts"] if k == "aspd")
    aps, hits, hm = WEAPON[w["kind"]]
    APS = min(2.5, aps * (1 + aspd))
    A = atk * (1 + atk_pct) * (1 + min(.5, crit) * (min(3, cd) - 1)) * (0.65 * APS * hits * hm / 1.5 + 0.35)
    hp = 2000 + armor["hp"]; df = armor["def"]
    S = hp * (1 + min(df, 4000) / 1000)
    return dict(atk=atk * (1 + atk_pct), A=A, S=S, M=1.0, watk=rnd(100 * scale(*w["s"])))
def comp(a, b): return 0.6 * (b["A"] / a["A"] - 1) + 0.3 * (b["S"] / a["S"] - 1)
armor = dict(hp=rnd(600 * scale(2, 7, 8)) + rnd(180 * 1.15 * ilv(7)), **{"def": rnd(160 * scale(2, 7, 8)) + rnd(45 * 1.15 * ilv(7))})
acc = dict(atk=rnd(60 * scale(2, 7, 5)), u_crit=0.03, opts=[("crit", .03 * 1.15), ("atk_pct", .045 * 1.15)])
cur = dict(kind="장검", s=(2, 7, 8), opts=[("atk_pct", .06 * 1.15), ("crit", .03 * 1.15)])
s0 = stats(cur, armor, acc)
print(f"장착: 희귀 장검 iLv7 +8 무기 공격력 {s0['watk']}, 캐릭터 공격력 {s0['atk']:.0f}, A {s0['A']:.0f}")
for lv in (0, 7):
    cand = dict(kind="대검", s=(3, 8, lv), opts=[("atk", rnd(30 * 1.3 * ilv(8))), ("critdmg", .15 * 1.3), ("aspd", .06 * 1.3)])
    s1 = stats(cand, armor, acc)
    print(f"후보 영웅 대검 iLv8 +{lv}: 무기 공격력 {s1['watk']}, 캐릭터 공격력 {s0['atk']:.0f}→{s1['atk']:.0f}, A {s0['A']:.0f}→{s1['A']:.0f}, 종합 {comp(s0, s1)*100:+.1f}%")
print("전설 +15 무기 iLv10 굴림 100%:", 100 * ilv(10) * 2.30 * (1 + CUM[15] / 100), "→", rnd(100 * ilv(10) * 2.30 * (1 + CUM[15] / 100)))
print("무기 공격력 일반/전설 +0 iLv1~11:", [(i, rnd(100 * ilv(i)), rnd(100 * ilv(i) * 2.3)) for i in range(1, 12)])
print("판금 방어 일반 +0 iLv1~11:", [rnd(240 * ilv(i)) for i in range(1, 12)])
print("판금 전설 iLv11 +15 방어:", rnd(240 * ilv(11) * 2.3 * 2.49), "+ 방어 옵션 최대(전설 iLv11)", rnd(60 * 1.5 * ilv(11)))
# 몬스터 표 (0.5 올림)
HPG, ATG, RATG = 1.27, 1.24, 1.25
print("\n| 층 | 체력 배율 | 공격 배율 | 굴쥐 | 멧돼지 | 궁수 |")
for f in range(1, 11):
    h = HPG ** (f - 1); a = ATG ** (f - 1)
    print(f"| {f} | x{h:.2f} | x{a:.2f} | {rnd(160 * RATG ** (f - 1))} / {rnd(60 * a)} | {rnd(900 * h)} / {rnd(300 * a)} (머리치기 {rnd(180 * a)}) | {rnd(400 * h)} / {rnd(100 * a)} |")
print("보스 5층:", rnd(18000 * 0.55 * HPG ** 4), rnd(300 * 0.85 * ATG ** 4), " 10층:", rnd(18000 * HPG ** 9), rnd(300 * ATG ** 9))
print("정예: 4층 멧돼지", rnd(900 * HPG ** 3) * 4, "7층 궁수", rnd(400 * HPG ** 6) * 4, "9층 멧돼지", rnd(900 * HPG ** 8) * 4, "9층 궁수", rnd(400 * HPG ** 8) * 4)
print("정예 공격: 4층 멧돼지 돌진", rnd(rnd(300 * ATG ** 3) * 1.3), "7층 궁수", rnd(rnd(100 * ATG ** 6) * 1.3), "9층 멧돼지", rnd(rnd(300 * ATG ** 8) * 1.3), "9층 궁수", rnd(rnd(100 * ATG ** 8) * 1.3))
# 위험도 (M7) 배율: 2개 층만큼
print("위험도 k 몬스터 배율: 체력", [round(HPG ** (2 * k), 2) for k in (1, 2, 3)], "공격", [round(ATG ** (2 * k), 2) for k in (1, 2, 3)])
print("iLv 12~14 배율:", [ilv(i) for i in (12, 13, 14)])
# 쌍검 공격 속도 상한 여유
print("쌍검 상한까지 공격 속도 여유 %:", round((2.5 / 1.45 - 1) * 100), " 옵션 최대(무기 12% + 장신구 9%) 시 초당", round(1.45 * 1.21, 2))
print("단일 대상 초당 계수: 장검", 1.5 * 1.0, "대검", round(1.1 * 1.32, 3), "쌍검", round(1.45 * 2 * 0.55, 3))
