# -*- coding: utf-8 -*-
"""2차 기획 진행 시뮬레이션. 1차 fin_progress.py 를 복사해 고쳤다. 고친 곳에는 '# v2:' 주석을 달았다.
(v3 = 2차 개정: 기본값(환경 변수 없음)은 사용자 확정 '상점 화폐 골드'(2차 기획서 14장 #18)를 반영한 최종 채택안이다.
 GOLD=0 은 '2차 강화석 전용 상점 비교'로, 개정 전 2차 최종 결과와 같은 값이 나온다(fin_*_stone_c/g.txt).)
1차 규칙 (그대로 둠)
 - 강화표 안5(+13~15) + 연속 하락 2번 뒤 확정, +7까지 안전, +8 이상 실패 시 1단계 하락, 계승 시 최고 기록 = min(원본 최고 기록, 새 상한)
 - 무기: 대검 1회 132%, 쌍검 초당 1.45회 x 2타(55%) / 방어 피해 감소 상한 80%(방어 400, 10배 전 기준)
 - 귀환 표지, 보스 앞 표지, 1층 상자 첫 개봉 희귀 이상(무기), 3층 상자 첫 개봉 영웅 이상
 - 사망 손실은 직접 강화석에만 50% / 6층 이상 일반 장비 자동 분해 / 보스 승률 로지스틱
v2 변경 (마을·주민 이야기 설계 + 시설·재화 설계)
 - 던전 안 계승 없음. 원정 중에는 '+0 그대로 끼면' 종합 +3% 이상일 때만 G로 낀다. 벗은 장비는 가방으로 간다(분해 안 함).
 - 계승은 마을 대장간에서만. 부위마다 '마을에서 계승하면' 값이 가장 큰 짝(원본 → 대상) 1개를 고른다(대장간 추천 짝).
   원본·대상은 장착·가방 어디서나 고른다. 강화석이 모자라 못 한 계승 후보는 가방(창고)에 남겨 다음 방문에 다시 본다.
 - INH=best(기본): 대장간 부위별 최선 짝 추천. INH=naive 는 비교용(추천 없이 '벗어 둔 원본 → 지금 장비', '더 높은 등급 +0 → 지금 장비 단계 받기'만).
 - 마을 체류: TOWN=final(기본, 2차 기획서) 첫 3번 4분, 4번째부터 4분 30초, 보스 앞 표지 재도전 직전(4번째부터) 3분.
   TOWN=sched 는 비교용 시설·재화 설계 일정(첫 방문 6분, 2·3번째 5분, 4번째부터 4분, 재도전 직전 3분). TOWN=3.5 처럼 고정값도 됨.
 - 의뢰: QUEST=final(기본, 2차 기획서 9-4 의뢰 보상표 12개) / small(축소안: 주민 5명, final 에서 상인 의뢰 2개를 뺀 10개)
         / v2(검토 반영 전 통합표, 비교용) / town(마을 설계 크기 그대로, 장비 보장 포함은 GUAR=1) / fac(시설 설계 Q01~Q10) / 0(없음)
   반복 의뢰(주막 게시판): BOUNTY=0(없음) / C(작음=2+기준층) / 숫자(고정 강화석). 기본값은 QUEST 에 따름(final·small 은 없음).
   BOUNTY_MAX=6: 10층 첫 처치 전 반복 의뢰 지급 횟수 상한(v2 통합안 82석 + 2석 x 6 = 94석 <= 95석). 0 이면 상한 없음.
 - 상점: 강화석으로 진열 3칸(고급, 5층 보스 뒤 희귀 30%), iLv = 최고 도달 층 - 1, 가격 6+2iLv / 18+4iLv. 상점·의뢰 상자는 따로 굴림.
   열리는 때 SHOP_UNLOCK: final·small 은 7층 도달 뒤 귀환(maxf7), v2·town 은 4층(maxf4), fac 은 3층 도달(reach3).
   되사기 칸(실수 분해 되돌리기)은 시뮬레이션이 실수를 하지 않으므로 넣지 않았다.
   (v2 당시에는 골드 없음. v3에서 GOLD=1 이 기본이 되었고, 예전 '시설 설계' 골드 비교 경로는 v3 골드 설계로 바꿔 더는 재현하지 않는다.)
 - SEED=숫자: 난수 시드 바꿔 보기(기본 20261002 = 1차와 같음).
 - 비교용: V1=1 이면 1차 규칙 그대로(던전 안 계승, 정비 3분, 의뢰·상점 없음). 1차 fin_final 과 같은 값이 나온다.
v3 변경 (2차 개정: 상점 화폐 골드 사용자 확정, 14장 #18. 고친 곳에는 '# v3:' 주석) — 기본값(환경 변수 없음)이 v3 최종 채택안이다
 - GOLD=1(기본): 골드는 상점과 마을 편의에만 쓴다. 강화·계승·분해 표는 그대로 강화석만.
   GOLD=0 이면 2차 강화석 전용 상점 비교(개정 전 2차 fin_final = 지금 fin_final_stone 과 같은 값), V1=1 이면 1차 규칙(1차 fin_final 과 같은 값).
 - 골드 출처(기대값으로 더함, 드랍 난수를 쓰지 않음): 골드 무더기 1개 = GPILE(3) x 아이템 레벨 배율.
   굴쥐 25% / 궁수 50% / 멧돼지 75% 확률로 무더기 1개, 정예 5개 확정. 계단방 상자 8 + 4 x 층, 보스 5층 60 / 10층 150.
   의뢰 골드 = 의뢰 강화석 x QGOLD(기본 10. 명패 의뢰는 강화석 0이라 골드도 0). (예전 2차 골드 비교 경로 값의 약 0.75배)
   사망·원정 중 종료: 이번 원정 골드 50%(내림)를 잃음(강화석과 같은 규칙). 의뢰 골드는 지갑으로 바로 가서 잃지 않음.
 - 골드 쓰는 곳: 진열(고급 60+20iLv, 희귀 180+40iLv = 2차 강화석 가격 x 10), 진열 새로고침(30+10iLv, 같은 방문 두 번째는 2배, 방문당 REROLL=2회),
   창고 칸 늘리기(5층 보스 뒤 20칸 무료 → 25칸 STORE1=400 / 30칸 STORE2=800), 가방 칸 늘리기(상점, 45칸 BAG1=600 / 50칸 BAG2=1200),
   되사기 칸 늘리기(상점, 6 → 12칸 BUYBACK_UP=300).
   되사기(분해로 받은 강화석을 그대로 돌려주고 수수료 골드 = 분해 강화석 x 10)는 시뮬레이션이 실수 분해를 하지 않아 넣지 않음.
 - 골드 쓰는 습관 GSPEND: base(기본) = 진열이 +5% 이상이면 삼 → 칸 늘리기는 살 수 있으면 삼(싼 것부터) →
   첫 새로고침은 지갑이 새로고침 값의 CASUAL(3)배 이상이면 구경 삼아 1번, 두 번째는 '희귀 진열이 나오면 +5% 이상 오를 부위'가 있고
   골드가 '새로고침 + 희귀 진열 값' 이상일 때만. 비교용: reserve = 첫 새로고침도 다음 칸 늘리기 값을 남길 때만 /
   smart = 새로고침은 오를 부위가 있을 때만 / always = 살 수 있으면 늘 2번 / save = 칸 늘리기를 사지 않음 / none = 진열 구매만.
 - 비교용 SELL: 0(기본, 장비 팔기 없음) / 3 = 일반 +0만 / 1 = 일반·고급 +0 / 2 = 일반~영웅 +0을 분해 대신 팔기(팔 등급의 6층 자동 분해는 끔).
   판매가 = 분해 강화석 기본값 x SELL_RATE(기본 10) 골드. GSCALE = 골드 출처 배율(민감도). SHOP_UNLOCK 에 first5, visitN 추가.
실행: python fin_progress.py [인원=1500] [체력성장=1.25] [정책 c|g] [10층 R50=1.05] [5층 R50=0.95] [공격성장=1.20]
  기획 값: python fin_progress.py 2000 1.27 c 1.00 0.95 1.24
  강화석 전용 상점 비교: GOLD=0 python fin_progress.py 2000 1.27 c 1.00 0.95 1.24
"""
import random, statistics, math, sys, io, os
if not isinstance(sys.stdout, io.TextIOWrapper) or (sys.stdout.encoding or "").lower() != "utf-8":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
P = int(sys.argv[1]) if len(sys.argv) > 1 else 1500
HP_G = float(sys.argv[2]) if len(sys.argv) > 2 else 1.25
POL = sys.argv[3] if len(sys.argv) > 3 else "c"
R50_10 = float(sys.argv[4]) if len(sys.argv) > 4 else 1.05
R50_5 = float(sys.argv[5]) if len(sys.argv) > 5 else 0.95
ATK_G = float(sys.argv[6]) if len(sys.argv) > 6 else 1.20
RAT_G = float(os.environ.get("RAT_G", "1.25"))   # v2: 굴쥐 체력 성장(표 출력용, 1차와 같은 1.25)
STEPS = [(1,100,0,0,2,5),(2,95,0,1,2,5),(3,90,0,1,3,5),(4,85,0,1,3,5),(5,80,0,1,4,5),(6,70,10,2,5,7),(7,60,10,2,6,7),
         (8,55,10,3,10,10),(9,50,10,3,12,10),(10,45,10,3,14,10),(11,40,10,3,18,13),(12,35,10,3,22,13),
         (13,40,15,2,46,18),(14,35,15,2,59,18),(15,30,15,2,72,18)]
S = {s[0]: s for s in STEPS}; SAFE = 7; BONUS = 20; STREAK = 2
CUM = {0: 0}
for t in range(1, 16): CUM[t] = CUM[t-1] + S[t][5]
REFUND = [0,0,1,1,2,3,4,6,10,16,25,37,56,82,121,178]
INHERIT = [0,1,1,2,3,4,5,7,10,16,25,38,56,82,122]
EA = {1:2.0,2:2.1,3:3.3,4:3.4,5:4.8,6:6.8,7:9.1,8:16.5,9:30.9,10:44.3,11:63.2,12:91.2,13:130.9,14:196.9,15:285.7}
GR = ["일반","고급","희귀","영웅","전설"]; GMULT = [1.00,1.25,1.55,1.90,2.30]; OPTN = [0,1,2,3,3]; OPTM = [0,1.00,1.15,1.30,1.50]
CAP = [5,7,10,12,15]; DIS = [1,2,6,15,40]
def ilv_mult(i): return 1 + 0.15 * (i - 1)
WEAPON = {"장검": (1.5, 1.00), "대검": (1.1, 1.32), "쌍검": (1.45, 1.10)}
ARMOR = {"가죽 갑옷": dict(def_=12, hp=40, u_move=0.06), "사슬 갑옷": dict(def_=16, hp=60), "판금 갑옷": dict(def_=24, hp=40, u_move=-0.06)}
ACC = {"반지": dict(atk=6, u_crit=0.03), "목걸이": dict(hp=40, u_cdr=0.05), "부적": dict(atk=3, hp=20, u_move=0.04)}
POOL = {
    0: [("atk",2,4,1,10),("atk_pct",.04,.08,0,10),("crit",.02,.04,0,8),("critdmg",.10,.20,0,8),("aspd",.04,.08,0,8),("boss",.06,.12,0,5),("leech",.004,.008,0,4),("onkill",2,4,1,5)],
    1: [("hp",12,24,1,10),("hp_pct",.04,.08,0,8),("def",3,6,1,10),("move",.03,.06,0,6),("regen",.6,1.2,1,6),("onkill",2,4,1,5),("cdr",.02,.04,0,4)],
    2: [("atk",2,4,1,8),("atk_pct",.03,.06,0,8),("hp",10,20,1,8),("crit",.02,.04,0,8),("critdmg",.10,.20,0,8),("aspd",.03,.06,0,6),("move",.03,.06,0,6),("cdr",.03,.06,0,6),("skill",.06,.12,0,6)]}
