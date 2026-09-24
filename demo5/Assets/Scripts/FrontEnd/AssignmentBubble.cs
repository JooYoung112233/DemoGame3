using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 // Pin over an assignment target: portraits in a paper disc, a segmented progress ring, a round action badge and an optional
 // label strip. The RectTransform pivot is the tail tip, so callers place Rect on the target point; everything else is the
 // prefab/Inspector layout. Presentation only: it never blocks clicks and never changes game state.
 public sealed class AssignmentBubble : MonoBehaviour {
  public enum Tone { Planned, Working, Warning }
  [System.Serializable] public sealed class GlyphTint {
   [Tooltip("이 행동일 때")] public ActionGlyph.Kind Kind;
   [Tooltip("배지·이름표 띠 색")] public Color Badge=new Color(.42f,.63f,.4f,1);
   [Tooltip("배지 그림 색")] public Color Ink=new Color(.97f,.94f,.86f,1);
  }
  [Tooltip("팝·맥동이 걸리는 그림 묶음(피벗 = 꼬리 끝)")] public RectTransform Visual;
  [Tooltip("진행 고리")] public SegmentRingGraphic Ring;
  [Tooltip("원판 안 초상. 0번이 담당 대원(맨 앞), 최대 3명")] public Image[] Portraits;
  [Tooltip("행동 배지 묶음")] public GameObject Badge;
  [Tooltip("행동 배지 바탕")] public Graphic BadgeFill;
  [Tooltip("행동 그림")] public ActionGlyph Glyph;
  [Tooltip("이름표 묶음(종이 + 글)")] public GameObject LabelRoot;
  [Tooltip("이름표 글")] public Text Label;
  [Tooltip("이름표 왼쪽 색 띠")] public Graphic LabelAccent;
  [Header("초상 배치")]
  [Tooltip("인원수에 따라 초상 위치·크기를 아래 값으로 맞춥니다. 끄면 프리팹의 초상 배치를 그대로 씁니다.")] public bool ArrangePortraits=true;
  [Tooltip("인원수 1·2·3명일 때 초상 크기(px)")] public float[] PortraitSizes={62,50,42};
  [Tooltip("인원수 1·2·3명일 때 초상 중심 간격(px)")] public float[] PortraitSpacing={0,24,19};
  [Tooltip("초상 세로 보정(px, 음수 = 아래)")] public float PortraitLift=-5;
  [Header("색")]
  [Tooltip("계획(이번 턴 예정) 배지·띠 색")] public Color PlannedColor=new Color(1,.79f,.39f,1);
  [Tooltip("진행 중 배지·띠 색")] public Color WorkingColor=new Color(.95f,.64f,.24f,1);
  [Tooltip("경고 배지·띠 색(행동별 색보다 우선)")] public Color WarningColor=new Color(.86f,.38f,.3f,1);
  [Tooltip("배지 그림 기본 색")] public Color GlyphInk=new Color(.06f,.07f,.07f,1);
  [Tooltip("행동별 배지 색 바꾸기(예: 휴식 = 초록)")] public GlyphTint[] Tints={new GlyphTint{Kind=ActionGlyph.Kind.Rest}};
  [Header("움직임")]
  [Tooltip("나타날 때 커지는 시간(초 · 실제 시간)")] [Min(0)] public float PopSeconds=.24f;
  [Tooltip("나타날 때 넘쳤다 돌아오는 정도")] [Range(0,.5f)] public float PopOvershoot=.16f;
  [Tooltip("계획 상태 맥동 속도")] [Min(0)] public float PulseSpeed=3.2f;
  [Tooltip("계획 상태 맥동 크기(배율)")] [Range(0,.2f)] public float PulseScale=.045f;

  public RectTransform Rect=>(RectTransform)transform;
  public bool Visible=>gameObject.activeSelf;
  public string LabelText=>LabelRoot&&LabelRoot.activeSelf&&Label?Label.text:"";
  public ActionGlyph.Kind GlyphKind=>Glyph&&Badge&&Badge.activeSelf?Glyph.Glyph:ActionGlyph.Kind.None;
  public int PortraitCount{get;private set;}
  public Sprite PortraitAt(int i)=>i>=0&&i<PortraitCount&&Portraits!=null&&i<Portraits.Length&&Portraits[i]?Portraits[i].sprite:null;
  public int Done{get;private set;}
  public int Next{get;private set;}
  public int Total{get;private set;}
  public float Progress{get;private set;}
  public Tone CurrentTone{get;private set;}
  readonly Sprite[] shownSprites=new Sprite[3];
  bool hasValues,hasBase;float popStart;Vector3 baseScale=Vector3.one;

  public void Show(IList<Sprite> portraits,ActionGlyph.Kind glyph,string label,int done,int next,int total,Tone tone){
   if(!gameObject.activeSelf)gameObject.SetActive(true);
   SetPortraits(portraits);
   var tint=Tint(glyph);Color accent=tone==Tone.Warning?WarningColor:tint!=null?tint.Badge:tone==Tone.Planned?PlannedColor:WorkingColor;
   if(Badge&&Badge.activeSelf!=(glyph!=ActionGlyph.Kind.None))Badge.SetActive(glyph!=ActionGlyph.Kind.None);
   if(Glyph){Glyph.Glyph=glyph;Glyph.color=tint!=null&&tone!=Tone.Warning?tint.Ink:GlyphInk;}
   if(BadgeFill)BadgeFill.color=accent;
   if(LabelAccent)LabelAccent.color=accent;
   bool hasLabel=!string.IsNullOrEmpty(label);
   if(LabelRoot&&LabelRoot.activeSelf!=hasLabel)LabelRoot.SetActive(hasLabel);
   if(Label&&hasLabel)Label.text=label;
   // A new whole-step value snaps the ring; SetProgress afterwards animates between steps.
   bool snap=!hasValues||done!=Done||next!=Next||total!=Total;
   Done=done;Next=next;Total=total;CurrentTone=tone;hasValues=true;
   if(Ring&&Ring.gameObject.activeSelf!=(total>0))Ring.gameObject.SetActive(total>0);
   if(snap)SetProgress(done);
  }
  public void Hide(){if(gameObject.activeSelf)gameObject.SetActive(false);}
  public void SetProgress(float done){Progress=done;if(Ring&&Total>0)Ring.Set(Total,done,Next);}

  GlyphTint Tint(ActionGlyph.Kind kind){if(Tints!=null)foreach(var t in Tints)if(t!=null&&t.Kind==kind&&kind!=ActionGlyph.Kind.None)return t;return null;}
  void SetPortraits(IList<Sprite> portraits){
   int slots=Portraits==null?0:Mathf.Min(3,Portraits.Length),count=0;
   for(int i=0;portraits!=null&&i<portraits.Count&&count<slots;i++)if(portraits[i])shownSprites[count++]=portraits[i];
   for(int i=count;i<3;i++)shownSprites[i]=null;
   PortraitCount=count;
   for(int i=0;i<slots;i++){
    var image=Portraits[i];if(!image)continue;bool on=i<count;
    if(image.gameObject.activeSelf!=on)image.gameObject.SetActive(on);if(!on)continue;
    if(image.sprite!=shownSprites[i])image.sprite=shownSprites[i];
    if(!ArrangePortraits)continue;
    float size=Pick(PortraitSizes,count,60),gap=Pick(PortraitSpacing,count,0);
    var r=image.rectTransform;var pos=new Vector2((i-(count-1)*.5f)*gap,PortraitLift);var dim=new Vector2(size,size);
    if(r.anchoredPosition!=pos)r.anchoredPosition=pos;if(r.sizeDelta!=dim)r.sizeDelta=dim;
   }
  }
  static float Pick(float[] values,int count,float fallback)=>values==null||values.Length==0?fallback:values[Mathf.Clamp(count-1,0,values.Length-1)];

  // The pop/pulse multiplies the prefab's own Visual scale; it is restored whenever the bubble hides.
  void Awake(){if(Visual&&!hasBase){var s=Visual.localScale;if(s.x!=0&&s.y!=0)baseScale=s;hasBase=true;}}
  void OnEnable(){Awake();popStart=Time.unscaledTime;if(Visual&&PopSeconds>0)Visual.localScale=Vector3.zero;}
  void OnDisable(){if(Visual&&hasBase)Visual.localScale=baseScale;}
  void Update(){
   if(!Visual)return;
   float t=PopSeconds<=0?1:Mathf.Clamp01((Time.unscaledTime-popStart)/PopSeconds);
   // Grow past full size, then settle (ease out), from the tail tip.
   float s=t<.7f?Mathf.SmoothStep(0,1+PopOvershoot,t/.7f):Mathf.SmoothStep(1+PopOvershoot,1,(t-.7f)/.3f);
   if(CurrentTone==Tone.Planned&&t>=1)s*=1+PulseScale*(.5f-.5f*Mathf.Cos((Time.unscaledTime-popStart-PopSeconds)*PulseSpeed));
   Visual.localScale=new Vector3(baseScale.x*s,baseScale.y*s,baseScale.z);
  }
 }
}
