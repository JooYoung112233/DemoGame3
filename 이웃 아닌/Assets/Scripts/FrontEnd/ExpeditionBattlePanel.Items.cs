using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Demo5.FrontEnd
{
    // Battle items (UI11): the acting ally opens only its own bag in a right-hand drawer, picks an item and an ally,
    // sees the before → after preview and spends its action. The board slides left so the drawer never hides it.
    public sealed partial class ExpeditionBattlePanel
    {
        [Header("전투 아이템 · UI11")]
        public BattleItemDrawer Drawer;
        [Tooltip("서랍을 여는 동안 숨길 기존 전투 화면 요소(하단 세 영역, 상단 차례 줄 등)")]
        public GameObject[] HideDuringItems;
        [Tooltip("서랍을 여는 동안 보일 요소(행동 순서, 대상 미리보기, 서랍 배경)")]
        public GameObject[] ShowDuringItems;
        public RectTransform OrderContent;
        public BattleTurnCard OrderCardPrefab;
        public BattleTargetChip PreviewBefore, PreviewAfter;
        [Tooltip("자기 자신에게만 쓰는 물건")]
        public string[] SelfOnlyItems = { "ration" };
        [Min(0)] public float ItemPan = 3f, PanDuration = .35f;
        public string ItemsDescription = "소지품 사용", ItemTitle = "{0} · 전투 아이템", ItemHint = "물건과 대상을 고른 뒤 사용하기 · 쓰면 이 대원의 행동이 끝납니다";
        public bool ItemMode => itemMode;
        public string SelectedItem => itemId;
        public int ItemTarget => itemTarget;
        bool itemMode;
        string itemId;
        int itemTarget = -1;
        float pan, panTarget;
        readonly List<BattleItemSlot> slots = new List<BattleItemSlot>();
        readonly List<string> slotIds = new List<string>();
        readonly List<BattleTargetChip> chips = new List<BattleTargetChip>();
        readonly List<int> chipUnits = new List<int>();
        readonly List<BattleTurnCard> orderCards = new List<BattleTurnCard>();

        bool ItemInput => IsOpen && State != null && itemMode && State.PlayerTurn && !Busy && !Result.activeSelf && !RetreatReview.activeSelf;
        SettlementInventoryPanel.Item ItemData(string id) => id == null ? null : arrival.Inventory.Items.FirstOrDefault(i => i.Id == id);
        static bool BattleUsable(SettlementInventoryPanel.Item item) => item != null && item.FieldUsable && item.Recovery > 0 && item.UseCost > 0;
        bool SelfOnly(string id) => SelfOnlyItems != null && SelfOnlyItems.Contains(id);

        public void OpenItems()
        {
            if (!CanInput || !Drawer) return;
            StopAim(); HideAim(); itemMode = true; hover = -1; panTarget = 1;
            var person = State.Current.Person;
            var carried = arrival.Inventory.Items.Where(i => arrival.Inventory.CountFor(person, i.Id) > 0).ToList();
            itemId = carried.Where(i => BattleUsable(i) && arrival.Inventory.CountFor(person, i.Id) >= i.UseCost && Enumerable.Range(0, State.Units.Count).Any(u => CanTreatWith(i.Id, u)))
                .OrderByDescending(i => State.ItemRecovery(i.Recovery,i.Id)).Select(i => i.Id).FirstOrDefault() ?? carried.OrderByDescending(BattleUsable).Select(i => i.Id).FirstOrDefault();
            itemTarget = DefaultTreatTarget();
            SetItemLayout(true); Refresh();
        }
        public void CloseItems()
        {
            if (!itemMode) return;
            itemMode = false; panTarget = 0; SetItemLayout(false); Refresh();
        }
        void SetItemLayout(bool open)
        {
            if (HideDuringItems != null) foreach (var g in HideDuringItems) if (g) g.SetActive(!open);
            if (ShowDuringItems != null) foreach (var g in ShowDuringItems) if (g) g.SetActive(open);
            if (Drawer) Drawer.gameObject.SetActive(open);
        }
        bool CanTreatWith(string id, int unit) => State.CanTreat(unit) && (!SelfOnly(id) || unit == State.Actor);
        // Most hurt living ally first; the actor itself for self-only items.
        int DefaultTreatTarget()
        {
            if (SelfOnly(itemId)) return State.Actor;
            int best = State.Actor; float worst = 2;
            for (int i = 0; i < State.Units.Count; i++)
            {
                var u = State.Units[i]; if (u.Enemy || !u.Alive) continue;
                float ratio = (float)u.Health / Mathf.Max(1, u.Maximum); if (ratio < worst) { worst = ratio; best = i; }
            }
            return best;
        }
        void PickItem(int slot)
        {
            if (!ItemInput || slot < 0 || slot >= slotIds.Count || slotIds[slot] == null) return;
            itemId = slotIds[slot]; if (!CanTreatWith(itemId, itemTarget)) itemTarget = DefaultTreatTarget();
            if (Presentation) Presentation.Select(State.Actor); RefreshItems();
        }
        void PickTarget(int chip)
        {
            if (!ItemInput || chip < 0 || chip >= chipUnits.Count) return;
            itemTarget = chipUnits[chip]; RefreshItems();
        }
        void RefreshItems()
        {
            if (!Drawer || !itemMode) return;
            var actor = State.Current; var person = actor.Person; if (person == null) return;
            Drawer.Title.text = string.Format(ItemTitle, actor.Name);
            var carried = arrival.Inventory.Items.Where(i => arrival.Inventory.CountFor(person, i.Id) > 0).ToList();
            while (slots.Count < Drawer.SlotCount)
            {
                var s = Instantiate(Drawer.SlotPrefab, Drawer.Slots); s.gameObject.SetActive(true); int k = slots.Count;
                s.Button.onClick.AddListener(() => PickItem(k)); slots.Add(s); slotIds.Add(null);
            }
            for (int k = 0; k < slots.Count; k++)
            {
                var item = k < carried.Count ? carried[k] : null; slotIds[k] = item?.Id;
                slots[k].Bind(item?.Icon, item != null ? arrival.Inventory.CountFor(person, item.Id) : 0, item != null && item.Id == itemId, item == null && k >= person.BagCapacity, BattleUsable(item));
            }
            var data = ItemData(itemId); bool usable = BattleUsable(data);
            int recovery = data == null ? 0 : State.ItemRecovery(data.Recovery,data.Id);
            Drawer.DetailIcon.sprite = data?.Icon; Drawer.DetailIcon.enabled = data != null && data.Icon;
            Drawer.DetailName.text = data?.Name ?? "가방이 비어 있습니다";
            Drawer.DetailDescription.text = data == null ? "전투 중에는 이 대원의 가방만 쓸 수 있습니다."
                : !usable ? "전투 중에는 쓸 수 없는 물건입니다."
                : (SelfOnly(data.Id) ? "스스로 " : "") + data.UseVerb + " · 체력 +" + recovery + (string.IsNullOrEmpty(data.Description) ? "" : "\n" + data.Description.Split('\n')[0]);

            var allies = Enumerable.Range(0, State.Units.Count).Where(i => !State.Units[i].Enemy && State.Units[i].Alive && !down[i]).ToList();
            if (Drawer.ChipGrid && allies.Count > 2)
            {
                // Two big cards, three side by side, then compact rows: always inside the fixed target area.
                int n = allies.Count; bool three = n == 3 || n >= 5; float width = Drawer.Chips.rect.width, height = Drawer.Chips.rect.height, gap = Drawer.ChipGrid.spacing.x;
                Drawer.ChipGrid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount; Drawer.ChipGrid.constraintCount = three ? 3 : 2;
                Drawer.ChipGrid.cellSize = new Vector2((width - gap * (three ? 2 : 1)) / (three ? 3 : 2), n >= 4 ? (height - Drawer.ChipGrid.spacing.y) / 2 : height);
            }
            while (chips.Count < allies.Count)
            {
                var c = Instantiate(Drawer.ChipPrefab, Drawer.Chips); int k = chips.Count;
                c.Button.onClick.AddListener(() => PickTarget(k)); chips.Add(c); chipUnits.Add(-1);
            }
            if (!CanTreatWith(itemId, itemTarget)) { var open = allies.Where(u => CanTreatWith(itemId, u)).ToList(); if (open.Count > 0) itemTarget = open[0]; }
            for (int k = 0; k < chips.Count; k++)
            {
                bool active = k < allies.Count; if (chips[k].gameObject.activeSelf != active) chips[k].gameObject.SetActive(active);
                if (!active) { chipUnits[k] = -1; continue; }
                int unit = allies[k]; chipUnits[k] = unit; var u = State.Units[unit];
                chips[k].Bind(Portrait(u), u.Name, shown[unit], u.Maximum, unit == itemTarget, usable && ItemInput && CanTreatWith(itemId, unit));
            }

            bool targetOk = itemTarget >= 0 && itemTarget < State.Units.Count && !State.Units[itemTarget].Enemy;
            bool can = usable && targetOk && CanTreatWith(itemId, itemTarget) && arrival.Inventory.CountFor(person, itemId) >= data.UseCost;
            if (targetOk && PreviewBefore && PreviewAfter)
            {
                var t = State.Units[itemTarget]; int before = shown[itemTarget], after = can ? Mathf.Min(t.Maximum, before + recovery) : before;
                PreviewBefore.Bind(Portrait(t), t.Name, before, t.Maximum, false, true); PreviewAfter.Bind(Portrait(t), t.Name, after, t.Maximum, false, true);
            }
            Drawer.Use.interactable = ItemInput && can;
            Drawer.Message.text = data == null ? "쓸 수 있는 물건이 없습니다."
                : !usable ? "다른 물건을 고르세요."
                : !targetOk ? "대상을 고르세요."
                : SelfOnly(data.Id) && itemTarget != State.Actor ? "자기 자신에게만 쓸 수 있습니다."
                : !State.CanTreat(itemTarget) ? State.Units[itemTarget].Name + " · 체력이 가득 찼습니다."
                : State.Units[itemTarget].Name + " 체력 " + shown[itemTarget] + " → " + Mathf.Min(State.Units[itemTarget].Maximum, shown[itemTarget] + recovery) + " · " + data.Name + " -" + data.UseCost;

            if (OrderContent && OrderCardPrefab)
            {
                var order = Enumerable.Range(0, State.Units.Count).Where(i => State.Units[i].Alive && !down[i]).ToList();
                while (orderCards.Count < order.Count) orderCards.Add(Instantiate(OrderCardPrefab, OrderContent));
                for (int k = 0; k < orderCards.Count; k++)
                {
                    bool active = k < order.Count; if (orderCards[k].gameObject.activeSelf != active) orderCards[k].gameObject.SetActive(active);
                    if (!active) continue;
                    var u = State.Units[order[k]]; orderCards[k].Portrait.sprite = Portrait(u); orderCards[k].Label.text = u.Enemy ? u.Name.Replace("감염자", "적") : u.Name;
                    orderCards[k].SetRole(u.Enemy,u.Creature);
                    orderCards[k].Paper.color = order[k] == State.Actor ? new Color(1, .78f, .38f) : u.Enemy ? new Color(.83f, .43f, .34f) : new Color(.78f, .78f, .74f);
                }
            }
        }
        // Wait for the board to slide home so treatment effects land on the ally, not where it was mid-slide.
        System.Collections.IEnumerator RunAfterPan(int actor)
        {
            Busy = true; while (pan > .001f) yield return null; yield return Run(actor);
        }
        void UseSelectedItem()
        {
            if (!ItemInput) return;
            var data = ItemData(itemId); if (!BattleUsable(data) || !CanTreatWith(itemId, itemTarget)) { RefreshItems(); return; }
            var person = State.Current.Person; int actor = State.Actor; string id = itemId; int cost = data.UseCost;
            if (!State.UseItem(itemTarget, data.Recovery, () => arrival.Inventory.TransferField(person, id, cost, false),id)) { RefreshItems(); return; }
            itemMode = false; panTarget = 0; SetItemLayout(false);
            StartCoroutine(RunAfterPan(actor));
        }
    }
}
