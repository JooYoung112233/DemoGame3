using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 바닥 함정 수치(기획/1-2층-탐험-맛-1차.md 4-5 낙석·4-6 가시 덫, 10장 시험 10): 낙석 한 방 1층 13~14%·2층 15~16%,
    /// 예고 1.0초 ≥ 예고 규칙 최소, 예고 소리 필요, 가시 5~7%, 웅크리면 낙석이 걸리지 않음, 적 피해 몫 표, 수 함수(첫 방문·다시 연 층·무너짐 +1).
    /// </summary>
    public sealed class TrapRulesTests
    {
        [Test]
        public void RockDamageUsesBoarAttack()
        {
            Assert.AreEqual(400, TrapRules.RockAttack(1), "1층 돌충이 공격");
            Assert.AreEqual(496, TrapRules.RockAttack(2), "2층 돌충이 공격 400 × 1.24");
            Assert.AreEqual(FloorScaling.MonsterAttack(MonsterRule.BoarV3, 2), TrapRules.RockAttack(2));
            Assert.AreEqual(150, TrapRules.SpikeAttack(1), "1층 궁수 공격");
            Assert.AreEqual(186, TrapRules.SpikeAttack(2), "2층 궁수 공격 150 × 1.24");
            Assert.AreEqual(90, TrapRules.RockPercentOfBoar);
            Assert.AreEqual(100, TrapRules.SpikePercentOfArcher);
        }

        [Test]
        public void RockHitShareFromDesign()
        {
            double one = TrapRules.RockHitFraction(1);
            double two = TrapRules.RockHitFraction(2);
            Assert.That(one, Is.InRange(0.13, 0.14), "1층 13.4%");
            Assert.That(two, Is.InRange(0.15, 0.16), "2층 15.1%");
            Assert.AreEqual(TelegraphRule.HitFraction(TrapRules.RockAttack(2), TrapRules.RockPercentOfBoar, 2), two, 1e-12, "셈은 예고 규칙과 같음");
        }

        [Test]
        public void RockWindupMeetsTelegraphRule()
        {
            for (int floor = 1; floor <= 2; floor++)
            {
                double share = TrapRules.RockHitFraction(floor);
                Assert.GreaterOrEqual(TrapRules.RockWindup, TelegraphRule.MinSeconds(share), $"{floor}층 예고 1.0초 ≥ 규칙 최소");
                Assert.AreEqual(TrapRules.RockWindup,
                    TelegraphRule.Seconds(TrapRules.RockWindup, TrapRules.RockAttack(floor), TrapRules.RockPercentOfBoar, floor), 1e-6f, "넉넉히 준 값 그대로");
                Assert.IsTrue(TelegraphRule.NeedsSound(share), $"{floor}층 낙석은 10%를 넘어 예고 소리가 필요");
            }
            Assert.AreEqual(1.0f, TrapRules.RockWindup);
            Assert.AreEqual(1.6f, TrapRules.RockRadius);
            Assert.AreEqual(1.1f, TrapRules.RockTriggerRadius);
            Assert.Less(TrapRules.RockTriggerRadius, TrapRules.RockRadius, "걸린 자리는 떨어지는 원 안");
        }

        [Test]
        public void SpikeHitShareFromDesign()
        {
            for (int floor = 1; floor <= 2; floor++)
            {
                double share = TrapRules.SpikeHitFraction(floor);
                Assert.That(share, Is.InRange(0.05, 0.07), $"{floor}층 가시 덫 5~7%");
                Assert.AreEqual(TelegraphRule.HitFraction(TrapRules.SpikeAttack(floor), TrapRules.SpikePercentOfArcher, floor), share, 1e-12);
            }
            Assert.That(TrapRules.SpikeHitFraction(1), Is.InRange(0.055, 0.057), "1층 5.6%");
            Assert.That(TrapRules.SpikeHitFraction(2), Is.InRange(0.062, 0.064), "2층 6.3%");
        }

        [Test]
        public void CrouchingNeverTriggersRockfall()
        {
            Assert.IsFalse(TrapRules.RockTriggers(true), "웅크려 지나가면 걸리지 않음");
            Assert.IsTrue(TrapRules.RockTriggers(false), "서서 걸으면 걸림");
        }

        [TestCase(MonsterKind.Rat, false, 1.0, false)]
        [TestCase(MonsterKind.Archer, false, 0.4, false)]
        [TestCase(MonsterKind.Boar, false, 0.25, true)]
        [TestCase(MonsterKind.Boar, true, 0.12, false)]
        [TestCase(MonsterKind.Nest, false, 0.0, false)]
        [TestCase(MonsterKind.Ogre, false, 0.0, false)]
        [TestCase(MonsterKind.Nest, true, 0.0, false)]
        [TestCase(MonsterKind.Ogre, true, 0.0, false)]
        public void RockEnemyShareTable(MonsterKind kind, bool elite, double share, bool breaks)
        {
            Assert.AreEqual(share, TrapRules.RockEnemyFraction(kind, elite), 1e-12, $"{kind} 정예 {elite}");
            Assert.AreEqual(breaks, TrapRules.RockBreaks(kind, elite), "정예 아닌 돌충이만 바로 무너짐");
        }

        [Test]
        public void EliteRockAlsoCutsPoise()
        {
            Assert.AreEqual(0.5, TrapRules.RockElitePoiseFraction, 1e-12, "정예 12% + 버팀 50%");
            Assert.AreEqual(0.12, TrapRules.RockEnemyFraction(MonsterKind.Archer, true), 1e-12, "정예는 종류와 상관없이 12%");
        }

        [Test]
        public void RockfallCountFirstVisitUsesRecipe()
        {
            var one = FloorRecipe.For(1);
            var two = FloorRecipe.For(2);
            Assert.AreEqual(0, one.FirstVisitRockfalls, "1층 첫 방문은 낙석 없음");
            Assert.AreEqual(1, two.FirstVisitRockfalls, "2층 첫 방문 가르치는 낙석 1");
            for (int roll = 0; roll < 1000; roll += 111)
            {
                Assert.AreEqual(0, TrapRules.RockfallCount(1, true, NightEvent.None, roll, one));
                Assert.AreEqual(1, TrapRules.RockfallCount(2, true, NightEvent.None, roll, two));
                Assert.AreEqual(1, TrapRules.RockfallCount(2, true, NightEvent.Collapse, roll, two), "첫 방문은 밤 사건을 받지 않음");
            }
            Assert.AreEqual(3, TrapRules.RockfallCount(2, true, NightEvent.None, 0, new FloorRecipe { Floor = 2, FirstVisitRockfalls = 3 }), "표 값 그대로");
            Assert.AreEqual(0, TrapRules.RockfallCount(2, true, NightEvent.None, 0, null), "표가 없으면 0");
        }

        [Test]
        public void RockfallCountReopenedAndCollapse()
        {
            var one = FloorRecipe.For(1);
            var two = FloorRecipe.For(2);
            Assert.AreEqual(1, TrapRules.RockfallCount(1, false, NightEvent.None, 0, one), "1층 다시: 굴림 < 500이면 1");
            Assert.AreEqual(1, TrapRules.RockfallCount(1, false, NightEvent.None, 499, one));
            Assert.AreEqual(0, TrapRules.RockfallCount(1, false, NightEvent.None, 500, one), "1층 다시: 굴림 ≥ 500이면 0");
            Assert.AreEqual(0, TrapRules.RockfallCount(1, false, NightEvent.Upheaval, 999, one), "다른 밤은 그대로");
            for (int roll = 0; roll < 1000; roll += 37)
            {
                Assert.AreEqual(1, TrapRules.RockfallCount(2, false, NightEvent.None, roll, two), "2층 다시 1");
                Assert.AreEqual(2, TrapRules.RockfallCount(2, false, NightEvent.Collapse, roll, two), "무너짐 밤 +1");
                Assert.AreEqual(1, TrapRules.RockfallCount(2, false, NightEvent.Rumble, roll, two), "거센 울림은 낙석을 바꾸지 않음");
            }
            Assert.AreEqual(2, TrapRules.RockfallCount(1, false, NightEvent.Collapse, 0, one), "1층 무너짐 밤 1 + 1");
            Assert.AreEqual(1, TrapRules.RockfallCount(1, false, NightEvent.Collapse, 999, one), "1층 무너짐 밤 0 + 1");
        }

        [Test]
        public void SpikeCountByFloor()
        {
            for (int roll = 0; roll < 1000; roll += 53)
            {
                Assert.AreEqual(0, TrapRules.SpikeCount(1, true, roll), "1층 첫 방문 0");
                Assert.AreEqual(0, TrapRules.SpikeCount(2, true, roll), "2층 첫 방문 0");
                Assert.AreEqual(1, TrapRules.SpikeCount(1, false, roll), "1층 다시 1");
                int two = TrapRules.SpikeCount(2, false, roll);
                Assert.AreEqual(roll < 500 ? 2 : 1, two, "2층 다시 1~2");
            }
        }

        [Test]
        public void PlacementGapsAndNoise()
        {
            Assert.AreEqual(4f, TrapRules.RockMinGroupGap);
            Assert.AreEqual(2.5f, TrapRules.RockMinThingGap);
            Assert.AreEqual(3f, TrapRules.SpikeMinGroupGap);
            Assert.AreEqual(1.5f, TrapRules.SpikeMinThingGap);
            Assert.AreEqual(12f, TrapRules.RockNoiseRadius);
            Assert.AreEqual(8f, TrapRules.SpikeNoiseRadius);
            Assert.AreEqual(1.2f, TrapRules.SpikeDisarmSeconds);
            Assert.AreEqual(1, TrapRules.SpikeStonesPerScene);
            Assert.Greater(TrapRules.RockClueRadius, TrapRules.RockTriggerRadius, "잔돌은 걸림 반경 밖까지 흩어져 미리 보인다");
        }
    }
}
