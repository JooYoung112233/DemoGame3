using System;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Progression;
using Demo6.Core.Stats;
using NUnit.Framework;

namespace Demo6.Tests
{
    /// <summary>능력치 합산기(장비 문서 2-1·2-3·4-4): 시작값, 출처별 몫, % 곱하기, 상한, 전투 시험장 손잡이.</summary>
    public sealed class StatCalcTests
    {
        static readonly StatKind[] Kinds = (StatKind[])Enum.GetValues(typeof(StatKind));
        static readonly StatSource[] Sources = (StatSource[])Enum.GetValues(typeof(StatSource));

        static int Bits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);

        static GearItem Item(string id, Grade grade = Grade.Common, int itemLevel = 1, int enhance = 0, GearOption[] options = null,
            string legendaryId = null, int legendaryRoll = 0) =>
            new GearItem(id, grade, itemLevel, 1000, enhance, options, legendaryId, legendaryRoll);

        static GearOption Opt(OptionKind kind, int value) => new GearOption(kind, value, 1);

        static Loadout With(Loadout l, GearSlot slot, GearItem item)
        {
            Assert.IsTrue(l.TryEquip(slot, item, out _), slot + "에 끼기");
            return l;
        }

        static void AssertSharesAddUp(StatSheet s, string label)
        {
            foreach (var kind in Kinds)
            {
                int flat = 0, percent = 0;
                foreach (var src in Sources)
                {
                    flat += s.Flat(kind, src);
                    percent += s.Percent(kind, src);
                }
                Assert.AreEqual(s.FlatSum(kind), flat, label + " " + kind + " 고정 몫 합");
                Assert.AreEqual(s.PercentSum(kind), percent, label + " " + kind + " % 몫 합");
                bool hasPercent = kind == StatKind.MaxHp || kind == StatKind.Attack;
                if (!hasPercent) Assert.AreEqual(0, percent, label + " " + kind + "는 % 몫이 없다");
                long expected = hasPercent ? (long)Math.Floor(flat * (1000.0 + percent) / 1000.0 + 0.5) : flat;
                Assert.AreEqual(expected, s.Uncapped(kind), label + " " + kind + " 몫 → Uncapped");
            }
        }

        [Test]
        public void StartingMatchesM0bNumbers()
        {
            var s = StatCalc.Starting();
            Assert.AreEqual(2400, s.MaxHp);
            Assert.AreEqual(200, s.Attack);
            Assert.AreEqual(120, s.Defense);
            Assert.AreEqual(70, s.CritChancePermille);
            Assert.AreEqual(1600, s.CritDamagePermille);
            Assert.AreEqual(0, s.AttackSpeedPermille);
            Assert.AreEqual(60, s.MoveSpeedPermille);
            Assert.AreEqual(0, s.CooldownReductionPermille);
            Assert.AreEqual(0, s.SkillDamagePermille);
            Assert.AreEqual(0, s.BossDamagePermille);
            Assert.AreEqual(0, s.LifeStealPermille);
            Assert.AreEqual(0, s.HpRegen);
            Assert.AreEqual(0, s.OnKillHeal);
            Assert.AreEqual(GearBaseTable.Longsword, s.WeaponId);
            Assert.AreSame(WeaponPresets.Longsword, s.WeaponRule);
            foreach (LegendaryEffect e in Enum.GetValues(typeof(LegendaryEffect)))
            {
                Assert.AreEqual(-1, s.LegendaryRollPermille(e));
                Assert.IsFalse(s.HasLegendary(e));
            }
            // 예전 LevelHp 식(ProgressionTests)과 같은 시작 체력: 2,000 + 장비 체력 400.
            Assert.AreEqual(LevelHp.MaxHp(1, 0), s.MaxHp);
            Assert.AreEqual(LevelHp.MaxHp(5, 2, StatBase.BareHp + 400), StatCalc.Compute(Loadout.Starting(), 5, 2).MaxHp);
        }

