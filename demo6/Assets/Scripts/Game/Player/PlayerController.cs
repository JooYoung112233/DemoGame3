using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using Demo6.Core.Stats;
using UnityEngine;

namespace Demo6.Game
{
    public enum PlayerPose
    {
        Idle,
        Move,
        Attack,
        Dodge,
        Whirl,
        WaveCast,
        Hurt,
        Down,
        /// <summary>
        /// 오른쪽 클릭 무기 행동(막기·기 모으기·놓아 베기·난사·끊김 경직, 기획/세-무기-우클릭-소켓-1차.md 0-3의 2). 끝에 덧붙여 차례를 지킨다.
        /// 세부 단계는 PlayerController.ActPhase. 그림은 WeaponStanceLook.Override 한 입구가 그린다(리그가 모르면 쉬는 자세로 보임).
        /// </summary>
        WeaponAct,
    }

    /// <summary>
    /// 검사. 기획 3장: 기본공격(누르고 있으면 반복), 회오리 베기(E), 검풍(Q), 구르기(Space), 물약(R), 무기 행동(오른쪽 클릭).
    /// 기본공격은 무기별 단계 콤보 + 마무리(M0a 판정 반영). 판정이 나간 뒤에는 구르기로 끊을 수 있고, 스킬은 동작이 끝나면 나간다.
    /// 장비 능력치(장비 문서 2·3장)는 ApplyStats 한 입구로 받는다: 공격 속도는 SwingTiming(동작 길이·판정 순간·이월 상한)과 타당 버팀에만,
    /// 치명은 전용 난수 흐름으로 굴리고 연출을 3단계(가벼움·보통·무거움, 무거움 0.5초 제한)로 낸다. 공격 속도 0이면 콤보 시간은 예전과 비트까지 같다.
    /// 전투·보스·무기 다듬기 1차(기획/전투-보스-무기-다듬기-1차.md 4장·2-3, 결정 ③): 웅크리기(C, 느리고 조용함), 기습 처형(웅크린 채 잠든 적 등 뒤 첫 타)·
    /// 무너짐 처형(마무리·검풍), 회피 반격(기본 켜짐, 첫 타 피해 +20%), 새 무기 깃발(끊기·벽 박기·끌어당김·출혈·등 찌르기·관성).
    /// 무기 종류 바꾸기는 숫자키가 아니라 F1 시험 패널 단추만 한다(키 배치 1차 0장 2·3).
    /// 웅크리지 않고 새 깃발이 없고 회피 반격 창이 닫혀 있으면 기존 3무기의 콤보 시간·히트스톱·넉백·판정·자동 조준·치명 난수 소비는 예전과 같다.
    /// 백어택·헤드어택(PositionalHitRule, 2026-10-04): 등 뒤 ±60°에서 맞히면 피해 +20%·치명 +100‰(단검 등 찌르기와는 큰 쪽 하나), 정면 ±45° 치명이면 버팀 × 1.5,
    /// 피해 숫자 위에 '백어택'·'헤드어택'. 옆에서 치거나 Tuning에서 끄면 피해·버팀·치명 난수 소비는 예전과 같다(치명 굴림은 언제나 한 번).
    /// 세 무기·오른쪽 클릭 1차(기획/세-무기-우클릭-소켓-1차.md, 몸통은 PlayerController.WeaponAct.cs): 오른쪽 클릭 = 무기 행동(State.WeaponAct 하나).
    /// 한손검과 방패 = 누르는 동안 앞 반원 막기 + 패링(반격 창 1.0초, 다음 왼쪽 클릭이 ③ 피해 × 1.5·버팀 × 2), 대검 = 누르는 동안 기 모으기 1~3단계 → 놓으면 놓아 베기,
    /// 쌍검 = 1.10초 난사 8타(재사용 6초). 쓰는 동안 구르기·공격·E·Q·무기 바꾸기로 취소할 수 없고, 막지 못한 적 공격에 맞으면 끊긴다(경직 0.35초, 대검 0.45초;
    /// 버팀 룬이면 대검 기 모으기·놓아 베기는 안 끊김). 판정 몸통은 DealHit 하나를 콤보·놓아 베기·난사가 같이 쓴다.
    /// 무기 행동이 없을 때(나머지 6종, 오른쪽 클릭을 쓰지 않을 때) 콤보·회오리·검풍·구르기·피격 경로는 예전과 비트까지 같다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(PlayerInputReader))]
    public sealed partial class PlayerController : MonoBehaviour
    {
        public const float Radius = 0.4f;
        /// <summary>시작 장비(가죽 한 벌)의 이동 몫(‰). 능력치를 넣기 전 걸음 속도에만 쓴다(넣은 뒤에는 StatSheet.MoveSpeed).</summary>
        const int StartingMovePermille = 60;
        /// <summary>
        /// 배율(Tuning.MoveSpeedScale)을 곱하기 전 시작 장비 걸음 속도(5.3). ApplyStats 전에 쓰는 값이고 화면 비교 글에도 쓴다.
        /// StatSheet.MoveSpeed(5.0 × (1 + ‰ ÷ 1000))와 같은 float 식이라 시작 장비 능력치를 넣어도 비트까지 같다.
        /// </summary>
        public const float EquippedWalkSpeed = StatBase.MoveSpeed * (1f + StartingMovePermille / 1000f);
        /// <summary>걷기 가감속(Tuning.MoveInertia): 멈춘 데서 다 빨라지기까지, 다 빠른 데서 서기까지 걸리는 시간(초).</summary>
        public const float WalkAccelTime = 0.16f;
        public const float WalkDecelTime = 0.10f;

        const float DodgeTime = 0.22f;
        const float DodgeDistance = 3.5f;
        const float DodgeInvulnerable = 0.18f;
        const float DodgeCooldownTime = 2.0f;

        const float WhirlTime = 0.6f;
        const float WhirlRadius = 2.6f;
        const float WhirlPercent = 90f;
        const float WhirlKnockback = 0.4f;
        /// <summary>회오리 재사용 2초(투지가 아껴 쓰는 몫을 맡는다, 기획/스킬-자원-트리-1차.md 1장). 예전 6초.</summary>
        const float WhirlCooldownTime = Demo6.Core.Progression.SpiritRules.WhirlCooldown;
        /// <summary>3차 버팀 피해(초안 3-4): 회오리 타마다 8, 검풍 40.</summary>
        const float WhirlPoise = 8f;
        const float WavePoise = 40f;
        /// <summary>회피 반격: 예고 공격을 구르기로 피하고 이 시간 안의 첫 타는 버팀 ×2, 피해 +20%(CounterRule).</summary>
        const float CounterWindow = CounterRule.Window;
        const float WhirlMoveScale = 0.7f;
        static readonly float[] WhirlTicks = { 0.1f, 0.3f, 0.5f };

        const float WaveCastTime = 0.25f;
        /// <summary>검풍 재사용 3초(예전 9초).</summary>
        const float WaveCooldownTime = Demo6.Core.Progression.SpiritRules.WaveCooldown;
        const float WaveCastMoveScale = 0.2f;

        const float PotionHeal = 0.4f;
        const float PotionCooldownTime = 3f;

        const float HurtFlash = 0.1f;
        const float HurtInvulnerable = 0.5f;
        const float ReviveDelay = 1.5f;

        enum State
        {
            Free,
            Swing,
            Dodge,
            Whirl,
            WaveCast,
            Down,
            /// <summary>오른쪽 클릭 무기 행동(막기·기 모으기·놓아 베기·난사, 끊김 경직·막기 깨짐 포함). 세부 단계는 _actPhase.</summary>
            WeaponAct,
        }

        public static PlayerController Instance { get; private set; }

        public int Attack { get; private set; } = 200;
        public float CritChance { get; set; } = (float)DamageMath.BaseCritChance;
        public float CritDamage { get; set; } = (float)DamageMath.BaseCritDamage;
        public WeaponAttackRule Weapon { get; private set; } = WeaponPresets.Longsword;
        public Health Health => _health;
        public Vector2 Position => _body ? _body.position : (Vector2)transform.position;
        /// <summary>
        /// 지금 걷기 속도: 장비 속도(WalkSpeed, 시작 5.3) 또는 덮어쓰기(아는 길 6.5)에 Tuning.MoveSpeedScale(기본 0.80)을 곱한 값.
        /// 웅크리면 덮어쓰기를 무시하고 장비 속도 × 배율 × Tuning.CrouchMoveScale(0.55)(시작 장비 4.24 → 약 2.33).
        /// </summary>
        public float MoveSpeed => Crouching
            ? WalkSpeed * Tuning.MoveSpeedScale * Tuning.CrouchMoveScale
            : (SpeedOverride > 0f ? SpeedOverride : WalkSpeed) * Tuning.MoveSpeedScale;
        /// <summary>장비 전투 걸음 속도(배율 곱하기 전, 장비 문서 2-1): StatSheet.MoveSpeed. 능력치를 넣기 전에는 시작 장비 값 5.3.</summary>
        public float WalkSpeed => Sheet != null ? Sheet.MoveSpeed : EquippedWalkSpeed;

        /// <summary>던전 '아는 길'(전투·보스·무기 다듬기 1차 1-2, 6.5) 같은 이동 속도 덮어쓰기(배율 곱하기 전 값). 0 이하면 장비 속도.</summary>
        public float SpeedOverride { get; set; }
        /// <summary>마지막으로 공격·스킬을 쓰거나 맞은 시각(아는 길 해제, 전투 중 판정).</summary>
        public float LastCombatActionTime { get; private set; } = -999f;
        /// <summary>마지막으로 맞은 공격이 온 자리(적·화살·덫). 피격 피가 반대쪽으로 튄다.</summary>
        public Vector2 LastHitFrom { get; private set; }
        /// <summary>쓰러지면 1.5초 뒤 제자리에서 일어나는가(전투 시험장). 던전은 끄고 말뚝에서 다시 세운다.</summary>
        public bool AutoRevive { get; set; } = true;
        /// <summary>스킬 1줄(3차 초안 4-5): 넓은 회오리 반경 +, 날 선 바람 검풍 계수 +%p, 마무리 일격 마무리 피해 +비율.</summary>
        public float WhirlRadiusBonus { get; set; }
        public float WavePercentBonus { get; set; }
        public float FinisherDamageBonus { get; set; }
        public float WhirlRadiusNow => WhirlRadius + WhirlRadiusBonus;
        public float WavePercentNow => SwordWave.Percent + WavePercentBonus;
        public bool IsDown => _state == State.Down;
        public bool IsSwinging => _state == State.Swing;
        /// <summary>지금(또는 마지막) 콤보 단계 번호(1부터)와 이름. 콤보가 끊겼으면 0.</summary>
        public int ComboStepNumber => _state == State.Swing || Time.time <= _comboExpire ? _comboStepNumber : 0;
        public string ComboStepName => _step != null ? _step.name : "";
        /// <summary>최근 연속 처치 수(2초 안에 다음 처치가 이어지면 계속).</summary>
        public int KillStreak { get; private set; }

        /// <summary>그림 고르기용 자세와 그 자세의 시간 정보.</summary>
        public PlayerPose Pose
        {
            get
            {
                switch (_state)
                {
                    case State.Swing: return PlayerPose.Attack;
                    case State.Dodge: return PlayerPose.Dodge;
                    case State.Whirl: return PlayerPose.Whirl;
                    case State.WaveCast: return PlayerPose.WaveCast;
                    case State.Down: return PlayerPose.Down;
                    // 무기 행동(끊김 경직·막기 깨짐 포함): 그림은 ActPhase 등을 직접 읽는다. 시간 약속: PoseTime = ActTime, PoseDuration = 0, PoseHitTime = −1.
                    case State.WeaponAct: return PlayerPose.WeaponAct;
                }
                if (Time.time - _hurtTime < 0.2f) return PlayerPose.Hurt;
                return IsMoving ? PlayerPose.Move : PlayerPose.Idle;
            }
        }

        public float PoseTime => _state == State.Down ? _downTimer : _state == State.Free ? Time.time - _hurtTime : _stateTime;

        public float PoseDuration
        {
            get
            {
                switch (_state)
                {
                    case State.Swing: return _swingDuration;
                    case State.Dodge: return DodgeTime;
                    case State.Whirl: return WhirlTime;
                    case State.WaveCast: return WaveCastTime;
                    default: return 0f;
                }
            }
        }

        public float PoseHitTime => _state == State.Swing && _step != null ? HitTime(0) : _state == State.Whirl ? WhirlTicks[0] : -1f;
        public int ComboIndex => Mathf.Max(0, _comboStepNumber - 1);
        public Vector2 FacingDirection => _facing;
        public bool IsMoving => _body && _body.linearVelocity.sqrMagnitude > 0.04f;
        /// <summary>
        /// 반격 칼빛을 켤 때인가(시험 패널·그림): 회피 반격 창 또는 패링 반격 창(ParryCounterReady). 피해·버팀 배율은 회피 창(DodgeCounterReady)만 본다.
        /// </summary>
        public bool CounterReady => DodgeCounterReady || ParryCounterReady;
        /// <summary>백어택·헤드어택(PositionalHitRule)으로 맞힌 횟수(이 판, 시험 패널 표시용). 처형이 난 타도 센다.</summary>
        public int BackAttackHits { get; private set; }
        public int HeadAttackHits { get; private set; }

