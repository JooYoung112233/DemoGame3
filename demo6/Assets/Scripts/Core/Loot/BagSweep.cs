using System;
using System.Collections.Generic;
using Demo6.Core.Town;

namespace Demo6.Core.Loot
{
    /// <summary>층을 떠날 때 바닥 장비를 어떻게 거둘지(BagSweep.Plan 결과).</summary>
    public sealed class BagSweepPlan
    {
        /// <summary>거둘 바닥 장비(입력 목록 번호). 전설 → 영웅 → 희귀 → 고급 → 일반 차례, 같은 등급은 입력 차례.</summary>
        public readonly List<int> Take = new List<int>();
        /// <summary>넘침 칸까지 다 차서 두고 가는 희귀·영웅 수(떠나기 전에 한 번 묻는다).</summary>
        public int RareLeft;
        /// <summary>가방 칸이 다 차서 두고 가는 일반·고급 수.</summary>
        public int LowLeft;
        /// <summary>거둔 뒤 가방 장비 수(넘침 칸 포함).</summary>
        public int BagAfter;
        /// <summary>그 자리에서 분해할 바닥 장비(입력 목록 번호, 거두는 차례). 장비 목록판 Plan만 채운다(재화 쓸 곳 1차 5-3).</summary>
        public readonly List<int> Salvage = new List<int>();
        /// <summary>분해로 받을 강화석 합(SalvageRules.Stones).</summary>
        public int SalvageStones;

        public int Taken => Take.Count;
        public int Left => RareLeft + LowLeft;
        public int Salvaged => Salvage.Count;
    }

    /// <summary>
    /// 층을 떠날 때 바닥 장비 거두기(시스템·컨텐츠 다듬기 검토 1차 Q3, 2차 기획 7-7 '방이나 층을 떠날 때'·'가방이 찼을 때'). 순수 규칙.
    /// 시험판 가방은 20칸(M0b 축소판, Inventory.BagCapacity)이고 넘침 칸은 2차 7-7대로 6칸이다. 칸은 수로만 센다.
    /// ① 희귀·영웅: 가방 + 넘침 칸(합 26)까지 거둔다(영웅 먼저). 남으면 떠나기 전에 한 번 묻는다(RareLeft).
    /// ② 일반·고급: 가방 칸(20)까지만 거둔다(고급 먼저). 7-7은 가방이 차면 줍는 순간 자동 분해지만 분해는 아직 없어(묶음 1) 바닥에 두고 도착 글에 적는다.
    ///    넘침 칸은 희귀·영웅 몫이라, 희귀·영웅을 되도록 많이 담은 뒤 남는 자리에서 일반·고급을 담는다.
    /// ③ 전설: 칸과 상관없이 늘 거둔다(넘침 칸이 다 차도 들어감).
    /// 지금 가방이 이미 넘쳐 있으면(벗은 장비는 가득 차도 넘쳐 들어감) 그만큼 자리가 줄어든다.
    /// 재화 쓸 곳 1차 5-3(분해가 들어온 뒤): 장비 목록판 Plan이 BagRules.Decide(Sweep)로 '떠날 때 거두기' 줄을 입힌다 —
    /// ㉡ 대상(일반 +0·나아지지 않음·켬)은 분해, 가방이 차면 일반·고급은 분해, 희귀·영웅은 넘침 칸, 넘침도 차면 묻기, 전설은 늘 거둔다.
    /// 등급 목록판 Plan(분해 없음)은 예전 뜻 그대로 남긴다.
    /// </summary>
    public static class BagSweep
    {
        /// <summary>넘침 칸(2차 7-7). 가방 칸 위에 이만큼 더 들어간다. 값은 BagRules 한 곳에서 읽는다(재화 쓸 곳 1차 5-1).</summary>
        public const int OverflowSlots = BagRules.OverflowSlots;

        /// <summary>떠나며 거둘 계획. bagCount = 지금 가방 장비 수, capacity = 가방 칸, floor = 바닥 장비 등급(차례 그대로 번호가 된다).</summary>
        public static BagSweepPlan Plan(int bagCount, int capacity, IReadOnlyList<Grade> floor)
        {
            bagCount = Math.Max(0, bagCount);
            capacity = Math.Max(0, capacity);
            var plan = new BagSweepPlan { BagAfter = bagCount };
            if (floor == null || floor.Count == 0) return plan;

            var legend = new List<int>();
            var rare = new List<int>();
            var low = new List<int>();
            for (int i = 0; i < floor.Count; i++)
            {
                var grade = floor[i];
                if (grade >= Grade.Legendary) legend.Add(i);
                else if (grade >= Grade.Rare) rare.Add(i);
                else low.Add(i);
            }
            Comparison<int> higherFirst = (a, b) =>
            {
                int byGrade = ((int)floor[b]).CompareTo((int)floor[a]);
                return byGrade != 0 ? byGrade : a.CompareTo(b);
            };
            rare.Sort(higherFirst);
            low.Sort(higherFirst);

            // 희귀·영웅이 쓸 수 있는 자리(가방 + 넘침), 그 가운데 일반·고급은 가방 칸 안의 남는 자리만.
            int rareRoom = Math.Max(0, capacity + OverflowSlots - bagCount);
            int rareTake = Math.Min(rare.Count, rareRoom);
            int lowTake = Math.Max(0, Math.Min(low.Count, Math.Min(capacity - bagCount, rareRoom - rareTake)));

            plan.Take.AddRange(legend);
            for (int i = 0; i < rareTake; i++) plan.Take.Add(rare[i]);
            for (int i = 0; i < lowTake; i++) plan.Take.Add(low[i]);
            plan.RareLeft = rare.Count - rareTake;
            plan.LowLeft = low.Count - lowTake;
            plan.BagAfter = bagCount + plan.Take.Count;
            return plan;
        }