        [Test]
        public void StartingGameUnitsAreBitIdenticalToOldConstants()
        {
            var s = StatCalc.Starting();
            // 예전 PlayerController 상수: BaseMoveSpeed 5.0f, ArmorMoveBonus 0.06f, EquippedWalkSpeed = BaseMoveSpeed * (1f + ArmorMoveBonus).
            const float BaseMoveSpeed = 5.0f;
            const float ArmorMoveBonus = 0.06f;
            const float EquippedWalkSpeed = BaseMoveSpeed * (1f + ArmorMoveBonus);
            Assert.AreEqual(Bits(EquippedWalkSpeed), Bits(s.MoveSpeed));
            float bonus = ArmorMoveBonus;
            // 상수 접기와 같은 float 반올림 경계를 둔다. 실행기의 중간 정밀도에 따른 1 ULP 차이는 비교하지 않는다.
            Assert.AreEqual(Bits(5.0f * (float)(1f + bonus)), Bits(s.MoveSpeed));
            Assert.AreEqual(5.3f, s.MoveSpeed, 1e-6f);
            Assert.AreEqual(Bits(1f), Bits(s.AttackSpeedFactor));
            Assert.AreEqual(Bits(1f), Bits(s.CooldownFactor));
            Assert.AreEqual(Bits(0.07f), Bits(s.CritChance));
            Assert.AreEqual(Bits(1.6f), Bits(s.CritDamage));
        }

        [Test]
        public void StartingSharesBySource()
        {
            var s = StatCalc.Starting();
            Assert.AreEqual(2000, s.Flat(StatKind.MaxHp, StatSource.Bare));
            Assert.AreEqual(0, s.Flat(StatKind.MaxHp, StatSource.Level));
            Assert.AreEqual(200, s.Flat(StatKind.MaxHp, StatSource.Armor));
            Assert.AreEqual(80, s.Flat(StatKind.MaxHp, StatSource.Helm));
            Assert.AreEqual(60, s.Flat(StatKind.MaxHp, StatSource.Gloves));
            Assert.AreEqual(60, s.Flat(StatKind.MaxHp, StatSource.Boots));
            Assert.AreEqual(60, s.Flat(StatKind.Defense, StatSource.Armor));
            Assert.AreEqual(24, s.Flat(StatKind.Defense, StatSource.Helm));
            Assert.AreEqual(18, s.Flat(StatKind.Defense, StatSource.Gloves));
            Assert.AreEqual(18, s.Flat(StatKind.Defense, StatSource.Boots));
            Assert.AreEqual(100, s.Flat(StatKind.Attack, StatSource.Bare));
            Assert.AreEqual(100, s.Flat(StatKind.Attack, StatSource.Weapon));
            Assert.AreEqual(50, s.Flat(StatKind.CritChance, StatSource.Bare));
            Assert.AreEqual(20, s.Flat(StatKind.CritChance, StatSource.Weapon));
            Assert.AreEqual(1500, s.Flat(StatKind.CritDamage, StatSource.Bare));
            Assert.AreEqual(100, s.Flat(StatKind.CritDamage, StatSource.Weapon));
            Assert.AreEqual(30, s.Flat(StatKind.MoveSpeed, StatSource.Armor));
            Assert.AreEqual(30, s.Flat(StatKind.MoveSpeed, StatSource.Boots));
            foreach (var kind in Kinds)
            {
                Assert.AreEqual(0, s.Flat(kind, StatSource.Ring1), kind + " 반지 1 빈 자리");
                Assert.AreEqual(0, s.Flat(kind, StatSource.Ring2), kind + " 반지 2 빈 자리");
                Assert.AreEqual(0, s.Flat(kind, StatSource.Amulet), kind + " 목걸이 빈 자리");
                Assert.AreEqual(0, s.Flat(kind, StatSource.Test), kind + " 손잡이 없음");
            }
            Assert.AreEqual(StatSource.Weapon, StatSources.Of(GearSlot.Weapon));
            Assert.AreEqual(StatSource.Ring2, StatSources.Of(GearSlot.Ring2));
            Assert.AreEqual(StatSource.Amulet, StatSources.Of(GearSlot.Amulet));
            Assert.AreEqual(Sources.Length, StatSources.Count);
            Assert.AreEqual(Kinds.Length, StatSheet.KindCount);
            AssertSharesAddUp(s, "시작");
        }

