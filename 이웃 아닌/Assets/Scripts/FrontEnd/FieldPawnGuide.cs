using System;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // The tutorial's words for placing pawns (Inspector: SettlementScreen → SettlementTutorialGuide → PawnTexts). One step at a time
    // (FieldPawnBoard.GuideTarget decides which): pick up a pawn, press its silhouette, pick up the second pawn, press the co-op silhouette,
    // then '턴 진행'. Doors: every pawn to the door (the next member is picked up by itself), then '턴 진행' moves.
    [Serializable] public sealed class FieldPawnGuideTexts
    {
        [Tooltip("첫 말 들기 (사물 · 흔적)")] [TextArea(2, 3)] public string Pick = "빛나는 대원 말을 눌러 드세요.\n말이 선 자리가 이번 턴 할 일입니다.";
        [Tooltip("실루엣에 놓기 (사물 · 흔적)")] [TextArea(2, 3)] public string Place = "반투명 실루엣을 눌러 말을 놓으세요.\n놓기만 해서는 시간이 흐르지 않습니다.";
        [Tooltip("협동 · 두 번째 말 들기")] [TextArea(2, 3)] public string PickHelper = "다른 대원 말도 누르세요.\n같은 곳에 두 명을 두면 협동입니다.";
        [Tooltip("협동 · 옆 실루엣에 놓기")] [TextArea(2, 3)] public string PlaceHelper = "옆 실루엣에 놓으면 함께 수색합니다.\n둘이 함께 뒤지면 한 턴 빨리 끝납니다.";
        [Tooltip("문에 모이기 · 말 들기")] [TextArea(2, 3)] public string PickDoor = "대원 말을 눌러 문 앞에 놓으세요.\n모두 한 문에 모이면 다음 턴에 이동합니다.";
        [Tooltip("문에 모이기 · 문 앞 실루엣")] [TextArea(2, 3)] public string PlaceDoor = "문 앞 실루엣을 누르세요.\n아직 안 온 대원 말이 이어서 들립니다.";
        [Tooltip("턴 진행 (수색 · 관찰)")] [TextArea(2, 3)] public string Turn = "놓은 말이 이번 턴 할 일입니다.\n'턴 진행'을 눌러 시간을 보내세요.";
        [Tooltip("턴 진행 (모두 문에 모임)")] [TextArea(2, 3)] public string TurnMove = "모두 문 앞에 모였습니다.\n'턴 진행'을 누르면 이동합니다.";
        [Header("할 일 없는 대원 확인창 (첫 방문)")]
        public string IdleTitle = "남은 대원 · 아직 할 일 없음";
        [TextArea(2, 3)] public string IdleText = "돌아가서 남은 대원 말도 같은 곳에 놓으세요.\n둘이 함께 뒤지면 한 턴 빨리 끝납니다.";
        [Header("07 창 · 읽기 전용 (사물 우클릭)")]
        public string DetailTitle = "자세히 · 읽기 전용";
        [TextArea(2, 3)] public string DetailText = "수색은 대원 말을 사물에 놓아 맡깁니다.\n돌아가서 대원 말을 누르세요.";
    }

    // The tutorial's next press on the pawn board (SettlementTutorialGuide.Field): the board says which Button (pawn, silhouette,
    // '턴 진행'), this says what the banner reads. lead: a story line for the first step (the missing person's route), else none.
    public static class FieldPawnGuide
    {
        public static bool Next(ExpeditionArrivalPanel a, string goal, bool coop, FieldPawnGuideTexts tx, string lead, out Button target, out string text)
        {
            target = null; text = null; var board = FieldPawnBoard.For(a);
            if (!board || !board.isActiveAndEnabled || tx == null || string.IsNullOrEmpty(goal)) return false;
            target = board.GuideTarget(goal, coop, out var step); if (!target || step == FieldGuideStep.None) { target = null; return false; }
            bool door = goal.StartsWith("door:");
            string line;
            switch (step)
            {
                case FieldGuideStep.PickPawn: line = door ? tx.PickDoor : tx.Pick; break;
                case FieldGuideStep.PlaceGhost: line = door ? tx.PlaceDoor : tx.Place; break;
                case FieldGuideStep.PickHelper: line = tx.PickHelper; break;
                case FieldGuideStep.PlaceHelper: line = tx.PlaceHelper; break;
                default: line = door ? tx.TurnMove : tx.Turn; break;
            }
            text = step == FieldGuideStep.PickPawn && !string.IsNullOrEmpty(lead) ? lead + "\n" + FirstLine(line) : line;
            return true;
        }
        static string FirstLine(string s) { int cut = s.IndexOf('\n'); return cut >= 0 ? s.Substring(0, cut) : s; }
    }
}
