using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // Derive the next click from real game state, so backtracking and loading do not replay a click script.
 [DefaultExecutionOrder(1000)]
 public sealed class SettlementTutorialGuide : MonoBehaviour {
  public SettlementController Owner;
  public GameObject Banner;
  public Text Title, Instruction;
  public TutorialTargetGraphic Marker;
  [Header("다음 클릭 강조 · 어둡게 가리기와 화살표")]
  [Tooltip("다음 클릭과 안내문만 밝게 남기고 나머지를 어둡게 덮음 (클릭은 막지 않음)")] public TutorialSpotlight Spotlight;
  [Tooltip("다음 클릭을 가리키는 화살표")] public TutorialPointer Pointer;
  [Tooltip("어둡게 가리는 정도 (방이 어두워 약하면 올림)")][Range(0,1)] public float DimStrength=.72f;
  [Min(0)] public float HolePadding=14,PointerGap=8,PointerBounce=12,PointerSpeed=5,PulseGrow=10;
  [Tooltip("화살표 크기")] public Vector2 PointerSize=new Vector2(58,70);
  public Vector2 HeaderPosition=new Vector2(720,-22),FooterPosition=new Vector2(600,-922);
  public Vector2 InventoryPosition=new Vector2(1080,-22);
  [Tooltip("발견물 창: 돌아가기(~400)와 선택 가져오기(1178~) 사이")] public Vector2 LootFooterPosition=new Vector2(464,-922);
  [Tooltip("탐험 기본 화면: 좌하단 현황 종이를 임시로 덮는 안내 위치/크기")] public Vector2 FieldPosition=new Vector2(80,-776),FieldSize=new Vector2(480,148);
  public Button Target {get;private set;}
  public string Guidance {get;private set;}
  SettlementFacilityFocus facility;
  Text coveredDestination;bool destinationWasEnabled;
  readonly Vector3[] corners=new Vector3[4];
  struct LayoutSnapshot {
   public Vector2 Min,Max,Pivot,Position,Size;
   public LayoutSnapshot(RectTransform r){Min=r.anchorMin;Max=r.anchorMax;Pivot=r.pivot;Position=r.anchoredPosition;Size=r.sizeDelta;}
   public void Restore(RectTransform r){r.anchorMin=Min;r.anchorMax=Max;r.pivot=Pivot;r.anchoredPosition=Position;r.sizeDelta=Size;}
  }
  struct TextSnapshot {
   public LayoutSnapshot Layout;public int Size;public float Spacing;public bool BestFit;public TextAnchor Alignment;public HorizontalWrapMode Horizontal;public VerticalWrapMode Vertical;
   public TextSnapshot(Text t){Layout=new LayoutSnapshot(t.rectTransform);Size=t.fontSize;Spacing=t.lineSpacing;BestFit=t.resizeTextForBestFit;Alignment=t.alignment;Horizontal=t.horizontalOverflow;Vertical=t.verticalOverflow;}
   public void Restore(Text t){Layout.Restore(t.rectTransform);t.fontSize=Size;t.lineSpacing=Spacing;t.resizeTextForBestFit=BestFit;t.alignment=Alignment;t.horizontalOverflow=Horizontal;t.verticalOverflow=Vertical;}
  }
  bool hasLayout;LayoutSnapshot bannerLayout;TextSnapshot titleLayout,instructionLayout;
  void RememberLayout(){if(hasLayout)return;bannerLayout=new LayoutSnapshot((RectTransform)Banner.transform);titleLayout=new TextSnapshot(Title);instructionLayout=new TextSnapshot(Instruction);hasLayout=true;}
  void RestoreLayout(){if(!hasLayout||!Banner||!Title||!Instruction)return;bannerLayout.Restore((RectTransform)Banner.transform);titleLayout.Restore(Title);instructionLayout.Restore(Instruction);}
  static void Place(RectTransform r,Vector2 position,Vector2 size){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;}
  void FieldLayout(){Place((RectTransform)Banner.transform,FieldPosition,FieldSize);Place(Title.rectTransform,new Vector2(24,-12),new Vector2(FieldSize.x-48,40));Place(Instruction.rectTransform,new Vector2(24,-56),new Vector2(FieldSize.x-48,78));Title.fontSize=26;Instruction.fontSize=22;Title.alignment=TextAnchor.MiddleLeft;Instruction.alignment=TextAnchor.UpperLeft;foreach(var t in new[]{Title,Instruction}){t.resizeTextForBestFit=false;t.lineSpacing=1;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;}}
  bool Usable(Button b)=>b&&b.IsActive()&&b.IsInteractable();
  void Show(string title,string text,Button target,bool banner=true,bool field=false){
   RememberLayout();RestoreLayout();
   Target=Usable(target)?target:null;Guidance=title+" · "+text;
   Banner.SetActive(banner);Title.text=title;Instruction.text=text;
   ((RectTransform)Banner.transform).anchoredPosition=Owner.InventoryPanel.IsOpen?InventoryPosition:Owner.PackingPanel.IsOpen?FooterPosition:Owner.ArrivalPanel.Loot.IsOpen?LootFooterPosition:HeaderPosition;
   if(field)FieldLayout();
   if(Owner.PackingPanel.IsOpen&&banner){coveredDestination=Owner.PackingPanel.Destination;destinationWasEnabled=coveredDestination.enabled;coveredDestination.enabled=false;}
   if(!Target)return;
   facility=Target.GetComponent<SettlementFacilityFocus>();
   if(facility)facility.TutorialHighlighted=true;
   var parent=(RectTransform)Marker.transform.parent;var box=TargetBounds(parent,field);
   float pulse=.5f+.5f*Mathf.Sin(Time.unscaledTime*3);
   // Every next click gets the pulsing frame (facilities also keep their artwork outline).
   {var r=Marker.rectTransform;r.pivot=new Vector2(.5f,.5f);r.position=parent.TransformPoint(box.center);r.sizeDelta=box.size+Vector2.one*(12+PulseGrow*pulse);
    Marker.color=new Color(1,.79f,.36f,.8f+.2f*pulse);Marker.gameObject.SetActive(true);}
   Focus(parent,box,banner,field);
  }
  // The rect of 'r' in the space of 'space'.
  Rect Bounds(RectTransform space,RectTransform r){r.GetWorldCorners(corners);Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);foreach(var c in corners){Vector2 p=space.InverseTransformPoint(c);min=Vector2.Min(min,p);max=Vector2.Max(max,p);}return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
  // A door's tall transparent hit area is not its label; focus the small visible mark instead.
  Rect TargetBounds(RectTransform space,bool field){
   var door=field?Target.GetComponent<ExplorationHotspot>():null;
   if(door&&door.IsDoor&&(door.Marker||door.Caption)){
    if(!door.Marker)return Bounds(space,(RectTransform)door.Caption.transform);
    var mark=Bounds(space,door.Marker.rectTransform);if(!door.Caption)return mark;
    var caption=Bounds(space,(RectTransform)door.Caption.transform);return Rect.MinMaxRect(Mathf.Min(mark.xMin,caption.xMin),Mathf.Min(mark.yMin,caption.yMin),Mathf.Max(mark.xMax,caption.xMax),Mathf.Max(mark.yMax,caption.yMax));
   }
   return Bounds(space,(RectTransform)Target.transform);
  }
  static Rect Grow(Rect r,float d)=>Rect.MinMaxRect(r.xMin-d,r.yMin-d,r.xMax+d,r.yMax+d);
  // Dim everything but the next click and where its instruction is written; point an arrow at the click.
  void Focus(RectTransform parent,Rect target,bool banner,bool field){
   Rect paper=banner&&Banner.activeSelf?Bounds(parent,(RectTransform)Banner.transform):default;
   if(Spotlight){var space=Spotlight.rectTransform;var holes=new List<Rect>{Grow(TargetBounds(space,field),HolePadding)};
    if(banner&&Banner.activeSelf)holes.Add(Bounds(space,(RectTransform)Banner.transform));
    else{if(Owner.NoticeTitle&&Owner.NoticeTitle.transform.parent.gameObject.activeInHierarchy)holes.Add(Bounds(space,(RectTransform)Owner.NoticeTitle.transform.parent));
     if(Owner.Introduction&&Owner.Introduction.Action.gameObject.activeInHierarchy)holes.Add(Bounds(space,(RectTransform)Owner.Introduction.Action.transform));}
    Spotlight.SetHoles(holes);Spotlight.color=new Color(0,0,0,DimStrength);Spotlight.gameObject.SetActive(true);}
   if(Pointer){PlacePointer(parent,target,paper);Pointer.gameObject.SetActive(true);}
  }
  // Arrow side: above, below, right, left of the click (pointing at it), full size or smaller. Each placement (with its bounce) must stay on screen;
  // among those, the one covering the least wins: another usable control or the instruction paper weighs 10, readable text 1. Ties keep the order.
  static readonly (Vector2 dir,float angle)[] Sides={(Vector2.up,0),(Vector2.down,180),(Vector2.right,-90),(Vector2.left,90)};
  readonly List<(Rect rect,float weight)> blockers=new List<(Rect,float)>();readonly List<Text> texts=new List<Text>();
  void PlacePointer(RectTransform parent,Rect target,Rect paper){
   var r=Pointer.rectTransform;var area=parent.rect;float bob=PointerBounce*(.5f+.5f*Mathf.Sin(Time.unscaledTime*PointerSpeed));
   blockers.Clear();if(paper.width>0)blockers.Add((paper,10));
   bool Skip(Transform t)=>t.IsChildOf(Target.transform)||Target.transform.IsChildOf(t)||t.IsChildOf(Marker.transform.parent);
   foreach(var s in Selectable.allSelectablesArray){if(!s||!s.IsActive()||!s.IsInteractable()||Skip(s.transform))continue;
    var b=Bounds(parent,(RectTransform)s.transform);if(b.width<area.width*.5f&&b.height<area.height*.5f)blockers.Add((b,10));}
   Owner.GetComponentsInChildren(false,texts);
   foreach(var t in texts){if(!t.isActiveAndEnabled||string.IsNullOrEmpty(t.text)||t.color.a<.2f||Skip(t.transform))continue;
    var b=Bounds(parent,t.rectTransform);if(b.width<area.width*.5f&&b.height<area.height*.5f)blockers.Add((b,1));}
   float best=float.MaxValue;Vector2 bestSize=PointerSize*.62f,bestAt=new Vector2(target.center.x,target.yMax+PointerGap+PointerSize.y*.31f+bob);float bestAngle=0;
   foreach(float scale in new[]{1f,.8f,.62f})foreach(var (dir,angle) in Sides){
    var size=PointerSize*scale;var box=dir.y!=0?size:new Vector2(size.y,size.x);
    float reach=(dir.y!=0?target.height:target.width)*.5f+PointerGap+(dir.y!=0?box.y:box.x)*.5f;
    var near=target.center+dir*reach;var sweep=Rect.MinMaxRect(Mathf.Min(near.x,near.x+dir.x*PointerBounce)-box.x*.5f,Mathf.Min(near.y,near.y+dir.y*PointerBounce)-box.y*.5f,Mathf.Max(near.x,near.x+dir.x*PointerBounce)+box.x*.5f,Mathf.Max(near.y,near.y+dir.y*PointerBounce)+box.y*.5f);
    if(sweep.xMin<area.xMin+4||sweep.xMax>area.xMax-4||sweep.yMin<area.yMin+4||sweep.yMax>area.yMax-4)continue;
    float cover=0;foreach(var (b,w) in blockers)if(b.Overlaps(sweep))cover+=w*(Mathf.Min(b.xMax,sweep.xMax)-Mathf.Max(b.xMin,sweep.xMin))*(Mathf.Min(b.yMax,sweep.yMax)-Mathf.Max(b.yMin,sweep.yMin));
    // A smaller arrow is a last resort: count it as covering a little more than a clear full-size one.
    cover+=(1-scale)*400;
    if(cover<best){best=cover;bestSize=size;bestAt=near+dir*bob;bestAngle=angle;}
   }
   Aim(parent,r,bestSize,bestAt,bestAngle);
  }
  static void Aim(RectTransform parent,RectTransform r,Vector2 size,Vector2 at,float angle){r.pivot=new Vector2(.5f,.5f);r.sizeDelta=size;r.position=parent.TransformPoint(at);r.localEulerAngles=new Vector3(0,0,angle);
  }
  void Clear(){if(coveredDestination)coveredDestination.enabled=destinationWasEnabled;coveredDestination=null;if(facility)facility.TutorialHighlighted=false;facility=null;Target=null;Guidance=null;RestoreLayout();if(Banner)Banner.SetActive(false);if(Marker)Marker.gameObject.SetActive(false);if(Spotlight)Spotlight.gameObject.SetActive(false);if(Pointer)Pointer.gameObject.SetActive(false);}
  void OnDisable()=>Clear();
  void LateUpdate(){
   Clear();var c=Owner;if(!c||c.Campaign==null||!c.Introduction||!c.Opening)return;
   if(c.Introduction.Step>=5&&(!c.Opening.State.Enabled||c.Opening.State.Complete))return;
   if(c.GameMenu.IsOpen)return;
   if(c.Opening.View.activeSelf){Show("기록 확인","내용을 읽고 ‘돌아가기’를 누르면 다음 목표가 이어집니다.",c.Opening.Back);return;}
   if(c.IsPopupOpen)return;
   if(c.WorkPanel.IsOpen){
    var w=c.WorkPanel;
    if(w.Orders.Count>0)Show("휴식 예약 확인","‘돌아가기’를 누른 뒤 시간을 진행하면 회복됩니다.",w.CloseButton);
    else if(w.Confirm.IsInteractable())Show("휴식 2 / 2 · 예약","‘휴식 시작’을 누르세요. 다음에 시간을 진행합니다.",w.Confirm);
    else {var row=w.Rows.FirstOrDefault(r=>r.Button.IsInteractable()&&c.Campaign.Party.Any(p=>p.Name==r.Name.text&&p.Health<p.MaxHealth&&!c.IsAssigned(p)));
     Show(row!=null?"휴식 1 / 2 · 대원 선택":"지금은 휴식할 대원이 없습니다",row!=null?"빛나는 대원 이름을 눌러 쉬게 할 사람을 고르세요.":"‘돌아가기’를 눌러 다음 목표나 진행 중인 작업을 확인하세요.",row?.Button??w.CloseButton);}
    return;
   }
   if(c.TimePanel.IsOpen){var t=c.TimePanel;
    if(t.NextCompletion()==0)Show("작업 완료","회복·제작 결과를 확인하고 ‘돌아가기’를 누르세요.",t.CloseButton);
    else if(t.SelectedMinutes!=t.NextCompletion())Show("시간 1 / 2 · 완료까지","‘다음 완료까지’를 누르면 필요한 시간이 선택됩니다.",t.Choices[3]);
    else Show("시간 2 / 2 · 진행","빛나는 진행 버튼을 눌러 예약한 작업을 끝내세요.",t.Confirm);
    return;
   }
   if(c.InventoryPanel.IsOpen){Inventory();return;}
   if(c.CraftPanel.IsOpen){Craft();return;}
   if(c.ReturnPanel.IsOpen){var p=c.ReturnPanel;bool can=p.StoreAll.IsInteractable();
    Show("귀환 · 회수품 정리",can?"‘모두 보관’을 누르세요. 넘치는 물자는 가방에 남습니다.":"‘돌아가기’를 누르세요. 이제 회수품으로 창고를 복구합니다.",can?p.StoreAll:p.Back);return;}
   if(c.PackingPanel.IsOpen){var p=c.PackingPanel;
    // First trip: 'nothing to pack' only when no ammo or bandage exists anywhere (stock or bags); ammo left in stock is named, as the packing notice does.
    var inv=c.InventoryPanel;int stockAmmo=inv.StockCount("ammo");bool anyBandage=inv.StockCount("bandage")>0||c.Campaign.Party.Any(x=>inv.CountFor(x,"bandage")>0);
    string hint=c.Opening.State.FirstReturn?"필요한 물건을 챙긴 뒤 ‘준비 내역 확인’을 누르세요.":c.Campaign.Ammo==0&&!anyBandage?"처음에는 챙길 탄약·붕대가 없습니다. ‘준비 내역 확인’을 누르세요.":stockAmmo>0?"창고에 탄약 "+stockAmmo+"발이 있습니다. 챙기거나 그대로 ‘준비 내역 확인’을 누르세요.":"필요한 물건을 챙긴 뒤 ‘준비 내역 확인’을 누르세요.";
    Show(p.Review.activeSelf?"출발 · 최종 확인":"짐 꾸리기 · 준비 확인",p.Review.activeSelf?"이동 시간과 대원을 확인한 뒤 ‘출발’을 누르세요.":hint,p.Review.activeSelf?p.Depart:p.Ready);return;}
   if(c.ExpeditionPanel.IsOpen){var p=c.ExpeditionPanel;int mall=System.Array.FindIndex(p.Destinations,d=>d.Id=="mall");
    if(p.Current?.Id!="mall")Show("원정 1 / 3 · 목적지","지도에서 ‘폐상가’를 누르세요.",mall>=0?p.Markers[mall]:null);
    else if(p.Selected.Count<Mathf.Min(2,c.Campaign.Party.Count(m=>m.Health>0&&!c.IsAssigned(m))))Show("원정 2 / 3 · 동행 대원","빛나는 이름 카드를 눌러 두 사람이 함께 출발하게 하세요.",p.Cards.FirstOrDefault(r=>Usable(r.Button)&&!r.Check.gameObject.activeSelf)?.Button);
    else Show("원정 3 / 3 · 짐 준비","‘짐 꾸리기’를 누르세요. 선택한 대원이 출발합니다.",p.Pack);
    return;
   }
   if(c.ArrivalPanel.IsOpen){Field();return;}
   if(!c.Main.gameObject.activeInHierarchy||!c.Main.interactable)return;
   int step=c.Introduction.Step;
   if(step==3)Show("동료 쉬게 하기",c.Introduction.ActionLabel.text,c.WorkPanel.Orders.Count>0?c.Advance:c.Bed,false);
   else if(c.Introduction.Action.gameObject.activeInHierarchy)Show("지금 할 일",c.Introduction.ActionLabel.text,c.Introduction.Action,false);
  }
  void Inventory(){var c=Owner;var p=c.InventoryPanel;
   if(p.QuantityPopup.activeSelf){
    bool makeRoom=c.Opening.State.ClueRead&&c.CraftPanel.Available("prybar")>0&&!c.Campaign.Party.Any(person=>p.CountFor(person,"prybar")>0)&&p.SelectedFromBag&&p.SelectedItemId!=null&&!c.Campaign.Party.Any(person=>p.TransferLimit(person,"prybar",true)>0);
    int held=makeRoom?p.CountFor(p.SelectedMember,p.SelectedItemId):0;
    if(makeRoom&&p.TransferLimit(p.SelectedMember,p.SelectedItemId,false)>=held&&int.TryParse(p.QuantityValue.text,out int n)&&n<held&&Usable(p.Max))Show("한 칸 비우기 · 전체 수량","‘최대’를 누르세요. 이 물건을 모두 옮기면 가방 한 칸이 비워집니다.",p.Max);
    else Show("수량 확인","옮길 수량을 확인하고 확정 버튼을 누르세요.",p.Confirm);
    return;
   }
   if(!c.Opening.State.FirstReturn){Show("보관함 · 공용 물자와 개인 가방","창고는 함께 쓰고 가방은 각자 들고 갑니다. ‘돌아가기’를 누르세요.",p.CloseButton);return;}
   c.Opening.Evaluate();var project=c.CraftPanel.Recipes.FirstOrDefault(r=>r.Id==c.Opening.CurrentRecipeId);
   if(project!=null&&project.Costs.All(cost=>c.CraftPanel.Available(cost.MaterialId)>=cost.Count)){Show("재료 보관 완료","‘돌아가기’를 누르면 목표 시설의 복구를 시작할 수 있습니다.",p.CloseButton);return;}
   if(project!=null){
    if(c.Development.FreeSpace==0){
     var spare=p.Items.Where(item=>item.Id!="supplies"&&item.Id!="ammo"&&p.StockCount(item.Id)>(project.Costs.FirstOrDefault(cost=>cost.MaterialId==item.Id)?.Count??0)).ToArray();
     var item=spare.FirstOrDefault(i=>p.TransferLimit(p.SelectedMember,i.Id,true)>0);
     if(item!=null){TransferHint(item.Id,true,"보관 공간 확보",item.Name+" 일부를 가방으로 옮겨 복구 재료를 둘 공간을 만드세요.");return;}
     var other=c.Campaign.Party.FirstOrDefault(person=>spare.Any(i=>p.TransferLimit(person,i.Id,true)>0));
     Show("빈 가방 찾기",other!=null?other.Name+"의 가방에 여분 물자를 나누어 챙기세요.":"가방과 창고가 모두 찼습니다. 돌아가서 사용하거나 제작해 공간을 확보하세요.",other!=null?MemberTarget(other):p.CloseButton);return;
    }
    var need=project.Costs.First(cost=>c.CraftPanel.Available(cost.MaterialId)<cost.Count);
    var carrier=p.CountFor(p.SelectedMember,need.MaterialId)>0?p.SelectedMember:c.Campaign.Party.FirstOrDefault(person=>p.CountFor(person,need.MaterialId)>0);
    if(carrier!=null&&carrier!=p.SelectedMember){Show("재료를 가진 대원",carrier.Name+"의 이름을 눌러 가방을 여세요.",MemberTarget(carrier));return;}
    if(carrier!=null){var item=p.Items.First(i=>i.Id==need.MaterialId);TransferHint(need.MaterialId,false,"필요한 재료 보관",item.Name+"을 창고에 보관하세요. 지금 복구할 시설에 필요합니다.");return;}
   }
   if(c.Opening.State.ClueRead&&c.CraftPanel.Available("prybar")>0&&!c.Campaign.Party.Any(person=>p.CountFor(person,"prybar")>0)){
    if(p.TransferLimit(p.SelectedMember,"prybar",true)>0){TransferHint("prybar",true,"외출 도구 챙기기","지렛대를 가방에 넣어야 잠긴 철문을 열 수 있습니다.");return;}
    var carrier=c.Campaign.Party.FirstOrDefault(person=>p.TransferLimit(person,"prybar",true)>0);
    if(carrier!=null){Show("도구를 들 대원",carrier.Name+"의 이름을 누르세요. 지렛대를 담을 빈칸이 있습니다.",MemberTarget(carrier));return;}
    bool CanEmpty(Demo5.NightRun.Adventurer person,string id){int held=p.CountFor(person,id);return held>0&&p.TransferLimit(person,id,false)>=held;}
    var spare=p.Items.FirstOrDefault(i=>CanEmpty(p.SelectedMember,i.Id));
    if(spare!=null){TransferHint(spare.Id,false,"도구를 담을 빈칸 마련",spare.Name+"을 모두 창고에 보관해 지렛대를 담을 한 칸을 만드세요.");return;}
    var other=c.Campaign.Party.FirstOrDefault(person=>p.Items.Any(i=>CanEmpty(person,i.Id)));
    if(other!=null){Show("비울 수 있는 가방",other.Name+"의 이름을 누르세요. 휴대품을 보관해 도구를 담을 칸을 만들 수 있습니다.",MemberTarget(other));return;}
    Show("가방·창고 공간 확인","한 칸을 비울 보관 공간이 부족합니다. 물품을 사용하거나 제작한 뒤 지렛대를 챙기세요.",p.CloseButton);return;
   }
   Show("정리 후 목표 확인","‘돌아가기’를 눌러 다음 할 일을 확인하세요.",p.CloseButton);
  }
  Button MemberTarget(Demo5.NightRun.Adventurer member){
   var p=Owner.InventoryPanel;var card=p.MemberCards.FirstOrDefault(r=>r.gameObject.activeInHierarchy&&r.Label.text==member.Name);if(card)return card.Button;
   var people=Owner.Campaign.Party.ToArray();int target=System.Array.IndexOf(people,member);
   var first=p.MemberCards.FirstOrDefault(r=>r.gameObject.activeInHierarchy);int visibleStart=System.Array.FindIndex(people,person=>person.Name==first?.Label.text);
   return target<visibleStart?p.PrevMember:p.NextMember;
  }
  void TransferHint(string id,bool toBag,string title,string body){
   var p=Owner.InventoryPanel;var item=p.Items.First(i=>i.Id==id);
   if(p.SelectedItemId==id&&p.SelectedFromBag!=toBag&&Usable(toBag?p.ToBag:p.ToStock)){Show(title,body,toBag?p.ToBag:p.ToStock);return;}
   var row=(toBag?p.StockRows:p.BagRows).FirstOrDefault(r=>r.Label.text==item.Name);
   Show(title,(toBag?"창고":"가방")+"에서 ‘"+item.Name+"’을 누르세요.",row?.Button??p.Tabs[0]);
  }
  void Craft(){var c=Owner;var p=c.CraftPanel;
   if(p.CancelPopup.activeSelf)return;
   c.Opening.Evaluate();string wanted=c.Opening.CurrentRecipeId;
   if(p.Orders.Count>0){Show("제작 예약 완료","‘돌아가기’를 누른 뒤 시간을 진행해 작업을 마치세요.",p.CloseButton);return;}
   if(wanted!=null&&p.SelectedRecipeId!=wanted){var recipe=p.Recipes.FirstOrDefault(r=>r.Id==wanted);var row=p.RecipeRows.FirstOrDefault(r=>r.Label.text==recipe?.Name);
    Show("복구할 시설 선택",(recipe?.Name??"목표 작업")+"을 선택하세요.",row?.Button??p.Tabs[recipe?.Category??0]);return;}
   if(p.Confirm.IsInteractable())Show("제작 2 / 2 · 시작","재료와 시간을 확인한 뒤 ‘작업 시작’을 누르세요.",p.Confirm);
   else if(!p.WorkerRows.Any(r=>r.Check.gameObject.activeSelf)){var worker=p.WorkerRows.FirstOrDefault(r=>Usable(r.Button));Show(worker!=null?"제작 1 / 2 · 담당자":"담당자가 모두 작업 중입니다",worker!=null?"빛나는 대원 이름을 눌러 담당자를 고르세요.":"돌아가서 시간을 진행하거나 기존 작업을 마치세요.",worker?.Button??p.CloseButton);}
   else Show("재료 확인","붉은 재료가 부족합니다. 돌아가서 가방·창고를 확인하세요.",p.CloseButton);
  }
  void Field(){var a=Owner.ArrivalPanel;
   // Keep the other agent's combat and later strategic board presentation intact.
   if(a.InTransit||(a.Story&&a.Story.IsOpen)||a.FieldBags.IsOpen||a.Encounter.IsOpen||a.Encounter.Battle.IsOpen||Owner.Opening.State.FirstReturn)return;
   if(a.Popup.activeSelf){Show("이동 확인","내용을 확인하고 빛나는 버튼을 누르세요.",Usable(a.ReturnConfirm)?a.ReturnConfirm:a.PopupBack);return;}
   if(a.Loot.IsOpen){var p=a.Loot;
    if(p.LeaveReview.activeSelf){bool room=LootHasSpace();Show("남은 물건 확인",room?"담을 수 있는 물건이 남았습니다. ‘계속 정리’를 누르세요.":"가방이 모두 찼습니다. ‘두고 나가기’를 누르면 남은 물건은 현장에 보관됩니다.",room?p.LeaveCancel:p.LeaveConfirm);return;}
    if(p.FieldRows.Count==0)Show("물품 정리 완료","‘돌아가기’를 눌러 수색 화면으로 돌아가세요.",p.Back);
    else if(Usable(p.Transfer)&&p.FieldRows.Any(r=>r.Selection&&r.Selection.enabled)){
     var row=p.FieldRows.FirstOrDefault(r=>r.Selection&&r.Selection.enabled);
     if(row!=null&&int.TryParse(row.Count.text,out int count)&&int.TryParse(p.Quantity.text,out int quantity)&&count>quantity&&Usable(p.Max))Show("챙길 수량 선택","‘최대’를 누르면 선택한 종류의 물건을 한 번에 담을 수 있습니다.",p.Max);
     else Show("물건 가져오기","수량을 정한 뒤 ‘선택 가져오기’를 누르세요.",p.Transfer);
    }
    else {
     var selected=p.FieldRows.FirstOrDefault(r=>r.Selection&&r.Selection.enabled);
     var item=Owner.InventoryPanel.Items.FirstOrDefault(i=>i.Name==selected?.Label.text);
     var carrier=item==null?null:a.Participants.FirstOrDefault(person=>CanCarry(person,item.Id));
     if(carrier!=null&&carrier!=p.Current)Show("다른 대원의 가방",carrier.Name+"의 이름을 누르면 이 물건을 나누어 담을 수 있습니다.",p.Cards.FirstOrDefault(r=>r.Name.text==carrier.Name)?.Button);
     else if(!LootHasSpace())Show("가방 공간이 없습니다","‘돌아가기’를 누르세요. 담지 못한 물건은 현장에 남겨둘 수 있습니다.",p.Back);
     else {var row=p.FieldRows.FirstOrDefault(r=>{var data=Owner.InventoryPanel.Items.FirstOrDefault(i=>i.Name==r.Label.text);return data!=null&&a.Participants.Any(person=>CanCarry(person,data.Id));});Show("발견물 선택",p.TakeAll?"빛나는 물건을 누르세요. 오른쪽 아래 ‘모두 담기’로 한 번에 담을 수도 있습니다.":"빛나는 물건을 누르세요. 같은 종류는 한 칸에 함께 담을 수 있습니다.",row?.Button??p.Back);}
    }
    return;
   }
   if(a.Search.IsOpen){var p=a.Search;
    if(p.Review.activeSelf)Show("수색 · 1턴 확인","예상 결과를 확인하고 ‘1턴 진행’을 누르세요.",p.Confirm);
    else if(p.Worker==null)Show("수색 1 / 2 · 담당자","빛나는 대원 이름을 눌러 수색할 사람을 고르세요.",p.Cards.FirstOrDefault(r=>Usable(r.Button))?.Button);
    else Show("수색 2 / 2 · 진행","기본 설정으로 시작해도 됩니다. 빛나는 진행 버튼을 누르세요.",p.Choose);
    return;
   }
   bool searched=a.Loot.State(0).Complete;
   // The first crate and the home exit are in the arcade; guide one real doorway at a time.
   if(a.Rooms&&a.Rooms.CurrentRoom!=0){
    bool storage=a.Rooms.CurrentRoom==2;var back=storage?a.Rooms.StorageBack:a.Rooms.CurrentRoom==1?a.Rooms.CorridorBack:null;
    if(Usable(back))Show(searched?"첫 수색 완료 · 출구로":"첫 수색 · 오락실로",storage?"복도로 돌아가는 문을 누르세요. 다음은 오락실입니다.":searched?"오락실로 돌아가는 문을 누르세요. 출구에서 거점으로 귀환합니다.":"오락실로 돌아가는 문을 누르세요. 입구 상자는 오락실에 있습니다.",back,field:true);
    return;
   }
   var target=searched?a.Return:a.Objects[0];
   if(Usable(target))Show(searched?"첫 수색 완료 · 귀환":"첫 수색 · 입구 상자",searched?"챙긴 물건을 가지고 ‘거점으로 귀환’을 누르세요.":"입구의 상자를 눌러 담당자를 선택하세요.",target,field:true);
  }
  bool CanCarry(Demo5.NightRun.Adventurer person,string id)=>person.Health>0&&(Owner.InventoryPanel.CountFor(person,id)>0||Owner.InventoryPanel.SlotsFor(person)<person.BagCapacity);
  bool LootHasSpace(){var a=Owner.ArrivalPanel;return a.Loot.FieldRows.Any(row=>{var item=Owner.InventoryPanel.Items.FirstOrDefault(i=>i.Name==row.Label.text);return item!=null&&a.Participants.Any(person=>CanCarry(person,item.Id));});}
 }
}