        [Test]
        public void PercentMultipliesFlatSumIncludingLevelHpAndRoundsHalfUpOnce()
        {
            // 체력: (2,000 + 레벨 체력 (120 + 15 × 2) × 2 + 고급 가죽 갑옷 250 + 두건 80 + 장갑 60 + 장화 60) × (1 + 46‰) = 2,750 × 1.046 = 2,876.5 → 2,877.
            var armor = Item(GearBaseTable.LeatherArmor, Grade.Uncommon, options: new[] { Opt(OptionKind.HpPercent, 46) });
            Assert.AreEqual(250, armor.Hp);
            // 공격력: (100 + 고급 한손검과 방패 125 + 고급 쇠 반지 31) × (1 + 60‰ + 30‰) = 256 × 1.09 = 279.04 → 279.
            var sword = Item(GearBaseTable.Longsword, Grade.Uncommon, options: new[] { Opt(OptionKind.AttackPercent, 60) });
            var ring = Item(GearBaseTable.IronRing, Grade.Uncommon, options: new[] { Opt(OptionKind.AttackPercent, 30) });
            Assert.AreEqual(125, sword.Attack);
            Assert.AreEqual(31, ring.Attack);
            var l = With(With(With(Loadout.Starting(), GearSlot.Armor, armor), GearSlot.Weapon, sword), GearSlot.Ring1, ring);

            var s = StatCalc.Compute(l, 3, 2);
            Assert.AreEqual(300, s.Flat(StatKind.MaxHp, StatSource.Level));
            Assert.AreEqual(2750, s.FlatSum(StatKind.MaxHp));
            Assert.AreEqual(46, s.Percent(StatKind.MaxHp, StatSource.Armor));
            Assert.AreEqual(2877, s.MaxHp);
            Assert.AreEqual(256, s.FlatSum(StatKind.Attack));
            Assert.AreEqual(60, s.Percent(StatKind.Attack, StatSource.Weapon));
            Assert.AreEqual(30, s.Percent(StatKind.Attack, StatSource.Ring1));
            Assert.AreEqual(279, s.Attack);
            // 방어는 % 없이 합만: 고급 가죽 갑옷 75 + 24 + 18 + 18.
            Assert.AreEqual(135, s.Defense);
            AssertSharesAddUp(s, "% 곱하기");
        }

        [Test]
        public void LevelHpUsesToughBodyAndClampsLevel()
        {
            var l = Loadout.Starting();
            Assert.AreEqual(2400 + 180 * 4, StatCalc.Compute(l, 5, 4).MaxHp);
            Assert.AreEqual(720, StatCalc.Compute(l, 5, 4).Flat(StatKind.MaxHp, StatSource.Level));
            Assert.AreEqual(2400 + 120 * 2, StatCalc.Compute(l, 3, 0).MaxHp);
            Assert.AreEqual(2400, StatCalc.Compute(l, 0, 4).MaxHp);
            Assert.AreEqual(2400 + LevelHp.PerLevelWith(4) * (LevelTable.MaxLevel - 1), StatCalc.Compute(l, 99, 4).MaxHp);
        }

        [Test]
        public void GreatswordEndBuildReachesCritDamageCapExactly()
        {
            // 7장: 150 + 대검 50 + 무기 옵션 30 + 목걸이 옵션 30 + 송곳 반지 15 × 2 + 판금 장갑 10 = 300%.
            var l = Loadout.Starting();
            With(l, GearSlot.Weapon, Item(GearBaseTable.Greatsword, Grade.Legendary, options: new[] { Opt(OptionKind.CritDamage, 300) }));
            With(l, GearSlot.Amulet, Item(GearBaseTable.FangAmulet, Grade.Legendary, options: new[] { Opt(OptionKind.CritDamage, 300) }));
            With(l, GearSlot.Ring1, Item(GearBaseTable.FangRing));
            With(l, GearSlot.Ring2, Item(GearBaseTable.FangRing));
            With(l, GearSlot.Gloves, Item(GearBaseTable.PlateGloves));
            var s = StatCalc.Compute(l, 1, 0);
            Assert.AreEqual(3000, s.Uncapped(StatKind.CritDamage));
            Assert.AreEqual(3000, s.CritDamagePermille);
            Assert.AreEqual(500 + 300, s.Flat(StatKind.CritDamage, StatSource.Weapon));
            Assert.AreEqual(150, s.Flat(StatKind.CritDamage, StatSource.Ring1));
            Assert.AreEqual(150, s.Flat(StatKind.CritDamage, StatSource.Ring2));
            Assert.AreEqual(100, s.Flat(StatKind.CritDamage, StatSource.Gloves));
            Assert.AreEqual(300, s.Flat(StatKind.CritDamage, StatSource.Amulet));
            AssertSharesAddUp(s, "대검 끝 빌드");

            // 하나 더 얹으면 상한에서 멈추고 Uncapped에는 남는다.
            With(l, GearSlot.Helm, Item(GearBaseTable.LeatherHelm, options: new[] { Opt(OptionKind.CritDamage, 150) }));
            s = StatCalc.Compute(l, 1, 0);
            Assert.AreEqual(3150, s.Uncapped(StatKind.CritDamage));
            Assert.AreEqual(StatCaps.CritDamagePermille, s.CritDamagePermille);
            AssertSharesAddUp(s, "상한 넘김");
        }

