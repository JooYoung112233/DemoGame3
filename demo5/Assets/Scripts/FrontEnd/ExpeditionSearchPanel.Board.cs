using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Site-board mode (2nd visit on): the 07 panel assigns a lead and a support role to one object; assigning costs no time.
    // 말 놓기 (2026-09-25): retired — the window is read only on both visits (ReadOnly.cs); this partial is deleted in phase 2.
    // No pace (2026-09-25): the object's own noise and turns come from the plan (FieldRun.Noise / Required), never recomputed here.
    // Confirm spends the turn only when every living member then has a running role, or the object's standing order is confirmed unchanged.
    public sealed partial class ExpeditionSearchPanel
    {
        FieldTurnPlanner Planner => arrival && arrival.Threat ? arrival.Threat.Planner : null;
        bool Board => Planner && Planner.Active;
        // Tapping the highlighted role again: search alone (the partner keeps their own slot).
        public bool Solo { get; private set; }
        int prefer = -1; string notice;

        void ResetBoard() { Solo = false; prefer = -1; notice = null; }
        // 말 놓기: read only (ExpeditionSearchPanel.ReadOnly.cs) — a card or a role press changes nothing.
        void OnCard(Adventurer person) { if (ReadOnly) return; if (Board) { BoardCard(person); return; } Worker = person; Refresh(); }
        void OnDuty(int k) { if (ReadOnly) return; if (Board) { BoardDuty(k); return; } Duty = k; Refresh(); }
        public void RefreshBoard() { if (!IsOpen) return; if (ReadOnly) ReadOnlyRefresh(); else if (Board) BoardRefresh(); }
        int Member(Adventurer p) => p == null ? -1 : arrival.Participants.ToList().IndexOf(p);
        // Observation ids are not search-site indices; keep this warning separate from FieldMoveNote.
        string ObservationMoveLine(int member)
        {
            if (member < 0 || member >= arrival.Participants.Count) return null;
            var observation = Planner.Plan.ObserveBy(member);
            if (observation == null) return null;
            string label = arrival.Story ? arrival.Story.ObserveLabel(observation.Id) : "흔적";
            return arrival.Participants[member].Name + " · " + label + " 관찰 배정에서 빠집니다.";
        }

        void BoardOpen()
        {
            var plan = Planner.Plan; var standing = plan.Find(site); var facts = Planner.Facts(); ResetBoard();
            if (standing != null && facts.Alive(standing.Lead)) { Worker = arrival.Participants[standing.Lead]; Duty = standing.Duty; Solo = standing.Solo; return; }
            if (arrival.Loot.State(site).Progress == 0 && !Assignments.ContainsKey(site)) Duty = 0;
            // The last lead of this object if idle, otherwise the first member with no slot this turn, then anyone not leading
            // (a busy member can still be picked by tapping; the panel says what they leave).
            var now = plan.Check(facts);
            bool Idle(int m) => facts.Alive(m) && m < now.Actions.Length && now.Actions[m] == FieldAction.Hush;
            bool Free(int m) => facts.Alive(m) && plan.SiteLedBy(m) == null;
            int lead = Assignments.ContainsKey(site) && Idle(Member(Worker)) ? Member(Worker) : -1;
            for (int m = 0; lead < 0 && m < arrival.Participants.Count; m++) if (Idle(m)) lead = m;
            for (int m = 0; lead < 0 && m < arrival.Participants.Count; m++) if (Free(m)) lead = m;
            for (int m = 0; lead < 0 && m < arrival.Participants.Count; m++) if (facts.Alive(m)) lead = m;
            Worker = lead >= 0 ? arrival.Participants[lead] : null;
        }
        void BoardCard(Adventurer person)
        {
            if (person == null || person.Health <= 0) return;
            notice = null;
            // The current lead again: release this object's order (only if it has one).
            if (person == Worker) { if (Planner.Plan.Find(site) != null) Worker = null; BoardRefresh(); return; }
            prefer = Member(Worker); Worker = person; BoardRefresh();
        }
        void BoardDuty(int k)
        {
            if (arrival.Loot.State(site).Progress > 0) return;
            notice = null;
            if (k == Duty && !Solo) Solo = true; else { Duty = k; Solo = false; }
            BoardRefresh();
        }

        struct BoardDraft { public FieldTurnPlan Plan; public FieldOrder Order; public FieldPlanCheck Check; public FieldSiteState Outlook; public bool Pass; public List<FieldMoveNote> Notes; public bool[] Offered; public bool SoloShown; }
        BoardDraft Draft()
        {
            var planner = Planner; var facts = planner.Facts(); var s = arrival.Loot.State(site); int lead = Member(Worker); var standing = planner.Plan.Find(site);
            var d = new BoardDraft { Offered = new bool[3], Notes = new List<FieldMoveNote>() };
            // CanOffer: someone free would be bound (never 망보기 on a silent object: FieldTurnPlan.Need).
            if (s.Progress == 0 && lead >= 0) for (int i = 0; i < 3; i++) d.Offered[i] = planner.Plan.CanOffer(i, site, lead, facts);
            if (s.Progress == 0 && lead >= 0 && !Solo && !d.Offered[Duty] && d.Offered.Any(x => x)) Duty = System.Array.IndexOf(d.Offered, true);
            d.SoloShown = s.Progress == 0 && lead >= 0 && (Solo || !d.Offered.Any(x => x));
            d.Plan = planner.Plan.Clone();
            if (lead >= 0)
            {
                d.Order = new FieldOrder { Site = site, Lead = lead, Duty = Duty, Solo = s.Progress == 0 ? d.SoloShown : standing != null && standing.Solo, Prefer = prefer, Support = standing != null ? standing.Support : -1 };
                d.Notes = d.Plan.Assign(d.Order);
            }
            else d.Plan.Release(site);
            var f = planner.Forecast(d.Plan); d.Check = f.check; d.Outlook = f.outlook; d.Pass = f.hushWouldPass;
            return d;
        }
        // Once a search has started its role (and its turns) are locked, so only the lead can change it.
        bool Unchanged(FieldOrder order) { var standing = Planner.Plan.Find(site); return standing != null && order != null && standing.Lead == order.Lead && (arrival.Loot.State(site).Progress > 0 || standing.SameAs(order)); }

        void BoardRefresh()
        {
            var planner = Planner; var tx = planner.Texts; var s = arrival.Loot.State(site);
            if (s.Progress > 0) Duty = s.Duty;
            var d = Draft(); var run = d.Check.RunFor(site); var pause = d.Check.PauseFor(site); var standing = planner.Plan.Find(site);
            ShowObjectNoise();
            for (int i = 0; i < 3; i++)
            {
                bool lit = i == Duty && (s.Progress > 0 ? Duty != 0 || s.Bonus > 0 : Worker != null && !d.SoloShown);
                Duties[i].GetComponent<Image>().color = lit ? planner.LeadTint : Color.white;
                Duties[i].interactable = s.Progress == 0 && Worker != null && d.Offered[i];
            }
            // The turns this search needs: the run's (함께 already counted), a started one's stored value, otherwise the object's base turns.
            int required = run != null ? run.Required : s.Progress > 0 ? s.Required : arrival.Loot.SiteTurns(site);
            if (DropPreview) DropPreview.Refresh(arrival, site, run != null ? run.Pace : s.Progress > 0 ? s.Pace : 1, run != null ? run.Duty : Duty, run != null ? run.Bonus : s.Progress > 0 ? s.Bonus : 0, run != null ? run.Noise : arrival.Loot.SiteNoise(site), required);
            for (int i = 0; i < Cards.Count && i < arrival.Participants.Count; i++)
            {
                var person = arrival.Participants[i]; int at = i < d.Check.SiteOf.Length ? d.Check.SiteOf[i] : -1;
                bool lead = person == Worker, helps = run != null && run.Support == i, busy = !lead && !helps && at >= 0 && at != site;
                bool ear = !lead && i < d.Check.DoorOf.Length && d.Check.DoorOf[i] >= 0;
                bool observes = !lead && !helps && i < d.Check.ObserveOf.Length && !string.IsNullOrEmpty(d.Check.ObserveOf[i]);
                Cards[i].Paper.color = lead ? planner.LeadTint : helps ? planner.SupportTint : busy || ear || observes ? planner.BusyTint : Color.white;
                Cards[i].Button.interactable = person.Health > 0;
                Cards[i].SetAction(person.Health <= 0 ? tx.CardDown : lead ? tx.CardLead : helps ? (run.Duty == 2 ? tx.CardLight : run.Duty == 1 ? tx.CardWatch : tx.CardTogether) : busy ? tx.CardBusy : ear ? tx.CardListen : observes ? tx.TagObserve : null);
            }
            string tool = arrival.Loot.Sites[site].RequiredTool; var toolItem = arrival.Inventory.Items.FirstOrDefault(i => i.Id == tool);
            bool needsTool = !string.IsNullOrEmpty(tool), opened = s.Opened;
            if (ToolIcon) { ToolIcon.gameObject.SetActive(needsTool); ToolIcon.sprite = toolItem?.Icon; }
            Equipment.text = !needsTool ? "도구 없이 수색 가능" : opened ? "덮개 개방 완료 · 도구 없이 재개 가능" : (toolItem?.Name ?? tool) + " · " + (Worker == null ? "담당자 선택" : arrival.Inventory.CountFor(Worker, tool) > 0 ? "휴대 확인 · 소모 없음" : "담당자 가방에 필요");
            // Cost: this object's progress and the whole room's noise this turn; then who supports it.
            string support = "";
            if (run != null)
            {
                string who = run.Support >= 0 ? arrival.Participants[run.Support].Name : "";
                support = run.Forfeits ? string.Format(tx.CostForfeit, run.Lost)
                    : run.Support >= 0 ? (run.Duty == 2 ? string.Format(tx.CostLight, who, run.Bonus) : run.Duty == 1 ? string.Format(tx.CostWatch, who, arrival.Loot.SiteNoise(site), run.Noise)
                        : run.Bonus > 0 ? string.Format(tx.CostTogetherBonus, who, run.Bonus) : string.Format(tx.CostTogether, who, run.Bonus))
                    : run.Duty == 1 ? (arrival.Loot.CanWatch(site) ? tx.CostWatchBusy : tx.CostWatchSilent) : Solo || s.Progress > 0 ? tx.CostSolo : tx.CostSoloBusy;
            }
            else if (pause == FieldPause.NoLight) support = tx.CostLightNone;
            Cost.text = string.Format(tx.CostLine, s.Progress, required, d.Check.Noise) + "\n" + support;
            // Choose: assign, continue (unchanged standing order), release, or why it cannot run.
            var label = Choose.GetComponentInChildren<Text>();
            if (Worker == null) { label.text = standing != null ? tx.ChooseRelease : tx.ChooseWorker; Choose.interactable = standing != null; }
            else if (run == null)
            {
                label.text = pause == FieldPause.NoTool ? string.Format(tx.ChooseTool, planner.ToolName(site)) : pause == FieldPause.NoLight ? tx.ChooseLight : pause == FieldPause.DenClosed ? tx.ChooseDen : tx.ChooseAssign;
                Choose.interactable = false;
            }
            else { label.text = Unchanged(d.Order) ? tx.ChooseNext : tx.ChooseAssign; Choose.interactable = true; }
            var moved = d.Notes.Count > 0 ? d.Notes[0] : (FieldMoveNote?)null;
            int listener = -1; for (int i = 0; i < d.Check.Actions.Length; i++) if (d.Check.Actions[i] == FieldAction.Listen && arrival.Participants[i] != Worker) { listener = i; break; }
            Notice.text = moved.HasValue ? (moved.Value.Kind == FieldMoveKind.ListenMoved ? string.Format(tx.NoticeListenMoved, arrival.Participants[moved.Value.Member].Name, FieldSiteState.RoomNames[moved.Value.Door])
                    : string.Format(tx.NoticeMoved, arrival.Participants[moved.Value.Member].Name, arrival.ObjectNames[moved.Value.Site]))
                : notice ?? (Worker == null && standing != null ? tx.NoticeRelease
                : run == null && Worker != null && pause == FieldPause.NoTool ? string.Format(tx.NoticeTool, planner.ToolName(site))
                : run == null && Worker != null && pause == FieldPause.NoLight ? tx.NoticeLight
                : run == null && Worker != null && pause == FieldPause.DenClosed ? tx.NoticeDen : s.Progress > 0 ? tx.NoticeLocked : standing != null && Worker != null ? tx.NoticeLeadAgain : Worker != null ? (!d.SoloShown ? tx.NoticeDutyAgain : d.Offered.Any(x => x) ? tx.NoticeSolo : listener >= 0 ? string.Format(tx.NoticeListenBusy, arrival.Participants[listener].Name) : tx.NoticeNoFree) : Duty == 2 ? "손전등은 소모되지 않습니다 · 수색도는 유지됩니다." : "조명 지원: 수색 담당자 외 손전등을 가진 동료 필요");
            string observationMove = run != null ? ObservationMoveLine(Member(Worker)) : null;
            if (observationMove != null) Notice.text = observationMove + (moved.HasValue ? "\n" + Notice.text : "");
        }

        void BoardAsk()
        {
            var planner = Planner; var tx = planner.Texts; var standing = planner.Plan.Find(site);
            if (Worker == null)
            {
                // Release at once: no review, no time. Progress stays with the object.
                if (standing == null) return;
                planner.Plan.Release(site); notice = tx.NoticeReleased; BoardRefresh(); arrival.Threat.Refresh(); return;
            }
            var d = Draft(); var run = d.Check.RunFor(site); if (run == null) return;
            bool full = d.Check.Full, same = Unchanged(d.Order), runs = full || same; var s = arrival.Loot.State(site);
            string tool = arrival.Loot.Sites[site].RequiredTool;
            var lines = new List<string>
            {
                string.Format(tx.ReviewTitle, Title.text, NoiseLabel(run.Noise)), "",
                string.Format(tx.ReviewLead, Worker.Name),
                run.Support >= 0 ? string.Format(run.Duty == 2 ? tx.ReviewLight : run.Duty == 1 ? tx.ReviewWatch : tx.ReviewTogether, arrival.Participants[run.Support].Name) : Solo || s.Progress > 0 ? tx.ReviewSolo : tx.ReviewSoloBusy,
                string.Format(tx.ReviewProgress, run.Before, run.After, run.Required) + (run.Completes ? tx.ReviewComplete : "") + (!string.IsNullOrEmpty(tool) && !s.Opened ? tx.ReviewOpens : "")
            };
            foreach (var n in d.Notes) lines.Add(n.Kind == FieldMoveKind.ListenMoved ? string.Format(tx.ReviewListenMoved, arrival.Participants[n.Member].Name, FieldSiteState.RoomNames[n.Door]) : string.Format(tx.ReviewMoved, arrival.Participants[n.Member].Name, arrival.ObjectNames[n.Site]));
            string observationMove = ObservationMoveLine(Member(Worker));
            if (observationMove != null) lines.Add(observationMove);
            // Reassignment notices take the separator's line so the cost/turn preview stays inside the existing paper.
            if (d.Notes.Count == 0 && observationMove == null) lines.Add("");
            lines.Add(runs ? (d.Check.Runs.Count > 1 ? string.Format(tx.ReviewRunMany, d.Check.Runs.Count) : "") + (full ? tx.ReviewRunFull : tx.ReviewRunSame) : tx.ReviewSave);
            lines.Add((runs ? "" : tx.ReviewChipPrefix) + planner.ChipLines(d.Check, d.Outlook, d.Pass, " · "));
            ReviewBody.text = string.Join("\n", lines);
            Confirm.GetComponentInChildren<Text>().text = runs ? tx.ConfirmRun : tx.ConfirmSave;
            Review.SetActive(true); Workspace.interactable = Workspace.blocksRaycasts = false;
        }
        void BoardSave()
        {
            if (!IsOpen || !Review.activeSelf || Worker == null || Worker.Health <= 0) return;
            var planner = Planner; var d = Draft(); var run = d.Check.RunFor(site); bool runs = d.Check.Full || Unchanged(d.Order);
            Dismiss();
            if (run == null) { BoardRefresh(); return; }
            planner.Plan.Assign(d.Order); prefer = -1; notice = null;
            if (runs) { if (!planner.Run(site)) BoardRefresh(); return; }
            // Saved only: back to the room, where '턴 진행' runs everything together.
            planner.Plan.Keep(planner.Plan.Check(planner.Facts()));
            string name = arrival.ObjectNames[site]; Close();
            arrival.Status.text = string.Format(planner.Texts.StatusSaved, name); arrival.Threat.Refresh();
        }
    }
}
