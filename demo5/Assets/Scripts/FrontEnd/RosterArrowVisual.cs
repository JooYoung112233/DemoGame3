using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class RosterArrowVisual:MonoBehaviour {
  public Button Button;public CanvasGroup Group;
  void OnEnable(){Refresh();}
  void LateUpdate(){Refresh();}
  void Refresh(){if(Button&&Group)Group.alpha=Button.interactable?1f:.28f;}
 }
}
