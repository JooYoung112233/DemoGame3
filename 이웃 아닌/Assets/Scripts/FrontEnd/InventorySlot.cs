using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class InventorySlot:MonoBehaviour {
  public Button Button; public Image Paper,Icon; public Text Count,Label; public Outline Selection;
  public void Bind(Sprite sprite,string label,int count,bool selected){Icon.sprite=sprite;Icon.enabled=sprite;Label.text=label;Count.text=count>0?count.ToString():"";Selection.enabled=selected;}
 }
}
