using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
 public sealed partial class ExpeditionLootPanel
 {
  // Derived from recorded state only: viewing guidance never rolls rewards or opens a site.
  public int StatusKind(int index){if(!states.TryGetValue(index,out var s)||s.Progress==0)return 0;if(!s.Complete)return 1;return s.Loot.Values.Any(n=>n>0)?2:3;}
  public string StatusText(int index){int kind=StatusKind(index);if(kind==0)return "미수색";var s=states[index];return kind==1?"수색 중 "+s.Progress+" / "+s.Required:kind==2?"물품 남음 · "+s.Loot.Count(x=>x.Value>0)+"종":"비어 있음";}
  public string SearchSummary(){var counts=new int[4];for(int i=0;i<Sites.Length;i++)if(Sites[i].Room>=0)counts[StatusKind(i)]++;return "미수색 "+counts[0]+" · 진행 "+counts[1]+"\n물품 남음 "+counts[2]+" · 비어 있음 "+counts[3];}
 }
 public sealed class ExpeditionSearchStatus:MonoBehaviour
 {
  public ExpeditionArrivalPanel Arrival;
  public Text[] Labels;
  public Text[] UnknownMarks; public Image[] SiteIcons;
  void LateUpdate(){if(!Arrival||!Arrival.IsOpen)return;for(int i=0;i<Labels.Length;i++){if(!Labels[i])continue;int kind=Arrival.Loot.StatusKind(i);Labels[i].text=Arrival.Loot.StatusText(i);if(UnknownMarks!=null&&i<UnknownMarks.Length&&UnknownMarks[i])UnknownMarks[i].gameObject.SetActive(kind==0);if(SiteIcons!=null&&i<SiteIcons.Length&&SiteIcons[i])SiteIcons[i].enabled=kind!=0;Labels[i].color=kind==2?new Color(1,.8f,.32f):kind==3?new Color(.7f,.73f,.72f):new Color(.96f,.94f,.85f);}}
 }
}