GT = {1:[82,16,2,0,0],2:[74,21,5,0,0],3:[66,25,8,1,0],4:[58,28,12,2,0],5:[50,30,15.5,4,.5],6:[44,31,18,6,1],7:[38,32,21,7.5,1.5],8:[32,33,24,9,2],9:[27,33,26,11.5,2.5],10:[22,33,28,14,3]}
BOSS_T = {5: (3, [0,45,40,13,2]), 10: (4, [0,20,45,30,5])}; LEG_PITY = 80
FLOORS = {1:(30,2,0,0),2:(28,8,0,0),3:(25,3,8,0),4:(28,8,8,1),5:(14,4,6,0),6:(28,8,12,0),7:(28,12,12,1),8:(32,12,12,0),9:(32,12,12,2),10:(16,6,6,0)}
FLOOR_MIN = {1:3,2:3,3:3,4:3,5:3.5,6:4.5,7:4.5,8:4.5,9:4.5,10:4.5}
BOSS_MIN = {5: 0.5, 10: 1.2}; TOWN_MIN = 3
DROP = dict(rat=0.06, boar=0.18, archer=0.12); STONE = dict(rat=0.05, boar=0.15, archer=0.10, elite=3.0)
def chest_stone(f): return 2 + f
BOSS_STONE = {5: 15, 10: 40}

# ---------------------------------------------------------------- v2: 변형 스위치 (기본값 = v2 채택안)
V1 = os.environ.get("V1") == "1"                      # v2: 1차 규칙 그대로 돌리기(비교용)
TOWN = os.environ.get("TOWN", "final")                 # v2: 마을 체류 시간 가정 (2차 기획서 최종: final)
QUEST = os.environ.get("QUEST", "final")               # v2: 의뢰 보상표 (2차 기획서 최종: final)
SHOP = os.environ.get("SHOP", "1") == "1"              # v2: 상점
SHOP_RARE = float(os.environ.get("SHOP_RARE", "0.30"))  # v2: 5층 보스 뒤 희귀 비율
SHOP_ILV = int(os.environ.get("SHOP_ILV", "-1"))       # v2: 진열 iLv = 최고 도달 층 + 이 값
GOLD = os.environ.get("GOLD", "1") == "1"              # v3: 골드 기본 켬(2차 개정 사용자 확정, 14장 #18). GOLD=0 = 2차 강화석 전용 상점 비교
GUAR = os.environ.get("GUAR", "0") == "1"              # v2: 마을 설계의 장비 보장(정예 의뢰 영웅+, 측량 의뢰 희귀+)
QSCALE = float(os.environ.get("QSCALE", "1"))          # v2: 의뢰 강화석 배율(민감도용)
INH = os.environ.get("INH", "best")                    # v2: 마을 계승 방식. best = 대장간 부위별 최선 짝 추천, naive = 비교용 단순 방식
QGOLD = float(os.environ.get("QGOLD", "10"))           # v3: 의뢰 골드 = 의뢰 강화석 x 이 값
GSPEND = os.environ.get("GSPEND", "base")              # v3: 골드 쓰는 습관 base / always / save / none
REROLL = int(os.environ.get("REROLL", "2"))            # v3: 방문당 진열 새로고침 상한
CASUAL = float(os.environ.get("CASUAL", "3"))          # v3: base 습관에서 '구경 새로고침'을 하는 지갑 하한(새로고침 값의 배수)
RR_BASE = float(os.environ.get("RR_BASE", "30")); RR_ILV = float(os.environ.get("RR_ILV", "10"))   # v3: 새로고침 값 = RR_BASE + RR_ILV x iLv
STORE_STEPS = [(25, float(os.environ.get("STORE1", "400"))), (30, float(os.environ.get("STORE2", "800")))]   # v3: 창고 칸 늘리기(칸, 골드)
BAG_STEPS = [(45, float(os.environ.get("BAG1", "600"))), (50, float(os.environ.get("BAG2", "1200")))]         # v3: 가방 칸 늘리기(칸, 골드)
BUYBACK_UP = float(os.environ.get("BUYBACK_UP", "300"))   # v3: 되사기 칸 6 → 12 (0 이면 없음)
SELL = os.environ.get("SELL", "0")                     # v3: 비교용 장비 팔기 0 없음 / 1 일반·고급 / 2 일반~영웅 / 3 일반만 (+0만)
SELL_RATE = float(os.environ.get("SELL_RATE", "10"))   # v3: 판매가 = 분해 강화석 기본값 x 이 값
GSCALE = float(os.environ.get("GSCALE", "1"))          # v3: 골드 출처 배율(민감도용)
if V1:
    TOWN = "3"; QUEST = "0"; SHOP = False; GOLD = False
if not GOLD:
    SELL = "0"   # v3: 골드가 없으면 팔기도 없음


def sell_ok(g):   # v3: 비교용 장비 팔기 대상 등급
    return (SELL == "1" and g <= 1) or (SELL == "2" and g <= 3) or (SELL == "3" and g == 0)


def town_minutes(visit, retry):
    """v2: 마을 체류 시간. final = 2차 기획서 방문 종류별 예산(첫 3번 4분, 4번째부터 4분 30초, 재도전 직전 3분).
    sched = 시설·재화 설계 6장 일정(비교용)."""
    if TOWN == "final":
        if visit <= 3: return 4.0
        return 3.0 if retry else 4.5
    if TOWN == "sched":
        if visit == 1: return 6.0
        if visit <= 3: return 5.0
        return 3.0 if retry else 4.0
    return float(TOWN)


def C(f): return 2 + f   # v2: 의뢰 강화석 기준값 = 계단방 상자 강화석(2 + 기준 층)
# v2: 의뢰 = (id, 받는 조건, 값, 완료 조건, 값, 강화석, 상자 층(0 없음), 보장 최소 등급, 보장 iLv)
#  받는 조건: start 처음부터 / visit n번째 귀환부터 / maxf 그 층 도달 뒤 귀환 / first5 5층 보스 첫 처치 뒤 귀환 / tags 명패를 들고 귀환
#            bossloss10 10층 보스에게 (첫 처치 전에) 처음 진 뒤 귀환
#  완료 조건: rats 굴쥐 누적 / arch3 받은 뒤 3층 이상 궁수(검풍 몫 50%라 2배 수) / boars 받은 뒤 멧돼지(기절 몫 25%라 4배 수)
#            clear 그 층 정리(받기 전 기록 인정) / clear_after 받은 뒤 그 층 정리 / boss 보스 첫 처치 / elite79 7·9층 정예 누적
#            elite (층, 수) / reach 그 층 도달 / tags 명패 누적 / enh 마을: 장착 최고 단계 / inh 마을: 첫 계승 / town1 마을: 첫 강화
#            elite7_9 7층 정예 1 + 9층 정예 2(층별로 따로 셈) / boss10try 받은 뒤 10층 보스 도전 횟수(마지막 돌진 기절은 도전마다 일어난다고 어림)
_g_el = (3, 9) if GUAR else (0, 0); _g_sv = (2, 8) if GUAR else (0, 0)
QTAB = {
    # 마을 설계 5-2 크기(작음 x1 / 중간 x2 / 큼 x3 + 의뢰 상자)를 시설 설계 공식 C x 배수로 그대로 바꾼 것
    "town": [("q.gate_rats", "start", 0, "rats", 30, C(1), 0, 0, 0),
             ("q.smith_enhance3", "visit", 1, "enh", 3, C(1), 0, 0, 0),
             ("q.trainer_windblade", "visit", 1, "arch3", 16, C(3), 0, 0, 0),
             ("q.kid_chalk", "visit", 1, "clear", 6, C(6), 0, 0, 0),
             ("q.gate_ogre5", "maxf", 4, "boss", 5, 3 * C(5), 5, 0, 0),
             ("q.merchant_horns", "maxf", 4, "boars", 24, 2 * C(4), 0, 0, 0),
             ("q.smith_inherit", "visit", 1, "inh", 1, C(3), 0, 0, 0),
             ("q.trainer_elites", "first5", 0, "elite79", 3, 2 * C(9), 0, _g_el[0], _g_el[1]),
             ("q.lamp_tags3", "tags", 1, "tags", 3, C(3), 0, 0, 0),
             ("q.lamp_tags5", "tags", 1, "tags", 5, 2 * C(5), 0, 0, 0),
             ("q.merchant_survey", "maxf", 7, "clear_after", 8, 2 * C(8), 0, _g_sv[0], _g_sv[1]),
             ("q.lamp_tags10", "tags", 1, "tags", 10, 3 * C(10), 10, 0, 0),
             ("q.smith_ogre10", "maxf", 9, "boss", 10, 3 * C(10), 10, 0, 0)],
    # 시설·재화 설계 5장 의뢰 칸 Q01~Q10 그대로
    "fac": [("Q01", "visit", 1, "town1", 1, 3, 0, 0, 0), ("Q02", "start", 0, "reach", 2, 4, 0, 0, 0),
            ("Q03", "start", 0, "reach", 3, 10, 0, 0, 0), ("Q04", "start", 0, "elite", (4, 1), 6, 0, 0, 0),
            ("Q05", "start", 0, "boss", 5, 21, 5, 0, 0), ("Q06", "start", 0, "reach", 6, 8, 0, 0, 0),
            ("Q07", "start", 0, "elite", (7, 1), 18, 0, 0, 0), ("Q08", "start", 0, "reach", 8, 10, 0, 0, 0),
            ("Q09", "start", 0, "elite", (9, 2), 11, 0, 0, 0), ("Q10", "start", 0, "boss", 10, 36, 10, 0, 0)],
    # v2 통합 보상표: 마을 설계의 의뢰 12개(조건·시점) + 시설 설계의 공식·상한(10층 첫 처치 전 95석 이하, 장비는 '큼'에만 의뢰 상자)
    # 2차 기획서 최종(9-4 의뢰 보상표): v2 통합표에 플레이·정합성 검토를 반영. 반복 의뢰 없음.
    #  - 굴쥐 의뢰는 '회오리 베기로 굴쥐 15마리'(회오리 몫 약 50%라 굴쥐 처치 30마리로 어림)
    #  - 검풍 의뢰는 두 번째 귀환(훈련장 열림)부터, 분필 의뢰는 5층 보스 첫 처치 뒤부터, 상인 의뢰는 7층·8층 도달 뒤 귀환부터
    #  - 정예 의뢰는 7층 1 + 9층 2(층별), 18석 / 측량은 9층(11석) / 10층 보스 첫 패배 뒤 '마지막 돌진을 기둥에 박기' 12석
    #  - 10층 첫 처치 전 고정분 최대 79 + 12 = 91석 <= 95석
    "final": [("q.gate_rats", "start", 0, "rats", 30, 3, 0, 0, 0),
              ("q.smith_enhance3", "visit", 1, "enh", 3, 3, 0, 0, 0),
              ("q.smith_inherit", "visit", 1, "inh", 1, 4, 0, 0, 0),
              ("q.trainer_windblade", "visit", 2, "arch3", 16, 5, 0, 0, 0),
              ("q.gate_ogre5", "maxf", 4, "boss", 5, 21, 5, 0, 0),
              ("q.kid_chalk", "first5", 0, "clear", 6, 8, 0, 0, 0),
              ("q.merchant_horns", "maxf", 7, "boars", 24, 6, 0, 0, 0),
              ("q.trainer_elites", "first5", 0, "elite7_9", 0, 18, 0, _g_el[0], _g_el[1]),
              ("q.lamp_tags3", "tags", 1, "tags", 3, 0, 0, 0, 0),
              ("q.lamp_tags5", "tags", 1, "tags", 5, 0, 0, 0, 0),
              ("q.merchant_survey", "maxf", 8, "clear_after", 9, 11, 0, _g_sv[0], _g_sv[1]),
              ("q.lamp_tags10", "tags", 1, "tags", 10, 0, 0, 0, 0),
              ("q.trainer_ogre_stun", "bossloss10", 0, "boss10try", 1, 12, 0, 0, 0),
              ("q.smith_ogre10", "maxf", 9, "boss", 10, 36, 10, 0, 0)],
    "v2": [("q.gate_rats", "start", 0, "rats", 30, 3, 0, 0, 0),
           ("q.smith_enhance3", "visit", 1, "enh", 3, 3, 0, 0, 0),
           ("q.trainer_windblade", "visit", 1, "arch3", 16, 5, 0, 0, 0),
           ("q.kid_chalk", "visit", 1, "clear", 6, 8, 0, 0, 0),
           ("q.gate_ogre5", "maxf", 4, "boss", 5, 21, 5, 0, 0),
           ("q.merchant_horns", "maxf", 4, "boars", 24, 6, 0, 0, 0),
           ("q.smith_inherit", "visit", 1, "inh", 1, 4, 0, 0, 0),
           ("q.trainer_elites", "first5", 0, "elite79", 3, 22, 0, _g_el[0], _g_el[1]),
           ("q.lamp_tags3", "tags", 1, "tags", 3, 0, 0, 0, 0),
           ("q.lamp_tags5", "tags", 1, "tags", 5, 0, 0, 0, 0),
           ("q.merchant_survey", "maxf", 7, "clear_after", 8, 10, 0, _g_sv[0], _g_sv[1]),
           ("q.lamp_tags10", "tags", 1, "tags", 10, 0, 0, 0, 0),
           ("q.smith_ogre10", "maxf", 9, "boss", 10, 36, 10, 0, 0)],
}
QTAB["small"] = [q for q in QTAB["final"] if not q[0].startswith("q.merchant_")]   # 축소안: 주민 5명(상인·등불지기 없음), 의뢰 10개
if os.environ.get("V2VALS"):   # v2: 통합 보상표 강화석을 바꿔 보기 (13개, 표 순서)
    _vals = [float(x) for x in os.environ["V2VALS"].split(",")]
    QTAB["v2"] = [q[:5] + (v,) + q[6:] for q, v in zip(QTAB["v2"], _vals)]
