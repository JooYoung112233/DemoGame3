using System.Linq;
using Demo5.NightRun;
using UnityEngine;

namespace Demo5.FrontEnd
{
    // 수색 쪽지 (FieldSearchNote · 기획/탐험-수색쪽지와-협동-1차.md): the first visit's '수색 · 1턴' and '자세히 >' on the note.
    public sealed partial class ExpeditionSearchPanel
    {
        // The first visit's confirm (Save) for the note's choice, without this window: one turn (Loot.Advance: time, noise, the
        // site's turn), the object's remembered assignment, the encounter roll, then the finds or the status line. Before it, what
        // Refresh does to the window's choice: a started search keeps its own pace and role, a role the window would not offer
        // becomes 함께 수색. Keep in step with Save (ExpeditionSearchPanel.cs); VerifySearchNote.FirstVisit compares the two.
        public bool RunFromNote(int index, Adventurer worker, int pace, int duty)
        {
            if (Board || IsOpen || !arrival || !arrival.Loot || worker == null || worker.Health <= 0) return false;
            var s = arrival.Loot.State(index);
            if (s.Progress > 0) { pace = s.Pace; duty = s.Duty; }
            else if (!arrival.Loot.CanSupport(duty, worker) || duty == 1 && !arrival.Loot.CanOfferWatch(index)) duty = 0; // 망보기: a noisy object and a watcher
            if (!arrival.Loot.Advance(index, pace, worker, duty)) return false;
            Assignments[index] = new Assignment { ObjectIndex = index, Worker = worker, Pace = pace, Duty = duty };
            if (arrival.Encounter && arrival.Encounter.AfterSearch(index)) return true;
            if (arrival.Loot.State(index).Complete) arrival.Loot.Open(index);
            else arrival.Status.text = arrival.ObjectNames[index] + " · 수색 중\n진행도 유지 · 다음 턴 대기";
            return true;
        }

        // '자세히 >' on the first visit: the window Inspect opened starts from the note's lead, pace and role
        // (the site board's window reads the standing order by itself).
        public void TakeNoteChoice(Adventurer worker, int pace, int duty)
        {
            if (!IsOpen || Board || worker == null || worker.Health <= 0 || !arrival.Participants.Contains(worker)) return;
            Worker = worker;
            if (arrival.Loot.State(site).Progress == 0) { Pace = Mathf.Clamp(pace, 0, 2); Duty = Mathf.Clamp(duty, 0, 2); }
            Refresh();
        }
    }
}
