using System.Linq;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using Demo6.Core.Town;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 재화 쓸 곳 1차 12-1 ForgeShopTests(14~17): 대장간 물건 셋(값 150·300·400, 큰 가방 쇠틀은 가방 쇠틀 뒤),
    /// 사기(골드가 빠지고 갖춤, 모자람·이미 있음·잠김이면 아무것도 안 바뀜), 가방 칸 20 → 25 → 30·물약 3 → 4,
    /// ForgeText 글 전부와 물건 이름·한 줄에 주민 이름(NpcTable)이 없음. 6-2·5-3·2-6 글 몇 줄은 글자까지 본다.
    /// </summary>
    public sealed class ForgeShopTests
    {
        static CarryData Carry(int gold)
        {
            var c = CarryData.NewProfile(11UL);
            c.Gold = gold;
            return c;
        }

        // ── 14. 물건 표 ──

        [Test]
        public void ThreeGoodsWithPricesAndOrder()
        {
            CollectionAssert.AreEqual(new[] { ForgeShop.Bag25, ForgeShop.Bag30, ForgeShop.Flask }, ForgeShop.All.Select(g => g.Id).ToArray());
            Assert.AreEqual("forge.bag25", ForgeShop.Bag25);
            Assert.AreEqual("forge.bag30", ForgeShop.Bag30);
            Assert.AreEqual("forge.flask", ForgeShop.Flask);
            CollectionAssert.AreEqual(new[] { 150, 300, 400 }, ForgeShop.All.Select(g => g.Price).ToArray());
            CollectionAssert.AreEqual(new[] { "가방 쇠틀", "큰 가방 쇠틀", "허리 병걸이" }, ForgeShop.All.Select(g => g.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "가방 20 → 25칸", "가방 25 → 30칸", "원정마다 물약 3 → 4병" }, ForgeShop.All.Select(g => g.Line).ToArray());
            Assert.AreEqual(ForgeShop.Bag25, ForgeShop.Get(ForgeShop.Bag30).Requires, "큰 가방 쇠틀은 가방 쇠틀 뒤");
            Assert.IsNull(ForgeShop.Get(ForgeShop.Bag25).Requires);
            Assert.IsNull(ForgeShop.Get(ForgeShop.Flask).Requires);
            Assert.IsNull(ForgeShop.Get("forge.unknown"));
            Assert.IsNull(ForgeShop.Get(null));
            Assert.AreEqual(3, ForgeShop.BasePotions);
        }

        // ── 15. 사기 ──

        [Test]
        public void BuyingSpendsGoldAndBlockedBuysChangeNothing()
        {
            var c = Carry(1000);
            Assert.AreEqual(ForgeBuyState.Locked, ForgeShop.State(c, ForgeShop.Bag30), "가방 쇠틀이 먼저");
            Assert.IsFalse(ForgeShop.TryBuy(c, ForgeShop.Bag30));
            Assert.AreEqual(1000, c.Gold, "잠김이면 골드 그대로");
            Assert.AreEqual(0, c.TownPurchases.Count);

            Assert.AreEqual(ForgeBuyState.CanBuy, ForgeShop.State(c, ForgeShop.Bag25));
            Assert.IsTrue(ForgeShop.TryBuy(c, ForgeShop.Bag25));
            Assert.AreEqual(850, c.Gold);
            Assert.IsTrue(ForgeShop.Owned(c, ForgeShop.Bag25));
            Assert.AreEqual(ForgeBuyState.Owned, ForgeShop.State(c, ForgeShop.Bag25));
            Assert.IsFalse(ForgeShop.TryBuy(c, ForgeShop.Bag25), "이미 있음");
            Assert.AreEqual(850, c.Gold, "이미 있으면 골드 그대로");
            Assert.AreEqual(1, c.TownPurchases.Count);

            Assert.AreEqual(ForgeBuyState.CanBuy, ForgeShop.State(c, ForgeShop.Bag30), "잠김이 풀림");
            c.Gold = 299;
            Assert.AreEqual(ForgeBuyState.NotEnoughGold, ForgeShop.State(c, ForgeShop.Bag30));
            Assert.IsFalse(ForgeShop.TryBuy(c, ForgeShop.Bag30));
            Assert.AreEqual(299, c.Gold, "모자라면 골드 그대로");
            Assert.IsFalse(ForgeShop.Owned(c, ForgeShop.Bag30));
            c.Gold = 300;
            Assert.IsTrue(ForgeShop.TryBuy(c, ForgeShop.Bag30), "딱 맞는 골드");
            Assert.AreEqual(0, c.Gold);

            Assert.AreEqual(ForgeBuyState.Locked, ForgeShop.State(c, "forge.unknown"), "모르는 물건");
            Assert.IsFalse(ForgeShop.TryBuy(c, "forge.unknown"));
            Assert.AreEqual(2, c.TownPurchases.Count);
            Assert.AreEqual(ForgeBuyState.Locked, ForgeShop.State(null, ForgeShop.Bag25));
            Assert.IsFalse(ForgeShop.TryBuy(null, ForgeShop.Bag25));
            Assert.IsFalse(ForgeShop.Owned(null, ForgeShop.Bag25));
        }

        // ── 16. 효과 ──

        [Test]
        public void BagGrowsTwentyToThirtyAndFlaskAddsAPotion()
        {
            var c = Carry(2000);
            Assert.AreEqual(0, ForgeShop.BagUpgrades(c));
            Assert.AreEqual(20, ForgeShop.BagCapacity(c));
            Assert.AreEqual(3, ForgeShop.PotionCapacity(c));
            Assert.IsTrue(ForgeShop.TryBuy(c, ForgeShop.Bag25));
            Assert.AreEqual(25, ForgeShop.BagCapacity(c));
            Assert.IsTrue(ForgeShop.TryBuy(c, ForgeShop.Bag30));
            Assert.AreEqual(2, ForgeShop.BagUpgrades(c));
            Assert.AreEqual(30, ForgeShop.BagCapacity(c));
            Assert.AreEqual(3, ForgeShop.PotionCapacity(c), "가방 물건은 물약과 상관없음");
            Assert.IsTrue(ForgeShop.TryBuy(c, ForgeShop.Flask));
            Assert.AreEqual(4, ForgeShop.PotionCapacity(c));
            Assert.AreEqual(2000 - 850, c.Gold, "셋 합 850골드");

            var back = CarryData.FromText(c.ToText());
            Assert.AreEqual(30, ForgeShop.BagCapacity(back), "꾸러미 글을 지나도 그대로");
            Assert.AreEqual(4, ForgeShop.PotionCapacity(back));
            Assert.AreEqual(BagRules.Capacity(0), ForgeShop.BagCapacity(null));
            Assert.AreEqual(ForgeShop.BasePotions, ForgeShop.PotionCapacity(null));
        }

        // ── 17. 주민 이름 없음·글 ──

        [Test]
        public void ForgeTextsHaveNoResidentNames()
        {
            var names = NpcTable.All.Select(n => n.DisplayName).ToArray();
            int count = 0;
            foreach (var t in ForgeText.AllTexts)
            {
                count++;
                Assert.IsFalse(string.IsNullOrWhiteSpace(t), "빈 글");
                foreach (var n in names) StringAssert.DoesNotContain(n, t, "모루 글에 주민 이름: " + t);
            }
            Assert.Greater(count, 40, "글을 모두 모았다");
            foreach (var g in ForgeShop.All)
                foreach (var n in names)
                {
                    StringAssert.DoesNotContain(n, g.Name);
                    StringAssert.DoesNotContain(n, g.Line);
                }
        }

        [Test]
        public void ForgeTextLinesFromDesign()
        {
            Assert.AreEqual("모루 — 무엇을 두드릴까", ForgeText.WindowTitle);
            Assert.AreEqual("가방 쇠틀을 달았다 — 가방 25칸", ForgeText.BoughtNotice(ForgeShop.Bag25));
            Assert.AreEqual("큰 가방 쇠틀을 달았다 — 가방 30칸", ForgeText.BoughtNotice(ForgeShop.Bag30));
            Assert.AreEqual("허리 병걸이를 찼다 — 원정마다 물약 4병", ForgeText.BoughtNotice(ForgeShop.Flask));
            Assert.IsNull(ForgeText.BoughtNotice("forge.unknown"));

            var c = Carry(100);
            Assert.AreEqual("골드가 모자란다 — 150 필요, 100 있음", ForgeText.GoodStateLine(c, ForgeShop.Bag25));
            Assert.AreEqual("가방 쇠틀이 먼저 있어야 한다", ForgeText.GoodStateLine(c, ForgeShop.Bag30));
            c.Gold = 500;
            Assert.AreEqual(ForgeText.BuyButton, ForgeText.GoodStateLine(c, ForgeShop.Bag25));
            ForgeShop.TryBuy(c, ForgeShop.Bag25);
            Assert.AreEqual(ForgeText.OwnedLabel, ForgeText.GoodStateLine(c, ForgeShop.Bag25));

            // 2-6 강화 칸
            Assert.AreEqual("+3 → +4", ForgeText.StepLine(3, 4));
            Assert.AreEqual("강화석 3 (가진 것 31)", ForgeText.CostLine(3, 31));
            Assert.AreEqual("성공 85%", ForgeText.ChanceLine(850));
            Assert.AreEqual("성공 100% — 확정", ForgeText.ChanceLine(1000));
            Assert.AreEqual("실패할 때마다 +10%p, 두 번 실패하면 다음은 반드시 성공한다. (지금 1/2)", ForgeText.PityGaugeLine(100, 2, 1));
            Assert.AreEqual("일반은 +5까지 오른다.", ForgeText.GradeCapLine(Grade.Common));
            Assert.AreEqual("고급은 +7까지 오른다.", ForgeText.GradeCapLine(Grade.Uncommon));
            Assert.AreEqual("강화석이 모자란다 — 3 필요, 2 있음", ForgeText.NotEnoughStonesLine(3, 2));
            Assert.AreEqual("치익 — 실패. 강화석 3을 잃었다. 다음은 확정.", ForgeText.FailLine(3, 1000));
            Assert.AreEqual("치익 — 실패. 강화석 2를 잃었다. 다음은 성공 80%.", ForgeText.FailLine(2, 800));
            var sword = new GearItem(GearBaseTable.Longsword, Grade.Rare, 2, 1000, 3);
            Assert.AreEqual("쨍 — " + sword.DisplayName + " +4!", ForgeText.SuccessLine(sword.WithEnhance(4)));
            Assert.AreEqual("무기 · " + sword.DisplayName + " +3 (+3/7)", ForgeText.EnhanceRow(sword));

            // 견적에서 고르는 천장·막힘 줄
            Assert.AreEqual(ForgeText.PityShortLine, ForgeText.PityLine(EnhanceRules.Quote(sword, 99)), "+4는 한 번 실패하면 확정");
            Assert.IsNull(ForgeText.PityLine(EnhanceRules.Quote(sword.WithEnhance(0), 99)), "+1은 천장 줄 없음");
            Assert.AreEqual("실패할 때마다 +10%p, 두 번 실패하면 다음은 반드시 성공한다. (지금 0/2)",
                ForgeText.PityLine(EnhanceRules.Quote(sword.WithEnhance(5), 99)));
            Assert.AreEqual(ForgeText.FirstPassCapLine, ForgeText.BlockLine(EnhanceRules.Quote(sword.WithEnhance(7), 99), sword, 99));
            Assert.AreEqual("강화석이 모자란다 — 3 필요, 1 있음", ForgeText.BlockLine(EnhanceRules.Quote(sword, 1), sword, 1));
            Assert.IsNull(ForgeText.BlockLine(EnhanceRules.Quote(sword, 3), sword, 3));

            // 3장·4장·5장
            Assert.AreEqual("강화 +3 사라짐 — 옮겨지지 않는다", ForgeText.EnhanceLostLine(3));
            Assert.AreEqual("한 번 더 누르면 분해 (+6석)", ForgeText.ConfirmSalvage(6));
            Assert.AreEqual("한 번 더 누르면 버린다 — 분해하면 강화석 +6", ForgeText.ConfirmDiscard(6));
            Assert.AreEqual("일반 장검을 발밑에 내려놓았다", ForgeText.PutDownNotice("일반 장검"));
            Assert.AreEqual("일반 가죽 두건을 버렸다", ForgeText.DiscardNotice("일반 가죽 두건"));
            Assert.AreEqual("일반 쇠 반지를 버렸다", ForgeText.DiscardNotice("일반 쇠 반지"));
            Assert.AreEqual("가방이 넘쳤다 — 벗은 일반 장검을 발밑에 내려놓았다", ForgeText.OverflowSwapNotice("일반 장검"));
            Assert.AreEqual("일반 모두 분해 — 8개 · 강화석 +8", ForgeText.BulkSalvageButton(8, 8));
            Assert.AreEqual("일반 가죽 장화 — 저절로 분해 · 강화석 +1", ForgeText.AutoSalvageNotice("일반 가죽 장화", 1));
            Assert.AreEqual("저절로 분해 3개 · 강화석 +3", ForgeText.AutoSalvageBatchNotice(3, 3));
            Assert.AreEqual("가방 가득 — 일반·고급 3개 분해 · 강화석 +4", ForgeText.SweepSalvageLine(3, 4));
            Assert.AreEqual("가방이 가득 찼다 (20/20) — [I] 가방에서 분해하거나 내려놓자", ForgeText.BagFullNotice(20, 20));
            Assert.AreEqual("17/20", ForgeText.BagCountLine(17, 20));
            Assert.AreEqual("가방 17/20", ForgeText.TownBagLine(17, 20));
            Assert.AreEqual("가방이 거의 찼다 (17/20) — 모루에서 분해하고 가도 된다.", ForgeText.GateNearlyFullLine(17, 20));
            Assert.AreEqual("강화석은 골드로 살 수 없다 — 갱도와 분해에서만 나온다.", ForgeText.NoStonesForGold);
        }
    }
}
