using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 바닥 장비 이름표(디아블로식, 던전만): 내려앉은 무기(LootDrop)마다 등급색 이름("희귀 대검")을 빛기둥 아랫부분(바닥에서 0.8유닛 위)에 작은 검은 판으로 띄운다.
    /// 시야 다각형 안이고 플레이어 12유닛 안인 것만 보인다(빛기둥과 같은 공정 규칙: 안개 밑 장비를 글로 드러내지 않는다).
    /// 가장 가까운 하나(지금 획득 카드가 뜨는 것, Inventory.NearestDrop)는 카드가 대신하므로 이름표를 숨긴다.
    /// 이름표끼리 겹치면 화면 아래쪽 것부터 두고 겹친 것을 위로 쌓는다. 싸움 중에는 빛기둥처럼 옅게(60%) 낮춘다.
    /// 골드·강화석 작은 보상은 줍는 순간 글자(지금 그대로)라 여기서 다루지 않는다. 수치·드랍 확률은 건드리지 않는다.
    /// 이름 글·너비는 장비마다 처음 한 번 만들고 다시 쓴다(매 프레임 할당 없음).
    /// </summary>
    public sealed class LootLabels : MonoBehaviour
    {
        /// <summary>플레이어에서 이 거리 안의 장비만 이름표를 띄운다.</summary>
        const float ShowRange = 12f;
        /// <summary>이름표 아래 끝: 바닥에서 위로(빛기둥 아랫부분, 획득 안내 1.1보다 낮게).</summary>
        const float LiftUnits = 0.8f;
        const float LabelHeight = 22f;
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
            public Color Color;
            public float Width = -1f;
            public float SeenAt;
        }

        struct Slot
        {
            public Label Label;
            public Rect Rect;
            public float Alpha;
        }

        readonly Dictionary<LootDrop, Label> _labels = new Dictionary<LootDrop, Label>();
        readonly List<LootDrop> _stale = new List<LootDrop>();
        readonly Slot[] _slots = new Slot[MaxLabels];
        readonly GUIContent _measure = new GUIContent();
        int _count;
        float _nextCleanup;
        GUIStyle _style;

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
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
            if (Event.current.type != EventType.Repaint) return;
            var root = DungeonRoot.Instance;
            if (!root || root.State == null) return;
            var player = root.Player;
            if (!player || player.IsDown || DungeonUi.ModalOpen) return;
            DungeonUi.Begin();
            GUI.depth = GuiDepth;
            if (_style == null)
            {
                _style = new GUIStyle(DungeonUi.Small) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
                _style.normal.textColor = Color.white;
                _style.hover.textColor = Color.white;
            }
            Collect(player);
            if (_count == 0) return;
            Stack();
            Draw();
        }

        /// <summary>보일 이름표를 모은다: 내려앉음, 카드 대상 아님, 12유닛 안, 시야 안.</summary>
        void Collect(PlayerController player)
        {
            _count = 0;
            var inv = Inventory.Instance;
            var carded = inv ? inv.NearestDrop(Inventory.EquipRange) : null;
            var vision = VisionSystem.Instance;
            var lighting = DungeonLighting.Instance;
            float combat = lighting && lighting.InCombat ? CombatAlpha : 1f;
            float now = Time.unscaledTime;
            Vector2 from = player.Position;
            foreach (var it in Interactable.All)
            {
                if (_count >= MaxLabels) break;
                var drop = it as LootDrop;
                if (!drop || !drop.Available || drop == carded) continue;
                Vector2 pos = drop.Position;
                if ((pos - from).sqrMagnitude > ShowRange * ShowRange) continue;
                if (vision && !vision.IsVisible(pos, 0.2f)) continue;
                var gui = DungeonUi.WorldToGui(pos + Vector2.up * LiftUnits);
                if (gui == null) continue;
                var label = LabelFor(drop, now);
                var g = gui.Value;
                _slots[_count++] = new Slot
                {
                    Label = label,
                    Rect = new Rect(g.x - label.Width * 0.5f, g.y - LabelHeight, label.Width, LabelHeight),
                    Alpha = Mathf.Clamp01((now - label.SeenAt) / FadeInSeconds) * combat,
                };
            }
        }

        /// <summary>장비마다 이름 글·등급색·너비를 처음 한 번 만든다.</summary>
        Label LabelFor(LootDrop drop, float now)
        {
            if (_labels.TryGetValue(drop, out var label)) return label;
            var item = drop.Item;
            label = new Label
            {
                Text = item.DisplayName,
                Color = LootVisuals.GradeColor(item.Grade),
                SeenAt = now,
            };
            _measure.text = label.Text;
            label.Width = _style.CalcSize(_measure).x + LabelPadX * 2f;
            _labels.Add(drop, label);
            return label;
        }

        /// <summary>화면 아래쪽(가까운) 이름표부터 자리를 잡고, 겹치는 것은 위로 올린다.</summary>
        void Stack()
        {
            // 아래 끝(y가 큰 것)이 먼저 오게 삽입 정렬(할당 없음).
            for (int i = 1; i < _count; i++)
            {
                var s = _slots[i];
                int j = i - 1;
                while (j >= 0 && _slots[j].Rect.y < s.Rect.y)
                {
                    _slots[j + 1] = _slots[j];
                    j--;
                }
                _slots[j + 1] = s;
            }
            for (int i = 1; i < _count; i++)
            {
                var r = _slots[i].Rect;
                // 겹침이 없어질 때까지 위로(한 번 옮기면 앞의 것과 다시 비교).
                for (int guard = 0; guard < _count; guard++)
                {
                    bool moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        var o = _slots[j].Rect;
                        if (!Overlaps(r, o)) continue;
                        r.y = o.y - LabelHeight - StackGap;
                        moved = true;
                    }
                    if (!moved) break;
                }
                _slots[i].Rect = r;
            }
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
                DungeonUi.Fill(s.Rect, new Color(PlateColor.r, PlateColor.g, PlateColor.b, PlateColor.a * s.Alpha));
                DungeonUi.Outline(s.Rect, new Color(c.r, c.g, c.b, 0.4f * s.Alpha), 1f);
                DungeonUi.ShadowLabel(s.Rect, s.Label.Text, _style, new Color(c.r, c.g, c.b, s.Alpha), 0.9f);
            }
            GUI.color = Color.white;
        }
    }
}
