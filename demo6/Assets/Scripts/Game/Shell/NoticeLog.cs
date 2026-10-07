using System.Collections.Generic;
using Demo6.Core.Dungeon;
using Demo6.Core.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Demo6.Game
{
    /// <summary>
    /// 지난 알림(기획/저장-처음화면-멈춤창-1차.md 7장, 11-7 'D'). 화면 아래 알림 한 줄(DungeonEvents.Message — 던전 HUD와 마을 HUD가 같은 통로를 듣는다)과
    /// 던전 HUD 발견 알림(DungeonEvents.Discovered — '손에 넣었다 · 곡괭이'·'숨은 방' 등, 글은 Core DiscoveryNoticeText, 조용한 지역 이름은 뺌),
    /// 레벨업('레벨 5에 올랐다')을 곳 표시('[마을]'·'[1층]'·'[굴]'·'[시험장]')와 함께 NoticeHistory(40줄)에 담고, 멈춤 창 '지난 알림'에서 스크롤 목록으로 보인다.
    /// 듣는 법: 장면 루트 Awake의 SceneStatics.Reset이 DungeonEvents 구독을 비우므로 SceneManager.sceneLoaded(장면 Awake 뒤)마다 빼고 다시 건다.
    /// sceneLoaded는 엔진 정적 사건이라 도메인 다시 불러오기가 꺼져 있으면 앞 플레이의 구독이 남는다 — 게임을 켤 때도 빼고 다시 더한다.
    /// 장면 루트 Awake 안에서 나온 알림 몇 줄은 놓칠 수 있다(14장 8). 게임을 켠 동안만 남고, 이어하기·새로 시작 때 Clear로 비운다(부르는 곳은 작성자 C).
    /// DungeonHud.cs·TownHud.cs는 고치지 않는다.
    /// </summary>
    public static class NoticeLog
    {
        /// <summary>목록이 비었을 때 글(7장).</summary>
        public const string EmptyText = "아직 알림이 없다.";

        const float LineGap = 4f;
        /// <summary>세로 스크롤 막대 자리(가로 스크롤이 생기지 않게 글 너비에서 뺀다).</summary>
        const float ScrollbarRoom = 22f;

        static NoticeHistory _history = new NoticeHistory();
        static Vector2 _scroll;
        /// <summary>마지막으로 맨 아래로 맞춘 목록 판(NoticeHistory.Revision). 다르면 목록이 늘었거나 바뀐 것.</summary>
        static int _drawnRevision = -1;
        /// <summary>마지막으로 그린 프레임. 한 프레임 넘게 비었으면 창을 새로 연 것으로 보고 맨 아래로 맞춘다.</summary>
        static int _lastDrawFrame = -10;
        static int _linesRevision = -1;
        static readonly string[] Lines = new string[NoticeHistory.Capacity];
        static readonly float[] Heights = new float[NoticeHistory.Capacity];
        static readonly GUIContent Content = new GUIContent();
        static GUIStyle _line;
        static GUIStyle _lineBase;
        static GUIStyle _empty;
        static GUIStyle _emptyBase;

        /// <summary>담긴 줄(오래된 것부터, 맨 끝이 가장 최근).</summary>
        public static IReadOnlyList<NoticeEntry> Entries => _history.Entries;

        /// <summary>도메인 다시 불러오기 꺼짐 대비: 플레이 시작 때 목록·스크롤·스타일 캐시를 새로.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _history = new NoticeHistory();
            _scroll = Vector2.zero;
            _drawnRevision = -1;
            _lastDrawFrame = -10;
            _linesRevision = -1;
            _line = null;
            _lineBase = null;
            _empty = null;
            _emptyBase = null;
        }

        /// <summary>게임을 켤 때 장면 불러오기 사건에 건다(앞 플레이에서 남은 구독은 빼고 다시 더함).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= Hook;
            SceneManager.sceneLoaded += Hook;
        }

        /// <summary>장면마다(장면 루트 Awake의 SceneStatics.Reset 뒤) 알림 통로에 다시 건다. 두 번 걸리지 않게 빼고 더한다.</summary>
        static void Hook(Scene scene, LoadSceneMode mode)
        {
            DungeonEvents.Message -= OnMessage;
            DungeonEvents.Message += OnMessage;
            DungeonEvents.LevelUp -= OnLevelUp;
            DungeonEvents.LevelUp += OnLevelUp;
            DungeonEvents.Discovered -= OnDiscovered;
            DungeonEvents.Discovered += OnDiscovered;
        }

        static void OnMessage(string text) => _history.Add(text, Place());

        /// <summary>발견 알림(DungeonHud 아래 알림과 같은 글). 조용한 지역 이름·층 완전 탐험은 담지 않는다(DiscoveryNoticeText가 null).</summary>
        static void OnDiscovered(DiscoveryKind kind, Vector2 pos, string label)
        {
            string line = DiscoveryNoticeText.Line(kind, label);
            if (line != null) _history.Add(line, Place());
        }

        static void OnLevelUp(int level) => _history.Add("레벨 " + level + "에 올랐다", Place());

        /// <summary>지금 곳: 마을 · n층 · 굴 · 시험장. 처음 화면처럼 루트가 없으면 null(곳 표시 없음).</summary>
        static string Place()
        {
            if (TownRoot.Instance) return "마을";
            var dungeon = DungeonRoot.Instance;
            if (dungeon) return dungeon.IsDen ? "굴" : dungeon.Floor + "층";
            if (CombatTestRoot.Instance) return "시험장";
            return null;
        }

        /// <summary>모두 비운다(이어하기·새로 시작 때, 7장).</summary>
        public static void Clear()
        {
            _history.Clear();
            _scroll = Vector2.zero;
        }

        /// <summary>
        /// 지난 알림 스크롤 목록을 area(DungeonUi 기준 좌표) 안에 그린다. 맨 아래가 가장 최근이고, 처음 그릴 때와 목록이 늘거나 바뀌었을 때 맨 아래로 맞춘다.
        /// 비었으면 '아직 알림이 없다.' OnGUI 안에서만 부른다(제목·'돌아가기'는 작성자 C가 그림).
        /// </summary>
        public static void DrawList(Rect area)
        {
            EnsureStyles();
            int frame = Time.frameCount;
            bool fresh = frame - _lastDrawFrame > 1;
            _lastDrawFrame = frame;

            var entries = _history.Entries;
            int count = Mathf.Min(entries.Count, Lines.Length);
            if (count == 0)
            {
                GUI.Label(area, EmptyText, _empty);
                _drawnRevision = _history.Revision;
                return;
            }

            if (_linesRevision != _history.Revision)
            {
                _linesRevision = _history.Revision;
                for (int i = 0; i < count; i++) Lines[i] = entries[i].Line();
            }

            float width = Mathf.Max(40f, area.width - ScrollbarRoom);
            float total = 0f;
            for (int i = 0; i < count; i++)
            {
                Content.text = Lines[i];
                Heights[i] = _line.CalcHeight(Content, width);
                total += Heights[i] + (i > 0 ? LineGap : 0f);
            }

            if (fresh || _drawnRevision != _history.Revision)
            {
                _drawnRevision = _history.Revision;
                _scroll.y = Mathf.Max(0f, total - area.height);
            }

            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0f, 0f, width, total));
            float y = 0f;
            for (int i = 0; i < count; i++)
            {
                GUI.Label(new Rect(0f, y, width, Heights[i]), Lines[i], _line);
                y += Heights[i] + LineGap;
            }
            GUI.EndScrollView();
        }

        /// <summary>DungeonUi 스타일(Small)로 만든다. DungeonUi가 플레이마다 스타일을 새로 만들면 따라 새로 만든다.</summary>
        static void EnsureStyles()
        {
            var lineBase = DungeonUi.Small ?? GUI.skin.label;
            if (_line == null || _lineBase != lineBase)
            {
                _lineBase = lineBase;
                _line = new GUIStyle(lineBase) { wordWrap = true };
                _line.normal.textColor = DungeonUi.Bone;
            }
            var emptyBase = DungeonUi.Small ?? GUI.skin.label;
            if (_empty == null || _emptyBase != emptyBase)
            {
                _emptyBase = emptyBase;
                _empty = new GUIStyle(emptyBase) { wordWrap = true };
                _empty.normal.textColor = DungeonUi.BoneDim;
            }
        }
    }
}
