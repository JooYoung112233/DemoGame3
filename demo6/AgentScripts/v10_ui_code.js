const fs=require('fs');const base='../topdown-v10-review-isolated/Assets/Scripts/Game/';
function edit(path,fn){const p=base+path;fs.writeFileSync(p,fn(fs.readFileSync(p,'utf8')))}
edit('Dungeon/UI/DungeonHud.cs',s=>{
s=s.replace('const float PanelWidth = 592f;','const float PanelWidth = 544f;').replace('const float PanelHeight = 74f;','const float PanelHeight = 112f;').replace('new Color(0.5f, 0.42f, 0.24f, 1f)','new Color32(0xD6, 0xB4, 0x48, 0xFF)');
s=s.replace('            // 이 화면에는 누르는 것이 없다. 그리기 사건에서만 그린다(배치 사건 반복을 줄인다).\n            if (Event.current.type != EventType.Repaint) return;','            // Painted action and utility buttons receive mouse events as well as repaint.');
const a=s.indexOf('        void DrawPanel('),b=s.indexOf('        /// <summary>\n        /// 경험치 막대',a);
s=s.slice(0,a)+String.raw`
        public static Rect ActionRect(int index) => new Rect(DungeonUi.Width * .5f - 162f + index * 66f, DungeonUi.Height - 96f, 60f, 60f);
        public static Rect UtilityRect(int index) => new Rect(DungeonUi.Width * .5f + 192f + index * 50f, DungeonUi.Height - 82f, 46f, 46f);
        public static Rect ExperienceRect => new Rect(16f, DungeonUi.Height - 12f, DungeonUi.Width - 32f, 7f);
        public static bool PointerOverHud
        {
            get
            {
                if (!Instance || !UnityEngine.InputSystem.Mouse.current) return false;
                var p = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                p = new Vector2(p.x, Screen.height - p.y) / DungeonUi.Scale;
                for (int i = 0; i < 5; i++) if (ActionRect(i).Contains(p)) return true;
                for (int i = 0; i < 3; i++) if (UtilityRect(i).Contains(p)) return true;
                return DungeonMiniMap.Visible && DungeonMiniMap.PanelRect.Contains(p);
            }
        }

        void DrawPanel(DungeonRoot root, PlayerController p)
        {
            var progress = PlayerProgress.Instance;
            var hp = p.Health;
            var health = new Rect(DungeonUi.Width * .5f - 192f, DungeonUi.Height - 130f, 384f, 25f);
            DungeonUi.Slot(health);
            DungeonUi.Bar(new Rect(health.x+8, health.y+6, health.width-16, 13), hp ? hp.Fraction : 0, DungeonUi.Blood);
            DungeonUi.ShadowLabel(new Rect(health.x,health.y,health.width,25), hp ? hp.Current+" / "+hp.Max : "-", DungeonUi.SmallCenter, DungeonUi.Bone);
            DungeonUi.ShadowLabel(new Rect(health.x-66,health.y,62,25), "Lv "+(progress ? progress.Level : 1), DungeonUi.SmallCenter, DungeonUi.BoneDim);
            DrawAction(p,0,"attack","LMB","기본 공격 · 좌클릭",0,0,!p.IsDown,p.Pose==PlayerPose.Attack,0);
            DrawAction(p,1,"whirl","RMB","회오리 · 우클릭",p.WhirlCooldown,p.WhirlCooldownMax,!p.IsDown,p.Pose==PlayerPose.Whirl,0);
            DrawAction(p,2,"wave","Q","검풍 · Q",p.WaveCooldown,p.WaveCooldownMax,!p.IsDown,p.Pose==PlayerPose.WaveCast,0);
            DrawAction(p,3,"dodge","Space","구르기 · Space",p.DodgeCooldown,p.DodgeCooldownMax,!p.IsDown,p.Pose==PlayerPose.Dodge,0);
            DrawAction(p,4,"potion","R","물약 · R",p.PotionCooldown,p.PotionCooldownMax,!p.IsDown&&p.Potions>0,false,p.Potions);
            DrawUtility(0,"bag","I","가방",Inventory.BagWindow,false);
            DrawUtility(1,"map","M","지도",BigMap.MapModal,false);
            DrawUtility(2,"skills","K","스킬 강화",SkillPanel.ModalName,progress&&progress.SkillPoints>0);
            float fraction = progress && progress.XpToNext>0 ? (float)progress.Xp/progress.XpToNext : 0;
            DrawXpBar(ExperienceRect,fraction,progress);
            DungeonUi.ShadowLabel(new Rect(18,DungeonUi.Height-33,240,19),"경험치 · Lv "+(progress ? progress.Level : 1),DungeonUi.Small,DungeonUi.BoneDim);
            DungeonUi.ShadowLabel(new Rect(DungeonUi.Width-220,DungeonUi.Height-33,202,19),progress ? progress.Xp+" / "+progress.XpToNext : "0 / 0",_smallRight,DungeonUi.BoneDim);
            var cell=root.CurrentCell;
            DungeonUi.ShadowLabel(new Rect(Margin,Margin,390,26),"제"+root.Floor+"층 · "+(cell!=null?cell.Name:"-"),DungeonUi.Small,DungeonUi.BoneDim);
            if (!DungeonUi.ModalOpen && Event.current.type==EventType.Repaint && !string.IsNullOrEmpty(GUI.tooltip))
            {
                var tip=new Rect(DungeonUi.Width*.5f-210,DungeonUi.Height-176,420,31);
                DungeonUi.Box(tip,.94f);DungeonUi.ShadowLabel(tip,GUI.tooltip,DungeonUi.SmallCenter,DungeonUi.Bone);
            }
        }

        void DrawAction(PlayerController player,int index,string icon,string key,string label,float remaining,float maximum,bool usable,bool active,int charges)
        {
            var r=ActionRect(index);bool cooling=remaining>0;bool previous=GUI.enabled;
            GUI.enabled=previous&&usable&&!cooling&&!DungeonUi.ModalOpen&&!TimeScaleService.Paused;
            bool click=GUI.Button(r,new GUIContent("",label+" · 클릭으로도 사용"),GUIStyle.none);
            GUI.enabled=previous;
            DungeonHudArt.Draw(r,icon,usable?Color.white:new Color(.48f,.48f,.48f,1));
            if (active) UiSkinArt.Selection(r);
            else if (!DungeonUi.ModalOpen&&r.Contains(Event.current.mousePosition)&&usable&&!cooling)
                DungeonUi.Outline(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),DungeonUi.Ember);
            if(cooling)
            {
                float fraction=maximum>0?Mathf.Clamp01(remaining/maximum):1;
                DungeonUi.Fill(new Rect(r.x+4,r.y+4,r.width-8,(r.height-8)*fraction),new Color(0,0,0,.76f));
                DungeonUi.ShadowLabel(new Rect(r.x,r.y+15,r.width,24),remaining>=10?Mathf.CeilToInt(remaining).ToString():remaining.ToString("0.0"),DungeonUi.Center,DungeonUi.Bone);
            }
            if(index==4)DungeonUi.ShadowLabel(new Rect(r.xMax-24,r.y+3,20,22),charges.ToString(),DungeonUi.SmallCenter,DungeonUi.Bone);
            var keyRect=new Rect(r.x+9,r.yMax-7,r.width-18,19);
            DungeonUi.Fill(keyRect,new Color(.035f,.031f,.025f,.97f));
            DungeonUi.Outline(keyRect,DungeonUi.IronEdge);
            DungeonUi.ShadowLabel(keyRect,key,DungeonUi.KeyLabel,DungeonUi.Bone);
            if(click)player.GetComponent<PlayerInputReader>()?.QueueHudAction(index);
        }

        static void DrawUtility(int index,string icon,string key,string label,string modal,bool notify)
        {
            var r=UtilityRect(index);bool previous=GUI.enabled;GUI.enabled=previous&&!DungeonUi.ModalOpen;
            bool click=GUI.Button(r,new GUIContent("",label+" · "+key),GUIStyle.none);GUI.enabled=previous;
            DungeonHudArt.Draw(r,icon,Color.white);
            DungeonUi.ShadowLabel(new Rect(r.x,r.yMax+1,r.width,19),key,DungeonUi.KeyLabel,DungeonUi.BoneDim);
            if(notify){DungeonUi.Fill(new Rect(r.xMax-9,r.y+3,6,6),DungeonUi.Ember);}
            if(click)DungeonUi.TryOpen(modal);
        }

`+s.slice(b);return s;});
edit('Dungeon/UI/DungeonUi.cs',s=>s.replace('public static GUIStyle Small { get; private set; }','public static GUIStyle Small { get; private set; }\n        public static GUIStyle SmallCenter { get; private set; }\n        public static GUIStyle KeyLabel { get; private set; }').replace('            _ready = true;','            _ready = true;').replace('Small = new GUIStyle', 'Small = new GUIStyle'));
// Insert small styles immediately after the existing small style is configured in EnsureSkin.
edit('Dungeon/UI/DungeonUi.cs',s=>{let i=s.indexOf('            Bold =');return s.slice(0,i)+'            SmallCenter = new GUIStyle(Small) { alignment=TextAnchor.MiddleCenter, wordWrap=false, fontSize=14 };\n            KeyLabel = new GUIStyle(SmallCenter) { fontSize=13 };\n'+s.slice(i)});
// Input queues use the existing action buffers and gameplay paths; aim stays at the last world pointer when clicking HUD.
edit('Player/PlayerInputReader.cs',s=>{
s=s.replace('public Vector2 PointerScreen => _point.ReadValue<Vector2>();','Vector2 _lastWorldPointer;\n        int _hudActions;\n        bool _hudAttackThisFrame;\n        public Vector2 PointerScreen => DungeonHud.PointerOverHud ? _lastWorldPointer : _point.ReadValue<Vector2>();\n        public void QueueHudAction(int index)\n        {\n            if(index>=0&&index<5&&!Blocked&&!CombatLocked&&!TimeScaleService.Paused)_hudActions |= 1 << index;\n        }');
s=s.replace('public bool AttackHeld => _attack.IsPressed() && !OverPanel && !Blocked && !CombatLocked;','public bool AttackHeld => ((_attack.IsPressed() && !OverPanel) || _hudAttackThisFrame) && !Blocked && !CombatLocked;');
s=s.replace('static bool OverPanel => CombatHud.PointerOverPanel','static bool OverPanel => DungeonHud.PointerOverHud || CombatHud.PointerOverPanel');
s=s.replace('        void Update()\n        {','        void Update()\n        {\n            int requested = _hudActions; _hudActions = 0; _hudAttackThisFrame = false;\n            if (!DungeonHud.PointerOverHud) _lastWorldPointer = _point.ReadValue<Vector2>();');
s=s.replace('            bool overPanel = OverPanel;','            bool overPanel = OverPanel;\n            if(!Blocked)\n            {\n                _hudAttackThisFrame = (requested & 1) != 0;\n                if((requested & 2) != 0)_skill1At = Time.time;\n                if((requested & 4) != 0)_skill2At = Time.time;\n                if((requested & 8) != 0)_dodgeAt = Time.time;\n            }');
s=s.replace('PotionPressed = _potion.WasPressedThisFrame();','PotionPressed = _potion.WasPressedThisFrame() || (!Blocked && (requested & 16) != 0);');return s;});
console.log('Isolated HUD, XP and input routes updated');
