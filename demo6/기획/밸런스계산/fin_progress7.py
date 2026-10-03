# -*- coding: utf-8 -*-
"""7칸 진행 시뮬레이션 (장비-능력치-파밍-1차.md 12장 단계 5). 2차 fin_progress.py(v3 기본값 = 2차 최종 채택안)를 복사해 고쳤다.
고친 곳에는 '# 7칸:' 주석을 달았다. 2차 규칙(강화표·마을 체류·의뢰·상점·골드·성장 곡선·보스 승률·사망 확률·드랍 개수)은 그대로다.
7칸 변경 (장비 문서 2-2·4장·5장·6장·7장·8장)
 - 장착 8자리: 무기·갑옷·투구·장갑·장화·반지 1·반지 2·목걸이(4-1). 시작은 일반 장검 + 가죽 갑옷·가죽 두건·가죽 장갑·가죽 장화 iLv1,
   반지 두 자리와 목걸이는 빈 자리(4-4).
 - 기본 장비 21종(4-3 1단계 값·종류 고유). 기본 능력치 = 1단계 값 × iLv 배율 × 등급 배율 × 굴림 × (1 + 누적 강화)를
   10배 단위에서 0.5 올림 반올림(GearMath.BaseStat). 2차 갑옷 한 벌을 몫 500/200/150/150으로 나눈 값이다.
 - A = 공격력 × (1 + 치명 × (치명 피해 − 1)) × [0.65 × 콤보 계수 × (1 + 공격 속도) ÷ 1.49 + 0.35 × (1 + 스킬 피해) ÷ (1 − 재사용 감소)]
       × (1 + 0.2 × 보스 피해)   (2-2). 콤보 계수 장검 1.49, 대검 1.45, 쌍검 1.59. 무기 고유 치명 장검 +20‰/+100‰, 대검 0/+500‰, 쌍검 +40‰/−200‰.
   S·M·종합 변화율은 2차 그대로.
 - 옵션 풀 32줄(5-3, 부위 전용, 고정값 옵션은 × iLv 배율). 드랍 부위 = 부위 ‰ 340 / 105·75·75·75 / 180·150(8-1, 묶음 340/330/330).
   희귀 이상은 부위 점수(2차 6-6 장비 점수, 빈 자리 0, 반지는 두 자리 중 약한 쪽)가 가장 낮은 2부위만 가중치 ×3(8-2).
 - 전설은 효과 1/3 → 효과의 부위 묶음 비율(6장: 번개 무기 700·장갑 300, 발자국 장화 600·갑옷 400, 폭발 목걸이 500·투구 300·반지 200).
   10층 보스 첫 처치 보장 전설은 '아직 없는 효과'를 먼저 고르고 그 효과의 부위 가운데 점수가 가장 낮은 부위. 효과는 두 값 점수에 넣지 않는다.
 - 1~4층 빈 자리 채우기(8-2): 그 층 방문에서 보장 상자가 아닌 첫 상자(시뮬레이션의 계단방 상자, 없으면 그 층 첫 장비) 1개는 빈 자리가 있으면
   빈 부위 가운데서(부위 비율대로) 나온다. 3층 첫 상자 보장 = 영웅 이상, 무기·갑옷 가운데 점수가 낮은 쪽. 1층 첫 상자 = 희귀 이상 무기(그대로).
 - 상점 진열·weak_slot 은 묶음 3칸(무기·방어구·장신구)으로 두고, 묶음 칸에는 그 묶음에서 부위 점수가 가장 낮은 부위(같으면 몫이 큰 부위)(8-3).
   대장간 추천은 부위마다 최선 짝 1개(최대 7줄, 8-4 '빼지 않는다').
 - 강화 시도 비용·계승 비용·환급 × 부위 비용 배율(8-4. 무기 1000·갑옷 500·투구 200·장갑 150·장화 150·반지 300·목걸이 400‰,
   시도·계승은 반올림 최소 1석, 환급은 최소 0). 분해 기본값(1/2/6/15/40)은 등급만.
 - G 장착·계승 후보·대장간 추천 문턱 = 부위 문턱(8-5): 무기 30‰, 갑옷 20‰, 나머지 10‰. 2차의 +3%를 바꿈.
 - 반지 2자리(8-6): G는 빈 반지 자리부터(반지 1 → 반지 2), 둘 다 차면 종합 변화가 큰 자리, 같으면 반지 1. 계승은 반지끼리(자리 무관),
   원본이 끼고 있던 반지면 결과 반지가 그 자리로 들어간다.
 - 상한(10배 전 단위): 공속 .3, 이동 ±.3, 재사용 .4, 치명 .5, 치명 피해 3.0, 방어 400(받는 피해 −80%), 흡수 .03.
가정 (문서에 숫자가 없어 정한 것. 보고에 적음)
 - 상점 구매·진열 새로고침의 '+5% 이상 오를 부위' 문턱은 부위 문턱에 비례: +5% × 부위 문턱 ÷ 30‰(무기 5%, 갑옷 3.3%, 나머지 1.7%). SHOP_TH=flat 이면 모두 5%.
 - 옵션 단계(Ⅰ~Ⅳ)는 굴린 값의 1/4 구간. 장비 점수의 품질 = (단계 − 1) ÷ 3(GearMath.ItemScore).
 - 강화 고르기의 단계 기대 비용 = 2차 EA × (부위 시도 비용 ÷ 무기 시도 비용).
환경 변수 (2차 것은 그대로. V1=1·INH=naive 는 7칸판에서 지원하지 않는다)
 - DROP_MULT=1.0|1.2: 장비 총량 배율(결정 1). 몬스터 드랍 확률 × 배율, 정예·상자·보스 장비는 (배율 − 1) 확률로 1개 더. 1.0 이면 난수를 더 쓰지 않는다.
 - RNORM=own|v2: 능력 비율 R 의 기준 S0. own(기본) = 7칸 시작 상태로 나눔(장비 문서 3-2 '시작 R은 그대로'),
   v2 = 2차 시작 A(치명 5%/150%)로 나눔(몬스터가 2차 시작 상태 기준 그대로라고 볼 때. 민감도용).
 - SHOP_TH=scaled|flat: 위 가정.
 - 13장 위험 1 손잡이: CRIT_GLV=lo,hi / CRIT_RING=lo,hi (‰, 장갑·반지 치명 확률 옵션 범위, 기본 20,40 / 10,20),
   WPN_CRIT=장검,대검,쌍검 / WPN_CD=장검,대검,쌍검 (‰, 무기 종류 고유, 기본 20,0,40 / 100,500,-200).
 - METRICS_JSON=1: 마지막 줄에 비교용 지표 JSON(#METRICS7 …). fin_variants.py fin7 묶음이 읽는다.
 - THRESH_PM=무기,갑옷,투구,장갑,장화,반지,목걸이 (‰, 기본 30,20,10,10,10,10,10): 부위 문턱 후보 시험용.
 - 진단용(원인 나눠 보기, 기획 값 아님): THRESH_FLAT=0.03(모든 부위 문턱을 2차 +3%로), NO_FILL=1(빈 자리 채우기 끔), OPT_MULT=배율(모든 옵션 값).
실행: python fin_progress7.py [인원=1500] [체력성장=1.25] [정책 c|g] [10층 R50=1.05] [5층 R50=0.95] [공격성장=1.20]
  기획 값: python fin_progress7.py 2000 1.27 c 1.00 0.95 1.24          (드랍 ×1.0)
          DROP_MULT=1.2 python fin_progress7.py 2000 1.27 c 1.00 0.95 1.24   (드랍 ×1.2)
"""
import random, statistics, math, sys, io, os, json
if not isinstance(sys.stdout, io.TextIOWrapper) or (sys.stdout.encoding or "").lower() != "utf-8":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
P = int(sys.argv[1]) if len(sys.argv) > 1 else 1500
HP_G = float(sys.argv[2]) if len(sys.argv) > 2 else 1.25
POL = sys.argv[3] if len(sys.argv) > 3 else "c"
R50_10 = float(sys.argv[4]) if len(sys.argv) > 4 else 1.05
R50_5 = float(sys.argv[5]) if len(sys.argv) > 5 else 0.95
ATK_G = float(sys.argv[6]) if len(sys.argv) > 6 else 1.20
RAT_G = float(os.environ.get("RAT_G", "1.25"))
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
GT = {1:[82,16,2,0,0],2:[74,21,5,0,0],3:[66,25,8,1,0],4:[58,28,12,2,0],5:[50,30,15.5,4,.5],6:[44,31,18,6,1],7:[38,32,21,7.5,1.5],8:[32,33,24,9,2],9:[27,33,26,11.5,2.5],10:[22,33,28,14,3]}
BOSS_T = {5: (3, [0,45,40,13,2]), 10: (4, [0,20,45,30,5])}; LEG_PITY = 80
FLOORS = {1:(30,2,0,0),2:(28,8,0,0),3:(25,3,8,0),4:(28,8,8,1),5:(14,4,6,0),6:(28,8,12,0),7:(28,12,12,1),8:(32,12,12,0),9:(32,12,12,2),10:(16,6,6,0)}
FLOOR_MIN = {1:3,2:3,3:3,4:3,5:3.5,6:4.5,7:4.5,8:4.5,9:4.5,10:4.5}
BOSS_MIN = {5: 0.5, 10: 1.2}; TOWN_MIN = 3
DROP = dict(rat=0.06, boar=0.18, archer=0.12); STONE = dict(rat=0.05, boar=0.15, archer=0.10, elite=3.0)
def chest_stone(f): return 2 + f
BOSS_STONE = {5: 15, 10: 40}


