using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace Demo5.FrontEnd {
 public sealed class ExpeditionArrivalPanel:MonoBehaviour {
  public ExpeditionBagPanel FieldBags; public ExpeditionEncounterPanel Encounter; public ExpeditionLootPanel Loot; public SettlementInventoryPanel Inventory=>owner.InventoryPanel; public ExpeditionRoomNavigation Rooms; public ExpeditionSearchPanel Search; public ExpeditionSiteThreat Threat;
  public GameObject View,World,SettlementWorld,Popup; public CanvasGroup Main,Fade;
  public Text Clock,Place,Resources,Status,PopupTitle,PopupBody;
  public Button Return,PopupBack,ReturnConfirm;public Button[] Objects;
  public string[] ObjectNames,ObjectDescriptions;
  public RectTransform MemberContent;public ExpeditionMemberCard MemberPrefab;
  public Transform PawnRoot;public GameObject PawnPrefab;public Sprite Scout,Medic;
  public readonly List<ExpeditionMemberCard> Cards=new List<ExpeditionMemberCard>();
  readonly List<GameObject> pawns=new List<GameObject>();
  // World children can also include a resident or a revealed NPC; only this list belongs to the expedition.
  public IReadOnlyList<GameObject> PartyPawns=>pawns;
  public ExpeditionNpcStory Story=>GetComponent<ExpeditionNpcStory>();
  public MissingPersonStory MissingPerson=>owner?owner.MissingPerson:null;
  public bool TutorialSkipped=>owner&&owner.TutorialSkipped;
  public bool IsOpen=>View.activeSelf; public bool InTransit{get;private set;}
  public IReadOnlyList<Adventurer> Participants=>people;
  Adventurer[] people=Array.Empty<Adventurer>();ExpeditionPlanPanel.Destination destination;SettlementController owner;bool returnPrompt;
  public void Initialize(SettlementController c){owner=c;View.SetActive(false);World.SetActive(false);Popup.SetActive(false);Return.onClick.AddListener(AskReturn);PopupBack.onClick.AddListener(ClosePopup);ReturnConfirm.onClick.AddListener(ConfirmReturn);if(Rooms)Rooms.Initialize(this);if(Search)Search.Initialize(this);if(Loot)Loot.Initialize(this);if(Encounter)Encounter.Initialize(this);if(FieldBags)FieldBags.Initialize(this);if(Threat)Threat.Initialize(this);if(Story)Story.Initialize(c);for(int i=0;i<Objects.Length;i++){int k=i;Objects[i].onClick.AddListener(()=>Press(k));}}
  public bool Begin(Adventurer[] party,ExpeditionPlanPanel.Destination target){
   if(IsOpen||InTransit||target==null||target.Id!="mall"||party==null||party.Any(p=>owner.IsAssigned(p)||owner.InventoryPanel.SlotsFor(p)>p.BagCapacity))return false;
   int started=(owner.Campaign.Day-1)*1440+owner.Campaign.MinuteOfDay;
   // First visit to the site: whatever lives here is still asleep (the introduction never meets it).
   bool firstVisit=Rooms&&!Rooms.CorridorVisited&&Rooms.Inspected.Count==0&&!(Threat&&Threat.Visited);
   if(!owner.Campaign.BeginFieldExpedition(target.Id,party,target.OneWayMinutes))return false;
   if(owner.ReturnPanel)owner.ReturnPanel.Begin(party,target.Name,started);
   people=party.ToArray();destination=target;owner.Main.gameObject.SetActive(false);SettlementWorld.SetActive(false);World.SetActive(true);View.SetActive(true);Popup.SetActive(false);Main.interactable=Main.blocksRaycasts=false;
   foreach(var card in Cards){card.gameObject.SetActive(false);Destroy(card.gameObject);}Cards.Clear();foreach(var pawn in pawns)Destroy(pawn);pawns.Clear();
   for(int i=0;i<people.Length;i++){int index=i;var p=people[i];int original=Array.IndexOf(owner.Campaign.Party.ToArray(),p);string id=PartySelectionSession.Selected.ElementAtOrDefault(original);var data=owner.Roster.Candidates.FirstOrDefault(x=>x.Id==id);var card=Instantiate(MemberPrefab,MemberContent);card.Name.text=p.Name;card.Portrait.sprite=data?.Portrait;card.Role.text="현장 가방";card.Health.text=p.Health+" / "+p.MaxHealth;SegmentedHealthGraphic.Set(card.HealthFill,p.Health,p.MaxHealth);card.State.text="가방 "+owner.InventoryPanel.SlotsFor(p)+" / "+p.BagCapacity;card.Check.gameObject.SetActive(false);card.Button.onClick.AddListener(()=>ShowBag(index));Cards.Add(card);
    var pawn=Instantiate(PawnPrefab,PawnRoot);var roomPresentation=World?World.GetComponent<ExplorationRoomPresentation>():null;pawn.transform.localPosition=roomPresentation?roomPresentation.ArrivalPosition(-1,0,i,people.Length):new Vector3((i-(people.Length-1)*.5f)*1.5f,-1.35f,0);owner.Roster.ApplyBody(pawn.transform.Find("Body").GetComponent<SpriteRenderer>(),id,Scout,Medic);pawns.Add(pawn);if(roomPresentation)roomPresentation.FaceArrival(pawn,-1,0);
   }
   Clock.text=owner.Campaign.ClockText;Place.text=target.Name+"\n1F · 오락실";Resources.text="탄약 "+people.Sum(p=>owner.InventoryPanel.CountFor(p,"ammo"))+"  ·  붕대 "+people.Sum(p=>owner.InventoryPanel.CountFor(p,"bandage"));Status.text="주변의 사물을 눌러 살펴보세요.";if(Rooms)Rooms.ResetVisit();if(Search)Search.ResetVisit();if(Encounter)Encounter.ResetVisit();if(Threat)Threat.Begin(firstVisit);if(Story)Story.BeginVisit(firstVisit);StartCoroutine(Arrive());return true;
  }
  IEnumerator Arrive(){InTransit=true;Fade.gameObject.SetActive(true);Fade.alpha=1;Fade.blocksRaycasts=true;float t=0;while(t<.7f){t+=Time.unscaledDeltaTime;Fade.alpha=1-Mathf.Clamp01(t/.7f);yield return null;}Fade.gameObject.SetActive(false);InTransit=false;Main.interactable=Main.blocksRaycasts=true;}
  public void OpenPopup(string title,string body,bool returning=false){if(!IsOpen||InTransit||(Encounter&&Encounter.IsOpen))return;returnPrompt=returning;ReturnConfirm.GetComponentInChildren<Text>().text=returning?"귀환":"확인";PopupTitle.text=title;PopupBody.text=body;ReturnConfirm.gameObject.SetActive(returning);Popup.SetActive(true);Main.interactable=Main.blocksRaycasts=false;}
  // 수색 쪽지 (FieldSearchNote · 기획/탐험-수색쪽지와-협동-1차.md): an object press asks Pressed first (the note beside the object);
  // Inspect itself still opens the 07 window (the note's '자세히 >', the den door when no note takes it, the verify scripts).
  public Func<int,bool> Pressed; public void Press(int index){if(Pressed!=null&&Pressed(index))return;Inspect(index);}
  public void Inspect(int index){if(!IsOpen||InTransit||(Encounter&&Encounter.IsOpen)||(Loot&&Loot.IsOpen)||(Search&&Search.IsOpen)||Popup.activeSelf||index<0||index>=ObjectNames.Length)return;if(Rooms&&index==3){if(Rooms.CurrentRoom==0)Rooms.AskMove();return;}if(!Loot.IsSiteInCurrentRoom(index))return;if(Rooms)Rooms.MarkInspected(index);if(Loot&&Loot.State(index).Complete){Loot.Open(index);return;}if(Search){Search.Open(index);return;}OpenPopup(ObjectNames[index],ObjectDescriptions[index]);}
  public void SpendFieldTime(int minutes){if(owner.Campaign.AdvanceFieldTime(minutes))Clock.text=owner.Campaign.ClockText;}
  public void RefreshFieldBags(){for(int i=0;i<Cards.Count;i++){Cards[i].State.text="가방 "+Inventory.SlotsFor(people[i])+" / "+people[i].BagCapacity;Cards[i].Health.text=people[i].Health+" / "+people[i].MaxHealth;SegmentedHealthGraphic.Set(Cards[i].HealthFill,people[i].Health,people[i].MaxHealth);}Resources.text="탄약 "+people.Sum(p=>Inventory.CountFor(p,"ammo"))+"  ·  붕대 "+people.Sum(p=>Inventory.CountFor(p,"bandage"));if(Threat&&Threat.Planner)Threat.Planner.Refresh();}
  void ShowBag(int index){if(FieldBags){FieldBags.Open(index);return;}var p=people[index];var items=owner.InventoryPanel.Items.Where(i=>owner.InventoryPanel.CountFor(p,i.Id)>0).ToArray();OpenPopup(p.Name+" · 현장 가방",(items.Length==0?"챙긴 물건이 없습니다.":string.Join("\n",items.Select(i=>i.Name+"  "+owner.InventoryPanel.CountFor(p,i.Id))))+"\n\n사용 공간  "+owner.InventoryPanel.SlotsFor(p)+" / "+p.BagCapacity+"\n공용 창고는 거점에서 이용할 수 있습니다.");}
  void AskReturn(){if(Rooms&&Rooms.CurrentRoom!=0){OpenPopup("출구로 돌아가야 합니다.","오락실의 출입구에서 거점으로 돌아갈 수 있습니다.");return;}OpenPopup("거점으로 돌아갈까요?",owner.Campaign.Home.Name+"까지 "+destination.OneWayMinutes+"분\n\n동행 대원과 챙긴 물건을 그대로 가져갑니다.",true);}
  void ConfirmReturn(){if(!IsOpen||InTransit||!Popup.activeSelf||!returnPrompt)return;returnPrompt=false;FinishReturn();}
  // Leave the site for home (the return popup, or retreating out of the arcade exit from an encounter or a battle).
  public bool FinishReturn(){if(!IsOpen||!owner.Campaign.EndFieldExpedition(destination.OneWayMinutes))return false;if(Story)Story.OnReturn();if(Threat)Threat.End();Popup.SetActive(false);World.SetActive(false);View.SetActive(false);SettlementWorld.SetActive(true);owner.Main.gameObject.SetActive(true);owner.Main.interactable=owner.Main.blocksRaycasts=true;owner.Clock.text=owner.Campaign.ClockText;owner.RefreshMembers();owner.RefreshResources();owner.NoticeTitle.text="거점 귀환";owner.NoticeBody.text=destination.Name+"에서 돌아왔습니다.";if(owner.ReturnPanel)owner.ReturnPanel.Complete();return true;}
  public void ClosePopup(){if(Rooms)Rooms.CancelPending();Popup.SetActive(false);returnPrompt=false;Main.interactable=Main.blocksRaycasts=true;}
  public void SetRoomTransit(bool value){InTransit=value;Main.interactable=Main.blocksRaycasts=!value;}
  void Update(){if(IsOpen&&!InTransit&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(MissingPerson&&MissingPerson.IsOpen)MissingPerson.Advance();else if(Story&&Story.IsOpen)Story.Close();else if(FieldBags&&FieldBags.IsOpen)FieldBags.Close();else if(Encounter&&Encounter.IsOpen)Encounter.Escape();else if(Loot&&Loot.IsOpen)Loot.Escape();else if(Search&&Search.IsOpen)Search.Escape();else if(Popup.activeSelf)ClosePopup();else AskReturn();}}
 }
}




