using UnityEngine;

namespace Demo5.FrontEnd
{
    // 방 영역 (탐험 화면 · Main): the room part of the exploration screen, above the dark tray (대원 · 이번 턴 · 행동), and the screen
    // papers that things drawn over the room must keep clear of (the place and time papers, the tray, the turn band). Moved off
    // FieldPlanTargetMarkers (the assignment bubbles, retired by 말 놓기 · 기획/탐험-말놓기-조작-재설계.md) so that layer can go in phase 2.
    // Data only: nothing here moves or hides anything. Written by AgentScripts/BuildRetireOldAssign.cs (from the bubbles' values while
    // they exist); the layout checks (VerifyExplorationHudTray, VerifyTurnFeedback) read it.
    [DisallowMultipleComponent]
    public sealed class FieldRoomArea : MonoBehaviour
    {
        [Tooltip("방 영역 (Main 기준 · 왼쪽 위 원점 px: x, y, 폭, 높이). 아래 판 위까지")] public Rect RoomArea = new Rect(16, 8, 1888, 752);
        [Tooltip("방 위 표시가 가리면 안 되는 화면 요소 (날짜·장소·턴 종이, 아래 판, 버튼). 켜져 있을 때만 봅니다")] public RectTransform[] KeepClear;
        [Tooltip("턴 연출 중에만 켜지는 요소 (턴 띠). 꺼져 있어도 비워 둡니다")] public RectTransform[] KeepClearAlways;

        readonly Vector3[] corners = new Vector3[4];
        public static FieldRoomArea Of(ExpeditionArrivalPanel a) => a && a.Main ? a.Main.GetComponent<FieldRoomArea>() : null;
        // The room area in this node's local space.
        public Rect Area { get { var r = ((RectTransform)transform).rect; return new Rect(r.xMin + RoomArea.x, r.yMax - RoomArea.y - RoomArea.height, RoomArea.width, RoomArea.height); } }
        // A rect in this node's local space.
        public Rect RectOf(RectTransform r)
        {
            r.GetWorldCorners(corners); Vector3 a = transform.InverseTransformPoint(corners[0]), b = transform.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }
        // Inside the room and over none of the papers to keep clear (the active ones, and the always ones).
        public bool IsClear(Rect local)
        {
            var area = Area; if (local.xMin < area.xMin - .5f || local.xMax > area.xMax + .5f || local.yMin < area.yMin - .5f || local.yMax > area.yMax + .5f) return false;
            if (KeepClear != null) foreach (var k in KeepClear) if (k && k.gameObject.activeInHierarchy && RectOf(k).Overlaps(local)) return false;
            if (KeepClearAlways != null) foreach (var k in KeepClearAlways) if (k && RectOf(k).Overlaps(local)) return false;
            return true;
        }
    }
}
