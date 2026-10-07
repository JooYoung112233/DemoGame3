namespace Demo6.Core.Combat
{
    /// <summary>
    /// 오른쪽 클릭 무기 행동 종류(기획/세-무기-우클릭-소켓-1차.md 0-3의 2). 한손검과 방패 = 막기, 대검 = 기 모으기, 쌍검 = 난사, 나머지 6종 = 없음.
    /// </summary>
    public enum WeaponActKind
    {
        None,
        /// <summary>한손검과 방패: 누르는 동안 앞 반원 막기 + 패링(2-5·2-6).</summary>
        Guard,
        /// <summary>대검: 누르는 동안 기 모으기(1~3단계) → 놓으면 놓아 베기(3-6·3-7).</summary>
        Charge,
        /// <summary>쌍검: 한 번 누르면 1.10초 동안 8타 난사(4-6).</summary>
        Flurry,
    }

    /// <summary>
    /// 무기 행동 단계(State.WeaponAct 안의 세부 단계). 그림 훅(WeaponStanceLook)과 HUD가 이 값 하나로 자세·글을 고른다.
    /// 막기: Raise → Hold → Lower(그 사이 Recoil·ParryPush·Break). 기 모으기: Charging → Release. 난사: Flurry. 맞아서 끊김: Flinch(5-6).
    /// </summary>
    public enum WeaponActPhase
    {
        None,
        /// <summary>방패 들기 0.12초(누른 순간부터 막기 켜짐).</summary>
        Raise,
        /// <summary>막기 자세 유지.</summary>
        Hold,
        /// <summary>방패 내리기 0.12초(앞 0.06초까지 막음, 못 끊음).</summary>
        Lower,
        /// <summary>막은 뒤 반동(일반 0.12초, 보스 0.25초).</summary>
        Recoil,
        /// <summary>패링 뒤 밀쳐 내기 0.15초.</summary>
        ParryPush,
        /// <summary>막기 깨짐 0.8초(걸음·행동 멈춤).</summary>
        Break,
        /// <summary>대검 기 모으기(누르는 동안).</summary>
        Charging,
        /// <summary>대검 놓아 베기(단계별 ComboStep, GreatswordCharge.Release).</summary>
        Release,
        /// <summary>쌍검 난사 1.10초.</summary>
        Flurry,
        /// <summary>맞아서 끊긴 경직(막기·난사 0.35초, 대검 0.45초). 그림 훅이 한 곳에서 그린다.</summary>
        Flinch,
    }

    /// <summary>
    /// 무기 id → 행동 종류·이름·카드 줄·걸음 배율·돌기 속도(0-3의 9·17, 1장 표). 저장 id는 그대로다(wpn_longsword = 한손검과 방패).
    /// 계약 단계 값(확정). 꾸러미 ① 규칙이 시험한다(WeaponActRuleTests).
    /// </summary>
    public static class WeaponActRules
    {
        public const string SwordShieldId = "wpn_longsword";
        public const string GreatswordId = "wpn_greatsword";
        public const string TwinbladesId = "wpn_twinblades";

        /// <summary>막기 걸음 배율(들기·막기·내리기·반동).</summary>
        public const float GuardMoveScale = 0.5f;
        /// <summary>패링 밀쳐 내기 걸음 배율.</summary>
        public const float ParryPushMoveScale = 0.2f;
        /// <summary>난사 걸음 배율.</summary>
        public const float FlurryMoveScale = 0.30f;

        /// <summary>행동 중 몸이 커서 쪽으로 도는 최대 속도(초당 도).</summary>
        public const float GuardTurnDegPerSec = 300f;
        public const float ChargeTurnDegPerSec = 120f;
        public const float FlurryTurnDegPerSec = 90f;

        /// <summary>무기 id의 오른쪽 클릭 행동. 세 무기 말고는 None(나머지 6종·모르는 id·null).</summary>
        public static WeaponActKind KindOf(string weaponId)
        {
            switch (weaponId)
            {
                case SwordShieldId: return WeaponActKind.Guard;
                case GreatswordId: return WeaponActKind.Charge;
                case TwinbladesId: return WeaponActKind.Flurry;
                default: return WeaponActKind.None;
            }
        }

        /// <summary>
        /// 휘두르기 첫 판정 뒤에 누른 오른쪽 클릭이 휘두르기를 끊고 바로 시작하는가(5-2). 막기만 그렇다(방패가 빨리 올라옴).
        /// 기 모으기·난사(그리고 행동 없음)는 언제 눌러도 그 휘두르기가 끝날 때 시작한다. 대검 ① 판정 뒤 끊고 1단계 놓기를 되풀이하면
        /// 초당 계수가 1.93으로 띠(1.45~1.60)와 놓기 상한(1.67)을 넘는다(0-3의 19).
        /// </summary>
        public static bool CutsSwing(WeaponActKind kind) => kind == WeaponActKind.Guard;

        /// <summary>행동 이름(HUD 칸·도움말). 없으면 '없음'.</summary>
        public static string Name(WeaponActKind kind)
        {
            switch (kind)
            {
                case WeaponActKind.Guard: return "막기";
                case WeaponActKind.Charge: return "기 모으기";
                case WeaponActKind.Flurry: return "난사";
                default: return "없음";
            }
        }

        /// <summary>
        /// 무기 카드의 행동 줄(2-1·3-1·4-1). 행동이 없으면 null(줄을 넣지 않음). 가방 카드 너비에 잘리지 않게 짧게 쓰고,
        /// 오른쪽 버튼은 HUD·알림과 같이 '우클릭'으로 부른다(0-3의 27).
        /// </summary>
        public static string CardLine(WeaponActKind kind)
        {
            switch (kind)
            {
                case WeaponActKind.Guard: return "우클릭: 막기 · 제때 들면 튕김";
                case WeaponActKind.Charge: return "우클릭: 기 모으기 · 놓으면 강타";
                case WeaponActKind.Flurry: return "우클릭: 난사 · 6초에 한 번";
                default: return null;
            }
        }

        /// <summary>무기 id의 카드 행동 줄(KindOf → CardLine).</summary>
        public static string CardLine(string weaponId) => CardLine(KindOf(weaponId));

        /// <summary>
        /// 행동 중 걸음 배율(시작 걸음 4.24에 곱함, 0-3의 9). 막기 0.5(패링 밀쳐 내기 0.2, 깨짐 0), 기 모으기 단계별(GreatswordCharge.MoveScale),
        /// 놓아 베기는 그 단계 ComboStep.moveScale, 난사 0.30, 끊김 경직 0. chargeLevel은 기 모으기 단계(0~3)이고 놓아 베기면 놓은 단계(1~3).
        /// </summary>
        public static float MoveScale(WeaponActKind kind, WeaponActPhase phase, int chargeLevel)
        {
            if (phase == WeaponActPhase.Flinch || phase == WeaponActPhase.Break) return 0f;
            switch (kind)
            {
                case WeaponActKind.Guard:
                    return phase == WeaponActPhase.ParryPush ? ParryPushMoveScale : GuardMoveScale;
                case WeaponActKind.Charge:
                    if (phase == WeaponActPhase.Release)
                    {
                        var step = GreatswordCharge.Release(chargeLevel);
                        return step != null ? step.moveScale : GreatswordCharge.MoveScale(chargeLevel);
                    }
                    return GreatswordCharge.MoveScale(chargeLevel);
                case WeaponActKind.Flurry:
                    return FlurryMoveScale;
                default:
                    return 1f;
            }
        }

        /// <summary>행동 중 최대 돌기 속도(초당 도): 막기 300, 기 모으기 120, 난사 90. 없으면 0(제한 없음).</summary>
        public static float TurnRate(WeaponActKind kind)
        {
            switch (kind)
            {
                case WeaponActKind.Guard: return GuardTurnDegPerSec;
                case WeaponActKind.Charge: return ChargeTurnDegPerSec;
                case WeaponActKind.Flurry: return FlurryTurnDegPerSec;
                default: return 0f;
            }
        }

        /// <summary>
        /// 이 순간 맞아도 끊기지 않는가(버팀 룬, 0-3의 11·6-7). 버팀 룬은 대검 기 모으기·놓아 베기에만 붙는다.
        /// testAllActs(Tuning.SuperArmorAllActs)면 막기·난사도 버틴다. 끊김 경직·막기 깨짐 중에는 늘 false.
        /// hasSuperArmorRune = 낀 무기에 버팀 룬이 있거나 시험 손잡이(Tuning.TestSuperArmorRune)가 켜짐.
        /// </summary>
        public static bool Armored(WeaponActKind kind, WeaponActPhase phase, bool hasSuperArmorRune, bool testAllActs)
        {
            if (!hasSuperArmorRune) return false;
            if (phase == WeaponActPhase.None || phase == WeaponActPhase.Flinch || phase == WeaponActPhase.Break) return false;
            if (testAllActs) return kind != WeaponActKind.None;
            return kind == WeaponActKind.Charge && (phase == WeaponActPhase.Charging || phase == WeaponActPhase.Release);
        }

        /// <summary>
        /// 맞으면 끊기는가(5-5 표). interruptible = 끊길 수 있는 순간인가(무기 행동 중, 대검 놓아 베기는 판정 전까지 — GreatswordCharge.BreaksOnHit),
        /// armored = Armored 결과, blocked = 방패로 앞 반원에서 막음·튕김. 피해 문턱은 최대 체력 대비 minPermille(기본 0 = 문턱 없음, 시험 무적 피해 0도 끊김).
        /// </summary>
        public static bool Interrupts(bool interruptible, bool armored, bool blocked, int damage, int maxHp, int minPermille)
        {
            if (!interruptible || armored || blocked) return false;
            if (minPermille <= 0) return true;
            return (long)damage * 1000 >= (long)maxHp * minPermille;
        }
    }

    /// <summary>무기 행동 공통 상수(0-3의 10, 5-1~5-9).</summary>
    public static class WeaponActCommon
    {
        /// <summary>오른쪽 클릭 입력 버퍼(초, 구르기·스킬과 같음).</summary>
        public const float InputBuffer = 0.15f;
        /// <summary>끊김 경직(맞은 순간부터): 막기·난사.</summary>
        public const float FlinchSeconds = 0.35f;
        /// <summary>끊김 경직: 대검 기 모으기·놓아 베기 판정 전.</summary>
        public const float ChargeFlinchSeconds = 0.45f;
        /// <summary>경직 처음 넉백 시간(지금 규칙 그대로).</summary>
        public const float FlinchKnockSeconds = 0.1f;
        /// <summary>행동이 없는 무기로 오른쪽 클릭했을 때 한 줄 알림을 띄우는 횟수(꾸러미 세기 NoActHintKey).</summary>
        public const int NoActHintTimes = 2;
        /// <summary>꾸러미 세기 키: 행동 없는 무기 오른쪽 클릭 알림.</summary>
        public const string NoActHintKey = "rmb_none";
        public const string NoActHintText = "이 무기는 우클릭 행동이 없다 · 회오리는 E";
        // 옛 '회오리는 이제 E · 우클릭은 무기 행동' 알림과 그 세기 키(whirl_e)는 뺐다(기획/키-배치-1차.md 0장 7).
        /// <summary>머리 위 글(WorldOverlay.Text).</summary>
        public const string InterruptedWord = "끊김";
        public const string SuperArmorWord = "버팀";
        public const string ParriedWord = "튕겨 냄!";
        /// <summary>HUD 키 글자.</summary>
        public const string ActKeyLabel = "우클릭";
        public const string WhirlKeyLabel = "E";

        /// <summary>그 행동이 끊겼을 때 경직(초).</summary>
        public static float FlinchFor(WeaponActKind kind) => kind == WeaponActKind.Charge ? ChargeFlinchSeconds : FlinchSeconds;

        /// <summary>
        /// 끊김 경직 남은 시간(remaining)이 입력 버퍼(0.15초) 안이면 그동안 누른 구르기·E·Q를 버리지 않고 경직이 끝나면 내보낸다(5-6 '경직 뒤 구르기 틈').
        /// 그 전에 누른 것은 버린다. 오른쪽 클릭은 늘 버린다(새로 눌러야 함). 0.16초면 버림, 0.15초면 남김(0-3의 21).
        /// </summary>
        public static bool KeepsBufferedInputs(float remaining) => remaining <= InputBuffer;
    }
}
