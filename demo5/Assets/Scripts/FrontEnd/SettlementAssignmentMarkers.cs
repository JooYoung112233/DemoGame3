using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // One assignment bubble per facility, over the facility (not over the member): who works there, what, and how far along.
 // Polls the rest / craft / cooking orders (no events exist) and maps them to facilities exactly like SettlementPawnMotion.JobPosition.
 // Only the bubble position follows the facility rect; every other layout value comes from the prefab and this Inspector.
 public sealed class SettlementAssignmentMarkers : MonoBehaviour {
  public enum Place { Bed, Stock, Workbench, Cabinet, Research }
  [Serializable] public sealed class Slot {
   [Tooltip("이 말풍선이 설 시설")] public Place Facility;
   [Tooltip("시설 버튼. 버튼이 켜져 있을 때만 말풍선을 보입니다.")] public Button Button;
   [Tooltip("시설 사각형 위 가운데에서 꼬리 끝까지의 보정(px, 위 = +)")] public Vector2 Offset;
   [Tooltip("이 시설의 말풍선(AssignmentBubble 프리팹)")] public AssignmentBubble Bubble;
  }
  [Tooltip("정착지 컨트롤러(비우면 부모에서 찾음)")] public SettlementController Owner;
  [Tooltip("시설별 말풍선")] public Slot[] Slots;
  [Tooltip("진행 고리 칸 수 = 전체 시간 ÷ 이 분(올림). 고리는 경과 ÷ 전체 비율로 칸 전체를 채웁니다.")] [Min(1)] public int MinutesPerSegment=10;
  [Tooltip("시간이 흐른 뒤 고리가 차오르는 시간(초 · 실제 시간)")] [Min(0)] public float AnimateSeconds=.6f;
  [Tooltip("말풍선 상태(진행 중 = 맥동 없음)")] public AssignmentBubble.Tone Tone=AssignmentBubble.Tone.Working;
  [Tooltip("말풍선에 넣을 초상 최대 수")] [Range(1,3)] public int MaxPortraits=3;
  [Tooltip("머리 위 작업 표식(대원 말의 WorkBadge)을 숨깁니다. 시설 말풍선이 대신합니다.")] public bool HideHeadBadges=true;
  [Header("문구")]
  [Tooltip("휴식: {0} 휴식 이름, {1} 남은 시간")] public string RestFormat="{0} · {1} 남음";
  [Tooltip("제작·조리: {0} 작업 이름, {1} 남은 시간")] public string WorkFormat="{0} · {1}";
  [Tooltip("제작 수량이 2 이상일 때 이름: {0} 이름, {1} 수량")] public string QuantityFormat="{0} ×{1}";
  [Tooltip("조리 이름(조리 창처럼 인분으로): {0} 요리 이름, {1} 인분 수")] public string ServingsFormat="{0} {1}인분";
  [Tooltip("같은 시설에 작업이 더 있을 때: {0} 문구, {1} 나머지 수")] public string OthersFormat="{0} 외 {1}";
  [Tooltip("분: {0}")] public string MinuteText="{0}분";
  [Tooltip("시간: {0}")] public string HourText="{0}시간";
  [Tooltip("시간+분: {0} 시간, {1} 분")] public string HourMinuteText="{0}시간 {1}분";

  sealed class Entry { public object Order; public Adventurer Member; public int Remaining,Total,Quantity,Servings; public string Name; public bool Rest; public ActionGlyph.Kind Glyph; }
  sealed class Anim { public object Primary; public float Shown,From,Target,Start=-1; }
  readonly Dictionary<object,int> firstSeen=new Dictionary<object,int>();
  readonly HashSet<object> present=new HashSet<object>();
  readonly List<Sprite> sprites=new List<Sprite>();
  readonly Dictionary<Adventurer,Sprite> portraitOf=new Dictionary<Adventurer,Sprite>();int partySize=-1;
  readonly Vector3[] corners=new Vector3[4];
  List<Entry>[] groups;Anim[] anims;SettlementPawnMotion motion;SpriteRenderer[] badges;

  public AssignmentBubble BubbleFor(Place place)=>Slots?.FirstOrDefault(s=>s!=null&&s.Facility==place)?.Bubble;
  public Slot SlotFor(Place place)=>Slots?.FirstOrDefault(s=>s!=null&&s.Facility==place);
  // Same mapping as SettlementPawnMotion.JobPosition. -1: no place in the room (side-room / housing work).
  public static int PlaceOf(string recipeId){
   string id=recipeId??"";
   if(id=="open-side-room"||id=="prepare-side-room")return -1;
   if(id.StartsWith("research-")||id=="build-research")return (int)Place.Research;
   if(id=="build-stock"||id=="expand-stock")return (int)Place.Cabinet;
   if(id=="build-bed"||id=="repair-bed")return (int)Place.Bed;
   if(id=="build-cooker"||id=="upgrade-cooker")return (int)Place.Stock;
   return (int)Place.Workbench;
  }
  public string Duration(int minutes)=>minutes<60?string.Format(MinuteText,minutes):minutes%60==0?string.Format(HourText,minutes/60):string.Format(HourMinuteText,minutes/60,minutes%60);

  void LateUpdate(){
   if(!Owner)Owner=GetComponentInParent<SettlementController>();
   if(!Owner||Owner.Campaign==null||Slots==null)return;
   HideBadges();Collect();
   bool screen=Owner.Main&&Owner.Main.gameObject.activeInHierarchy&&Owner.Main.interactable&&!Owner.IsPopupOpen&&Owner.Campaign.Stage==JourneyStage.Settlement;
   for(int i=0;i<Slots.Length;i++){
    var s=Slots[i];if(s==null||!s.Bubble)continue;var list=groups[i];
    if(!screen||list.Count==0||!s.Button||!s.Button.gameObject.activeInHierarchy){s.Bubble.Hide();continue;}
    var primary=list.OrderBy(e=>e.Remaining).First();
    sprites.Clear();AddPortrait(primary.Member);foreach(var e in list)if(sprites.Count<MaxPortraits)AddPortrait(e.Member);
    string name=primary.Servings>0?string.Format(ServingsFormat,primary.Name,primary.Servings):primary.Quantity>1?string.Format(QuantityFormat,primary.Name,primary.Quantity):primary.Name;
    string label=string.Format(primary.Rest?RestFormat:WorkFormat,name,Duration(primary.Remaining));
    if(list.Count>1)label=string.Format(OthersFormat,label,list.Count-1);
    // Elapsed share of the whole job over all cells, so a 15-minute job (2 cells) fills both before it ends.
    int step=Mathf.Max(1,MinutesPerSegment),total=Mathf.CeilToInt(primary.Total/(float)step);float target=primary.Total>0?(primary.Total-primary.Remaining)/(float)primary.Total*total:0;
    var a=anims[i];
    if(a.Primary!=primary.Order){a.Primary=primary.Order;a.Shown=a.Target=target;a.Start=-1;}
    else if(!Mathf.Approximately(a.Target,target)){a.From=a.Shown;a.Target=target;a.Start=Time.unscaledTime;}
    if(a.Start>=0){float t=AnimateSeconds<=0?1:(Time.unscaledTime-a.Start)/AnimateSeconds;a.Shown=Mathf.Lerp(a.From,a.Target,Mathf.SmoothStep(0,1,t));if(t>=1){a.Shown=a.Target;a.Start=-1;}}
    s.Bubble.Show(sprites,primary.Glyph,label,Mathf.FloorToInt(target),0,total,Tone);s.Bubble.SetProgress(a.Shown);
    Follow(s);
   }
  }
  void AddPortrait(Adventurer member){
   if(member==null)return;int size=Owner.Campaign.Party.Count();if(size!=partySize){portraitOf.Clear();partySize=size;}
   if(!portraitOf.TryGetValue(member,out var p))portraitOf[member]=p=Owner.DataFor(member)?.Portrait;
   if(p&&!sprites.Contains(p))sprites.Add(p);
  }
  // Tail tip on the top centre of the facility rect (+ Offset), in the bubble's parent space.
  void Follow(Slot s){
   ((RectTransform)s.Button.transform).GetWorldCorners(corners);var parent=s.Bubble.Rect.parent;if(!parent)return;
   var top=parent.InverseTransformPoint((corners[1]+corners[2])*.5f);var pos=new Vector3(top.x+s.Offset.x,top.y+s.Offset.y,0);
   if(s.Bubble.Rect.localPosition!=pos)s.Bubble.Rect.localPosition=pos;
  }
  void Collect(){
   if(groups==null||groups.Length!=Slots.Length){groups=new List<Entry>[Slots.Length];anims=new Anim[Slots.Length];for(int i=0;i<Slots.Length;i++){groups[i]=new List<Entry>();anims[i]=new Anim();}}
   foreach(var g in groups)g.Clear();present.Clear();
   if(Owner.WorkPanel)foreach(var o in Owner.WorkPanel.Orders){
    int total=o.Name=="수면"?Owner.WorkPanel.SleepMinutes:Owner.CraftPanel&&Owner.CraftPanel.BedLevel==0?Owner.WorkPanel.ShortMinutes*2:Owner.WorkPanel.ShortMinutes;
    Add((int)Place.Bed,new Entry{Order=o,Member=o.Member,Remaining=o.Minutes,Total=total,Quantity=1,Name=o.Name,Rest=true,Glyph=ActionGlyph.Kind.Rest});
   }
   if(Owner.CraftPanel)foreach(var o in Owner.CraftPanel.Orders){
    if(o.Recipe==null)continue;int place=PlaceOf(o.Recipe.Id);if(place<0)continue;string id=o.Recipe.Id??"";
    Add(place,new Entry{Order=o,Member=o.Member,Remaining=o.Minutes,Total=Owner.CraftPanel.DurationFor(o.Recipe,o.Quantity,o.Member),Quantity=o.Quantity,Name=o.Recipe.Name,
     Glyph=id.StartsWith("research-")?ActionGlyph.Kind.Research:ActionGlyph.Kind.Work});
   }
   if(Owner.CookingPanel)foreach(var o in Owner.CookingPanel.Orders)
    Add((int)Place.Stock,new Entry{Order=o,Member=o.Member,Remaining=o.Minutes,Total=o.Recipe==null?0:Owner.CookingPanel.DurationFor(o.Recipe,o.Quantity,o.Member),Quantity=o.Quantity,Servings=Mathf.Max(1,o.OutputCount),Name=o.Recipe?.Name??"",Glyph=ActionGlyph.Kind.Cook});
   if(firstSeen.Count>present.Count)foreach(var key in firstSeen.Keys.Where(k=>!present.Contains(k)).ToArray())firstSeen.Remove(key);
  }
  // The order's own duration when it can be recomputed, never less than the remaining time first seen (restored saves, later upgrades).
  void Add(int place,Entry e){
   present.Add(e.Order);if(!firstSeen.TryGetValue(e.Order,out int seen)){seen=e.Remaining;firstSeen[e.Order]=seen;}
   e.Total=Mathf.Max(e.Total,Mathf.Max(seen,e.Remaining));
   for(int i=0;i<Slots.Length;i++)if(Slots[i]!=null&&(int)Slots[i].Facility==place){groups[i].Add(e);return;}
  }
  // Only the renderer is switched: SettlementPawnMotion still toggles the badge objects as before (its checks keep their meaning).
  void HideBadges(){
   if(!motion){motion=FindAnyObjectByType<SettlementPawnMotion>();badges=null;}
   if(!motion||motion.WorkBadges==null)return;
   if(badges==null||badges.Length!=motion.WorkBadges.Length)badges=motion.WorkBadges.Select(b=>b?b.GetComponent<SpriteRenderer>():null).ToArray();
   foreach(var b in badges)if(b&&b.enabled==HideHeadBadges)b.enabled=!HideHeadBadges;
  }
 }
}
