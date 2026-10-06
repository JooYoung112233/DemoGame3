using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 문에 귀 대기 (기획/탐험-문에귀대기-1차-구현.md · 말 놓기 2026-09-25): a member pawn placed at a door (FieldPlacement) listens on
    // every '턴 진행' while it stays there: present → its decided next step and, if the party stays quiet, the step after; absent → only
    // that it is not there (or just left). The fresh result (this turn only: door markers, the 2-turns-ahead chip, AutoStop) is
    // Reports / TryReport; everything heard this visit goes to DoorLog (hover · the door-log popup). ClearFresh on arrival and
    // encounters; the log only at the visit's start and end (Reset). The first visit (it sleeps) has no listening.
    // The door popup's listen button (ListenButton, UpdateListenOffer, FillListen, ToggleListen, HeardLine, OfferDen) is phase 1 of
    // its removal: BuildPawnRules clears ListenButton so it never shows; the code goes in phase 2.
    public sealed partial class FieldTurnPlanner
    {
        [Header("문에 귀 대기 · 문 확인창 안")]
        public Button ListenButton;
        public Text ListenTitle, ListenSubtitle;

        readonly List<(FieldDoorReport R, int Member)> reports = new List<(FieldDoorReport, int)>();
        int offerDoor = -1, offerMember = -1, offerVersion = -1, listenFrame = -1; bool offerRelease, denOffer, listenWired;
        public IReadOnlyList<(FieldDoorReport R, int Member)> Reports => reports;

        void ClearFresh() { reports.Clear(); offerDoor = -1; denOffer = false; }
        bool Fresh(FieldDoorReport r) => threat && threat.State != null && r.Turn == threat.State.TurnsUsed && r.Room == threat.State.PartyRoom;
        // A result heard at this door this turn (valid until the next turn or a move).
        public bool TryReport(int door, out FieldDoorReport r, out int member)
        {
            foreach (var x in reports) if (x.R.Door == door && Fresh(x.R)) { r = x.R; member = x.Member; return true; }
            r = default; member = -1; return false;
        }
        public bool HeardPresent(out FieldDoorReport r, out int member)
        {
            foreach (var x in reports) if (x.R.Present && Fresh(x.R)) { r = x.R; member = x.Member; return true; }
            r = default; member = -1; return false;
        }
        // One line for the move popup of that door: what was heard behind it this turn.
        public string HeardLine(int door)
        {
            if (!Active || door < 0 || !TryReport(door, out var r, out _)) return "";
            return "\n" + (!r.Present ? Texts.MoveHeardQuiet : r.Next == door || r.Next == r.Room ? Texts.MoveHeardMeet : string.Format(Texts.MoveHeardLeave, FieldSiteState.RoomNames[r.Next]));
        }
        // The den door popup (it is home): offer listening at it.
        public void OfferDen() { if (Active) denOffer = true; }

        // This turn's results (the fresh set) and, for the rest of the visit, the door log (FieldDoorLog: hover · door-log popup).
        void Heard(FieldPlanCheck k)
        {
            reports.Clear();
            foreach (var l in k.Listens.OrderBy(x => x.Door))
            {
                var r = threat.State.ListenAt(l.Door); reports.Add((r, l.Member));
                if (DoorLog != null) DoorLog.Add(r, l.Member);
            }
        }
        FieldPause ListenBlock(int door) => ((IFieldListenFacts)facts).ListenBlock(door);

        // The popup shows the listen button while it asks about a door that can be listened at.
        void UpdateListenOffer()
        {
            if (!ListenButton) return;
            if (!listenWired) { ListenButton.onClick.AddListener(ToggleListen); listenWired = true; }
            int door = -1;
            if (arrival.Popup.activeSelf && Active && !resolving)
            {
                door = arrival.Rooms.PendingRoom; if (door < 0 && denOffer) door = FieldSiteState.Den;
                if (door >= 0 && ListenBlock(door) != FieldPause.None) door = -1;
            }
            else if (!arrival.Popup.activeSelf) denOffer = false;
            bool show = door >= 0;
            if (ListenButton.gameObject.activeSelf != show) ListenButton.gameObject.SetActive(show);
            if (!show) { offerDoor = -1; return; }
            if (door != offerDoor || Plan.Version != offerVersion) FillListen(door);
        }
        void FillListen(int door)
        {
            offerDoor = door; offerVersion = Plan.Version; var tx = Texts; var standing = Plan.ListenAt(door);
            string Name(int m) => m >= 0 && m < arrival.Participants.Count ? arrival.Participants[m].Name : "";
            if (standing != null)
            {
                offerMember = standing.Member; offerRelease = true; ListenButton.interactable = true;
                if (ListenTitle) ListenTitle.text = string.Format(tx.ListenReleaseTitle, Name(standing.Member));
                if (ListenSubtitle) ListenSubtitle.text = tx.ListenReleaseSubtitle;
                return;
            }
            var now = Plan.Check(facts); int m = FieldTurnPlan.PickListener(now); offerMember = m; offerRelease = false; ListenButton.interactable = m >= 0;
            if (m < 0)
            {
                if (ListenTitle) ListenTitle.text = tx.ListenNobodyTitle;
                if (ListenSubtitle) ListenSubtitle.text = tx.ListenNobody;
                return;
            }
            var draft = Plan.Clone(); draft.AssignListen(m, door);
            var after = Forecast(draft); var before = Forecast(Plan); var a = now.Actions[m];
            string sub;
            if (after.outlook.Encounter && !before.outlook.Encounter) sub = Colored(tx.ListenBreaksPass, WarnColor);
            else if (a == FieldAction.Together || a == FieldAction.Watch || a == FieldAction.Light)
            {
                int site = now.SiteOf[m]; string role = a == FieldAction.Light ? tx.CardLight : a == FieldAction.Watch ? tx.CardWatch : tx.CardTogether;
                sub = string.Format(tx.ListenFromSupport, site >= 0 && site < arrival.ObjectNames.Length ? arrival.ObjectNames[site] : "", role) + (a == FieldAction.Light ? tx.ListenLightPause : "");
            }
            else if (a == FieldAction.Listen || a == FieldAction.Paused && now.DoorOf[m] >= 0) sub = string.Format(tx.ListenFromDoor, FieldSiteState.RoomNames[now.DoorOf[m]]);
            else sub = before.check.HushedAll ? tx.ListenBreaksHush : tx.ListenQuiet;
            if (ListenTitle) ListenTitle.text = string.Format(tx.ListenTitle, Name(m));
            if (ListenSubtitle) ListenSubtitle.text = sub;
        }
        // Put the offered member at the door, or take them off it. No time passes; the popup closes.
        void ToggleListen()
        {
            if (Time.frameCount == listenFrame) return; listenFrame = Time.frameCount;
            if (!Active || offerDoor < 0 || offerMember < 0 || !arrival.Popup.activeSelf) return;
            var tx = Texts; string name = arrival.Participants[offerMember].Name, doorName = FieldSiteState.RoomNames[offerDoor]; string status;
            if (offerRelease) { Plan.ReleaseListen(offerDoor); status = string.Format(tx.StatusListenOff, doorName); }
            else
            {
                var was = Plan.Check(facts).Actions[offerMember];
                var notes = Plan.AssignListen(offerMember, offerDoor); Plan.Keep(Plan.Check(facts));
                var left = notes.FirstOrDefault(n => n.Kind != FieldMoveKind.ListenMoved); var moved = notes.FirstOrDefault(n => n.Kind == FieldMoveKind.ListenMoved);
                if (notes.Any(n => n.Kind != FieldMoveKind.ListenMoved))
                    status = string.Format(tx.StatusListenFromSupport, name, doorName, left.Site >= 0 && left.Site < arrival.ObjectNames.Length ? arrival.ObjectNames[left.Site] : "", was == FieldAction.Lead ? tx.CardLead : was == FieldAction.Light ? tx.CardLight : was == FieldAction.Watch ? tx.CardWatch : tx.CardTogether);
                else if (notes.Any(n => n.Kind == FieldMoveKind.ListenMoved)) status = string.Format(tx.StatusListenFromDoor, name, doorName, FieldSiteState.RoomNames[moved.Door]);
                else status = string.Format(tx.StatusListenSet, name, doorName);
            }
            offerDoor = -1; arrival.ClosePopup(); arrival.Status.text = status; threat.Refresh();
        }
        string ListenStatus(FieldPlanCheck k, List<(int Door, int Member, FieldPause Reason)> deaf)
        {
            var den = deaf.FirstOrDefault(x => x.Reason == FieldPause.DenEmpty);
            if (deaf.Any(x => x.Reason == FieldPause.DenEmpty)) return string.Format(Texts.StatusListenReleased, FieldSiteState.RoomNames[den.Door], Texts.ReleaseListenDen);
            if (k.Runs.Count == 0 && k.Listens.Count > 0) return string.Format(Texts.StatusListened, k.Listens.Count);
            return null;
        }
    }
}
