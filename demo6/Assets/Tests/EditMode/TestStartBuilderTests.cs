using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Progression;
using Demo6.Core.Stats;
using Demo6.Core.TestStart;
using Demo6.Core.Town;
using NUnit.Framework;
using TownKillSource = Demo6.Core.Town.KillSource;

namespace Demo6.Tests
{
    /// <summary>
    /// 바로 가기 시험 메뉴(TestStartPreset·TestStartBuilder): 각 의뢰 단계가 정식 흐름으로 만든 상태와 같은지, 이름 공개, 시작 위치 쪽지 값,
    /// 장비 묶음 등급·칸 수·전설, 레벨·스킬·레벨 체력, 설정 글 왕복.
    /// </summary>
    public sealed class TestStartBuilderTests
    {
        const ulong Salt = 0x5EED_1234UL;

        static readonly TestQuestStage[] Stages =
        {
            TestQuestStage.Fresh, TestQuestStage.AfterFirstReturn, TestQuestStage.ArcherDone, TestQuestStage.OgreAccepted, TestQuestStage.OgreKilled,
        };

        static readonly TestStartAt[] Starts =
        {
            TestStartAt.TownFresh, TestStartAt.Town, TestStartAt.Floor1, TestStartAt.Floor2, TestStartAt.DenFront, TestStartAt.DenFight, TestStartAt.CombatTest,
        };

        /// <summary>마을에서 그 단계로 시작(레벨 단계대로, 스킬 자동 배분 끔).</summary>
        static TestStartPreset TownAt(TestQuestStage stage) =>
            new TestStartPreset { StartAt = TestStartAt.Town, Stage = stage, Level = 0, AutoSkills = false };

        static CarryData Built(TestQuestStage stage) => TestStartBuilder.Build(TownAt(stage), Salt).Carry;

        static TestStartPlan Plan(TestStartAt at, TestQuestStage stage, Action<TestStartPreset> edit = null)
        {
            var p = new TestStartPreset { StartAt = at, Stage = stage };
            edit?.Invoke(p);
            return TestStartBuilder.Build(p, Salt);
        }

        // ── (a) 단계마다 정식 흐름을 손으로 다시 밟은 꾸러미와 같다 ──

        /// <summary>
        /// 층에 들어섬의 꾸러미 몫(DungeonRoot.BuildMap 정식 갈래 + BeginPlay): 굴이 아니면 그 원정 지도의 글자, 처음 밟는 층이면 발견 주머니,
        /// 밟은 층, 굴이 아니면 가장 깊은 층·켠 승강장·줄 깊이.
        /// </summary>
        static void Walk(CarryData c, int floor, bool den = false)
        {
            if (!den)
            {
                bool first = !c.VisitedFloors.Contains(floor);
                var gen = MapByHand(c, floor);
                c.LastGlyphs[floor] = gen.Glyphs;
                if (first && !c.DiscoveryPouch.ContainsKey(floor)) c.DiscoveryPouch[floor] = PouchByHand(gen.Build(), floor);
            }
            c.VisitedFloors.Add(floor);
            if (!den)
            {
                if (floor > c.DeepestFloor) c.DeepestFloor = floor;
                c.LitLandings.Add(floor);
                c.RopeDepth = Math.Max(c.RopeDepth, floor);
            }
            new QuestBook(c).Handle(QuestEvent.FloorEntered(floor));
        }

        /// <summary>DungeonRoot.BuildMap(쪽지 씨앗 없음)을 손으로: 처음 밟는 층은 고른 씨앗, 다시 연 층은 원정 씨앗 + 지난 글자와 견줌.</summary>
        static GeneratedFloor MapByHand(CarryData c, int floor)
        {
            var recipe = FloorRecipe.For(floor);
            bool first = !c.VisitedFloors.Contains(floor);
            string prev = null;
            if (!first) c.LastGlyphs.TryGetValue(floor, out prev);
            var input = new GeneratorInput
            {
                Floor = floor,
                Seed = ExpeditionSeeds.Choose(recipe, first, c.ProfileSalt, c.Expedition),
                FirstVisit = first,
                DeepestFloor = c.DeepestFloor,
                HasPickaxe = c.HasPickaxe,
                HasKey = c.HasKey,
                OnceDone = new HashSet<string>(c.OnceDone),
                Night = c.Night,
            };
            return string.IsNullOrEmpty(prev) ? FloorGenerator.Generate(input) : FloorGenerator.GenerateUnlike(input, prev);
        }

        /// <summary>
        /// PlayerProgress.ComputeDiscoveryPouch를 손으로: 칸마다 새 칸 경험치 + 장면이 등록하는 궤짝·등잔·말뚝(DungeonContent)
        /// + 판자벽 문틈 숨은 방(PlankWall) + 금 간 벽 문틈 지름길(CrackedWall).
        /// </summary>
        static int PouchByHand(FloorMap map, int floor)
        {
            int total = 0;
            var ids = new HashSet<string>();
            foreach (var cell in map.Cells)
            {
                total += XpRules.ForDiscovery(DiscoveryKind.NewCell, floor);
                foreach (var f in cell.Features)
                {
                    if (!ids.Add(f.Id ?? "")) continue;
                    if (f.Kind == FeatureKind.WoodChest) total += XpRules.ForDiscovery(DiscoveryKind.WoodChest, floor);
                    else if (f.Kind == FeatureKind.IronChest) total += XpRules.ForDiscovery(DiscoveryKind.IronChest, floor);
                    else if (f.Kind == FeatureKind.WallLamp) total += XpRules.ForDiscovery(DiscoveryKind.WallLamp, floor);
                    else if (f.Kind == FeatureKind.Stake) total += XpRules.ForDiscovery(DiscoveryKind.Stake, floor);
                }
            }
            foreach (var e in map.Edges)
            {
                if (e.Kind == EdgeKind.Plank) total += XpRules.ForDiscovery(DiscoveryKind.HiddenRoom, floor);
                if (e.Kind == EdgeKind.Cracked) total += XpRules.ForDiscovery(DiscoveryKind.Shortcut, floor);
            }
            return total;
        }

        static void Ev(CarryData c, QuestEvent e) => new QuestBook(c).Handle(e);

        static void Skip(CarryData c, TalkScene scene) => TalkDirector.Skip(scene, 0, c, QuestContext.Live);

        static void UpToTown(CarryData c)
        {
            TownNight.AdvanceForAscend(c);
            TownArrivalRules.Apply(c, TownArrivalKind.Basket, QuestContext.Live);
        }

        /// <summary>정식 흐름 손으로: 대화 장면을 직접 골라 Skip하고, 던전 사건·밤·도착·보스 기록을 정식 함수로 직접 넣는다.</summary>
        static CarryData Manual(TestQuestStage stage)
        {
            var ctx = QuestContext.Live;
            var c = CarryData.NewProfile(Salt);
            if (stage == TestQuestStage.Fresh) return c;

            TownArrivalRules.Apply(c, TownArrivalKind.NewPlay, ctx);
            Skip(c, TownScript.Opening);
            TownNight.Depart(c);
            Walk(c, 1);
            Ev(c, QuestEvent.Ascended(1));
            UpToTown(c);
            if (stage == TestQuestStage.AfterFirstReturn) return c;

            Skip(c, TownScript.GateReportDescend);
            Skip(c, TownScript.GateOfferFloor2);
            Skip(c, TownScript.TrainerOfferWindblade);
            TownNight.Depart(c);
            Walk(c, 1);
            for (int i = 0; i < 8; i++) Ev(c, QuestEvent.Killed(MonsterKind.Rat, TownKillSource.Whirlwind));
            Walk(c, 2);
            for (int i = 0; i < 3; i++) Ev(c, QuestEvent.Killed(MonsterKind.Archer, TownKillSource.SwordWave));
            Ev(c, QuestEvent.StakeLit(2, true));
            Ev(c, QuestEvent.Ascended(2));
            UpToTown(c);
            if (stage == TestQuestStage.ArcherDone) return c;

            Skip(c, TownScript.GateReportFloor2);
            Skip(c, TownScript.SmithReportRats);
            Skip(c, TownScript.TrainerReportWindblade);
            Skip(c, TownScript.TrainerOfferOgre);
            if (stage == TestQuestStage.OgreAccepted) return c;

            TownNight.Depart(c);
            Walk(c, 2);
            Walk(c, OgreDen.Floor, true);
            BossLedger.LightStake(c, OgreDen.FrontStakeId);
            Ev(c, QuestEvent.StakeLit(OgreDen.Floor, false));
            BossLedger.RecordKill(c, OgreDen.BossId, c.Expedition);
            Ev(c, QuestEvent.Killed(MonsterKind.Ogre, TownKillSource.None, boss: true));
            TownSave.NoteBossKilled(c);
            Ev(c, QuestEvent.Ascended(2));
            UpToTown(c);
            return c;
        }

