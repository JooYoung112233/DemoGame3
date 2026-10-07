using System.Linq;
using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 층을 떠날 때 바닥 장비 거두기(시스템·컨텐츠 다듬기 검토 1차 Q3, 2차 7-7): 가방 20칸 + 넘침 6칸.
    /// 희귀·영웅은 넘침 칸까지, 일반·고급은 가방 칸까지만, 전설은 늘 거둔다. 넘침 칸까지 차서 남는 희귀·영웅은 RareLeft(떠나기 전에 묻기).
    /// </summary>
    public sealed class BagSweepTests
    {
        const int Capacity = 20;

        static Grade[] G(params Grade[] grades) => grades;

        [Test]
        public void RoomyBagTakesEverything()
        {
            var plan = BagSweep.Plan(5, Capacity, G(Grade.Common, Grade.Rare, Grade.Uncommon, Grade.Legendary, Grade.Epic));
            Assert.AreEqual(5, plan.Taken);
            Assert.AreEqual(0, plan.Left);
            Assert.AreEqual(10, plan.BagAfter);
            // 전설 → 영웅 → 희귀 → 고급 → 일반 차례.
            CollectionAssert.AreEqual(new[] { 3, 4, 1, 2, 0 }, plan.Take.ToArray());
        }

        [Test]
        public void NothingOnTheFloorIsEmpty()
        {
            var plan = BagSweep.Plan(12, Capacity, G());
            Assert.AreEqual(0, plan.Taken);
            Assert.AreEqual(0, plan.Left);
            Assert.AreEqual(12, plan.BagAfter);
            Assert.IsNull(BagSweep.ArrivalLine(plan, Capacity));
            Assert.AreEqual(0, BagSweep.Plan(3, Capacity, null).Taken);
        }

        [Test]
        public void RareGoesIntoOverflowButCommonStopsAtTheBag()
        {
            // 가방 19/20: 일반 2개 가운데 1개만 가방 칸에, 희귀 2개는 넘침 칸으로.
            var plan = BagSweep.Plan(19, Capacity, G(Grade.Common, Grade.Common, Grade.Rare, Grade.Rare));
            Assert.AreEqual(3, plan.Taken);
            Assert.AreEqual(1, plan.LowLeft);
            Assert.AreEqual(0, plan.RareLeft);
            Assert.AreEqual(22, plan.BagAfter);
            // 가방이 가득(20/20)이면 일반·고급은 하나도 들어가지 않고 희귀·영웅은 넘침 6칸까지.
            plan = BagSweep.Plan(Capacity, Capacity, G(Grade.Uncommon, Grade.Epic, Grade.Rare));
            CollectionAssert.AreEqual(new[] { 1, 2 }, plan.Take.ToArray());
            Assert.AreEqual(1, plan.LowLeft);
        }

        [Test]
        public void FullOverflowLeavesRareAndAsks()
        {
            // 넘침 칸이 하나 남음: 영웅이 먼저 들어가고 희귀 둘은 남는다.
            var plan = BagSweep.Plan(Capacity + BagSweep.OverflowSlots - 1, Capacity, G(Grade.Rare, Grade.Epic, Grade.Rare));
            CollectionAssert.AreEqual(new[] { 1 }, plan.Take.ToArray());
            Assert.AreEqual(2, plan.RareLeft);
            // 넘침 칸까지 다 참: 희귀는 남고, 전설은 그래도 들어간다(7/6처럼 넘침을 넘어도).
            plan = BagSweep.Plan(Capacity + BagSweep.OverflowSlots, Capacity, G(Grade.Rare, Grade.Legendary));
            CollectionAssert.AreEqual(new[] { 1 }, plan.Take.ToArray());
            Assert.AreEqual(1, plan.RareLeft);
            Assert.AreEqual(Capacity + BagSweep.OverflowSlots + 1, plan.BagAfter);
            // 벗은 장비로 이미 넘침을 넘은 가방(28칸)도 같다.
            plan = BagSweep.Plan(28, Capacity, G(Grade.Epic, Grade.Common));
            Assert.AreEqual(0, plan.Taken);
            Assert.AreEqual(1, plan.RareLeft);
            Assert.AreEqual(1, plan.LowLeft);
        }

        [Test]
        public void RareKeepsTheRoomBeforeCommon()
        {
            // 가방 15칸에 일반 5 + 희귀 12: 희귀를 먼저 11개(가방 5 + 넘침 6) 담고, 일반은 자리가 없다.
            var floor = Enumerable.Repeat(Grade.Common, 5).Concat(Enumerable.Repeat(Grade.Rare, 12)).ToArray();
            var plan = BagSweep.Plan(15, Capacity, floor);
            Assert.AreEqual(11, plan.Taken);
            Assert.IsTrue(plan.Take.All(i => floor[i] == Grade.Rare));
            Assert.AreEqual(1, plan.RareLeft);
            Assert.AreEqual(5, plan.LowLeft);
            Assert.AreEqual(Capacity + BagSweep.OverflowSlots, plan.BagAfter);
        }

        [Test]
        public void ArrivalLineCountsTakenOverflowAndLeft()
        {
            Assert.AreEqual("떠나며 바닥 장비 2개를 가방에 거뒀다",
                BagSweep.ArrivalLine(BagSweep.Plan(3, Capacity, G(Grade.Common, Grade.Rare)), Capacity));
            Assert.AreEqual("떠나며 바닥 장비 3개를 가방에 거뒀다 · 넘침 칸 2/6 · 가방이 차 1개는 두고 왔다",
                BagSweep.ArrivalLine(BagSweep.Plan(19, Capacity, G(Grade.Common, Grade.Common, Grade.Rare, Grade.Rare)), Capacity));
            Assert.AreEqual("가방이 차 바닥 장비 2개는 두고 왔다",
                BagSweep.ArrivalLine(BagSweep.Plan(Capacity + BagSweep.OverflowSlots, Capacity, G(Grade.Common, Grade.Rare)), Capacity));
        }

        // ── 재화 쓸 곳 1차 5-3 '떠날 때 거두기'(장비 목록판 Plan: ㉡ 분해·가득이면 일반·고급 분해) ──

        static GearItem Gear(Grade grade, int enhance = 0) => new GearItem(GearBaseTable.Longsword, grade, 1, 1000, enhance);
        static GearItem[] Floor(params GearItem[] items) => items;
        static bool[] Better(params bool[] improves) => improves;

        [Test]
        public void OverflowSlotsComeFromBagRules()
        {
            Assert.AreEqual(BagRules.OverflowSlots, BagSweep.OverflowSlots);
            Assert.AreEqual(6, BagSweep.OverflowSlots);
            Assert.AreEqual(0, BagSweep.Plan(19, Capacity, G(Grade.Common, Grade.Rare)).Salvaged, "등급 목록판은 분해하지 않음");
        }

        [Test]
        public void GearPlanSalvagesUselessCommonsWhenRoomy()
        {
            // 일반 +0 나아지지 않음(㉡ 대상) · 고급 · 일반 +0 나아짐 · 희귀 · 일반 +1 나아지지 않음
            var floor = Floor(Gear(Grade.Common), Gear(Grade.Uncommon), Gear(Grade.Common), Gear(Grade.Rare), Gear(Grade.Common, 1));
            var better = Better(false, false, true, false, false);
            var plan = BagSweep.Plan(5, Capacity, floor, better, true);
            CollectionAssert.AreEqual(new[] { 3, 1, 2, 4 }, plan.Take.ToArray(), "희귀 → 고급 → 일반(입력 차례)");
            CollectionAssert.AreEqual(new[] { 0 }, plan.Salvage.ToArray(), "㉡ 대상만 분해");
            Assert.AreEqual(1, plan.Salvaged);
            Assert.AreEqual(1, plan.SalvageStones);
            Assert.AreEqual(9, plan.BagAfter);
            Assert.AreEqual(0, plan.Left);
            Assert.AreEqual("떠나며 바닥 장비 4개를 가방에 거뒀다 · 저절로 분해 1개 · 강화석 +1", BagSweep.ArrivalLine(plan, Capacity));

            var off = BagSweep.Plan(5, Capacity, floor, better, false);
            CollectionAssert.AreEqual(new[] { 3, 1, 0, 2, 4 }, off.Take.ToArray(), "㉡ 끔이면 모두 넣음");
            Assert.AreEqual(0, off.Salvaged);
            Assert.AreEqual(0, off.SalvageStones);
            Assert.AreEqual(10, off.BagAfter);
            Assert.AreEqual("떠나며 바닥 장비 5개를 가방에 거뒀다", BagSweep.ArrivalLine(off, Capacity));
        }

        [Test]
        public void GearPlanWhenFullSalvagesLowAndOverflowsRare()
        {
            // 가방 20/20: 일반 +2 · 고급 · 영웅 · 희귀 · 전설 · 희귀
            var floor = Floor(Gear(Grade.Common, 2), Gear(Grade.Uncommon), Gear(Grade.Epic), Gear(Grade.Rare), Gear(Grade.Legendary), Gear(Grade.Rare));
            var plan = BagSweep.Plan(Capacity, Capacity, floor, Better(false, false, false, false, false, false), true);
            CollectionAssert.AreEqual(new[] { 4, 2, 3, 5 }, plan.Take.ToArray(), "전설 → 영웅 → 희귀가 넘침 칸으로");
            CollectionAssert.AreEqual(new[] { 1, 0 }, plan.Salvage.ToArray(), "가득이면 일반·고급은 분해(고급 먼저)");
            Assert.AreEqual(SalvageRules.Stones(floor[1]) + SalvageRules.Stones(floor[0]), plan.SalvageStones);
            Assert.AreEqual(4, plan.SalvageStones, "고급 2 + 일반 +2(1 + 환급 1)");
            Assert.AreEqual(Capacity + 4, plan.BagAfter);
            Assert.AreEqual(0, plan.RareLeft);
            Assert.AreEqual(0, plan.LowLeft);
            Assert.AreEqual("떠나며 바닥 장비 4개를 가방에 거뒀다 · 넘침 칸 4/6 · 가방 가득 — 일반·고급 2개 분해 · 강화석 +4",
                BagSweep.ArrivalLine(plan, Capacity));
        }

        [Test]
        public void GearPlanAsksWhenOverflowIsFullButLegendAlwaysComes()
        {
            // 넘침 칸 하나 남음(25/20): 전설이 먼저 마지막 칸을 쓰고, 영웅·희귀는 묻기, 나아지는 일반도 가득이라 분해.
            var floor = Floor(Gear(Grade.Rare), Gear(Grade.Epic), Gear(Grade.Rare), Gear(Grade.Legendary), Gear(Grade.Common));
            var plan = BagSweep.Plan(Capacity + BagSweep.OverflowSlots - 1, Capacity, floor, Better(false, false, false, false, true), true);
            CollectionAssert.AreEqual(new[] { 3 }, plan.Take.ToArray());
            Assert.AreEqual(3, plan.RareLeft, "넘침도 가득 → 떠나기 전에 묻는다");
            CollectionAssert.AreEqual(new[] { 4 }, plan.Salvage.ToArray());
            Assert.AreEqual(1, plan.SalvageStones);
            Assert.AreEqual(Capacity + BagSweep.OverflowSlots, plan.BagAfter);
            Assert.AreEqual("떠나며 바닥 장비 1개를 가방에 거뒀다 · 넘침 칸 6/6 · 가방이 차 3개는 두고 왔다 · 가방 가득 — 일반·고급 1개 분해 · 강화석 +1",
                BagSweep.ArrivalLine(plan, Capacity));

            // 넘침 끝을 넘은 가방(28)에도 전설은 늘 들어간다.
            plan = BagSweep.Plan(28, Capacity, Floor(Gear(Grade.Legendary), Gear(Grade.Epic)), Better(false, false), true);
            CollectionAssert.AreEqual(new[] { 0 }, plan.Take.ToArray());
            Assert.AreEqual(1, plan.RareLeft);
            Assert.AreEqual(29, plan.BagAfter);
        }

        [Test]
        public void GearPlanEdgeCases()
        {
            var empty = BagSweep.Plan(7, Capacity, Floor(), Better(), true);
            Assert.AreEqual(0, empty.Taken);
            Assert.AreEqual(0, empty.Salvaged);
            Assert.AreEqual(7, empty.BagAfter);
            Assert.IsNull(BagSweep.ArrivalLine(empty, Capacity));
            Assert.AreEqual(0, BagSweep.Plan(7, Capacity, null, null, true).Taken);

            // null 칸은 건너뛰고, 나아짐 목록이 없으면 나아지는 것으로 보아 녹이지 않는다.
            var plan = BagSweep.Plan(3, Capacity, Floor(null, Gear(Grade.Common)), null, true);
            CollectionAssert.AreEqual(new[] { 1 }, plan.Take.ToArray());
            Assert.AreEqual(0, plan.Salvaged);

            // 분해만 있을 때(가방 여유): ㉡ 몫이라 '저절로 분해' 조각만.
            plan = BagSweep.Plan(3, Capacity, Floor(Gear(Grade.Common)), Better(false), true);
            Assert.AreEqual(0, plan.Taken);
            Assert.AreEqual(1, plan.Salvaged);
            Assert.AreEqual("저절로 분해 1개 · 강화석 +1", BagSweep.ArrivalLine(plan, Capacity));
        }
    }
}
