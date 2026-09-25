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
 // The hover label of each object: its recorded search state; with members placed there (말 놓기 · FieldPawnBoard), who stands at it
 // and what this turn does to it ('윤서진 외 1 · 0→1/2', '… · 완료', '… · 멈춤'; one line of the 200 px label). Read from the plan's check only.
 public sealed class ExpeditionSearchStatus:MonoBehaviour
 {
  public ExpeditionArrivalPanel Arrival;
  public Text[] Labels;
  public Text[] UnknownMarks; public Image[] SiteIcons;
  [Header("말을 놓은 사물 (마우스를 올릴 때)")]
  [Tooltip("놓인 대원이 있을 때 한 줄 ({0}: 수색 기록, {1}: 누가, {2}: 이번 턴)")] public string PlacedFormat="{1} · {2}";
  [Tooltip("두 명 이상 ({0}: 담당, {1}: 나머지 인원)")] public string WhoMore="{0} 외 {1}";
  [Tooltip("이번 턴 진행 ({0}: 지금, {1}: 턴 뒤, {2}: 필요)")] public string TurnProgress="{0}→{1}/{2}";
  [Tooltip("이번 턴에 끝남 (이름표 한 줄 · 짧게)")] public string TurnDone="완료";
  [Tooltip("배정이 멈춤")] public string TurnPaused="멈춤";
  string lastSummary;int lastRoom=-1;
  void LateUpdate(){if(!Arrival||!Arrival.IsOpen){lastSummary=null;lastRoom=-1;return;}var check=PlacedCheck();for(int i=0;i<Labels.Length;i++){if(!Labels[i])continue;int kind=Arrival.Loot.StatusKind(i);string text=Arrival.Loot.StatusText(i),placed=Placed(check,i);if(placed!=null)text=placed;if(Labels[i].text!=text)Labels[i].text=text;if(UnknownMarks!=null&&i<UnknownMarks.Length&&UnknownMarks[i])UnknownMarks[i].gameObject.SetActive(kind==0);if(SiteIcons!=null&&i<SiteIcons.Length&&SiteIcons[i])SiteIcons[i].enabled=kind!=0;Labels[i].color=kind==2?new Color(1,.8f,.32f):kind==3?new Color(.7f,.73f,.72f):new Color(.96f,.94f,.85f);}
   if(!Arrival.Rooms)return;int room=Arrival.Rooms.CurrentRoom;string summary=Arrival.Loot.RoomSearchSummary(room);if(lastRoom==room&&lastSummary==summary)return;lastRoom=room;lastSummary=summary;Arrival.Rooms.RefreshSearchSummary();
  }
  // The plan's check while members are placed on this visit (null otherwise).
  FieldPlanCheck PlacedCheck(){var pl=Arrival.Threat?Arrival.Threat.Planner:null;if(!pl||!pl.Placing||!pl.Plan.HasAssignments)return null;var place=FieldPlacement.Of(Arrival);return place!=null?place.CheckNow():null;}
  string Placed(FieldPlanCheck k,int site){
   if(k==null)return null;var r=k.RunFor(site);string status=Arrival.Loot.StatusText(site);
   if(r!=null)return string.Format(PlacedFormat,status,Who(r.Lead,r.Support),r.Completes?TurnDone:string.Format(TurnProgress,r.Before,r.After,r.Required));
   for(int m=0;m<k.Actions.Length;m++)if(k.Actions[m]==FieldAction.Paused&&k.SiteOf[m]==site&&k.DoorOf[m]<0&&string.IsNullOrEmpty(k.ObserveOf[m]))return string.Format(PlacedFormat,status,Who(m,-1),TurnPaused);
   return null;
  }
  string Who(int lead,int helper){string Name(int m)=>m>=0&&m<Arrival.Participants.Count&&Arrival.Participants[m]!=null?Arrival.Participants[m].Name:"";return helper>=0?string.Format(WhoMore,Name(lead),1):Name(lead);}
 }
}