        /// <summary>꾸러미 글에서 장비 줄(eq.*·bag)을 뺀 나머지.</summary>
        static string WithoutGear(string text)
        {
            var kept = new List<string>();
            foreach (var line in text.Split('\n'))
                if (!line.StartsWith("eq.", StringComparison.Ordinal) && !line.StartsWith("bag=", StringComparison.Ordinal)) kept.Add(line);
            return string.Join("\n", kept);
        }

        [TestCase(TestQuestStage.Fresh)]
        [TestCase(TestQuestStage.AfterFirstReturn)]
        [TestCase(TestQuestStage.ArcherDone)]
        [TestCase(TestQuestStage.OgreAccepted)]
        [TestCase(TestQuestStage.OgreKilled)]
        public void StageMatchesOfficialFlowByHand(TestQuestStage stage)
        {
            string built = WithoutGear(Built(stage).ToText());
            string manual = WithoutGear(Manual(stage).ToText());
            Assert.AreEqual(manual, built, stage + ": 정식 흐름을 손으로 밟은 꾸러미와 같아야 함");
        }

        [Test]
        public void TalkPlansPickTheSameScenesAsTheOfficialTalk()
        {
            // 첫 귀환 뒤 춘삼: 내려가기 보고 → 버팀목 길 받기. 무진은 내려가기 보고(지급) 전에는 받을 의뢰가 없고(반복 대사), 보고 뒤 궁수 받기.
            var c = Built(TestQuestStage.AfterFirstReturn);
            var gate = TalkDirector.Build(NpcTable.Gate, c, QuestContext.Live);
            CollectionAssert.AreEqual(new[] { TownScript.GateReportDescend.Id, TownScript.GateOfferFloor2.Id }, SceneIds(gate));
            Assert.AreEqual(TalkSceneKind.Repeat, TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live).Scenes[0].Kind, "춘삼 보고 전 무진");
            foreach (var s in gate.Scenes) Skip(c, s);
            CollectionAssert.AreEqual(new[] { TownScript.TrainerOfferWindblade.Id }, SceneIds(TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live)));

