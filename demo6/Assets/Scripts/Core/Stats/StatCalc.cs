using System;
using Demo6.Core.Combat;
using Demo6.Core.Loot;
using Demo6.Core.Progression;

namespace Demo6.Core.Stats
{
    /// <summary>
    /// 전투 시험장 손잡이(장비 문서 3-4 '전투 시험장 손잡이'). null 칸은 장비대로.
    /// ‰ 손잡이(공격 속도·치명 확률·치명 피해)는 상한 전 합을 그 값으로 바꾼다. 차이는 출처 Test 몫으로 보이고, 상한은 그대로 받는다
    /// (예: 공격 속도 400이면 Uncapped 400, 최종 300).
    /// Attack·MaxHp·Defense는 '층 기준'(FloorScaling.Baseline) 시험에서 최종값을 그대로 덮는다. 이때도 차이를 Test 몫에 넣어
    /// (체력·공격력은 % 합도 Test 몫으로 0을 맞춤) 출처별 몫의 합이 Uncapped와 맞는다.
    /// </summary>
    public sealed class StatOverrides
    {
        /// <summary>무기 종류 고유(치명 확률·피해) 켜기. 끄면 무기 고유만 빠진다(반지·장갑 등 다른 칸 고유는 남음). 맨몸 치명(50‰/1,500‰)과 손맛을 비교한다.</summary>
        public bool WeaponIntrinsic = true;
        public int? AttackSpeedPermille;
        public int? CritChancePermille;
        public int? CritDamagePermille;
        /// <summary>최종 공격력·최대 체력·방어 덮어쓰기(전투 시험장 층 기준). 공격력·체력은 1 이상, 방어는 0~4,000으로만 자른다.</summary>
        public int? Attack;
        public int? MaxHp;
        public int? Defense;
        /// <summary>전설 효과 세기 덮어쓰기(효과 차례 칸, 길이 3). 칸 값 0~1000이면 그 세기로 켬(1000 넘으면 1000), −1 이하면 끔. 배열이 null이면 장비대로.</summary>
        public int[] LegendaryRollPermille;
    }

    /// <summary>
    /// 능력치 합산기(장비 문서 2-1·2-3). 장착 8자리 + 레벨 + 질긴 몸 랭크를 받아 StatSheet를 낸다.
    /// 순서: 고정값을 모두 더함 → (1 + 같은 종류 % 합)을 곱함(체력·공격력만) → 상한. 반올림은 % 곱하기 뒤 한 번(0.5 올림).
    /// 최대 체력 = (2,000 + 레벨 체력 + 장비 체력 + 체력+ 옵션) × (1 + 체력% 합). 레벨 체력 = LevelHp.PerLevelWith(질긴 몸) × (Lv − 1).
    /// 문서 식대로 체력%는 레벨 체력에도 곱해진다(위험 10: 3차 4-4 분모와의 차이는 밸런스 계산이 잰다).
    /// 공격력 = (100 + 무기·반지 공격력 + 공격력+ 옵션) × (1 + 공격력% 합). 방어 = 방어구 방어 + 방어+ 옵션(층 기준표 방어는 넣지 않음).
    /// 그 밖의 ‰ 능력치(치명·공속·이동·재사용·스킬·보스·흡수)와 재생·처치 회복은 맨몸 + 종류 고유 + 옵션의 합이다.
    /// 상한(StatCaps): 방어 0~4,000, 치명 0~500‰, 치명 피해 1,000~3,000‰, 공속 0~300‰, 이동 −300~+300‰, 재사용 0~400‰, 흡수 0~30‰.
    /// 시작 장비(장검 + 가죽 한 벌, Lv1)면 체력 2,400 · 공격 200 · 방어 120 · 치명 70‰/1,600‰ · 공속 0 · 이동 +60‰(5.3).
    /// </summary>
    public static class StatCalc
    {
        /// <summary>시작 상태 능력치(장검 + 가죽 한 벌, Lv1, 질긴 몸 0).</summary>
        public static StatSheet Starting() => Compute(Loadout.Starting(), 1, 0);

