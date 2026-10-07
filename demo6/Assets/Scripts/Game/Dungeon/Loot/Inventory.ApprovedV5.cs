using Demo6.Core.Loot;
using Demo6.Core.Stats;
using UnityEngine;

namespace Demo6.Game
{
    public sealed partial class Inventory
    {
        bool _v5AllStats;
        Vector2 _v5StatScroll;
        readonly EquipmentPortraitV5 _v5Portrait = new EquipmentPortraitV5();
        static readonly GearSlot[] V5Slots={GearSlot.Helm,GearSlot.Weapon,GearSlot.Gloves,GearSlot.Armor,GearSlot.Boots,GearSlot.Ring1,GearSlot.Ring2,GearSlot.Amulet};
        static readonly Rect[] V5SlotRects={new Rect(805,190,80,80),new Rect(640,300,80,80),new Rect(970,300,80,80),new Rect(705,453,80,80),new Rect(905,453,80,80),new Rect(737,570,56,56),new Rect(817,570,56,56),new Rect(897,570,56,56)};
        static Rect V45Face(Rect r)=>new Rect(r.x+4,r.y+4,r.width-8,r.height-8);

        void DrawApprovedBagV5()
        {
            var originalMatrix=GUI.matrix;var originalColor=GUI.color;var originalEnabled=GUI.enabled;
            DungeonUi.Begin();
            float scale=Mathf.Min(DungeonUi.Width/1920f,DungeonUi.Height/1080f);
            GUI.matrix=GUI.matrix*Matrix4x4.TRS(new Vector3((DungeonUi.Width-1920*scale)*.5f,(DungeonUi.Height-1080*scale)*.5f,0),Quaternion.identity,new Vector3(scale,scale,1));
            try
            {
                using(var buttons=new ApprovedUiV5.ButtonScope())
                {
                    BeginInventoryPointer();
                    if(_statValue==null)_statValue=new GUIStyle(V39Style(20,TextAnchor.MiddleRight,true));
                    if(_statChange==null)_statChange=new GUIStyle(V39Style(18,TextAnchor.MiddleRight));
                    DungeonUi.Fill(new Rect(0,0,1920,1080),new Color(0,0,0,.52f));
                    ApprovedUiV5.Round(new Rect(100,70,1720,940),new Color32(15,14,15,252),16,new Color32(6,6,7,255),5);
                    ApprovedUiV5.Round(new Rect(107,77,1706,926),new Color32(27,24,23,252),12,ApprovedUiV5.Edge,2);
                    ApprovedUiV5.Round(new Rect(112,82,1696,77),new Color32(58,28,33,255),9,new Color32(108,74,55,255),1);
                    ApprovedUiV5.TitleLabel(new Rect(142,94,460,49),"장비와 가방",35,ApprovedUiV5.Gold,TextAnchor.MiddleLeft);
                    V39Label(new Rect(530,104,620,32),"더블클릭 또는 장비 칸으로 드래그해 장착",19,ApprovedUiV5.Muted,TextAnchor.MiddleLeft);
                    var state=DungeonRoot.Instance?DungeonRoot.Instance.State:null;
                    if(state!=null)V39Label(new Rect(1220,105,460,32),"강화석 "+state.Stones+"    골드 "+state.Gold,20,ApprovedUiV5.Gold,TextAnchor.MiddleRight);
                    if(ApprovedUiV5.Close(new Rect(1732,93,56,52))){CancelInventoryPointer();DungeonUi.Close(BagWindow);}
                    V5Panel(new Rect(133,179,372,466));V5Panel(new Rect(521,179,594,466));V5Panel(new Rect(1140,179,647,786),true);
                    V39Label(new Rect(155,194,230,35),"능력치",23,null,TextAnchor.MiddleLeft,true);
                    DrawV45CharacterStats(new Rect(155,245,326,375));
                    V39Label(new Rect(543,194,180,34),"장착 장비",23,null,TextAnchor.MiddleLeft,true);
                    if(_selected!=null&&!Equipment.IsEquipped(_selected)&&!_bag.Contains(_selected))_selected=null;
                    if(_selected==null)_selected=Equipped;
                    ApprovedUiV5.Round(new Rect(740,277,210,210),new Color32(18,17,17,210),75,new Color32(76,61,44,150),1);
                    _v5Portrait.Draw(new Rect(740,258,210,246),_player);
                    for(int i=0;i<V5Slots.Length;i++)
                    {
                        var slot=V5Slots[i];var at=V5SlotRects[i];var item=Equipment[slot];
                        DrawApprovedItemSlot(at,item,item!=null&&ReferenceEquals(item,_selected),true,slot);
                        V39Label(new Rect(at.center.x-65,at.yMax+4,130,24),GearSlots.SlotName(slot),i<5?18:16,ApprovedUiV5.Muted,TextAnchor.MiddleCenter);
                    }
                    // Only the eight real loadout slots are exposed. Deferred back equipment has no target.
                    V39Label(new Rect(140,663,280,34),"가방",24,null,TextAnchor.MiddleLeft,true);
                    V39Label(new Rect(745,664,353,32),_bag.Count+" / "+BagCapacity+"칸",20,_bag.Count>BagCapacity?DownColor:ApprovedUiV5.Muted,TextAnchor.MiddleRight);
                    const float strideX=94,strideY=100,cell=84;
                    int total=Mathf.Max(BagCapacity,_bag.Count),rows=Mathf.CeilToInt(total/10f);
                    var view=new Rect(137,708,978,212);
                    SetInventoryPointerClip(view);
                    _scroll=GUI.BeginScrollView(view,_scroll,new Rect(0,0,944,rows*strideY-8),false,false);
                    for(int i=0;i<total;i++)
                    {
                        var item=i<_bag.Count?_bag[i]:null;var at=new Rect(3+(i%10)*strideX,3+(i/10)*strideY,cell,cell);
                        DrawApprovedItemSlot(at,item,item!=null&&ReferenceEquals(_selected,item),true);
                    }
                    GUI.EndScrollView();_pointerClipped=false;
                    V39Label(new Rect(140,930,380,30),"빈 칸 "+Mathf.Max(0,BagCapacity-_bag.Count)+"개",18,ApprovedUiV5.Muted);
                    V39RunePouch(new Rect(540,930,560,30));
                    var card=_selected!=null?CardFor(_selected):null;
                    if(card!=null)DrawApprovedDetailsV5(card);
                    V39Label(new Rect(140,973,1120,27),"클릭 상세 · 더블클릭 장착 · 장비 칸으로 드래그 · 우클릭 취소",18,ApprovedUiV5.Muted,TextAnchor.MiddleLeft);
                    V39Label(new Rect(1500,973,280,27),"I 가방 · Esc 닫기",18,ApprovedUiV5.Muted,TextAnchor.MiddleRight);
                    EndInventoryPointer();
                }
            }
            finally{GUI.matrix=originalMatrix;GUI.color=originalColor;GUI.enabled=originalEnabled;}
        }
        static void V5Panel(Rect r,bool detail=false)=>ApprovedUiV5.Round(r,detail?new Color32(35,30,27,255):new Color32(21,20,20,235),8,new Color32(76,64,49,230),1);
        void DrawV45CharacterStats(Rect area)
        {
            var sheet=Sheet??Compute(Equipment);
            for(int i=0;i<StatSheet.KindCount;i++)
            {
                var line=new Rect(area.x,area.y+i*26,area.width,26);
                V39Label(new Rect(line.x,line.y,213,line.height),StatNames[i],18,ApprovedUiV5.Muted,TextAnchor.MiddleLeft);
                V39Label(new Rect(line.xMax-106,line.y,106,line.height),StatValue((StatKind)i,sheet.Get((StatKind)i)),19,ApprovedUiV5.Light,TextAnchor.MiddleRight,true);
            }
            V39Label(new Rect(area.x,area.y+346,area.width,28),"초당 휘두르기   "+StatCalc.SwingsPerSecond(sheet.WeaponRule,sheet.AttackSpeedPermille).ToString("0.00"),18,ApprovedUiV5.Gold,TextAnchor.MiddleRight);
        }
        bool DrawApprovedItemSlot(Rect r,GearItem item,bool selected,bool interactive=false,GearSlot? slot=null)
        {
            ApprovedUiV5.Round(r,new Color32(9,9,10,255),7,new Color32(4,4,5,255),3);
            var face=V45Face(r);
            ApprovedUiV5.Round(face,new Color32(30,29,28,255),5,new Color32(93,77,54,255),1);
            if(item!=null)
            {
                var grade=LootVisuals.GradeColor(item.Grade);
                ApprovedUiV5.Round(face,new Color(grade.r,grade.g,grade.b,.13f),5,new Color(grade.r,grade.g,grade.b,.8f),1.5f);
                float footer=Mathf.Min(11,r.height*.16f);
                V39Icon(new Rect(face.x+3,face.y+3,face.width-6,face.height-footer-5),item.Base.IconId,item.Grade);
                V39Marks(face,item);
            }
            else if(slot.HasValue)V39Label(face,GearSlots.SlotName(slot.Value),13,new Color(.43f,.4f,.35f),TextAnchor.MiddleCenter);
            if(selected)ApprovedUiV5.Round(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),Color.clear,8,ApprovedUiV5.Gold,2);
            if(interactive)RegisterInventorySlot(r,item,slot);
            return false;
        }
        void V45ComparisonTile(Rect r,string label,GearItem item)
        {
            ApprovedUiV5.Round(r,new Color32(22,21,21,255),7,ApprovedUiV5.Edge,1);
            V39Label(new Rect(r.x+12,r.y+8,r.width-24,26),label,18,ApprovedUiV5.Muted,TextAnchor.MiddleLeft,true);
            if(item==null){V39Label(new Rect(r.x+15,r.y+66,r.width-30,45),"빈 장비 칸",20,ApprovedUiV5.Muted,TextAnchor.MiddleCenter);return;}
            DrawApprovedItemSlot(new Rect(r.x+12,r.y+45,78,78),item,false);
            V39Label(new Rect(r.x+101,r.y+45,r.width-113,78),item.DisplayName,20,LootVisuals.GradeColor(item.Grade),TextAnchor.UpperLeft,true,true);
            V39Label(new Rect(r.x+12,r.y+130,r.width-24,25),"레벨 "+item.ItemLevel+"  ·  +"+item.Enhance,16,ApprovedUiV5.Muted);
            V39Label(new Rect(r.x+12,r.y+160,r.width-24,45),BaseStatText(item),18,ApprovedUiV5.Light,TextAnchor.UpperLeft,false,true);
        }
        void DrawApprovedDetailsV5(Card card)
        {
            var item=card.Item;var before=Sheet??Compute(Equipment);var after=card.G!=null?card.G.After:null;
            V39Label(new Rect(1163,194,594,34),_v5AllStats?"능력치 비교":"아이템 비교",23,null,TextAnchor.MiddleLeft,true);
            V45ComparisonTile(new Rect(1163,244,289,213),"선택 아이템",item);
            V45ComparisonTile(new Rect(1467,244,296,213),"현재 장착",Equipment[card.G!=null?card.G.Slot:card.EquippedSlot??EquipSlotFor(item)]);
            if(_v5AllStats)
            {
                _v5StatScroll=GUI.BeginScrollView(new Rect(1163,476,602,288),_v5StatScroll,new Rect(0,0,580,28f*(StatSheet.KindCount+1)),false,false);
                V39StatTable(new Rect(0,0,580,28f*(StatSheet.KindCount+1)),before,after);GUI.EndScrollView();
            }
            else
            {
                _detailScroll=GUI.BeginScrollView(new Rect(1163,476,602,288),_detailScroll,new Rect(0,0,580,card.Lines.Count*28f+8),false,false);
                for(int i=0;i<card.Lines.Count;i++){var line=card.Lines[i];GUI.color=line.Color;DungeonUi.CompactLabel(new Rect(0,i*28,580,28),line.Text,V39Style(21,TextAnchor.MiddleLeft));}
                GUI.color=Color.white;GUI.EndScrollView();
            }
            bool enabled=GUI.enabled;GUI.enabled=enabled&&_pointerItem==null;
            DrawDetailButtons(new Rect(1163,790,600,49),card);
            DrawRuneButtons(new Rect(1163,851,600,35),item);
            if(ApprovedUiV5.Button(new Rect(1163,906,600,37),_v5AllStats?"아이템 상세":"능력치 전체 비교"))_v5AllStats=!_v5AllStats;
            GUI.enabled=enabled;GUI.color=Color.white;
        }
    }
}
