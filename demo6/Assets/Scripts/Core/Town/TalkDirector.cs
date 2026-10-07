using System;
using System.Collections.Generic;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Town
{
    /// <summary>주민에게 한 번 말을 걸었을 때 이어 볼 장면들(TalkDirector.Build).</summary>
    public sealed class TalkPlan
    {
        public string NpcId = "";
        public readonly List<TalkScene> Scenes = new List<TalkScene>();

        /// <summary>오프닝(강제 장면)인가.</summary>
        public bool IsOpening => Scenes.Count > 0 && Scenes[0].Kind == TalkSceneKind.Opening;

        public override string ToString() => NpcId + ": " + string.Join(" → ", Scenes);
    }

    /// <summary>
    /// 장면 하나를 끝낸 결과(TalkDirector.End·Skip). Lines = 대화창에 보일 보상 줄·레벨 줄, Notices = 마을 알림(품삯 — …, 몸에 힘이 차오른다 — …).
    /// Summary·Speakers는 건너뛰었을 때만(요약 한 줄, ▼를 차례로 띄울 주민).
    /// </summary>
    public sealed class TalkResult
    {
        public TalkScene Scene;
        public bool Skipped;
        public string Summary;
        public readonly List<string> Accepted = new List<string>();
        public readonly List<string> Reported = new List<string>();
        public readonly List<string> Revealed = new List<string>();
        /// <summary>이 장면 끝에서 새로 '받을 수 있음'이 된 의뢰.</summary>
        public readonly List<string> Opened = new List<string>();
        public readonly List<QuestReward> Rewards = new List<QuestReward>();
        public readonly List<string> Lines = new List<string>();
        public readonly List<string> Notices = new List<string>();
        public readonly List<string> Speakers = new List<string>();
    }

    /// <summary>
    /// 대화 순서와 장면 효과(기획/마을-의뢰-첫판.md 1-5·3-2·6-3). TalkWindow(Game)는 Build로 장면 목록을 받고,
    /// 줄마다 시작할 때 Label로 이름표를 정하고(한 줄 안에서는 바뀌지 않음), 줄을 넘길 때 PassLine(이름 공개), 장면 끝에 End를 부른다.
    /// Esc 건너뛰기는 Skip(남은 줄의 공개 + 끝 효과를 끝까지 본 것과 똑같이). 이름 공개·의뢰 상태는 모두 꾸러미에 바로 적힌다.
    /// 한 번 말을 걸면: ① 반응 한 줄(한 번만) → ② 보고 장면(이 주민의 달성 의뢰, 오래된 것 먼저) → ③ 의뢰 다시 계산 →
    /// ④ 받기 장면(이 주민의 받을 수 있는 의뢰, 처음 · 진 적 있음 · 이미 함 중 하나) → ⑤ ①에서 아껴 둔 오우거 패배 반응이 ④의 받기로
    /// 꺼낼 수 있게 됐으면 끝에 붙임(한 대화에 반응 하나) → ⑥ 다 없으면 반복 대사 1줄. 오프닝 전 춘삼·옥금에게 말을 걸면 오프닝을 연다.
    /// </summary>
    public static class TalkDirector
    {
        /// <summary>
        /// 이 주민과의 대화 장면 목록. 보고 뒤에 열릴 받기 장면까지 미리 알려고 꾸러미 복사본에서 보고·다시 계산을 흉내 낸다(실제 꾸러미는 바꾸지 않음).
        /// </summary>
        public static TalkPlan Build(string npcId, CarryData carry, QuestContext ctx = default)
        {
            var plan = new TalkPlan { NpcId = npcId ?? "" };
            if (carry == null || !NpcTable.Exists(npcId)) return plan;
            var sim = carry.Clone();
            var book = new QuestBook(sim);

            if (!book.OpeningSeen && (npcId == NpcTable.Gate || npcId == NpcTable.Smith))
            {
                plan.Scenes.Add(TownScript.Opening);
                return plan;
            }

            var reaction = PendingReaction(npcId, sim);
            if (reaction != null)
            {
                plan.Scenes.Add(reaction);
                TownSave.ClearReaction(sim, reaction.OnEnd.ClearReaction);
            }

            foreach (var q in book.ToReport(npcId))
            {
                var scene = TownScript.ReportScene(q.Id);
                if (scene == null) continue;
                plan.Scenes.Add(scene);
                book.Report(q.Id);
            }

            // 오우거 보고 바로 뒤 마무리(묶음 3 나-6) — 그 보고로 새로 열린 받기(묶음 3 나-10)보다 먼저.
            AddAfterOgre(plan, npcId, sim);

            book.Refresh(ctx);
            foreach (var q in book.ToOffer(npcId))
            {
                var scene = OfferSceneFor(q, sim);
                // 오프닝 의뢰가 오프닝을 본 뒤에도 '받을 수 있음'으로 남는 것은 시험 패널 길뿐이다. 오프닝을 다시 틀지 않는다.
                if (scene == null || scene.Kind == TalkSceneKind.Opening) continue;
                plan.Scenes.Add(scene);
                book.Accept(q.Id);
                if (scene.Kind == TalkSceneKind.OfferDone) book.Report(q.Id);
            }

            // 오우거 뒤 매듭(묶음 3 나-6): 받기와 보고를 한 번에 한 경우(이미 잡음)는 받기 장면 뒤에. 이미 붙였으면 본 기록으로 건너뛴다.
            AddAfterOgre(plan, npcId, sim);

            // 아껴 둔 패배 반응(검토 1차 Q2): 첫머리에 꺼내지 못했는데 이 대화의 받기로 꺼낼 수 있게 됐으면 받기 장면 뒤 끝에 붙인다.
            // 첫머리에 반응을 이미 냈으면 붙이지 않는다(한 대화에 하나, 남은 반응은 다음 대화).
            if (reaction == null)
            {
                var late = PendingReaction(npcId, sim);
                if (late != null)
                {
                    plan.Scenes.Add(late);
                    TownSave.ClearReaction(sim, late.OnEnd.ClearReaction);
                }
            }

            if (plan.Scenes.Count == 0) plan.Scenes.Add(RepeatScene(npcId, carry));
            return plan;
        }

        /// <summary>
        /// 이 주민의 반응 장면(다음 대화 첫 줄, 한 대화에 하나): 춘삼 = 쓰러짐(rx:downed)·명패(rx:tag.f1), 무진 = 오우거 패배(rx:ogre_lost).
        /// 오우거 패배는 그 판에 가장 많이 맞은 패턴(TownSave.OgreLossHint)에 맞는 장면 하나를 고른다(돌진·내려찍기 한 줄, 힌트 없음 두 줄).
        /// 오우거 패배는 '굴의 큰 놈'을 받은 뒤(OgreLossReady)에만 꺼낸다. 그 전에는 null로 두되 rx:ogre_lost와 힌트를 지우지 않는다(검토 1차 Q2).
        /// </summary>
        public static TalkScene PendingReaction(string npcId, CarryData carry)
        {
            foreach (var s in TownScript.Reactions)
            {
                if (s.OwnerNpcId != npcId || !TownSave.HasReaction(carry, s.OnEnd.ClearReaction)) continue;
                if (s.OnEnd.ClearReaction != TownSave.RxOgreLost) return s;
                if (!OgreLossReady(carry)) continue;
                return TownScript.OgreLostScene(TownSave.OgreLossHint(carry));
            }
            return null;
        }

        /// <summary>
        /// 오우거 패배 반응을 꺼낼 수 있는가(검토 1차 Q2): '굴의 큰 놈'이 진행 중·달성·지급일 때만. 잠김·받을 수 있음이면 아껴 둔다.
        /// 그래서 의뢰보다 먼저 굴에서 져도 무진과의 첫 대화는 첫 만남(궁수 받기) 장면부터 시작하고, 진 기록은 의뢰를 받는 대화의
        /// '진 적 있음' 받기 장면 뒤에 힌트 줄로 나온다.
        /// </summary>
        public static bool OgreLossReady(CarryData carry) =>
            TownSave.GetQuestState(carry, QuestTable.TrainerOgre) >= QuestState.Active;

        /// <summary>
        /// 받기 장면 경우(검토 1차 Q2): 받기 전 기록이 있으면 이미 함(QuestBook.DoneBeforeAccept → OfferDone, 받기와 보고를 한 번에),
        /// 보스 의뢰(QuestDef.BossId)에서 그 보스에게 진 적이 있으면 진 적 있음(OfferLost), 그 밖은 처음(보통 받기).
        /// 이미 함이 진 적 있음보다 먼저다(지고 나서 잡았으면 잡은 쪽). 진 적 = 남은 패배 반응(rx:ogre_lost)이 있거나 그 보스방에서 쓰러진 기록(BossLedger.Losses).
        /// </summary>
        public static OfferCase OfferCaseFor(QuestDef q, CarryData carry)
        {
            if (q == null || carry == null) return OfferCase.First;
            if (new QuestBook(carry).DoneBeforeAccept(q)) return OfferCase.Done;
            if (!string.IsNullOrEmpty(q.BossId) &&
                (TownSave.HasReaction(carry, TownSave.RxOgreLost) || BossLedger.Losses(carry, q.BossId) > 0))
                return OfferCase.Lost;
            return OfferCase.First;
        }

        /// <summary>이 의뢰의 지금 받기 장면(OfferCaseFor로 고른 경우, 그 경우 장면이 없으면 보통 받기 장면, 오프닝 의뢰는 오프닝).</summary>
        public static TalkScene OfferSceneFor(QuestDef q, CarryData carry) =>
            q == null ? null : TownScript.OfferScene(q.Id, OfferCaseFor(q, carry));

        /// <summary>반복 대사 단계(5-6): 춘삼 q.gate_floor2 지급 뒤 2, 옥금 q.smith_rats 지급 뒤 2, 무진 이름 공개 뒤 2, 그 전은 1.</summary>
        public static int RepeatStage(string npcId, CarryData carry)
        {
            // 굴의 큰 놈 지급 뒤에는 세 주민 모두 3단계(묶음 3 나-7).
            if (OgreRewarded(carry)) return 3;
            switch (npcId)
            {
                case NpcTable.Gate: return TownSave.GetQuestState(carry, QuestTable.GateFloor2) == QuestState.Rewarded ? 2 : 1;
                case NpcTable.Smith: return TownSave.GetQuestState(carry, QuestTable.SmithRats) == QuestState.Rewarded ? 2 : 1;
                case NpcTable.Trainer: return TownSave.KnowsName(carry, NpcTable.Trainer) ? 2 : 1;
                default: return 1;
            }
        }

        /// <summary>굴의 큰 놈 의뢰를 보고해 지급까지 받았나.</summary>
        public static bool OgreRewarded(CarryData carry) =>
            carry != null && TownSave.GetQuestState(carry, QuestTable.TrainerOgre) == QuestState.Rewarded;

        /// <summary>
        /// 오우거 뒤 매듭 장면 붙이기(묶음 3 나-6). sim = 이 대화의 보고·받기를 이미 넣은 복사본.
        /// 무진: 이 대화로 지급이 끝났고(또는 끝나 있고) 마무리를 아직 안 봤으면 TownScript.OgreAfter.
        /// 춘삼: 지급 뒤 예고를 아직 안 봤으면 TownScript.GateAfterOgre. 둘 다 본 기록(sc:)으로 한 번만.
        /// </summary>
        static void AddAfterOgre(TalkPlan plan, string npcId, CarryData sim)
        {
            if (!OgreRewarded(sim)) return;
            TalkScene scene = null;
            if (npcId == NpcTable.Trainer && !TownSave.SeenScene(sim, TownScript.OgreAfterId)) scene = TownScript.OgreAfter;
            else if (npcId == NpcTable.Gate && !TownSave.SeenScene(sim, TownScript.GateAfterOgreId)) scene = TownScript.GateAfterOgre;
            if (scene == null) return;
            plan.Scenes.Add(scene);
            TownSave.MarkSceneSeen(sim, scene.Id);
        }

        /// <summary>
        /// 지금 차례의 반복 대사(rp:{npc} 차례로 돌림, 장면 끝에 차례가 오른다). 없으면 null.
        /// 지난밤이 있어야 하는 줄("어젯밤에도 울리더라.")은 지난밤(TownNight.LastNight)이 없으면 빼고 남은 줄로 돌린다(첫 울림 전에 미리 말하지 않게).
        /// </summary>
        public static TalkScene RepeatScene(string npcId, CarryData carry)
        {
            var set = TownScript.RepeatSet(npcId, RepeatStage(npcId, carry));
            bool pastNight = TownNight.LastNight(carry) != NightEvent.None;
            var usable = new List<TalkScene>(set.Length);
            foreach (var s in set)
                if (pastNight || !s.NeedsPastNight) usable.Add(s);
            if (usable.Count == 0) return null;
            int i = TownSave.RepeatIndex(carry, npcId) % usable.Count;
            return usable[i < 0 ? 0 : i];
        }

        /// <summary>이 줄의 이름표(줄이 시작될 때 한 번 정한다). 나레이션은 빈 글.</summary>
        public static string Label(TalkLine line, CarryData carry) => line == null ? "" : SpeakerIdentity.Label(carry, line.SpeakerId);

        /// <summary>줄을 넘김: 이 줄의 공개를 꾸러미에 적는다. 새로 알게 된 주민을 돌려준다.</summary>
        public static List<string> PassLine(TalkLine line, CarryData carry)
        {
            var revealed = new List<string>();
            if (line == null) return revealed;
            foreach (var id in line.Reveals)
                if (SpeakerIdentity.Reveal(carry, id)) revealed.Add(id);
            return revealed;
        }

        /// <summary>
        /// 장면 끝 효과(onEnd): 의뢰 다시 계산 → 받기 → 보고(보상 넣기, 보고마다 다시 계산) → 본 장면·반응 지우기·반복 차례.
        /// 같은 장면을 두 번 끝내도 받기·보고는 한 번만 일어난다(상태가 이미 넘어감).
        /// </summary>
        public static TalkResult End(TalkScene scene, CarryData carry, QuestContext ctx = default)
        {
            var result = new TalkResult { Scene = scene };
            if (scene == null || carry == null) return result;
            var book = new QuestBook(carry);
            result.Opened.AddRange(book.Refresh(ctx));
            var end = scene.OnEnd ?? new TalkEnd();
            foreach (var id in end.Accept)
                if (book.Accept(id)) result.Accepted.Add(id);
            foreach (var id in end.Report)
            {
                var r = book.Report(id);
                if (!r.HasValue) continue;
                result.Reported.Add(id);
                result.Rewards.Add(r.Value);
                result.Lines.Add(TownScript.RewardLine(r.Value));
                result.Notices.Add(TownScript.RewardNotice(r.Value));
                string up = TownScript.LevelUpNotice(r.Value);
                if (up != null)
                {
                    result.Lines.Add(up);
                    result.Notices.Add(up);
                }
                result.Opened.AddRange(book.Refresh(ctx));
            }
            if (end.Seen != null) TownSave.MarkSceneSeen(carry, end.Seen);
            if (end.ClearReaction != null) TownSave.ClearReaction(carry, end.ClearReaction);
            if (end.BumpRepeat != null) TownSave.BumpRepeat(carry, end.BumpRepeat);
            // 이 장면 끝에서 새로 열린 의뢰를 결과 줄·알림 맨 끝에 한 줄(묶음 3 가-1). 같은 장면에서 바로 받은 의뢰는 빠진다.
            string fresh = book.NewQuestLine(result.Opened);
            if (fresh != null)
            {
                result.Lines.Add(fresh);
                result.Notices.Add(fresh);
            }
            return result;
        }

        /// <summary>
        /// 건너뛰기(6-3): fromLine(아직 넘기지 않은 지금 줄)부터 끝까지의 이름 공개를 넣고 End와 같은 효과를 넣는다.
        /// 요약 한 줄과 ▼를 띄울 주민을 돌려준다.
        /// </summary>
        public static TalkResult Skip(TalkScene scene, int fromLine, CarryData carry, QuestContext ctx = default)
        {
            var revealed = new List<string>();
            if (scene != null)
                for (int i = Math.Max(0, fromLine); i < scene.Lines.Length; i++)
                    revealed.AddRange(PassLine(scene.Lines[i], carry));
            var result = End(scene, carry, ctx);
            result.Skipped = true;
            result.Summary = scene?.SkipSummary ?? "";
            result.Revealed.AddRange(revealed);
            if (scene != null) result.Speakers.AddRange(scene.Speakers);
            return result;
        }

        /// <summary>
        /// 이 주민의 지금 바크(5-7, 이름표 없음). 춘삼: 오프닝 전 "어이, 이쪽!", 보고할 의뢰가 하나라도 있으면 "올라왔으면 말을 해.",
        /// 옥금: 첫 귀환(원정 2, 오프닝 뒤) "살아 왔네. 칼 꼴 좀 봐.", 그 밖은 평소 바크.
        /// </summary>
        public static string BarkFor(string npcId, CarryData carry)
        {
            if (carry == null) return TownScript.Bark(npcId, BarkWhen.Idle);
            var book = new QuestBook(carry);
            switch (npcId)
            {
                case NpcTable.Gate:
                    if (!book.OpeningSeen) return TownScript.Bark(npcId, BarkWhen.BeforeOpening);
                    if (book.UnreportedCount > 0) return TownScript.Bark(npcId, BarkWhen.HasReport);
                    break;
                case NpcTable.Smith:
                    if (book.OpeningSeen && carry.Expedition == 2) return TownScript.Bark(npcId, BarkWhen.FirstReturn);
                    break;
            }
            return TownScript.Bark(npcId, BarkWhen.Idle);
        }

        /// <summary>주민 F 안내(6-3): 'F 춘삼 · 보고', 'F 말 걸기 · 의뢰', 'F 무진'.</summary>
        public static string Hint(string npcId, CarryData carry) =>
            SpeakerIdentity.Hint(carry, npcId, carry != null ? new QuestBook(carry).Marker(npcId) : QuestMarker.None);
    }
}
