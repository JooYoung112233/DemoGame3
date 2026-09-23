using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Demo5.NightRun;
namespace Demo5.FrontEnd {
 public sealed class SettlementTimePanel:MonoBehaviour {
  public GameObject View;
  public GameObject[] HideWhileOpen;bool[] hidden;
  public Button CloseButton,Confirm;
  public Button[] Choices;
  public Text Clock,Preview,Summary,ConfirmLabel;
  public ScrollRect Scroll;
  public RectTransform Content;public TimeWorkRow RowPrefab;public Sprite RestIcon,CompletedIcon;public Text Empty;
  readonly System.Collections.Generic.List<TimeWorkRow> rows=new System.Collections.Generic.List<TimeWorkRow>();
  void AddRow(Sprite icon,string title,string detail,string result,bool complete){var row=Instantiate(RowPrefab,Content);row.Bind(icon,title,detail,result,complete);rows.Add(row);}
  SettlementController owner;int minutes=30;
  public bool IsOpen=>View&&View.activeSelf;
  public void Initialize(SettlementController value){owner=value;View.SetActive(false);CloseButton.onClick.AddListener(Close);Confirm.onClick.AddListener(Advance);for(int i=0;i<Choices.Length;i++){int k=i;Choices[i].onClick.AddListener(()=>{minutes=k==0?15:k==1?30:k==2?60:NextCompletion();Refresh();});}}
  public int NextCompletion()=>owner.WorkPanel.Orders.Select(o=>o.Minutes).Concat(owner.CraftPanel.Orders.Select(o=>o.Minutes)).DefaultIfEmpty(0).Min();
  public void Open(){if(IsOpen||owner.Campaign?.Stage!=JourneyStage.Settlement||owner.IsPopupOpen||owner.WorkPanel.IsOpen||owner.CraftPanel.IsOpen||owner.InventoryPanel.IsOpen)return;minutes=30;hidden=HideWhileOpen.Select(g=>g.activeSelf).ToArray();foreach(var g in HideWhileOpen)g.SetActive(false);owner.Main.interactable=owner.Main.blocksRaycasts=false;View.SetActive(true);Refresh();EventSystem.current?.SetSelectedGameObject(CloseButton.gameObject);}
  void Refresh(){
   Clock.text=owner.Campaign.ClockText.Replace("\n"," · ");int total=owner.Campaign.MinuteOfDay+minutes;
   Preview.text=minutes+"분 후  ·  DAY "+(owner.Campaign.Day+total/1440)+"  "+((total%1440)/60).ToString("00")+":"+(total%60).ToString("00");
   var jobs=owner.WorkPanel.Orders.Select(o=>o.Member.Name+" · "+o.Name+"\n"+(o.Minutes<=minutes?"완료 예정 · 체력 +"+System.Math.Max(0,System.Math.Min(o.Recovery,o.Member.MaxHealth-o.Member.Health)):"남은 시간 "+(o.Minutes-minutes)+"분"))
    .Concat(owner.CraftPanel.Orders.Select(o=>o.Member.Name+" · "+o.Recipe.Name+" ×"+o.Quantity+"\n"+(o.Minutes<=minutes?"완료 예정":"남은 시간 "+(o.Minutes-minutes)+"분"))).ToArray();
   Summary.text=jobs.Length==0?"진행 중인 작업이 없습니다.\n시간만 흘려보낼 수 있습니다.":string.Join("\n\n",jobs);
   Summary.text+="\n\n작업은 동시에 진행됩니다.\n완료한 제작물은 공용 창고에 보관됩니다.";
   if(owner.ActivityLog.Count>0)Summary.text+="\n\n최근 완료 기록\n"+string.Join("\n",owner.ActivityLog.Skip(System.Math.Max(0,owner.ActivityLog.Count-6)));
   ConfirmLabel.text=minutes+"분 진행";Confirm.interactable=minutes>0;Choices[3].interactable=NextCompletion()>0;
   for(int i=0;i<3;i++)Choices[i].GetComponent<Image>().color=minutes==(i==0?15:i==1?30:60)?new Color(1,.78f,.37f):Color.white;
   foreach(var row in rows){row.gameObject.SetActive(false);Destroy(row.gameObject);}rows.Clear();
   foreach(var o in owner.WorkPanel.Orders){bool done=o.Minutes<=minutes;int gain=System.Math.Max(0,System.Math.Min(o.Recovery,o.Member.MaxHealth-o.Member.Health));AddRow(RestIcon,o.Name,o.Member.Name+" · 현재 "+o.Minutes+"분 남음",done?"완료 예정\n체력 +"+gain:"진행 후\n"+(o.Minutes-minutes)+"분 남음",done);}
   foreach(var o in owner.CraftPanel.Orders){bool done=o.Minutes<=minutes;AddRow(o.Recipe.Icon,o.Recipe.Name+" ×"+o.Quantity,o.Member.Name+" · 현재 "+o.Minutes+"분 남음",done?"완료 예정":"진행 후\n"+(o.Minutes-minutes)+"분 남음",done);}
   foreach(var log in owner.ActivityLog.Skip(System.Math.Max(0,owner.ActivityLog.Count-6)))AddRow(CompletedIcon,"완료 기록",log,"완료",true);
   Empty.gameObject.SetActive(rows.Count==0);
   Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(Content);Scroll.StopMovement();Scroll.verticalNormalizedPosition=1;
  }
  void Advance(){if(!IsOpen||minutes<=0)return;if(owner.Campaign.AdvanceSettlementTime(minutes)){minutes=30;Refresh();}}
  public void Close(){if(!IsOpen)return;View.SetActive(false);for(int i=0;i<HideWhileOpen.Length;i++)HideWhileOpen[i].SetActive(hidden[i]);owner.Main.interactable=owner.Main.blocksRaycasts=true;EventSystem.current?.SetSelectedGameObject(owner.Advance.gameObject);}
  void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Close();}
 }
}
