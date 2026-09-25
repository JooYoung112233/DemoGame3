using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 다음 턴 이동 (말 놓기 · 기획/탐험-말놓기-조작-재설계.md): every living member standing at one door of the room (the planner's plan:
    // FieldTurnPlan gathers, plus the door's listener while more than one member lives) is the move next turn; the next '턴 진행'
    // performs it instead of the member turn (hook in FieldTurnPlanner.Run). There is no stored reservation: taking any pawn away
    // cancels it, and it dies with the room, the visit or an encounter. The locked storage door (option A): everyone gathered with the
    // prybar in someone's bag is '문 따고 보관실로' (1 + UnlockTurns turns, its noise in the storage). Both visits (FieldTurnPlanner.Placing).
    // The door popup (AskMove / AskStorage → ConfirmMove) moves at once: the encounter's retreat and the verify scripts use it.
    public sealed partial class ExpeditionRoomNavigation
    {
        [Header("다음 턴 이동 · 모든 대원이 한 문에 모였을 때")]
        [Tooltip("문 위와 턴 버튼에 쓰는 예약 문구 ({0}: 갈 방)")] public string QueueLabelFormat = "다음 턴 · {0}로 이동";
        [Tooltip("잠긴 문을 열고 들어가는 예약 ({0}: 갈 방, {1}: 걸리는 턴)")] public string QueueUnlockFormat = "다음 턴 · 문 열고 {0}로 ({1}턴)";
        [Tooltip("모두 모였을 때 상황판 ({0}: 갈 방) · 말 놓기는 FieldPlacementTexts.StatusGathered를 씁니다")] [TextArea(2, 3)] public string QueueStatus = "{0}로 이동 예약\n'턴 진행'을 누르면 이동합니다.";
        [Tooltip("모인 대원을 흩었을 때 상황판")] [TextArea(2, 3)] public string QueueCancelStatus = "이동 예약을 취소했습니다.\n시간은 흐르지 않았습니다.";
        [Tooltip("턴 진행 때 이동할 수 없게 됐을 때 상황판 ({0}: 까닭)")] [TextArea(2, 3)] public string QueueDroppedStatus = "이동 예약 취소 · {0}\n시간은 흐르지 않았습니다.";
        [Tooltip("잠긴 문을 열 도구를 가진 대원이 없어졌을 때의 까닭")] public string QueueNoToolReason = "도구를 가진 대원이 없음";

        FieldTurnPlanner QueuePlanner => owner && owner.Threat ? owner.Threat.Planner : null;
        // The door everyone stands at, from this room, on an open board without an encounter (-1 when none).
        int Gathered()
        {
            if (!owner || !owner.IsOpen) return -1;
            var t = owner.Threat; var pl = QueuePlanner;
            if (!t || !pl || !pl.Placing || t.State == null || owner.Encounter && owner.Encounter.IsOpen) return -1;
            int door = pl.GatheredDoor;
            return door >= 0 && DoorButton(CurrentRoom, door) ? door : -1;
        }

        public bool HasQueuedMove => Gathered() >= 0;
        // Destination room index (-1 when none).
        public int QueuedRoom => Gathered();
        // The door hotspot the move goes through (anchor for its marker).
        public RectTransform QueuedDoor { get { int next = Gathered(); var door = next >= 0 ? DoorButton(CurrentRoom, next) : null; return door ? (RectTransform)door.transform : null; } }
        public string QueuedLabel { get { int next = Gathered(); return next < 0 ? "" : next == 2 && !StorageUnlocked ? string.Format(QueueUnlockFormat, FieldSiteState.RoomNames[next], MoveTurns(next)) : string.Format(QueueLabelFormat, FieldSiteState.RoomNames[next]); } }
        // Turns the move spends (a lock forced open on the way adds its turns).
        public int QueuedTurns { get { int next = Gathered(); return next < 0 ? 0 : MoveTurns(next); } }
        // Turns a move to `next` takes from here.
        public int MoveTurns(int next) => 1 + (next == 2 && !StorageUnlocked ? UnlockTurns : 0);
        // Sends everyone waiting at doors back (the door's listener keeps listening): the move is off. No time.
        public void CancelQueuedMove() { var pl = QueuePlanner; if (pl && pl.Plan.ReleaseGathers() && owner.Threat) owner.Threat.Refresh(); }

        // The same turn as ConfirmMove, without the popup. True when a gathered move was handled (moved, or dropped because it can no
        // longer be made: then no time passes); false when there is none.
        public bool RunQueuedMove()
        {
            int next = Gathered(); if (next < 0) return false;
            if (owner.InTransit || owner.Popup.activeSelf) return false;
            bool unlock = next == 2 && !StorageUnlocked;
            if (next == 2 && (CurrentRoom != 1 || !Storage) || unlock && UnlockWorker() == null)
            {
                CancelQueuedMove(); owner.Status.text = string.Format(QueueDroppedStatus, QueueNoToolReason); if (owner.Threat) owner.Threat.Refresh(); return true;
            }
            // Everyone leaves the room: the plan belongs to it (PartyArrived clears the rest on arrival).
            var pl = QueuePlanner; if (pl) pl.Plan.Clear();
            int cost = MoveTurns(next);
            owner.SetRoomTransit(true); if (unlock) { StorageUnlocked = true; Noise += UnlockNoise; }
            Turns += cost; owner.SpendFieldTime(cost * MinutesPerTurn); RefreshLabels(); StartCoroutine(Travel(next, cost, unlock ? UnlockNoise : 0));
            return true;
        }
        // Where the party would stand after the gathered move resolves (a copy; the real state is never touched). Null when none.
        public FieldSiteState QueuedMoveOutlook()
        {
            int next = Gathered(); if (next < 0) return null;
            var o = owner.Threat.State.Copy(); int turns = MoveTurns(next), noise = next == 2 && !StorageUnlocked ? UnlockNoise : 0; o.MoveParty(next);
            for (int i = 0; i < Mathf.Max(1, turns); i++) { o.EndTurn(i == 0 ? noise : 0, false); if (o.Encounter) break; }
            return o;
        }
        // Put every living member at the door onto `next` (a test and tool helper: the pawn board places them one by one). They leave
        // what they did. No time. True when the move is then set (not a move door from here, no tool for the lock, a window or walk under way: false).
        public bool QueueMove(int next)
        {
            if (!owner || !owner.IsOpen || owner.InTransit) return false;
            var t = owner.Threat; var pl = QueuePlanner; if (!t || !pl || !pl.Placing || t.State == null || owner.Encounter && owner.Encounter.IsOpen) return false;
            if (!FieldSiteState.Adjacent(CurrentRoom, next) || next == FieldSiteState.Den || !DoorButton(CurrentRoom, next)) return false;
            bool unlock = next == 2 && !StorageUnlocked;
            if (next == 2 && (CurrentRoom != 1 || !Storage) || unlock && UnlockWorker() == null) return false;
            var facts = pl.Facts(); int alive = 0; for (int m = 0; m < owner.Participants.Count; m++) if (facts.Alive(m)) alive++;
            for (int m = 0; m < owner.Participants.Count; m++)
            {
                if (!facts.Alive(m)) continue;
                var l = pl.Plan.ListenBy(m); if (alive > 1 && l != null && l.Door == next) continue; // the listener there counts (not a lone member)
                pl.Plan.Unassign(m, facts); pl.Plan.AssignGather(m, next);
            }
            t.Refresh();
            return QueuedRoom == next;
        }
        Button DoorButton(int from, int to) => from == 0 ? (owner.Objects != null && owner.Objects.Length > 3 ? owner.Objects[3] : null) : from == 1 ? (to == 0 ? CorridorBack : to == 2 ? LockedDoor : null) : from == 2 ? StorageBack : null;
    }
}
