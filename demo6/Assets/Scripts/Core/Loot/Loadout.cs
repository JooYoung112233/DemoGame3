using System;
using System.Collections.Generic;

namespace Demo6.Core.Loot
{
    /// <summary>
    /// 장착 8자리(장비 문서 4-1, 8-6). 무기·갑옷·투구·장갑·장화는 비울 수 없고 바꾸기만 한다. 반지 1·2·목걸이는 비울 수 있다.
    /// 같은 종류 반지 둘을 껴도 되고 고유는 합산한다. 같은 전설 효과 둘은 LegendaryTable.Active가 높은 하나만 켠다.
    /// 자리 차례는 GearSlot이다. 이 클래스 밖에서 배열을 고치지 않는다(Inventory·꾸러미 풀기는 TryEquip으로 채운다).
    /// </summary>
    public sealed class Loadout
    {
        readonly GearItem[] _slots = new GearItem[GearSlots.SlotCount];

        /// <summary>그 자리 장비(빈 자리 null).</summary>
        public GearItem this[GearSlot slot] => _slots[(int)slot];

        public GearItem Weapon => _slots[(int)GearSlot.Weapon];

        /// <summary>낀 장비(빈 자리 뺌, 자리 차례).</summary>
        public IEnumerable<GearItem> Items
        {
            get
            {
                foreach (var item in _slots)
                    if (item != null) yield return item;
            }
        }

        /// <summary>시작 장비(4-4): 일반 장검 + 가죽 갑옷·두건·장갑·장화(iLv1, 굴림 1000‰). 반지 2·목걸이 비움.</summary>
        public static Loadout Starting()
        {
            var l = new Loadout();
            foreach (var slot in GearSlots.All) l._slots[(int)slot] = GearItem.Starting(slot);
            return l;
        }

        /// <summary>얕은 복사(장비는 바뀌지 않는 값이라 같은 참조를 나눠 씀). '이걸 끼면' 비교에 쓴다.</summary>
        public Loadout Clone()
        {
            var l = new Loadout();
            Array.Copy(_slots, l._slots, _slots.Length);
            return l;
        }

        /// <summary>그 자리에 낄 수 있는가(부위가 맞음).</summary>
        public static bool CanEquip(GearSlot slot, GearItem item) => item != null && GearSlots.Accepts(slot, item.Part);

        /// <summary>
        /// 그 자리에 낀다. 부위가 맞지 않으면 false(아무것도 바뀌지 않음). removed = 벗은 장비(빈 자리였으면 null).
        /// 같은 장비가 다른 자리에 이미 있으면(반지 1 → 반지 2 옮기기) 그 자리를 비우고 옮긴다.
        /// </summary>
        public bool TryEquip(GearSlot slot, GearItem item, out GearItem removed)
        {
            removed = null;
            if (!CanEquip(slot, item)) return false;
            var from = SlotOf(item);
            if (from.HasValue && from.Value == slot) return true;
            removed = _slots[(int)slot];
            if (from.HasValue)
            {
                // 반지 두 자리 사이 옮기기: 벗은 반지는 옮겨 온 자리로 간다(맞바꿈).
                _slots[(int)from.Value] = removed;
                removed = null;
            }
            _slots[(int)slot] = item;
            return true;
        }

        /// <summary>반지·목걸이 자리를 비운다(벗은 장비를 돌려줌). 다른 자리는 비우지 않고 null.</summary>
        public GearItem Unequip(GearSlot slot)
        {
            if (!GearSlots.CanUnequip(slot)) return null;
            var old = _slots[(int)slot];
            _slots[(int)slot] = null;
            return old;
        }

        /// <summary>이 장비를 낀 자리(없으면 null). 참조로 찾는다.</summary>
        public GearSlot? SlotOf(GearItem item)
        {
            if (item == null) return null;
            for (int i = 0; i < _slots.Length; i++)
                if (ReferenceEquals(_slots[i], item)) return (GearSlot)i;
            return null;
        }

        public bool IsEquipped(GearItem item) => SlotOf(item).HasValue;

        /// <summary>빈 자리(자리 차례). 시작 장비에서 채운 장착 상태면 반지 1·2·목걸이 가운데서만 나온다.</summary>
        public List<GearSlot> EmptySlots()
        {
            var list = new List<GearSlot>();
            foreach (var slot in GearSlots.All)
                if (_slots[(int)slot] == null) list.Add(slot);
            return list;
        }

        /// <summary>
        /// G(바로 끼기)가 이 장비를 낄 자리(8-6). 반지가 아니면 그 부위 자리. 반지면 빈 자리부터(반지 1 → 반지 2),
        /// 둘 다 차 있으면 value(그 자리에 낀 뒤 장착 상태의 값, 예: 종합 지수)가 더 큰 자리, 같으면 반지 1.
        /// value가 null이면 반지 1. 이미 낀 장비면 지금 자리를 돌려준다(G를 다시 눌러도 옮기지 않음, 옮기기는 Shift+G).
        /// </summary>
        public GearSlot ChooseSlot(GearItem item, Func<Loadout, double> value = null)
        {
            if (item == null) return GearSlot.Weapon;
            if (item.Part != GearPart.Ring) return GearSlots.FirstSlotOf(item.Part);
            var current = SlotOf(item);
            if (current.HasValue) return current.Value;
            if (this[GearSlot.Ring1] == null) return GearSlot.Ring1;
            if (this[GearSlot.Ring2] == null) return GearSlot.Ring2;
            if (value == null) return GearSlot.Ring1;
            var a = Clone();
            a.TryEquip(GearSlot.Ring1, item, out _);
            var b = Clone();
            b.TryEquip(GearSlot.Ring2, item, out _);
            return value(b) > value(a) ? GearSlot.Ring2 : GearSlot.Ring1;
        }

        /// <summary>겉모습 id 4개(갑옷·투구·장갑·장화).</summary>
        public GearLook Look => new GearLook(
            this[GearSlot.Armor]?.BaseId, this[GearSlot.Helm]?.BaseId, this[GearSlot.Gloves]?.BaseId, this[GearSlot.Boots]?.BaseId);

        /// <summary>작동하는 전설 효과(겹침 적용, 효과 차례 칸에 굴림‰, 없으면 -1).</summary>
        public int[] ActiveLegendaries() => LegendaryTable.Active(Items);

        /// <summary>낀 장비 전체의 그 종류 고유 값 합(같은 종류 반지 둘이면 두 번 더함, 8-6).</summary>
        public int IntrinsicTotal(OptionKind kind)
        {
            int sum = 0;
            foreach (var item in _slots)
                if (item != null) sum += item.IntrinsicTotal(kind);
            return sum;
        }

        /// <summary>낀 장비 전체의 그 종류 옵션 값 합(장비끼리는 합산, 반지 1·2도 합산, 5-2).</summary>
        public int OptionTotal(OptionKind kind)
        {
            int sum = 0;
            foreach (var item in _slots)
                if (item != null) sum += item.OptionTotal(kind);
            return sum;
        }
    }
}