        /// <summary>
        /// 떠나며 거둘 계획(장비 목록판, 재화 쓸 곳 1차 5-3 '떠날 때 거두기'). bagCount = 지금 가방 장비 수, capacity = 가방 칸,
        /// floor = 바닥 장비(차례 그대로 번호가 된다, null 칸은 건너뜀), improves = 같은 번호 장비를 끼면 나아지는가(Inventory.Improves.
        /// null이거나 짧으면 나아지는 것으로 보아 녹이지 않는다), autoSalvageCommon = ㉡ 켬(Tuning.AutoSalvageCommon).
        /// 전설 → 영웅 → 희귀 → 고급 → 일반(같은 등급은 입력 차례)으로 BagRules.Decide(Sweep, …, 여유 칸 0)를 부르며 가방 수를 늘린다.
        /// 넣음·넘침 칸 → Take, 분해 → Salvage(+ SalvageStones), 묻기 → RareLeft, 남김 → LowLeft. BagAfter = 거둔 뒤 가방 수.
        /// 내려놓은 장비(4-4)는 부르는 쪽이 floor에서 뺀다.
        /// </summary>
        public static BagSweepPlan Plan(int bagCount, int capacity, IReadOnlyList<GearItem> floor, IReadOnlyList<bool> improves, bool autoSalvageCommon)
        {
            bagCount = Math.Max(0, bagCount);
            capacity = Math.Max(0, capacity);
            var plan = new BagSweepPlan { BagAfter = bagCount };
            if (floor == null || floor.Count == 0) return plan;

            var order = new List<int>(floor.Count);
            for (int i = 0; i < floor.Count; i++)
                if (floor[i] != null) order.Add(i);
            order.Sort((a, b) =>
            {
                int byGrade = ((int)floor[b].Grade).CompareTo((int)floor[a].Grade);
                return byGrade != 0 ? byGrade : a.CompareTo(b);
            });

            int count = bagCount;
            foreach (int i in order)
            {
                var item = floor[i];
                bool better = improves == null || i >= improves.Count || improves[i];
                switch (BagRules.Decide(PickupReason.Sweep, item.Grade, item.Enhance, better, count, capacity, autoSalvageCommon, 0))
                {
                    case PickupAction.Take:
                    case PickupAction.TakeOverflow:
                        plan.Take.Add(i);
                        count++;
                        break;
                    case PickupAction.Salvage:
                        plan.Salvage.Add(i);
                        plan.SalvageStones += SalvageRules.Stones(item);
                        break;
                    case PickupAction.Ask:
                        plan.RareLeft++;
                        break;
                    default:
                        plan.LowLeft++;
                        break;
                }
            }
            plan.BagAfter = count;
            return plan;
        }

        /// <summary>
        /// 도착 글 한 줄(마을 도착 카드 또는 다음 층 알림): "떠나며 바닥 장비 3개를 가방에 거뒀다 · 넘침 칸 2/6 · 가방이 차 1개는 두고 왔다".
        /// 거둔 것도 두고 온 것도 분해한 것도 없으면 null. 넘침 칸은 가방 칸을 넘었을 때만 적는다.
        /// 분해한 것이 있으면 맨 끝에 한 조각을 더한다(재화 쓸 곳 1차 5-3): 거둔 뒤 가방이 가득이면 "가방 가득 — 일반·고급 n개 분해 · 강화석 +n",
        /// 아니면 ㉡ 몫뿐이라 "저절로 분해 n개 · 강화석 +n"(글은 ForgeText).
        /// </summary>
        public static string ArrivalLine(BagSweepPlan plan, int capacity)
        {
            if (plan == null || (plan.Taken == 0 && plan.Left == 0 && plan.Salvaged == 0)) return null;
            var parts = new List<string>();
            if (plan.Taken > 0) parts.Add($"떠나며 바닥 장비 {plan.Taken}개를 가방에 거뒀다");
            int over = plan.BagAfter - Math.Max(0, capacity);
            if (plan.Taken > 0 && over > 0) parts.Add($"넘침 칸 {over}/{OverflowSlots}");
            if (plan.Left > 0) parts.Add(plan.Taken > 0 ? $"가방이 차 {plan.Left}개는 두고 왔다" : $"가방이 차 바닥 장비 {plan.Left}개는 두고 왔다");
            if (plan.Salvaged > 0)
                parts.Add(plan.BagAfter >= Math.Max(0, capacity)
                    ? ForgeText.SweepSalvageLine(plan.Salvaged, plan.SalvageStones)
                    : ForgeText.AutoSalvageBatchNotice(plan.Salvaged, plan.SalvageStones));
            return string.Join(" · ", parts);
        }
    }
}
