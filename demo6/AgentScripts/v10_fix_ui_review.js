const fs=require('fs');const base='../topdown-v10-review-isolated/Assets/Scripts/Game/';
function edit(p,fn){p=base+p;fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8')))}
edit('Dungeon/UI/DungeonUi.cs',s=>s.replace('b.border = new RectOffset(16, 16, 16, 16);','b.border = new RectOffset(8, 8, 8, 8);'));
function find(d,n){for(const e of fs.readdirSync(d,{withFileTypes:true})){const p=d+'/'+e.name;if(e.isDirectory()){const f=find(p,n);if(f)return f}else if(e.name===n)return p}}
const p=find(base,'UiSkinArt.cs');let s=fs.readFileSync(p,'utf8').replace('var source = Resources.Load<Texture2D>("UI/V10/frame")','var source = Resources.Load<Texture2D>("UI/V10/button")');fs.writeFileSync(p,s);
edit('Dungeon/UI/ExplorationLog.cs',s=>{
s=s.replace('new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = true }','new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true, padding = new RectOffset(5,5,4,4), margin=new RectOffset(2,2,3,3) }');
s=s.replace('GUILayout.Height(24f)','GUILayout.Height(30f)');
s=s.replace('GUILayout.Label(line.text, DungeonUi.Small);','GUILayout.Label(line.text, DungeonUi.Small, GUILayout.MaxWidth(Instance && Instance.PanelVisible ? PanelWidth - 64f : 780f));');
const a=s.indexOf('        void DrawPanel'),b=s.indexOf('        void TeleportTo',a);let part=s.slice(a,b);
part=part.replace(/GUILayout\.Label\(([^\n]+), DungeonUi\.Small\);/g,'GUILayout.Label($1, DungeonUi.Small, GUILayout.MaxWidth(PanelWidth - 64f));');
// Narrow debug buttons always wrap; grouped controls keep a bounded shared width.
part=part.replace('GUILayout.BeginVertical(GUILayout.Width(_panelRect.width - 56f));','GUILayout.BeginVertical(GUILayout.Width(_panelRect.width - 64f));');
return s.slice(0,a)+part+s.slice(b);});
// Keep the same action imagery in the upgrade list.
edit('Dungeon/Progression/SkillPanel.cs',s=>s.replace('            Vector2 P(float x,float y)=>', '            if(kind==0||kind==1||kind==2||kind==4){DungeonHudArt.Draw(r,kind==0?"whirl":kind==1?"wave":kind==2?"attack":"dodge",Color.white);return;}\n            Vector2 P(float x,float y)=>'));
let test=fs.readFileSync('AgentScripts/V10UiChecks.cs','utf8');
test=test.replace('input.Buttons=1;await Task.Delay(100);input.Buttons=0;',String.raw`input.Buttons=1;QueueClick(EventType.MouseDown,r.center*DungeonUi.Scale);await Task.Delay(100);input.Buttons=0;QueueClick(EventType.MouseUp,r.center*DungeonUi.Scale);`);
test=test.replace(' static async Task Click(',String.raw` static void QueueClick(EventType type,Vector2 at){var method=typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(Event)},null);method.Invoke(null,new object[]{new Event{type=type,mousePosition=at,button=0}});}
 static async Task Click(`);
fs.writeFileSync('AgentScripts/V10UiChecks.cs',test);console.log('Button nine-slice, F1 wrapping, shared skill imagery and click verification updated');
