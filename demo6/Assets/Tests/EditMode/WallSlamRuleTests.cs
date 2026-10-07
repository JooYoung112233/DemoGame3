using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>벽·기둥 박기 공용 표(기획/전투-보스-무기-다듬기-1차.md 4-2 [1], WallSlamRule).</summary>
    public sealed class WallSlamRuleTests
    {
        static TargetClass Rat => new TargetClass(MonsterKind.Rat, WeightClass.Light, false, false, false, false);
        static TargetClass Archer => new TargetClass(MonsterKind.Archer, WeightClass.Medium, false, false, false, false);
        static TargetClass Boar => new TargetClass(MonsterKind.Boar, WeightClass.Heavy, false, false, false, false);
        /// <summary>정예는 무게가 늘 무거움이다(Enemy.MakeElite).</summary>
        static TargetClass EliteBoar => new TargetClass(MonsterKind.Boar, WeightClass.Heavy, true, false, false, false);
        static TargetClass Ogre => new TargetClass(MonsterKind.Ogre, WeightClass.Heavy, false, false, true, false);
        static TargetClass Nest => new TargetClass(MonsterKind.Nest, WeightClass.Heavy, false, false, false, false);
        static TargetClass RatDummy => new TargetClass(MonsterKind.Rat, WeightClass.Light, false, false, false, true);
        static TargetClass WoodDummy => new TargetClass(MonsterKind.Boar, WeightClass.Heavy, false, false, false, true);

        [Test]
        public void WeightTableMatchesDocument()
        {
            var rat = WallSlamRule.Resolve(Rat, false);
            Assert.AreEqual(WallSlamKind.ExtraDamage, rat.Kind);
            Assert.AreEqual(0.5f, rat.ExtraDamageFraction, 1e-6f, "굴쥐: 그 타 피해 50% 추가");
            Assert.IsTrue(rat.BloodMark, "굴쥐: 벽 피 자국");

            var archer = WallSlamRule.Resolve(Archer, false);
            Assert.AreEqual(WallSlamKind.Break, archer.Kind, "궁수: 바로 무너짐");

            var boar = WallSlamRule.Resolve(Boar, false);
            Assert.AreEqual(WallSlamKind.PoiseHit, boar.Kind);
            Assert.AreEqual(0.35f, boar.PoiseFractionOfMax, 1e-6f);
            Assert.AreEqual(0.15f, boar.FlinchSeconds, 1e-6f, "멧돼지: 0.15초 움찔");
            // 멧돼지 버팀 90의 35% = 31.5(문서 표).
            Assert.AreEqual(31.5, MonsterRule.PoiseOf(MonsterKind.Boar) * boar.PoiseFractionOfMax, 1e-4);
        }

        [Test]
        public void EliteIsHalfOfHeavy()
        {
            var elite = WallSlamRule.Resolve(EliteBoar, false);
            Assert.AreEqual(WallSlamKind.PoiseHit, elite.Kind);
            Assert.AreEqual(WallSlamRule.HeavyPoiseFraction * WallSlamRule.EliteFactor, elite.PoiseFractionOfMax, 1e-6f);
            Assert.AreEqual(0.175f, elite.PoiseFractionOfMax, 1e-6f);
            Assert.AreEqual(0.15f, elite.FlinchSeconds, 1e-6f);
        }

        [Test]
        public void ImmuneTargetsGetNothing()
        {
            foreach (var t in new[] { Ogre, Nest, RatDummy, WoodDummy })
                foreach (bool wallBreak in new[] { false, true })
                {
                    var r = WallSlamRule.Resolve(t, wallBreak);
                    Assert.AreEqual(WallSlamKind.None, r.Kind, t.Kind + " wallBreak=" + wallBreak);
                    Assert.IsFalse(r.BloodMark);
                }
        }

        [Test]
        public void WallBreakRaisesOnlyPlainHeavy()
        {
            // 쇠망치 땅 울리기: 무거움만 한 칸 위(바로 무너짐).
            Assert.AreEqual(WallSlamKind.Break, WallSlamRule.Resolve(Boar, true).Kind);
            // 가벼움·보통은 같은 결과.
            Assert.AreEqual(WallSlamKind.ExtraDamage, WallSlamRule.Resolve(Rat, true).Kind);
            Assert.AreEqual(0.5f, WallSlamRule.Resolve(Rat, true).ExtraDamageFraction, 1e-6f);
            Assert.AreEqual(WallSlamKind.Break, WallSlamRule.Resolve(Archer, true).Kind);
            // 정예는 올리지 않는다(× 0.5 버팀 그대로).
            var elite = WallSlamRule.Resolve(EliteBoar, true);
            Assert.AreEqual(WallSlamKind.PoiseHit, elite.Kind);
            Assert.AreEqual(0.175f, elite.PoiseFractionOfMax, 1e-6f);
        }

        [Test]
        public void PushedEnoughAtHalfUnit()
        {
            Assert.IsTrue(WallSlamRule.PushedEnough(0.5f), "0.5 경계는 박기");
            Assert.IsFalse(WallSlamRule.PushedEnough(0.49f));
            Assert.IsFalse(WallSlamRule.PushedEnough(0f));

            // 멧돼지(넉백 저항 0.5): 대검 내려찍기 1.6 → 0.8, 장검 찌르기 1.4 → 0.7은 박기, 쌍검 회전베기 0.9 → 0.45는 아님(문서 4-2 [1]).
            float resist = MonsterRule.Boar.KnockbackResist;
            Assert.AreEqual(0.5f, resist, 1e-6f);
            var greatsword = WeaponPresets.Greatsword.combo;
            var longsword = WeaponPresets.Longsword.combo;
            var twin = WeaponPresets.Twinblades.combo;
            Assert.IsTrue(WallSlamRule.PushedEnough(greatsword[2].knockback * (1f - resist)), "대검 내려찍기");
            Assert.IsTrue(WallSlamRule.PushedEnough(longsword[2].knockback * (1f - resist)), "장검 찌르기");
            Assert.IsFalse(WallSlamRule.PushedEnough(twin[twin.Length - 1].knockback * (1f - resist)), "쌍검 회전베기");
            // 굴쥐(저항 0): 대검 1·2단계 0.8도 박기.
            Assert.IsTrue(WallSlamRule.PushedEnough(greatsword[0].knockback * (1f - MonsterRule.Rat.KnockbackResist)), "대검 큰 베기 → 굴쥐");
        }

        [Test]
        public void SameEnemyOncePerHalfSecond()
        {
            Assert.IsTrue(WallSlamRule.Ready(double.NegativeInfinity, 0.0), "처음은 바로");
            Assert.IsFalse(WallSlamRule.Ready(1.0, 1.0));
            Assert.IsFalse(WallSlamRule.Ready(1.0, 1.49));
            Assert.IsTrue(WallSlamRule.Ready(1.0, 1.5), "0.5초 경계는 다시 됨");
            Assert.IsTrue(WallSlamRule.Ready(1.0, 2.0));
        }

        [Test]
        public void FeedbackConstantsMatchDocument()
        {
            Assert.AreEqual(0.5f, WallSlamRule.MinPush, 1e-6f);
            Assert.AreEqual(0.5f, WallSlamRule.Cooldown, 1e-6f);
            Assert.AreEqual(0.05f, WallSlamRule.HitStop, 1e-6f);
            Assert.AreEqual(6, WallSlamRule.DustCount);
            Assert.AreEqual(0.7f, WallSlamRule.ThudPitch, 1e-6f);
            Assert.IsTrue(WallSlamRule.DefaultOn);
        }
    }
}
