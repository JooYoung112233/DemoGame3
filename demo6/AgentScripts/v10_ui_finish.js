const fs=require('fs');const base='../topdown-v10-review-isolated/Assets/Scripts/Game/';
function edit(p,fn){p=base+p;fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8')))}
edit('Dungeon/UI/DungeonHud.cs',s=>s.replace('!UnityEngine.InputSystem.Mouse.current','UnityEngine.InputSystem.Mouse.current == null').replace('            Instance = this;','            Instance = this;\n            if(!GetComponent<DungeonMiniMap>())gameObject.AddComponent<DungeonMiniMap>();'));
edit('Dungeon/UI/BigMap.cs',s=>s.replace('bool EdgeVisible(DungeonEdge e) => RevealAll || e.Kind != EdgeKind.Plank || e.Opened;', 'public static bool RouteKnown(DungeonEdge e) => e != null && (e.Kind != EdgeKind.Plank || e.Opened);\n        bool EdgeVisible(DungeonEdge e) => RevealAll || RouteKnown(e);\n        public static bool KnownFrontier(DungeonCell c)\n        {\n            if(c==null||c.Visited)return false;\n            foreach(var edge in c.Edges)if(RouteKnown(edge)&&edge.Other(c).Visited)return true;\n            return false;\n        }'));
// Shared frames are nine-sliced, so main windows and controls do not stretch their corner artwork.
function find(d,n){for(const e of fs.readdirSync(d,{withFileTypes:true})){const p=d+'/'+e.name;if(e.isDirectory()){const f=find(p,n);if(f)return f}else if(e.name===n)return p}}
const p=find(base,'UiSkinArt.cs');let s=fs.readFileSync(p,'utf8').replace('Resources.Load<Texture2D>("UI/Skin/panel")','Resources.Load<Texture2D>("UI/V10/frame") ?? Resources.Load<Texture2D>("UI/Skin/panel")').replace('Resources.Load<Texture2D>("UI/Skin/slot")','Resources.Load<Texture2D>("UI/V10/frame") ?? Resources.Load<Texture2D>("UI/Skin/slot")').replace('var source = Resources.Load<Texture2D>("UI/Skin/button");','var source = Resources.Load<Texture2D>("UI/V10/frame") ?? Resources.Load<Texture2D>("UI/Skin/button");');fs.writeFileSync(p,s);
edit('Dungeon/UI/ExplorationLog.cs',s=>{
s=s.replace('fontSize = 15, wordWrap = false','fontSize = 15, wordWrap = true').replace('GUILayout.Width(124f)','GUILayout.Width((_panelRect.width - 64f) / 3f)');
let a=s.indexOf('        void DrawPanel('),b=s.indexOf('\n        void ',a+20);if(b<0)b=s.length;let block=s.slice(a,b);
block=block.replace('_scroll = GUILayout.BeginScrollView(_scroll);','_scroll = GUILayout.BeginScrollView(_scroll, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);\n            GUILayout.BeginVertical(GUILayout.Width(_panelRect.width - 56f));');
block=block.replace('GUILayout.EndScrollView();','GUILayout.EndVertical();\n            GUILayout.EndScrollView();');
block=block.replace('                GUILayout.BeginHorizontal();\n                vision.VisionOn','                vision.VisionOn').replace('                GUI.enabled = true;\n                GUILayout.EndHorizontal();','                GUI.enabled = true;');
return s.slice(0,a)+block+s.slice(b);});
console.log('Minimap visibility, shared frames and F1 vertical layout wired');
