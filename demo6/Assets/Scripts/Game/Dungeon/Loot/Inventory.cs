using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 낀 무기·가방·처치 장비 드랍(3차 초안 7-2 '최소 무기 능력치': 무기 3종 공격력 숫자, G로 끼기).
    /// 낀 무기는 player.SetWeapon(그 종류) + player.SetAttack(맨몸 100 + 무기 공격력)(2차 6-1). 시작 무기 = 일반 장검 공격 100(공격력 200).
    /// 처치 보상(3차 초안 4-6): CombatEvents.EnemyKilled(둥지·허수아비·NoReward 굴쥐 제외), 둥지 정리는 DungeonEvents.GroupCleared(nest).
    /// 가방은 20칸(M0b 축소판). G로 벗은 무기는 가득 차도 넘쳐 들어간다(2차 7-7). I로 가방 창("bag")을 열고 Esc·I로 닫는다.
    /// 강화석·골드는 DungeonState에 더한다(쓸 곳은 M0b에 없음).
    /// </summary>
    public sealed class Inventory : MonoBehaviour
    {
        public const string BagWindow = "bag";
        public const int BagCapacity = 20;
        /// <summary>G로 바로 끼는 거리(F 상호작용 거리와 같음).</summary>
        public const float EquipRange = 1.8f;
        const ulong KillStream = 23;
        const float RowHeight = 34f;
        const int VisibleRows = 14;

        static readonly Color UpColor = new Color(0.45f, 0.9f, 0.5f);
        static readonly Color DownColor = new Color(1f, 0.42f, 0.38f);
        static readonly Color SameColor = new Color(0.7f, 0.7f, 0.7f);
        static readonly Color HintColor = new Color(0.85f, 0.82f, 0.75f);

        public static Inventory Instance { get; private set; }

        /// <summary>지금 낀 무기(화면: 이름·공격).</summary>
        public WeaponItem Equipped { get; private set; }
        public IReadOnlyList<WeaponItem> Bag => _bag;
        public int BagCount => _bag.Count;
        public bool BagFull => _bag.Count >= BagCapacity;

        readonly List<WeaponItem> _bag = new List<WeaponItem>();
        PlayerController _player;
        PlayerInputReader _input;
        Pcg32Random _rng;
        Vector2 _scroll;

        void Awake()
        {
            Instance = this;
            _rng = new Pcg32Random((ulong)System.DateTime.UtcNow.Ticks, KillStream);
            CombatEvents.EnemyKilled += OnEnemyKilled;
            DungeonEvents.GroupCleared += OnGroupCleared;
        }

        void OnDestroy()
        {
            if (_player) _player.WeaponChanged -= OnPlayerWeaponChanged;
            CombatEvents.EnemyKilled -= OnEnemyKilled;
            DungeonEvents.GroupCleared -= OnGroupCleared;
            if (DungeonUi.Modal == BagWindow) DungeonUi.Close(BagWindow);
            if (Instance == this) Instance = null;
        }

        /// <summary>플레이어를 만든 뒤 DungeonRoot가 부른다(시작 무기: 일반 장검 공격 100 → 공격력 200).</summary>
        public void Init(PlayerController player)
        {
            if (_player) _player.WeaponChanged -= OnPlayerWeaponChanged;
            _player = player;
            _input = player ? player.GetComponent<PlayerInputReader>() : null;
            _bag.Clear();
            Equipped = WeaponItem.Starting();
            ApplyToPlayer();
            if (_player) _player.WeaponChanged += OnPlayerWeaponChanged;
        }

        /// <summary>
        /// 시험용 무기 키(1·2·3)로 종류만 바꿨을 때: 낀 무기의 등급·층·굴림은 두고 종류만 맞춘다(화면·비교가 실제 휘두르는 무기와 같게).
        /// </summary>
        void OnPlayerWeaponChanged(Demo6.Core.Combat.WeaponAttackRule rule)
        {
            if (Equipped == null || rule == null || rule == Equipped.Rule) return;
            Equipped = new WeaponItem(rule.id, Equipped.Grade, Equipped.ItemLevel, Equipped.RollPermille);
            if (_player) _player.SetAttack(Equipped.PlayerAttack);
        }

        /// <summary>낀 무기를 플레이어에 반영: 공격 모양(종류) + 공격력 = 맨몸 100 + 무기 공격력.</summary>
        void ApplyToPlayer()
        {
            if (!_player || Equipped == null) return;
            _player.SetWeapon(Equipped.Rule);
            _player.SetAttack(Equipped.PlayerAttack);
        }

        /// <summary>낀 무기와 공격 차이(+면 세짐).</summary>
        public int AttackDiff(WeaponItem item) => item == null ? 0 : item.Attack - (Equipped != null ? Equipped.Attack : 0);

        /// <summary>
        /// 가방이나 바닥의 무기를 낀다. 가방에 있던 것이면 벗은 무기가 그 칸으로, 아니면 가방 끝으로 간다(가득 차도 넘쳐 들어감).
        /// </summary>
        public void Equip(WeaponItem item)
        {
            if (item == null || item == Equipped) return;
            var old = Equipped;
            int index = _bag.IndexOf(item);
            if (index >= 0)
            {
                if (old != null) _bag[index] = old;
                else _bag.RemoveAt(index);
            }
            else if (old != null) _bag.Add(old);
            Equipped = item;
            ApplyToPlayer();
            Sfx.Play(SfxKind.Pickup);
            DungeonEvents.RaiseGearEquipped(item.DisplayName + " (공격 " + item.Attack + ")");
            if (_player)
                WorldOverlay.Text(_player.Position + Vector2.up * 1.2f, item.DisplayName + " 공격 " + item.Attack, LootVisuals.GradeColor(item.Grade));
        }

        /// <summary>F: 바닥 무기를 가방에 넣는다. 가방이 가득 차면 알리고 false.</summary>
        public bool PickUp(LootDrop drop)
        {
            if (!drop || !drop.Available) return false;
            if (BagFull)
            {
                DungeonEvents.Say("가방이 가득 찼다 (" + BagCapacity + "칸)");
                return false;
            }
            var item = drop.Item;
            _bag.Add(item);
            drop.Take();
            Sfx.Play(SfxKind.Pickup);
            if (_player)
                WorldOverlay.Text(_player.Position + Vector2.up * 1.2f, "가방 ← " + item.DisplayName, LootVisuals.GradeColor(item.Grade));
            return true;
        }

        /// <summary>G: 바닥 무기를 바로 낀다(벗은 무기는 가방으로).</summary>
        public void EquipFromFloor(LootDrop drop)
        {
            if (!drop || !drop.Available) return;
            var item = drop.Item;
            drop.Take();
            Equip(item);
        }

        /// <summary>플레이어에게서 range 안 가장 가까운, 내려앉은 바닥 무기.</summary>
        public LootDrop NearestDrop(float range)
        {
            if (!_player) return null;
            LootDrop best = null;
            float bestD = range;
            foreach (var it in Interactable.All)
            {
                var drop = it as LootDrop;
                if (!drop || !drop.Available) continue;
                float d = (drop.Position - _player.Position).magnitude;
                if (d > bestD) continue;
                best = drop;
                bestD = d;
            }
            return best;
        }

        void Update()
        {
            HandleWindowKeys();
            if (!_player || _player.IsDown || DungeonUi.ModalOpen || TimeScaleService.Paused) return;
            if (!_input) _input = _player.GetComponent<PlayerInputReader>();
            if (_input && _input.EquipPressed)
            {
                var drop = NearestDrop(EquipRange);
                if (drop) EquipFromFloor(drop);
            }
        }

        /// <summary>I: 가방 창 열기·닫기, Esc: 닫기. 시간이 멈춘 동안에도 들어야 하므로 키보드에서 직접 읽는다.</summary>
        void HandleWindowKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.iKey.wasPressedThisFrame)
            {
                if (DungeonUi.Modal == BagWindow) DungeonUi.Close(BagWindow);
                else if (!DungeonUi.ModalOpen && _player && !_player.IsDown) DungeonUi.TryOpen(BagWindow);
            }
            else if (kb.escapeKey.wasPressedThisFrame && DungeonUi.Modal == BagWindow)
                DungeonUi.Close(BagWindow);
        }

        int CurrentFloor => DungeonRoot.Instance != null ? DungeonRoot.Instance.Floor : 1;

        Vector2 AwayFromPlayer(Vector2 pos)
        {
            var player = PlayerController.Instance;
            if (!player) return Vector2.zero;
            return pos - player.Position;
        }

        /// <summary>처치 보상(3차 초안 4-6). 둥지 자체는 GroupCleared(nest)가 맡고, 둥지가 부른 굴쥐(NoReward)는 0.</summary>
        void OnEnemyKilled(Enemy e)
        {
            if (!e || e.IsDummy || e.NoReward || e.Kind == MonsterKind.Nest) return;
            KillSource source = e.IsElite ? KillSource.Elite
                : e.Kind == MonsterKind.Boar ? KillSource.Boar
                : e.Kind == MonsterKind.Archer ? KillSource.Archer
                : KillSource.Rat;
            int floor = CurrentFloor;
            var bundle = LootRules.RollKill(source, floor, _rng);
            Vector2 pos = e.Position;
            LootSpawner.Spawn(bundle, pos, AwayFromPlayer(pos), LootSpawner.KillDelay);
        }

        /// <summary>둥지 정리(3차 초안 4-6): 장비 50%, 강화석 0.5 × 배율, 골드 무더기 3개.</summary>
        void OnGroupCleared(int group, bool nest, Vector2 pos)
        {
            if (!nest) return;
            var bundle = LootRules.RollKill(KillSource.NestClear, CurrentFloor, _rng);
            LootSpawner.Spawn(bundle, pos, AwayFromPlayer(pos), LootSpawner.KillDelay);
        }

        void OnGUI()
        {
            bool bagOpen = DungeonUi.Modal == BagWindow;
            GUI.depth = bagOpen ? -20 : 0;
            if (bagOpen)
            {
                DrawBag();
                return;
            }
            if (DungeonUi.ModalOpen || !_player || _player.IsDown) return;
            var drop = NearestDrop(EquipRange);
            if (drop) DrawCard(drop);
        }

        /// <summary>가장 가까운 바닥 무기 카드: 등급색 이름, 공격과 낀 무기 대비 ▲/▼, 키 안내(2차 7-7 획득 카드의 M0b판).</summary>
        void DrawCard(LootDrop drop)
        {
            DungeonUi.Begin();
            var gui = DungeonUi.WorldToGui(drop.Position + Vector2.down * 0.6f);
            if (gui == null) return;
            var item = drop.Item;
            var gradeColor = LootVisuals.GradeColor(item.Grade);
            var r = new Rect(gui.Value.x - 130f, gui.Value.y + 4f, 260f, 80f);
            DungeonUi.Box(r, 0.85f);
            DungeonUi.Fill(new Rect(r.x, r.y, 4f, r.height), gradeColor);
            var prev = GUI.color;
            GUI.color = gradeColor;
            GUI.Label(new Rect(r.x + 12f, r.y + 4f, r.width - 16f, 24f), item.DisplayName, DungeonUi.Bold);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 12f, r.y + 28f, 100f, 24f), "공격 " + item.Attack, DungeonUi.Label);
            DrawDiff(new Rect(r.x + 108f, r.y + 28f, r.width - 116f, 24f), item, DungeonUi.Label);
            GUI.color = HintColor;
            GUI.Label(new Rect(r.x + 12f, r.y + 54f, r.width - 16f, 22f), "[F] 가방 · [G] 바로 끼기", DungeonUi.Small);
            GUI.color = prev;
        }

        /// <summary>낀 무기 대비 공격 차이: ▲ 초록, ▼ 빨강, = 회색. 무기 종류가 다르면 '방식 다름'.</summary>
        void DrawDiff(Rect r, WeaponItem item, GUIStyle style)
        {
            int diff = AttackDiff(item);
            string text;
            Color color;
            if (diff > 0)
            {
                text = "▲" + diff;
                color = UpColor;
            }
            else if (diff < 0)
            {
                text = "▼" + (-diff);
                color = DownColor;
            }
            else
            {
                text = "=";
                color = SameColor;
            }
            if (Equipped != null && item.WeaponId != Equipped.WeaponId) text += "  방식 다름";
            var prev = GUI.color;
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = prev;
        }

        /// <summary>가방 창: 낀 무기, 강화석·골드, 무기 목록(등급색 이름, 공격, ▲/▼, [끼기]).</summary>
        void DrawBag()
        {
            DungeonUi.Begin();
            const float w = 620f;
            int rows = Mathf.Clamp(_bag.Count, 1, VisibleRows);
            float listH = rows * RowHeight;
            float h = 168f + listH + 44f;
            var r = new Rect((DungeonUi.Width - w) * 0.5f, (DungeonUi.Height - h) * 0.5f, w, h);
            DungeonUi.Box(r, 0.94f);
            var prev = GUI.color;
            float x = r.x + 20f;
            float y = r.y + 14f;

            GUI.Label(new Rect(x, y, 300f, 30f), "가방", DungeonUi.Title);
            GUI.color = _bag.Count > BagCapacity ? DownColor : Color.white;
            GUI.Label(new Rect(r.xMax - 150f, y + 4f, 130f, 26f), _bag.Count + " / " + BagCapacity + "칸", DungeonUi.Label);
            GUI.color = prev;
            y += 36f;

            var state = DungeonRoot.Instance != null ? DungeonRoot.Instance.State : null;
            if (state != null)
                GUI.Label(new Rect(x, y, w - 40f, 22f), "강화석 " + state.Stones + " · 골드 " + state.Gold, DungeonUi.Small);
            y += 28f;

            GUI.Label(new Rect(x, y, 90f, 26f), "끼고 있음", DungeonUi.Small);
            if (Equipped != null)
            {
                GUI.color = LootVisuals.GradeColor(Equipped.Grade);
                GUI.Label(new Rect(x + 90f, y, 200f, 26f), Equipped.DisplayName, DungeonUi.Bold);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 300f, y, 280f, 26f), "공격 " + Equipped.Attack + "  (공격력 " + Equipped.PlayerAttack + ")", DungeonUi.Label);
                GUI.color = prev;
            }
            y += 34f;
            DungeonUi.Fill(new Rect(x, y, w - 40f, 1f), new Color(1f, 1f, 1f, 0.15f));
            y += 8f;

            int equipIndex = -1;
            var view = new Rect(x, y, w - 40f, listH);
            if (_bag.Count == 0)
            {
                GUI.color = SameColor;
                GUI.Label(new Rect(x, y + 6f, w - 40f, 24f), "비어 있다. 바닥 무기 앞에서 [F]로 넣는다.", DungeonUi.Label);
                GUI.color = prev;
            }
            else
            {
                var content = new Rect(0f, 0f, view.width - 20f, _bag.Count * RowHeight);
                _scroll = GUI.BeginScrollView(view, _scroll, content);
                for (int i = 0; i < _bag.Count; i++)
                {
                    var item = _bag[i];
                    var row = new Rect(0f, i * RowHeight, content.width, RowHeight - 4f);
                    if (i % 2 == 0) DungeonUi.Fill(row, new Color(1f, 1f, 1f, 0.04f));
                    var gradeColor = LootVisuals.GradeColor(item.Grade);
                    DungeonUi.Fill(new Rect(row.x, row.y, 3f, row.height), gradeColor);
                    GUI.color = gradeColor;
                    GUI.Label(new Rect(row.x + 10f, row.y + 4f, 190f, 24f), item.DisplayName, DungeonUi.Bold);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(row.x + 205f, row.y + 4f, 90f, 24f), "공격 " + item.Attack, DungeonUi.Label);
                    GUI.color = prev;
                    DrawDiff(new Rect(row.x + 300f, row.y + 4f, 150f, 24f), item, DungeonUi.Label);
                    if (GUI.Button(new Rect(row.xMax - 80f, row.y + 2f, 76f, row.height - 4f), "끼기")) equipIndex = i;
                }
                GUI.EndScrollView();
            }
            y += listH + 10f;

            GUI.color = HintColor;
            GUI.Label(new Rect(x, y, 360f, 24f), "[I] · [Esc] 닫기", DungeonUi.Small);
            GUI.color = prev;
            if (GUI.Button(new Rect(r.xMax - 110f, y - 2f, 90f, 28f), "닫기")) DungeonUi.Close(BagWindow);

            if (equipIndex >= 0 && equipIndex < _bag.Count) Equip(_bag[equipIndex]);
        }
    }
}
