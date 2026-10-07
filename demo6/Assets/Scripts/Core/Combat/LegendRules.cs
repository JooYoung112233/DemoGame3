using System;
using System.Collections.Generic;

namespace Demo6.Core.Combat
{
    /// <summary>
    /// 전설 고유 효과 3종의 수치와 발동 규칙(장비 문서 6장, 12장 단계 4, 수치는 2차 6-5). 엔진을 모르는 순수 함수라 EditMode에서 시험한다.
    /// 세기 굴림‰(0~1000, StatSheet.LegendaryRollPermille)은 수치 범위 안 위치다. 겹침(같은 효과는 높은 하나)은 StatSheet가 이미 적용했다.
    /// Game/Combat/Legend의 효과 컴포넌트가 이 값과 함수만 쓴다(수치를 따로 적지 않음).
    /// </summary>
    public static class LegendRules
    {
        // ── 연쇄 번개 (기본공격 적중, 동작마다 한 번 굴림) ──

        /// <summary>발동 확률 아래·위 끝(‰). 세기 0이면 30%, 1000이면 40%.</summary>
        public const int LightningChanceMinPermille = 300;
        public const int LightningChanceMaxPermille = 400;
        /// <summary>발동 뒤 다시 발동까지(초).</summary>
        public const double LightningCooldown = 0.2;
        /// <summary>최대 튕김 수(맞은 적에서 다른 적으로 건너가는 횟수).</summary>
        public const int LightningMaxBounces = 4;
        /// <summary>튕김 거리(유닛, 앞 적 중심에서 다음 적 몸 가장자리까지).</summary>
        public const double LightningRange = 4.0;
        /// <summary>튕길 때마다 공격력 배율(%) 아래·위 끝. 튕겨도 줄지 않는다.</summary>
        public const double LightningPercentMin = 110;
        public const double LightningPercentMax = 130;
        /// <summary>치명 가능(플레이어 치명 확률·피해를 그대로 씀). 대검 번개는 드물게 크게 터진다.</summary>
        public const bool LightningCanCrit = true;

        // ── 불꽃 발자국 (전투 중 걸은 거리) ──

        /// <summary>이만큼 걸을 때마다 불길 하나(유닛). 걷기 5.3이면 약 0.23초마다.</summary>
        public const double FlameStepDistance = 1.2;
        /// <summary>전투 중 = 마지막 공격·스킬·피격(PlayerController.LastCombatActionTime)에서 이 시간 안.</summary>
        public const double CombatWindow = 3.0;
        /// <summary>한 프레임에 이보다 멀리 옮겨졌으면 걸음이 아니라 순간 이동이다(누적을 비우고 깔지 않음).</summary>
        public const double FlameTeleportGuard = 2.0;
        public const double FlameRadius = 0.8;
        public const double FlameLifetime = 2.5;
        /// <summary>적 하나는 이 간격에 한 번만 탄다(불길이 겹쳐도).</summary>
        public const double FlameTickInterval = 0.5;
        /// <summary>한 번 탈 때 공격력 배율(%) 아래·위 끝.</summary>
        public const double FlamePercentMin = 50;
        public const double FlamePercentMax = 60;
        /// <summary>불 위 적 이동 배율(−30%).</summary>
        public const double FlameSlowFactor = 0.7;
        /// <summary>불에서 나온 뒤 느려짐이 남는 시간(초). 매 프레임 다시 걸어 '불 위에 있는 동안'이 된다.</summary>
        public const double FlameSlowLinger = 0.15;
        /// <summary>문서에 치명이 없다(지속 피해).</summary>
        public const bool FlameCanCrit = false;

        // ── 연쇄 폭발 (보스가 아닌 적 처치) ──

        public const double BlastRadius = 2.5;
        /// <summary>폭발 공격력 배율(%) 아래·위 끝.</summary>
        public const double BlastPercentMin = 150;
        public const double BlastPercentMax = 180;
        /// <summary>연쇄 하나의 최대 폭발 수(첫 폭발 포함).</summary>
        public const int BlastMaxChain = 12;
        /// <summary>연쇄 안 폭발 사이 간격(초).</summary>
        public const double BlastInterval = 0.1;
        public const bool BlastCanCrit = false;
        /// <summary>
        /// 여러 마리 처치 연출(히트스톱 0.08, 5마리부터 느린 화면)이 실제로 나는 한 번의 처치 수. PlayerController.OnKills와 같은 문턱이다.
        /// 연쇄 폭발은 이 수 이상을 잡은 첫 폭발에서만 '연쇄 하나에 한 번' 기회를 쓴다(1~2마리 폭발은 연출이 없어 기회를 남김).
        /// </summary>
        public const int MultiKillJuiceMin = 3;

        // ── 세기 보간 ──

        /// <summary>세기 굴림‰(0~1000, 밖은 자름)으로 아래·위 끝 사이를 보간한다.</summary>
        public static double Lerp(double min, double max, int rollPermille)
        {
            int r = Math.Max(0, Math.Min(1000, rollPermille));
            return min + (max - min) * r / 1000.0;
        }

