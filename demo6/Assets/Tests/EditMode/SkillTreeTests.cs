using System.Linq;
using Demo6.Core.Dungeon;
using Demo6.Core.Progression;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>스킬 자원(투지)·배우기·트리(기획/스킬-자원-트리-1차.md).</summary>
    public class SkillTreeTests
    {
        static SkillState State(int level, int points)
        {
            var s = new SkillState();
            s.Load(level, points, null, null);
            return s;
        }

        [Test]
        public void NoSkillsAtStartAndLearnOnlyAtTrainer()
        {
            var s = State(1, 0);
            Assert.IsFalse(s.Has(SkillNodeId.LearnWhirl), "처음엔 회오리가 없다");
            Assert.IsFalse(s.Has(SkillNodeId.LearnWave), "처음엔 검풍이 없다");
            Assert.AreEqual("레벨 2부터", SkillTree.Check(s, SkillNodeId.LearnWhirl, true).Reason);

            s = State(2, 1);
            var away = SkillTree.Check(s, SkillNodeId.LearnWhirl, false);
            Assert.IsFalse(away.Ok);
            Assert.IsTrue(away.NeedsTrainer, "배우기 칸은 마을 무진에게서");
            Assert.IsTrue(SkillTree.AnyTrainerNode(s));
            Assert.IsTrue(SkillTree.Take(s, SkillNodeId.LearnWhirl, true));
            Assert.IsTrue(s.Has(SkillNodeId.LearnWhirl));
            Assert.AreEqual(0, s.Points);
            Assert.AreEqual("점수 1점 필요", SkillTree.Check(s, SkillNodeId.LearnWave, true).Reason);
        }

        [Test]
        public void RanksNeedTheSkillAndCanBeRaisedAnywhere()
        {
            var s = State(3, 2);
            Assert.AreEqual("먼저: 회오리 베기", SkillTree.Check(s, SkillNodeId.WideWhirl, false).Reason);
            SkillTree.Take(s, SkillNodeId.LearnWhirl, true);
            Assert.IsTrue(SkillTree.Check(s, SkillNodeId.WideWhirl, false).Ok, "랭크 칸은 K 창 어디서나");
            Assert.IsTrue(SkillTree.Take(s, SkillNodeId.WideWhirl, false));
            Assert.AreEqual(1, s.Rank(SkillId.WideWhirl));
            // 몸 갈래는 배우기 없이 Lv 2부터.
            var body = State(2, 1);
            Assert.IsTrue(SkillTree.Check(body, SkillNodeId.ToughBody, false).Ok);
            Assert.AreEqual("레벨 4부터", SkillTree.Check(body, SkillNodeId.BoilingBlood, false).Reason);
        }

        [Test]
        public void ChoiceNodesAreExclusiveAndNeedLevelFive()
        {
            var s = State(4, 6);
            SkillTree.Take(s, SkillNodeId.LearnWave, true);
            SkillTree.Take(s, SkillNodeId.SharpWind, false);
            Assert.AreEqual("레벨 5부터", SkillTree.Check(s, SkillNodeId.ThreeWave, true).Reason);
            s.Level = 5;
            Assert.IsTrue(SkillTree.Check(s, SkillNodeId.ThreeWave, false).NeedsTrainer);
            Assert.IsTrue(SkillTree.Take(s, SkillNodeId.ThreeWave, true));
            var other = SkillTree.Check(s, SkillNodeId.WallBurst, true);
            Assert.IsFalse(other.Ok);
            StringAssert.Contains("세 갈래 검풍", other.Reason);
        }

        [Test]
        public void TrialPointsReachBothSkillsAndOneChoice()
        {
            // 시험판은 Lv 5~6에서 멈춘다(4~5점): 둘 다 배우고, 한 갈래를 1랭크 올린 뒤 그 갈래의 갈림 하나까지 닿는다.
            var s = State(5, 4);
            Assert.IsTrue(SkillTree.Take(s, SkillNodeId.LearnWhirl, true));
            Assert.IsTrue(SkillTree.Take(s, SkillNodeId.LearnWave, true));
            Assert.IsTrue(SkillTree.Take(s, SkillNodeId.WideWhirl, false));
            Assert.IsTrue(SkillTree.Take(s, SkillNodeId.BloodWhirl, true));
            Assert.AreEqual(0, s.Points);
        }

        [Test]
        public void OldSavesKeepSkillsTheyRanked()
        {
            var c = CarryData.NewProfile(3UL);
            c.Level = 4;
            c.SkillRanks = new[] { 2, 0, 1, 0 };
            var s = new SkillState();
            s.Load(c.Level, c.SkillPoints, c.SkillRanks, c.SkillNodes);
            Assert.IsTrue(s.Has(SkillNodeId.LearnWhirl), "랭크가 있던 회오리는 배운 것으로");
            Assert.IsFalse(s.Has(SkillNodeId.LearnWave));
            Assert.AreEqual(0, s.Rank(SkillId.BoilingBlood), "옛 4칸 저장은 끓는 피 0");
        }

        [Test]
        public void LearnedNodesSurviveSaveText()
        {
            var c = CarryData.NewProfile(9UL);
            Assert.IsFalse(c.ToText().Contains("skillnodes"), "배운 것이 없으면 줄이 없다(예전 글과 같음)");
            c.SkillNodes.Add("node.learn_wave");
            c.SkillNodes.Add("node.three_wave");
            var back = CarryData.FromText(c.ToText());
            CollectionAssert.AreEquivalent(new[] { "node.learn_wave", "node.three_wave" }, back.SkillNodes.ToArray());
        }

        [Test]
        public void NodeTableIsConsistent()
        {
            foreach (var n in SkillTree.Nodes)
            {
                Assert.AreSame(n, SkillTree.FindNode(n.Key), n.Key);
                if (n.Parent.HasValue) Assert.IsNotNull(SkillTree.Node(n.Parent.Value));
                if (n.Exclusive.HasValue) Assert.AreEqual(n.Id, SkillTree.Node(n.Exclusive.Value).Exclusive, n.Key + " 갈림 짝");
                Assert.AreEqual(n.Kind == SkillNodeKind.Rank, n.RankSkill.HasValue, n.Key);
                Assert.AreEqual(n.Kind != SkillNodeKind.Rank, n.Trainer, n.Key + " 배우기·갈림만 무진");
            }
            foreach (var def in SkillTree.All) Assert.IsNotNull(SkillTree.NodeOf(def.Id), def.Key);
        }

        [Test]
        public void SpiritGainsCostsAndDecay()
        {
            Assert.AreEqual(6f, SpiritRules.FromDamage(100, 100), 1e-4, "공격력 100%어치 피해 +6");
            Assert.AreEqual(SpiritRules.HitCap, SpiritRules.FromDamage(10000, 100), 1e-4, "한 번에 +20까지");
            Assert.AreEqual(0f, SpiritRules.FromDamage(50, 0));
            Assert.AreEqual(6.6f, SpiritRules.Scaled(6f, 1), 1e-4, "끓는 피 1랭크 +10%");
            Assert.AreEqual(SpiritRules.Max, SpiritRules.Add(95f, 20f));
            Assert.IsTrue(SpiritRules.CanPay(40f, SpiritRules.WhirlCost));
            Assert.IsFalse(SpiritRules.CanPay(49f, SpiritRules.WaveCost));
            Assert.AreEqual(50f, SpiritRules.Decay(50f, true, 10f, 1f), "싸우는 중에는 그대로");
            Assert.AreEqual(50f, SpiritRules.Decay(50f, false, 2f, 1f), "3초 전에는 그대로");
            Assert.AreEqual(42f, SpiritRules.Decay(50f, false, 4f, 1f), 1e-4, "그 뒤 초당 −8");
            // 첫 싸움: 일반 공격 7번쯤이면 회오리 한 번(40).
            float v = 0f;
            for (int i = 0; i < 7; i++) v = SpiritRules.Add(v, SpiritRules.FromDamage(100, 100));
            Assert.IsTrue(SpiritRules.CanPay(v, SpiritRules.WhirlCost));
        }

        [Test]
        public void FirstRatQuestNeedsNoSkill()
        {
            var q = QuestTable.Get(QuestTable.SmithRats);
            Assert.AreEqual(KillSource.None, q.Steps[0].Skill, "처음엔 스킬이 없으니 어떤 공격이든");
            Assert.IsFalse(q.Steps[0].Hud.Contains("회오리"));
        }

        [Test]
        public void LevelUpLineMentionsTrainerOnlyWhenSomethingToLearn()
        {
            Assert.AreEqual("몸에 힘이 차오른다 — 레벨 2 · 스킬 점수 +1 [K] · 마을 경비 초소에서 기술을 배울 수 있다", SkillTree.LevelUpLine(2, 1, true, "경비 초소에서"));
            Assert.AreEqual("몸에 힘이 차오른다 — 레벨 3 · 스킬 점수 +1 [K]", SkillTree.LevelUpLine(3, 1, false, "무진에게"));
            var c = CarryData.NewProfile(1UL);
            Assert.AreEqual("경비 초소에서", SpeakerIdentity.TeachAt(c), "이름 공개 전에는 자리로");
            SpeakerIdentity.Reveal(c, NpcTable.Trainer);
            Assert.AreEqual("무진에게", SpeakerIdentity.TeachAt(c));
        }
    }
}
