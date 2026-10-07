using System;
using Demo6.Core.Combat;
using Demo6.Core.Stats;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 등 각도 판정(전투·보스·무기 다듬기 1차 2-3 단검 등 찌르기, 4-2 기습 처형): 적이 보는 방향의 뒤쪽 120°(±60°).
    /// 단검 등 찌르기는 치명 확률 +400‰를 상한 500‰ 밖에서 더하고 1000에서 자른다. 기습 처형과 같은 각도 판정을 쓴다.
    /// </summary>
    public sealed class BackstabRuleTests
    {
        /// <summary>적이 +x를 볼 때 (적 → 공격자) 방향이 보는 방향과 이루는 각(도).</summary>
        static bool BehindAt(float degreesFromFacing)
        {
            double r = degreesFromFacing * Math.PI / 180.0;
            return BackstabRule.IsBehind(1f, 0f, (float)Math.Cos(r), (float)Math.Sin(r));
        }

        [Test]
        public void BehindIsRearOneHundredTwentyDegrees()
        {
            Assert.AreEqual(60f, BackstabRule.HalfAngle);
            Assert.IsTrue(BehindAt(180f), "바로 뒤");
            Assert.IsTrue(BehindAt(150f));
            Assert.IsTrue(BehindAt(-150f));
            Assert.IsTrue(BehindAt(120.5f), "±60° 경계 안");
            Assert.IsTrue(BehindAt(-120.5f), "반대쪽 경계 안");
            Assert.IsFalse(BehindAt(119.5f), "±60° 경계 밖");
            Assert.IsFalse(BehindAt(-119.5f));
            Assert.IsFalse(BehindAt(90f), "옆");
            Assert.IsFalse(BehindAt(0f), "정면");
        }

        [Test]
        public void LengthDoesNotMatterAndZeroVectorIsNotBehind()
        {
            Assert.IsTrue(BackstabRule.IsBehind(0f, 5f, 0f, -0.01f), "길이와 상관없음");
            Assert.IsTrue(BackstabRule.IsBehind(0.2f, 0.2f, -30f, -30f));
            Assert.IsFalse(BackstabRule.IsBehind(1f, 0f, 0f, 0f), "공격자가 같은 자리면 등 뒤가 아님");
            Assert.IsFalse(BackstabRule.IsBehind(0f, 0f, -1f, 0f), "보는 방향이 없으면 등 뒤가 아님");
        }

        [Test]
        public void CritBonusAddsOutsideCapAndClampsAtThousand()
        {
            Assert.AreEqual(400, BackstabRule.CritBonusPermille);
            Assert.AreEqual(500, StatCaps.CritChancePermille);
            // 시트 값은 상한(500)이 적용된 값이다. 등 뒤 보너스는 그 밖에서 더한다.
            Assert.AreEqual(900, BackstabRule.CritChancePermille(StatCaps.CritChancePermille, true), "시트 500 + 400");
            Assert.AreEqual(490, BackstabRule.CritChancePermille(90, true), "단검 시작 9% + 40%");
            Assert.AreEqual(1000, BackstabRule.CritChancePermille(800, true), "1000에서 자름");
            Assert.AreEqual(400, BackstabRule.CritChancePermille(-30, true), "음수 시트는 0으로 보고 더함");
            Assert.AreEqual(90, BackstabRule.CritChancePermille(90, false), "등 뒤가 아니면 그대로");
            Assert.AreEqual(500, BackstabRule.CritChancePermille(500, false));
        }
    }
}