        /// <summary>연쇄 번개 발동 확률(0~1). 30~40%.</summary>
        public static double LightningChance(int rollPermille) =>
            Lerp(LightningChanceMinPermille, LightningChanceMaxPermille, rollPermille) / 1000.0;

        /// <summary>연쇄 번개 튕김 한 번의 공격력 배율(%). 110~130.</summary>
        public static double LightningPercent(int rollPermille) => Lerp(LightningPercentMin, LightningPercentMax, rollPermille);

        /// <summary>불길이 한 번 태우는 공격력 배율(%). 50~60.</summary>
        public static double FlamePercent(int rollPermille) => Lerp(FlamePercentMin, FlamePercentMax, rollPermille);

        /// <summary>폭발 공격력 배율(%). 150~180.</summary>
        public static double BlastPercent(int rollPermille) => Lerp(BlastPercentMin, BlastPercentMax, rollPermille);

        // ── 판단 함수 ──

        /// <summary>전투 중인가: 마지막 공격·스킬·피격에서 3초 안.</summary>
        public static bool InCombat(double now, double lastCombatActionTime) => now - lastCombatActionTime <= CombatWindow;

        /// <summary>적 하나가 지금 다시 탈 수 있는가(마지막으로 탄 뒤 0.5초). 처음이면 lastBurn에 음의 무한대를 넘긴다.</summary>
        public static bool CanBurn(double now, double lastBurn) => now - lastBurn >= FlameTickInterval - 1e-6;

        /// <summary>연쇄 폭발을 일으키는 처치인가: 보스가 아닌 적(허수아비는 처치 사건이 없다).</summary>
        public static bool TriggersBlast(bool isBoss) => !isBoss;

        /// <summary>
        /// 느려짐 겹치기(Enemy.ApplySlow): 지금 느려짐이 살아 있으면 배율은 더 센(작은) 쪽, 끝 시각은 더 긴 쪽. 끝났으면 새것을 그대로.
        /// 배율은 0~1로 자른다.
        /// </summary>
        public static void MergeSlow(double factor, double until, double newFactor, double newUntil, double now,
            out double mergedFactor, out double mergedUntil)
        {
            newFactor = Math.Max(0, Math.Min(1, newFactor));
            if (now >= until)
            {
                mergedFactor = newFactor;
                mergedUntil = newUntil;
                return;
            }
            mergedFactor = Math.Min(factor, newFactor);
            mergedUntil = Math.Max(until, newUntil);
        }

        /// <summary>
        /// 연쇄 번개가 건너갈 적들(차례대로). start에서 시작해, 앞 적 중심에서 몸 가장자리까지 range 안의 가장 가까운 적으로 건너간다.
        /// 같은 적은 다시 맞지 않는다(start 포함). eligible이 false인 칸(쓰러진 적 등)과 canLink(앞, 다음)가 false인 짝(벽 너머)은 건너뛴다.
        /// 거리가 같으면 번호가 작은 적.
        /// </summary>
        public static List<int> PickChain(IReadOnlyList<ChainPoint> points, int start, int maxBounces, double range,
            Func<int, int, bool> canLink = null)
        {
            var result = new List<int>(Math.Max(0, maxBounces));
            if (points == null || start < 0 || start >= points.Count || maxBounces <= 0) return result;
            var visited = new bool[points.Count];
            visited[start] = true;
            int current = start;
            for (int b = 0; b < maxBounces; b++)
            {
                int best = -1;
                double bestDist = double.MaxValue;
                var from = points[current];
                for (int i = 0; i < points.Count; i++)
                {
                    if (visited[i] || !points[i].Eligible) continue;
                    double dx = points[i].X - from.X;
                    double dy = points[i].Y - from.Y;
                    double edge = Math.Sqrt(dx * dx + dy * dy) - points[i].Radius;
                    if (edge > range || edge >= bestDist) continue;
                    if (canLink != null && !canLink(current, i)) continue;
                    best = i;
                    bestDist = edge;
                }
                if (best < 0) break;
                result.Add(best);
                visited[best] = true;
                current = best;
            }
            return result;
        }
    }

    /// <summary>연쇄 번개 후보 하나(위치·몸 반지름·고를 수 있나).</summary>
    public readonly struct ChainPoint
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Radius;
        public readonly bool Eligible;

