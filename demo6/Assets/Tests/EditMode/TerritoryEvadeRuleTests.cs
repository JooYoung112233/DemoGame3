using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 칸 밖 공격 회피(3차 초안 2-6 '치고 빠지기 꼼수는 막음', TerritoryEvadeRule)와 처형 예외(기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q5).
    /// Enemy.TakeHit이 이 규칙 한 곳을 읽고, Enemy.Execute(기습 처형·무너짐 처형)는 '이미 들어간 한 방의 뒷부분'(followUp)으로 넘긴다.
    /// </summary>
    public sealed class TerritoryEvadeRuleTests
    {
        [Test]
        public void AwakePackEvadesHitsFromOutside()
        {
            Assert.IsTrue(TerritoryEvadeRule.Evades(aware: true, hasTerritory: true, attackerOutside: true, followUp: false),
                "깨어 있는 칸 무리를 경계 밖에서 일반 공격으로 치면 회피");
        }

        [Test]
        public void OtherCasesLandNormally()
        {
            Assert.IsFalse(TerritoryEvadeRule.Evades(false, true, true, false), "쉬는 무리를 밖에서 먼저 치는 기습 첫 타는 들어간다");
            Assert.IsFalse(TerritoryEvadeRule.Evades(true, true, false, false), "칸 안에서 치면 들어간다");
            Assert.IsFalse(TerritoryEvadeRule.Evades(true, false, true, false), "칸이 없는 적(전투 시험장·궤짝 굴쥐)은 피하지 않는다");
        }

        [Test]
        public void FollowUpNeverEvades()
        {
            // 출혈 틱·처형: 깨어 있고 플레이어가 칸 밖이어도 들어간다.
            Assert.IsFalse(TerritoryEvadeRule.Evades(true, true, true, true));
            foreach (bool aware in new[] { false, true })
                foreach (bool territory in new[] { false, true })
                    foreach (bool outside in new[] { false, true })
                        Assert.IsFalse(TerritoryEvadeRule.Evades(aware, territory, outside, true), $"깸={aware} 칸={territory} 밖={outside}");
        }

        [Test]
        public void AmbushExecutionFromOutsideLands()
        {
            // 대검·창·큰 낫으로 칸 경계 밖에서 잠든 궁수를 기습 처형: 첫 타는 쉬는 적이라 들어가고(그 타가 적을 깨움),
            // 이어지는 처형 피해는 같은 한 방의 뒷부분이라 깨어 있어도 '회피'로 빠지지 않는다.
            bool firstHitEvades = TerritoryEvadeRule.Evades(aware: false, hasTerritory: true, attackerOutside: true, followUp: false);
            bool executionEvades = TerritoryEvadeRule.Evades(aware: true, hasTerritory: true, attackerOutside: true, followUp: true);
            Assert.IsFalse(firstHitEvades, "기습 첫 타");
            Assert.IsFalse(executionEvades, "처형(Enemy.Execute)");
            // 처형 뒤에도 칸 밖에서 깨어난 다른 무리를 일반 공격으로 치면 회피는 그대로다.
            Assert.IsTrue(TerritoryEvadeRule.Evades(aware: true, hasTerritory: true, attackerOutside: true, followUp: false), "다음 일반 타");
        }
    }
}
