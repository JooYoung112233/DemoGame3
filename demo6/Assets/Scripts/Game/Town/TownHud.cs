using System.Collections.Generic;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 HUD(기획/마을-의뢰-첫판.md 6-1, IMGUI 기능만, GUI 순서 5). 왼쪽 위 "등불골 · 갱도 마당"(던전 층 이름 줄과 같은 모양, 그 아래 3줄은 목표 HUD QuestHud, 꾸러미 3),
    /// 오른쪽 위 지갑 "강화석 n · 골드 n"과 안내 줄 "I 가방 · K 스킬"(F1 시험은 적지 않음, 기획/시스템-컨텐츠-다듬기-검토-1차.md 4장 Q7), 아래 가운데 알림(DungeonEvents.Message 구독, 던전 HUD 알림과 같은 모양).
    /// 가방 창의 지갑 줄은 던전 루트가 없으면 보이지 않으므로 마을 지갑은 여기서 보인다(꾸러미 값 그대로).
    /// 지갑 줄 아래에는 '가방 n/칸'(재화 쓸 곳 1차 5-4, 마을 가방 TownRoot.Inventory): 가득(개수 ≥ 칸)이면 붉게, 넘쳐도 '22/20'처럼 그대로 붉다. 안내 줄은 그 아래.
    /// 마을 알림: 보상("품삯 — …")·레벨업·바크 대신 길·물체 글 대신 길이 여기 뜬다. 사람을 가리키는 알림 글은 부르는 쪽이 SpeakerIdentity를 거친다.
    /// 멈춘 동안(대화창·창)은 알림이 늙지 않아 창을 닫은 뒤에도 놓치지 않는다.
    /// </summary>
    public sealed class TownHud : MonoBehaviour
    {
        /// <summary>목표 HUD(QuestHud, 꾸러미 3)가 제목 아래 3줄을 그리기 시작할 높이(기준 높이 900 좌표, 던전 층 이름 줄 아래와 같음).</summary>
        public const float QuestLinesTop = 46f;
        const int GuiDepth = 5;
        const int ToastMax = 3;
        const float ToastLife = 3f;
        const float ToastFade = 0.6f;
        const float Margin = 16f;

        static readonly Color MessageColor = new Color(0.86f, 0.8f, 0.68f, 1f);

        sealed class Toast
        {
            public string Text;
            public float Age;
            public float Width = -1f;
        }

        readonly List<Toast> _toasts = new List<Toast>();
        readonly GUIContent _measure = new GUIContent();
        GUIStyle _right;
        /// <summary>'가방 n/칸' 글(개수·칸이 바뀔 때만 다시 만든다).</summary>
        string _bagLine;
        int _bagCount = -1;
        int _bagCapacity = -1;

        /// <summary>지금 떠 있는 알림 글(시험·확인용, 오래된 것 먼저).</summary>
        public IEnumerable<string> Toasts
        {
            get
            {
                foreach (var t in _toasts) yield return t.Text;
            }
        }

        void Awake() => DungeonEvents.Message += Push;

        void OnDestroy() => DungeonEvents.Message -= Push;

        /// <summary>알림 한 줄. 같은 글이 아직 떠 있으면 새로 쌓지 않고 다시 맨 아래로 올린다(F 연타 등).</summary>
        public void Push(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < _toasts.Count; i++)
            {
                if (_toasts[i].Text != text) continue;
                var same = _toasts[i];
                _toasts.RemoveAt(i);
                same.Age = Mathf.Min(same.Age, 0.15f);
                _toasts.Add(same);
                return;
            }
            _toasts.Add(new Toast { Text = text });
            while (_toasts.Count > ToastMax) _toasts.RemoveAt(0);
        }

        void Update()
        {
            float dt = TimeScaleService.Paused ? 0f : Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                _toasts[i].Age += dt;
                if (_toasts[i].Age >= ToastLife + ToastFade) _toasts.RemoveAt(i);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var card = NightCard.Instance;
            if (card && card.Showing) return;
            DungeonUi.Begin();
            GUI.depth = GuiDepth;
            if (_right == null)
            {
                _right = new GUIStyle(DungeonUi.Small) { alignment = TextAnchor.UpperRight, wordWrap = false, clipping = TextClipping.Overflow };
                _right.normal.textColor = Color.white;
            }

            // 왼쪽 위: 자리 이름(던전 층 이름 줄과 같은 자리·모양). 목표 줄은 그 아래 QuestLinesTop부터(QuestHud는 제목을 그리지 않게 둔다).
            DungeonUi.ShadowLabel(new Rect(Margin, Margin, 390f, 26f), TownScript.TownTitle, DungeonUi.Small, DungeonUi.BoneDim);

            // 오른쪽 위: 지갑·가방 칸·키 안내.
            var carry = ProfileCarry.Data;
            float w = 420f;
            float x = DungeonUi.Width - Margin - w;
            if (carry != null) DungeonUi.ShadowLabel(new Rect(x, 14f, w, 26f), TownScript.WalletLine(carry.Stones, carry.Gold), _right, DungeonUi.Bone);
            float y = 40f;
            var town = TownRoot.Instance;
            var inv = town ? town.Inventory : null;
            if (inv)
            {
                // 재화 쓸 곳 1차 5-4: '가방 17/20', 가득(개수 ≥ 칸)이면 붉게.
                int count = inv.BagCount;
                int capacity = inv.Capacity;
                if (_bagLine == null || count != _bagCount || capacity != _bagCapacity)
                {
                    _bagCount = count;
                    _bagCapacity = capacity;
                    _bagLine = ForgeText.TownBagLine(count, capacity);
                }
                DungeonUi.ShadowLabel(new Rect(x, y, w, 24f), _bagLine, _right, inv.BagFull ? DungeonUi.Rust : DungeonUi.BoneDim);
                y += 24f;
            }
            DungeonUi.ShadowLabel(new Rect(x, y, w, 24f), TownScript.ControlsLine, _right, DungeonUi.BoneDim);

            DrawToasts();
        }

        /// <summary>화면 아래 가운데, 새 알림이 아래로 쌓인다. 양 끝이 흐려지는 검은 띠 위 바탕체 글(던전 HUD 알림과 같은 모양).</summary>
        void DrawToasts()
        {
            if (_toasts.Count == 0) return;
            float cx = DungeonUi.Width * 0.5f;
            float y = DungeonUi.Height - 120f;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                float a = Mathf.Clamp01(t.Age / 0.18f) * (t.Age < ToastLife ? 1f : Mathf.Clamp01(1f - (t.Age - ToastLife) / ToastFade));
                const float h = 32f;
                if (t.Width < 0f)
                {
                    _measure.text = t.Text;
                    t.Width = DungeonUi.Toast.CalcSize(_measure).x;
                }
                float tw = Mathf.Min(DungeonUi.Width - 40f, t.Width + 160f);
                var r = new Rect(cx - tw * 0.5f, y - h, tw, h);
                DungeonUi.Strip(r, new Color(0f, 0f, 0f, 0.72f * a));
                DungeonUi.ShadowLabel(r, t.Text, DungeonUi.Toast, new Color(MessageColor.r, MessageColor.g, MessageColor.b, a));
                y -= h + 6f;
            }
        }
    }
}