        [Test]
        public void CapsClampEveryCappedStat()
        {
            var l = Loadout.Starting();
            // 범위 밖 값을 일부러 넣은 시험용 장비(실제 풀에서는 나오지 않음).
            With(l, GearSlot.Weapon, Item(GearBaseTable.Twinblades, Grade.Legendary,
                options: new[] { Opt(OptionKind.CritChance, 600), Opt(OptionKind.AttackSpeed, 400), Opt(OptionKind.LifeSteal, 40) }));
            With(l, GearSlot.Boots, Item(GearBaseTable.LeatherBoots, Grade.Legendary, options: new[] { Opt(OptionKind.MoveSpeed, 400) }));
            With(l, GearSlot.Helm, Item(GearBaseTable.ChainHelm, Grade.Legendary, options: new[] { Opt(OptionKind.CooldownReduction, 500) }));
            With(l, GearSlot.Armor, Item(GearBaseTable.PlateArmor, Grade.Legendary, 11, 15, options: new[] { Opt(OptionKind.DefenseFlat, 3000) }));
            var s = StatCalc.Compute(l, 1, 0);
            Assert.AreEqual(StatCaps.CritChancePermille, s.CritChancePermille);
            Assert.AreEqual(50 + 40 + 600, s.Uncapped(StatKind.CritChance));
            Assert.AreEqual(StatCaps.AttackSpeedPermille, s.AttackSpeedPermille);
            Assert.AreEqual(400, s.Uncapped(StatKind.AttackSpeed));
            Assert.AreEqual(StatCaps.LifeStealPermille, s.LifeStealPermille);
            Assert.AreEqual(StatCaps.MoveSpeedPermille, s.MoveSpeedPermille);
            Assert.AreEqual(-30 + 30 + 400, s.Uncapped(StatKind.MoveSpeed));
            Assert.AreEqual(StatCaps.CooldownReductionPermille, s.CooldownReductionPermille);
            Assert.AreEqual(530, s.Uncapped(StatKind.CooldownReduction));
            Assert.That(s.Uncapped(StatKind.Defense), Is.GreaterThan(StatCaps.Defense));
            Assert.AreEqual(StatCaps.Defense, s.Defense);
            AssertSharesAddUp(s, "상한");

            // 쌍검 고유 −200‰ 치명 피해는 1,300‰. 아래끝 1,000‰은 손잡이로 확인한다.
            Assert.AreEqual(1300, s.CritDamagePermille);
            var low = StatCalc.Compute(l, 1, 0, new StatOverrides { CritDamagePermille = 800 });
            Assert.AreEqual(800, low.Uncapped(StatKind.CritDamage));
            Assert.AreEqual(StatCaps.CritDamageMinPermille, low.CritDamagePermille);
        }

        [Test]
        public void HeavySetSlowsAndMixingHalvesThePenalty()
        {
            var l = Loadout.Starting();
            With(l, GearSlot.Armor, Item(GearBaseTable.PlateArmor));
            With(l, GearSlot.Boots, Item(GearBaseTable.PlateBoots));
            var s = StatCalc.Compute(l, 1, 0);
            Assert.AreEqual(-60, s.MoveSpeedPermille);
            Assert.AreEqual(4.7f, s.MoveSpeed, 1e-5f);
            Assert.AreEqual(120 + 24 + 18 + 36, s.Defense);
            // 판금 갑옷 + 가죽 장화면 무게 벌칙이 반(−30 + 30 = 0).
            With(l, GearSlot.Boots, Item(GearBaseTable.LeatherBoots));
            Assert.AreEqual(0, StatCalc.Compute(l, 1, 0).MoveSpeedPermille);
        }

