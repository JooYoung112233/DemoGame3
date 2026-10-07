using System.Collections.Generic;
using Demo6.Core.Loot;
using UnityEngine;

namespace Demo6.Game
{
    public sealed partial class Inventory
    {
        struct PointerSlot
        {
            public Rect ScreenRect;
            public GearItem Item;
            public GearSlot? EquipmentSlot;
        }

        readonly List<PointerSlot> _pointerSlots = new List<PointerSlot>(32);
        GearItem _pointerItem, _lastClickItem;
        GearSlot? _pointerOriginSlot;
        Vector2 _pointerDown, _lastClickPosition;
        float _lastClickAt = -10f;
        bool _pointerDragging;
        int _pointerControl;
        Rect _pointerClip;
        Matrix4x4 _pointerMatrix;
        Vector2 _pointerContentOrigin;
        bool _pointerClipped;
        const float PointerDragPixels = 6f;
        const float DoubleClickSeconds = .32f;
        public static void DrawItemIconForUi(Rect area,string id)=>V39Icon(area,id);
        public static void DrawItemIconForUi(Rect area,GearItem item){if(item!=null)V39Icon(area,item.Base.IconId,item.Grade);}

        // UI-only entry: a stale/foreign reference must never manufacture an inventory item.
        public bool TryEquipOwnedItem(GearItem item, GearSlot? slot = null)
        {
            if (item == null || (!_bag.Contains(item) && !Equipment.IsEquipped(item))) return false;
            return Equip(item, slot).HasValue;
        }

        void BeginInventoryPointer()
        {
            _pointerSlots.Clear();
            _pointerClipped = false;
            _pointerMatrix = GUI.matrix;
            _pointerControl = GUIUtility.GetControlID(0x494e5635, FocusType.Passive);
        }

