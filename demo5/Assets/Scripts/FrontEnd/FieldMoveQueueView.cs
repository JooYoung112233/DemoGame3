using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // Texts of the next-turn move (ExpeditionRoomNavigation.Queue.cs): every living member's pawn placed at one door (말 놓기 ·
    // 기획/탐험-말놓기-조작-재설계.md) is the move on the next '턴 진행'; while it stands the '턴 진행' subtitle and the plan chip say the move
    // happens instead. The door popup no longer reserves anything (its confirm is an immediate move, kept for the encounter's retreat
    // and the verify scripts), so this view writes no popup label. Runs after the planner writes them (text only, no layout).
    [DefaultExecutionOrder(300)]
    public sealed class FieldMoveQueueView : MonoBehaviour
    {
        public ExpeditionArrivalPanel Arrival;
        [Header("문 확인창 버튼 (쓰지 않음 · 말 놓기: 모두 문에 놓으면 이동)")]
        [Tooltip("쓰지 않음 (옛 문 확인창의 예약 버튼)")] public string ReserveLabel = "다음 턴 이동 예약";
        [Tooltip("쓰지 않음 (옛 예약 취소 버튼)")] public string CancelLabel = "예약 취소";
        [Header("'턴 진행' 버튼 · 모두 문에 모였을 때")]
        [Tooltip("부제 ({0}: 갈 방, {1}: 걸리는 턴)")] public string TurnSubtitleFormat = "{0}로 이동 · {1}턴";
        [Header("미리보기 두 줄 · 모두 문에 모였을 때")]
        [Tooltip("첫 줄 ({0}: 갈 방, {1}: 걸리는 턴)")] public string ChipMove = "다음 턴 {0}로 이동 · {1}턴";
        [Tooltip("둘째 줄: 조용히 이동 ({0}: 배정 해제 문구)")] public string ChipQuiet = "소음 없음{0}";
        [Tooltip("둘째 줄: 문을 열며 이동 ({0}: 소음, {1}: 배정 해제 문구)")] public string ChipNoisy = "소음 +{0}{1}";
        [Tooltip("문 밖의 할 일(수색 · 관찰 · 가방 사용)이 남아 있을 때 붙는 문구 · 문의 귀 대기와 모인 대원은 이동 그 자체라 붙지 않습니다")] public string ChipReleases = " · 배정 해제";
        [Tooltip("둘째 줄: 들어가면 마주칠 때")] public string ChipMeets = "들어가면 무언가와 마주칩니다";
        bool wrote; string subtitle, chip; (int room, int turns, int clock, int version, FieldSiteState state) key;

        void LateUpdate()
        {
            var a = Arrival; var rooms = a ? a.Rooms : null; var t = a ? a.Threat : null; var pl = t ? t.Planner : null;
            if (!rooms || !pl || !a.IsOpen) { wrote = false; return; }
            if (!rooms.HasQueuedMove)
            {
                // The move went (a pawn left the door, the party moved, a meeting): the planner writes its own lines again.
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
            // Everyone stands at the door, so only work left outside it would be dropped by the move (none in practice; kept for plans made in code).
            var plan = pl.Plan; string release = plan.Orders.Count > 0 || plan.Observations.Count > 0 || plan.Uses.Count > 0 ? ChipReleases : "";
            string second = o != null && o.Encounter ? "<color=#" + ColorUtility.ToHtmlStringRGB(pl.ChipWarnColor) + ">" + ChipMeets + "</color>"
                : noise > 0 ? string.Format(ChipNoisy, noise, release) : string.Format(ChipQuiet, release);
            subtitle = string.Format(TurnSubtitleFormat, room, turns);
            chip = string.Format(ChipMove, room, turns) + "\n" + second;
        }
    }
}
