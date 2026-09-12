using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Live49.Core
{
    // Story advance input: a new primary click or a new Space press. Held keys never repeat,
    // and each press is handed to one reader only so it cannot skip two beats at once.
    public static class FreshInput
    {
        static int _consumedFrame = -1;
        static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        public static bool TryAdvance()
        {
            if (GamePause.IsPaused) { DiscardPending(); return false; }
            if (_consumedFrame == Time.frameCount)
                return false;

            bool pressed = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
            // Button pointer-down must not complete/advance dialogue before its pointer-up opens the menu.
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && EventSystem.current != null)
            {
                UiHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() }, UiHits);
                foreach (var hit in UiHits)
                    if (hit.gameObject.GetComponentInParent<Selectable>() != null) { DiscardPending(); return false; }
            }
#if UNITY_EDITOR
            if (_simulated)
            {
                _simulated = false;
                pressed = true;
            }
#endif
            if (pressed)
                _consumedFrame = Time.frameCount;
            return pressed;
        }

#if UNITY_EDITOR
        static bool _simulated;

        // Editor automation (unity command eval) can advance without Game-view focus; compiled out of builds.
        public static void SimulateAdvance() => _simulated = true;
#endif

        public static void ConsumeThisFrame() => _consumedFrame = Time.frameCount;

        public static void DiscardPending()
        {
            ConsumeThisFrame();
#if UNITY_EDITOR
            _simulated = false;
#endif
        }
    }
}