_BOUNTY_DEFAULT = {"town": "C", "v2": "2", "fac": "0", "0": "0", "final": "0", "small": "0"}   # v2: 통합안 반복 의뢰는 고정 2석(상한 95석 안에 90% 사람이 들게)
BOUNTY = os.environ.get("BOUNTY", _BOUNTY_DEFAULT.get(QUEST, "0"))   # v2: 반복 의뢰 보상
if V1: BOUNTY = "0"
BOUNTY_KILLS = 60
BOUNTY_MAX = int(os.environ.get("BOUNTY_MAX", "6" if QUEST == "v2" else "0"))   # v2: 10층 첫 처치 전 반복 의뢰 지급 상한(회)
SHOP_UNLOCK = os.environ.get("SHOP_UNLOCK", "reach3" if QUEST == "fac" else ("maxf7" if QUEST in ("final", "small", "0") else "maxf4"))   # v2: 상점 열리는 때
GPILE = float(os.environ.get("GPILE", "3"))   # v3: 골드 무더기 1개 = GPILE x 아이템 레벨 배율. 굴쥐 25% / 궁수 50% / 멧돼지 75% 확률로 1개, 정예 5개 확정
GOLD_KILL = dict(rat=0.25 * GPILE * GSCALE, archer=0.50 * GPILE * GSCALE, boar=0.75 * GPILE * GSCALE); GOLD_ELITE = 5 * GPILE * GSCALE   # v3: 기대값
GOLD_BOSS = {5: float(os.environ.get("GBOSS5", "60")) * GSCALE, 10: float(os.environ.get("GBOSS10", "150")) * GSCALE}   # v3
GCHEST = (float(os.environ.get("GCHEST0", "8")), float(os.environ.get("GCHEST1", "4")))   # v3: 계단방 상자 골드 = 8 + 4 x 층
def gold_chest(f): return (GCHEST[0] + GCHEST[1] * f) * GSCALE   # v3
TAG_FLOORS = (1, 2, 3, 4, 6, 7, 8, 9)
def reroll_price(il, k): return (RR_BASE + RR_ILV * il) * (2 ** (k - 1))   # v3: 같은 방문 k번째 새로고침 값
def shop_price(g, il): return ((6 + 2 * il) if g == 1 else (18 + 4 * il)) * (10 if GOLD else 1)   # v3: 진열 가격(골드는 10배)


def bounty_reward(base_floor):
    if BOUNTY == "0": return 0.0
    if BOUNTY == "C": return float(C(max(1, base_floor)))
    return float(BOUNTY)


class Item:
    __slots__ = ("slot","kind","g","ilv","roll","opts","lv","best","gauge","streak")
    def __init__(s, slot, kind, g, ilv, roll, opts):
        s.slot, s.kind, s.g, s.ilv, s.roll, s.opts = slot, kind, g, ilv, roll, opts
        s.lv = 0; s.best = 0; s.gauge = {}; s.streak = 0


def base_scale(it, lv): return ilv_mult(it.ilv) * GMULT[it.g] * it.roll * (1 + CUM[lv] / 100)


def stats(eq, lvo=None):
    st = dict(atk=10.0, atk_pct=0, hp=200.0, hp_pct=0, def_=0.0, crit=.05, critdmg=1.5, aspd=0, move=0, cdr=0, skill=0, boss=0, leech=0, regen=0, onkill=0)
    aps, wm = 0.0, 1.0
    for it in eq:
        if it is None: continue
        lv = lvo[it.slot] if (lvo and it.slot in lvo) else it.lv
        sc = base_scale(it, lv)
        if it.slot == 0:
            st["atk"] += round(10 * sc); aps, wm = WEAPON[it.kind]
        elif it.slot == 1:
            a = ARMOR[it.kind]; st["def_"] += round(a["def_"] * sc); st["hp"] += round(a["hp"] * sc); st["move"] += a.get("u_move", 0)
        else:
            a = ACC[it.kind]; st["atk"] += round(a.get("atk", 0) * sc); st["hp"] += round(a.get("hp", 0) * sc)
            st["crit"] += a.get("u_crit", 0); st["cdr"] += a.get("u_cdr", 0); st["move"] += a.get("u_move", 0)
        for k, v in it.opts:
            st["def_" if k == "def" else k] += v
    if aps == 0: aps, wm = 1.5, 0.5
    atk = st["atk"] * (1 + st["atk_pct"]); hp = st["hp"] * (1 + st["hp_pct"])
    crit = min(.5, st["crit"]); cd = min(3.0, st["critdmg"]); APS = min(2.5, aps * (1 + st["aspd"]))
    cdr = min(.4, st["cdr"]); move = min(.3, st["move"])
    A = atk * (1 + crit * (cd - 1)) * (0.65 * APS * wm / 1.5 + 0.35 * (1 + st["skill"]) / (1 - cdr)) * (1 + 0.2 * st["boss"])
    Sv = hp * (1 + min(st["def_"], 400) / 100) + min(.03, st["leech"]) * A * 10 + st["regen"] * 10 + st["onkill"] * 5
    kind = eq[0].kind if eq[0] else "맨손"
    return dict(A=A, S=Sv, M=1 + move, atk=atk, hp=hp, def_=st["def_"], aps=APS, wm=wm, crit=crit, cd=cd, kind=kind)


def composite(s0, s1): return 0.6 * (s1["A"] / s0["A"] - 1) + 0.3 * (s1["S"] / s0["S"] - 1) + 0.1 * (s1["M"] / s0["M"] - 1)


def roll_opts(rng, slot, g, ilv):
    out = []; pool = list(POOL[slot])
    for _ in range(OPTN[g]):
        w = sum(p[4] for p in pool); r = rng.random() * w; acc = 0
        for i, p in enumerate(pool):
            acc += p[4]
            if r < acc: break
        k, lo, hi, flat, _w = pool.pop(i)
        out.append((k, rng.uniform(lo, hi) * OPTM[g] * (ilv_mult(ilv) if flat else 1)))
    return out


def make_item(rng, slot, g, ilv):
    kinds = list((WEAPON, ARMOR, ACC)[slot].keys())
    return Item(slot, rng.choice(kinds), g, ilv, rng.uniform(.9, 1.1), roll_opts(rng, slot, g, ilv))


def roll_grade(rng, w):
    r = rng.random() * sum(w); acc = 0
    for i, x in enumerate(w):
        acc += x
        if r < acc: return i
    return 0


START_ITEMS = lambda: [Item(0, "장검", 0, 1, 1.0, []), Item(1, "가죽 갑옷", 0, 1, 1.0, []), None]
S0 = stats(START_ITEMS())
def hpM(f): return HP_G ** (f - 1)
def atkM(f): return ATK_G ** (f - 1)
def ratio(st, f): return (st["A"] / S0["A"] / hpM(f)) ** 0.6 * (st["S"] / S0["S"] / atkM(f)) ** 0.4
def p_death(R): return min(0.9, 0.01 + 0.5 / (1 + math.exp((R - 0.85) / 0.05)))


def p_boss_win(R, f):
    r50 = R50_10 if f == 10 else R50_5
    return 1 / (1 + math.exp(-(R - r50) / 0.07))


CONT, CONT_BOSS = (0.90, 0.95) if POL == "c" else (0.80, 0.80)


def try_enhance(rng, it, wallet):
    t = it.lv + 1; wallet[0] -= S[t][4]; f = it.gauge.get(t, 0)
    _, base, inc, ceil_, *_ = S[t]
    p = 100 if ((ceil_ and f >= ceil_) or it.streak >= STREAK) else min(100, base + inc * f + (BONUS if t <= it.best else 0))
    if rng.random() * 100 < p:
        it.lv = t; it.gauge[t] = 0; it.streak = 0; nb = t > it.best; it.best = max(it.best, t)
        return True, nb
    it.gauge[t] = f + 1
    if it.lv > SAFE:
        it.lv -= 1; it.streak += 1
    return False, False


