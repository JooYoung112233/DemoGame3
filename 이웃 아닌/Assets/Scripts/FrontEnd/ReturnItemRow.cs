using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class ReturnItemRow:MonoBehaviour {
  public Image Icon;public Text Name,Before,After,Change;
  public void Bind(Sprite icon,string name,int before,int after){
   Icon.sprite=icon;Name.text=name;Before.text=before.ToString();After.text=after.ToString();int delta=after-before;
   Change.text=delta>0?"+"+delta:delta<0?delta.ToString():"—";
   Change.color=delta<0?new Color(.58f,.15f,.11f):delta>0?new Color(.16f,.36f,.22f):new Color(.32f,.34f,.29f);
  }
 }
}
