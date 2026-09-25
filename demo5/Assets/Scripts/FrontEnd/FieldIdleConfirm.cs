using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    // 할 일이 없는 대원 확인 (2026-09-25, 기획/탐험-화면정리와-행동칸-1차.md §4 · 말 놓기): on every visit (FieldTurnPlanner.Placing,
    // the first one included), '턴 진행' with a living member who has nothing to do (the planner's own check: they would hush; a pawn
    // waiting at a door or with a queued bag item is not idle) and no gathered move asks first, in the arrival panel's popup (the door
    // popup frame: title, body, confirm, back). With nobody placed it asks '아무도 할 일이 없습니다. 모두 숨죽이고 진행할까요?' — the
    // '모두 숨죽이기' button is gone, this is the hush turn (user decision 2). '숨죽이고 진행' runs the turn exactly as the button does;
    // '돌아가기' (or Esc) closes it and no time passes. A gathered move never asks (everyone moves together).
    // '계속 진행' asks once when it is switched on (FieldAutoAdvance.Flip) and then runs every turn without asking.
    // Hook: one line in FieldTurnPlanner.Run after the move line. Lives on the arrival panel root next to the planner (BuildIdleConfirm.Run).
    public sealed class FieldIdleConfirm : MonoBehaviour
    {
        // Verify scripts that press '턴 진행' with idle members set this: no question, the turn runs (and '계속 진행' starts at once).
        // Off again at every Play start.
        public static bool AutoAccept;

        public ExpeditionArrivalPanel Arrival;
        [Tooltip("'계속 진행' · 켜져 있는 동안은 '턴 진행'도 묻지 않습니다")] public FieldAutoAdvance Auto;
        [Header("확인창 · 턴 진행")]
        [Tooltip("확인창 제목")] public string Title = "할 일이 없는 대원";
        [Tooltip("일부 대원만 할 일이 없을 때 ({0}: 대원 이름, {1}: 이/가 · 마지막 글자 받침에 맞춤)")] [TextArea(2, 3)] public string BodyFormat = "{0}{1} 아직 할 일이 없습니다.\n숨죽이고 진행할까요?";
        [Tooltip("살아 있는 대원 모두 할 일이 없을 때")] [TextArea(2, 3)] public string BodyAll = "아무도 할 일이 없습니다.\n모두 숨죽이고 진행할까요?";
        [Tooltip("확인 버튼 · '턴 진행'과 똑같이 한 턴을 진행합니다")] public string ConfirmLabel = "숨죽이고 진행";
        [Tooltip("돌아가기 버튼 · 시간이 흐르지 않습니다")] public string BackLabel = "돌아가기";
        [Header("확인창 · '계속 진행'을 켤 때 (한 번만)")]
        [Tooltip("일부 대원만 할 일이 없을 때 ({0}: 대원 이름, {1}: 이/가)")] [TextArea(2, 4)] public string KeepGoingBodyFormat = "{0}{1} 아직 할 일이 없습니다.\n숨죽인 채 계속 진행할까요?\n멈출 때까지 다시 묻지 않습니다.";
        [Tooltip("살아 있는 대원 모두 할 일이 없을 때")] [TextArea(2, 4)] public string KeepGoingBodyAll = "아무도 할 일이 없습니다.\n모두 숨죽인 채 계속 진행할까요?\n멈출 때까지 다시 묻지 않습니다.";
        [Tooltip("확인 버튼 · '계속 진행'을 켭니다")] public string KeepGoingConfirmLabel = "숨죽이고 계속";
        [Header("이름")]
        [Tooltip("이름 사이")] public string NameSeparator = ", ";
        [Tooltip("할 일이 없는 대원이 이보다 많으면 줄여 씁니다")] [Min(1)] public int MaxNames = 3;
        [Tooltip("줄여 쓸 때 ({0}: 앞 이름들, {1}: 나머지 인원)")] public string MoreFormat = "{0} 외 {1}명";
        [Tooltip("받침 있는 글자 뒤 / 받침 없는 글자 뒤 / 한글이 아닐 때")] public string ParticleConsonant = "이", ParticleVowel = "가", ParticleOther = "이(가)";

        public enum Mode { None, Turn, KeepGoing }
        // The question open now (None when the popup shows something else).
        public Mode Asking { get; private set; }
        // Questions opened since the panel loaded (verify: '계속 진행' asks once, never per turn).
        public int Asked { get; private set; }
        // The members named by the last question (participant indices).
        public IReadOnlyList<int> LastIdle => lastIdle;

        static int passing;
        readonly List<int> lastIdle = new List<int>();
        Button wired; string askedTitle, backWas;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { AutoAccept = false; passing = 0; }

        // The hook in FieldTurnPlanner.Run ('턴 진행' only, after the reserved-move line): true = the question opened and this
        // press runs no turn.
        public static bool AskFirst(FieldTurnPlanner planner)
        {
            if (AutoAccept || passing > 0 || !planner) return false;
            var c = planner.GetComponent<FieldIdleConfirm>();
            return c && c.isActiveAndEnabled && c.Ask(planner, Mode.Turn);
        }
        // A press that must not ask (the accepted question, '계속 진행' turns).
        public static void Pass(Action press) { passing++; try { press(); } finally { passing--; } }

        // Living members with nothing to do this turn (the planner's check: they would hush), in member order.
        public static List<int> IdleMembers(FieldTurnPlanner planner) => IdleMembers(planner, out _);
        public static List<int> IdleMembers(FieldTurnPlanner planner, out int living)
        {
            var idle = new List<int>(); living = 0; if (!planner || !planner.Placing) return idle;
            var k = planner.Current ?? planner.Plan.Check(planner.Facts());
            for (int m = 0; m < k.Actions.Length; m++) { if (k.Actions[m] != FieldAction.Down) living++; if (k.Actions[m] == FieldAction.Hush) idle.Add(m); }
            return idle;
        }

        // '계속 진행' is being switched on: true = asked (it starts on '숨죽이고 계속', FieldAutoAdvance.StartAfterAsking).
        public bool AskKeepGoing(FieldTurnPlanner planner) => !AutoAccept && Ask(planner, Mode.KeepGoing);

        ExpeditionArrivalPanel Panel => Arrival ? Arrival : Arrival = GetComponent<ExpeditionArrivalPanel>();
        FieldTurnPlanner Planner { get { var a = Panel; return a && a.Threat && a.Threat.Planner ? a.Threat.Planner : GetComponent<FieldTurnPlanner>(); } }
        FieldAutoAdvance Toggle { get { if (!Auto && Panel && Panel.Main) Auto = Panel.Main.GetComponentInChildren<FieldAutoAdvance>(true); return Auto; } }

        bool Ask(FieldTurnPlanner planner, Mode mode)
        {
            var a = Panel; if (!a || !a.IsOpen || a.InTransit || !a.Popup || a.Popup.activeSelf || !planner || !planner.Placing) return false;
            if (a.Rooms && a.Rooms.HasQueuedMove) return false;
            if (mode == Mode.Turn && Toggle && Toggle.On) return false;
            var idle = IdleMembers(planner, out int living); if (idle.Count == 0) return false;
            if (a.Rooms) a.Rooms.CancelPending();
            a.OpenPopup(Title, BodyFor(idle, idle.Count >= living, mode == Mode.KeepGoing));
            if (!a.Popup.activeSelf) return false;
            if (wired != a.ReturnConfirm) { a.ReturnConfirm.onClick.AddListener(OnConfirm); wired = a.ReturnConfirm; }
            var ok = Label(a.ReturnConfirm); if (ok) ok.text = mode == Mode.KeepGoing ? KeepGoingConfirmLabel : ConfirmLabel;
            a.ReturnConfirm.gameObject.SetActive(true);
            var back = Label(a.PopupBack); backWas = null; if (back && back.text != BackLabel) { backWas = back.text; back.text = BackLabel; }
            Asking = mode; Asked++; askedTitle = a.PopupTitle.text; lastIdle.Clear(); lastIdle.AddRange(idle);
            return true;
        }

        public string BodyFor(IReadOnlyList<int> idle, bool all, bool keepGoing)
        {
            if (all) return keepGoing ? KeepGoingBodyAll : BodyAll;
            string names = NamesOf(idle);
            return string.Format(keepGoing ? KeepGoingBodyFormat : BodyFormat, names, Particle(names));
        }
        public string NamesOf(IReadOnlyList<int> idle)
        {
            var a = Panel; var names = idle.Where(m => a && m >= 0 && m < a.Participants.Count).Select(m => a.Participants[m].Name).ToList();
            int max = Mathf.Max(1, MaxNames);
            return names.Count > max ? string.Format(MoreFormat, string.Join(NameSeparator, names.Take(max)), names.Count - max) : string.Join(NameSeparator, names);
        }
        // 이 / 가 after the last syllable (a final consonant takes 이).
        public string Particle(string word)
        {
            char ch = string.IsNullOrEmpty(word) ? ' ' : word[word.Length - 1];
            if (ch < 0xAC00 || ch > 0xD7A3) return ParticleOther; // not a Hangul syllable (가..힣)
            return (ch - 0xAC00) % 28 != 0 ? ParticleConsonant : ParticleVowel;
        }

        // The popup's confirm (its other listeners do nothing for this question: no return prompt, no pending door).
        void OnConfirm()
        {
            var mode = Asking; if (mode == Mode.None) return;
            var a = Panel; bool mine = a && a.Popup.activeSelf && a.PopupTitle.text == askedTitle;
            Forget(); if (!mine) return;
            a.ClosePopup();
            if (mode == Mode.Turn) RunTurn(); else if (Toggle) Toggle.StartAfterAsking();
        }
        // '숨죽이고 진행': the same press as the '턴 진행' button, without asking again.
        public void RunTurn() { var pl = Planner; if (pl && pl.TurnButton) Pass(() => pl.TurnButton.onClick.Invoke()); }

        void Forget()
        {
            Asking = Mode.None;
            if (backWas == null) return;
            var back = Panel ? Label(Panel.PopupBack) : null; if (back && back.text == BackLabel) back.text = backWas; backWas = null;
        }
        // Back, Esc or another popup ends the question.
        void LateUpdate() { if (Asking != Mode.None) { var a = Panel; if (!a || !a.IsOpen || !a.Popup.activeSelf || a.PopupTitle.text != askedTitle) Forget(); } }
        void OnDisable() { if (Asking != Mode.None) Forget(); }
        static Text Label(Button b) => b ? b.GetComponentInChildren<Text>(true) : null;
    }
}
