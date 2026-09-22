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
  public ExpeditionEncounterPanel Encounter; public ExpeditionLootPanel Loot; public SettlementInventoryPanel Inventory=>owner.InventoryPanel; public ExpeditionRoomNavigation Rooms; public ExpeditionSearchPanel Search;
  public GameObject View,World,SettlementWorld,Popup; public CanvasGroup Main,Fade;
  public Text Clock,Place,Resources,Status,PopupTitle,PopupBody;
  public Button Return,PopupBack,ReturnConfirm;public Button[] Objects;
  public string[] ObjectNames,ObjectDescriptions;
  public RectTransform MemberContent;public ExpeditionMemberCard MemberPrefab;
  public Transform PawnRoot;public GameObject PawnPrefab;public Sprite Scout,Medic;
  public readonly List<ExpeditionMemberCard> Cards=new List<ExpeditionMemberCard>();
  readonly List<GameObject> pawns=new List<GameObject>();
  public bool IsOpen=>View.activeSelf; public bool InTransit{get;private set;}
  public IReadOnlyList<Adventurer> Participants=>people;
  Adventurer[] people=Array.Empty<Adventurer>();ExpeditionPlanPanel.Destination destination;SettlementController owner;bool returnPrompt;
  public void Initialize(SettlementController c){owner=c;View.SetActive(false);World.SetActive(false);Popup.SetActive(false);Return.onClick.AddListener(AskReturn);PopupBack.onClick.AddListener(ClosePopup);ReturnConfirm.onClick.AddListener(ConfirmReturn);if(Rooms)Rooms.Initialize(this);if(Search)Search.Initialize(this);if(Loot)Loot.Initialize(this);if(Encounter)Encounter.Initialize(this);for(int i=0;i<Objects.Length;i++){int k=i;Objects[i].onClick.AddListener(()=>Inspect(k));}}
  public bool Begin(Adventurer[] party,ExpeditionPlanPanel.Destination target){
   if(IsOpen||InTransit||target==null||target.Id!="mall"||party==null||party.Any(p=>owner.IsAssigned(p)||owner.InventoryPanel.SlotsFor(p)>p.BagCapacity))return false;
   if(!owner.Campaign.BeginFieldExpedition(target.Id,party,target.OneWayMinutes))return false;
   people=party.ToArray();destination=target;owner.Main.gameObject.SetActive(false);SettlementWorld.SetActive(false);World.SetActive(true);View.SetActive(true);Popup.SetActive(false);Main.interactable=Main.blocksRaycasts=false;
   foreach(var card in Cards){card.gameObject.SetActive(false);Destroy(card.gameObject);}Cards.Clear();foreach(var pawn in pawns)Destroy(pawn);pawns.Clear();
   for(int i=0;i<people.Length;i++){int index=i;var p=people[i];int original=Array.IndexOf(owner.Campaign.Party.ToArray(),p);string id=PartySelectionSession.Selected.ElementAtOrDefault(original);var data=owner.Roster.Candidates.FirstOrDefault(x=>x.Id==id);var card=Instantiate(MemberPrefab,MemberContent);card.Name.text=p.Name;card.Portrait.sprite=data?.Portrait;card.Role.text="현장 가방";card.Health.text=p.Health+" / "+p.MaxHealth;card.HealthFill.fillAmount=(float)p.Health/Math.Max(1,p.MaxHealth);card.State.text="가방 "+owner.InventoryPanel.SlotsFor(p)+" / "+p.BagCapacity;card.Check.gameObject.SetActive(false);card.Button.onClick.AddListener(()=>ShowBag(index));Cards.Add(card);
    var pawn=Instantiate(PawnPrefab,PawnRoot);pawn.transform.localPosition=new Vector3((i-(people.Length-1)*.5f)*1.5f,-1.35f,0);pawn.transform.Find("Body").GetComponent<SpriteRenderer>().sprite=id=="medic"?Medic:Scout;pawns.Add(pawn);
   }
   Clock.text=owner.Campaign.ClockText;Place.text=target.Name+"\n1F · 오락실";Resources.text="보급품 "+people.Sum(p=>owner.InventoryPanel.CountFor(p,"supplies"))+"  ·  탄약 "+people.Sum(p=>owner.InventoryPanel.CountFor(p,"ammo"));Status.text="주변의 사물을 눌러 살펴보세요.";if(Rooms)Rooms.ResetVisit();if(Search)Search.ResetVisit();if(Encounter)Encounter.ResetVisit();StartCoroutine(Arrive());return true;
  }
  IEnumerator Arrive(){InTransit=true;Fade.gameObject.SetActive(true);Fade.alpha=1;Fade.blocksRaycasts=true;float t=0;while(t<.7f){t+=Time.unscaledDeltaTime;Fade.alpha=1-Mathf.Clamp01(t/.7f);yield return null;}Fade.gameObject.SetActive(false);InTransit=false;Main.interactable=Main.blocksRaycasts=true;}
  public void OpenPopup(string title,string body,bool returning=false){if(!IsOpen||InTransit||(Encounter&&Encounter.IsOpen))return;returnPrompt=returning;ReturnConfirm.GetComponentInChildren<Text>().text=returning?"귀환":"확인";PopupTitle.text=title;PopupBody.text=body;ReturnConfirm.gameObject.SetActive(returning);Popup.SetActive(true);Main.interactable=Main.blocksRaycasts=false;}
  public void Inspect(int index){if(InTransit||(Encounter&&Encounter.IsOpen)||(Loot&&Loot.IsOpen)||(Search&&Search.IsOpen)||Popup.activeSelf||index<0||index>=ObjectNames.Length)return;if(Rooms&&index==3){Rooms.AskMove();return;}if(Rooms)Rooms.MarkInspected(index);if(Loot&&Loot.State(index).Complete){Loot.Open(index);return;}if(Search){Search.Open(index);return;}OpenPopup(ObjectNames[index],ObjectDescriptions[index]);}
  public void RefreshFieldBags(){for(int i=0;i<Cards.Count;i++)Cards[i].State.text="가방 "+Inventory.SlotsFor(people[i])+" / "+people[i].BagCapacity;Resources.text="보급품 "+people.Sum(p=>Inventory.CountFor(p,"supplies"))+"  ·  탄약 "+people.Sum(p=>Inventory.CountFor(p,"ammo"));}
  void ShowBag(int index){var p=people[index];var items=owner.InventoryPanel.Items.Where(i=>owner.InventoryPanel.CountFor(p,i.Id)>0).ToArray();OpenPopup(p.Name+" · 현장 가방",(items.Length==0?"챙긴 물건이 없습니다.":string.Join("\n",items.Select(i=>i.Name+"  "+owner.InventoryPanel.CountFor(p,i.Id))))+"\n\n사용 공간  "+owner.InventoryPanel.SlotsFor(p)+" / "+p.BagCapacity+"\n공용 창고는 거점에서 이용할 수 있습니다.");}
  void AskReturn(){if(Rooms&&Rooms.CurrentRoom!=0){OpenPopup("출구로 돌아가야 합니다.","오락실의 출입구에서 거점으로 돌아갈 수 있습니다.");return;}OpenPopup("거점으로 돌아갈까요?",owner.Campaign.Home.Name+"까지 "+destination.OneWayMinutes+"분\n\n동행 대원과 챙긴 물건을 그대로 가져갑니다.",true);}
  void ConfirmReturn(){if(!IsOpen||InTransit||!Popup.activeSelf||!returnPrompt)return;returnPrompt=false;if(!owner.Campaign.EndFieldExpedition(destination.OneWayMinutes))return;Popup.SetActive(false);World.SetActive(false);View.SetActive(false);SettlementWorld.SetActive(true);owner.Main.gameObject.SetActive(true);owner.Main.interactable=owner.Main.blocksRaycasts=true;owner.Clock.text=owner.Campaign.ClockText;owner.RefreshMembers();owner.SupplyCount.text=owner.Campaign.Supplies.ToString();owner.AmmoCount.text=owner.Campaign.Ammo.ToString();owner.NoticeTitle.text="거점 귀환";owner.NoticeBody.text=destination.Name+"에서 돌아왔습니다.";}
  public void ClosePopup(){if(Rooms)Rooms.CancelPending();Popup.SetActive(false);returnPrompt=false;Main.interactable=Main.blocksRaycasts=true;}
  public void SetRoomTransit(bool value){InTransit=value;Main.interactable=Main.blocksRaycasts=!value;}
  void Update(){if(IsOpen&&!InTransit&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame){if(Encounter&&Encounter.IsOpen)Encounter.Escape();else if(Loot&&Loot.IsOpen)Loot.Escape();else if(Search&&Search.IsOpen)Search.Escape();else if(Popup.activeSelf)ClosePopup();else AskReturn();}}
 }
}




