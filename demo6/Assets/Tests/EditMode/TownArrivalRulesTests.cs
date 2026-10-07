using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 기획/마을-의뢰-첫판.md 9-3 TownArrivalRulesTests(1-4 단계 4): 명패 새로 맡김 수, 처음 맡길 때만 rx:tag.f1,
    /// 두 번 처리해도 결과가 같음, 지급이 일어나지 않음.
    /// </summary>
    public sealed class TownArrivalRulesTests
    {
        const string Nameplate = "f1.T.nameplate";

        static CarryData Returned()
        {
            var c = CarryData.NewProfile(0x77UL);
            TownArrivalRules.Apply(c, TownArrivalKind.NewPlay);
            TalkDirector.Skip(TownScript.Opening, 0, c);
            TownNight.AdvanceForAscend(c);
            return c;
        }

        [Test]
        public void NewPlayArrival()
        {
            var c = CarryData.NewProfile(1UL);
            var r = TownArrivalRules.Apply(c, TownArrivalKind.NewPlay);
            Assert.AreEqual(1, r.Visits);
            Assert.IsTrue(r.FirstVisit);
            Assert.IsFalse(r.FirstReturn);
            Assert.IsTrue(r.OpeningPending);
            Assert.AreEqual(0, r.NewTags);
            Assert.AreEqual(12, r.LitLamps);
            CollectionAssert.AreEquivalent(new[] { QuestTable.GateDescend, QuestTable.SmithRats }, r.Opened, "오프닝 의뢰가 받을 수 있음");
            var again = TownArrivalRules.Apply(c, TownArrivalKind.NewPlay);
            Assert.IsTrue(again.Repeated);
            Assert.AreEqual(1, again.Visits, "같은 도착은 다시 세지 않음");
        }

        [Test]
        public void NameplateDepositAndFirstTagReaction()
        {
            var c = Returned();
            Assert.AreEqual(0, TownArrivalRules.NameplatesHeld(c));
            c.OnceDone.Add(Nameplate);
            c.OnceDone.Add("f1.H.pickaxe");
            Assert.AreEqual(1, TownArrivalRules.NameplatesHeld(c), "명패만 셈");
            var r = TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.AreEqual(1, r.NewTags);
            Assert.AreEqual(1, r.Tags);
            Assert.AreEqual(11, r.LitLamps);
            Assert.IsTrue(r.FirstTag);
            Assert.IsTrue(TownSave.HasReaction(c, TownSave.RxTagF1), "처음 맡기면 rx:tag.f1");
            Assert.IsTrue(r.FirstReturn);
            Assert.AreEqual(2, r.Visits);
            CollectionAssert.Contains(TownArrivalRules.CardLines(c, r), "광부 명패를 권양기 틀에 걸었다. 등불 하나가 꺼졌다. (남은 등불 11)");

            // 반응을 대화에서 써서 지운 뒤 다시 처리해도 명패를 또 맡기거나 반응을 다시 적지 않는다.
            TalkDirector.End(TownScript.ReactionScene(TownSave.RxTagF1), c);
            Assert.IsFalse(TownSave.HasReaction(c, TownSave.RxTagF1));
            var again = TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.AreEqual(0, again.NewTags);
            Assert.AreEqual(1, again.Tags);
            Assert.IsFalse(again.FirstTag);
            Assert.IsFalse(TownSave.HasReaction(c, TownSave.RxTagF1));
            Assert.IsNull(TownScript.NameplateLine(again.NewTags, again.LitLamps));

            // 다음 귀환에도 같은 명패는 다시 맡기지 않는다.
            TownNight.AdvanceForAscend(c);
            var next = TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.AreEqual(0, next.NewTags);
            Assert.IsFalse(next.FirstReturn);
            Assert.AreEqual(3, next.Visits);
        }

        [Test]
        public void ApplyTwiceGivesSameResult()
        {
            var c = Returned();
            c.OnceDone.Add(Nameplate);
            new QuestBook(c).Handle(QuestEvent.FloorEntered(1));
            new QuestBook(c).Handle(QuestEvent.Ascended());
            var first = TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            string after = c.ToText();
            var second = TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.AreEqual(after, c.ToText(), "두 번 처리해도 꾸러미가 같음");
            Assert.IsFalse(first.Repeated);
            Assert.IsTrue(second.Repeated);
            Assert.AreEqual(first.Visits, second.Visits);
            Assert.AreEqual(first.Tags, second.Tags);
            Assert.AreEqual(first.LitLamps, second.LitLamps);
        }

        [Test]
        public void ArrivalDoesNotPayRewards()
        {
            var c = Returned();
            var book = new QuestBook(c);
            book.Handle(QuestEvent.FloorEntered(1));
            book.Handle(QuestEvent.Ascended());
            book.ForceState(QuestTable.SmithRats, QuestState.Achieved);
            int stones = c.Stones, gold = c.Gold, xp = c.TotalXp, level = c.Level, sp = c.SkillPoints;
            int expedition = c.Expedition;
            var night = c.Night;
            var r = TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.AreEqual(stones, c.Stones);
            Assert.AreEqual(gold, c.Gold);
            Assert.AreEqual(xp, c.TotalXp);
            Assert.AreEqual(level, c.Level);
            Assert.AreEqual(sp, c.SkillPoints);
            Assert.AreEqual(QuestState.Achieved, book.State(QuestTable.GateDescend), "보고 전까지 달성 그대로");
            Assert.AreEqual(QuestState.Achieved, book.State(QuestTable.SmithRats));
            Assert.AreEqual(expedition, c.Expedition, "원정 번호를 바꾸지 않음");
            Assert.AreEqual(night, c.Night, "밤 사건을 바꾸지 않음");
            Assert.IsEmpty(r.Opened, "보고 전에는 다음 의뢰가 열리지 않음");
            var lines = TownArrivalRules.CardLines(c, r);
            Assert.AreEqual("지갑 강화석 0 · 골드 0", lines[0]);
            CollectionAssert.Contains(lines, "알릴 일: 갱도로 내려가기 — 춘삼에게 · 굴쥐 쫓기 — 옥금에게");
        }

        [Test]
        public void ActiveLineShowsDepth()
        {
            var c = Returned();
            var book = new QuestBook(c);
            book.ForceState(QuestTable.GateFloor2, QuestState.Active);
            c.DeepestFloor = 1;
            var lines = TownArrivalRules.CardLines(c, TownArrivalRules.Apply(c, TownArrivalKind.Basket));
            CollectionAssert.Contains(lines, "진행 중: 갱도로 내려가기 · 굴쥐 쫓기 (0/8) · 버팀목 길 끝까지 (지금 1층까지)");
        }

        [Test]
        public void DungeonFirstPathNeedsOpening()
        {
            // DungeonTest를 바로 Play해서 올라온 시험 경로(1-6): 오프닝을 못 봄.
            var c = CarryData.NewProfile(9UL);
            TownNight.AdvanceForAscend(c);
            var r = TownArrivalRules.Apply(c, TownArrivalKind.Basket);
            Assert.AreEqual(1, r.Visits);
            Assert.IsTrue(r.FirstVisit);
            Assert.IsTrue(r.OpeningPending);
            Assert.IsTrue(r.FirstReturn);
            Assert.IsTrue(TalkDirector.Build(NpcTable.Gate, c).IsOpening, "도착 카드 뒤 오프닝");
        }

        [Test]
        public void DownedArrivalHasNoLossYet()
        {
            var c = Returned();
            c.Stones = 20;
            c.Gold = 300;
            var r = TownArrivalRules.Apply(c, TownArrivalKind.Downed);
            Assert.AreEqual(TownArrivalKind.Downed, r.Kind);
            Assert.AreEqual(20, c.Stones);
            Assert.AreEqual(300, c.Gold);
        }
    }
}