        public ChainPoint(double x, double y, double radius = 0, bool eligible = true)
        {
            X = x;
            Y = y;
            Radius = radius;
            Eligible = eligible;
        }
    }

    /// <summary>
    /// 연쇄 번개 굴림 문(장비 문서 6장 '동작마다 한 번'): 기본공격 동작 번호(ActionId)가 바뀐 첫 적중 사건에서만 굴린다.
    /// 그 순간 다시 발동 대기(0.2초) 중이면 그 동작은 굴리지 않고 넘어간다. 타마다 굴리면 쌍검이 장검의 약 2.5배 터지기 때문이다.
    /// </summary>
    public sealed class LightningGate
    {
        bool _any;
        int _lastAction;
        double _readyAt = double.NegativeInfinity;

        /// <summary>마지막으로 발동한 뒤 다시 발동할 수 있는 시각.</summary>
        public double ReadyAt => _readyAt;

        /// <summary>이 사건이 그 동작의 굴림 순간인가(동작 번호가 바뀐 첫 사건이고 다시 발동 대기가 끝났나). 같은 동작의 다음 사건은 늘 false.</summary>
        public bool IsRollMoment(int actionId, double now)
        {
            if (_any && actionId == _lastAction) return false;
            _any = true;
            _lastAction = actionId;
            return now >= _readyAt - 1e-9;
        }

        /// <summary>
        /// 굴린다. roll01은 0~1 난수 하나(굴림 순간에만 쓰인다). 발동하면 다시 발동 대기 0.2초를 건다.
        /// </summary>
        /// <param name="rollPermille">효과 세기‰(확률 30~40%를 정함).</param>
        public bool TryProc(int actionId, double now, double roll01, int rollPermille)
        {
            if (!IsRollMoment(actionId, now)) return false;
            if (roll01 >= LegendRules.LightningChance(rollPermille)) return false;
            MarkProc(now);
            return true;
        }

        /// <summary>발동했다: 다시 발동 대기 0.2초를 건다(IsRollMoment 뒤 난수를 따로 뽑는 쪽이 부른다).</summary>
        public void MarkProc(double now) => _readyAt = now + LegendRules.LightningCooldown;
    }

    /// <summary>
    /// 불꽃 발자국 걸음 세기: 전투 중 걸은 거리를 모아 1.2유닛마다 불길 하나를 깔게 한다.
    /// 전투가 아니거나 순간 이동(한 번에 2유닛 넘게)이면 모은 거리를 비운다. 장화 이동이 올라도 불길 간격이 같아 끊기지 않는다.
    /// </summary>
    public sealed class FlameStepCounter
    {
        double _carry;

        /// <summary>지금까지 모은(아직 불길이 되지 않은) 거리.</summary>
        public double Carry => _carry;

        public void Reset() => _carry = 0;

        /// <summary>
        /// 한 프레임 걸음을 더한다. 깔 불길 자리를 이번 걸음 시작점에서의 거리로 offsets에 담는다(없으면 null 가능).
        /// </summary>
        /// <returns>이번에 깔 불길 수.</returns>
        public int Advance(double distance, bool inCombat, List<double> offsets = null)
        {
            offsets?.Clear();
            if (!inCombat || distance < 0 || distance > LegendRules.FlameTeleportGuard)
            {
                _carry = 0;
                return 0;
            }
            double before = _carry;
            _carry += distance;
            int count = 0;
            double step = LegendRules.FlameStepDistance;
            while (_carry + 1e-9 >= step)
            {
                _carry -= step;
                count++;
                offsets?.Add(step * count - before);
            }
            if (_carry < 0) _carry = 0;
            return count;
        }
    }

    /// <summary>
    /// 연쇄 폭발 하나의 흐름(장비 문서 6장): 첫 폭발 포함 최대 12번, 폭발 사이 0.1초.
    /// 같은 순간의 처치(한 번 휘둘러 여럿)는 같은 연쇄로 묶고, 이 연쇄의 폭발로 죽은 적도 이 연쇄에 줄을 선다.
    /// 여러 마리 처치 연출(히트스톱·느린 화면)은 연쇄 하나에 처음 한 번만(TakeJuice). 연속 처치 숫자는 계속 올린다.
    /// </summary>
    public sealed class BlastChain
    {
        bool _juiceUsed;

        /// <summary>지금까지 예약한 폭발 수(1~12).</summary>
        public int Count { get; private set; }
        /// <summary>마지막으로 예약한 폭발 시각.</summary>
        public double LastAt { get; private set; } = double.NegativeInfinity;
        public bool Full => Count >= LegendRules.BlastMaxChain;

        /// <summary>
        /// 폭발 하나를 예약한다. 첫 폭발은 지금, 그다음은 앞 폭발 + 0.1초(지금보다 이르면 지금). 12번이 차면 false.
        /// </summary>
        public bool TrySchedule(double now, out double at)
        {
            at = 0;
            if (Full) return false;
            at = Count == 0 ? now : Math.Max(now, LastAt + LegendRules.BlastInterval);
            LastAt = at;
            Count++;
            return true;
        }

        /// <summary>처치 연출을 낼 차례인가. 처음 한 번만 true.</summary>
        public bool TakeJuice()
        {
            if (_juiceUsed) return false;
            _juiceUsed = true;
            return true;
        }

        /// <summary>
        /// 이 폭발의 처치 수로 여러 마리 처치 연출을 낼 차례인가. 연출이 실제로 나는 처치 수(LegendRules.MultiKillJuiceMin 이상)일 때만
        /// 기회를 쓴다. 그래서 첫 폭발이 1~2마리를 잡고 다음 폭발이 5마리를 잡으면 다음 폭발이 연출을 받는다(장비 문서 6장 '연쇄 하나에 처음 한 번').
        /// </summary>
        public bool TakeJuice(int kills) => kills >= LegendRules.MultiKillJuiceMin && TakeJuice();
    }
}
