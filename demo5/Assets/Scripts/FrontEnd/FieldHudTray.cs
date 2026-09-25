using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 탐험 화면 정리 (2026-09-25, 기획/탐험-화면정리와-행동칸-1차.md · approved mock 탐험-화면정리-시안/01): the exploration room HUD is two
    // papers at the top (place + route, time) and a bottom tray of three panels ('대원' cards, '이번 턴' risk/forecast/status, '행동'
    // buttons). Layout is the prefab (BuildExplorationHudTray); this only fills what must be composed or switched per mode:
    // the one-line place title (the arrival Place text keeps its two lines for other screens), the 위험도 dots on the site board
    // (the first visit shows its old risk line instead), the status line's warning colour and icon, the status line hidden while
    // the turn warning strip says the same thing (FieldTurnWarning.SuppressStatus), and the tray floor (with the two texts left on it)
    // hidden while the encounter view uses the bottom of the screen (it hides the rest of the bottom HUD itself, as before; other
    // windows hide the whole HUD or sit over it as before). Never moves anything.
    [DefaultExecutionOrder(500)]
    public sealed class FieldHudTray : MonoBehaviour
    {
        public ExpeditionArrivalPanel Arrival;
        [Tooltip("트레이 바닥 · 칸 · 제목표 (조우 창이 아래를 쓰는 동안 숨김 · 다른 창은 원래대로)")] public CanvasGroup Tray;
        [Tooltip("조우 창이 열린 동안 트레이와 함께 숨기는 글 (탄약·붕대, 첫 방문 위험 줄 · 조우 창이 직접 숨기지 않는 것)")] public Graphic[] Floating;
        [Header("장소 종이 · 한 줄 제목")]
        public Text PlaceTitle;
        [Tooltip("장소 두 줄을 한 줄로 ({0}: 첫 줄 · 장소, {1}: 둘째 줄 · 층과 방)")] public string PlaceFormat = "{0} · {1}";
        [Tooltip("둘째 줄 안의 구분자를 바꿈 ('1F · 오락실' → '1F 오락실')")] public string FloorSeparator = " · ", FloorJoin = " ";
        [Header("이번 턴 칸")]
        [Tooltip("장소 판 위험도 줄 (첫 방문에는 숨김)")] public GameObject DangerRow;
        public FieldDotsGraphic DangerDots;
        [Tooltip("첫 방문 위험 한 줄 ('위험 · 미확인' · 장소 판에서는 점으로 대신)")] public Text Risk;
        [Tooltip("경고 · 상황 한 줄 (내용이 있을 때만 보임)")] public Text Status;
        [Tooltip("상황 줄이 경고일 때 옆에 뜨는 표시")] public GameObject StatusWarn;
        [Tooltip("글자 색 / 경고 색")] public Color TextColor = new Color(.93f, .91f, .85f, 1), WarnColor = new Color(1f, .62f, .5f, 1);
        [Tooltip("턴 경고 띠 · 띠가 보이는 동안 같은 경고 줄을 숨김")] public FieldTurnWarning Warning;
        [Tooltip("위험도 미리보기를 다시 계산하는 최소 간격 (초)")] [Min(.05f)] public float PollSeconds = .25f;

        public bool Board { get; private set; }
        public bool StatusWarns { get; private set; }
        public int DangerPreview { get; private set; }

        float nextPoll; string warnFor, warnLine; (int, int, FieldSiteState, int, int) key;

        public string Title(string place)
        {
            if (string.IsNullOrEmpty(place)) return "";
            int cut = place.IndexOf('\n'); if (cut < 0) return place;
            string head = place.Substring(0, cut).Trim(), tail = place.Substring(cut + 1).Replace("\n", " ").Trim();
            if (!string.IsNullOrEmpty(FloorSeparator)) tail = tail.Replace(FloorSeparator, FloorJoin);
            return string.Format(PlaceFormat, head, tail);
        }

        void LateUpdate()
        {
            var a = Arrival; if (!a || !a.IsOpen) return;
            if (PlaceTitle && a.Place) { var s = Title(a.Place.text); if (PlaceTitle.text != s) PlaceTitle.text = s; }
            var t = a.Threat; bool board = t && t.Active && t.State != null; Board = board;
            float want = a.Encounter && a.Encounter.IsOpen ? 0 : 1;
            if (Tray && !Mathf.Approximately(Tray.alpha, want)) Tray.alpha = want;
            if (Floating != null) foreach (var g in Floating) if (g && !Mathf.Approximately(g.canvasRenderer.GetAlpha(), want)) g.canvasRenderer.SetAlpha(want);
            if (DangerRow && DangerRow.activeSelf != board) DangerRow.SetActive(board);
            // The first-visit risk line switches off as a whole (its board text is two lines; the dots replace it).
            if (Risk && Risk.gameObject.activeSelf == board) Risk.gameObject.SetActive(!board);
            if (board && DangerDots) { Poll(a, t); DangerDots.Set(t.State.Danger, DangerPreview); }
            if (Risk) Paint(Risk, !board && Warning && Warning.Condition);
            if (!Status) return;
            bool hide = Warning && Warning.SuppressStatus; if (Status.enabled == hide) Status.enabled = !hide;
            // The threat's own warning line (ExpeditionSiteThreat.StatusLine) is on the paper: warning colour and icon.
            if (board && Status.text != warnFor) { warnFor = Status.text; warnLine = t.AutoStopLine; }
            StatusWarns = board && !string.IsNullOrEmpty(Status.text) && Status.text == warnLine;
            Paint(Status, StatusWarns);
            if (StatusWarn) { bool on = StatusWarns && !hide; if (StatusWarn.activeSelf != on) StatusWarn.SetActive(on); }
        }
        // This turn's plan (or the reserved move) may raise danger: the next dots show it lightly.
        void Poll(ExpeditionArrivalPanel a, ExpeditionSiteThreat t)
        {
            var pl = t.Planner; var rooms = a.Rooms; if (!rooms) { DangerPreview = t.State.Danger; return; }
            var now = (pl ? pl.Plan.Version : 0, t.State.TurnsUsed, t.State, rooms.HasQueuedMove ? rooms.QueuedRoom : -1, rooms.CurrentRoom);
            if (now.Equals(key) && Time.unscaledTime < nextPoll) return;
            key = now; nextPoll = Time.unscaledTime + PollSeconds; warnFor = null;
            // A move everyone gathered for (ExpeditionRoomNavigation.Queue.cs) or the placed pawns' turn (every visit: Placing).
            var o = rooms.HasQueuedMove ? rooms.QueuedMoveOutlook() : pl && pl.Placing ? pl.Forecast(pl.Plan).outlook : null;
            DangerPreview = Mathf.Max(t.State.Danger, o != null ? o.Danger : 0);
        }
        void Paint(Text text, bool warn) { var c = warn ? WarnColor : TextColor; if (text.color != c) text.color = c; }
    }
}