        public static StatSheet Compute(Loadout loadout, int level, int toughRank, StatOverrides overrides = null)
        {
            var s = new StatSheet();
            s.AddFlat(StatKind.MaxHp, StatSource.Bare, StatBase.BareHp);
            s.AddFlat(StatKind.Attack, StatSource.Bare, StatBase.BareAttack);
            s.AddFlat(StatKind.CritChance, StatSource.Bare, StatBase.CritChancePermille);
            s.AddFlat(StatKind.CritDamage, StatSource.Bare, StatBase.CritDamagePermille);
            int lv = Math.Max(1, Math.Min(LevelTable.MaxLevel, level));
            s.AddFlat(StatKind.MaxHp, StatSource.Level, LevelHp.PerLevelWith(toughRank) * (lv - 1));

            if (loadout != null)
            {
                foreach (var slot in GearSlots.All)
                {
                    var item = loadout[slot];
                    if (item == null) continue;
                    var src = StatSources.Of(slot);
                    s.AddFlat(StatKind.Attack, src, item.Attack);
                    s.AddFlat(StatKind.Defense, src, item.Defense);
                    s.AddFlat(StatKind.MaxHp, src, item.Hp);
                    bool intrinsic = slot != GearSlot.Weapon || overrides == null || overrides.WeaponIntrinsic;
                    if (intrinsic)
                        foreach (var b in item.Base.Intrinsic) Add(s, b.Kind, b.Value, src);
                    foreach (var o in item.Options) Add(s, o.Kind, o.Value, src);
                }
                s.WeaponId = loadout.Weapon != null ? loadout.Weapon.BaseId : GearBaseTable.Longsword;
                s.SetLegendary(loadout.ActiveLegendaries());
            }

            if (overrides != null)
            {
                Override(s, StatKind.AttackSpeed, overrides.AttackSpeedPermille);
                Override(s, StatKind.CritChance, overrides.CritChancePermille);
                Override(s, StatKind.CritDamage, overrides.CritDamagePermille);
                OverrideTotal(s, StatKind.Attack, overrides.Attack);
                OverrideTotal(s, StatKind.MaxHp, overrides.MaxHp);
                OverrideTotal(s, StatKind.Defense, overrides.Defense);
                if (overrides.LegendaryRollPermille != null)
                    for (int i = 0; i < LegendaryTable.Count && i < overrides.LegendaryRollPermille.Length; i++)
                        s.SetLegendary((LegendaryEffect)i, Math.Max(-1, Math.Min(1000, overrides.LegendaryRollPermille[i])));
            }

            Finish(s, StatKind.MaxHp, true, 1, int.MaxValue);
            Finish(s, StatKind.Attack, true, 1, int.MaxValue);
            Finish(s, StatKind.Defense, false, 0, StatCaps.Defense);
            Finish(s, StatKind.CritChance, false, 0, StatCaps.CritChancePermille);
            Finish(s, StatKind.CritDamage, false, StatCaps.CritDamageMinPermille, StatCaps.CritDamagePermille);
            Finish(s, StatKind.AttackSpeed, false, 0, StatCaps.AttackSpeedPermille);
            Finish(s, StatKind.MoveSpeed, false, -StatCaps.MoveSpeedPermille, StatCaps.MoveSpeedPermille);
            Finish(s, StatKind.CooldownReduction, false, 0, StatCaps.CooldownReductionPermille);
            Finish(s, StatKind.SkillDamage, false, 0, int.MaxValue);
            Finish(s, StatKind.BossDamage, false, 0, int.MaxValue);
            Finish(s, StatKind.LifeSteal, false, 0, StatCaps.LifeStealPermille);
            Finish(s, StatKind.HpRegen, false, 0, int.MaxValue);
            Finish(s, StatKind.OnKillHeal, false, 0, int.MaxValue);
            return s;
        }

        /// <summary>옵션·고유 한 줄을 능력치 몫으로 옮긴다. 체력%·공격력%만 % 몫이고 나머지는 고정값·‰ 합이다.</summary>
        static void Add(StatSheet s, OptionKind kind, int value, StatSource src)
        {
            switch (kind)
            {
                case OptionKind.AttackFlat: s.AddFlat(StatKind.Attack, src, value); break;
                case OptionKind.AttackPercent: s.AddPercent(StatKind.Attack, src, value); break;
                case OptionKind.CritChance: s.AddFlat(StatKind.CritChance, src, value); break;
                case OptionKind.CritDamage: s.AddFlat(StatKind.CritDamage, src, value); break;
                case OptionKind.AttackSpeed: s.AddFlat(StatKind.AttackSpeed, src, value); break;
                case OptionKind.BossDamage: s.AddFlat(StatKind.BossDamage, src, value); break;
                case OptionKind.LifeSteal: s.AddFlat(StatKind.LifeSteal, src, value); break;
                case OptionKind.OnKillHeal: s.AddFlat(StatKind.OnKillHeal, src, value); break;
                case OptionKind.HpFlat: s.AddFlat(StatKind.MaxHp, src, value); break;
                case OptionKind.HpPercent: s.AddPercent(StatKind.MaxHp, src, value); break;
                case OptionKind.DefenseFlat: s.AddFlat(StatKind.Defense, src, value); break;
                case OptionKind.CooldownReduction: s.AddFlat(StatKind.CooldownReduction, src, value); break;
                case OptionKind.SkillDamage: s.AddFlat(StatKind.SkillDamage, src, value); break;
                case OptionKind.MoveSpeed: s.AddFlat(StatKind.MoveSpeed, src, value); break;
                case OptionKind.HpRegen: s.AddFlat(StatKind.HpRegen, src, value); break;
            }
        }

