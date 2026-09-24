using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Texts of the next-turn move reservation (ExpeditionRoomNavigation.Queue.cs) on the site board: the door popup's
    // confirm reads '다음 턴 이동 예약' (or '예약 취소' at the reserved door), and while a move is reserved the '턴 진행'
    // subtitle and the plan chip say the move happens instead. Runs after the planner writes them (text only, no layout).
    [DefaultExecutionOrder(300)]
    public sealed class FieldMoveQueueView : MonoBehaviour
    {
        public ExpeditionArrivalPanel Arrival;
        [Header("문 확인창 버튼")]
        [Tooltip("두 번째 방문부터 문 확인창의 확인 버튼")] public string ReserveLabel = "다음 턴 이동 예약";
        [Tooltip("이미 예약한 문을 다시 눌렀을 때")] public string CancelLabel = "예약 취소";
        [Header("'턴 진행' 버튼 · 이동 예약 중")]
        [Tooltip("부제 ({0}: 갈 방, {1}: 걸리는 턴)")] public string TurnSubtitleFormat = "{0}로 이동 · {1}턴";
        [Header("미리보기 두 줄 · 이동 예약 중")]
        [Tooltip("첫 줄 ({0}: 갈 방, {1}: 걸리는 턴)")] public string ChipMove = "다음 턴 {0}로 이동 · {1}턴";
        [Tooltip("둘째 줄: 조용히 이동 ({0}: 배정 해제 문구)")] public string ChipQuiet = "소음 없음{0}";
        [Tooltip("둘째 줄: 문을 열며 이동 ({0}: 소음, {1}: 배정 해제 문구)")] public string ChipNoisy = "소음 +{0}{1}";
        [Tooltip("배정이 있을 때 붙는 문구")] public string ChipReleases = " · 배정 해제";
        [Tooltip("둘째 줄: 들어가면 마주칠 때")] public string ChipMeets = "들어가면 무언가와 마주칩니다";
        bool wrote; string subtitle, chip; (int room, int turns, int clock, int version, FieldSiteState state) key;

        void LateUpdate()
        {
            var a = Arrival; var rooms = a ? a.Rooms : null; var t = a ? a.Threat : null; var pl = t ? t.Planner : null;
            if (!rooms || !pl || !a.IsOpen) { wrote = false; return; }
            // The door popup of the site board: confirming reserves (the first visit keeps '이동 · 1턴').
            if (a.Popup.activeSelf && rooms.PendingRoom >= 0 && pl.Active && t.State != null && !t.State.Encounter && a.ReturnConfirm && a.ReturnConfirm.gameObject.activeSelf)
            {
                var label = a.ReturnConfirm.GetComponentInChildren<Text>(); string want = rooms.QueuedRoom == rooms.PendingRoom ? CancelLabel : ReserveLabel;
                if (label && label.text != want) label.text = want;
            }
            if (!rooms.HasQueuedMove)
            {
                // The reservation went (cancelled, moved, dropped): the planner writes its own lines again.
                if (wrote) { wrote = false; pl.Refresh(); }
                return;
            }
            var now = (rooms.QueuedRoom, rooms.QueuedTurns, t.State.TurnsUsed, pl.Plan.Version, t.State);
            if (!wrote || now != key) { key = now; Compose(rooms, pl); }
            if (pl.TurnSubtitle && pl.TurnSubtitle.text != subtitle) pl.TurnSubtitle.text = subtitle;
            if (pl.Chip && pl.Chip.text != chip) pl.Chip.text = chip;
            wrote = true;
        }
        void Compose(ExpeditionRoomNavigation rooms, FieldTurnPlanner pl)
        {
            string room = FieldSiteState.RoomNames[rooms.QueuedRoom]; int turns = rooms.QueuedTurns;
            var o = rooms.QueuedMoveOutlook(); int noise = rooms.QueuedRoom == 2 && !rooms.StorageUnlocked ? rooms.UnlockNoise : 0;
            string release = pl.Plan.HasAssignments ? ChipReleases : "";
            string second = o != null && o.Encounter ? "<color=#" + ColorUtility.ToHtmlStringRGB(pl.ChipWarnColor) + ">" + ChipMeets + "</color>"
                : noise > 0 ? string.Format(ChipNoisy, noise, release) : string.Format(ChipQuiet, release);
            subtitle = string.Format(TurnSubtitleFormat, room, turns);
            chip = string.Format(ChipMove, room, turns) + "\n" + second;
        }
    }
}
