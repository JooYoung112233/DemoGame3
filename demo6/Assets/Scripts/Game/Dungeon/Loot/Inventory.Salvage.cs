using System.Collections.Generic;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Stats;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 가방 칸·줍기 판정·분해·내려놓기(기획/재화-쓸-곳-1차.md 3장·4-2~4-4·5장, 작성자 B 11-3). 던전과 마을이 같은 가방(같은 가방 창)을 쓴다.
    /// 칸 = 20·25·30(꾸러미의 대장간 물건, ForgeShop.BagCapacity), 넘침 끝 = 칸 + 6. '가득'(BagFull) = 개수 ≥ 칸(LootDrop이 읽는 뜻 그대로).
    /// 줍기 판정은 Core BagRules.Decide 한 곳: F 줍기(Manual)·저절로 줍기(Auto)·떠날 때 거두기(Sweep)·바닥에서 G로 바꿔 껴 벗은 장비(Swap).
    /// ㉡ 저절로 분해(Tuning.AutoSalvageCommon, 기본 켬): 끼어도 나아지지 않는 일반 +0은 15 밖으로 멀어지면 그 자리에서 강화석으로. 가방이 가득이어도 0.25초마다 살핀다.
    /// 분해 강화석은 Wallet.AddSalvageStones 한 곳으로만 들어간다(묶음 5 주머니가 따로 셈). 마을에서 바꾸면 그 자리에서 꾸러미에 담는다(SyncCarryIfTown).
    /// 내려놓은 장비는 참조로 기억해(_dropped) 저절로 줍기·저절로 분해·떠날 때 거두기에서 뺀다. F·G로 직접 주우면 지운다. 장면이 바뀌면 가방과 함께 사라진다.
    /// 새 정적 값은 없다(모두 이 장면 가방의 몫). UI는 IMGUI 기능만(배치는 Unity 개발 단계). 화면 글은 Core ForgeText 한 곳.
    /// </summary>
    public sealed partial class Inventory
    {
        /// <summary>바닥 일반 장비를 살피는 간격(실제 시간 초, 5-2). LootDrop은 가득이면 줍기를 부르지 않아 가방 쪽이 따로 살핀다.</summary>
        const float AutoSalvageScanSeconds = 0.25f;
        /// <summary>저절로 분해 알림을 한 줄로 모으는 시간(실제 시간 초, 11-3).</summary>
        const float AutoSalvageNoticeSeconds = 2f;
        /// <summary>한 번 더 묻기가 풀리는 시간(실제 시간 초, 4-3).</summary>
        const float ConfirmSeconds = 4f;
        /// <summary>한 번 더 묻는 단추 바탕(붉게, 4-3).</summary>
        static readonly Color ConfirmButtonTint = new Color(0.86f, 0.24f, 0.2f, 1f);

        /// <summary>한 번 더 묻는 단추 종류.</summary>
        enum BagPress { None, Salvage, PutDown }

        /// <summary>내려놓은 장비(참조로 기억, 4-4). Init·ImportFrom에서 비운다.</summary>
        readonly HashSet<GearItem> _dropped = new HashSet<GearItem>();
        /// <summary>
        /// 발밑에 둔 뒤 아직 내려앉지 않은 바닥 장비(LeaveAtFeet). LootDrop.Land가 '새 드랍'처럼 GearDropped를 내므로 탐험 기록이 이것을 건너뛴다
        /// (TakePutDownLanding, 반박 검토: 내려놓을 때마다 '가장 긴 빈 구간'이 거짓으로 끊기지 않게). 내려앉았으면 UpdateBagUpkeep이 지운다.
        /// </summary>
        readonly List<LootDrop> _putDownLandings = new List<LootDrop>();
        readonly List<LootDrop> _salvageScan = new List<LootDrop>();
        /// <summary>㉡ 판정용 '나아짐' 답(능력치 Sheet가 바뀌면 비움).</summary>
        readonly Dictionary<GearItem, bool> _improvesCache = new Dictionary<GearItem, bool>();
        StatSheet _improvesSheet;
        float _nextSalvageScan;
        int _autoSalvageCount;
        int _autoSalvageStones;
        string _autoSalvageName;
        float _autoSalvageNoticeAt;
        GearItem _confirmItem;
        BagPress _confirmPress;
        float _confirmUntil;
        /// <summary>EquipFromFloor가 Equip을 부르는 동안만 켠다(벗은 장비 넘침 판정, 5-1).</summary>
        bool _swapFromFloor;
        GUIStyle _bagButtonStyle;
        GUIStyle _bagButtonBase;
        readonly GUIContent _bagButtonContent = new GUIContent();

        // ── 칸과 넘침(5-1) ──

        /// <summary>가방 칸 수(20·25·30). Init은 20, ImportFrom은 꾸러미의 대장간 물건(ForgeShop.BagCapacity). 모루 창이 산 뒤 SetCapacity로 바꾼다.</summary>
        public int Capacity { get; private set; } = BagRules.BaseCapacity;
        /// <summary>넘침 끝(칸 + 6 = 26·31·36).</summary>
        public int HardCap => BagRules.HardCap(Capacity);
        /// <summary>넘침 칸까지 다 찼는가(개수 ≥ 넘침 끝). 반지·목걸이 벗기를 막고, 바닥에서 G로 바꿔 낀 장비는 발밑에 둔다.</summary>
        public bool OverflowFull => _bag.Count >= HardCap;
        /// <summary>거의 참(개수 ≥ 칸 − 4, 권양기 창 덧줄 5-4).</summary>
        public bool NearlyFull => BagRules.NearlyFull(_bag.Count, Capacity);
        /// <summary>가방 창에서 지금 고른 장비(없으면 null).</summary>
        public GearItem Selected => _selected;

        /// <summary>칸 수를 바꾼다(가방 쇠틀을 산 뒤 ForgeShop.BagCapacity로). 든 장비는 그대로다.</summary>
        public void SetCapacity(int capacity) => Capacity = Mathf.Max(0, capacity);

        /// <summary>Init(data 없음 → 20칸)·ImportFrom(꾸러미의 대장간 물건 칸)이 부른다: 칸 수와 내려놓은 기억·한 번 더 묻기·저절로 분해 알림을 처음으로.</summary>
        void ResetBagState(CarryData data)
        {
            Capacity = data != null ? ForgeShop.BagCapacity(data) : BagRules.BaseCapacity;
            _dropped.Clear();
            _improvesCache.Clear();
            _improvesSheet = null;
            ClearConfirm();
            _autoSalvageCount = 0;
            _autoSalvageStones = 0;
            _autoSalvageName = null;
        }

        /// <summary>
        /// 지금 끼면 나아지는가: 카드 첫 줄과 같은 비교(G 자리, GearScore.Mark == Up, 부위 문턱 장비 문서 8-5). 굴림만 다른 경우도 숫자로 문턱을 넘으면 ▲로 본다.
        /// 빈 자리(반지·목걸이)를 채우면 ▲다. ㉡의 '나아지지 않음'은 이 함수 하나를 쓴다(10장 묶음 4). 낀 장비는 false.
        /// </summary>
        public bool Improves(GearItem item)
        {
            if (item == null || Equipment.IsEquipped(item)) return false;
            var before = Sheet ?? Compute(Equipment);
            return CompareAt(item, EquipSlotFor(item), before).Mark == CompareMark.Up;
        }

        /// <summary>마을이면(TownRoot가 있음) 바뀐 장착·가방·룬 주머니를 그 자리에서 꾸러미에 담는다(10장 묶음 2와 겹쳐도 안전하게).</summary>
        public void SyncCarryIfTown()
        {
            if (Wallet.InTown && TownRoot.Instance) ExportTo(ProfileCarry.Ensure());
        }

        // ── 줍기 판정(5-3) ──

        /// <summary>
        /// PickUp의 판정. F 대상(InteractionSystem.Current)이거나 F 거리 안(이름표 눌러 줍기, Inventory.ClickPickupV45)이면 F 줍기,
        /// 그 밖(LootDrop의 15 밖 저절로 줍기)은 저절로 줍기로 본다. 내려놓은 장비의 저절로 줍기는 늘 남김(4-4).
        /// </summary>
        PickupAction DecidePickup(LootDrop drop, out bool manual)
        {
            var item = drop.Gear;
            manual = IsManualPickup(drop);
            if (!manual && _dropped.Contains(item)) return PickupAction.Leave;
            return BagRules.Decide(manual ? PickupReason.Manual : PickupReason.Auto, item.Grade, item.Enhance,
                manual || ImprovesForAutoSalvage(item), _bag.Count, Capacity, Tuning.AutoSalvageCommon, Tuning.AutoPickupReserve);
        }

        /// <summary>F로 줍는가: F 대상이거나 F 거리 안(저절로 줍기는 15 밖에서만 불려 겹치지 않는다).</summary>
        bool IsManualPickup(LootDrop drop)
        {
            var interaction = InteractionSystem.Instance;
            if (interaction && interaction.Current == drop) return true;
            return _player && (drop.Position - _player.Position).sqrMagnitude <= drop.Range * drop.Range;
        }

        /// <summary>
        /// ㉡ 판정용 '나아짐': ㉡ 대상이 될 수 있는 장비(켬·일반·강화 +0)만 실제로 견준다(저절로 줍기는 매 프레임 불릴 수 있어 셈을 아낀다).
        /// 그 밖에는 true(㉡ 대상 아님)로 둔다. Decide는 improves를 ㉡ 판정에만 쓴다. 능력치(Sheet)가 그대로면 지난 답을 다시 쓴다.
        /// </summary>
        bool ImprovesForAutoSalvage(GearItem item)
        {
            if (!(Tuning.AutoSalvageCommon && item.Grade == Grade.Common && item.Enhance == 0)) return true;
            if (!ReferenceEquals(_improvesSheet, Sheet))
            {
                _improvesCache.Clear();
                _improvesSheet = Sheet;
            }
            if (!_improvesCache.TryGetValue(item, out bool up)) _improvesCache[item] = up = Improves(item);
            return up;
        }

        /// <summary>F 줍기 막힘 알림: "가방이 가득 찼다 (20/20) — [I] 가방에서 분해하거나 내려놓자".</summary>
        string PickupBlockedText() => ForgeText.BagFullNotice(_bag.Count, Capacity);

        /// <summary>바닥 장비를 그 자리에서 분해한다(㉡, 5-2). 강화석은 Wallet, 알림은 2초 안 것을 한 줄로 모은다.</summary>
        bool AutoSalvage(LootDrop drop)
        {
            if (!drop || !drop.Available) return false;
            var item = drop.Gear;
            int stones = SalvageRules.Stones(item);
            drop.Take();
            Wallet.AddSalvageStones(stones);
            QueueAutoSalvageNotice(item, stones);
            return true;
        }

        /// <summary>
        /// Update마다(창·멈춤과 상관없이): 저절로 분해 알림 내보내기, 창을 닫거나 4초가 지나면 한 번 더 묻기 풀기,
        /// 던전에서 0.25초(실제 시간)마다 바닥 일반 장비 저절로 분해(가방이 가득이어도, 5-2).
        /// </summary>
        void UpdateBagUpkeep()
        {
            FlushAutoSalvageNotice();
            PrunePutDownLandings();
            if (_confirmItem != null && (DungeonUi.Modal != BagWindow || Time.unscaledTime > _confirmUntil)) ClearConfirm();
            if (!_player || _player.IsDown || DungeonUi.ModalOpen || TimeScaleService.Paused || Wallet.InTown) return;
            if (Time.unscaledTime < _nextSalvageScan) return;
            _nextSalvageScan = Time.unscaledTime + AutoSalvageScanSeconds;
            ScanFloorForAutoSalvage();
        }

        /// <summary>내려앉은 바닥 일반 장비 가운데 저절로 줍기 거리(15) 밖·내려놓은 것 아님·Decide(Auto) == Salvage인 것을 분해한다.</summary>
        void ScanFloorForAutoSalvage()
        {
            if (!Tuning.AutoSalvageCommon) return;
            float far = BagRules.AutoPickupDistance;
            Vector2 at = _player.Position;
            _salvageScan.Clear();
            foreach (var it in Interactable.All)
            {
                var drop = it as LootDrop;
                if (!drop || !drop.Available || drop.Gear.Grade != Grade.Common || _dropped.Contains(drop.Gear)) continue;
                if ((drop.Position - at).sqrMagnitude <= far * far) continue;
                _salvageScan.Add(drop);
            }
            foreach (var drop in _salvageScan)
            {
                var item = drop.Gear;
                var action = BagRules.Decide(PickupReason.Auto, item.Grade, item.Enhance, ImprovesForAutoSalvage(item),
                    _bag.Count, Capacity, Tuning.AutoSalvageCommon, Tuning.AutoPickupReserve);
                if (action == PickupAction.Salvage) AutoSalvage(drop);
            }
            _salvageScan.Clear();
        }

        void QueueAutoSalvageNotice(GearItem item, int stones)
        {
            if (_autoSalvageCount == 0)
            {
                _autoSalvageName = item.DisplayName;
                _autoSalvageNoticeAt = Time.unscaledTime + AutoSalvageNoticeSeconds;
            }
            _autoSalvageCount++;
            _autoSalvageStones += stones;
        }

        /// <summary>첫 저절로 분해 뒤 2초가 지나면 한 줄: 하나면 "{이름} — 저절로 분해 · 강화석 +1", 여럿이면 "저절로 분해 n개 · 강화석 +n".</summary>
        void FlushAutoSalvageNotice()
        {
            if (_autoSalvageCount <= 0 || Time.unscaledTime < _autoSalvageNoticeAt) return;
            DungeonEvents.Say(_autoSalvageCount == 1
                ? ForgeText.AutoSalvageNotice(_autoSalvageName, _autoSalvageStones)
                : ForgeText.AutoSalvageBatchNotice(_autoSalvageCount, _autoSalvageStones));
            _autoSalvageCount = 0;
            _autoSalvageStones = 0;
            _autoSalvageName = null;
        }

        // ── 넘침(5-1) ──

        /// <summary>
        /// Equip이 가방 밖에서 온 장비를 낀 뒤 벗은 장비를 둔다. 바닥에서 G로 바꿔 끼었고 Decide(Swap) == DropAtFeet(넘침 끝)면 발밑에 내려놓고 알린다.
        /// 그 밖에는 가방 끝(넘침 칸 포함).
        /// </summary>
        void StoreSwapped(GearItem removed)
        {
            if (_swapFromFloor && _player && !Wallet.InTown &&
                BagRules.Decide(PickupReason.Swap, removed.Grade, removed.Enhance, false, _bag.Count, Capacity, Tuning.AutoSalvageCommon, 0) == PickupAction.DropAtFeet)
            {
                LeaveAtFeet(removed);
                DungeonEvents.Say(ForgeText.OverflowSwapNotice(removed.DisplayName));
                return;
            }
            _bag.Add(removed);
        }

        /// <summary>넘침 끝이면 반지·목걸이를 벗지 않고 알린다(5-1). 빈 자리면 막지 않는다.</summary>
        bool UnequipBlockedByOverflow(GearSlot slot)
        {
            if (Equipment[slot] == null || !OverflowFull) return false;
            DungeonEvents.Say(ForgeText.OverflowUnequipNotice);
            return true;
        }

        /// <summary>카드 빨간 줄(3장): "강화 +3 사라짐 — 옮겨지지 않는다".</summary>
        static string EnhanceLostText(GearItem replaced) => ForgeText.EnhanceLostLine(replaced.Enhance);

        // ── 내려놓기(4-4) ──

        /// <summary>
        /// 플레이어 발밑에 바닥 장비로 놓고 내려놓은 것으로 기억한다(LootDrop.Spawn을 부르기만). 내려앉을 때 LootDrop.Land의 소리·전설 연출은
        /// LootDrop.cs 몫이라 그대로 나고, 탐험 기록의 '새 장비'·빈 구간 끊기만 TakePutDownLanding으로 뺀다.
        /// </summary>
        void LeaveAtFeet(GearItem item)
        {
            Vector2 at = _player.Position;
            var drop = LootDrop.Spawn(item, at, at, 0f);
            _dropped.Add(item);
            if (drop) _putDownLandings.Add(drop);
        }

        /// <summary>
        /// DungeonEvents.GearDropped(이름 글)가 방금 발밑에 둔 장비가 내려앉은 것이면 그 장비를 목록에서 지우고 true(ExplorationLog가 새 장비로 세지 않음).
        /// LootDrop.Land는 Landed를 켠 뒤 곧바로 알리므로, 막 내려앉았고 이름이 같은 것을 하나 거둔다. 같은 순간 같은 이름의 새 드랍이 함께 내려앉아도 하나만 거두므로 수는 맞다.
        /// </summary>
        public bool TakePutDownLanding(string name)
        {
            for (int i = _putDownLandings.Count - 1; i >= 0; i--)
            {
                var drop = _putDownLandings[i];
                if (!drop)
                {
                    _putDownLandings.RemoveAt(i);
                    continue;
                }
                if (!drop.Landed || drop.Gear == null || drop.Gear.DisplayName != name) continue;
                _putDownLandings.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary>내려앉은(알림을 이미 낸) 것·사라진 것을 지운다. 알림은 Land 안에서 곧바로 나므로, 다음 Update에 남은 내려앉은 것은 들은 곳이 없던 것이다.</summary>
        void PrunePutDownLandings()
        {
            for (int i = _putDownLandings.Count - 1; i >= 0; i--)
                if (!_putDownLandings[i] || _putDownLandings[i].Landed) _putDownLandings.RemoveAt(i);
        }

        /// <summary>F·G로 직접 주운 장비는 내려놓은 기억에서 지운다.</summary>
        void ForgetDropped(GearItem item)
        {
            if (item != null) _dropped.Remove(item);
        }

        /// <summary>떠날 때 거둘 바닥 장비(FloorDrops에서 내려놓은 장비를 뺌).</summary>
        List<LootDrop> SweepDrops()
        {
            var drops = FloorDrops();
            if (_dropped.Count > 0) drops.RemoveAll(d => _dropped.Contains(d.Gear));
            return drops;
        }

        /// <summary>
        /// 가방 장비를 내려놓는다. 던전: 발밑에 바닥 장비로 두고 저절로 다시 줍지 않는다('일반 장검을 발밑에 내려놓았다').
        /// 마을: 없앤다('일반 장검을 버렸다', 끼운 룬도 함께). 가방에 없으면 false. 한 번 더 묻기는 단추 쪽.
        /// </summary>
        public bool PutDown(GearItem item)
        {
            int index = item != null ? _bag.IndexOf(item) : -1;
            if (index < 0) return false;
            bool town = Wallet.InTown;
            if (!town && !_player) return false;
            _bag.RemoveAt(index);
            AfterLeftBag(item);
            if (town)
            {
                DungeonEvents.Say(ForgeText.DiscardNotice(item.DisplayName));
                SyncCarryIfTown();
                return true;
            }
            LeaveAtFeet(item);
            DungeonEvents.Say(ForgeText.PutDownNotice(item.DisplayName));
            return true;
        }

        // ── 분해(4-1) ──

        /// <summary>
        /// 가방 장비 하나를 분해한다: SalvageRules.Stones만큼 강화석(Wallet.AddSalvageStones), 끼운 룬은 룬 주머니로(가득이면 버리고 한 줄 알림),
        /// 소리 Break, 알림 '희귀 대검 분해 — 강화석 +6', 마을이면 꾸러미에 담음. 받은 강화석(가방 장비가 아니면 0). 한 번 더 묻기는 단추 쪽.
        /// </summary>
        public int Salvage(GearItem item)
        {
            int index = item != null ? _bag.IndexOf(item) : -1;
            if (index < 0) return 0;
            _bag.RemoveAt(index);
            int stones = SalvageRules.Stones(item);
            int lostRunes = ReturnRunes(item);
            AfterLeftBag(item);
            Wallet.AddSalvageStones(stones);
            Sfx.Play(SfxKind.Break);
            DungeonEvents.Say(ForgeText.SalvagedNotice(NameWithStep(item), stones));
            if (lostRunes > 0) DungeonEvents.Say(ForgeText.RunePouchFullNotice(lostRunes));
            SyncCarryIfTown();
            return stones;
        }

        /// <summary>
        /// 가방의 일반 +0 장비(SalvageRules.InBulkCommon)를 모두 분해한다(모루 창 '일반 모두 분해', 4-5). 받은 강화석 합, count = 분해한 수.
        /// 알림은 한 줄('일반 8개 분해 — 강화석 +8'), 소리 한 번. 한 번 더 묻기는 단추 쪽.
        /// </summary>
        public int SalvageAllCommons(out int count)
        {
            count = 0;
            int stones = 0, lostRunes = 0;
            for (int i = _bag.Count - 1; i >= 0; i--)
            {
                var item = _bag[i];
                if (!SalvageRules.InBulkCommon(item)) continue;
                _bag.RemoveAt(i);
                stones += SalvageRules.Stones(item);
                lostRunes += ReturnRunes(item);
                AfterLeftBag(item);
                count++;
            }
            if (count == 0) return 0;
            Wallet.AddSalvageStones(stones);
            Sfx.Play(SfxKind.Break);
            DungeonEvents.Say(ForgeText.BulkSalvagedNotice(count, stones));
            if (lostRunes > 0) DungeonEvents.Say(ForgeText.RunePouchFullNotice(lostRunes));
            SyncCarryIfTown();
            return stones;
        }

        /// <summary>분해한 장비의 룬을 주머니로 돌려보낸다(장비 문서 8-4). 주머니가 가득(종류마다 RuneRules.PouchCap)이라 못 넣은 수.</summary>
        int ReturnRunes(GearItem item)
        {
            int lost = 0;
            foreach (var id in item.Runes)
                if (RuneRules.PouchAdd(_runePouch, id, 1) <= 0) lost++;
            return lost;
        }

        /// <summary>가방에서 빠진 장비의 고름·한 번 더 묻기·카드를 푼다(다음 그림에서 낀 무기를 고른다).</summary>
        void AfterLeftBag(GearItem item)
        {
            if (ReferenceEquals(_selected, item)) _selected = null;
            if (ReferenceEquals(_confirmItem, item)) ClearConfirm();
            _card = null;
        }

        /// <summary>알림 이름: 강화했으면 단계를 붙인다("희귀 대검 +2").</summary>
        static string NameWithStep(GearItem item) => item.Enhance > 0 ? item.DisplayName + " +" + item.Enhance : item.DisplayName;

        // ── 가방 상세 단추(4-2·4-3) ──

        /// <summary>
        /// 가방 장비 상세 단추 줄(DrawDetailButtons가 부름, 무기 행동 중이면 거기서 회색 한 줄): 받은 줄을 나눠
        /// 끼기(반지는 '반지 1에 끼기'·'반지 2에 끼기') · '분해 (+n석)' · '내려놓기'(마을 '버리기').
        /// 한 번 더 묻기(SalvageRules.NeedsConfirm, 마을 버리기는 늘): 첫 누름에 단추가 붉고 글이 '한 번 더 누르면 …'으로 바뀌고,
        /// 같은 장비·같은 단추를 4초 안에 다시 누르면 한다. 고름이 바뀌거나 창을 닫으면 풀린다.
        /// </summary>
        void DrawBagItemButtons(Rect btn, GearItem item)
        {
            if (_confirmItem != null && (!ReferenceEquals(_confirmItem, item) || Time.unscaledTime > _confirmUntil)) ClearConfirm();
            const float gap = 10f;
            bool ring = item.Part == GearPart.Ring;
            int columns = ring ? 4 : 3;
            float w = (btn.width - gap * (columns - 1)) / columns;
            int at = 0;
            Rect Next() => new Rect(btn.x + at++ * (w + gap), btn.y, w, btn.height);
            if (ring)
            {
                if (BagButton(Next(), "반지 1에 끼기", false)) { ClearConfirm(); Equip(item, GearSlot.Ring1); return; }
                if (BagButton(Next(), "반지 2에 끼기", false)) { ClearConfirm(); Equip(item, GearSlot.Ring2); return; }
            }
            else if (BagButton(Next(), "끼기", false)) { ClearConfirm(); Equip(item); return; }
            if (!_bag.Contains(item)) return;

            bool town = Wallet.InTown;
            int stones = SalvageRules.Stones(item);
            bool salvageAsk = Confirming(item, BagPress.Salvage);
            if (BagButton(Next(), salvageAsk ? ForgeText.ConfirmSalvage(stones) : ForgeText.SalvageButton(stones), salvageAsk))
            {
                if (PressTwice(item, BagPress.Salvage, SalvageRules.NeedsConfirm(item))) Salvage(item);
                return;
            }
            bool putAsk = Confirming(item, BagPress.PutDown);
            string put = putAsk
                ? (town ? ForgeText.ConfirmDiscard(stones) : ForgeText.ConfirmPutDown)
                : (town ? ForgeText.DiscardButton : ForgeText.PutDownButton);
            if (BagButton(Next(), put, putAsk) && PressTwice(item, BagPress.PutDown, town || SalvageRules.NeedsConfirm(item)))
                PutDown(item);
        }

        /// <summary>
        /// 낀 장비 상세 단추 줄(4-2, DrawDetailButtons의 낀 장비 갈래가 부름): 받은 줄 오른쪽 두 칸에 회색 '분해 (+n석)'·'내려놓기'(마을 '버리기')를 두고,
        /// 마우스를 올리면 그 위에 말풍선 '낀 장비는 벗은 뒤에'(ForgeText.EquippedLocked)를 띄운다(가방 창에는 말풍선을 그리는 곳이 없어 여기서 그린다).
        /// 남은 왼쪽 칸(벗기·옮기기·'장착 중' 자리)과 그 반쪽 둘을 돌려준다. 칸 나누기는 가방 장비 줄(DrawBagItemButtons)과 같다(반지 4칸, 그 밖 3칸).
        /// 배치 다듬기는 Unity 개발 단계.
        /// </summary>
        Rect DrawEquippedLockedButtons(Rect btn, GearItem item, out Rect left, out Rect right)
        {
            const float gap = 10f;
            int columns = item.Part == GearPart.Ring ? 4 : 3;
            float w = (btn.width - gap * (columns - 1)) / columns;
            var put = new Rect(btn.xMax - w, btn.y, w, btn.height);
            var salvage = new Rect(put.x - gap - w, btn.y, w, btn.height);
            bool was = GUI.enabled;
            GUI.enabled = false;
            BagButton(salvage, ForgeText.SalvageButton(SalvageRules.Stones(item)), false);
            BagButton(put, Wallet.InTown ? ForgeText.DiscardButton : ForgeText.PutDownButton, false);
            GUI.enabled = was;
            var e = Event.current;
            if (e != null && e.type == EventType.Repaint && (salvage.Contains(e.mousePosition) || put.Contains(e.mousePosition)))
            {
                var tip = new Rect(salvage.x, salvage.y - 36f, put.xMax - salvage.x, 32f);
                DungeonUi.Box(tip, 0.94f);
                DungeonUi.ShadowLabel(tip, ForgeText.EquippedLocked, DungeonUi.SmallCenter, DungeonUi.Bone);
            }
            var rest = new Rect(btn.x, btn.y, salvage.x - gap - btn.x, btn.height);
            float half = (rest.width - gap) * 0.5f;
            left = new Rect(rest.x, rest.y, half, rest.height);
            right = new Rect(rest.x + half + gap, rest.y, half, rest.height);
            return rest;
        }

        /// <summary>누름: 묻지 않아도 되거나 4초 안 두 번째 누름이면 true(한다). 첫 누름이면 묻기 상태로 두고 false.</summary>
        bool PressTwice(GearItem item, BagPress press, bool ask)
        {
            if (!ask || Confirming(item, press))
            {
                ClearConfirm();
                return true;
            }
            _confirmItem = item;
            _confirmPress = press;
            _confirmUntil = Time.unscaledTime + ConfirmSeconds;
            return false;
        }

        bool Confirming(GearItem item, BagPress press) =>
            _confirmPress == press && _confirmItem != null && ReferenceEquals(_confirmItem, item) && Time.unscaledTime <= _confirmUntil;

        void ClearConfirm()
        {
            _confirmItem = null;
            _confirmPress = BagPress.None;
            _confirmUntil = 0f;
        }

        /// <summary>
        /// 상세 단추 하나: 지금 단추 모양(승인된 가방 창의 ButtonScope)을 그대로 쓰되 글이 칸을 넘으면 글자를 줄인다(최소 12).
        /// danger면 바탕을 붉게(한 번 더 묻기).
        /// </summary>
        bool BagButton(Rect r, string text, bool danger)
        {
            var skin = GUI.skin.button;
            if (_bagButtonStyle == null || !ReferenceEquals(_bagButtonBase, skin))
            {
                _bagButtonStyle = new GUIStyle(skin);
                _bagButtonBase = skin;
            }
            _bagButtonContent.text = text;
            int size = skin.fontSize > 0 ? skin.fontSize : 18;
            _bagButtonStyle.fontSize = size;
            while (_bagButtonStyle.fontSize > 12 &&
                   (_bagButtonStyle.CalcHeight(_bagButtonContent, r.width) > r.height ||
                    (!_bagButtonStyle.wordWrap && _bagButtonStyle.CalcSize(_bagButtonContent).x > r.width)))
                _bagButtonStyle.fontSize--;
            var prev = GUI.backgroundColor;
            if (danger) GUI.backgroundColor = ConfirmButtonTint;
            bool pressed = GUI.Button(r, _bagButtonContent, _bagButtonStyle);
            GUI.backgroundColor = prev;
            return pressed;
        }
    }
}