def best_pair(eq, bag, sl, wallet=None, targets=None):
    """v2: 대장간 추천 짝. 부위 sl 에서 (대상, 원본, 결과 단계)의 종합 변화율이 가장 큰 경우를 돌려준다.
    원본이 None 이면 가방 장비를 지금 단계 그대로 끼는 경우다. wallet=None 이면 비용을 무시(대기 손해 측정용)."""
    cur = eq[sl]
    items = [x for x in bag if x.slot == sl]
    if cur is not None: items.append(cur)
    srcs = [x for x in items if x.lv >= 2]
    base = stats(eq); best = None; bestv = 0.03
    for tg in (targets if targets is not None else items):
        trial = list(eq); trial[sl] = tg
        if tg is not cur and targets is None:
            v = composite(base, stats(trial))
            if v >= bestv: best, bestv = (tg, None, tg.lv), v
        for s in srcs:
            if s is tg: continue
            kept = min(s.lv - 1, CAP[tg.g])
            if kept <= tg.lv: continue
            if wallet is not None and wallet[0] < INHERIT[kept]: continue
            v = composite(base, stats(trial, {sl: kept}))
            if v >= bestv: best, bestv = (tg, s, kept), v
    return best, bestv


_PID = [0]


def play(rng, max_min=480, max_runs=45):
    _PID[0] += 1
    rng_shop = random.Random(9_000_000 + _PID[0]); rng_q = random.Random(7_000_000 + _PID[0])   # v2: 상점·의뢰 상자는 따로 굴림
    eq = START_ITEMS(); bag = []; wallet = [0.0]; gold = [0.0]
    t = 0.0; run = 0; unlocked6 = False; pity = 0; first_kill = {5: False, 10: False}
    WP = 0; BC = 0; chest_done = set()
    ev = dict(first5=None, first10=None, firstleg=None, firstepic=None, done=None, deaths=0, risk=None, drop=None,
              boss5_first=None, boss10_first=None, deaths_pre10=0, boss10_tries=0, firstinh=None)
    entry = {}; log = []; notable = []; per_run = []
    # v2: 의뢰·마을 상태
    SS = dict(visits=0, maxf=0, cleared=set(), clear_cnt={}, bosses=set(), elite={}, rats=0, arch3=0, boars=0, inh=0, b10try=0, b10loss=0)
    QL = QTAB.get(QUEST, []); qst = {q[0]: None for q in QL}; qsnap = {}
    bounty_on = False; shop_open = False; shop_items = set()
    stat = dict(q_pre=0.0, q_all=0.0, shop_pre=0.0, shop_buy_pre=0, inc_pre=0.0, town_min_pre=0.0, visits_pre=0, qtime={},
                bounty_pre=0.0, bounty_n_pre=0, gold_pre=0.0, inh_pre=0, q_60=0.0)
    TA = [0.0]   # v2: 이번 마을 도착 시각(의뢰 완료·의뢰 상자 시각 기록용)
    # v3: 골드 기록. earn = 지갑에 들어온 골드 누계(사망 손실 뒤), VG = 이번 방문 [들어온 골드, 쓴 골드, 구매 수]
    G = dict(earn=0.0, src_pre=dict(field=0.0, quest=0.0, sell=0.0), lost_pre=0.0, spent=0.0,
             sp_pre=dict(disp=0.0, rr=0.0, store=0.0, bag=0.0, bb=0.0), n_pre=dict(disp=0, rr=0, store=0, bag=0, bb=0),
             kill=None, start=None, after=None, shop_open_gold=None, buy_t={}, post=[], stone_sell_pre=0.0)
    VG = [0.0, 0.0, 0, 0.0, 0.0]   # v3: [들어온 골드, 쓴 골드, 구매 수, 의뢰 골드, 그중 10층 첫 처치 전 의뢰 골드]
    caps = dict(store=0, bag=0, bb=0)   # v3: 산 칸 늘리기 단계 수(창고, 가방, 되사기 칸)
    # v2: '마을에서만 계승' 체감 측정
    W = dict(cand=[], gwait=[], wait_time=0.0, dun_time=0.0, gain_sum=0.0, rgap_sum=0.0, endcand=[], cand_runs=0, runs=0)
    pend = []   # 기다리는 계승 후보: dict(kind='cand'|'g', it, t, run, gap)

    def tags():
        return sum(1 for x in TAG_FLOORS if x in SS["cleared"]) + len(SS["bosses"])

    def q_done(q):
        k, v = q[3], q[4]
        if k == "rats": return SS["rats"] >= v
        if k == "arch3": return SS["arch3"] - qsnap.get(q[0], 0) >= v
        if k == "boars": return SS["boars"] - qsnap.get(q[0], 0) >= v
        if k == "clear": return v in SS["cleared"]
        if k == "clear_after": return SS["clear_cnt"].get(v, 0) - qsnap.get(q[0], 0) >= 1
        if k == "boss": return v in SS["bosses"]
        if k == "elite79": return SS["elite"].get(7, 0) + SS["elite"].get(9, 0) >= v
        if k == "elite7_9": return SS["elite"].get(7, 0) >= 1 and SS["elite"].get(9, 0) >= 2
        if k == "boss10try": return SS["b10try"] - qsnap.get(q[0], 0) >= v
        if k == "elite": return SS["elite"].get(v[0], 0) >= v[1]
        if k == "reach": return SS["maxf"] >= v
        if k == "tags": return tags() >= v
        if k == "enh": return max(x.lv for x in eq if x is not None) >= v
        if k == "inh": return SS["inh"] >= v
        return False

    def q_acc(q):
        k, v = q[1], q[2]
        if k == "start": return True
        if k == "visit": return SS["visits"] >= max(1, v)
        if k == "bossloss10": return SS["b10loss"] >= 1
        if k == "maxf": return SS["maxf"] >= v
        if k == "first5": return 5 in SS["bosses"]
        if k == "tags": return tags() >= v
        return False

    def q_snap(q):
        k, v = q[3], q[4]
        if k == "arch3": return SS["arch3"]
        if k == "boars": return SS["boars"]
        if k == "clear_after": return SS["clear_cnt"].get(v, 0)
        if k == "boss10try": return SS["b10try"]
        return 0

    def give_box(bf, mg, il):
        nonlocal pity
        g = max(1, roll_grade(rng_q, GT[bf]), mg)
        if bf >= 5:
            pity += 1
            if pity >= LEG_PITY: g = 4
        if g == 4: pity = 0
        w = [34, 33, 33]
        if g >= 2: w = [x * (3 if (eq[i] is None or eq[i].g < g) else 1) for i, x in enumerate(w)]
        it = make_item(rng_q, roll_grade(rng_q, w), g, il)
        if g == 4 and ev["firstleg"] is None: ev["firstleg"] = (run, TA[0])
        if g >= 3 and ev["firstepic"] is None: ev["firstepic"] = (run, TA[0])
        bag.append(it)

    def q_pay(q):
        qst[q[0]] = "done"; amt = q[5] * QSCALE
        wallet[0] += amt; stat["q_all"] += amt
        if ev["first10"] is None: stat["q_pre"] += amt
        if TA[0] <= 60: stat["q_60"] += amt   # v2: 60분 안에 받은 의뢰 강화석
        stat["qtime"][q[0]] = (run, TA[0], ev["first10"] is None)   # v2: 마을 도착 시각, 10층 첫 처치 전에 받았는지
        if GOLD and QGOLD and amt:   # v3: 의뢰 골드 = 의뢰 강화석 x QGOLD
            ga = amt * QGOLD; gold[0] += ga; G["earn"] += ga; VG[0] += ga; VG[3] += ga
            if ev["first10"] is None: G["src_pre"]["quest"] += ga; VG[4] += ga
        if q[6] or q[7]:
            give_box(q[6] or q[8], q[7], q[6] or q[8])
        return amt

    def g_spend(cat, amt):   # v3: 골드 지출 기록. 10층 첫 처치 전 방문이면 종류별로 셈
        gold[0] -= amt; G["spent"] += amt; VG[1] += amt; VG[2] += 1
        if ev["first10"] is None:
            G["sp_pre"][cat] += amt; G["n_pre"][cat] += 1

    def cap_opts():   # v3: 지금 살 수 있는 칸 늘리기(종류, 값). 창고는 5층 보스 첫 처치 뒤, 가방·되사기 칸은 상점
        opts = []
        if 5 in SS["bosses"] and caps["store"] < len(STORE_STEPS): opts.append(("store", STORE_STEPS[caps["store"]][1]))
        if SHOP and shop_open and caps["bag"] < len(BAG_STEPS): opts.append(("bag", BAG_STEPS[caps["bag"]][1]))
        if SHOP and shop_open and BUYBACK_UP and caps["bb"] < 1: opts.append(("bb", BUYBACK_UP))
        return opts

    def buy_caps():   # v3: 칸 늘리기는 살 수 있으면 싼 것부터 삼
        if GSPEND in ("save", "none", "smart0"): return
        while True:
            opts = [o for o in cap_opts() if o[1] <= gold[0]]
            if not opts: return
            k, p = min(opts, key=lambda o: o[1])
            g_spend(k, p); caps[k] += 1; G["buy_t"].setdefault(f"{k}{caps[k]}", TA[0])

    def reroll_ok(il, k):   # v3: k번째 새로고침을 할지(GSPEND)
        rp = reroll_price(il, k)
        if gold[0] < rp: return False
        if GSPEND == "always": return True
        smart = gold[0] >= rp + shop_price(2, il) and weak_slot(il)
        if GSPEND in ("smart", "smart0") or k > 1: return smart
        if GSPEND == "reserve":   # 비교용: 첫 새로고침도 '다음 칸 늘리기 값(없으면 희귀 진열 값)'을 남길 때만
            nxt = [o[1] for o in cap_opts()]
            return smart or gold[0] - rp >= (min(nxt) if nxt else shop_price(2, il))
        return smart or gold[0] >= CASUAL * rp   # base·save: 첫 새로고침은 지갑이 새로고침 값의 CASUAL배 이상이면 구경 삼아 1번

    def weak_slot(il):   # v3: '희귀 진열(굴림 최고, 옵션 없음)이 나오면 +5% 이상 오를 부위'가 있는지. 난수를 쓰지 않음
        s0 = stats(eq)
        for sl in range(3):
            cur = eq[sl]
            kind = cur.kind if cur is not None else list((WEAPON, ARMOR, ACC)[sl].keys())[0]
            h = Item(sl, kind, 2, il, 1.1, []); trial = list(eq); trial[sl] = h
            v = composite(s0, stats(trial)) if cur is not None else 1.0
            if cur is not None and cur.lv >= 2:
                v = max(v, composite(s0, stats(trial, {sl: min(cur.lv - 1, CAP[2])})))
            if v >= 0.05: return True
        return False

    def q_check(kinds_town):
        """받은 의뢰 중 완료된 것을 지급. kinds_town=True 면 마을 안 조건만 본다."""
        got = 0.0
        for q in QL:
            if qst[q[0]] != "act": continue
            town_kind = q[3] in ("enh", "inh", "town1")
            if town_kind != kinds_town: continue
            if q[3] == "town1":
                continue
            if q_done(q): got += q_pay(q)
        return got

    def q_accept():
        for q in QL:
            if qst[q[0]] is None and q_acc(q):
                qst[q[0]] = "act"; qsnap[q[0]] = q_snap(q)

    def do_enhance():
        nb_any = False; tries = 0
        while True:
            cands = [it for it in eq if it is not None and it.lv < CAP[it.g] and wallet[0] >= S[it.lv + 1][4]]
            if not cands: break
            s0 = stats(eq)
            it = max(cands, key=lambda it: composite(s0, stats(eq, {it.slot: it.lv + 1})) / EA[it.lv + 1])
            blv = it.lv
            if blv >= 7 and ev["risk"] is None: ev["risk"] = t
            ok, nb = try_enhance(rng, it, wallet); tries += 1
            if (not ok) and blv >= 8 and ev["drop"] is None: ev["drop"] = t
            nb_any = nb_any or nb
        return tries, nb_any

    def measure_wait(f, dt):
        """v2: 기다리는 계승 후보가 있을 때 그 시간과 손해(마을에서 바로 계승했다면 얼마나 셌을지)."""
        if ev["first10"] is not None: return
        W["dun_time"] += dt
        live = []
        for e in pend:
            if e["kind"] == "cand" and e["it"] in bag: live.append(e)
            elif e["kind"] == "g" and eq[e["it"].slot] is e["it"]: live.append(e)
        if not live: return
        base = stats(eq); eq_h = list(eq); lvo = {}
        for sl in set(e["it"].slot for e in live):
            tg_list = [e["it"] for e in live if e["it"].slot == sl]
            res, v = best_pair(eq, bag, sl, None, tg_list)
            if res is not None:
                eq_h[sl] = res[0]; lvo[sl] = res[2]
        if not lvo: return
        sh = stats(eq_h, lvo); gain = composite(base, sh)
        if gain < 0.03: return
        W["wait_time"] += dt; W["gain_sum"] += gain * dt; W["rgap_sum"] += (ratio(sh, f) / ratio(base, f) - 1) * dt

    q_accept()
    while t < max_min and run < max_runs:
        run += 1; st = stats(eq); t_run0 = t
        qact0 = sum(1 for q in QL if qst[q[0]] == "act" and not q[0].startswith("q.lamp_tags"))   # v2: 이번 원정 시작 때 진행 중인 의뢰 수(명패 제외)
        if BC:
            start = BC; boss_only = True
        else:
            cands = [1] + ([6] if unlocked6 else []) + ([WP] if WP else [])
            ok = [s for s in cands if ratio(st, s) >= CONT]
            start = max(ok) if ok else 1; boss_only = False
        WP = 0
        f = start; direct = 0.0; dis = 0.0; died = False
        maxf0 = SS["maxf"]; bkills = 0; bounty_now = bounty_on; gold_run = 0.0   # v2: 반복 의뢰·골드
        g_start = (gold[0], G["earn"])   # v3: 원정 출발 때 지갑 골드와 번 골드 누계
        while True:
            st = stats(eq)
            if f not in entry:
                entry[f] = dict(run=run, time=t, atk=st["atk"], hp=st["hp"], def_=st["def_"], R=ratio(st, f), kind=st["kind"])
            SS["maxf"] = max(SS["maxf"], f)   # v2
            R = ratio(st, f); r_, b_, a_, e_ = FLOORS[f]
            drops = []; was_boss_only = boss_only   # v2: 보스 앞 표지 재도전인지 기억(줍는 시각 계산용)
            if boss_only:
                die_here = False; boss_only = False
                t += BOSS_MIN[f]
            else:
                log.append((run, f, t, R))
                die_here = rng.random() < p_death(R); frac = rng.random() if die_here else 1.0
                if not V1: measure_wait(f, FLOOR_MIN[f] * frac)   # v2: 계승 대기 측정(난수 안 씀)
                for kind, n in (("rat", r_), ("boar", b_), ("archer", a_)):
                    k = int(round(n * frac))
                    for _ in range(k):
                        if rng.random() < DROP[kind]: drops.append(("mob", f))
                        direct += STONE[kind] * ilv_mult(f)
                    # v2: 의뢰 처치 수, 반복 의뢰, 골드
                    if kind == "rat": SS["rats"] += k
                    elif kind == "boar": SS["boars"] += k
                    elif f >= 3: SS["arch3"] += k
                    if f >= maxf0 - 2: bkills += k
                    gold_run += GOLD_KILL[kind] * k * ilv_mult(f)
                if not die_here:
                    for _ in range(e_):
                        drops.append(("elite", f)); direct += STONE["elite"] * ilv_mult(f)
                    if e_:
                        SS["elite"][f] = SS["elite"].get(f, 0) + e_   # v2
                        if f >= maxf0 - 2: bkills += e_
                        gold_run += GOLD_ELITE * e_ * ilv_mult(f)
                t += FLOOR_MIN[f] * frac
            boss_dead = False
            if not die_here and f in BOSS_T:
                win = rng.random() < p_boss_win(R, f)
                if f == 5 and ev["boss5_first"] is None: ev["boss5_first"] = win
                if f == 10:
                    if ev["boss10_first"] is None: ev["boss10_first"] = win
                    if not first_kill[10]:
                        ev["boss10_tries"] += 1
                        if not win: SS["b10loss"] += 1   # v2: 10층 첫 처치 전 보스 패배 수(무진 장면·기둥 의뢰)
                    SS["b10try"] += 1
                if win:
                    boss_dead = True; direct += BOSS_STONE[f]; gold_run += GOLD_BOSS[f]
                else:
                    die_here = True
                    if not first_kill[f]: BC = f
            elif not die_here:
                drops.append(("chest", f)); direct += chest_stone(f); gold_run += gold_chest(f)
            if not die_here:   # v2: 층 정리 기록(명패, 의뢰)
                SS["cleared"].add(f); SS["clear_cnt"][f] = SS["clear_cnt"].get(f, 0) + 1
            gen = []
            for src, fl in drops:
                g = roll_grade(rng, GT[fl]); forced_slot = None
                if src == "elite" and g == 0: g = 1
                if src == "chest" and fl in (1, 3) and fl not in chest_done:
                    chest_done.add(fl); g = max(g, 2 if fl == 1 else 3)
                    if fl == 1: forced_slot = 0
                if fl >= 5:
                    pity += 1
                    if pity >= LEG_PITY: g = 4
                if g == 4: pity = 0
                gen.append((g, fl, forced_slot))
            if boss_dead:
                if f == BC: BC = 0
                n, bt = BOSS_T[f]; bg = [roll_grade(rng, bt) for _ in range(n)]
                if not first_kill[f]:
                    if f == 5 and max(bg) < 3: bg[0] = 3
                    if f == 10 and 4 not in bg: bg[0] = 4
                    first_kill[f] = True
                    SS["bosses"].add(f)   # v2
                for g in bg:
                    if g == 4: pity = 0
                    gen.append((g, f + 1, None))
            t_mid = t - (BOSS_MIN[f] * 0.5 if was_boss_only else FLOOR_MIN[f] * 0.5)   # v2: 줍는 시각(층 가운데로 어림)
            for g, il, fs in gen:
                if fs is not None:
                    slot = fs
                else:
                    w = [34, 33, 33]
                    if g >= 2: w = [x * (3 if (eq[i] is None or eq[i].g < g) else 1) for i, x in enumerate(w)]
                    slot = roll_grade(rng, w)
                it = make_item(rng, slot, g, il)
                if g == 4 and ev["firstleg"] is None: ev["firstleg"] = (run, t)
                if g >= 3 and ev["firstepic"] is None: ev["firstepic"] = (run, t)
                cur = eq[slot]; s_cur = stats(eq); trial = list(eq); trial[slot] = it
                if V1:
                    done_eq = False
                    if cur is not None and cur.lv >= 2:
                        moved = cur.lv - 1; kept = min(moved, CAP[g]); cost = INHERIT[kept] if kept < 15 else 999
                        if kept > 0 and wallet[0] + direct + dis >= cost and composite(s_cur, stats(trial, {slot: kept})) >= 0.03:
                            pay = cost; take = min(pay, wallet[0]); wallet[0] -= take; pay -= take
                            take = min(pay, dis); dis -= take; pay -= take; direct -= pay
                            dis += DIS[cur.g] + (REFUND[moved] - REFUND[kept])
                            it.lv = kept; it.best = min(cur.best, CAP[g]); eq[slot] = it; done_eq = True
                    if not done_eq:
                        if cur is None or composite(s_cur, stats(trial)) >= 0.03:
                            eq[slot] = it
                            if cur is not None and cur.lv >= 2: bag.append(cur)
                            elif cur is not None: dis += DIS[cur.g] + REFUND[cur.lv]
                        elif g == 0 and f >= 6:
                            dis += DIS[0]
                        else:
                            bag.append(it)
                    if len(bag) > 40:
                        bag.sort(key=lambda x: (x.lv, x.g)); x = bag.pop(0); dis += DIS[x.g] + REFUND[x.lv]
                    continue
                # v2: 던전 안 계승 없음. '+0 그대로 끼면'이 +3% 이상일 때만 G로 낀다. 벗은 장비는 가방으로.
                v0 = composite(s_cur, stats(trial)) if cur is not None else 1.0
                if cur is None or v0 >= 0.03:
                    eq[slot] = it
                    if cur is not None:
                        bag.append(cur)
                        if cur.lv >= 2 and ev["first10"] is None:
                            pend.append(dict(kind="g", it=it, t=t_mid, run=run, gap=0.0, f=f))
                elif g == 0 and f >= 6 and not sell_ok(0):   # v3: 일반을 팔려는 비교용(SELL)이면 자동 분해를 끔
                    dis += DIS[0]
                else:
                    bag.append(it)
                    if g >= 1 and ev["first10"] is None:   # 계승 후보인지(계승 값 +3% 이상이고 '지금 끼면'보다 3%p 이상 높음)
                        res, vi = best_pair(eq, bag, slot, None, [it])
                        if res is not None and vi >= v0 + 0.03:
                            pend.append(dict(kind="cand", it=it, t=t_mid, run=run, gap=vi - max(v0, 0.0), f=f))
                if len(bag) > 40 + 5 * caps["bag"]:   # v3: 가방 칸 늘리기(45/50칸)
                    keep = set(id(e["it"]) for e in pend)
                    bag.sort(key=lambda x: (id(x) in keep, x.lv, x.g)); x = bag.pop(0); dis += DIS[x.g] + REFUND[x.lv]
            if boss_dead and f == 5:
                unlocked6 = True
                if ev["first5"] is None: ev["first5"] = (run, t)
            if boss_dead and f == 10 and ev["first10"] is None:
                ev["first10"] = (run, t)
                if GOLD:   # v3: 10층 첫 처치 순간의 골드(지갑 + 손에 든 이번 원정 골드)와 번 골드, 이 원정 출발 때 값
                    G["kill"] = (gold[0] + gold_run, G["earn"] + gold_run); G["start"] = g_start
                    G["src_pre"]["field"] += gold_run
            if die_here:
                died = True; ev["deaths"] += 1
                if ev["first10"] is None: ev["deaths_pre10"] += 1
                if direct > 0: direct = math.floor(direct * 0.5)
                g_before = gold_run
                gold_run = math.floor(gold_run * 0.5)
                if ev["first10"] is None: G["lost_pre"] += g_before - gold_run   # v3: 사망으로 잃은 골드
                break
            if f == 10: break
            nst = stats(eq); need = CONT_BOSS if (f + 1) in BOSS_T else CONT
            if ratio(nst, f + 1) < need:
                WP = f + 1
                break
            f += 1
        wallet[0] += direct + dis
        if V1:
            t += TOWN_MIN; before = stats(eq); town = 0.0
            for it in bag:
                cur = eq[it.slot]
                if cur is not None and cur is not it and it.lv >= 2:
                    moved = it.lv - 1; kept = min(moved, CAP[cur.g])
                    if kept > cur.lv:
                        cost = INHERIT[kept]
                        if wallet[0] >= cost:
                            wallet[0] -= cost; wallet[0] += DIS[it.g] + (REFUND[moved] - REFUND[kept])
                            cur.lv = kept; cur.best = max(cur.best, min(it.best, CAP[cur.g])); cur.gauge = {}; cur.streak = 0
                            continue
                if cur is not None and it.lv == 0 and it.g > cur.g and cur.lv >= 2:
                    moved = cur.lv - 1; kept = min(moved, CAP[it.g]); cost = INHERIT[kept]
                    trial = list(eq); trial[it.slot] = it
                    if wallet[0] >= cost and composite(stats(eq), stats(trial, {it.slot: kept})) >= 0.03:
                        wallet[0] -= cost; wallet[0] += DIS[cur.g] + (REFUND[moved] - REFUND[kept])
                        it.lv = kept; it.best = min(cur.best, CAP[it.g]); eq[it.slot] = it
                        continue
                wallet[0] += DIS[it.g] + REFUND[it.lv]; town += DIS[it.g] + REFUND[it.lv]
            bag = []
            tries, nb_any = do_enhance()
            notable.append(nb_any or composite(before, stats(eq)) >= 0.05)
            per_run.append(dict(run=run, start=start, end=f, died=died, tries=tries, direct=direct, dis=dis, town=town, quest=0.0, quest_pre=0.0, shop=0.0,
                                gold=0.0, tmin=TOWN_MIN, inh=0, mins=t - t_run0, pre10=ev['first10'] is None or ev['first10'][0] == run, shop_eq=0))
            if ev["first10"] is None or ev["first10"][0] == run: stat["inc_pre"] += direct + dis + town
        else:
            # ------------------------------------------------ v2: 마을 (귀환 지점 → 결과 화면 정산 → 대장간 → 상점 → 갱도 입구)
            pre10_visit = ev["first10"] is None
            SS["visits"] += 1; visit = SS["visits"]; t_arr = t; TA[0] = t
            tm = town_minutes(visit, BC != 0 and visit >= 4)
            t += tm; before = stats(eq); town = 0.0; quest_now = 0.0; shop_spend = 0.0; ninh = 0; qpre0 = stat["q_pre"]
            VG[0] = 0.0; VG[1] = 0.0; VG[2] = 0; VG[3] = 0.0; VG[4] = 0.0   # v3: 이번 방문 골드 기록
            if GOLD:
                gold[0] += gold_run; G["earn"] += gold_run; VG[0] += gold_run   # v3
                if pre10_visit: G["src_pre"]["field"] += gold_run
            # 1) 결과 화면 자동 지급: 받은 의뢰 완료분 → 새 의뢰 받기 → 받기 전 기록 인정분
            quest_now += q_check(False)
            if bounty_now and bkills >= BOUNTY_KILLS and not (BOUNTY_MAX and ev["first10"] is None and stat["bounty_n_pre"] >= BOUNTY_MAX):
                b = bounty_reward(maxf0 - 2) * QSCALE
                if TA[0] <= 60: stat["q_60"] += b
                wallet[0] += b; quest_now += b; stat["q_all"] += b
                if ev["first10"] is None: stat["q_pre"] += b; stat["bounty_pre"] += b; stat["bounty_n_pre"] += 1
            q_accept()
            quest_now += q_check(False)
            if BOUNTY != "0" and 5 in SS["bosses"]: bounty_on = True
            if SHOP_UNLOCK.startswith("maxf") and SS["maxf"] >= int(SHOP_UNLOCK[4:]): shop_open = True
            if SHOP_UNLOCK == "reach3" and SS["maxf"] >= 3: shop_open = True
            if SHOP_UNLOCK == "first5" and 5 in SS["bosses"]: shop_open = True   # v3: 5층 보스 첫 처치 뒤 귀환(창고와 같은 방문)
            if SHOP_UNLOCK.startswith("visit") and visit >= int(SHOP_UNLOCK[5:]): shop_open = True   # v3: n번째 귀환
            if GOLD and shop_open and G["shop_open_gold"] is None: G["shop_open_gold"] = (gold[0], TA[0])   # v3: 상점이 열린 방문의 지갑 골드
            n_end = sum(1 for e in pend if e["kind"] == "cand" and e["it"] in bag)
            if pre10_visit:
                W["endcand"].append(n_end); W["runs"] += 1
                if any(e["kind"] == "cand" and e["run"] == run for e in pend): W["cand_runs"] += 1
            # 2) 대장간 추천 짝(부위마다 1개, 바뀌면 다시 봄)
            if INH == "naive":   # v2: 비교용. 추천 없이 '벗어 둔 +2 이상 원본 → 지금 장비' 또는 '더 높은 등급 +0 → 지금 장비 단계를 받아 낌'만 함
                for it in list(bag):
                    cur = eq[it.slot]
                    if cur is None: continue
                    if it.lv >= 2:
                        kept = min(it.lv - 1, CAP[cur.g])
                        if kept > cur.lv and wallet[0] >= INHERIT[kept]:
                            wallet[0] -= INHERIT[kept]; wallet[0] += DIS[it.g] + (REFUND[it.lv - 1] - REFUND[kept])
                            cur.lv = kept; cur.best = max(cur.best, min(it.best, CAP[cur.g])); cur.gauge = {}; cur.streak = 0
                            bag.remove(it); ninh += 1; SS["inh"] += 1
                            if ev["firstinh"] is None: ev["firstinh"] = (run, TA[0])   # v2: 마을 도착 시각
                    elif it.g > cur.g and cur.lv >= 2:
                        kept = min(cur.lv - 1, CAP[it.g]); trial = list(eq); trial[it.slot] = it
                        if wallet[0] >= INHERIT[kept] and composite(stats(eq), stats(trial, {it.slot: kept})) >= 0.03:
                            wallet[0] -= INHERIT[kept]; wallet[0] += DIS[cur.g] + (REFUND[cur.lv - 1] - REFUND[kept])
                            it.lv = kept; it.best = min(cur.best, CAP[it.g]); eq[it.slot] = it
                            bag.remove(it); ninh += 1; SS["inh"] += 1
                            if ev["firstinh"] is None: ev["firstinh"] = (run, TA[0])   # v2: 마을 도착 시각
            for _ in range(3 if INH == "best" else 0):
                changed = False
                for sl in range(3):
                    res, v = best_pair(eq, bag, sl, wallet)
                    if res is None: continue
                    tg, src, kept = res; cur = eq[sl]
                    if src is not None:
                        wallet[0] -= INHERIT[kept]; wallet[0] += DIS[src.g] + (REFUND[src.lv - 1] - REFUND[kept])
                        tg.best = max(tg.best, min(src.best, CAP[tg.g])); tg.lv = kept; tg.gauge = {}; tg.streak = 0
                        if src is cur: eq[sl] = None
                        else: bag.remove(src)
                        ninh += 1; SS["inh"] += 1
                        if ev["firstinh"] is None: ev["firstinh"] = (run, TA[0])   # v2: 마을 도착 시각
                    if tg is not eq[sl]:
                        if tg in bag: bag.remove(tg)
                        if eq[sl] is not None: bag.append(eq[sl])
                        eq[sl] = tg
                    changed = True
                if not changed: break
            # 계승 대기 기록 정리
            still = []
            for e in pend:
                it = e["it"]
                if e["kind"] == "g":
                    W["gwait"].append((t_arr - e["t"], run - e["run"], it.lv > 0 and eq[it.slot] is it))
                    continue
                if eq[it.slot] is it:
                    W["cand"].append((t_arr - e["t"], run - e["run"], "inh" if it.lv > 0 else "eq0", e["gap"], f - e["f"]))
                elif it in bag:
                    still.append(e)
                else:
                    W["cand"].append((t_arr - e["t"], run - e["run"], "gone", e["gap"], f - e["f"]))
            pend = still
            if ninh: quest_now += q_check(True)
            # 3) 남은 가방 분해. 강화석이 모자라 못 한 계승 후보와 그 원본은 남김(창고)
            keep = set()
            for e in pend:
                res, v = best_pair(eq, bag, e["it"].slot, None, [e["it"]])
                if res is not None and res[1] is not None and wallet[0] < INHERIT[res[2]]:
                    keep.add(id(e["it"])); keep.add(id(res[1]))
            nb = []
            for it in bag:
                if id(it) in keep: nb.append(it); continue
                if it.lv == 0 and sell_ok(it.g):   # v3: 비교용 장비 팔기(분해 대신 골드)
                    sg = DIS[it.g] * SELL_RATE; gold[0] += sg; G["earn"] += sg; VG[0] += sg
                    if pre10_visit: G["src_pre"]["sell"] += sg; G["stone_sell_pre"] += DIS[it.g]
                    continue
                wallet[0] += DIS[it.g] + REFUND[it.lv]; town += DIS[it.g] + REFUND[it.lv]
            bag = nb
            for e in pend:
                if e["it"] not in bag:   # 더 좋은 짝에 밀려 분해됨
                    W["cand"].append((t_arr - e["t"], run - e["run"], "gone", e["gap"], f - e["f"]))
            pend = [e for e in pend if e["it"] in bag]
            # 4) 상점 (v3: 기본 골드, GOLD=0이면 강화석 전용 비교. 진열 3칸 + 골드 새로고침, 새로고침은 산 칸까지 3칸 모두 다시 굴림). 강화보다 먼저 보고 +5% 이상이면 산다
            if SHOP and shop_open:
                il = max(1, SS["maxf"] + SHOP_ILV)
                for _pass in range(1 + (REROLL if GOLD and GSPEND != "none" else 0)):   # v3: 0 = 도착 때 진열, 1~ = 골드 새로고침
                    if _pass > 0:   # v3: 새로고침 조건(GSPEND)
                        if not reroll_ok(il, _pass): break
                        g_spend("rr", reroll_price(il, _pass))
                    for sl in range(3):
                        g = 2 if (unlocked6 and rng_shop.random() < SHOP_RARE) else 1
                        it = make_item(rng_shop, sl, g, il); price = shop_price(g, il)   # v3: 골드면 10배
                        if GOLD:
                            if price > gold[0]: continue
                        elif price > 0.3 * wallet[0]:
                            continue
                        cur = eq[sl]; trial = list(eq); trial[sl] = it; s0 = stats(eq)
                        v0 = composite(s0, stats(trial)) if cur is not None else 1.0; vi = -1.0; kept = 0
                        if cur is not None and cur.lv >= 2:
                            kept = min(cur.lv - 1, CAP[g])
                            if wallet[0] >= INHERIT[kept] + (0 if GOLD else price): vi = composite(s0, stats(trial, {sl: kept}))
                        if max(v0, vi) >= 0.05:
                            if GOLD: g_spend("disp", price)   # v3
                            else: wallet[0] -= price
                            shop_spend += price
                            if vi > v0:
                                moved = cur.lv - 1; wallet[0] -= INHERIT[kept]; wallet[0] += DIS[cur.g] + (REFUND[moved] - REFUND[kept])
                                it.lv = kept; it.best = min(cur.best, CAP[g]); SS["inh"] += 1
                            elif cur is not None:
                                wallet[0] += DIS[cur.g] + REFUND[cur.lv]
                            eq[sl] = it; shop_items.add(id(it))
                            if ev["first10"] is None: stat["shop_buy_pre"] += 1
                    if _pass == 0 and GOLD: buy_caps()   # v3: 칸 늘리기는 진열을 본 다음(싼 것부터)
            elif GOLD:
                buy_caps()   # v3: 상점이 열리기 전에는 창고 칸만
            # 5) 강화. 마을 안 의뢰(+3 강화, 첫 강화)가 끝나면 그 보상으로 한 번 더
            tries, nb_any = do_enhance()
            paid_town = 0
            for q in QL:
                if qst[q[0]] == "act" and q[3] == "town1" and tries >= 1:
                    quest_now += q_pay(q); paid_town += 1
            got = q_check(True); quest_now += got
            if got > 0 or paid_town:
                tr2, nb2 = do_enhance(); tries += tr2; nb_any = nb_any or nb2
            notable.append(nb_any or composite(before, stats(eq)) >= 0.05)
            pre = ev['first10'] is None or ev['first10'][0] == run
            per_run.append(dict(run=run, start=start, end=f, died=died, tries=tries, direct=direct, dis=dis, town=town, quest=quest_now, quest_pre=stat["q_pre"] - qpre0,
                                shop=shop_spend, gold=gold_run if GOLD else 0.0, tmin=tm, inh=ninh, mins=t - t_run0, pre10=pre,
                                shop_eq=sum(1 for x in eq if x is not None and id(x) in shop_items),
                                tags=tags(), b10loss=SS["b10loss"], maxf=SS["maxf"], first10_run=(ev["first10"] or (None,))[0], qact=qact0,
                                g_in=VG[0], g_sp=VG[1], g_n=VG[2], g_q=VG[3], g_q_pre=VG[4], g_bal=gold[0], pre10_visit=pre10_visit))   # v3: 골드 들어옴·씀·구매 수·남은 골드
            if GOLD:   # v3: 10층 첫 처치 방문을 마친 뒤 골드, 그 뒤 방문 기록
                if ev["first10"] is not None and ev["first10"][0] == run: G["after"] = (gold[0], G["earn"])
                elif ev["first10"] is not None: G["post"].append((VG[0], VG[1], VG[2], gold[0]))
            if pre:
                stat["inc_pre"] += direct + dis + town; stat["shop_pre"] += shop_spend; stat["gold_pre"] += gold_run if GOLD else 0.0
            if pre10_visit:
                stat["town_min_pre"] += tm; stat["visits_pre"] += 1; stat["inh_pre"] += ninh
        if ev["done"] is None and all(x is not None and x.g == 4 and x.lv == 15 for x in eq):
            ev["done"] = (run, t)
    ev["stat"] = stat; ev["W"] = W; ev["G"] = G   # v3
    return ev, entry, log, notable, per_run