            // 궁수까지 끝 무진: 궁수 보고(이름 공개) → 같은 대화에서 '굴의 큰 놈' 받기. 옥금: 굴쥐 보고.
            c = Built(TestQuestStage.ArcherDone);
            CollectionAssert.AreEqual(new[] { TownScript.TrainerReportWindblade.Id, TownScript.TrainerOfferOgre.Id }, SceneIds(TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live)));
            CollectionAssert.AreEqual(new[] { TownScript.SmithReportRats.Id }, SceneIds(TalkDirector.Build(NpcTable.Smith, c, QuestContext.Live)));

            // 오우거 처치 뒤 무진: '굴의 큰 놈' 보고.
            c = Built(TestQuestStage.OgreKilled);
            CollectionAssert.AreEqual(new[] { TownScript.TrainerReportOgre.Id, TownScript.OgreAfter.Id, TownScript.TrainerOfferSlam.Id }, SceneIds(TalkDirector.Build(NpcTable.Trainer, c, QuestContext.Live)));
        }

        static List<string> SceneIds(TalkPlan plan)
        {
            var ids = new List<string>();
            foreach (var s in plan.Scenes) ids.Add(s.Id);
            return ids;
        }

        [Test]
        public void NoRepeatTalkIsCounted()
        {
            foreach (var stage in Stages)
            {
                var c = Built(stage);
                foreach (var npc in NpcTable.All)
                    Assert.AreEqual(0, TownSave.RepeatIndex(c, npc.Id), stage + " " + npc.Id + ": 반복 대사 차례가 오르지 않음");
            }
        }

        // ── (b) 기대 상태 표 ──

        static void AssertQuest(CarryData c, string id, QuestState state, string why)
        {
            Assert.AreEqual(state, new QuestBook(c).State(id), why + " " + id);
        }

        [Test]
        public void FreshIsNewProfile()
        {
            var c = Built(TestQuestStage.Fresh);
            Assert.AreEqual(0, c.Counters.Count, "키 없음");
            Assert.AreEqual(1, c.Expedition);
            Assert.AreEqual(NightEvent.None, c.Night);
            Assert.AreEqual(Salt, c.ProfileSalt);
            Assert.AreEqual(1, c.Level);
            Assert.AreEqual(0, c.TotalXp);
            Assert.AreEqual(0, c.Stones);
            Assert.AreEqual(0, c.Gold);
            Assert.AreEqual(0, c.VisitedFloors.Count);
            Assert.AreEqual(0, c.LitLandings.Count);
            Assert.AreEqual(1, c.RopeDepth);
            Assert.IsNull(c.Leg);
        }

        [Test]
        public void AfterFirstReturnState()
        {
            var c = Built(TestQuestStage.AfterFirstReturn);
            var book = new QuestBook(c);
            Assert.AreEqual(2, c.Expedition);
            Assert.AreEqual(NightEvent.FirstNight, c.Night);
            Assert.AreEqual(2, TownSave.Visits(c));
            Assert.AreEqual(1, TownSave.Departures(c));
            Assert.AreEqual(2, TownSave.LastArrival(c));
            AssertQuest(c, QuestTable.GateDescend, QuestState.Achieved, "첫 귀환 뒤");
            Assert.AreEqual(1, book.Step(QuestTable.GateDescend), "내려가기 단계 1(올라오기)");
            Assert.AreEqual(1, book.Count(QuestTable.GateDescend));
            AssertQuest(c, QuestTable.SmithRats, QuestState.Active, "첫 귀환 뒤");
            Assert.AreEqual(0, book.Count(QuestTable.SmithRats), "굴쥐 0/8");
            AssertQuest(c, QuestTable.GateFloor2, QuestState.Locked, "첫 귀환 뒤");
            AssertQuest(c, QuestTable.TrainerWindblade, QuestState.Locked, "첫 귀환 뒤");
            AssertQuest(c, QuestTable.TrainerOgre, QuestState.Locked, "첫 귀환 뒤");
            Assert.IsTrue(TownSave.KnowsName(c, NpcTable.Gate));
            Assert.IsTrue(TownSave.KnowsName(c, NpcTable.Smith));
            Assert.IsFalse(TownSave.KnowsName(c, NpcTable.Trainer));
            Assert.IsTrue(TownSave.SeenScene(c, TownScript.OpeningId), "sc:scene.opening");
            CollectionAssert.AreEquivalent(new[] { 1 }, c.VisitedFloors);
            CollectionAssert.AreEquivalent(new[] { 1 }, c.LitLandings);
            Assert.AreEqual(1, c.RopeDepth);
            Assert.AreEqual(1, c.DeepestFloor);
            Assert.AreEqual(0, c.Stones, "보고 전이라 보상 없음");
            Assert.AreEqual(0, c.TotalXp);
            Assert.IsNull(c.Leg, "마을에서는 원정 몫이 없다");
        }

        [Test]
        public void ArcherDoneState()
        {
            var c = Built(TestQuestStage.ArcherDone);
            Assert.AreEqual(3, c.Expedition);
            Assert.AreEqual(ExpeditionSeeds.NightBefore(Salt, 3), c.Night);
            Assert.AreEqual(3, TownSave.Visits(c));
            Assert.AreEqual(2, TownSave.Departures(c));
            AssertQuest(c, QuestTable.GateDescend, QuestState.Rewarded, "궁수까지 끝");
            AssertQuest(c, QuestTable.SmithRats, QuestState.Achieved, "궁수까지 끝");
            AssertQuest(c, QuestTable.GateFloor2, QuestState.Achieved, "궁수까지 끝");
            AssertQuest(c, QuestTable.TrainerWindblade, QuestState.Achieved, "궁수까지 끝");
            AssertQuest(c, QuestTable.TrainerOgre, QuestState.Locked, "궁수까지 끝(보고 전)");
            Assert.IsTrue(TownSave.HasMilestone(c, TownSave.MilestoneStairsF2), "ms:stairs.f2");
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, c.VisitedFloors);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, c.LitLandings);
            Assert.AreEqual(2, c.RopeDepth);
            Assert.AreEqual(2, c.DeepestFloor);
            Assert.AreEqual(3, c.Stones);
            Assert.AreEqual(30, c.Gold);
            Assert.AreEqual(40, c.TotalXp);
            Assert.AreEqual(1, c.Level);
            Assert.AreEqual(3, new QuestBook(c).UnreportedCount, "알릴 일 셋");
        }

        [Test]
        public void OgreAcceptedState()
        {
            var c = Built(TestQuestStage.OgreAccepted);
            Assert.AreEqual(3, c.Expedition, "대화만 했다");
            Assert.AreEqual(3, TownSave.Visits(c));
            Assert.AreEqual(2, TownSave.Departures(c));
            AssertQuest(c, QuestTable.GateDescend, QuestState.Rewarded, "오우거 받음");
            AssertQuest(c, QuestTable.SmithRats, QuestState.Rewarded, "오우거 받음");
            AssertQuest(c, QuestTable.GateFloor2, QuestState.Rewarded, "오우거 받음");
            AssertQuest(c, QuestTable.TrainerWindblade, QuestState.Rewarded, "오우거 받음");
            AssertQuest(c, QuestTable.TrainerOgre, QuestState.Active, "오우거 받음");
            Assert.AreEqual(14, c.Stones);
            Assert.AreEqual(140, c.Gold);
            Assert.AreEqual(176, c.TotalXp);
            Assert.AreEqual(1, c.Level);
            Assert.AreEqual(0, c.SkillPoints);
            Assert.AreEqual(0, BossLedger.Kills(c, OgreDen.BossId));
        }

        [Test]
        public void OgreKilledState()
        {
            var c = Built(TestQuestStage.OgreKilled);
            Assert.AreEqual(4, c.Expedition);
            Assert.AreEqual(4, TownSave.Visits(c));
            Assert.AreEqual(3, TownSave.Departures(c));
            AssertQuest(c, QuestTable.TrainerOgre, QuestState.Achieved, "오우거 처치 뒤");
            AssertQuest(c, QuestTable.TrainerWindblade, QuestState.Rewarded, "오우거 처치 뒤");
            Assert.AreEqual(1, BossLedger.Kills(c, OgreDen.BossId));
            Assert.AreEqual(1, c.BossKills[OgreDen.BossId]);
            Assert.AreEqual(3, c.BossKillExpedition[OgreDen.BossId], "원정 3에 잡음");
            CollectionAssert.AreEquivalent(new[] { OgreDen.FrontStakeId }, c.BossStakes);
            Assert.AreEqual(0, c.BossLosses.Count);
            Assert.IsFalse(TownSave.HasReaction(c, TownSave.RxOgreLost));
            Assert.IsFalse(BossLedger.FrontLandingOpen(c), "첫 처치 뒤 '보스방 앞'은 닫힘");
            Assert.AreEqual(14, c.Stones, "오우거 보고 전");
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, c.LitLandings, "굴은 승강장을 켜지 않음");
        }

        // ── (c) 이름 공개 ──

        [Test]
        public void NamesFollowStage()
        {
            foreach (var stage in Stages)
            {
                var c = Built(stage);
                bool gateSmith = stage != TestQuestStage.Fresh;
                bool trainer = stage == TestQuestStage.OgreAccepted || stage == TestQuestStage.OgreKilled;
                Assert.AreEqual(gateSmith ? "춘삼" : SpeakerIdentity.Unknown, SpeakerIdentity.Label(c, NpcTable.Gate), stage + " 춘삼");
                Assert.AreEqual(gateSmith ? "옥금" : SpeakerIdentity.Unknown, SpeakerIdentity.Label(c, NpcTable.Smith), stage + " 옥금");
                Assert.AreEqual(trainer ? "무진" : SpeakerIdentity.Unknown, SpeakerIdentity.Label(c, NpcTable.Trainer), stage + " 무진");
            }
        }

        // ── (d) 시작 위치 ──

        [Test]
        public void TownStarts()
        {
            var fresh = Plan(TestStartAt.TownFresh, TestQuestStage.OgreKilled);
            Assert.AreEqual(TestStartScene.Town, fresh.Scene);
            Assert.AreEqual(TestQuestStage.Fresh, fresh.Preset.EffectiveStage, "마을 처음은 단계를 무시");
            Assert.AreEqual(0, fresh.Carry.Counters.Count);
            Assert.AreEqual(1, fresh.Carry.Expedition);
            Assert.IsTrue(fresh.TownNewPlay);
            Assert.IsTrue(fresh.FirstStart);
            Assert.AreEqual(0, fresh.Floor);
            Assert.IsFalse(fresh.Den);
            Assert.IsNull(fresh.ForcedSeed);

            var townFresh = Plan(TestStartAt.Town, TestQuestStage.Fresh);
            Assert.IsTrue(townFresh.TownNewPlay, "마을 + 단계 '처음' = 마을 처음");
            Assert.AreEqual(fresh.Carry.ToText(), townFresh.Carry.ToText());

            var town = Plan(TestStartAt.Town, TestQuestStage.ArcherDone, p => { p.FixSeed = true; p.Seed = 5; });
            Assert.AreEqual(TestStartScene.Town, town.Scene);
            Assert.IsFalse(town.TownNewPlay, "바구니 도착 쪽지");
            Assert.IsFalse(town.FirstStart);
            Assert.IsNull(town.ForcedSeed, "마을은 지도 씨앗 없음");
            Assert.AreEqual(2, TownSave.Departures(town.Carry), "마을 시작은 출발을 적지 않음");
        }

        [Test]
        public void DungeonStartFromFreshMakesNoTownKeys()
        {
            var plan = Plan(TestStartAt.Floor1, TestQuestStage.Fresh);
            Assert.AreEqual(TestStartScene.Dungeon, plan.Scene);
            Assert.AreEqual(1, plan.Floor);
            Assert.IsFalse(plan.Den);
            Assert.IsFalse(plan.DenFight);
            Assert.IsTrue(plan.FirstStart, "원정 1 = FirstStart");
            Assert.IsFalse(plan.TownNewPlay);
            Assert.AreEqual(0, plan.Carry.Counters.Count, "DungeonTest 바로 Play처럼 마을 키 없음");
            Assert.AreEqual(1, plan.Carry.RopeDepth);
        }

        [TestCase(TestStartAt.Floor1)]
        [TestCase(TestStartAt.Floor2)]
        [TestCase(TestStartAt.DenFront)]
        [TestCase(TestStartAt.DenFight)]
        public void DungeonStartAddsOneDeparture(TestStartAt at)
        {
            foreach (var stage in Stages)
            {
                if (stage == TestQuestStage.Fresh) continue;
                var town = Built(stage);
                var plan = Plan(at, stage, p => { p.Level = 0; p.AutoSkills = false; });
                Assert.AreEqual(TownSave.Departures(town) + 1, TownSave.Departures(plan.Carry), at + " " + stage + ": 출발 +1");
                Assert.AreEqual(town.Expedition, plan.Carry.Expedition, "원정 번호는 그대로");
                Assert.IsFalse(plan.FirstStart, "원정 2부터는 바구니 도착");
            }
        }

        [Test]
        public void FloorTwoStart()
        {
            var plan = Plan(TestStartAt.Floor2, TestQuestStage.Fresh, p => { p.FixSeed = true; p.Seed = 77; });
            Assert.AreEqual(TestStartScene.Dungeon, plan.Scene);
            Assert.AreEqual(2, plan.Floor);
            Assert.IsFalse(plan.Den);
            Assert.AreEqual(77UL, plan.ForcedSeed);
            Assert.AreEqual(77UL, plan.Carry.ProfileSalt);
            Assert.GreaterOrEqual(plan.Carry.RopeDepth, 2);
            Assert.IsTrue(plan.Carry.VisitedFloors.Contains(1));
            Assert.IsTrue(plan.Carry.LitLandings.Contains(1));
            Assert.IsFalse(plan.Carry.VisitedFloors.Contains(2), "2층은 처음 밟는 층(장면이 적음)");
            Assert.GreaterOrEqual(plan.Carry.DeepestFloor, 1);
            Assert.IsFalse(BossLedger.StakeLit(plan.Carry, OgreDen.FrontStakeId));
        }

        [TestCase(TestStartAt.DenFront, false)]
        [TestCase(TestStartAt.DenFight, true)]
        public void DenStarts(TestStartAt at, bool fight)
        {
            foreach (var stage in Stages)
            {
                var plan = Plan(at, stage, p => { p.FixSeed = true; p.Seed = 9; });
                Assert.AreEqual(TestStartScene.Dungeon, plan.Scene);
                Assert.AreEqual(OgreDen.Floor, plan.Floor);
                Assert.IsTrue(plan.Den);
                Assert.AreEqual(fight, plan.DenFight);
                Assert.IsNull(plan.ForcedSeed, "굴은 고정 칸이라 지도 씨앗 없음");
                Assert.GreaterOrEqual(plan.Carry.RopeDepth, 2);
                Assert.IsTrue(plan.Carry.VisitedFloors.IsSupersetOf(new[] { 1, 2 }), stage + " 밟은 층 ⊇ {1,2}");
                Assert.IsTrue(plan.Carry.LitLandings.IsSupersetOf(new[] { 1, 2 }));
                Assert.GreaterOrEqual(plan.Carry.DeepestFloor, 2);
                Assert.IsTrue(BossLedger.StakeLit(plan.Carry, OgreDen.FrontStakeId), stage + " 굴 앞 말뚝 켬");
                Assert.AreEqual(stage == TestQuestStage.Fresh, plan.FirstStart);
            }
        }

        [Test]
        public void VisitedFloorsHavePouchAndGlyphs()
        {
            // 장면은 처음 밟는 층에서만 발견 주머니를 적는다(DungeonRoot.BeginPlay). 꾸러미가 '밟음'으로 적은 층에 주머니가 없으면 상한이 영영 걸리지 않는다.
            foreach (var at in Starts)
                foreach (var stage in Stages)
                {
                    var c = Plan(at, stage).Carry;
                    foreach (int floor in c.VisitedFloors)
                    {
                        Assert.IsTrue(c.DiscoveryPouch.ContainsKey(floor), at + " " + stage + ": " + floor + "층 발견 주머니");
                        Assert.Greater(c.DiscoveryPouch[floor], 0, at + " " + stage + ": " + floor + "층 주머니 > 0");
                        Assert.IsTrue(c.LastGlyphs.ContainsKey(floor) && c.LastGlyphs[floor].Length > 0, at + " " + stage + ": " + floor + "층 지도 글자");
                    }
                    Assert.AreEqual(0, c.DiscoveryXpGiven.Count, at + " " + stage + ": 발견 경험치는 넣지 않음(받은 몫 없음)");
                    foreach (int floor in c.DiscoveryPouch.Keys) Assert.IsTrue(c.VisitedFloors.Contains(floor), "주머니는 밟은 층에만");
                }
        }

        [Test]
        public void PouchIsTheFirstVisitMap()
        {
            // 첫 방문 지도는 고른 씨앗(1층 = 손 지도)이라 프로필 소금과 상관없이 같은 주머니다.
            foreach (int floor in new[] { 1, 2 })
            {
                var recipe = FloorRecipe.For(floor);
                var first = FloorGenerator.Generate(new GeneratorInput { Floor = floor, Seed = recipe.ChosenSeed, FirstVisit = true, DeepestFloor = floor }).Build();
                int expect = PouchByHand(first, floor);
                Assert.AreEqual(expect, TestStartBuilder.DiscoveryPouchOf(first, floor), floor + "층 고른 지도");
                Assert.Greater(expect, first.Cells.Count * XpRules.ForDiscovery(DiscoveryKind.NewCell, floor), floor + "층: 칸 말고도 등잔·궤짝·말뚝이 듦");
                foreach (ulong salt in new[] { 1UL, Salt, 0xABCDEFUL })
                {
                    var c = TestStartBuilder.Build(TownAt(TestQuestStage.ArcherDone), salt).Carry;
                    Assert.AreEqual(expect, c.DiscoveryPouch[floor], floor + "층 소금 " + salt);
                }
            }

            // 첫 귀환 뒤 1층 글자 = 손 지도(첫 방문), 궁수까지 끝 1층 글자 = 원정 2 지도(다시 연 층).
            Assert.AreEqual(FloorOneMap.Glyphs, Built(TestQuestStage.AfterFirstReturn).LastGlyphs[1]);
            Assert.AreNotEqual(FloorOneMap.Glyphs, Built(TestQuestStage.ArcherDone).LastGlyphs[1], "원정 2의 1층은 새 지도");

            // 생성 지도 몇 개도 손 셈과 같다.
            for (ulong seed = 1; seed <= 6; seed++)
            {
                var map = FloorGenerator.Generate(new GeneratorInput { Floor = 2, Seed = seed, FirstVisit = false, DeepestFloor = 2 }).Build();
                Assert.AreEqual(PouchByHand(map, 2), TestStartBuilder.DiscoveryPouchOf(map, 2), "2층 씨앗 " + seed);
            }
        }

        [Test]
        public void CombatTestStart()
        {
            var plan = Plan(TestStartAt.CombatTest, TestQuestStage.OgreAccepted, p =>
            {
                p.CombatPreset = "Boss";
                p.CombatFloor = 4;
                p.FixSeed = true;
            });
            Assert.AreEqual(TestStartScene.CombatTest, plan.Scene);
            Assert.AreEqual(0, plan.Floor);
            Assert.IsFalse(plan.Den);
            Assert.IsNull(plan.ForcedSeed);
            Assert.AreEqual("Boss", plan.CombatPreset);
            Assert.AreEqual(4, plan.CombatFloor);
            Assert.AreEqual(2, TownSave.Departures(plan.Carry), "전투 시험장은 출발을 적지 않음");
        }

        [Test]
        public void ForcedSeedOnlyForFixedNonDenDungeon()
        {
            foreach (var at in Starts)
            {
                var off = Plan(at, TestQuestStage.AfterFirstReturn);
                Assert.IsNull(off.ForcedSeed, at + ": 씨앗 고정이 꺼지면 없음");
                var on = Plan(at, TestQuestStage.AfterFirstReturn, p => { p.FixSeed = true; p.Seed = 123; });
                bool expect = at == TestStartAt.Floor1 || at == TestStartAt.Floor2;
                Assert.AreEqual(expect ? (ulong?)123UL : null, on.ForcedSeed, at.ToString());
                Assert.AreEqual(at == TestStartAt.TownFresh || at == TestStartAt.Town, on.Scene == TestStartScene.Town, at + " 장면");
                Assert.IsFalse(string.IsNullOrEmpty(TestStartBuilder.StartLabel(at)));
                StringAssert.StartsWith("시험 시작: ", on.Summary());
            }
        }

        // ── (e) 장비 ──

        static TestStartPreset Gear(TestGearSet set, Action<TestStartPreset> edit = null)
        {
            var p = new TestStartPreset { GearSet = set, GearLevel = 4, WeaponId = GearBaseTable.Axe, ArmorWeight = ArmorWeight.Heavy, LegendRoll = 650 };
            edit?.Invoke(p);
            return p;
        }

        static void On(TestStartPreset p, params LegendaryEffect[] effects)
        {
            foreach (var e in effects) p.LegendOn[(int)e] = true;
        }

        [Test]
        public void StartingGearIsTheOfficialStartingSetWithChosenWeapon()
        {
            var p = Gear(TestGearSet.Starting, x => On(x, LegendaryEffect.ChainLightning, LegendaryEffect.FlameSteps, LegendaryEffect.ChainBlast));
            var gear = TestStartBuilder.BuildGear(p);
            Assert.AreEqual(GearSlots.SlotCount, gear.Length);
            Assert.AreEqual(GearBaseTable.Axe, gear[(int)GearSlot.Weapon].BaseId);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsNotNull(gear[i]);
                Assert.AreEqual(Grade.Common, gear[i].Grade);
                Assert.AreEqual(1, gear[i].ItemLevel, "시작 장비는 iLv1 그대로");
                Assert.AreEqual(1000, gear[i].RollPermille);
                Assert.IsFalse(gear[i].IsLegendary, "시작 장비는 전설 무시");
                Assert.AreEqual(0, gear[i].Options.Count);
                if (i > 0) Assert.AreEqual(GearItem.Starting((GearSlot)i).BaseId, gear[i].BaseId);
            }
            Assert.IsNull(gear[(int)GearSlot.Ring1]);
            Assert.IsNull(gear[(int)GearSlot.Ring2]);
            Assert.IsNull(gear[(int)GearSlot.Amulet]);
            CollectionAssert.AreEqual(new[] { -1, -1, -1 }, LegendaryTable.Active(gear));

            var plan = TestStartBuilder.Build(p, Salt);
            Assert.AreEqual(0, plan.Carry.Bag.Count, "가방은 비움");
            for (int i = 0; i < GearSlots.SlotCount; i++)
                Assert.AreEqual(CarryData.GearText(gear[i]), CarryData.GearText(plan.Carry.Equipment[i]));
        }

        [TestCase(TestGearSet.Common, Grade.Common)]
        [TestCase(TestGearSet.Uncommon, Grade.Uncommon)]
        [TestCase(TestGearSet.Rare, Grade.Rare)]
        [TestCase(TestGearSet.Epic, Grade.Epic)]
        public void PlainSetsFillEightSlots(TestGearSet set, Grade grade)
        {
            var gear = TestStartBuilder.BuildGear(Gear(set));
            Assert.AreEqual(GearSlots.SlotCount, gear.Length);
            foreach (var slot in GearSlots.All)
            {
                var g = gear[(int)slot];
                Assert.IsNotNull(g, slot + " 자리");
                Assert.AreEqual(grade, g.Grade, slot.ToString());
                Assert.AreEqual(4, g.ItemLevel, slot + " iLv = 장비 층");
                Assert.AreEqual(1000, g.RollPermille);
                Assert.AreEqual(0, g.Enhance);
                Assert.IsFalse(g.IsLegendary);
                Assert.AreEqual(GearSlots.PartOf(slot), g.Part);
                Assert.AreEqual(OptionTable.CountFor(grade), g.Options.Count, slot + " 옵션 줄 수");
            }
            Assert.AreEqual(GearBaseTable.Axe, gear[(int)GearSlot.Weapon].BaseId);
            foreach (var slot in new[] { GearSlot.Armor, GearSlot.Helm, GearSlot.Gloves, GearSlot.Boots })
                Assert.AreEqual(ArmorWeight.Heavy, gear[(int)slot].Base.Weight, slot + " 판금");
            Assert.AreEqual(GearBaseTable.IronRing, gear[(int)GearSlot.Ring1].BaseId);
            Assert.AreEqual(GearBaseTable.BloodRing, gear[(int)GearSlot.Ring2].BaseId);
            Assert.AreEqual(GearBaseTable.FangAmulet, gear[(int)GearSlot.Amulet].BaseId);
        }

        [Test]
        public void ArmorWeightPicksMatchingBases()
        {
            foreach (var w in new[] { ArmorWeight.Light, ArmorWeight.Medium, ArmorWeight.Heavy })
            {
                var gear = TestStartBuilder.BuildGear(Gear(TestGearSet.Common, p => p.ArmorWeight = w));
                foreach (var slot in new[] { GearSlot.Armor, GearSlot.Helm, GearSlot.Gloves, GearSlot.Boots })
                    Assert.AreEqual(w, gear[(int)slot].Base.Weight, w + " " + slot);
            }
            var none = TestStartBuilder.BuildGear(Gear(TestGearSet.Common, p => p.ArmorWeight = ArmorWeight.None));
            Assert.AreEqual(GearBaseTable.LeatherArmor, none[(int)GearSlot.Armor].BaseId, "None은 가죽");
        }

        [Test]
        public void PlainSetWithLightningMakesOnlyWeaponLegendary()
        {
            var gear = TestStartBuilder.BuildGear(Gear(TestGearSet.Common, p => On(p, LegendaryEffect.ChainLightning)));
            var weapon = gear[(int)GearSlot.Weapon];
            Assert.AreEqual(Grade.Legendary, weapon.Grade);
            Assert.AreEqual(LegendaryTable.ChainLightningId, weapon.LegendaryId);
            Assert.AreEqual(650, weapon.LegendaryRollPermille, "세기 = LegendRoll");
            Assert.AreEqual(GearBaseTable.Axe, weapon.BaseId);
            Assert.AreEqual(OptionTable.CountFor(Grade.Legendary), weapon.Options.Count);
            foreach (var slot in GearSlots.All)
            {
                if (slot == GearSlot.Weapon) continue;
                Assert.AreEqual(Grade.Common, gear[(int)slot].Grade, slot.ToString());
                Assert.IsFalse(gear[(int)slot].IsLegendary, slot.ToString());
            }
            CollectionAssert.AreEqual(new[] { 650, -1, -1 }, LegendaryTable.Active(gear));
        }

        [Test]
        public void PlainSetPutsEachEffectOnItsRepresentativeSlot()
        {
            var gear = TestStartBuilder.BuildGear(Gear(TestGearSet.Rare,
                p => On(p, LegendaryEffect.ChainLightning, LegendaryEffect.FlameSteps, LegendaryEffect.ChainBlast)));
            Assert.AreEqual(LegendaryTable.ChainLightningId, gear[(int)GearSlot.Weapon].LegendaryId);
            Assert.AreEqual(LegendaryTable.FlameStepsId, gear[(int)GearSlot.Boots].LegendaryId);
            Assert.AreEqual(LegendaryTable.ChainBlastId, gear[(int)GearSlot.Amulet].LegendaryId);
            int legendary = 0;
            foreach (var g in gear)
            {
                if (g.IsLegendary) legendary++;
                else Assert.AreEqual(Grade.Rare, g.Grade);
                Assert.AreEqual(OptionTable.CountFor(g.Grade), g.Options.Count);
            }
            Assert.AreEqual(3, legendary);
            CollectionAssert.AreEqual(new[] { 650, 650, 650 }, LegendaryTable.Active(gear));
        }

        [Test]
        public void LegendarySetAllOn()
        {
            var gear = TestStartBuilder.BuildGear(Gear(TestGearSet.Legendary, p =>
            {
                p.LegendRoll = 700;
                On(p, LegendaryEffect.ChainLightning, LegendaryEffect.FlameSteps, LegendaryEffect.ChainBlast);
            }));
            var expect = new Dictionary<GearSlot, string>
            {
                [GearSlot.Weapon] = LegendaryTable.ChainLightningId,
                [GearSlot.Gloves] = LegendaryTable.ChainLightningId,
                [GearSlot.Armor] = LegendaryTable.FlameStepsId,
                [GearSlot.Boots] = LegendaryTable.FlameStepsId,
                [GearSlot.Helm] = LegendaryTable.ChainBlastId,
                [GearSlot.Ring1] = LegendaryTable.ChainBlastId,
                [GearSlot.Ring2] = LegendaryTable.ChainBlastId,
                [GearSlot.Amulet] = LegendaryTable.ChainBlastId,
            };
            foreach (var slot in GearSlots.All)
            {
                var g = gear[(int)slot];
                Assert.AreEqual(Grade.Legendary, g.Grade, slot.ToString());
                Assert.AreEqual(expect[slot], g.LegendaryId, slot.ToString());
                Assert.AreEqual(700, g.LegendaryRollPermille);
                Assert.AreEqual(OptionTable.CountFor(Grade.Legendary), g.Options.Count);
                Assert.IsTrue(LegendaryTable.CanAppearOn(g.Legendary.Value, g.Part), slot + " 그 부위에 나올 수 있는 효과");
            }
            CollectionAssert.AreEqual(new[] { 700, 700, 700 }, LegendaryTable.Active(gear));
        }

        [Test]
        public void LegendarySetWithFlameStepsOffMakesArmorAndBootsEpic()
        {
            var gear = TestStartBuilder.BuildGear(Gear(TestGearSet.Legendary, p => On(p, LegendaryEffect.ChainLightning, LegendaryEffect.ChainBlast)));
            foreach (var slot in new[] { GearSlot.Armor, GearSlot.Boots })
            {
                Assert.AreEqual(Grade.Epic, gear[(int)slot].Grade, slot.ToString());
                Assert.IsFalse(gear[(int)slot].IsLegendary, slot.ToString());
                Assert.AreEqual(OptionTable.CountFor(Grade.Epic), gear[(int)slot].Options.Count);
            }
            Assert.AreEqual(Grade.Legendary, gear[(int)GearSlot.Gloves].Grade);
            Assert.AreEqual(Grade.Legendary, gear[(int)GearSlot.Helm].Grade);
            CollectionAssert.AreEqual(new[] { 650, -1, 650 }, LegendaryTable.Active(gear));

            var none = TestStartBuilder.BuildGear(Gear(TestGearSet.Legendary));
            foreach (var g in none) Assert.AreEqual(Grade.Epic, g.Grade, "효과를 모두 끄면 영웅 한 벌");
        }

        [Test]
        public void SameSettingsGiveSameGear()
        {
            Func<TestStartPreset> make = () => Gear(TestGearSet.Legendary, p => On(p, LegendaryEffect.FlameSteps));
            var a = TestStartBuilder.BuildGear(make());
            var b = TestStartBuilder.BuildGear(make());
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(CarryData.GearText(a[i]), CarryData.GearText(b[i]));

            // 씨앗 고정 값이 기본 장비 씨앗과 같으면 고정하지 않은 것과 같은 굴림이다.
            var c = TestStartBuilder.BuildGear(Gear(TestGearSet.Legendary, p =>
            {
                On(p, LegendaryEffect.FlameSteps);
                p.FixSeed = true;
                p.Seed = TestStartBuilder.DefaultGearSeed;
            }));
            for (int i = 0; i < a.Length; i++) Assert.AreEqual(CarryData.GearText(a[i]), CarryData.GearText(c[i]));
        }

        [Test]
        public void GearSurvivesCarryText()
        {
            var plan = TestStartBuilder.Build(Gear(TestGearSet.Legendary, p => On(p, LegendaryEffect.ChainLightning, LegendaryEffect.ChainBlast)), Salt);
            var back = CarryData.FromText(plan.Carry.ToText());
            for (int i = 0; i < GearSlots.SlotCount; i++)
                Assert.AreEqual(CarryData.GearText(plan.Carry.Equipment[i]), CarryData.GearText(back.Equipment[i]), ((GearSlot)i).ToString());
        }

        // ── (f) 레벨·스킬·레벨 체력 ──

        static Loadout LoadoutOf(CarryData c)
        {
            var l = new Loadout();
            foreach (var slot in GearSlots.All)
            {
                var item = c.Equipment[(int)slot] ?? GearItem.Starting(slot);
                if (item != null) l.TryEquip(slot, item, out _);
            }
            return l;
        }

        static int Rank(CarryData c, SkillId id) => c.SkillRanks != null && (int)id < c.SkillRanks.Length ? c.SkillRanks[(int)id] : 0;

        static int RankSum(CarryData c)
        {
            int sum = 0;
            if (c.SkillRanks != null) foreach (int r in c.SkillRanks) sum += r;
            return sum;
        }

        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(5, true)]
        [TestCase(10, true)]
        [TestCase(5, false)]
        [TestCase(10, false)]
        public void LevelAndSkills(int level, bool auto)
        {
            foreach (var set in new[] { TestGearSet.Starting, TestGearSet.Common })
            {
                var plan = Plan(TestStartAt.Town, TestQuestStage.OgreAccepted, p =>
                {
                    p.Level = level;
                    p.AutoSkills = auto;
                    p.GearSet = set;
                });
                var c = plan.Carry;
                Assert.AreEqual(level, c.Level);
                // 오우거 받음은 보고 보상 176(레벨 1 구간)이라 레벨 1이면 그대로, 그 밖은 그 레벨 누계.
                Assert.AreEqual(level == 1 ? 176 : LevelTable.Cumulative(level), c.TotalXp);
                Assert.AreEqual(level, LevelTable.LevelFor(c.TotalXp), "누계 경험치가 그 레벨");
                Assert.AreEqual(SkillTree.Count, c.SkillRanks.Length);
                Assert.AreEqual(level - 1, c.SkillPoints + RankSum(c) + c.SkillNodes.Count, "점수 + 랭크 합 + 배운 칸 = 레벨 − 1");
                if (auto) Assert.AreEqual(0, c.SkillPoints, "자동 배분이면 점수가 남지 않음");
                else
                {
                    Assert.AreEqual(0, RankSum(c), "끄면 랭크 0");
                    Assert.AreEqual(level - 1, c.SkillPoints);
                }
                foreach (int r in c.SkillRanks) Assert.LessOrEqual(r, SkillDef.MaxRank);

                // 레벨 체력: 레벨 L과 1의 최대 체력 차이 = 레벨당 체력(질긴 몸 랭크) × (L − 1).
                int tough = Rank(c, SkillId.ToughBody);
                var loadout = LoadoutOf(c);
                int diff = StatCalc.Compute(loadout, c.Level, tough).MaxHp - StatCalc.Compute(loadout, 1, tough).MaxHp;
                Assert.AreEqual(LevelHp.PerLevelWith(tough) * (level - 1), diff, set + " 레벨 체력");
            }
        }

        [Test]
        public void AutoSkillsGoRoundInTreeOrder()
        {
            var c = Plan(TestStartAt.Town, TestQuestStage.Fresh, p => p.Level = 10).Carry;
            // 회오리·검풍을 먼저 배우고(2점), 남은 7점을 넓은 회오리 → 날 선 바람 → 질긴 몸 → 마무리 → 끓는 피 차례로 한 점씩.
            CollectionAssert.AreEqual(new[] { 2, 2, 1, 1, 1 }, c.SkillRanks, "넓은 회오리·날 선 바람·마무리·질긴 몸·끓는 피");
            CollectionAssert.IsSupersetOf(c.SkillNodes, new[] { "node.learn_whirl", "node.learn_wave" });
            c = Plan(TestStartAt.Town, TestQuestStage.Fresh, p => p.Level = 3).Carry;
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, c.SkillRanks, "Lv 3 두 점은 배우기에 쓴다");
            Assert.AreEqual(2, c.SkillNodes.Count);

            // 랭크 상한(4)에서 멈추고 남은 점수는 둔다.
            var big = CarryData.NewProfile(1UL);
            big.Level = 20;
            big.SkillPoints = 30;
            TestStartBuilder.AutoAllocateSkills(big);
            CollectionAssert.AreEqual(new[] { 4, 4, 4, 4, 4 }, big.SkillRanks);
            Assert.AreEqual(8, big.SkillPoints, "배우기 2 + 랭크 20 뒤 남은 점수(갈림 칸은 고르지 않는다)");

            // 레벨 2 미만이면 넣지 않는다.
            var low = CarryData.NewProfile(1UL);
            low.SkillPoints = 3;
            TestStartBuilder.AutoAllocateSkills(low);
            Assert.AreEqual(3, low.SkillPoints);
            Assert.AreEqual(0, RankSum(low));
        }

        [Test]
        public void LevelZeroKeepsStageProgress()
        {
            var plan = Plan(TestStartAt.Town, TestQuestStage.OgreAccepted, p => p.Level = 0);
            var town = Built(TestQuestStage.OgreAccepted);
            Assert.AreEqual(town.Level, plan.Carry.Level);
            Assert.AreEqual(town.TotalXp, plan.Carry.TotalXp, "단계대로(보고 보상 경험치)");
            Assert.AreEqual(town.SkillPoints, plan.Carry.SkillPoints);
        }

        [Test]
        public void ChosenLevelKeepsStageXpInsideItsBand()
        {
            // 오우거 받음(보고 보상 176, 레벨 1): 레벨 1을 골라도 176을 지키고(오우거 보고 뒤 272 → 레벨 2까지 68 남음), 레벨 5면 Cumulative(5).
            foreach (var stage in new[] { TestQuestStage.OgreAccepted, TestQuestStage.OgreKilled })
            {
                var one = Plan(TestStartAt.Town, stage, p => p.Level = 1).Carry;
                Assert.AreEqual(176, one.TotalXp, stage + " 레벨 1");
                Assert.AreEqual(1, one.Level);
                Assert.AreEqual(Built(stage).TotalXp, one.TotalXp, stage + " 레벨 1 = 단계대로 경험치");
                var five = Plan(TestStartAt.Town, stage, p => p.Level = 5).Carry;
                Assert.AreEqual(LevelTable.Cumulative(5), five.TotalXp, stage + " 레벨 5");
            }

            // 궁수까지 끝(40)·처음(0)도 레벨 1이면 그대로, 던전 시작도 같다.
            Assert.AreEqual(40, Plan(TestStartAt.Town, TestQuestStage.ArcherDone, p => p.Level = 1).Carry.TotalXp);
            Assert.AreEqual(0, Plan(TestStartAt.Town, TestQuestStage.Fresh, p => p.Level = 1).Carry.TotalXp);
            Assert.AreEqual(176, Plan(TestStartAt.DenFight, TestQuestStage.OgreAccepted, p => p.Level = 1).Carry.TotalXp);

            // 구간 위(레벨 2 누계 이상)를 레벨 1로 고르면 레벨 1 누계로 내린다(ApplyLevel 바로 부름).
            var rich = CarryData.NewProfile(1UL);
            rich.TotalXp = LevelTable.Cumulative(3) + 5;
            rich.Level = 3;
            TestStartBuilder.ApplyLevel(rich, 1, false);
            Assert.AreEqual(0, rich.TotalXp);
            Assert.AreEqual(1, rich.Level);
            TestStartBuilder.ApplyLevel(rich, 2, false);
            Assert.AreEqual(LevelTable.Cumulative(2), rich.TotalXp, "구간 아래면 그 레벨 누계로 올림");
        }

        // ── (g) 정식 저장 형식·되풀이 ──

        [Test]
        public void CarryTextRoundTrips()
        {
            foreach (var at in Starts)
                foreach (var stage in Stages)
                {
                    var plan = Plan(at, stage, p =>
                    {
                        p.Level = 6;
                        p.GearSet = TestGearSet.Epic;
                        p.GearLevel = 3;
                        On(p, LegendaryEffect.ChainBlast);
                    });
                    string text = plan.Carry.ToText();
                    Assert.AreEqual(text, CarryData.FromText(text).ToText(), at + " " + stage);
                    Assert.IsNull(plan.Carry.Leg, "원정 몫은 장면이 만든다");
                }
        }

        [Test]
        public void StonesAndGoldAreAdded()
        {
            var plain = Plan(TestStartAt.Town, TestQuestStage.OgreAccepted).Carry;
            var rich = Plan(TestStartAt.Town, TestQuestStage.OgreAccepted, p =>
            {
                p.AddStones = 50;
                p.AddGold = 1234;
            }).Carry;
            Assert.AreEqual(plain.Stones + 50, rich.Stones);
            Assert.AreEqual(plain.Gold + 1234, rich.Gold);
            Assert.AreEqual(14 + 50, rich.Stones);
        }

        [Test]
        public void SameSettingsAndSaltGiveSameText()
        {
            foreach (var stage in Stages)
            {
                var a = Plan(TestStartAt.Floor2, stage, p => p.GearSet = TestGearSet.Rare);
                var b = Plan(TestStartAt.Floor2, stage, p => p.GearSet = TestGearSet.Rare);
                Assert.AreEqual(a.Carry.ToText(), b.Carry.ToText(), stage.ToString());
            }

            var fixedA = TestStartBuilder.Build(new TestStartPreset { StartAt = TestStartAt.Town, Stage = TestQuestStage.OgreKilled, FixSeed = true, Seed = 4242 }, 1UL);
            var fixedB = TestStartBuilder.Build(new TestStartPreset { StartAt = TestStartAt.Town, Stage = TestQuestStage.OgreKilled, FixSeed = true, Seed = 4242 }, 999UL);
            Assert.AreEqual(fixedA.Carry.ToText(), fixedB.Carry.ToText(), "씨앗 고정이면 salt와 상관없음");
            Assert.AreEqual(4242UL, fixedA.Carry.ProfileSalt);

            var loose = TestStartBuilder.Build(new TestStartPreset(), 31UL);
            Assert.AreEqual(31UL, loose.Carry.ProfileSalt, "고정하지 않으면 salt가 프로필 소금");
        }

        [Test]
        public void BuildDoesNotChangeThePreset()
        {
            var p = new TestStartPreset { StartAt = TestStartAt.Floor2, Level = 99, GearLevel = 0, WeaponId = "nope" };
            p.LegendOn[1] = true;
            string before = p.ToText();
            var plan = TestStartBuilder.Build(p, Salt);
            Assert.AreEqual(before, p.ToText(), "원본 설정은 그대로");
            Assert.AreEqual(99, p.Level);
            Assert.AreNotSame(p, plan.Preset);
            Assert.AreEqual(TestStartPreset.MaxLevel, plan.Preset.Level, "계획에는 정리한 복사본");
            Assert.AreEqual(1, plan.Preset.GearLevel);
            Assert.AreEqual(GearBaseTable.Longsword, plan.Preset.WeaponId);
            Assert.AreEqual(TestStartPreset.MaxLevel, plan.Carry.Level);
        }

        [Test]
        public void LabelsAndSummary()
        {
            foreach (var at in Starts) Assert.IsFalse(string.IsNullOrEmpty(TestStartBuilder.StartLabel(at)), at.ToString());
            foreach (var stage in Stages)
            {
                Assert.IsFalse(string.IsNullOrEmpty(TestStartBuilder.StageLabel(stage)), stage.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(TestStartBuilder.StageNote(stage)), stage.ToString());
            }
            foreach (TestGearSet set in Enum.GetValues(typeof(TestGearSet)))
                Assert.IsFalse(string.IsNullOrEmpty(TestStartBuilder.GearSetLabel(set)), set.ToString());
            Assert.AreEqual("굴 안 바로 싸움", TestStartBuilder.StartLabel(TestStartAt.DenFight));
            Assert.AreEqual("궁수까지 끝(보고 전)", TestStartBuilder.StageLabel(TestQuestStage.ArcherDone));
            Assert.AreEqual("갱도지기(춘삼)·대장장이(옥금) 이름 앎 · 알릴 일: 갱도로 내려가기", TestStartBuilder.StageNote(TestQuestStage.AfterFirstReturn));
            Assert.AreEqual("희귀 한 벌", TestStartBuilder.GearSetLabel(TestGearSet.Rare));

            var plan = Plan(TestStartAt.Floor2, TestQuestStage.OgreAccepted, p =>
            {
                p.Level = 5;
                p.GearSet = TestGearSet.Rare;
                p.GearLevel = 2;
                p.WeaponId = GearBaseTable.Axe;
                p.Invincible = true;
            });
            Assert.AreEqual("시험 시작: 던전 2층 · 오우거 받음 · 레벨 5 · 희귀 한 벌 iLv2 · 도끼 · 무적", plan.Summary());

            // 전투 시험장: 의뢰 단계·레벨·장비 묶음은 쓰지 않아 빼고, 켠 전설(시험장 손잡이)은 적는다.
            var combat = Plan(TestStartAt.CombatTest, TestQuestStage.OgreAccepted, p =>
            {
                p.CombatPreset = "Boss";
                p.CombatFloor = 2;
                p.WeaponId = GearBaseTable.Axe;
                p.Level = 5;
                p.LegendOn[(int)LegendaryEffect.ChainLightning] = true;
            });
            Assert.AreEqual("시험 시작: 전투 시험장 Boss 2층 · 전설 연쇄 번개 · 도끼", combat.Summary());
        }

        // ── (h) 설정 글 ──

        static TestStartPreset Busy()
        {
            var p = new TestStartPreset
            {
                StartAt = TestStartAt.DenFight,
                Stage = TestQuestStage.OgreKilled,
                Level = 7,
                AutoSkills = false,
                WeaponId = GearBaseTable.Flail,
                GearSet = TestGearSet.Legendary,
                GearLevel = GearMath.MaxItemLevel,
                ArmorWeight = ArmorWeight.Heavy,
                LegendRoll = 333,
                Invincible = true,
                NoEnemies = true,
                DarknessOff = true,
                VisionOff = true,
                FixSeed = true,
                Seed = ulong.MaxValue,
                AddStones = 50,
                AddGold = TestStartPreset.MaxAdd,
                CombatPreset = "Boss=1,x",
                CombatFloor = FloorScaling.MaxFloor,
            };
            p.LegendOn[0] = true;
            p.LegendOn[2] = true;
            return p;
        }

        [Test]
        public void PresetTextRoundTrips()
        {
            var p = Busy();
            string text = p.ToText();
            StringAssert.StartsWith(TestStartPreset.Header + TestStartPreset.CurrentVersion + "\n", text);
            StringAssert.Contains("legend=1,0,1\n", text);
            StringAssert.Contains("start=DenFight\n", text);
            StringAssert.Contains("invincible=1\n", text);
            StringAssert.Contains("seed=" + ulong.MaxValue + "\n", text);
            Assert.IsTrue(TestStartPreset.TryParse(text, out var back));
            Assert.AreEqual(text, back.ToText());
            Assert.AreEqual(TestStartAt.DenFight, back.StartAt);
            Assert.AreEqual(ulong.MaxValue, back.Seed);
            Assert.AreEqual("Boss=1,x", back.CombatPreset, "나누는 글자는 %XX로 감쌌다 푼다");
            CollectionAssert.AreEqual(new[] { true, false, true }, back.LegendOn);

            var defaults = new TestStartPreset();
            Assert.IsTrue(TestStartPreset.TryParse(defaults.ToText(), out var d));
            Assert.AreEqual(defaults.ToText(), d.ToText());
            Assert.AreEqual(Busy().ToText(), Busy().ToText(), "같은 값이면 같은 글");
        }

        [Test]
        public void PresetTextRejectsBadHeader()
        {
            Assert.IsFalse(TestStartPreset.TryParse(null, out var p));
            Assert.IsNull(p);
            Assert.IsFalse(TestStartPreset.TryParse("", out p));
            Assert.IsNull(p);
            Assert.IsFalse(TestStartPreset.TryParse("carry v2\nlevel=3\n", out p), "다른 머리");
            Assert.IsNull(p);
            Assert.IsFalse(TestStartPreset.TryParse("testpreset vX\nlevel=3\n", out p), "판본이 숫자가 아님");
            Assert.IsNull(p);
            Assert.IsFalse(TestStartPreset.TryParse("level=3\ntestpreset v1\n", out p), "첫 줄이 머리가 아님");
        }

        [Test]
        public void PresetTextSkipsUnknownKeysAndBadValues()
        {
            string text = "testpreset v1\r\n" +
                          "foo=bar\n" +
                          "no equals sign\n" +
                          "level=abc\n" +
                          "start=Floor2\n" +
                          "stage=Nope\n" +
                          "gearset=3\n" +
                          "armor=Medium, Heavy\n" +
                          "weapon=wpn_axe\n" +
                          "legend=1,x,1\n" +
                          "seed=-5\n" +
                          "invincible=maybe\n" +
                          "noenemies=1\n" +
                          "gold=12\n";
            Assert.IsTrue(TestStartPreset.TryParse(text, out var p));
            Assert.AreEqual(TestStartAt.Floor2, p.StartAt);
            Assert.AreEqual(0, p.Level, "읽지 못한 값은 기본값");
            Assert.AreEqual(TestQuestStage.Fresh, p.Stage);
            Assert.AreEqual(TestGearSet.Starting, p.GearSet, "숫자 글은 enum 이름이 아님");
            Assert.AreEqual(ArmorWeight.Light, p.ArmorWeight);
            Assert.AreEqual(GearBaseTable.Axe, p.WeaponId);
            CollectionAssert.AreEqual(new[] { true, false, true }, p.LegendOn);
            Assert.AreEqual(1UL, p.Seed);
            Assert.IsFalse(p.Invincible);
            Assert.IsTrue(p.NoEnemies);
            Assert.AreEqual(12, p.AddGold);
        }

        [Test]
        public void NormalizeClampsValues()
        {
            var p = new TestStartPreset
            {
                Level = 99,
                GearLevel = 0,
                LegendRoll = 2000,
                AddStones = -1,
                AddGold = 999999,
                CombatFloor = 0,
                WeaponId = GearBaseTable.LeatherArmor,
                ArmorWeight = ArmorWeight.None,
                CombatPreset = "  ",
                StartAt = (TestStartAt)99,
                Stage = (TestQuestStage)(-1),
                GearSet = (TestGearSet)42,
            };
            Assert.AreSame(p, p.Normalize(), "자신을 돌려줌");
            Assert.AreEqual(TestStartPreset.MaxLevel, p.Level);
            Assert.AreEqual(1, p.GearLevel);
            Assert.AreEqual(1000, p.LegendRoll);
            Assert.AreEqual(0, p.AddStones);
            Assert.AreEqual(TestStartPreset.MaxAdd, p.AddGold);
            Assert.AreEqual(1, p.CombatFloor);
            Assert.AreEqual(GearBaseTable.Longsword, p.WeaponId, "무기가 아닌 id는 장검");
            Assert.AreEqual(ArmorWeight.Light, p.ArmorWeight);
            Assert.AreEqual(TestStartPreset.DefaultCombatPreset, p.CombatPreset);
            Assert.AreEqual(TestStartAt.TownFresh, p.StartAt);
            Assert.AreEqual(TestQuestStage.Fresh, p.Stage);
            Assert.AreEqual(TestGearSet.Starting, p.GearSet);

            var q = new TestStartPreset { Level = -3, GearLevel = 99, LegendRoll = -1, CombatFloor = 99, WeaponId = null }.Normalize();
            Assert.AreEqual(0, q.Level);
            Assert.AreEqual(GearMath.MaxItemLevel, q.GearLevel);
            Assert.AreEqual(0, q.LegendRoll);
            Assert.AreEqual(FloorScaling.MaxFloor, q.CombatFloor);
            Assert.AreEqual(GearBaseTable.Longsword, q.WeaponId);

            Assert.IsTrue(TestStartPreset.TryParse("testpreset v1\nlevel=50\ngearlevel=-2\nstones=9999999\n", out var r));
            Assert.AreEqual(TestStartPreset.MaxLevel, r.Level, "읽은 뒤 Normalize");
            Assert.AreEqual(1, r.GearLevel);
            Assert.AreEqual(TestStartPreset.MaxAdd, r.AddStones);
        }

        // ── (i) 룬(기획/세-무기-우클릭-소켓-1차.md 6장): '룬 +n', '시작 무기에 버팀 룬 끼우기'(기본 꺼짐) ──

        [Test]
        public void RunePresetDefaultsAddNothing()
        {
            var p = new TestStartPreset();
            Assert.AreEqual(0, p.AddRunes);
            Assert.IsFalse(p.StartWeaponRune, "기본 꺼짐");
            foreach (var at in Starts)
            {
                var plan = Plan(at, TestQuestStage.ArcherDone);
                Assert.AreEqual(0, plan.Carry.RunePouch.Count, at.ToString());
                Assert.AreEqual(0, plan.Carry.Equipment[(int)GearSlot.Weapon].Runes.Count, at.ToString());
                StringAssert.DoesNotContain("\nrunes=", plan.Carry.ToText(), "룬이 없으면 꾸러미 글에 주머니 줄 없음");
                StringAssert.DoesNotContain(RuneTable.SuperArmorId, plan.Carry.ToText());
            }
            StringAssert.DoesNotContain("룬", Plan(TestStartAt.Floor1, TestQuestStage.Fresh).Summary());
        }

        [Test]
        public void RunePresetFillsPouchAndStartWeapon()
        {
            foreach (var set in new[] { TestGearSet.Starting, TestGearSet.Rare, TestGearSet.Epic })
            {
                var plan = Plan(TestStartAt.Floor1, TestQuestStage.AfterFirstReturn, p =>
                {
                    p.GearSet = set;
                    p.WeaponId = GearBaseTable.Greatsword;
                    p.AddRunes = 3;
                    p.StartWeaponRune = true;
                });
                var carry = plan.Carry;
                Assert.AreEqual(3, carry.RuneCount(RuneTable.SuperArmorId), set + " 주머니 +3");
                var weapon = carry.Equipment[(int)GearSlot.Weapon];
                Assert.AreEqual(GearBaseTable.Greatsword, weapon.BaseId);
                CollectionAssert.AreEqual(new[] { RuneTable.SuperArmorId }, new List<string>(weapon.Runes), set + " 시작 무기에 버팀 룬");
                Assert.AreEqual(set == TestGearSet.Epic ? 1 : 0, weapon.FreeSockets, set + " 남은 홈");
                foreach (var slot in GearSlots.All)
                    if (slot != GearSlot.Weapon && carry.Equipment[(int)slot] != null)
                        Assert.AreEqual(0, carry.Equipment[(int)slot].Runes.Count, slot + ": 무기만");

                string text = carry.ToText();
                StringAssert.Contains("\nrunes=rune_superarmor:3\n", text);
                StringAssert.Contains("|rune_superarmor\n", text, "무기 장비 글 8번째 칸");
                var back = CarryData.FromText(text);
                Assert.AreEqual(text, back.ToText(), set + " 왕복");
                Assert.AreEqual(3, back.RuneCount(RuneTable.SuperArmorId));
                Assert.IsTrue(back.Equipment[(int)GearSlot.Weapon].HasRune(RuneTable.SuperArmorId));
            }

            var summary = Plan(TestStartAt.Floor2, TestQuestStage.OgreAccepted, p =>
            {
                p.AddRunes = 5;
                p.StartWeaponRune = true;
            }).Summary();
            StringAssert.EndsWith(" · 버팀 룬 +5 · 무기에 버팀 룬", summary);

            // 전투 시험장은 꾸러미 무기를 쓰지 않지만(시험 손잡이로 대신) 계획 꾸러미는 같은 규칙으로 만든다.
            var combat = Plan(TestStartAt.CombatTest, TestQuestStage.Fresh, p => p.StartWeaponRune = true);
            Assert.IsTrue(combat.Preset.StartWeaponRune);
            StringAssert.EndsWith("무기에 버팀 룬", combat.Summary());
        }

        [Test]
        public void ApplyRunesKeepsExistingRuneAndClampsPouch()
        {
            var carry = CarryData.NewProfile(1UL);
            carry.Equipment[(int)GearSlot.Weapon] = new GearItem(GearBaseTable.Greatsword, Grade.Common, 1, 1000, runes: new[] { RuneTable.SuperArmorId });
            carry.AddRunes(RuneTable.SuperArmorId, 98);
            var before = carry.Equipment[(int)GearSlot.Weapon];
            TestStartBuilder.ApplyRunes(carry, new TestStartPreset { AddRunes = 10, StartWeaponRune = true });
            Assert.AreSame(before, carry.Equipment[(int)GearSlot.Weapon], "이미 끼워져 있으면 그대로");
            Assert.AreEqual(RuneRules.PouchCap, carry.RuneCount(RuneTable.SuperArmorId), "주머니는 99까지");

            var empty = CarryData.NewProfile(2UL);
            TestStartBuilder.ApplyRunes(empty, new TestStartPreset { StartWeaponRune = true });
            Assert.IsNull(empty.Equipment[(int)GearSlot.Weapon], "무기 칸이 비었으면(시작 장비로 채울 자리) 아무것도 안 함");
            TestStartBuilder.ApplyRunes(null, new TestStartPreset());
            TestStartBuilder.ApplyRunes(empty, null);
        }

        [Test]
        public void RunePresetTextAndNormalize()
        {
            var p = new TestStartPreset { AddRunes = 12, StartWeaponRune = true };
            string text = p.ToText();
            StringAssert.Contains("\nrunes=12\n", text);
            StringAssert.Contains("\nstartrune=1\n", text);
            Assert.IsTrue(TestStartPreset.TryParse(text, out var back));
            Assert.AreEqual(12, back.AddRunes);
            Assert.IsTrue(back.StartWeaponRune);
            Assert.AreEqual(text, back.ToText());
            Assert.AreEqual(text, p.Clone().ToText(), "복사도 같은 값");

            StringAssert.Contains("\nrunes=0\n", new TestStartPreset().ToText());
            StringAssert.Contains("\nstartrune=0\n", new TestStartPreset().ToText());
            Assert.IsTrue(TestStartPreset.TryParse("testpreset v1\nlevel=3\n", out var old), "룬 키가 없는 옛 설정 글");
            Assert.AreEqual(0, old.AddRunes);
            Assert.IsFalse(old.StartWeaponRune);

            Assert.AreEqual(RuneRules.PouchCap, new TestStartPreset { AddRunes = 500 }.Normalize().AddRunes, "99까지");
            Assert.AreEqual(0, new TestStartPreset { AddRunes = -3 }.Normalize().AddRunes);
            Assert.IsTrue(TestStartPreset.TryParse("testpreset v1\nrunes=250\nstartrune=maybe\n", out var r));
            Assert.AreEqual(RuneRules.PouchCap, r.AddRunes);
            Assert.IsFalse(r.StartWeaponRune, "읽지 못한 값은 기본값");
        }

        [Test]
        public void CloneIsDeep()
        {
            var p = Busy();
            var c = p.Clone();
            Assert.AreNotSame(p, c);
            Assert.AreNotSame(p.LegendOn, c.LegendOn);
            Assert.AreEqual(p.ToText(), c.ToText());
            c.LegendOn[1] = true;
            c.Level = 2;
            Assert.IsFalse(p.LegendOn[1], "전설 배열까지 따로");
            Assert.AreEqual(7, p.Level);
            Assert.AreEqual(TestQuestStage.Fresh, new TestStartPreset { StartAt = TestStartAt.TownFresh, Stage = TestQuestStage.OgreKilled }.EffectiveStage);
        }
    }
}
