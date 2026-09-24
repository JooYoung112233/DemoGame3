using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class ExpeditionMemberCard:MonoBehaviour {
  public Button Button; public Image Paper,Portrait,HealthFill,Check; public Text Name,Health,State,Role;
  [Tooltip("행동 이름표 · 탐험 방 화면 카드와 수색 담당 카드만 사용")] public Text Action; public GameObject ActionRoot;
  public void SetAction(string text){var root=ActionRoot?ActionRoot:Action?Action.gameObject:null;if(!root)return;bool on=!string.IsNullOrEmpty(text);if(root.activeSelf!=on)root.SetActive(on);if(on&&Action)Action.text=text;}
 }
}