        // ── 전투·보스·무기 다듬기 1차 계약(기획/전투-보스-무기-다듬기-1차.md). PlayerController는 꾸러미 ⑤가 소유한다 ──
        // 다른 꾸러미는 아래 읽기 전용 값과 사건만 쓴다(시야·소음 ①, 소리 ④, 몸 그림 ⑦, HUD ①⑧, 감지 ⑧). 계약 단계 값은 모두 '꺼짐'이다.

        /// <summary>웅크렸는가(결정 ③, 키 C로 켜고 끔, 구르기·기본공격·회오리·검풍·쓰러짐·말뚝에서 다시 섬에 일어섬).</summary>
        public bool Crouching { get; private set; }
        /// <summary>웅크림이 바뀔 때(새 값).</summary>
        public event System.Action<bool> CrouchChanged;

        /// <summary>웅크림을 바꾸고 알린다(같은 값이면 아무것도 하지 않음). 부르는 곳: C 키(자유 상태에서만), 일어서는 동작들.</summary>
        void SetCrouching(bool on)
        {
            if (Crouching == on) return;
            Crouching = on;
            CrouchChanged?.Invoke(on);
        }

        /// <summary>
        /// 휘두르기·구르기·스킬 중(히트스톱 포함)에 누른 C(마무리 검토 ④). 버리지 않고 다음에 자유 상태가 될 때 한 번 바꾼다(두 번 누르면 없던 일).
        /// 쓰러짐·순간 이동·다시 섬에서는 지운다.
        /// </summary>
        bool _crouchQueued;
        /// <summary>
        /// 소음 배율: 웅크리면 Tuning.CrouchNoiseScale(0.3), 아니면 1. 잠든 적 등 뒤 감지·큰 소리 반경·발소리 음량에 곱한다.
        /// 웅크린 채 시작한 기본공격은 첫 판정까지 조용함을 유지한다(휘두르며 일어선 순간 등 뒤 감지 3에 걸려 기습 처형이 '들킴'이 되지 않게).
        /// </summary>
        public float NoiseScale => Crouching || SneakWindup ? Tuning.CrouchNoiseScale : 1f;
        bool SneakWindup => _state == State.Swing && _swingFromCrouch && _hitsDone == 0;
        /// <summary>웅크린 채 시작한 기본공격이 첫 판정 전인가(Enemy 등 뒤 감지가 이 동안 듣지 않는다: 공격 내딛기가 붙여도 그 타가 '들킴'이 되지 않게).</summary>
        public bool SneakStriking => SneakWindup;
        /// <summary>
        /// 처형 자세(대검 내려찍기 자세를 칼 1.25배로, ExecutionRule.SwordScale): 그림(⑦)이 이 동안 그 자세를 그린다.
        /// 근접 처형(무너짐 처형·기습 처형)을 한 그 휘두르기 동안만 켜진다(구르기 등으로 동작이 끝나면 함께 끝남). 검풍 처형은 자세가 없다.
        /// 시간 0 = 처형 순간(0.06초 당겨 붙기 시작), ExecutionPoseHitTime(0.06초) = 내려찍는 순간, 길이 ExecutionRule.PoseSeconds(0.4초).
        /// </summary>
        public bool ExecutionPoseActive => _state == State.Swing && _execPoseAction == _actionId && Time.time - _execPoseStart < ExecutionRule.PoseSeconds;
        public float ExecutionPoseTime => ExecutionPoseActive ? Time.time - _execPoseStart : 0f;
        public float ExecutionPoseDuration => ExecutionPoseActive ? ExecutionRule.PoseSeconds : 0f;
        public float ExecutionPoseHitTime => ExecutionRule.PullSeconds;
        /// <summary>
        /// 사슬 철퇴 관성이 지금 동작에 붙었는가(InertiaRule, 그림·HUD용). 마무리(또는 관성이 붙은 동작)가 끝난 뒤 0.5초 안에 관성 단계를 다시 치면 켜지고,
        /// 회오리·검풍·구르기를 쓰면 다음 마무리까지 끊긴다. 그래서 ① → ②가 이어지는 동안 ①② 모두 × 0.9다(계수 1.46 → 1.55).
        /// </summary>
        public bool InertiaActive => _state == State.Swing && _inertiaActive;

        /// <summary>회피 반격 첫 타 버팀 배율(회피 창만 봄, 패링 반격은 DealHit에서 따로 × 2).</summary>
        float CounterMultiplier => CounterRule.PoiseScale(DodgeCounterReady);

        /// <summary>장비 공격력(맨몸 100 + 무기)을 넣는다.</summary>
        public void SetAttack(int attack) => Attack = Mathf.Max(1, attack);

        /// <summary>마지막으로 ApplyStats로 넣은 능력치(장비 문서 2-3). 아직 넣지 않았으면 null(예전 상수 그대로 돎).</summary>
        public StatSheet Sheet { get; private set; }

        /// <summary>지금 공격 속도(‰). SwingTiming·조준 회전(TopDownPlayerRig)·타당 버팀이 쓴다. 넣기 전 0.</summary>
        public int AttackSpeedPermille => Sheet != null ? Sheet.AttackSpeedPermille : 0;

        /// <summary>
        /// 능력치 한 입구(장비 문서 2-3). 부르는 곳: 던전 = Inventory(장착·레벨·스킬이 바뀔 때), 전투 시험장 = CombatTestRoot(무기·손잡이·층이 바뀔 때).
        /// 공격력·치명 확률·치명 피해·최대 체력(SetMax: 늘어난 만큼 지금 체력도)·방어·무기 종류를 넣고, 나머지는 Sheet에서 바로 읽는다:
        /// 공격 속도(다음 StartSwing의 SwingTiming·타당 버팀), 이동(WalkSpeed), 재사용(× CooldownFactor, 남은 재사용은 새 최대로 자름),
        /// 스킬·보스 피해(DamageMath 인자), 체력 흡수(기본공격·스킬), 초당 재생, 처치 시 회복.
        /// 다시 불러도 같은 값이면 아무것도 바뀌지 않는다(체력·재사용·무기 그대로).
        /// </summary>
        public void ApplyStats(StatSheet sheet)
        {
            if (sheet == null) return;
            Sheet = sheet;
            Attack = Mathf.Max(1, sheet.Attack);
            CritChance = sheet.CritChance;
            CritDamage = sheet.CritDamage;
            if (_health)
            {
                _health.SetMax(sheet.MaxHp);
                _health.Defense = sheet.Defense;
            }
            // 재사용 감소가 늘면 돌고 있는 재사용도 새 최대를 넘지 않게 자른다(같은 값이면 그대로).
            _whirlCooldown = Mathf.Min(_whirlCooldown, WhirlCooldownMax);
            _waveCooldown = Mathf.Min(_waveCooldown, WaveCooldownMax);
            var rule = sheet.WeaponRule;
            if (rule != null) SetWeapon(rule);
        }

        /// <summary>스킬 재사용 배율(1 − 재사용 감소). 능력치를 넣기 전에는 1.</summary>
        public float CooldownFactor => Sheet != null ? Sheet.CooldownFactor : 1f;
        /// <summary>스킬 피해 보너스(0.1 = +10%). 회오리·검풍 피해에만 곱한다.</summary>
        double SkillDamageBonus => Sheet != null ? Sheet.SkillDamagePermille / 1000.0 : 0.0;
        /// <summary>보스 피해 보너스(0.1 = +10%). Enemy.IsBoss인 적에게만 곱한다.</summary>
        double BossDamageBonus => Sheet != null ? Sheet.BossDamagePermille / 1000.0 : 0.0;
        int LifeStealPermille => Sheet != null ? Sheet.LifeStealPermille : 0;
        int HpRegenPerSecond => Sheet != null ? Sheet.HpRegen : 0;
        int OnKillHealAmount => Sheet != null ? Sheet.OnKillHeal : 0;

        /// <summary>지금 휘두르는(또는 마지막) 동작의 시간표(SwingTiming.Plan). 시험 기록용.</summary>
        public SwingPlan CurrentSwingPlan => _plan;
        /// <summary>기본공격 동작 번호(StartSwing마다 1씩 오름). BasicHitInfo.ActionId와 같다.</summary>
        public int ActionId => _actionId;

        /// <summary>치명 굴림 전용 난수의 씨앗(판마다 다름, 장비 문서 3-4 '치명 난수'). 시험 기록에 함께 적는다.</summary>
        public ulong CritSeed { get; private set; }

        /// <summary>치명 굴림 난수를 이 씨앗으로 다시 시작한다(시험 재현용). 피해 굴림 흐름(_rng)은 건드리지 않는다.</summary>
        public void ReseedCrit(ulong seed)
        {
            CritSeed = seed;
            _critRng = new Pcg32Random(seed, CritStream);
        }

        /// <summary>지금 치명 확률로 치명을 한 번 굴린다(치명 전용 흐름). 플레이어 몫 피해를 따로 넣는 곳(전설 연쇄 번개 등)이 쓴다.</summary>
        public bool RollCrit() => _critRng.NextDouble() < CritChance;

        /// <summary>플레이어 겉모습 id 4개(장비 문서 9-1). 그림 쪽이 LookChanged를 듣고 몸·투구·주먹·장화를 고른다.</summary>
        public GearLook Look { get; private set; } = GearLook.Starting;
        public event System.Action LookChanged;

        /// <summary>겉모습을 바꾼다(Inventory가 갑옷·투구·장갑·장화를 바꿀 때). 같으면 알리지 않는다.</summary>
        public void SetLook(GearLook look)
        {
            if (look.Equals(Look)) return;
            Look = look;
            LookChanged?.Invoke();
        }

        /// <summary>레벨·스킬로 최대 체력이 바뀔 때. 늘어난 만큼 지금 체력도 늘린다.</summary>
        public void SetMaxHp(int max) => _health.SetMax(max);

        /// <summary>던전: 쓰러진 뒤 말뚝에서 다시 선다(물약·재사용 채움, 2초 무적).</summary>
        public void ReviveAt(Vector2 position)
        {
            Teleport(position);
            ClearSpirit();
            _whirlCooldown = 0f;
            _waveCooldown = 0f;
            _dodgeCooldown = 0f;
            _potionCooldown = 0f;
            _health.Revive();
            SetCrouching(false);
            StandUp(true);
        }

        /// <summary>순간 이동(말뚝 이동·F1 칸 이동·시험장 배치·보스 재도전). 이동은 사건 경계라 웅크림도 풀고 일어선다(마무리 검토 ⑫).</summary>
        public void Teleport(Vector2 position)
        {
            // 무기 행동 중이면 조용히 끝(기·남은 타가 사라짐, 5-3).
            EndWeaponActQuietly();
            _body.position = position;
            transform.position = position;
            _knockTime = 0f;
            _staggerTime = 0f;
            _walkVelocity = Vector2.zero;
            _walkSnap = false;
            _crouchQueued = false;
            SetCrouching(false);
        }
        public float KillStreakTime { get; private set; } = -999f;
        public int BestKillStreak { get; private set; }
        /// <summary>지금 구르기로 피할 수 있나(예고 계측). 무기 행동·끊김 경직·막기 깨짐 중에는 구를 수 없어 false.</summary>
        public bool DodgeReady => _dodgeCooldown <= 0f && _state != State.Whirl && _state != State.WaveCast && _state != State.Down && _state != State.WeaponAct;
        public float WhirlCooldown => _whirlCooldown;
        public float WaveCooldown => _waveCooldown;
        public float DodgeCooldown => _dodgeCooldown;
        /// <summary>회오리 6초·검풍 9초 × (1 − 재사용 감소)(장비 문서 2-1). 감소 0이면 6·9 그대로.</summary>
        public float WhirlCooldownMax => WhirlCooldownTime * CooldownFactor;
        public float WaveCooldownMax => WaveCooldownTime * CooldownFactor;
        public float DodgeCooldownMax => DodgeCooldownTime;
        /// <summary>
        /// 원정마다 드는 물약 칸 수(재화 쓸 곳 1차 6-3). 기본 3, 허리 병걸이를 샀으면 4(던전에 들어설 때 ProfileCarry.Apply가 SetPotionCapacity로 넣음).
        /// 가득 채우는 곳(층 시작 ApplyBaseline·채움 Refill·쓰러진 뒤 다시 섬)과 계단으로 잇는 물약 수 자르기(RestoreVitals)가 모두 이 값을 쓴다.
        /// </summary>
        public int PotionCapacity { get; private set; } = Demo6.Core.Town.ForgeShop.BasePotions;
        public int Potions { get; private set; } = Demo6.Core.Town.ForgeShop.BasePotions;
        public float PotionCooldown => _potionCooldown;
        public float PotionCooldownMax => PotionCooldownTime;
        public int Downs { get; private set; }
        public event System.Action<WeaponAttackRule> WeaponChanged;

