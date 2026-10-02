# -*- coding: utf-8 -*-
"""2차 기획 검산 (2차에서 새로 만든 스크립트. 1차에는 없음): fin_progress.py 를 여러 변형으로 동시에 돌려 핵심 지표를 한 표로 모은다.
실행: python fin_variants.py 묶음이름 [인원=2000]
  묶음: town(마을 체류·의뢰·상점 변형), curves(성장 곡선 비교), bounty(반복 의뢰 보상), seeds(난수 씨앗), gold(v3 골드 변형), all
  환경 변수를 비워 두면 fin_progress.py 기본값 = 2차 기획서 최종 채택안(체류 4/4/4/4.5·재도전 3, 의뢰 final, 반복 없음, 상점 7층, 골드)
  v3(2차 개정): 기본값에 골드(상점·마을 편의, 사용자 확정)가 켜졌다. 개정 전 2차 강화석 전용 상점은 GOLD=0 비교 행이다. gold 묶음은 골드 지표 열을 따로 보여 준다.
결과: fin_variants_<묶음>.txt"""
import sys, os, io, re, subprocess
from concurrent.futures import ThreadPoolExecutor
HERE = os.path.dirname(os.path.abspath(__file__))
GROUP = sys.argv[1] if len(sys.argv) > 1 else "town"
N = sys.argv[2] if len(sys.argv) > 2 else "2000"

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
    out = subprocess.run([sys.executable, os.path.join(HERE, "fin_progress.py"), N, hp, pol, "1.00", "0.95", at], env=e, capture_output=True).stdout.decode("utf-8")
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
    io.open(os.path.join(HERE, f"fin_variants_{GROUP}.txt"), "w", encoding="utf-8").write(txt + "\n")
    sys.stdout.buffer.write((txt + "\n").encode("utf-8"))


if __name__ == "__main__":
    main()
