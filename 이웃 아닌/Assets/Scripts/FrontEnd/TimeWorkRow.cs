using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class TimeWorkRow:MonoBehaviour {
  public Image Icon;public Text Title,Detail,Result;
  public void Bind(Sprite icon,string title,string detail,string result,bool complete){Icon.sprite=icon;Title.text=title;Detail.text=detail;Result.text=result;Result.color=complete?new Color(.16f,.36f,.22f):new Color(.31f,.29f,.23f);}
 }
}