def pct(xs, q):
    xs = sorted(xs); return xs[min(len(xs) - 1, int(math.ceil(q * len(xs))) - 1)]


def p1(atk, mult, hp, hits=1, n=4000, seed=3):
    rg = random.Random(seed); ok = 0
    for _ in range(n):
        tot = 0
        for _h in range(hits):
            m = mult * (1.5 if rg.random() < 0.05 else 1)
            tot += max(1, round(atk * m * rg.uniform(.92, 1.08)))
        ok += tot >= hp
    return ok / n


def main():
    rng = random.Random(int(os.environ.get("SEED", "20261002")))   # v2: SEED 로 난수 바꿔 보기(기본값은 1차와 같음)
    res = [play(rng) for _ in range(P)]
    med = statistics.median
    gtag = (f", 골드(의뢰 x{QGOLD:g}, 쓰기={GSPEND}, 새로고침 {REROLL}회, 창고 {STORE_STEPS[0][1]:g}/{STORE_STEPS[1][1]:g}, 가방 {BAG_STEPS[0][1]:g}/{BAG_STEPS[1][1]:g}, 되사기 칸 {BUYBACK_UP:g}"
            f"{', 상점 열림=' + SHOP_UNLOCK if SHOP_UNLOCK != 'maxf7' else ''}{', 팔기=' + SELL if SELL != '0' else ''}{', 골드 배율 ' + format(GSCALE, 'g') if GSCALE != 1 else ''})") if GOLD else ""   # v3
    tag = "1차 규칙(V1)" if V1 else ("v3 " if GOLD else "v2 ") + "마을=" + ({"sched": "일정 6/5/5/4·재도전 3", "final": "최종 4/4/4/4.5·재도전 3"}.get(TOWN, f"{TOWN}분 고정")) + f", 의뢰={QUEST}, 반복 의뢰={BOUNTY}{f'(10층 전 최대 {BOUNTY_MAX}회)' if BOUNTY_MAX else ''}, 상점={'켬' if SHOP else '끔'}{gtag}{', 장비 보장' if GUAR else ''}{', 계승=단순 방식' if INH == 'naive' else ''}"   # v3: 골드 설명
    print(f"# 인원 {P}, 체력 성장 {HP_G}, 공격 성장 {ATK_G}, 정책 {'신중(R>=0.90, 보스 앞 0.95)' if POL == 'c' else '밀어붙임(R>=0.80)'}, 보스 R50 5층 {R50_5} / 10층 {R50_10} | {tag}")
    for key, name in (("first5", "5층 보스 첫 처치"), ("first10", "10층 보스 첫 처치"), ("firstepic", "첫 영웅(보라) 획득"), ("firstleg", "첫 전설 획득"), ("done", "전설 +15 세 부위")):
        xs = [r[0][key] for r in res if r[0][key] is not None]
        if not xs:
            print(f"{name}: 도달 0%"); continue
        mins = [x[1] for x in xs]; runs = [x[0] for x in xs]
        extra = ""
        if key == "first10": extra = f", 1h45~2h45 안 {sum(1 for m in mins if 105 <= m <= 165) / len(mins) * 100:.0f}%"
        print(f"{name}: 도달 {len(xs) / P * 100:.0f}%, 원정 중앙 {med(runs):.0f}, 시간 중앙 {med(mins):.0f}분 (빠른10% {pct(mins, .1):.0f}, 늦은10% {pct(mins, .9):.0f}){extra}")
    b5 = [r[0]["boss5_first"] for r in res if r[0]["boss5_first"] is not None]
    b10 = [r[0]["boss10_first"] for r in res if r[0]["boss10_first"] is not None]
    print(f"5층 보스 첫 도전 승률 {sum(b5) / len(b5) * 100:.0f}%, 10층 보스 첫 도전 승률 {sum(b10) / len(b10) * 100:.0f}%, 10층 첫 처치까지 보스 도전 평균 {statistics.mean(r[0]['boss10_tries'] for r in res):.2f}회")
    print(f"10층 첫 처치 전 사망 평균 {statistics.mean(r[0]['deaths_pre10'] for r in res):.2f}회, 전체 사망 평균 {statistics.mean(r[0]['deaths'] for r in res):.2f}회")
    easy = []
    for r in res:
        end = r[0]["first10"][1] if r[0]["first10"] else 999
        easy.append(sum(FLOOR_MIN[f] for (run, f, tt, R) in r[2] if tt < end and R >= 1.30))
    print(f"10층 첫 처치 전 쉬운 층(R>=1.30)에서 보낸 시간 중앙 {med(easy):.0f}분")
    risk = [r[0]["risk"] or 999 for r in res]; drop = [r[0]["drop"] or 999 for r in res]
    print(f"+8 첫 시도 중앙 {med(risk):.0f}분, 첫 하락 중앙 {med(drop):.0f}분, 60분 안 첫 하락 {sum(1 for x in drop if x <= 60) / P * 100:.0f}%")
    for a, b in ((1, 4), (5, 8), (9, 12), (13, 16), (17, 20)):
        xs = [r[3][e - 1] for r in res for e in range(a, b + 1) if e <= len(r[3])]
        if xs: print(f"원정 {a}~{b}: 정비(마을 방문)에서 눈에 띄는 강화 {sum(xs) / len(xs) * 100:.0f}%")
    tries = [x["tries"] for r in res for x in r[4][:8]]
    print(f"원정 1~8 정비(마을 방문)당 강화 시도 평균 {statistics.mean(tries):.1f}, 3회 이상 {sum(1 for x in tries if x >= 3) / len(tries) * 100:.0f}%")
    if not V1:   # v2: 마을·의뢰·상점·계승 대기 지표
        stt = [r[0]["stat"] for r in res]
        vis = [s["visits_pre"] for s in stt]; tmin = [s["town_min_pre"] for s in stt]
        print(f"[v2] 10층 첫 처치 전 마을 방문 평균 {statistics.mean(vis):.1f}회, 마을 체류 합 중앙 {med(tmin):.0f}분, 방문당 평균 {sum(tmin) / max(1, sum(vis)):.2f}분")
        qp = [s["q_pre"] for s in stt]; inc = [s["inc_pre"] for s in stt]; tot = [a + b for a, b in zip(qp, inc)]
        print(f"[v2] 10층 첫 처치 전 의뢰 강화석 중앙 {med(qp):.0f}석 (평균 {statistics.mean(qp):.1f}, 늦은 사람 90% {pct(qp, .9):.0f}, 최대 {max(qp):.0f}), "
              f"총수입 중앙 {med(tot):.0f}석, 의뢰 비중 평균 {statistics.mean(q / t_ for q, t_ in zip(qp, tot) if t_) * 100:.1f}%")
        bp = [s["bounty_pre"] for s in stt]; bn = [s["bounty_n_pre"] for s in stt]
        print(f"[v2] 그중 반복 의뢰 중앙 {med(bp):.0f}석 ({statistics.mean(bn):.1f}회), 95석을 넘은 사람 {sum(1 for x in qp if x > 95) / P * 100:.1f}%, "
              f"60분 안에 받은 의뢰 강화석 중앙 {med(s['q_60'] for s in stt):.0f}석")
        qshare = [x["quest"] / max(1, x["direct"] + x["dis"] + x["town"] + x["quest"]) for r in res for x in r[4] if x["quest"] > 0 and x["pre10"]]
        if qshare: print(f"[v2] 의뢰를 받은 원정에서 의뢰 몫 중앙 {med(qshare) * 100:.0f}%, 상위 10% {pct(qshare, .9) * 100:.0f}%")
        shop = [s["shop_pre"] for s in stt]; buys = [s["shop_buy_pre"] for s in stt]
        shop_eq = [x["shop_eq"] for r in res for x in r[4] if x["pre10"]]
        print(f"[v2] 상점(10층 전): 구매 평균 {statistics.mean(buys):.2f}회, 마을 방문당 {sum(buys) / max(1, sum(vis)):.3f}회, 지출 평균 {statistics.mean(shop):.1f}{'골드' if GOLD else '석'}"
              + ("" if GOLD else f" (수입 대비 {statistics.mean(s / t_ for s, t_ in zip(shop, tot) if t_) * 100:.1f}%)") + f", 원정 끝 상점 장비를 낀 칸 평균 {statistics.mean(shop_eq):.2f}/3")
        if GOLD:   # v3: 골드 지표
            gs = [r[0]["G"] for r in res]; gk = [(r[0]["G"], r[0]["first10"]) for r in res if r[0]["G"]["kill"]]
            earn_k = [g["kill"][1] for g, _ in gk]; left_k = [g["kill"][0] for g, _ in gk]
            rk = [l / e for l, e in zip(left_k, earn_k) if e]
            rs = [g["start"][0] / g["start"][1] for g, _ in gk if g["start"][1]]
            ra = [g["after"][0] / g["after"][1] for g in gs if g["after"] and g["after"][1]]
            src = {k: statistics.mean(g["src_pre"][k] for g, _ in gk) for k in ("field", "quest", "sell")}
            print(f"[v3] 골드 수입(10층 첫 처치 순간까지) 중앙 {med(earn_k):.0f} (평균 구성: 사냥·상자·정예·보스 {src['field']:.0f}, 의뢰 {src['quest']:.0f}, 팔기 {src['sell']:.0f}), "
                  f"사망으로 잃은 골드 평균 {statistics.mean(g['lost_pre'] for g, _ in gk):.0f}")
            print(f"[v3] 10층 첫 처치 순간 남은 골드(지갑 + 손에 든 이번 원정 골드) 중앙 {med(left_k):.0f} = 번 골드의 중앙 {med(rk) * 100:.0f}% "
                  f"(합계 기준 {sum(left_k) / sum(earn_k) * 100:.0f}%, 늦은 쪽 90% {pct(rk, .9) * 100:.0f}%, 30% 이하인 사람 {sum(1 for x in rk if x <= 0.30) / len(rk) * 100:.0f}%), "
                  f"그 원정 출발 때 지갑 기준 중앙 {med(rs) * 100:.0f}%, 마무리 방문을 마친 뒤 중앙 {med(ra) * 100:.0f}%")
            nv = max(1, sum(vis)); KS = ("disp", "rr", "store", "bag", "bb"); npre = {k: sum(g["n_pre"][k] for g in gs) for k in KS}
            spre = {k: statistics.mean(g["sp_pre"][k] for g in gs) for k in KS}
            per = [sum(g["n_pre"].values()) / max(1, s["visits_pre"]) for g, s in zip(gs, stt)]
            print(f"[v3] 10층 전 골드 구매: 마을 방문당 {sum(npre.values()) / nv:.2f}회 (진열 {npre['disp'] / nv:.2f}, 새로고침 {npre['rr'] / nv:.2f}, 창고 칸 {npre['store'] / nv:.2f}, 가방 칸 {npre['bag'] / nv:.2f}, 되사기 칸 {npre['bb'] / nv:.2f}; "
                  f"사람별 중앙 {med(per):.2f}회, 0.5회 이상인 사람 {sum(1 for x in per if x >= 0.5) / len(per) * 100:.0f}%), "
                  f"사람당 지출 평균 진열 {spre['disp']:.0f} / 새로고침 {spre['rr']:.0f} / 창고 {spre['store']:.0f} / 가방 {spre['bag']:.0f} / 되사기 칸 {spre['bb']:.0f}")
            sv = [g["shop_open_gold"] for g in gs if g["shop_open_gold"]]
            if sv: print(f"[v3] 상점이 열린 방문(도착 중앙 {med(x[1] for x in sv):.0f}분)의 지갑 골드 중앙 {med(x[0] for x in sv):.0f} (빠른 쪽 10% {pct([x[0] for x in sv], .1):.0f}, 많은 쪽 90% {pct([x[0] for x in sv], .9):.0f})")
            bt = []
            for k, name in (("store1", "창고 25칸"), ("store2", "창고 30칸"), ("bag1", "가방 45칸"), ("bag2", "가방 50칸"), ("bb1", "되사기 12칸")):
                xs = [r[0]["G"]["buy_t"][k] for r in res if k in r[0]["G"]["buy_t"]]
                pre = sum(1 for r in res if k in r[0]["G"]["buy_t"] and r[0]["first10"] and r[0]["G"]["buy_t"][k] < r[0]["first10"][1])
                bt.append(f"{name} {med(xs):.0f}분 (10층 전 {pre / P * 100:.0f}%)" if xs else f"{name} 안 삼")
            print("[v3] 칸 늘리기를 산 마을 도착 시각 중앙: " + ", ".join(bt))
            post = [p for g in gs for p in g["post"][:10]]
            if post:
                pin = sum(p[0] for p in post); psp = sum(p[1] for p in post)
                last = [g["post"][min(9, len(g["post"]) - 1)][3] for g in gs if g["post"]]
                print(f"[v3] 10층 첫 처치 뒤 마을 방문(사람당 최대 10번): 방문당 들어온 골드 평균 {pin / len(post):.0f}, 쓴 골드 {psp / len(post):.0f} ({psp / max(1, pin) * 100:.0f}%), "
                      f"구매 {sum(p[2] for p in post) / len(post):.2f}회, 마지막 방문 뒤 남은 골드 중앙 {med(last):.0f}")
            if SELL != "0":
                ssp = [g["stone_sell_pre"] for g in gs]
                print(f"[v3] 팔기로 포기한 분해 강화석(10층 전) 평균 {statistics.mean(ssp):.0f}석 = 총수입의 평균 {statistics.mean(s / (s + t_) for s, t_ in zip(ssp, tot) if t_) * 100:.1f}%")
        qids = [q[0] for q in QTAB.get(QUEST, [])]
        if qids:
            print("[v2] 의뢰 완료 시점 (완료한 사람 중앙값, 10층 첫 처치 전 완료 비율)")
            for qid in qids:
                xs = [r[0]["stat"]["qtime"][qid] for r in res if qid in r[0]["stat"]["qtime"]]
                pre = sum(1 for r in res if qid in r[0]["stat"]["qtime"] and r[0]["stat"]["qtime"][qid][2])
                if xs: print(f"    {qid}: 원정 {med(x[0] for x in xs):.0f}, {med(x[1] for x in xs):.0f}분, 완료 {len(xs) / P * 100:.0f}%, 10층 전 {pre / P * 100:.0f}%")
        Ws = [r[0]["W"] for r in res]
        cand = [c for w in Ws for c in w["cand"]]; gw = [c for w in Ws for c in w["gwait"]]
        inh = [c for c in cand if c[2] == "inh"]
        print(f"[v2] 계승 대기(10층 첫 처치 전): 계승 후보를 주운 원정 {sum(w['cand_runs'] for w in Ws) / max(1, sum(w['runs'] for w in Ws)) * 100:.0f}%, 사람당 후보 {len(cand) / P:.1f}개 → 실제 계승 {len(inh) / max(1, len(cand)) * 100:.0f}%, "
              f"그냥 낌 {sum(1 for c in cand if c[2] == 'eq0') / max(1, len(cand)) * 100:.0f}%, 더 좋은 후보에 밀려 분해 {sum(1 for c in cand if c[2] == 'gone') / max(1, len(cand)) * 100:.0f}%")
        if inh:
            print(f"[v2] 계승된 후보가 가방에서 기다린 시간 평균 {statistics.mean(c[0] for c in inh):.1f}분 (중앙 {med(c[0] for c in inh):.1f}, 90% {pct([c[0] for c in inh], .9):.1f}), "
                  f"다음 원정으로 넘어간 비율 {sum(1 for c in inh if c[1] > 0) / len(inh) * 100:.1f}%, 기다린 원정 수 평균 {statistics.mean(c[1] for c in inh):.2f}, "
                  f"줍는 순간 '계승하면 - 지금 끼면' 차이 평균 {statistics.mean(c[3] for c in inh) * 100:.1f}%p, "
                  f"같은 원정에서 주운 뒤 더 내려간 층 평균 {statistics.mean(c[4] for c in inh if c[1] == 0):.2f}")
        if gw:
            print(f"[v2] +0으로 바꿔 낀 뒤(+2 이상 원본은 가방) 마을까지 평균 {statistics.mean(c[0] for c in gw):.1f}분, 사람당 {len(gw) / P:.1f}번")
        dun = sum(w["dun_time"] for w in Ws); wt = sum(w["wait_time"] for w in Ws)
        if wt:
            print(f"[v2] 던전 시간 중 계승 대기(+3% 이상 손해) 비율 {wt / dun * 100:.0f}% (사람당 10층 전 던전 {dun / P:.0f}분 중 {wt / P:.1f}분), 그동안 종합 손해 평균 {sum(w['gain_sum'] for w in Ws) / wt * 100:.1f}%, 능력 비율 R 손해 평균 {sum(w['rgap_sum'] for w in Ws) / wt * 100:.1f}%")
        ec = [n for w in Ws for n in w["endcand"]]
        print(f"[v2] 귀환할 때 가방의 계승 후보 수: 0개 {sum(1 for n in ec if n == 0) / len(ec) * 100:.0f}%, 1개 {sum(1 for n in ec if n == 1) / len(ec) * 100:.0f}%, 2개 이상 {sum(1 for n in ec if n >= 2) / len(ec) * 100:.0f}%")
        fi = [r[0]["firstinh"] for r in res if r[0]["firstinh"]]
        if fi: print(f"[v2] 첫 마을 계승 원정 중앙 {med(x[0] for x in fi):.0f}, {med(x[1] for x in fi):.0f}분. 10층 전 마을 방문당 계승 {sum(s['inh_pre'] for s in stt) / max(1, sum(vis)):.2f}회")
    RAT = lambda f: 16 * RAT_G ** (f - 1)
    print("\n| 층 | 첫 진입 원정 중앙 | 첫 진입 분 중앙 | R 중앙 (10%/90%) | 공격력 중앙 (하위10%) | 굴쥐 체력(10배 전) | 장검 중앙 굴쥐 1타 | 장검 하위10% | 쌍검 중앙(55%x2) | 체력 중앙 | 방어 중앙 |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")
    for f in range(1, 11):
        es = [r[1][f] for r in res if f in r[1]]
        if not es: continue
        atks = [e["atk"] for e in es]; Rs = [e["R"] for e in es]
        hp = round(RAT(f) * 10)
        print(f"| {f} | {med(e['run'] for e in es):.0f} | {med(e['time'] for e in es):.0f} | {med(Rs):.2f} ({pct(Rs, .1):.2f}/{pct(Rs, .9):.2f}) | {med(atks):.0f} ({pct(atks, .1):.0f}) | {hp / 10:.1f} | "
              f"{p1(med(atks) * 10, 1.0, hp) * 100:.0f}% | {p1(pct(atks, .1) * 10, 1.0, hp) * 100:.0f}% | {p1(med(atks) * 10, 0.55, hp, 2) * 100:.0f}% | {med(e['hp'] for e in es):.0f} | {med(e['def_'] for e in es):.0f} |")


if __name__ == "__main__":
    main()
