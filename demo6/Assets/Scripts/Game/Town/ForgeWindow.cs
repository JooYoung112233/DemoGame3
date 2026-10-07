using System;
using System.Collections.Generic;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Random;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Demo6.Game
{
    /// <summary>
    /// 마을 모루 창(기획/재화-쓸-곳-1차.md 2-1·2-4·2-6·2-7, 4-5, 6장, 작성자 C). IMGUI 기능만 — 배치·반응형 다듬기는 Unity 개발 단계로 둔다.
    /// 모루 F(TownFacility)와 F1 '모루 창 열기'가 Open()으로 연다. 창 이름 "forge"(DungeonUi.TryOpen: 열린 동안 시간 멈춤·입력 막기), GUI.depth −20,
    /// 권양기 창과 같은 틀(DungeonUi.Box·Title·CloseButton·KeepOnScreen). 칸 셋: 강화 · 분해 · 대장간 물건. 처음 열면 강화 칸.
    /// 키(TownKeyLatch, 열 때 Snap — 누른 채 들어온 키는 받지 않음): Tab 칸 바꾸기, Enter = 강화 칸 두드리기 · 분해 칸 고른 장비 분해 · 물건 칸 고른 물건 사기, Esc 닫기.
    ///   분해·물건 칸은 그 칸에서 줄을 직접 누른 것만 Enter가 한다. 저절로 고르지 않는다(반박 검토: Tab 뒤 Enter 한 번에 고르지 않은 장비를 녹이거나 골드를 쓰지 않게.
    ///   되사기·되팔기가 없어 되돌릴 수 없다 — 4-3 한 번 더 묻기와 같은 까닭). 분해한 뒤·산 뒤에도 다음 줄로 저절로 옮겨 가지 않는다.
    /// 강화 칸(2-1·2-6): 왼쪽 목록 = 낀 8자리 차례(빈 자리 뺌) → 가방 차례, 오른쪽 = 고른 장비 견적(EnhanceRules.Quote).
    ///   처음 고름은 가방 창에서 마지막으로 고른 장비(Inventory.Selected), 없으면 낀 무기.
    ///   두드리기(2-4) = EnhanceRules.Try → Wallet.TrySpendStones → Inventory.ReplaceItem → SyncCarryIfTown. 성공은 ChargeRing(단계마다 조금 높게)과 모루 위 '+n'(2-7),
    ///   실패는 Thud와 녹 빛 결과 줄. 결과 줄은 창 안에 2초(실제 시간).
    /// 분해 칸(4-5): 가방 장비 줄마다 '분해'(희귀 이상·강화 +1 이상은 4초 안에 두 번 — 가방 창과 같은 방식), 맨 아래 '일반 모두 분해'(늘 두 번).
    /// 대장간 물건 칸(6장): ForgeShop 물건 셋. 사면 꾸러미 골드를 빼고, 가방 물건이면 마을 가방 칸을 바로 늘린다(6-3).
    /// 단추는 OnGUI에서 받아 다음 Update에서 처리한다(TownDebugPanel 방식). Press*·Select·PickSalvage·PickGood·SetTab은 부르면 바로 처리한다(시험·F1용).
    /// 화면 글은 모두 ForgeText에서 가져온다. 창 글에 주민 이름을 쓰지 않는다(2-6 아래).
    /// </summary>
    public sealed class ForgeWindow : MonoBehaviour
    {
        public const string Modal = "forge";
        /// <summary>강화 굴림 난수 흐름(다른 흐름 1·2·3·7·23·29·31·41·43·47·61과 겹치지 않게). 씨앗은 붙은 시각.</summary>
        public const ulong RngStream = 67;
        public const int TabEnhance = 0;
        public const int TabSalvage = 1;
        public const int TabShop = 2;
        public const int TabCount = 3;
        /// <summary>창 안 결과 줄이 보이는 시간(실제 시간 초, 2-7).</summary>
        public const float ResultSeconds = 2f;

        const int GuiDepth = -20;
        const float PanelWidth = 1000f;
        const float PanelHeight = 700f;
        const float Pad = 22f;
        const float TabHeight = 40f;
        const float RowHeight = 46f;
        const float RowGap = 4f;
        const float ButtonHeight = 46f;
        const float ListWidth = 470f;
        const float SalvageButtonWidth = 300f;
        const float GoodHeight = 78f;

        /// <summary>키 칸: 0 Tab, 1 Enter, 2 숫자판 Enter, 3 Esc.</summary>
        const int SlotTab = 0;
        const int SlotEnter = 1;
        const int SlotPadEnter = 2;
        const int SlotEsc = 3;
        const int SlotCount = 4;

        static readonly Color UpColor = new Color(0.45f, 0.9f, 0.5f);
        static readonly Color TagColor = new Color(0.62f, 0.62f, 0.62f);
        static readonly Color ArmedColor = new Color(1f, 0.42f, 0.38f);
        static readonly Color PaneFill = new Color(0.02f, 0.02f, 0.022f, 0.55f);
        static readonly Color RowFill = new Color(0.03f, 0.028f, 0.026f, 0.7f);
        static readonly Color RowFillSelected = new Color(0.16f, 0.12f, 0.08f, 0.85f);
        /// <summary>'일반 모두 분해' 한 번 더 묻기 열쇠(장비가 아닌 단추).</summary>
        static readonly object BulkKey = new object();

        public static ForgeWindow Instance { get; private set; }

        /// <summary>강화 칸 목록 한 줄(ForgeText.EnhanceRow를 나눔): '무기 · ' + 등급색 이름 + ' +3 (+3/7)' + 회색 '낀 것'·'가방'.</summary>
        sealed class Row
        {
            public GearItem Item;
            public bool Worn;
            public string Where;
            public string Head;
            public string Name;
            public string Tail;
            public string Tag;
        }

        /// <summary>견적 한 줄(글·색·굵게).</summary>
        readonly struct QuoteLine
        {
            public readonly string Text;
            public readonly Color Color;
            public readonly bool Bold;

            public QuoteLine(string text, Color color, bool bold = false)
            {
                Text = text;
                Color = color;
                Bold = bold;
            }
        }

        readonly TownKeyLatch _keys = new TownKeyLatch(ForgeKey, SlotCount);
        readonly List<Row> _rows = new List<Row>();
        readonly List<GearItem> _bagRows = new List<GearItem>();
        readonly List<QuoteLine> _quote = new List<QuoteLine>();
        readonly GUIContent _measure = new GUIContent();
        IRandom _rng;
        bool _open;
        int _openedFrame;
        int _tab;
        GearItem _selected;
        GearItem _salvagePick;
        int _goodIndex;
        Action _pending;
        bool _clickedClose;
        object _armedKey;
        float _armedUntil;
        string _resultText;
        Color _resultColor = Color.white;
        float _resultUntil;
        // 견적 줄을 마지막으로 만든 장비·강화석(바뀌면 다시 만든다).
        GearItem _quoteItem;
        int _quoteStones = -1;
        bool _quoteCanTry;
        Vector2 _listScroll;
        Vector2 _bagScroll;
        Font _stylesFont;
        GUIStyle _line;
        GUIStyle _lineBold;
        GUIStyle _rowText;
        GUIStyle _rowName;
        GUIStyle _tabOn;
        GUIStyle _armedButton;

        /// <summary>창이 열려 있다.</summary>
        public bool IsOpen => _open;
        /// <summary>지금 칸(0 강화, 1 분해, 2 대장간 물건).</summary>
        public int Tab => _tab;
        /// <summary>강화 칸에서 고른 장비.</summary>
        public GearItem Selected => _selected;
        /// <summary>분해 칸에서 사용자가 직접 고른 가방 장비(Enter·PressSalvage가 녹이는 것). 고르지 않았으면 null.</summary>
        public GearItem SalvagePick => _salvagePick;
        /// <summary>물건 칸에서 사용자가 직접 고른 물건 차례(Enter가 사는 것). 고르지 않았으면 −1.</summary>
        public int GoodPick => _goodIndex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance = null;

        /// <summary>장면 정적 비우기(TownRoot.ResetUiStatics가 부름).</summary>
        public static void ResetStatics() => Instance = null;

        /// <summary>모루 창을 연다(마을 루트 물체에 없으면 붙인다). 마을이 아니거나 떠나는 중이거나 다른 창이 열려 있으면 false.</summary>
        public static bool Open()
        {
            var root = TownRoot.Instance;
            if (!root || root.Leaving) return false;
            var window = Instance;
            if (!window) window = root.GetComponent<ForgeWindow>();
            if (!window) window = root.gameObject.AddComponent<ForgeWindow>();
            Instance = window;
            return window.OpenWindow();
        }

        void Awake()
        {
            Instance = this;
            _rng = new Pcg32Random((ulong)DateTime.UtcNow.Ticks, RngStream);
        }

        void OnDisable() => Close();

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        bool OpenWindow()
        {
            if (_open && DungeonUi.Modal == Modal) return true;
            var root = TownRoot.Instance;
            if (!root || root.Leaving) return false;
            if (!DungeonUi.TryOpen(Modal)) return false;
            _open = true;
            _openedFrame = Time.frameCount;
            _tab = TabEnhance;
            _pending = null;
            _clickedClose = false;
            _resultText = null;
            Disarm();
            _listScroll = Vector2.zero;
            _bagScroll = Vector2.zero;
            _selected = FirstSelection(Inv);
            // 분해·물건 칸은 고르지 않은 채로 연다(Enter가 고르지 않은 것을 하지 않게).
            _salvagePick = null;
            _goodIndex = -1;
            Refresh(true);
            _keys.Snap();
            return true;
        }

        /// <summary>창을 닫는다(열려 있지 않으면 아무것도 하지 않음). 한 번 더 묻기도 풀린다.</summary>
        public void Close()
        {
            if (!_open) return;
            _open = false;
            _pending = null;
            _clickedClose = false;
            Disarm();
            DungeonUi.Close(Modal);
        }

        /// <summary>강화 칸에서 장비를 고른다(낀 것·가방 것). 분해 칸 고름은 바꾸지 않는다(PickSalvage). 다른 장비를 고르면 한 번 더 묻기가 풀린다(4-3).</summary>
        public void Select(GearItem item)
        {
            var inv = Inv;
            if (item == null || !inv || !Owns(inv, item)) return;
            if (_armedKey != null && !ReferenceEquals(_armedKey, item)) Disarm();
            _selected = item;
            BuildQuote(_selected, Wallet.Stones);
        }

        /// <summary>분해 칸에서 가방 장비를 고른다(줄을 누름). Enter는 이렇게 고른 장비만 녹인다. 다른 장비를 고르면 한 번 더 묻기가 풀린다(4-3).</summary>
        public void PickSalvage(GearItem item)
        {
            if (item == null || !InBag(Inv, item)) return;
            if (_armedKey != null && !ReferenceEquals(_armedKey, item)) Disarm();
            _salvagePick = item;
        }

        /// <summary>물건 칸에서 물건을 고른다(줄을 누름). Enter는 이렇게 고른 물건만 산다. 모르는 차례면 고름을 푼다.</summary>
        public void PickGood(int index) => _goodIndex = index >= 0 && index < ForgeShop.All.Count ? index : -1;

        /// <summary>칸을 바꾼다(0 강화, 1 분해, 2 대장간 물건). 바뀌면 한 번 더 묻기가 풀린다.</summary>
        public void SetTab(int tab)
        {
            tab = Mathf.Clamp(tab, 0, TabCount - 1);
            if (tab == _tab) return;
            _tab = tab;
            Disarm();
        }

        // ── Update: 단추·키 처리 ─────────────────────────────

        void Update()
        {
            if (!_open) return;
            // 다른 곳이 창을 닫았으면(장면 정적 비우기 등) 따라 닫힌다.
            if (DungeonUi.Modal != Modal)
            {
                _open = false;
                _pending = null;
                Disarm();
                return;
            }
            var pending = _pending;
            bool close = _clickedClose;
            _pending = null;
            _clickedClose = false;
            if (_armedKey != null && Time.realtimeSinceStartup > _armedUntil) Disarm();
            bool tab = false, enter = false, esc = false;
            if (Time.frameCount > _openedFrame && Keyboard.current != null)
            {
                // 키 칸은 매 프레임 모두 읽는다(뗀 상태를 놓치지 않게).
                tab = _keys.Pressed(SlotTab);
                enter = _keys.Pressed(SlotEnter) | _keys.Pressed(SlotPadEnter);
                esc = _keys.Pressed(SlotEsc);
            }
            if (close || esc)
            {
                Close();
                return;
            }
            Refresh(false);
            if (pending != null) pending();
            else if (tab) SetTab((_tab + 1) % TabCount);
            else if (enter) PressCurrent();
            if (_open) Refresh(false);
        }

        /// <summary>Enter: 강화 칸 두드리기, 분해 칸 직접 고른 장비 분해, 물건 칸 직접 고른 물건 사기(고르지 않았으면 아무것도 하지 않음).</summary>
        void PressCurrent()
        {
            switch (_tab)
            {
                case TabEnhance:
                    PressEnhance();
                    break;
                case TabSalvage:
                    PressSalvage();
                    break;
                default:
                    var goods = ForgeShop.All;
                    if (_goodIndex >= 0 && _goodIndex < goods.Count) PressBuy(goods[_goodIndex].Id);
                    break;
            }
        }

        static KeyControl ForgeKey(Keyboard kb, int slot)
        {
            switch (slot)
            {
                case SlotTab: return kb.tabKey;
                case SlotEnter: return kb.enterKey;
                case SlotPadEnter: return kb.numpadEnterKey;
                case SlotEsc: return kb.escapeKey;
                default: return null;
            }
        }

        // ── 강화(2-4·2-7) ────────────────────────────────────

        /// <summary>
        /// 고른 장비를 한 번 두드린다(2-4): EnhanceRules.Try → Wallet.TrySpendStones(쓴 만큼) → Inventory.ReplaceItem(같은 자리, 낀 것이면 능력치 다시) → SyncCarryIfTown.
        /// 막히면(상한·강화석 모자람) 아무것도 쓰지 않고 까닭을 결과 줄에 띄운다. 성공·실패 연출은 2-7.
        /// </summary>
        public void PressEnhance()
        {
            if (!_open) return;
            var inv = Inv;
            var item = _selected;
            if (!inv || item == null || !Owns(inv, item)) return;
            int stones = Wallet.Stones;
            var outcome = EnhanceRules.Try(item, stones, _rng);
            if (outcome.Block != EnhanceBlock.None)
            {
                ShowResult(ForgeText.BlockLine(EnhanceRules.Quote(item, stones), item, stones), DungeonUi.Rust);
                return;
            }
            if (!Wallet.TrySpendStones(outcome.Spent)) return;
            var placed = inv.ReplaceItem(item, outcome.Item);
            if (placed == null)
            {
                // 위에서 가진 장비인지 보았으므로 오지 않는 길. 강화석은 이미 썼으니 로그로 남긴다.
                Debug.LogWarning($"[모루] 강화한 장비를 제자리에 넣지 못했다 — {item} (강화석 {outcome.Spent} 씀)");
                return;
            }
            inv.SyncCarryIfTown();
            _selected = placed;
            if (ReferenceEquals(_salvagePick, item)) _salvagePick = placed;
            var color = LootVisuals.GradeColor(placed.Grade);
            if (outcome.Success)
            {
                ShowResult(ForgeText.SuccessLine(placed), color);
                Sfx.PlayScaled(SfxKind.ChargeRing, 1f, 1f + 0.05f * placed.Enhance);
                WorldOverlay.Text(AnvilTop, ForgeText.OverlayLine(placed.Enhance), color);
            }
            else
            {
                ShowResult(ForgeText.FailLine(outcome), DungeonUi.Rust);
                Sfx.Play(SfxKind.Thud);
            }
            Debug.Log($"[모루] {item.DisplayName} +{item.Enhance} → {(outcome.Success ? "성공" : "실패")} +{placed.Enhance}" +
                      $"{(outcome.Guaranteed ? " (확정)" : "")} · 강화석 {outcome.Spent} 씀, 남은 {Wallet.Stones} · 실패 수 {placed.EnhanceFails}");
            Refresh(true);
        }

        /// <summary>모루 위 글 자리(모루 자리에서 조금 위).</summary>
        static Vector2 AnvilTop => new Vector2(TownLayout.Anvil.Pos.X, TownLayout.Anvil.Pos.Y + 1.2f);

        // ── 분해(4-3·4-5) ────────────────────────────────────

        /// <summary>분해 칸에서 직접 고른 가방 장비를 녹인다(고르지 않았으면 아무것도 하지 않음). 희귀 이상·강화 +1 이상은 4초 안에 두 번 눌러야 한다(4-3).</summary>
        public void PressSalvage()
        {
            if (!_open) return;
            SalvageOne(_salvagePick);
        }

        /// <summary>'일반 모두 분해'(4-5): 일반이면서 강화 +0인 가방 장비 모두. 늘 두 번 눌러야 한다. 대상이 없으면 아무것도 하지 않는다.</summary>
        public void PressSalvageAll()
        {
            if (!_open) return;
            var inv = Inv;
            if (!inv) return;
            BulkCount(inv, out int count, out _);
            if (count <= 0) return;
            if (!IsArmed(BulkKey))
            {
                Arm(BulkKey);
                return;
            }
            Disarm();
            int got = inv.SalvageAllCommons(out int done);
            if (done <= 0) return;
            ShowResult(ForgeText.BulkSalvagedNotice(done, got), DungeonUi.Bone);
            Refresh(true);
        }

        /// <summary>가방 장비 하나를 녹인다(묻기 대상이면 첫 누름은 단추를 붉게만). 알림·소리·꾸러미 담기는 Inventory.Salvage가 한다.</summary>
        void SalvageOne(GearItem item)
        {
            var inv = Inv;
            if (!inv || item == null || !InBag(inv, item)) return;
            if (!ReferenceEquals(_salvagePick, item)) PickSalvage(item);
            if (SalvageRules.NeedsConfirm(item) && !IsArmed(item))
            {
                Arm(item);
                return;
            }
            Disarm();
            int got = inv.Salvage(item);
            if (got <= 0) return;
            // 가방 분해 알림과 같은 글(이름 뒤에 강화 단계, +0이면 뺌).
            string name = item.Enhance > 0 ? item.DisplayName + " +" + item.Enhance : item.DisplayName;
            ShowResult(ForgeText.SalvagedNotice(name, got), DungeonUi.Bone);
            // 다음 장비를 저절로 고르지 않는다(Enter를 한 번 더 눌러도 고르지 않은 장비를 녹이지 않게).
            _salvagePick = null;
            Refresh(true);
        }

        static void BulkCount(Inventory inv, out int count, out int stones)
        {
            count = 0;
            stones = 0;
            foreach (var item in inv.Bag)
            {
                if (!SalvageRules.InBulkCommon(item)) continue;
                count++;
                stones += SalvageRules.Stones(item);
            }
        }

        bool IsArmed(object key) => key != null && ReferenceEquals(_armedKey, key) && Time.realtimeSinceStartup <= _armedUntil;

        void Arm(object key)
        {
            _armedKey = key;
            _armedUntil = Time.realtimeSinceStartup + SalvageRules.ConfirmSeconds;
        }

        void Disarm()
        {
            _armedKey = null;
            _armedUntil = 0f;
        }

        // ── 대장간 물건(6장) ─────────────────────────────────

        /// <summary>대장간 물건을 산다(6-2·6-3): ForgeShop.TryBuy(꾸러미 골드를 뺌) → 가방 물건이면 마을 가방 칸을 바로 늘림 → Pickup 소리·알림.</summary>
        public void PressBuy(string goodId)
        {
            if (!_open) return;
            var good = ForgeShop.Get(goodId);
            if (good == null) return;
            var carry = ProfileCarry.Ensure();
            if (!ForgeShop.TryBuy(carry, good.Id))
            {
                ShowResult(ForgeText.GoodStateLine(carry, good.Id), DungeonUi.Rust);
                return;
            }
            if (good.Id == ForgeShop.Bag25 || good.Id == ForgeShop.Bag30)
            {
                var inv = Inv;
                if (inv) inv.SetCapacity(ForgeShop.BagCapacity(carry));
            }
            Sfx.Play(SfxKind.Pickup);
            string notice = ForgeText.BoughtNotice(good.Id);
            if (notice != null)
            {
                DungeonEvents.Say(notice);
                ShowResult(notice, DungeonUi.Ember);
            }
            Debug.Log($"[모루] 대장간 물건 {good.Id} 삼 — 골드 {good.Price}, 남은 {carry.Gold} · 가방 {ForgeShop.BagCapacity(carry)}칸 · 물약 {ForgeShop.PotionCapacity(carry)}병");
            // 고름은 산 물건(이제 '갖춤')에 그대로 둔다. 다음 살 수 있는 물건으로 저절로 옮기지 않는다(Enter 한 번 더에 골드를 쓰지 않게).
        }

        // ── 목록·견적 ─────────────────────────────────────

        /// <summary>마을 가방(TownRoot.Inventory).</summary>
        static Inventory Inv
        {
            get
            {
                var root = TownRoot.Instance;
                return root && root.Inventory ? root.Inventory : Inventory.Instance;
            }
        }

        static bool InBag(Inventory inv, GearItem item)
        {
            if (!inv || item == null) return false;
            foreach (var b in inv.Bag)
                if (ReferenceEquals(b, item)) return true;
            return false;
        }

        static bool Owns(Inventory inv, GearItem item) =>
            inv && item != null && (inv.Equipment.SlotOf(item).HasValue || InBag(inv, item));

        /// <summary>처음 고름(2-1): 가방 창에서 마지막으로 고른 장비, 없으면 낀 무기.</summary>
        static GearItem FirstSelection(Inventory inv)
        {
            if (!inv) return null;
            var last = inv.Selected;
            if (last != null && Owns(inv, last)) return last;
            if (inv.Equipped != null) return inv.Equipped;
            foreach (var slot in GearSlots.All)
                if (inv.Equipment[slot] != null) return inv.Equipment[slot];
            foreach (var item in inv.Bag)
                if (item != null) return item;
            return null;
        }

        /// <summary>목록이 바뀌었으면 다시 만들고, 고름이 사라졌으면 다시 고르고, 장비·강화석이 바뀌었으면 견적을 다시 만든다.</summary>
        void Refresh(bool force)
        {
            var inv = Inv;
            if (force || RowsChanged(inv)) RebuildRows(inv);
            if (inv && (_selected == null || !Owns(inv, _selected))) _selected = FirstSelection(inv);
            // 분해 칸 고름이 가방에서 사라졌으면 푼다. 비어도 저절로 고르지 않는다(사용자가 줄을 누를 때만, PickSalvage).
            if (_salvagePick != null && !InBag(inv, _salvagePick)) _salvagePick = null;
            int stones = Wallet.Stones;
            if (force || !ReferenceEquals(_quoteItem, _selected) || stones != _quoteStones) BuildQuote(_selected, stones);
        }

        bool RowsChanged(Inventory inv)
        {
            if (!inv) return _rows.Count > 0;
            int i = 0;
            foreach (var slot in GearSlots.All)
            {
                var item = inv.Equipment[slot];
                if (item == null) continue;
                if (i >= _rows.Count || !_rows[i].Worn || !ReferenceEquals(_rows[i].Item, item)) return true;
                i++;
            }
            int b = 0;
            foreach (var item in inv.Bag)
            {
                if (item == null) continue;
                if (i >= _rows.Count || _rows[i].Worn || !ReferenceEquals(_rows[i].Item, item)) return true;
                if (b >= _bagRows.Count || !ReferenceEquals(_bagRows[b], item)) return true;
                i++;
                b++;
            }
            return i != _rows.Count || b != _bagRows.Count;
        }

        void RebuildRows(Inventory inv)
        {
            _rows.Clear();
            _bagRows.Clear();
            if (!inv) return;
            foreach (var slot in GearSlots.All)
            {
                var item = inv.Equipment[slot];
                if (item != null) _rows.Add(MakeRow(item, GearSlots.SlotName(slot), true));
            }
            foreach (var item in inv.Bag)
            {
                if (item == null) continue;
                _rows.Add(MakeRow(item, GearSlots.Name(item.Part), false));
                _bagRows.Add(item);
            }
        }

        /// <summary>ForgeText.EnhanceRow('무기 · 희귀 장검 +3 (+3/7)')를 이름 앞·이름·이름 뒤로 나눈다(이름만 등급색).</summary>
        static Row MakeRow(GearItem item, string where, bool worn)
        {
            SplitAtName(ForgeText.EnhanceRow(item, where), item.DisplayName, out string head, out string name, out string tail);
            return new Row
            {
                Item = item,
                Worn = worn,
                Where = where,
                Head = head,
                Name = name,
                Tail = tail,
                Tag = worn ? ForgeText.EquippedTag : ForgeText.BagTag,
            };
        }

        /// <summary>글을 이름 앞·이름·이름 뒤로 나눈다. 이름이 없으면 글 전부가 앞 조각.</summary>
        static void SplitAtName(string full, string name, out string head, out string found, out string tail)
        {
            full = full ?? "";
            int at = string.IsNullOrEmpty(name) ? -1 : full.IndexOf(name, StringComparison.Ordinal);
            if (at < 0)
            {
                head = full;
                found = "";
                tail = "";
                return;
            }
            head = full.Substring(0, at);
            found = name;
            tail = full.Substring(at + name.Length);
        }

        /// <summary>견적 줄(2-6): 단계, 능력치 지금 → 다음, 비용(가진 것), 확률, 천장, 실패 규칙, 막힘 이유. 상한이면 상한 글만.</summary>
        void BuildQuote(GearItem item, int stones)
        {
            _quoteItem = item;
            _quoteStones = stones;
            _quote.Clear();
            var q = EnhanceRules.Quote(item, stones);
            _quoteCanTry = q.CanTry;
            if (item == null || q.Block == EnhanceBlock.NoItem)
            {
                _quote.Add(new QuoteLine(ForgeText.NoItemLine, DungeonUi.BoneDim));
                return;
            }
            if (q.Block == EnhanceBlock.GradeCap || q.Block == EnhanceBlock.FirstPassCap)
            {
                _quote.Add(new QuoteLine(ForgeText.BlockLine(q, item, stones), DungeonUi.Ember, true));
                return;
            }
            _quote.Add(new QuoteLine(ForgeText.StepLine(q.From, q.To), DungeonUi.Bone, true));
            string stat = ForgeText.StatLine(item, item.WithEnhance(q.To));
            if (!string.IsNullOrEmpty(stat)) _quote.Add(new QuoteLine(stat, UpColor));
            bool lacking = q.Block == EnhanceBlock.NotEnoughStones;
            _quote.Add(new QuoteLine(ForgeText.CostLine(q.Cost, stones), lacking ? DungeonUi.Rust : DungeonUi.Bone));
            _quote.Add(new QuoteLine(ForgeText.ChanceLine(q.ChancePermille), DungeonUi.Ember));
            string pity = ForgeText.PityLine(q);
            if (pity != null) _quote.Add(new QuoteLine(pity, DungeonUi.BoneDim));
            if (!q.Guaranteed) _quote.Add(new QuoteLine(ForgeText.FailRuleLine, DungeonUi.BoneDim));
            if (lacking) _quote.Add(new QuoteLine(ForgeText.BlockLine(q, item, stones), DungeonUi.Rust));
        }

        void ShowResult(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            _resultText = text;
            _resultColor = color;
            _resultUntil = Time.realtimeSinceStartup + ResultSeconds;
        }

        // ── 그리기 ───────────────────────────────────────────

        void EnsureStyles()
        {
            var font = DungeonUi.Label.font;
            if (_line != null && _stylesFont == font) return;
            _stylesFont = font;
            _line = new GUIStyle(DungeonUi.Small) { wordWrap = true };
            _line.normal.textColor = Color.white;
            _lineBold = new GUIStyle(DungeonUi.Bold) { wordWrap = true };
            _lineBold.normal.textColor = Color.white;
            _rowText = new GUIStyle(DungeonUi.Small) { wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft };
            _rowText.normal.textColor = Color.white;
            _rowName = new GUIStyle(DungeonUi.Bold) { wordWrap = false, clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, fontSize = 17 };
            _rowName.normal.textColor = Color.white;
            _tabOn = new GUIStyle(GUI.skin.button);
            _tabOn.normal.background = GUI.skin.button.active.background;
            _tabOn.normal.textColor = DungeonUi.Ember;
            _tabOn.hover.textColor = DungeonUi.Ember;
            _armedButton = new GUIStyle(GUI.skin.button);
            _armedButton.normal.textColor = ArmedColor;
            _armedButton.hover.textColor = ArmedColor;
            _armedButton.active.textColor = ArmedColor;
        }

        float LineHeight(GUIStyle style, string text, float width)
        {
            _measure.text = text;
            return Mathf.Ceil(style.CalcHeight(_measure, width));
        }

        float TextWidth(GUIStyle style, string text)
        {
            _measure.text = text;
            return Mathf.Ceil(style.CalcSize(_measure).x);
        }

        void OnGUI()
        {
            if (!_open || DungeonUi.Modal != Modal) return;
            var prevMatrix = GUI.matrix;
            var prevColor = GUI.color;
            bool prevEnabled = GUI.enabled;
            DungeonUi.Begin();
            EnsureStyles();
            GUI.depth = GuiDepth;
            var r = DungeonUi.KeepOnScreen(new Rect((DungeonUi.Width - PanelWidth) * 0.5f, (DungeonUi.Height - PanelHeight) * 0.5f, PanelWidth, PanelHeight));
            DungeonUi.Box(r, 0.95f);
            float x = r.x + Pad;
            float w = r.width - Pad * 2f;
            float y = r.y + Pad;
            GUI.Label(new Rect(x, y - 4f, w - 300f, 40f), ForgeText.WindowTitle, DungeonUi.Title);
            DrawWallet(new Rect(r.xMax - 80f - 220f, y, 220f, 32f));
            if (DungeonUi.CloseButton(r)) _clickedClose = true;
            y += 50f;
            DrawTabs(new Rect(x, y, w, TabHeight));
            y += TabHeight + 14f;
            const float footHeight = 26f;
            const float resultHeight = 28f;
            var foot = new Rect(x, r.yMax - Pad - footHeight, w, footHeight);
            var result = new Rect(x, foot.y - resultHeight - 6f, w, resultHeight);
            var body = new Rect(x, y, w, result.y - 8f - y);
            switch (_tab)
            {
                case TabEnhance:
                    DrawEnhance(body);
                    break;
                case TabSalvage:
                    DrawSalvage(body);
                    break;
                default:
                    DrawShop(body);
                    break;
            }
            GUI.enabled = true;
            if (_resultText != null && Time.realtimeSinceStartup <= _resultUntil)
                DungeonUi.ShadowLabel(result, _resultText, _lineBold, _resultColor);
            DungeonUi.Strip(new Rect(x, foot.y - 4f, w, 1f), DungeonUi.IronEdge);
            DungeonUi.ShadowLabel(foot, ForgeText.FooterHintFor(_tab), _line, DungeonUi.BoneDim);
            GUI.enabled = prevEnabled;
            GUI.color = prevColor;
            GUI.matrix = prevMatrix;
        }

        /// <summary>창 머리 오른쪽 지갑: 강화석·골드 수(ItemIconArt 'stone'·'gold', 13장).</summary>
        void DrawWallet(Rect r)
        {
            float half = r.width * 0.5f;
            ItemIconArt.Draw(new Rect(r.x, r.y, r.height, r.height), "stone");
            DungeonUi.ShadowLabel(new Rect(r.x + r.height + 6f, r.y, half - r.height - 6f, r.height), Wallet.Stones.ToString(), _rowName, DungeonUi.Bone);
            ItemIconArt.Draw(new Rect(r.x + half, r.y, r.height, r.height), "gold");
            DungeonUi.ShadowLabel(new Rect(r.x + half + r.height + 6f, r.y, half - r.height - 6f, r.height), Wallet.Gold.ToString(), _rowName, DungeonUi.Bone);
        }

        void DrawTabs(Rect r)
        {
            const float gap = 8f;
            var names = ForgeText.Tabs;
            float tw = Mathf.Min(220f, (r.width - gap * (TabCount - 1)) / TabCount);
            for (int i = 0; i < TabCount; i++)
            {
                var t = new Rect(r.x + i * (tw + gap), r.y, tw, r.height);
                bool on = i == _tab;
                if (GUI.Button(t, i < names.Count ? names[i] : "", on ? _tabOn : GUI.skin.button))
                {
                    int tab = i;
                    _pending = () => SetTab(tab);
                }
                if (on) DungeonUi.Outline(new Rect(t.x - 2f, t.y - 2f, t.width + 4f, t.height + 4f), DungeonUi.Ember, 2f);
            }
            DungeonUi.Strip(new Rect(r.x, r.yMax + 6f, r.width, 1f), DungeonUi.IronEdge);
        }

        // 강화 칸(2-1·2-6): 왼쪽 장비 목록, 오른쪽 견적과 '두드리기'(막히면 회색).
        void DrawEnhance(Rect body)
        {
            var list = new Rect(body.x, body.y, ListWidth, body.height);
            DrawGearList(list);
            float px = list.xMax + 20f;
            var pane = new Rect(px, body.y, body.xMax - px, body.height);
            DungeonUi.Fill(pane, PaneFill);
            DungeonUi.Outline(pane, DungeonUi.IronEdge, 1f);
            float ix = pane.x + 14f;
            float iw = pane.width - 28f;
            float y = pane.y + 12f;
            var item = _selected;
            if (item != null)
            {
                var color = LootVisuals.GradeColor(item.Grade);
                DungeonUi.Slot(new Rect(ix, y, 72f, 72f));
                ItemIconArt.Draw(new Rect(ix + 6f, y + 6f, 60f, 60f), item.Base.IconId,item.Grade);
                DungeonUi.ShadowLabel(new Rect(ix + 84f, y + 4f, iw - 84f, 30f), item.DisplayName + " +" + item.Enhance, _lineBold, color);
                DungeonUi.ShadowLabel(new Rect(ix + 84f, y + 38f, iw - 84f, 26f), WhereLine(item), _line, TagColor);
                y += 84f;
            }
            float bottom = pane.yMax - ButtonHeight - 20f;
            foreach (var line in _quote)
            {
                var style = line.Bold ? _lineBold : _line;
                float h = LineHeight(style, line.Text, iw);
                if (y + h > bottom) break;
                DungeonUi.ShadowLabel(new Rect(ix, y, iw, h), line.Text, style, line.Color);
                y += h + 4f;
            }
            var button = new Rect(ix, pane.yMax - ButtonHeight - 12f, iw, ButtonHeight);
            GUI.enabled = _quoteCanTry;
            if (GUI.Button(button, ForgeText.EnhanceButton)) _pending = PressEnhance;
            GUI.enabled = true;
        }

        /// <summary>오른쪽 머리 둘째 줄: '낀 것 · 반지 1' / '가방'(회색).</summary>
        string WhereLine(GearItem item)
        {
            foreach (var row in _rows)
                if (ReferenceEquals(row.Item, item))
                    return row.Worn ? row.Tag + " · " + row.Where : row.Tag;
            return "";
        }

        /// <summary>장비 목록(낀 것 → 가방). 줄을 누르면 고른다.</summary>
        void DrawGearList(Rect area)
        {
            DungeonUi.Fill(area, PaneFill);
            DungeonUi.Outline(area, DungeonUi.IronEdge, 1f);
            var view = new Rect(area.x + 4f, area.y + 4f, area.width - 8f, area.height - 8f);
            float contentHeight = Mathf.Max(view.height, _rows.Count * (RowHeight + RowGap));
            bool scroll = contentHeight > view.height + 0.5f;
            float rowWidth = view.width - (scroll ? 18f : 0f);
            _listScroll = GUI.BeginScrollView(view, _listScroll, new Rect(0f, 0f, rowWidth, contentHeight));
            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var rect = new Rect(0f, i * (RowHeight + RowGap), rowWidth, RowHeight);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    var item = row.Item;
                    _pending = () => Select(item);
                }
                DrawGearRow(rect, row, ReferenceEquals(row.Item, _selected));
            }
            GUI.EndScrollView();
        }

        void DrawGearRow(Rect rect, Row row, bool selected)
        {
            DungeonUi.Fill(rect, selected ? RowFillSelected : RowFill);
            DungeonUi.Outline(rect, selected ? DungeonUi.Ember : DungeonUi.IronEdge, selected ? 2f : 1f);
            var gradeColor = LootVisuals.GradeColor(row.Item.Grade);
            DungeonUi.Fill(new Rect(rect.x, rect.y, 4f, rect.height), gradeColor);
            ItemIconArt.Draw(new Rect(rect.x + 8f, rect.y + 4f, rect.height - 8f, rect.height - 8f), row.Item.Base.IconId,row.Item.Grade);
            float tagWidth = TextWidth(_rowText, row.Tag);
            float right = rect.xMax - 10f - tagWidth;
            float tx = rect.x + rect.height + 8f;
            tx = DrawPiece(new Rect(tx, rect.y, right - tx, rect.height), row.Head, _rowText, DungeonUi.BoneDim);
            tx = DrawPiece(new Rect(tx, rect.y, right - tx, rect.height), row.Name, _rowName, gradeColor);
            DrawPiece(new Rect(tx, rect.y, right - tx, rect.height), row.Tail, _rowText, DungeonUi.Bone);
            DungeonUi.ShadowLabel(new Rect(right, rect.y, tagWidth + 4f, rect.height), row.Tag, _rowText, TagColor);
        }

        /// <summary>한 줄 안의 한 조각을 그리고 다음 조각의 x를 돌려준다(칸이 모자라면 잘림).</summary>
        float DrawPiece(Rect r, string text, GUIStyle style, Color color)
        {
            if (string.IsNullOrEmpty(text) || r.width <= 1f) return r.x;
            float width = Mathf.Min(r.width, TextWidth(style, text));
            DungeonUi.ShadowLabel(new Rect(r.x, r.y, width + 2f, r.height), text, style, color);
            return r.x + width;
        }

        // 분해 칸(4-5): 맨 위 안내, 가방 장비 줄마다 '분해', 맨 아래 '일반 모두 분해'(대상이 없으면 회색).
        void DrawSalvage(Rect body)
        {
            var inv = Inv;
            float y = body.y;
            float introHeight = LineHeight(_line, ForgeText.SalvageTabIntro, body.width);
            DungeonUi.ShadowLabel(new Rect(body.x, y, body.width, introHeight), ForgeText.SalvageTabIntro, _line, DungeonUi.Bone);
            y += introHeight + 10f;
            var bulkRect = new Rect(body.x, body.yMax - ButtonHeight, body.width, ButtonHeight);
            var area = new Rect(body.x, y, body.width, bulkRect.y - 10f - y);
            DungeonUi.Fill(area, PaneFill);
            DungeonUi.Outline(area, DungeonUi.IronEdge, 1f);
            if (_bagRows.Count == 0)
                DungeonUi.ShadowLabel(new Rect(area.x + 14f, area.y + 12f, area.width - 28f, 28f), ForgeText.SalvageTabEmpty, _line, DungeonUi.BoneDim);
            else
            {
                var view = new Rect(area.x + 4f, area.y + 4f, area.width - 8f, area.height - 8f);
                float contentHeight = Mathf.Max(view.height, _bagRows.Count * (RowHeight + RowGap));
                bool scroll = contentHeight > view.height + 0.5f;
                float rowWidth = view.width - (scroll ? 18f : 0f);
                _bagScroll = GUI.BeginScrollView(view, _bagScroll, new Rect(0f, 0f, rowWidth, contentHeight));
                for (int i = 0; i < _bagRows.Count; i++)
                    DrawSalvageRow(new Rect(0f, i * (RowHeight + RowGap), rowWidth, RowHeight), _bagRows[i]);
                GUI.EndScrollView();
            }
            int count = 0, stones = 0;
            if (inv) BulkCount(inv, out count, out stones);
            bool armed = count > 0 && IsArmed(BulkKey);
            string bulk = count <= 0 ? ForgeText.BulkSalvageNone
                : armed ? ForgeText.ConfirmBulkSalvage(count, stones)
                : ForgeText.BulkSalvageButton(count, stones);
            GUI.enabled = count > 0;
            if (GUI.Button(bulkRect, bulk, armed ? _armedButton : GUI.skin.button)) _pending = PressSalvageAll;
            GUI.enabled = true;
            if (armed) DungeonUi.Outline(bulkRect, ArmedColor, 2f);
        }

        void DrawSalvageRow(Rect rect, GearItem item)
        {
            bool selected = ReferenceEquals(item, _salvagePick);
            var button = new Rect(rect.xMax - SalvageButtonWidth - 6f, rect.y + 5f, SalvageButtonWidth, rect.height - 10f);
            var pick = new Rect(rect.x, rect.y, button.x - rect.x - 6f, rect.height);
            if (GUI.Button(pick, GUIContent.none, GUIStyle.none)) _pending = () => PickSalvage(item);
            DungeonUi.Fill(rect, selected ? RowFillSelected : RowFill);
            DungeonUi.Outline(rect, selected ? DungeonUi.Ember : DungeonUi.IronEdge, selected ? 2f : 1f);
            var gradeColor = LootVisuals.GradeColor(item.Grade);
            DungeonUi.Fill(new Rect(rect.x, rect.y, 4f, rect.height), gradeColor);
            ItemIconArt.Draw(new Rect(rect.x + 8f, rect.y + 4f, rect.height - 8f, rect.height - 8f), item.Base.IconId,item.Grade);
            // '희귀 대검 +2 — 강화석 7'(ForgeText.SalvageRow): 이름만 등급색.
            SplitAtName(ForgeText.SalvageRow(item), item.DisplayName, out string head, out string name, out string tail);
            float tx = rect.x + rect.height + 8f;
            float right = pick.xMax;
            tx = DrawPiece(new Rect(tx, rect.y, right - tx, rect.height), head, _rowText, DungeonUi.Bone);
            tx = DrawPiece(new Rect(tx, rect.y, right - tx, rect.height), name, _rowName, gradeColor);
            DrawPiece(new Rect(tx, rect.y, right - tx, rect.height), tail, _rowText, DungeonUi.Bone);
            bool armed = IsArmed(item);
            string label = armed ? ForgeText.ConfirmSalvage(SalvageRules.Stones(item)) : ForgeText.SalvageRowButton;
            if (GUI.Button(button, label, armed ? _armedButton : GUI.skin.button)) _pending = () => SalvageOne(item);
            if (armed) DungeonUi.Outline(button, ArmedColor, 2f);
        }

        // 대장간 물건 칸(6장): 골드 줄, 물건 셋(상태마다 '사기' 단추·회색 '갖춤'·잠김 글·모자람 글), 맨 아래 '강화석은 골드로 살 수 없다'.
        void DrawShop(Rect body)
        {
            var carry = ProfileCarry.Ensure();
            float y = body.y;
            ItemIconArt.Draw(new Rect(body.x, y, 30f, 30f), "gold");
            DungeonUi.ShadowLabel(new Rect(body.x + 38f, y, body.width - 38f, 30f), ForgeText.GoldLine(carry.Gold), _lineBold, DungeonUi.Bone);
            y += 42f;
            var goods = ForgeShop.All;
            for (int i = 0; i < goods.Count; i++)
            {
                DrawGood(new Rect(body.x, y, body.width, GoodHeight), i, goods[i], carry);
                y += GoodHeight + 8f;
            }
            float noteHeight = LineHeight(_line, ForgeText.NoStonesForGold, body.width);
            DungeonUi.ShadowLabel(new Rect(body.x, body.yMax - noteHeight, body.width, noteHeight), ForgeText.NoStonesForGold, _line, DungeonUi.BoneDim);
        }

        void DrawGood(Rect rect, int index, ForgeGood good, CarryData carry)
        {
            bool selected = index == _goodIndex;
            var state = ForgeShop.State(carry, good.Id);
            const float side = 320f;
            var right = new Rect(rect.xMax - side - 10f, rect.y + 12f, side, rect.height - 24f);
            var pick = new Rect(rect.x, rect.y, right.x - rect.x - 6f, rect.height);
            if (GUI.Button(pick, GUIContent.none, GUIStyle.none))
            {
                int at = index;
                _pending = () => PickGood(at);
            }
            DungeonUi.Fill(rect, selected ? RowFillSelected : RowFill);
            DungeonUi.Outline(rect, selected ? DungeonUi.Ember : DungeonUi.IronEdge, selected ? 2f : 1f);
            float tx = rect.x + 16f;
            float tw = pick.xMax - tx;
            bool owned = state == ForgeBuyState.Owned;
            float nameWidth = Mathf.Min(tw, TextWidth(_rowName, good.Name));
            DungeonUi.ShadowLabel(new Rect(tx, rect.y + 8f, nameWidth + 2f, 30f), good.Name, _rowName, owned ? TagColor : DungeonUi.Bone);
            DungeonUi.ShadowLabel(new Rect(tx + nameWidth + 16f, rect.y + 8f, Mathf.Max(0f, tw - nameWidth - 16f), 30f), ForgeText.PriceLine(good.Price), _rowText, owned ? TagColor : DungeonUi.Ember);
            DungeonUi.ShadowLabel(new Rect(tx, rect.y + 42f, tw, 28f), good.Line, _rowText, DungeonUi.BoneDim);
            switch (state)
            {
                case ForgeBuyState.CanBuy:
                    if (GUI.Button(right, ForgeText.BuyButton))
                    {
                        string id = good.Id;
                        _pending = () => PressBuy(id);
                    }
                    break;
                case ForgeBuyState.Owned:
                    GUI.enabled = false;
                    GUI.Button(right, ForgeText.OwnedLabel);
                    GUI.enabled = true;
                    break;
                default:
                    var color = state == ForgeBuyState.NotEnoughGold ? DungeonUi.Rust : DungeonUi.BoneDim;
                    DungeonUi.ShadowLabel(right, ForgeText.GoodStateLine(carry, good.Id), _line, color);
                    break;
            }
        }
    }
}
