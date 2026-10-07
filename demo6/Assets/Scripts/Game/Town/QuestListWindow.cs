using System.Collections.Generic;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 의뢰 목록 창(기획/마을-의뢰-첫판.md 5-8·6-5, 꾸러미 3, IMGUI 임시판). 주막 앞 의뢰 게시판 F가 연다(QuestListWindow.Open()).
    /// 첫 줄은 게시판 글(나레이션 모양), 그 아래 줄마다 제목·상태(받을 수 있음 / 진행 n/목표 / 알릴 일)·보상 크기·강화석·골드·경험치·주는 사람.
    /// 주는 사람은 QuestBook.ListRows가 SpeakerIdentity.GiverLabel을 거쳐 '춘삼' 또는 '? (경비 초소)'로 준다. 잠긴 의뢰와 지급된 의뢰는 보이지 않는다.
    /// 읽기만 한다(받기·보고는 주민에게서만). 창 "board"(시간 멈춤·입력 막기), Esc·F·Enter·닫기 단추로 닫는다. 연 F는 한 번 뗀 뒤에야 듣는다.
    /// GUI.depth −20(권양기 창·말뚝 메뉴와 같은 창 틀 값). 화면 배치·반응형 다듬기는 Unity 개발 단계로 둔다.
    /// </summary>
    public sealed class QuestListWindow : MonoBehaviour
    {
        public const string ModalName = "board";
        public const string Title = "의뢰 게시판";
        /// <summary>J로 연 창 제목(묶음 3 가-3: 마을·던전 어디서나, 게시판 글 없이).</summary>
        public const string KeyTitle = "맡은 일";
        /// <summary>보이는 의뢰가 없을 때.</summary>
        public const string EmptyLine = "붙은 일이 없다.";
        public const string ReadOnlyLine = "일은 사람에게서 받고, 끝내면 그 사람에게 알린다.";

        const int GuiDepth = -20;
        const float PanelWidth = 760f;
        const float Pad = 22f;
        const float RowHeight = 80f;
        const float RowGap = 6f;

        static readonly Color OfferColor = new Color(1f, 0.95f, 0.82f, 1f);
        static readonly Color ReportColor = new Color(0.86f, 0.78f, 0.60f, 1f);
        static readonly Color ActiveColor = new Color(0.9f, 0.86f, 0.77f, 1f);

        public static QuestListWindow Instance { get; private set; }

        /// <summary>창이 열려 있다.</summary>
        public static bool IsOpen => DungeonUi.Modal == ModalName;

        readonly List<QuestRow> _rows = new List<QuestRow>();
        int _openedFrame;
        bool _fArmed;
        bool _enterArmed;
        bool _jArmed;
        /// <summary>게시판 F로 열었나(제목·게시판 글). J로 열면 거짓.</summary>
        bool _fromBoard = true;
        GUIStyle _note;
        GUIStyle _rowTitle;
        GUIStyle _rowDetail;
        Font _stylesFont;
        readonly GUIContent _measure = new GUIContent();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;

        /// <summary>장면 정적 비우기(SceneStatics.Reset이 부름).</summary>
        public static void ResetStatics() => Instance = null;

        /// <summary>게시판 F: 창을 연다(이 장면에 창이 없으면 만든다). 다른 창이 열려 있으면 false.</summary>
        public static bool Open() => Open(true);

        /// <summary>
        /// 창을 연다. fromBoard = 게시판 F(제목 '의뢰 게시판'과 게시판 글), 거짓이면 J 키(제목 '맡은 일', 마을·던전 어디서나 — GameShell이 부름).
        /// </summary>
        public static bool Open(bool fromBoard)
        {
            var window = Instance;
            if (!window) window = FindAnyObjectByType<QuestListWindow>();
            if (!window) window = new GameObject("QuestListWindow").AddComponent<QuestListWindow>();
            Instance = window;
            window._fromBoard = fromBoard;
            return window.OpenWindow();
        }

        /// <summary>창을 닫는다(열려 있지 않으면 아무것도 하지 않음).</summary>
        public static void Close() => DungeonUi.Close(ModalName);

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (IsOpen) DungeonUi.Close(ModalName);
        }

        bool OpenWindow()
        {
            if (!DungeonUi.TryOpen(ModalName)) return false;
            _openedFrame = Time.frameCount;
            var kb = Keyboard.current;
            _fArmed = kb == null || !kb.fKey.isPressed;
            _enterArmed = kb == null || !kb.enterKey.isPressed;
            _jArmed = kb == null || !kb.jKey.isPressed;
            Refresh();
            return true;
        }

        /// <summary>목록을 다시 읽는다(창을 열 때).</summary>
        void Refresh()
        {
            _rows.Clear();
            _rows.AddRange(new QuestBook(ProfileCarry.Ensure()).ListRows());
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = Keyboard.current;
            if (kb == null || Time.frameCount <= _openedFrame) return;
            if (!kb.fKey.isPressed) _fArmed = true;
            if (!kb.enterKey.isPressed) _enterArmed = true;
            if (!kb.jKey.isPressed) _jArmed = true;
            bool f = _fArmed && kb.fKey.wasPressedThisFrame;
            bool enter = _enterArmed && kb.enterKey.wasPressedThisFrame;
            bool j = _jArmed && kb.jKey.wasPressedThisFrame;
            if (kb.escapeKey.wasPressedThisFrame || f || enter || j) Close();
        }

        void EnsureStyles()
        {
            var font = DungeonUi.Label.font;
            if (_note != null && _stylesFont == font) return;
            _stylesFont = font;
            _note = new GUIStyle(DungeonUi.Label) { fontStyle = FontStyle.Italic, wordWrap = true };
            if (DungeonUi.Serif) _note.font = DungeonUi.Serif;
            _note.normal.textColor = Color.white;
            _rowTitle = new GUIStyle(DungeonUi.Bold) { wordWrap = false, clipping = TextClipping.Clip };
            _rowTitle.normal.textColor = Color.white;
            _rowDetail = new GUIStyle(DungeonUi.Small) { wordWrap = false, clipping = TextClipping.Clip };
            _rowDetail.normal.textColor = Color.white;
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            var prevMatrix = GUI.matrix;
            var prevColor = GUI.color;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = GuiDepth;

            float width = Mathf.Min(PanelWidth, DungeonUi.Width - 32f);
            float inner = width - Pad * 2f;
            var board = _fromBoard ? TownScript.Object(TownScript.ObjBoard) : null;
            string note = board != null ? board.Text : "";
            _measure.text = note;
            float noteHeight = Mathf.Max(26f, _note.CalcHeight(_measure, inner));
            int rows = Mathf.Max(1, _rows.Count);
            float height = 62f + noteHeight + 14f + rows * (RowHeight + RowGap) + 12f + 26f + 30f;
            var r = DungeonUi.KeepOnScreen(new Rect((DungeonUi.Width - width) * 0.5f, (DungeonUi.Height - height) * 0.5f, width, height));
            DungeonUi.Box(r, 0.94f);
            float x = r.x + Pad;
            GUI.Label(new Rect(x, r.y + 14f, inner - 60f, 36f), _fromBoard ? Title : KeyTitle, DungeonUi.Title);
            float y = r.y + 58f;
            DungeonUi.ShadowLabel(new Rect(x, y, inner, noteHeight), note, _note, new Color(0.78f, 0.72f, 0.60f, 1f));
            y += noteHeight + 14f;

            if (_rows.Count == 0)
            {
                DungeonUi.ShadowLabel(new Rect(x, y + 14f, inner, 26f), EmptyLine, DungeonUi.Small, DungeonUi.BoneDim);
                y += RowHeight + RowGap;
            }
            else
            {
                foreach (var row in _rows)
                {
                    var slot = new Rect(x, y, inner, RowHeight);
                    DungeonUi.Fill(slot, new Color(0.03f, 0.028f, 0.026f, 0.7f));
                    DungeonUi.Outline(slot, DungeonUi.IronEdge, 1f);
                    string title = row.Quest != null ? row.Quest.Title : "";
                    DungeonUi.ShadowLabel(new Rect(slot.x + 12f, slot.y + 4f, inner - 24f, 26f), title + "  —  " + row.Status, _rowTitle, StateColor(row.State));
                    string detail = row.SizeLabel + " · " + row.Reward.AmountText + " · 주는 사람 " + row.GiverLabel;
                    DungeonUi.ShadowLabel(new Rect(slot.x + 12f, slot.y + 29f, inner - 24f, 24f), row.GoalLine, _rowDetail, StateColor(row.State));
                    DungeonUi.ShadowLabel(new Rect(slot.x + 12f, slot.y + 52f, inner - 24f, 24f), detail, _rowDetail, DungeonUi.BoneDim);
                    y += RowHeight + RowGap;
                }
            }

            y += 6f;
            DungeonUi.ShadowLabel(new Rect(x, y, inner, 24f), ReadOnlyLine, DungeonUi.Small, DungeonUi.BoneDim);
            bool close = DungeonUi.CloseButton(r);
            GUI.Label(new Rect(x, r.yMax - 34f, inner, 26f), _fromBoard ? "[Esc] 닫기" : "[J · Esc] 닫기", DungeonUi.Small);
            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
            if (close) Close();
        }

        static Color StateColor(QuestState state)
        {
            switch (state)
            {
                case QuestState.Offered: return OfferColor;
                case QuestState.Achieved: return ReportColor;
                default: return ActiveColor;
            }
        }
    }
}
