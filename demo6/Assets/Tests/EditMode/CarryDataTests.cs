using System.Linq;
using Demo6.Core.Dungeon;
using Demo6.Core.Loot;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 매판 새 탐험 1차 3-3 꾸러미: 글로 담기·풀기 왕복, 깊은 복사, 모르는 키 무시, 승강장 목록과 줄 끝(시험판 상한 2). 6장 위험 5 대응.
    /// 판본 2(장비 문서 12장 단계 2, 13장 위험 8): 장착 8자리 왕복(옵션·전설·강화·반지 2), 판본 1 글 읽기, 모르는 옵션 조각 건너뛰기.
    /// </summary>
    public sealed class CarryDataTests
    {
        static GearOption O(OptionKind kind, int value, int tier) => new GearOption(kind, value, tier);

        /// <summary>모든 칸을 기본값이 아닌 값으로 채운 꾸러미.</summary>
        static CarryData Full()
        {
            var d = CarryData.NewProfile(0xDEADBEEFCAFEF00DUL);
            d.Expedition = 4;
            d.Night = NightEvent.RatBurrow;
            d.Level = 5;
            d.TotalXp = 1834;
            d.SkillPoints = 2;
            d.SkillRanks = new[] { 1, 0, 3, 2 };
            d.Equipment[(int)GearSlot.Weapon] = new GearItem(GearBaseTable.Greatsword, Grade.Rare, 2, 1050);
            d.Equipment[(int)GearSlot.Armor] = new GearItem(GearBaseTable.ChainArmor, Grade.Uncommon, 2, 960, 1, new[] { O(OptionKind.HpPercent, 55, 2) });
            d.Equipment[(int)GearSlot.Ring2] = new GearItem(GearBaseTable.FangRing, Grade.Rare, 2, 1010, 0,
                new[] { O(OptionKind.CritChance, 19, 4), O(OptionKind.AttackFlat, 12, 1) });
            d.Bag.Add(GearItem.Starting(GearSlot.Weapon));
            d.Bag.Add(new GearItem(GearBaseTable.Twinblades, Grade.Epic, 3, 930));
            d.Stones = 37;
            d.Gold = 412;
            d.HasPickaxe = true;
            d.HasKey = true;
            d.RopeDepth = 2;
            d.LitLandings.Add(1);
            d.LitLandings.Add(2);
            d.VisitedFloors.Add(2);
            d.VisitedFloors.Add(1);
            d.DeepestFloor = 2;
            d.OnceDone.Add("f1.H.pickaxe");
            d.OnceDone.Add("f1.H.iron");
            d.OnceDone.Add("odd,id:with|marks=%");
            d.LastGlyphs[1] = FloorOneMap.Glyphs;
            d.LastGlyphs[2] = "E-1\n|.#\nS:H";
            d.DiscoveryPouch[1] = 260;
            d.DiscoveryPouch[2] = 190;
            d.DiscoveryXpGiven[1] = 240;
            d.DiscoveryXpGiven[2] = 12;
            d.SurveySheets[1] = 2;
            d.EventXpFloors.Add(1);
            d.Counters["landing_line"] = 3;
            d.Counters["map:first"] = 1;
            d.Leg = new ExpeditionLeg
            {
                Hp = 1720,
                Potions = 1,
                StartRealtime = 123.456f,
                Kills = 13,
                ChestsOpened = 4,
                CellsVisited = 9,
                CellsTotal = 10,
                StonesGained = 6,
                GoldGained = 88,
            };
            d.Leg.Floors.Add(1);
            d.Leg.Floors.Add(2);
            return d;
        }

        static void AssertGear(GearItem expected, GearItem actual, string what)
        {
            if (expected == null)
            {
                Assert.IsNull(actual, what);
                return;
            }
            Assert.IsNotNull(actual, what);
            Assert.AreEqual(expected.BaseId, actual.BaseId, what + " id");
            Assert.AreEqual(expected.Grade, actual.Grade, what + " 등급");
            Assert.AreEqual(expected.ItemLevel, actual.ItemLevel, what + " 아이템 레벨");
            Assert.AreEqual(expected.RollPermille, actual.RollPermille, what + " 굴림");
            Assert.AreEqual(expected.Enhance, actual.Enhance, what + " 강화");
            CollectionAssert.AreEqual(expected.Options.ToArray(), actual.Options.ToArray(), what + " 옵션(종류·값·단계, 차례)");
            Assert.AreEqual(expected.LegendaryId, actual.LegendaryId, what + " 전설");
            Assert.AreEqual(expected.LegendaryRollPermille, actual.LegendaryRollPermille, what + " 전설 굴림");
        }

        static void AssertSame(CarryData a, CarryData b)
        {
            Assert.AreEqual(a.Version, b.Version);
            Assert.AreEqual(a.ProfileSalt, b.ProfileSalt);
            Assert.AreEqual(a.Expedition, b.Expedition);
            Assert.AreEqual(a.Night, b.Night);
            Assert.AreEqual(a.Level, b.Level);
            Assert.AreEqual(a.TotalXp, b.TotalXp);
            Assert.AreEqual(a.SkillPoints, b.SkillPoints);
            CollectionAssert.AreEqual(a.SkillRanks, b.SkillRanks);
            Assert.AreEqual(GearSlots.SlotCount, b.Equipment.Length, "장착 8자리");
            foreach (var slot in GearSlots.All) AssertGear(a.Equipment[(int)slot], b.Equipment[(int)slot], "장착 " + GearSlots.SlotName(slot));
            Assert.AreEqual(a.Bag.Count, b.Bag.Count, "가방 칸 수");
            for (int i = 0; i < a.Bag.Count; i++) AssertGear(a.Bag[i], b.Bag[i], "가방 " + i);
            Assert.AreEqual(a.Stones, b.Stones);
            Assert.AreEqual(a.Gold, b.Gold);
            Assert.AreEqual(a.HasPickaxe, b.HasPickaxe);
            Assert.AreEqual(a.HasKey, b.HasKey);
            Assert.AreEqual(a.RopeDepth, b.RopeDepth);
            CollectionAssert.AreEqual(a.LitLandings.ToArray(), b.LitLandings.ToArray());
            CollectionAssert.AreEquivalent(a.VisitedFloors, b.VisitedFloors);
            Assert.AreEqual(a.DeepestFloor, b.DeepestFloor);
            CollectionAssert.AreEquivalent(a.OnceDone, b.OnceDone);
            CollectionAssert.AreEquivalent(a.LastGlyphs, b.LastGlyphs);
            CollectionAssert.AreEquivalent(a.DiscoveryPouch, b.DiscoveryPouch);
            CollectionAssert.AreEquivalent(a.DiscoveryXpGiven, b.DiscoveryXpGiven);
            CollectionAssert.AreEquivalent(a.SurveySheets, b.SurveySheets);
            CollectionAssert.AreEquivalent(a.EventXpFloors, b.EventXpFloors);
            CollectionAssert.AreEquivalent(a.Counters, b.Counters);
            if (a.Leg == null)
            {
                Assert.IsNull(b.Leg, "원정 몫");
                return;
            }
            Assert.IsNotNull(b.Leg, "원정 몫");
            Assert.AreEqual(a.Leg.Hp, b.Leg.Hp);
            Assert.AreEqual(a.Leg.Potions, b.Leg.Potions);
            Assert.AreEqual(a.Leg.StartRealtime, b.Leg.StartRealtime);
            Assert.AreEqual(a.Leg.Kills, b.Leg.Kills);
            Assert.AreEqual(a.Leg.ChestsOpened, b.Leg.ChestsOpened);
            Assert.AreEqual(a.Leg.CellsVisited, b.Leg.CellsVisited);
            Assert.AreEqual(a.Leg.CellsTotal, b.Leg.CellsTotal);
            Assert.AreEqual(a.Leg.StonesGained, b.Leg.StonesGained);
            Assert.AreEqual(a.Leg.GoldGained, b.Leg.GoldGained);
            CollectionAssert.AreEqual(a.Leg.Floors, b.Leg.Floors);
        }

        [Test]
        public void TextRoundTripKeepsEveryField()
        {
            var d = Full();
            string text = d.ToText();
            StringAssert.StartsWith("carry v" + CarryData.CurrentVersion + "\n", text);
            var back = CarryData.FromText(text);
            AssertSame(d, back);
            Assert.AreEqual(text, back.ToText(), "다시 담아도 같은 글");
            Assert.AreEqual(FloorOneMap.Glyphs.Replace("\r", ""), back.LastGlyphs[1].Replace("\r", ""), "지도 글자(줄바꿈·판자벽 ':'·자물쇠 '=')");
        }

        [Test]
        public void CloneIsDeepAndEqual()
        {
            var d = Full();
            var c = d.Clone();
            AssertSame(d, c);
            c.OnceDone.Add("f1.T.nameplate");
            c.Bag.Clear();
            c.Leg.Floors.Add(3);
            c.LastGlyphs[1] = "";
            Assert.IsFalse(d.OnceDone.Contains("f1.T.nameplate"), "복사본을 고쳐도 원본은 그대로");
            Assert.AreEqual(2, d.Bag.Count);
            Assert.AreEqual(2, d.Leg.Floors.Count);
            Assert.AreEqual(FloorOneMap.Glyphs, d.LastGlyphs[1]);
        }

        [Test]
        public void NewProfileRoundTripsWithoutLeg()
        {
            var d = CarryData.NewProfile(7UL);
            Assert.IsNull(d.Leg);
            var back = CarryData.FromText(d.ToText());
            AssertSame(d, back);
            foreach (var slot in GearSlots.All)
                Assert.IsNull(back.Equipment[(int)slot], GearSlots.SlotName(slot) + " 비어 있음 = 시작 장비(앞 5자리)·빈 자리(반지·목걸이)");
            Assert.AreEqual(1, back.Expedition);
            Assert.AreEqual(1, back.Level);
            Assert.AreEqual(1, back.RopeDepth);
        }

        [Test]
        public void UnknownKeysAndOtherVersionsReadOnlyKnownKeys()
        {
            string text = "carry v99\n" +
                          "level=6\n" +
                          "someday_village=3\n" +
                          "no equals sign here\n" +
                          "gold=not-a-number\n" +
                          "stones=12\n" +
                          "leg.unknown=5\n" +
                          "oncedone=f1.H.pickaxe\n";
            var d = CarryData.FromText(text);
            Assert.AreEqual(CarryData.CurrentVersion, d.Version, "풀린 꾸러미는 지금 버전");
            Assert.AreEqual(6, d.Level);
            Assert.AreEqual(12, d.Stones);
            Assert.AreEqual(0, d.Gold, "읽지 못한 값은 기본값");
            Assert.AreEqual(1, d.Expedition, "없는 키는 기본값");
            Assert.IsTrue(d.OnceDone.Contains("f1.H.pickaxe"));
            Assert.IsNull(d.Leg, "모르는 leg 키로는 원정 몫을 만들지 않음");
            Assert.Throws<System.FormatException>(() => CarryData.FromText("not a carry\nlevel=3"));
            Assert.Throws<System.FormatException>(() => CarryData.FromText(""));
        }

        [Test]
        public void LandingFloorsStopAtRopeEndAndTestCap()
        {
            int cap = FloorRecipe.MaxTestFloor;
            Assert.AreEqual(2, cap, "첫 시험판은 1층 + 생성 2층");
            var d = CarryData.NewProfile(1UL);
            CollectionAssert.AreEqual(new[] { 1 }, d.LandingFloors(cap));
            Assert.AreEqual(1, d.RopeEnd(cap));

            d.RopeDepth = 2;
            CollectionAssert.AreEqual(new[] { 1, 2 }, d.LandingFloors(cap));
            Assert.AreEqual(2, d.RopeEnd(cap), "줄 끝 = 가장 깊은 승강장");

            d.RopeDepth = 5;
            CollectionAssert.AreEqual(new[] { 1, 2 }, d.LandingFloors(cap), "시험판 상한을 넘지 않음");
            Assert.AreEqual(2, d.RopeEnd(cap));

            d.RopeDepth = 0;
            CollectionAssert.AreEqual(new[] { 1 }, d.LandingFloors(cap), "1층 승강장은 늘 있음");
            Assert.AreEqual(1, d.RopeEnd(cap));
        }

        [Test]
        public void CountersBumpFromZero()
        {
            var d = CarryData.NewProfile(1UL);
            Assert.AreEqual(0, d.Count("landing_line"));
            Assert.AreEqual(1, d.Bump("landing_line"));
            Assert.AreEqual(2, d.Bump("landing_line"));
            Assert.AreEqual(2, d.Count("landing_line"));
        }

        /// <summary>장착 8자리 전부(반지 1은 비우고 반지 2만), 옵션·전설·강화가 붙은 장비.</summary>
        static CarryData FullGear()
        {
            var d = CarryData.NewProfile(9UL);
            d.Equipment[(int)GearSlot.Weapon] = new GearItem(GearBaseTable.Twinblades, Grade.Legendary, 7, 1080, 12,
                new[] { O(OptionKind.AttackPercent, 96, 3), O(OptionKind.CritChance, 51, 2), O(OptionKind.AttackSpeed, 104, 4) },
                LegendaryTable.ChainLightningId, 640);
            d.Equipment[(int)GearSlot.Armor] = new GearItem(GearBaseTable.PlateArmor, Grade.Epic, 5, 940, 3,
                new[] { O(OptionKind.HpFlat, 172, 2), O(OptionKind.DefenseFlat, 88, 4), O(OptionKind.OnKillHeal, 21, 1) });
            d.Equipment[(int)GearSlot.Helm] = new GearItem(GearBaseTable.ChainHelm, Grade.Rare, 4, 1000, 0,
                new[] { O(OptionKind.CooldownReduction, 35, 3), O(OptionKind.SkillDamage, 52, 2) });
            d.Equipment[(int)GearSlot.Gloves] = new GearItem(GearBaseTable.PlateGloves, Grade.Uncommon, 2, 1100, 7, new[] { O(OptionKind.LifeSteal, 3, 3) });
            d.Equipment[(int)GearSlot.Boots] = new GearItem(GearBaseTable.LeatherBoots, Grade.Legendary, 6, 900, 0,
                new[] { O(OptionKind.MoveSpeed, 110, 4), O(OptionKind.HpRegen, 14, 2), O(OptionKind.DefenseFlat, 30, 1) },
                LegendaryTable.FlameStepsId, 1000);
            d.Equipment[(int)GearSlot.Ring2] = new GearItem(GearBaseTable.BloodRing, Grade.Rare, 3, 1010, 4,
                new[] { O(OptionKind.CritChance, 19, 4), O(OptionKind.AttackFlat, 21, 2) });
            d.Equipment[(int)GearSlot.Amulet] = new GearItem(GearBaseTable.AmberAmulet, Grade.Legendary, 5, 990, 15,
                new[] { O(OptionKind.CritDamage, 270, 4), O(OptionKind.HpFlat, 80, 1), O(OptionKind.CooldownReduction, 41, 2) },
                LegendaryTable.ChainBlastId, 0);
            d.Bag.Add(new GearItem(GearBaseTable.IronRing, Grade.Uncommon, 1, 1000, 0, new[] { O(OptionKind.HpFlat, 63, 2) }));
            d.Bag.Add(new GearItem(GearBaseTable.ChainGloves, Grade.Legendary, 8, 1040, 2,
                new[] { O(OptionKind.AttackSpeed, 88, 4), O(OptionKind.CritChance, 44, 3), O(OptionKind.AttackFlat, 41, 2) },
                LegendaryTable.ChainLightningId, 777));
            d.Bag.Add(GearItem.Starting(GearSlot.Helm));
            return d;
        }

        [Test]
        public void EightSlotsRoundTripWithOptionsLegendaryEnhanceAndRingTwo()
        {
            var d = FullGear();
            string text = d.ToText();
            var back = CarryData.FromText(text);
            AssertSame(d, back);
            Assert.IsNull(back.Equipment[(int)GearSlot.Ring1], "반지 1은 빈 자리 그대로");
            Assert.IsNotNull(back.Equipment[(int)GearSlot.Ring2], "반지 2만 낀 상태가 남음");
            Assert.AreEqual(text, back.ToText(), "다시 담아도 같은 글");
            Assert.AreEqual(text, back.Clone().ToText(), "깊은 복사도 같은 글");
            Assert.AreEqual(text, FullGear().ToText(), "같은 값이면(다른 객체라도) 같은 글");

            StringAssert.Contains("\neq.weapon=wpn_twinblades|Legendary|7|1080|12|AttackPercent~96~3;CritChance~51~2;AttackSpeed~104~4|leg_chain_lightning~640\n", text);
            StringAssert.Contains("\neq.ring1=\n", text);
            StringAssert.Contains("\neq.ring2=rng_blood|Rare|3|1010|4|CritChance~19~4;AttackFlat~21~2|\n", text);
            StringAssert.Contains("\neq.amulet=amu_amber|Legendary|5|990|15|CritDamage~270~4;HpFlat~80~1;CooldownReduction~41~2|leg_chain_blast~0\n", text);
            StringAssert.Contains("\nbag=rng_iron|Uncommon|1|1000|0|HpFlat~63~2|,glv_chain|Legendary|8|1040|2|", text);
            Assert.AreEqual(-1, text.IndexOf("\nequipped=", System.StringComparison.Ordinal), "판본 2는 옛 'equipped' 줄을 쓰지 않음");
            int eqLines = text.Split('\n').Count(l => l.StartsWith("eq.", System.StringComparison.Ordinal));
            Assert.AreEqual(GearSlots.SlotCount, eqLines, "장착 8줄");
        }

        [Test]
        public void ReadsVersionOneEquippedWeaponAndFourFieldBag()
        {
            string v1 = "carry v1\n" +
                        "level=3\n" +
                        "equipped=wpn_greatsword|Rare|2|1050\n" +
                        "bag=wpn_longsword|Common|1|1000,wpn_twinblades|Epic|3|930\n" +
                        "stones=5\n";
            var d = CarryData.FromText(v1);
            Assert.AreEqual(CarryData.CurrentVersion, d.Version);
            Assert.AreEqual(3, d.Level);
            Assert.AreEqual(5, d.Stones);
            AssertGear(new GearItem(GearBaseTable.Greatsword, Grade.Rare, 2, 1050), d.Equipment[(int)GearSlot.Weapon], "옛 낀 무기 → 무기 자리");
            for (int i = 1; i < GearSlots.SlotCount; i++)
                Assert.IsNull(d.Equipment[i], GearSlots.SlotName((GearSlot)i) + ": 옛 글에는 없음(시작 장비·빈 자리)");
            Assert.AreEqual(2, d.Bag.Count, "4칸짜리 가방 항목 2개");
            AssertGear(new GearItem(GearBaseTable.Longsword, Grade.Common, 1, 1000), d.Bag[0], "가방 0");
            AssertGear(new GearItem(GearBaseTable.Twinblades, Grade.Epic, 3, 930), d.Bag[1], "가방 1");
            Assert.IsTrue(d.Bag[1].IsWeapon);

            string v2 = d.ToText();
            StringAssert.StartsWith("carry v2\n", v2);
            StringAssert.Contains("\neq.weapon=wpn_greatsword|Rare|2|1050|0||\n", v2);
            AssertSame(d, CarryData.FromText(v2));
        }

        [Test]
        public void UnknownOptionAndLegendaryPiecesAreSkippedButGearSurvives()
        {
            string text = "carry v2\n" +
                          "eq.armor=arm_chain|Rare|3|1000|2|HpFlat~150~3;MagicFind~40~2;DefenseFlat~x~1;Bogus;DefenseFlat~60~4|leg_unknown~500\n" +
                          "eq.helm=rng_iron|Common|1|1000|0||\n" +
                          "eq.boots=bts_bone|Rare|1|1000|0||\n" +
                          "eq.ring1=rng_fang|Epic|2|1000|notanumber|CritChance~12~2|leg_chain_blast~720\n" +
                          "eq.cape=arm_plate|Common|1|1000|0||\n" +
                          "bag=arm_plate|Common|1|1000|0||,arm_mystery|Rare|1|1000|0||,amu_charm|Uncommon|1|1000|0|Mystery~1~1|,glv_chain|Unheard|1|1000|0||\n";
            var d = CarryData.FromText(text);

            var armor = d.Equipment[(int)GearSlot.Armor];
            AssertGear(new GearItem(GearBaseTable.ChainArmor, Grade.Rare, 3, 1000, 2,
                new[] { O(OptionKind.HpFlat, 150, 3), O(OptionKind.DefenseFlat, 60, 4) }), armor, "모르는 옵션·전설 조각만 버린 갑옷");
            Assert.IsFalse(armor.IsLegendary, "모르는 전설은 버림");
            Assert.IsNull(d.Equipment[(int)GearSlot.Helm], "투구 자리에 반지 → 시작 장비");
            Assert.IsNull(d.Equipment[(int)GearSlot.Boots], "모르는 종류 id → 시작 장비");
            AssertGear(new GearItem(GearBaseTable.FangRing, Grade.Epic, 2, 1000, 0, new[] { O(OptionKind.CritChance, 12, 2) }, LegendaryTable.ChainBlastId, 720),
                d.Equipment[(int)GearSlot.Ring1], "읽지 못한 강화는 0, 전설은 살림");

            Assert.AreEqual(2, d.Bag.Count, "모르는 종류·등급 장비는 버리고 나머지는 살림");
            AssertGear(new GearItem(GearBaseTable.PlateArmor, Grade.Common, 1, 1000), d.Bag[0], "가방 0");
            AssertGear(new GearItem(GearBaseTable.CharmAmulet, Grade.Uncommon, 1, 1000), d.Bag[1], "가방 1(모르는 옵션 버림)");
        }

        [Test]
        public void GearTextParsesBackToSameGear()
        {
            var g = new GearItem(GearBaseTable.PlateHelm, Grade.Epic, 9, 1093, 11,
                new[] { O(OptionKind.SkillDamage, 71, 4), O(OptionKind.HpFlat, 133, 2), O(OptionKind.OnKillHeal, 30, 3) },
                LegendaryTable.ChainBlastId, 512);
            string text = CarryData.GearText(g);
            Assert.AreEqual("hlm_plate|Epic|9|1093|11|SkillDamage~71~4;HpFlat~133~2;OnKillHeal~30~3|leg_chain_blast~512", text);
            AssertGear(g, CarryData.ParseGear(text), "장비 글 왕복");
            Assert.AreEqual("", CarryData.GearText(null));
            Assert.IsNull(CarryData.ParseGear(""));
            Assert.IsNull(CarryData.ParseGear("wpn_longsword|Common|1"), "칸이 모자람");
        }

        [Test]
        public void EscapeRoundTripsSeparators()
        {
            string raw = "a%b\nc,d:e|f=g\rh";
            string esc = CarryData.Escape(raw);
            foreach (char c in "\n\r,:|=") Assert.AreEqual(-1, esc.IndexOf(c), "나누는 글자 '" + c + "'가 남음");
            Assert.AreEqual(raw, CarryData.Unescape(esc));
        }
    }
}
