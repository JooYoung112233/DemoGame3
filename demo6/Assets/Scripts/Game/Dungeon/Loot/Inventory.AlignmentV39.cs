using System.Collections.Generic;
using Demo6.Core.Loot;
using Demo6.Core.Stats;
using UnityEngine;
namespace Demo6.Game
{
    public sealed partial class Inventory
    {
        // Alpha bounds measured from the unchanged approved and legacy PNGs.
        static readonly Dictionary<string,Rect> V39IconUvs=new Dictionary<string,Rect>
        {
            {"amu_amber",new Rect(0.25195313f,0.03125000f,0.49804688f,0.95507813f)},
            {"amu_charm",new Rect(0.12304688f,0.02734375f,0.76171875f,0.94140625f)},
            {"amu_fang",new Rect(0.18750000f,0.08723958f,0.62500000f,0.79557292f)},
            {"arm_chain",new Rect(0.07682292f,0.08723958f,0.84635417f,0.77473958f)},
            {"arm_leather",new Rect(0.07682292f,0.08723958f,0.84635417f,0.77473958f)},
            {"arm_plate",new Rect(0.07682292f,0.08723958f,0.84635417f,0.77473958f)},
            {"bts_chain",new Rect(0.12500000f,0.04687500f,0.84179688f,0.90625000f)},
            {"bts_leather",new Rect(0.08203125f,0.06445313f,0.89843750f,0.87695313f)},
            {"bts_plate",new Rect(0.10286458f,0.11197917f,0.72916667f,0.72005208f)},
            {"empty_amulet",new Rect(0.26757813f,0.11132813f,0.46484375f,0.69531250f)},
            {"empty_armor",new Rect(0.12500000f,0.17382813f,0.75000000f,0.65820313f)},
            {"empty_boots",new Rect(0.27734375f,0.20117188f,0.52148438f,0.60156250f)},
            {"empty_gloves",new Rect(0.23437500f,0.21289063f,0.46289063f,0.55859375f)},
            {"empty_helm",new Rect(0.23828125f,0.21484375f,0.52343750f,0.55664063f)},
            {"empty_ring",new Rect(0.25390625f,0.22851563f,0.49218750f,0.61328125f)},
            {"empty_weapon",new Rect(0.24023438f,0.16601563f,0.52929688f,0.67968750f)},
            {"figure",new Rect(0.30468750f,0.00390625f,0.39062500f,0.99414063f)},
            {"glv_chain",new Rect(0.04687500f,0.04101563f,0.91601563f,0.91210938f)},
            {"glv_leather",new Rect(0.03906250f,0.03710938f,0.92773438f,0.91796875f)},
            {"glv_plate",new Rect(0.12760417f,0.14192708f,0.71223958f,0.67838542f)},
            {"gold",new Rect(0.02734375f,0.09179688f,0.94726563f,0.81250000f)},
            {"hlm_chain",new Rect(0.02539063f,0.01953125f,0.95117188f,0.95507813f)},
            {"hlm_leather",new Rect(0.16796875f,0.01757813f,0.72851563f,0.95898438f)},
            {"hlm_plate",new Rect(0.12760417f,0.11718750f,0.74479167f,0.77604167f)},
            {"key",new Rect(0.01367188f,0.01757813f,0.97851563f,0.96093750f)},
            {"pickaxe",new Rect(0.03320313f,0.01757813f,0.95703125f,0.97070313f)},
            {"potion",new Rect(0.18750000f,0.08723958f,0.62500000f,0.78515625f)},
            {"rng_blood",new Rect(0.19791667f,0.10677083f,0.60416667f,0.76562500f)},
            {"rng_fang",new Rect(0.08398438f,0.08203125f,0.85351563f,0.85742188f)},
            {"rng_iron",new Rect(0.09375000f,0.06640625f,0.86132813f,0.85937500f)},
            {"stone",new Rect(0.08007813f,0.03320313f,0.88476563f,0.92578125f)},
            {"wpn_greatsword",new Rect(0.19791667f,0.07031250f,0.64453125f,0.84765625f)},
            {"wpn_longsword",new Rect(0.11718750f,0.07031250f,0.72526042f,0.84765625f)},
            {"wpn_twinblades",new Rect(0.20703125f,0.11588542f,0.70703125f,0.80859375f)},
        };
        // Reviewable per-icon optical anchors, measured from the unchanged source pixels.
        // Weighted alpha mass: alpha * (0.75 + 0.25 * normalized luminance).
        // This is a positioning proxy; runtime visual review remains required.
        static readonly Dictionary<string,Vector2> V40OpticalCenters=new Dictionary<string,Vector2>
        {
            {"wpn_longsword",new Vector2(0.45238051f,0.48158506f)},
            {"wpn_greatsword",new Vector2(0.51893736f,0.48841279f)},
            {"wpn_twinblades",new Vector2(0.51301440f,0.50022320f)},
            {"arm_leather",new Vector2(0.49984241f,0.45477179f)},
            {"arm_chain",new Vector2(0.49975630f,0.45461472f)},
            {"arm_plate",new Vector2(0.49982436f,0.45474212f)},
            {"hlm_leather",new Vector2(0.45827004f,0.49932962f)},
            {"hlm_chain",new Vector2(0.51078464f,0.56522643f)},
            {"hlm_plate",new Vector2(0.49789836f,0.50869845f)},
            {"glv_leather",new Vector2(0.51277540f,0.51199977f)},
            {"glv_chain",new Vector2(0.51067846f,0.51417684f)},
            {"glv_plate",new Vector2(0.50560936f,0.52339213f)},
            {"bts_leather",new Vector2(0.43272430f,0.51775971f)},
            {"bts_chain",new Vector2(0.42817076f,0.51103738f)},
            {"bts_plate",new Vector2(0.50578712f,0.53649536f)},
            {"rng_iron",new Vector2(0.49631129f,0.48904953f)},
            {"rng_blood",new Vector2(0.49034250f,0.51355208f)},
            {"rng_fang",new Vector2(0.57710016f,0.45983383f)},
            {"amu_fang",new Vector2(0.50971253f,0.53284171f)},
            {"amu_charm",new Vector2(0.50162627f,0.52817530f)},
            {"amu_amber",new Vector2(0.51137500f,0.66659528f)},
            {"potion",new Vector2(0.49876981f,0.53734194f)},
        };
        static readonly Dictionary<int,GUIStyle> V39Styles=new Dictionary<int,GUIStyle>();
        static Rect V39Slot(float faceCenterX,float y,float size)=>new Rect(faceCenterX-size*67f/119f,y,size,size);
        static Rect V39Face(Rect r)=>new Rect(r.x+r.width*24f/119f,r.y+r.height*17f/118f,r.width*86f/119f,r.height*86f/118f);
        static void V39Frame(Rect r,bool selected)
        {
            ApprovedUiV5.Image(r,"action-frame",new Color(.78f,.78f,.78f,1));
            if(selected)ApprovedUiV5.Round(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),Color.clear,10,ApprovedUiV5.Gold,2);
        }
        static GUIStyle V39Style(int size=18,TextAnchor anchor=TextAnchor.UpperLeft,bool bold=false,bool wrap=false)
        {
            int key=size+((int)anchor<<8)+(bold?4096:0)+(wrap?8192:0);
            if(!V39Styles.TryGetValue(key,out var style))
            {
                style=new GUIStyle(ApprovedUiV5.Style(size,anchor,bold,wrap)){padding=new RectOffset(),margin=new RectOffset(),contentOffset=Vector2.zero};
                V39Styles[key]=style;
            }
            return style;
        }
        static void V39Label(Rect r,string text,int size=18,Color? color=null,TextAnchor anchor=TextAnchor.UpperLeft,bool bold=false,bool wrap=false)
        {
            var prev=GUI.color;GUI.color=color??ApprovedUiV5.Light;GUI.Label(r,text,V39Style(size,anchor,bold,wrap));GUI.color=prev;
        }
        static void V39Icon(Rect area,string id,Grade grade=Grade.Common)
        {
            if(EquipmentVisualV049.DrawIcon(area,id,grade))return;
            var texture=ApprovedUiV5.Texture(id);if(!texture)texture=ItemIconArt.Get(id);
            if(!texture)return;
            if(!V39IconUvs.TryGetValue(id,out var uv))uv=new Rect(0,0,1,1);
            float width=texture.width*uv.width,height=texture.height*uv.height;
            var anchor=V40OpticalCenters.TryGetValue(id,out var measured)?measured:new Vector2(.5f,.5f);
            float ax=width*anchor.x,ay=height*anchor.y;
            // Center the visible mass while fitting BOTH sides of the silhouette in the safe area.
            float scale=Mathf.Min(area.width/(2f*Mathf.Max(ax,width-ax)),area.height/(2f*Mathf.Max(ay,height-ay)));
            var dest=new Rect(area.center.x-ax*scale,area.center.y-ay*scale,width*scale,height*scale);
            var prev=GUI.color;GUI.color=new Color(1,1,1,prev.a);GUI.DrawTextureWithTexCoords(dest,texture,uv,true);GUI.color=prev;
        }
        static void V39Marks(Rect face,GearItem item)
        {
            float y=face.yMax-6f;
            for(int i=0;i<(int)item.Grade+1;i++)DungeonUi.Fill(new Rect(face.x+3+i*5,y-1.5f,3,3),ApprovedUiV5.Light);
            int count=item.SocketCount;
            float size=Mathf.Min(12,face.width*.20f);
            for(int i=0;i<count;i++)
            {
                bool filled=i<item.Runes.Count;
                var r=new Rect(face.xMax-3-size*(count-i),y-7,size,14);
                V39Label(r,filled?GearNaming.FilledSocket.ToString():GearNaming.EmptySocket.ToString(),12,filled?RuneLook.ColorOf(item.Runes[i]):SameColor,TextAnchor.MiddleCenter);
            }
        }
        void V39RunePouch(Rect r)
        {
            var parts=new List<string>();Color color=DungeonUi.BoneDim;
            foreach(var rune in RuneTable.All){int count=RuneCount(rune.Id);if(count<=0)continue;if(parts.Count==0)color=RuneLook.ColorOf(rune);parts.Add(rune.ShortName+" ×"+count);}
            var prev=GUI.color;GUI.color=color;DungeonUi.CompactLabel(r,"룬: "+(parts.Count>0?string.Join(" · ",parts):"없음"),V39Style(18));GUI.color=prev;
        }
        void V39StatTable(Rect area,StatSheet before,StatSheet after)
        {
            for(int i=0;i<StatSheet.KindCount;i++)
            {
                var kind=(StatKind)i;int now=before.Get(kind);int delta=after!=null?after.Get(kind)-now:0;
                V39StatRow(new Rect(area.x,area.y+i*28,area.width,28),StatNames[i],StatValue(kind,now),delta!=0?SignedStat(kind,delta)+" "+Arrow(delta):null,delta);
            }
            double s0=StatCalc.SwingsPerSecond(before.WeaponRule,before.AttackSpeedPermille);string swing=null;int direction=0;
            if(after!=null){double s1=StatCalc.SwingsPerSecond(after.WeaponRule,after.AttackSpeedPermille);if(System.Math.Abs(s1-s0)>.005){direction=s1>s0?1:-1;swing="→ "+s1.ToString("0.00")+" "+Arrow(direction);}}
            V39StatRow(new Rect(area.x,area.y+StatSheet.KindCount*28,area.width,28),"초당 휘두르기",s0.ToString("0.00"),swing,direction);
        }
        void V39StatRow(Rect r,string name,string value,string change,int direction)
        {
            V39Label(new Rect(r.x,r.y,234,r.height),name,20,DungeonUi.BoneDim,TextAnchor.MiddleLeft);
            var previous=GUI.color;
            GUI.color=Color.white;FitStat(_statValue,value,88,20);
            GUI.Label(new Rect(r.x+238,r.y,88,r.height),value,_statValue);
            if(change!=null)
            {
                GUI.color=direction>0?UpColor:DownColor;FitStat(_statChange,change,r.width-338,18);
                GUI.Label(new Rect(r.x+338,r.y,r.width-338,r.height),change,_statChange);
            }
            GUI.color=previous;
        }
    }
}
