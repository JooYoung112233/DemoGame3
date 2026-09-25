using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // The press area of one world pawn on the canvas (기획/탐험-말놓기-조작-재설계.md): the pawns and the silhouettes are world
    // sprites under a Screen Space – Camera canvas that draws over them, so each gets a transparent UI Button that FieldPawnBoard
    // keeps over it every frame (the tutorial and the verify scripts press these Buttons). A press counts only on the pawn's own
    // shape: the visible body narrowed by BodyInset on each side, plus the ellipse of its base (ICanvasRaycastFilter), so an
    // object behind a pawn stays pressable around it. Left press = the Button (the board: pick up / place); right press, hover
    // and the drag of a pawn (carry it onto a place) come here and go to the board. Presentation only.
    [DisallowMultipleComponent]
    public sealed class FieldPawnHandle : MonoBehaviour, ICanvasRaycastFilter, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
        IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public enum Kind { Pawn, Ghost }
        [Tooltip("대원 말 (누르면 집기 · 끌기) · 실루엣 (누르면 그 자리에 놓기)")] public Kind Role;
        public FieldPawnBoard Board;
        public Button Button;
        [Tooltip("몸 누름 영역을 좌우에서 줄이는 비율 (보이는 몸 폭 대비)")] [Range(0, .45f)] public float BodyInset = .2f;

        // Member (a pawn) or pool index (a silhouette); −1 = unused. Set by the board.
        public int Index { get; set; } = -1;
        public SpriteRenderer Body { get; set; }
        public SpriteRenderer Base { get; set; }
        public bool Dragging { get; private set; }
        public bool Hovered { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        void Awake() { if (!Button) Button = GetComponent<Button>(); if (Button) Button.onClick.AddListener(() => { if (Board) Board.OnHandlePressed(this); }); }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) => Contains(screenPoint);
        // The pawn's own shape under a screen point (the world camera draws the pawns).
        public bool Contains(Vector2 screen)
        {
            if (!Board || !Body || !Body.enabled || !Body.gameObject.activeInHierarchy) return false;
            var cam = FieldPawnBoard.WorldCamera; if (!cam) return false;
            var w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z - Body.transform.position.z)));
            var b = Board.VisibleBounds(Body); float inset = b.size.x * BodyInset;
            if (w.x >= b.min.x + inset && w.x <= b.max.x - inset && w.y >= b.min.y && w.y <= b.max.y) return true;
            if (!Base || !Base.enabled) return false;
            var e = Base.bounds; if (e.extents.x <= 0 || e.extents.y <= 0) return false;
            float dx = (w.x - e.center.x) / e.extents.x, dy = (w.y - e.center.y) / e.extents.y;
            return dx * dx + dy * dy <= 1;
        }

        // Right press only (the Button takes the left press).
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Right && Board) Board.OnHandleRight(this); }
        public void OnPointerEnter(PointerEventData e) { Hovered = true; }
        public void OnPointerExit(PointerEventData e) { Hovered = false; }

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = true; Dragging = false; }
        public void OnBeginDrag(PointerEventData e)
        {
            if (Role != Kind.Pawn || e.button != PointerEventData.InputButton.Left || !Board) return;
            Dragging = Board.BeginCarry(this, e.position);
        }
        public void OnDrag(PointerEventData e) { if (Dragging && Board) Board.Carry(e.position); }
        // The release click (sent before this) was ignored while Dragging: the drop is resolved here.
        public void OnEndDrag(PointerEventData e) { if (!Dragging) return; Dragging = false; if (Board) Board.EndCarry(e.position); }
        void OnDisable() { Hovered = false; if (Dragging) { Dragging = false; if (Board) Board.EndCarry(new Vector2(-1e5f, -1e5f)); } }
    }
}
