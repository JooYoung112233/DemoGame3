using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>3차 초안 3-4 버팀·무너짐과 M0b '적게·강하게' 수치.</summary>
    public sealed class PoiseRuleTests
    {
        [Test]
        public void PoiseBreaksAtZeroAndGrowsUpToDouble()
        {
            var m = new PoiseMeter(90);
            Assert.IsFalse(m.Apply(36, 0));
            Assert.IsFalse(m.Apply(36, 0.5));
            Assert.IsTrue(m.Apply(36, 1.0));
            Assert.AreEqual(1, m.Breaks);
            Assert.IsFalse(m.Apply(36, 1.2), "무너진 동안에는 다시 무너지지 않는다");

            m.Recover();
            Assert.AreEqual(112.5, m.Max, 1e-9);
            Assert.AreEqual(m.Max, m.Current, 1e-9);
            for (int i = 0; i < 10; i++) m.Recover();
            Assert.AreEqual(180, m.Max, 1e-9);
        }

        [Test]
        public void PoiseRegenWaitsForDelay()
        {
            var m = new PoiseMeter(100);
            m.Apply(50, 0);
            m.Tick(2.0, 0.5);
            Assert.AreEqual(50, m.Current, 1e-9);
            m.Tick(3.0, 1.0);
            Assert.AreEqual(70, m.Current, 1e-9);
            m.Tick(10.0, 5.0);
            Assert.AreEqual(100, m.Current, 1e-9);

            var boss = new PoiseMeter(100, boss: true);
            boss.Apply(50, 0);
            boss.Tick(3.5, 1.0);
            Assert.AreEqual(50, boss.Current, 1e-9);
            boss.Tick(4.5, 1.0);
            Assert.AreEqual(60, boss.Current, 1e-9);
        }

        [Test]
        public void AmbushMultipliesAndCapsAtSeventyPercent()
        {
            Assert.AreEqual(30, PoiseMeter.AmbushDamage(10, true, 90), 1e-9);
            Assert.AreEqual(63, PoiseMeter.AmbushDamage(36, true, 90), 1e-9);
            Assert.AreEqual(60, PoiseMeter.AmbushDamage(40, false, 90), 1e-9);
        }

        [Test]
        public void V3MonsterValues()
        {
            var boar = MonsterRule.Of(MonsterKind.Boar, CombatRuleset.V3);
            Assert.AreEqual(2000, boar.Hp);
            Assert.AreEqual(400, boar.Attack);
            Assert.AreEqual(1600, MonsterRule.Of(MonsterKind.Boar, CombatRuleset.V3, true).Hp);
            var archer = MonsterRule.Of(MonsterKind.Archer, CombatRuleset.V3);
            Assert.AreEqual(600, archer.Hp);
            Assert.AreEqual(150, archer.Attack);
            Assert.AreEqual(MonsterRule.Rat.Hp, MonsterRule.Of(MonsterKind.Rat, CombatRuleset.V3).Hp, "굴쥐는 3차에서도 한 번에 베인다");
            Assert.AreEqual(MonsterRule.Boar.Hp, MonsterRule.Of(MonsterKind.Boar).Hp, "M0a 값은 그대로");

            Assert.AreEqual(90, MonsterRule.PoiseOf(MonsterKind.Boar));
            Assert.AreEqual(70, MonsterRule.PoiseOf(MonsterKind.Boar, true));
            Assert.AreEqual(30, MonsterRule.PoiseOf(MonsterKind.Archer));
        }

        /// <summary>1층 기준 장비, 치명 없이 보통 굴림이면 장검 콤보 한 바퀴로 3차 궁수(600)를 쓰러뜨린다.</summary>
        [Test]
        public void LongswordCycleKillsV3ArcherOnFloorOne()
        {
            var b = FloorScaling.Baseline(1);
            int sum = 0;
            foreach (var s in WeaponPresets.Longsword.combo)
                for (int i = 0; i < s.hits; i++)
                    sum += DamageMath.ToMonster(b.Attack, s.hitPercent, false, 1.5, 1.0);
            Assert.GreaterOrEqual(sum, FloorScaling.MonsterHp(MonsterRule.Of(MonsterKind.Archer, CombatRuleset.V3), 1));
        }

        /// <summary>
        /// 마무리가 한 단계 중 버팀을 가장 많이 깎는다. 대검은 첫 바퀴 마무리에서 멧돼지(90)를 무너뜨리고,
        /// 장검·쌍검은 한 바퀴로는 못 무너뜨린다(무기마다 무너뜨리는 박자가 다르게).
        /// </summary>
        [Test]
        public void FinisherCarriesMostPoiseAndGreatswordBreaksBoarInOneCycle()
        {
            double boarPoise = MonsterRule.PoiseOf(MonsterKind.Boar);
            foreach (var w in WeaponPresets.All)
            {
                double cycle = 0;
                double finisher = 0;
                double other = 0;
                foreach (var s in w.combo)
                {
                    double step = s.hits * s.poiseDamage;
                    Assert.Greater(s.poiseDamage, 0, $"{w.displayName} {s.name}");
                    cycle += step;
                    if (s.finisher) finisher = step;
                    else other = System.Math.Max(other, step);
                }
                Assert.Greater(finisher, other, w.displayName);
                if (w == WeaponPresets.Greatsword) Assert.GreaterOrEqual(cycle, boarPoise, w.displayName);
                else Assert.Less(cycle, boarPoise, w.displayName);
            }
        }
    }
}
