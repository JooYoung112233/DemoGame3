using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed partial class ExpeditionRoomNavigation {
  public Vector3 StorageDoor=new Vector3(2.2f,.2f,0),StorageExit=new Vector3(-7,-.2f,0);
  [Min(1)] public int UnlockTurns=1;
  [Min(0)] public int UnlockNoise=2;
  public string UnlockTool="prybar";
  public Text StorageDoorStatus;
  void LateUpdate(){if(StorageDoorStatus&&owner&&owner.IsOpen){StorageDoorStatus.text=StorageUnlocked?"개방됨 · 보관실":"잠김 · 지렛대 필요";StorageDoorStatus.color=StorageUnlocked?new Color(1,.8f,.32f):new Color(.96f,.94f,.85f);}}
  public bool StorageUnlocked{get;private set;}
  public bool StorageVisited{get;private set;}
  public string RetreatRoomName=>CurrentRoom==1?"오락실":"복도";
  Demo5.NightRun.Adventurer UnlockWorker()=>owner.Participants.FirstOrDefault(p=>p.Health>0&&owner.Inventory.CountFor(p,UnlockTool)>0);
  public void AskStorage(){
   if(!owner.IsOpen||owner.InTransit||owner.Popup.activeSelf||owner.Search.IsOpen||owner.Loot.IsOpen||(owner.FieldBags&&owner.FieldBags.IsOpen)||(owner.Encounter&&owner.Encounter.IsOpen)||CurrentRoom!=1||!Storage)return;
   var worker=UnlockWorker();var item=owner.Inventory.Items.FirstOrDefault(i=>i.Id==UnlockTool);
   if(!StorageUnlocked&&worker==null){owner.OpenPopup("잠긴 철문",(item?.Name??UnlockTool)+"를 가진 대원이 필요합니다.\n\n도구는 동행 대원의 가방에 넣어야 합니다.\n문을 열면 보관실로 이동할 수 있습니다.");return;}
   pending=2;
   owner.OpenPopup(StorageUnlocked?"보관실로 이동할까요?":"철문을 열고 들어갈까요?",StorageUnlocked?"원정대 전체 이동 · 1턴 · "+MinutesPerTurn+"분\n\n잠금장치는 이미 열려 있습니다.\n챙긴 물건과 수색 진행은 유지됩니다.":worker.Name+" · "+(item?.Name??UnlockTool)+" 휴대\n\n잠금 해제 "+UnlockTurns+"턴 + 이동 1턴 · "+((UnlockTurns+1)*MinutesPerTurn)+"분\n소음 +"+UnlockNoise+" · 도구 소모 없음\n\n열린 문은 다음 원정에도 유지됩니다.");
   owner.ReturnConfirm.gameObject.SetActive(true);owner.ReturnConfirm.GetComponentInChildren<Text>().text=StorageUnlocked?"이동 · 1턴":"개방 후 이동";
  }
 }
}
