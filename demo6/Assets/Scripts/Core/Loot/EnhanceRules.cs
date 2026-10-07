using System;
using Demo6.Core.Random;

namespace Demo6.Core.Loot
{
    /// <summary>강화 견적이 막힌 까닭(재화 쓸 곳 1차 2-4). None이면 두드릴 수 있다.</summary>
    public enum EnhanceBlock
    {
        None,
        /// <summary>고른 장비가 없다.</summary>
        NoItem,
        /// <summary>등급 상한에 닿았다(일반 +5, 고급 +7).</summary>
        GradeCap,
        /// <summary>1차 상한(+7)에 닿았다. +8부터는 하락이 있어 다음에 연다.</summary>
        FirstPassCap,
        /// <summary>강화석이 시도 비용보다 적다.</summary>
        NotEnoughStones,
    }

    /// <summary>
    /// 강화 견적 한 장(재화 쓸 곳 1차 2-4·2-6). From → To 단계, 시도 비용, 이번 확률(‰), 지금 실패 수와 천장(실패 몇 번 뒤 확정),
    /// 실패마다 더하는 보정(‰), 오를 수 있는 끝(Cap). 상한 막힘이면 To = From이고 비용·확률은 0이다.
    /// 강화석 모자람 막힘은 비용·확률을 그대로 채운다('강화석이 모자란다 — 3 필요, 2 있음').
    /// </summary>
    public readonly struct EnhanceQuote
    {
        public readonly int From, To, Cost, ChancePermille, Fails, PityFails, GaugeStepPermille, Cap;
        public readonly bool Guaranteed;
        public readonly EnhanceBlock Block;

        public EnhanceQuote(int from, int to, int cost, int chancePermille, int fails, int pityFails, int gaugeStepPermille, int cap,
            bool guaranteed, EnhanceBlock block)
        {
            From = from;
            To = to;
            Cost = cost;
            ChancePermille = chancePermille;
            Fails = fails;
            PityFails = pityFails;
            GaugeStepPermille = gaugeStepPermille;
            Cap = cap;
            Guaranteed = guaranteed;
            Block = block;
        }

        /// <summary>두드릴 수 있는가(막힘 없음).</summary>
        public bool CanTry => Block == EnhanceBlock.None;
    }

    /// <summary>
    /// 강화 굴림 결과(재화 쓸 곳 1차 2-4). Item = 갈아 끼울 새 장비(막히면 받은 장비 그대로), Spent = 쓴 강화석(막히면 0).
    /// Guaranteed = 천장(또는 100%)이라 난수를 굴리지 않았다.
    /// </summary>
    public readonly struct EnhanceOutcome
    {
        public readonly bool Success;
        public readonly GearItem Item;
        public readonly int Spent;
        public readonly bool Guaranteed;
        public readonly EnhanceBlock Block;

        public EnhanceOutcome(bool success, GearItem item, int spent, bool guaranteed, EnhanceBlock block)
        {
            Success = success;
            Item = item;
            Spent = spent;
            Guaranteed = guaranteed;
            Block = block;
        }
    }

    /// <summary>
    /// 강화 확률·천장·보정과 굴림(재화 쓸 곳 1차 2-2~2-5, 2차 8-1·8-3). 순수 규칙. 시험: EnhanceRulesTests.
    /// 처음 확률 +1~+7 = 100·95·90·85·80·70·60%. 천장(실패 몇 번 뒤 확정) +1 0, +2~+5 1, +6·+7 2. 보정은 +6·+7만 실패마다 +10%p.
    /// 최종 확률 = 천장에 닿았으면 100%, 아니면 min(100%, 처음 + 실패 수 × 보정). 실패하면 강화석만 잃고 장비·단계는 그대로다.
    /// 1차는 +7까지만 연다(+8부터 하락·되찾기·연속 하락 천장은 2차). 숨은 보정 없이 보이는 확률 그대로 굴린다(2차 8-5).
    /// </summary>
    public static class EnhanceRules
    {
        /// <summary>1차에서 오를 수 있는 가장 높은 단계(2-2).</summary>
        public const int FirstPassMax = 7;

        /// <summary>2차 8-1 처음 확률(‰): +1~+7.</summary>
        static readonly int[] BaseChances = { 0, 1000, 950, 900, 850, 800, 700, 600 };
        /// <summary>천장(실패 몇 번 뒤 다음은 확정): +1~+7.</summary>
        static readonly int[] Pities = { 0, 0, 1, 1, 1, 1, 2, 2 };
        /// <summary>실패마다 더하는 보정(‰): +6·+7만 100.</summary>
        static readonly int[] GaugeSteps = { 0, 0, 0, 0, 0, 0, 100, 100 };

        static bool InRange(int target) => target >= 1 && target <= FirstPassMax;

        /// <summary>그 단계로 오르는 처음 확률(‰). +1 1000 … +7 600, 범위 밖 0.</summary>
        public static int BaseChancePermille(int target) => InRange(target) ? BaseChances[target] : 0;

        /// <summary>실패마다 더하는 보정(‰). +6·+7 100, 그 밖 0.</summary>
        public static int GaugeStepPermille(int target) => InRange(target) ? GaugeSteps[target] : 0;