# ---------------------------------------------------------------- 7칸: 부위·기본 장비 21종·옵션 풀 32줄 (장비 문서 4장·5장)
def _permille_env(name, default):   # 7칸: '‰,‰,…' 환경 변수 → 비율 튜플
    return tuple(float(x) / 1000 for x in os.environ.get(name, default).split(","))


DROP_MULT = float(os.environ.get("DROP_MULT", "1.0"))   # 7칸: 장비 총량 배율(결정 1: ×1.0으로 시작, 기록 뒤 ×1.2)
RNORM = os.environ.get("RNORM", "own")                   # 7칸: R 기준 S0 (own = 7칸 시작, v2 = 2차 시작 A)
SHOP_TH = os.environ.get("SHOP_TH", "scaled")            # 7칸: 상점·새로고침 문턱 (가정)
PARTS = ("무기", "갑옷", "투구", "장갑", "장화", "반지", "목걸이")
SLOT_NAMES = ("무기", "갑옷", "투구", "장갑", "장화", "반지1", "반지2", "목걸이")
SLOT_PART = (0, 1, 2, 3, 4, 5, 5, 6)                     # 8자리 → 부위
PART_SLOTS = ((0,), (1,), (2,), (3,), (4,), (5, 6), (7,))
GROUP_PARTS = ((0,), (1, 2, 3, 4), (5, 6))               # 묶음: 무기 · 방어구 · 장신구
PART_DROP = (340, 105, 75, 75, 75, 180, 150)             # 8-1 부위 ‰
COST_PM = (1000, 500, 200, 150, 150, 300, 400)           # 8-4 부위 비용 배율 ‰
THRESH = (.03, .02, .01, .01, .01, .01, .01)             # 8-5 부위 문턱
if os.environ.get("THRESH_PM"): THRESH = _permille_env("THRESH_PM", "30,20,10,10,10,10,10")   # 7칸 손잡이: 부위 문턱(‰, 부위 차례)
if os.environ.get("THRESH_FLAT"): THRESH = (float(os.environ["THRESH_FLAT"]),) * 7   # 7칸 진단: 모든 부위 같은 문턱(2차 = 0.03)
NO_FILL = os.environ.get("NO_FILL") == "1"               # 7칸 진단: 빈 자리 채우기 끔
OPT_MULT = float(os.environ.get("OPT_MULT", "1"))        # 7칸 진단: 모든 옵션 값 배율
_wc = _permille_env("WPN_CRIT", "20,0,40"); _wd = _permille_env("WPN_CD", "100,500,-200")
WEAPON = {"장검": 1.49, "대검": 1.45, "쌍검": 1.59}       # 2-2 콤보 계수(치명 뺌)
# 부위마다 {종류: (1단계 기본 능력치(10배 단위 정수), 종류 고유(10배 전 단위))}. 4-3 표.
KINDS = (
    {"장검": (dict(atk=100), dict(crit=_wc[0], critdmg=_wd[0])), "대검": (dict(atk=100), dict(crit=_wc[1], critdmg=_wd[1])),
     "쌍검": (dict(atk=100), dict(crit=_wc[2], critdmg=_wd[2]))},
    {"가죽 갑옷": (dict(def_=60, hp=200), dict(move=.03)), "사슬 갑옷": (dict(def_=80, hp=300), {}), "판금 갑옷": (dict(def_=120, hp=200), dict(move=-.03))},
    {"가죽 두건": (dict(def_=24, hp=80), {}), "사슬 두건": (dict(def_=32, hp=120), dict(cdr=.03)), "판금 투구": (dict(def_=48, hp=80), dict(boss=.05))},
    {"가죽 장갑": (dict(def_=18, hp=60), {}), "사슬 장갑": (dict(def_=24, hp=90), dict(aspd=.03)), "판금 장갑": (dict(def_=36, hp=60), dict(critdmg=.10))},
    {"가죽 장화": (dict(def_=18, hp=60), dict(move=.03)), "사슬 장화": (dict(def_=24, hp=90), {}), "판금 장화": (dict(def_=36, hp=60), dict(move=-.03))},
    {"쇠 반지": (dict(atk=25), {}), "핏빛 반지": (dict(atk=20), dict(crit=.02)), "송곳 반지": (dict(atk=20), dict(critdmg=.15))},
    {"이빨 목걸이": (dict(hp=200), dict(cdr=.05)), "부적 목걸이": (dict(hp=200), dict(move=.04)), "검은 호박 목걸이": (dict(hp=200), dict(skill=.10))},
)
KIND_LIST = tuple(tuple(k.keys()) for k in KINDS)
_cg = _permille_env("CRIT_GLV", "20,40"); _cr = _permille_env("CRIT_RING", "10,20")
# 5-3 옵션 풀 (종류, 아래, 위, 고정값이면 1(× iLv 배율), 가중치). 10배 전 단위(2차 POOL 과 같은 단위).
POOL = (
    [("atk",2,4,1,10),("atk_pct",.04,.08,0,10),("crit",.02,.04,0,8),("critdmg",.10,.20,0,8),("aspd",.04,.08,0,8),("boss",.06,.12,0,5),("leech",.004,.008,0,4),("onkill",2,4,1,5)],
    [("hp",6,12,1,10),("hp_pct",.04,.08,0,8),("def",3,6,1,10),("onkill",1,2,1,5)],
    [("hp",3,6,1,10),("cdr",.02,.04,0,8),("skill",.03,.06,0,8),("onkill",1,2,1,5)],
    [("aspd",.03,.06,0,8),("crit",_cg[0],_cg[1],0,8),("atk",1,2,1,8),("leech",.002,.004,0,4)],
    [("move",.04,.08,0,10),("hp",3,6,1,10),("def",1.5,3,1,8),("regen",.6,1.2,1,5)],
    [("atk",1,2,1,10),("atk_pct",.015,.03,0,8),("crit",_cr[0],_cr[1],0,8),("hp",5,10,1,6)],
    [("critdmg",.10,.20,0,8),("cdr",.03,.06,0,6),("skill",.03,.06,0,6),("hp",5,10,1,8)],
)
LEG_NAMES = ("연쇄 번개", "불꽃 발자국", "연쇄 폭발")
LEG_PARTS = (((0, 700), (3, 300)), ((4, 600), (1, 400)), ((6, 500), (2, 300), (5, 200)))   # 6장 효과 → 부위 묶음
SK = ("atk", "atk_pct", "hp", "hp_pct", "def_", "crit", "critdmg", "aspd", "move", "cdr", "skill", "boss", "leech", "regen", "onkill")
IX = {k: i for i, k in enumerate(SK)}; IX["def"] = IX["def_"]
NS = len(SK)


