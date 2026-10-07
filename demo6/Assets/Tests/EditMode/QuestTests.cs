using System.Collections.Generic;
using System.Linq;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Progression;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 기획/마을-의뢰-첫판.md 9-3 QuestTests: 보상 표, 표 검사, 상태 기계, 처치 필터·도움 2초, 단계, 받기 전 기록, 경험치·레벨, 이름, 머리 위 표시.
    /// 사용자 결정: 오우거 의뢰는 오우거 굴과 함께 연다(굴 없음 가정 default ctx면 4개, 2층 계단 아래 굴이 있는 지금 던전 Live ctx면 5개),
    /// 첫 의뢰는 아무 층에 내려섰다가 바구니로 올라오면 끝.
    /// </summary>
    public sealed class QuestTests
    {
        static CarryData NewCarry() => CarryData.NewProfile(0x1234UL);

        /// <summary>첫 판 의뢰 다섯(오우거까지). 묶음 3 나-10 새 의뢰는 이 뒤에 열린다.</summary>
        static readonly string[] CoreFive =
            { QuestTable.GateDescend, QuestTable.SmithRats, QuestTable.GateFloor2, QuestTable.TrainerWindblade, QuestTable.TrainerOgre };

        /// <summary>오프닝 의뢰 둘을 받은 꾸러미.</summary>
        static (CarryData carry, QuestBook book) AfterOpening()
        {
            var c = NewCarry();
            TalkDirector.Skip(TownScript.Opening, 0, c);
            return (c, new QuestBook(c));
        }

        static QuestEvent Kill(MonsterKind m, KillSource s, bool noReward = false, KillSource assist = KillSource.None) =>
            QuestEvent.Killed(m, s, noReward, false, assist);

        // ── 1. 보상 ──

        [TestCase(QuestTable.GateDescend, 3, 30, 40)]
        [TestCase(QuestTable.SmithRats, 3, 30, 40)]
        [TestCase(QuestTable.GateFloor2, 4, 40, 48)]
        [TestCase(QuestTable.TrainerWindblade, 4, 40, 48)]
        [TestCase(QuestTable.TrainerOgre, 8, 80, 96)]
        public void RewardsMatchTable(string id, int stones, int gold, int xp)
        {
            var r = QuestTable.Get(id).Reward;
            Assert.AreEqual(stones, r.Stones, id + " 강화석");
            Assert.AreEqual(gold, r.Gold, id + " 골드");
            Assert.AreEqual(xp, r.Xp, id + " 경험치");
            Assert.AreEqual(0, r.Chests, id + " 의뢰 상자는 '큼'에만");
        }

        [Test]
        public void RewardTotalsAndCaps()
        {
            // 첫 다섯(오우거까지) + 묶음 3 나-10 새 의뢰 다섯. 첫 다섯 합은 그대로.
            Assert.AreEqual(10, QuestTable.All.Count);
            var core = QuestTable.All.Where(q => CoreFive.Contains(q.Id)).ToList();
            Assert.AreEqual(5, core.Count);
            Assert.AreEqual(22, core.Sum(q => q.Reward.Stones));
            Assert.AreEqual(220, core.Sum(q => q.Reward.Gold));
            Assert.AreEqual(272, core.Sum(q => q.Reward.Xp));
            Assert.Less(272, LevelTable.Cumulative(2), "다섯 개 합이 레벨 2 누계 아래");
            foreach (var q in QuestTable.All)
            {
                Assert.LessOrEqual(q.Reward.Stones, QuestRewards.MaxStonesEarly, q.Id + " 1~4층 11석 이하");
                Assert.AreEqual(q.Reward.Stones * 10, q.Reward.Gold, q.Id + " 골드 = 강화석 × 10");
            }
            Assert.AreEqual("중간", QuestTable.Get(QuestTable.TrainerOgre).SizeLabel);
            Assert.AreEqual(1, QuestRewards.For(2, QuestRewards.Large).Chests, "큼은 의뢰 상자 1");
        }

        // ── 2. 표 검사 ──

        [Test]
        public void TableIsConsistent()
        {
            var ids = QuestTable.All.Select(q => q.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
            foreach (var q in QuestTable.All)
            {
                Assert.IsTrue(NpcTable.Exists(q.GiverNpcId), q.Id + " 주는 사람이 주민 표에 있음");
                Assert.IsNotEmpty(q.Steps, q.Id + " 단계");
                foreach (var r in q.Requires) Assert.IsTrue(QuestTable.Exists(r), q.Id + " 선행 " + r);
                if (q.CountsBeforeAccept)
                    Assert.IsTrue(!string.IsNullOrEmpty(q.Milestone) || !string.IsNullOrEmpty(q.BossId), q.Id + " 받기 전 기록(이정표 또는 보스)");
                foreach (var g in q.Steps)
                {
                    Assert.Greater(g.Target, 0);
                    if (g.Kind == GoalKind.KillWithSkill && q.Id != QuestTable.SmithRats) Assert.AreNotEqual(KillSource.None, g.Skill, q.Id + " 스킬");
                }
            }
            foreach (var q in QuestTable.All) Assert.IsFalse(Reaches(q.Id, q.Id, new HashSet<string>()), q.Id + " 선행 고리");
        }

        static bool Reaches(string from, string target, HashSet<string> seen)
        {
            foreach (var r in QuestTable.Get(from).Requires)
            {
                if (r == target) return true;
                if (seen.Add(r) && Reaches(r, target, seen)) return true;
            }
            return false;
        }

        [Test]
        public void AtMostFourAtOnce()
        {
            // 동시에 받은 채(Active·Achieved)일 수 있는 의뢰 묶음 = 서로 선행 관계가 없는 묶음. 가장 큰 것이 4 이하(2차 10-5), 첫 판은 3.
            var all = QuestTable.All.ToList();
            int best = 0;
            for (int mask = 1; mask < 1 << all.Count; mask++)
            {
                var set = Enumerable.Range(0, all.Count).Where(i => (mask & (1 << i)) != 0).Select(i => all[i].Id).ToList();
                bool ok = true;
                foreach (var a in set)
                    foreach (var b in set)
                        if (a != b && Reaches(a, b, new HashSet<string>())) ok = false;
                if (ok) best = System.Math.Max(best, set.Count);
            }
            Assert.LessOrEqual(best, 4);
            // 첫 다섯만이면 3, 오우거 뒤 새 의뢰(묶음 3 나-10)까지 넣으면 4(상한).
            Assert.AreEqual(4, best);
        }

        /// <summary>오프닝 의뢰 둘을 보고하고 궁수 의뢰를 받아 보고한 꾸러미(ctx로 다시 계산).</summary>
        static (CarryData carry, QuestBook book) AfterWindblade(QuestContext ctx)
        {
            var (c, book) = AfterOpening();
            foreach (var id in new[] { QuestTable.GateDescend, QuestTable.SmithRats })
            {
                book.ForceState(id, QuestState.Achieved);
                Assert.IsTrue(book.Report(id).HasValue);
            }
            book.Refresh(ctx);
            Assert.AreEqual(QuestState.Offered, book.State(QuestTable.TrainerWindblade));
            Assert.AreEqual(QuestState.Locked, book.State(QuestTable.TrainerOgre), "궁수 의뢰 지급 전에는 굴이 있어도 잠김");
            book.Accept(QuestTable.TrainerWindblade);
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Achieved);
            Assert.IsTrue(book.Report(QuestTable.TrainerWindblade).HasValue);
            return (c, book);
        }

        [Test]
        public void OgreQuestStaysLockedWithoutDen()
        {
            // default(QuestContext) = 굴 없음 가정. 궁수 보고 뒤에도 잠기고 목록에 없다.
            Assert.IsFalse(default(QuestContext).OgreDenReady);
            var (c, book) = AfterWindblade(default);
            book.Refresh();
            Assert.AreEqual(QuestState.Locked, book.State(QuestTable.TrainerOgre), "굴 없음 가정이면 잠김");
            Assert.IsFalse(book.ListRows().Any(r => r.Quest.Id == QuestTable.TrainerOgre), "목록에도 없음");
            Assert.AreEqual(QuestMarker.None, book.Marker(NpcTable.Trainer));
            book.Refresh(new QuestContext(true));
            Assert.AreEqual(QuestState.Offered, book.State(QuestTable.TrainerOgre), "오우거 굴 있음 가정이면 열림");
        }

        [Test]
        public void OgreQuestOpensWithLiveDen()
        {
            // 2층 계단 아래 오우거 굴(OgreDen)이 들어와 지금 던전 상태(Live)에서 잠금이 풀린다(12장 결정 1 A).
            Assert.IsTrue(OgreDen.InDungeon);
            Assert.IsTrue(QuestTable.OgreDenInDungeon);
            Assert.IsTrue(QuestContext.Live.OgreDenReady);
            var (c, book) = AfterWindblade(QuestContext.Live);
            CollectionAssert.AreEqual(new[] { QuestTable.TrainerOgre }, book.Refresh(QuestContext.Live), "궁수 보고 뒤 열림");
            Assert.AreEqual(QuestState.Offered, book.State(QuestTable.TrainerOgre));
            Assert.AreEqual(QuestMarker.Offer, book.Marker(NpcTable.Trainer), "무진 '!'");
            Assert.AreEqual("받을 수 있음", book.ListRows().Single(r => r.Quest.Id == QuestTable.TrainerOgre).Status);

            // 받기 장면(3마디)이 나오고 끝나면 진행 중.
            SpeakerIdentity.Reveal(c, NpcTable.Trainer);
            var plan = TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live);
            Assert.AreSame(TownScript.TrainerOfferOgre, plan.Scenes.Single());
            Assert.AreEqual(3, TownScript.TrainerOfferOgre.Utterances);
            TalkDirector.End(plan.Scenes[0], c, QuestContext.Live);
            Assert.AreEqual(QuestState.Active, book.State(QuestTable.TrainerOgre));
            Assert.AreEqual("· 갱도 오우거 쓰러뜨리기", book.HudLines(QuestHudMode.Dungeon, 0).First());
        }

        [Test]
        public void LiveDenOgreKillReportsOnce()
        {
            // 굴 오우거 처치(처치 사건, 출처 없음, 보스) → 달성 알림·HUD '…' → 보고하면 8·80·96을 한 번만.
            var (c, book) = AfterWindblade(QuestContext.Live);
            book.Refresh(QuestContext.Live);
            Assert.IsTrue(book.Accept(QuestTable.TrainerOgre));
            SpeakerIdentity.Reveal(c, NpcTable.Trainer);
            var ups = book.Handle(QuestEvent.Killed(MonsterKind.Ogre, KillSource.None, false, true, floor: OgreDen.Floor));
            var done = ups.Single(u => u.Quest.Id == QuestTable.TrainerOgre);
            Assert.IsTrue(done.Achieved);
            Assert.AreEqual("의뢰 끝 — 굴의 큰 놈. 올라가면 무진에게 알리자.", done.Notice);
            Assert.Contains("… 굴의 큰 놈 — 무진에게 알리기", book.HudLines(QuestHudMode.Dungeon, 0));
            Assert.AreEqual(QuestMarker.Report, book.Marker(NpcTable.Trainer));
            Assert.IsEmpty(book.Handle(QuestEvent.Killed(MonsterKind.Ogre, KillSource.None, false, true)), "달성 뒤 다시 잡아도 안 셈");

            int stones = c.Stones, gold = c.Gold, xp = c.TotalXp;
            var plan = TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live);
            // 보고 뒤 마무리 장면이 붙는다(묶음 3 나-6).
            // 보고 뒤 마무리(묶음 3 나-6), 그 보고로 열린 '뿔을 벽에'(묶음 3 나-10) 받기.
            CollectionAssert.AreEqual(new[] { TownScript.TrainerReportOgre, TownScript.OgreAfter, TownScript.TrainerOfferSlam }, plan.Scenes);
            var r = TalkDirector.End(plan.Scenes[0], c, QuestContext.Live);
            CollectionAssert.AreEqual(new[] { QuestTable.TrainerOgre }, r.Reported);
            TalkDirector.End(plan.Scenes[0], c, QuestContext.Live);
            Assert.IsNull(book.Report(QuestTable.TrainerOgre), "두 번째 보고는 아무것도 주지 않음");
            Assert.AreEqual(stones + 8, c.Stones);
            Assert.AreEqual(gold + 80, c.Gold);
            Assert.AreEqual(xp + 96, c.TotalXp);
            Assert.AreEqual(QuestState.Rewarded, book.State(QuestTable.TrainerOgre));
        }

        [Test]
        public void FirstPlayHasFourQuests()
        {
            // 굴 없음 가정(default ctx)으로 열 수 있는 의뢰 = 4개.
            var (c, book) = AfterOpening();
            for (int round = 0; round < 6; round++)
            {
                book.Refresh();
                foreach (var q in book.InState(QuestState.Offered)) book.Accept(q.Id);
                foreach (var q in book.InState(QuestState.Active)) book.ForceState(q.Id, QuestState.Achieved);
                foreach (var q in book.InState(QuestState.Achieved)) book.Report(q.Id);
            }
            Assert.AreEqual(4, book.InState(QuestState.Rewarded).Count);
            Assert.AreEqual(QuestState.Locked, book.State(QuestTable.TrainerOgre));
        }

        [Test]
        public void LiveDenAllFiveQuestsPay()
        {
            // 지금 던전(Live ctx)이면 다섯 의뢰를 모두 받고 보고한다: 강화석 22 · 골드 220 · 경험치 272(4-1 합).
            var c = NewCarry();
            int stones = c.Stones, gold = c.Gold, xp = c.TotalXp;
            TalkDirector.Skip(TownScript.Opening, 0, c, QuestContext.Live);
            var book = new QuestBook(c);
            for (int round = 0; round < 6; round++)
            {
                book.Refresh(QuestContext.Live);
                foreach (var q in book.InState(QuestState.Offered)) book.Accept(q.Id);
                foreach (var q in book.InState(QuestState.Active))
                {
                    if (q.Id == QuestTable.TrainerOgre) book.Handle(QuestEvent.Killed(MonsterKind.Ogre, KillSource.None, false, true, floor: OgreDen.Floor));
                    else book.ForceState(q.Id, QuestState.Achieved);
                }
                foreach (var q in book.InState(QuestState.Achieved)) book.Report(q.Id);
            }
            foreach (var id in CoreFive) Assert.AreEqual(QuestState.Rewarded, book.State(id), id);
            // 오우거 뒤 새 의뢰(묶음 3 나-10) 가운데 열쇠·패배가 필요 없는 셋도 같은 돌림으로 끝난다(금고는 열쇠, 기둥은 패배가 있어야 열림).
            Assert.AreEqual(QuestState.Locked, book.State(QuestTable.SmithSafe), "열쇠 없음");
            Assert.AreEqual(QuestState.Locked, book.State(QuestTable.TrainerPillar), "진 적 없음");
            var paid = QuestTable.All.Where(q => book.State(q.Id) == QuestState.Rewarded).ToList();
            Assert.AreEqual(8, paid.Count);
            Assert.AreEqual(paid.Sum(q => q.Reward.Stones), c.Stones - stones, "강화석");
            Assert.AreEqual(paid.Sum(q => q.Reward.Gold), c.Gold - gold, "골드");
            Assert.AreEqual(paid.Sum(q => q.Reward.Xp), c.TotalXp - xp, "경험치");
            Assert.AreEqual(272, QuestTable.All.Where(q => CoreFive.Contains(q.Id)).Sum(q => q.Reward.Xp), "첫 다섯 합 272는 레벨 2 누계 아래");
        }

        // ── 3. 상태 기계 ──

        [Test]
        public void StateMachineOrder()
        {
            var c = NewCarry();
            var book = new QuestBook(c);
            Assert.AreEqual(QuestState.Locked, book.State(QuestTable.GateDescend));
            Assert.IsFalse(book.Accept(QuestTable.GateDescend), "Locked에서 받기 안 됨");
            CollectionAssert.AreEquivalent(new[] { QuestTable.GateDescend, QuestTable.SmithRats }, book.Refresh(), "선행 없는 의뢰만 열림");
            Assert.AreEqual(QuestState.Locked, book.State(QuestTable.GateFloor2));
            Assert.IsFalse(book.Accept(QuestTable.GateFloor2), "선행이 안 된 의뢰는 받을 수 없음");
            Assert.IsEmpty(book.Refresh(), "다시 불러도 같음");

            Assert.IsTrue(book.Accept(QuestTable.SmithRats));
            Assert.IsFalse(book.Accept(QuestTable.SmithRats), "Offered에서만");
            Assert.AreEqual(QuestState.Active, book.State(QuestTable.SmithRats));

            // Active가 아닌 의뢰(gate_descend는 Offered)는 사건을 무시한다.
            book.Handle(QuestEvent.FloorEntered(1));
            Assert.AreEqual(0, book.Step(QuestTable.GateDescend));
            Assert.AreEqual(QuestState.Offered, book.State(QuestTable.GateDescend));

            for (int i = 0; i < 12; i++) book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind));
            Assert.AreEqual(QuestState.Achieved, book.State(QuestTable.SmithRats));
            Assert.AreEqual(8, book.Count(QuestTable.SmithRats), "센 수는 목표에서 멈춤");

            Assert.IsNull(book.Report(QuestTable.GateDescend), "Achieved가 아니면 보상 없음");
            int stones = c.Stones, gold = c.Gold, xp = c.TotalXp;
            var r = book.Report(QuestTable.SmithRats);
            Assert.IsTrue(r.HasValue);
            Assert.AreEqual(QuestState.Rewarded, book.State(QuestTable.SmithRats));
            Assert.AreEqual(stones + 3, c.Stones);
            Assert.AreEqual(gold + 30, c.Gold);
            Assert.AreEqual(xp + 40, c.TotalXp);
            Assert.IsNull(book.Report(QuestTable.SmithRats), "두 번째 보고는 아무것도 주지 않음");
            Assert.AreEqual(stones + 3, c.Stones, "재화는 한 번만 늚");
            Assert.AreEqual(xp + 40, c.TotalXp);
            Assert.IsEmpty(book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind)), "지급 뒤 사건 무시");
        }

        [Test]
        public void AchievedSurvivesFurtherEvents()
        {
            var (c, book) = AfterOpening();
            for (int i = 0; i < 8; i++) book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind));
            Assert.AreEqual(QuestState.Achieved, book.State(QuestTable.SmithRats));
            Assert.IsEmpty(book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind)).Where(u => u.Quest.Id == QuestTable.SmithRats), "달성 뒤 사건 무시");
            var copy = CarryData.FromText(c.ToText());
            Assert.AreEqual(QuestState.Achieved, new QuestBook(copy).State(QuestTable.SmithRats), "원정을 넘어 남음(꾸러미 왕복)");
        }

        // ── 4. 처치 필터 ──

        [Test]
        public void KillFilters()
        {
            var (c, book) = AfterOpening();
            // 굴쥐 쫓기는 처음엔 스킬이 없어 어떤 공격이든 센다(기획/스킬-자원-트리-1차.md 2장, 목표 출처 None).
            book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind));
            Assert.AreEqual(1, book.Count(QuestTable.SmithRats), "회오리 굴쥐 셈");
            book.Handle(Kill(MonsterKind.Rat, KillSource.Other));
            Assert.AreEqual(2, book.Count(QuestTable.SmithRats), "평타 굴쥐도 셈");
            book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind, noReward: true));
            Assert.AreEqual(2, book.Count(QuestTable.SmithRats), "보상 없는 굴쥐 안 셈");
            book.Handle(Kill(MonsterKind.Archer, KillSource.Whirlwind));
            Assert.AreEqual(2, book.Count(QuestTable.SmithRats), "궁수 안 셈");
            book.Handle(Kill(MonsterKind.Rat, KillSource.None));
            Assert.AreEqual(2, book.Count(QuestTable.SmithRats), "출처 없는 처치 사건은 처치 목표를 채우지 않음(두 번 세지 않게)");
            book.Handle(Kill(MonsterKind.Rat, KillSource.SwordWave));
            Assert.AreEqual(3, book.Count(QuestTable.SmithRats), "검풍 굴쥐도 셈");

            book.ForceState(QuestTable.TrainerWindblade, QuestState.Active);
            book.Handle(Kill(MonsterKind.Archer, KillSource.SwordWave));
            Assert.AreEqual(1, book.Count(QuestTable.TrainerWindblade), "검풍 궁수 셈");
            book.Handle(Kill(MonsterKind.Archer, KillSource.None));
            Assert.AreEqual(1, book.Count(QuestTable.TrainerWindblade));

            book.ForceState(QuestTable.TrainerOgre, QuestState.Active);
            book.Handle(QuestEvent.Killed(MonsterKind.Ogre, KillSource.SwordWave, false, true));
            Assert.AreEqual(QuestState.Active, book.State(QuestTable.TrainerOgre), "보스는 피해 사건으로 세지 않음(두 번 셈 방지)");
            book.Handle(QuestEvent.Killed(MonsterKind.Ogre, KillSource.None, false, false));
            Assert.AreEqual(QuestState.Active, book.State(QuestTable.TrainerOgre), "보스가 아니면 안 셈");
            book.Handle(QuestEvent.Killed(MonsterKind.Ogre, KillSource.None, false, true));
            Assert.AreEqual(QuestState.Achieved, book.State(QuestTable.TrainerOgre), "출처 없음 + 보스");
        }

        [Test]
        public void OneKillCountsOnceAcrossBothEvents()
        {
            // 한 처치에 처치 사건(EnemyKilled, 출처 없음)이 먼저, 피해 사건(PlayerDealtDamage)이 뒤에 나간다.
            var (c, book) = AfterOpening();
            book.Handle(QuestEvent.Killed(MonsterKind.Rat, KillSource.None));
            book.Handle(QuestEvent.Killed(MonsterKind.Rat, KillSource.Whirlwind));
            Assert.AreEqual(1, book.Count(QuestTable.SmithRats));
        }

        [Test]
        public void AssistWindowTwoSeconds()
        {
            var (c, book) = AfterOpening();
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Active);
            var assist = new SkillAssist();
            assist.Hit(7, KillSource.SwordWave, 10.0);
            assist.Hit(7, KillSource.Other, 10.5);
            Assert.AreEqual(KillSource.SwordWave, assist.AssistFor(7, 12.0), "2초 안");
            Assert.AreEqual(KillSource.None, assist.AssistFor(7, 12.01), "2초 밖");
            Assert.AreEqual(KillSource.None, assist.AssistFor(8, 10.5), "맞은 적 없음");

            book.Handle(QuestEvent.Killed(MonsterKind.Archer, KillSource.Other, assist: assist.AssistFor(7, 11.9)));
            Assert.AreEqual(1, book.Count(QuestTable.TrainerWindblade), "검풍을 맞히고 평타로 마무리해도 셈");
            book.Handle(QuestEvent.Killed(MonsterKind.Archer, KillSource.Other, assist: assist.AssistFor(7, 12.5)));
            Assert.AreEqual(1, book.Count(QuestTable.TrainerWindblade), "2초 밖은 안 셈");

            assist.Hit(9, KillSource.Whirlwind, 20.0);
            book.Handle(QuestEvent.Killed(MonsterKind.Rat, KillSource.Other, assist: assist.AssistFor(9, 21.0)));
            Assert.AreEqual(1, book.Count(QuestTable.SmithRats), "회오리 도움도 셈");
            assist.Forget(9);
            Assert.AreEqual(KillSource.None, assist.AssistFor(9, 21.0));
            assist.Clear();
            Assert.AreEqual(0, assist.Count);
        }

        // ── 5. 단계 ──

        [Test]
        public void DescendNeedsFloorThenAscend()
        {
            var (c, book) = AfterOpening();
            book.Handle(QuestEvent.Ascended());
            Assert.AreEqual(0, book.Step(QuestTable.GateDescend), "올라가기가 먼저 와도 넘어가지 않음");
            book.Handle(QuestEvent.FloorEntered(1, rebuild: true));
            Assert.AreEqual(0, book.Step(QuestTable.GateDescend), "다시 짓기는 무시");
            var ups = book.Handle(QuestEvent.FloorEntered(2));
            Assert.AreEqual(1, book.Step(QuestTable.GateDescend), "아무 층에 내려서면 다음 단계");
            Assert.IsTrue(ups.Single(u => u.Quest.Id == QuestTable.GateDescend).StepAdvanced);
            Assert.AreEqual(QuestState.Active, book.State(QuestTable.GateDescend));
            book.Handle(QuestEvent.FloorEntered(1));
            Assert.AreEqual(1, book.Step(QuestTable.GateDescend), "같은 사건은 다음 단계를 채우지 않음");
            var done = book.Handle(QuestEvent.Ascended()).Single(u => u.Quest.Id == QuestTable.GateDescend);
            Assert.AreEqual(QuestState.Achieved, book.State(QuestTable.GateDescend));
            Assert.IsTrue(done.Achieved);
            Assert.AreEqual("의뢰 끝 — 갱도로 내려가기. 올라가면 춘삼에게 알리자.", done.Notice);
        }

        [Test]
        public void ProgressNotices()
        {
            var (c, book) = AfterOpening();
            var u = book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind)).Single();
            Assert.AreEqual("굴쥐 1/8", u.Notice);
            for (int i = 0; i < 6; i++) book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind));
            u = book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind)).Single();
            Assert.AreEqual("굴쥐 8/8", u.ProgressNotice);
            Assert.AreEqual("의뢰 끝 — 굴쥐 쫓기. 올라가면 옥금에게 알리자.", u.Notice);
        }

        // ── 6. 받기 전 기록 ──

        [Test]
        public void MilestoneBeforeAccept()
        {
            var (c, book) = AfterOpening();
            Assert.AreEqual(QuestState.Locked, book.State(QuestTable.GateFloor2));
            book.Handle(QuestEvent.StakeLit(2, false));
            Assert.IsFalse(TownSave.HasMilestone(c, TownSave.MilestoneStairsF2), "계단 앞이 아니면 이정표 아님");
            book.Handle(QuestEvent.StakeLit(1, true));
            Assert.IsFalse(TownSave.HasMilestone(c, TownSave.MilestoneStairsF2), "1층 계단 앞은 2층 이정표 아님");
            book.Handle(QuestEvent.StakeLit(2, true));
            Assert.IsTrue(TownSave.HasMilestone(c, TownSave.MilestoneStairsF2), "Locked일 때도 이정표를 적음");
            Assert.AreEqual(1, c.Count("ms:stairs.f2"));

            book.ForceState(QuestTable.GateDescend, QuestState.Achieved);
            book.Report(QuestTable.GateDescend);
            book.Refresh();
            Assert.IsTrue(book.Accept(QuestTable.GateFloor2));
            Assert.AreEqual(QuestState.Achieved, book.State(QuestTable.GateFloor2), "받는 자리에서 곧바로 달성");
        }

        /// <summary>
        /// 시스템-컨텐츠-다듬기-검토-1차.md Q2 '이미 한 일 인정': 의뢰가 열리기 전에 굴 오우거를 잡았으면(꾸러미 처치 수 1 이상) 받는 자리에서 달성이고,
        /// 받기 장면은 '이미 잡음'(받기와 보고를 한 번에)이다. 의뢰 보상은 한 번, 오우거 첫 처치 기록(첫 처치 보상의 기준)은 건드리지 않는다.
        /// </summary>
        [Test]
        public void OgreKilledBeforeOfferIsCreditedOnce()
        {
            var (c, book) = AfterWindblade(QuestContext.Live);
            var q = QuestTable.Get(QuestTable.TrainerOgre);
            Assert.AreEqual(QuestState.Locked, book.State(q.Id));
            Assert.IsFalse(book.DoneBeforeAccept(q), "처치 전에는 기록 없음");
            Assert.IsTrue(BossLedger.RecordKill(c, OgreDen.BossId, c.Expedition), "의뢰 전 첫 처치(첫 처치 보상은 던전에서 이 한 번)");
            Assert.IsEmpty(book.Handle(QuestEvent.Killed(MonsterKind.Ogre, KillSource.None, false, true, floor: OgreDen.Floor)), "잠긴 의뢰는 처치 사건을 세지 않음");
            Assert.IsTrue(book.DoneBeforeAccept(q), "꾸러미 오우거 처치 수 1 이상 = 받기 전 기록");
            Assert.AreEqual(QuestState.Locked, book.State(q.Id), "기록이 있어도 열리는 조건은 그대로(궁수 지급 뒤 Refresh)");

            book.Refresh(QuestContext.Live);
            Assert.AreEqual(QuestState.Offered, book.State(q.Id));
            Assert.AreEqual(OfferCase.Done, TalkDirector.OfferCaseFor(q, c));
            SpeakerIdentity.Reveal(c, NpcTable.Trainer);
            int stones = c.Stones, gold = c.Gold, xp = c.TotalXp;
            string before = c.ToText();
            var plan = TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live);
            Assert.AreEqual(before, c.ToText(), "Build는 꾸러미를 바꾸지 않음");
            CollectionAssert.AreEqual(new[] { TownScript.TrainerOfferOgreDone, TownScript.OgreAfter }, plan.Scenes, "받기와 보고를 한 번에, 뒤에 마무리(묶음 3 나-6)");
            Assert.LessOrEqual(TownScript.TrainerOfferOgreDone.Utterances, 3, "2~3마디");

            var r = TalkDirector.End(plan.Scenes[0], c, QuestContext.Live);
            CollectionAssert.AreEqual(new[] { q.Id }, r.Accepted);
            CollectionAssert.AreEqual(new[] { q.Id }, r.Reported);
            Assert.AreEqual(1, r.Rewards.Count);
            Assert.AreEqual(QuestState.Rewarded, book.State(q.Id));
            Assert.IsEmpty(TalkDirector.End(plan.Scenes[0], c, QuestContext.Live).Reported, "같은 장면을 두 번 끝내도 보상은 한 번");
            Assert.IsNull(book.Report(q.Id), "두 번째 보고는 아무것도 주지 않음");
            Assert.IsEmpty(book.Handle(QuestEvent.Killed(MonsterKind.Ogre, KillSource.None, false, true, floor: OgreDen.Floor)), "지급 뒤 다시 잡아도 안 셈");
            Assert.AreEqual(stones + 8, c.Stones, "의뢰 보상 강화석 한 번");
            Assert.AreEqual(gold + 80, c.Gold, "의뢰 보상 골드 한 번");
            Assert.AreEqual(xp + 96, c.TotalXp, "의뢰 보상 경험치 한 번");
            Assert.AreEqual(1, BossLedger.Kills(c, OgreDen.BossId), "의뢰가 보스 처치 수를 바꾸지 않음");
            Assert.IsFalse(BossLedger.RecordKill(c, OgreDen.BossId, c.Expedition + 1), "다음 처치는 첫 처치가 아님(첫 처치 보상은 한 번)");
            TalkDirector.End(plan.Scenes[1], c, QuestContext.Live); // 마무리 장면까지 본다
            // 지급 뒤 열린 '뿔을 벽에'(묶음 3 나-10)를 받고 나면 반복 대사.
            CollectionAssert.AreEqual(new[] { TownScript.TrainerOfferSlam }, TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live).Scenes, "다시 말을 걸면 새 의뢰");
            TalkDirector.End(TownScript.TrainerOfferSlam, c, QuestContext.Live);
            Assert.AreEqual(TalkSceneKind.Repeat, TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live).Scenes.Single().Kind, "다시 말을 걸면 반복 대사");
        }

        [Test]
        public void OgreKillBeforeAcceptAchievesOnAccept()
        {
            // 받기 장면을 거치지 않고 Accept만 불러도(시험 패널·건너뛰기 길) 같은 기록으로 곧바로 달성. 처치가 없으면 진행 중.
            var (c, book) = AfterWindblade(QuestContext.Live);
            book.Refresh(QuestContext.Live);
            var copy = CarryData.FromText(c.ToText());
            Assert.IsTrue(book.Accept(QuestTable.TrainerOgre));
            Assert.AreEqual(QuestState.Active, book.State(QuestTable.TrainerOgre), "처치 기록 없음 → 진행 중");

            var other = new QuestBook(copy);
            BossLedger.RecordKill(copy, OgreDen.BossId, 1);
            Assert.IsTrue(other.Accept(QuestTable.TrainerOgre));
            Assert.AreEqual(QuestState.Achieved, other.State(QuestTable.TrainerOgre), "받는 자리에서 곧바로 달성");
            Assert.AreEqual(QuestMarker.Report, other.Marker(NpcTable.Trainer));
            Assert.IsTrue(other.Report(QuestTable.TrainerOgre).HasValue);
            Assert.IsNull(other.Report(QuestTable.TrainerOgre));
            Assert.IsFalse(new QuestBook(copy).DoneBeforeAccept(QuestTable.Get(QuestTable.TrainerWindblade)), "받기 전 기록을 인정하지 않는 의뢰");
        }

        [Test]
        public void KillsBeforeAcceptDoNotCount()
        {
            var c = NewCarry();
            var book = new QuestBook(c);
            book.Refresh();
            for (int i = 0; i < 5; i++) book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind));
            book.Accept(QuestTable.SmithRats);
            Assert.AreEqual(0, book.Count(QuestTable.SmithRats), "받기 전 처치는 세지 않음");

            book.ForceState(QuestTable.TrainerWindblade, QuestState.Offered);
            book.Handle(Kill(MonsterKind.Archer, KillSource.SwordWave));
            book.Accept(QuestTable.TrainerWindblade);
            Assert.AreEqual(QuestState.Active, book.State(QuestTable.TrainerWindblade));
            Assert.AreEqual(0, book.Count(QuestTable.TrainerWindblade));
        }

        // ── 7. 경험치·레벨 ──

        [Test]
        public void RewardXpLevelsUpWithSkillPoint()
        {
            var c = NewCarry();
            c.TotalXp = 330;
            c.Level = LevelTable.LevelFor(330);
            Assert.AreEqual(1, c.Level);
            var book = new QuestBook(c);
            book.Refresh();
            book.Accept(QuestTable.GateDescend);
            book.ForceState(QuestTable.GateDescend, QuestState.Achieved);
            var r = book.Report(QuestTable.GateDescend).Value;
            Assert.AreEqual(370, c.TotalXp);
            Assert.AreEqual(2, c.Level);
            Assert.AreEqual(LevelTable.LevelFor(c.TotalXp), c.Level);
            Assert.AreEqual(1, c.SkillPoints, "스킬 점수 +1");
            Assert.IsTrue(r.LeveledUp);
            Assert.AreEqual(1, r.LevelBefore);
            Assert.AreEqual(2, r.LevelAfter);
            Assert.AreEqual(1, r.SkillPointsGained);
            Assert.AreEqual("몸에 힘이 차오른다 — 레벨 2 · 스킬 점수 +1 [K]", TownScript.LevelUpNotice(r), "던전 레벨업 알림과 같은 [K] 표기(마을에서도 K가 열림)");
            Assert.AreEqual("강화석 3 · 골드 30 · 경험치 40을 받았다.", TownScript.RewardLine(r));
            Assert.AreEqual("품삯 — 강화석 3 · 골드 30 · 경험치 40", TownScript.RewardNotice(r));
        }

        [Test]
        public void RewardWithoutLevelUp()
        {
            var c = NewCarry();
            var r = QuestRewards.Grant(c, QuestTable.Get(QuestTable.GateFloor2).Reward);
            Assert.AreEqual(1, c.Level);
            Assert.AreEqual(0, c.SkillPoints);
            Assert.IsFalse(r.LeveledUp);
            Assert.IsNull(TownScript.LevelUpNotice(r));
            Assert.AreEqual("강화석 4 · 골드 40 · 경험치 48을 받았다.", TownScript.RewardLine(r));
        }

        [TestCase(40, "을")]
        [TestCase(48, "을")]
        [TestCase(96, "을")]
        [TestCase(12, "를")]
        [TestCase(4, "를")]
        [TestCase(5, "를")]
        [TestCase(9, "를")]
        [TestCase(100, "을")]
        public void ObjectParticle(int n, string particle) => Assert.AreEqual(particle, TownScript.ObjectParticle(n));

        // ── 8. 이름 ──

        [Test]
        public void SpeakerIdentityRules()
        {
            var c = NewCarry();
            string t = NpcTable.Trainer;
            Assert.AreEqual("?", SpeakerIdentity.Label(c, t));
            Assert.AreEqual("경비 초소에", SpeakerIdentity.ReportTarget(c, t));
            Assert.AreEqual("? (경비 초소)", SpeakerIdentity.GiverLabel(c, t));
            Assert.AreEqual("F 말 걸기 · 의뢰", SpeakerIdentity.Hint(c, t, QuestMarker.Offer));
            Assert.AreEqual("", SpeakerIdentity.Label(c, null), "나레이션은 이름표 없음");

            Assert.IsTrue(SpeakerIdentity.Reveal(c, t));
            Assert.IsFalse(SpeakerIdentity.Reveal(c, t), "여러 번 해도 같음");
            Assert.IsFalse(SpeakerIdentity.Reveal(c, "npc.nobody"), "표에 없는 id는 무시");
            Assert.AreEqual("무진", SpeakerIdentity.Label(c, t));
            Assert.AreEqual("무진에게", SpeakerIdentity.ReportTarget(c, t));
            Assert.AreEqual("무진", SpeakerIdentity.GiverLabel(c, t));
            Assert.AreEqual("F 무진", SpeakerIdentity.Hint(c, t, QuestMarker.None));
            Assert.AreEqual("F 무진 · 보고", SpeakerIdentity.Hint(c, t, QuestMarker.Report));

            // 되돌아가지 않음: 꾸러미 왕복 뒤에도, 다른 주민을 공개해도.
            var copy = CarryData.FromText(c.ToText());
            SpeakerIdentity.Reveal(copy, NpcTable.Gate);
            Assert.AreEqual("무진", SpeakerIdentity.Label(copy, t));
            Assert.AreEqual("?", SpeakerIdentity.Label(copy, NpcTable.Smith));
        }

        [Test]
        public void HudUsesReportTarget()
        {
            var (c, book) = AfterOpening();
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Achieved);
            var lines = book.HudLines(QuestHudMode.Town, 3);
            Assert.Contains("… 궁수 떨구기 — 경비 초소에 알리기", lines);
            Assert.IsFalse(lines.Any(l => l.Contains("무진")), "모르는 이름은 HUD에 없음");
            Assert.IsFalse(book.ListRows().Any(r => r.Line().Contains("무진")));
            SpeakerIdentity.Reveal(c, NpcTable.Trainer);
            Assert.Contains("… 궁수 떨구기 — 무진에게 알리기", book.HudLines(QuestHudMode.Town, 3));
        }

        // ── 9. 표시 ──

        [Test]
        public void Markers()
        {
            var c = NewCarry();
            var book = new QuestBook(c);
            Assert.AreEqual(QuestMarker.None, book.Marker(NpcTable.Gate), "Refresh 전에는 없음");
            book.Refresh();
            Assert.AreEqual(QuestMarker.Offer, book.Marker(NpcTable.Gate));
            Assert.AreEqual(QuestMarker.None, book.Marker(NpcTable.Trainer));
            book.Accept(QuestTable.GateDescend);
            Assert.AreEqual(QuestMarker.None, book.Marker(NpcTable.Gate), "진행 중은 표시 없음");
            book.ForceState(QuestTable.GateDescend, QuestState.Achieved);
            Assert.AreEqual(QuestMarker.Report, book.Marker(NpcTable.Gate));
            book.ForceState(QuestTable.GateFloor2, QuestState.Offered);
            Assert.AreEqual(QuestMarker.Report, book.Marker(NpcTable.Gate), "둘 다면 '…'");
            Assert.AreEqual("!", QuestMarkers.Glyph(QuestMarker.Offer));
            Assert.AreEqual("…", QuestMarkers.Glyph(QuestMarker.Report));
            foreach (QuestMarker m in System.Enum.GetValues(typeof(QuestMarker)))
                StringAssert.DoesNotContain("?", QuestMarkers.Glyph(m), "'?'는 이름표 전용");
        }

        // ── HUD 순서·목록 ──

        [Test]
        public void HudOrder()
        {
            var c = NewCarry();
            var book = new QuestBook(c);
            CollectionAssert.AreEqual(new[] { "· 갱도 마당으로" }, book.HudLines(QuestHudMode.Town, 3), "오프닝 전");
            Assert.IsEmpty(book.HudLines(QuestHudMode.Dungeon, 2));
            TownSave.MarkSceneSeen(c, TownScript.OpeningId);
            CollectionAssert.AreEqual(new[] { "· 권양기 바구니로 내려가기" }, book.HudLines(QuestHudMode.Town, 3), "의뢰가 하나도 없을 때");

            book.Refresh();
            book.Accept(QuestTable.GateDescend);
            book.Accept(QuestTable.SmithRats);
            book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind));
            CollectionAssert.AreEqual(new[] { "· 굴쥐 1/8", "· 갱도로 내려가기 — 권양기 바구니" }, book.HudLines(QuestHudMode.Dungeon, 2), "최근에 받은 순");
            book.Handle(QuestEvent.FloorEntered(1));
            Assert.AreEqual("· 말뚝에서 바구니로 올라오기", book.HudLines(QuestHudMode.Dungeon, 2)[1]);
            book.Handle(QuestEvent.Ascended());
            CollectionAssert.AreEqual(new[] { "· 굴쥐 1/8", "… 갱도로 내려가기 — 권양기에 알리기" }, book.HudLines(QuestHudMode.Dungeon, 2),
                "던전은 진행 중 먼저, 이름을 모르면 자리 이름");
            SpeakerIdentity.Reveal(c, NpcTable.Gate);
            CollectionAssert.AreEqual(new[] { "… 갱도로 내려가기 — 춘삼에게 알리기", "· 굴쥐 1/8" }, book.HudLines(QuestHudMode.Town, 3), "마을은 보고할 것 먼저");
            Assert.AreEqual(1, book.HudLines(QuestHudMode.Town, 1).Count, "최대 줄");
        }

        [Test]
        public void ListRowsHideLockedAndRewarded()
        {
            var (c, book) = AfterOpening();
            book.Handle(Kill(MonsterKind.Rat, KillSource.Whirlwind));
            var rows = book.ListRows();
            CollectionAssert.AreEquivalent(new[] { QuestTable.GateDescend, QuestTable.SmithRats }, rows.Select(r => r.Quest.Id));
            var rats = rows.Single(r => r.Quest.Id == QuestTable.SmithRats);
            Assert.AreEqual("진행 1/8", rats.Status);
            Assert.AreEqual("옥금", rats.GiverLabel);
            Assert.AreEqual("작음", rats.SizeLabel);
            Assert.AreEqual("굴쥐 쫓기 · 진행 1/8 · 작음 · 강화석 3 · 골드 30 · 경험치 40 · 옥금", rats.Line());
            book.ForceState(QuestTable.GateDescend, QuestState.Achieved);
            Assert.AreEqual("알릴 일", book.ListRows().Single(r => r.Quest.Id == QuestTable.GateDescend).Status);
            book.Report(QuestTable.GateDescend);
            book.Refresh();
            rows = book.ListRows();
            Assert.IsFalse(rows.Any(r => r.Quest.Id == QuestTable.GateDescend), "지급된 의뢰는 안 보임");
            var wind = rows.Single(r => r.Quest.Id == QuestTable.TrainerWindblade);
            Assert.AreEqual("받을 수 있음", wind.Status);
            Assert.AreEqual("? (경비 초소)", wind.GiverLabel);
        }

        [Test]
        public void ForceStateAndAchieveAll()
        {
            var (c, book) = AfterOpening();
            var ups = book.AchieveAllActive();
            Assert.AreEqual(2, ups.Count);
            Assert.IsTrue(ups.All(u => u.Achieved && u.Notice.StartsWith("의뢰 끝 — ")));
            Assert.AreEqual(QuestState.Achieved, book.State(QuestTable.GateDescend));
            Assert.AreEqual(1, book.Step(QuestTable.GateDescend));
            book.ForceState(QuestTable.GateDescend, QuestState.Locked);
            Assert.AreEqual(QuestState.Locked, book.State(QuestTable.GateDescend));
            Assert.AreEqual(0, book.Order(QuestTable.GateDescend));
            StringAssert.StartsWith(QuestTable.SmithRats + " Achieved", book.DebugLine(QuestTable.SmithRats));
        }

        // ── 묶음 3 가-1: 새로 열린 의뢰 알림 ──

        [Test]
        public void NewQuestLineNamesGiversOnlyWhileOffered()
        {
            var (c, book) = AfterOpening();
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Offered);
            Assert.AreEqual("새 일이 생겼다 — ? (경비 초소)", book.NewQuestLine(new[] { QuestTable.TrainerWindblade }), "이름을 모르면 자리 이름");
            SpeakerIdentity.Reveal(c, NpcTable.Trainer);
            Assert.AreEqual("새 일이 생겼다 — 무진", book.NewQuestLine(new[] { QuestTable.TrainerWindblade }));
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Active);
            Assert.IsNull(book.NewQuestLine(new[] { QuestTable.TrainerWindblade }), "이미 받은 의뢰는 빠짐");
            Assert.IsNull(book.NewQuestLine(null));
        }

        [Test]
        public void TownHudEndsWithOfferCount()
        {
            var (c, book) = AfterOpening();
            foreach (var q in book.InState(QuestState.Offered)) book.ForceState(q.Id, QuestState.Locked);
            Assert.AreEqual(0, book.OfferedCount);
            CollectionAssert.DoesNotContain(book.HudLines(QuestHudMode.Town, 3), "! 받을 일 1");
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Offered);
            Assert.AreEqual(1, book.OfferedCount);
            var lines = book.HudLines(QuestHudMode.Town, 3);
            Assert.AreEqual("! 받을 일 1", lines[lines.Count - 1], "마을은 맨 끝");
            Assert.AreEqual("! 받을 일 1", book.HudLines(QuestHudMode.Town, 1)[0], "줄이 모자라도 보임");
            CollectionAssert.DoesNotContain(book.HudLines(QuestHudMode.Dungeon, 3), "! 받을 일 1", "던전에는 없음");
            Assert.AreEqual("받지 않은 일 1 — 받지 않고 떠나도 된다.", TownScript.GateOfferLine(book.OfferedCount));
            Assert.IsNull(TownScript.GateOfferLine(0));
        }

        [Test]
        public void ReportSceneEndAnnouncesNewQuest()
        {
            var (c, book) = AfterOpening();
            book.ForceState(QuestTable.GateDescend, QuestState.Achieved);
            var before = new System.Collections.Generic.HashSet<string>();
            foreach (var q in book.InState(QuestState.Offered)) before.Add(q.Id);
            var scene = TalkDirector.Build(NpcTable.Gate, c).Scenes.Find(s => s.OnEnd != null && System.Array.IndexOf(s.OnEnd.Report, QuestTable.GateDescend) >= 0);
            Assert.IsNotNull(scene, "춘삼 보고 장면");
            var r = TalkDirector.End(scene, c);
            bool opened = false;
            foreach (var id in r.Opened) opened |= book.State(id) == QuestState.Offered && !before.Contains(id);
            string line = book.NewQuestLine(r.Opened);
            if (opened)
            {
                Assert.IsNotNull(line);
                Assert.AreEqual(line, r.Lines[r.Lines.Count - 1], "결과 줄 맨 끝");
                CollectionAssert.Contains(r.Notices, line);
            }
            else Assert.IsNull(line);
        }

        [Test]
        public void GateFloorRowsShowQuestsForThatFloor()
        {
            var (c, book) = AfterOpening();
            foreach (var q in QuestTable.All) book.ForceState(q.Id, QuestState.Locked);
            Assert.AreEqual("", book.FloorHint(1, false), "진행 중 의뢰가 없으면 빈 글");
            book.ForceState(QuestTable.SmithRats, QuestState.Active);
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Active);
            book.ForceState(QuestTable.TrainerOgre, QuestState.Active);
            Assert.AreEqual(" · 의뢰: 굴쥐 쫓기 0/8", book.FloorHint(1, false));
            StringAssert.StartsWith(" · 의뢰: 궁수 떨구기", book.FloorHint(2, false));
            StringAssert.DoesNotContain("굴의 큰 놈", book.FloorHint(2, false), "보스 의뢰는 보통 층 줄에 없음");
            StringAssert.Contains("굴의 큰 놈", book.FloorHint(2, true), "보스방 앞 줄에만");
            StringAssert.DoesNotContain("궁수", book.FloorHint(2, true));
            book.ForceState(QuestTable.SmithRats, QuestState.Achieved);
            Assert.AreEqual("", book.FloorHint(1, false), "달성한 의뢰는 빠짐");
        }

        [Test]
        public void ListRowsCarryGoalLines()
        {
            var (c, book) = AfterOpening();
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Offered);
            book.ForceState(QuestTable.GateDescend, QuestState.Achieved);
            var rows = book.ListRows();
            var rats = rows.Find(r => r.Quest.Id == QuestTable.SmithRats);
            Assert.AreEqual(QuestState.Active, rats.State);
            StringAssert.StartsWith("지금: ", rats.GoalLine);
            StringAssert.Contains("0/8", rats.GoalLine);
            StringAssert.StartsWith("할 일: ", rows.Find(r => r.Quest.Id == QuestTable.TrainerWindblade).GoalLine);
            StringAssert.StartsWith("알리기: ", rows.Find(r => r.Quest.Id == QuestTable.GateDescend).GoalLine);
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Achieved);
            var trainer = book.ListRows().Find(r => r.Quest.Id == QuestTable.TrainerWindblade);
            Assert.AreEqual("알리기: 경비 초소에", trainer.GoalLine, "이름을 모르면 자리 이름");
        }

        // ── 묶음 3 나-6·7: 오우거 뒤 매듭 ──

        [Test]
        public void OgreReportIsFollowedByOneTimeEpilogue()
        {
            var (c, book) = AfterOpening();
            SpeakerIdentity.Reveal(c, NpcTable.Trainer);
            book.ForceState(QuestTable.TrainerOgre, QuestState.Achieved);
            var plan = TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live);
            int report = plan.Scenes.IndexOf(TownScript.TrainerReportOgre);
            int after = plan.Scenes.IndexOf(TownScript.OgreAfter);
            Assert.GreaterOrEqual(report, 0, plan.ToString());
            Assert.Greater(after, report, "보고 뒤에 마무리");
            foreach (var s in plan.Scenes) TalkDirector.End(s, c, QuestContext.Live);
            Assert.IsTrue(TownSave.SeenScene(c, TownScript.OgreAfterId));
            Assert.IsFalse(TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live).Scenes.Contains(TownScript.OgreAfter), "한 번만");

            var gate = TalkDirector.Build(NpcTable.Gate, c, QuestContext.Live);
            CollectionAssert.Contains(gate.Scenes, TownScript.GateAfterOgre, "춘삼 예고");
            foreach (var s in gate.Scenes) TalkDirector.End(s, c, QuestContext.Live);
            Assert.IsFalse(TalkDirector.Build(NpcTable.Gate, c, QuestContext.Live).Scenes.Contains(TownScript.GateAfterOgre), "예고도 한 번만");
        }

        [Test]
        public void AfterOgreRepeatsAndLeftoverGoal()
        {
            var (c, book) = AfterOpening();
            foreach (var q in QuestTable.All) book.ForceState(q.Id, QuestState.Rewarded);
            foreach (var npc in new[] { NpcTable.Gate, NpcTable.Smith, NpcTable.Trainer })
            {
                Assert.AreEqual(3, TalkDirector.RepeatStage(npc, c), npc);
                Assert.AreEqual(3, TownScript.RepeatSet(npc, 3).Length, npc);
                foreach (var s in TownScript.RepeatSet(npc, 3))
                    Assert.LessOrEqual(s.Lines[0].Text.Length, TownScript.MaxShortChars, s.Lines[0].Text);
            }
            c.SurveySheets[1] = 2;
            var lines = book.HudLines(QuestHudMode.Town, 3);
            Assert.AreEqual("· 남은 일 — 측량 2/" + QuestBook.SurveyMax + " · 명패 0", lines[0]);
            Assert.AreEqual(TownScript.HudNoQuest, lines[1]);
        }

        [Test]
        public void KnotCardLines()
        {
            var lines = TownScript.KnotCardLines(4, 3725, 2, 1, 3, 6, Demo6.Core.Loot.Grade.Rare);
            Assert.AreEqual("굴의 큰 놈을 눕혔다. 원정 4번 · 놀이 시간 1시간 2분", lines[0]);
            Assert.AreEqual("그놈에게 진 횟수 2", lines[1]);
            Assert.AreEqual("찾은 명패 1 · 측량 3/6", lines[2]);
            Assert.AreEqual("가장 좋은 장비: 희귀", lines[3]);
            Assert.AreEqual("한 번도 지지 않고 눕혔다", TownScript.KnotCardLines(1, 0, 0, 0, 0, 6, null)[1]);
        }
    }
}