        Rigidbody2D _body;
        Health _health;
        PlayerInputReader _input;
        SpriteRenderer _bodySprite;
        SpriteFlash _flash;
        Transform _facingMark;
        Camera _cam;
        /// <summary>피해 굴림(0.92~1.08)과 몬스터가 때리는 피해 굴림. 예전 씨앗·흐름 그대로.</summary>
        readonly IRandom _rng = new Pcg32Random(20261002, 7);
        /// <summary>치명 굴림 전용 흐름 번호(피해 굴림 7, 처치 보상 23, 궤짝 41과 겹치지 않음).</summary>
        const ulong CritStream = 31;
        /// <summary>치명 굴림 전용(장비 문서 3-4): 치명 확률이 바뀌어도 피해 굴림 순서가 밀리지 않는다. 씨앗은 판마다 다르다(Awake).</summary>
        IRandom _critRng;
        readonly List<Collider2D> _overlap = new List<Collider2D>(32);
        readonly List<(Enemy enemy, float dist)> _targets = new List<(Enemy, float)>(16);
        readonly HashSet<Enemy> _seen = new HashSet<Enemy>();
        ContactFilter2D _enemyFilter;

        State _state;
        float _stateTime;
        Vector2 _aimWorld;
        Vector2 _aimDir = Vector2.right;
        Vector2 _facing = Vector2.right;

        Vector2 _swingDir;
        float _swingDuration;
        /// <summary>이 동작의 시간표(StartSwing에서 한 번 구함): 길이·판정 순간·이월 상한.</summary>
        SwingPlan _plan;
        ComboStep _step;
        int _comboIndex;
        int _comboStepNumber;
        float _comboExpire = -999f;
        int _hitsDone;
        int _actionId;
        bool _swingStopped;
        /// <summary>이 동작에서 보통·무거운 치명이 났는가(마지막 타에만 넉백을 주는 단계의 넉백 0.9 올림). 가벼운 치명은 보통 타와 같다.</summary>
        bool _swingStrongCrit;
        /// <summary>이 동작에서 낸 치명 연출 단계(동작마다 처음 치명 때 한 번 정함, 장비 문서 3-4).</summary>
        CritTier _swingCritShown;
        /// <summary>마지막으로 무거운 치명 연출을 낸 게임 시각(0.5초 제한).</summary>
        double _lastHeavyCrit = double.NegativeInfinity;
        /// <summary>체력 흡수·초당 재생의 1 미만 나머지(체력은 정수라 모았다가 넣는다).</summary>
        float _lifeStealCarry;
        float _regenCarry;
        /// <summary>휘두르는 중에 누른 구르기·스킬. 버퍼 0.15초가 지나도 쓸 수 있는 순간까지 보존한다.</summary>
        bool _pendingDodge;
        bool _pendingSkill1;
        bool _pendingSkill2;
        Enemy _lastTarget;
        float _lastAttackTime = -999f;
        float _lungeTime;
        Vector2 _lungeVelocity;

        Vector2 _dodgeDir;
        int _whirlTicksDone;
        Vector2 _waveDir;

        float _dodgeCooldown;
        float _whirlCooldown;
        float _waveCooldown;
        float _potionCooldown;
        float _knockTime;
        float _staggerTime;
        Vector2 _knockVelocity;
        float _downTimer;
        float _hurtTime = -999f;
        float _counterUntil = -999f;
        /// <summary>구르기 번호(StartDodge마다 1씩)와 회피 반격 창을 연 마지막 구르기 번호(한 번 구르기에 CounterOpened 한 번).</summary>
        int _dodgeCount;
        int _counterDodge = -1;
        /// <summary>이 기본공격을 웅크린 채 시작했나(기습 처형 조건, StartSwing이 일어서기 전에 적음).</summary>
        bool _swingFromCrouch;
        /// <summary>기습 처형 결과(SwingHit에서 피해를 넣기 전에 대상마다 정함, _targets와 같은 차례).</summary>
        readonly List<AmbushOutcome> _ambush = new List<AmbushOutcome>(16);
        /// <summary>이 동작(기본공격 한 번·회오리 한 번)에서 위치 글자(백어택·헤드어택)를 띄운 적. 연타·회오리 여러 타에 글자를 겹쳐 띄우지 않는다.</summary>
        readonly HashSet<Enemy> _tagged = new HashSet<Enemy>();
        /// <summary>근접 처형 자세를 시작한 시각과 그 기본공격 동작 번호.</summary>
        float _execPoseStart = -999f;
        int _execPoseAction = -1;
        /// <summary>
        /// 사슬 철퇴 관성(InertiaRule): 마무리나 관성이 붙은 동작이 끝난 시각, 그 뒤 스킬·구르기로 끊겼나, 지금 동작에 관성이 붙었나.
        /// </summary>
        float _inertiaAnchor = -999f;
        bool _inertiaBroken;
        bool _inertiaActive;
        /// <summary>직접 걷는 속도(가감속을 거친 값). 휘두르기·스킬 중 걷기도 적어 두어 끝난 뒤 그 속도에서 이어 붙는다.</summary>
        Vector2 _walkVelocity;
        /// <summary>구르기·내딛기 직후 첫 걷기는 가감속 없이 바로 목표 속도(지금처럼 미끄러지지 않게).</summary>
        bool _walkSnap;