        [Test]
        public void PermilleKnobsGoIntoTestShare()
        {
            var o = new StatOverrides { AttackSpeedPermille = 150, CritChancePermille = 200, CritDamagePermille = 2500 };
            var s = StatCalc.Compute(Loadout.Starting(), 1, 0, o);
            Assert.AreEqual(150, s.AttackSpeedPermille);
            Assert.AreEqual(200, s.CritChancePermille);
            Assert.AreEqual(2500, s.CritDamagePermille);
            Assert.AreEqual(150, s.Flat(StatKind.AttackSpeed, StatSource.Test));
            Assert.AreEqual(200 - 70, s.Flat(StatKind.CritChance, StatSource.Test));
            Assert.AreEqual(2500 - 1600, s.Flat(StatKind.CritDamage, StatSource.Test));
            // 장비 몫은 그대로 보인다.
            Assert.AreEqual(20, s.Flat(StatKind.CritChance, StatSource.Weapon));
            Assert.AreEqual(100, s.Flat(StatKind.CritDamage, StatSource.Weapon));
            AssertSharesAddUp(s, "‰ 손잡이");

            // 손잡이도 상한을 받는다(시험 범위 400‰ → 300‰).
            var fast = StatCalc.Compute(Loadout.Starting(), 1, 0, new StatOverrides { AttackSpeedPermille = 400, CritChancePermille = 900 });
            Assert.AreEqual(400, fast.Uncapped(StatKind.AttackSpeed));
            Assert.AreEqual(300, fast.AttackSpeedPermille);
            Assert.AreEqual(500, fast.CritChancePermille);
            AssertSharesAddUp(fast, "손잡이 상한");
        }

        [Test]
        public void FinalOverridesReplaceAttackHpDefenseAndKeepSharesConsistent()
        {
            var l = Loadout.Starting();
            With(l, GearSlot.Weapon, Item(GearBaseTable.Longsword, Grade.Rare, 8, options: new[] { Opt(OptionKind.AttackPercent, 62) }));
            With(l, GearSlot.Armor, Item(GearBaseTable.ChainArmor, Grade.Rare, 8, options: new[] { Opt(OptionKind.HpPercent, 70) }));
            var o = new StatOverrides { Attack = 1462, MaxHp = 7076, Defense = 1600 };
            var s = StatCalc.Compute(l, 7, 2, o);
            Assert.AreEqual(1462, s.Attack);
            Assert.AreEqual(7076, s.MaxHp);
            Assert.AreEqual(1600, s.Defense);
            Assert.AreEqual(1462, s.Uncapped(StatKind.Attack));
            Assert.AreEqual(0, s.PercentSum(StatKind.Attack));
            Assert.AreEqual(-62, s.Percent(StatKind.Attack, StatSource.Test));
            Assert.AreEqual(62, s.Percent(StatKind.Attack, StatSource.Weapon));
            AssertSharesAddUp(s, "최종값 덮기");

            // 범위 밖 덮기는 최종값에서만 자른다.
            var clamped = StatCalc.Compute(l, 1, 0, new StatOverrides { Attack = 0, MaxHp = -5, Defense = 9000 });
            Assert.AreEqual(1, clamped.Attack);
            Assert.AreEqual(1, clamped.MaxHp);
            Assert.AreEqual(StatCaps.Defense, clamped.Defense);
            Assert.AreEqual(9000, clamped.Uncapped(StatKind.Defense));
            AssertSharesAddUp(clamped, "덮기 자르기");
        }

