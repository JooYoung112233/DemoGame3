using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 턴 진행 경고 (2026-09-25, user: '다음 턴에 크리쳐가 등장할 수 있으면 '크리쳐가 다가옵니다' 경고'): after a turn, when something can
    // appear next turn, a red paper strip under the top papers says so, with a short red pulse at the screen edges.
    // The facts are the game's own (never a new rule): on the site board the thing's announced step into the party's room
    // (FieldSiteState.Incoming, the door tag '다음 턴 들어옴'; whether hushing lets it pass is the hush run on a copy of the state) or,
    // with a move reserved, that move's outlook meeting it; on the first visit the old encounter's chance for the next search at or
    // above FirstVisitChance. The strip stays until the player acts (click, key, a window) or HoldSeconds, then shrinks into the
    // '이번 턴' panel, whose status line already carries the same warning (FieldHudTray hides that line while the strip shows:
    // SuppressStatus). Presentation only: reads state, never changes it; takes no clicks; hides under windows.
    [DefaultExecutionOrder(450)]
    public sealed class FieldTurnWarning : MonoBehaviour
    {
        public enum Cause { None, Incoming, Hunt, MoveMeets, FirstVisit }

        public ExpeditionArrivalPanel Arrival;
        [Header("경고 띠 · 위쪽 종이 아래")]
        public CanvasGroup Strip;
        public Text Title, Line;
        [Tooltip("띠 제목 (정체를 알기 전 이름은 '무언가')")] public string TitleText = "무언가가 다가옵니다";
        [Tooltip("장소 판 · 다음 턴 이 방에 들어오고, 모두 숨죽이면 지나갈 때")] public string LineIncoming = "다음 턴에 이 방으로 들어옵니다 · 숨죽이면 지나갈 수 있습니다";
        [Tooltip("장소 판 · 다음 턴 이 방에 들어오고, 숨죽여도 지나가지 않을 때 (위험도 3 추적)")] public string LineHunt = "쫓아오고 있습니다 · 숨어도 지나가지 않습니다";
        [Tooltip("장소 판 · 예약한 이동이 무언가와 마주칠 때")] public string LineMove = "예약한 이동 · 들어가면 마주칩니다";
        [Tooltip("첫 방문 · 다음 수색의 조우 확률이 문턱 이상일 때 (수색한 턴 수로 오름)")] public string LineFirstVisit = "오래 뒤지는 사이 무언가 다가올 수 있습니다";
        [Tooltip("첫 방문 경고 문턱: 다음 수색의 무작위 조우 확률 (%)")] [Range(1, 100)] public int FirstVisitChance = 25;
        [Header("화면 가장자리 붉은 맥박")]
        public FieldEdgePulse Edge;
        [Tooltip("가장 진할 때 세기")] [Range(0, 1)] public float EdgeStrength = .7f;
        [Tooltip("맥박 횟수")] [Min(1)] public int EdgeBeats = 2;
        [Header("첫 방문 옛 경고 띠 (조우 창의 WarningBanner) · 이 경고가 대신하는 동안 숨김")]
        public CanvasGroup LegacyBanner;
        [Header("접히는 곳 · 이번 턴 칸")]
        [Tooltip("장소 판: 상황 줄")] public RectTransform ShrinkTarget;
        [Tooltip("첫 방문: 위험 줄")] public RectTransform ShrinkTargetFirstVisit;
        [Header("시간 · 초")]
        [Tooltip("띠가 나타나는 시간")] [Min(.01f)] public float PopIn = .2f;
        [Tooltip("나타날 때 시작 크기")] [Range(.3f, 1)] public float PopScale = .7f;
        [Tooltip("톡톡 뛰는 시간")] [Min(0)] public float PulseSeconds = 1.1f;
        [Tooltip("톡톡 뛰는 크기")] [Range(0, .3f)] public float PulseScale = .05f;
        [Tooltip("톡톡 뛰는 빠르기")] [Min(0)] public float PulseSpeed = 11;
        [Tooltip("아무 입력이 없으면 이만큼 보인 뒤 접힘")] [Min(.2f)] public float HoldSeconds = 3f;
        [Tooltip("나타난 직후 입력을 무시하는 시간 (턴 버튼 클릭이 바로 접지 않게)")] [Min(0)] public float Grace = .35f;
        [Tooltip("이번 턴 칸으로 접히는 시간")] [Min(.05f)] public float ShrinkSeconds = .4f;
        [Tooltip("접힐 때 끝 크기")] [Range(.05f, 1)] public float ShrinkScale = .3f;
        [Tooltip("가장자리 맥박 전체 시간")] [Min(.1f)] public float EdgeSeconds = .9f;

        // The facts say something can appear next turn (whether the strip shows now or has shrunk into the panel).
        public bool Condition { get; private set; }
        public Cause Reason { get; private set; }
        public string CurrentLine { get; private set; } = "";
        public bool OnBoard { get; private set; }
        // The strip is up (not shrinking); the panel's status line hides while it is on the board (same fact twice otherwise).
        public bool StripShowing => phase == Phase.Show;
        public bool SuppressStatus => StripShowing && OnBoard;
        public bool Shrinking => phase == Phase.Shrink;
        public bool EdgePlaying => Edge && Edge.gameObject.activeSelf && Edge.Strength > 0;
        // Times the strip came up since the panel loaded (verify: once per warning moment, never while nothing threatens).
        public int ShowCount { get; private set; }

        enum Phase { None, Show, Shrink }
        Phase phase; bool pending, homeKnown; int seenTurns = -1, planVersion; float shownAt, shrinkAt, edgeAt = -100; Vector2 home, shrinkFrom, shrinkTo;

        // The old first-visit encounter's chance (%) that the next search meets something (0 when it cannot: the site board, a
        // cooldown, not warned yet). ExpeditionEncounterPanel.NextChance: the ChanceAt its AfterSearch rolls (search turns, plus the noise
        // so far and the noise of the searches placed for the coming turn: FieldTurnPlanner.Current, the same number as the chip).
        public static int NextSearchChance(ExpeditionArrivalPanel a)
        {
            var e = a ? a.Encounter : null; if (!e || !a.Rooms || e.IsOpen || a.Threat && a.Threat.Active) return 0;
            var pl = a.Threat ? a.Threat.Planner : null; var k = pl && pl.Placing ? pl.Current : null;
            return e.NextChance(a.Rooms.Noise + (k != null ? k.Noise : 0));
        }
        // What the facts say now (no side effects: the hush and the move are run on copies).
        public (bool on, Cause cause, string line, bool board) Evaluate()
        {
            var a = Arrival; var t = a ? a.Threat : null; var rooms = a ? a.Rooms : null;
            if (!a || !rooms) return (false, Cause.None, "", false);
            if (t && t.Active && t.State != null)
            {
                var s = t.State;
                if (rooms.HasQueuedMove) { var o = rooms.QueuedMoveOutlook(); return o != null && o.Encounter ? (true, Cause.MoveMeets, LineMove, true) : (false, Cause.None, "", true); }
                if (!s.Incoming) return (false, Cause.None, "", true);
                var h = s.Copy(); h.MoveParty(rooms.CurrentRoom); h.EndTurn(0, true);
                return h.PassedBy ? (true, Cause.Incoming, LineIncoming, true) : (true, Cause.Hunt, LineHunt, true);
            }
            int chance = NextSearchChance(a);
            return chance >= FirstVisitChance ? (true, Cause.FirstVisit, LineFirstVisit, false) : (false, Cause.None, "", false);
        }
        public static bool Covered(ExpeditionArrivalPanel a) => a.Popup.activeSelf || a.Search && a.Search.IsOpen || a.Loot && a.Loot.IsOpen || a.FieldBags && a.FieldBags.IsOpen
            || a.Encounter && a.Encounter.IsOpen || a.Story && a.Story.IsOpen || a.Main && a.Main.alpha < .01f;

        void OnDisable() { HideNow(); pending = false; }

        void LateUpdate()
        {
            var a = Arrival;
            if (!a || !a.IsOpen || !a.Rooms) { HideNow(); pending = false; Condition = false; Reason = Cause.None; CurrentLine = ""; seenTurns = -1; Legacy(false); return; }
            var (on, cause, line, board) = Evaluate(); int turns = a.Rooms.Turns; var pl = a.Threat ? a.Threat.Planner : null;
            // A new warning moment: it just became true, a turn passed while it holds, or it says something else now.
            if (on && (!Condition || turns != seenTurns || line != CurrentLine)) pending = true;
            if (!on) pending = false;
            Condition = on; Reason = cause; CurrentLine = on ? line : ""; OnBoard = board; seenTurns = turns;
            // The turn's clock sequence plays first; the warning (and its edge pulse) follows it, so the eye takes one thing at a time.
            bool covered = Covered(a), busy = a.InTransit || pl && pl.Resolving || FieldTurnPulse.Playing;
            Legacy(covered || on);
            if (pending && !covered && !busy) { pending = false; Show(a, pl); }
            float now = Time.unscaledTime;
            if (phase == Phase.Show)
            {
                bool acted = now - shownAt > Grace && (Pressed() || pl && pl.Plan.Version != planVersion);
                if (!on || covered || acted || now - shownAt >= HoldSeconds) StartShrink(a, board);
                else Pop(now - shownAt);
            }
            if (phase == Phase.Shrink) Shrink(now - shrinkAt, covered);
            if (Edge)
            {
                float u = (now - edgeAt) / EdgeSeconds; bool play = u >= 0 && u < 1 && !covered;
                if (Edge.gameObject.activeSelf != play) Edge.gameObject.SetActive(play);
                Edge.Strength = play ? EdgeStrength * Mathf.Abs(Mathf.Sin(u * Mathf.PI * EdgeBeats)) * (1 - .35f * u) : 0;
            }
        }

        void Show(ExpeditionArrivalPanel a, FieldTurnPlanner pl)
        {
            if (!Strip) return;
            var r = (RectTransform)Strip.transform; if (!homeKnown) { home = r.anchoredPosition; homeKnown = true; }
            r.anchoredPosition = home; r.localScale = Vector3.one * PopScale; Strip.alpha = 0;
            if (Title) Title.text = TitleText; if (Line) Line.text = CurrentLine;
            if (!Strip.gameObject.activeSelf) Strip.gameObject.SetActive(true);
            phase = Phase.Show; shownAt = edgeAt = Time.unscaledTime; planVersion = pl ? pl.Plan.Version : 0; ShowCount++;
        }
        void Pop(float t)
        {
            float u = Mathf.Clamp01(t / PopIn); Strip.alpha = u;
            float scale = Mathf.LerpUnclamped(PopScale, 1, 1 - (1 - u) * (1 - u) + .12f * Mathf.Sin(u * Mathf.PI));
            if (t < PulseSeconds) scale += PulseScale * Mathf.Sin(t * PulseSpeed) * (1 - t / PulseSeconds) * u;
            Strip.transform.localScale = Vector3.one * scale;
        }
        void StartShrink(ExpeditionArrivalPanel a, bool board)
        {
            phase = Phase.Shrink; shrinkAt = Time.unscaledTime; var r = (RectTransform)Strip.transform; shrinkFrom = r.anchoredPosition; shrinkTo = shrinkFrom;
            var target = board ? ShrinkTarget : ShrinkTargetFirstVisit ? ShrinkTargetFirstVisit : ShrinkTarget;
            if (target && r.parent is RectTransform parent)
            {
                // The target's centre in the strip parent's space, as the strip's anchored position (pivot kept).
                Vector2 local = parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
                shrinkTo = shrinkFrom + (local - (Vector2)parent.InverseTransformPoint(r.TransformPoint(r.rect.center)));
            }
        }
        void Shrink(float t, bool covered)
        {
            float u = Mathf.Clamp01(t / ShrinkSeconds), e = u * u * (3 - 2 * u); var r = (RectTransform)Strip.transform;
            r.anchoredPosition = Vector2.Lerp(shrinkFrom, shrinkTo, e); r.localScale = Vector3.one * Mathf.Lerp(1, ShrinkScale, e);
            Strip.alpha = covered ? 0 : 1 - e;
            if (u >= 1) HideNow();
        }
        void HideNow()
        {
            phase = Phase.None;
            if (Strip)
            {
                Strip.alpha = 0; var r = (RectTransform)Strip.transform; if (homeKnown) r.anchoredPosition = home; r.localScale = Vector3.one;
                if (Strip.gameObject.activeSelf) Strip.gameObject.SetActive(false);
            }
            if (Edge) { Edge.Strength = 0; if (Edge.gameObject.activeSelf) Edge.gameObject.SetActive(false); }
            edgeAt = -100;
        }
        // The first visit's old banner ('… 가까운 곳에서 발소리가 들립니다.') steps aside while this warning speaks for it or a window is up.
        void Legacy(bool hide) { if (!LegacyBanner) return; float want = hide ? 0 : 1; if (!Mathf.Approximately(LegacyBanner.alpha, want)) LegacyBanner.alpha = want; }
        static bool Pressed()
        {
            var m = Mouse.current; var k = Keyboard.current;
            return m != null && (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame) || k != null && k.anyKey.wasPressedThisFrame;
        }
    }
}