def rh(x): return int(math.floor(x + 0.5))   # 7칸: 0.5 올림 반올림(GearMath)
def enh_cost(part, t): return max(1, (S[t][4] * COST_PM[part] + 500) // 1000)          # 7칸: 시도 비용 × 부위 배율, 최소 1석
def inh_cost(part, k): return max(1, (INHERIT[k] * COST_PM[part] + 500) // 1000) if k > 0 else 0   # 7칸: 계승 비용 × 부위 배율, 최소 1석
def refund(part, k): return (REFUND[k] * COST_PM[part] + 500) // 1000                    # 7칸: 환급 × 부위 배율(최소 0)
EA_P = [{t: EA[t] * enh_cost(p, t) / S[t][4] for t in range(1, 16)} for p in range(7)]   # 7칸: 단계 기대 비용(가정)
def shop_th(part): return 0.05 if SHOP_TH == "flat" else 0.05 * THRESH[part] / 0.03    # 7칸: 상점 문턱(가정)

# ---------------------------------------------------------------- v2: 변형 스위치 (기본값 = v2 채택안)
V1 = os.environ.get("V1") == "1"
if V1: raise SystemExit("fin_progress7.py: V1(1차 규칙)은 7칸판에서 지원하지 않는다")   # 7칸
TOWN = os.environ.get("TOWN", "final")
QUEST = os.environ.get("QUEST", "final")
SHOP = os.environ.get("SHOP", "1") == "1"
SHOP_RARE = float(os.environ.get("SHOP_RARE", "0.30"))
SHOP_ILV = int(os.environ.get("SHOP_ILV", "-1"))
GOLD = os.environ.get("GOLD", "1") == "1"
GUAR = os.environ.get("GUAR", "0") == "1"
QSCALE = float(os.environ.get("QSCALE", "1"))
INH = os.environ.get("INH", "best")
if INH != "best": raise SystemExit("fin_progress7.py: INH=naive 는 7칸판에서 지원하지 않는다")   # 7칸
QGOLD = float(os.environ.get("QGOLD", "10"))
GSPEND = os.environ.get("GSPEND", "base")
REROLL = int(os.environ.get("REROLL", "2"))
CASUAL = float(os.environ.get("CASUAL", "3"))
RR_BASE = float(os.environ.get("RR_BASE", "30")); RR_ILV = float(os.environ.get("RR_ILV", "10"))
STORE_STEPS = [(25, float(os.environ.get("STORE1", "400"))), (30, float(os.environ.get("STORE2", "800")))]
BAG_STEPS = [(45, float(os.environ.get("BAG1", "600"))), (50, float(os.environ.get("BAG2", "1200")))]
BUYBACK_UP = float(os.environ.get("BUYBACK_UP", "300"))
SELL = os.environ.get("SELL", "0")
SELL_RATE = float(os.environ.get("SELL_RATE", "10"))
GSCALE = float(os.environ.get("GSCALE", "1"))
if not GOLD:
    SELL = "0"


def sell_ok(g):
    return (SELL == "1" and g <= 1) or (SELL == "2" and g <= 3) or (SELL == "3" and g == 0)


def town_minutes(visit, retry):
    if TOWN == "final":
        if visit <= 3: return 4.0
        return 3.0 if retry else 4.5
    if TOWN == "sched":
        if visit == 1: return 6.0
        if visit <= 3: return 5.0
        return 3.0 if retry else 4.0
    return float(TOWN)


def C(f): return 2 + f
_g_el = (3, 9) if GUAR else (0, 0); _g_sv = (2, 8) if GUAR else (0, 0)
QTAB = {
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
    "fac": [("Q01", "visit", 1, "town1", 1, 3, 0, 0, 0), ("Q02", "start", 0, "reach", 2, 4, 0, 0, 0),
            ("Q03", "start", 0, "reach", 3, 10, 0, 0, 0), ("Q04", "start", 0, "elite", (4, 1), 6, 0, 0, 0),
            ("Q05", "start", 0, "boss", 5, 21, 5, 0, 0), ("Q06", "start", 0, "reach", 6, 8, 0, 0, 0),
            ("Q07", "start", 0, "elite", (7, 1), 18, 0, 0, 0), ("Q08", "start", 0, "reach", 8, 10, 0, 0, 0),
            ("Q09", "start", 0, "elite", (9, 2), 11, 0, 0, 0), ("Q10", "start", 0, "boss", 10, 36, 10, 0, 0)],
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
QTAB["small"] = [q for q in QTAB["final"] if not q[0].startswith("q.merchant_")]
if os.environ.get("V2VALS"):
    _vals = [float(x) for x in os.environ["V2VALS"].split(",")]
    QTAB["v2"] = [q[:5] + (v,) + q[6:] for q, v in zip(QTAB["v2"], _vals)]
_BOUNTY_DEFAULT = {"town": "C", "v2": "2", "fac": "0", "0": "0", "final": "0", "small": "0"}
BOUNTY = os.environ.get("BOUNTY", _BOUNTY_DEFAULT.get(QUEST, "0"))
BOUNTY_KILLS = 60
BOUNTY_MAX = int(os.environ.get("BOUNTY_MAX", "6" if QUEST == "v2" else "0"))
SHOP_UNLOCK = os.environ.get("SHOP_UNLOCK", "reach3" if QUEST == "fac" else ("maxf7" if QUEST in ("final", "small", "0") else "maxf4"))
GPILE = float(os.environ.get("GPILE", "3"))
GOLD_KILL = dict(rat=0.25 * GPILE * GSCALE, archer=0.50 * GPILE * GSCALE, boar=0.75 * GPILE * GSCALE); GOLD_ELITE = 5 * GPILE * GSCALE
GOLD_BOSS = {5: float(os.environ.get("GBOSS5", "60")) * GSCALE, 10: float(os.environ.get("GBOSS10", "150")) * GSCALE}
GCHEST = (float(os.environ.get("GCHEST0", "8")), float(os.environ.get("GCHEST1", "4")))
def gold_chest(f): return (GCHEST[0] + GCHEST[1] * f) * GSCALE
TAG_FLOORS = (1, 2, 3, 4, 6, 7, 8, 9)
def reroll_price(il, k): return (RR_BASE + RR_ILV * il) * (2 ** (k - 1))
def shop_price(g, il): return ((6 + 2 * il) if g == 1 else (18 + 4 * il)) * (10 if GOLD else 1)


def bounty_reward(base_floor):
    if BOUNTY == "0": return 0.0
    if BOUNTY == "C": return float(C(max(1, base_floor)))
    return float(BOUNTY)


class Item:   # 7칸: 부위·종류·옵션(종류, 값, 단계)·전설 효과
    __slots__ = ("part", "kind", "g", "ilv", "roll", "opts", "leg", "lv", "best", "gauge", "streak", "cache")
    def __init__(s, part, kind, g, ilv, roll, opts, leg=None):
        s.part, s.kind, s.g, s.ilv, s.roll, s.opts, s.leg = part, kind, g, ilv, roll, opts, leg
        s.lv = 0; s.best = 0; s.gauge = {}; s.streak = 0; s.cache = {}


def base_scale(it, lv): return ilv_mult(it.ilv) * GMULT[it.g] * it.roll * (1 + CUM[lv] / 100)


def contrib(it, lv):
    """7칸: 장비 하나가 강화 단계 lv 에서 더하는 능력치(10배 전 단위, SK 차례). 기본 능력치는 10배 단위에서 반올림."""
    c = it.cache.get(lv)
    if c is None:
        v = [0.0] * NS; sc = base_scale(it, lv); base, uniq = KINDS[it.part][it.kind]
        for k, x in base.items(): v[IX[k]] += rh(x * sc) / 10
        for k, x in uniq.items(): v[IX[k]] += x
        for k, x, _tier in it.opts: v[IX[k]] += x
        c = it.cache[lv] = tuple(v)
    return c


def stats(eq, lvo=None):
    """7칸: 장착 8자리 → A·S·M 과 능력치. lvo = {자리: 강화 단계} 가정."""
    t = [0.0] * NS
    for i, it in enumerate(eq):
        if it is None: continue
        c = contrib(it, lvo[i] if (lvo and i in lvo) else it.lv)
        t = [a + b for a, b in zip(t, c)]
    atk = (10 + t[0]) * (1 + t[1]); hp = (200 + t[2]) * (1 + t[3]); df = t[4]
    crit = min(.5, .05 + t[5]); cd = min(3.0, 1.5 + t[6]); aspd = min(.3, t[7]); move = max(-.3, min(.3, t[8])); cdr = min(.4, t[9])
    skill, boss = t[10], t[11]
    coef = WEAPON[eq[0].kind]
    cm = 1 + crit * (cd - 1)
    br = 0.65 * coef * (1 + aspd) / 1.49 + 0.35 * (1 + skill) / (1 - cdr)
    A = atk * cm * br * (1 + 0.2 * boss)
    B = atk * cm * br * (1 + boss)   # 7칸: 보스전 초당 피해 지수(보스 피해를 다 받음)
    Sv = hp * (1 + min(df, 400) / 100) + min(.03, t[12]) * A * 10 + t[13] * 10 + t[14] * 5
    return dict(A=A, S=Sv, M=1 + move, atk=atk, hp=hp, def_=df, crit=crit, cd=cd, cm=cm, aspd=aspd, cdr=cdr, skill=skill, boss=boss, B=B,
                move=move, kind=eq[0].kind)


def composite(s0, s1): return 0.6 * (s1["A"] / s0["A"] - 1) + 0.3 * (s1["S"] / s0["S"] - 1) + 0.1 * (s1["M"] / s0["M"] - 1)


def roll_opts(rng, part, g, ilv):   # 7칸: 부위 풀, 단계(굴린 값의 1/4 구간) 함께 저장
    out = []; pool = list(POOL[part])
    for _ in range(OPTN[g]):
        w = sum(p[4] for p in pool); r = rng.random() * w; acc = 0
        for i, p in enumerate(pool):
            acc += p[4]
            if r < acc: break
        k, lo, hi, flat, _w = pool.pop(i)
        raw = rng.uniform(lo, hi)
        tier = min(4, int((raw - lo) / (hi - lo) * 4) + 1) if hi > lo else 4
        out.append((k, raw * OPTM[g] * (ilv_mult(ilv) if flat else 1) * OPT_MULT, tier))
    return out


def make_item(rng, part, g, ilv):
    return Item(part, rng.choice(KIND_LIST[part]), g, ilv, rng.uniform(.9, 1.1), roll_opts(rng, part, g, ilv))


def roll_grade(rng, w):
    r = rng.random() * sum(w); acc = 0
    for i, x in enumerate(w):
        acc += x
        if r < acc: return i
    return 0


def item_score(it):   # 7칸: 2차 6-6 장비 점수(GearMath.ItemScore). 빈 자리 0
    if it is None: return 0.0
    return 100 * ilv_mult(it.ilv) * GMULT[it.g] * (1 + CUM[it.lv] / 100) + sum(10 + 10 * (o[2] - 1) / 3 for o in it.opts) + (50 if it.leg is not None else 0)


def part_score(eq, p): return min(item_score(eq[s]) for s in PART_SLOTS[p])   # 7칸: 반지는 두 자리 중 약한 쪽


def weakest(eq, parts, n):   # 7칸: 부위 점수가 가장 낮은 n부위(같으면 부위 ‰가 큰 쪽, 그다음 앞 차례) = LootRules.WeakestParts
    return sorted(parts, key=lambda p: (part_score(eq, p), -PART_DROP[p], p))[:n]


def pick_part(rng, g, eq, only=None):   # 7칸: 부위 ‰(8-1) + 희귀 이상이면 약한 2부위 ×3(8-2)
    w = [PART_DROP[p] if (only is None or p in only) else 0 for p in range(7)]
    if g >= 2:
        for p in weakest(eq, range(7), 2): w[p] *= 3
    return roll_grade(rng, w)


def pick_leg(rng, eq, owned, only=None, guar=False):   # 7칸: 전설 = 효과 1/3 → 효과의 부위 묶음 비율(6장)
    effs = [e for e in range(3) if only is None or any(p in only for p, _ in LEG_PARTS[e])]
    if guar:
        new = [e for e in effs if e not in owned]
        if new: effs = new
    e = effs[rng.randrange(len(effs))]
    cand = [(p, w) for p, w in LEG_PARTS[e] if only is None or p in only]
    if guar: return e, weakest(eq, [p for p, _ in cand], 1)[0]
    return e, cand[roll_grade(rng, [w for _, w in cand])][0]


def new_item(rng, g, il, eq, owned=(), forced=None, only=None, guar=False):
    """7칸: 굴리는 순서(8-1) 등급 → [전설이면 효과 → 부위] 아니면 부위 → 종류 1/3 → 굴림 → 옵션."""
    leg = None
    if g == 4: leg, part = pick_leg(rng, eq, owned, (forced,) if forced is not None else only, guar)
    elif forced is not None: part = forced
    else: part = pick_part(rng, g, eq, only)
    it = make_item(rng, part, g, il); it.leg = leg
    return it


def slot_of(eq, it):
    for i, x in enumerate(eq):
        if x is it: return i
    return None


def g_slot(eq, it):   # 7칸: G 대상 자리(8-6). 반지는 빈 자리부터(1 → 2), 둘 다 차면 종합 변화가 큰 자리, 같으면 반지 1
    slots = PART_SLOTS[it.part]
    if len(slots) == 1: return slots[0]
    for s in slots:
        if eq[s] is None: return s
    s0 = stats(eq); best = None; bv = None
    for s in slots:
        trial = list(eq); trial[s] = it; v = composite(s0, stats(trial))
        if bv is None or v > bv: best, bv = s, v
    return best


START_ITEMS = lambda: [Item(0, "장검", 0, 1, 1.0, []), Item(1, "가죽 갑옷", 0, 1, 1.0, []), Item(2, "가죽 두건", 0, 1, 1.0, []),
                       Item(3, "가죽 장갑", 0, 1, 1.0, []), Item(4, "가죽 장화", 0, 1, 1.0, []), None, None, None]   # 7칸: 4-4
S0 = stats(START_ITEMS())
# 7칸: 2차 시작 상태(장검 + 가죽 갑옷, 치명 5%/150%, 휘두르기 1.5 × 1타 1.0 ÷ 1.5) — 2차 fin_progress.S0 와 같은 식
S0_V2 = dict(A=20 * (1 + .05 * .5) * (0.65 * 1.5 * 1.0 / 1.5 + 0.35), S=240 * (1 + 12 / 100), M=1.06)
S0R = dict(S0, A=S0_V2["A"]) if RNORM == "v2" else S0
def hpM(f): return HP_G ** (f - 1)
def atkM(f): return ATK_G ** (f - 1)
def ratio(st, f): return (st["A"] / S0R["A"] / hpM(f)) ** 0.6 * (st["S"] / S0R["S"] / atkM(f)) ** 0.4
def p_death(R): return min(0.9, 0.01 + 0.5 / (1 + math.exp((R - 0.85) / 0.05)))


def p_boss_win(R, f):
    r50 = R50_10 if f == 10 else R50_5
    return 1 / (1 + math.exp(-(R - r50) / 0.07))


CONT, CONT_BOSS = (0.90, 0.95) if POL == "c" else (0.80, 0.80)


def try_enhance(rng, it, wallet, part):
    t = it.lv + 1; wallet[0] -= enh_cost(part, t); f = it.gauge.get(t, 0)   # 7칸: 시도 비용 × 부위 배율
    _, base, inc, ceil_, *_ = S[t]
    p = 100 if ((ceil_ and f >= ceil_) or it.streak >= STREAK) else min(100, base + inc * f + (BONUS if t <= it.best else 0))
    if rng.random() * 100 < p:
        it.lv = t; it.gauge[t] = 0; it.streak = 0; nb = t > it.best; it.best = max(it.best, t)
        return True, nb
    it.gauge[t] = f + 1
    if it.lv > SAFE:
        it.lv -= 1; it.streak += 1
    return False, False


def best_pair(eq, bag, part, wallet=None, targets=None):
    """7칸: 대장간 추천 짝(부위마다). (대상, 원본, 결과 단계, 놓을 자리, 비울 자리)의 종합 변화율이 가장 큰 경우.
    원본이 None 이면 가방 장비를 지금 단계 그대로 끼는 경우. wallet=None 이면 비용 무시. 문턱 = 부위 문턱(8-5).
    반지: 원본이 끼고 있던 반지면 결과 반지가 그 자리로, 아니면 대상 자리(가방이면 두 자리 중 나은 쪽)."""
    slots = PART_SLOTS[part]
    items = [x for x in bag if x.part == part]
    for s in slots:
        if eq[s] is not None: items.append(eq[s])
    srcs = [x for x in items if x.lv >= 2]
    base = stats(eq); best = None; bestv = THRESH[part]
    for tg in (targets if targets is not None else items):
        tsl = slot_of(eq, tg)
        places = (tsl,) if tsl is not None else slots
        if tsl is None and targets is None:
            for P_ in places:
                trial = list(eq); trial[P_] = tg
                v = composite(base, stats(trial))
                if v >= bestv: best, bestv = (tg, None, tg.lv, P_, None), v
        for s in srcs:
            if s is tg: continue
            kept = min(s.lv - 1, CAP[tg.g])
            if kept <= tg.lv: continue
            if wallet is not None and wallet[0] < inh_cost(part, kept): continue
            ssl = slot_of(eq, s)
            for P_ in ((ssl,) if ssl is not None else places):
                trial = list(eq); vac = None
                if tsl is not None and tsl != P_: trial[tsl] = None; vac = tsl
                trial[P_] = tg
                v = composite(base, stats(trial, {P_: kept}))
                if v >= bestv: best, bestv = (tg, s, kept, P_, vac), v
    return best, bestv


_PID = [0]


def play(rng, max_min=480, max_runs=45):
    _PID[0] += 1
    rng_shop = random.Random(9_000_000 + _PID[0]); rng_q = random.Random(7_000_000 + _PID[0])
    eq = START_ITEMS(); bag = []; wallet = [0.0]; gold = [0.0]
    t = 0.0; run = 0; unlocked6 = False; pity = 0; first_kill = {5: False, 10: False}
    WP = 0; BC = 0; chest_done = set()
    ev = dict(first5=None, first10=None, firstleg=None, firstepic=None, done=None, deaths=0, risk=None, drop=None,
              boss5_first=None, boss10_first=None, deaths_pre10=0, boss10_tries=0, firstinh=None,
              risk_wa=None, drop_wa=None, drop_small=None)   # 7칸: 무기·갑옷 기준 첫 +8 도전·첫 하락, 작은 칸 첫 하락
    entry = {}; log = []; notable = []; per_run = []
    # 7칸: 지표 — 10층 첫 처치 전 부위별 장비 수, 가방(가득 찬 채로 주운 원정, 원정 최대 칸, 보스 직전 칸), 빈 자리 채우기, 전설 부위
    K = dict(parts=[0] * 7, drops=0, legparts=[0] * 7, fill=0, runs=0, full_runs=0, full_picks=0, bagmax=[], bossbag={5: [], 10: []},
             notable_wa=[], empty_last=None, kept=[])
    SS = dict(visits=0, maxf=0, cleared=set(), clear_cnt={}, bosses=set(), elite={}, rats=0, arch3=0, boars=0, inh=0, b10try=0, b10loss=0)
    QL = QTAB.get(QUEST, []); qst = {q[0]: None for q in QL}; qsnap = {}
    bounty_on = False; shop_open = False; shop_items = set()
    stat = dict(q_pre=0.0, q_all=0.0, shop_pre=0.0, shop_buy_pre=0, inc_pre=0.0, town_min_pre=0.0, visits_pre=0, qtime={},
                bounty_pre=0.0, bounty_n_pre=0, gold_pre=0.0, inh_pre=0, q_60=0.0)
    TA = [0.0]
    G = dict(earn=0.0, src_pre=dict(field=0.0, quest=0.0, sell=0.0), lost_pre=0.0, spent=0.0,
             sp_pre=dict(disp=0.0, rr=0.0, store=0.0, bag=0.0, bb=0.0), n_pre=dict(disp=0, rr=0, store=0, bag=0, bb=0),
             kill=None, start=None, after=None, shop_open_gold=None, buy_t={}, post=[], stone_sell_pre=0.0)
    VG = [0.0, 0.0, 0, 0.0, 0.0]
    caps = dict(store=0, bag=0, bb=0)
    W = dict(cand=[], gwait=[], wait_time=0.0, dun_time=0.0, gain_sum=0.0, rgap_sum=0.0, endcand=[], cand_runs=0, runs=0)
    pend = []

    def owned():   # 7칸: 가진 전설 효과(장착·가방)
        return {x.leg for x in list(eq) + bag if x is not None and x.leg is not None}

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
        it = new_item(rng_q, g, il, eq)   # 7칸: 부위 ‰ + 순위 보정, 전설은 효과 → 부위
        if g == 4 and ev["firstleg"] is None: ev["firstleg"] = (run, TA[0])
        if g >= 3 and ev["firstepic"] is None: ev["firstepic"] = (run, TA[0])
        bag.append(it)

    def q_pay(q):
        qst[q[0]] = "done"; amt = q[5] * QSCALE
        wallet[0] += amt; stat["q_all"] += amt
        if ev["first10"] is None: stat["q_pre"] += amt
        if TA[0] <= 60: stat["q_60"] += amt
        stat["qtime"][q[0]] = (run, TA[0], ev["first10"] is None)
        if GOLD and QGOLD and amt:
            ga = amt * QGOLD; gold[0] += ga; G["earn"] += ga; VG[0] += ga; VG[3] += ga
            if ev["first10"] is None: G["src_pre"]["quest"] += ga; VG[4] += ga
        if q[6] or q[7]:
            give_box(q[6] or q[8], q[7], q[6] or q[8])
        return amt

    def g_spend(cat, amt):
        gold[0] -= amt; G["spent"] += amt; VG[1] += amt; VG[2] += 1
        if ev["first10"] is None:
            G["sp_pre"][cat] += amt; G["n_pre"][cat] += 1

    def cap_opts():
        opts = []
        if 5 in SS["bosses"] and caps["store"] < len(STORE_STEPS): opts.append(("store", STORE_STEPS[caps["store"]][1]))
        if SHOP and shop_open and caps["bag"] < len(BAG_STEPS): opts.append(("bag", BAG_STEPS[caps["bag"]][1]))
        if SHOP and shop_open and BUYBACK_UP and caps["bb"] < 1: opts.append(("bb", BUYBACK_UP))
        return opts

    def buy_caps():
        if GSPEND in ("save", "none", "smart0"): return
        while True:
            opts = [o for o in cap_opts() if o[1] <= gold[0]]
            if not opts: return
            k, p = min(opts, key=lambda o: o[1])
            g_spend(k, p); caps[k] += 1; G["buy_t"].setdefault(f"{k}{caps[k]}", TA[0])

    def reroll_ok(il, k):
        rp = reroll_price(il, k)
        if gold[0] < rp: return False
        if GSPEND == "always": return True
        smart = gold[0] >= rp + shop_price(2, il) and weak_slot(il)
        if GSPEND in ("smart", "smart0") or k > 1: return smart
        if GSPEND == "reserve":
            nxt = [o[1] for o in cap_opts()]
            return smart or gold[0] - rp >= (min(nxt) if nxt else shop_price(2, il))
        return smart or gold[0] >= CASUAL * rp

    def weak_slot(il):   # 7칸: 묶음 3칸, 묶음 칸 = 묶음 안 가장 약한 부위. 문턱 = shop_th(부위)
        s0 = stats(eq)
        for grp in range(3):
            part = weakest(eq, GROUP_PARTS[grp], 1)[0]
            cur0 = next((eq[s] for s in PART_SLOTS[part] if eq[s] is not None), None)
            kind = cur0.kind if cur0 is not None else KIND_LIST[part][0]
            h = Item(part, kind, 2, il, 1.1, []); sl = g_slot(eq, h); cur = eq[sl]; trial = list(eq); trial[sl] = h
            v = composite(s0, stats(trial)) if cur is not None else 1.0
            if cur is not None and cur.lv >= 2:
                v = max(v, composite(s0, stats(trial, {sl: min(cur.lv - 1, CAP[2])})))
            if v >= shop_th(part): return True
        return False

    def q_check(kinds_town):
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

    def do_enhance():   # 7칸: 8자리, 비용 × 부위 배율. 무기·갑옷 첫 +8 도전·첫 하락을 따로 기록
        nb_any = False; nb_wa = False; tries = 0
        while True:
            cands = [i for i, it in enumerate(eq) if it is not None and it.lv < CAP[it.g] and wallet[0] >= enh_cost(SLOT_PART[i], it.lv + 1)]
            if not cands: break
            s0 = stats(eq)
            i = max(cands, key=lambda i: composite(s0, stats(eq, {i: eq[i].lv + 1})) / EA_P[SLOT_PART[i]][eq[i].lv + 1])
            it = eq[i]; part = SLOT_PART[i]; blv = it.lv
            if blv >= 7 and ev["risk"] is None: ev["risk"] = t
            if blv >= 7 and part <= 1 and ev["risk_wa"] is None: ev["risk_wa"] = t
            ok, nb = try_enhance(rng, it, wallet, part); tries += 1
            if (not ok) and blv >= 8:
                if ev["drop"] is None: ev["drop"] = t
                if part <= 1 and ev["drop_wa"] is None: ev["drop_wa"] = t
                if part >= 2 and ev["drop_small"] is None: ev["drop_small"] = t
            nb_any = nb_any or nb; nb_wa = nb_wa or (nb and part <= 1)
        return tries, nb_any, nb_wa

    def measure_wait(f, dt):
        if ev["first10"] is not None: return
        W["dun_time"] += dt
        live = []
        for e in pend:
            if e["kind"] == "cand" and e["it"] in bag: live.append(e)
            elif e["kind"] == "g" and slot_of(eq, e["it"]) is not None: live.append(e)
        if not live: return
        base = stats(eq); eq_h = list(eq); lvo = {}
        for part in set(e["it"].part for e in live):
            tg_list = [e["it"] for e in live if e["it"].part == part]
            res, v = best_pair(eq, bag, part, None, tg_list)
            if res is not None:
                tg, _src, kept, P_, vac = res
                if vac is not None: eq_h[vac] = None
                eq_h[P_] = tg; lvo[P_] = kept
        if not lvo: return
        sh = stats(eq_h, lvo); gain = composite(base, sh)
        if gain < 0.03: return
        W["wait_time"] += dt; W["gain_sum"] += gain * dt; W["rgap_sum"] += (ratio(sh, f) / ratio(base, f) - 1) * dt

    q_accept()
    while t < max_min and run < max_runs:
        run += 1; st = stats(eq); t_run0 = t
        pre_run = ev["first10"] is None; run_full = False; run_bagmax = len(bag)   # 7칸: 가방 지표
        if pre_run: K["kept"].append(len(bag))   # 7칸: 출발 때 가방에 남아 있는 장비(강화석이 모자라 남긴 계승 후보·원본 = 창고 몫)
        qact0 = sum(1 for q in QL if qst[q[0]] == "act" and not q[0].startswith("q.lamp_tags"))
        if BC:
            start = BC; boss_only = True
        else:
            cands = [1] + ([6] if unlocked6 else []) + ([WP] if WP else [])
            ok = [s for s in cands if ratio(st, s) >= CONT]
            start = max(ok) if ok else 1; boss_only = False
        WP = 0
        f = start; direct = 0.0; dis = 0.0; died = False
        maxf0 = SS["maxf"]; bkills = 0; bounty_now = bounty_on; gold_run = 0.0
        g_start = (gold[0], G["earn"])
        while True:
            st = stats(eq)
            if f not in entry:
                entry[f] = dict(run=run, time=t, atk=st["atk"], hp=st["hp"], def_=st["def_"], R=ratio(st, f), kind=st["kind"],
                                A=st["A"], S=st["S"], B=st["B"], cm=st["cm"], crit=st["crit"], cd=st["cd"], aspd=st["aspd"], move=st["move"],
                                cdr=st["cdr"], skill=st["skill"], boss=st["boss"],
                                eqg=[x.g if x is not None else -1 for x in eq], eqlv=[x.lv if x is not None else -1 for x in eq])   # 7칸
            SS["maxf"] = max(SS["maxf"], f)
            R = ratio(st, f); r_, b_, a_, e_ = FLOORS[f]
            drops = []; was_boss_only = boss_only
            if boss_only:
                die_here = False; boss_only = False
                t += BOSS_MIN[f]
            else:
                log.append((run, f, t, R))
                die_here = rng.random() < p_death(R); frac = rng.random() if die_here else 1.0
                measure_wait(f, FLOOR_MIN[f] * frac)
                for kind, n in (("rat", r_), ("boar", b_), ("archer", a_)):
                    k = int(round(n * frac))
                    for _ in range(k):
                        if rng.random() < DROP[kind] * DROP_MULT: drops.append(("mob", f))   # 7칸: 드랍 ×배율(1.0이면 2차와 같음)
                        direct += STONE[kind] * ilv_mult(f)
                    if kind == "rat": SS["rats"] += k
                    elif kind == "boar": SS["boars"] += k
                    elif f >= 3: SS["arch3"] += k
                    if f >= maxf0 - 2: bkills += k
                    gold_run += GOLD_KILL[kind] * k * ilv_mult(f)
                if not die_here:
                    for _ in range(e_):
                        drops.append(("elite", f)); direct += STONE["elite"] * ilv_mult(f)
                        if DROP_MULT != 1.0 and rng.random() < DROP_MULT - 1: drops.append(("elite", f))   # 7칸: ×배율
                    if e_:
                        SS["elite"][f] = SS["elite"].get(f, 0) + e_
                        if f >= maxf0 - 2: bkills += e_
                        gold_run += GOLD_ELITE * e_ * ilv_mult(f)
                t += FLOOR_MIN[f] * frac
            boss_dead = False
            if not die_here and f in BOSS_T:
                if pre_run: K["bossbag"][f].append(len(bag))   # 7칸: 보스 직전 가방 칸
                win = rng.random() < p_boss_win(R, f)
                if f == 5 and ev["boss5_first"] is None: ev["boss5_first"] = win
                if f == 10:
                    if ev["boss10_first"] is None: ev["boss10_first"] = win
                    if not first_kill[10]:
                        ev["boss10_tries"] += 1
                        if not win: SS["b10loss"] += 1
                    SS["b10try"] += 1
                if win:
                    boss_dead = True; direct += BOSS_STONE[f]; gold_run += GOLD_BOSS[f]
                else:
                    die_here = True
                    if not first_kill[f]: BC = f
            elif not die_here:
                drops.append(("chest", f)); direct += chest_stone(f); gold_run += gold_chest(f)
                if DROP_MULT != 1.0 and rng.random() < DROP_MULT - 1: drops.append(("chestx", f))   # 7칸: ×배율(보장 없는 상자 장비)
            if not die_here:
                SS["cleared"].add(f); SS["clear_cnt"][f] = SS["clear_cnt"].get(f, 0) + 1
            gen = []
            for src, fl in drops:
                g = roll_grade(rng, GT[fl]); forced_slot = None
                if src == "elite" and g == 0: g = 1
                if src == "chest" and fl in (1, 3) and fl not in chest_done:
                    chest_done.add(fl); g = max(g, 2 if fl == 1 else 3)
                    forced_slot = "w" if fl == 1 else "wa"   # 7칸: 1층 = 무기, 3층 = 무기·갑옷 가운데 점수가 낮은 쪽(8-2)
                if fl >= 5:
                    pity += 1
                    if pity >= LEG_PITY: g = 4
                if g == 4: pity = 0
                gen.append([g, fl, forced_slot, None])
            if f <= 4 and not was_boss_only and gen and not NO_FILL:   # 7칸: 1~4층 빈 자리 채우기 — 보장 아닌 첫 상자, 없으면 그 층 첫 장비
                fi = next((i for i, (src, _fl) in enumerate(drops) if src == "chest" and gen[i][2] is None), None)
                if fi is None: fi = next((i for i in range(len(gen)) if gen[i][2] is None), None)
                if fi is not None: gen[fi][3] = "fill"
            if boss_dead:
                if f == BC: BC = 0
                n, bt = BOSS_T[f]; bg = [roll_grade(rng, bt) for _ in range(n)]
                if DROP_MULT != 1.0: bg += [roll_grade(rng, bt) for _ in range(n) if rng.random() < DROP_MULT - 1]   # 7칸: ×배율
                first_b = not first_kill[f]
                if first_b:
                    if f == 5 and max(bg) < 3: bg[0] = 3
                    if f == 10 and 4 not in bg: bg[0] = 4
                    first_kill[f] = True
                    SS["bosses"].add(f)
                gdone = False
                for g in bg:
                    if g == 4: pity = 0
                    sp = None
                    if first_b and f == 10 and g == 4 and not gdone: sp = "guar"; gdone = True   # 7칸: 10층 첫 처치 보장 전설
                    gen.append([g, f + 1, None, sp])
            t_mid = t - (BOSS_MIN[f] * 0.5 if was_boss_only else FLOOR_MIN[f] * 0.5)
            for g, il, fs, sp in gen:
                cap_n = 40 + 5 * caps["bag"]   # 7칸: 가방이 찬 채로 장비를 주움(넘침 칸·자동 분해 대상) — 2차 비교와 같은 정의
                run_bagmax = max(run_bagmax, len(bag))
                if len(bag) >= cap_n:
                    run_full = True
                    if pre_run: K["full_picks"] += 1
                forced = None; only = None
                if fs == "w": forced = 0
                elif fs == "wa": forced = weakest(eq, (0, 1), 1)[0]
                if sp == "fill":
                    empt = [p for p in range(7) if any(eq[s] is None for s in PART_SLOTS[p])]
                    if empt:
                        only = empt
                        if ev["first10"] is None: K["fill"] += 1
                it = new_item(rng, g, il, eq, owned() if sp == "guar" else (), forced, only, sp == "guar")
                if ev["first10"] is None:   # 7칸: 부위별 장비 수(10층 첫 처치 전, 던전 드랍만)
                    K["parts"][it.part] += 1; K["drops"] += 1
                    if it.leg is not None: K["legparts"][it.part] += 1
                if g == 4 and ev["firstleg"] is None: ev["firstleg"] = (run, t)
                if g >= 3 and ev["firstepic"] is None: ev["firstepic"] = (run, t)
                part = it.part; th = THRESH[part]
                slot = g_slot(eq, it); cur = eq[slot]; s_cur = stats(eq); trial = list(eq); trial[slot] = it
                # v2: 던전 안 계승 없음. 7칸: '+0 그대로 끼면'이 부위 문턱 이상일 때만 G로 낀다. 벗은 장비는 가방으로.
                v0 = composite(s_cur, stats(trial)) if cur is not None else 1.0
                if cur is None or v0 >= th:
                    eq[slot] = it
                    if cur is not None:
                        bag.append(cur)
                        if cur.lv >= 2 and ev["first10"] is None:
                            pend.append(dict(kind="g", it=it, t=t_mid, run=run, gap=0.0, f=f))
                elif g == 0 and f >= 6 and not sell_ok(0):
                    dis += DIS[0]
                else:
                    bag.append(it)
                    if g >= 1 and ev["first10"] is None:   # 계승 후보(계승 값이 문턱 이상이고 '지금 끼면'보다 문턱만큼 높음)
                        res, vi = best_pair(eq, bag, part, None, [it])
                        if res is not None and vi >= v0 + th:
                            pend.append(dict(kind="cand", it=it, t=t_mid, run=run, gap=vi - max(v0, 0.0), f=f))
                if len(bag) > 40 + 5 * caps["bag"]:
                    keep = set(id(e["it"]) for e in pend)
                    bag.sort(key=lambda x: (id(x) in keep, x.lv, x.g)); x = bag.pop(0); dis += DIS[x.g] + refund(x.part, x.lv)   # 7칸: 환급 × 부위 배율
            if boss_dead and f == 5:
                unlocked6 = True
                if ev["first5"] is None: ev["first5"] = (run, t)
            if boss_dead and f == 10 and ev["first10"] is None:
                ev["first10"] = (run, t)
                if GOLD:
                    G["kill"] = (gold[0] + gold_run, G["earn"] + gold_run); G["start"] = g_start
                    G["src_pre"]["field"] += gold_run
            if die_here:
                died = True; ev["deaths"] += 1
                if ev["first10"] is None: ev["deaths_pre10"] += 1
                if direct > 0: direct = math.floor(direct * 0.5)
                g_before = gold_run
                gold_run = math.floor(gold_run * 0.5)
                if ev["first10"] is None: G["lost_pre"] += g_before - gold_run
                break
            if f == 10: break
            nst = stats(eq); need = CONT_BOSS if (f + 1) in BOSS_T else CONT
            if ratio(nst, f + 1) < need:
                WP = f + 1
                break
            f += 1
        if pre_run:   # 7칸: 가방 지표(10층 첫 처치 전에 시작한 원정)
            K["runs"] += 1; K["full_runs"] += run_full; K["bagmax"].append(run_bagmax)
        wallet[0] += direct + dis
        # ------------------------------------------------ v2: 마을 (귀환 지점 → 결과 화면 정산 → 대장간 → 상점 → 갱도 입구)
        pre10_visit = ev["first10"] is None
        SS["visits"] += 1; visit = SS["visits"]; t_arr = t; TA[0] = t
        tm = town_minutes(visit, BC != 0 and visit >= 4)
        t += tm; before = stats(eq); town = 0.0; quest_now = 0.0; shop_spend = 0.0; ninh = 0; qpre0 = stat["q_pre"]
        VG[0] = 0.0; VG[1] = 0.0; VG[2] = 0; VG[3] = 0.0; VG[4] = 0.0
        if GOLD:
            gold[0] += gold_run; G["earn"] += gold_run; VG[0] += gold_run
            if pre10_visit: G["src_pre"]["field"] += gold_run
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
        if SHOP_UNLOCK == "first5" and 5 in SS["bosses"]: shop_open = True
        if SHOP_UNLOCK.startswith("visit") and visit >= int(SHOP_UNLOCK[5:]): shop_open = True
        if GOLD and shop_open and G["shop_open_gold"] is None: G["shop_open_gold"] = (gold[0], TA[0])
        n_end = sum(1 for e in pend if e["kind"] == "cand" and e["it"] in bag)
        if pre10_visit:
            W["endcand"].append(n_end); W["runs"] += 1
            if any(e["kind"] == "cand" and e["run"] == run for e in pend): W["cand_runs"] += 1
        # 2) 대장간 추천 짝. 7칸: 부위마다 1개(최대 7줄), 바뀌면 다시 봄(최대 3번)
        for _ in range(3):
            changed = False
            for part in range(7):
                res, v = best_pair(eq, bag, part, wallet)
                if res is None: continue
                tg, src, kept, P_, vac = res
                if src is not None:
                    wallet[0] -= inh_cost(part, kept); wallet[0] += DIS[src.g] + (refund(part, src.lv - 1) - refund(part, kept))
                    tg.best = max(tg.best, min(src.best, CAP[tg.g])); tg.lv = kept; tg.gauge = {}; tg.streak = 0
                    ssl = slot_of(eq, src)
                    if ssl is not None: eq[ssl] = None
                    else: bag.remove(src)
                    ninh += 1; SS["inh"] += 1
                    if ev["firstinh"] is None: ev["firstinh"] = (run, TA[0])
                if vac is not None and eq[vac] is tg: eq[vac] = None
                if tg is not eq[P_]:
                    if tg in bag: bag.remove(tg)
                    if eq[P_] is not None: bag.append(eq[P_])
                    eq[P_] = tg
                changed = True
            if not changed: break
        still = []
        for e in pend:
            it = e["it"]
            if e["kind"] == "g":
                W["gwait"].append((t_arr - e["t"], run - e["run"], it.lv > 0 and slot_of(eq, it) is not None))
                continue
            if slot_of(eq, it) is not None:
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
            res, v = best_pair(eq, bag, e["it"].part, None, [e["it"]])
            if res is not None and res[1] is not None and wallet[0] < inh_cost(e["it"].part, res[2]):
                keep.add(id(e["it"])); keep.add(id(res[1]))
        nb = []
        for it in bag:
            if id(it) in keep: nb.append(it); continue
            if it.lv == 0 and sell_ok(it.g):
                sg = DIS[it.g] * SELL_RATE; gold[0] += sg; G["earn"] += sg; VG[0] += sg
                if pre10_visit: G["src_pre"]["sell"] += sg; G["stone_sell_pre"] += DIS[it.g]
                continue
            wallet[0] += DIS[it.g] + refund(it.part, it.lv); town += DIS[it.g] + refund(it.part, it.lv)   # 7칸: 환급 × 부위 배율
        bag = nb
        for e in pend:
            if e["it"] not in bag:
                W["cand"].append((t_arr - e["t"], run - e["run"], "gone", e["gap"], f - e["f"]))
        pend = [e for e in pend if e["it"] in bag]
        # 4) 상점. 7칸: 진열 3칸 = 무기 1 · 방어구 묶음 1 · 장신구 묶음 1, 묶음 칸은 묶음 안 가장 약한 부위(8-3)
        if SHOP and shop_open:
            il = max(1, SS["maxf"] + SHOP_ILV)
            for _pass in range(1 + (REROLL if GOLD and GSPEND != "none" else 0)):
                if _pass > 0:
                    if not reroll_ok(il, _pass): break
                    g_spend("rr", reroll_price(il, _pass))
                for grp in range(3):
                    part = weakest(eq, GROUP_PARTS[grp], 1)[0]
                    g = 2 if (unlocked6 and rng_shop.random() < SHOP_RARE) else 1
                    it = make_item(rng_shop, part, g, il); price = shop_price(g, il)
                    if GOLD:
                        if price > gold[0]: continue
                    elif price > 0.3 * wallet[0]:
                        continue
                    sl = g_slot(eq, it); cur = eq[sl]; trial = list(eq); trial[sl] = it; s0 = stats(eq)
                    v0 = composite(s0, stats(trial)) if cur is not None else 1.0; vi = -1.0; kept = 0
                    if cur is not None and cur.lv >= 2:
                        kept = min(cur.lv - 1, CAP[g])
                        if wallet[0] >= inh_cost(part, kept) + (0 if GOLD else price): vi = composite(s0, stats(trial, {sl: kept}))
                    if max(v0, vi) >= shop_th(part):
                        if GOLD: g_spend("disp", price)
                        else: wallet[0] -= price
                        shop_spend += price
                        if vi > v0:
                            moved = cur.lv - 1; wallet[0] -= inh_cost(part, kept); wallet[0] += DIS[cur.g] + (refund(part, moved) - refund(part, kept))
                            it.lv = kept; it.best = min(cur.best, CAP[g]); SS["inh"] += 1
                        elif cur is not None:
                            wallet[0] += DIS[cur.g] + refund(part, cur.lv)
                        eq[sl] = it; shop_items.add(id(it))
                        if ev["first10"] is None: stat["shop_buy_pre"] += 1
                if _pass == 0 and GOLD: buy_caps()
        elif GOLD:
            buy_caps()
        # 5) 강화
        tries, nb_any, nb_wa = do_enhance()
        paid_town = 0
        for q in QL:
            if qst[q[0]] == "act" and q[3] == "town1" and tries >= 1:
                quest_now += q_pay(q); paid_town += 1
        got = q_check(True); quest_now += got
        if got > 0 or paid_town:
            tr2, nb2, nw2 = do_enhance(); tries += tr2; nb_any = nb_any or nb2; nb_wa = nb_wa or nw2
        big = composite(before, stats(eq)) >= 0.05
        notable.append(nb_any or big); K["notable_wa"].append(nb_wa or big)   # 7칸: '눈에 띄는 강화'를 무기·갑옷 기준으로 따로
        pre = ev['first10'] is None or ev['first10'][0] == run
        per_run.append(dict(run=run, start=start, end=f, died=died, tries=tries, direct=direct, dis=dis, town=town, quest=quest_now, quest_pre=stat["q_pre"] - qpre0,
                            shop=shop_spend, gold=gold_run if GOLD else 0.0, tmin=tm, inh=ninh, mins=t - t_run0, pre10=pre,
                            shop_eq=sum(1 for x in eq if x is not None and id(x) in shop_items),
                            tags=tags(), b10loss=SS["b10loss"], maxf=SS["maxf"], first10_run=(ev["first10"] or (None,))[0], qact=qact0,
                            g_in=VG[0], g_sp=VG[1], g_n=VG[2], g_q=VG[3], g_q_pre=VG[4], g_bal=gold[0], pre10_visit=pre10_visit))
        if GOLD:
            if ev["first10"] is not None and ev["first10"][0] == run: G["after"] = (gold[0], G["earn"])
            elif ev["first10"] is not None: G["post"].append((VG[0], VG[1], VG[2], gold[0]))
        if pre:
            stat["inc_pre"] += direct + dis + town; stat["shop_pre"] += shop_spend; stat["gold_pre"] += gold_run if GOLD else 0.0
        if pre10_visit:
            stat["town_min_pre"] += tm; stat["visits_pre"] += 1; stat["inh_pre"] += ninh
        if pre10_visit and any(x is None for x in eq): K["empty_last"] = t   # 7칸: 빈 자리가 남아 있던 마지막 방문 끝 시각
        if ev["done"] is None and all(x is not None and x.g == 4 and x.lv == 15 for x in eq):
            ev["done"] = (run, t)
    ev["stat"] = stat; ev["W"] = W; ev["G"] = G; ev["K"] = K   # 7칸: K
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


def v7_window(per_run):
    """7칸: '7층 도달 뒤 귀환'(maxf >= 7 이 된 첫 마을 방문)의 (도착 시각, 방문 끝 시각). 강화는 방문 끝 시각에 기록된다."""
    t0 = 0.0
    for x in per_run:
        t1 = t0 + x["mins"]
        if x["maxf"] >= 7: return (t1 - x["tmin"], t1)
        t0 = t1
    return None


def drop_vs_v7(res, key):
    """7칸: 첫 하락이 '7층 도달 뒤 귀환' 방문까지(포함) 일어난 비율, 그 방문 전(제외) 비율, 그 방문 시각 중앙."""
    inc = exc = n = 0; arr = []
    for r in res:
        w = v7_window(r[4])
        if w is None: continue
        n += 1; arr.append(w[0]); d = r[0][key]
        if d is not None and d <= w[1] + 1e-9: inc += 1
        if d is not None and d < w[0] - 1e-9: exc += 1
    return (inc / n if n else 0.0, exc / n if n else 0.0, statistics.median(arr) if arr else float("nan"))


BOAR_ATK = lambda f: round(300 * ATK_G ** (f - 1))   # 7칸: fin_floor 의 멧돼지 돌진(10배 단위)


def main():
    rng = random.Random(int(os.environ.get("SEED", "20261002")))
    res = [play(rng) for _ in range(P)]
    med = statistics.median
    gtag = (f", 골드(의뢰 x{QGOLD:g}, 쓰기={GSPEND}, 새로고침 {REROLL}회, 창고 {STORE_STEPS[0][1]:g}/{STORE_STEPS[1][1]:g}, 가방 {BAG_STEPS[0][1]:g}/{BAG_STEPS[1][1]:g}, 되사기 칸 {BUYBACK_UP:g}"
            f"{', 상점 열림=' + SHOP_UNLOCK if SHOP_UNLOCK != 'maxf7' else ''}{', 팔기=' + SELL if SELL != '0' else ''}{', 골드 배율 ' + format(GSCALE, 'g') if GSCALE != 1 else ''})") if GOLD else ""
    knobs = "".join(f", {k}={os.environ[k]}" for k in ("CRIT_GLV", "CRIT_RING", "WPN_CRIT", "WPN_CD", "SHOP_TH", "RNORM", "SEED", "THRESH_PM", "THRESH_FLAT", "NO_FILL", "OPT_MULT") if os.environ.get(k))
    tag = f"7칸, 드랍 x{DROP_MULT:g}{knobs} | " + ("v3 " if GOLD else "v2 ") + "마을=" + ({"sched": "일정 6/5/5/4·재도전 3", "final": "최종 4/4/4/4.5·재도전 3"}.get(TOWN, f"{TOWN}분 고정")) + f", 의뢰={QUEST}, 반복 의뢰={BOUNTY}{f'(10층 전 최대 {BOUNTY_MAX}회)' if BOUNTY_MAX else ''}, 상점={'켬' if SHOP else '끔'}{gtag}{', 장비 보장' if GUAR else ''}"
    print(f"# 인원 {P}, 체력 성장 {HP_G}, 공격 성장 {ATK_G}, 정책 {'신중(R>=0.90, 보스 앞 0.95)' if POL == 'c' else '밀어붙임(R>=0.80)'}, 보스 R50 5층 {R50_5} / 10층 {R50_10} | {tag}")
    for key, name in (("first5", "5층 보스 첫 처치"), ("first10", "10층 보스 첫 처치"), ("firstepic", "첫 영웅(보라) 획득"), ("firstleg", "첫 전설 획득"), ("done", "전설 +15 여덟 자리")):
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
          + ("" if GOLD else f" (수입 대비 {statistics.mean(s / t_ for s, t_ in zip(shop, tot) if t_) * 100:.1f}%)") + f", 원정 끝 상점 장비를 낀 자리 평균 {statistics.mean(shop_eq):.2f}/8")
    if GOLD:
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
              f"같은 원정에서 주운 뒤 더 내려간 층 평균 {statistics.mean(c[4] for c in inh if c[1] == 0) if any(c[1] == 0 for c in inh) else 0:.2f}")
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
    # ------------------------------------------------------------ 7칸: 추가 지표
    m = report7(res)
    if os.environ.get("METRICS_JSON") == "1":
        print("#METRICS7 " + json.dumps(m, ensure_ascii=False))


def report7(res):
    """7칸: 장비 문서 12장 단계 5 합격선에 쓰는 지표를 찍고 dict 로 돌려준다(fin_variants.py fin7 묶음이 읽음)."""
    med = statistics.median; n = len(res)
    print("\n# 7칸 지표")
    print(f"[7칸] 시작 상태 S0: 공격 {S0['atk'] * 10:.0f}, 체력 {S0['hp'] * 10:.0f}, 방어 {S0['def_'] * 10:.0f}, 이동 {5.0 * S0['M']:.2f}, 치명 {S0['crit'] * 100:.0f}% / {S0['cd'] * 100:.0f}%, "
          f"A {S0['A']:.3f} / S {S0['S']:.2f} / M {S0['M']:.2f} — 2차 S0 A {S0_V2['A']:.3f} / S {S0_V2['S']:.2f} / M {S0_V2['M']:.2f} "
          f"(A {(S0['A'] / S0_V2['A'] - 1) * 100:+.2f}% = 장검 고유 치명, S·M {'같음' if abs(S0['S'] - S0_V2['S']) < 1e-9 and abs(S0['M'] - S0_V2['M']) < 1e-9 else '다름'}), "
          f"시작 R {ratio(S0, 1):.3f} (R 기준 = {'7칸 시작' if RNORM != 'v2' else '2차 시작 A'})")
    Rm = {}; atk = {}; hp = {}; df = {}
    for f in range(1, 11):
        es = [r[1][f] for r in res if f in r[1]]
        if not es: continue
        Rm[f] = med(e["R"] for e in es); atk[f] = med(e["atk"] for e in es); hp[f] = med(e["hp"] for e in es); df[f] = med(e["def_"] for e in es)
    print("[7칸] 층 첫 진입 R 중앙: " + ", ".join(f"{f}층 {Rm[f]:.3f}" for f in Rm))
    print("[7칸] 멧돼지 돌진 한 번에 잃는 체력(층 첫 진입 중앙 장비, fin_floor 식): " + ", ".join(
        f"{f}층 {BOAR_ATK(f) * 1000 / (1000 + df[f] * 10) / (hp[f] * 10) * 100:.1f}%" for f in hp))
    e10 = [r[1][10] for r in res if 10 in r[1]]
    B10 = med(e["B"] for e in e10) if e10 else float("nan")
    if e10:
        print(f"[7칸] 10층 첫 진입: 공격력 중앙 {atk[10] * 10:.0f}, 기대 치명 배율 중앙 {med(e['cm'] for e in e10):.3f} (치명 {med(e['crit'] for e in e10) * 100:.1f}% / 치명 피해 {med(e['cd'] for e in e10) * 100:.0f}%), "
              f"공격 속도 중앙 +{med(e['aspd'] for e in e10) * 100:.1f}%, 재사용 감소 {med(e['cdr'] for e in e10) * 100:.1f}%, 스킬 피해 {med(e['skill'] for e in e10) * 100:.1f}%, 보스 피해 {med(e['boss'] for e in e10) * 100:.1f}%, "
              f"이동 {med(e['move'] for e in e10) * 100:+.1f}%, 보스전 지수 B 중앙 {B10:.1f}")
        kinds = {}
        for e in e10: kinds[e["kind"]] = kinds.get(e["kind"], 0) + 1
        print("[7칸] 10층 첫 진입 무기: " + ", ".join(f"{k} {v / len(e10) * 100:.0f}%" for k, v in sorted(kinds.items(), key=lambda kv: -kv[1])))
        print("[7칸] 10층 첫 진입 자리별 평균 등급(0 일반~4 전설) / 평균 강화 / 빈 자리: " + ", ".join(
            f"{SLOT_NAMES[i]} {statistics.mean(max(0, e['eqg'][i]) for e in e10 if e['eqg'][i] >= 0) if any(e['eqg'][i] >= 0 for e in e10) else 0:.2f}"
            f"/+{statistics.mean(e['eqlv'][i] for e in e10 if e['eqlv'][i] >= 0) if any(e['eqlv'][i] >= 0 for e in e10) else 0:.1f}"
            f"/{sum(1 for e in e10 if e['eqg'][i] < 0) / len(e10) * 100:.0f}%" for i in range(8)))
    risk_wa = [r[0]["risk_wa"] or 999 for r in res]; drop_wa = [r[0]["drop_wa"] or 999 for r in res]
    drop_any = [r[0]["drop"] or 999 for r in res]; drop_sm = [r[0]["drop_small"] or 999 for r in res]
    wa_inc, wa_exc, v7arr = drop_vs_v7(res, "drop_wa"); an_inc, an_exc, _ = drop_vs_v7(res, "drop")
    print(f"[7칸] 무기·갑옷 기준: +8 첫 시도 중앙 {med(risk_wa):.0f}분, 첫 하락 중앙 {med(drop_wa):.0f}분, 60분 안 첫 하락 {sum(1 for x in drop_wa if x <= 60) / n * 100:.0f}% "
          f"(작은 칸 첫 하락 중앙 {med(drop_sm):.0f}분, 모든 부위 첫 하락 중앙 {med(drop_any):.0f}분)")
    print(f"[7칸] '7층 도달 뒤 귀환'(도착 중앙 {v7arr:.0f}분) 때까지 첫 하락: 무기·갑옷 기준 {wa_inc * 100:.0f}% (그 방문 전 {wa_exc * 100:.0f}%), 모든 부위 {an_inc * 100:.0f}% (그 방문 전 {an_exc * 100:.0f}%)")
    for a, b in ((1, 4), (5, 8), (9, 12), (13, 16), (17, 20)):
        xs = [r[0]["K"]["notable_wa"][e - 1] for r in res for e in range(a, b + 1) if e <= len(r[0]["K"]["notable_wa"])]
        if xs: print(f"[7칸] 원정 {a}~{b}: 눈에 띄는 강화(무기·갑옷 새 최고 단계 또는 종합 +5%) {sum(xs) / len(xs) * 100:.0f}%")
    Ks = [r[0]["K"] for r in res]
    parts = [statistics.mean(k["parts"][p] for k in Ks) for p in range(7)]; tot = sum(parts)
    print(f"[7칸] 10층 첫 처치 전 던전 장비 수(사람당 평균) {tot:.1f}개: " + ", ".join(f"{PARTS[p]} {parts[p]:.1f} ({parts[p] / tot * 1000:.0f}‰)" for p in range(7)))
    sc = 118.5 * DROP_MULT / tot
    print(f"[7칸] 문서 기준(첫 탐험 + 재방문 118.5개 × 드랍 배율 {DROP_MULT:g})으로 비율 환산: " + ", ".join(f"{PARTS[p]} {parts[p] * sc:.1f}" for p in range(7))
          + f" (빈 자리 채우기 사람당 {statistics.mean(k['fill'] for k in Ks):.2f}번)")
    lp = [statistics.mean(k["legparts"][p] for k in Ks) for p in range(7)]; lt = sum(lp)
    if lt: print(f"[7칸] 10층 첫 처치 전 전설 {lt:.2f}개의 부위: " + ", ".join(f"{PARTS[p]} {lp[p] / lt * 100:.0f}%" for p in range(7)))
    runs = sum(k["runs"] for k in Ks); full = sum(k["full_runs"] for k in Ks); bm = [x for k in Ks for x in k["bagmax"]]
    bb5 = [x for k in Ks for x in k["bossbag"][5]]; bb10 = [x for k in Ks for x in k["bossbag"][10]]; kp = [x for k in Ks for x in k["kept"]]
    print(f"[7칸] 가방(10층 첫 처치 전에 시작한 원정): 가득 찬 채로 장비를 주운 원정 {full / max(1, runs) * 100:.1f}% (사람당 그런 줍기 {statistics.mean(k['full_picks'] for k in Ks):.2f}번), "
          f"원정 중 가방 최대 칸 평균 {statistics.mean(bm):.1f} (상위 1% {pct(bm, .99)}), 보스 직전 가방 평균 5층 {statistics.mean(bb5) if bb5 else 0:.1f} / 10층 {statistics.mean(bb10) if bb10 else 0:.1f}칸, "
          f"출발 때 남긴 장비(창고 몫) 평균 {statistics.mean(kp):.2f} (최대 {max(kp)})")
    el = [k["empty_last"] for k in Ks if k["empty_last"] is not None]
    print(f"[7칸] 빈 자리가 남은 채 끝난 마을 방문이 있었던 사람 {len(el) / n * 100:.0f}%, 그 마지막 방문 끝 시각 중앙 {med(el) if el else 0:.0f}분")
    t10 = [r[0]["first10"][1] for r in res if r[0]["first10"]]
    return dict(prog="7칸", drop_mult=DROP_MULT, pol=POL, n=n,
                t10=med(t10) if t10 else None, t10_in=sum(1 for m in t10 if 105 <= m <= 165) / max(1, len(t10)), t10_reach=len(t10) / n,
                R={f: Rm[f] for f in Rm}, atk={f: atk[f] for f in atk}, hp={f: hp[f] for f in hp}, df={f: df[f] for f in df},
                B10=B10, cm10=med(e["cm"] for e in e10) if e10 else None, atk10=atk.get(10),
                drop_wa_inc=wa_inc, drop_wa_exc=wa_exc, drop_any_inc=an_inc, drop_any_exc=an_exc, v7arr=v7arr,
                d60_wa=sum(1 for x in drop_wa if x <= 60) / n, d60_any=sum(1 for x in drop_any if x <= 60) / n,
                full_runs=full / max(1, runs), bagmax=statistics.mean(bm), bossbag5=statistics.mean(bb5) if bb5 else None,
                bossbag10=statistics.mean(bb10) if bb10 else None, kept=statistics.mean(kp), kept_max=max(kp), bagmax99=pct(bm, .99), parts=parts, drops=tot,
                S0=dict(A=S0["A"], S=S0["S"], M=S0["M"]), S0_v2=S0_V2, R0=ratio(S0, 1))


if __name__ == "__main__":
    main()
