using System;
using System.Collections.Generic;
using System.Text;
using Demo6.Core.Dungeon;

namespace Demo6.Core.Town
{
    /// <summary>
    /// 의뢰 상태 기계(기획/마을-의뢰-첫판.md 4-4, UnityEngine 없음). 꾸러미 하나를 감싸고, 상태는 모두 꾸러미에 있다(TownSave를 거침).
    /// 그래서 장면마다 new QuestBook(ProfileCarry.Ensure())로 만들어 써도 된다.
    /// Locked → Offered: Refresh(마을 도착 때, 대화가 끝날 때, 보고와 받기 사이) — 선행 의뢰가 모두 Rewarded, 필요하면 ctx.OgreDenReady.
    /// Offered → Active: Accept(받기 장면 끝). 받기 전 기록을 인정하는 의뢰는 기록(이정표 또는 보스 처치 수, DoneBeforeAccept)이 있으면 그 자리에서 Achieved.
    /// Active → Achieved: Handle(던전 사건)이 단계 목표를 채움. 센 수는 목표에서 멈춘다. 한 사건은 의뢰마다 한 단계만 채운다.
    /// Achieved → Rewarded: Report(보고 장면 끝). 보상 넣기와 상태 바꾸기를 한 함수에서 한다(두 번 받기 방지).
    /// 시간 제한·실패·포기·거절·반복 의뢰는 없다(4-5).
    /// 상태 키가 없으면 Locked다. 오프닝 의뢰 둘(선행 없음)은 첫 Refresh에서 Offered가 된다(마을 도착 처리 TownArrivalRules.Apply와
    /// 장면 끝 TalkDirector.End가 먼저 Refresh를 부르므로, 새 플레이 첫 도착부터 춘삼·옥금 머리 위에 '!'가 뜬다).
    /// </summary>
    public sealed class QuestBook
    {
        public CarryData Carry { get; }

        public QuestBook(CarryData carry)
        {
            Carry = carry ?? throw new ArgumentNullException(nameof(carry));
        }

        // ── 읽기 ──

        public QuestState State(string id) => TownSave.GetQuestState(Carry, id);

        /// <summary>지금 단계(0부터).</summary>
        public int Step(string id) => TownSave.GetQuestStep(Carry, id);

        /// <summary>지금 단계에서 센 수.</summary>
        public int Count(string id) => TownSave.GetQuestCount(Carry, id);

        /// <summary>받은 차례(받지 않았으면 0).</summary>
        public int Order(string id) => TownSave.GetQuestOrder(Carry, id);

        /// <summary>지금 단계 목표(의뢰가 없으면 null).</summary>
        public QuestGoal CurrentGoal(string id) => QuestTable.Get(id)?.Goal(Step(id));

        /// <summary>이 상태인 의뢰(표 차례).</summary>
        public List<QuestDef> InState(QuestState state)
        {
            var list = new List<QuestDef>();
            foreach (var q in QuestTable.All)
                if (State(q.Id) == state) list.Add(q);
            return list;
        }

        /// <summary>오프닝을 봤는가(sc:scene.opening).</summary>
        public bool OpeningSeen => TownSave.SeenScene(Carry, TownScript.OpeningId);

        // ── 넘어감 ──

        /// <summary>잠긴 의뢰가 열릴 조건인가: 선행 의뢰가 모두 Rewarded, 오우거 굴이 필요하면 ctx.OgreDenReady.</summary>
        public bool CanOpen(QuestDef q, QuestContext ctx)
        {
            if (q == null) return false;
            if (q.NeedsOgreDen && !ctx.OgreDenReady) return false;
            if (q.NeedsKey && !Carry.HasKey) return false;
            if (!string.IsNullOrEmpty(q.NeedsLossTo) && BossLedger.Losses(Carry, q.NeedsLossTo) < 1) return false;
            foreach (var r in q.RequiresInProgress)
            {
                var rs = State(r);
                if (rs != QuestState.Active && rs != QuestState.Achieved) return false;
            }
            foreach (var r in q.Requires)
                if (State(r) != QuestState.Rewarded) return false;
            return true;
        }