        [Test]
        public void WeaponIntrinsicOffRemovesOnlyWeaponIntrinsic()
        {
            var l = Loadout.Starting();
            With(l, GearSlot.Ring1, Item(GearBaseTable.BloodRing));
            With(l, GearSlot.Gloves, Item(GearBaseTable.PlateGloves));
            var on = StatCalc.Compute(l, 1, 0);
            Assert.AreEqual(50 + 20 + 20, on.CritChancePermille);
            Assert.AreEqual(1500 + 100 + 100, on.CritDamagePermille);

            var off = StatCalc.Compute(l, 1, 0, new StatOverrides { WeaponIntrinsic = false });
            Assert.AreEqual(50 + 20, off.CritChancePermille);
            Assert.AreEqual(1500 + 100, off.CritDamagePermille);
            Assert.AreEqual(0, off.Flat(StatKind.CritChance, StatSource.Weapon));
            Assert.AreEqual(20, off.Flat(StatKind.CritChance, StatSource.Ring1));
            Assert.AreEqual(100, off.Flat(StatKind.CritDamage, StatSource.Gloves));
            // 무기 공격력·옵션은 고유가 아니라 남는다.
            Assert.AreEqual(on.Attack, off.Attack);
            AssertSharesAddUp(off, "무기 고유 끔");

            // 갑옷·장화의 이동 고유도 남는다.
            var bare = StatCalc.Compute(Loadout.Starting(), 1, 0, new StatOverrides { WeaponIntrinsic = false });
            Assert.AreEqual(50, bare.CritChancePermille);
            Assert.AreEqual(1500, bare.CritDamagePermille);
            Assert.AreEqual(60, bare.MoveSpeedPermille);
        }

        [Test]
        public void TwoBloodRingsAddFortyCrit()
        {
            var l = Loadout.Starting();
            With(l, GearSlot.Ring1, Item(GearBaseTable.BloodRing));
            With(l, GearSlot.Ring2, Item(GearBaseTable.BloodRing));
            var s = StatCalc.Compute(l, 1, 0);
            Assert.AreEqual(70 + 40, s.CritChancePermille);
            Assert.AreEqual(20, s.Flat(StatKind.CritChance, StatSource.Ring1));
            Assert.AreEqual(20, s.Flat(StatKind.CritChance, StatSource.Ring2));
            Assert.AreEqual(200 + 20 + 20, s.Attack);
        }

        [Test]
        public void SmallSlotIntrinsicsAndOptionsReachTheirStats()
        {
            var l = Loadout.Starting();
            With(l, GearSlot.Helm, Item(GearBaseTable.PlateHelm, Grade.Uncommon, options: new[] { Opt(OptionKind.SkillDamage, 40) }));
            With(l, GearSlot.Gloves, Item(GearBaseTable.ChainGloves, Grade.Uncommon, options: new[] { Opt(OptionKind.LifeSteal, 3) }));
            With(l, GearSlot.Boots, Item(GearBaseTable.ChainBoots, Grade.Uncommon, options: new[] { Opt(OptionKind.HpRegen, 9) }));
            With(l, GearSlot.Armor, Item(GearBaseTable.ChainArmor, Grade.Uncommon, options: new[] { Opt(OptionKind.OnKillHeal, 15) }));
            With(l, GearSlot.Amulet, Item(GearBaseTable.AmberAmulet, Grade.Uncommon, options: new[] { Opt(OptionKind.CooldownReduction, 40) }));
            var s = StatCalc.Compute(l, 1, 0);
            Assert.AreEqual(50, s.BossDamagePermille);
            Assert.AreEqual(40 + 100, s.SkillDamagePermille);
            Assert.AreEqual(30, s.AttackSpeedPermille);
            Assert.AreEqual(3, s.LifeStealPermille);
            Assert.AreEqual(9, s.HpRegen);
            Assert.AreEqual(15, s.OnKillHeal);
            Assert.AreEqual(40, s.CooldownReductionPermille);
            Assert.AreEqual(0, s.MoveSpeedPermille);
            Assert.AreEqual(1.03f, s.AttackSpeedFactor, 1e-6f);
            Assert.AreEqual(0.96f, s.CooldownFactor, 1e-6f);
            AssertSharesAddUp(s, "작은 칸");
        }