        /// <summary>천장: 이만큼 실패하면 다음은 확정. +1 0, +2~+5 1, +6·+7 2, 범위 밖 0.</summary>
        public static int PityFails(int target) => InRange(target) ? Pities[target] : 0;

        /// <summary>
        /// 실패 수를 넣은 이번 확률(‰). 천장에 닿았으면 1000, 아니면 min(1000, 처음 + 실패 × 보정). 범위 밖 단계는 0.
        /// 예: +6은 700·800·1000, +7은 600·700·1000, +2~+5는 실패 1번 뒤 1000.
        /// </summary>
        public static int ChancePermille(int target, int fails)
        {
            int baseChance = BaseChancePermille(target);
            if (baseChance <= 0) return 0;
            fails = Math.Max(0, fails);
            if (fails >= PityFails(target)) return 1000;
            return Math.Min(1000, baseChance + fails * GaugeStepPermille(target));
        }

        /// <summary>오를 수 있는 끝 = min(등급 상한, 7). 일반 5, 고급 7, 희귀·영웅·전설은 1차에서 7.</summary>
        public static int MaxTarget(Grade grade) => Math.Min(GearMath.EnhanceCap(grade), FirstPassMax);

        /// <summary>
        /// 이 장비를 한 번 두드리는 견적(2-4). 막힘 차례: 장비 없음 → 등급 상한 → 1차 상한 → 강화석 모자람.
        /// 비용 = GearMath.EnhanceCost(부위, 다음 단계).
        /// </summary>
        public static EnhanceQuote Quote(GearItem item, int stones)
        {
            if (item == null) return new EnhanceQuote(0, 0, 0, 0, 0, 0, 0, 0, false, EnhanceBlock.NoItem);
            int from = item.Enhance;
            int fails = item.EnhanceFails;
            int cap = MaxTarget(item.Grade);
            if (from >= GearMath.EnhanceCap(item.Grade))
                return new EnhanceQuote(from, from, 0, 0, fails, 0, 0, cap, false, EnhanceBlock.GradeCap);
            if (from >= FirstPassMax)
                return new EnhanceQuote(from, from, 0, 0, fails, 0, 0, cap, false, EnhanceBlock.FirstPassCap);
            int to = from + 1;
            int cost = GearMath.EnhanceCost(item.Part, to);
            int chance = ChancePermille(to, fails);
            var block = stones < cost ? EnhanceBlock.NotEnoughStones : EnhanceBlock.None;
            return new EnhanceQuote(from, to, cost, chance, fails, PityFails(to), GaugeStepPermille(to), cap, chance >= 1000, block);
        }

        /// <summary>
        /// 한 번 두드린다(2-4). 막히면 아무것도 쓰지 않는다(Item 그대로, Spent 0). 확정(천장·100%)이면 난수를 부르지 않는다.
        /// 아니면 rng.NextInt(0, 1000) &lt; 확률이면 성공. 성공 = 강화 +1·실패 수 0, 실패 = 단계 그대로·실패 수 +1. 어느 쪽이든 Spent = 시도 비용.
        /// 옵션·전설·룬·굴림‰·아이템 레벨은 그대로다. 강화석을 실제로 빼는 일은 부르는 쪽(Wallet.TrySpendStones)이 한다.
        /// </summary>
        public static EnhanceOutcome Try(GearItem item, int stones, IRandom rng)
        {
            var q = Quote(item, stones);
            if (q.Block != EnhanceBlock.None) return new EnhanceOutcome(false, item, 0, false, q.Block);
            bool success;
            if (q.Guaranteed) success = true;
            else
            {
                if (rng == null) throw new ArgumentNullException(nameof(rng), "확정이 아닌 강화는 난수가 있어야 한다");
                success = rng.NextInt(0, 1000) < q.ChancePermille;
            }
            var next = success ? item.WithEnhance(q.To) : item.WithEnhanceFails(q.Fails + 1);
            return new EnhanceOutcome(success, next, q.Cost, q.Guaranteed, EnhanceBlock.None);
        }

        /// <summary>
        /// 그 단계로 오르기까지 평균 시도 수(실패 0에서 시작, 2-3). +1 1.00, +2 1.05, +3 1.10, +4 1.15, +5 1.20, +6 1.36, +7 1.52. 범위 밖 0.
        /// </summary>
        public static double ExpectedAttempts(int target)
        {
            if (BaseChancePermille(target) <= 0) return 0;
            double reach = 1, sum = 0;
            for (int fails = 0; ; fails++)
            {
                sum += reach;
                int chance = ChancePermille(target, fails);
                if (chance >= 1000) break;
                reach *= (1000 - chance) / 1000.0;
            }
            return sum;
        }

        /// <summary>
        /// from에서 to까지 올리는 평균 강화석(실패 포함, 각 단계 실패 0에서 시작, 2-3). 1차 범위(0~7) 밖은 잘라서 센다.
        /// 예: 무기 0→5 15.65, 0→7 31.57, 갑옷 0→5 8.95, 투구 0→7 8.38.
        /// </summary>
        public static double ExpectedStones(GearPart part, int from, int to)
        {
            from = Math.Max(0, from);
            to = Math.Min(FirstPassMax, to);
            double sum = 0;
            for (int target = from + 1; target <= to; target++)
                sum += GearMath.EnhanceCost(part, target) * ExpectedAttempts(target);
            return sum;
        }
    }
}
