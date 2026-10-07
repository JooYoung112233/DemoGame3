const fs=require('fs');
function edit(p,fn){const before=fs.readFileSync(p,'utf8');const after=fn(before);if(after===before)throw Error('No changes: '+p);fs.writeFileSync(p,after);}
edit('Assets/Scripts/Game/Dungeon/UI/DungeonUi.cs',s=>{
 s=s.replace('b.border = new RectOffset(3, 3, 3, 3);','b.border = new RectOffset(16, 16, 16, 16);');
 const start=s.indexOf('        /// <summary>검은 쇠 판:');const end=s.indexOf('        /// <summary>정보 카드',start);
 if(start<0||end<0)throw Error('Box bounds');
 s=s.slice(0,start)+`        /// <summary>칠한 철제 테와 가죽 바탕. 큰 창은 32px 모서리, 작은 카드는 높이에 맞춰 얇게 쓴다.</summary>
        public static void Box(Rect r, float alpha = 0.82f)
        {
            if (alpha <= 0.002f) return;
            float corner = Mathf.Min(32f, r.height * .18f);
            Fill(r, new Color(.018f,.018f,.021f,alpha));
            UiSkinArt.NineSlice(r, UiSkinArt.Panel, corner, new Color(.74f,.73f,.71f,alpha), .22f);
        }

`+s.slice(end);
 s=s.replace('            Fill(rect, new Color(0.075f, 0.08f, 0.09f, 1f));\n            Outline(rect, selected ? Ember : IronEdge);','            Fill(rect, new Color(.028f, .026f, .025f, 1f));\n            UiSkinArt.NineSlice(rect, UiSkinArt.Socket, Mathf.Min(14f,rect.height*.1f), Color.white);\n            if(selected) UiSkinArt.Selection(rect);');
 s=s.replace('if (!_btnNormal) _btnNormal = BuildButton','if (!_btnNormal) _btnNormal = UiSkinArt.Button(.88f) ?? BuildButton');
 s=s.replace('if (!_btnHover) _btnHover = BuildButton','if (!_btnHover) _btnHover = UiSkinArt.Button(1.4f) ?? BuildButton');
 s=s.replace('if (!_btnActive) _btnActive = BuildButton','if (!_btnActive) _btnActive = UiSkinArt.Button(.60f) ?? BuildButton');
 s=s.replace('그림(구슬·띠·비네트·단추)은 처음 한 번 절차 텍스처로 만들고 다시 쓴다(매 프레임 할당 없음).','철제·가죽 그림은 UI/Skin PNG를 사용한다. 구슬·띠·비네트와 버튼 상태는 처음 한 번 만들고 재사용한다.');
 return s;
});
edit('Assets/Scripts/Game/Dungeon/Loot/Inventory.cs',s=>{
 s=s.replace('GUI.Label(new Rect(r.x+24,r.y+16,520,36), "장비와 가방", DungeonUi.Title);','GUI.Label(new Rect(r.x+32,r.y+18,520,36), "장비와 가방", DungeonUi.Title);\n            DungeonUi.Strip(new Rect(r.x+28,r.y+59,r.width-56,1),new Color(.61f,.53f,.39f,.55f));');
 s=s.replace('ItemIconArt.Draw(new Rect(r.x+202,r.y+64,178,202), "figure");','GUI.color=new Color(.69f,.64f,.55f,.65f);\n            GUI.DrawTexture(new Rect(r.x+194,r.y+70,206,206),ItemIconArt.Get("figure"),ScaleMode.ScaleToFit,true);\n            GUI.color=Color.white;');
 s=s.replace('new Rect(artRect.x+2,artRect.y+2,artRect.width-4,artRect.height-4)','new Rect(artRect.x+9,artRect.y+9,artRect.width-18,artRect.height-18)');
 s=s.replace('DungeonUi.Outline(artRect,color,2f);','DungeonUi.Outline(new Rect(artRect.x+8,artRect.y+8,artRect.width-16,artRect.height-16),new Color(color.r,color.g,color.b,.68f),1f);');
 s=s.replace('bool clicked=GUI.Button(rect,new GUIContent("",item.DisplayName));','bool clicked=GUI.Button(rect,new GUIContent("",item.DisplayName),GUIStyle.none);\n            DungeonUi.Slot(rect);\n            if(rect.Contains(Event.current.mousePosition))DungeonUi.Fill(new Rect(rect.x+8,rect.y+8,rect.width-16,rect.height-16),new Color(1,1,1,.06f));');
 s=s.replace('new Rect(rect.x+3,rect.y+3,rect.width-6,rect.height-6)','new Rect(rect.x+7,rect.y+7,rect.width-14,rect.height-14)');
 s=s.replace('new Rect(rect.x+8,rect.y+5,rect.width-16,rect.height-18)','new Rect(rect.x+6,rect.y+4,rect.width-12,rect.height-18)');
 s=s.replace('DungeonUi.Outline(rect,LootVisuals.GradeColor(item.Grade),2f);','DungeonUi.Outline(new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10),new Color(gradeColor.r,gradeColor.g,gradeColor.b,.85f),1f);');
 s=s.replace('if(selected)DungeonUi.Outline(new Rect(rect.x+4,rect.y+4,rect.width-8,rect.height-8),DungeonUi.Bone,2f);','if(selected)UiSkinArt.Selection(rect);');
 s=s.replace('rect.x+10+i*9,rect.yMax-10','rect.x+12+i*9,rect.yMax-13');
 return s;
});
edit('Assets/Scripts/Game/Art/UI/ItemIconArt.cs',s=>s.replace('인벤토리 전용 자체 아이콘. 등급은 슬롯 테두리/이름이 표시한다. 원본은 1024px 직접 래스터화, 게임 PNG는 512px.','인벤토리 전용 자체 아이콘. 현재 그림 원본은 UI-정리-v2의 1254px PNG/Aseprite, 게임 PNG는 512px. 등급은 슬롯·이름이 표시한다. Build는 자산 누락 시 v1 대체 그림만 만든다.'));
console.log('Updated shared skin, inventory visuals and art source comment');
