using System;
using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 재화 쓸 곳 1차 12-1 BagRulesTests(12~13): 가방 칸 20·25·30(늘린 수 0~2로 자름), 넘침 끝 = 칸 + 6, 거의 참 = 칸 − 4 이상,
    /// 5-3 줍기 판정 표의 모든 줄(F·저절로·떠날 때·바꿔 낌 × 등급 × 칸 상태 × ㉡ 켬/끔 × ㉠ 0/3).
    /// </summary>
    public sealed class BagRulesTests
    {
        const int Cap = 20;
        /// <summary>칸 상태: 칸 남음 / 가득(넘침 남음) / 넘침도 가득.</summary>
        const int Room = 10, Full = 20, OverFull = 26;

        static readonly Grade[] Grades = (Grade[])Enum.GetValues(typeof(Grade));
        static readonly bool[] Bools = { false, true };
        static readonly int[] Reserves = { 0, BagRules.AutoReserveSlots };

        static PickupAction D(PickupReason reason, Grade grade, int count, int enhance = 0, bool improves = false,
            bool autoSalvage = true, int reserve = 0, int capacity = Cap) =>
            BagRules.Decide(reason, grade, enhance, improves, count, capacity, autoSalvage, reserve);

        /// <summary>㉡ 대상이 아닌 일반·고급(강화·나아짐·끔·고급).</summary>
        static readonly (Grade grade, int enhance, bool improves, bool auto)[] LowNotTarget =
        {
            (Grade.Common, 0, false, false),
            (Grade.Common, 0, true, true),
            (Grade.Common, 1, false, true),
            (Grade.Common, 3, true, false),
            (Grade.Uncommon, 0, false, true),
            (Grade.Uncommon, 2, false, false),
        };

        // ── 12. 칸·넘침 끝·거의 참 ──

        [Test]
        public void CapacityHardCapAndNearlyFull()
        {
            Assert.AreEqual(20, BagRules.BaseCapacity);
            Assert.AreEqual(20, BagRules.Capacity(0));
            Assert.AreEqual(25, BagRules.Capacity(1));
            Assert.AreEqual(30, BagRules.Capacity(2));
            Assert.AreEqual(30, BagRules.Capacity(3), "늘린 수는 2까지");
            Assert.AreEqual(20, BagRules.Capacity(-1));
            Assert.AreEqual(6, BagRules.OverflowSlots);
            Assert.AreEqual(26, BagRules.HardCap(20));
            Assert.AreEqual(31, BagRules.HardCap(25));
            Assert.AreEqual(36, BagRules.HardCap(30));
            Assert.IsFalse(BagRules.NearlyFull(15, 20));
            Assert.IsTrue(BagRules.NearlyFull(16, 20), "20칸이면 16부터");
            Assert.IsTrue(BagRules.NearlyFull(20, 20));
            Assert.IsTrue(BagRules.NearlyFull(23, 20), "넘쳐도 거의 참");
            Assert.IsFalse(BagRules.NearlyFull(20, 25));
            Assert.IsTrue(BagRules.NearlyFull(21, 25));
            Assert.AreEqual(3, BagRules.AutoReserveSlots);
            Assert.AreEqual(4, BagRules.NearlyFullMargin);
            Assert.AreEqual(15f, BagRules.AutoPickupDistance, "LootDrop.cs의 저절로 줍기 거리와 같음");
            Assert.IsTrue(BagRules.AutoSalvageCommonDefault, "㉡ 기본 켬");
        }

        // ── 13. 5-3 판정 표 ──

        [Test]
        public void ManualPickupTakesUntilFullForEveryGrade()
        {
            foreach (var g in Grades)
            foreach (var auto in Bools)
            foreach (var improves in Bools)
            foreach (var reserve in Reserves)
            {
                string label = $"F {g} ㉡{auto} 나아짐{improves} ㉠{reserve}";
                Assert.AreEqual(PickupAction.Take, D(PickupReason.Manual, g, Room, 0, improves, auto, reserve), label + " 칸 남음");
                Assert.AreEqual(PickupAction.Take, D(PickupReason.Manual, g, Cap - 1, 0, improves, auto, reserve), label + " 19/20(㉠은 F에 없음)");
                Assert.AreEqual(PickupAction.Leave, D(PickupReason.Manual, g, Full, 0, improves, auto, reserve), label + " 가득 → 막힘");
                Assert.AreEqual(PickupAction.Leave, D(PickupReason.Manual, g, OverFull, 0, improves, auto, reserve), label + " 넘침도 가득 → 막힘");
            }
        }

        [Test]
        public void AutoPickupSalvagesUselessCommonsAndLeavesRare()
        {
            foreach (var reserve in Reserves)
            {
                foreach (int count in new[] { Room, Full, OverFull })
                    Assert.AreEqual(PickupAction.Salvage, D(PickupReason.Auto, Grade.Common, count, 0, false, true, reserve),
                        $"㉡ 대상(일반 +0·나아지지 않음·켬) {count}/20 ㉠{reserve} → 분해");

                foreach (var low in LowNotTarget)
                {
                    string label = $"저절로 {low.grade} +{low.enhance} 나아짐{low.improves} ㉡{low.auto} ㉠{reserve}";
                    Assert.AreEqual(PickupAction.Take, D(PickupReason.Auto, low.grade, Room, low.enhance, low.improves, low.auto, reserve), label + " 칸 남음");
                    Assert.AreEqual(reserve == 0 ? PickupAction.Take : PickupAction.Leave,
                        D(PickupReason.Auto, low.grade, Cap - BagRules.AutoReserveSlots, low.enhance, low.improves, low.auto, reserve), label + " 17/20");
                    Assert.AreEqual(PickupAction.Take,
                        D(PickupReason.Auto, low.grade, Cap - BagRules.AutoReserveSlots - 1, low.enhance, low.improves, low.auto, reserve), label + " 16/20");
                    Assert.AreEqual(PickupAction.Leave, D(PickupReason.Auto, low.grade, Full, low.enhance, low.improves, low.auto, reserve), label + " 가득 → 남김");
                    Assert.AreEqual(PickupAction.Leave, D(PickupReason.Auto, low.grade, OverFull, low.enhance, low.improves, low.auto, reserve), label + " 넘침도 가득 → 남김");
                }

                foreach (var g in new[] { Grade.Rare, Grade.Epic, Grade.Legendary })
                foreach (var auto in Bools)
                foreach (int count in new[] { 0, Room, Full, OverFull })
                    Assert.AreEqual(PickupAction.Leave, D(PickupReason.Auto, g, count, 0, false, auto, reserve), $"저절로 {g} {count}/20 → 직접 줍는다");
            }
        }

        [Test]
        public void SweepFollowsLeaveFloorRows()
        {
            foreach (var reserve in Reserves)
            {
                foreach (int count in new[] { Room, Full, OverFull })
                    Assert.AreEqual(PickupAction.Salvage, D(PickupReason.Sweep, Grade.Common, count, 0, false, true, reserve), $"떠날 때 ㉡ 대상 {count}/20 → 분해");

                foreach (var low in LowNotTarget)
                {
                    string label = $"떠날 때 {low.grade} +{low.enhance} 나아짐{low.improves} ㉡{low.auto} ㉠{reserve}";
                    Assert.AreEqual(PickupAction.Take, D(PickupReason.Sweep, low.grade, Room, low.enhance, low.improves, low.auto, reserve), label + " 칸 남음");
                    Assert.AreEqual(PickupAction.Take, D(PickupReason.Sweep, low.grade, Cap - 1, low.enhance, low.improves, low.auto, reserve), label + " 19/20(㉠은 떠날 때 없음)");
                    Assert.AreEqual(PickupAction.Salvage, D(PickupReason.Sweep, low.grade, Full, low.enhance, low.improves, low.auto, reserve), label + " 가득 → 분해");
                    Assert.AreEqual(PickupAction.Salvage, D(PickupReason.Sweep, low.grade, 23, low.enhance, low.improves, low.auto, reserve), label + " 넘침 칸 → 분해");
                    Assert.AreEqual(PickupAction.Salvage, D(PickupReason.Sweep, low.grade, OverFull, low.enhance, low.improves, low.auto, reserve), label + " 넘침도 가득 → 분해");
                }

                foreach (var g in new[] { Grade.Rare, Grade.Epic })
                foreach (var auto in Bools)
                {
                    string label = $"떠날 때 {g} ㉡{auto} ㉠{reserve}";
                    Assert.AreEqual(PickupAction.Take, D(PickupReason.Sweep, g, Room, 0, false, auto, reserve), label + " 칸 남음");
                    Assert.AreEqual(PickupAction.TakeOverflow, D(PickupReason.Sweep, g, Full, 0, false, auto, reserve), label + " 가득 → 넘침 칸");
                    Assert.AreEqual(PickupAction.TakeOverflow, D(PickupReason.Sweep, g, OverFull - 1, 0, false, auto, reserve), label + " 25/20 → 넘침 칸");
                    Assert.AreEqual(PickupAction.Ask, D(PickupReason.Sweep, g, OverFull, 0, false, auto, reserve), label + " 넘침도 가득 → 묻기");
                    Assert.AreEqual(PickupAction.Ask, D(PickupReason.Sweep, g, 30, 0, false, auto, reserve), label + " 넘침을 넘은 가방 → 묻기");
                }

                foreach (var auto in Bools)
                {
                    Assert.AreEqual(PickupAction.Take, D(PickupReason.Sweep, Grade.Legendary, Room, 0, false, auto, reserve), "전설 칸 남음");
                    Assert.AreEqual(PickupAction.TakeOverflow, D(PickupReason.Sweep, Grade.Legendary, Full, 0, false, auto, reserve), "전설 가득 → 넘침 칸");
                    Assert.AreEqual(PickupAction.TakeOverflow, D(PickupReason.Sweep, Grade.Legendary, OverFull, 0, false, auto, reserve), "전설 넘침도 가득 → 늘 얻음");
                    Assert.AreEqual(PickupAction.TakeOverflow, D(PickupReason.Sweep, Grade.Legendary, 40, 0, false, auto, reserve), "전설은 넘침 끝을 넘어도");
                }
            }

            // 25칸 가방: 넘침 끝 31.
            Assert.AreEqual(PickupAction.Take, D(PickupReason.Sweep, Grade.Uncommon, 24, capacity: 25));
            Assert.AreEqual(PickupAction.Salvage, D(PickupReason.Sweep, Grade.Uncommon, 25, capacity: 25));
            Assert.AreEqual(PickupAction.TakeOverflow, D(PickupReason.Sweep, Grade.Rare, 30, capacity: 25));
            Assert.AreEqual(PickupAction.Ask, D(PickupReason.Sweep, Grade.Rare, 31, capacity: 25));
        }

        [Test]
        public void SwappedOutGearOverflowsThenDropsAtFeet()
        {
            foreach (var g in Grades)
            foreach (var auto in Bools)
            foreach (var reserve in Reserves)
            {
                string label = $"벗은 {g} ㉡{auto} ㉠{reserve}";
                Assert.AreEqual(PickupAction.Take, D(PickupReason.Swap, g, Room, 0, false, auto, reserve), label + " 칸 남음(㉡ 대상도 분해하지 않음)");
                Assert.AreEqual(PickupAction.Take, D(PickupReason.Swap, g, Cap - 1, 0, false, auto, reserve), label + " 19/20");
                Assert.AreEqual(PickupAction.TakeOverflow, D(PickupReason.Swap, g, Full, 0, false, auto, reserve), label + " 가득 → 넘침 칸");
                Assert.AreEqual(PickupAction.TakeOverflow, D(PickupReason.Swap, g, OverFull - 1, 0, false, auto, reserve), label + " 25/20 → 넘침 칸");
                Assert.AreEqual(PickupAction.DropAtFeet, D(PickupReason.Swap, g, OverFull, 0, false, auto, reserve), label + " 넘침도 가득 → 발밑");
                Assert.AreEqual(PickupAction.DropAtFeet, D(PickupReason.Swap, g, 30, 0, false, auto, reserve), label + " 넘침을 넘은 가방 → 발밑");
            }
            Assert.AreEqual(PickupAction.TakeOverflow, D(PickupReason.Swap, Grade.Epic, 35, capacity: 30), "30칸 가방 넘침 끝 36");
            Assert.AreEqual(PickupAction.DropAtFeet, D(PickupReason.Swap, Grade.Epic, 36, capacity: 30));
        }
    }
}