        public static PlayerController Create(Vector2 position)
        {
            var go = new GameObject("검사");
            go.layer = Layers.Player;
            go.transform.position = position;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Radius;

            var visual = new GameObject("Body");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * (Radius * 2f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = ShapeSprites.Circle;
            sr.color = Palette.Player;

            var mark = new GameObject("Facing");
            mark.transform.SetParent(go.transform, false);
            var markSprite = mark.AddComponent<SpriteRenderer>();
            markSprite.sprite = ShapeSprites.Triangle;
            markSprite.color = Palette.Player;
            markSprite.sortingOrder = 1;
            var markVisual = mark.transform;
            markVisual.localScale = Vector3.one * 0.32f;

            var flash = go.AddComponent<SpriteFlash>();
            flash.target = sr;
            flash.baseColor = Palette.Player;

            go.AddComponent<Health>();
            go.AddComponent<PlayerInputReader>();
            go.AddComponent<YSort>();
            var player = go.AddComponent<PlayerController>();
            player._facingMark = markVisual;
            go.AddComponent<PlayerVisual>().Bind(player, sr, flash, markVisual);
            // 전설 고유 효과 3종(장비 문서 6장). 켜진 효과가 없으면 아무것도 하지 않는다.
            go.AddComponent<LegendEffects>();
            // 전투·보스·무기 다듬기 1차 살 붙이기(4장): 두 시험장 모두 플레이어에 붙는다. 각자 OnEnable에서 사건을 구독한다(ResetStatics 뒤라 안전).
            go.AddComponent<WallSlam>();
            go.AddComponent<PackFear>();
            go.AddComponent<ExecutionFx>();
            go.AddComponent<CounterFx>();
            go.AddComponent<SleeperFidget>();
            return player;
        }

        void Awake()
        {
            Instance = this;
            _body = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _input = GetComponent<PlayerInputReader>();
            _flash = GetComponent<SpriteFlash>();
            _bodySprite = _flash ? _flash.target : GetComponentInChildren<SpriteRenderer>();
            _enemyFilter = Layers.EnemyFilter();
            // 치명 굴림 씨앗은 판마다 다르다(시계). 시험 기록에는 CritSeed를 함께 적는다.
            ReseedCrit((ulong)System.DateTime.UtcNow.Ticks);
            _health.Init(2400, 120);
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            SpiritAwake();
        }

        void OnDestroy()
        {
            SpiritDestroy();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 체력·물약을 채운다. 능력치를 아직 넣지 않았으면(ApplyStats 전) 예전처럼 층 기준 장비(공격·체력·방어)로 맞추고,
        /// 넣은 뒤에는 Sheet의 최대 체력·방어로 채우기만 한다(공격·체력·방어는 ApplyStats 한 입구가 정함, 장비 문서 2-3).
        /// </summary>
        public void ApplyBaseline(int floor)
        {
            if (Sheet != null)
            {
                _health.Init(Sheet.MaxHp, Sheet.Defense);
            }
            else
            {
                var b = FloorScaling.Baseline(floor);
                Attack = b.Attack;
                _health.Init(b.MaxHp, b.Defense);
            }
            _regenCarry = 0f;
            _lifeStealCarry = 0f;
            Potions = PotionCapacity;
            ClearSpirit();
            if (_state == State.Down) StandUp(false);
        }

        /// <summary>
        /// 물약 칸 수를 바꾼다(재화 쓸 곳 1차 6-3: 허리 병걸이면 4). 1 미만은 1로 올린다.
        /// 지금 물약이 가득(옛 칸 수와 같음)이었으면 새 칸까지 채우고, 아니면 새 칸 수로 자르기만 한다.
        /// </summary>
        public void SetPotionCapacity(int capacity)
        {
            capacity = Mathf.Max(1, capacity);
            bool full = Potions == PotionCapacity;
            PotionCapacity = capacity;
            Potions = full ? capacity : Mathf.Min(Potions, capacity);
        }

        /// <summary>무기를 바꾼다(장착·전투 시험장·F1 시험 패널). 무기 행동 중(끊김 경직·막기 깨짐 포함)에는 막는다: 무기를 바꿔 행동을 끝내는 길을 막는다(0-3의 16).</summary>
        public void SetWeapon(WeaponAttackRule weapon)
        {
            if (weapon == null || weapon == Weapon) return;
            if (InWeaponAct) return;
            Weapon = weapon;
            // 패링 반격 창은 그 방패의 것(다른 무기로 ③ 반격 찌르기를 하지 않게).
            _riposteUntil = -999f;
            ResetCombo();
            // 바꿔 쥐면 관성은 처음부터.
            _inertiaAnchor = -999f;
            _inertiaActive = false;
            if (_state == State.Swing) EnterFree();
            WeaponChanged?.Invoke(weapon);
        }

        public void Refill()
        {
            // 채움: 무기 행동은 조용히 끝, 난사 재사용 0·방패 버팀 가득(5-3).
            EndWeaponActQuietly();
            ResetWeaponActCooldowns();
            _health.Revive();
            Potions = PotionCapacity;
            _potionCooldown = 0f;
            _whirlCooldown = 0f;
            _waveCooldown = 0f;
            _dodgeCooldown = 0f;
            if (_state == State.Down) StandUp(false);
        }

        /// <summary>
        /// 계단으로 내려온 새 장면에서 원정 몫(매판 새 탐험 1차 3-3)의 체력·물약을 잇는다. 레벨 체력을 넣은 뒤 부른다.
        /// 값이 음수면(가득) 그대로 둔다. 물약 수는 지금 칸 수(PotionCapacity)로 자른다(재화 쓸 곳 1차 6-3).
        /// </summary>
        public void RestoreVitals(int hp, int potions)
        {
            if (hp > 0) _health.SetCurrent(hp);
            if (potions >= 0) Potions = Mathf.Clamp(potions, 0, PotionCapacity);
        }

        /// <summary>쓰러짐에서 일어난다. 자연 부활이면 물약을 채우고 2초 무적과 깜빡임을 준다.</summary>
        void StandUp(bool naturalRevive)
        {
            _bodySprite.transform.localScale = Vector3.one * (Radius * 2f);
            if (_health.Dead) _health.Revive();
            EndWeaponActQuietly();
            EnterFree();
            if (naturalRevive)
            {
                // 다시 섬: 난사 재사용 0·방패 버팀 가득(5-3).
                ResetWeaponActCooldowns();
                Potions = PotionCapacity;
                _health.GrantInvulnerability(2f);
                _flash.Blink(2f);
            }
        }

        public void ResetDowns()
        {
            Downs = 0;
            BestKillStreak = 0;
            KillStreak = 0;
        }

        void ResetCombo()
        {
            _comboIndex = 0;
            _comboExpire = -999f;
        }

        /// <summary>
        /// 몬스터가 플레이어를 때릴 때 부른다(기획/세-무기-우클릭-소켓-1차.md 2-7·5-5): ① 쓰러짐 → 피함 ② 무적 → 피함(구르기 무적이면 회피 반격 창)
        /// ③ 방패 막기 가르기(ShieldRule.Resolve: 튕김 → source.Parried·반격 창, 막음 → 배율·막기 무적·방패 버팀) ④ 피해(시험 무적이면 0)
        /// ⑤ 무기 행동 끊김 또는 버팀 ⑥ 넉백(회오리·버팀이면 면함) ⑦ LastHitResult. 무기 행동이 없으면 예전 길과 비트까지 같다(피해 굴림 차례 포함).
        /// </summary>
        /// <returns>맞음·막음이면 true, 튕김·피함(무적·쓰러짐)이면 false.</returns>
        /// <param name="opensCounter">구르기로 피하면 회피 반격 창을 여는 공격인가(적의 예고 공격). 놓여 있는 덫은 false.</param>
        /// <param name="kind">공격 종류(2-7, 방패 막기·패링 표). 옛 호출은 Melee.</param>
        /// <param name="travel">공격 진행 방향(돌진·화살·휩쓸기). default면 (from − 내 위치)로 방향을 본다.</param>
        /// <param name="source">때린 적(패링이면 source.Parried(kind)). 화살·덫·낙석은 null이어도 된다.</param>
        public bool ReceiveHit(int monsterAttack, float patternPercent, Vector2 from, float knockback, bool opensCounter = true,
            HitKind kind = HitKind.Melee, Vector2 travel = default, Enemy source = null)
        {
            if (_state == State.Down)
            {
                LastHitResult = HitResult.Avoided;
                return false;
            }
            if (_health.IsInvulnerable)
            {
                // 구르기 무적으로 피했다: 회피 반격 창을 연다(4-2 [4], 기본 켜짐). 연출(CounterFx)은 한 번 구르기에 한 번만 낸다.
                if (opensCounter && _health.ExtraInvulnerable && Tuning.DodgeCounter)
                {
                    _counterUntil = Time.time + CounterWindow + DodgeTime;
                    if (_counterDodge != _dodgeCount)
                    {
                        _counterDodge = _dodgeCount;
                        CombatEvents.RaiseCounterOpened();
                    }
                }
                LastHitResult = HitResult.Avoided;
                return false;
            }
            int damage = DamageMath.ToPlayer(monsterAttack, patternPercent, DamageMath.Roll(_rng), _health.Defense);
            // 피격 연출(피 튀는 방향)이 실제로 때린 쪽을 쓰게 피해를 넣기 전에 적어 둔다.
            LastHitFrom = from;
            // ③ 방패 막기·패링. 막기가 켜져 있지 않으면 Hit(배율 1)이라 아래는 예전 길 그대로다. 시험 무적이어도 막기·패링 효과는 돈다.
            var guard = ResolveGuard(kind, patternPercent, from, travel);
            if (guard.Result == HitResult.Parried)
            {
                ParryHit(kind, source, guard);
                return false;
            }
            if (guard.Result == HitResult.Blocked) return BlockHit(kind, source, damage, knockback, from, guard);
            // ⑤ 끊김·버팀은 피해를 넣기 전 행동 상태로 정한다(크기 문턱 없음, 시험 무적 피해 0도 끊김). 행동 중이 아니면 둘 다 false.
            bool actLive = _state == State.WeaponAct && _actLive;
            bool armored = actLive && WeaponActRules.Armored(_actKind, _actPhase, HasSuperArmorRune, Tuning.SuperArmorAllActs);
            bool interruptible = actLive && (_actKind != WeaponActKind.Charge || GreatswordCharge.BreaksOnHit(_actPhase, _actHitsDone, false));
            bool interrupts = WeaponActRules.Interrupts(interruptible, armored, false, damage, _health.Max, Tuning.WeaponActInterruptMinPermille);
            if (Tuning.Invincible)
            {
                WorldOverlay.Number(Position + Vector2.up * Radius, 0, NumberKind.Taken);
                _health.GrantInvulnerability(HurtInvulnerable);
                _flash.Flash(Palette.NumberTaken, HurtFlash);
                _flash.Blink(HurtInvulnerable);
                AfterHitWeaponAct(interrupts, armored);
                LastHitResult = HitResult.Hit;
                return true;
            }
            int applied = _health.ApplyDamage(damage, false);
            if (applied <= 0)
            {
                LastHitResult = HitResult.Avoided;
                return false;
            }
            AfterHitWeaponAct(interrupts, armored);
            LastHitResult = HitResult.Hit;
            // 회오리 베기 중·버팀 룬으로 버틴 무기 행동 중에는 넉백·경직 면역(피해는 받음).
            if (_state != State.Whirl && !armored && knockback > 0f)
            {
                Vector2 dir = Position - from;
                if (dir.sqrMagnitude < 0.0001f) dir = -_facing;
                _knockVelocity = dir.normalized * (knockback * Tuning.KnockbackScale / 0.1f);
                _knockTime = 0.1f;
                _staggerTime = 0f;
            }
            return true;
        }

        void OnDamaged(int amount, bool crit)
        {
            // 방패로 막은 피해(2-5): 피격 자세·소리·숫자·깜빡임·흔들림·피격 무적 0.5초가 없다(막은 숫자·'퉁'은 WeaponActFx.Blocked가 냄).
            // PlayerDamaged도 내지 않는다(듣는 GoreSystem이 플레이어 피·화면 붉은 테두리를 내므로). 그래서 계측 DamageTaken에 막은 피해는 들어가지 않는다.
            if (_blockingHit)
            {
                LastCombatActionTime = Time.time;
                return;
            }
            _hurtTime = Time.time;
            LastCombatActionTime = Time.time;
            Sfx.Play(SfxKind.Hurt);
            WorldOverlay.Number(Position + Vector2.up * Radius, amount, NumberKind.Taken);
            CombatEvents.RaisePlayerDamaged(amount);
            _health.GrantInvulnerability(HurtInvulnerable);
            _flash.Flash(Palette.NumberTaken, HurtFlash);
            _flash.Blink(HurtInvulnerable);
            ScreenShake.Add(0.12f, 0.12f);
        }

        void OnDied()
        {
            // 쓰러지면 무기 행동은 조용히 끝(기·남은 타가 사라짐).
            EndWeaponActQuietly();
            ResetCombo();
            _crouchQueued = false;
            SetCrouching(false);
            _state = State.Down;
            _downTimer = 0f;
            _health.ExtraInvulnerable = false;
            Downs++;
            CombatEvents.RaisePlayerDowned();
            _bodySprite.transform.localScale = new Vector3(Radius * 2f, Radius * 0.9f, 1f);
        }

        void Update()
        {
            if (TimeScaleService.Paused) return;
            float dt = Time.deltaTime;
            TickSpirit(dt);

            if (!_cam) _cam = Camera.main;
            if (_cam)
            {
                Vector3 sp = _input.PointerScreen;
                sp.z = -_cam.transform.position.z;
                _aimWorld = _cam.ScreenToWorldPoint(sp);
                Vector2 toAim = _aimWorld - Position;
                if (toAim.sqrMagnitude > 0.0001f) _aimDir = toAim.normalized;
            }

            // 숫자키 1~9로 무기 종류를 바꾸지 않는다. 무기 종류는 F1 시험 패널 단추가 SetWeapon을 부른다(키 배치 1차 0장 2·3).

            _dodgeCooldown = Mathf.Max(0f, _dodgeCooldown - dt);
            _whirlCooldown = Mathf.Max(0f, _whirlCooldown - dt);
            _waveCooldown = Mathf.Max(0f, _waveCooldown - dt);
            _potionCooldown = Mathf.Max(0f, _potionCooldown - dt);
            Regenerate(dt);
            // 난사 재사용·방패 버팀 회복(무기 행동 밖에서도 돎).
            TickWeaponActTimers(dt);

            if (_state == State.Down)
            {
                _downTimer += dt;
                if (AutoRevive && _downTimer >= ReviveDelay)
                {
                    _whirlCooldown = 0f;
                    _waveCooldown = 0f;
                    _dodgeCooldown = 0f;
                    _potionCooldown = 0f;
                    StandUp(true);
                }
                return;
            }

            if (_input.PotionPressed) TryDrinkPotion();
            // C: 웅크리기 켜고 끄기(결정 ③). 휘두르기·구르기·스킬 중에 누르면 기억해 두었다가 끝나 자유 상태가 되는 순간 바꾼다.
            if (_input.CrouchPressed)
            {
                if (_state == State.Free) SetCrouching(!Crouching);
                else _crouchQueued = !_crouchQueued;
            }

            switch (_state)
            {
                case State.Free:
                    TryStartAction(true);
                    break;

                case State.Swing:
                    _stateTime += dt;
                    _lastAttackTime = Time.time;
                    HoldPendingInputs();
                    while (_hitsDone < _step.hits && _stateTime >= HitTime(_hitsDone))
                        SwingHit(_hitsDone++);
                    // 기획 3-2: 판정이 나간 뒤에는 구르기로 동작을 끊을 수 있다. 스킬은 휘두르기가 끝나면 바로 나간다.
                    if (_hitsDone >= 1 && _pendingDodge && _dodgeCooldown <= 0f)
                    {
                        StartDodge();
                        break;
                    }
                    // 무기 행동(5-2): 막기만 첫 판정이 나간 뒤 누른 오른쪽 클릭으로 휘두르기를 끊고 바로 든다. 판정 전에 누른 막기와 기 모으기·난사는 동작이 끝날 때(0-3의 19).
                    if (_hitsDone >= 1 && _input.WeaponActBuffered && WeaponActRules.CutsSwing(ActKind) && TryStartWeaponAct(false)) break;
                    if (_stateTime >= _swingDuration)
                    {
                        float carry = _stateTime - _swingDuration;
                        // 마무리까지 갔으면 1단계로, 아니면 잠깐 동안 다음 단계를 이어 갈 수 있다.
                        _comboExpire = Time.time + Weapon.comboResetTime;
                        if (_step.finisher) _comboIndex = 0;
                        // 관성(InertiaRule): 마무리나 관성이 붙은 동작이 끝난 시각을 적는다. 이어 치면 다음 관성 단계가 × 0.9.
                        if (_step.finisher || _inertiaActive)
                        {
                            _inertiaAnchor = Time.time - carry;
                            _inertiaBroken = false;
                        }
                        if (_pendingSkill1 && _whirlCooldown <= 0f && SkillGate(1)) StartWhirl();
                        else if (_pendingSkill2 && _waveCooldown <= 0f && SkillGate(2)) StartWave();
                        else if (_pendingDodge && _dodgeCooldown <= 0f) StartDodge();
                        else if (TryStartWeaponAct(_pendingWeaponAct)) { }
                        else if (_input.AttackHeld)
                        {
                            // 넘친 시간을 다음 휘두르기로 넘겨 프레임에 따라 공격 속도가 줄지 않게 한다(상한 = 새 동작 길이 × 0.5, 장비 문서 3-3 규칙 5).
                            StartSwing();
                            _stateTime = Mathf.Min(carry, _plan.CarryCap);
                            while (_hitsDone < _step.hits && _stateTime >= HitTime(_hitsDone))
                                SwingHit(_hitsDone++);
                        }
                        else EnterFree();
                    }
                    break;

                case State.Dodge:
                    _stateTime += dt;
                    _health.ExtraInvulnerable = _stateTime < DodgeInvulnerable;
                    if (_stateTime >= DodgeTime) EnterFree();
                    break;

                case State.Whirl:
                    _stateTime += dt;
                    while (_whirlTicksDone < WhirlTicks.Length && _stateTime >= WhirlTicks[_whirlTicksDone])
                    {
                        WhirlTick();
                        _whirlTicksDone++;
                    }
                    if (_stateTime >= WhirlTime) EnterFree();
                    break;

                case State.WaveCast:
                    _stateTime += dt;
                    if (_stateTime >= WaveCastTime)
                    {
                        SpawnWaves();
                        EnterFree();
                    }
                    break;

                case State.WeaponAct:
                    TickWeaponAct(dt);
                    break;
            }

            UpdateFacing();
        }

        /// <param name="allowAttack">자유 상태에서는 기본공격도 시작할 수 있다.</param>
        void HoldPendingInputs()
        {
            if (_input.DodgeBuffered)
            {
                _pendingDodge = true;
                _input.ConsumeDodge();
            }
            if (_input.Skill1Buffered)
            {
                _pendingSkill1 = true;
                _input.ConsumeSkill1();
            }
            if (_input.Skill2Buffered)
            {
                _pendingSkill2 = true;
                _input.ConsumeSkill2();
            }
            // 오른쪽 클릭: 첫 판정 전에 누른 것만 들고 있다가 동작이 끝날 때 시작(판정 뒤 누른 것은 Update가 바로 끊고 시작).
            HoldPendingWeaponAct();
        }

        void ClearPending()
        {
            _pendingDodge = false;
            _pendingSkill1 = false;
            _pendingSkill2 = false;
            _pendingWeaponAct = false;
        }

        /// <summary>자유 상태 입력. 차례: 구르기 > 회오리(E) > 검풍(Q) > 무기 행동(오른쪽 클릭) > 공격(5-2).</summary>
        bool TryStartAction(bool allowAttack)
        {
            // 끊김 경직·막기 깨짐 중에는 아무것도 시작하지 않는다(그 동안은 State.WeaponAct라 여기 오지 않지만 지켜 둔다).
            if (Flinching) return false;
            if (_input.DodgeBuffered && _dodgeCooldown <= 0f)
            {
                StartDodge();
                return true;
            }
            if (_input.Skill1Buffered && _whirlCooldown <= 0f && SkillGate(1))
            {
                StartWhirl();
                return true;
            }
            if (_input.Skill2Buffered && _waveCooldown <= 0f && SkillGate(2))
            {
                StartWave();
                return true;
            }
            if (TryStartWeaponAct(false)) return true;
            if (allowAttack && _input.AttackHeld)
            {
                StartSwing();
                return true;
            }
            return false;
        }

        void FixedUpdate()
        {
            float fdt = Time.fixedDeltaTime;
            Vector2 velocity;
            Vector2 move = _input.Move;
            if (move.sqrMagnitude > 1f) move.Normalize();

            if (_knockTime > 0f)
            {
                velocity = _knockVelocity;
                _knockTime -= fdt;
                // 기획 11-6: 넉백 뒤 경직 0.15초 동안은 이동 입력으로 덮어쓰지 않는다.
                if (_knockTime <= 0f) _staggerTime = 0.15f;
                StopWalk();
            }
            else if (_staggerTime > 0f && _state != State.Dodge)
            {
                velocity = Vector2.zero;
                _staggerTime -= fdt;
                StopWalk();
            }
            else
            {
                switch (_state)
                {
                    case State.Swing:
                        if (_lungeTime > 0f)
                        {
                            velocity = _lungeVelocity;
                            _lungeTime -= fdt;
                            _walkSnap = true;
                        }
                        else velocity = ActionWalk(move * (MoveSpeed * (_step != null ? _step.moveScale : 0.4f)));
                        break;
                    case State.Dodge:
                        velocity = _dodgeDir * (DodgeDistance / DodgeTime);
                        _walkSnap = true;
                        break;
                    case State.Whirl:
                        velocity = ActionWalk(move * (MoveSpeed * WhirlMoveScale));
                        break;
                    case State.WaveCast:
                        velocity = ActionWalk(move * (MoveSpeed * WaveCastMoveScale));
                        break;
                    case State.WeaponAct:
                        // 무기 행동 걸음 = MoveSpeed × WeaponActRules.MoveScale(막기 0.5·밀쳐 내기 0.2·기 모으기 0.16→0.09·놓아 베기 단계 값·난사 0.30, 경직·깨짐 0).
                        // 난사 다가가기(첫 0.1초 최대 1.0)·놓아 베기 내딛기는 휘두르기 내딛기와 같은 길.
                        if (_lungeTime > 0f)
                        {
                            velocity = _lungeVelocity;
                            _lungeTime -= fdt;
                            _walkSnap = true;
                        }
                        else velocity = ActionWalk(move * (MoveSpeed * WeaponActMoveScale));
                        break;
                    case State.Down:
                        velocity = Vector2.zero;
                        StopWalk();
                        break;
                    default:
                        velocity = FreeWalk(move * MoveSpeed, fdt);
                        break;
                }
            }
            if (TimeScaleService.Paused) velocity = Vector2.zero;
            _body.linearVelocity = velocity;
        }

        /// <summary>넉백·경직·쓰러짐: 몸이 멈췄으니 다음 걷기는 멈춘 데서 다시 붙는다.</summary>
        void StopWalk()
        {
            _walkVelocity = Vector2.zero;
            _walkSnap = false;
        }

        /// <summary>휘두르기·회오리·검풍 중 걷기: 지금처럼 바로 그 속도. 끝난 뒤 자유 걷기가 이 속도에서 이어 붙게 적어 둔다.</summary>
        Vector2 ActionWalk(Vector2 velocity)
        {
            _walkVelocity = velocity;
            _walkSnap = false;
            return velocity;
        }

        /// <summary>자유 걷기. 가감속(Tuning.MoveInertia)을 켜면 목표 속도로 천천히 붙는다. 구르기·내딛기 직후 첫 걸음은 지금처럼 바로 목표 속도.</summary>
        Vector2 FreeWalk(Vector2 target, float dt)
        {
            if (!Tuning.MoveInertia || _walkSnap) _walkVelocity = target;
            else _walkVelocity = ApproachWalk(_walkVelocity, target, MoveSpeed, dt);
            _walkSnap = false;
            return _walkVelocity;
        }

        /// <summary>
        /// 가는 쪽 성분은 다 빨라지기까지 WalkAccelTime, 넘치거나 반대로 가는 성분과 옆 성분은 서기까지 WalkDecelTime 속도로 붙인다.
        /// 돌아설 때 먼저 멈췄다가(0.10초) 다시 붙어(0.16초) 몸이 무겁게 느껴진다.
        /// </summary>
        static Vector2 ApproachWalk(Vector2 current, Vector2 target, float maxSpeed, float dt)
        {
            if (maxSpeed <= 0f) return target;
            float accel = maxSpeed / WalkAccelTime * dt;
            float decel = maxSpeed / WalkDecelTime * dt;
            float targetSpeed = target.magnitude;
            if (targetSpeed < 0.0001f) return Vector2.MoveTowards(current, Vector2.zero, decel);
            Vector2 dir = target / targetSpeed;
            float along = Vector2.Dot(current, dir);
            Vector2 side = current - dir * along;
            if (along < 0f) along = Mathf.Min(0f, along + decel);
            else if (along < targetSpeed) along = Mathf.Min(targetSpeed, along + accel);
            else along = Mathf.Max(targetSpeed, along - decel);
            side = Vector2.MoveTowards(side, Vector2.zero, decel);
            return dir * along + side;
        }

        void EnterFree()
        {
            // 무기 행동 몫(단계·기·남은 타)을 비운다. 재사용·방패 버팀·잠금은 행동 밖 값이라 그대로다.
            ClearWeaponActFields();
            _state = State.Free;
            _stateTime = 0f;
            _lungeTime = 0f;
            _health.ExtraInvulnerable = false;
            ClearPending();
            if (_crouchQueued)
            {
                _crouchQueued = false;
                SetCrouching(!Crouching);
            }
        }

        /// <summary>k번째 판정 시각 = 첫 판정 + k × 연타 간격(SwingTiming.Plan, 공격 속도 0이면 예전 식과 비트까지 같음).</summary>
        float HitTime(int index) => _plan.HitTime(index);

        void StartSwing()
        {
            LastCombatActionTime = Time.time;
            // 기습 처형은 웅크린 채 시작한 기본공격만(결정 ③). 적어 둔 뒤 일어선다.
            _swingFromCrouch = Crouching;
            SetCrouching(false);
            if (Time.time > _comboExpire || _comboIndex >= Weapon.combo.Length) _comboIndex = 0;
            // 패링 반격(2-6): 반격 창 안의 다음 왼쪽 클릭은 콤보 단계와 상관없이 ③ '반격 찌르기'(피해 × 1.5·버팀 × 2·마무리, DealHit). 그 뒤 콤보는 1단계.
            // 창이 닫혀 있으면 아래 콤보 차례는 예전과 같다.
            bool riposte = ParryCounterReady && ActKind == WeaponActKind.Guard && Weapon.combo.Length >= ShieldRule.RiposteStepNumber;
            if (riposte)
            {
                _comboIndex = ShieldRule.RiposteStepNumber - 1;
                _riposteUntil = -999f;
            }
            _riposteActive = riposte;
            _step = Weapon.combo[_comboIndex];
            _comboStepNumber = _comboIndex + 1;
            _comboIndex = riposte ? 0 : (_comboIndex + 1) % Weapon.combo.Length;
            _comboExpire = float.MaxValue;

            _state = State.Swing;
            _stateTime = 0f;
            _hitsDone = 0;
            _actionId++;
            _swingStopped = false;
            _swingStrongCrit = false;
            _swingCritShown = CritTier.None;
            _tagged.Clear();
            ClearPending();
            // 공격 속도는 동작 길이·판정 순간·이월 상한에만 쓴다(장비 문서 3-3). 다가가기·내딛기 0.1, 콤보 끊김, 입력 버퍼는 그대로.
            // 사슬 철퇴 관성(InertiaRule)은 동작 길이 배율로만 들어간다. 관성 단계가 아니면 배율이 정확히 1이라 예전 Plan과 비트까지 같다.
            float inertiaScale = InertiaRule.Scale(_step.inertia, Time.time - _inertiaAnchor, _inertiaBroken);
            _inertiaActive = inertiaScale != 1f;
            _plan = SwingTiming.Plan(_step, AttackSpeedPermille, inertiaScale);
            _swingDuration = _plan.Duration;
            _lungeTime = 0f;

            float reach = _step.Reach;
            var target = AutoTargeter.Pick(Position, _aimWorld, reach, _input.Move, _lastTarget, Time.time - _lastAttackTime);
            float lunge = 0f;
            if (target)
            {
                Vector2 to = target.Position - Position;
                _swingDir = to.sqrMagnitude > 0.0001f ? to.normalized : _aimDir;
                float gap = to.magnitude - target.Radius - reach;
                // 2차 규칙: 사거리 밖이면 0.1초 동안 최대 1.5유닛 다가가서 휘두른다.
                if (Tuning.SmartTargeting && gap > 0f) lunge = Mathf.Min(AutoTargeter.ExtraSearch, gap + 0.2f);
            }
            else
            {
                _swingDir = _aimDir;
            }
            // 마무리 동작은 앞으로 한 발 내딛는다(찌르기·내려찍기). 플레이어와 적은 서로 밀지 않으므로 대상 몸 앞에서 멈춘다.
            float advance = _step.advance;
            if (target) advance = Mathf.Min(advance, Mathf.Max(0f, (target.Position - Position).magnitude - target.Radius - Radius));
            lunge = Mathf.Max(lunge, advance);
            if (lunge > 0f)
            {
                _lungeVelocity = _swingDir * (lunge / 0.1f);
                _lungeTime = 0.1f;
            }
            _lastTarget = target;
            _lastAttackTime = Time.time;
            _facing = _swingDir;
        }

        /// <summary>콤보 판정 한 번: 칼 그림 → 휘두르기 소리 → 공용 판정 몸통(DealHit).</summary>
        void SwingHit(int index)
        {
            var step = _step;
            SwingVisual.ShowStep(Weapon, _comboStepNumber - 1, step, Position, _swingDir, index);
            var stepArt = StepArtNow();
            PlaySwingSound(stepArt, index);
            DealHit(step, stepArt, index, true);
        }

        /// <summary>
        /// 판정 한 번의 몸통(기획/세-무기-우클릭-소켓-1차.md 5-7): 대상 모으기 → 대상마다 치명·피해·처형·넉백·효과 → 타격음·히트스톱·흔들림·사건.
        /// 콤보(SwingHit)·대검 놓아 베기(ReleaseHit)·쌍검 난사(FlurryHit)가 같이 쓴다. 방향은 _swingDir, 피해 출처는 기본공격(체력 흡수·치명·백어택·회피 반격 첫 타·전설 번개).
        /// artStep = 단계 그림 칸(타격음, 없으면 null), index = 이 동작 안 판정 번호, allowExecution = 마무리가 무너진 적을 처형할 수 있나(난사는 false).
        /// 콤보는 예전 SwingHit과 비트까지 같다: 대상 모으기가 칼 그림·휘두르기 소리 뒤로 왔을 뿐(둘 다 물리·난수를 쓰지 않음) 피해·치명 난수 차례·히트스톱·_swingStopped·넉백·소리는 같다.
        /// 다른 점은 무기 행동에서만: 패링 반격 찌르기(피해 × 1.5·버팀 × 2·마무리, 회피 반격 배율은 곱하지 않음), 무기 행동은 공격 속도 버팀 배율을 받지 않음,
        /// 난사 작은 타는 히트스톱 0(처치가 나면 그 동작에서 한 번만, 마지막 타는 FlurryHit가 다시 엶). 타격 효과는 WeaponActFx.StepHitEffects 한 입구.
        /// </summary>
        void DealHit(ComboStep step, StepArt artStep, int index, bool allowExecution)
        {
            bool last = index == step.hits - 1;
            CollectStepTargets(step, Position, _swingDir);
            bool swinging = _state == State.Swing;
            bool riposte = swinging && _riposteActive;
            bool flurry = _state == State.WeaponAct && _actKind == WeaponActKind.Flurry;
            bool finisher = step.finisher || riposte;
            var stepArt = artStep;

            bool anyHit = false;
            bool anyCrit = false;
            bool heavyKill = false;
            bool finisherOnBroken = false;
            bool anyExecuted = false;
            int kills = 0;
            int targetsHit = 0;
            Enemy firstTarget = null;
            Enemy executedFirst = null;
            // 회피 반격 첫 타(CounterRule): 버팀 × 2, 피해 배율 × 1.2. 창이 닫혀 있으면 둘 다 그대로(예전과 비트까지 같음).
            // 패링 반격 찌르기와 겹치면 패링 반격만 쓴다(피해 × 1.5·버팀 × 2, 곱하지 않음, 0-3의 14). 반격 배율은 튕겨 낸 그 적에게만(0-3의 23),
            // 같은 찌르기에 걸린 다른 적은 보통 ③이다(회피 반격 배율도 없음).
            bool counterNow = DodgeCounterReady && !riposte;
            float counter = riposte ? 1f : CounterMultiplier;
            // 마무리 일격 피해 보너스는 대검 놓아 베기에 주지 않는다(GreatswordCharge.TakesFinisherDamageBonus, 0-3의 22). 마무리(무너짐 처형)는 그대로.
            bool chargeRelease = _state == State.WeaponAct && _actKind == WeaponActKind.Charge;
            bool finisherBonus = finisher && (!chargeRelease || GreatswordCharge.TakesFinisherDamageBonus);
            float percent = finisherBonus ? step.hitPercent * (1f + FinisherDamageBonus) : step.hitPercent;
            double dealPercent = riposte ? percent : CounterRule.DamagePercent(percent, counterNow);
            double ripostePercent = percent * (double)ShieldRule.RiposteDamageScale;
            // 치명이면 이 타의 무게 단계(장비 문서 3-4: 그 타 배율 × 치명 피해). 한 동작의 타는 배율이 같아 단계도 같다.
            var critTier = CritTiers.Of(percent, CritDamage);
            // 타당 버팀 = 단계 버팀 ÷ (1 + 공격 속도)(3-4: 초당 버팀 깎기를 무기마다 고정). 공격 속도 0이면 그대로. 무기 행동은 공격 속도를 받지 않는다(0-3의 7).
            float poise = step.poiseDamage * (swinging ? SwingTiming.PoiseScale(AttackSpeedPermille) : 1f);
            double bossBonus = BossDamageBonus;
            // 기습 처형 결과는 피해를 넣기 전에 대상마다 정한다(첫 대상이 맞아 무리가 0.8초 뒤 깨는 것이 같은 판정의 다른 대상을 '들킴'으로 만들지 않게).
            DecideAmbush();
            for (int i = 0; i < _targets.Count; i++)
            {
                var enemy = _targets[i].enemy;
                var ambush = _ambush[i];
                // 백어택·헤드어택(PositionalHitRule): 등 뒤 ±60°면 피해 +20%·치명 +100‰(단검 등 찌르기 +400‰와는 큰 쪽 하나), 정면 ±45° 치명이면 버팀 × 1.5.
                // 둘 다 아니면 배율을 곱하지 않아 예전과 비트까지 같다. 숫자 자리는 피해를 넣기 전에 적는다(넉백·처형 뒤에도 그 숫자 위에 띄움).
                var side = SideOf(enemy, Position - enemy.Position);
                bool back = side == HitSide.Back;
                Vector2 numberAt = enemy.Position + Vector2.up * enemy.Radius;
                // 치명 굴림은 전용 흐름, 피해 굴림은 예전 흐름(장비 문서 3-4 '치명 난수'). 단검 등 찌르기·백어택도 같은 한 번을 확률만 바꿔 굴린다.
                bool crit = RollHitCrit(enemy, back, step.backstab);
                bool head = PositionalHitRule.IsHeadAttack(side, crit, enemy.Poise != null, enemy.Broken);
                bool riposteHere = riposte && enemy == _riposteTarget;
                double hitPercent = PositionalHitRule.DamagePercent(riposteHere ? ripostePercent : dealPercent, back, Tuning.BackAttackDamagePermille);
                int damage = DamageMath.ToMonster(Attack, hitPercent, crit, CritDamage, DamageMath.Roll(_rng), 0, 0.0, false, bossBonus, enemy.IsBoss);
                // 치명 숫자 크기(가벼움 1.2배, 보통·무거움 1.4배)를 이 한 번에만 알린다.
                if (crit) WorldOverlay.SetNextCritTier(critTier);
                // 창 끊어 찌르기: 마무리처럼 넣어 보통 무게는 준비가 끊기고 무거운 적은 0.15초 움찔한다(같은 적 1.5초에 한 번, Core WeaponTraitRules).
                bool interrupts = finisher || (step.staggers && enemy.TryStaggerInterrupt(WeaponTraitRules.StaggerCooldown));
                float poiseScale = (riposteHere ? ShieldRule.RipostePoiseScale : counter) * PositionalHitRule.PoiseScale(head, Tuning.HeadAttackPoiseScale);
                int applied = enemy.TakeHit(damage, crit, DamageSource.Basic, poise, interrupts, poiseScale, out _, out bool wasBroken);
                if (crit) WorldOverlay.ClearNextCritTier();
                if (applied <= 0) continue;
                anyHit = true;
                targetsHit++;
                if (!firstTarget) firstTarget = enemy;
                StealLife(applied);
                var shown = CritTier.None;
                if (crit)
                {
                    anyCrit = true;
                    shown = DecideCrit(ref _swingCritShown, critTier, finisher);
                }
                bool strong = shown >= CritTier.Normal;
                _swingStrongCrit |= strong;
                // 처형(4-2 [3]): 기습 처형(굴쥐·궁수 즉사, 멧돼지 무너짐, 정예·보스는 TakeHit의 기습 버팀 규칙 그대로) 또는 무너짐 처형(마무리).
                bool executed = false;
                bool ambushKill = false;
                if (ambush == AmbushOutcome.Kill)
                {
                    if (!enemy.Dead) enemy.Execute(DamageSource.Basic);
                    executed = ambushKill = enemy.Dead;
                }
                else if (ambush == AmbushOutcome.Break)
                {
                    if (!enemy.Dead) enemy.BreakNow();
                }
                else if (finisher && wasBroken && allowExecution)
                {
                    executed = TryBrokenExecution(enemy, DamageSource.Basic);
                }
                if (executed)
                {
                    anyExecuted = true;
                    if (!executedFirst) executedFirst = enemy;
                    OnExecuted(enemy, ambushKill);
                }
                // 위치 글자: 기습 처형(즉사·바로 무너짐)·무너짐 처형이 난 타는 띄우지 않는다(처형 연출과 겹치지 않게). 한 동작에 한 적당 한 번.
                ShowPositional(enemy, numberAt, back, head, executed || ambush == AmbushOutcome.Kill || ambush == AmbushOutcome.Break, true);
                if (enemy.Dead)
                {
                    kills++;
                    heavyKill |= enemy.IsV3 && enemy.Weight == EnemyWeight.Heavy;
                }
                bool onBroken = wasBroken && finisher;
                finisherOnBroken |= onBroken;
                Vector2 away = step.shape == ComboShape.Line ? _swingDir : enemy.Position - HitCenter(step, Position, _swingDir);
                if (away.sqrMagnitude < 0.0001f) away = _swingDir;
                // 마지막 타에만 넉백을 주는 단계는 첫 타가 치명이어도 밀지 않고, 마지막 타를 0.9로 올린다.
                // 넉백 최소 0.9는 보통·무거운 치명만(가벼운 치명은 보통 타와 같음, 장비 문서 3-4).
                float knock = step.knockbackOnLastHitOnly && !last ? 0f : step.knockback;
                if (knock > 0f && (strong || (step.knockbackOnLastHitOnly && _swingStrongCrit))) knock = Mathf.Max(knock, 0.9f);
                // 넉백 정보(벽 박기 WallSlam이 읽음, 쇠망치 땅 울리기는 한 칸 위). 큰 낫 끌어당김은 몸 쪽으로, 몸에 겹치지 않게 자른다.
                var info = KnockInfo.Player(DamageSource.Basic, applied, step.wallBreak);
                Vector2 knockDir = away;
                if (step.pull && knock > 0f && !enemy.Dead)
                {
                    // 끌리는 거리 = WeaponTraitRules.PullDistance(넉백, 저항, 몸 사이 거리)(시험이 지키는 Core 식). 저항은 ApplyKnockback이 곱하므로 여기서는 저항 0으로 상한만 정한다.
                    float gap = (enemy.Position - Position).magnitude - enemy.Radius - Radius;
                    float cap = WeaponTraitRules.PullDistance(knock * Tuning.KnockbackScale, 0f, gap);
                    if (cap > 0.001f)
                    {
                        knockDir = -away;
                        info.MaxDistance = cap;
                    }
                    else knock = 0f;
                }
                enemy.ApplyKnockback(knockDir, knock, info);
                // 도끼 쪼개기 출혈(BleedRule): 맞았고 살아 있으면 건다(이미 걸려 있으면 시간만 다시).
                if (step.bleedPercent > 0f && !enemy.Dead) EnemyBleed.Apply(enemy, this, step.bleedPercent);
                // 무너진 적에게 마무리: 파편 12개(3차 초안 3-4). 타격 효과는 무기 행동 연출 입구 하나로(방패 치기 둔탁한 타격 등, 지금은 HitEffects.OnHit 그대로).
                WeaponActFx.StepHitEffects(enemy, away, shown, finisher, onBroken ? Mathf.Max(0, 12 - (strong ? 10 : 8)) : 0, step);
            }

            CombatEvents.RaisePlayerSwing(Position, _swingDir, step, anyHit);
            if (!anyHit) return;
            _counterUntil = -999f;
            if (counterNow) CombatEvents.RaiseCounterLanded(firstTarget);
            if (executedFirst) StartExecutionPose(executedFirst);
            // 이 판정의 치명 연출 단계(동작에서 정한 하나). 이 판정에 치명이 없으면 없음.
            var tierNow = anyCrit ? _swingCritShown : CritTier.None;
            // 단계 전용 타격음(그림 칸) → 무기 합성 타격음(WeaponSfx) 순. 있으면 그 위에 치명·처치 소리를 겹친다.
            var hitClip = stepArt != null && stepArt.impactSound ? stepArt.impactSound : WeaponSfx.HitClip(Weapon.id, step);
            if (hitClip)
            {
                Sfx.Play(SfxKind.Hit, hitClip);
                if (kills > 0) Sfx.Play(SfxKind.Kill);
                else if (anyCrit) Sfx.Play(CritSound(tierNow));
            }
            else if (kills > 0 || anyCrit) Sfx.Play(kills > 0 ? SfxKind.Kill : CritSound(tierNow));
            else Sfx.PlayScaled(SfxKind.Hit, 1f, WeaponSfx.HitPitch(Weapon.id));
            // 히트스톱은 동작 1번에 1회만 준다. 마무리는 더 길다. 무거운 치명·처치만 0.06 이상(가벼움·보통 치명은 보통 타와 같음).
            // 난사 작은 타(히트스톱 0)는 실제로 멈출 때만(처치 0.06) 한 번을 쓴다. 마지막 타는 FlurryHit가 _swingStopped를 다시 연다.
            if (!_swingStopped)
            {
                float stop = tierNow == CritTier.Heavy || kills > 0 ? Mathf.Max(step.hitStop, 0.06f) : step.hitStop;
                if (!flurry || stop > 0f)
                {
                    _swingStopped = true;
                    TimeScaleService.HitStop(stop);
                }
            }
            // 3차: 무너진 적에게 마무리 0.1초, 무거운 적 처치 0.12초(한 마리의 무게를 마지막에 갚아 줌). 더 길 때만 덮어쓴다.
            // 처형이 났으면 처형 0.10(ExecutionFx)이 둘을 대신한다(무너짐 0.08 + 처형 0.10, 예산 0.2 안).
            if (finisherOnBroken && !anyExecuted) TimeScaleService.HitStop(0.1f);
            if (heavyKill && !anyExecuted)
            {
                TimeScaleService.HitStop(0.12f);
                ScreenShake.Add(0.1f, 0.12f);
            }
            if (step.shake > 0f) ScreenShake.Add(step.shake, 0.1f);
            CritShake(tierNow);
            OnKills(kills);
            // 전설 연쇄 번개의 발동 자리(장비 문서 6장): 맞힌 판정마다 한 번. 번개는 동작 번호가 바뀐 첫 사건만 굴린다.
            CombatEvents.RaiseBasicHit(new BasicHitInfo(_actionId, index, finisher, firstTarget, targetsHit, anyCrit, Position, _swingDir));
        }

        /// <summary>
        /// 휘두르기 소리: 단계 전용 소리(그림 칸) → 무기 합성 클립(WeaponSfx) → 무기별 음량·음높이 배율 순.
        /// 기존 3종은 클립이 없어 예전 값(대검 1.12/0.72, 쌍검 0.82/1.3, 장검 1/1) 그대로다.
        /// </summary>
        void PlaySwingSound(StepArt stepArt, int index)
        {
            if (stepArt != null && stepArt.swingSound)
            {
                Sfx.Play(SfxKind.Swing, stepArt.swingSound);
                return;
            }
            var clip = WeaponSfx.SwingClip(Weapon.id, ComboIndex, (_actionId + index) & 1);
            if (clip) Sfx.Play(SfxKind.Swing, clip);
            else Sfx.PlayScaled(SfxKind.Swing, WeaponSfx.SwingVolume(Weapon.id), WeaponSfx.SwingPitch(Weapon.id));
        }

        /// <summary>적의 등 뒤 ±60°(BackstabRule)에서 치는가. 기습 처형과 단검 등 찌르기가 같은 판정을 쓴다.</summary>
        bool BehindOf(Enemy enemy)
        {
            Vector2 facing = enemy.FacingDirection;
            Vector2 toPlayer = Position - enemy.Position;
            return BackstabRule.IsBehind(facing.x, facing.y, toPlayer.x, toPlayer.y);
        }

        /// <summary>
        /// 기습 처형 결과를 _targets 차례대로 _ambush에 담는다(ExecutionRule.Ambush). 웅크린 채 시작한 기본공격이고 Tuning.AmbushExecutionOn일 때만,
        /// 3차 규칙(버팀·무너짐)이 있는 적만 본다. 아니면 모두 None(지금 기습 규칙: TakeHit 버팀 × 3, 상한 70%).
        /// </summary>
        void DecideAmbush()
        {
            _ambush.Clear();
            bool can = _swingFromCrouch && Tuning.AmbushExecutionOn;
            for (int i = 0; i < _targets.Count; i++)
            {
                var enemy = _targets[i].enemy;
                var outcome = AmbushOutcome.None;
                if (can && enemy && enemy.IsV3)
                    outcome = ExecutionRule.Ambush(enemy.Class, !enemy.Aware, enemy.WakePending, true, true, BehindOf(enemy));
                _ambush.Add(outcome);
            }
        }

        /// <summary>
        /// 치명 굴림 한 번(치명 흐름 그대로). 단검 등 찌르기(BackstabRule, 등 뒤 +400‰)나 백어택(PositionalHitRule, + Tuning.BackAttackCritPermille)이면
        /// 치명 확률 = 시트 치명‰(상한 적용된 값) + 둘 중 큰 보너스 하나(1000에서 자름). 보너스가 없으면 RollCrit()와 같다.
        /// 단검 등 찌르기는 백어택을 끄거나 허수아비·둥지를 쳐도 예전처럼 걸린다(등 뒤 판정은 같은 BehindOf).
        /// </summary>
        bool RollHitCrit(Enemy enemy, bool back, bool daggerBackstab)
        {
            bool daggerBehind = daggerBackstab && BehindOf(enemy);
            int backBonus = Tuning.BackAttackCritPermille;
            if (PositionalHitRule.CritBonusPermille(back, daggerBehind, backBonus) <= 0) return RollCrit();
            int permille = PositionalHitRule.CritChancePermille(Mathf.RoundToInt(CritChance * 1000f), back, daggerBehind, backBonus);
            return _critRng.NextDouble() < permille / 1000.0;
        }

        /// <summary>
        /// 백어택·헤드어택 자리(PositionalHitRule.Side): 적이 보는 방향과 적 → 공격이 온 쪽(toAttacker). 둥지·허수아비·둘 다 꺼짐이면 None.
        /// 기본공격·회오리는 내 자리에서, 검풍은 날아온 쪽(진행 방향의 반대)에서 온 것으로 본다. 등 뒤 판정은 기습 처형·단검과 같은 BackstabRule.IsBehind.
        /// </summary>
        HitSide SideOf(Enemy enemy, Vector2 toAttacker)
        {
            if (!enemy || (!Tuning.BackAttackOn && !Tuning.HeadAttackOn)) return HitSide.None;
            Vector2 facing = enemy.FacingDirection;
            return PositionalHitRule.Side(enemy.Class, facing.x, facing.y, toAttacker.x, toAttacker.y, Tuning.BackAttackOn, Tuning.HeadAttackOn);
        }

        /// <summary>
        /// 백어택·헤드어택을 세고 글자를 띄운다(WorldOverlay.Tag, 피해 숫자 바로 위). 처형이 난 타는 글자가 없다.
        /// oncePerAction이면 이 동작(_tagged)에서 이미 글자를 띄운 적에게는 다시 띄우지 않는다(쌍검 연타·회전베기·회오리 여러 타).
        /// </summary>
        void ShowPositional(Enemy enemy, Vector2 numberAt, bool back, bool head, bool executed, bool oncePerAction)
        {
            if (back) BackAttackHits++;
            if (head) HeadAttackHits++;
            var tag = PositionalHitRule.TagOf(back, head, executed);
            if (tag == HitTag.None) return;
            if (oncePerAction && !_tagged.Add(enemy)) return;
            WorldOverlay.Tag(numberAt, tag);
        }

        /// <summary>
        /// 무너짐 처형(4-2 [3], Tuning.ExecutionOn): 맞기 전에 무너져 있던 적에게 마무리 타나 검풍이 맞았고, 그 타로 죽었거나 남은 체력이 문턱
        /// (Tuning.ExecuteThreshold*: 보통 30%·무거움 20%·정예 12%) 이하면 남은 체력을 모두 깎는다. 보스·둥지·허수아비·가벼운 적은 하지 않는다.
        /// 부르는 쪽이 wasBroken과 마무리·검풍을 이미 확인했다. 처형했으면(그 적이 쓰러졌으면) true.
        /// </summary>
        bool TryBrokenExecution(Enemy enemy, DamageSource source)
        {
            if (!Tuning.ExecutionOn || !enemy || !enemy.IsV3 || enemy.Health == null) return false;
            if (!ExecutionRule.ShouldExecute(enemy.Class, true, true, enemy.Dead, enemy.Health.Fraction,
                    Tuning.ExecuteThresholdMedium, Tuning.ExecuteThresholdHeavy, Tuning.ExecuteThresholdElite)) return false;
            if (!enemy.Dead) enemy.Execute(source);
            return enemy.Dead;
        }

        /// <summary>
        /// 처형 한 번의 연출·사건·회복: ExecutionFx(히트스톱 0.10, 흔들림, 고어, 느린 화면은 마주침의 마지막 적·정예만), CombatEvents.EnemyExecuted(무리 공포 2.5초),
        /// Tuning.ExecutionHealOn이면 최대 체력 3% 회복(기본 끔).
        /// </summary>
        void OnExecuted(Enemy enemy, bool ambush)
        {
            Vector2 to = enemy.Position - Position;
            Vector2 dir = to.sqrMagnitude > 0.0001f ? to.normalized : _facing;
            bool slow = ExecutionRule.UsesSlowMotion(enemy.Class, ExecutionFx.IsLastOfEncounter(enemy));
            ExecutionFx.Play(enemy, dir, slow, ambush);
            CombatEvents.RaiseEnemyExecuted(enemy, ambush);
            if (Tuning.ExecutionHealOn && !_health.Dead)
            {
                int healed = _health.Heal(Mathf.Max(1, Mathf.RoundToInt(_health.Max * ExecutionRule.HealFraction)));
                if (healed > 0) WorldOverlay.Number(Position + Vector2.up * Radius, healed, NumberKind.Heal);
            }
        }

        /// <summary>
        /// 근접 처형 자세: 0.06초 동안 적 쪽으로 최대 0.6유닛 당겨 붙고(내딛기 경로, 몸에 겹치지 않게 틈까지만), 이 휘두르기 동안 처형 자세를 켠다.
        /// 몸은 처형한 적 쪽을 본다.
        /// </summary>
        void StartExecutionPose(Enemy enemy)
        {
            if (_state != State.Swing || !enemy) return;
            Vector2 to = enemy.Position - Position;
            float dist = to.magnitude;
            Vector2 dir = dist > 0.0001f ? to / dist : _swingDir;
            float pull = ExecutionRule.PullTo(dist - enemy.Radius - Radius);
            if (pull > 0f)
            {
                _lungeVelocity = dir * (pull / ExecutionRule.PullSeconds);
                _lungeTime = ExecutionRule.PullSeconds;
            }
            _facing = dir;
            _execPoseStart = Time.time;
            _execPoseAction = _actionId;
        }

        /// <summary>
        /// 치명 연출 단계를 한 번 정한다(장비 문서 3-4). shown이 비어 있으면 무거움 0.5초 제한(마무리 예외)을 거쳐 정하고 RaiseCritShown을 한 번 낸다.
        /// 이미 정했으면(같은 동작·회오리 한 타·검풍 한 번 안의 두 번째 치명부터) 그 단계를 그대로 쓴다.
        /// </summary>
        CritTier DecideCrit(ref CritTier shown, CritTier tier, bool finisher)
        {
            if (shown != CritTier.None) return shown;
            shown = CritTiers.Gate(tier, Time.time, ref _lastHeavyCrit, finisher);
            CombatEvents.RaiseCritShown(shown, finisher);
            return shown;
        }

        /// <summary>치명 소리: 가벼움은 짧은 치명 소리, 보통·무거움은 치명 소리(장비 문서 3-4).</summary>
        static SfxKind CritSound(CritTier tier) => tier == CritTier.Light ? SfxKind.CritLight : SfxKind.Crit;

        /// <summary>치명 흔들림: 보통 0.04/0.06, 무거움 0.06/0.08(예전 치명 연출). 가벼움은 보통 타와 같아 없음.</summary>
        static void CritShake(CritTier tier)
        {
            if (tier == CritTier.Heavy) ScreenShake.Add(0.06f, 0.08f);
            else if (tier == CritTier.Normal) ScreenShake.Add(0.04f, 0.06f);
        }

        /// <summary>체력 흡수(장비 문서 2-1): 기본공격·스킬이 실제로 넣은 피해 × ‰. 전설 피해는 여기를 지나지 않는다. 1 미만은 모았다가 넣는다.</summary>
        void StealLife(int applied)
        {
            int permille = LifeStealPermille;
            if (permille <= 0 || applied <= 0 || _health.Dead) return;
            _lifeStealCarry += applied * permille / 1000f;
            if (_lifeStealCarry < 1f) return;
            int heal = Mathf.FloorToInt(_lifeStealCarry);
            _lifeStealCarry -= heal;
            _health.Heal(heal);
        }

        /// <summary>초당 체력 재생(장비 문서 2-1): 게임 시간으로 모아 정수만큼 넣는다. 쓰러진 동안과 가득 찬 동안은 모으지 않는다.</summary>
        void Regenerate(float dt)
        {
            int perSecond = HpRegenPerSecond;
            if (perSecond <= 0 || _state == State.Down || _health.Dead || _health.Current >= _health.Max)
            {
                _regenCarry = 0f;
                return;
            }
            if (dt <= 0f) return;
            _regenCarry += perSecond * dt;
            if (_regenCarry < 1f) return;
            int heal = Mathf.FloorToInt(_regenCarry);
            _regenCarry -= heal;
            _health.Heal(heal);
        }

        StepArt StepArtNow()
        {
            var set = ArtRuntime.Active;
            return set ? set.Weapon(Weapon.id)?.Step(ComboIndex) : null;
        }

        /// <summary>
        /// 플레이어 몫 처치를 센다(전설 효과처럼 PlayerController 밖에서 쓰러뜨린 적). 연속 처치 수는 늘 올리고,
        /// juice면 여러 마리 처치 연출(히트스톱 0.08·느린 화면)도 낸다. 연쇄 폭발은 연쇄 하나에 처음 한 번만 juice = true(장비 문서 6장).
        /// </summary>
        public void AddKills(int kills, bool juice)
        {
            if (kills <= 0) return;
            if (juice) OnKills(kills);
            else CountKills(kills);
        }

        void CountKills(int kills)
        {
            KillStreak = Time.time - KillStreakTime <= 2f ? KillStreak + kills : kills;
            KillStreakTime = Time.time;
            if (KillStreak > BestKillStreak) BestKillStreak = KillStreak;
            // 처치 시 체력 회복(장비 문서 2-1): 처치마다(전설 효과로 쓰러뜨린 적도 플레이어 처치로 센다).
            int heal = OnKillHealAmount;
            if (heal > 0 && !_health.Dead) _health.Heal(heal * kills);
        }

        /// <summary>한 번에 여러 마리를 쓰러뜨렸을 때의 보상 연출과 연속 처치 수.</summary>
        void OnKills(int kills)
        {
            if (kills <= 0) return;
            CountKills(kills);
            if (!Tuning.MultiKillJuice) return;
            if (kills >= 5)
            {
                TimeScaleService.HitStop(0.08f);
                TimeScaleService.SlowMotion(0.18f, 0.35f);
                ScreenShake.Add(0.14f, 0.16f);
            }
            else if (kills >= LegendRules.MultiKillJuiceMin)
            {
                TimeScaleService.HitStop(0.08f);
                ScreenShake.Add(0.1f, 0.12f);
            }
        }

        static Vector2 HitCenter(ComboStep step, Vector2 origin, Vector2 dir) =>
            step.shape == ComboShape.Circle ? origin + dir * step.centerOffset : origin;

        /// <summary>콤보 단계 모양(부채꼴·직선·원) 안의 적을 가까운 순으로 담는다.</summary>
        void CollectStepTargets(ComboStep step, Vector2 origin, Vector2 dir)
        {
            switch (step.shape)
            {
                case ComboShape.Line:
                    CollectLineTargets(origin, dir, step.size, step.width, step.maxTargets);
                    break;
                case ComboShape.Circle:
                    CollectTargets(HitCenter(step, origin, dir), step.size, 360f, dir, step.maxTargets);
                    break;
                default:
                    CollectTargets(origin, step.size, step.arcDeg, dir, step.maxTargets);
                    break;
            }
        }

        void CollectLineTargets(Vector2 origin, Vector2 dir, float length, float width, int maxTargets)
        {
            _targets.Clear();
            _overlap.Clear();
            _seen.Clear();
            Vector2 side = new Vector2(-dir.y, dir.x);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Physics2D.OverlapBox(origin + dir * (length * 0.5f), new Vector2(length + 2.6f, width + 2.6f), angle, _enemyFilter, _overlap);
            foreach (var col in _overlap)
            {
                var enemy = col ? col.GetComponent<Enemy>() : null;
                if (!enemy || enemy.Dead || !_seen.Add(enemy)) continue;
                Vector2 local = enemy.Position - origin;
                float along = Vector2.Dot(local, dir);
                float across = Mathf.Abs(Vector2.Dot(local, side));
                if (along < -enemy.Radius || along > length + enemy.Radius || across > width * 0.5f + enemy.Radius) continue;
                if (Physics2D.Linecast(origin, enemy.Position, Layers.WallMask)) continue;
                _targets.Add((enemy, along));
            }
            _targets.Sort((a, b) => a.dist.CompareTo(b.dist));
            if (_targets.Count > maxTargets) _targets.RemoveRange(maxTargets, _targets.Count - maxTargets);
        }

        /// <summary>부채꼴 안의 적을 가까운 순으로 maxTargets개까지 _targets에 담는다. arcDeg ≥ 360이면 원.</summary>
        void CollectTargets(Vector2 center, float range, float arcDeg, Vector2 dir, int maxTargets)
        {
            _targets.Clear();
            _overlap.Clear();
            _seen.Clear();
            Physics2D.OverlapCircle(center, range + 1.3f, _enemyFilter, _overlap);
            foreach (var col in _overlap)
            {
                var enemy = col ? col.GetComponent<Enemy>() : null;
                if (!enemy || enemy.Dead || !_seen.Add(enemy)) continue;
                Vector2 to = enemy.Position - center;
                float centerDist = to.magnitude;
                float edgeDist = centerDist - enemy.Radius;
                if (edgeDist > range) continue;
                if (arcDeg < 360f && centerDist > enemy.Radius + Radius)
                {
                    float slack = Mathf.Asin(Mathf.Clamp01(enemy.Radius / centerDist)) * Mathf.Rad2Deg;
                    if (Vector2.Angle(dir, to) > arcDeg * 0.5f + slack) continue;
                }
                if (Physics2D.Linecast(center, enemy.Position, Layers.WallMask)) continue;
                _targets.Add((enemy, centerDist));
            }
            _targets.Sort((a, b) => a.dist.CompareTo(b.dist));
            if (_targets.Count > maxTargets) _targets.RemoveRange(maxTargets, _targets.Count - maxTargets);
        }

        void StartDodge()
        {
            _input.ConsumeDodge();
            // 구르면 일어서고 관성이 끊긴다. 구르기 번호는 회피 반격 연출을 한 번 구르기에 한 번만 내는 데 쓴다.
            SetCrouching(false);
            _inertiaBroken = true;
            _dodgeCount++;
            Vector2 move = _input.Move;
            _dodgeDir = move.sqrMagnitude > 0.01f ? move.normalized : _aimDir;
            _state = State.Dodge;
            _stateTime = 0f;
            _dodgeCooldown = DodgeCooldownTime;
            _knockTime = 0f;
            _staggerTime = 0f;
            _lungeTime = 0f;
            _health.ExtraInvulnerable = true;
            _facing = _dodgeDir;
            ClearPending();
            ResetCombo();
            Sfx.Play(SfxKind.Dodge);
        }

        void StartWhirl()
        {
            LastCombatActionTime = Time.time;
            _input.ConsumeSkill1();
            SetCrouching(false);
            _inertiaBroken = true;
            _state = State.Whirl;
            _stateTime = 0f;
            _whirlTicksDone = 0;
            _tagged.Clear();
            _whirlCooldown = WhirlCooldownMax;
            PaySpirit(Demo6.Core.Progression.SpiritRules.WhirlCost);
            _bloodGiven = 0f;
            _lungeTime = 0f;
            _knockTime = 0f;
            _staggerTime = 0f;
            ClearPending();
            ResetCombo();
        }

        void WhirlTick()
        {
            CollectTargets(Position, WhirlRadiusNow, 360f, Vector2.right, 64);
            SwingVisual.ShowRing(Position, WhirlRadiusNow);
            Sfx.Play(SfxKind.Swing);
            bool anyHit = false;
            bool anyCrit = false;
            bool heavyKill = false;
            int kills = 0;
            // 회오리 한 타의 치명 단계(1타 90% × 치명 피해, 장비 문서 3-4). 연출은 이 한 타에 하나.
            var critTier = CritTiers.Of(WhirlPercent, CritDamage);
            var shownTier = CritTier.None;
            double skillBonus = SkillDamageBonus;
            double bossBonus = BossDamageBonus;
            // 회피 반격 첫 타(CounterRule): 이 한 타의 모든 대상이 버팀 × 2, 피해 배율 × 1.2. 창이 닫혀 있으면 그대로. 패링 반격 배율은 회오리에 없다(2-6).
            bool counterNow = DodgeCounterReady;
            double dealPercent = CounterRule.DamagePercent(WhirlPercent, counterNow);
            Enemy firstHit = null;
            foreach (var (enemy, _) in _targets)
            {
                // 백어택·헤드어택(PositionalHitRule): 내 자리에서 본 적의 등 뒤·정면. 글자는 회오리 한 번에 한 적당 한 번.
                var side = SideOf(enemy, Position - enemy.Position);
                bool back = side == HitSide.Back;
                Vector2 numberAt = enemy.Position + Vector2.up * enemy.Radius;
                bool crit = RollHitCrit(enemy, back, false);
                bool head = PositionalHitRule.IsHeadAttack(side, crit, enemy.Poise != null, enemy.Broken);
                double hitPercent = PositionalHitRule.DamagePercent(dealPercent, back, Tuning.BackAttackDamagePermille);
                int damage = DamageMath.ToMonster(Attack, hitPercent, crit, CritDamage, DamageMath.Roll(_rng), 0, skillBonus, true, bossBonus, enemy.IsBoss);
                if (crit) WorldOverlay.SetNextCritTier(critTier);
                float poiseScale = CounterMultiplier * PositionalHitRule.PoiseScale(head, Tuning.HeadAttackPoiseScale);
                int applied = enemy.TakeHit(damage, crit, DamageSource.Whirlwind, WhirlPoise, false, poiseScale, out _, out _);
                if (crit) WorldOverlay.ClearNextCritTier();
                if (applied <= 0) continue;
                if (!firstHit) firstHit = enemy;
                anyHit = true;
                StealLife(applied);
                BloodWhirlHit();
                ShowPositional(enemy, numberAt, back, head, false, true);
                var shown = CritTier.None;
                if (crit)
                {
                    anyCrit = true;
                    shown = DecideCrit(ref shownTier, critTier, false);
                }
                if (enemy.Dead)
                {
                    kills++;
                    heavyKill |= enemy.IsV3 && enemy.Weight == EnemyWeight.Heavy;
                }
                Vector2 away = enemy.Position - Position;
                // 보통·무거운 치명만 넉백 0.9(가벼운 치명은 보통 타와 같음). 플레이어 몫 넉백이라 벽 박기 대상이다.
                // 끌어당기는 회오리(갈림 칸): 밀어내는 대신 안쪽으로 0.6 당긴다(내 몸 안까지 당기지 않게 거리에서 자른다).
                if (PullWhirl)
                {
                    float pull = Mathf.Min(Demo6.Core.Progression.SkillTree.PullDistance, Mathf.Max(0f, away.magnitude - enemy.Radius - Radius - 0.1f));
                    if (pull > 0.01f) enemy.ApplyKnockback(-away, pull, KnockInfo.Player(DamageSource.Whirlwind, applied));
                }
                else enemy.ApplyKnockback(away, shown >= CritTier.Normal ? 0.9f : WhirlKnockback, KnockInfo.Player(DamageSource.Whirlwind, applied));
                HitEffects.OnHit(enemy, away, shown, false);
            }
            CombatEvents.RaisePlayerWhirl(Position, WhirlRadiusNow, anyHit);
            if (!anyHit) return;
            _counterUntil = -999f;
            if (counterNow) CombatEvents.RaiseCounterLanded(firstHit);
            // 기획 3-6: 회오리 1타 0.02, 무거운 치명·마지막 일격 0.06.
            TimeScaleService.HitStop(shownTier == CritTier.Heavy || kills > 0 ? 0.06f : 0.02f);
            if (heavyKill)
            {
                TimeScaleService.HitStop(0.12f);
                ScreenShake.Add(0.1f, 0.12f);
            }
            Sfx.Play(kills > 0 ? SfxKind.Kill : anyCrit ? CritSound(shownTier) : SfxKind.Hit);
            CritShake(shownTier);
            OnKills(kills);
        }

        void StartWave()
        {
            LastCombatActionTime = Time.time;
            _input.ConsumeSkill2();
            SetCrouching(false);
            _inertiaBroken = true;
            _state = State.WaveCast;
            _stateTime = 0f;
            _waveCooldown = WaveCooldownMax;
            PaySpirit(Demo6.Core.Progression.SpiritRules.WaveCost);
            _lungeTime = 0f;
            ClearPending();
            ResetCombo();
            Sfx.Play(SfxKind.Swing);
            // 커서 1.5 안에 적이 있으면 그 적 방향.
            Enemy near = null;
            float best = AutoTargeter.CursorRadius;
            foreach (var e in Enemy.All)
            {
                if (!e || e.Dead || (e.VisionHidden && !e.VisionInSight)) continue;
                float d = (e.Position - _aimWorld).magnitude - e.Radius;
                if (d <= best)
                {
                    best = d;
                    near = e;
                }
            }
            Vector2 to = near ? near.Position - Position : _aimDir;
            _waveDir = to.sqrMagnitude > 0.0001f ? to.normalized : _aimDir;
            _facing = _waveDir;
        }

        void TryDrinkPotion()
        {
            // 물약이 떨어졌으면 챙긴 식은 주먹밥을 먹는다(묶음 5-7, PlayerController.Spirit.cs).
            if (Potions <= 0) { TryEatRice(); return; }
            if (_potionCooldown > 0f || _health.Current >= _health.Max) return;
            int healed = _health.Heal(Mathf.RoundToInt(_health.Max * PotionHeal));
            Potions--;
            _potionCooldown = PotionCooldownTime;
            WorldOverlay.Number(Position + Vector2.up * Radius, healed, NumberKind.Heal);
        }

        void UpdateFacing()
        {
            if (_state == State.Free || _state == State.Whirl) _facing = _aimDir;
            // 무기 행동 중에는 커서 쪽으로 초당 최대 막기 300°·기 모으기 120°·난사 90°만 돈다(놓아 베기·경직·깨짐은 고정).
            else if (_state == State.WeaponAct && WeaponActTurns) _facing = TurnToward(_facing, _aimDir, WeaponActRules.TurnRate(_actKind) * Time.deltaTime);
            if (!_facingMark) return;
            _facingMark.localPosition = _facing * (Radius + 0.12f);
            _facingMark.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_facing.y, _facing.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// 검풍이 맞힌 적에게 피해를 준다. waveShown은 이 검풍 한 번의 치명 연출 단계(SwordWave가 들고 있음):
        /// 비어 있으면 첫 치명에서 정하고(무거움 0.5초 제한) RaiseCritShown을 한 번 낸다. crit은 치명이었는가, 단계는 waveShown으로 읽는다.
        /// </summary>
        public bool HitWithWave(Enemy enemy, Vector2 direction, ref CritTier waveShown, out bool crit, out bool killed, float percentScale = 1f)
        {
            // 백어택·헤드어택(PositionalHitRule): 검풍은 날아온 쪽(진행 방향의 반대)에서 맞힌 것으로 본다. 적마다 한 번 맞히므로 글자도 한 번.
            var side = SideOf(enemy, -direction);
            bool back = side == HitSide.Back;
            Vector2 numberAt = enemy.Position + Vector2.up * enemy.Radius;
            crit = RollHitCrit(enemy, back, false);
            bool head = PositionalHitRule.IsHeadAttack(side, crit, enemy.Poise != null, enemy.Broken);
            killed = false;
            // 세 갈래 검풍은 한 갈래 60%(percentScale).
            float percent = WavePercentNow * percentScale;
            var critTier = CritTiers.Of(percent, CritDamage);
            // 회피 반격 첫 타(CounterRule): 버팀 × 2, 피해 배율 × 1.2. 창은 처음 맞힌 적에서 닫힌다. 패링 반격 배율은 검풍에 없다(2-6).
            bool counterNow = DodgeCounterReady;
            double dealPercent = PositionalHitRule.DamagePercent(CounterRule.DamagePercent(percent, counterNow), back, Tuning.BackAttackDamagePermille);
            int damage = DamageMath.ToMonster(Attack, dealPercent, crit, CritDamage, DamageMath.Roll(_rng), 0, SkillDamageBonus, true, BossDamageBonus, enemy.IsBoss);
            if (crit) WorldOverlay.SetNextCritTier(critTier);
            float poiseScale = CounterMultiplier * PositionalHitRule.PoiseScale(head, Tuning.HeadAttackPoiseScale);
            int applied = enemy.TakeHit(damage, crit, DamageSource.SwordWave, WavePoise, false, poiseScale, out _, out bool wasBroken);
            if (crit) WorldOverlay.ClearNextCritTier();
            if (applied <= 0) return false;
            _counterUntil = -999f;
            if (counterNow) CombatEvents.RaiseCounterLanded(enemy);
            StealLife(applied);
            var shown = crit ? DecideCrit(ref waveShown, critTier, false) : CritTier.None;
            // 무너짐 처형(4-2 [3]): 검풍은 마무리처럼 친다. 멀리서 맞히므로 당겨 붙기·처형 자세는 없다.
            bool executed = wasBroken && TryBrokenExecution(enemy, DamageSource.SwordWave);
            if (executed) OnExecuted(enemy, false);
            ShowPositional(enemy, numberAt, back, head, executed, false);
            killed = enemy.Dead;
            // 무거운 적 처치 0.12초. 처형이면 처형 0.10(ExecutionFx)이 대신한다.
            if (killed && !executed && enemy.IsV3 && enemy.Weight == EnemyWeight.Heavy) TimeScaleService.HitStop(0.12f);
            enemy.ApplyKnockback(direction, SwordWave.Knockback, KnockInfo.Player(DamageSource.SwordWave, applied));
            HitEffects.OnHit(enemy, direction, shown, true);
            if (killed) OnKills(1);
            return true;
        }
    }
}
