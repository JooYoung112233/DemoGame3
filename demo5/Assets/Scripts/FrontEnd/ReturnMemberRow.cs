using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class ReturnMemberRow:MonoBehaviour {
  public Image Portrait,HealthFill;
  public Text Name,Health,Change;
  public void Bind(Sprite portrait,string name,int before,int after,int maximum){
   Portrait.sprite=portrait;Name.text=name;Health.text=before+" → "+after+" / "+maximum;
   int delta=after-before;Change.text=delta==0?"변화 없음":delta>0?"회복 +"+delta:"체력 "+delta;
   Change.color=delta<0?new Color(.58f,.15f,.11f):delta>0?new Color(.16f,.36f,.22f):new Color(.32f,.34f,.29f);
   HealthFill.fillAmount=(float)after/Mathf.Max(1,maximum);
  }
 }
}
