using Demo6.Core.Dungeon;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>원정의 긴장: 쓰러짐 대가·쉬기·주머니·주먹밥(시스템-컨텐츠-다듬기-검토-1차.md 묶음 5, 결정 D1 '다').</summary>
    public class DownRulesTests
    {
        [Test]
        public void RespawnKeepsPotionsAtLeastOneAndSixtyPercentHp()
        {
            Assert.AreEqual(1, DownRules.RespawnPotions(0, 3), "0병이면 1병");
            Assert.AreEqual(2, DownRules.RespawnPotions(2, 3), "남은 수 그대로");
            Assert.AreEqual(3, DownRules.RespawnPotions(5, 3), "칸 수로 자름");
            Assert.AreEqual(1440, DownRules.RespawnHp(2400));
            Assert.AreEqual(1, DownRules.RespawnHp(1));
        }

        [Test]
        public void PouchTakesTwentyPercentOfThisTripOnly()
        {
            Assert.AreEqual(4, DownRules.PouchAmount(20, 100), "이번 원정에 번 20의 20%");
            Assert.AreEqual(0, DownRules.PouchAmount(0, 100), "번 것이 없으면 떨구지 않는다");
            Assert.AreEqual(3, DownRules.PouchAmount(20, 3), "가진 것보다 많지 않게");
            Assert.AreEqual(0, DownRules.PouchAmount(20, 100, 0f), "손잡이 0%");
            Assert.AreEqual(1, DownRules.PouchAmount(3, 10), "0.6 → 1(반올림)");
            Assert.AreEqual(0, DownRules.Excess(4, 10), "이 층에서 번 것 안이면 넘는 몫 없음");
            Assert.AreEqual(3, DownRules.Excess(4, 1));
            Assert.AreEqual(4, DownRules.Excess(4, -2), "이 층에서 쓴 것은 번 것 0으로");
            StringAssert.Contains("강화석 4 · 골드 12", DownRules.PouchDropLine(4, 12));
            Assert.AreEqual("피 묻은 주머니를 되찾았다 — 골드 5", DownRules.PouchTakeLine(0, 5));
        }

        [Test]
        public void LegKeepsRestRiceAndDownsAcrossFloors()
        {
            var c = CarryData.NewProfile(4UL);
            c.Leg = new ExpeditionLeg { Rested = true, RiceBalls = 1, Downs = 2 };
            var back = CarryData.FromText(c.ToText());
            Assert.IsTrue(back.Leg.Rested);
            Assert.AreEqual(1, back.Leg.RiceBalls);
            Assert.AreEqual(2, back.Leg.Downs);
            var clone = c.Leg.Clone();
            Assert.IsTrue(clone.Rested);
            Assert.AreEqual(1, clone.RiceBalls);

            c.Leg = new ExpeditionLeg();
            Assert.IsFalse(c.ToText().Contains("leg.rested"), "쉬지 않았으면 줄이 없다");
        }

        [Test]
        public void OilIsCountedPerExpedition()
        {
            Assert.AreEqual(2, new ExpeditionLeg().Oil, "원정을 기름 병 2개로 시작");
            Assert.AreEqual(DownRules.OilCap, DownRules.AddOil(3, 5), "상한");
            Assert.AreEqual(0, DownRules.AddOil(0, -1));
            var c = CarryData.NewProfile(5UL);
            c.Leg = new ExpeditionLeg { Oil = 1 };
            Assert.AreEqual(1, CarryData.FromText(c.ToText()).Leg.Oil, "계단으로 내려가도 남은 병 그대로");
            Assert.AreEqual(1, c.Leg.Clone().Oil);
        }

        [Test]
        public void ClueNeedsThreeLitLamps()
        {
            Assert.IsFalse(DownRules.CluesShown(2, 5));
            Assert.IsTrue(DownRules.CluesShown(3, 5), "켠 등잔 3개");
            Assert.IsTrue(DownRules.CluesShown(2, 2), "층 등잔이 셋보다 적으면 모두");
            Assert.IsFalse(DownRules.CluesShown(0, 0), "등잔 없는 층");
        }
    }
}
