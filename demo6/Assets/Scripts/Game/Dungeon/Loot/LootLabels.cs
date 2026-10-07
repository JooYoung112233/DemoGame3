using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 바닥 장비 이름표(디아블로식, 던전만): 내려앉은 장비(LootDrop.Gear, 7부위)마다 등급색 이름(등급 + 종류, "희귀 대검"·"고급 가죽 장화")을 빛기둥 아랫부분(바닥에서 0.8유닛 위)에 작은 검은 판으로 띄운다.
    /// 시야 다각형 안이고 플레이어 12유닛 안인 것만 보인다(빛기둥과 같은 공정 규칙: 안개 밑 장비를 글로 드러내지 않는다).
    /// 실제 F 대상을 이름표에 표시한다. 0.22초 의도적으로 가리킬 때만 상세 카드를 연다.
    /// 이름표끼리 겹치면 화면 안의 빈 행·열에 놓는다. 싸움 중에는 빛기둥처럼 옅게(60%) 낮춘다.
    /// 골드·강화석 작은 보상은 줍는 순간 글자(지금 그대로)라 여기서 다루지 않는다. 수치·드랍 확률은 건드리지 않는다.
    /// 이름 글·너비는 장비마다 처음 한 번 만들고 다시 쓴다(매 프레임 할당 없음).
    /// </summary>
    [DefaultExecutionOrder(-60)] // Claim a label press before PlayerInputReader (-50) observes attack input.
    public sealed class LootLabels : MonoBehaviour
    {
        /// <summary>플레이어에서 이 거리 안의 장비만 이름표를 띄운다.</summary>
        const float ShowRange = 12f;
        /// <summary>이름표 아래 끝: 바닥에서 위로(빛기둥 아랫부분, 획득 안내 1.1보다 낮게).</summary>
        const float LiftUnits = 0.8f;
        const float LabelHeight = 28f;
        const float LabelPadX = 10f;
        const float StackGap = 2f;
        const float FadeInSeconds = 0.2f;
        const float CombatAlpha = 0.6f;
        const float CleanupInterval = 1f;
        const int MaxLabels = 48;
        /// <summary>WorldOverlay(10) 아래에 깔아 피해 숫자가 이름표 위로 보이게 한다.</summary>
        const int GuiDepth = 11;

        static readonly Color PlateColor = new Color(0f, 0f, 0f, 0.72f);

        public static LootLabels Instance { get; private set; }

        sealed class Label
        {
            public string Text;
            public string PickupText;
            public Color Color;
            public float Width = -1f;
            public float SeenAt;
            /// <summary>가방이 가득일 때 F 대상 글('[F]' 회색 + ' · 가방 가득')과 그 너비(처음 쓸 때 잰다, 재화 쓸 곳 1차 5-4).</summary>
            public string FullText;
            public float FullWidth = -1f;
        }

        struct Slot
        {
            public LootDrop Drop;
            public bool Pickup;
            public Label Label;
            public Rect Rect;
            public float Alpha;
            public float Distance;
        }

        readonly Dictionary<LootDrop, Label> _labels = new Dictionary<LootDrop, Label>();
        readonly List<LootDrop> _stale = new List<LootDrop>();
        readonly Slot[] _slots = new Slot[MaxLabels];
        readonly GUIContent _measure = new GUIContent();
        int _count;
        float _nextCleanup;
        GUIStyle _style;
        LootDrop _hover;
        float _hoverSince;
        bool _leftCaptured;
        int _pointerBlockedFrame = -1;
        int _paintWidth, _paintHeight;
        float _paintTime = -10f;
        Vector2 _lastPressPosition;
        float _lastPressTime = -10f;
        LootDrop _lastPressDrop;
        public static bool BlocksWorldPointer => Instance && Instance.PointerClaimed;
        bool PointerClaimed => _leftCaptured || _pointerBlockedFrame == Time.frameCount || PointerSurface(out _, out _);
        public LootDrop HoveredDrop => _hover && Time.unscaledTime - _hoverSince >= .22f ? _hover : null;
        public bool TryLabelRect(LootDrop drop, out Rect rect)
        {
            for (int i=0;i<_count;i++) if (_slots[i].Drop == drop) { rect=_slots[i].Rect; return true; }
            rect=default; return false;
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void OnDisable() { _leftCaptured=false; _pointerBlockedFrame=-1; _count=0; _hover=null; }
        void OnApplicationFocus(bool focused) { if(!focused)OnDisable(); }

        bool PointerSurface(out LootDrop drop, out Vector2 point)
        {
            drop=null; point=default;
            var mouse=Mouse.current;
            if(mouse==null || DungeonUi.ModalOpen || !DungeonRoot.Instance ||
               _paintWidth!=Screen.width || _paintHeight!=Screen.height || Time.unscaledTime-_paintTime>.5f)return false;
            point=mouse.position.ReadValue();point=new Vector2(point.x,Screen.height-point.y)/DungeonUi.Scale;
            // Existing HUD/debug controls have priority over labels underneath them.
            if(DungeonHud.CombatRect.Contains(point) || CombatHud.PointerOverPanel ||
               (ExplorationLog.Instance && ExplorationLog.Instance.PointerOverPanel))return false;
            var inv=Inventory.Instance;
            if(HoveredDrop && inv && inv.GroundCardRect.Contains(point))return true;
            // Use the last DRAWN hitboxes: never retarget to the nearest world item.
            // A disappeared item still consumes the press on its stale visible label.
            for(int i=_count-1;i>=0;i--)
                if(_slots[i].Alpha>.002f && _slots[i].Rect.Contains(point)){drop=_slots[i].Drop;return true;}
            return false;
        }

        void HandlePointerPickup()
        {
            var mouse=Mouse.current;
            if(mouse==null){_leftCaptured=false;return;}
            if(_leftCaptured)
            {
                _pointerBlockedFrame=Time.frameCount;
                if(!mouse.leftButton.isPressed)_leftCaptured=false;
                return;
            }
            if(!mouse.leftButton.wasPressedThisFrame || !PointerSurface(out var drop,out var point))return;
            _leftCaptured=true;_pointerBlockedFrame=Time.frameCount;
            // A second press in the same double-click burst must not collect a neighbour
            // that slid under the cursor when the first label disappeared.
            bool repeated=Time.unscaledTime-_lastPressTime<=.32f &&
                          (point-_lastPressPosition).sqrMagnitude<=36f;
            bool changedTarget=!ReferenceEquals(drop,_lastPressDrop);
            _lastPressTime=Time.unscaledTime;_lastPressPosition=point;
            if(repeated && changedTarget)return;
            _lastPressDrop=drop;
            if(drop && Inventory.Instance)Inventory.Instance.TryPickUpClickedDrop(drop);
        }

        void Update()
        {
            HandlePointerPickup();
            if (Time.unscaledTime < _nextCleanup) return;
            _nextCleanup = Time.unscaledTime + CleanupInterval;
            // 주웠거나 지워진 장비의 이름표를 버린다.
            _stale.Clear();
            foreach (var kv in _labels)
                if (!kv.Key || !kv.Key.Available) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++) _labels.Remove(_stale[i]);
            _stale.Clear();
        }

        void OnGUI()
        {
            GUI.depth = GuiDepth;
            var e=Event.current;
            if(e.button==0 && (e.type==EventType.MouseDown || e.type==EventType.MouseUp || e.type==EventType.MouseDrag) && PointerClaimed)
            { e.Use(); return; }
            if (Event.current.type != EventType.Repaint) return;
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            var player = root.Player;
            if (!player || player.IsDown || DungeonUi.ModalOpen) { _hover=null; _count=0; return; }
            DungeonUi.Begin();
            GUI.depth = GuiDepth;
            if (_style == null)
            {
                _style = new GUIStyle(ApprovedUiV5.Style(16,TextAnchor.MiddleLeft,true)) { padding=new RectOffset(),wordWrap=false,clipping=TextClipping.Clip };
                _style.normal.textColor = Color.white;
                _style.hover.textColor = Color.white;
            }
            Collect(player);
            if (_count == 0) { _hover=null; return; }
            Stack();
            Draw();
            _paintWidth=Screen.width;_paintHeight=Screen.height;_paintTime=Time.unscaledTime;
            Hover();
        }

        /// <summary>보일 이름표를 모은다: 내려앉음, 12유닛 안, 시야 안.</summary>
        void Collect(PlayerController player)
        {
            _count = 0;
            _bagFull = Inventory.Instance && Inventory.Instance.BagFull;
            var pickup = InteractionSystem.Instance ? InteractionSystem.Instance.Current : null;
            var vision = VisionSystem.Instance;
            var lighting = DungeonLighting.Instance;
            // 싸움 섞기(켜질 때 0.25초, 꺼질 때 약 1.4초)로 옅게 해 판정이 바뀌는 순간 이름표가 탁 바뀌지 않게 한다.
            float combat = lighting ? Mathf.Lerp(1f, CombatAlpha, lighting.CombatBlend) : 1f;
            float now = Time.unscaledTime;
            Vector2 from = player.Position;
            foreach (var it in Interactable.All)
            {
                if (_count >= MaxLabels) break;
                var drop = it as LootDrop;
                if (!drop || !drop.Available) continue;
                Vector2 pos = drop.Position;
                if ((pos - from).sqrMagnitude > ShowRange * ShowRange) continue;
                if (vision && !vision.IsVisible(pos, 0.2f)) continue;
                var gui = DungeonUi.WorldToGui(pos + Vector2.up * LiftUnits);
                if (gui == null) continue;
                var label = LabelFor(drop, now);
                var g = gui.Value;
                bool isPickup = pickup == drop;
                float width = isPickup ? PickupWidth(label) : label.Width;
                _slots[_count++] = new Slot
                {
                    Drop = drop,
                    Pickup = isPickup,
                    Label = label,
                    Rect = new Rect(g.x - width * 0.5f, g.y - LabelHeight, width, LabelHeight),
                    Alpha = Mathf.Clamp01((now - label.SeenAt) / FadeInSeconds) * combat,
                    Distance = (pos-from).sqrMagnitude,
                };
            }
            // Priority only chooses placement order. Every visible label keeps its icon AND name.
            for(int i=1;i<_count;i++)
            {
                var s=_slots[i];int j=i-1;
                while(j>=0&&Priority(_slots[j])>Priority(s)){_slots[j+1]=_slots[j];j--;}
                _slots[j+1]=s;
            }
        }
        float Priority(Slot s)=>s.Pickup?-10000:s.Drop==_hover?-9000:s.Distance-(int)s.Drop.Gear.Grade*3;

        /// <summary>장비마다 이름 글·등급색·너비를 처음 한 번 만든다.</summary>
        Label LabelFor(LootDrop drop, float now)
        {
            if (_labels.TryGetValue(drop, out var label)) return label;
            var item = drop.Gear;
            label = new Label
            {
                Text = item.DisplayName,
                PickupText = "[F] " + item.DisplayName,
                Color = LootVisuals.GradeColor(item.Grade),
                SeenAt = now,
            };
            _measure.text = label.Text;
            label.Width = Mathf.Min(260,_style.CalcSize(_measure).x + LabelPadX * 2f+28f);
            _labels.Add(drop, label);
            return label;
        }

        /// <summary>획득 대상부터 아이콘·이름 한 묶음으로 가까운 행·열에 배치한다.</summary>
        void Stack()
        {
            // Collection already prioritizes the actual pickup target before resolving overlaps.
            for (int i = 0; i < _count; i++)
            {
                var r = _slots[i].Rect;
                r.x = Mathf.Clamp(r.x, 12f, DungeonUi.Width-r.width-12f);
                r.y = Mathf.Clamp(r.y, 52f, DungeonUi.Height-114f-r.height);
                var origin = r;
                bool placed=false;
                // Search nearby rows and columns together, instead of building one tall column.
                // A side column costs three rows, keeping dense piles compact around their origin.
                for (int distance=0;distance<60 && !placed;distance++)
                {
                    for(int col=0;col<5 && !placed;col++)
                    {
                        int column=(col==0?0:(col%2==1?-1:1)*((col+1)/2));
                        int row=distance-Mathf.Abs(column)*3;
                        if(row<0)continue;
                        for(int side=0;side<(row==0?1:2);side++)
                        {
                            r.x=origin.x+column*(r.width+StackGap);
                            r.y=origin.y+(side==0?-row:row)*(LabelHeight+StackGap);
                            if(r.x<12 || r.xMax>DungeonUi.Width-12 || r.y<52 || r.yMax>DungeonUi.Height-114) continue;
                            bool overlap=false;
                            for(int j=0;j<i;j++) if(_slots[j].Alpha>0 && Overlaps(r,_slots[j].Rect)) {overlap=true;break;}
                            if(!overlap) {placed=true;break;}
                        }
                    }
                }
                if(!placed) _slots[i].Alpha=0;
                _slots[i].Rect = r;
            }
        }

        void Hover()
        {
            var mouse=Mouse.current;
            if(mouse==null || mouse.leftButton.isPressed || mouse.rightButton.isPressed) { _hover=null; return; }
            Vector2 p=mouse.position.ReadValue();
            p=new Vector2(p.x,Screen.height-p.y)*(DungeonUi.Height/Screen.height);
            if(DungeonHud.CombatRect.Contains(p)) { _hover=null; return; }
            LootDrop next=null; Rect anchor=default;
            // 이름표를 먼저 판정하므로 쌓인 아이템 중 하나만 고를 수 있다.
            for(int i=0;i<_count;i++) if(_slots[i].Alpha>.01f && _slots[i].Rect.Contains(p)) {next=_slots[i].Drop;anchor=_slots[i].Rect;break;}
            if(!next) {
                float best=24*24;
                for(int i=0;i<_count;i++) {
                    var s=_slots[i]; if(s.Alpha<=.01f)continue;
                    var at=DungeonUi.WorldToGui(s.Drop.Position);if(!at.HasValue)continue;
                    float d=(at.Value-p).sqrMagnitude;if(d<best){best=d;next=s.Drop;anchor=s.Rect;}
                }
            }
            var inv=Inventory.Instance;
            if(!next && HoveredDrop && inv && inv.GroundCardRect.Contains(p) && TryLabelRect(_hover,out anchor)) next=_hover;
            if(next!=_hover){_hover=next;_hoverSince=Time.unscaledTime;}
            if(HoveredDrop && inv) inv.DrawGroundHoverCard(_hover,anchor);
        }

        static bool Overlaps(Rect a, Rect b) =>
            a.xMin < b.xMax + StackGap && a.xMax > b.xMin - StackGap && a.yMin < b.yMax + StackGap && a.yMax > b.yMin - StackGap;

        /// <summary>검은 반투명 판 + 등급색 가는 테 + 등급색 이름(그림자).</summary>
        void Draw()
        {
            for (int i = 0; i < _count; i++)
            {
                var s = _slots[i];
                if (s.Alpha <= 0.002f) continue;
                var c = s.Label.Color;
                ApprovedUiV5.Round(s.Rect,new Color(.03f,.03f,.04f,.85f*s.Alpha),4,new Color(c.r,c.g,c.b,(s.Pickup ? .95f : .5f)*s.Alpha),s.Pickup?2f:1f);
                GUI.color=new Color(1,1,1,s.Alpha);
                Inventory.DrawItemIconForUi(new Rect(s.Rect.x+4,s.Rect.y+3,22,22),s.Drop.Gear);
                DungeonUi.ShadowLabel(new Rect(s.Rect.x+30,s.Rect.y,s.Rect.width-36,s.Rect.height),s.Pickup?PickupText(s.Label):s.Label.Text,s.Pickup&&_bagFull?FullStyle:_style,new Color(c.r,c.g,c.b,s.Alpha),.9f);
            }
            GUI.color = Color.white;
        }

        // ── 가방 가득(재화 쓸 곳 1차 5-4): F 대상 이름표의 '[F]'를 회색으로, 끝에 ' · 가방 가득'. 너비는 그 글로 다시 잰다 ──

        /// <summary>'[F]'를 회색으로(rich text). 이름 글색(등급색)에 곱해져 옅게 보인다.</summary>
        const string FullPickupKey = "<color=#8C8C8C>[F]</color> ";
        /// <summary>이번 그리기에서 가방이 가득인가(Inventory.BagFull, Collect가 정함).</summary>
        bool _bagFull;
        GUIStyle _fullStyle;

        /// <summary>F 대상 이름표 너비: 평소 '[F] 이름'(이름 너비 + 32), 가방 가득이면 '[F] 이름 · 가방 가득'을 잰 너비.</summary>
        float PickupWidth(Label label)
        {
            if (!_bagFull) return label.Width + 32f;
            if (label.FullWidth < 0f)
            {
                _measure.text = "[F] " + label.Text + Demo6.Core.Town.ForgeText.FloorLabelFullSuffix;
                label.FullWidth = Mathf.Min(360f, _style.CalcSize(_measure).x + LabelPadX * 2f + 28f);
                label.FullText = FullPickupKey + label.Text + Demo6.Core.Town.ForgeText.FloorLabelFullSuffix;
            }
            return label.FullWidth;
        }

        string PickupText(Label label) => _bagFull && label.FullText != null ? label.FullText : label.PickupText;

        /// <summary>회색 '[F]'를 그리는 글꼴(이름표 글꼴 + rich text).</summary>
        GUIStyle FullStyle => _fullStyle ?? (_fullStyle = new GUIStyle(_style) { richText = true });
    }
}
