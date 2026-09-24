namespace Demo5.FrontEnd
{
    // Which warning the status paper shows now (same order as StatusLine). Other: a line this list does not know yet.
    public enum FieldAutoStop { None, PassedBy, Visible, Incoming, Hunt, HeardBehindDoor, Footsteps, DenReturning, DenEmpty, DenGone, Noted, DangerRose, Linger, Overtime, Other }

    // Read-only accessors for '계속 진행' (FieldAutoAdvance): it stops by itself when the status paper warns.
    public sealed partial class ExpeditionSiteThreat
    {
        // The warning line the status paper shows now (null when none, or off the board).
        public string AutoStopLine => Active ? StatusLine() : null;
        public FieldAutoStop AutoStopKind() => AutoStopKind(out _);
        // The kind with the exact line it names (FieldAutoAdvance lets a standing line pass only while its text is unchanged).
        public FieldAutoStop AutoStopKind(out string line)
        {
            line = AutoStopLine;
            if (string.IsNullOrEmpty(line)) return FieldAutoStop.None;
            var s = State;
            if (s.PassedBy) return FieldAutoStop.PassedBy;
            if (s.Visible) return FieldAutoStop.Visible;
            if (s.Incoming) return FieldAutoStop.Incoming;
            if (dangerRose && s.Danger >= 3) return FieldAutoStop.Hunt;
            if (Planner && Planner.HeardPresent(out _, out _)) return FieldAutoStop.HeardBehindDoor;
            if (s.Heard && s.ResidentRoom >= 0 && s.ResidentRoom < FieldSiteState.RoomNames.Length) return FieldAutoStop.Footsteps;
            if (s.PartyRoom == FieldSiteState.Corridor && s.DenEmpty && !s.DenStaysEmpty) return FieldAutoStop.DenReturning;
            if (s.PartyRoom == FieldSiteState.Corridor && s.DenEmpty && s.Resident != ResidentState.Gone) return FieldAutoStop.DenEmpty;
            if (s.PartyRoom == FieldSiteState.Corridor && s.Resident == ResidentState.Gone) return FieldAutoStop.DenGone;
            if (s.LastNoise >= Rules.LoudNoise && s.Remembered == s.PartyRoom) return FieldAutoStop.Noted;
            if (dangerRose) return FieldAutoStop.DangerRose;
            if (s.NextLinger == 1 && s.Preview(0, false).danger > s.Danger) return FieldAutoStop.Linger;
            if (s.TurnsUsed > Rules.TurnBudget) return FieldAutoStop.Overtime;
            return FieldAutoStop.Other;
        }
    }
}
