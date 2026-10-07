using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 오른쪽 클릭 무기 행동(기획/세-무기-우클릭-소켓-1차.md 0-3·2-5~2-7·3-6~3-8·4-6·5장). 한손검과 방패 = 막기·패링, 대검 = 기 모으기 → 놓아 베기, 쌍검 = 난사.
    /// 상태는 State.WeaponAct 하나이고 세부 단계는 ActPhase다(끊김 경직 Flinch·막기 깨짐 Break 포함). 행동 수명(시작·Tick·끊김·조용히 끝)과
    /// 막기 가르기(ReceiveHit 안), 기 모으기·놓아 베기, 난사가 여기 있다. PlayerController.cs에는 훅만 있다(Update·FixedUpdate·UpdateFacing·ReceiveHit·StartSwing·DealHit).
    /// 규칙:
    /// - 들어가기(5-2): 차례 구르기 > 회오리(E) > 검풍(Q) > 무기 행동 > 공격. 막기만 휘두르기 첫 판정 뒤에 누르면 끊고 바로 들고(0-3의 19),
    ///   그 밖(판정 전에 누른 막기, 언제 누른 기 모으기·난사)은 그 동작이 끝날 때.
    ///   누르는 행동(막기·기 모으기)은 시작 순간 버튼이 눌려 있어야 하고, 끊김·깨짐·행동 없음으로 쓴 누름은 다시 쓰지 않는다(새로 눌러야 함).
    /// - 쓰는 동안(5-3): 구르기·왼쪽 공격·E·Q·G는 무시하고 쌓아 두지 않는다(끝나기 0.15초 안에 누른 것만 버퍼대로 끝난 뒤 나감). 물약은 된다, C는 끝난 뒤 바꾼다.
    /// - 맞으면 끊김(5-5·5-6): 앞 반원에서 막은 타는 아니다. 버팀 룬은 대검 기 모으기·놓아 베기만(시험 손잡이로 셋 다).
    /// - 연출은 WeaponActFx로만, 적 반응은 Enemy.Parried로만 낸다.
    /// 다른 꾸러미는 아래 읽기 값·사건·SetWeaponRunes만 쓴다(그림 WeaponStanceLook·ShieldPart, 가방, HUD).
    /// </summary>
    public sealed partial class PlayerController
    {
        static readonly string[] NoRunes = System.Array.Empty<string>();
        /// <summary>낀 무기(장착 자리)의 룬 id(Inventory가 장착이 바뀔 때마다 넣음). 효과는 이 목록만 센다(6-5).</summary>
        readonly List<string> _weaponRunes = new List<string>(2);

        // ── 행동 상태(State.WeaponAct 동안만 뜻이 있음. 행동 시간은 _stateTime) ──

        /// <summary>지금 행동 종류(행동 중이 아니면 None). 무기는 행동 중 바꿀 수 없어 ActKind와 같다.</summary>
        WeaponActKind _actKind;
        WeaponActPhase _actPhase;
        /// <summary>지금 단계 시작부터 지난 시간(게임 시간, Update에서 더함).</summary>
        float _actPhaseTime;
        /// <summary>행동이 살아 있나(시작 ~ 끝 사건 전). 끊김 경직·막기 깨짐은 벌 단계라 false다(WeaponActEnded는 이미 냄).</summary>
        bool _actLive;
        /// <summary>이번 행동에서 나간 판정 수(난사 0~8, 놓아 베기 0~1).</summary>
        int _actHitsDone;
        /// <summary>끊김 경직·막기 깨짐이 끝나는 시각(맞은 순간부터 센다).</summary>
        float _flinchUntil = -999f;
        /// <summary>이미 쓴 오른쪽 클릭 누름 번호(PlayerInputReader.WeaponActPresses). 같으면 누르고 있어도 누르는 행동을 시작하지 않는다.</summary>
        int _actSpentPress;
        /// <summary>휘두르기 첫 판정 전에 누른 오른쪽 클릭(그 동작이 끝날 때 시작).</summary>
        bool _pendingWeaponAct;
        /// <summary>방패로 막은 피해를 넣는 중(OnDamaged가 피격 자세·깜빡임·흔들림·피격 무적·피격 숫자·소리를 건너뜀).</summary>
        bool _blockingHit;

        // ── 막기·패링(2-5·2-6) ──
        /// <summary>방어 게이지(예전 숨은 방패 버팀, 0-3의 28). 줄어듦·회복·깨짐·패링 환급·깨진 뒤 다시 들기 문턱·막대 보이기를 든다.</summary>
        readonly GuardGauge _gauge = new GuardGauge(ShieldRule.GuardMax);
        /// <summary>이번 들기 시작 시각(패링 창)과 직전 들기 시작 시각(연타 막기 잠금 ParryRearm).</summary>
        float _guardRaiseStart = -999f;
        float _lastRaiseStart = -999f;
        /// <summary>이번 들기에 패링 창이 있나. 패링에 성공하면 다음 들기는 잠금 없이 창이 열린다(_parryRearmFree).</summary>
        bool _parryArmed;
        bool _parryRearmFree;
        /// <summary>마지막 패링 때의 누름 번호. 그 누름을 계속 누르고 있으면 밀쳐 내기 뒤 막기를 이어 가고, 같은 누름으로 다시 드는 들기에는 패링 창이 없다(0-3의 20).</summary>
        int _parryPress = -1;
        /// <summary>밀쳐 내기 동안 왼쪽 클릭을 눌렀나(밀쳐 내기가 끝날 때 반격 창이 열려 있으면 반격 베기, ShieldRule.AfterParryPush).</summary>
        bool _riposteQueued;
        /// <summary>다시 들기 잠금(깨짐 1.5초, 막지 못한 타로 끊김 0.4초).</summary>
        float _guardRelockUntil = -999f;
        bool _guardRecoilBoss;
        /// <summary>패링 반격 창 끝 시각과, 지금 휘두르기가 반격 베기인가. 반격 배율은 튕겨 낸 그 적(_riposteTarget)에게만 준다(0-3의 23).</summary>
        float _riposteUntil = -999f;
        bool _riposteActive;
        Enemy _riposteTarget;
        /// <summary>
        /// 패링 피해를 넣을 적(튕긴 다음 프레임 TickWeaponActTimers 처음에 넣음). ParryHit은 적 두뇌의 공격 판정 안에서 불려 그 자리에서 죽이면
        /// 두뇌가 죽은 적의 상태를 이어서 바꾸므로 한 프레임 미룬다.
        /// </summary>
        Enemy _parryDamageTarget;

        // ── 기 모으기·놓아 베기(3-6~3-8) ──
        float _chargeHeld;
        int _chargeLevel;
        bool _chargeReleaseQueued;
        int _chargeReleaseLevel = 1;
        int _releaseLevel;
        ComboStep _releaseStep;

        // ── 난사(4-6) ──
        float _flurryCooldown;
        float _flurryCooldownMax = TwinFlurry.Cooldown;

        // ── 읽기 값(그림·HUD·가방) ──

        /// <summary>지금 무기의 오른쪽 클릭 행동 종류(행동 중이 아니어도, 무기 id로 정함). 나머지 6종은 None.</summary>
        public WeaponActKind ActKind => WeaponActRules.KindOf(Weapon != null ? Weapon.id : null);
        /// <summary>무기 행동 중인가(State.WeaponAct, 끊김 경직·막기 깨짐 포함). 이 동안 무기 바꾸기·룬 끼우기가 막힌다(0-3의 16).</summary>
        public bool InWeaponAct => _state == State.WeaponAct;
        /// <summary>행동 세부 단계(행동 중이 아니면 None).</summary>
        public WeaponActPhase ActPhase => InWeaponAct ? _actPhase : WeaponActPhase.None;
        /// <summary>행동 시작부터 지난 시간(한 행동 안에서 줄지 않음, 끊김 경직·깨짐까지 이어짐, 그림 PoseTime과 같음).</summary>
        public float ActTime => InWeaponAct ? _stateTime : 0f;
        /// <summary>지금 단계 시작부터 지난 시간.</summary>
        public float ActPhaseTime => InWeaponAct ? _actPhaseTime : 0f;

        bool InCharge => InWeaponAct && _actKind == WeaponActKind.Charge && (_actPhase == WeaponActPhase.Charging || _actPhase == WeaponActPhase.Release);

        /// <summary>대검 기 모으기 단계(0~3, 모으는 중에만). 놓아 베기 중에는 놓은 단계.</summary>
        public int ChargeLevel => !InCharge ? 0 : _actPhase == WeaponActPhase.Release ? _releaseLevel : _chargeLevel;
        /// <summary>대검 모은 시간(초, 모으는 중에만 오름, 놓아 베기 중에는 놓은 값 그대로). 그림 떨림·빛 줄에 쓴다.</summary>
        public float ChargeHeldTime => InCharge ? _chargeHeld : 0f;
        /// <summary>대검 놓아 베기 단계(1~3). 놓아 베기 중이 아니면 0.</summary>
        public int ReleaseLevel => InWeaponAct && _actPhase == WeaponActPhase.Release ? _releaseLevel : 0;

        /// <summary>막기 판정이 켜져 있나(들기 순간부터, 내리기 앞 0.06초까지, 반동·밀쳐 내기 포함).</summary>
        public bool GuardActive
        {
            get
            {
                if (_state != State.WeaponAct || _actKind != WeaponActKind.Guard || !_actLive) return false;
                switch (_actPhase)
                {
                    case WeaponActPhase.Raise:
                    case WeaponActPhase.Hold:
                    case WeaponActPhase.Recoil:
                    case WeaponActPhase.ParryPush:
                        return true;
                    case WeaponActPhase.Lower:
                        return _actPhaseTime < ShieldRule.LowerBlockTime;
                    default:
                        return false;
                }
            }
        }

        /// <summary>패링 창이 지금 열려 있나(들기 시작부터 Tuning.ParryWindow 안, 연타 막기 잠금이 없을 때).</summary>
        public bool ParryWindowOpen => GuardActive && _parryArmed && Time.time - _guardRaiseStart <= Tuning.ParryWindow;
        /// <summary>지금 반동이 보스 타였나(반동 그림 길이 0.25초).</summary>
        public bool GuardRecoilBoss => InWeaponAct && _actPhase == WeaponActPhase.Recoil && _guardRecoilBoss;
        /// <summary>방어 게이지(0~GuardMeterMax). 방패 무기가 아니면 가득.</summary>
        public float GuardMeter => ActKind == WeaponActKind.Guard ? _gauge.Value : _gauge.Max;
        /// <summary>방어 게이지 최대치(기본 100, 시험 손잡이 Tuning.GuardMeterMax).</summary>
        public float GuardMeterMax => _gauge.Max;
        /// <summary>방어 게이지 몫(0~1). ShieldPart가 낮으면(0.35 아래) 색 × 0.8·떨림을 주고, 게이지 막대가 붉어진다.</summary>
        public float GuardMeterFraction => GuardMeter / _gauge.Max;
        /// <summary>깎인 몫까지 더한 게이지 몫(0~1, 막대의 옅은 꼬리). 방패 무기가 아니면 1.</summary>
        public float GuardMeterTrailFraction => ActKind == WeaponActKind.Guard ? _gauge.TrailFraction : 1f;
        /// <summary>막기가 깨진 뒤 게이지가 다시 들기 문턱(최대치의 절반)까지 차기 전인가. 이 동안은 막기를 들 수 없다.</summary>
        public bool GuardRecovering => ActKind == WeaponActKind.Guard && _gauge.Recovering;
        /// <summary>
        /// 방어 게이지 막대 보이기 세기(0~1, WorldOverlay가 캐릭터 밑에 그림). 막는 동안·깨진 뒤·차는 동안 1, 가득 찬 뒤 잠깐 머물다 사라진다.
        /// 한손검과 방패가 아니거나 막기 손잡이가 꺼졌거나 쓰러졌으면 0.
        /// </summary>
        public float GuardGaugeAlpha => ActKind == WeaponActKind.Guard && Tuning.GuardEnabled && _state != State.Down ? _gauge.Alpha(Time.time) : 0f;
        /// <summary>패링 환급 번쩍임(0~1, 튕긴 순간 1에서 0.2초에 0).</summary>
        public float GuardParryFlash => ActKind == WeaponActKind.Guard ? _gauge.ParryFlash(Time.time) : 0f;
        /// <summary>패링 반격 창이 열려 있나(1.0초, 다음 왼쪽 클릭이 '반격 베기').</summary>
        public bool ParryCounterReady => Time.time <= _riposteUntil;
        /// <summary>지금 휘두르기가 패링 반격 베기인가(그림이 막기 자세에서 ③ 키로 그림).</summary>
        public bool RiposteActive => _state == State.Swing && _riposteActive;

        /// <summary>난사 남은 재사용(초)과 최대(6초 또는 끊긴 뒤 3초). 다른 행동은 0.</summary>
        public float ActCooldown => ActKind == WeaponActKind.Flurry ? _flurryCooldown : 0f;
        public float ActCooldownMax => _flurryCooldownMax;
        /// <summary>이번 행동에서 나간 판정 수(난사 0~8, 놓아 베기 0~1).</summary>
        public int ActHitsDone => InWeaponAct ? _actHitsDone : 0;

        /// <summary>지금 맞아도 끊기지 않는가(버팀 룬 또는 시험 손잡이, WeaponActRules.Armored).</summary>
        public bool SuperArmorNow => InWeaponAct && _actLive && WeaponActRules.Armored(_actKind, _actPhase, HasSuperArmorRune, Tuning.SuperArmorAllActs);
        /// <summary>끊김 경직 중인가(맞은 순간부터 0.35초, 대검 0.45초). 이 동안 공격·무기 행동·회오리·검풍·구르기를 시작하지 못한다.</summary>
        public bool Flinching => InWeaponAct && _actPhase == WeaponActPhase.Flinch;
        /// <summary>끊김 경직 남은 시간(초).</summary>
        public float FlinchRemaining => Flinching ? Mathf.Max(0f, _flinchUntil - Time.time) : 0f;

        /// <summary>
        /// 무기 행동이 상호작용(F)을 막는가(PlayerInputReader가 읽음, 5-3): 막기 중에는 F가 되고, 기 모으기·놓아 베기·난사·끊김 경직·막기 깨짐 중에는 막는다.
        /// </summary>
        public bool InteractBlockedByAct =>
            _state == State.WeaponAct && (_actKind != WeaponActKind.Guard || _actPhase == WeaponActPhase.Flinch || _actPhase == WeaponActPhase.Break);

        /// <summary>마지막 ReceiveHit 결과(맞음·막음·튕김·피함). 화살은 막음·튕김이면 멈춘다.</summary>
        public HitResult LastHitResult { get; private set; } = HitResult.None;

        /// <summary>낀 무기의 룬 id(읽기 전용, 없으면 빈 목록).</summary>
        public IReadOnlyList<string> WeaponRunes => _weaponRunes;

        /// <summary>
        /// 낀 무기의 룬을 넣는다(부르는 곳: Inventory.RecomputeAfterGearChange — 장착 무기의 GearItem.Runes, 전투 시험장은 시험 손잡이로 대신).
        /// null이면 비운다. 목록을 복사해 둔다(장착이 바뀔 때만 불려 할당은 그때뿐).
        /// </summary>
        public void SetWeaponRunes(IReadOnlyList<string> runes)
        {
            _weaponRunes.Clear();
            if (runes == null) return;
            for (int i = 0; i < runes.Count; i++)
                if (!string.IsNullOrEmpty(runes[i])) _weaponRunes.Add(runes[i]);
        }

        /// <summary>낀 무기에 그 룬이 있는가.</summary>
        public bool HasRune(string runeId)
        {
            if (string.IsNullOrEmpty(runeId)) return false;
            for (int i = 0; i < _weaponRunes.Count; i++)
                if (_weaponRunes[i] == runeId) return true;
            return false;
        }

        /// <summary>버팀 룬(슈퍼아머)이 낀 무기에 있거나 시험 손잡이(Tuning.TestSuperArmorRune)가 켜졌나.</summary>
        public bool HasSuperArmorRune => Tuning.TestSuperArmorRune || RuneTable.Has(_weaponRunes, RuneEffect.SuperArmor);

        // ── 사건(HUD 알림·계측) ──

        /// <summary>무기 행동을 시작했다(종류).</summary>
        public event System.Action<WeaponActKind> WeaponActStarted;
        /// <summary>
        /// 무기 행동이 끝났다(종류, 맞아서 끊겼나). 맞아서 끊김·막기 깨짐은 맞은 순간 true로 낸다(그 뒤 경직·깨짐 단계는 사건 없이 끝남).
        /// 정상 끝(막기 내림·놓아 베기·난사 끝·패링 밀쳐 내기 끝)과 조용히 끝(쓰러짐·순간 이동·다시 섬·채움)은 false.
        /// </summary>
        public event System.Action<WeaponActKind, bool> WeaponActEnded;
        /// <summary>행동이 없는 무기(나머지 6종)나 막기가 꺼진 방패로 오른쪽 클릭했다. 듣는 곳: DungeonHud(처음 두 번만 한 줄 알림, 꾸러미 세기 rmb_none).</summary>
        public event System.Action NoWeaponActPressed;

        void RaiseWeaponActStarted(WeaponActKind kind) => WeaponActStarted?.Invoke(kind);
        void RaiseWeaponActEnded(WeaponActKind kind, bool interrupted) => WeaponActEnded?.Invoke(kind, interrupted);
        void RaiseNoWeaponActPressed() => NoWeaponActPressed?.Invoke();

        // ── 회피 반격과 패링 반격 나누기(2-6, 0-3의 14) ──

        /// <summary>회피 반격 창(구르기로 예고 공격을 피함)만. 배율(피해 × 1.2·버팀 × 2)은 이 창만 본다.</summary>
        bool DodgeCounterReady => Tuning.DodgeCounter && Time.time <= _counterUntil;

        // ── 입력 ──

        /// <summary>오른쪽 버튼을 아직 쓰지 않은 누름으로 누르고 있나(누르는 행동 시작 조건).</summary>
        bool WeaponActHeldFresh => _input.WeaponActHeld && _input.WeaponActPresses != _actSpentPress;

        /// <summary>지금 누름을 쓴 것으로 적는다(누르고 있어도 다시 시작하지 않음, 새로 눌러야 함).</summary>
        void SpendWeaponActPress() => _actSpentPress = _input.WeaponActPresses;

        /// <summary>끊김 경직 동안: 구르기·스킬·무기 행동 입력을 버린다(5-6). 오른쪽 누름도 쓴 것으로 적는다.</summary>
        void DiscardActionInputs()
        {
            _input.ConsumeDodge();
            _input.ConsumeSkill1();
            _input.ConsumeSkill2();
            _input.ConsumeWeaponAct();
            SpendWeaponActPress();
        }

        /// <summary>
        /// 휘두르기 중 누른 오른쪽 클릭(HoldPendingInputs): 그 동작이 끝날 때 시작하도록 들고 있는다. 막기만 첫 판정 뒤에 누른 것을 버퍼 그대로 두어
        /// Update가 휘두르기를 끊고 바로 든다(WeaponActRules.CutsSwing, 0-3의 19). 기 모으기·난사는 판정 뒤에 눌러도 끝날 때(끊고 1단계 놓기 되풀이 막음).
        /// </summary>
        void HoldPendingWeaponAct()
        {
            if (!_input.WeaponActBuffered) return;
            if (_hitsDone >= 1 && WeaponActRules.CutsSwing(ActKind)) return;
            _pendingWeaponAct = true;
            _input.ConsumeWeaponAct();
        }

        /// <summary>
        /// 무기 행동을 시작할 수 있으면 시작한다(5-2). pending = 휘두르기 첫 판정 전에 눌러 둔 오른쪽 클릭.
        /// 누르는 행동(막기·기 모으기)은 쓰지 않은 누름으로 누르고 있어야 하고, 난사는 버퍼(0.15초) 안이거나 들고 있던 누름이어야 한다.
        /// 행동이 없는 무기거나 막기가 꺼진 방패면 NoWeaponActPressed만 올리고 누름을 쓴다. 막기 다시 들기 잠금·난사 재사용 중이면 시작하지 않는다.
        /// </summary>
        bool TryStartWeaponAct(bool pending)
        {
            var kind = ActKind;
            if (kind == WeaponActKind.None || (kind == WeaponActKind.Guard && !Tuning.GuardEnabled))
            {
                if (pending || _input.WeaponActBuffered)
                {
                    _input.ConsumeWeaponAct();
                    _pendingWeaponAct = false;
                    SpendWeaponActPress();
                    RaiseNoWeaponActPressed();
                }
                return false;
            }
            switch (kind)
            {
                case WeaponActKind.Guard:
                    // 깨진 뒤에는 시간 잠금(1.5초)과 게이지 문턱(최대치의 절반)을 둘 다 넘어야 든다(0-3의 28).
                    if (!WeaponActHeldFresh || Time.time < _guardRelockUntil || !_gauge.CanRaise) return false;
                    break;
                case WeaponActKind.Charge:
                    if (!WeaponActHeldFresh) return false;
                    break;
                case WeaponActKind.Flurry:
                    if (!(pending || _input.WeaponActBuffered) || _flurryCooldown > 0f) return false;
                    break;
            }
            StartWeaponAct(kind);
            return true;
        }

        // ── 수명 ──

        void StartWeaponAct(WeaponActKind kind)
        {
            _input.ConsumeWeaponAct();
            LastCombatActionTime = Time.time;
            // 시작하면 일어선다(기습 처형은 기본공격만), 콤보 1단계, 관성 끊김.
            _swingFromCrouch = false;
            SetCrouching(false);
            _inertiaBroken = true;
            ResetCombo();
            ClearPending();
            _state = State.WeaponAct;
            _stateTime = 0f;
            _actionId++;
            _actKind = kind;
            _actLive = true;
            _actHitsDone = 0;
            _flinchUntil = -999f;
            _lungeTime = 0f;
            _riposteActive = false;
            // 판정 몸통(DealHit)이 쓰는 동작 몫을 이 행동 것으로 비운다(치명 연출 단계·위치 글자·히트스톱 한 번).
            _swingStopped = false;
            _swingStrongCrit = false;
            _swingCritShown = CritTier.None;
            _tagged.Clear();
            switch (kind)
            {
                case WeaponActKind.Guard: StartGuard(); break;
                case WeaponActKind.Charge: StartCharge(); break;
                case WeaponActKind.Flurry: StartFlurry(); break;
            }
            RaiseWeaponActStarted(kind);
        }

        void SetActPhase(WeaponActPhase phase)
        {
            _actPhase = phase;
            _actPhaseTime = 0f;
        }

        /// <summary>Update의 State.WeaponAct 한 프레임.</summary>
        void TickWeaponAct(float dt)
        {
            _stateTime += dt;
            _actPhaseTime += dt;
            switch (_actPhase)
            {
                case WeaponActPhase.Flinch:
                    // 경직 마지막 입력 버퍼(0.15초) 안에 누른 구르기·E·Q는 남겨 끝나면 나간다(대검 0.45초 경직도 빠질 틈, 0-3의 21). 오른쪽 클릭은 늘 버린다.
                    if (WeaponActCommon.KeepsBufferedInputs(_flinchUntil - Time.time))
                    {
                        _input.ConsumeWeaponAct();
                        SpendWeaponActPress();
                    }
                    else DiscardActionInputs();
                    if (Time.time >= _flinchUntil) EnterFree();
                    return;
                case WeaponActPhase.Break:
                    if (Time.time >= _flinchUntil) EnterFree();
                    return;
            }
            switch (_actKind)
            {
                case WeaponActKind.Guard: TickGuard(); break;
                case WeaponActKind.Charge: TickCharge(); break;
                case WeaponActKind.Flurry: TickFlurry(); break;
                default: FinishWeaponAct(false); break;
            }
        }

        /// <summary>
        /// 정상 끝(5-4) → 자유 상태. 끝나기 0.15초 안에 누른 오른쪽 클릭만 살리고(버퍼), 그 전에 누른 것은 쓴 것으로 적는다(쌓아 두지 않음).
        /// keepPress = 패링 밀쳐 내기 끝(반격 베기로 나가거나 버튼을 뗌). 누름을 쓰지 않아 반격 베기 뒤에도 누르고 있으면 다시 든다(창 없음, StartGuard).
        /// </summary>
        void FinishWeaponAct(bool keepPress)
        {
            var kind = _actKind;
            bool live = _actLive;
            if (!keepPress && !_input.WeaponActBuffered) SpendWeaponActPress();
            EnterFree();
            if (live) RaiseWeaponActEnded(kind, false);
        }

        /// <summary>EnterFree가 부른다: 행동 몫을 비운다(재사용·방어 게이지·잠금·반격 창은 행동 밖 값이라 그대로).</summary>
        void ClearWeaponActFields()
        {
            _actKind = WeaponActKind.None;
            _actPhase = WeaponActPhase.None;
            _actPhaseTime = 0f;
            _actLive = false;
            _actHitsDone = 0;
            _flinchUntil = -999f;
            _chargeHeld = 0f;
            _chargeLevel = 0;
            _chargeReleaseQueued = false;
            _chargeReleaseLevel = 1;
            _releaseLevel = 0;
            _releaseStep = null;
            _guardRecoilBoss = false;
            _riposteQueued = false;
        }

        /// <summary>
        /// 조용히 끝(5-3: 쓰러짐·순간 이동·다시 섬·채움). 기·남은 타가 사라지고 자유 상태가 된다(웅크림 기억도 지움). 살아 있던 행동이면 끊김 false로 끝 사건.
        /// 행동 중이 아니면 아무것도 하지 않고 false.
        /// </summary>
        bool EndWeaponActQuietly()
        {
            if (_state != State.WeaponAct) return false;
            var kind = _actKind;
            bool live = _actLive;
            ClearWeaponActFields();
            _state = State.Free;
            _stateTime = 0f;
            _lungeTime = 0f;
            _crouchQueued = false;
            ClearPending();
            if (live) RaiseWeaponActEnded(kind, false);
            return true;
        }

        /// <summary>다시 섬·채움: 난사 재사용 0, 방어 게이지 가득(깨진 뒤 문턱도 풀림), 다시 들기 잠금·반격 창 풀림.</summary>
        void ResetWeaponActCooldowns()
        {
            _flurryCooldown = 0f;
            _flurryCooldownMax = TwinFlurry.Cooldown;
            _gauge.SetMax(Tuning.GuardMeterMax);
            _gauge.Fill();
            _guardRelockUntil = -999f;
            _riposteUntil = -999f;
            _riposteTarget = null;
        }

        /// <summary>행동 밖에서도 도는 시간: 미룬 패링 피해, 난사 재사용, 방어 게이지 회복(GuardGauge.Tick, 최대치·회복 빠르기 손잡이).</summary>
        void TickWeaponActTimers(float dt)
        {
            FlushParryDamage();
            _flurryCooldown = Mathf.Max(0f, _flurryCooldown - dt);
            _gauge.SetMax(Tuning.GuardMeterMax);
            _gauge.Tick(Time.time, dt, GuardActive, Tuning.GuardRegenScale);
        }

        /// <summary>
        /// 맞아서 끊김(5-6): 행동이 즉시 끝나고 Flinch 단계(경직 FlinchFor(kind), 맞은 순간부터). 막기는 0.4초 다시 들기 잠금, 난사는 재사용 3초.
        /// 오른쪽 버튼은 새로 눌러야 다시 시작한다.
        /// </summary>
        void InterruptWeaponAct()
        {
            var kind = _actKind;
            if (kind == WeaponActKind.Guard) _guardRelockUntil = Mathf.Max(_guardRelockUntil, Time.time + ShieldRule.HurtLock);
            else if (kind == WeaponActKind.Flurry)
            {
                _flurryCooldown = TwinFlurry.InterruptedCooldown;
                _flurryCooldownMax = TwinFlurry.InterruptedCooldown;
            }
            SpendWeaponActPress();
            _flinchUntil = Time.time + WeaponActCommon.FlinchFor(kind);
            _lungeTime = 0f;
            _chargeLevel = 0;
            _chargeReleaseQueued = false;
            _releaseLevel = 0;
            _releaseStep = null;
            _actLive = false;
            SetActPhase(WeaponActPhase.Flinch);
            WeaponActFx.Interrupted(Position, kind);
            RaiseWeaponActEnded(kind, true);
        }

        /// <summary>행동 중 걸음 배율(FixedUpdate). 끊김 경직·깨짐 0.</summary>
        float WeaponActMoveScale => WeaponActRules.MoveScale(_actKind, _actPhase, _actPhase == WeaponActPhase.Release ? _releaseLevel : _chargeLevel);

        /// <summary>행동 중 몸이 커서 쪽으로 도는가(UpdateFacing). 놓아 베기는 휘두르기처럼 방향을 고정하고, 경직·깨짐은 돌지 않는다.</summary>
        bool WeaponActTurns
        {
            get
            {
                switch (_actPhase)
                {
                    case WeaponActPhase.Raise:
                    case WeaponActPhase.Hold:
                    case WeaponActPhase.Lower:
                    case WeaponActPhase.Recoil:
                    case WeaponActPhase.ParryPush:
                    case WeaponActPhase.Charging:
                    case WeaponActPhase.Flurry:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>from에서 to 쪽으로 최대 maxDeg만큼 돈 단위 방향.</summary>
        static Vector2 TurnToward(Vector2 from, Vector2 to, float maxDeg)
        {
            if (to.sqrMagnitude < 0.000001f) return from;
            if (from.sqrMagnitude < 0.000001f) return to.normalized;
            float a = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
            float b = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float n = Mathf.MoveTowardsAngle(a, b, Mathf.Max(0f, maxDeg)) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(n), Mathf.Sin(n));
        }

        // ── 막기·패링(2-5·2-6) ──

        void StartGuard()
        {
            // 연타 막기: 직전 들기 시작에서 0.6초가 안 지났으면 이번 들기에는 패링 창이 없다(패링 성공이면 바로 풀림).
            // 패링한 그 누름을 계속 누르고 있어 다시 드는 것(반격 베기 뒤)은 창이 없고, 연타 잠금 시각·성공 풀림도 그대로 둔다(새 누름이 바로 창을 받게, 0-3의 20).
            bool samePress = _input.WeaponActPresses == _parryPress;
            _parryArmed = ShieldRule.ParryArmedOnRaise(samePress, Time.time - _lastRaiseStart, _parryRearmFree);
            if (!samePress)
            {
                _parryRearmFree = false;
                _lastRaiseStart = Time.time;
            }
            _guardRaiseStart = Time.time;
            _guardRecoilBoss = false;
            SetActPhase(WeaponActPhase.Raise);
        }

        void TickGuard()
        {
            switch (_actPhase)
            {
                case WeaponActPhase.Raise:
                    if (_actPhaseTime >= ShieldRule.RaiseTime) SetActPhase(WeaponActPhase.Hold);
                    break;
                case WeaponActPhase.Hold:
                    // 톡 눌러도 0.25초는 든다. 떼면 내리기 0.12초(못 끊음).
                    if (!_input.WeaponActHeld && _stateTime >= ShieldRule.MinHold) SetActPhase(WeaponActPhase.Lower);
                    break;
                case WeaponActPhase.Recoil:
                    if (_actPhaseTime >= (_guardRecoilBoss ? ShieldRule.BossRecoilTime : ShieldRule.RecoilTime)) SetActPhase(WeaponActPhase.Hold);
                    break;
                case WeaponActPhase.Lower:
                    if (_actPhaseTime >= ShieldRule.LowerTime) FinishWeaponAct(false);
                    break;
                case WeaponActPhase.ParryPush:
                    // 밀쳐 내기 동안 누른 왼쪽 클릭은 담아 둔다(공격은 누르고 있기 방식이라 짧게 누른 것도 여기서 잡음).
                    if (_input.AttackHeld) _riposteQueued = true;
                    if (_actPhaseTime >= ShieldRule.ParryPushTime) EndParryPush();
                    break;
                default:
                    FinishWeaponAct(false);
                    break;
            }
        }

        /// <summary>
        /// 밀쳐 내기 끝(2-6, 0-3의 20, ShieldRule.AfterParryPush): 반격 창 안에서 그동안 왼쪽 클릭을 눌렀으면 자유 상태로 빠져 바로 반격 베기,
        /// 아니고 패링한 그 누름을 아직 누르고 있으면 막기를 이어 감(막기가 꺼지는 틈 없음, 새 패링 창 없음 — 새로 누르면 잠금 없이 창), 뗐으면 자유.
        /// </summary>
        void EndParryPush()
        {
            bool samePressHeld = _input.WeaponActHeld && _input.WeaponActPresses == _parryPress;
            var next = ShieldRule.AfterParryPush(samePressHeld, _riposteQueued, ParryCounterReady);
            _riposteQueued = false;
            if (next == ParryPushEnd.Hold)
            {
                _parryArmed = false;
                SetActPhase(WeaponActPhase.Hold);
                return;
            }
            FinishWeaponAct(true);
            if (next == ParryPushEnd.Riposte && _state == State.Free) StartSwing();
        }

        /// <summary>방패로 이 타를 가른다(막기가 꺼져 있으면 Hit, 배율 1). ToSource = 진행 방향이 있으면 −진행 방향, 없으면 때린 자리 − 내 위치.</summary>
        GuardOutcome ResolveGuard(HitKind kind, float patternPercent, Vector2 from, Vector2 travel)
        {
            if (!GuardActive) return new GuardOutcome(HitResult.Hit, 1f, 1f, _gauge.Value, false);
            Vector2 toSource = from - Position;
            float distance = toSource.magnitude;
            bool hasTravel = travel.sqrMagnitude > 0.000001f;
            if (hasTravel) toSource = -travel;
            var q = new GuardQuery
            {
                Kind = kind,
                Guarding = true,
                ParryOpen = ParryWindowOpen,
                FacingX = _facing.x,
                FacingY = _facing.y,
                ToSourceX = toSource.x,
                ToSourceY = toSource.y,
                HasTravel = hasTravel,
                SourceDistance = distance,
                PatternPercent = patternPercent,
                Meter = _gauge.Value,
                GeneralDamageScale = Tuning.GuardDamageScale,
                MeterMax = _gauge.Max,
                Refund = Tuning.GuardParryRefund,
            };
            return ShieldRule.Resolve(q);
        }

        static bool IsBossKind(HitKind kind) => kind == HitKind.BossSweep || kind == HitKind.BossSlam || kind == HitKind.BossRush;

        /// <summary>
        /// 패링(튕김, 2-6): 피해·밀림 0, 방어 게이지 + 환급(기본 20), 막기 무적 0.12초(같은 타 겹침), 적 반응(source.Parried), 밀쳐 내기 0.15초.
        /// 반격 창 1.0초는 튕긴 적이 틈을 보일 때만 연다(source.ParryOpensRiposte: 화살·오우거 휩쓸기 1타는 아님, 0-3의 23). 배율은 그 적에게만 준다.
        /// 히트스톱·흔들림·불꽃·소리·머리 위 글은 WeaponActFx.Parried가 낸다.
        /// </summary>
        void ParryHit(HitKind kind, Enemy source, in GuardOutcome g)
        {
            _gauge.Apply(g, Time.time);
            LastCombatActionTime = Time.time;
            _health.GrantInvulnerability(ShieldRule.BlockInvulnerable);
            _parryRearmFree = true;
            _parryPress = _input.WeaponActPresses;
            _riposteQueued = false;
            SetActPhase(WeaponActPhase.ParryPush);
            // 틈은 적 반응을 넣기 전 상태로 묻는다(휘청·끊김이 그 상태를 바꾸기 전).
            bool opens = source && source.ParryOpensRiposte(kind);
            if (source) source.Parried(kind);
            // 패링 보상(2026-10-05): 그로기(버팀) 게이지는 source.Parried가 깎고, 피해(공격력 30%)는 다음 프레임에 넣는다.
            if (source && Tuning.ParryDamagePercent > 0f) _parryDamageTarget = source;
            if (opens)
            {
                _riposteUntil = Time.time + ShieldRule.RiposteWindow;
                _riposteTarget = source;
                // 반격 베기가 이 적을 먼저 노리게(커서 위 적이 먼저인 자동 조준 규칙은 그대로).
                _lastTarget = source;
                _lastAttackTime = Time.time;
            }
            WeaponActFx.Parried(Position, _facing);
            LastHitResult = HitResult.Parried;
            SpiritOnParry();
        }

        /// <summary>
        /// 미룬 패링 피해(ShieldRule.ParryDamage): 공격력 × Tuning.ParryDamagePercent%(기본 30), 치명 없이 피해 굴림만, 보스 피해 보너스는 받음.
        /// 숫자가 뜨고(Enemy.TakeHit) 버팀은 깎지 않는다(그로기 깎기는 Enemy.Parried 몫). 그 사이 적이 죽었으면 넣지 않는다.
        /// 멧돼지 돌진 패링은 곧바로 무너지므로 이 피해도 무너짐 배율을 받는다.
        /// </summary>
        void FlushParryDamage()
        {
            var e = _parryDamageTarget;
            if (ReferenceEquals(e, null)) return;
            _parryDamageTarget = null;
            if (!e || e.Dead) return;
            int damage = ShieldRule.ParryDamage(Attack, Tuning.ParryDamagePercent, DamageMath.Roll(_rng), 0, BossDamageBonus, e.IsBoss);
            if (damage <= 0) return;
            LastParryDamage = e.TakeHit(damage, false, DamageSource.Basic, 0f, false, 1f, out _, out _);
            LastParryDamageTime = Time.time;
        }

        /// <summary>시험·확인용: 마지막 패링 피해(들어간 값)와 그 시각.</summary>
        public int LastParryDamage { get; private set; }
        public float LastParryDamageTime { get; private set; } = -999f;

        /// <summary>
        /// 방패로 막음(2-5): 피해·밀림에 표 배율, 막기 무적 0.12초, 방어 게이지 깎기(0이거나 내려찍기면 깨짐), 반동 그림. 시험 무적이면 피해만 0.
        /// 피격 자세·깜빡임·흔들림·피격 무적 0.5초·피격 숫자는 없다(_blockingHit로 OnDamaged가 건너뜀). 반환 true(막음).
        /// </summary>
        bool BlockHit(HitKind kind, Enemy source, int damage, float knockback, Vector2 from, in GuardOutcome g)
        {
            bool boss = IsBossKind(kind) || (source && source.IsBoss);
            _gauge.Apply(g, Time.time);
            LastCombatActionTime = Time.time;
            LastHitResult = HitResult.Blocked;
            int scaled = g.DamageScale <= 0f || damage <= 0 ? 0 : Mathf.Max(1, Mathf.RoundToInt(damage * g.DamageScale));
            int applied = 0;
            if (!Tuning.Invincible && scaled > 0)
            {
                _blockingHit = true;
                try
                {
                    applied = _health.ApplyDamage(scaled, false);
                }
                finally
                {
                    _blockingHit = false;
                }
                // 막은 피해로 쓰러졌으면 OnDied가 행동을 조용히 끝냈다.
                if (_state == State.Down) return true;
            }
            _health.GrantInvulnerability(ShieldRule.BlockInvulnerable);
            float knock = knockback * g.KnockScale;
            if (knock > 0f)
            {
                Vector2 dir = Position - from;
                if (dir.sqrMagnitude < 0.0001f) dir = -_facing;
                _knockVelocity = dir.normalized * (knock * Tuning.KnockbackScale / 0.1f);
                _knockTime = 0.1f;
                _staggerTime = 0f;
            }
            WeaponActFx.Blocked(Position, _facing, applied, boss);
            if (g.Broke) BreakGuard();
            else if (_actPhase != WeaponActPhase.Lower)
            {
                SetActPhase(WeaponActPhase.Recoil);
                _guardRecoilBoss = boss;
            }
            return true;
        }

        /// <summary>
        /// 막기 깨짐(2-5): 0.8초 걸음·행동 멈춤, 게이지 0에서 다시 참, 1.5초가 지나고 게이지가 최대치의 절반까지 차야 다시 들 수 있음, 새로 눌러야 함.
        /// </summary>
        void BreakGuard()
        {
            _gauge.Break(Time.time);
            _guardRelockUntil = Mathf.Max(_guardRelockUntil, Time.time + ShieldRule.BreakLock);
            _flinchUntil = Time.time + ShieldRule.BreakStagger;
            SpendWeaponActPress();
            _actLive = false;
            SetActPhase(WeaponActPhase.Break);
            WeaponActFx.GuardBroken(Position, _facing);
            RaiseWeaponActEnded(WeaponActKind.Guard, true);
        }

        // ── 기 모으기·놓아 베기(3-6~3-8) ──

        void StartCharge()
        {
            _chargeHeld = 0f;
            _chargeLevel = 0;
            _chargeReleaseQueued = false;
            _chargeReleaseLevel = 1;
            _releaseLevel = 0;
            _releaseStep = null;
            SetActPhase(WeaponActPhase.Charging);
        }

        void TickCharge()
        {
            switch (_actPhase)
            {
                case WeaponActPhase.Charging:
                {
                    // 단계는 절대 시간(공격 속도 무관). 0.40초 전에 떼면 0.40초까지 마저 모은 뒤 1단계(취소 불가), 2.5초면 저절로 3단계.
                    _chargeHeld = _actPhaseTime;
                    int level = GreatswordCharge.LevelAt(_chargeHeld);
                    if (level > _chargeLevel)
                    {
                        _chargeLevel = level;
                        WeaponActFx.ChargeLevel(Position, level);
                    }
                    if (!_chargeReleaseQueued && !_input.WeaponActHeld)
                    {
                        _chargeReleaseQueued = true;
                        _chargeReleaseLevel = GreatswordCharge.ReleaseLevel(_chargeHeld);
                    }
                    if (GreatswordCharge.AutoRelease(_chargeHeld)) StartRelease(3);
                    else if (_chargeReleaseQueued && _chargeHeld >= GreatswordCharge.L1) StartRelease(_chargeReleaseLevel);
                    break;
                }
                case WeaponActPhase.Release:
                    if (_releaseStep == null)
                    {
                        FinishWeaponAct(false);
                        break;
                    }
                    if (_actHitsDone == 0 && _actPhaseTime >= _releaseStep.duration * _releaseStep.hitMoment) ReleaseHit();
                    if (_state == State.WeaponAct && _actPhase == WeaponActPhase.Release && _actPhaseTime >= _releaseStep.duration) FinishWeaponAct(false);
                    break;
                default:
                    FinishWeaponAct(false);
                    break;
            }
        }

        /// <summary>
        /// 놓아 베기 시작(3-7): 그 단계 ComboStep(공격 속도 무관). 방향은 놓는 순간 바라보는 쪽, 사거리 안이고 ±30° 안인 대상만 조준한다(1.5 다가가기 없음, 단계 내딛기만).
        /// </summary>
        void StartRelease(int level)
        {
            _releaseLevel = Mathf.Clamp(level, 1, 3);
            _releaseStep = GreatswordCharge.Release(_releaseLevel);
            _actHitsDone = 0;
            _swingStopped = false;
            SetActPhase(WeaponActPhase.Release);
            LastCombatActionTime = Time.time;
            var step = _releaseStep;
            var target = AutoTargeter.PickInCone(Position, _aimWorld, step.Reach, _facing, GreatswordCharge.AimCone);
            Vector2 dir = _facing;
            float advance = step.advance;
            if (target)
            {
                Vector2 to = target.Position - Position;
                float dist = to.magnitude;
                if (dist > 0.0001f) dir = to / dist;
                advance = Mathf.Min(advance, Mathf.Max(0f, dist - target.Radius - Radius));
            }
            _swingDir = dir;
            _facing = dir;
            if (advance > 0f)
            {
                _lungeVelocity = dir * (advance / 0.1f);
                _lungeTime = 0.1f;
            }
            _lastTarget = target;
            _lastAttackTime = Time.time;
        }

        /// <summary>놓아 베기 판정 한 번: 칼 그림·소리(WeaponActFx.ReleaseSwing) → 공용 판정(DealHit, 2·3단계는 마무리라 무너짐 처형이 된다).</summary>
        void ReleaseHit()
        {
            _actHitsDone = 1;
            WeaponActFx.ReleaseSwing(Weapon, _releaseLevel, _releaseStep, Position, _swingDir);
            DealHit(_releaseStep, null, 0, true);
        }

        // ── 난사(4-6) ──

        /// <summary>난사 시작: 재사용 6초, 자동 조준(사거리 1.9). 대상이 1.9 밖이면 첫 0.1초 동안 최대 1.0 다가간다(몸 앞까지).</summary>
        void StartFlurry()
        {
            _flurryCooldown = TwinFlurry.Cooldown;
            _flurryCooldownMax = TwinFlurry.Cooldown;
            SetActPhase(WeaponActPhase.Flurry);
            var target = AutoTargeter.Pick(Position, _aimWorld, TwinFlurry.AimRange, _input.Move, _lastTarget, Time.time - _lastAttackTime);
            Vector2 dir = _aimDir;
            if (target)
            {
                Vector2 to = target.Position - Position;
                float dist = to.magnitude;
                if (dist > 0.0001f) dir = to / dist;
                float gap = dist - target.Radius - TwinFlurry.AimRange;
                if (gap > 0f)
                {
                    float approach = Mathf.Min(TwinFlurry.MaxApproach, Mathf.Min(gap + 0.2f, Mathf.Max(0f, dist - target.Radius - Radius)));
                    if (approach > 0f)
                    {
                        _lungeVelocity = dir * (approach / TwinFlurry.ApproachTime);
                        _lungeTime = TwinFlurry.ApproachTime;
                    }
                }
            }
            _facing = dir;
            _swingDir = dir;
            _lastTarget = target;
            _lastAttackTime = Time.time;
        }

        void TickFlurry()
        {
            int due = TwinFlurry.HitsDue(_actPhaseTime);
            while (_actHitsDone < due && _state == State.WeaponAct && _actPhase == WeaponActPhase.Flurry)
                FlurryHit(_actHitsDone++);
            if (_state == State.WeaponAct && _actPhase == WeaponActPhase.Flurry && _actPhaseTime >= TwinFlurry.Duration) FinishWeaponAct(false);
        }

        /// <summary>
        /// 난사 한 타: 방향은 그 순간 몸 방향. 작은 타는 히트스톱 0(처치가 나면 그 동작에서 한 번만 0.06, DealHit), 마지막 타는 히트스톱을 다시 열어 0.06·끊기(staggers).
        /// 무너짐 처형은 없다. 칼 그림·소리는 WeaponActFx.FlurrySwing.
        /// </summary>
        void FlurryHit(int index)
        {
            var step = TwinFlurry.StepOf(index);
            _swingDir = _facing;
            if (TwinFlurry.IsFinal(index)) _swingStopped = false;
            WeaponActFx.FlurrySwing(Weapon, index, step, Position, _swingDir);
            DealHit(step, null, index, false);
        }

        // ── 맞음(ReceiveHit ⑤) ──

        /// <summary>
        /// 막지 못한 타가 피해 단계까지 왔을 때 무기 행동 몫(5-5): 끊기면 InterruptWeaponAct, 버팀이면 WeaponActFx.SuperArmorHeld.
        /// interrupts·armored는 피해를 넣기 전에 정했다(ReceiveHit). 행동 중이 아니거나 쓰러졌으면 아무것도 하지 않는다.
        /// </summary>
        void AfterHitWeaponAct(bool interrupts, bool armored)
        {
            if (_state != State.WeaponAct || !_actLive) return;
            if (interrupts) InterruptWeaponAct();
            else if (armored) WeaponActFx.SuperArmorHeld(Position);
        }
    }
}