        Rect InventoryScreenRect(Rect r)
        {
            // Keep one root-canvas transform. GUIToScreenPoint inside a scaled scroll
            // group applies that group's offset differently across GameView scales.
            Vector2 a = _pointerMatrix.MultiplyPoint3x4(r.min), b = _pointerMatrix.MultiplyPoint3x4(r.max);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        void SetInventoryPointerClip(Rect view)
        {
            _pointerClip = InventoryScreenRect(view);
            _pointerContentOrigin = view.position;
            _pointerClipped = true;
        }

        void RegisterInventorySlot(Rect r, GearItem item, GearSlot? slot)
        {
            Rect rootRect = r;
            if (_pointerClipped) rootRect.position += _pointerContentOrigin - _scroll;
            Rect screen = InventoryScreenRect(rootRect);
            if (_pointerClipped)
            {
                screen = Rect.MinMaxRect(Mathf.Max(screen.xMin, _pointerClip.xMin), Mathf.Max(screen.yMin, _pointerClip.yMin),
                    Mathf.Min(screen.xMax, _pointerClip.xMax), Mathf.Min(screen.yMax, _pointerClip.yMax));
                if (screen.width <= 0 || screen.height <= 0) return;
            }
            _pointerSlots.Add(new PointerSlot { ScreenRect = screen, Item = item, EquipmentSlot = slot });
            if (!_pointerDragging || !slot.HasValue) return;
            bool valid = Loadout.CanEquip(slot.Value, _pointerItem) && !WeaponActLocked;
            bool hovered = screen.Contains(_pointerMatrix.MultiplyPoint3x4(Event.current.mousePosition));
            if (valid || hovered)
                ApprovedUiV5.Round(V45Face(r), Color.clear, 6, valid ? UpColor : DownColor, hovered ? 3 : 1.5f);
        }

        bool PointerSourceStillOwned() => _pointerItem != null && (_pointerOriginSlot.HasValue
            ? ReferenceEquals(Equipment[_pointerOriginSlot.Value], _pointerItem)
            : _bag.Contains(_pointerItem));

        void CancelInventoryPointer(bool resetDoubleClick = true)
        {
            if (_pointerControl != 0 && GUIUtility.hotControl == _pointerControl) GUIUtility.hotControl = 0;
            _pointerItem = null;
            _pointerOriginSlot = null;
            _pointerDragging = false;
            if (resetDoubleClick) { _lastClickItem = null; _lastClickAt = -10f; }
        }

        void OnApplicationFocus(bool focused) { if (!focused) CancelInventoryPointer(); }
        void OnDisable() => CancelInventoryPointer();

        void EndInventoryPointer()
        {
            Event e = Event.current;
            if (DungeonUi.Modal != BagWindow || (_pointerItem != null && !PointerSourceStillOwned()))
            { CancelInventoryPointer(); return; }
            Vector2 point = _pointerMatrix.MultiplyPoint3x4(e.mousePosition);
            PointerSlot? hit = null;
            foreach (var s in _pointerSlots) if (s.ScreenRect.Contains(point)) { hit = s; break; }

            // Right-click, Escape, closing the window and losing focus are cancellation only.
            if ((e.type == EventType.MouseDown && e.button == 1) ||
                (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape))
            {
                bool active = _pointerItem != null;
                CancelInventoryPointer();
                if (active) e.Use();
                return;
            }
            if (e.type == EventType.MouseDown && e.button == 0 && hit.HasValue && hit.Value.Item != null && GUIUtility.hotControl == 0)
            {
                _pointerItem = hit.Value.Item;
                _pointerOriginSlot = hit.Value.EquipmentSlot;
                _pointerDown = point;
                _pointerDragging = false;
                SelectItem(_pointerItem);
                GUIUtility.hotControl = _pointerControl;
                e.Use();
            }
            else if (_pointerItem != null && e.type == EventType.MouseDrag && e.button == 0)
            {
                if ((point - _pointerDown).sqrMagnitude >= PointerDragPixels * PointerDragPixels)
                { _pointerDragging = true; _lastClickItem = null; }
                e.Use();
            }
            else if (_pointerItem != null && e.rawType == EventType.MouseUp && e.button == 0)
            {
                var item = _pointerItem;
                bool dragged = _pointerDragging;
                bool sourceValid = PointerSourceStillOwned();
                // Release capture BEFORE committing: repeated/reentrant MouseUp has no item to consume.
                CancelInventoryPointer(false);
                if (sourceValid && dragged)
                {
                    if (hit.HasValue && hit.Value.EquipmentSlot.HasValue)
                        TryEquipOwnedItem(item, hit.Value.EquipmentSlot);
                    _lastClickItem = null;
                }
                else if (sourceValid && hit.HasValue && ReferenceEquals(hit.Value.Item, item))
                {
                    bool twice = ReferenceEquals(_lastClickItem, item) && Time.unscaledTime - _lastClickAt <= DoubleClickSeconds &&
                                 (point - _lastClickPosition).sqrMagnitude <= PointerDragPixels * PointerDragPixels;
                    if (twice)
                    {
                        // Already-equipped items stay equipped; ring swaps require an explicit slot/drop.
                        if (_bag.Contains(item)) TryEquipOwnedItem(item);
                        _lastClickItem = null;
                    }
                    else { _lastClickItem = item; _lastClickAt = Time.unscaledTime; _lastClickPosition = point; }
                }
                else _lastClickItem = null;
                e.Use();
            }

            if (_pointerDragging && _pointerItem != null && e.type == EventType.Repaint)
            {
                var p = e.mousePosition;
                var ghost = new Rect(Mathf.Clamp(p.x - 31, 8, 1848), Mathf.Clamp(p.y - 31, 8, 1008), 64, 64);
                ApprovedUiV5.Round(ghost, new Color(.08f, .075f, .065f, .92f), 8, LootVisuals.GradeColor(_pointerItem.Grade), 2);
                V39Icon(new Rect(ghost.x + 5, ghost.y + 5, 54, 54), _pointerItem.Base.IconId, _pointerItem.Grade);
                V39Label(new Rect(Mathf.Clamp(p.x + 16, 8, 1520), Mathf.Clamp(p.y + 32, 8, 1040), 390, 30),
                    "맞는 장비 칸에 놓기 · 우클릭 취소", 18, ApprovedUiV5.Light);
            }
        }
    }
}
