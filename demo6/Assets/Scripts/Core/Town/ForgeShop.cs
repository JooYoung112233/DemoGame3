using System;
using System.Collections.Generic;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;

namespace Demo6.Core.Town
{
    /// <summary>대장간 물건 칸 줄 상태(재화 쓸 곳 1차 6-2).</summary>
    public enum ForgeBuyState
    {
        /// <summary>살 수 있음('사기' 단추).</summary>
        CanBuy,
        /// <summary>이미 갖춤(회색 '갖춤').</summary>
        Owned,
        /// <summary>먼저 있어야 할 물건이 없음(또는 모르는 물건).</summary>
        Locked,
        /// <summary>골드가 모자람.</summary>
        NotEnoughGold,
    }

    /// <summary>대장간 물건 하나(6-2 표). Requires = 먼저 있어야 할 물건 id(없으면 null).</summary>
    public sealed class ForgeGood
    {
        public string Id, Name, Line, Requires;
        public int Price;

        public override string ToString() => Id;
    }

    /// <summary>
    /// 골드 쓸 곳: 모루 창 '대장간 물건' 칸(재화 쓸 곳 1차 6장). 순수 규칙. 시험: ForgeShopTests.
    /// 산 물건은 꾸러미 TownPurchases(글 줄 'bought=')에 id로 남고 한 번 사면 계속 간다(되팔기 없음, F1 되돌리기는 시험용).
    /// 효과: 가방 쇠틀 둘은 가방 칸 +5씩(BagRules.Capacity), 허리 병걸이는 원정마다 물약 칸 +1.
    /// 골드는 강화석과 바꾸지 않는다(2차 9-1). 물건 이름·글에 주민 이름을 쓰지 않는다.
    /// </summary>
    public static class ForgeShop
    {
        public const string Bag25 = "forge.bag25", Bag30 = "forge.bag30", Flask = "forge.flask";
        /// <summary>원정마다 물약 칸 기본값(병걸이 없음).</summary>
        public const int BasePotions = 3;
        /// <summary>허리 병걸이가 더하는 물약 칸.</summary>
        public const int FlaskPotions = 1;

        static readonly ForgeGood[] Table =
        {
            new ForgeGood { Id = Bag25, Name = "가방 쇠틀", Line = "가방 20 → 25칸", Price = 150 },
            new ForgeGood { Id = Bag30, Name = "큰 가방 쇠틀", Line = "가방 25 → 30칸", Price = 300, Requires = Bag25 },
            new ForgeGood { Id = Flask, Name = "허리 병걸이", Line = "원정마다 물약 3 → 4병", Price = 400 },
        };

        /// <summary>물건 셋(칸에 보이는 차례).</summary>
        public static IReadOnlyList<ForgeGood> All => Table;

        /// <summary>이 id의 물건(없으면 null).</summary>
        public static ForgeGood Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var g in Table)
                if (string.Equals(g.Id, id, StringComparison.Ordinal)) return g;
            return null;
        }

        /// <summary>이미 샀는가.</summary>
        public static bool Owned(CarryData carry, string id) => carry != null && !string.IsNullOrEmpty(id) && carry.TownPurchases.Contains(id);

        /// <summary>칸 줄 상태: 갖춤 → 잠김(먼저 있어야 할 것 없음·모르는 물건) → 골드 모자람 → 살 수 있음.</summary>
        public static ForgeBuyState State(CarryData carry, string id)
        {
            var good = Get(id);
            if (good == null || carry == null) return ForgeBuyState.Locked;
            if (Owned(carry, id)) return ForgeBuyState.Owned;
            if (!string.IsNullOrEmpty(good.Requires) && !Owned(carry, good.Requires)) return ForgeBuyState.Locked;
            if (carry.Gold < good.Price) return ForgeBuyState.NotEnoughGold;
            return ForgeBuyState.CanBuy;
        }

        /// <summary>산다: 골드를 빼고 TownPurchases에 더한다. 살 수 없으면(모자람·이미 있음·잠김) 아무것도 바꾸지 않고 false.</summary>
        public static bool TryBuy(CarryData carry, string id)
        {
            if (State(carry, id) != ForgeBuyState.CanBuy) return false;
            carry.Gold -= Get(id).Price;
            carry.TownPurchases.Add(id);
            return true;
        }

        /// <summary>산 가방 물건 수(0~2).</summary>
        public static int BagUpgrades(CarryData carry) => (Owned(carry, Bag25) ? 1 : 0) + (Owned(carry, Bag30) ? 1 : 0);

        /// <summary>가방 칸 = BagRules.Capacity(산 가방 물건 수): 20·25·30.</summary>
        public static int BagCapacity(CarryData carry) => BagRules.Capacity(BagUpgrades(carry));

        /// <summary>원정마다 물약 칸 = 3 + 병걸이(1): 3·4.</summary>
        public static int PotionCapacity(CarryData carry) => BasePotions + (Owned(carry, Flask) ? FlaskPotions : 0);
    }
}
