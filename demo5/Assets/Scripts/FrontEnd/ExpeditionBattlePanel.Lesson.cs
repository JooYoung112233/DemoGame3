using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Demo5.FrontEnd
{
    // First meeting with the site board's resident (귀기울임, Listen), taught once (2026-09-25 user decision '튜토에서 알려주는걸로하자').
    // The first time it marks the cell of the member who fired: the cause in the feedback strip, one instruction in the hint strip,
    // a red frame on the marked cell and a gold frame plus arrow on ONE recommended safe cell (the Guard card when the shooter cannot step aside).
    // Same frame and arrow as the settlement tutorial (TutorialTargetGraphic / TutorialPointer); no dim, so the creature and its tags stay readable.
    // Shown once per site (ExpeditionSiteThreat.ResidentLessonShown, saved with the site board). Provisional layout: approved mock 10 has no tutorial element.
    // Objects are made and wired by AgentScripts/FixBattleActionCardLines. No Update/LateUpdate here: the main LateUpdate calls PlaceLesson().
    public sealed partial class ExpeditionBattlePanel
    {
        [Header("첫 만남 안내 · 장소 판의 그것(귀기울임) · 한 번")]
        [Tooltip("장소 판의 그것이 총을 쏜 대원의 칸을 처음 노릴 때 한 번 안내 (장소 판 저장에 기록)")] public bool ResidentLesson = true;
        [Tooltip("예고 직후 멈춰 보여 주는 시간(초) · 연출 속도로 나눔")][Min(0)] public float LessonHold = 1.6f;
        [Tooltip("{0} 크리쳐+이/가, {1} 쏜 대원+이/가")] public string LessonFeedback = "{0} 총소리를 들었습니다 · {1} 선 칸을 노립니다";
        [Tooltip("{0} 먼저 움직일 대원")] public string AllyPhaseLessonDetail = "{0}부터 · 표시된 칸에서 비키세요";
        [Tooltip("쏜 대원의 차례 · 옮길 칸이 있을 때")] public string LessonMoveHint = "총을 쏜 칸이 노려집니다 · 빛나는 칸으로 옮기면 헛칩니다 · 옮긴 뒤에도 행동 1회";
        [Tooltip("{0} 예고 피해 감소 · 쏜 대원이 비킬 칸이 없을 때")] public string LessonGuardHint = "비킬 빈 칸이 없습니다 · 방어하면 예고 피해 -{0} · 밀림 없음";
        [Tooltip("{0} 빈틈 명중 보너스 · 비운 칸을 내려쳤을 때")] public string LessonWhiff = "헛쳤습니다 · 빈틈 동안 방어 0 · 명중 +{0}%p";
        [Tooltip("칸 틀과 화살표의 부모 (Workspace의 PawnHudLayer 바로 위, 1920×1080 좌상단)")] public RectTransform LessonMarks;
        [Tooltip("노려진 칸(붉은 틀) · 권하는 칸(금색 틀) · 방어 카드(금색 틀, 하단 띠 위)")] public TutorialTargetGraphic LessonDangerFrame, LessonMoveFrame, LessonGuardFrame;
        [Tooltip("권하는 칸을 가리키는 화살표")] public TutorialPointer LessonPointer;
        public Color LessonDangerColor = new Color(.95f, .36f, .28f, .95f), LessonTargetColor = new Color(1, .79f, .36f, 1);
        [Min(0)] public float LessonPadding = 6, LessonPulseGrow = 10, LessonPointerGap = 8, LessonPointerBounce = 12, LessonPointerSpeed = 5;
        [Tooltip("화살표 크기")] public Vector2 LessonPointerSize = new Vector2(58, 70);

        int lessonShooter = -1, lessonCreature = -1, lessonCell = -1, lessonTarget = -1;
        bool lessonActive, lessonTargetDirty, lessonMarksShown;
        readonly Vector2[] lessonQuad = new Vector2[4];
        readonly Vector3[] lessonCorners = new Vector3[4];

        // Review/test reads.
        public bool LessonActive => lessonActive;
        public int LessonShooter => lessonShooter;
        public int LessonCell => lessonCell;
        bool LessonAvailable => ResidentLesson && arrival && arrival.Threat && arrival.Threat.Active && !arrival.Threat.ResidentLessonShown;
        // The shooter's own decision while it still stands on the marked cell (a push by another creature ends the instruction; the mark stays until the strike).
        bool LessonShooterTurn => State != null && State.PlayerTurn && State.Actor == lessonShooter && !State.Moved
            && FieldBattleState.CellOf(State.Current.Depth, State.Current.Lane) == lessonCell;
        bool LessonEmphasis(int unit) => lessonActive && (unit == lessonShooter || unit == lessonCreature);

        // Hook A (Run, after each enemy step's replay). Starts on the resident's first shot-marked cell; ends when that strike plays.
        IEnumerator LessonAfterEnemyStep()
        {
            if (State == null) yield break;
            if (lessonActive)
            {
                var strike = State.Events.FirstOrDefault(e => e.Kind == BattleEventKind.Strike && e.Actor == lessonCreature);
                if (strike == null) yield break;
                bool whiff = strike.Whiff; EndLesson();
                if (whiff) { feedbackOverride = string.Format(LessonWhiff, Rules.StaggerHitBonus); Refresh(); yield return Pause(LessonHold * .6f); }
                yield break;
            }
            if (!LessonAvailable) yield break;
            var windup = State.Events.FirstOrDefault(e => e.Kind == BattleEventKind.Windup && e.Attack == CreatureAttack.Listen && e.Cells.Count > 0
                && e.Actor >= 0 && e.Actor < State.Units.Count && State.Units[e.Actor].Creature != null && State.Units[e.Actor].Creature.Id == arrival.Threat.ResidentId);
            if (windup == null) yield break;
            // A wind-up moves nobody: the shooter is whoever stands on the marked cell (Loud is already reset when the round turned over).
            int shooter = State.Units.FindIndex(u => !u.Enemy && u.Alive && FieldBattleState.CellOf(u.Depth, u.Lane) == windup.Cells[0]);
            if (shooter < 0) yield break;
            lessonShooter = shooter; lessonCreature = windup.Actor; lessonCell = windup.Cells[0]; lessonActive = lessonTargetDirty = true;
            arrival.Threat.MarkResidentLesson();
            feedbackOverride = LessonCause();
            blinkUntil = Time.unscaledTime + LessonHold / (Presentation ? Mathf.Max(.1f, Presentation.Speed) : 1); tintDirty = true;
            Refresh();
            yield return Pause(LessonHold);
        }
        string LessonCause() => string.Format(LessonFeedback, Subject(State.Units[lessonCreature].Name), Subject(State.Units[lessonShooter].Name));
        // Ends the lesson once it no longer applies (shooter down, the mark gone because the resident fell, the fight over). Not during a replay:
        // the strike clears the mark in the rules before it plays, and hook A still has to see it.
        bool LessonLive()
        {
            if (!lessonActive) return false;
            if (Busy) return true;
            bool live = State != null && State.Outcome == FieldBattleOutcome.Playing && lessonShooter >= 0 && lessonShooter < State.Units.Count && lessonCreature >= 0 && lessonCreature < State.Units.Count
                && State.Units[lessonShooter].Alive && State.Units[lessonCreature].Alive && State.Units[lessonCreature].Pending != null;
            if (!live) EndLesson();
            return live;
        }
        // Hook D (Refresh, feedback strip): the cause stays up for the shooter's whole decision. Also marks the recommended cell for re-evaluation.
        string LessonLine()
        {
            lessonTargetDirty = true;
            if (Busy || !LessonLive() || !LessonShooterTurn) return null;
            return LessonCause();
        }
        // Hook C (HintText, before the bound line): one instruction for the shooter.
        string LessonHint()
        {
            if (!LessonLive() || !LessonShooterTurn) return null;
            return LessonMoveCell() >= 0 ? LessonMoveHint : string.Format(LessonGuardHint, Rules.GuardStrikeReduction);
        }
        // The ONE recommended cell (lane*3+depth): the shooter's neighbours in a fixed order (lane-1, lane+1, depth+1, depth-1), the first it can step to
        // that is not marked and where the forecast shows no attack on it (the same forecast as the hover MoveHint). -1 when none qualifies.
        public int LessonMoveCell()
        {
            if (!lessonActive || !LessonShooterTurn) return -1;
            var u = State.Units[lessonShooter]; var danger = new HashSet<int>(State.DangerCells());
            int[,] steps = { { 0, -1 }, { 0, 1 }, { 1, 0 }, { -1, 0 } };
            for (int k = 0; k < 4; k++)
            {
                int d = u.Depth + steps[k, 0], l = u.Lane + steps[k, 1];
                if (!State.CanMove(d, l) || danger.Contains(FieldBattleState.CellOf(d, l))) continue;
                if (State.PredictIntents(State.Actor, d, l).Any(p => (p.Kind == EnemyIntentKind.Attack || p.Kind == EnemyIntentKind.Strike) && p.Hits != null && p.Hits.Any(h => h.Target == State.Actor))) continue;
                return FieldBattleState.CellOf(d, l);
            }
            return -1;
        }
        void ResetLesson()
        {
            lessonActive = false; lessonShooter = lessonCreature = lessonCell = lessonTarget = -1; lessonTargetDirty = false; HideLesson();
        }
        void EndLesson()
        {
            ResetLesson();
            linkEnemy = linkAlly = int.MinValue; // UpdateLinks re-applies the tag emphasis on the next frame
        }
        void HideLesson()
        {
            LessonToggle(LessonDangerFrame, false); LessonToggle(LessonMoveFrame, false); LessonToggle(LessonGuardFrame, false); LessonToggle(LessonPointer, false);
            lessonMarksShown = false;
        }
        static void LessonToggle(Component c, bool on) { if (c && c.gameObject.activeSelf != on) c.gameObject.SetActive(on); }

        // Hook E (LateUpdate): frames follow the board through screen shake and the item-drawer pan.
        void PlaceLesson()
        {
            if (!lessonActive) { if (lessonMarksShown) HideLesson(); return; }
            if (!LessonLive()) { Refresh(); return; }
            if (lessonTargetDirty) { lessonTarget = LessonMoveCell(); lessonTargetDirty = false; }
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 3);
            var shooter = State.Units[lessonShooter];
            // Red: the marked cell, through the hold and the party's phase, until the shooter steps off it.
            bool danger = LessonDangerFrame && shooter.Alive && FieldBattleState.CellOf(shooter.Depth, shooter.Lane) == lessonCell;
            if (danger) LessonFrame(LessonDangerFrame, LessonCellRect((RectTransform)LessonDangerFrame.transform.parent, lessonCell), LessonDangerColor, pulse);
            LessonToggle(LessonDangerFrame, danger);
            // Gold: the shooter's own decision (not aiming, no drawer, no retreat review): one cell with the arrow, or the Guard card.
            bool turn = CanInput && !aiming && LessonShooterTurn;
            bool move = turn && lessonTarget >= 0 && LessonMoveFrame;
            if (move)
            {
                var box = LessonCellRect((RectTransform)LessonMoveFrame.transform.parent, lessonTarget);
                LessonFrame(LessonMoveFrame, box, LessonTargetColor, pulse);
                if (LessonPointer) LessonPoint(LessonCellRect((RectTransform)LessonPointer.transform.parent, lessonTarget));
            }
            LessonToggle(LessonMoveFrame, move); LessonToggle(LessonPointer, move && LessonPointer);
            bool guard = turn && lessonTarget < 0 && LessonGuardFrame && Guard;
            if (guard) LessonFrame(LessonGuardFrame, LessonBounds((RectTransform)LessonGuardFrame.transform.parent, (RectTransform)Guard.transform), LessonTargetColor, pulse);
            LessonToggle(LessonGuardFrame, guard);
            lessonMarksShown = danger || move || guard;
        }
        // Pulsing frame drawn just outside the box (the box is in the frame's parent space), as the settlement guide does.
        void LessonFrame(TutorialTargetGraphic frame, Rect box, Color color, float pulse)
        {
            var r = frame.rectTransform; var parent = (RectTransform)r.parent;
            r.pivot = new Vector2(.5f, .5f); r.position = parent.TransformPoint(box.center);
            r.sizeDelta = box.size + Vector2.one * (LessonPadding * 2 + LessonPulseGrow * pulse);
            color.a *= .8f + .2f * pulse; frame.color = color;
        }
        // Arrow beside the cell: above, left, right or below (pointing at it), wherever it and its bounce cover the least readable text
        // (pawn names, tags, the feedback strip); ties keep that order. It stays under the top band; nothing fits: below.
        static readonly (Vector2 dir, float angle)[] LessonSides = { (Vector2.up, 0), (Vector2.left, 90), (Vector2.right, -90), (Vector2.down, 180) };
        readonly List<UnityEngine.UI.Text> lessonTexts = new List<UnityEngine.UI.Text>();
        void LessonPoint(Rect target)
        {
            var r = LessonPointer.rectTransform; var parent = (RectTransform)r.parent; var area = parent.rect; var size = LessonPointerSize;
            float bob = LessonPointerBounce * (.5f + .5f * Mathf.Sin(Time.unscaledTime * LessonPointerSpeed)), clear = LessonPadding + LessonPulseGrow * .5f + LessonPointerGap;
            GetComponentsInChildren(false, lessonTexts);
            float best = float.MaxValue; Vector2 at = new Vector2(target.center.x, target.yMin - clear - size.y * .5f - bob); float angle = 180;
            foreach (var (dir, a) in LessonSides)
            {
                var box = dir.y != 0 ? size : new Vector2(size.y, size.x);
                float reach = (dir.y != 0 ? target.height : target.width) * .5f + clear + (dir.y != 0 ? box.y : box.x) * .5f;
                var near = target.center + dir * reach; var far = near + dir * LessonPointerBounce;
                var sweep = Rect.MinMaxRect(Mathf.Min(near.x, far.x) - box.x * .5f, Mathf.Min(near.y, far.y) - box.y * .5f, Mathf.Max(near.x, far.x) + box.x * .5f, Mathf.Max(near.y, far.y) + box.y * .5f);
                if (sweep.yMax > area.yMax - TopBandBottom || sweep.yMin < area.yMin + 4 || sweep.xMin < area.xMin + 4 || sweep.xMax > area.xMax - 4) continue;
                float cover = 0;
                foreach (var t in lessonTexts)
                {
                    if (!t.isActiveAndEnabled || string.IsNullOrEmpty(t.text) || t.color.a < .2f) continue;
                    var b = LessonBounds(parent, t.rectTransform); if (b.width > area.width * .5f || b.height > area.height * .5f || !b.Overlaps(sweep)) continue;
                    cover += (Mathf.Min(b.xMax, sweep.xMax) - Mathf.Max(b.xMin, sweep.xMin)) * (Mathf.Min(b.yMax, sweep.yMax) - Mathf.Max(b.yMin, sweep.yMin));
                }
                if (cover < best) { best = cover; at = near + dir * bob; angle = a; }
            }
            r.pivot = new Vector2(.5f, .5f); r.sizeDelta = size; r.position = parent.TransformPoint(at); r.localEulerAngles = new Vector3(0, 0, angle);
        }
        // A board cell's bounding box in 'space', projected like RebuildGrid draws it (through the stage, so shake and pan follow).
        Rect LessonCellRect(RectTransform space, int cell)
        {
            var c = Cells[cell]; lessonQuad[0] = c.TopLeft; lessonQuad[1] = c.TopRight; lessonQuad[2] = c.BottomRight; lessonQuad[3] = c.BottomLeft;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in lessonQuad)
            {
                var q = LayerPoint(space, stage.TransformPoint(LocalFromCanvas(new Vector2(p.x, -p.y))));
                min = Vector2.Min(min, q); max = Vector2.Max(max, q);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
        // The rect of 'r' in the space of 'space' (SettlementTutorialGuide.Bounds).
        Rect LessonBounds(RectTransform space, RectTransform r)
        {
            r.GetWorldCorners(lessonCorners); Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var c in lessonCorners) { Vector2 p = space.InverseTransformPoint(c); min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
