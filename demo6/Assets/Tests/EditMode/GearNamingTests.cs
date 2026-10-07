using Demo6.Core.Loot;
using Demo6.Core.Random;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>
    /// 장비 이름(장비 문서 5-4, 2026-10-03 결정 '등급 + 종류만'): 이름 = 등급 말 + 기본 이름.
    /// 꾸밈말·영웅 고유 이름·전설 칭호는 쓰지 않고, 전설 효과는 카드 첫 옵션 줄 '★ 효과 이름'으로 따로 보인다.
    /// </summary>
    public sealed class GearNamingTests
    {
        [Test]
        public void NameIsGradePlusKind()
        {
            Assert.AreEqual("희귀 핏빛 반지", new GearItem(GearBaseTable.BloodRing, Grade.Rare, 3, 1000).DisplayName);
            Assert.AreEqual("일반 한손검과 방패", GearItem.Starting(GearSlot.Weapon).DisplayName);
            Assert.AreEqual("일반 가죽 장갑", GearItem.Starting(GearSlot.Gloves).DisplayName);
            Assert.AreEqual("영웅 판금 장갑", new GearItem(GearBaseTable.PlateGloves, Grade.Epic, 9, 1000).DisplayName);
            Assert.AreEqual("고급 검은 호박 목걸이", new GearItem(GearBaseTable.AmberAmulet, Grade.Uncommon, 2, 950).DisplayName);
            Assert.AreEqual("전설 대검", new GearItem(GearBaseTable.Greatsword, Grade.Legendary, 10, 1000, 0, null, LegendaryTable.ChainLightningId, 800).DisplayName);
        }

        /// <summary>무기 이름은 옛 WeaponItem.DisplayName과 같은 모양이다('희귀 대검').</summary>
        [Test]
        public void WeaponNameMatchesLegacyWeaponItem()
        {
            foreach (var b in GearBaseTable.ForPart(GearPart.Weapon))
                for (int g = 0; g < GradeRules.Count; g++)
                {
                    var legacy = new WeaponItem(b.Id, (Grade)g, 4, 1000);
                    var gear = GearItem.FromWeapon(legacy);
                    Assert.AreEqual(legacy.DisplayName, gear.DisplayName);
                    Assert.AreEqual(legacy.Attack, gear.Attack);
                }
            Assert.AreEqual("희귀 대검", new GearItem(GearBaseTable.Greatsword, Grade.Rare, 1, 1000).DisplayName);
        }

        /// <summary>27종 × 5등급 모두 '등급 + 기본 이름'(새 무기 '희귀 쇠망치', '전설 사슬 철퇴'). 옵션·전설·강화·굴림은 이름을 바꾸지 않는다(꾸밈말 없음).</summary>
        [Test]
        public void OptionsAndLegendaryDoNotChangeTheName()
        {
            var rng = new Pcg32Random(321);
            Assert.AreEqual(27, GearBaseTable.All.Count);
            Assert.AreEqual("희귀 쇠망치", new GearItem(GearBaseTable.Maul, Grade.Rare, 2, 1000).DisplayName);
            Assert.AreEqual("전설 사슬 철퇴", GearNaming.DisplayName(Grade.Legendary, GearBaseTable.Flail));
            foreach (var b in GearBaseTable.All)
                for (int g = 0; g < GradeRules.Count; g++)
                {
                    var grade = (Grade)g;
                    string expected = GradeRules.Name(grade) + " " + b.Name;
                    var options = OptionTable.RollAll(b.Part, grade, 6, rng);
                    var legend = grade == Grade.Legendary ? LegendaryTable.Get(LegendaryTable.EffectsOn(b.Part)[0]).Id : null;
                    var item = new GearItem(b.Id, grade, 6, 1080, 3, options, legend, 650);
                    Assert.AreEqual(expected, item.DisplayName);
                    Assert.AreEqual(expected, GearNaming.DisplayName(grade, b.Id));
                    Assert.AreEqual(expected, new GearItem(b.Id, grade, 1, 900).DisplayName);
                }
        }

        /// <summary>전설은 '★ 효과 이름'이 카드 첫 옵션 줄이다. 칭호(벼락 갈래 등)는 쓰지 않는다.</summary>
        [Test]
        public void LegendaryLineIsFirstCardLine()
        {
            OptionTable.TryGetRule(GearPart.Weapon, OptionKind.AttackPercent, out var atk);
            var options = new[] { OptionTable.FromRaw(atk, Grade.Legendary, 8, 60 * OptionTable.RawScale) };
            var item = new GearItem(GearBaseTable.Greatsword, Grade.Legendary, 8, 1000, 0, options, LegendaryTable.ChainLightningId, 500);
            Assert.AreEqual("★ 연쇄 번개", GearNaming.LegendaryLine(item));
            var lines = GearNaming.CardLines(item);
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("★ 연쇄 번개", lines[0]);
            Assert.AreEqual("+7.2% 공격력", lines[1], "원값 60‰ × 전설 1.5 × 옵션 세기 0.8 = 72‰");

            Assert.AreEqual("★ 불꽃 발자국", GearNaming.LegendaryLine(new GearItem(GearBaseTable.LeatherBoots, Grade.Legendary, 5, 1000, 0, null, LegendaryTable.FlameStepsId, 0)));
            Assert.AreEqual("★ 연쇄 폭발", GearNaming.LegendaryLine(new GearItem(GearBaseTable.FangAmulet, Grade.Legendary, 5, 1000, 0, null, LegendaryTable.ChainBlastId, 0)));
            Assert.AreEqual("", GearNaming.LegendaryLine(GearItem.Starting(GearSlot.Weapon)));
            Assert.AreEqual(0, GearNaming.CardLines(GearItem.Starting(GearSlot.Weapon)).Count);
            Assert.AreEqual(0, GearNaming.CardLines(null).Count);

            foreach (var def in LegendaryTable.All)
            {
                StringAssert.DoesNotContain("벼락", def.Name);
                StringAssert.DoesNotContain("잿불", def.Name);
                StringAssert.DoesNotContain("무덤", def.Name);
            }
        }

        [Test]
        public void OptionLineAndTierMark()
        {
            Assert.AreEqual("+6.2% 공격력", GearNaming.OptionLine(new GearOption(OptionKind.AttackPercent, 62, 2)));
            Assert.AreEqual("+25 공격력", GearNaming.OptionLine(new GearOption(OptionKind.AttackFlat, 25, 3)));
            Assert.AreEqual("+4.4% 치명타 확률", GearNaming.OptionLine(new GearOption(OptionKind.CritChance, 44, 4)));
            Assert.AreEqual("Ⅰ", GearNaming.TierMark(1));
            Assert.AreEqual("Ⅳ", GearNaming.TierMark(4));
            Assert.AreEqual("Ⅰ", GearNaming.TierMark(0));
            Assert.AreEqual("Ⅳ", GearNaming.TierMark(9));
            Assert.AreEqual("가죽 갑옷", GearNaming.BaseName(GearBaseTable.LeatherArmor));
            Assert.AreEqual("", GearNaming.DisplayName(null));
        }
    }
}
