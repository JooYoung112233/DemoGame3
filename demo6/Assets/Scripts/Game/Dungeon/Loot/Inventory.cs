using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using Demo6.Core.Stats;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo6.Game
{
    /// <summary>
    /// 장착 8자리·가방·처치 장비 드랍(장비 문서 4-1·4-4·8-1·8-2·8-3·8-5·8-6, 12장 단계 2).
    /// 장착 = Core Loadout(무기·갑옷·투구·장갑·장화·반지 1·2·목걸이), 가방 = 장비 목록. 시작은 일반 장검 + 가죽 한 벌, 반지·목걸이는 비어 있다(4-4).
    /// 능력치 한 입구(2-3): RecomputeStats = StatCalc.Compute(장착, 레벨, 질긴 몸) → player.ApplyStats → player.SetLook(겉모습 4개).
    /// 던전에서 ApplyStats를 부르는 곳은 이것 하나다(레벨·질긴 몸이 바뀌면 PlayerProgress가 이것을 부른다).
    /// G = 바닥 장비 바로 끼기(반지는 빈 자리부터 반지 1 → 반지 2, 둘 다 차면 종합 변화가 큰 자리), Shift+G = 반지를 다른 쪽에(8-6).
    /// 벗은 장비는 가방으로 간다(가득 차도 넘침 6칸까지, 넘침 끝이면 바닥에서 바꿔 낀 장비는 발밑에 — 재화 쓸 곳 1차 5-1). 반지·목걸이만 벗어 비울 수 있다(4-1).
    /// 처치 보상(3차 초안 4-6): CombatEvents.EnemyKilled(둥지·허수아비·NoReward 굴쥐 제외), 둥지 정리는 DungeonEvents.GroupCleared(nest).
    /// 굴림 조건(8-2): 부위 점수가 가장 낮은 2부위 보정, 가진 전설 효과.
    /// 가방은 20칸, 대장간 물건으로 25·30칸(재화 쓸 곳 1차 5-1, 넘침 6칸). I로 가방 창("bag", IMGUI 기능만: 장착 8자리·능력치 표 13줄·가방·상세)을 열고 Esc·I로 닫는다.
    /// 칸·줍기 판정(BagRules.Decide)·분해·내려놓기·저절로 분해는 Inventory.Salvage.cs(재화 쓸 곳 1차 3장·4-2~4-4·5장).
    /// 층을 떠날 때(올라가기·계단·다시 짓기) 바닥에 남은 장비는 SweepFloor가 가방으로 거둔다(검토 1차 Q3, 2차 7-7: 희귀·영웅은 넘침 6칸까지, 전설은 늘, Core BagSweep).
    /// 강화석·골드는 DungeonState에 더한다. 분해 강화석은 Wallet.AddSalvageStones 한 곳으로(재화 쓸 곳 1차 4-1, 마을이면 꾸러미).
    /// 매판 새 탐험 1차: 장착 8자리·가방은 꾸러미(ExportTo/ImportFrom)로 장면을 다시 불러와도 남는다.
    /// 룬 주머니·룬 홈 끼우기·빼기는 Inventory.Runes.cs(기획/세-무기-우클릭-소켓-1차.md 6장). 무기 행동 중에는 장착·룬 단추가 회색이고 G를 무시한다.
    /// </summary>
    public sealed partial class Inventory : MonoBehaviour
    {
        public const string BagWindow = "bag";
        /// <summary>지금 가방 칸 수(= Capacity, 20·25·30). 승인된 가방 창(Inventory.ApprovedV5.cs)이 칸 줄·빈 칸을 이 값으로 그린다(재화 쓸 곳 1차 5-4).</summary>
        public int BagCapacity => Capacity;
        /// <summary>G로 바로 끼는 거리(F 상호작용 거리와 같음).</summary>
        public const float EquipRange = 1.8f;
        const ulong KillStream = 23;
        const float RowHeight = 54f;
        const int VisibleRows = 10;

        static readonly Color UpColor = new Color(0.45f, 0.9f, 0.5f);
        static readonly Color DownColor = new Color(1f, 0.42f, 0.38f);
        static readonly Color SameColor = new Color(0.7f, 0.7f, 0.7f);
        static readonly Color HintColor = new Color(0.85f, 0.82f, 0.75f);
        /// <summary>옵션 줄(옅은 파랑).</summary>
        static readonly Color OptionColor = new Color(0.62f, 0.78f, 1f);
        /// <summary>종류 고유 줄(바랜 뼈색).</summary>
        static readonly Color IntrinsicColor = new Color(0.78f, 0.74f, 0.64f);
        const float CardLineHeight = 22f;

        public static Inventory Instance { get; private set; }

        /// <summary>장착 8자리(장비 문서 4-1). 이 클래스 안에서만 바꾸고, 바꾼 뒤에는 RecomputeStats를 부른다.</summary>
        public Loadout Equipment { get; private set; } = Loadout.Starting();
        /// <summary>지금 낀 무기(화면: 이름·대표 능력치).</summary>
        public GearItem Equipped => Equipment.Weapon;
        /// <summary>마지막으로 RecomputeStats가 낸 능력치(카드·능력치 표·HUD가 읽는다). Init 전에는 null.</summary>
        public StatSheet Sheet { get; private set; }
        public IReadOnlyList<GearItem> Bag => _bag;
        public int BagCount => _bag.Count;
        public bool BagFull => _bag.Count >= BagCapacity;

        readonly List<GearItem> _bag = new List<GearItem>();
        PlayerController _player;
        PlayerInputReader _input;
        PlayerProgress _progress;
        Pcg32Random _rng;
        Vector2 _scroll;
        Vector2 _detailScroll;
        GearItem _selected;
        GUIStyle _detailTitle;
        GUIStyle _statValue;
        GUIStyle _statChange;
        GUIStyle _slotCaption;
        GUIStyle _iconCaption;
        Card _card;

        void Awake()
        {
            Instance = this;
            _rng = new Pcg32Random((ulong)System.DateTime.UtcNow.Ticks, KillStream);
            CombatEvents.EnemyKilled += OnEnemyKilled;
            DungeonEvents.GroupCleared += OnGroupCleared;
        }

        void OnDestroy()
        {
            CancelInventoryPointer();
            _v5Portrait.Dispose();
            if (_player) _player.WeaponChanged -= OnPlayerWeaponChanged;
            CombatEvents.EnemyKilled -= OnEnemyKilled;
            DungeonEvents.GroupCleared -= OnGroupCleared;
            if (DungeonUi.Modal == BagWindow) DungeonUi.Close(BagWindow);
            if (Instance == this) Instance = null;
        }

        /// <summary>플레이어를 만든 뒤 DungeonRoot가 부른다(시작 장비: 일반 장검 + 가죽 한 벌 → 공격 200·체력 2,400·방어 120, 4-4).</summary>
        public void Init(PlayerController player)
        {
            if (_player) _player.WeaponChanged -= OnPlayerWeaponChanged;
            _player = player;
            _input = player ? player.GetComponent<PlayerInputReader>() : null;
            _bag.Clear();
            Equipment = Loadout.Starting();
            _selected = null;
            ResetBagState(null);
            ClearRunePouch();
            RecomputeStats();
            if (_player) _player.WeaponChanged += OnPlayerWeaponChanged;
        }

        /// <summary>꾸러미에 담기(매판 새 탐험 1차 3-3): 장착 8자리와 가방, 룬 주머니. 장비는 바뀌지 않는 값이라 그대로 넘긴다(끼운 룬도 장비에 들어 있음).</summary>
        public void ExportTo(CarryData data)
        {
            if (data == null) return;
            foreach (var slot in GearSlots.All) data.Equipment[(int)slot] = Equipment[slot];
            data.Bag.Clear();
            foreach (var item in _bag)
                if (item != null) data.Bag.Add(item);
            ExportRunes(data);
        }

        /// <summary>
        /// 꾸러미에서 풀기: 장착 8자리(빈 칸은 앞 5자리 시작 장비, 반지·목걸이 빈 자리)와 가방을 덮어쓰고 RecomputeStats로 플레이어에 넣는다.
        /// 부위가 맞지 않는 칸은 버린다(꾸러미 글이 이미 거른다).
        /// </summary>
        public void ImportFrom(CarryData data)
        {
            if (data == null) return;
            var loadout = Loadout.Starting();
            foreach (var slot in GearSlots.All)
            {
                var item = data.Equipment[(int)slot];
                if (Loadout.CanEquip(slot, item)) loadout.TryEquip(slot, item, out _);
            }
            Equipment = loadout;
            _bag.Clear();
            foreach (var item in data.Bag)
                if (item != null) _bag.Add(item);
            ImportRunes(data);
            _selected = null;
            ResetBagState(data);
            RecomputeStats();
        }

        /// <summary>
        /// 능력치 한 입구(장비 문서 2-3): StatCalc.Compute(장착 8자리, 레벨, 질긴 몸 랭크) → player.ApplyStats → player.SetLook(Equipment.Look).
        /// 장착이 바뀔 때(G·Shift+G·가방에서 끼기·벗기·시험 키 1·2·3·꾸러미 풀기)와 레벨·질긴 몸이 바뀔 때(PlayerProgress) 부른다.
        /// DungeonRoot에서는 ApplyBaseline(층 기준표) 뒤에 불려 공격·체력·방어를 장비 값으로 덮어쓴다(방어는 층 기준표가 아니라 장비 방어).
        /// </summary>
        public StatSheet RecomputeStats()
        {
            Sheet = Compute(Equipment);
            _card = null;
            if (_player)
            {
                _player.ApplyStats(Sheet);
                _player.SetLook(Equipment.Look);
                // 룬 효과는 낀 무기(장착 자리)의 룬만 센다(6-5). 장착이 바뀌는 모든 길(꾸러미 풀기 포함)이 여기를 지난다.
                _player.SetWeaponRunes(Equipped != null ? Equipped.Runes : null);
            }
            return Sheet;
        }

        /// <summary>
        /// 장비를 바꾼 뒤의 RecomputeStats: 지금 체력은 그대로 두고 새 최대 체력으로 자르기만 한다
        /// (ApplyStats의 SetMax는 늘어난 만큼 지금 체력도 늘려서, 체력 장비를 벗었다 끼기를 되풀이하면 체력이 차오르기 때문).
        /// 레벨업·꾸러미 풀기는 RecomputeStats를 그대로 쓴다(늘어난 만큼 지금 체력도).
        /// </summary>
        void RecomputeAfterGearChange()
        {
            var health = _player ? _player.Health : null;
            int current = health ? health.Current : -1;
            // RecomputeStats가 _player.SetWeaponRunes(Equipped?.Runes)까지 부른다(낀 무기의 룬만 효과).
            RecomputeStats();
            if (health && !health.Dead && current > 0) health.SetCurrent(Mathf.Min(current, health.Max));
        }

        /// <summary>그 장착 상태의 능력치(레벨·질긴 몸은 지금 값). '이걸 끼면' 비교에 쓴다.</summary>
        StatSheet Compute(Loadout loadout)
        {
            var progress = Progress;
            return StatCalc.Compute(loadout, progress ? progress.Level : 1, progress ? progress.ToughRank : 0);
        }

        PlayerProgress Progress
        {
            get
            {
                if (_progress) return _progress;
                _progress = GetComponent<PlayerProgress>();
                if (!_progress) _progress = PlayerProgress.Instance;
                return _progress;
            }
        }

        /// <summary>
        /// 시험용 무기 키(1·2·3)로 종류만 바꿨을 때: 낀 무기를 WithBase로 바꿔 등급·iLv·굴림·강화·옵션·전설을 그대로 두고 종류만 맞춘다(장비 문서 8-6 아래).
        /// 화면·비교·능력치가 실제 휘두르는 무기와 같게 RecomputeStats를 부른다.
        /// </summary>
        void OnPlayerWeaponChanged(WeaponAttackRule rule)
        {
            var weapon = Equipped;
            if (weapon == null || rule == null || rule.id == weapon.BaseId) return;
            var swapped = weapon.WithBase(rule.id);
            if (ReferenceEquals(swapped, weapon)) return;
            Equipment.TryEquip(GearSlot.Weapon, swapped, out _);
            if (ReferenceEquals(_selected, weapon)) _selected = swapped;
            RecomputeAfterGearChange();
        }

        /// <summary>
        /// G가 이 장비를 낄 자리(8-6): 반지가 아니면 그 부위 자리. 반지는 빈 자리부터(반지 1 → 반지 2),
        /// 둘 다 차면 바꿨을 때 종합 변화(GearScore 종합 지수)가 더 큰 자리, 같으면 반지 1.
        /// </summary>
        public GearSlot EquipSlotFor(GearItem item)
        {
            if (item == null) return GearSlot.Weapon;
            if (item.Part != GearPart.Ring) return GearSlots.FirstSlotOf(item.Part);
            var before = GearScore.Of(Sheet ?? Compute(Equipment));
            return Equipment.ChooseSlot(item, l => GearScore.Composite(before, GearScore.Of(Compute(l))));
        }

        /// <summary>Shift+G 자리: 반지면 G 자리의 다른 쪽, 아니면 G 자리.</summary>
        public GearSlot OtherSlotFor(GearItem item) => GearSlots.OtherRing(EquipSlotFor(item));

        /// <summary>
        /// 가방이나 바닥의 장비를 낀다(slot이 없으면 G 자리). 가방에 있던 것이면 벗은 장비가 그 칸으로, 아니면 가방 끝으로 간다(넘침 칸까지).
        /// 바닥에서 바꿔 껴 넘침 끝에 닿으면 벗은 장비는 발밑에 내려놓는다(재화 쓸 곳 1차 5-1, StoreSwapped).
        /// 낀 반지를 다른 반지 자리로 옮기면 두 반지를 맞바꾼다. 낀 자리를 돌려준다(부위가 맞지 않거나 이미 그 자리면 null).
        /// </summary>
        public GearSlot? Equip(GearItem item, GearSlot? slot = null)
        {
            if (item == null || WeaponActLocked) return null;
            var target = slot ?? EquipSlotFor(item);
            if (!Loadout.CanEquip(target, item)) return null;
            var from = Equipment.SlotOf(item);
            if (from.HasValue && from.Value == target) return null;
            int index = _bag.IndexOf(item);
            if (!Equipment.TryEquip(target, item, out var removed)) return null;
            if (index >= 0)
            {
                if (removed != null) _bag[index] = removed;
                else _bag.RemoveAt(index);
            }
            else if (removed != null) StoreSwapped(removed);
            RecomputeAfterGearChange();
            Sfx.Play(SfxKind.Pickup);
            string where = item.Part == GearPart.Ring ? " → " + GearSlots.SlotName(target) : "";
            DungeonEvents.RaiseGearEquipped(item.DisplayName + where + " (" + BaseStatText(item) + ")");
            ExpeditionLedger.NoteSwapped();
            if (_player)
                WorldOverlay.Text(_player.Position + Vector2.up * 1.2f, item.DisplayName + " " + BaseStatText(item), LootVisuals.GradeColor(item.Grade));
            // 룬은 옛 무기에 남는다(6-5): 한 줄로 알린다.
            if (removed != null && removed.IsWeapon && removed.Runes.Count > 0) DungeonEvents.Say(OldWeaponRunesNotice);
            return target;
        }

        /// <summary>
        /// 반지·목걸이를 벗어 가방에 넣는다(가득 차도 넘침 칸으로). 넘침 끝이면 벗지 않고 알린다(재화 쓸 곳 1차 5-1).
        /// 무기·갑옷·투구·장갑·장화는 바꾸기만 한다(4-1). 무기 행동 중에는 하지 않는다.
        /// </summary>
        public bool Unequip(GearSlot slot)
        {
            if (!GearSlots.CanUnequip(slot) || WeaponActLocked) return false;
            if (UnequipBlockedByOverflow(slot)) return false;
            var old = Equipment.Unequip(slot);
            if (old == null) return false;
            _bag.Add(old);
            RecomputeAfterGearChange();
            Sfx.Play(SfxKind.Pickup);
            DungeonEvents.Say(old.DisplayName + " 벗음 — 가방으로");
            return true;
        }

        /// <summary>
        /// 바닥 장비를 가방에 넣는다(재화 쓸 곳 1차 5-3, 판정은 BagRules.Decide 한 곳 — DecidePickup).
        /// F 대상이거나 이름표를 눌러 주운 것은 F 줍기: 가득이면 알리고 false. LootDrop의 15 밖 저절로 줍기는 거절을 알리지 않고(매 프레임 부를 수 있음),
        /// ㉡ 대상(일반 +0, 끼어도 나아지지 않음)은 그 자리에서 분해한다. 내려놓은 장비는 저절로 줍지 않고, F로 주우면 기억에서 지운다(4-4).
        /// </summary>
        public bool PickUp(LootDrop drop)
        {
            if (!drop || !drop.Available) return false;
            var action = DecidePickup(drop, out bool manual);
            if (action == PickupAction.Salvage && !manual) return AutoSalvage(drop);
            if (action != PickupAction.Take && action != PickupAction.TakeOverflow)
            {
                if (manual) DungeonEvents.Say(PickupBlockedText());
                return false;
            }
            var item = drop.Gear;
            _bag.Add(item);
            ExpeditionLedger.NotePicked(item); // 원정 '남은 것'(묶음 3 가-4)
            ForgetDropped(item);
            drop.Take();
            Sfx.Play(SfxKind.Pickup);
            if (_player)
                WorldOverlay.Text(_player.Position + Vector2.up * 1.2f, "가방 ← " + item.DisplayName, LootVisuals.GradeColor(item.Grade));
            return true;
        }

        // ── 떠날 때 바닥 장비 거두기(시스템·컨텐츠 다듬기 검토 1차 Q3, 2차 7-7) ──

        /// <summary>
        /// 떠날 때 거둘 바닥 장비: 내려앉은 것(아직 줍지 않음)과 아직 날아가는 것(보스 보상을 흩뿌리는 중 등). 줍거나 낀 것은 빠진다.
        /// 날아가는 장비는 Available이 아직 false지만 줍는 길(F·G·저절로 줍기)이 모두 내려앉은 뒤라 줍힌 적이 없다.
        /// </summary>
        static List<LootDrop> FloorDrops()
        {
            var list = new List<LootDrop>();
            foreach (var it in Interactable.All)
            {
                var drop = it as LootDrop;
                if (drop && drop.Gear != null && (drop.Available || !drop.Landed)) list.Add(drop);
            }
            return list;
        }

        /// <summary>바닥 장비 목록(FloorDrops 차례 그대로)의 장비.</summary>
        static List<GearItem> GearsOf(List<LootDrop> drops)
        {
            var gears = new List<GearItem>(drops.Count);
            foreach (var d in drops) gears.Add(d.Gear);
            return gears;
        }

        /// <summary>바닥 장비마다 '지금 끼면 ▲'(Improves, ㉡ 판정 재료).</summary>
        List<bool> ImprovesOf(List<LootDrop> drops)
        {
            var improves = new List<bool>(drops.Count);
            foreach (var d in drops) improves.Add(Improves(d.Gear));
            return improves;
        }

        /// <summary>
        /// 지금 떠나면 바닥 장비를 어떻게 거둘지(가방은 바꾸지 않음). 떠나기 전 확인(DungeonRoot)이 RareLeft를 본다.
        /// 재화 쓸 곳 1차 5-3 '떠날 때 거두기' 줄(BagSweep 장비 목록판): 내려놓은 장비는 빼고, ㉡ 대상·가득일 때 일반·고급은 분해 몫.
        /// </summary>
        public BagSweepPlan PlanFloorSweep()
        {
            var drops = SweepDrops();
            return BagSweep.Plan(_bag.Count, Capacity, GearsOf(drops), ImprovesOf(drops), Tuning.AutoSalvageCommon);
        }

        /// <summary>
        /// 떠나는 암전 뒤(DungeonRoot 올라가기·계단·다시 짓기, 꾸러미 담기 전): BagSweep 계획대로 바닥 장비를 가방에 넣거나 분해하고 바닥에서 지운다.
        /// 가방이 차도 희귀·영웅은 넘침 칸(6)까지, 전설은 늘 들어간다. 가득일 때 일반·고급과 ㉡ 대상은 분해해 강화석으로(Wallet.AddSalvageStones, 재화 쓸 곳 1차 5-3).
        /// 내려놓은 장비는 거두지 않는다(4-4). 검은 화면 뒤라 소리·글 없이, 거둔 결과(도착 글 재료)를 돌려준다.
        /// </summary>
        public BagSweepPlan SweepFloor()
        {
            var drops = SweepDrops();
            var plan = BagSweep.Plan(_bag.Count, Capacity, GearsOf(drops), ImprovesOf(drops), Tuning.AutoSalvageCommon);
            foreach (int i in plan.Take)
            {
                var drop = drops[i];
                _bag.Add(drop.Gear);
                drop.Take();
            }
            foreach (int i in plan.Salvage) drops[i].Take();
            Wallet.AddSalvageStones(plan.SalvageStones);
            if (plan.Taken > 0 || plan.Left > 0 || plan.Salvaged > 0)
                Debug.Log($"[떠날 때 거두기] 바닥 장비 {plan.Taken}개 → 가방 {_bag.Count}/{Capacity}(넘침 {BagSweep.OverflowSlots}) · 분해 {plan.Salvaged}개 강화석 +{plan.SalvageStones} · 두고 감 희귀 이상 {plan.RareLeft} · 일반·고급 {plan.LowLeft}");
            return plan;
        }

        /// <summary>
        /// G: 바닥 장비를 바로 낀다(벗은 장비는 가방으로, 넘침 끝이면 발밑에 — 재화 쓸 곳 1차 5-1). otherRing(Shift+G)이면 반지를 G 자리의 다른 쪽에 낀다.
        /// 내려놓았던 장비를 직접 끼면 내려놓은 기억에서 지운다(4-4).
        /// </summary>
        public void EquipFromFloor(LootDrop drop, bool otherRing = false)
        {
            if (!drop || !drop.Available || WeaponActLocked) return;
            var item = drop.Gear;
            var slot = otherRing ? OtherSlotFor(item) : EquipSlotFor(item);
            if (!Loadout.CanEquip(slot, item)) return;
            drop.Take();
            ForgetDropped(item);
            _swapFromFloor = true;
            try { Equip(item, slot); }
            finally { _swapFromFloor = false; }
        }

        /// <summary>
        /// 장비 굴림 조건(장비 문서 8-2·6장): 부위 보정 = 부위 점수가 가장 낮은 2부위(LootRules.WeakestParts), 가진 전설 효과(장착·가방).
        /// 처치 보상이 쓰고, 궤짝(Chest)은 여기에 빈 자리 채우기·층 보장 부위(OnlyParts)를 더한다.
        /// </summary>
        public GearRollContext RollContext() => new GearRollContext
        {
            BoostedParts = LootRules.WeakestParts(PartScores()),
            OwnedLegendaries = OwnedLegendaries(),
        };

        /// <summary>부위 점수 7칸(GearPart 차례, 2차 6-6 장비 점수 GearMath.ItemScore). 반지는 두 자리 중 약한 쪽, 빈 자리 0(8-2).</summary>
        public double[] PartScores()
        {
            var scores = new double[GearSlots.PartCount];
            foreach (var part in GearSlots.Parts)
                scores[(int)part] = part == GearPart.Ring
                    ? System.Math.Min(GearMath.ItemScore(Equipment[GearSlot.Ring1]), GearMath.ItemScore(Equipment[GearSlot.Ring2]))
                    : GearMath.ItemScore(Equipment[GearSlots.FirstSlotOf(part)]);
            return scores;
        }

        /// <summary>가진 전설 효과(장착·가방. 창고는 M0b에 없음). 같은 효과가 다시 나오면 세기를 위쪽 절반에서 굴린다(6장).</summary>
        public HashSet<LegendaryEffect> OwnedLegendaries()
        {
            var set = new HashSet<LegendaryEffect>();
            foreach (var item in Equipment.Items)
                if (item.Legendary.HasValue) set.Add(item.Legendary.Value);
            foreach (var item in _bag)
                if (item != null && item.Legendary.HasValue) set.Add(item.Legendary.Value);
            return set;
        }

        /// <summary>빈 자리의 부위(반지·목걸이 가운데, 중복 없이 부위 차례). 궤짝 빈 자리 채우기(8-2)가 쓴다.</summary>
        public GearPart[] EmptyParts()
        {
            var list = new List<GearPart>();
            foreach (var slot in Equipment.EmptySlots())
            {
                var part = GearSlots.PartOf(slot);
                if (!list.Contains(part)) list.Add(part);
            }
            return list.ToArray();
        }

        /// <summary>플레이어에게서 range 안 가장 가까운, 내려앉은 바닥 장비.</summary>
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
            if (DungeonUi.Modal != BagWindow) CancelInventoryPointer();
            // 재화 쓸 곳 1차: 저절로 분해(0.25초마다)·알림 묶기(2초)·창을 닫으면 한 번 더 묻기 풀기.
            UpdateBagUpkeep();
            if (!_player || _player.IsDown || DungeonUi.ModalOpen || TimeScaleService.Paused) return;
            if (!_input) _input = _player.GetComponent<PlayerInputReader>();
            // 무기 행동 중에는 G 바로 끼기를 무시한다(쌓아 두지 않음, 5-3).
            if (_input && _input.EquipPressed && !WeaponActLocked)
            {
                var drop = NearestDrop(EquipRange);
                if (drop) EquipFromFloor(drop, _input.EquipOtherPressed);
            }
        }

        /// <summary>
        /// I: 가방 창 열기·닫기, Esc: 닫기. 시간이 멈춘 동안에도 들어야 하므로 키보드에서 직접 읽는다.
        /// 창 없이 입력이 막힌 동안(떠나는 암전·밤 카드 대기: DungeonRoot.HoldForLeaving·HoldForNight)에는 열지 않는다.
        /// 꾸러미를 담은 뒤 바꾼 장비가 장면 다시 불러오기에서 버려지지 않게 하기 위해서다. 평소에는 Blocked가 ModalOpen과 같아 동작이 같다.
        /// </summary>
        void HandleWindowKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.iKey.wasPressedThisFrame)
            {
                if (DungeonUi.Modal == BagWindow) DungeonUi.Close(BagWindow);
                else if (!DungeonUi.ModalOpen && !PlayerInputReader.Blocked && _player && !_player.IsDown) DungeonUi.TryOpen(BagWindow);
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

        /// <summary>처치 보상(3차 초안 4-6, 장비 문서 8-1·8-2: 부위 보정·가진 전설). 둥지 자체는 GroupCleared(nest)가 맡고, 둥지가 부른 굴쥐(NoReward)는 0.</summary>
        void OnEnemyKilled(Enemy e)
        {
            if (!e || e.IsDummy || e.NoReward || e.IsBoss || e.Kind == MonsterKind.Nest) return;
            KillSource source = e.IsElite ? KillSource.Elite
                : e.Kind == MonsterKind.Boar ? KillSource.Boar
                : e.Kind == MonsterKind.Archer ? KillSource.Archer
                : KillSource.Rat;
            int floor = CurrentFloor;
            var bundle = LootRules.RollKill(source, floor, _rng, RollContext());
            RollKillRune(bundle, source);
            Vector2 pos = e.Position;
            LootSpawner.Spawn(bundle, pos, AwayFromPlayer(pos), LootSpawner.KillDelay);
        }

        /// <summary>둥지 정리(3차 초안 4-6): 장비 50%, 강화석 0.5 × 배율, 골드 무더기 3개. 룬 80‰(따로 흐르는 난수).</summary>
        void OnGroupCleared(int group, bool nest, Vector2 pos)
        {
            if (!nest) return;
            var bundle = LootRules.RollKill(KillSource.NestClear, CurrentFloor, _rng, RollContext());
            RollKillRune(bundle, KillSource.NestClear);
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
            // 바닥 상세는 LootLabels의 의도적인 가리키기에서만 그린다.
        }

        /// <summary>
        /// 가장 가까운 바닥 장비 카드(2차 7-7 획득 카드, 장비 문서 5-4 카드 글·8-5·8-6): 등급색 이름(등급 + 종류), 카드 줄(CardFor), 키 안내.
        /// 반지면 [Shift+G] 다른 쪽도 안내한다.
        /// </summary>
        public Rect GroundCardRect { get; private set; }

        public void DrawGroundHoverCard(LootDrop drop, Rect labelRect)
        {
            if (!drop || !drop.Available || DungeonUi.ModalOpen || !_player || _player.IsDown) return;
            DungeonUi.Begin();
            GUI.depth = -10;
            var item = drop.Gear;
            var card = CardFor(item);
            var gradeColor = LootVisuals.GradeColor(item.Grade);
            const float width = 420f, padding = 14f, footerPadding = 24f, textX = 112f;
            string keys = item.Part == GearPart.Ring
                ? "[F] 가방 · [G] 바로 끼기\n[Shift+G] 다른 반지 칸에 끼기"
                : "[F] 가방 · [G] 바로 끼기";
            bool canPickup = InteractionSystem.Instance && InteractionSystem.Instance.Current == drop;
            bool canEquip = NearestDrop(EquipRange) == drop;
            if (!canPickup) keys = keys.Replace("[F] 가방 · ", "");
            if (!canEquip) keys = canPickup ? "[F] 가방 · [I] 가방에서 상세 보기" : "가까이에서 선택 · [I] 가방에서 상세 보기";
            float titleHeight = Mathf.Max(28f, DungeonUi.Bold.CalcHeight(new GUIContent(item.DisplayName), width-textX-padding));
            float bodyHeight = card.Lines.Count * CardLineHeight;
            float contentHeight = Mathf.Max(88f, titleHeight + 8f + bodyHeight);
            float footerHeight = Mathf.Max(28f, DungeonUi.Small.CalcHeight(new GUIContent(keys), width-footerPadding*2f));
            float height = padding + contentHeight + 18f + footerHeight + 20f;
            float x = labelRect.xMax + 12f;
            if (x + width > DungeonUi.Width - 12f) x = labelRect.xMin - width - 12f;
            var r = DungeonUi.KeepOnScreen(new Rect(x, labelRect.y, width, height), 104f);
            GroundCardRect = r;
            DungeonUi.Box(r, 0.96f);
            DungeonUi.Fill(new Rect(r.x, r.y, 4f, r.height), gradeColor);
            DrawIcon(new Rect(r.x+padding,r.y+padding,88f,88f),item);
            var prev = GUI.color;
            GUI.color = gradeColor;
            GUI.Label(new Rect(r.x+textX,r.y+padding,r.width-textX-padding,titleHeight),item.DisplayName,DungeonUi.Bold);
            DrawLines(new Rect(r.x+textX,r.y+padding+titleHeight+8f,r.width-textX-padding,bodyHeight),card.Lines,canEquip);
            float footerY = r.yMax-20f-footerHeight;
            DungeonUi.Strip(new Rect(r.x+padding,footerY-8f,r.width-padding*2f,1f),DungeonUi.IronEdge);
            GUI.color = HintColor;
            GUI.Label(new Rect(r.x+footerPadding,footerY,r.width-footerPadding*2f,footerHeight),keys,DungeonUi.Small);
            GUI.color = prev;
        }

        /// <summary>카드 줄을 위에서부터 한 줄씩(넘치면 말줄임, 전체 글은 툴팁).</summary>
        static void DrawLines(Rect area, List<CardLine> lines, bool groundEquipKeys = true)
        {
            var prev = GUI.color;
            for (int i = 0; i < lines.Count; i++)
            {
                GUI.color = lines[i].Color;
                string text = groundEquipKeys ? lines[i].Text : lines[i].Text.Replace("G: ", "비교: ");
                DungeonUi.CompactLabel(new Rect(area.x, area.y + i * CardLineHeight, area.width, CardLineHeight), text, DungeonUi.Small);
            }
            GUI.color = prev;
        }

        // ── 카드(장비 문서 5-4 카드 글, 8-5 비교, 8-6 반지) ─────────────────

        /// <summary>카드 한 줄(글과 색).</summary>
        readonly struct CardLine
        {
            public readonly string Text;
            public readonly Color Color;

            public CardLine(string text, Color color)
            {
                Text = text;
                Color = color;
            }
        }

        /// <summary>'지금 끼면' 비교 한 판(2차 6-6 종합 변화율, 장비 문서 8-5 부위 문턱).</summary>
        sealed class Comparison
        {
            public GearSlot Slot;
            /// <summary>그 자리에서 벗게 될 장비(빈 자리면 null).</summary>
            public GearItem Replaced;
            public StatSheet After;
            public Loadout Loadout;
            public int Permille;
            public CompareMark Mark;
            /// <summary>같은 종류·같은 등급에서 굴린 값만 다름: 문턱과 관계없이 ▲ 없이 숫자만(8-5 ▲ 남발 방지).</summary>
            public bool RollOnly;
        }

        /// <summary>장비 카드(바닥 카드와 가방 상세가 같이 씀). 장비와 능력치(Sheet)가 그대로면 다시 만들지 않는다.</summary>
        sealed class Card
        {
            public GearItem Item;
            public StatSheet Base;
            /// <summary>이미 낀 자리(없으면 null).</summary>
            public GearSlot? EquippedSlot;
            /// <summary>G 자리와 비교(낀 장비면 null).</summary>
            public Comparison G;
            /// <summary>반지: G 자리의 다른 쪽(Shift+G)과 비교.</summary>
            public Comparison Other;
            public readonly List<CardLine> Lines = new List<CardLine>();
        }

        static readonly string[] StatNames =
        {
            "최대 체력", "공격력", "방어", "치명타 확률", "치명타 피해", "공격 속도", "이동 속도",
            "스킬 재사용 감소", "스킬 피해", "보스 피해", "체력 흡수", "초당 체력 재생", "처치 시 회복",
        };

        Card CardFor(GearItem item)
        {
            if (_card != null && ReferenceEquals(_card.Item, item) && _card.Base != null && ReferenceEquals(_card.Base, Sheet)) return _card;
            _card = BuildCard(item);
            return _card;
        }

        /// <summary>
        /// 카드 줄: 부위·아이템 레벨·강화, 기본 능력치, 무기면 초당 휘두르기·한 방 세기·치명 %/%·무너뜨리기(끼고 난 최종 숫자, 3-1),
        /// 무기가 아니면 종류 고유 줄, 전설 ★(첫 옵션 줄, 겹치면 회색 '겹침 — 작동 안 함'), 옵션 줄, 끼지 않은 장비면 비교 줄.
        /// </summary>
        Card BuildCard(GearItem item)
        {
            var before = Sheet ?? Compute(Equipment);
            var card = new Card { Item = item, Base = Sheet, EquippedSlot = Equipment.SlotOf(item) };
            if (!card.EquippedSlot.HasValue)
            {
                var slot = EquipSlotFor(item);
                card.G = CompareAt(item, slot, before);
                if (item.Part == GearPart.Ring) card.Other = CompareAt(item, GearSlots.OtherRing(slot), before);
            }
            var shown = card.G != null ? card.G.After : before;
            var lines = card.Lines;
            lines.Add(new CardLine(GearSlots.Name(item.Part) + " · 아이템 레벨 " + item.ItemLevel + " · +" + item.Enhance + "/" + item.EnhanceCap, HintColor));
            lines.Add(new CardLine(BaseStatText(item), Color.white));
            if (item.IsWeapon)
            {
                var rule = item.WeaponRule ?? shown.WeaponRule;
                lines.Add(new CardLine("초당 휘두르기 " + StatCalc.SwingsPerSecond(rule, shown.AttackSpeedPermille).ToString("0.00") +
                                       " · 한 방 " + Mathf.RoundToInt((float)rule.AverageHitPercent) + "%", Color.white));
                lines.Add(new CardLine("치명 " + Pct(shown.CritChancePermille) + " / " + Pct(shown.CritDamagePermille) + " · 무너뜨리기 " + rule.PoiseWord, Color.white));
                // 오른쪽 클릭 무기 행동 줄(세 무기만, 2-1·3-1·4-1).
                string act = WeaponActRules.CardLine(item.BaseId);
                if (act != null) lines.Add(new CardLine(act, HintColor));
            }
            else
                foreach (var b in item.Base.Intrinsic)
                    lines.Add(new CardLine("고유 " + OptionKinds.Format(b.Kind, b.Value), IntrinsicColor));
            if (item.IsLegendary)
            {
                // 전설 효과는 첫 옵션 줄(5-4). 같은 효과가 더 센 다른 장비에 있으면 회색 '겹침 — 작동 안 함'(6장).
                var worn = card.G != null ? card.G.Loadout : Equipment;
                bool off = LegendaryTable.IsSuppressed(item, worn.Items);
                lines.Add(new CardLine(GearNaming.LegendaryLine(item) + (off ? " · 겹침 — 작동 안 함" : ""), off ? SameColor : LootVisuals.GradeColor(Grade.Legendary)));
            }
            foreach (var o in item.Options) lines.Add(new CardLine(GearNaming.OptionLine(o), OptionColor));
            // 룬 홈 줄(옵션 줄 뒤, 6-5)과 이 무기에서 효과 없는 룬의 회색 줄.
            AddRuneCardLines(lines, item);
            if (card.EquippedSlot.HasValue)
                lines.Add(new CardLine("장착 중 · " + GearSlots.SlotName(card.EquippedSlot.Value), SameColor));
            else
                AddCompareLines(card, before);
            return card;
        }

        /// <summary>그 자리에 끼면 어떻게 바뀌나(장착 복사본에 끼워 StatCalc로 다시 계산).</summary>
        Comparison CompareAt(GearItem item, GearSlot slot, StatSheet before)
        {
            var loadout = Equipment.Clone();
            loadout.TryEquip(slot, item, out _);
            var after = Compute(loadout);
            int permille = GearScore.CompositePermille(before, after);
            var replaced = Equipment[slot];
            return new Comparison
            {
                Slot = slot,
                Replaced = replaced,
                After = after,
                Loadout = loadout,
                Permille = permille,
                Mark = GearScore.Mark(permille, item.Part),
                RollOnly = RollOnlyDiff(replaced, item),
            };
        }

        /// <summary>
        /// 비교 줄(8-5·8-6): 맨 위는 G 자리와 비교한 종합 변화(부위 문턱 ▲ = ▼, 반지는 'G: 반지 2 대신 끼기 (+1.8%)'),
        /// 반지면 둘째 줄 회색 '반지 1 대신이면 −0.4%', 가장 크게 바뀐 능력치 2줄, 무기 종류가 다르면 '방식 다름'과 공격 방식 줄.
        /// </summary>
        void AddCompareLines(Card card, StatSheet before)
        {
            var item = card.Item;
            var g = card.G;
            var lines = card.Lines;
            bool styleDiff = item.IsWeapon && Equipped != null && item.BaseId != Equipped.BaseId;
            string head = item.Part == GearPart.Ring
                ? "G: " + GearSlots.SlotName(g.Slot) + (g.Replaced != null ? " 대신 끼기" : "에 끼기") + " (" + SignedPct(g.Permille) + ")"
                : "지금 끼면 " + SignedPct(g.Permille);
            if (!g.RollOnly) head = MarkGlyph(g.Mark) + " " + head;
            if (styleDiff) head += " · 방식 다름";
            lines.Add(new CardLine(head, g.RollOnly ? Color.white : MarkColor(g.Mark)));
            // 재화 쓸 곳 1차 3장: 벗게 될 장비의 강화는 옮겨지지 않는다(계승 없음). 숫자 변화는 이미 그 강화를 넣고 셈한 값이다.
            if (g.Replaced != null && g.Replaced.Enhance >= 1) lines.Add(new CardLine(EnhanceLostText(g.Replaced), DownColor));
            if (card.Other != null)
            {
                var o = card.Other;
                string other = GearSlots.SlotName(o.Slot) + (o.Replaced != null ? " 대신이면 " : "에 끼면 ") + SignedPct(o.Permille);
                lines.Add(new CardLine(other, SameColor));
            }
            foreach (var kind in TopChanges(before, g.After, 2))
            {
                int d = g.After.Get(kind) - before.Get(kind);
                string text = StatNames[(int)kind] + " " + SignedStat(kind, d);
                lines.Add(g.RollOnly ? new CardLine(text, Color.white) : new CardLine(text + " " + Arrow(d), d > 0 ? UpColor : DownColor));
            }
            if (styleDiff) AddStyleLines(lines, before, g.After);
        }

        /// <summary>무기 종류가 다를 때(8-5): 초당 휘두르기, 한 방 세기, 치명 확률·피해, 무너뜨리기, 한 번에 최대.</summary>
        static void AddStyleLines(List<CardLine> lines, StatSheet before, StatSheet after)
        {
            var a = before.WeaponRule;
            var b = after.WeaponRule;
            if (a == null || b == null) return;
            double s0 = StatCalc.SwingsPerSecond(a, before.AttackSpeedPermille);
            double s1 = StatCalc.SwingsPerSecond(b, after.AttackSpeedPermille);
            lines.Add(Trend("초당 휘두르기 " + s0.ToString("0.00") + " → " + s1.ToString("0.00"), s1 - s0));
            lines.Add(Trend("한 방 " + Mathf.RoundToInt((float)a.AverageHitPercent) + "% → " + Mathf.RoundToInt((float)b.AverageHitPercent) + "%",
                b.AverageHitPercent - a.AverageHitPercent));
            lines.Add(Trend("치명 " + Pct(before.CritChancePermille) + "/" + Pct(before.CritDamagePermille) + " → " +
                            Pct(after.CritChancePermille) + "/" + Pct(after.CritDamagePermille),
                StatCalc.ExpectedCritMultiplier(after) - StatCalc.ExpectedCritMultiplier(before)));
            lines.Add(Trend("무너뜨리기 " + a.PoiseWord + " → " + b.PoiseWord, b.PoisePerSecond - a.PoisePerSecond));
            lines.Add(Trend("한 번에 최대 " + a.MaxTargets + " → " + b.MaxTargets, b.MaxTargets - a.MaxTargets));
        }

        static CardLine Trend(string text, double delta)
        {
            if (delta > 1e-3) return new CardLine(text + " ▲", UpColor);
            if (delta < -1e-3) return new CardLine(text + " ▼", DownColor);
            return new CardLine(text, SameColor);
        }

        /// <summary>같은 종류·같은 등급·같은 아이템 레벨·강화·전설이고 옵션 종류도 같아 굴린 값만 다른가(8-5).</summary>
        static bool RollOnlyDiff(GearItem a, GearItem b)
        {
            if (a == null || b == null || ReferenceEquals(a, b)) return false;
            if (a.BaseId != b.BaseId || a.Grade != b.Grade || a.ItemLevel != b.ItemLevel || a.Enhance != b.Enhance || a.LegendaryId != b.LegendaryId) return false;
            if (a.Options.Count != b.Options.Count) return false;
            foreach (var o in a.Options)
                if (!HasOption(b, o.Kind)) return false;
            return true;
        }

        static bool HasOption(GearItem item, OptionKind kind)
        {
            foreach (var o in item.Options)
                if (o.Kind == kind) return true;
            return false;
        }

        /// <summary>
        /// 가장 크게 바뀐 능력치 count개(8-5 대표 줄). 크기는 판단 지수(2-2)에 미치는 몫으로 어림한다:
        /// 체력·공격력은 비율, 방어는 ÷ (1000 + 방어), ‰ 능력치는 %p, 재생·처치 회복은 체력 대비.
        /// </summary>
        static List<StatKind> TopChanges(StatSheet before, StatSheet after, int count)
        {
            var list = new List<StatKind>();
            var weights = new List<double>();
            for (int i = 0; i < StatSheet.KindCount; i++)
            {
                var kind = (StatKind)i;
                int d = after.Get(kind) - before.Get(kind);
                if (d == 0) continue;
                double w = ChangeWeight(kind, before, d);
                int at = 0;
                while (at < weights.Count && weights[at] >= w) at++;
                list.Insert(at, kind);
                weights.Insert(at, w);
            }
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            return list;
        }

        static double ChangeWeight(StatKind kind, StatSheet before, int diff)
        {
            double d = System.Math.Abs(diff);
            double hp = System.Math.Max(1, before.MaxHp);
            switch (kind)
            {
                case StatKind.MaxHp: return d / hp;
                case StatKind.Attack: return d / System.Math.Max(1, before.Attack);
                case StatKind.Defense: return d / (1000.0 + before.Defense);
                case StatKind.HpRegen: return d * 10.0 / hp;
                case StatKind.OnKillHeal: return d * 5.0 / hp;
                default: return d / 1000.0;
            }
        }

        /// <summary>‰로 보이는 능력치(치명·공속·이동·재사용·스킬·보스·흡수).</summary>
        static bool IsPermilleStat(StatKind kind) => kind >= StatKind.CritChance && kind <= StatKind.LifeSteal;

        static string StatValue(StatKind kind, int value) => IsPermilleStat(kind) ? Pct(value) : value.ToString();

        /// <summary>바뀐 값: 고정값은 '+24', ‰ 능력치는 '+3%p'.</summary>
        static string SignedStat(StatKind kind, int diff) => IsPermilleStat(kind) ? SignedPct(diff) + "p" : SignedInt(diff);

        /// <summary>‰ → '7%', '7.5%'(소수 한 자리, 0이면 뺌).</summary>
        static string Pct(int permille)
        {
            int abs = Mathf.Abs(permille);
            string s = (abs / 10) + (abs % 10 != 0 ? "." + (abs % 10) : "") + "%";
            return permille < 0 ? "−" + s : s;
        }

        static string SignedPct(int permille) => (permille >= 0 ? "+" : "−") + Pct(Mathf.Abs(permille));
        static string SignedInt(int value) => (value >= 0 ? "+" : "−") + Mathf.Abs(value);
        static string Arrow(int diff) => diff > 0 ? "▲" : diff < 0 ? "▼" : "=";
        static string MarkGlyph(CompareMark mark) => mark == CompareMark.Up ? "▲" : mark == CompareMark.Down ? "▼" : "=";
        static Color MarkColor(CompareMark mark) => mark == CompareMark.Up ? UpColor : mark == CompareMark.Down ? DownColor : SameColor;

        /// <summary>부위 대표 수치(장착 알림·카드 둘째 줄): 무기·반지 '공격력 n', 갑옷·투구·장갑·장화 '방어 n · 체력 n', 목걸이 '체력 n'.</summary>
        static string BaseStatText(GearItem item)
        {
            if (item == null) return "";
            switch (item.Part)
            {
                case GearPart.Weapon:
                case GearPart.Ring:
                    return "공격력 " + item.Attack;
                case GearPart.Amulet:
                    return "체력 " + item.Hp;
                default:
                    return "방어 " + item.Defense + " · 체력 " + item.Hp;
            }
        }

        /// <summary>
        /// 가방 창(IMGUI 기능만, 배치는 Unity 개발 단계): 왼쪽 장착 8자리(4-1)·강화석·골드·가방, 가운데 능력치 표(StatSheet 13줄 + 초당 휘두르기,
        /// 끼지 않은 장비를 고르면 끼었을 때 줄마다 ▲▼), 오른쪽 고른 장비 상세(카드와 같은 줄)와 끼기·반지 1/2에 끼기·벗기(8-6).
        /// </summary>
        void DrawBag()
        {
            DrawApprovedBagV5();
        }

        /// <summary>
        /// 상세 단추(8-6): 가방 반지 '반지 1에 끼기 / 반지 2에 끼기', 그 밖의 가방 장비 '끼기'(G 자리), 가방 장비는 같은 줄에 '분해 (+n석)'·'내려놓기'(마을 '버리기', 재화 쓸 곳 1차 4-2),
        /// 낀 반지 '벗기 / 반지 n으로 옮기기', 낀 목걸이 '벗기', 낀 무기·갑옷·투구·장갑·장화는 '장착 중'(바꾸기만 됨). 낀 장비는 분해하지 않는다.
        /// </summary>
        void DrawDetailButtons(Rect btn, Card card)
        {
            var item = card.Item;
            float half = (btn.width - 10f) * .5f;
            var left = new Rect(btn.x, btn.y, half, btn.height);
            var right = new Rect(btn.x + half + 10f, btn.y, half, btn.height);
            if (WeaponActLocked)
            {
                // 무기 행동 중(5-3): 장착 단추는 회색.
                bool was = GUI.enabled;
                GUI.enabled = false;
                GUI.Button(btn, WeaponActLockedText);
                GUI.enabled = was;
                return;
            }
            if (card.EquippedSlot.HasValue)
            {
                var slot = card.EquippedSlot.Value;
                // 낀 장비: 줄 오른쪽에 회색 '분해'·'내려놓기'와 말풍선 '낀 장비는 벗은 뒤에'(재화 쓸 곳 1차 4-2, Inventory.Salvage.cs). 남은 칸에 벗기·옮기기·장착 중.
                btn = DrawEquippedLockedButtons(btn, item, out left, out right);
                if (item.Part == GearPart.Ring)
                {
                    if (GUI.Button(left, "벗기")) Unequip(slot);
                    if (GUI.Button(right, GearSlots.SlotName(GearSlots.OtherRing(slot)) + "로 옮기기")) Equip(item, GearSlots.OtherRing(slot));
                }
                else if (GearSlots.CanUnequip(slot))
                {
                    if (GUI.Button(btn, "벗기")) Unequip(slot);
                }
                else
                {
                    bool enabled = GUI.enabled;
                    GUI.enabled = false;
                    GUI.Button(btn, "장착 중");
                    GUI.enabled = enabled;
                }
                return;
            }
            // 가방 장비: 받은 줄을 나눠 끼기(반지는 반지 1·2)와 분해·내려놓기(마을은 버리기)를 같은 줄에(재화 쓸 곳 1차 4-2·4-3, Inventory.Salvage.cs).
            DrawBagItemButtons(btn, item);
        }

        /// <summary>능력치 표: 이름, 지금 값, (끼지 않은 장비를 골랐으면) 끼었을 때 바뀌는 값과 ▲▼. 마지막 줄은 초당 휘두르기(보이기만, 3-1).</summary>
        void DrawStatTable(Rect area, StatSheet before, StatSheet after)
        {
            const float row = 28f;
            var prev = GUI.color;
            for (int i = 0; i < StatSheet.KindCount; i++)
            {
                var kind = (StatKind)i;
                float y = area.y + i * row;
                int now = before.Get(kind);
                string value = StatValue(kind, now);
                int d = after != null ? after.Get(kind) - now : 0;
                DrawStatRow(new Rect(area.x, y, area.width, row), StatNames[i], value, d != 0 ? SignedStat(kind, d) + " " + Arrow(d) : null, d);
            }
            double s0 = StatCalc.SwingsPerSecond(before.WeaponRule, before.AttackSpeedPermille);
            string swing = null;
            int sd = 0;
            if (after != null)
            {
                double s1 = StatCalc.SwingsPerSecond(after.WeaponRule, after.AttackSpeedPermille);
                if (System.Math.Abs(s1 - s0) > 0.005)
                {
                    sd = s1 > s0 ? 1 : -1;
                    swing = "→ " + s1.ToString("0.00") + " " + Arrow(sd);
                }
            }
            DrawStatRow(new Rect(area.x, area.y + StatSheet.KindCount * row, area.width, row), "초당 휘두르기", s0.ToString("0.00"), swing, sd);
            GUI.color = prev;
        }

        void DrawStatRow(Rect row, string name, string value, string change, int direction)
        {
            GUI.color = DungeonUi.BoneDim;
            GUI.Label(new Rect(row.x, row.y, 132f, row.height), name, DungeonUi.Small);
            GUI.color = Color.white;
            FitStat(_statValue,value,68f,18);
            GUI.Label(new Rect(row.x + 136f, row.y, 68f, row.height), value, _statValue);
            if (change == null) return;
            GUI.color = direction > 0 ? UpColor : DownColor;
            FitStat(_statChange,change,row.width-212f,16);
            GUI.Label(new Rect(row.x + 212f, row.y, row.width - 212f, row.height), change, _statChange);
            GUI.color = Color.white;
        }

        static void FitStat(GUIStyle style,string text,float width,int maximum)
        {
            style.fontSize=maximum;
            var content=new GUIContent(text);
            while(style.fontSize>13 && style.CalcSize(content).x>width)style.fontSize--;
        }

        void SelectItem(GearItem item) { if(!ReferenceEquals(_selected,item))_detailScroll=Vector2.zero;_selected=item; }

        /// <summary>
        /// 장비 21종 아이콘(ItemIconArt, id = 종류 id). 알려지지 않은 종류는 부위 이름으로 표시한다.
        /// </summary>
        void DrawIcon(Rect rect, GearItem item)
        {
            string id = item.Base.IconId;
            ItemIconArt.Draw(rect, id);
            if (System.Array.IndexOf(ItemIconArt.Ids, id) >= 0) return;
            if (_iconCaption == null) _iconCaption = new GUIStyle(DungeonUi.Bold) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _iconCaption.fontSize = Mathf.Clamp(Mathf.RoundToInt(rect.height * 0.26f), 13, 30);
            var prev = GUI.color;
            GUI.color = new Color(DungeonUi.Bone.r, DungeonUi.Bone.g, DungeonUi.Bone.b, 0.9f);
            GUI.Label(rect, GearSlots.Name(item.Part), _iconCaption);
            GUI.color = prev;
        }

        bool DrawItemSlot(Rect rect, GearItem item, bool selected)
        {
            if(item==null){DungeonUi.Slot(rect);return false;}
            bool clicked=GUI.Button(rect,new GUIContent("",item.DisplayName),GUIStyle.none);
            DungeonUi.Slot(rect);
            if(rect.Contains(Event.current.mousePosition))DungeonUi.Fill(new Rect(rect.x+8,rect.y+8,rect.width-16,rect.height-16),new Color(1,1,1,.06f));
            var gradeColor = LootVisuals.GradeColor(item.Grade);
            DungeonUi.Fill(new Rect(rect.x+7,rect.y+7,rect.width-14,rect.height-14),new Color(gradeColor.r,gradeColor.g,gradeColor.b,selected ? .14f : .09f));
            DrawIcon(new Rect(rect.x+6,rect.y+4,rect.width-12,rect.height-18),item);
            DungeonUi.Outline(new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10),new Color(gradeColor.r,gradeColor.g,gradeColor.b,.85f),1f);
            if(selected)UiSkinArt.Selection(rect);
            // 색 이외의 등급 단서: 일반 한 점부터 전설 다섯 점. 상세 이름에도 등급을 적는다.
            int marks=(int)item.Grade+1;
            for(int i=0;i<marks;i++)DungeonUi.Fill(new Rect(rect.x+12+i*9,rect.yMax-13,5,3),DungeonUi.Bone);
            DrawRuneMarks(rect, item);
            return clicked;
        }
    }
}
