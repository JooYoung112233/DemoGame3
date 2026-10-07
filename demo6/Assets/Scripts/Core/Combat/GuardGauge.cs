using System;

namespace Demo6.Core.Combat
{
    /// <summary>
    /// 한손검과 방패의 방어 게이지(기획/세-무기-우클릭-소켓-1차.md 2-5·0-3의 28). 예전 숨은 '방패 버팀'을 캐릭터 밑 막대로 보이게 한 것이다.
    /// 규칙(숫자는 ShieldRule, 시험 손잡이는 최대치·회복 빠르기·패링 환급):
    /// - 막을 때마다 막은 공격의 세기만큼 줄어든다(ShieldRule.GuardCost: 근접 = 배율 ÷ 4, 화살 10, 멧돼지 돌진 30, 오우거 휩쓸기 40 …).
    /// - 막지 않으면 마지막 막기 0.5초 뒤부터 초당 50, 막는 중에는 1.0초 뒤부터 초당 10(아주 느리게) 다시 찬다.
    /// - 0이 되면 막기 깨짐(경직 0.8초). 깨진 뒤에는 게이지가 최대치의 절반까지 다시 차야 들 수 있다(BreakLock 1.5초도 함께, 기본 빠르기에서 두 순간이 같다).
    /// - 패링 성공은 깎지 않고 환급(기본 20)을 돌려준다. 정확히 막는 쪽이 이득이다.
    /// - 보이기(Alpha): 막는 동안·깨진 뒤·가득 차지 않은 동안 보이고, 가득 찬 뒤 ShowLinger초 머문 뒤 FadeTime초에 걸쳐 사라진다.
    /// - 깎인 몫(Trail)은 TrailHold초 남았다가 줄어든다(막을 때마다 얼마나 깎였는지 읽힘).
    /// 시간은 부르는 쪽 시계(now, 초, PlayerController는 Time.time)를 그대로 쓴다. UnityEngine 없음, 할당 없음.
    /// </summary>
    public sealed class GuardGauge
    {
        // ── 시험 손잡이 범위(전투 시험장 F1) ──
        public const float KnobMaxLow = 50f;
        public const float KnobMaxHigh = 200f;
        public const float KnobRegenLow = 0.25f;
        public const float KnobRegenHigh = 3f;
        public const float KnobRefundHigh = 50f;
        /// <summary>최대치 하한(손잡이 밖 값이 와도 막대·나눗셈이 깨지지 않게).</summary>
        public const float MinMax = 10f;

        // ── 보이기 ──
        /// <summary>가득 찬 뒤(그리고 막기를 내린 뒤) 이만큼 더 보인다.</summary>
        public const float ShowLinger = 0.6f;
        /// <summary>그 뒤 이만큼에 걸쳐 사라진다.</summary>
        public const float FadeTime = 0.25f;
        /// <summary>깎인 몫이 머무는 시간과 줄어드는 빠르기(최대치에 대한 몫, 초당).</summary>
        public const float TrailHold = 0.3f;
        public const float TrailDrainFractionPerSecond = 1.2f;
        /// <summary>패링 환급 번쩍임 길이.</summary>
        public const float ParryFlashTime = 0.2f;

        const float Never = -1e6f;

        float _max;
        float _value;
        float _trail;
        float _trailHoldUntil = Never;
        float _lastDrain = Never;
        float _lastNeeded = Never;
        float _lastParry = Never;

        public GuardGauge(float max = ShieldRule.GuardMax)
        {
            _max = Math.Max(MinMax, max);
            _value = _max;
            _trail = _max;
        }

        /// <summary>지금 최대치.</summary>
        public float Max => _max;
        /// <summary>지금 게이지(0~Max).</summary>
        public float Value => _value;
        /// <summary>게이지 몫(0~1).</summary>
        public float Fraction => _value / _max;
        /// <summary>깎인 몫까지 더한 몫(0~1, Fraction 이상). 막대에서 Fraction~TrailFraction 사이를 옅게 그린다.</summary>
        public float TrailFraction => Math.Max(_value, _trail) / _max;
        /// <summary>깨진 뒤 아직 다시 들기 문턱(RaiseThreshold) 전인가. 이 동안은 들 수 없다.</summary>
        public bool Recovering { get; private set; }
        /// <summary>깨진 뒤 다시 들 수 있는 게이지(최대치 × ShieldRule.RaiseAfterBreakFraction).</summary>
        public float RaiseThreshold => _max * ShieldRule.RaiseAfterBreakFraction;
        /// <summary>게이지 쪽에서 막기를 들 수 있나(깨진 뒤 문턱 전이면 아님). 시간 잠금(BreakLock·HurtLock)은 부르는 쪽이 따로 본다.</summary>
        public bool CanRaise => !Recovering;
        /// <summary>마지막으로 막거나 튕긴 시각(회복 쉬는 시간의 기준).</summary>
        public float LastDrainTime => _lastDrain;

