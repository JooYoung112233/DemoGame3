using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed partial class ExpeditionRoomNavigation:MonoBehaviour {
  public SpriteRenderer Background;
  public Sprite Arcade,Corridor,Storage;
  public GameObject CorridorHotspots,StorageHotspots;
  public Button CorridorBack,LockedDoor,OfficeDoor,StorageBack;
  public Text TurnLabel,RouteLabel;
  public float MoveDuration=1.5f,FadeDuration=.25f;
  public Vector3 ArcadeDoor=new Vector3(8.2f,-.3f,0),CorridorDoor=new Vector3(-7,-.2f,0);
  public int CurrentRoom{get;private set;}
  public int Noise{get;private set;}
  [Min(1)] public int MinutesPerTurn=10;
  public void SpendSearchTurn(int noise)=>SpendTurn(noise,false);
  public void AddNoise(int noise){Noise=Mathf.Max(0,Noise+noise);RefreshLabels();}
  public int Turns{get;private set;}
  public bool CorridorVisited{get;private set;}
  public IReadOnlyCollection<int> Inspected=>inspected;
  readonly HashSet<int> inspected=new HashSet<int>();ExpeditionArrivalPanel owner;int pending=-1;
  public void Initialize(ExpeditionArrivalPanel panel){owner=panel;CorridorBack.onClick.AddListener(AskMove);LockedDoor.onClick.AddListener(AskStorage);if(StorageBack)StorageBack.onClick.AddListener(AskMove);OfficeDoor.onClick.AddListener(()=>{if(owner.Threat&&owner.Threat.OpenDen())return;owner.OpenPopup("닫힌 문","문틈으로는 안쪽을 확인하기 어렵습니다.\n지금은 들어갈 수 없습니다.");});owner.ReturnConfirm.onClick.AddListener(ConfirmMove);}
  public void ResetVisit(){CurrentRoom=Turns=Noise=0;pending=-1;ApplyRoom();}
  public void MarkInspected(int index){inspected.Add(index);RefreshLabels();}
  public void CancelPending(){pending=-1;}
  public void AskMove(){
   if(!owner.IsOpen||owner.InTransit||owner.Popup.activeSelf||owner.Search.IsOpen||owner.Loot.IsOpen||(owner.FieldBags&&owner.FieldBags.IsOpen)||(owner.Encounter&&owner.Encounter.IsOpen))return;
   pending=CurrentRoom==1?0:1;
   string title=CurrentRoom==2?"복도로 돌아갈까요?":pending==1?"복도로 이동할까요?":"오락실로 돌아갈까요?";
   owner.OpenPopup(title,"원정대 전체 이동 · 1턴 · "+MinutesPerTurn+"분\n소음 없음"+PlanSuffix+"\n\n"+(pending==1&&!CorridorVisited?"미방문 구역 · 내부는 들어간 뒤 확인합니다.":"방문한 방 · 물품과 수색 기록이 유지됩니다."));
   owner.ReturnConfirm.gameObject.SetActive(true);owner.ReturnConfirm.GetComponentInChildren<Text>().text="이동 · 1턴";
  }
  public void ConfirmMove(){if(QueueInstead())return;if(pending<0||!owner.IsOpen||owner.InTransit||!owner.Popup.activeSelf)return;int next=pending;bool unlock=next==2&&!StorageUnlocked;if(next==2&&(CurrentRoom!=1||!Storage)||unlock&&UnlockWorker()==null){owner.ClosePopup();return;}int cost=1+(unlock?UnlockTurns:0);pending=-1;owner.ClosePopup();owner.SetRoomTransit(true);if(unlock){StorageUnlocked=true;Noise+=UnlockNoise;}Turns+=cost;owner.SpendFieldTime(cost*MinutesPerTurn);RefreshLabels();StartCoroutine(Travel(next,cost,unlock?UnlockNoise:0));}
  IEnumerator Travel(int next,int turns=1,int noise=0){
   int from=CurrentRoom;var presentation=owner.World?owner.World.GetComponent<ExplorationRoomPresentation>():null;var route=presentation?presentation.Route(from,next):null;
   var pawns=new List<Transform>();var starts=new List<Vector3>();foreach(var member in owner.PartyPawns){if(!member)continue;var p=member.transform;pawns.Add(p);starts.Add(p.localPosition);}
   Vector3 door=CurrentRoom==0?ArcadeDoor:CurrentRoom==2?StorageExit:next==2?StorageDoor:CorridorDoor;
   float duration=Mathf.Max(.01f,MoveDuration);float t=0;while(t<duration){t+=Time.unscaledDeltaTime;float u=Mathf.Clamp01(t/duration);for(int i=0;i<pawns.Count;i++){if(route!=null){pawns[i].localPosition=ExplorationRoomPresentation.TravelPoint(route,starts[i],i,u);continue;}float v=Mathf.SmoothStep(0,1,u);Vector3 via=new Vector3(door.x,-2.15f-i*.13f,0);Vector3 end=door+new Vector3((door.x>0?-1:1)*i*.55f,-i*.12f,0);pawns[i].localPosition=v<.72f?Vector3.Lerp(starts[i],via,v/.72f):Vector3.Lerp(via,end,(v-.72f)/.28f);}yield return null;}
   owner.Fade.gameObject.SetActive(true);owner.Fade.blocksRaycasts=true;t=0;while(t<FadeDuration){t+=Time.unscaledDeltaTime;owner.Fade.alpha=Mathf.Clamp01(t/FadeDuration);yield return null;}
   CurrentRoom=next;if(next==1)CorridorVisited=true;if(next==2)StorageVisited=true;ApplyRoom();for(int i=0;i<pawns.Count;i++){pawns[i].localPosition=presentation?presentation.ArrivalPosition(from,next,i,pawns.Count):next!=0?new Vector3(-5.6f+i*1.35f,-1.35f,0):new Vector3(5.8f-i*1.35f,-2.15f,0);if(presentation)presentation.FaceArrival(pawns[i].gameObject,from,next);else{var facing=pawns[i].GetComponent<PawnFacing>();if(facing)facing.ResetTracking();}}
   t=0;while(t<FadeDuration){t+=Time.unscaledDeltaTime;owner.Fade.alpha=1-Mathf.Clamp01(t/FadeDuration);yield return null;}owner.Fade.gameObject.SetActive(false);owner.SetRoomTransit(false);
   if(owner.Threat)owner.Threat.PartyArrived(next,turns,noise);
  }
  void ApplyRoom(){Background.sprite=CurrentRoom==0?Arcade:CurrentRoom==1?Corridor:Storage;Background.transform.localScale=new Vector3(19.2f/Background.sprite.bounds.size.x,10.8f/Background.sprite.bounds.size.y,1);var presentation=owner.World?owner.World.GetComponent<ExplorationRoomPresentation>():null;if(presentation)presentation.ApplyRoom(CurrentRoom);for(int i=0;i<owner.Objects.Length;i++)owner.Objects[i].gameObject.SetActive(i==3?CurrentRoom==0:owner.Loot.IsSiteInCurrentRoom(i));CorridorHotspots.SetActive(CurrentRoom==1);if(StorageHotspots)StorageHotspots.SetActive(CurrentRoom==2);LockedDoor.transform.Find("Caption/Text").GetComponent<Text>().text=StorageUnlocked?"보관실로":"잠긴 철문";owner.Return.interactable=CurrentRoom==0;owner.Place.text="폐상가\n1F · "+(CurrentRoom==0?"오락실":CurrentRoom==1?"복도":"보관실");owner.ReturnConfirm.GetComponentInChildren<Text>().text="귀환";RefreshLabels();}
  void RefreshLabels(){TurnLabel.text="탐험 "+Turns+"턴";RouteLabel.text=(CurrentRoom==0?"● ":"")+"오락실 ─ "+(CurrentRoom==1?"● ":"")+"복도"+(!CorridorVisited&&CurrentRoom==0?" · 미방문":"")+(StorageVisited||StorageUnlocked||CurrentRoom==2?" ─ "+(CurrentRoom==2?"● ":"")+"보관실":"");RefreshSearchSummary();}
  public void RefreshSearchSummary(){owner.Status.text=owner.Loot.RoomSearchSummary(CurrentRoom);if(owner.Threat)owner.Threat.Refresh();}
 }
}


