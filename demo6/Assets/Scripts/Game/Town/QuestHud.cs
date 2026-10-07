using System.Collections.Generic;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 목표 HUD(기획/마을-의뢰-첫판.md 6-1, 꾸러미 3, 마을·던전 공용). 왼쪽 위에 의뢰 줄을 그린다: 던전은 층 이름(DungeonHud) 바로 아래(y ≈ 46)에 2줄,
    /// 마을은 "등불골 · 갱도 마당" 아래 3줄. 줄은 QuestBook.HudLines가 만든다(마을은 보고할 것 먼저, 던전은 진행 중 먼저, 같은 갈래는 최근에 받은 순,
    /// 진행 중 '· ', 보고할 것 '… '). '— 무진에게'·'— 경비 초소에'는 SpeakerIdentity.ReportTarget을 거쳤다(이름을 모르면 자리 이름).
    /// 던전 HUD 파일은 다른 작업이 고치는 중이라 이 부품이 따로 그린다. OnGUI(그리기 사건만, GUI.depth 5).
    /// 줄은 0.2초마다(또는 Refresh 뒤 바로) 다시 읽는다. 화면 배치·반응형 다듬기는 Unity 개발 단계로 둔다.
    /// </summary>
    public sealed class QuestHud : MonoBehaviour
    {
        public const int TownLines = 3;
        /// <summary>던전 의뢰 줄 수(묶음 3 가-3: 2 → 3, 셋째 의뢰가 가려지지 않게).</summary>
        public const int DungeonLines = 3;
        /// <summary>줄을 다시 읽는 간격(초, 실제 시간).</summary>
        public const float RefreshSeconds = 0.2f;

        const int GuiDepth = 5;
        const float Margin = 16f;
        /// <summary>층 이름·마을 이름 줄 아래 첫 줄 높이(DungeonHud 층 이름 Rect(16, 16, 390, 26) 바로 아래).</summary>
        const float FirstLineY = 46f;
        const float LineHeight = 24f;
        const float LineWidth = 600f;

        static readonly Color ActiveColor = new Color(0.9f, 0.86f, 0.77f, 1f);
        static readonly Color ReportColor = new Color(0.86f, 0.78f, 0.60f, 1f);
        static readonly Color TitleColor = new Color(0.69f, 0.66f, 0.60f, 1f);

        /// <summary>Refresh가 부른 차례(바뀌면 줄을 바로 다시 읽는다).</summary>
        static int s_version;

        /// <summary>마을 / 던전. 정하지 않으면 던전 루트가 있으면 던전, 없으면 마을.</summary>
        public QuestHudMode Mode
        {
            get => _mode ?? (DungeonRoot.Instance ? QuestHudMode.Dungeon : QuestHudMode.Town);
            set
            {
                _mode = value;
                _readVersion = -1;
            }
        }

        /// <summary>최대 줄 수(0 이하면 모드 기본: 마을 3, 던전 2).</summary>
        public int MaxLines { get; set; }

        /// <summary>마을에서 "등불골 · 갱도 마당"을 이 부품이 그린다(마을 HUD가 이미 그리면 끈다).</summary>
        public bool DrawTownTitle { get; set; } = true;

        QuestHudMode? _mode;
        readonly List<string> _lines = new List<string>();
        float _nextRead = -1f;
        int _readVersion = -1;
        GUIStyle _style;
        Font _styleFont;

        /// <summary>이 오브젝트에 HUD를 붙인다(이미 있으면 모드만 바꿈).</summary>
        public static QuestHud Attach(GameObject host, QuestHudMode mode)
        {
            if (!host) return null;
            var hud = host.GetComponent<QuestHud>();
            if (!hud) hud = host.AddComponent<QuestHud>();
            hud.Mode = mode;
            hud.Refresh(true);
            return hud;
        }

        /// <summary>의뢰 상태가 바뀜(받기·보고·던전 사건): 모든 HUD가 다음 그리기에서 줄을 다시 읽는다.</summary>
        public static void Refresh() => s_version++;

        /// <summary>지금 보이는 줄(시험 확인용).</summary>
        public IReadOnlyList<string> Lines
        {
            get
            {
                Refresh(false);
                return _lines;
            }
        }

        int Limit => MaxLines > 0 ? MaxLines : Mode == QuestHudMode.Town ? TownLines : DungeonLines;

        void Refresh(bool force)
        {
            float now = Time.unscaledTime;
            if (!force && _readVersion == s_version && now < _nextRead) return;
            _readVersion = s_version;
            _nextRead = now + RefreshSeconds;
            _lines.Clear();
            var carry = ProfileCarry.Ensure();
            _lines.AddRange(new QuestBook(carry).HudLines(Mode, Limit));
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            Refresh(false);
            bool title = Mode == QuestHudMode.Town && DrawTownTitle;
            if (_lines.Count == 0 && !title) return;
            var prevMatrix = GUI.matrix;
            DungeonUi.Begin();
            EnsureStyle();
            GUI.depth = GuiDepth;
            if (title) DungeonUi.ShadowLabel(new Rect(Margin, Margin, 390f, 26f), TownScript.TownTitle, _style, TitleColor);
            float y = FirstLineY;
            foreach (var line in _lines)
            {
                bool report = line.StartsWith(TownScript.HudReportPrefix);
                DungeonUi.ShadowLabel(new Rect(Margin, y, LineWidth, LineHeight), line, _style, report ? ReportColor : ActiveColor);
                y += LineHeight;
            }
            GUI.matrix = prevMatrix;
        }

        void EnsureStyle()
        {
            var font = DungeonUi.Small.font;
            if (_style != null && _styleFont == font) return;
            _styleFont = font;
            _style = new GUIStyle(DungeonUi.Small) { wordWrap = false, clipping = TextClipping.Clip };
            _style.normal.textColor = Color.white;
        }
    }
}
