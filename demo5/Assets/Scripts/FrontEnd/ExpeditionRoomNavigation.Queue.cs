using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 다음 턴 이동 예약 (2026-09-25): on the site board (2nd visit on) confirming a door reserves the move; the next '턴 진행'
    // performs it instead of the member turn (hook in FieldTurnPlanner.Run). The first visit and the retreat out of an
    // encounter still leave at once. The same door again cancels, another door replaces the reservation.
    public sealed partial class ExpeditionRoomNavigation
    {
        [Header("다음 턴 이동 예약 · 두 번째 방문부터")]
        [Tooltip("문 위 말풍선과 턴 버튼에 쓰는 예약 문구 ({0}: 갈 방)")] public string QueueLabelFormat = "다음 턴 · {0}로 이동";
        [Tooltip("잠긴 문을 열고 들어가는 예약 ({0}: 갈 방, {1}: 걸리는 턴)")] public string QueueUnlockFormat = "다음 턴 · 문 열고 {0}로 ({1}턴)";
        [Tooltip("예약 직후 상황판 ({0}: 갈 방)")] [TextArea(2, 3)] public string QueueStatus = "{0}로 이동 예약\n'턴 진행'을 누르면 이동합니다.";
        [Tooltip("같은 문을 다시 눌러 예약을 취소했을 때 상황판")] [TextArea(2, 3)] public string QueueCancelStatus = "이동 예약을 취소했습니다.\n시간은 흐르지 않았습니다.";
        [Tooltip("턴 진행 때 예약한 이동을 할 수 없게 됐을 때 상황판 ({0}: 까닭)")] [TextArea(2, 3)] public string QueueDroppedStatus = "이동 예약 취소 · {0}\n시간은 흐르지 않았습니다.";
        [Tooltip("잠긴 문을 열 도구를 가진 대원이 없어졌을 때의 까닭")] public string QueueNoToolReason = "도구를 가진 대원이 없음";

        int queued = -1, queuedFrom = -1; FieldSiteState queuedState;

        public bool HasQueuedMove => QueueValid();
        // Destination room index (-1 when none).
        public int QueuedRoom => QueueValid() ? queued : -1;
        // The door hotspot the reservation goes through (anchor for its bubble).
        public RectTransform QueuedDoor { get { var door = QueueValid() ? DoorButton(queuedFrom, queued) : null; return door ? (RectTransform)door.transform : null; } }
        public string QueuedLabel => !QueueValid() ? "" : queued == 2 && !StorageUnlocked ? string.Format(QueueUnlockFormat, FieldSiteState.RoomNames[queued], QueuedTurns) : string.Format(QueueLabelFormat, FieldSiteState.RoomNames[queued]);
        // Turns the reserved move spends (a lock forced open on the way adds its turns).
        public int QueuedTurns => !QueueValid() ? 0 : 1 + (queued == 2 && !StorageUnlocked ? UnlockTurns : 0);
        public void CancelQueuedMove() { if (queued < 0) return; queued = queuedFrom = -1; queuedState = null; if (owner && owner.Threat) owner.Threat.Refresh(); }

        // The same turn as the old ConfirmMove, without the popup. True when a reservation was handled (moved, or dropped
        // because it can no longer be made: then no time passes); false when there is none.
        public bool RunQueuedMove()
        {
            if (queued < 0) return false;
            if (!QueueValid()) { CancelQueuedMove(); return false; }
            if (!owner.IsOpen || owner.InTransit || owner.Popup.activeSelf) return false;
            int next = queued; bool unlock = next == 2 && !StorageUnlocked;
            if (next == 2 && (CurrentRoom != 1 || !Storage) || unlock && UnlockWorker() == null)
            {
                CancelQueuedMove(); owner.Status.text = string.Format(QueueDroppedStatus, QueueNoToolReason); if (owner.Threat) owner.Threat.Refresh(); return true;
            }
            int cost = 1 + (unlock ? UnlockTurns : 0); queued = queuedFrom = -1; queuedState = null;
            owner.SetRoomTransit(true); if (unlock) { StorageUnlocked = true; Noise += UnlockNoise; }
            Turns += cost; owner.SpendFieldTime(cost * MinutesPerTurn); RefreshLabels(); StartCoroutine(Travel(next, cost, unlock ? UnlockNoise : 0));
            return true;
        }
        // Where the party would stand after the reserved move resolves (a copy; the real state is never touched). Null when none.
        public FieldSiteState QueuedMoveOutlook()
        {
            if (!QueueValid()) return null;
            var o = owner.Threat.State.Copy(); int turns = QueuedTurns, noise = queued == 2 && !StorageUnlocked ? UnlockNoise : 0; o.MoveParty(queued);
            for (int i = 0; i < Mathf.Max(1, turns); i++) { o.EndTurn(i == 0 ? noise : 0, false); if (o.Encounter) break; }
            return o;
        }

        // One-line redirect at the top of ConfirmMove. Board mode only; an open encounter's retreat (State.Encounter) leaves at once.
        bool QueueInstead()
        {
            if (pending < 0 || !owner || !owner.IsOpen || owner.InTransit || !owner.Popup.activeSelf) return false;
            var t = owner.Threat; if (!t || !t.Planner || !t.Planner.Active || t.State == null || t.State.Encounter) return false;
            int next = pending;
            if (queued == next && QueueValid()) { CancelQueuedMove(); owner.ClosePopup(); owner.Status.text = QueueCancelStatus; t.Refresh(); return true; }
            bool unlock = next == 2 && !StorageUnlocked;
            if (next == 2 && (CurrentRoom != 1 || !Storage) || unlock && UnlockWorker() == null) { owner.ClosePopup(); return true; }
            queued = next; queuedFrom = CurrentRoom; queuedState = t.State;
            owner.ClosePopup(); owner.Status.text = string.Format(QueueStatus, FieldSiteState.RoomNames[next]); t.Refresh();
            return true;
        }
        // A reservation belongs to one room of one visit's board and dies with an encounter.
        bool QueueValid()
        {
            if (queued < 0 || !owner || !owner.IsOpen) return false;
            var t = owner.Threat;
            return t && t.State != null && t.State == queuedState && t.Planner && t.Planner.Active && CurrentRoom == queuedFrom && !(owner.Encounter && owner.Encounter.IsOpen) && DoorButton(queuedFrom, queued);
        }
        Button DoorButton(int from, int to) => from == 0 ? (owner.Objects != null && owner.Objects.Length > 3 ? owner.Objects[3] : null) : from == 1 ? (to == 0 ? CorridorBack : to == 2 ? LockedDoor : null) : from == 2 ? StorageBack : null;
        void Update() { if (queued >= 0 && !QueueValid()) CancelQueuedMove(); }
    }
}
