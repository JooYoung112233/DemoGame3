using System;
using System.Collections.Generic;
using Demo6.Core.Dungeon;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Demo6.Game
{
    /// <summary>
    /// 갱도 입구 F(기획/마을-의뢰-첫판.md 1-3·2-4): 'F 갱도 입구' → 권양기 창(창 이름 "gate", 말뚝 메뉴와 같은 IMGUI 틀) → 고른 층으로 출발(TownRoot.Depart).
    /// 창 제목 "갱도 입구 — 어디서 내려갈까". 줄은 ProfileCarry.LandingOptions()를 그대로 쓰고 글 모양은 지금 승강장 고르기(NightCard)와 같다.
    /// 기본은 줄 끝(테두리). 숫자 키·Enter(기본)·단추로 고르고 Esc·닫기로 취소한다(NightCard.ShowLandingPicker는 취소가 없어 마을에서는 쓰지 않는다).
    /// 굴 앞 말뚝을 켰고 첫 처치 전이면 마지막에 '보스방 앞'(오우거 굴 쉼터) 줄이 붙고, 고르면 OgreDen.PickCode로 출발한다.
    /// 알리지 않은 의뢰가 있으면 한 줄을 더한다("아직 알리지 않은 일 n — 보상은 다음에 와서 받아도 된다."). 출발은 막지 않는다.
    /// 마을 가방이 거의 찼으면(개수 ≥ 칸 − 4, Inventory.NearlyFull) 그 아래에 같은 모양으로 한 줄 더("가방이 거의 찼다 (n/칸) — 모루에서 분해하고 가도 된다.",
    /// 재화 쓸 곳 1차 5-4, ForgeText.GateNearlyFullLine). 이것도 출발은 막지 않는다.
    /// 고르면 창을 먼저 닫고 출발한다(다른 창이 열려 있으면 밤 카드 창이 안 열림). 창을 열 때 눌려 있던 키는 받지 않는다.
    /// 화면 배치·반응형 다듬기는 Unity 개발 단계로 둔다(기능만).
    /// </summary>
    public sealed class MineGate : Interactable
    {
        public const string Modal = "gate";
        const float PanelWidth = 860f;
        const float Pad = 24f;
        const float ButtonHeight = 44f;
        const float RowGap = 8f;
        const string Desc = "켠 승강장 하나를 고른다. 기본은 가장 깊은 곳('줄 끝').";
        const string Foot = "숫자 키로 고른다. Enter = 기본(테두리 친 줄). [Esc] 닫기";
        /// <summary>키 칸: 0 Enter, 1 숫자판 Enter, 2~10 숫자 1~9, 11~19 숫자판 1~9, 20 Esc.</summary>
        const int EscSlot = 20;

        readonly List<LandingOption> _options = new List<LandingOption>();
        readonly List<string> _rowText = new List<string>();
        readonly TownKeyLatch _keys = new TownKeyLatch(GateKey, 21);
        readonly GUIContent _measure = new GUIContent();
        string _prompt;
        bool _open;
        int _openedFrame;
        int _defaultIndex = -1;
        int _defaultFloor = 1;
        string _unreported;
        /// <summary>가방이 거의 찼을 때 덧줄(재화 쓸 곳 1차 5-4). 아니면 null.</summary>
        string _bagNote;
        int _clickedRow = -1;
        bool _clickedClose;
        GUIStyle _rowStyle;
        GUIStyle _rowDefaultStyle;

        /// <summary>권양기 창이 열려 있다.</summary>
        public bool IsOpen => _open;
        /// <summary>지금 창의 기본 층(줄 끝).</summary>
        public int DefaultFloor => _defaultFloor;
        public IReadOnlyList<LandingOption> Options => _options;

        public override float Range => TownLayout.Gate.Radius;
        public override string Prompt => _prompt ?? (_prompt = TownFacility.PromptOf(TownScript.Object(TownScript.ObjGate)));
        public override bool Available => TownRoot.Instance && !TownRoot.Instance.Busy && !_open;

        public static MineGate Create(Transform parent)
        {
            var spot = TownLayout.Gate;
            var go = new GameObject("F " + spot.Id);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector2(spot.Pos.X, spot.Pos.Y);
            return go.AddComponent<MineGate>();
        }

        public override void Interact() => Open();

        /// <summary>권양기 창을 연다(시간 멈춤·입력 막기). 떠나는 중이거나 다른 창이 열려 있으면 false.</summary>
        public bool Open()
        {
            var root = TownRoot.Instance;
            if (_open || !root || root.Leaving) return false;
            if (!DungeonUi.TryOpen(Modal)) return false;
            _open = true;
            _openedFrame = Time.frameCount;
            _clickedRow = -1;
            _clickedClose = false;
            var carry = ProfileCarry.Ensure();
            _options.Clear();
            _rowText.Clear();
            _options.AddRange(ProfileCarry.LandingOptions());
            _defaultFloor = carry.RopeEnd(FloorRecipe.MaxTestFloor);
            _defaultIndex = DefaultIndex(_options, _defaultFloor);
            var gateBook = new QuestBook(carry);
            // 층 줄 끝에 그 층에서 하기 좋은 진행 중 의뢰(묶음 3 가-2, QuestBook.FloorHint).
            for (int i = 0; i < _options.Count; i++) _rowText.Add(RowText(i + 1, _options[i]) + gateBook.FloorHint(_options[i].Floor, _options[i].Den));
            _unreported = TownScript.GateUnreportedLine(gateBook.UnreportedCount);
            // 받지 않은 일(묶음 3 가-1)은 같은 덧줄 자리에 한 줄 더한다(출발은 막지 않음).
            string offered = TownScript.GateOfferLine(gateBook.OfferedCount);
            if (offered != null) _unreported = _unreported == null ? offered : _unreported + "\n" + offered;
            var inv = root.Inventory;
            _bagNote = inv && inv.NearlyFull ? ForgeText.GateNearlyFullLine(inv.BagCount, inv.Capacity) : null;
            _keys.Snap();
            return true;
        }

        /// <summary>창을 닫는다(취소).</summary>
        public void Close()
        {
            if (!_open) return;
            _open = false;
            DungeonUi.Close(Modal);
        }

        /// <summary>시험용: index번째 줄(0부터) 단추를 누른 것과 같다(다음 Update에서 고름).</summary>
        public void PressRow(int index)
        {
            if (_open && index >= 0 && index < _options.Count) _clickedRow = index;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Close();
        }

        /// <summary>기본 줄(승강장 고르기와 같음): '보스방 앞' 줄은 기본이 되지 않는다.</summary>
        static int DefaultIndex(List<LandingOption> options, int defaultFloor) => NightCard.DefaultIndex(options, defaultFloor);

        /// <summary>
        /// 승강장 고르기와 같은 줄: "[n] 제{층}층 — {이름} · 권장 레벨 {lv} · 측량 {s}/3" + 명패 찾음/못 찾음 + (줄 끝) + (처음).
        /// '보스방 앞' 줄은 "[n] 제2층 바닥 — 보스방 앞 · 권장 레벨 3".
        /// </summary>
        public static string RowText(int n, LandingOption o) => NightCard.RowText(n, o);

        void Update()
        {
            if (!_open) return;
            // 다른 곳이 창을 닫았으면(장면 정적 비우기 등) 따라 닫힌다.
            if (DungeonUi.Modal != Modal)
            {
                _open = false;
                return;
            }
            int pick = _clickedRow;
            bool cancel = _clickedClose;
            _clickedRow = -1;
            _clickedClose = false;
            bool go = pick >= 0;
            if (Time.frameCount > _openedFrame && Keyboard.current != null)
            {
                bool enter = _keys.Pressed(0) | _keys.Pressed(1);
                for (int n = 1; n <= 9; n++)
                {
                    bool digit = _keys.Pressed(1 + n) | _keys.Pressed(10 + n);
                    if (digit && !go && n - 1 < _options.Count)
                    {
                        pick = n - 1;
                        go = true;
                    }
                }
                if (_keys.Pressed(EscSlot)) cancel = true;
                // Enter = 기본 줄(목록이 비었으면 기본 층).
                if (enter && !go)
                {
                    pick = _defaultIndex;
                    go = true;
                }
            }
            if (cancel && !go)
            {
                Close();
                return;
            }
            if (!go) return;
            // '보스방 앞' 줄은 층 번호 대신 OgreDen.PickCode를 넘긴다(TownRoot.Depart가 굴 쪽지로 바꿈).
            int floor = pick >= 0 && pick < _options.Count ? ProfileCarry.PickValue(_options[pick]) : _defaultFloor;
            Close();
            var root = TownRoot.Instance;
            if (root) root.Depart(floor);
        }

        static KeyControl GateKey(Keyboard kb, int slot)
        {
            if (slot == 0) return kb.enterKey;
            if (slot == 1) return kb.numpadEnterKey;
            if (slot == EscSlot) return kb.escapeKey;
            if (slot <= 10) return TownKeyLatch.Digit(kb, slot - 1);
            return TownKeyLatch.Numpad(kb, slot - 10);
        }

        // ── 그리기 ───────────────────────────────────────────

        void EnsureStyles()
        {
            if (_rowStyle != null) return;
            _rowStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip,
                padding = new RectOffset(14, 10, 6, 6),
            };
            _rowDefaultStyle = new GUIStyle(_rowStyle);
            _rowDefaultStyle.normal.textColor = DungeonUi.Ember;
            _rowDefaultStyle.hover.textColor = DungeonUi.Ember;
            _rowDefaultStyle.focused.textColor = DungeonUi.Ember;
        }

        float TextHeight(GUIStyle style, string text, float width)
        {
            _measure.text = text;
            return Mathf.Ceil(style.CalcHeight(_measure, width));
        }

        void OnGUI()
        {
            if (!_open || DungeonUi.Modal != Modal) return;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = -20;
            var prevColor = GUI.color;
            float w = PanelWidth - Pad * 2f;
            string title = TownScript.GateWindowTitle;
            float titleH = Mathf.Max(36f, TextHeight(DungeonUi.Title, title, w - 72f));
            float descH = TextHeight(DungeonUi.Small, Desc, w);
            float footH = TextHeight(DungeonUi.Small, Foot, w);
            float noteH = _unreported != null ? TextHeight(DungeonUi.Small, _unreported, w) + 8f : 0f;
            float bagNoteH = _bagNote != null ? TextHeight(DungeonUi.Small, _bagNote, w) + 8f : 0f;
            string empty = _options.Count == 0 ? $"켠 승강장이 없다 — 제{_defaultFloor}층으로 내려간다. (Enter)" : null;
            float rowsH = empty != null ? TextHeight(DungeonUi.Small, empty, w) + RowGap : 0f;
            for (int i = 0; i < _options.Count; i++) rowsH += Mathf.Max(ButtonHeight, TextHeight(_rowStyle, _rowText[i], w)) + RowGap;
            float h = Pad + titleH + 6f + descH + 16f + rowsH + noteH + bagNoteH + 6f + footH + Pad;
            var r = DungeonUi.KeepOnScreen(new Rect((DungeonUi.Width - PanelWidth) * 0.5f, (DungeonUi.Height - h) * 0.5f, PanelWidth, h));
            DungeonUi.Box(r, 0.93f);
            float x = r.x + Pad;
            float y = r.y + Pad;
            GUI.Label(new Rect(x, y, w - 72f, titleH), title, DungeonUi.Title);
            if (DungeonUi.CloseButton(r)) _clickedClose = true;
            y += titleH + 6f;
            GUI.Label(new Rect(x, y, w, descH), Desc, DungeonUi.Small);
            y += descH + 16f;
            if (empty != null)
            {
                float eh = TextHeight(DungeonUi.Small, empty, w);
                GUI.Label(new Rect(x, y, w, eh), empty, DungeonUi.Small);
                y += eh + RowGap;
            }
            for (int i = 0; i < _options.Count; i++)
            {
                float rh = Mathf.Max(ButtonHeight, TextHeight(_rowStyle, _rowText[i], w));
                var row = new Rect(x, y, w, rh);
                bool isDefault = i == _defaultIndex;
                if (isDefault) DungeonUi.Outline(new Rect(row.x - 3f, row.y - 3f, row.width + 6f, row.height + 6f), DungeonUi.Ember, 2f);
                if (GUI.Button(row, _rowText[i], isDefault ? _rowDefaultStyle : _rowStyle)) _clickedRow = i;
                y += rh + RowGap;
            }
            if (_unreported != null)
            {
                GUI.color = DungeonUi.Bone;
                GUI.Label(new Rect(x, y, w, noteH - 8f), _unreported, DungeonUi.Small);
                y += noteH;
            }
            // 재화 쓸 곳 1차 5-4: 가방이 거의 찼다는 덧줄('아직 알리지 않은 일' 줄과 같은 모양, 출발은 막지 않음).
            if (_bagNote != null)
            {
                GUI.color = DungeonUi.Bone;
                GUI.Label(new Rect(x, y, w, bagNoteH - 8f), _bagNote, DungeonUi.Small);
                y += bagNoteH;
            }
            y += 6f;
            GUI.color = DungeonUi.BoneDim;
            GUI.Label(new Rect(x, y, w, footH), Foot, DungeonUi.Small);
            GUI.color = prevColor;
        }
    }

    /// <summary>
    /// 마을 창 키 읽기(밤 카드와 같은 방식): 이번 프레임에 눌렸거나, 지난 검사 때 떼어 있던 키가 지금 눌려 있으면 한 번 받는다
    /// (시험 입력 흉내가 프레임 사이에 넣은 입력도 한 번 받음). 창을 열 때 Snap으로 지금 눌린 키를 적어 두므로 누른 채로 들어온 키는 받지 않는다.
    /// </summary>
    public sealed class TownKeyLatch
    {
        readonly Func<Keyboard, int, KeyControl> _key;
        readonly bool[] _down;

        public TownKeyLatch(Func<Keyboard, int, KeyControl> key, int slots)
        {
            _key = key;
            _down = new bool[Mathf.Max(1, slots)];
        }

        public void Snap()
        {
            var kb = Keyboard.current;
            for (int i = 0; i < _down.Length; i++)
            {
                var k = kb != null ? _key(kb, i) : null;
                _down[i] = k != null && k.isPressed;
            }
        }

        public bool Pressed(int slot)
        {
            var kb = Keyboard.current;
            if (kb == null || slot < 0 || slot >= _down.Length) return false;
            var key = _key(kb, slot);
            bool down = key != null && key.isPressed;
            bool was = _down[slot];
            _down[slot] = down;
            return (down && !was) || (key != null && key.wasPressedThisFrame);
        }

        public static KeyControl Digit(Keyboard kb, int n)
        {
            switch (n)
            {
                case 1: return kb.digit1Key;
                case 2: return kb.digit2Key;
                case 3: return kb.digit3Key;
                case 4: return kb.digit4Key;
                case 5: return kb.digit5Key;
                case 6: return kb.digit6Key;
                case 7: return kb.digit7Key;
                case 8: return kb.digit8Key;
                case 9: return kb.digit9Key;
                default: return null;
            }
        }

        public static KeyControl Numpad(Keyboard kb, int n)
        {
            switch (n)
            {
                case 1: return kb.numpad1Key;
                case 2: return kb.numpad2Key;
                case 3: return kb.numpad3Key;
                case 4: return kb.numpad4Key;
                case 5: return kb.numpad5Key;
                case 6: return kb.numpad6Key;
                case 7: return kb.numpad7Key;
                case 8: return kb.numpad8Key;
                case 9: return kb.numpad9Key;
                default: return null;
            }
        }
    }
}