        /// <summary>‰ 손잡이: 고정값 합을 그 값으로 맞춘다(차이를 Test 몫에 더함). 상한은 Finish가 그대로 자른다.</summary>
        static void Override(StatSheet s, StatKind kind, int? value)
        {
            if (!value.HasValue) return;
            s.AddFlat(kind, StatSource.Test, value.Value - s.FlatSum(kind));
        }

        /// <summary>최종값 덮어쓰기: 고정값 합을 그 값으로, % 합을 0으로 맞춘다(차이를 Test 몫에 더함). 그래서 Uncapped = 그 값이다.</summary>
        static void OverrideTotal(StatSheet s, StatKind kind, int? value)
        {
            if (!value.HasValue) return;
            s.AddFlat(kind, StatSource.Test, value.Value - s.FlatSum(kind));
            int percent = s.PercentSum(kind);
            if (percent != 0) s.AddPercent(kind, StatSource.Test, -percent);
        }

        static void Finish(StatSheet s, StatKind kind, bool percent, int min, int max)
        {
            long flat = s.FlatSum(kind);
            long value = percent ? RoundHalfUpPermille(flat * (1000L + s.PercentSum(kind))) : flat;
            int uncapped = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value));
            s.SetFinal(kind, uncapped, Math.Max(min, Math.Min(max, uncapped)));
        }

        /// <summary>‰를 곱한 값 ÷ 1000을 한 번 반올림(0.5 올림, 음수도 위쪽으로).</summary>
        static long RoundHalfUpPermille(long timesThousand)
        {
            long q = timesThousand / 1000, r = timesThousand % 1000;
            if (r < 0)
            {
                q -= 1;
                r += 1000;
            }
            return r >= 500 ? q + 1 : q;
        }

        // ── 보이기만 하는 값(장비 문서 3-1) ──

        /// <summary>
        /// 초당 휘두르기 = 콤보 동작 수 ÷ 한 바퀴 시간 × (1 + 공격 속도). 장검 1.40, 대검 0.97, 쌍검 1.54(공속 0), 공속 +24%면 1.73 / 1.20 / 1.91.
        /// 공격 속도는 SwingTiming처럼 0~1,000‰로 자른다(상한 300은 StatCalc.Compute가 이미 자름).
        /// </summary>
        public static double SwingsPerSecond(WeaponAttackRule rule, int attackSpeedPermille)
        {
            if (rule == null || rule.combo == null || rule.combo.Length == 0) return 0;
            return rule.combo.Length / rule.CycleSeconds * SwingTiming.SpeedFactor(attackSpeedPermille);
        }

        /// <summary>시트의 무기·공격 속도로 초당 휘두르기.</summary>
        public static double SwingsPerSecond(StatSheet s) => s == null ? 0 : SwingsPerSecond(s.WeaponRule, s.AttackSpeedPermille);

        /// <summary>기대 치명 배율 = 1 + 확률 × (피해 − 1)(3-2). 맨몸 1.025, 장검 1.042, 대검 1.050, 쌍검 1.027.</summary>
        public static double ExpectedCritMultiplier(StatSheet s) =>
            s == null ? 1 : ExpectedCritMultiplier(s.CritChancePermille, s.CritDamagePermille);

        /// <summary>기대 치명 배율(‰ 입력).</summary>
        public static double ExpectedCritMultiplier(int critChancePermille, int critDamagePermille) =>
            1 + critChancePermille / 1000.0 * (critDamagePermille / 1000.0 - 1);
    }
}
