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
  bool HasSiteRecord(int index)=>arrival&&arrival.Rooms&&arrival.Rooms.Inspected.Contains(index)||states.TryGetValue(index,out var s)&&(s.Progress>0||s.Opened||s.Complete);
  bool KnowsRoom(int room){
   if(!arrival||!arrival.Rooms)return false;
   var rooms=arrival.Rooms;
   if(arrival.IsOpen&&rooms.CurrentRoom==room)return true;
   for(int i=0;i<Sites.Length;i++)if(Sites[i].Room==room&&HasSiteRecord(i))return true;
   if(room==0)return arrival.IsOpen||(arrival.Threat&&arrival.Threat.Visited)||rooms.CorridorVisited||rooms.StorageVisited||rooms.Inspected.Count>0;
   if(room==1)return rooms.CorridorVisited||rooms.StorageVisited;
   return room==2&&rooms.StorageVisited;
  }
  // The office shelf is behind its door: seeing the corridor alone does not reveal it.
  public bool IsKnownSite(int index)=>Sites!=null&&index>=0&&index<Sites.Length&&Sites[index].Room>=0&&(arrival&&arrival.Threat&&index==arrival.Threat.DenSite?HasSiteRecord(index):KnowsRoom(Sites[index].Room));
  static string Summary(int[] counts)=>"미수색 "+counts[0]+" · 진행 "+counts[1]+"\n물품 남음 "+counts[2]+" · 비어 있음 "+counts[3];
  public string SearchSummary(){var counts=new int[4];int known=0;for(int i=0;Sites!=null&&i<Sites.Length;i++)if(IsKnownSite(i)){counts[StatusKind(i)]++;known++;}return known==0?"아직 수색 기록이 없습니다.\n방문한 구역부터 표시됩니다.":Summary(counts);}
  public string RoomSearchSummary(int room){var counts=new int[4];for(int i=0;Sites!=null&&i<Sites.Length;i++)if(Sites[i].Room==room&&IsKnownSite(i))counts[StatusKind(i)]++;return Summary(counts);}
 }
 public sealed class ExpeditionSearchStatus:MonoBehaviour
 {
  public ExpeditionArrivalPanel Arrival;
  public Text[] Labels;
  public Text[] UnknownMarks; public Image[] SiteIcons;
  string lastSummary;int lastRoom=-1;
  void LateUpdate(){if(!Arrival||!Arrival.IsOpen){lastSummary=null;lastRoom=-1;return;}for(int i=0;i<Labels.Length;i++){if(!Labels[i])continue;int kind=Arrival.Loot.StatusKind(i);Labels[i].text=Arrival.Loot.StatusText(i);if(UnknownMarks!=null&&i<UnknownMarks.Length&&UnknownMarks[i])UnknownMarks[i].gameObject.SetActive(kind==0);if(SiteIcons!=null&&i<SiteIcons.Length&&SiteIcons[i])SiteIcons[i].enabled=kind!=0;Labels[i].color=kind==2?new Color(1,.8f,.32f):kind==3?new Color(.7f,.73f,.72f):new Color(.96f,.94f,.85f);}
   if(!Arrival.Rooms)return;int room=Arrival.Rooms.CurrentRoom;string summary=Arrival.Loot.RoomSearchSummary(room);if(lastRoom==room&&lastSummary==summary)return;lastRoom=room;lastSummary=summary;Arrival.Rooms.RefreshSearchSummary();
  }
 }
}
