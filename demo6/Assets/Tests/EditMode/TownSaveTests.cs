using System.Linq;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 기획/마을-의뢰-첫판.md 9-3 TownSaveTests(7장 저장): 의뢰 상태·이름·장면·이정표가 꾸러미 글 왕복을 거쳐도 남고,
    /// 첫 줄은 그대로 'carry v2'(판본을 올리지 않음). 기존 키와 겹치지 않음.
    /// </summary>
    public sealed class TownSaveTests
    {
        static CarryData Filled()
        {
            var c = CarryData.NewProfile(0xABCDEFUL);
            c.Counters["landing_line"] = 2;
            c.Counters["map:first"] = 1;
            var book = new QuestBook(c);
            TalkDirector.Skip(TownScript.Opening, 0, c);
            book.Handle(QuestEvent.FloorEntered(1));
            for (int i = 0; i < 3; i++) book.Handle(QuestEvent.Killed(MonsterKind.Rat, KillSource.Whirlwind));
            book.Handle(QuestEvent.StakeLit(2, true));
            TownSave.NoteDowned(c, true, OgreLossHint.Slam);
            TownSave.BumpRepeat(c, NpcTable.Smith);
            TownSave.BumpVisits(c);
            TownSave.BumpDepartures(c);
            TownSave.SetTags(c, 1);
            TownSave.SetLastArrival(c, 1);
            return c;
        }

        [Test]
        public void RoundTripKeepsTownState()
        {
            var c = Filled();
            string text = c.ToText();
            StringAssert.StartsWith("carry v2\n", text, "판본을 올리지 않음");
            Assert.AreEqual(2, CarryData.CurrentVersion);
            var d = CarryData.FromText(text);
            Assert.AreEqual(text, d.ToText(), "왕복해도 같은 글");
            var a = new QuestBook(c);
            var b = new QuestBook(d);
            foreach (var q in QuestTable.All)
            {
                Assert.AreEqual(a.State(q.Id), b.State(q.Id), q.Id + " 상태");
                Assert.AreEqual(a.Step(q.Id), b.Step(q.Id), q.Id + " 단계");
                Assert.AreEqual(a.Count(q.Id), b.Count(q.Id), q.Id + " 센 수");
                Assert.AreEqual(a.Order(q.Id), b.Order(q.Id), q.Id + " 받은 차례");
            }
            Assert.AreEqual(QuestState.Active, b.State(QuestTable.SmithRats));
            Assert.AreEqual(3, b.Count(QuestTable.SmithRats));
            Assert.AreEqual(1, b.Step(QuestTable.GateDescend));
            foreach (var n in NpcTable.All) Assert.AreEqual(SpeakerIdentity.Knows(c, n.Id), SpeakerIdentity.Knows(d, n.Id), n.Id);
            Assert.IsTrue(SpeakerIdentity.Knows(d, NpcTable.Gate));
            Assert.IsFalse(SpeakerIdentity.Knows(d, NpcTable.Trainer));
            Assert.IsTrue(TownSave.SeenScene(d, TownScript.OpeningId));
            Assert.IsTrue(TownSave.HasMilestone(d, TownSave.MilestoneStairsF2));
            Assert.IsTrue(TownSave.HasReaction(d, TownSave.RxDowned));
            Assert.IsTrue(TownSave.HasReaction(d, TownSave.RxOgreLost));
            Assert.AreEqual(OgreLossHint.Slam, TownSave.OgreLossHint(d));
            Assert.AreEqual(1, TownSave.RepeatIndex(d, NpcTable.Smith));
            Assert.AreEqual(1, TownSave.Visits(d));
            Assert.AreEqual(1, TownSave.Departures(d));
            Assert.AreEqual(1, TownSave.Tags(d));
            Assert.AreEqual(1, TownSave.LastArrival(d));
            Assert.AreEqual(2, d.Count("landing_line"), "기존 키 그대로");
            Assert.AreEqual(1, d.Count("map:first"));
        }

        [Test]
        public void KeysUseDocumentedNames()
        {
            var c = Filled();
            Assert.AreEqual((int)QuestState.Active, c.Count("q.st:" + QuestTable.SmithRats));
            Assert.AreEqual(1, c.Count("q.sp:" + QuestTable.GateDescend));
            Assert.AreEqual(3, c.Count("q.n:" + QuestTable.SmithRats));
            Assert.AreEqual(1, c.Count("nm:" + NpcTable.Gate));
            Assert.AreEqual(1, c.Count("sc:scene.opening"));
            Assert.AreEqual(1, c.Count("ms:stairs.f2"));
            Assert.AreEqual(1, c.Count("rx:downed"));
            Assert.AreEqual(1, c.Count("rx:ogre_lost"));
            Assert.AreEqual((int)OgreLossHint.Slam, c.Count("rx:ogre_lost.hint"));
            Assert.AreEqual(1, c.Count("rp:" + NpcTable.Smith));
            Assert.AreEqual(1, c.Count("town.visits"));
            Assert.AreEqual(1, c.Count("town.departures"));
            Assert.AreEqual(1, c.Count("town.tags"));
            StringAssert.Contains("q.st%3Aq.gate_descend:2", c.ToText(), "':'는 이스케이프");
        }

        [Test]
        public void KeysDoNotCollideWithExisting()
        {
            foreach (var existing in new[] { "landing_line", "map:first" })
                Assert.IsFalse(TownSave.IsTownKey(existing), existing);
            var c = Filled();
            foreach (var k in c.Counters.Keys.Where(k => k != "landing_line" && k != "map:first"))
                Assert.IsTrue(TownSave.IsTownKey(k), "머리말 없는 키: " + k);
            var prefixes = TownSave.Prefixes;
            CollectionAssert.AllItemsAreUnique(prefixes);
            foreach (var p in prefixes)
                foreach (var o in prefixes)
                    if (p != o) Assert.IsFalse(o.StartsWith(p) && p.Length < o.Length && !p.EndsWith("."), $"{p}가 {o}를 덮음");
        }

        [Test]
        public void DefaultsRemoveKeys()
        {
            var c = CarryData.NewProfile(1UL);
            TownSave.SetQuestState(c, QuestTable.GateDescend, QuestState.Offered);
            TownSave.SetQuestState(c, QuestTable.GateDescend, QuestState.Locked);
            TownSave.SetReaction(c, TownSave.RxTagF1);
            TownSave.ClearReaction(c, TownSave.RxTagF1);
            TownSave.SetTags(c, 0);
            Assert.IsEmpty(c.Counters, "기본값이면 키가 없음");
            Assert.AreEqual(QuestState.Locked, TownSave.GetQuestState(c, "q.unknown"));
            c.Counters["q.st:" + QuestTable.GateDescend] = 99;
            Assert.AreEqual(QuestState.Locked, TownSave.GetQuestState(c, QuestTable.GateDescend), "읽지 못한 값은 Locked");
        }

        [Test]
        public void NamesNeverGoBackInPlay()
        {
            var c = CarryData.NewProfile(2UL);
            Assert.IsTrue(TownSave.SetNameKnown(c, NpcTable.Smith));
            Assert.IsFalse(TownSave.SetNameKnown(c, NpcTable.Smith));
            Assert.IsTrue(TownSave.KnowsName(c, NpcTable.Smith));
            TownSave.ForgetNameForTest(c, NpcTable.Smith);
            Assert.IsFalse(TownSave.KnowsName(c, NpcTable.Smith), "시험 패널만 지울 수 있음");
        }

        [Test]
        public void ClearStoryAndClearAll()
        {
            var c = Filled();
            TownSave.ClearStory(c);
            Assert.IsFalse(c.Counters.Keys.Any(k => TownSave.IsTownKey(k) && !k.StartsWith("town.")), "의뢰·이름·장면 지움");
            Assert.AreEqual(1, TownSave.Visits(c), "마을 세기는 남음");
            Assert.AreEqual(2, c.Count("landing_line"));
            TownSave.ClearAll(c);
            Assert.IsFalse(c.Counters.Keys.Any(TownSave.IsTownKey));
            Assert.AreEqual(2, c.Count("landing_line"), "기존 키는 남음");
        }

        [Test]
        public void OgreLossHintWrittenWithReaction()
        {
            var c = CarryData.NewProfile(4UL);
            TownSave.NoteDowned(c, true);
            Assert.IsTrue(TownSave.HasReaction(c, TownSave.RxOgreLost));
            Assert.AreEqual(OgreLossHint.None, TownSave.OgreLossHint(c), "옛 호출(2인자)은 힌트 없음");
            Assert.IsFalse(c.Counters.ContainsKey("rx:ogre_lost.hint"), "None이면 키가 없음");

            TownSave.NoteDowned(c, true, OgreLossHint.Charge);
            Assert.AreEqual(OgreLossHint.Charge, TownSave.OgreLossHint(c));
            Assert.AreEqual((int)OgreLossHint.Charge, c.Count("rx:ogre_lost.hint"), "키는 rx:{반응}.hint");
            Assert.IsTrue(TownSave.IsTownKey(TownSave.ReactionHintKey(TownSave.RxOgreLost)));

            var d = CarryData.FromText(c.ToText());
            Assert.AreEqual(OgreLossHint.Charge, TownSave.OgreLossHint(d), "꾸러미 글 왕복");
            StringAssert.StartsWith("carry v2\n", c.ToText());

            TownSave.NoteDowned(c, false, OgreLossHint.Slam);
            Assert.AreEqual(OgreLossHint.Charge, TownSave.OgreLossHint(c), "보스 없이 쓰러지면 남은 힌트 그대로");
            TownSave.NoteDowned(c, true, OgreLossHint.Slam);
            Assert.AreEqual(OgreLossHint.Slam, TownSave.OgreLossHint(c), "보스 앞 패배마다 새로 적음");
            TownSave.NoteDowned(c, true, OgreLossHint.None);
            Assert.AreEqual(OgreLossHint.None, TownSave.OgreLossHint(c), "힌트 없는 패배는 지난 힌트를 지움");

            TownSave.NoteDowned(c, true, OgreLossHint.Slam);
            TownSave.ClearReaction(c, TownSave.RxOgreLost);
            Assert.IsFalse(TownSave.HasReaction(c, TownSave.RxOgreLost));
            Assert.AreEqual(OgreLossHint.None, TownSave.OgreLossHint(c), "반응을 지우면 힌트도");
            Assert.IsFalse(c.Counters.ContainsKey("rx:ogre_lost.hint"));
            Assert.IsTrue(TownSave.HasReaction(c, TownSave.RxDowned), "쓰러짐 반응은 따로");

            TownSave.NoteDowned(c, true, OgreLossHint.Charge);
            TownSave.ClearStory(c);
            Assert.AreEqual(OgreLossHint.None, TownSave.OgreLossHint(c), "의뢰·이름·장면 지우기에 함께 지워짐");
            c.Counters["rx:ogre_lost.hint"] = 9;
            Assert.AreEqual(OgreLossHint.None, TownSave.OgreLossHint(c), "읽지 못한 값은 None");
        }

        /// <summary>보스를 쓰러뜨리면 지난 오우거 패배 반응·힌트가 지워져, 무진과의 다음 대화가 패배 공략 줄 없이 보고 장면부터 시작한다(마을 문서 5-5).</summary>
        [Test]
        public void BossKillClearsOgreLoss()
        {
            var c = CarryData.NewProfile(5UL);
            new QuestBook(c).ForceState(QuestTable.TrainerOgre, QuestState.Active); // 반응은 '굴의 큰 놈'을 받은 뒤에 꺼낸다(검토 1차 Q2)
            TownSave.NoteDowned(c, true, OgreLossHint.Slam);
            Assert.IsNotNull(TalkDirector.PendingReaction(NpcTable.Trainer, c), "진 뒤에는 무진 반응이 있음");

            TownSave.NoteBossKilled(c);
            Assert.IsFalse(TownSave.HasReaction(c, TownSave.RxOgreLost));
            Assert.AreEqual(OgreLossHint.None, TownSave.OgreLossHint(c));
            Assert.IsFalse(c.Counters.ContainsKey("rx:ogre_lost.hint"));
            Assert.IsTrue(TownSave.HasReaction(c, TownSave.RxDowned), "쓰러짐 반응(춘삼)은 그대로");
            Assert.IsNull(TalkDirector.PendingReaction(NpcTable.Trainer, c), "이긴 뒤 무진 첫 줄은 패배 공략이 아님");

            TownSave.NoteBossKilled(c);
            Assert.IsFalse(TownSave.HasReaction(c, TownSave.RxOgreLost), "두 번 불러도 같음");
            TownSave.NoteDowned(c, true, OgreLossHint.Charge);
            Assert.AreEqual(OgreLossHint.Charge, TownSave.OgreLossHint(c), "그 뒤에 다시 지면 새로 적음");
        }

        [Test]
        public void LossHintFromMostHitPattern()
        {
            Assert.AreEqual(OgreLossHint.Charge, TownSave.LossHintFor(BossPattern.Charge));
            Assert.AreEqual(OgreLossHint.Slam, TownSave.LossHintFor(BossPattern.Slam));
            Assert.AreEqual(OgreLossHint.None, TownSave.LossHintFor(BossPattern.Sweep));
            Assert.AreEqual(OgreLossHint.None, TownSave.LossHintFor(BossPattern.Roar));
            Assert.AreEqual(OgreLossHint.None, TownSave.LossHintFor(null), "맞은 적 없음");
        }

        [Test]
        public void QuestOrderStamps()
        {
            var c = CarryData.NewProfile(3UL);
            Assert.AreEqual(1, TownSave.StampQuestOrder(c, QuestTable.GateDescend));
            Assert.AreEqual(2, TownSave.StampQuestOrder(c, QuestTable.SmithRats));
            TownSave.ClearQuest(c, QuestTable.GateDescend);
            Assert.AreEqual(0, TownSave.GetQuestOrder(c, QuestTable.GateDescend));
            Assert.AreEqual(3, TownSave.StampQuestOrder(c, QuestTable.GateFloor2));
        }
    }
}
