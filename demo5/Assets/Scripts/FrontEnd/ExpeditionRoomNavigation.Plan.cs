using UnityEngine;
namespace Demo5.FrontEnd {
 public sealed partial class ExpeditionRoomNavigation {
  // One party turn with the room's summed noise. hushed: nobody searched this turn (pass-by condition on the site board).
  public void SpendTurn(int noise,bool hushed){Turns++;owner.SpendFieldTime(MinutesPerTurn);Noise=Mathf.Max(0,Noise+noise);RefreshLabels();if(owner.Threat)owner.Threat.EndTurn(Mathf.Max(0,noise),hushed);}
  // The door a move popup is asking about (-1 when none): the listen button in the popup uses it.
  public int PendingRoom=>pending;
  // Moving clears the member assignments (they belong to the room's objects); a door listened at this turn adds what was heard.
  string PlanSuffix{get{var p=owner&&owner.Threat?owner.Threat.Planner:null;if(!p||!p.Active)return "";var heard=p.HeardLine(pending);return (p.Plan.HasAssignments?p.Texts.MoveSuffix:"")+(heard.Length>0?heard:owner.Threat.IncomingLine(pending));}}
 }
}