        [Test]
        public void LegendaryRollsFollowGearAndOverrides()
        {
            var l = Loadout.Starting();
            With(l, GearSlot.Weapon, Item(GearBaseTable.Longsword, Grade.Legendary, legendaryId: LegendaryTable.ChainLightningId, legendaryRoll: 700));
            With(l, GearSlot.Boots, Item(GearBaseTable.LeatherBoots, Grade.Legendary, legendaryId: LegendaryTable.FlameStepsId, legendaryRoll: 300));
            var s = StatCalc.Compute(l, 1, 0);
            Assert.AreEqual(700, s.LegendaryRollPermille(LegendaryEffect.ChainLightning));
            Assert.IsTrue(s.HasLegendary(LegendaryEffect.ChainLightning));
            Assert.AreEqual(300, s.LegendaryRollPermille(LegendaryEffect.FlameSteps));
            Assert.AreEqual(-1, s.LegendaryRollPermille(LegendaryEffect.ChainBlast));
            Assert.IsFalse(s.HasLegendary(LegendaryEffect.ChainBlast));

            var o = new StatOverrides { LegendaryRollPermille = new[] { -1, 0, 1500 } };
            var t = StatCalc.Compute(l, 1, 0, o);
            Assert.AreEqual(-1, t.LegendaryRollPermille(LegendaryEffect.ChainLightning));
            Assert.IsFalse(t.HasLegendary(LegendaryEffect.ChainLightning));
            Assert.AreEqual(0, t.LegendaryRollPermille(LegendaryEffect.FlameSteps));
            Assert.IsTrue(t.HasLegendary(LegendaryEffect.FlameSteps));
            Assert.AreEqual(1000, t.LegendaryRollPermille(LegendaryEffect.ChainBlast));

            var minus = StatCalc.Compute(l, 1, 0, new StatOverrides { LegendaryRollPermille = new[] { -40 } });
            Assert.AreEqual(-1, minus.LegendaryRollPermille(LegendaryEffect.ChainLightning));
            Assert.AreEqual(300, minus.LegendaryRollPermille(LegendaryEffect.FlameSteps), "짧은 배열 밖 칸은 장비대로");
        }

        [Test]
        public void EmptyOverridesEqualNoOverrides()
        {
            var l = Loadout.Starting();
            With(l, GearSlot.Ring2, Item(GearBaseTable.FangRing, Grade.Rare, 5, options: new[] { Opt(OptionKind.AttackPercent, 20) }));
            var a = StatCalc.Compute(l, 4, 1);
            var b = StatCalc.Compute(l, 4, 1, new StatOverrides());
            foreach (var kind in Kinds)
            {
                Assert.AreEqual(a.Get(kind), b.Get(kind), kind.ToString());
                Assert.AreEqual(a.Uncapped(kind), b.Uncapped(kind), kind.ToString());
                foreach (var src in Sources)
                {
                    Assert.AreEqual(a.Flat(kind, src), b.Flat(kind, src), kind + " " + src);
                    Assert.AreEqual(a.Percent(kind, src), b.Percent(kind, src), kind + " " + src);
                }
            }
        }

        [Test]
        public void NullLoadoutIsBareBody()
        {
            var s = StatCalc.Compute(null, 1, 0);
            Assert.AreEqual(StatBase.BareHp, s.MaxHp);
            Assert.AreEqual(StatBase.BareAttack, s.Attack);
            Assert.AreEqual(0, s.Defense);
            Assert.AreEqual(StatBase.CritChancePermille, s.CritChancePermille);
            Assert.AreEqual(StatBase.CritDamagePermille, s.CritDamagePermille);
            Assert.AreEqual(Bits(StatBase.MoveSpeed), Bits(s.MoveSpeed));
            Assert.AreEqual(GearBaseTable.Longsword, s.WeaponId);
        }

        [Test]
        public void SwingsPerSecondAndExpectedCritMultiplier()
        {
            Assert.AreEqual(1.42, StatCalc.SwingsPerSecond(WeaponPresets.Longsword, 0), 0.005);
            Assert.AreEqual(WeaponPresets.Twinblades.SwingsPerSecond * 1.3, StatCalc.SwingsPerSecond(WeaponPresets.Twinblades, 300), 1e-5);
            Assert.AreEqual(StatCalc.SwingsPerSecond(WeaponPresets.Longsword, 0), StatCalc.SwingsPerSecond(StatCalc.Starting()), 1e-12);
            Assert.AreEqual(0.0, StatCalc.SwingsPerSecond(null, 0));
            Assert.AreEqual(1.025, StatCalc.ExpectedCritMultiplier(50, 1500), 1e-12);
            Assert.AreEqual(1.042, StatCalc.ExpectedCritMultiplier(StatCalc.Starting()), 1e-12);
            Assert.AreEqual(1.0, StatCalc.ExpectedCritMultiplier(null), 0);
        }
    }
}
