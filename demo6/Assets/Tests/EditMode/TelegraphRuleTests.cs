using System;
using Demo6.Core.Combat;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 3차 예고 규칙을 일반 적도 함께 쓴다(기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q6, TelegraphRule): 칸 경계값, 보스 셈과 같음,
    /// 2층 멧돼지(돌진 16.8% → 0.8초, 머리치기·뒷발 10.1% → 0.6초 + 소리), 정예(바탕 + 0.1초에 맞댐), 8층 다시 조준, 궁수 꿰뚫는 화살·가시 덫.
    /// 바탕 예고 값(멧돼지 0.7·0.4·다시 조준 0.4, 꿰뚫는 화살 1.4, 덫 0.6, 정예 +0.1)은 BoarBrain·ArcherBrain·SpikeTrap 상수와 같다.
    /// </summary>
    public sealed class TelegraphRuleTests
    {
        const float BoarCharge = 0.7f;
        const float BoarMelee = 0.4f;
        const float BoarReaim = 0.4f;
        const float EliteBonus = 0.1f;
        const float PierceAim = 1.4f;
        const float TrapArm = 0.6f;

        static int BoarAttack(int floor) => FloorScaling.MonsterAttack(MonsterRule.BoarV3, floor);
        static int ArcherAttack(int floor) => FloorScaling.MonsterAttack(MonsterRule.ArcherV3, floor);
        /// <summary>정예 공격 = 공격 × 1.18 반올림(Enemy.EliteAttackScale, Enemy.MakeElite의 Mathf.RoundToInt와 같은 float 셈).</summary>
        static int Elite(int attack) => (int)Math.Round(attack * 1.18f);

        [Test]
        public void BandsMatchThirdDraftTable()
        {
            Assert.AreEqual(0.25f, TelegraphRule.MinSeconds(0.0), 1e-6f);
            Assert.AreEqual(0.25f, TelegraphRule.MinSeconds(0.05), 1e-6f, "5% 정확히는 아래 칸");
            Assert.AreEqual(0.4f, TelegraphRule.MinSeconds(0.0501), 1e-6f);
            Assert.AreEqual(0.4f, TelegraphRule.MinSeconds(0.10), 1e-6f, "10% 정확히는 아래 칸");
            Assert.AreEqual(0.6f, TelegraphRule.MinSeconds(0.1001), 1e-6f);
            Assert.AreEqual(0.6f, TelegraphRule.MinSeconds(0.16), 1e-6f);
            Assert.AreEqual(0.8f, TelegraphRule.MinSeconds(0.1601), 1e-6f);
            Assert.AreEqual(0.8f, TelegraphRule.MinSeconds(0.20), 1e-6f);
            Assert.AreEqual(0.9f, TelegraphRule.MinSeconds(0.2001), 1e-6f);
            Assert.IsFalse(TelegraphRule.NeedsSound(0.10), "소리는 10%를 넘을 때부터");
            Assert.IsTrue(TelegraphRule.NeedsSound(0.1001));
            Assert.IsTrue(TelegraphRule.NeedsSound(0.25), "16% 넘는 칸도 소리");
        }

        [Test]
        public void BossUsesSameCalculation()
        {
            foreach (int floor in new[] { 2, 5, 10 })
                foreach (BossPattern p in Enum.GetValues(typeof(BossPattern)))
                {
                    double moved = TelegraphRule.HitFraction(BossRules.Attack(floor), BossRules.Percent(p), floor);
                    Assert.AreEqual(moved, BossRules.HitFraction(p, floor), 1e-12, $"{floor}층 {p}");
                    Assert.AreEqual(TelegraphRule.MinSeconds(moved), BossRules.MinTelegraph(moved), 1e-6f, $"{floor}층 {p}");
                }
        }

        [Test]
        public void Floor2BoarWasShortAndIsRaised()
        {
            int attack = BoarAttack(2);
            Assert.AreEqual(496, attack, "400 × 1.24");
            // 문서 Q6: 돌진 약 443 = 2,640의 16.8%, 머리치기·뒷발 약 266 = 10.1%.
            Assert.AreEqual(0.168, TelegraphRule.HitFraction(attack, 100, 2), 0.001);
            Assert.AreEqual(0.101, TelegraphRule.HitFraction(attack, 60, 2), 0.001);
            Assert.Greater(TelegraphRule.MinSeconds(TelegraphRule.HitFraction(attack, 100, 2)), BoarCharge, "바탕 0.7초는 규칙 미달이었다");
            Assert.AreEqual(0.8f, TelegraphRule.Seconds(BoarCharge, attack, 100, 2), 1e-6f, "돌진 0.7 → 0.8");
            Assert.AreEqual(0.6f, TelegraphRule.Seconds(BoarMelee, attack, 60, 2), 1e-6f, "머리치기·뒷발 0.4 → 0.6");
            Assert.IsTrue(TelegraphRule.NeedsSound(attack, 100, 2), "돌진 소리");
            Assert.IsTrue(TelegraphRule.NeedsSound(attack, 60, 2), "머리치기·뒷발 소리");
        }

        [Test]
        public void Floor1BoarKeepsDocValues()
        {
            int attack = BoarAttack(1);
            Assert.AreEqual(0.7f, TelegraphRule.Seconds(BoarCharge, attack, 100, 1), 1e-6f, "1층 돌진 14.9%는 규칙 안");
            Assert.AreEqual(0.4f, TelegraphRule.Seconds(BoarMelee, attack, 60, 1), 1e-6f, "1층 머리치기 8.9%는 규칙 안");
            Assert.IsTrue(TelegraphRule.NeedsSound(attack, 100, 1), "돌진은 1층도 10% 넘음");
            Assert.IsFalse(TelegraphRule.NeedsSound(attack, 60, 1), "1층 머리치기는 소리 없음");
        }

        [Test]
        public void BoarChargeAndMeleeMeetRuleOnEveryFloor()
        {
            for (int floor = FloorScaling.MinFloor; floor <= FloorScaling.MaxFloor; floor++)
            {
                foreach (int attack in new[] { BoarAttack(floor), Elite(BoarAttack(floor)) })
                {
                    bool elite = attack != BoarAttack(floor);
                    float bonus = elite ? EliteBonus : 0f;
                    foreach (var (baseTime, percent) in new[] { (BoarCharge, 100.0), (BoarMelee, 60.0), (BoarReaim, 100.0) })
                    {
                        float used = TelegraphRule.Seconds(baseTime + bonus, attack, percent, floor);
                        double fraction = TelegraphRule.HitFraction(attack, percent, floor);
                        Assert.GreaterOrEqual(used, TelegraphRule.MinSeconds(fraction), $"{floor}층 정예={elite} 바탕 {baseTime} {percent}%");
                        Assert.GreaterOrEqual(used, baseTime + bonus - 1e-6f, "바탕값보다 짧아지지 않는다");
                    }
                }
                Assert.IsTrue(TelegraphRule.NeedsSound(BoarAttack(floor), 100, floor), $"{floor}층 돌진은 늘 10% 넘음");
            }
        }

        [Test]
        public void EliteBoarUsesBonusFirst()
        {
            // 문서 3-3 표: 정예 돌진(×1.18) 17.6% → 0.8초 = 바탕 0.7 + 정예 0.1이 규칙을 채운다(더 얹지 않음).
            int elite1 = Elite(BoarAttack(1));
            Assert.AreEqual(472, elite1);
            Assert.AreEqual(0.176, TelegraphRule.HitFraction(elite1, 100, 1), 0.001);
            Assert.AreEqual(0.8f, TelegraphRule.Seconds(BoarCharge + EliteBonus, elite1, 100, 1), 1e-6f);
            // 2층 정예 돌진 19.8% → 0.8초(정예 공격 ×1.18로 일반·정예 상한 20% 안, 1-2층 탐험 맛 1차 4-1. ×1.2였을 때는 20.1%·0.9초).
            int elite2 = Elite(BoarAttack(2));
            Assert.AreEqual(585, elite2);
            Assert.LessOrEqual(TelegraphRule.HitFraction(elite2, 100, 2), 0.20);
            Assert.AreEqual(0.8f, TelegraphRule.Seconds(BoarCharge + EliteBonus, elite2, 100, 2), 1e-6f);
            Assert.AreEqual(0.6f, TelegraphRule.Seconds(BoarMelee + EliteBonus, elite2, 60, 2), 1e-6f, "정예 머리치기 11.9% → 0.5 → 0.6");
        }

        [Test]
        public void Floor8ReaimIsRaised()
        {
            int attack = BoarAttack(8);
            Assert.AreEqual(0.134, TelegraphRule.HitFraction(attack, 100, 8), 0.001);
            Assert.AreEqual(0.6f, TelegraphRule.Seconds(BoarReaim, attack, 100, 8), 1e-6f, "8층 다시 조준 0.4 → 0.6");
            Assert.AreEqual(0.6f, TelegraphRule.Seconds(BoarReaim + EliteBonus, Elite(attack), 100, 8), 1e-6f, "8층 정예 다시 조준 15.8% → 0.6(×1.2였을 때 16.05% → 0.8)");
        }

        [Test]
        public void ArcherPierceAndTrapKeepTimes()
        {
            for (int floor = FloorScaling.MinFloor; floor <= FloorScaling.MaxFloor; floor++)
            {
                foreach (int attack in new[] { ArcherAttack(floor), Elite(ArcherAttack(floor)) })
                {
                    Assert.AreEqual(PierceAim, TelegraphRule.Seconds(PierceAim, attack, 200, floor), 1e-6f, $"{floor}층 꿰뚫는 화살 1.4초 그대로");
                    Assert.AreEqual(TrapArm, TelegraphRule.Seconds(TrapArm, attack, 100, floor), 1e-6f, $"{floor}층 덫 0.6초 그대로");
                    Assert.IsFalse(TelegraphRule.NeedsSound(attack, 100, floor), $"{floor}층 덫은 10% 아래");
                }
            }
            // 꿰뚫는 화살 소리: 문서 1층 11.2%, 2층 12.6%. 7층은 9.8%라 소리 없음.
            Assert.AreEqual(0.112, TelegraphRule.HitFraction(ArcherAttack(1), 200, 1), 0.001);
            Assert.AreEqual(0.126, TelegraphRule.HitFraction(ArcherAttack(2), 200, 2), 0.001);
            Assert.IsTrue(TelegraphRule.NeedsSound(ArcherAttack(2), 200, 2));
            Assert.IsFalse(TelegraphRule.NeedsSound(ArcherAttack(7), 200, 7));
            Assert.AreEqual(0.063, TelegraphRule.HitFraction(ArcherAttack(2), 100, 2), 0.001, "2층 덫 6.3%");
        }
    }
}
