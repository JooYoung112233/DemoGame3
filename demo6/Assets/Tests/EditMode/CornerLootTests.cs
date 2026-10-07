using System.Collections.Generic;
using System.Linq;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 구석 보상과 탐험 글 모음(기획/1-2층-탐험-맛-1차.md 4-7·4-9·9장, 10장 시험 12·13).
    /// 품삯 궤짝: 골드 = 나무 궤짝 골드 × 2, 쥐 궤짝 없음, 장비·강화석은 같은 씨앗 나무 궤짝과 같음.
    /// 유품 상자: 강화석 1 이상, 장비 비율 1,000번 굴림에서 25~35%. 글 모음: '멧돼지'와 주민 이름이 없다.
    /// </summary>
    public sealed class CornerLootTests
    {
        [Test]
        public void NumbersFromDesign()
        {
            Assert.AreEqual("wage", CornerLoot.WageParam);
            Assert.AreEqual("keepsake", CornerLoot.KeepsakeParam);
            Assert.AreEqual("buried", CornerLoot.BuriedParam);
            Assert.AreEqual(2, CornerLoot.WageGoldFactor);
            Assert.AreEqual(1, CornerLoot.WageHealOrbs);
            Assert.AreEqual(300, CornerLoot.KeepsakeGearPermille);
            Assert.AreEqual(1, CornerLoot.KeepsakeStones);
            Assert.AreEqual(1, CornerLoot.SurveyOverflowStones);
            Assert.AreEqual(1, CornerLoot.SpikeStones);
            Assert.AreEqual(2f, CornerLoot.BuriedDigSeconds);
            Assert.AreEqual(3f, CornerLoot.BuriedDigCrouchSeconds);
            Assert.AreEqual(12f, CornerLoot.BuriedNoiseRadius);
        }

        [Test]
        public void CornerParams()
        {
            Assert.IsTrue(CornerLoot.IsCornerParam(CornerLoot.WageParam));
            Assert.IsTrue(CornerLoot.IsCornerParam(CornerLoot.KeepsakeParam));
            Assert.IsTrue(CornerLoot.IsCornerParam(CornerLoot.BuriedParam));
            Assert.IsFalse(CornerLoot.IsCornerParam(""));
            Assert.IsFalse(CornerLoot.IsCornerParam(null));
            Assert.IsFalse(CornerLoot.IsCornerParam(LootRules.RareWeaponParam), "보장 상자 표시와 겹치지 않음");
            Assert.IsFalse(CornerLoot.IsCornerParam(LootRules.EpicParam));
        }

        [TestCase(1, 12)]
        [TestCase(2, 16)]
        public void WageIsWoodChestWithDoubleGold(int floor, int gold)
        {
            Assert.AreEqual(gold, LootRules.WoodGold(floor) * CornerLoot.WageGoldFactor, "1층 12, 2층 16");
            int woodRats = 0;
            for (ulong seed = 1; seed <= 600; seed++)
            {
                var wage = CornerLoot.RollWage(floor, new Pcg32Random(seed, 9), null);
                var wood = LootRules.RollChest(false, floor, "", new Pcg32Random(seed, 9), null);
                if (wood.RatChest) woodRats++;
                Assert.IsFalse(wage.RatChest, "쥐 궤짝 없음");
                Assert.AreEqual(gold, wage.Gold, "골드 = 나무 궤짝 골드 × 2");
                Assert.AreEqual(wood.Gold * CornerLoot.WageGoldFactor, wage.Gold);
                Assert.AreEqual(wood.Stones, wage.Stones, "강화석 굴림은 나무 궤짝과 같음");
                Assert.AreEqual(wood.Gear.Count, wage.Gear.Count, "장비 굴림은 나무 궤짝과 같음");
                for (int i = 0; i < wood.Gear.Count; i++)
                    Assert.AreEqual(wood.Gear[i].ToString(), wage.Gear[i].ToString(), "같은 씨앗이면 같은 장비");
                Assert.LessOrEqual(wage.GoldPiles.Count, LootRules.MaxGoldPiles);
                Assert.IsTrue(wage.GoldPiles.All(g => g > 0));
            }
            Assert.Greater(woodRats, 0, "같은 씨앗 나무 궤짝에는 쥐 궤짝이 있었다(지운 것을 확인)");
        }

        [TestCase(1)]
        [TestCase(2)]
        public void KeepsakeRollsGearMoreAndAlwaysStone(int floor)
        {
            const int rolls = 1000;
            int gear = 0, extra = 0;
            for (ulong seed = 1; seed <= rolls; seed++)
            {
                var b = CornerLoot.RollKeepsake(floor, new Pcg32Random(seed, 11), null);
                Assert.IsFalse(b.RatChest, "쥐 궤짝 없음");
                Assert.GreaterOrEqual(b.Stones, CornerLoot.KeepsakeStones, "강화석 1 이상");
                Assert.LessOrEqual(b.Stones, CornerLoot.KeepsakeStones + 1, "나무 궤짝 몫으로 1 더까지");
                Assert.AreEqual(LootRules.WoodGold(floor), b.Gold, "골드는 나무 궤짝과 같음");
                Assert.LessOrEqual(b.Gear.Count, 1);
                if (b.Gear.Count > 0)
                {
                    gear++;
                    Assert.AreEqual(floor, b.Gear[0].ItemLevel, "아이템 레벨 = 층");
                }
                if (b.Stones > CornerLoot.KeepsakeStones) extra++;
            }
            Assert.That(gear, Is.InRange(250, 350), "장비 30%(1,000번 굴림 25~35%)");
            Assert.That(extra, Is.InRange(190, 310), "강화석 하나 더 25%");
        }

        [Test]
        public void KeepsakeRandomOrderIsFixed()
        {
            // 난수 차례: 장비(‰ < 300) → 강화석 더(‰ < 250) → 장비면 RollGear.
            for (ulong seed = 1; seed <= 300; seed++)
            {
                var b = CornerLoot.RollKeepsake(2, new Pcg32Random(seed, 13), null);
                var rng = new Pcg32Random(seed, 13);
                bool gear = rng.NextInt(0, 1000) < CornerLoot.KeepsakeGearPermille;
                bool extra = rng.NextInt(0, 1000) < LootRules.WoodStonePermille;
                Assert.AreEqual(gear ? 1 : 0, b.Gear.Count, "씨앗 " + seed);
                Assert.AreEqual(CornerLoot.KeepsakeStones + (extra ? 1 : 0), b.Stones, "씨앗 " + seed);
                if (gear) Assert.AreEqual(LootRules.RollGear(2, rng, Grade.Common, null).ToString(), b.Gear[0].ToString(), "씨앗 " + seed);
            }
        }

        [Test]
        public void KeepsakeLineIsStableAndCoversAll()
        {
            Assert.AreEqual(5, ExploreText.KeepsakeLines.Count);
            var seen = new HashSet<string>();
            for (ulong seed = 0; seed < 200; seed++)
            {
                string line = ExploreText.KeepsakeLine(seed);
                Assert.AreEqual(line, ExploreText.KeepsakeLine(seed), "같은 씨앗이면 같은 글");
                CollectionAssert.Contains(ExploreText.KeepsakeLines.ToArray(), line);
                seen.Add(line);
            }
            Assert.AreEqual(5, seen.Count, "다섯 글이 다 나온다");
            Assert.AreEqual(ExploreText.KeepsakeLine(LootRules.ChestSeed(3, "f2.H.wood")), ExploreText.KeepsakeLine(LootRules.ChestSeed(3, "f2.H.wood")));
        }

        [Test]
        public void ExploreTextHasNoBoarWordOrResidentNames()
        {
            var names = NpcTable.All.Select(n => n.DisplayName).Where(n => !string.IsNullOrEmpty(n)).ToArray();
            Assert.Greater(names.Length, 0);
            int count = 0;
            foreach (var t in ExploreText.AllTexts)
            {
                count++;
                Assert.IsFalse(string.IsNullOrWhiteSpace(t), "빈 글");
                StringAssert.DoesNotContain("멧돼지", t, "사람에게 보이는 글은 '돌충이': " + t);
                foreach (var n in names) StringAssert.DoesNotContain(n, t, "탐험 글에 주민 이름: " + t);
                StringAssert.DoesNotContain("[F]", t, "새 안내 글에 키 이름을 넣지 않음: " + t);
            }
            Assert.Greater(count, 50, "글을 모두 모았다");
        }

        [Test]
        public void ExploreTextLinesFromDesign()
        {
            Assert.AreEqual("문틈 너머 — 조용하다", ExploreText.ListenQuiet);
            Assert.AreEqual("머리 위에서 돌이 쏟아진다", ExploreText.RockfallFirst);
            Assert.AreEqual("낙석!", ExploreText.RockfallEnemy);
            Assert.AreEqual("가시 덫 — 쇳소리가 울린다", ExploreText.SpikeStepped);
            Assert.AreEqual("가시 덫 걷어 내기", ExploreText.SpikePrompt);
            Assert.AreEqual("웅크려야 걷어 낼 수 있다", ExploreText.SpikeNeedCrouch);
            Assert.AreEqual("가시 덫을 걷어 냈다 — 쓸 만한 쇠붙이 · 강화석 +1", ExploreText.SpikeDisarmedStone);
            Assert.AreEqual("가시 덫을 걷어 냈다", ExploreText.SpikeDisarmed);
            Assert.AreEqual("광부 품삯 궤짝 열기", ExploreText.WagePrompt);
            Assert.AreEqual("광부 유품 상자 열기", ExploreText.KeepsakePrompt);
            Assert.AreEqual("흙 묻은 쇠 궤짝 파내기", ExploreText.BuriedPrompt);
            Assert.AreEqual("꼬깃한 품삯 봉투 — 끝내 받아 가지 못한 돈이다", ExploreText.WageOpened);
            Assert.AreEqual("흙더미가 무너지는 소리가 갱도에 울린다", ExploreText.BuriedDug);
            Assert.AreEqual("측량 — 도면은 이미 다 그렸다. 버려진 측량 못을 챙겼다 · 강화석 +1", ExploreText.SurveyOverflow);
            Assert.AreEqual("계단 아래에서 돌 씹는 소리가 올라온다", ExploreText.DenRumbleFirst);
            Assert.AreEqual("땅 밑에서 큰 것이 몸을 뒤척였다.", TownNight.RumbleLine);
            CollectionAssert.Contains(ExploreText.AllTexts.ToArray(), TownNight.RumbleLine);
        }

        [Test]
        public void PackNamesMatchDesignTable()
        {
            // 무리 후보 표(PackTable)의 이름은 문서 9장 글 그대로(ExploreText에 모은 글과 같음).
            string[] floorOne =
            {
                ExploreText.PackRatSwarm, ExploreText.PackSleepingBoar, ExploreText.PackEatingBoar,
                ExploreText.PackRatPatrol, ExploreText.PackTwoBoars, ExploreText.PackBoarPatrol,
            };
            string[] floorTwo =
            {
                ExploreText.PackEatingBoarArcher, ExploreText.PackSleepingBoarArcher, ExploreText.PackTwoArchers,
                ExploreText.PackBoarPatrol, ExploreText.PackArcherPatrol, ExploreText.PackRatSwarm,
            };
            CollectionAssert.AreEqual(floorOne, PackTable.Options(1).Select(o => o.Label).ToArray(), "1층 후보 표 차례·이름");
            CollectionAssert.AreEqual(floorTwo, PackTable.Options(2).Select(o => o.Label).ToArray(), "2층 후보 표 차례·이름");
            Assert.AreEqual(ExploreText.PackElite, PackTable.EliteOption(1).Label);
            Assert.AreEqual(ExploreText.PackElite, PackTable.EliteOption(2).Label);
            CollectionAssert.AreEqual(new[] { ExploreText.PackArcherRat, ExploreText.PackBoarArcher, ExploreText.PackElite },
                FloorRecipe.For(2).Groups.Select(g => g.Label).ToArray(), "2층 층 예산 무리(D4)");
        }
    }
}
