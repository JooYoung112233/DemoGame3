using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // The member card's input on the site board (a transparent layer over the whole card, its last child): press = pick up this
    // member's pawn on the pawn board (FieldPawnBoard · 기획/탐험-말놓기-조작-재설계.md), press again = let go, right press = let go
    // (or open the bag while nothing is held). The bag line still opens the bag. A mostly sideways drag scrolls the member row;
    // the card itself is no longer carried (the pawn is: drag it in the room). Off (no raycast) outside the board and in copies of
    // the card, so the card's own button (the bag) works as before; code that presses the card button directly is unaffected.
    [DisallowMultipleComponent]
    public sealed class FieldMemberCardInput : MonoBehaviour, IPointerClickHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public ExpeditionMemberCard Card;
        public FieldMemberActionSlot Slot;
        [Tooltip("투명 입력 면 (카드 전체)")] public Graphic Hit;
        [Tooltip("누르면 가방이 열리는 곳 (가방 줄) · 켜져 있을 때만")] public RectTransform[] BagZones;
        [Tooltip("옆으로 끈 거리가 위아래의 이 배수보다 크면 카드 줄을 스크롤합니다")] [Min(1)] public float ScrollBias = 1.4f;

        public bool On => Hit && Hit.raycastTarget;
        FieldPawnBoard board; ScrollRect scroll; bool scrolling, dragged;

        void Start()
        {
            var a = Card ? Card.GetComponentInParent<ExpeditionArrivalPanel>(true) : null;
            if (a && a.MemberContent && Card.transform.parent == a.MemberContent) return;
            SetHit(false); if (Slot) Slot.SetSelected(false);
        }
        public void SetHit(bool on) { if (Hit && Hit.raycastTarget != on) Hit.raycastTarget = on; }

        FieldPawnBoard Board
        {
            get
            {
                if (board) return board;
                var a = Card ? Card.GetComponentInParent<ExpeditionArrivalPanel>(true) : null;
                board = FieldPawnBoard.For(a); return board;
            }
        }
        Camera Cam { get { var c = GetComponentInParent<Canvas>(); return c ? c.rootCanvas.worldCamera : null; } }
        bool InBag(Vector2 screen)
        {
            if (BagZones == null) return false;
            foreach (var z in BagZones) if (z && z.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(z, screen, Cam)) return true;
            return false;
        }
        void OpenBag() { if (Card && Card.Button && Card.Button.IsInteractable()) Card.Button.onClick.Invoke(); }

        public void OnPointerClick(PointerEventData e)
        {
            // A drag (the row scroll) ends in a click on the same card: not a press.
            if (dragged || scrolling) return;
            var b = Board;
            if (!b) { if (e.button == PointerEventData.InputButton.Left) OpenBag(); return; }
            if (e.button == PointerEventData.InputButton.Right) { if (b.Held >= 0) b.Cancel(); else OpenBag(); return; }
            if (e.button != PointerEventData.InputButton.Left) return;
            if (InBag(e.position)) { b.Cancel(); OpenBag(); return; }
            b.OnCard(Card);
        }

        public void OnInitializePotentialDrag(PointerEventData e) { e.useDragThreshold = true; scroll = GetComponentInParent<ScrollRect>(); scrolling = dragged = false; }
        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            dragged = true;
            var d = e.position - e.pressPosition;
            if (scroll && scroll.horizontal && Mathf.Abs(d.x) > Mathf.Abs(d.y) * ScrollBias && scroll.content && scroll.viewport && scroll.content.rect.width > scroll.viewport.rect.width + 1)
            {
                scrolling = true; ExecuteEvents.Execute(scroll.gameObject, e, ExecuteEvents.beginDragHandler);
            }
        }
        public void OnDrag(PointerEventData e) { if (scrolling && scroll) ExecuteEvents.Execute(scroll.gameObject, e, ExecuteEvents.dragHandler); }
        // The release click came before this (and was skipped): the next press is a press again.
        public void OnEndDrag(PointerEventData e)
        {
            dragged = false;
            if (!scrolling) return;
            scrolling = false; if (scroll) ExecuteEvents.Execute(scroll.gameObject, e, ExecuteEvents.endDragHandler);
        }
        void OnDisable() { scrolling = dragged = false; }
    }
}