        /// <summary>Locked → Offered(조건이 된 것). 새로 열린 의뢰 id를 돌려준다. 여러 번 불러도 결과가 같다.</summary>
        public List<string> Refresh(QuestContext ctx = default)
        {
            SyncStateGoals();
            var opened = new List<string>();
            foreach (var q in QuestTable.All)
            {
                if (State(q.Id) != QuestState.Locked || !CanOpen(q, ctx)) continue;
                TownSave.SetQuestState(Carry, q.Id, QuestState.Offered);
                opened.Add(q.Id);
            }
            return opened;
        }

        /// <summary>
        /// Offered → Active(받기 장면 끝, 건너뛰어도 같음). 센 수·단계는 0에서 시작한다(처치는 받은 뒤부터 셈).
        /// 받기 전 기록을 인정하는 의뢰는 기록이 있으면(DoneBeforeAccept) 곧바로 Achieved. Offered가 아니면 아무것도 하지 않고 false.
        /// </summary>
        public bool Accept(string id)
        {
            var q = QuestTable.Get(id);
            if (q == null || State(id) != QuestState.Offered) return false;
            TownSave.SetQuestState(Carry, id, QuestState.Active);
            TownSave.SetQuestStep(Carry, id, 0);
            TownSave.SetQuestCount(Carry, id, 0);
            TownSave.StampQuestOrder(Carry, id);
            if (DoneBeforeAccept(q)) MarkAchieved(q);
            else SyncStateGoals();
            return true;
        }

        /// <summary>
        /// 상태로 세는 목표(GoalKind.SurveyTotal — 받은 측량 장 합계)를 진행 중 의뢰에 맞춘다(묶음 3 나-10). 받기 전 몫도 인정한다.
        /// Refresh·Accept·Handle이 부른다(측량은 사건을 따로 내지 않으므로 다음 던전 사건·마을 도착 때 맞춰진다). 달성한 의뢰 id를 돌려준다.
        /// </summary>
        public List<string> SyncStateGoals()
        {
            var done = new List<string>();
            foreach (var q in QuestTable.All)
            {
                if (State(q.Id) != QuestState.Active || q.StepCount == 0) continue;
                int step = Math.Min(Step(q.Id), q.StepCount - 1);
                var goal = q.Steps[step];
                if (goal.Kind != GoalKind.SurveyTotal) continue;
                int count = Math.Min(goal.Target, SurveyTotal);
                if (count >= goal.Target && step + 1 >= q.StepCount)
                {
                    MarkAchieved(q);
                    done.Add(q.Id);
                }
                else TownSave.SetQuestCount(Carry, q.Id, count);
            }
            return done;
        }

        /// <summary>
        /// 받기 전 기록이 있는가('이미 한 일 인정', 받기 장면은 받기와 보고를 한 번에 하는 OfferDone). CountsBeforeAccept인 의뢰만 본다:
        /// 이정표(ms:{Milestone})가 있거나, 보스 의뢰(BossId)면 꾸러미의 그 보스 처치 수(BossLedger.Kills)가 1 이상(시스템-컨텐츠-다듬기-검토-1차.md Q2).
        /// 보스 기록은 읽기만 한다(첫 처치 보상은 던전의 처치 수 0 → 1 한 번, 의뢰 보상은 Report 한 번으로 따로).
        /// </summary>
        public bool DoneBeforeAccept(QuestDef q)
        {
            if (q == null || !q.CountsBeforeAccept) return false;
            if (TownSave.HasMilestone(Carry, q.Milestone)) return true;
            return !string.IsNullOrEmpty(q.BossId) && BossLedger.Kills(Carry, q.BossId) >= 1;
        }

