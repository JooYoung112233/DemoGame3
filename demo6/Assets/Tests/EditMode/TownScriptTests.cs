using System.Collections.Generic;
using System.Linq;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 기획/마을-의뢰-첫판.md 9-3 TownScriptTests(대본 검사): 길이, 첫 마디 공개 금지, 정해진 길을 따라가며 이름이 새지 않음,
    /// 장면과 의뢰 표가 맞음, 건너뛰기 효과가 끝까지 본 것과 같음. 화자 규칙: 이름과 인물이 이어지기 전까지 '?'.
    /// </summary>
    public sealed class TownScriptTests
    {
        static IEnumerable<string> Names => NpcTable.All.Select(n => n.DisplayName);

        // ── 1. 길이 ──

        static void AssertUtterance(string text, string where)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(text), where + " 빈 글");
            var pieces = text.Split('\n');
            Assert.LessOrEqual(pieces.Length, 2, where + " 한 발화 2줄 이하: " + text);
            foreach (var p in pieces) Assert.LessOrEqual(p.Length, TownScript.MaxLineChars, where + " 한 줄 32자 이하: " + p);
            Assert.LessOrEqual(text.Replace("\n", "").Length, TownScript.MaxUtteranceChars, where + " 한 발화 64자 이하");
        }

        [Test]
        public void LengthRules()
        {
            foreach (var s in TownScript.AllScenes)
            {
                Assert.IsNotEmpty(s.Lines, s.Id);
                int limit = s.Forced ? TownScript.MaxForcedUtterances : TownScript.MaxSceneUtterances;
                Assert.LessOrEqual(s.Utterances, limit, s.Id + " 마디 수");
                foreach (var l in s.Lines)
                {
                    AssertUtterance(l.Text, s.Id);
                    Assert.LessOrEqual(l.Choices.Length, TownScript.MaxChoices, s.Id + " 선택지 3개 이하");
                    foreach (var ch in l.Choices)
                    {
                        Assert.LessOrEqual(ch.Label.Length, TownScript.MaxChoiceChars, s.Id + " 선택지 14자 이하: " + ch.Label);
                        AssertUtterance(ch.Reply, s.Id + " 대답");
                    }
                    if (s.Kind == TalkSceneKind.Repeat)
                        Assert.LessOrEqual(l.Text.Length, TownScript.MaxShortChars, s.Id + " 반복 대사 24자 이하: " + l.Text);
                }
            }
            foreach (var b in TownScript.AllBarks) Assert.LessOrEqual(b.Length, TownScript.MaxShortChars, "바크 24자 이하: " + b);
            Assert.AreEqual(7, TownScript.Opening.Utterances, "오프닝 7마디");
            Assert.IsTrue(TownScript.Opening.Forced);
            Assert.AreEqual(1, TownScript.AllScenes.Count(s => s.Forced), "강제 장면은 오프닝 하나뿐");
        }

        [Test]
        public void RepeatAndBarkTables()
        {
            foreach (var npc in NpcTable.All)
            {
                for (int stage = 1; stage <= 2; stage++)
                {
                    var set = TownScript.RepeatSet(npc.Id, stage);
                    Assert.AreEqual(3, set.Length, npc.Id + " 단계 " + stage);
                    foreach (var s in set) Assert.AreEqual(npc.Id, s.Lines[0].SpeakerId);
                }
                Assert.IsNotNull(TownScript.Bark(npc.Id, BarkWhen.Idle), npc.Id + " 평소 바크");
            }
            Assert.AreEqual("어이, 이쪽!", TownScript.Bark(NpcTable.Gate, BarkWhen.BeforeOpening));
            Assert.AreEqual("올라왔으면 말을 해.", TownScript.Bark(NpcTable.Gate, BarkWhen.HasReport));
            Assert.AreEqual("살아 왔네. 칼 꼴 좀 봐.", TownScript.Bark(NpcTable.Smith, BarkWhen.FirstReturn));
            Assert.AreEqual("볼일 없으면 지나가.", TownScript.Bark(NpcTable.Trainer, BarkWhen.FirstReturn), "없는 때는 평소 바크");
            var ids = TownScript.AllScenes.Select(s => s.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
        }

        // ── 2. 첫 마디 ──

        [Test]
        public void FirstLineNeverRevealsItsSpeaker()
        {
            foreach (var s in TownScript.AllScenes)
            {
                var first = s.Lines[0];
                if (first.IsNarration) continue;
                CollectionAssert.DoesNotContain(first.Reveals, first.SpeakerId, s.Id + " 첫 마디에 화자 자신의 공개");
            }
            foreach (var s in TownScript.AllScenes)
                foreach (var l in s.Lines)
                    foreach (var id in l.Reveals)
                        Assert.IsTrue(NpcTable.Exists(id), s.Id + " 공개 id " + id);
        }

        [Test]
        public void RevealLinesFromDesign()
        {
            var op = TownScript.Opening.Lines;
            CollectionAssert.AreEqual(new[] { NpcTable.Gate }, op[2].Reveals, "오프닝 3마디 옥금이 춘삼을 부름");
            Assert.AreEqual(NpcTable.Smith, op[2].SpeakerId);
            CollectionAssert.AreEqual(new[] { NpcTable.Smith }, op[3].Reveals, "오프닝 4마디 춘삼이 옥금을 부름");
            var wb = TownScript.TrainerReportWindblade.Lines;
            CollectionAssert.AreEqual(new[] { NpcTable.Trainer }, wb[1].Reveals, "궁수 보고 2마디 자기소개");
            Assert.IsEmpty(TownScript.TrainerOfferWindblade.Lines.SelectMany(l => l.Reveals), "받기 장면은 이름을 밝히지 않음");
            var smithRepeat = TownScript.RepeatSet(NpcTable.Smith, 2).Single(s => s.Lines[0].Text.Contains("춘삼"));
            CollectionAssert.AreEqual(new[] { NpcTable.Gate }, smithRepeat.Lines[0].Reveals, "옥금 반복 대사의 춘삼 공개");
        }

        // ── 3. 정해진 길을 따라가며 이름이 새지 않음 ──

        [Test]
        public void FixedTextsHaveNoNames()
        {
            foreach (var t in TownScript.FixedTexts)
            {
                if (t == null) continue;
                foreach (var n in Names) StringAssert.DoesNotContain(n, t, "고정 글에 주민 이름: " + t);
            }
            foreach (var s in TownScript.RepeatSet(NpcTable.Trainer, 1))
                foreach (var n in Names) StringAssert.DoesNotContain(n, s.Lines[0].Text, "무진 1단계 반복 대사");
            foreach (var s in TownScript.Reactions)
                foreach (var l in s.Lines)
                    foreach (var n in Names) StringAssert.DoesNotContain(n, l.Text, "반응 줄");
        }

        /// <summary>따라가며 화면에 보일 글을 모두 검사하는 길잡이.</summary>
        sealed class Walker
        {
            public readonly CarryData Carry = CarryData.NewProfile(0xBEEFUL);
            public readonly HashSet<string> Visited = new HashSet<string>();
            public readonly QuestContext Ctx;
            readonly string _path;

            public Walker(string path, bool ogreDen = false)
            {
                _path = path;
                Ctx = new QuestContext(ogreDen);
            }

            public QuestBook Book => new QuestBook(Carry);

            /// <summary>모르는 주민의 이름이 글에 있으면 그 주민이 allowed(이 줄이 공개함)에 있어야 한다.</summary>
            public void Check(string text, string where, IEnumerable<string> allowed = null)
            {
                if (string.IsNullOrEmpty(text)) return;
                var ok = allowed != null ? new HashSet<string>(allowed) : new HashSet<string>();
                foreach (var npc in NpcTable.All)
                    if (!SpeakerIdentity.Knows(Carry, npc.Id) && text.Contains(npc.DisplayName) && !ok.Contains(npc.Id))
                        Assert.Fail($"[{_path}] {where}: 모르는 이름 '{npc.DisplayName}'이 공개 없이 보임: {text}");
            }

            /// <summary>마을·던전에서 보이는 글(HUD·목록·F 안내·바크·도착 카드).</summary>
            public void CheckScreens(string where)
            {
                var book = Book;
                foreach (var l in book.HudLines(QuestHudMode.Town, 0)) Check(l, where + " 마을 HUD");
                foreach (var l in book.HudLines(QuestHudMode.Dungeon, 0)) Check(l, where + " 던전 HUD");
                foreach (var r in book.ListRows()) Check(r.Line(), where + " 의뢰 목록");
                foreach (var npc in NpcTable.All)
                {
                    Check(TalkDirector.Hint(npc.Id, Carry), where + " F 안내");
                    Check(TalkDirector.BarkFor(npc.Id, Carry), where + " 바크");
                    Assert.IsFalse(TalkDirector.BarkFor(npc.Id, Carry).Contains(npc.DisplayName), "바크에 이름 없음");
                }
                Check(book.ArrivalReportLine(), where + " 도착 카드");
                Check(book.ArrivalActiveLine(), where + " 도착 카드");
            }

            public void Play(TalkScene s, bool skipAt1 = false)
            {
                Visited.Add(s.Id);
                var knownAtStart = NpcTable.All.Where(n => SpeakerIdentity.Knows(Carry, n.Id)).Select(n => n.Id).ToList();
                var sceneReveals = s.Lines.SelectMany(l => l.Reveals).ToList();
                // 요약: 그 장면 시작에 알던 이름이거나 그 장면이 공개하는 이름만.
                foreach (var npc in NpcTable.All)
                    if (s.SkipSummary.Contains(npc.DisplayName))
                        Assert.IsTrue(knownAtStart.Contains(npc.Id) || sceneReveals.Contains(npc.Id),
                            $"[{_path}] {s.Id} 요약에 모르는 이름: {s.SkipSummary}");
                for (int i = 0; i < s.Lines.Length; i++)
                {
                    var l = s.Lines[i];
                    if (skipAt1 && i == 1)
                    {
                        var skipped = TalkDirector.Skip(s, i, Carry, Ctx);
                        foreach (var t in skipped.Lines) Check(t, s.Id + " 보상 줄");
                        return;
                    }
                    Check(TalkDirector.Label(l, Carry), s.Id + " 이름표");
                    Check(l.Text, s.Id + " " + i, l.Reveals);
                    foreach (var ch in l.Choices)
                    {
                        Check(ch.Label, s.Id + " 선택지");
                        Check(ch.Reply, s.Id + " 대답");
                    }
                    TalkDirector.PassLine(l, Carry);
                }
                var r = TalkDirector.End(s, Carry, Ctx);
                foreach (var t in r.Lines) Check(t, s.Id + " 보상 줄");
                foreach (var t in r.Notices) Check(t, s.Id + " 알림");
            }

            public void Talk(string npcId, bool skipAt1 = false)
            {
                CheckScreens("말 걸기 전 " + npcId);
                var plan = TalkDirector.Build(npcId, Carry, Ctx);
                Assert.IsNotEmpty(plan.Scenes, npcId + " 장면 없음");
                foreach (var s in plan.Scenes) Play(s, skipAt1);
                new QuestBook(Carry).Refresh(Ctx);
                CheckScreens("말 건 뒤 " + npcId);
            }

            public void Dungeon(params QuestEvent[] events)
            {
                var book = Book;
                foreach (var e in events)
                    foreach (var u in book.Handle(e))
                    {
                        Check(u.ProgressNotice, "던전 알림");
                        Check(u.DoneNotice, "던전 완료 알림");
                    }
                CheckScreens("던전");
            }

            public void Arrive()
            {
                TownNight.AdvanceForAscend(Carry);
                var a = TownArrivalRules.Apply(Carry, TownArrivalKind.Basket, Ctx);
                foreach (var l in TownArrivalRules.CardLines(Carry, a)) Check(l, "도착 카드");
                CheckScreens("도착");
            }

            public QuestState State(string id) => new QuestBook(Carry).State(id);
        }

        static QuestEvent RatKill => QuestEvent.Killed(MonsterKind.Rat, KillSource.Whirlwind);
        static QuestEvent ArcherKill => QuestEvent.Killed(MonsterKind.Archer, KillSource.SwordWave);

        /// <summary>9-4 꾸러미 5 한 바퀴: 2층 계단 말뚝을 먼저 켜서 '이미 한 경우' 받기 장면으로 간다.</summary>
        static Walker PathMilestone(bool skip)
        {
            var w = new Walker("이정표 길" + (skip ? " 건너뛰기" : ""));
            TownArrivalRules.Apply(w.Carry, TownArrivalKind.NewPlay, w.Ctx);
            w.CheckScreens("새 플레이");
            Assert.AreEqual("어이, 이쪽!", TalkDirector.BarkFor(NpcTable.Gate, w.Carry));
            w.Talk(NpcTable.Trainer);
            w.Talk(NpcTable.Gate, skip);
            Assert.IsTrue(w.Book.OpeningSeen);
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.GateDescend));
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.SmithRats));
            for (int i = 0; i < 3; i++)
            {
                w.Talk(NpcTable.Gate);
                w.Talk(NpcTable.Smith);
            }
            w.Talk(NpcTable.Trainer);
            w.Talk(NpcTable.Trainer);

            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(1));
            for (int i = 0; i < 9; i++) w.Dungeon(RatKill);
            w.Dungeon(QuestEvent.FloorEntered(2), QuestEvent.StakeLit(2, true));
            TownSave.NoteDowned(w.Carry, false);
            w.Carry.OnceDone.Add("f1.T.nameplate");
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            Assert.AreEqual(QuestState.Achieved, w.State(QuestTable.GateDescend));
            Assert.AreEqual(QuestState.Achieved, w.State(QuestTable.SmithRats));
            Assert.AreEqual("살아 왔네. 칼 꼴 좀 봐.", TalkDirector.BarkFor(NpcTable.Smith, w.Carry));
            Assert.AreEqual("올라왔으면 말을 해.", TalkDirector.BarkFor(NpcTable.Gate, w.Carry));

            w.Talk(NpcTable.Gate, skip);   // 반응(쓰러짐) → 보고 → 2층 받기(이미 한 경우)
            Assert.AreEqual(QuestState.Rewarded, w.State(QuestTable.GateFloor2));
            w.Talk(NpcTable.Gate);         // 반응(명패)
            w.Talk(NpcTable.Smith, skip);  // 보고
            w.Talk(NpcTable.Trainer, skip); // 궁수 받기(이름 모름)
            Assert.AreEqual("?", SpeakerIdentity.Label(w.Carry, NpcTable.Trainer));
            w.Talk(NpcTable.Smith);
            w.Talk(NpcTable.Smith);
            w.Talk(NpcTable.Smith);
            w.Talk(NpcTable.Gate);
            w.Talk(NpcTable.Gate);
            w.Talk(NpcTable.Gate);

            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(2));
            for (int i = 0; i < 3; i++) w.Dungeon(ArcherKill);
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            Assert.AreEqual(QuestState.Achieved, w.State(QuestTable.TrainerWindblade));
            w.Talk(NpcTable.Trainer, skip); // 보고(이름 공개)
            Assert.AreEqual("무진", SpeakerIdentity.Label(w.Carry, NpcTable.Trainer));
            w.Talk(NpcTable.Trainer);
            w.Talk(NpcTable.Trainer);
            w.Talk(NpcTable.Trainer);
            Assert.AreEqual(QuestState.Locked, w.State(QuestTable.TrainerOgre), "굴 없음 가정(default ctx)이면 잠김");
            return w;
        }

        /// <summary>보통 받기 길: 2층 의뢰를 받은 뒤 말뚝을 켜고 보고한다. 오우거 굴이 있는 던전(ogreDen: true = QuestContext.Live)으로 오우거 의뢰까지.</summary>
        static Walker PathPlain()
        {
            var w = new Walker("보통 길", ogreDen: true);
            TownArrivalRules.Apply(w.Carry, TownArrivalKind.NewPlay, w.Ctx);
            w.Talk(NpcTable.Smith); // 오프닝 전 옥금에게 말을 걸어도 오프닝
            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(1), QuestEvent.Ascended());
            w.Arrive();
            w.Talk(NpcTable.Gate);   // 보고 → 2층 받기(보통)
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.GateFloor2));
            w.Talk(NpcTable.Trainer); // 궁수 받기
            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(1));
            for (int i = 0; i < 8; i++) w.Dungeon(RatKill);
            w.Dungeon(QuestEvent.FloorEntered(2), QuestEvent.StakeLit(2, true));
            for (int i = 0; i < 3; i++) w.Dungeon(ArcherKill);
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            w.Talk(NpcTable.Trainer); // 궁수 보고(이름 공개) → 오우거 받기
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.TrainerOgre));
            w.Talk(NpcTable.Gate);   // 2층 보고
            w.Talk(NpcTable.Smith);  // 굴쥐 보고
            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(2));
            TownSave.NoteDowned(w.Carry, true);
            w.Dungeon(QuestEvent.Killed(MonsterKind.Ogre, KillSource.None, false, true));
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            w.Talk(NpcTable.Trainer); // 반응(오우거 패배) → 오우거 보고
            w.Talk(NpcTable.Gate);    // 반응(쓰러짐)
            Assert.AreEqual(5, w.Book.InState(QuestState.Rewarded).Count);
            return w;
        }

        /// <summary>
        /// 오우거 패배 길(굴이 있는 지금 던전, 시스템-컨텐츠-다듬기-검토-1차.md Q2): 이름을 알기 전에 굴에서 돌진에 가장 많이 맞고 쓰러져도,
        /// 무진 첫 대화는 첫 만남(궁수 받기) 장면부터다. 패배 반응은 '굴의 큰 놈'을 받을 수 있게 될 때까지 아껴 두고 지우지 않는다.
        /// 다음 원정에서 내려찍기에 쓰러지면 궁수 보고 대화가 궁수 보고(이름 공개) → '진 적 있음' 받기 → 내려찍기 힌트 줄로 이어진다.
        /// 의뢰를 받은 뒤에 지면 예전처럼 다음 대화 첫 줄이 힌트다.
        /// </summary>
        static Walker PathOgreLosses()
        {
            var w = new Walker("오우거 패배 길", ogreDen: true);
            TownArrivalRules.Apply(w.Carry, TownArrivalKind.NewPlay, w.Ctx);
            w.Talk(NpcTable.Gate); // 오프닝
            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(1), QuestEvent.FloorEntered(2));
            TownSave.NoteDowned(w.Carry, true, OgreLossHint.Charge);
            BossLedger.RecordLoss(w.Carry, OgreDen.BossId);
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            w.Talk(NpcTable.Gate); // 반응(쓰러짐) → 첫 일 보고 → 2층 받기

            var plan = TalkDirector.Build(NpcTable.Trainer, w.Carry, w.Ctx);
            CollectionAssert.AreEqual(new[] { QuestTable.TrainerWindblade + ".offer" }, plan.Scenes.Select(s => s.Id),
                "첫 만남 장면이 먼저, 패배 반응은 아껴 둠");
            Assert.AreEqual("?", TalkDirector.Label(plan.Scenes[0].Lines[0], w.Carry), "이름을 알기 전 이름표 '?'");
            w.Talk(NpcTable.Trainer); // 궁수 받기
            Assert.IsTrue(TownSave.HasReaction(w.Carry, TownSave.RxOgreLost), "진 기록은 지우지 않음");
            Assert.AreEqual(OgreLossHint.Charge, TownSave.OgreLossHint(w.Carry), "힌트도 그대로");

            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(2));
            for (int i = 0; i < 3; i++) w.Dungeon(ArcherKill);
            TownSave.NoteDowned(w.Carry, true, OgreLossHint.Slam);
            BossLedger.RecordLoss(w.Carry, OgreDen.BossId);
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            plan = TalkDirector.Build(NpcTable.Trainer, w.Carry, w.Ctx);
            CollectionAssert.AreEqual(
                new[] { QuestTable.TrainerWindblade + ".report", QuestTable.TrainerOgre + ".offer_lost", "rx.ogre_lost.slam" }, plan.Scenes.Select(s => s.Id),
                "궁수 보고 → 오우거 받기(진 적 있음) → 내려찍기 힌트");
            w.Talk(NpcTable.Trainer);
            Assert.AreEqual("무진", SpeakerIdentity.Label(w.Carry, NpcTable.Trainer));
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.TrainerOgre));
            Assert.IsFalse(TownSave.HasReaction(w.Carry, TownSave.RxOgreLost), "힌트 줄을 보면 반응을 씀");
            Assert.AreEqual(OgreLossHint.None, TownSave.OgreLossHint(w.Carry), "반응을 쓰면 힌트도 지움");
            Assert.AreEqual(2, BossLedger.Losses(w.Carry, OgreDen.BossId), "쓰러진 기록은 남음");

            // 받은 뒤에 지면 다음 대화 첫 줄이 힌트(돌진).
            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(2));
            TownSave.NoteDowned(w.Carry, true, OgreLossHint.Charge);
            BossLedger.RecordLoss(w.Carry, OgreDen.BossId);
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            CollectionAssert.AreEqual(new[] { "rx.ogre_lost.charge", QuestTable.TrainerPillar + ".offer" }, TalkDirector.Build(NpcTable.Trainer, w.Carry, w.Ctx).Scenes.Select(s => s.Id),
                "받은 뒤 패배는 다음 대화 첫 줄, 진 뒤라 '기둥 앞에 서라'가 열림(묶음 3 나-10)");
            w.Talk(NpcTable.Trainer);
            Assert.IsNull(TalkDirector.PendingReaction(NpcTable.Trainer, w.Carry), "반응은 한 번만");
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.TrainerPillar));
            w.Talk(NpcTable.Gate); // 반응(쓰러짐)

            // 기둥에 박기 → 보고(묶음 3 나-10).
            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(2), QuestEvent.PillarBroken(MonsterKind.Ogre, OgreDen.Floor));
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            Assert.AreEqual(QuestState.Achieved, w.State(QuestTable.TrainerPillar));
            w.Talk(NpcTable.Trainer);
            Assert.AreEqual(QuestState.Rewarded, w.State(QuestTable.TrainerPillar));
            return w;
        }

        /// <summary>
        /// 오우거 먼저 처치 길(검토 1차 Q2 '이미 한 일 인정'): 의뢰가 열리기 전에 굴에서 한 번 지고, 다시 서서 잡는다. 이긴 처치가 지난 패배 반응을 지우고,
        /// 무진 첫 대화는 첫 만남 장면, 궁수 보고 대화는 궁수 보고 → '이미 잡음'(받기와 보고를 한 번에)으로 끝난다. 진 적이 있어도 '이미 잡음'이 먼저다.
        /// 의뢰 보상은 궁수·오우거 각각 한 번이고, 꾸러미 오우거 처치 수는 그대로 1이다(첫 처치 보상은 던전에서 따로 한 번).
        /// </summary>
        static Walker PathOgreKilledFirst(bool safeBeforeAccept = true)
        {
            var w = new Walker("오우거 먼저 처치 길", ogreDen: true);
            TownArrivalRules.Apply(w.Carry, TownArrivalKind.NewPlay, w.Ctx);
            w.Talk(NpcTable.Gate); // 오프닝
            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(1), QuestEvent.FloorEntered(2));
            TownSave.NoteDowned(w.Carry, true, OgreLossHint.Slam);
            BossLedger.RecordLoss(w.Carry, OgreDen.BossId);
            Assert.IsTrue(BossLedger.RecordKill(w.Carry, OgreDen.BossId, w.Carry.Expedition), "의뢰 전 첫 처치");
            TownSave.NoteBossKilled(w.Carry);
            w.Dungeon(QuestEvent.Killed(MonsterKind.Ogre, KillSource.None, false, true, floor: OgreDen.Floor));
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            Assert.AreEqual(QuestState.Locked, w.State(QuestTable.TrainerOgre), "의뢰 전 처치는 의뢰를 열지 않음");
            w.Talk(NpcTable.Gate); // 반응(쓰러짐) → 첫 일 보고 → 2층 받기
            CollectionAssert.AreEqual(new[] { QuestTable.TrainerWindblade + ".offer" }, TalkDirector.Build(NpcTable.Trainer, w.Carry, w.Ctx).Scenes.Select(s => s.Id),
                "첫 만남 장면, 이겨서 패배 반응 없음");
            w.Talk(NpcTable.Trainer); // 궁수 받기

            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(2));
            for (int i = 0; i < 3; i++) w.Dungeon(ArcherKill);
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            var plan = TalkDirector.Build(NpcTable.Trainer, w.Carry, w.Ctx);
            CollectionAssert.AreEqual(new[] { QuestTable.TrainerWindblade + ".report", QuestTable.TrainerOgre + ".offer_done", TownScript.OgreAfterId }, plan.Scenes.Select(s => s.Id),
                "궁수 보고 → 이미 잡음(진 적 있음보다 먼저)");
            int stones = w.Carry.Stones, gold = w.Carry.Gold, xp = w.Carry.TotalXp;
            var wind = QuestTable.Get(QuestTable.TrainerWindblade).Reward;
            var ogre = QuestTable.Get(QuestTable.TrainerOgre).Reward;
            w.Talk(NpcTable.Trainer);
            Assert.AreEqual("무진", SpeakerIdentity.Label(w.Carry, NpcTable.Trainer));
            Assert.AreEqual(QuestState.Rewarded, w.State(QuestTable.TrainerOgre));
            Assert.AreEqual(stones + wind.Stones + ogre.Stones, w.Carry.Stones, "의뢰 보상 강화석 각각 한 번");
            Assert.AreEqual(gold + wind.Gold + ogre.Gold, w.Carry.Gold, "의뢰 보상 골드 각각 한 번");
            Assert.AreEqual(xp + wind.Xp + ogre.Xp, w.Carry.TotalXp, "의뢰 보상 경험치 각각 한 번");
            Assert.AreEqual(1, BossLedger.Kills(w.Carry, OgreDen.BossId), "보스 처치 수는 그대로");
            w.Talk(NpcTable.Trainer); // 반복 대사
            Assert.AreEqual(stones + wind.Stones + ogre.Stones, w.Carry.Stones, "다시 말을 걸어도 그대로");
            w.Talk(NpcTable.Gate); // 오우거 뒤 춘삼 예고(묶음 3 나-6)

            // 오우거 뒤 새 의뢰(묶음 3 나-10). 앞 의뢰는 이 길의 몫이 아니라 지급으로 넘긴다(받기·보고 장면은 다른 길이 지난다).
            var book = w.Book;
            foreach (var id in new[] { QuestTable.GateFloor2, QuestTable.SmithRats })
                if (book.State(id) != QuestState.Rewarded) book.ForceState(id, QuestState.Rewarded);
            book.Refresh(w.Ctx);
            foreach (var npc in new[] { NpcTable.Trainer, NpcTable.Gate, NpcTable.Smith }) w.Talk(npc); // 뿔을 벽에·도면·쇠 냄새 받기
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.TrainerSlam));
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.GateSurvey));
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.SmithOre));
            TownNight.Depart(w.Carry);
            w.Carry.HasKey = true; // 오우거 유해 곁 열쇠(결정 D3 '나')
            if (safeBeforeAccept) w.Dungeon(QuestEvent.Found(DiscoveryKind.Safe, 1)); // 받기 전에 금고를 염 → 옥금 '이미 연 금고'
            for (int i = 0; i < 3; i++) w.Dungeon(QuestEvent.WallSlammed(MonsterKind.Boar, 2));
            w.Dungeon(QuestEvent.Found(DiscoveryKind.Ore, 1), QuestEvent.Found(DiscoveryKind.Ore, 2));
            w.Carry.SurveySheets[1] = 2;
            w.Carry.SurveySheets[2] = 1;
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            Assert.AreEqual(QuestState.Achieved, w.State(QuestTable.TrainerSlam));
            Assert.AreEqual(QuestState.Achieved, w.State(QuestTable.GateSurvey), "측량 합계 3");
            Assert.AreEqual(QuestState.Achieved, w.State(QuestTable.SmithOre));
            foreach (var npc in new[] { NpcTable.Trainer, NpcTable.Gate, NpcTable.Smith }) w.Talk(npc); // 보고(옥금은 이어서 금고 받기)
            if (safeBeforeAccept) Assert.AreEqual(QuestState.Rewarded, w.State(QuestTable.SmithSafe), "이미 연 금고: 받기와 보고를 한 번에");
            else
            {
                Assert.AreEqual(QuestState.Active, w.State(QuestTable.SmithSafe));
                TownNight.Depart(w.Carry);
                w.Dungeon(QuestEvent.Found(DiscoveryKind.Safe, 1));
                w.Dungeon(QuestEvent.Ascended());
                w.Arrive();
                w.Talk(NpcTable.Smith); // 금고 보고
                Assert.AreEqual(QuestState.Rewarded, w.State(QuestTable.SmithSafe));
            }

            // 오우거 지급 뒤 3단계 반복 대사를 한 바퀴씩(묶음 3 나-7).
            foreach (var npc in new[] { NpcTable.Gate, NpcTable.Smith, NpcTable.Trainer })
                for (int i = 0; i < 4; i++) w.Talk(npc);
            // 둘째·셋째 명패 반응(묶음 3 나-8): 1·2층 명패를 들고 귀환 → 춘삼(한 대화에 반응 하나), 보스 자리 명패를 들고 귀환 → 춘삼.
            TownNight.Depart(w.Carry);
            w.Carry.OnceDone.Add("f1.T.nameplate");
            w.Carry.OnceDone.Add("f2.nameplate");
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            for (int i = 0; i < 2; i++) w.Talk(NpcTable.Gate);
            TownNight.Depart(w.Carry);
            w.Carry.OnceDone.Add(FloorRecipe.DenNameplate.Id);
            w.Dungeon(QuestEvent.Ascended());
            w.Arrive();
            w.Talk(NpcTable.Gate);
            return w;
        }

        /// <summary>
        /// 느린 길: 2층 의뢰를 받고도 끝내지 않은 채 첫 밤을 지난다. 춘삼 1단계 "어젯밤에도 울리더라."는 첫 울림(둘째 방문 뒤 밤 카드) 전에는 나오지 않고,
        /// 셋째 방문(지난밤 = 첫 밤)에서 비로소 돈다.
        /// </summary>
        static Walker PathSlow()
        {
            var w = new Walker("느린 길");
            var night = TownScript.RepeatSet(NpcTable.Gate, 1).Single(s => s.NeedsPastNight);
            TownArrivalRules.Apply(w.Carry, TownArrivalKind.NewPlay, w.Ctx);
            w.Talk(NpcTable.Gate); // 오프닝
            for (int i = 0; i < 3; i++) w.Talk(NpcTable.Gate);
            Assert.IsFalse(w.Visited.Contains(night.Id), "원정 1: 지난밤 없음");

            TownNight.Depart(w.Carry);
            w.Dungeon(QuestEvent.FloorEntered(1), QuestEvent.Ascended());
            w.Arrive();
            w.Talk(NpcTable.Gate); // 보고 → 2층 받기(끝내지 않음)
            Assert.AreEqual(QuestState.Active, w.State(QuestTable.GateFloor2));
            for (int i = 0; i < 3; i++) w.Talk(NpcTable.Gate);
            Assert.IsFalse(w.Visited.Contains(night.Id), "원정 2: 오늘 밤이 첫 울림, 지난밤 없음");

            Assert.AreEqual(TownNight.FirstNightLine, TownNight.Depart(w.Carry).Lines[0], "둘째 출발 밤 카드가 첫 울림");
            w.Dungeon(QuestEvent.FloorEntered(1), QuestEvent.Ascended());
            w.Arrive();
            Assert.AreEqual(NightEvent.FirstNight, TownNight.LastNight(w.Carry));
            Assert.AreEqual(1, TalkDirector.RepeatStage(NpcTable.Gate, w.Carry));
            for (int i = 0; i < 3; i++) w.Talk(NpcTable.Gate);
            Assert.IsTrue(w.Visited.Contains(night.Id), "원정 3: 첫 울림 다음 날에는 나옴");
            return w;
        }

        [Test]
        public void PastNightRepeatWaitsForFirstNight()
        {
            // 1-7: "어젯밤에도 울리더라."는 지난밤(TownNight.LastNight)이 있어야 한다. 원정 1·2에는 지난밤이 없다.
            foreach (int expedition in new[] { 1, 2, 3 })
            {
                var c = CarryData.NewProfile(7UL);
                c.Expedition = expedition;
                bool expect = expedition >= 3;
                Assert.AreEqual(expect, TownNight.LastNight(c) != NightEvent.None, "원정 " + expedition);
                bool seen = false;
                for (int i = 0; i < 6; i++)
                {
                    var s = TalkDirector.RepeatScene(NpcTable.Gate, c);
                    Assert.IsNotNull(s);
                    seen |= s.Lines[0].Text.Contains("어젯밤");
                    TownSave.BumpRepeat(c, NpcTable.Gate);
                }
                Assert.AreEqual(expect, seen, "원정 " + expedition + " 어젯밤 줄");
            }
        }

        [Test]
        public void NamesNeverLeakAlongThePaths()
        {
            var a = PathMilestone(false);
            var b = PathMilestone(true);
            var c = PathPlain();
            var d = PathSlow();
            var e = PathOgreLosses();
            var f = PathOgreKilledFirst();
            var g = PathOgreKilledFirst(false); // 금고를 받은 뒤에 여는 길(옥금 보통 받기·보고)
            var visited = new HashSet<string>(a.Visited.Concat(b.Visited).Concat(c.Visited).Concat(d.Visited).Concat(e.Visited).Concat(f.Visited).Concat(g.Visited));
            foreach (var s in TownScript.StoryScenes) Assert.IsTrue(visited.Contains(s.Id), "길이 지나지 않은 장면: " + s.Id);
            foreach (var s in TownScript.RepeatScenes) Assert.IsTrue(visited.Contains(s.Id), "길이 지나지 않은 반복 대사: " + s.Id);
            // 건너뛴 길과 끝까지 본 길의 끝 상태가 같다(재화·이름·의뢰).
            Assert.AreEqual(a.Carry.Stones, b.Carry.Stones);
            Assert.AreEqual(a.Carry.TotalXp, b.Carry.TotalXp);
            foreach (var npc in NpcTable.All)
                Assert.AreEqual(SpeakerIdentity.Knows(a.Carry, npc.Id), SpeakerIdentity.Knows(b.Carry, npc.Id), npc.Id);
        }

        [Test]
        public void OpeningRevealsBeforeLineFourLabel()
        {
            // 9-4 꾸러미 3: 3번째 줄을 넘긴 뒤 4번째 줄 이름표가 '춘삼'. 3번째 줄 이름표(옥금)는 아직 '?'.
            var c = CarryData.NewProfile(1UL);
            var lines = TownScript.Opening.Lines;
            Assert.AreEqual("", TalkDirector.Label(lines[0], c), "나레이션");
            Assert.AreEqual("?", TalkDirector.Label(lines[1], c));
            TalkDirector.PassLine(lines[0], c);
            TalkDirector.PassLine(lines[1], c);
            Assert.AreEqual("?", TalkDirector.Label(lines[2], c));
            TalkDirector.PassLine(lines[2], c);
            Assert.AreEqual("춘삼", TalkDirector.Label(lines[3], c));
            Assert.AreEqual("?", TalkDirector.Label(lines[5], c), "옥금은 4번째 줄을 넘기기 전 '?'");
            TalkDirector.PassLine(lines[3], c);
            Assert.AreEqual("옥금", TalkDirector.Label(lines[5], c));
            Assert.AreEqual("F 춘삼 · 의뢰", SpeakerIdentity.Hint(c, NpcTable.Gate, QuestMarker.Offer), "F 안내가 '말 걸기'에서 '춘삼'으로");
        }

        // ── 4. 장면과 의뢰 표 ──

        [Test]
        public void ScenesMatchQuestTable()
        {
            foreach (var s in TownScript.StoryScenes)
            {
                foreach (var id in s.OnEnd.Accept.Concat(s.OnEnd.Report))
                {
                    var q = QuestTable.Get(id);
                    Assert.IsNotNull(q, s.Id + " 표에 없는 의뢰 " + id);
                    if (s.Kind == TalkSceneKind.Opening)
                        CollectionAssert.Contains(s.Speakers, q.GiverNpcId, s.Id + " 오프닝 의뢰의 주는 사람이 장면에 나옴");
                    else
                        Assert.AreEqual(q.GiverNpcId, s.OwnerNpcId, s.Id + " 주는 사람과 장면 주인");
                }
                if (s.QuestId != null) Assert.AreEqual(QuestTable.Get(s.QuestId).GiverNpcId, s.OwnerNpcId, s.Id);
                Assert.IsTrue(NpcTable.Exists(s.OwnerNpcId), s.Id + " 주인");
            }
            foreach (var q in QuestTable.All)
            {
                bool opening = QuestTable.OpeningQuests.Contains(q.Id);
                var offers = TownScript.StoryScenes.Where(s => s.Kind == TalkSceneKind.Offer && s.OnEnd.Accept.Contains(q.Id)).ToList();
                var reports = TownScript.StoryScenes.Where(s => s.Kind == TalkSceneKind.Report && s.OnEnd.Report.Contains(q.Id)).ToList();
                Assert.AreEqual(1, reports.Count, q.Id + " 보고 장면 하나");
                Assert.AreSame(reports[0], TownScript.ReportScene(q.Id));
                if (opening)
                {
                    Assert.AreEqual(0, offers.Count, q.Id + " 오프닝이 받기 장면");
                    CollectionAssert.Contains(TownScript.Opening.OnEnd.Accept, q.Id);
                    Assert.AreSame(TownScript.Opening, TownScript.OfferScene(q.Id));
                }
                else
                {
                    Assert.AreEqual(1, offers.Count, q.Id + " 받기 장면 하나");
                    Assert.AreSame(offers[0], TownScript.OfferScene(q.Id));
                }
                var done = TownScript.StoryScenes.Where(s => s.Kind == TalkSceneKind.OfferDone && s.QuestId == q.Id).ToList();
                if (q.CountsBeforeAccept)
                {
                    Assert.AreEqual(1, done.Count, q.Id + " '이미 한 경우' 장면");
                    CollectionAssert.Contains(done[0].OnEnd.Accept, q.Id);
                    CollectionAssert.Contains(done[0].OnEnd.Report, q.Id);
                    Assert.AreSame(done[0], TownScript.OfferScene(q.Id, true));
                }
                else Assert.AreEqual(0, done.Count, q.Id);
                // '진 적 있음' 받기 장면은 보스 의뢰에만 하나(받기만, 보고 없음). 없는 의뢰는 보통 받기 장면으로 돌아간다(검토 1차 Q2).
                var lost = TownScript.StoryScenes.Where(s => s.Kind == TalkSceneKind.OfferLost && s.QuestId == q.Id).ToList();
                if (!string.IsNullOrEmpty(q.BossId))
                {
                    Assert.AreEqual(1, lost.Count, q.Id + " '진 적 있음' 장면");
                    CollectionAssert.AreEqual(new[] { q.Id }, lost[0].OnEnd.Accept);
                    Assert.IsEmpty(lost[0].OnEnd.Report, q.Id + " 진 적 있음은 받기만");
                    Assert.IsNull(lost[0].OnEnd.ClearReaction, q.Id + " 진 기록(반응)은 받기 장면이 지우지 않음");
                    Assert.AreSame(lost[0], TownScript.OfferScene(q.Id, OfferCase.Lost));
                }
                else
                {
                    Assert.AreEqual(0, lost.Count, q.Id);
                    Assert.AreSame(TownScript.OfferScene(q.Id), TownScript.OfferScene(q.Id, OfferCase.Lost), q.Id + " 없으면 보통 받기");
                }
                Assert.AreSame(TownScript.OfferScene(q.Id), TownScript.OfferScene(q.Id, OfferCase.First), q.Id);
            }
            Assert.AreEqual(TownScript.OpeningId, TownScript.Opening.OnEnd.Seen);
        }

        // ── 받기 장면 세 경우(시스템-컨텐츠-다듬기-검토-1차.md Q2) ──

        /// <summary>궁수 의뢰를 알리기 직전(달성)의 꾸러미(굴이 있는 지금 던전). 오프닝 의뢰 둘은 지급, 무진 이름은 아직 모름.</summary>
        static CarryData BeforeWindbladeReport()
        {
            var c = CarryData.NewProfile(0x0612UL);
            var ctx = QuestContext.Live;
            TalkDirector.Skip(TownScript.Opening, 0, c, ctx);
            var book = new QuestBook(c);
            foreach (var id in new[] { QuestTable.GateDescend, QuestTable.SmithRats })
            {
                book.ForceState(id, QuestState.Achieved);
                Assert.IsTrue(book.Report(id).HasValue);
            }
            book.Refresh(ctx);
            Assert.IsTrue(book.Accept(QuestTable.TrainerWindblade));
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Achieved);
            return c;
        }

        [TestCase(OfferCase.First, "q.trainer_windblade.report → q.trainer_ogre.offer")]
        [TestCase(OfferCase.Lost, "q.trainer_windblade.report → q.trainer_ogre.offer_lost → rx.ogre_lost.charge")]
        [TestCase(OfferCase.Done, "q.trainer_windblade.report → q.trainer_ogre.offer_done → scene.ogre_after")]
        public void OgreOfferPicksOneOfThreeCases(OfferCase which, string expected)
        {
            var c = BeforeWindbladeReport();
            var ctx = QuestContext.Live;
            var q = QuestTable.Get(QuestTable.TrainerOgre);
            if (which != OfferCase.First)
            {
                // 진 적 있음: 굴에서 돌진에 가장 많이 맞고 쓰러짐. 이미 잡음: 지고 나서 다시 서서 잡음(이긴 처치가 반응을 지움, 진 기록은 남음).
                TownSave.NoteDowned(c, true, OgreLossHint.Charge);
                BossLedger.RecordLoss(c, OgreDen.BossId);
            }
            if (which == OfferCase.Done)
            {
                BossLedger.RecordKill(c, OgreDen.BossId, c.Expedition);
                TownSave.NoteBossKilled(c);
            }
            Assert.AreEqual(which, TalkDirector.OfferCaseFor(q, c));
            Assert.AreSame(TownScript.OfferScene(q.Id, which), TalkDirector.OfferSceneFor(q, c));
            string before = c.ToText();
            var plan = TalkDirector.Build(NpcTable.Trainer, c, ctx);
            Assert.AreEqual(before, c.ToText(), "Build는 꾸러미를 바꾸지 않음");
            Assert.AreEqual(expected, string.Join(" → ", plan.Scenes.Select(s => s.Id)));
            Assert.AreEqual(which == OfferCase.Lost ? 1 : 0, plan.Scenes.Count(s => s.Kind == TalkSceneKind.Reaction), "반응은 받기 장면 뒤에 하나까지");

            int losses = BossLedger.Losses(c, OgreDen.BossId);
            foreach (var s in plan.Scenes)
            {
                foreach (var l in s.Lines) TalkDirector.PassLine(l, c);
                TalkDirector.End(s, c, ctx);
            }
            Assert.AreEqual(which == OfferCase.Done ? QuestState.Rewarded : QuestState.Active, new QuestBook(c).State(q.Id));
            Assert.IsFalse(TownSave.HasReaction(c, TownSave.RxOgreLost), "힌트 줄을 봤거나(진 적 있음) 이겨서 지움(이미 잡음)");
            Assert.AreEqual(losses, BossLedger.Losses(c, OgreDen.BossId), "쓰러진 기록은 지우지 않음");
            Assert.AreEqual("무진", SpeakerIdentity.Label(c, NpcTable.Trainer), "받기 장면은 궁수 보고(이름 공개) 뒤");
        }

        [Test]
        public void OgreLossWaitsUntilOgreQuestCanBeTaken()
        {
            // 의뢰보다 먼저 굴에서 지면: 무진 첫 대화는 첫 만남(궁수 받기) 장면부터, 패배 반응은 아껴 두고 지우지 않는다.
            var c = CarryData.NewProfile(0x0613UL);
            var ctx = QuestContext.Live;
            TalkDirector.Skip(TownScript.Opening, 0, c, ctx);
            var book = new QuestBook(c);
            book.ForceState(QuestTable.GateDescend, QuestState.Achieved);
            Assert.IsTrue(book.Report(QuestTable.GateDescend).HasValue);
            book.Refresh(ctx);
            Assert.AreEqual(QuestState.Offered, book.State(QuestTable.TrainerWindblade));
            TownSave.NoteDowned(c, true, OgreLossHint.Slam);
            BossLedger.RecordLoss(c, OgreDen.BossId);

            Assert.IsFalse(TalkDirector.OgreLossReady(c), "잠김");
            Assert.IsNull(TalkDirector.PendingReaction(NpcTable.Trainer, c), "받기 전에는 꺼내지 않음");
            var plan = TalkDirector.Build(NpcTable.Trainer, c, ctx);
            CollectionAssert.AreEqual(new[] { TownScript.TrainerOfferWindblade.Id }, plan.Scenes.Select(s => s.Id), "첫 만남 장면만");
            Assert.AreEqual("?", TalkDirector.Label(plan.Scenes[0].Lines[0], c));
            foreach (var s in plan.Scenes) TalkDirector.Skip(s, 0, c, ctx);
            Assert.IsTrue(TownSave.HasReaction(c, TownSave.RxOgreLost), "진 기록은 지우지 않음");
            Assert.AreEqual(OgreLossHint.Slam, TownSave.OgreLossHint(c), "힌트도 그대로");
            Assert.AreEqual(TalkSceneKind.Repeat, TalkDirector.Build(NpcTable.Trainer, c, ctx).Scenes.Single().Kind, "궁수 진행 중에도 반응 없이 반복 대사");

            // 받을 수 있음(Offered)일 때도 첫머리에는 꺼내지 않고, '진 적 있음' 받기 장면 뒤 끝에 붙인다.
            book.ForceState(QuestTable.TrainerWindblade, QuestState.Rewarded);
            book.Refresh(ctx);
            Assert.AreEqual(QuestState.Offered, book.State(QuestTable.TrainerOgre));
            Assert.IsFalse(TalkDirector.OgreLossReady(c), "받을 수 있음");
            Assert.IsNull(TalkDirector.PendingReaction(NpcTable.Trainer, c));
            plan = TalkDirector.Build(NpcTable.Trainer, c, ctx);
            CollectionAssert.AreEqual(new[] { TownScript.TrainerOfferOgreLost.Id, TownScript.OgreLostSlam.Id }, plan.Scenes.Select(s => s.Id));
            foreach (var s in plan.Scenes) TalkDirector.Skip(s, 0, c, ctx);
            Assert.AreEqual(QuestState.Active, book.State(QuestTable.TrainerOgre));
            Assert.IsTrue(TalkDirector.OgreLossReady(c), "받은 뒤");
            Assert.IsFalse(TownSave.HasReaction(c, TownSave.RxOgreLost), "힌트 줄을 보면 씀");

            // 받은 뒤 지면 예전처럼 다음 대화 첫 줄.
            TownSave.NoteDowned(c, true, OgreLossHint.Charge);
            Assert.AreSame(TownScript.OgreLostCharge, TalkDirector.PendingReaction(NpcTable.Trainer, c));
            // 진 뒤라 '기둥 앞에 서라'(묶음 3 나-10)가 반응 뒤에 열린다.
            CollectionAssert.AreEqual(new[] { TownScript.OgreLostCharge.Id, TownScript.TrainerOfferPillar.Id }, TalkDirector.Build(NpcTable.Trainer, c, ctx).Scenes.Select(s => s.Id));
        }

        [Test]
        public void OgreOfferVariantsFollowTheVoiceRules()
        {
            // 새 받기 장면(진 적 있음·이미 잡음): 무진 혼자 2~3마디, 한 줄 24자 안팎, 이름 공개·이름 글 없음.
            foreach (var s in new[] { TownScript.TrainerOfferOgreLost, TownScript.TrainerOfferOgreDone })
            {
                Assert.AreEqual(NpcTable.Trainer, s.OwnerNpcId, s.Id);
                Assert.AreEqual(QuestTable.TrainerOgre, s.QuestId, s.Id);
                Assert.That(s.Utterances, Is.InRange(2, 3), s.Id + " 2~3마디");
                foreach (var l in s.Lines)
                {
                    Assert.AreEqual(NpcTable.Trainer, l.SpeakerId, s.Id);
                    Assert.IsEmpty(l.Reveals, s.Id);
                    Assert.IsEmpty(l.Choices, s.Id);
                    Assert.LessOrEqual(l.Text.Length, TownScript.MaxShortChars + 2, s.Id + " 한 줄 24자 안팎: " + l.Text);
                    foreach (var n in Names) StringAssert.DoesNotContain(n, l.Text, s.Id);
                }
                foreach (var n in Names) StringAssert.DoesNotContain(n, s.SkipSummary, s.Id + " 요약");
                CollectionAssert.Contains(TownScript.StoryScenes.ToList(), s, s.Id + " 대본 검사 목록");
            }
            Assert.AreEqual(TalkSceneKind.OfferLost, TownScript.TrainerOfferOgreLost.Kind);
            Assert.AreEqual(TalkSceneKind.OfferDone, TownScript.TrainerOfferOgreDone.Kind);
            CollectionAssert.AreEqual(new[] { QuestTable.TrainerOgre }, TownScript.TrainerOfferOgreDone.OnEnd.Report, "이미 잡음은 받기와 보고를 한 번에");
        }

        // ── 5. 건너뛰기 ──

        /// <summary>
        /// 장면 효과가 들어갈 수 있는 앞 상태(받기 → Offered, 받기·보고 → Offered + 받기 전 기록(이정표 또는 보스 처치), 보고 → Achieved, 반응 → rx).
        /// </summary>
        static CarryData Before(TalkScene s)
        {
            var c = CarryData.NewProfile(0x5EEDUL);
            var book = new QuestBook(c);
            book.Refresh();
            foreach (var id in s.OnEnd.Report)
                book.ForceState(id, s.OnEnd.Accept.Contains(id) ? QuestState.Offered : QuestState.Achieved);
            foreach (var id in s.OnEnd.Accept)
            {
                if (book.State(id) == QuestState.Locked) book.ForceState(id, QuestState.Offered);
                var q = QuestTable.Get(id);
                if (s.Kind != TalkSceneKind.OfferDone) continue;
                TownSave.SetMilestone(c, q.Milestone);
                if (!string.IsNullOrEmpty(q.BossId)) BossLedger.RecordKill(c, q.BossId, c.Expedition);
                Assert.IsTrue(book.DoneBeforeAccept(q), s.Id + " 받기 전 기록");
            }
            if (s.OnEnd.ClearReaction != null) TownSave.SetReaction(c, s.OnEnd.ClearReaction);
            return c;
        }

        [Test]
        public void SkipEqualsWatchingToTheEnd()
        {
            foreach (var s in TownScript.AllScenes)
            {
                var watched = Before(s);
                foreach (var l in s.Lines) TalkDirector.PassLine(l, watched);
                var full = TalkDirector.End(s, watched);

                var skipped = Before(s);
                var r0 = TalkDirector.Skip(s, 0, skipped);
                Assert.AreEqual(watched.ToText(), skipped.ToText(), s.Id + " 처음부터 건너뛰기");
                Assert.IsTrue(r0.Skipped);
                Assert.AreEqual(s.SkipSummary, r0.Summary);
                CollectionAssert.AreEqual(s.Speakers, r0.Speakers);
                CollectionAssert.AreEqual(full.Accepted, r0.Accepted, s.Id);
                CollectionAssert.AreEqual(full.Reported, r0.Reported, s.Id);
                CollectionAssert.AreEqual(full.Lines, r0.Lines, s.Id);

                if (s.Lines.Length > 1)
                {
                    var mid = Before(s);
                    TalkDirector.PassLine(s.Lines[0], mid);
                    TalkDirector.Skip(s, 1, mid);
                    Assert.AreEqual(watched.ToText(), mid.ToText(), s.Id + " 중간에 건너뛰기");
                }
            }
        }

        [Test]
        public void OpeningSkipGivesQuestsAndNames()
        {
            var c = CarryData.NewProfile(3UL);
            var r = TalkDirector.Skip(TownScript.Opening, 0, c);
            CollectionAssert.AreEquivalent(new[] { QuestTable.GateDescend, QuestTable.SmithRats }, r.Accepted);
            CollectionAssert.AreEquivalent(new[] { NpcTable.Gate, NpcTable.Smith }, r.Revealed);
            Assert.IsTrue(TownSave.SeenScene(c, TownScript.OpeningId));
            Assert.AreEqual("(갱도지기 춘삼, 대장장이 옥금과 인사를 나눴다. 의뢰 둘을 받았다.)", r.Summary);
            CollectionAssert.AreEqual(new[] { NpcTable.Gate, NpcTable.Smith }, r.Speakers);
            Assert.IsFalse(SpeakerIdentity.Knows(c, NpcTable.Trainer));
            var again = TalkDirector.End(TownScript.Opening, c);
            Assert.IsEmpty(again.Accepted, "두 번 끝내도 받기는 한 번");
        }

        // ── 대화 순서 ──

        [Test]
        public void TalkOrderReportThenOfferInOneConversation()
        {
            var c = CarryData.NewProfile(4UL);
            TalkDirector.Skip(TownScript.Opening, 0, c);
            var book = new QuestBook(c);
            book.Handle(QuestEvent.FloorEntered(1));
            book.Handle(QuestEvent.Ascended());
            TownSave.SetReaction(c, TownSave.RxDowned);
            TownSave.SetReaction(c, TownSave.RxTagF1);
            string before = c.ToText();
            var plan = TalkDirector.Build(NpcTable.Gate, c);
            Assert.AreEqual(before, c.ToText(), "Build는 꾸러미를 바꾸지 않음");
            CollectionAssert.AreEqual(new[] { "rx.downed", QuestTable.GateDescend + ".report", QuestTable.GateFloor2 + ".offer" }, plan.Scenes.Select(s => s.Id),
                "반응 하나 → 보고 → 받기");
            foreach (var s in plan.Scenes) TalkDirector.Skip(s, 0, c);
            Assert.AreEqual(QuestState.Active, book.State(QuestTable.GateFloor2));
            Assert.AreEqual(QuestState.Offered, book.State(QuestTable.TrainerWindblade), "춘삼 보고 뒤 무진에게 '!'");
            Assert.AreEqual(QuestMarker.Offer, book.Marker(NpcTable.Trainer));
            CollectionAssert.AreEqual(new[] { "rx.tag.f1" }, TalkDirector.Build(NpcTable.Gate, c).Scenes.Select(s => s.Id), "남은 반응은 다음 대화");
            var repeat = TalkDirector.Build(NpcTable.Smith, c).Scenes.Single();
            Assert.AreEqual(TalkSceneKind.Repeat, repeat.Kind);
            TalkDirector.End(repeat, c);
            Assert.AreNotSame(repeat, TalkDirector.Build(NpcTable.Smith, c).Scenes.Single(), "반복 대사는 차례로 돌림");
        }

        // ── 오우거 패배 반응(전투 문서 3-7 '그 판에 가장 많이 맞은 패턴을 가리킴') ──

        [Test]
        public void OgreLossReactionsSplitTheTwoLines()
        {
            var all = TownScript.Reactions.Where(s => s.OnEnd.ClearReaction == TownSave.RxOgreLost).ToList();
            CollectionAssert.AreEqual(new[] { TownScript.OgreLost, TownScript.OgreLostCharge, TownScript.OgreLostSlam }, all, "반응 목록에 셋");
            CollectionAssert.AreEqual(new[] { "들이받을 땐 앞을 안 봐. 마지막 걸 기둥에 박아.", "내려찍을 땐 걸어선 못 빠져. 굴러." },
                TownScript.OgreLost.Lines.Select(l => l.Text), "힌트 없음은 두 줄 그대로");
            Assert.AreEqual(TownScript.OgreLost.Lines[0].Text, TownScript.OgreLostCharge.Lines.Single().Text, "돌진 줄은 나누기만");
            Assert.AreEqual(TownScript.OgreLost.Lines[1].Text, TownScript.OgreLostSlam.Lines.Single().Text, "내려찍기 줄은 나누기만");
            foreach (var s in all)
            {
                Assert.AreEqual(TalkSceneKind.Reaction, s.Kind, s.Id);
                Assert.AreEqual(NpcTable.Trainer, s.OwnerNpcId, s.Id);
                CollectionAssert.Contains(TownScript.StoryScenes.ToList(), s, s.Id + " 대본 검사 목록");
                foreach (var l in s.Lines)
                {
                    Assert.AreEqual(NpcTable.Trainer, l.SpeakerId, s.Id);
                    Assert.IsEmpty(l.Reveals, s.Id + " 반응 줄은 이름을 공개하지 않음");
                    Assert.LessOrEqual(l.Text.Length, TownScript.MaxLineChars, s.Id + " 한 줄 32자 이하");
                    foreach (var n in Names) StringAssert.DoesNotContain(n, l.Text, s.Id);
                }
            }
            Assert.AreSame(TownScript.OgreLost, TownScript.ReactionScene(TownSave.RxOgreLost), "반응 이름으로 찾으면 두 줄 장면");
        }

        [TestCase(OgreLossHint.None, "rx.ogre_lost")]
        [TestCase(OgreLossHint.Charge, "rx.ogre_lost.charge")]
        [TestCase(OgreLossHint.Slam, "rx.ogre_lost.slam")]
        public void OgreLossReactionFollowsHint(OgreLossHint hint, string sceneId)
        {
            var c = CarryData.NewProfile(6UL);
            TalkDirector.Skip(TownScript.Opening, 0, c);
            // 패배 반응은 '굴의 큰 놈'을 받은 뒤에만 첫 줄로 꺼낸다(검토 1차 Q2). 받기 전은 OgreLossWaitsUntilOgreQuestCanBeTaken.
            new QuestBook(c).ForceState(QuestTable.TrainerOgre, QuestState.Active);
            TownSave.NoteDowned(c, true, hint);
            var s = TalkDirector.PendingReaction(NpcTable.Trainer, c);
            Assert.AreEqual(sceneId, s.Id);
            Assert.AreSame(TownScript.OgreLostScene(hint), s);
            Assert.AreSame(TownScript.ReactionScene(TownSave.RxDowned), TalkDirector.PendingReaction(NpcTable.Gate, c), "춘삼은 쓰러짐 반응");
            string before = c.ToText();
            var plan = TalkDirector.Build(NpcTable.Trainer, c);
            Assert.AreEqual(before, c.ToText(), "Build는 꾸러미를 바꾸지 않음");
            Assert.AreSame(s, plan.Scenes[0], "다음 대화 첫 줄");
            Assert.AreEqual(1, plan.Scenes.Count(x => x.Kind == TalkSceneKind.Reaction), "한 대화에 하나");
            Assert.AreEqual("?", TalkDirector.Label(s.Lines[0], c), "이름 공개 전 이름표 '?'");
            TalkDirector.End(s, c);
            Assert.IsNull(TalkDirector.PendingReaction(NpcTable.Trainer, c), "한 번만");
            Assert.AreEqual(OgreLossHint.None, TownSave.OgreLossHint(c), "힌트도 함께 지움");
            Assert.IsTrue(TownSave.HasReaction(c, TownSave.RxDowned), "춘삼 반응은 남음");
        }

        [Test]
        public void OldTwoArgDownedPicksTwoLines()
        {
            var c = CarryData.NewProfile(8UL);
            new QuestBook(c).ForceState(QuestTable.TrainerOgre, QuestState.Active); // 받은 뒤라 반응을 꺼냄(검토 1차 Q2)
            TownSave.NoteDowned(c, true);
            Assert.AreSame(TownScript.OgreLost, TalkDirector.PendingReaction(NpcTable.Trainer, c));
            TownSave.NoteDowned(c, false);
            Assert.AreSame(TownScript.OgreLost, TalkDirector.PendingReaction(NpcTable.Trainer, c), "보스 없이 쓰러져도 남은 반응 그대로");
            var d = CarryData.NewProfile(9UL);
            new QuestBook(d).ForceState(QuestTable.TrainerOgre, QuestState.Active);
            TownSave.NoteDowned(d, false, OgreLossHint.Charge);
            Assert.IsNull(TalkDirector.PendingReaction(NpcTable.Trainer, d), "살아 있는 보스가 없으면 오우거 반응 없음");
        }

        [Test]
        public void HintsFollowMarkers()
        {
            var c = CarryData.NewProfile(5UL);
            new QuestBook(c).Refresh();
            Assert.AreEqual("F 말 걸기 · 의뢰", TalkDirector.Hint(NpcTable.Gate, c));
            Assert.AreEqual("F 말 걸기", TalkDirector.Hint(NpcTable.Trainer, c));
            TalkDirector.Skip(TownScript.Opening, 0, c);
            Assert.AreEqual("F 춘삼", TalkDirector.Hint(NpcTable.Gate, c));
            new QuestBook(c).ForceState(QuestTable.GateDescend, QuestState.Achieved);
            Assert.AreEqual("F 춘삼 · 보고", TalkDirector.Hint(NpcTable.Gate, c));
        }

        // ── 재화 쓸 곳 1차 7장: 모루 물체 글·옥금 2단계 강화 예고 줄 ──

        [Test]
        public void AnvilObjectShowsForgeWindowTitle()
        {
            var anvil = TownScript.Object(TownScript.ObjAnvil);
            Assert.IsNotNull(anvil);
            Assert.AreEqual(ForgeText.WindowTitle, anvil.Text, "막힌 글 대신 창 제목(권양기 물체 글과 같은 방식)");
            Assert.AreEqual("모루 — 무엇을 두드릴까", anvil.Text);
            Assert.AreEqual("F 모루", anvil.Hint, "안내는 그대로");
            Assert.IsFalse(TownScript.FixedTexts.Any(t => t != null && t.Contains("준비 중")), "막힌 글이 남지 않음");
        }

        [Test]
        public void SmithStageTwoHintsEnhanceUpToSeven()
        {
            var set = TownScript.RepeatSet(NpcTable.Smith, 2);
            Assert.AreEqual("모루 데웠어. +7까진 돌만 잃어.", set[0].Lines[0].Text, "2단계 첫 줄");
            Assert.AreEqual(NpcTable.Smith, set[0].Lines[0].SpeakerId);
            Assert.IsEmpty(set[0].Lines[0].Reveals, "이름 공개 없음");
            Assert.LessOrEqual(set[0].Lines[0].Text.Length, TownScript.MaxShortChars);
            Assert.AreEqual(1, set.Count(s => s.Lines[0].Text.Contains("+7")), "+7 안내 줄 하나");
            Assert.IsTrue(set.Any(s => s.Lines[0].Text == "춘삼 영감 말은 반만 들어."), "이름 공개 줄은 그대로");
            Assert.IsFalse(TownScript.RepeatScenes.Any(s => s.Lines[0].Text.Contains("숯 자루가 오늘은")), "옛 줄은 바뀜");
            Assert.AreEqual("모루는 식어도 쇠는 기억해.", TownScript.RepeatSet(NpcTable.Smith, 1)[0].Lines[0].Text, "1단계는 그대로");
        }
    }
}
