using UnityEngine;
namespace Demo5.FrontEnd {
 // Hide the underlying HUD, while keeping the world and its dim layer visible.
 public sealed class PopupBackgroundHud:MonoBehaviour {
  CanvasGroup hud;float previousAlpha;
  void OnEnable(){
   if(!Application.isPlaying)return;
   var arrival=GetComponentInParent<ExpeditionArrivalPanel>();
   var settlement=GetComponentInParent<SettlementController>();
   if(!settlement)settlement=FindAnyObjectByType<SettlementController>();
   hud=arrival?arrival.Main:settlement?settlement.Main:null;
   if(!hud||transform.IsChildOf(hud.transform)){hud=null;return;}
   previousAlpha=hud.alpha;hud.alpha=0;
  }
  void OnDisable(){if(hud){hud.alpha=previousAlpha;hud=null;}}
 }
}
