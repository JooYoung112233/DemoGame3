namespace Demo5.FrontEnd {
 public sealed partial class ExpeditionLootPanel {
  // Site-board turns: read a search without creating it, and apply one planned run (the roll happens only on completion).
  public bool Peek(int index,out SearchState s)=>states.TryGetValue(index,out s);
  // The state change of one run. Refuses a run that does not start from the state it was planned on (no double apply).
  public static bool Apply(SearchState s,FieldRun r){if(s==null||r==null||s.Complete||s.Progress!=r.Before)return false;s.Opened=true;if(r.Starts){s.Pace=r.Pace;s.Duty=r.Duty;s.Required=r.Required;s.Bonus=r.Bonus;}else if(r.Forfeits)s.Bonus=0;s.Progress=r.After;if(r.Completes)s.Complete=true;return true;}
  public bool ApplyRun(FieldRun r){if(r==null||r.Site<0||r.Site>=Sites.Length)return false;var s=State(r.Site);if(!Apply(s,r))return false;if(r.Completes)Roll(r.Site,s);return true;}
 }
}