        /// <summary>
        /// 던전 사건 하나를 넣는다(QuestTracker). Active 의뢰만 센다(받기 전·달성 뒤·지급 뒤 사건은 무시).
        /// 2층(그 층) 계단 앞 말뚝 사건은 의뢰 상태와 상관없이 이정표(ms:stairs.f{층})를 먼저 적는다.
        /// 바뀐 의뢰마다 QuestUpdate(알림 글 포함)를 돌려준다.
        /// </summary>
        public List<QuestUpdate> Handle(QuestEvent e)
        {
            var updates = new List<QuestUpdate>();
            if (e.Kind == QuestEventKind.StakeLit && e.StairsFront && e.Floor >= 1)
                TownSave.SetMilestone(Carry, TownSave.StairsMilestone(e.Floor));
            // 금고를 연 기록(받기 전 몫 인정, '광업소 금고' — 금고는 프로필에 한 번 열린다).
            if (e.Kind == QuestEventKind.Found && e.Discovery == DiscoveryKind.Safe)
                TownSave.SetMilestone(Carry, TownSave.MilestoneSafeOpened);
            foreach (var id in SyncStateGoals())
            {
                var sq = QuestTable.Get(id);
                updates.Add(new QuestUpdate { Quest = sq, Step = sq.StepCount - 1, Count = sq.Steps[sq.StepCount - 1].Target, Target = sq.Steps[sq.StepCount - 1].Target, Achieved = true, DoneNotice = DoneNotice(sq) });
            }
            foreach (var q in QuestTable.All)
            {
                if (State(q.Id) != QuestState.Active || q.StepCount == 0) continue;
                int step = Math.Min(Step(q.Id), q.StepCount - 1);
                var goal = q.Steps[step];
                if (!Matches(goal, e)) continue;
                int count = Math.Min(goal.Target, Count(q.Id) + 1);
                var u = new QuestUpdate { Quest = q, Step = step, Count = count, Target = goal.Target };
                if (count >= goal.Target)
                {
                    u.ProgressNotice = goal.NoticeText(goal.Target);
                    if (step + 1 >= q.StepCount)
                    {
                        MarkAchieved(q);
                        u.Achieved = true;
                        u.DoneNotice = DoneNotice(q);
                    }
                    else
                    {
                        TownSave.SetQuestStep(Carry, q.Id, step + 1);
                        TownSave.SetQuestCount(Carry, q.Id, 0);
                        u.StepAdvanced = true;
                        u.Step = step + 1;
                        u.Count = 0;
                        u.Target = q.Steps[step + 1].Target;
                    }
                }
                else
                {
                    TownSave.SetQuestCount(Carry, q.Id, count);
                    u.ProgressNotice = goal.NoticeText(count);
                }
                updates.Add(u);
            }
            return updates;
        }

