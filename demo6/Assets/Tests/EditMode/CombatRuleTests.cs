using Demo6.Core.Combat;
using Demo6.Core.Random;
using Demo6.Core.Time;
using NUnit.Framework;

namespace Demo6.Tests
{
    public sealed class CombatRuleTests
    {
        [Test]
        public void DamageIgnoresZeroDefenseAndHalvesAtThousand()
        {
            Assert.AreEqual(200, DamageMath.ToMonster(200, 100, false, 1.5, 1.0, 0));
            Assert.AreEqual(100, DamageMath.ToMonster(200, 100, false, 1.5, 1.0, 1000));
            Assert.AreEqual(300, DamageMath.ToMonster(200, 100, true, 1.5, 1.0, 0));
            Assert.AreEqual(1, DamageMath.ToMonster(1, 10, false, 1.5, 0.92, 4000));
        }

        [Test]
        public void DamageRoundsHalfUp()
        {
            Assert.AreEqual(3, DamageMath.RoundHalfUp(2.5));
            Assert.AreEqual(313, FloorScaling.MonsterHp(MonsterKind.Rat, 4)); // 160 × 1.25³ = 312.5
        }

        [Test]
        public void PlayerDamageUsesDefense()
        {
            Assert.AreEqual(30, DamageMath.ToPlayer(60, 100, 1.0, 1000));
            Assert.AreEqual(180, DamageMath.ToPlayer(300, 60, 1.0, 0));
        }

        [Test]
        public void RollStaysInRange()
        {
            var rng = new Pcg32Random(1);
            for (int i = 0; i < 10000; i++)
            {
                double r = DamageMath.Roll(rng);
                Assert.That(r, Is.InRange(DamageMath.RollMin, DamageMath.RollMax));
            }
        }

        /// <summary>기획 3-2: 콤보 한 바퀴의 단일 대상 계수는 1.50 / 1.45 / 1.60 근처, 1.45~1.60 안.</summary>
        [TestCase("wpn_longsword", 1.50)]
        [TestCase("wpn_greatsword", 1.45)]
        [TestCase("wpn_twinblades", 1.60)]
        public void WeaponSingleTargetCoefficients(string id, double expected)
        {
            foreach (var w in WeaponPresets.All)
                if (w.id == id)
                {
                    Assert.AreEqual(expected, w.SingleTargetCoefficient, 0.015);
                    Assert.That(w.SingleTargetCoefficient, Is.InRange(1.45, 1.60));
                    return;
                }
            Assert.Fail(id);
        }

        [Test]
        public void EveryComboEndsWithOneFinisherAndHitsFitInStep()
        {
            foreach (var w in WeaponPresets.All)
            {
                Assert.GreaterOrEqual(w.combo.Length, 3, w.displayName);
                for (int i = 0; i < w.combo.Length; i++)
                {
                    var s = w.combo[i];
                    Assert.AreEqual(i == w.combo.Length - 1, s.finisher, $"{w.displayName} {i + 1}단계");
                    float lastHit = s.duration * s.hitMoment + (s.hits - 1) * s.hitInterval;
                    Assert.Less(lastHit, s.duration, $"{w.displayName} {s.name} 마지막 타가 동작 안에 들어가야 함");
                }
            }
        }

        [Test]
        public void FloorTableMatchesPlan()
        {
            Assert.AreEqual(1192, FloorScaling.MonsterHp(MonsterKind.Rat, 10));
            Assert.AreEqual(416, FloorScaling.MonsterAttack(MonsterKind.Rat, 10));
            Assert.AreEqual(7735, FloorScaling.MonsterHp(MonsterKind.Boar, 10));
            Assert.AreEqual(2079, FloorScaling.MonsterAttack(MonsterKind.Boar, 10));
            Assert.AreEqual(3438, FloorScaling.MonsterHp(MonsterKind.Archer, 10));
            Assert.AreEqual(693, FloorScaling.MonsterAttack(MonsterKind.Archer, 10));
            Assert.AreEqual(1143, FloorScaling.MonsterHp(MonsterKind.Boar, 2));
        }

        /// <summary>기획 4-8: 기준 장비면 세 무기 모두 전 층에서 굴쥐를 한 번 휘둘러 잡는다(치명 없음, 가장 낮은 굴림).</summary>
        [Test]
        public void BaselineGearOneShotsRatsOnEveryFloor()
        {
            for (int floor = FloorScaling.MinFloor; floor <= FloorScaling.MaxFloor; floor++)
            {
                var b = FloorScaling.Baseline(floor);
                int ratHp = FloorScaling.MonsterHp(MonsterKind.Rat, floor);
                foreach (var w in WeaponPresets.All)
                foreach (var s in w.combo)
                {
                    int swing = 0;
                    for (int i = 0; i < s.hits; i++)
                        swing += DamageMath.ToMonster(b.Attack, s.hitPercent, false, 1.5, DamageMath.RollMin);
                    Assert.GreaterOrEqual(swing, ratHp, $"{floor}층 {w.displayName} {s.name}");
                }
            }
        }

        [Test]
        public void HitStopOnlyExtendsWhenLonger()
        {
            var t = new TimeScaleArbiter();
            Assert.AreEqual(0.04, t.RequestHitStop(0.04), 1e-9);
            Assert.AreEqual(0.0, t.RequestHitStop(0.03), 1e-9);
            Assert.AreEqual(0.02, t.RequestHitStop(0.06), 1e-9);
            Assert.AreEqual(0.0, t.Effective);
            t.Advance(0.06);
            Assert.AreEqual(1.0, t.Effective);
        }

        [Test]
        public void HitStopBudgetIsPointTwoPerSecond()
        {
            var t = new TimeScaleArbiter();
            double total = 0;
            for (int i = 0; i < 10; i++)
            {
                total += t.RequestHitStop(0.06);
                t.Advance(0.07);
            }
            Assert.AreEqual(0.2, total, 1e-9);
            t.Advance(1.0);
            Assert.AreEqual(0.06, t.RequestHitStop(0.06), 1e-9);
        }

        [Test]
        public void PauseBeatsHitStopBeatsSlowMotion()
        {
            var t = new TimeScaleArbiter();
            t.RequestSlowMotion(1.0, 0.3);
            Assert.AreEqual(0.3, t.Effective, 1e-9);
            t.RequestHitStop(0.05);
            Assert.AreEqual(0.0, t.Effective, 1e-9);
            t.Advance(0.05);
            Assert.AreEqual(0.3, t.Effective, 1e-9);
            t.Paused = true;
            Assert.AreEqual(0.0, t.Effective, 1e-9);
            t.Advance(5.0);
            t.Paused = false;
            Assert.AreEqual(0.3, t.Effective, 1e-9, "멈춘 동안에는 느린 화면 시간이 흐르지 않는다");
            t.Advance(1.0);
            Assert.AreEqual(1.0, t.Effective, 1e-9);
        }
    }
}
