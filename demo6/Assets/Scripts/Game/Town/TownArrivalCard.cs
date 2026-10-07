using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 도착 카드(기획/마을-의뢰-첫판.md 6-4, 창 이름 "arrival", 마을 위 반투명 판, GUI 순서 −60):
    /// "등불골로 돌아왔다" / 원정 요약 두 줄(옛 결과 창 숫자 그대로) / 지갑 / 명패(가져왔을 때만) / 알릴 일 / 진행 중 / [마을로 (Enter)].
    /// 줄은 TownRoot가 만든다(TownArrivalRules.CardLines). Enter·F·Space·Esc·클릭으로 닫고, 창을 열 때 눌려 있던 키는 받지 않는다
    /// (Esc는 저장·처음 화면·멈춤 창 1차 5-1·5-2: 카드가 열려 있으면 멈춤 창은 물러나고 카드가 Esc로 닫힌다).
    /// 닫으면 창을 닫은 뒤 콜백(오프닝·첫 귀환 바크)을 부른다. 화면 배치 다듬기는 Unity 개발 단계로 둔다(기능만).
    /// </summary>
    public sealed class TownArrivalCard : MonoBehaviour
    {
        public const string Modal = "arrival";
        const int GuiDepth = -60;
        const float PanelWidth = 760f;
        const float Pad = 24f;
        const float ButtonHeight = 44f;
        /// <summary>키 칸: 0 Enter, 1 숫자판 Enter, 2 F, 3 Space, 4 Esc.</summary>
        const int KeySlots = 5;

        readonly List<string> _lines = new List<string>();
        readonly GUIContent _measure = new GUIContent();
        readonly TownKeyLatch _keys = new TownKeyLatch(CardKey, KeySlots);
        string _title = "";
        string _button = "";
        int _strong;
        Action _onClose;
        string _modal;
        int _openedFrame;
        bool _clicked;
        bool _mouseWasDown;

        /// <summary>카드가 떠 있다.</summary>
        public bool Showing { get; private set; }
        /// <summary>지금 카드 줄(시험·확인용).</summary>
        public IReadOnlyList<string> Lines => _lines;

        /// <summary>
        /// 옛 결과 창 둘째 줄과 같은 글(NightCard.ShowResults): "이번 원정 강화석 +12 · 골드 +340 · 가장 깊이 2층".
        /// 첫째 줄은 ExpeditionSummary.Line().
        /// </summary>
        public static string GainLine(ExpeditionSummary s) =>
            s == null ? "" : $"이번 원정 강화석 +{s.StonesGained} · 골드 +{s.GoldGained} · 가장 깊이 {s.DeepestFloorThisTrip}층";

        /// <summary>
        /// 카드를 띄운다(시간 멈춤·입력 막기). button은 "[마을로 (Enter)]" 같은 단추 글(대괄호는 단추에서 뗀다).
        /// strongLines = 앞에서부터 뼈색으로 보일 줄 수(원정 요약 두 줄), 나머지 마을 몫은 조금 흐리게.
        /// </summary>
        public void Show(string title, IEnumerable<string> lines, string button, Action onClose, int strongLines = 0)
        {
            _strong = Mathf.Max(0, strongLines);
            _lines.Clear();
            if (lines != null)
                foreach (var l in lines)
                    if (!string.IsNullOrEmpty(l)) _lines.Add(l);
            _title = title ?? "";
            _button = (button ?? "").Trim().TrimStart('[').TrimEnd(']');
            _onClose = onClose;
            _clicked = false;
            _openedFrame = Time.frameCount;
            if (_modal == null) _modal = DungeonUi.TryOpen(Modal) ? Modal : null;
            if (_modal == null) Debug.LogWarning($"[마을] 다른 창({DungeonUi.Modal})이 열려 있어 도착 카드를 창 없이 그린다(시간이 멈추지 않음)");
            Showing = true;
            _keys.Snap();
            var mouse = Mouse.current;
            _mouseWasDown = mouse != null && mouse.leftButton.isPressed;
            Debug.Log("[마을] 도착 카드: " + _title + " / " + string.Join(" / ", _lines));
        }

        /// <summary>시험용: 닫기 단추를 누른 것과 같다(다음 Update에서 닫힘).</summary>
        public void PressClose()
        {
            if (Showing) _clicked = true;
        }

        void OnDestroy()
        {
            if (_modal != null) DungeonUi.Close(_modal);
            _modal = null;
        }

        void Update()
        {
            if (!Showing) return;
            bool close = _clicked;
            if (Time.frameCount > _openedFrame)
            {
                for (int i = 0; i < KeySlots; i++) close |= _keys.Pressed(i);
                var mouse = Mouse.current;
                bool down = mouse != null && mouse.leftButton.isPressed;
                if (down && !_mouseWasDown) close = true;
                _mouseWasDown = down;
            }
            if (!close) return;
            Showing = false;
            _clicked = false;
            if (_modal != null) DungeonUi.Close(_modal);
            _modal = null;
            var callback = _onClose;
            _onClose = null;
            callback?.Invoke();
        }

        static KeyControl CardKey(Keyboard kb, int slot)
        {
            switch (slot)
            {
                case 0: return kb.enterKey;
                case 1: return kb.numpadEnterKey;
                case 2: return kb.fKey;
                case 3: return kb.spaceKey;
                case 4: return kb.escapeKey;
                default: return null;
            }
        }

        float TextHeight(GUIStyle style, string text, float width)
        {
            _measure.text = text;
            return Mathf.Ceil(style.CalcHeight(_measure, width));
        }

        void OnGUI()
        {
            if (!Showing) return;
            DungeonUi.Begin();
            GUI.depth = GuiDepth;
            float w = PanelWidth - Pad * 2f;
            float titleH = Mathf.Max(36f, TextHeight(DungeonUi.Title, _title, w));
            float linesH = 0f;
            for (int i = 0; i < _lines.Count; i++) linesH += TextHeight(DungeonUi.Label, _lines[i], w) + 6f;
            float h = Pad + titleH + 12f + linesH + 14f + ButtonHeight + Pad;
            var r = DungeonUi.KeepOnScreen(new Rect((DungeonUi.Width - PanelWidth) * 0.5f, (DungeonUi.Height - h) * 0.5f, PanelWidth, h));
            // 마을이 비치는 반투명 판(밤 카드처럼 화면을 덮지 않음).
            DungeonUi.Fill(new Rect(0f, 0f, DungeonUi.Width, DungeonUi.Height), new Color(0f, 0f, 0f, 0.35f));
            DungeonUi.Box(r, 0.9f);
            float x = r.x + Pad;
            float y = r.y + Pad;
            GUI.Label(new Rect(x, y, w, titleH), _title, DungeonUi.Title);
            y += titleH + 12f;
            var prev = GUI.color;
            for (int i = 0; i < _lines.Count; i++)
            {
                float lh = TextHeight(DungeonUi.Label, _lines[i], w);
                // 원정 요약 두 줄은 뼈색, 마을 몫(지갑·명패·알릴 일·진행 중)은 조금 흐리게.
                GUI.color = i < _strong ? DungeonUi.Bone : DungeonUi.BoneDim;
                GUI.Label(new Rect(x, y, w, lh), _lines[i], DungeonUi.Label);
                y += lh + 6f;
            }
            GUI.color = prev;
            y += 14f;
            if (GUI.Button(new Rect(x, y, w, ButtonHeight), _button)) _clicked = true;
        }
    }
}