        /// <summary>
        /// 목표에 맞는 사건인가(4-3 표). 스킬 목표는 피해 사건(Source ≠ None)에서만: 출처가 그 스킬이거나 도움 2초(Assist)가 그 스킬,
        /// 종류가 같고 보상 없는 적이 아님. 보스 목표는 처치 사건(Source = None)에서만: 보스이고 종류가 같음.
        /// </summary>
        public static bool Matches(QuestGoal goal, QuestEvent e)
        {
            if (goal == null) return false;
            switch (goal.Kind)
            {
                case GoalKind.EnterFloor:
                    return e.Kind == QuestEventKind.FloorEntered && !e.Rebuild && e.Floor >= goal.MinFloor;
                case GoalKind.Ascend:
                    return e.Kind == QuestEventKind.Ascended;
                case GoalKind.KillWithSkill:
                    // 목표 출처가 None이면 어떤 공격이든(피해 사건에서만 세서 처치 사건과 두 번 세지 않는다).
                    return e.Kind == QuestEventKind.Killed && !e.NoReward && e.Monster == goal.Monster && e.Source != KillSource.None &&
                           (goal.Skill == KillSource.None || e.Source == goal.Skill || e.Assist == goal.Skill);
                case GoalKind.KillBoss:
                    return e.Kind == QuestEventKind.Killed && !e.NoReward && e.Boss && e.Source == KillSource.None && e.Monster == goal.Monster;
                case GoalKind.LightStairsStake:
                    return e.Kind == QuestEventKind.StakeLit && e.StairsFront && e.Floor == goal.MinFloor;
                case GoalKind.WallSlam:
                    return e.Kind == QuestEventKind.WallSlam && e.Monster == goal.Monster;
                case GoalKind.BossPillarBreak:
                    return e.Kind == QuestEventKind.PillarBreak && e.Monster == goal.Monster;
                case GoalKind.MineOre:
                    return e.Kind == QuestEventKind.Found && e.Discovery == DiscoveryKind.Ore;
                case GoalKind.OpenSafe:
                    return e.Kind == QuestEventKind.Found && e.Discovery == DiscoveryKind.Safe;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Achieved → Rewarded(보고 장면 끝). 강화석·골드·경험치를 꾸러미에 바로 더하고 레벨·스킬 점수를 같이 셈한 뒤 상태를 바꾼다.
        /// Achieved가 아니면(두 번째 보고 포함) 아무것도 주지 않고 null.
        /// </summary>
        public QuestReward? Report(string id)
        {
            var q = QuestTable.Get(id);
            if (q == null || State(id) != QuestState.Achieved) return null;
            var r = QuestRewards.Grant(Carry, q.Reward);
            TownSave.SetQuestState(Carry, id, QuestState.Rewarded);
            return r;
        }

        void MarkAchieved(QuestDef q)
        {
            var last = q.StepCount > 0 ? q.Steps[q.StepCount - 1] : null;
            TownSave.SetQuestStep(Carry, q.Id, Math.Max(0, q.StepCount - 1));
            TownSave.SetQuestCount(Carry, q.Id, last != null ? last.Target : 0);
            TownSave.SetQuestState(Carry, q.Id, QuestState.Achieved);
        }

        // ── 주민 ──

        /// <summary>머리 위 표시(6-2): 보고할 의뢰가 있으면 Report('…'), 받을 의뢰가 있으면 Offer('!'), 둘 다면 Report. 매 프레임 불러도 목록을 만들지 않는다.</summary>
        public QuestMarker Marker(string npcId)
        {
            bool offer = false;
            var all = QuestTable.All;
            for (int i = 0; i < all.Count; i++)
            {
                var q = all[i];
                if (!string.Equals(q.GiverNpcId, npcId, StringComparison.Ordinal)) continue;
                var s = State(q.Id);
                if (s == QuestState.Achieved) return QuestMarker.Report;
                if (s == QuestState.Offered) offer = true;
            }
            return offer ? QuestMarker.Offer : QuestMarker.None;
        }

        /// <summary>이 주민에게 보고할 의뢰(오래된 것 먼저 = 받은 차례).</summary>
        public List<QuestDef> ToReport(string npcId)
        {
            var list = new List<QuestDef>();
            foreach (var q in QuestTable.ByGiver(npcId))
                if (State(q.Id) == QuestState.Achieved) list.Add(q);
            list.Sort((a, b) => CompareOrder(a, b, false));
            return list;
        }

        /// <summary>이 주민에게 받을 의뢰(표 차례).</summary>
        public List<QuestDef> ToOffer(string npcId)
        {
            var list = new List<QuestDef>();
            foreach (var q in QuestTable.ByGiver(npcId))
                if (State(q.Id) == QuestState.Offered) list.Add(q);
            return list;
        }

        /// <summary>보고하지 않은 의뢰 수(권양기 창 '아직 알리지 않은 일 n').</summary>
        public int UnreportedCount => InState(QuestState.Achieved).Count;

        /// <summary>측량 한 층에 3장(승강장 줄 '측량 n/3'), 시험판 층 수만큼.</summary>
        public const int SurveyPerFloor = 3;
        public static int SurveyMax => SurveyPerFloor * FloorRecipe.MaxTestFloor;

        /// <summary>받은 측량 장 합계.</summary>
        public int SurveyTotal
        {
            get
            {
                int n = 0;
                foreach (var v in Carry.SurveySheets.Values) n += Math.Min(SurveyPerFloor, v);
                return n;
            }
        }

        /// <summary>받을 수 있는데 아직 받지 않은 의뢰 수(마을 목표 줄 '! 받을 일 n', 권양기 창 '받지 않은 일 n' — 검토 1차 묶음 3 가-1).</summary>
        public int OfferedCount => InState(QuestState.Offered).Count;

        /// <summary>
        /// 권양기 층 줄 덧글(검토 1차 묶음 3 가-2): 그 층이 기준 층(BaseFloor)인 진행 중 의뢰를 " · 의뢰: 굴쥐 쫓기 3/8"처럼 붙인다.
        /// 보스 의뢰(BossId)는 보스방 앞 줄(den)에만, 보통 의뢰는 보통 층 줄에만. 여럿이면 받은 차례로 ' · '. 없으면 빈 글. 출발은 막지 않는다.
        /// </summary>
        public string FloorHint(int floor, bool den)
        {
            var list = InState(QuestState.Active);
            list.Sort((a, b) => CompareOrder(a, b, false));
            var sb = new StringBuilder();
            foreach (var q in list)
            {
                bool boss = !string.IsNullOrEmpty(q.BossId);
                if (boss != den || (!den && q.BaseFloor != floor)) continue;
                sb.Append(sb.Length == 0 ? " · 의뢰: " : " · ").Append(q.Title);
                string p = ProgressShort(q);
                if (p.Length > 0) sb.Append(' ').Append(p);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 새로 열린 의뢰 한 줄(TownScript.NewQuestLine). openedIds 가운데 지금도 '받을 수 있음'인 것만, 주는 사람을 겹치지 않게 표 차례로 적는다
        /// (같은 장면에서 열리자마자 받은 의뢰는 빠진다). 없으면 null.
        /// </summary>
        public string NewQuestLine(IEnumerable<string> openedIds)
        {
            if (openedIds == null) return null;
            var ids = new HashSet<string>(openedIds);
            var givers = new List<string>();
            var labels = new List<string>();
            foreach (var q in QuestTable.All)
            {
                if (!ids.Contains(q.Id) || State(q.Id) != QuestState.Offered) continue;
                if (givers.Contains(q.GiverNpcId)) continue;
                givers.Add(q.GiverNpcId);
                labels.Add(SpeakerIdentity.GiverLabel(Carry, q.GiverNpcId));
            }
            return TownScript.NewQuestLine(labels);
        }

        // ── 글 ──

        /// <summary>던전 완료 알림: "의뢰 끝 — 굴쥐 쫓기. 올라가면 옥금에게 알리자."(알릴 곳은 ReportTarget).</summary>
        public string DoneNotice(QuestDef q) => q == null ? "" : TownScript.QuestDoneNotice(q.Title, SpeakerIdentity.ReportTarget(Carry, q.GiverNpcId));

        /// <summary>목표 HUD 진행 중 한 줄: "· 회오리로 굴쥐 3/8".</summary>
        public string ActiveLine(QuestDef q)
        {
            var goal = q?.Goal(Step(q.Id));
            return goal == null ? "" : TownScript.HudActivePrefix + goal.HudText(Count(q.Id));
        }

        /// <summary>목표 HUD 보고할 것 한 줄: "… 굴쥐 쫓기 — 옥금에게 알리기".</summary>
        public string ReportLine(QuestDef q) =>
            q == null ? "" : TownScript.HudReportPrefix + q.Title + " — " + SpeakerIdentity.ReportTarget(Carry, q.GiverNpcId) + " 알리기";

        /// <summary>
        /// 목표 HUD 줄(6-1). 마을은 보고할 것 먼저, 던전은 진행 중 먼저. 같은 갈래 안에서는 최근에 받은 순.
        /// 의뢰가 없으면 마을은 오프닝 전 '· 갱도 마당으로', 뒤에는 '· 권양기 바구니로 내려가기', 던전은 빈 목록. maxLines ≤ 0이면 자르지 않는다.
        /// </summary>
        public List<string> HudLines(QuestHudMode mode, int maxLines)
        {
            var active = InState(QuestState.Active);
            var achieved = InState(QuestState.Achieved);
            active.Sort((a, b) => CompareOrder(a, b, true));
            achieved.Sort((a, b) => CompareOrder(a, b, true));
            var lines = new List<string>();
            if (mode == QuestHudMode.Town)
            {
                foreach (var q in achieved) lines.Add(ReportLine(q));
                foreach (var q in active) lines.Add(ActiveLine(q));
                if (lines.Count == 0)
                {
                    // 의뢰를 다 끝낸 뒤(굴의 큰 놈 지급 뒤)는 '남은 일'을 먼저(묶음 3 나-7).
                    if (OpeningSeen && TalkDirector.OgreRewarded(Carry))
                        lines.Add(TownScript.HudLeftoverLine(SurveyTotal, SurveyMax, TownArrivalRules.NameplatesHeld(Carry)));
                    lines.Add(OpeningSeen ? TownScript.HudNoQuest : TownScript.HudBeforeOpening);
                }
                // 받을 일이 있으면 맨 끝에 '! 받을 일 n'(묶음 3 가-1). 줄이 모자라면 마지막 줄 자리를 차지해 늘 보인다.
                string offer = OpeningSeen ? TownScript.HudOfferLine(OfferedCount) : null;
                if (offer != null)
                {
                    if (maxLines > 0 && lines.Count >= maxLines) lines.RemoveRange(maxLines - 1, lines.Count - (maxLines - 1));
                    lines.Add(offer);
                }
            }
            else
            {
                foreach (var q in active) lines.Add(ActiveLine(q));
                foreach (var q in achieved) lines.Add(ReportLine(q));
            }
            if (maxLines > 0 && lines.Count > maxLines) lines.RemoveRange(maxLines, lines.Count - maxLines);
            return lines;
        }

        /// <summary>짧은 진행: 센 목표 "3/8", 계단 말뚝 "지금 1층까지", 그 밖은 빈 글.</summary>
        public string ProgressShort(QuestDef q)
        {
            var goal = q?.Goal(Step(q.Id));
            if (goal == null) return "";
            if (goal.Counted) return Math.Min(goal.Target, Count(q.Id)) + "/" + goal.Target;
            if (goal.Kind == GoalKind.LightStairsStake) return "지금 " + Math.Max(1, Carry.DeepestFloor) + "층까지";
            return "";
        }

        /// <summary>도착 카드 '알릴 일: 갱도로 내려가기 — 춘삼에게 · 굴쥐 쫓기 — 옥금에게'(없으면 null).</summary>
        public string ArrivalReportLine()
        {
            var list = InState(QuestState.Achieved);
            if (list.Count == 0) return null;
            list.Sort((a, b) => CompareOrder(a, b, false));
            var sb = new StringBuilder("알릴 일: ");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(list[i].Title).Append(" — ").Append(SpeakerIdentity.ReportTarget(Carry, list[i].GiverNpcId));
            }
            return sb.ToString();
        }

        /// <summary>도착 카드 '진행 중: 버팀목 길 끝까지 (지금 1층까지)'(없으면 null).</summary>
        public string ArrivalActiveLine()
        {
            var list = InState(QuestState.Active);
            if (list.Count == 0) return null;
            list.Sort((a, b) => CompareOrder(a, b, false));
            var sb = new StringBuilder("진행 중: ");
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(list[i].Title);
                string p = ProgressShort(list[i]);
                if (p.Length > 0) sb.Append(" (").Append(p).Append(')');
            }
            return sb.ToString();
        }

        /// <summary>의뢰 목록 창 줄(6-5): 잠긴 의뢰와 지급된 의뢰는 보이지 않는다(표 차례).</summary>
        public List<QuestRow> ListRows()
        {
            var rows = new List<QuestRow>();
            foreach (var q in QuestTable.All)
            {
                var s = State(q.Id);
                if (s == QuestState.Locked || s == QuestState.Rewarded) continue;
                rows.Add(new QuestRow
                {
                    Quest = q,
                    State = s,
                    Status = StatusText(q, s),
                    SizeLabel = q.SizeLabel,
                    Reward = q.Reward,
                    GiverLabel = SpeakerIdentity.GiverLabel(Carry, q.GiverNpcId),
                    GoalLine = GoalLine(q, s),
                });
            }
            return rows;
        }

        /// <summary>의뢰 창 목표 줄(QuestRow.GoalLine, 묶음 3 가-3).</summary>
        string GoalLine(QuestDef q, QuestState s)
        {
            switch (s)
            {
                case QuestState.Active:
                    var goal = q.Goal(Step(q.Id));
                    return goal == null ? "" : "지금: " + goal.HudText(Count(q.Id));
                case QuestState.Offered:
                    var first = q.Goal(0);
                    return first == null ? "" : "할 일: " + first.HudText(0);
                case QuestState.Achieved:
                    return "알리기: " + SpeakerIdentity.ReportTarget(Carry, q.GiverNpcId);
                default: return "";
            }
        }

        string StatusText(QuestDef q, QuestState s)
        {
            switch (s)
            {
                case QuestState.Offered: return "받을 수 있음";
                case QuestState.Achieved: return "알릴 일";
                case QuestState.Active:
                    var goal = q.Goal(Step(q.Id));
                    return goal != null && goal.Counted ? "진행 " + Math.Min(goal.Target, Count(q.Id)) + "/" + goal.Target : "진행 중";
                default: return "";
            }
        }

        /// <summary>받은 차례로 정렬(newestFirst면 최근 먼저). 같으면 표 차례.</summary>
        int CompareOrder(QuestDef a, QuestDef b, bool newestFirst)
        {
            int oa = Order(a.Id), ob = Order(b.Id);
            if (oa != ob) return newestFirst ? ob.CompareTo(oa) : oa.CompareTo(ob);
            return QuestTable.IndexOf(a.Id).CompareTo(QuestTable.IndexOf(b.Id));
        }

        // ── F1 시험 ──

        /// <summary>
        /// 시험 패널: 상태를 바로 바꾼다(보상은 주지 않음 — 받으려면 Report). Locked = 처음으로(키를 지움), Offered = 받을 수 있음,
        /// Active = 받음(단계·센 수 0), Achieved = 달성(마지막 단계·목표 수), Rewarded = 지급됨.
        /// </summary>
        public void ForceState(string id, QuestState state)
        {
            var q = QuestTable.Get(id);
            if (q == null) return;
            switch (state)
            {
                case QuestState.Locked:
                    TownSave.ClearQuest(Carry, id);
                    break;
                case QuestState.Offered:
                    TownSave.ClearQuest(Carry, id);
                    TownSave.SetQuestState(Carry, id, QuestState.Offered);
                    break;
                case QuestState.Active:
                    if (Order(id) == 0) TownSave.StampQuestOrder(Carry, id);
                    TownSave.SetQuestStep(Carry, id, 0);
                    TownSave.SetQuestCount(Carry, id, 0);
                    TownSave.SetQuestState(Carry, id, QuestState.Active);
                    break;
                case QuestState.Achieved:
                    if (Order(id) == 0) TownSave.StampQuestOrder(Carry, id);
                    MarkAchieved(q);
                    break;
                case QuestState.Rewarded:
                    if (Order(id) == 0) TownSave.StampQuestOrder(Carry, id);
                    TownSave.SetQuestState(Carry, id, QuestState.Rewarded);
                    break;
            }
        }

        /// <summary>시험 패널 '진행 중 의뢰 모두 달성'. 달성시킨 의뢰의 알림을 돌려준다.</summary>
        public List<QuestUpdate> AchieveAllActive()
        {
            var updates = new List<QuestUpdate>();
            foreach (var q in InState(QuestState.Active))
            {
                MarkAchieved(q);
                var last = q.Goal(q.StepCount - 1);
                updates.Add(new QuestUpdate
                {
                    Quest = q,
                    Step = Math.Max(0, q.StepCount - 1),
                    Count = last?.Target ?? 0,
                    Target = last?.Target ?? 0,
                    Achieved = true,
                    DoneNotice = DoneNotice(q),
                });
            }
            return updates;
        }

        /// <summary>시험 패널 상태 줄: "q.smith_rats Active 단계 1/1 3/8 차례 2".</summary>
        public string DebugLine(string id)
        {
            var q = QuestTable.Get(id);
            if (q == null) return id + " ?";
            var goal = q.Goal(Step(id));
            return $"{id} {State(id)} 단계 {Step(id) + 1}/{q.StepCount} {Count(id)}/{goal?.Target ?? 0} 차례 {Order(id)}";
        }
    }
}
