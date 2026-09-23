using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class ExpeditionRoomNavigation:MonoBehaviour {
  public SpriteRenderer Background;
  public Sprite Arcade,Corridor;
  public GameObject CorridorHotspots;
  public Button CorridorBack,LockedDoor,OfficeDoor;
  public Text TurnLabel,RouteLabel;
  public float MoveDuration=1.5f,FadeDuration=.25f;
  public Vector3 ArcadeDoor=new Vector3(8.2f,-.3f,0),CorridorDoor=new Vector3(-7,-.2f,0);
  public int CurrentRoom{get;private set;}
  public int Noise{get;private set;}
  [Min(1)] public int MinutesPerTurn=10;
  public void SpendSearchTurn(int noise){Turns++;owner.SpendFieldTime(MinutesPerTurn);Noise=Mathf.Max(0,Noise+noise);RefreshLabels();}
  public int Turns{get;private set;}
  public bool CorridorVisited{get;private set;}
  public IReadOnlyCollection<int> Inspected=>inspected;
  readonly HashSet<int> inspected=new HashSet<int>();ExpeditionArrivalPanel owner;int pending=-1;
  public void Initialize(ExpeditionArrivalPanel panel){owner=panel;CorridorBack.onClick.AddListener(AskMove);LockedDoor.onClick.AddListener(()=>owner.OpenPopup("잠긴 철문","문이 잠겨 있습니다.\n안쪽은 보이지 않습니다."));OfficeDoor.onClick.AddListener(()=>owner.OpenPopup("닫힌 문","문틈으로는 안쪽을 확인하기 어렵습니다.\n지금은 들어갈 수 없습니다."));owner.ReturnConfirm.onClick.AddListener(ConfirmMove);}
  public void ResetVisit(){CurrentRoom=Turns=Noise=0;pending=-1;ApplyRoom();}
  public void MarkInspected(int index){inspected.Add(index);RefreshLabels();}
  public void CancelPending(){pending=-1;}
  public void AskMove(){
   if(!owner.IsOpen||owner.InTransit||owner.Popup.activeSelf||(owner.Encounter&&owner.Encounter.IsOpen))return;
   pending=1-CurrentRoom;
   owner.OpenPopup(pending==1?"복도로 이동할까요?":"오락실로 돌아갈까요?","원정대 전체 이동 · 1턴\n\n"+(pending==1&&!CorridorVisited?"아직 방문하지 않은 공간입니다.\n문 너머의 내부는 들어간 뒤 확인할 수 있습니다.":"이미 방문한 방입니다.\n챙긴 물건과 확인한 사물 정보는 유지됩니다."));
   owner.ReturnConfirm.gameObject.SetActive(true);owner.ReturnConfirm.GetComponentInChildren<Text>().text="이동 · 1턴";
  }
  public void ConfirmMove(){if(pending<0||!owner.IsOpen||owner.InTransit||!owner.Popup.activeSelf)return;int next=pending;pending=-1;owner.ClosePopup();owner.SetRoomTransit(true);Turns++;owner.SpendFieldTime(MinutesPerTurn);RefreshLabels();StartCoroutine(Travel(next));}
  IEnumerator Travel(int next){
   var pawns=new List<Transform>();var starts=new List<Vector3>();foreach(Transform p in owner.PawnRoot){pawns.Add(p);starts.Add(p.localPosition);}
   Vector3 door=CurrentRoom==0?ArcadeDoor:CorridorDoor;
   float t=0;while(t<MoveDuration){t+=Time.unscaledDeltaTime;float u=Mathf.Clamp01(t/MoveDuration);for(int i=0;i<pawns.Count;i++){float v=Mathf.SmoothStep(0,1,u);Vector3 via=new Vector3(door.x,-2.15f-i*.13f,0);Vector3 end=door+new Vector3((CurrentRoom==0?-1:1)*i*.55f,-i*.12f,0);pawns[i].localPosition=v<.72f?Vector3.Lerp(starts[i],via,v/.72f):Vector3.Lerp(via,end,(v-.72f)/.28f);}yield return null;}
   owner.Fade.gameObject.SetActive(true);owner.Fade.blocksRaycasts=true;t=0;while(t<FadeDuration){t+=Time.unscaledDeltaTime;owner.Fade.alpha=Mathf.Clamp01(t/FadeDuration);yield return null;}
   CurrentRoom=next;if(next==1)CorridorVisited=true;ApplyRoom();for(int i=0;i<pawns.Count;i++)pawns[i].localPosition=next==1?new Vector3(-5.6f+i*1.35f,-1.35f,0):new Vector3(5.8f-i*1.35f,-2.15f,0);
   t=0;while(t<FadeDuration){t+=Time.unscaledDeltaTime;owner.Fade.alpha=1-Mathf.Clamp01(t/FadeDuration);yield return null;}owner.Fade.gameObject.SetActive(false);owner.SetRoomTransit(false);
  }
  void ApplyRoom(){Background.sprite=CurrentRoom==0?Arcade:Corridor;Background.transform.localScale=new Vector3(19.2f/Background.sprite.bounds.size.x,10.8f/Background.sprite.bounds.size.y,1);foreach(var b in owner.Objects)b.gameObject.SetActive(CurrentRoom==0);CorridorHotspots.SetActive(CurrentRoom==1);owner.Return.interactable=CurrentRoom==0;owner.Place.text="폐상가\n1F · "+(CurrentRoom==0?"오락실":"복도");owner.ReturnConfirm.GetComponentInChildren<Text>().text="귀환";RefreshLabels();}
  void RefreshLabels(){TurnLabel.text="탐험 "+Turns+"턴";RouteLabel.text=CurrentRoom==0?"● 오락실  ─  "+(CorridorVisited?"복도":"복도 · 미방문"):"오락실  ─  ● 복도";owner.Status.text=CurrentRoom==0?(inspected.Count==0?"사물과 복도로 향하는 문을 살펴보세요.":"확인한 사물 "+inspected.Count+"곳 · 기록 유지"):"왼쪽 문으로 오락실에 돌아갈 수 있습니다.";}
 }
}


