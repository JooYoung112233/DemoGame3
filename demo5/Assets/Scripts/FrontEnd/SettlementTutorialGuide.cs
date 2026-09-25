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
  [Tooltip("말 놓기 안내 문구 (탐험 · 대원 말 → 실루엣 → 턴 진행 · 문에 모이기)")] public FieldPawnGuideTexts PawnTexts=new FieldPawnGuideTexts();
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
   Clear();var c=Owner;if(!c||c.Campaign==null||c.TutorialSkipped||!c.Introduction||!c.Opening)return;
   if(c.GetComponent<SettlementTutorialNarrative>()?.IsBlocking==true)return;
   if(c.Introduction.Step>=5&&(!c.Opening.State.Enabled||c.Opening.State.Complete))return;
   if(c.GameMenu.IsOpen)return;
   if(c.Opening.View.activeSelf){Show("기록을 남기고","확인한 내용을 기억해두고 돌아갑시다.",c.Opening.Back);return;}
   if(c.IsPopupOpen)return;
   if(c.WorkPanel.IsOpen){
    var w=c.WorkPanel;
    if(w.Orders.Count>0)Show("휴식 예약 · 아직 회복 전","휴식은 시간이 지나야 완료됩니다.\n돌아가서 ‘시간 진행’을 여세요.",w.CloseButton);
    else if(w.Confirm.IsInteractable())Show("휴식 시작 · 대원에게 배정","표시된 시간 뒤에 체력이 회복됩니다.\n시간·회복량을 보고 휴식을 맡기세요.",w.Confirm);
    else {var row=w.Rows.FirstOrDefault(r=>r.Button.IsInteractable()&&c.Campaign.Party.Any(p=>p.Name==r.Name.text&&p.Health<p.MaxHealth&&!c.IsAssigned(p)));
     Show(row!=null?"대원 선택 · 체력 확인":"지금은 휴식할 대원이 없습니다",row!=null?"체력은 현재 / 최대, 빈 칸은 줄어든 양입니다.\n빛나는 대원을 눌러 휴식을 정하세요.":"예약된 대원은 다른 일을 맡을 수 없습니다.\n돌아가서 맡긴 일을 확인하세요.",row?.Button??w.CloseButton);}
    return;
   }
   if(c.TimePanel.IsOpen){var t=c.TimePanel;
    if(t.NextCompletion()==0)Show("완료 기록 · 실제로 바뀐 상태","끝난 휴식·제작 결과를 보여줍니다.\n회복량과 완성품을 보고 돌아가세요.",t.CloseButton);
    else if(t.SelectedMinutes!=t.NextCompletion())Show("다음 완료까지 · 대기 시간 선택","가장 먼저 끝날 일까지 시간을 고릅니다.\n이 버튼은 시간을 아직 진행하지 않습니다.",t.Choices[3]);
    else Show("시간 진행 · 예약한 일 처리","선택한 시간만큼 모든 맡긴 일이 진행됩니다.\n아래 ‘"+t.ConfirmLabel.text+"’을 눌러 확정하세요.",t.Confirm);
    return;
   }
   if(c.InventoryPanel.IsOpen){Inventory();return;}
   if(c.CraftPanel.IsOpen){Craft();return;}
   if(c.ReturnPanel.IsOpen){var p=c.ReturnPanel;bool can=p.StoreAll.IsInteractable();
    Show("모두 보관 · 가방에서 공용 창고로",can?"공용 창고의 재료로 시설을 고칩니다.\n눌러 보관하세요. 넘친 물자는 가방에 남습니다.":"여기서는 가방과 창고의 물자를 확인합니다.\n돌아가서 필요한 복구를 정하세요.",can?p.StoreAll:p.Back);return;}
   if(c.PackingPanel.IsOpen){var p=c.PackingPanel;
    // First trip: 'nothing to pack' only when no ammo or bandage exists anywhere (stock or bags); ammo left in stock is named, as the packing notice does.
    var inv=c.InventoryPanel;int stockAmmo=inv.StockCount("ammo");bool anyBandage=inv.StockCount("bandage")>0||c.Campaign.Party.Any(x=>inv.CountFor(x,"bandage")>0);
    string hint=c.Opening.State.FirstReturn?"창고 물건 선택 → ‘가방에 넣기’로 휴대합니다.\n짐을 정했으면 ‘준비 내역 확인’을 누르세요.":c.Campaign.Ammo==0&&!anyBandage?"챙길 탄약·붕대가 없어도 출발할 수 있습니다.\n빈 가방으로 가려면 ‘준비 내역 확인’을 누르세요.":stockAmmo>0?"창고 탄약 "+stockAmmo+"발은 ‘가방에 넣기’로 챙깁니다.\n짐을 정했으면 ‘준비 내역 확인’을 누르세요.":"가방의 물건만 원정에 가져갑니다.\n담은 물건을 보고 ‘준비 내역 확인’을 누르세요.";
    Show(p.Review.activeSelf?"출발 확인 · 이동 시간 소비":"개인 가방 · 사용 칸 / 전체 칸",p.Review.activeSelf?"출발하면 표시된 편도 시간이 지납니다.\n행선지·대원·휴대품을 확인하고 출발하세요.":hint,p.Review.activeSelf?p.Depart:p.Ready);return;}
   if(c.ExpeditionPanel.IsOpen){var p=c.ExpeditionPanel;int mall=System.Array.FindIndex(p.Destinations,d=>d.Id=="mall");
    if(p.Current?.Id!="mall")Show("지도 · 수색할 장소 선택","장소를 고르면 거리와 알려진 물자가 보입니다.\n금례의 인수증에 적힌 폐상가를 누르세요.",mall>=0?p.Markers[mall]:null);
    else if(p.Selected.Count<Mathf.Min(2,c.Campaign.Party.Count(m=>m.Health>0&&!c.IsAssigned(m))))Show("동행 대원 · 원정에 갈 사람","체크된 대원만 함께 가고 가방도 나눠 씁니다.\n빛나는 대원을 눌러 동행에 추가하세요.",p.Cards.FirstOrDefault(r=>Usable(r.Button)&&!r.Check.gameObject.activeSelf)?.Button);
    else Show("짐 꾸리기 · 가져갈 물건 선택","공용 창고의 물건을 개인 가방으로 옮깁니다.\n‘짐 꾸리기’를 눌러 휴대품을 준비하세요.",p.Pack);
    return;
   }
   if(c.ArrivalPanel.IsOpen){Field();return;}
   if(!c.Main.gameObject.activeInHierarchy||!c.Main.interactable)return;
   int step=c.Introduction.Step;
   if(step>=5&&c.Opening.Active){
    c.Opening.Evaluate();
    switch(c.Opening.CurrentAction){
     case 0:Show("출입구 · 원정 준비","지도에서 수색할 곳과 동행 대원을 정합니다.\n오른쪽 출입구를 눌러 준비를 시작하세요.",c.Exit);break;
     case 1:Show("작업대 · 시설 복구와 도구 제작","공용 창고의 재료로 일할 내용을 정합니다.\n뒤쪽 작업대를 눌러 필요한 작업을 고르세요.",c.Workbench);break;
     case 2:Show("공용 보관함 · 재료를 모으는 창고","시설 복구에는 창고에 보관한 재료를 씁니다.\n빛나는 보관함을 눌러 가방과 물자를 옮기세요.",c.Cabinet);break;
     case 3:Show("시간 진행 · 맡긴 일 진행","휴식·제작은 예약한 시간이 지나면 끝납니다.\n‘시간 진행’에서 완료될 일을 확인하세요.",c.Advance);break;
     default:Show(c.Opening.CurrentAction==4?"관리표 · 다음 수색의 단서":"생활 기록 · 확인한 사실 정리",c.Opening.CurrentAction==4?"작업대에서 발견한 물자 기록을 읽습니다.\n‘관리표 읽기’를 눌러 내용을 확인하세요.":"직접 확인한 장소와 단서를 기록합니다.\n‘첫 생활 기록 정리’를 누르세요.",c.Introduction.Action);break;
    }
   }
   else if(c.Introduction.Action.gameObject.activeInHierarchy)Show("지금 할 일",c.Introduction.ActionLabel.text,c.Introduction.Action,false);
  }
  void Inventory(){var c=Owner;var p=c.InventoryPanel;
   if(p.QuantityPopup.activeSelf){
    bool makeRoom=c.Opening.State.ClueRead&&c.CraftPanel.Available("prybar")>0&&!c.Campaign.Party.Any(person=>p.CountFor(person,"prybar")>0)&&p.SelectedFromBag&&p.SelectedItemId!=null&&!c.Campaign.Party.Any(person=>p.TransferLimit(person,"prybar",true)>0);
    int held=makeRoom?p.CountFor(p.SelectedMember,p.SelectedItemId):0;
    if(makeRoom&&p.TransferLimit(p.SelectedMember,p.SelectedItemId,false)>=held&&int.TryParse(p.QuantityValue.text,out int n)&&n<held&&Usable(p.Max))Show("도구를 담을 한 칸","이 물건을 모두 내려놓으면 한 칸이 빕니다. ‘최대’를 선택하세요.",p.Max);
    else Show("옮길 수량 · 물건의 개수","여기서 정한 개수만 선택한 쪽으로 옮깁니다.\n수량과 이동 방향을 확인하고 확정하세요.",p.Confirm);
    return;
   }
   if(!c.Opening.State.FirstReturn){Show("공용 창고와 개인 가방","창고는 함께 쓰고, 가방의 물건만 휴대합니다.\n위의 이름으로 가방을 바꿉니다. 확인 후 돌아가세요.",p.CloseButton);return;}
   c.Opening.Evaluate();var project=c.CraftPanel.Recipes.FirstOrDefault(r=>r.Id==c.Opening.CurrentRecipeId);
   if(project!=null&&project.Costs.All(cost=>c.CraftPanel.Available(cost.MaterialId)>=cost.Count)){Show("공용 창고 · 제작 재료 준비됨","가방과 달리 창고의 자재는 제작에 씁니다.\n돌아가서 작업대에 일을 맡기세요.",p.CloseButton);return;}
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
    if(carrier!=null&&carrier!=p.SelectedMember){Show("이름 탭 · 대원별 가방",carrier.Name+"이 필요한 자재를 들고 있습니다.\n그 이름을 눌러 가방을 확인하세요.",MemberTarget(carrier));return;}
    if(carrier!=null){var item=p.Items.First(i=>i.Id==need.MaterialId);TransferHint(need.MaterialId,false,"창고로 옮기기 · 제작에 쓸 재료","가방의 "+item.Name+"은 아직 제작에 못 씁니다.\n창고로 옮겨 시설 복구에 쓰세요.");return;}
   }
   if(c.Opening.State.ClueRead&&c.CraftPanel.Available("prybar")>0&&!c.Campaign.Party.Any(person=>p.CountFor(person,"prybar")>0)){
    if(p.TransferLimit(p.SelectedMember,"prybar",true)>0){TransferHint("prybar",true,"문을 열 도구 챙기기","걸린 문틈을 벌릴 지렛대입니다. 함께 나갈 대원에게 챙겨줍시다.");return;}
    var carrier=c.Campaign.Party.FirstOrDefault(person=>p.TransferLimit(person,"prybar",true)>0);
    if(carrier!=null){Show("도구를 들 대원",carrier.Name+"의 이름을 누르세요. 지렛대를 담을 빈칸이 있습니다.",MemberTarget(carrier));return;}
    bool CanEmpty(Demo5.NightRun.Adventurer person,string id){int held=p.CountFor(person,id);return held>0&&p.TransferLimit(person,id,false)>=held;}
    var spare=p.Items.FirstOrDefault(i=>CanEmpty(p.SelectedMember,i.Id));
    if(spare!=null){TransferHint(spare.Id,false,"도구를 담을 빈칸 마련",spare.Name+"을 모두 창고에 보관해 지렛대를 담을 한 칸을 만드세요.");return;}
    var other=c.Campaign.Party.FirstOrDefault(person=>p.Items.Any(i=>CanEmpty(person,i.Id)));
    if(other!=null){Show("비울 수 있는 가방",other.Name+"의 이름을 누르세요. 휴대품을 보관해 도구를 담을 칸을 만들 수 있습니다.",MemberTarget(other));return;}
    Show("가방·창고 공간 확인","한 칸을 비울 보관 공간이 부족합니다. 물품을 사용하거나 제작한 뒤 지렛대를 챙기세요.",p.CloseButton);return;
   }
   Show("개인 가방 · 사용 칸 / 전체 칸","같은 물건은 여러 개여도 한 칸에 담습니다.\n짐 정리가 끝났으면 돌아가세요.",p.CloseButton);
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
   Show(title,(toBag?"창고 물건은 가방에 넣어야 휴대합니다.":"제작에는 공용 창고에 넣은 재료를 씁니다.")+"\n"+(toBag?"창고":"가방")+"에서 ‘"+item.Name+"’을 누르세요.",row?.Button??p.Tabs[0]);
  }
  void Craft(){var c=Owner;var p=c.CraftPanel;
   if(p.CancelPopup.activeSelf)return;
   c.Opening.Evaluate();string wanted=c.Opening.CurrentRecipeId;
   if(p.Orders.Count>0){Show("제작 예약 · 시간 후 완성","담당 대원이 작업 중이며 아직 완성 전입니다.\n돌아가서 ‘시간 진행’을 여세요.",p.CloseButton);return;}
   if(wanted!=null&&p.SelectedRecipeId!=wanted){var recipe=p.Recipes.FirstOrDefault(r=>r.Id==wanted);var row=p.RecipeRows.FirstOrDefault(r=>r.Label.text==recipe?.Name);
    Show("작업 목록 · 복구와 제작법","작업을 고르면 재료와 걸리는 시간이 보입니다.\n‘"+(recipe?.Name??"목표 작업")+"’을 선택하세요.",row?.Button??p.Tabs[recipe?.Category??0]);return;}
   if(p.Confirm.IsInteractable())Show("작업 시작 · 재료와 대원 예약","표시된 재료를 쓰고 완료까지 시간이 필요합니다.\n재료·시간을 확인하고 작업을 시작하세요.",p.Confirm);
   else if(!p.WorkerRows.Any(r=>r.Check.gameObject.activeSelf)){var worker=p.WorkerRows.FirstOrDefault(r=>Usable(r.Button));Show(worker!=null?"담당 대원 · 작업할 사람":"모두 다른 일을 하고 있습니다",worker!=null?"한 대원은 동시에 한 가지 일만 맡습니다.\n빛나는 대원을 눌러 담당자로 정하세요.":"예약된 대원은 다른 작업을 맡을 수 없습니다.\n돌아가서 먼저 맡긴 일을 마치세요.",worker?.Button??p.CloseButton);}
   else Show("재료 수량 · 사용 가능 / 필요","공용 창고 기준이며 붉은 재료가 부족합니다.\n돌아가서 가방의 자재를 보관하세요.",p.CloseButton);
  }
  void Field(){var a=Owner.ArrivalPanel;
   // Keep the other agent's combat and later strategic board presentation intact.
   if(a.InTransit||(a.Story&&a.Story.IsOpen)||a.FieldBags.IsOpen||a.Encounter.IsOpen||a.Encounter.Battle.IsOpen||Owner.Opening.State.FirstReturn)return;
   {var idle=a.GetComponent<FieldIdleConfirm>();if(a.Popup.activeSelf&&idle&&idle.Asking!=FieldIdleConfirm.Mode.None){Show(PawnTexts.IdleTitle,PawnTexts.IdleText,a.PopupBack);return;}}
   if(a.Popup.activeSelf){Show("돌아갈 길 확인","이동 시간과 상황을 확인한 뒤 이어갑시다.",Usable(a.ReturnConfirm)?a.ReturnConfirm:a.PopupBack);return;}
   if(a.Loot.IsOpen){var p=a.Loot;
    if(p.LeaveReview.activeSelf){bool room=LootHasSpace();Show("무엇을 남겨둘까",room?"아직 담을 자리가 있습니다. 계속 정리할 수 있습니다.":"가방이 찼습니다. 두고 나가도 남은 물건은 현장에 있습니다.",room?p.LeaveCancel:p.LeaveConfirm);return;}
    if(p.FieldRows.Count==0)Show("발견물 정리 완료","이곳에서 챙길 물건은 없습니다.\n현장으로 돌아가세요.",p.Back);
    else if(Usable(p.Transfer)&&p.FieldRows.Any(r=>r.Selection&&r.Selection.enabled)){
     var row=p.FieldRows.FirstOrDefault(r=>r.Selection&&r.Selection.enabled);
     if(row!=null&&int.TryParse(row.Count.text,out int count)&&int.TryParse(p.Quantity.text,out int quantity)&&count>quantity&&Usable(p.Max))Show("최대 · 담을 수 있는 개수","이 버튼은 가져올 수량만 정합니다.\n‘최대’를 눌러 수량을 선택하세요.",p.Max);
     else Show("선택 가져오기 · 가방에 담기","발견만 한 물건은 아직 현장에 있습니다.\n눌러서 선택한 수량을 가방에 담으세요.",p.Transfer);
    }
    else {
     var selected=p.FieldRows.FirstOrDefault(r=>r.Selection&&r.Selection.enabled);
     var item=Owner.InventoryPanel.Items.FirstOrDefault(i=>i.Name==selected?.Label.text);
     var carrier=item==null?null:a.Participants.FirstOrDefault(person=>CanCarry(person,item.Id));
     if(carrier!=null&&carrier!=p.Current)Show("다른 대원의 가방",carrier.Name+"의 이름을 누르면 이 물건을 나누어 담을 수 있습니다.",p.Cards.FirstOrDefault(r=>r.Name.text==carrier.Name)?.Button);
     else if(!LootHasSpace())Show("가방이 찼습니다","남은 물건은 현장에 남겨둡시다. 돌아가서 짐을 내려놓을 수 있습니다.",p.Back);
     else {var row=p.FieldRows.FirstOrDefault(r=>{var data=Owner.InventoryPanel.Items.FirstOrDefault(i=>i.Name==r.Label.text);return data!=null&&a.Participants.Any(person=>CanCarry(person,data.Id));});Show("발견물 · 현장에 남아 있는 물건",p.TakeAll?"수색 완료만으로는 물건을 가져오지 않습니다.\n물건을 고르거나 ‘모두 담기’를 누르세요.":"발견한 물건은 가방에 담아야 휴대합니다.\n가져갈 물건을 고르세요.",row?.Button??p.Back);}
    }
    return;
   }
   if(a.Search.IsOpen){var p=a.Search;
    if(p.ReadOnly){Show(PawnTexts.DetailTitle,PawnTexts.DetailText,p.Back);return;}
    if(p.Review.activeSelf)Show("수색도 · 진행한 턴 / 필요한 턴","1턴 진행은 이곳에서 "+a.Rooms.MinutesPerTurn+"분을 씁니다.\n예상 변화와 위험을 보고 확정하세요.",p.Confirm);
    else if(p.Worker==null)Show("수색 대원 · 물건을 조사할 사람","대원 한 명에게 이곳 수색을 맡깁니다.\n빛나는 대원 이름을 눌러 고르세요.",p.Cards.FirstOrDefault(r=>Usable(r.Button))?.Button);
    else Show("수색 방법 · 결과 미리보기","진행 전에 수색·소음 변화를 미리 봅니다.\n‘"+p.Choose.GetComponentInChildren<Text>().text+"’을 눌러 확인하세요.",p.Choose);
    return;
   }
   // 말 놓기 (기획/탐험-말놓기-조작-재설계.md): each goal below is reached one real press at a time on the pawn board (Pawn → FieldPawnGuide:
   // a pawn, its silhouette, the second pawn and the co-op silhouette on both tutorial objects, then '턴 진행'; a door by gathering).
   bool searched=a.Loot.Peek(0,out var crate)&&crate.Complete;
   if(searched&&Owner.MissingPerson&&!Owner.MissingPerson.State.Found){
    var clue=a.Rooms.CurrentRoom==0?a.Objects[3]:a.Rooms.CurrentRoom==1?a.Objects[MissingPersonStory.SiteIndex]:a.Rooms.StorageBack;
    string clueText=a.Rooms.CurrentRoom==1?"적재함에서 보관표를 찾아봅시다.\n잠긴 보관실 밖이라 지렛대는 필요 없습니다.":"인수증에 적힌 보관실은 복도 쪽입니다.\n먼저 복도의 적재함을 확인합시다.";
    if(Pawn(a,"금례의 수리품 · 인계 기록",clueText.Split('\n')[0],a.Rooms.CurrentRoom==1?"search:"+MissingPersonStory.SiteIndex:"door:1",a.Rooms.CurrentRoom==1))return;
    Show("금례의 수리품 · 인계 기록",clueText,clue,field:true);return;
   }
   // The first crate and the home exit are in the arcade; guide one real doorway at a time.
   if(a.Rooms&&a.Rooms.CurrentRoom!=0){
    bool storage=a.Rooms.CurrentRoom==2;var back=storage?a.Rooms.StorageBack:a.Rooms.CurrentRoom==1?a.Rooms.CorridorBack:null;
    string backTitle=searched?"돌아갈 길":"입구 쪽 자재 상자",backText=storage?"출구는 오락실에 있습니다.\n먼저 복도로 돌아갑시다.":searched?"들어온 문으로 돌아갑시다.\n오락실에 거점으로 가는 출구가 있습니다.":"자재 상자는 입구 쪽에 있습니다.\n오락실로 돌아갑시다.";
    if(Pawn(a,backTitle,backText.Split('\n')[0],storage?"door:1":"door:0",false))return;
    if(Usable(back))Show(backTitle,backText,back,field:true);
    return;
   }
   if(!searched&&Pawn(a,"미수색 상자 · 물음표","?는 아직 조사하지 않은 곳입니다.","search:0",true))return;
   var target=searched?a.Return:a.Objects[0];
   if(Usable(target))Show(searched?"출구 · 거처로 귀환":"미수색 상자 · 물음표",searched?"가방에 담은 물건만 가져갑니다.\n출구를 눌러 거처로 돌아가세요.":"?는 아직 조사하지 않은 곳입니다.\n빛나는 상자를 눌러 수색하세요.",target,field:true);
  }
  // 말 놓기: the next real press toward goal ("search:N" · "door:R" · "observe:ID"; coop: a second pawn on the object) in the field banner.
  // False when there is no pawn board or nothing leads there from here (the caller then shows its old target).
  bool Pawn(ExpeditionArrivalPanel a,string title,string lead,string goal,bool coop){if(!FieldPawnGuide.Next(a,goal,coop,PawnTexts,lead,out var target,out var text)||!Usable(target))return false;Show(title,text,target,field:true);return true;}
  bool CanCarry(Demo5.NightRun.Adventurer person,string id)=>person.Health>0&&(Owner.InventoryPanel.CountFor(person,id)>0||Owner.InventoryPanel.SlotsFor(person)<person.BagCapacity);
  bool LootHasSpace(){var a=Owner.ArrivalPanel;return a.Loot.FieldRows.Any(row=>{var item=Owner.InventoryPanel.Items.FirstOrDefault(i=>i.Name==row.Label.text);return item!=null&&a.Participants.Any(person=>CanCarry(person,item.Id));});}
 }
}
