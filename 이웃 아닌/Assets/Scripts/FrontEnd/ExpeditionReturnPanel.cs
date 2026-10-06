using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Demo5.NightRun;
namespace Demo5.FrontEnd {
 public sealed partial class ExpeditionReturnPanel:MonoBehaviour {
  public GameObject View;public GameObject[] HideWhileOpen;
  public Button Back,StoreAll,InspectBags;public Text Heading,Overview,Body,Notice;public ScrollRect Scroll;
  public ReturnMemberRow MemberPrefab;public ReturnItemRow ItemPrefab;
  public RectTransform MemberContent,ItemContent;public ScrollRect MemberScroll;
  public Text Duration,MemberCount,ItemCount,EmptyItems;
  readonly List<ReturnMemberRow> memberRows=new List<ReturnMemberRow>();readonly List<ReturnItemRow> itemRows=new List<ReturnItemRow>();
  readonly List<MemberResult> memberResults=new List<MemberResult>();readonly List<ItemResult> itemResults=new List<ItemResult>();
  sealed class MemberResult {public Sprite Portrait;public string Name;public int Before,After,Maximum;}
  sealed class ItemResult {public Sprite Icon;public string Id,Name;public int Before,After;}
  int elapsedMinutes;
  public bool IsOpen=>View&&View.activeSelf;public bool HasReport{get;private set;}
  SettlementController owner;bool[] hidden;Adventurer[] party=Array.Empty<Adventurer>();
  Dictionary<Adventurer,Dictionary<string,int>> initial=new Dictionary<Adventurer,Dictionary<string,int>>();
  Dictionary<Adventurer,int> health=new Dictionary<Adventurer,int>();
  int start;string destination,report,overview;
  public void Initialize(SettlementController c){owner=c;View.SetActive(false);Back.onClick.AddListener(Close);StoreAll.onClick.AddListener(Unpack);InspectBags.onClick.AddListener(()=>{Close();int index=Array.IndexOf(owner.Campaign.Party.ToArray(),party.FirstOrDefault());owner.InventoryPanel.Open(Math.Max(0,index));});}
  public void Begin(Adventurer[] people,string place,int absoluteStart){party=people.ToArray();destination=place;start=absoluteStart;initial.Clear();health.Clear();HasReport=false;foreach(var p in party){health[p]=p.Health;initial[p]=owner.InventoryPanel.Items.ToDictionary(i=>i.Id,i=>owner.InventoryPanel.CountFor(p,i.Id));}}
  public void Complete(){
   int elapsed=(owner.Campaign.Day-1)*1440+owner.Campaign.MinuteOfDay-start;
   elapsedMinutes=elapsed;overview=destination+" → "+owner.Campaign.Home.Name+"  ·  "+elapsed+"분 경과";
   memberResults.Clear();itemResults.Clear();foreach(var p in party){int index=Array.IndexOf(owner.Campaign.Party.ToArray(),p);var id=PartySelectionSession.Selected.ElementAtOrDefault(index);var data=owner.Roster.Candidates.FirstOrDefault(x=>x.Id==id)??owner.Roster.Candidates.FirstOrDefault(x=>x.DisplayName==p.Name);memberResults.Add(new MemberResult{Portrait=data?.Portrait,Name=p.Name,Before=health[p],After=p.Health,Maximum=p.MaxHealth});}
   foreach(var item in owner.InventoryPanel.Items){int before=party.Sum(p=>initial[p][item.Id]),after=party.Sum(p=>owner.InventoryPanel.CountFor(p,item.Id));if(before!=0||after!=0)itemResults.Add(new ItemResult{Id=item.Id,Icon=item.Icon,Name=item.Name,Before=before,After=after});}
   var lines=new List<string>();foreach(var p in party){lines.Add(p.Name+"  ·  체력 "+health[p]+" → "+p.Health+" / "+p.MaxHealth);var items=new List<string>();foreach(var item in owner.InventoryPanel.Items){int before=initial[p][item.Id],after=owner.InventoryPanel.CountFor(p,item.Id);if(before==0&&after==0)continue;int change=after-before;items.Add(item.Name+"  "+before+" → "+after+(change==0?"":"  ("+(change>0?"+":"")+change+")"));}lines.Add(items.Count==0?"휴대품 없음":string.Join("\n",items));lines.Add("");}
   report=string.Join("\n",lines)+"\n출발 → 귀환 시점의 휴대 수량입니다.\n변화량은 획득·사용·현장에 두기를 합산합니다.";HasReport=true;Open();
  }
  public void Open(){if(!HasReport||IsOpen||owner.Campaign?.Stage!=JourneyStage.Settlement)return;hidden=HideWhileOpen.Select(g=>g.activeSelf).ToArray();foreach(var g in HideWhileOpen)g.SetActive(false);owner.Main.interactable=owner.Main.blocksRaycasts=false;View.SetActive(true);Heading.text="원정 귀환";Overview.text=destination+" → "+owner.Campaign.Home.Name;Body.text=report;Duration.text=elapsedMinutes+"분 경과";Rebuild();Refresh();Canvas.ForceUpdateCanvases();Scroll.verticalNormalizedPosition=1;MemberScroll.verticalNormalizedPosition=1;EventSystem.current?.SetSelectedGameObject(Back.gameObject);}
  void Rebuild(){
   foreach(var row in memberRows){row.gameObject.SetActive(false);Destroy(row.gameObject);}memberRows.Clear();foreach(var row in itemRows){row.gameObject.SetActive(false);Destroy(row.gameObject);}itemRows.Clear();
   foreach(var data in memberResults){var row=Instantiate(MemberPrefab,MemberContent);row.Bind(data.Portrait,data.Name,data.Before,data.After,data.Maximum);memberRows.Add(row);}
   foreach(var data in itemResults){var row=Instantiate(ItemPrefab,ItemContent);row.Bind(data.Icon,data.Name,data.Before,data.After);itemRows.Add(row);}
   MemberCount.text=memberResults.Count+"명";ItemCount.text=itemResults.Count+"종";EmptyItems.gameObject.SetActive(itemResults.Count==0);
  }
  void Refresh(){
   int count=party.Sum(p=>owner.InventoryPanel.Items.Sum(i=>owner.InventoryPanel.CountFor(p,i.Id)));
   bool canStore=party.Any(p=>owner.InventoryPanel.Items.Any(i=>owner.InventoryPanel.TransferLimit(p,i.Id,false)>0));
   StoreAll.interactable=canStore;
   Notice.text=count==0?"정리 완료 · 원정대의 가방이 비었습니다.":!canStore?"창고가 가득 찼습니다 · 휴대품은 가방에 유지됩니다 · ‘가방별 정리’에서 확인하세요.":"휴대품 "+count+"개 · "+(owner.Development?owner.Development.StockSummary:"")+" · 초과분은 가방 유지";
  }
  void Unpack(){if(!IsOpen)return;foreach(var p in party)foreach(var item in owner.InventoryPanel.Items){int count=owner.InventoryPanel.TransferLimit(p,item.Id,false);if(count>0)owner.InventoryPanel.MoveFor(p,item.Id,count,false);}Refresh();}
  public void Close(){if(!IsOpen)return;View.SetActive(false);for(int i=0;i<HideWhileOpen.Length;i++)HideWhileOpen[i].SetActive(hidden[i]);owner.Main.interactable=owner.Main.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(owner.Journal.gameObject);}
  void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Close();}
 }
}