        /// <summary>
        /// 최대치를 바꾼다(시험 손잡이). 지금 값이 새 최대치보다 크면 내리고, 작으면 그대로 두어 회복으로 찬다. MinMax 아래는 MinMax.
        /// </summary>
        public void SetMax(float max)
        {
            max = Math.Max(MinMax, max);
            if (max == _max) return;
            _max = max;
            if (_value > _max) _value = _max;
            if (_trail > _max) _trail = _max;
        }

        /// <summary>가득 채운다(다시 섬·채움). 깨진 뒤 잠금·깎인 몫·번쩍임도 지운다.</summary>
        public void Fill()
        {
            _value = _max;
            _trail = _max;
            _trailHoldUntil = Never;
            _lastDrain = Never;
            _lastParry = Never;
            Recovering = false;
        }

        /// <summary>
        /// 막기 가르기 결과(ShieldRule.Resolve)를 넣는다. 막음·튕김만 바꾼다(못 막은 타는 그대로).
        /// 막음 = 게이지를 MeterAfter로(깎인 몫은 잠깐 남음), 튕김 = 환급 + 번쩍임, Broke면 Break. 둘 다 회복 쉬는 시간을 다시 센다.
        /// </summary>
        public void Apply(in GuardOutcome g, float now)
        {
            if (!g.Stopped) return;
            float before = _value;
            _value = Clamp(g.MeterAfter, 0f, _max);
            _lastDrain = now;
            _lastNeeded = now;
            if (g.Result == HitResult.Parried) _lastParry = now;
            if (_value < before)
            {
                _trail = Math.Max(_trail, before);
                _trailHoldUntil = now + TrailHold;
            }
            if (g.Broke) Break(now);
        }

        /// <summary>막기 깨짐: 게이지 0, 깨진 뒤 문턱(RaiseThreshold)까지 들 수 없음, 회복 쉬는 시간을 다시 센다.</summary>
        public void Break(float now)
        {
            if (_value > 0f)
            {
                _trail = Math.Max(_trail, _value);
                _trailHoldUntil = now + TrailHold;
            }
            _value = 0f;
            _lastDrain = now;
            _lastNeeded = now;
            Recovering = true;
        }

        /// <summary>
        /// 한 프레임: 회복(ShieldRule.Regen, 회복 빠르기 rateScale), 깨진 뒤 문턱을 넘으면 다시 들 수 있음, 깎인 몫 줄이기, 보이기 시각.
        /// guarding = 막기 판정이 켜져 있나(PlayerController.GuardActive).
        /// </summary>
        public void Tick(float now, float dt, bool guarding, float rateScale = 1f)
        {
            _value = ShieldRule.Regen(_value, now - _lastDrain, guarding, dt, _max, rateScale);
            if (Recovering && _value >= RaiseThreshold) Recovering = false;
            if (_trail <= _value) _trail = _value;
            else if (now >= _trailHoldUntil && dt > 0f)
                _trail = Math.Max(_value, _trail - TrailDrainFractionPerSecond * _max * dt);
            if (guarding || Recovering || _value < _max) _lastNeeded = now;
        }

        /// <summary>
        /// 막대 보이기 세기(0~1): 막는 동안·깨진 뒤·가득 차지 않은 동안(마지막 Tick·Apply 시각 기준) 1, 그 뒤 ShowLinger초 1, FadeTime초에 걸쳐 0.
        /// 한 번도 필요한 적이 없으면 0.
        /// </summary>
        public float Alpha(float now)
        {
            float since = now - _lastNeeded;
            if (since <= ShowLinger) return 1f;
            return Clamp(1f - (since - ShowLinger) / FadeTime, 0f, 1f);
        }

        /// <summary>패링 환급 번쩍임 세기(0~1): 튕긴 순간 1에서 ParryFlashTime초에 0.</summary>
        public float ParryFlash(float now)
        {
            float since = now - _lastParry;
            if (since < 0f) return 0f;
            return Clamp(1f - since / ParryFlashTime, 0f, 1f);
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
