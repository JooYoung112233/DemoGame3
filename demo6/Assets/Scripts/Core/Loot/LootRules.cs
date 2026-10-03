using System;
using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Random;

namespace Demo6.Core.Loot
{
    /// <summary>처치 보상 출처(3차 초안 4-6 처치 보상 표의 줄).</summary>
    public enum KillSource
    {
        /// <summary>굴쥐 졸개(둥지 굴쥐는 보상 없음).</summary>
        Rat,
        /// <summary>가시 궁수.</summary>
        Archer,
        /// <summary>뿔멧돼지.</summary>
        Boar,
        /// <summary>둥지 정리(무리 정리 알림 nest=true).</summary>
        NestClear,
        /// <summary>정예(이름난 정예는 M0b에서 뺀다).</summary>
        Elite,
    }

    /// <summary>
    /// 한 번에 떨어지는 보상 묶음. 결과는 Core가 먼저 확정하고, 화면 연출은 이것을 보여 주기만 한다(2차 7-6).
    /// </summary>
    public sealed class LootBundle
    {
        /// <summary>장비(7부위 공통, LootRules.RollGear가 굴림).</summary>
        public readonly List<GearItem> Gear = new List<GearItem>();
        /// <summary>강화석 개수.</summary>
        public int Stones;
        /// <summary>골드 무더기마다 값.</summary>
        public readonly List<int> GoldPiles = new List<int>();
        /// <summary>나무 궤짝 10% '쥐 궤짝'(굴쥐 3). 보상은 그대로 준다.</summary>
        public bool RatChest;

        public int Gold
        {
            get
            {
                int sum = 0;
                foreach (int g in GoldPiles) sum += g;
                return sum;
            }
        }

        public bool Empty => Gear.Count == 0 && Stones <= 0 && GoldPiles.Count == 0;
    }

    /// <summary>
    /// 장비 굴림 조건(장비 문서 8-1·8-2·6장). 비워 두면 보통 규칙. 화면 쪽(Inventory·Chest)이 장착 상태를 보고 만든다
    /// (LootRules.PartScores·WeakestParts·EmptyParts). 필드 이름·뜻은 바꾸지 않는다.
    /// </summary>
    public sealed class GearRollContext
    {
        /// <summary>부위 보정 대상(보통 부위 점수가 가장 낮은 2부위, LootRules.WeakestParts). 희귀 이상이 나오면 이 부위 가중치 ×3.</summary>
        public GearPart[] BoostedParts;
        /// <summary>
        /// 부위를 이 가운데서만 고른다(부위 비율대로). 빈 자리 채우기(1~4층 첫 쇠 궤짝의 빈 부위들), 3층 보장('무기·갑옷 가운데 점수가 낮은 쪽')에 쓴다.
        /// 전설이면 이 부위에 나올 수 있는 효과 가운데서 고른다. 비었거나 null이면 제한 없음.
        /// </summary>
        public GearPart[] OnlyParts;
        /// <summary>이미 가진 전설 효과(장착·가방·창고). 같은 효과가 다시 나오면 세기를 위쪽 절반에서 굴린다.</summary>
        public ICollection<LegendaryEffect> OwnedLegendaries;
        /// <summary>정예: 일반이 나오면 고급으로 올리고 옵션 1줄을 굴린다.</summary>
        public bool PromoteCommon;
    }

    /// <summary>
    /// 궤짝·처치 보상 규칙(3차 초안 2-5 찾을 거리 표, 4-6 처치 보상 표). 장비는 7부위(장비 문서 8-1: 부위 ‰ 340/105/75/75/75/180/150, 종류는 1/3씩).
    /// 장비 개수·강화석·골드·나무 궤짝 규칙은 M0b 그대로다(결정 1: 총량 그대로). 배율 = 1 + 0.15 × (층 − 1)은 ‰ 정수(1000 + 150 × (층 − 1))로 계산한다.
    /// </summary>
    public static class LootRules
    {
        /// <summary>쇠 궤짝 Param: 희귀 이상 무기 보장(1층 H, 2차 7-4 첫 상자 보장을 옮김). 부위는 무기만.</summary>
        public const string RareWeaponParam = "rare-weapon";
        /// <summary>쇠 궤짝 Param: 영웅 이상 보장(3층 첫 쇠 궤짝, 8-2). 부위는 context.OnlyParts(부르는 쪽이 무기·갑옷 가운데 점수가 낮은 쪽을 넣음), 없으면 무기·갑옷.</summary>
        public const string EpicParam = "epic";

        /// <summary>부위 보정 배율(8-2): 희귀 이상이 나오면 BoostedParts 가중치 ×3.</summary>
        public const int BoostFactor = 3;
        /// <summary>부위 보정 부위 수(8-2): 부위 점수가 가장 낮은 2부위.</summary>
        public const int BoostedPartCount = 2;

        static readonly GearPart[] WeaponOnly = { GearPart.Weapon };
        static readonly GearPart[] WeaponOrArmor = { GearPart.Weapon, GearPart.Armor };

        /// <summary>나무 궤짝: 장비 10%, 강화석 25%로 1개, 쥐 궤짝 10%.</summary>
        public const int WoodGearPermille = 100;
        public const int WoodStonePermille = 250;
        public const int WoodRatPermille = 100;
        /// <summary>쥐 궤짝에서 나오는 굴쥐 수.</summary>
        public const int RatChestRats = 3;

        /// <summary>2차 7-2 정예: 일반이 나오면 고급.</summary>
        public const int EliteGearCount = 2;

        /// <summary>골드 무더기를 나눌 때 최대 개수(연출이 장비를 가리지 않게).</summary>
        public const int MaxGoldPiles = 6;

        /// <summary>처치 보상 한 줄(기댓값 ‰). 강화석은 × 층 배율, 골드는 무더기 수.</summary>
        readonly struct KillRow
        {
            public readonly int GearPermille;
            public readonly int StoneBasePermille;
            public readonly int GoldPilePermille;

            public KillRow(int gear, int stone, int piles)
            {
                GearPermille = gear;
                StoneBasePermille = stone;
                GoldPilePermille = piles;
            }
        }

        /// <summary>3차 초안 4-6: 굴쥐 3% / 5%×배율 / 25%, 궁수 22% / 10%×배율 / 1개, 멧돼지 33% / 15%×배율 / 1.5개, 둥지 정리 50% / 0.5×배율 / 3개.</summary>
        static KillRow Row(KillSource source)
        {
            switch (source)
            {
                case KillSource.Archer: return new KillRow(220, 100, 1000);
                case KillSource.Boar: return new KillRow(330, 150, 1500);
                case KillSource.NestClear: return new KillRow(500, 500, 3000);
                default: return new KillRow(30, 50, 250);
            }
        }

        /// <summary>층 배율(‰) = 1000 + 150 × (층 − 1).</summary>
        public static int FloorMultiplierPermille(int floor) => 1000 + 150 * (FloorScaling.Clamp(floor) - 1);

        /// <summary>정수 × 배율‰ ÷ 1000을 반올림(0.5 올림). round(3 × 배율) 같은 값.</summary>
        public static int ScaleByFloor(int value, int floor) => (int)(((long)value * FloorMultiplierPermille(floor) + 500) / 1000);

        /// <summary>골드 무더기 하나의 값 = round(3 × 배율).</summary>
        public static int GoldPileValue(int floor) => ScaleByFloor(3, floor);

        /// <summary>나무 궤짝 골드 = 4 + 2 × 층.</summary>
        public static int WoodGold(int floor) => 4 + 2 * FloorScaling.Clamp(floor);

        /// <summary>쇠 궤짝 골드 = 6 + 3 × 층.</summary>
        public static int IronGold(int floor) => 6 + 3 * FloorScaling.Clamp(floor);

        /// <summary>쇠 궤짝 강화석 = max(1, 층 − 2).</summary>
        public static int IronStones(int floor) => Math.Max(1, FloorScaling.Clamp(floor) - 2);

        /// <summary>
        /// 옛 무기 하나(M0b): 등급(층 표, min 이상) → 종류(1/3씩) → 굴림(900~1100‰) 순서로 난수 3번.
        /// 보상은 이제 RollGear를 쓴다. 이 함수는 옛 시험과 꾸러미 v1 읽기용으로 남긴다.
        /// </summary>
        public static WeaponItem RollWeapon(int floor, IRandom rng, Grade min = Grade.Common)
        {
            int f = FloorScaling.Clamp(floor);
            var grade = GradeRules.RollAtLeast(f, min, rng);
            var presets = WeaponPresets.All;
            string id = presets[rng.NextInt(0, presets.Length)].id;
            int roll = rng.NextInt(GearMath.RollMinPermille, GearMath.RollMaxPermille + 1);
            return new WeaponItem(id, grade, f, roll);
        }

        /// <summary>
        /// 장비 하나(7부위, 장비 문서 8-1 굴리는 순서). 같은 씨앗이면 같은 장비가 나온다.
        /// ① 등급: GradeRules.RollAtLeast(층, min)(난수 1번). context.PromoteCommon이면 일반을 고급으로 올린다(그래서 옵션 1줄을 굴림).
        /// ② 부위: 전설이면 6장(효과 1/3 → 그 효과의 부위 묶음 안 비율, 난수 2번). OnlyParts가 있으면 그 부위에 나올 수 있는 효과 k개 가운데 1/k,
        ///    부위는 묶음 가운데 OnlyParts 안의 부위만. 전설이 아니면 GearSlots.PartDropPermille 7칸으로 고른다(난수 1번):
        ///    희귀 이상이면 BoostedParts 가중치 ×3, OnlyParts가 있으면 그 안에서만.
        /// ③ 종류 1/3(난수 1번) → ④ 굴림 900~1100‰(난수 1번) → ⑤ 옵션 OptionTable.RollAll(등급 줄 수, 줄마다 난수 2번)
        /// → ⑥ 전설이면 세기 LegendaryTable.RollStrength(OwnedLegendaries에 있으면 위쪽 절반, 난수 1번). 아이템 레벨 = 층.
        /// </summary>
        public static GearItem RollGear(int floor, IRandom rng, Grade min = Grade.Common, GearRollContext context = null)
        {
            int f = FloorScaling.Clamp(floor);
            var grade = GradeRules.RollAtLeast(f, min, rng);
            if (context != null && context.PromoteCommon && grade == Grade.Common) grade = Grade.Uncommon;

            var only = context != null && context.OnlyParts != null && context.OnlyParts.Length > 0 ? context.OnlyParts : null;
            GearPart part;
            LegendaryDef legendary = null;
            if (grade == Grade.Legendary)
            {
                var effect = LegendaryTable.RollEffect(only, rng);
                legendary = LegendaryTable.Get(effect);
                part = LegendaryTable.RollPart(effect, only, rng);
            }
            else
            {
                var boosted = grade >= Grade.Rare && context != null ? context.BoostedParts : null;
                part = RollPart(rng, only, boosted);
            }

            var kinds = GearBaseTable.ForPart(part);
            var kind = kinds[rng.NextInt(0, kinds.Count)];
            int roll = rng.NextInt(GearMath.RollMinPermille, GearMath.RollMaxPermille + 1);
            var options = OptionTable.RollAll(part, grade, f, rng);
            string legendaryId = null;
            int legendaryRoll = 0;
            if (legendary != null)
            {
                bool owned = context != null && context.OwnedLegendaries != null && context.OwnedLegendaries.Contains(legendary.Effect);
                legendaryId = legendary.Id;
                legendaryRoll = LegendaryTable.RollStrength(owned, rng);
            }
            return new GearItem(kind.Id, grade, f, roll, 0, options, legendaryId, legendaryRoll);
        }

        /// <summary>
        /// 전설이 아닌 장비의 부위(난수 1번): 7부위 비율(‰ 340/105/75/75/75/180/150). boosted 부위는 ×3,
        /// only가 있으면 그 밖의 부위는 0. 남은 가중치가 없으면 제한 없이 고른다.
        /// </summary>
        static GearPart RollPart(IRandom rng, GearPart[] only, GearPart[] boosted)
        {
            var weights = new int[GearSlots.PartCount];
            int total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                var p = (GearPart)i;
                if (only != null && Array.IndexOf(only, p) < 0) continue;
                int w = GearSlots.PartDropPermille(p);
                if (boosted != null && Array.IndexOf(boosted, p) >= 0) w *= BoostFactor;
                weights[i] = w;
                total += w;
            }
            if (total <= 0)
            {
                for (int i = 0; i < weights.Length; i++)
                {
                    weights[i] = GearSlots.PartDropPermille((GearPart)i);
                    total += weights[i];
                }
            }
            int r = rng.NextInt(0, total);
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (r < 0) return (GearPart)i;
            }
            return GearPart.Weapon;
        }

        /// <summary>
        /// 부위 점수가 가장 낮은 부위 count개(장비 문서 8-2 부위 보정). partScores는 GearPart 차례 7칸(반지는 두 자리 중 약한 쪽을 부르는 쪽이 넣음, 빈 자리 0).
        /// 같은 점수면 부위 비율(GearSlots.PartDropPermille)이 큰 부위, 그래도 같으면 앞 차례.
        /// </summary>
        public static GearPart[] WeakestParts(IReadOnlyList<double> partScores, int count = BoostedPartCount)
        {
            var order = new List<GearPart>(GearSlots.Parts);
            if (partScores == null || partScores.Count < GearSlots.PartCount) return order.GetRange(0, Math.Max(0, Math.Min(count, order.Count))).ToArray();
            order.Sort((a, b) =>
            {
                int c = partScores[(int)a].CompareTo(partScores[(int)b]);
                if (c != 0) return c;
                c = GearSlots.PartDropPermille(b).CompareTo(GearSlots.PartDropPermille(a));
                return c != 0 ? c : ((int)a).CompareTo((int)b);
            });
            return order.GetRange(0, Math.Max(0, Math.Min(count, order.Count))).ToArray();
        }

        /// <summary>
        /// 장착 상태의 부위 점수 7칸(GearPart 차례, GearMath.ItemScore). 빈 자리는 0, 반지는 두 자리 중 약한 쪽(8-2·8-6 '묶음 단위 판단').
        /// WeakestParts의 입력이다. 장착이 null이면 모두 0.
        /// </summary>
        public static double[] PartScores(Loadout loadout)
        {
            var scores = new double[GearSlots.PartCount];
            if (loadout == null) return scores;
            for (int i = 0; i < scores.Length; i++)
            {
                var part = (GearPart)i;
                if (part == GearPart.Ring)
                    scores[i] = Math.Min(GearMath.ItemScore(loadout[GearSlot.Ring1]), GearMath.ItemScore(loadout[GearSlot.Ring2]));
                else
                    scores[i] = GearMath.ItemScore(loadout[GearSlots.FirstSlotOf(part)]);
            }
            return scores;
        }

        /// <summary>
        /// 빈 자리가 있는 부위(GearPart 차례, 8-2 빈 자리 채우기의 OnlyParts). 반지는 두 자리 중 하나라도 비면 넣는다. 없으면 빈 배열.
        /// </summary>
        public static GearPart[] EmptyParts(Loadout loadout)
        {
            var list = new List<GearPart>();
            if (loadout == null) return list.ToArray();
            foreach (var part in GearSlots.Parts)
            {
                bool empty = part == GearPart.Ring
                    ? loadout[GearSlot.Ring1] == null || loadout[GearSlot.Ring2] == null
                    : loadout[GearSlots.FirstSlotOf(part)] == null;
                if (empty) list.Add(part);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 궤짝 보상(3차 초안 2-5). 아이템 레벨 = 층(M0b는 1층뿐이라 '최고 도달 층 − 1' 규칙은 쓰지 않는다).
        /// 나무: 장비 10%, 강화석 25%로 1개, 골드 4 + 2 × 층, 10%는 쥐 궤짝(보상은 그대로).
        /// 쇠: 장비 1개 확정, 강화석 max(1, 층 − 2), 골드 6 + 3 × 층.
        /// Param "rare-weapon"이면 희귀 이상 무기(OnlyParts = 무기), "epic"이면 영웅 이상(OnlyParts = context.OnlyParts, 없으면 무기·갑옷).
        /// context는 빈 자리 채우기(OnlyParts)·순위 보정(BoostedParts)·가진 전설에 쓴다. null이면 보통 규칙.
        /// </summary>
        public static LootBundle RollChest(bool iron, int floor, string param, IRandom rng, GearRollContext context)
        {
            var bundle = new LootBundle();
            int f = FloorScaling.Clamp(floor);
            if (iron)
            {
                var min = Grade.Common;
                var ctx = context;
                if (param == RareWeaponParam)
                {
                    min = Grade.Rare;
                    ctx = With(context, WeaponOnly);
                }
                else if (param == EpicParam)
                {
                    min = Grade.Epic;
                    var only = context != null && context.OnlyParts != null && context.OnlyParts.Length > 0 ? context.OnlyParts : WeaponOrArmor;
                    ctx = With(context, only);
                }
                bundle.Gear.Add(RollGear(f, rng, min, ctx));
                bundle.Stones = IronStones(f);
                SplitGold(bundle, IronGold(f), f);
                return bundle;
            }
            bool gear = rng.NextInt(0, 1000) < WoodGearPermille;
            bool stone = rng.NextInt(0, 1000) < WoodStonePermille;
            bundle.RatChest = rng.NextInt(0, 1000) < WoodRatPermille;
            if (gear) bundle.Gear.Add(RollGear(f, rng, Grade.Common, context));
            bundle.Stones = stone ? 1 : 0;
            SplitGold(bundle, WoodGold(f), f);
            return bundle;
        }

        /// <summary>궤짝 보상(보통 규칙, context 없음).</summary>
        public static LootBundle RollChest(bool iron, int floor, string param, IRandom rng) => RollChest(iron, floor, param, rng, null);

        /// <summary>
        /// 광업소 금고(3차 초안 2-5): 장비 2, 강화석 2 + 아이템 레벨 ÷ 2(내림), 골드 15 + 6 × 아이템 레벨.
        /// M0b에서는 열쇠가 없어 열리지 않지만 금고(D)가 같은 규칙을 쓰도록 둔다.
        /// </summary>
        public static LootBundle RollSafe(int itemLevel, IRandom rng, GearRollContext context)
        {
            var bundle = new LootBundle();
            int lv = FloorScaling.Clamp(itemLevel);
            for (int i = 0; i < 2; i++) bundle.Gear.Add(RollGear(lv, rng, Grade.Common, context));
            bundle.Stones = 2 + lv / 2;
            SplitGold(bundle, 15 + 6 * lv, lv);
            return bundle;
        }

        /// <summary>광업소 금고(보통 규칙, context 없음).</summary>
        public static LootBundle RollSafe(int itemLevel, IRandom rng) => RollSafe(itemLevel, rng, null);

        /// <summary>
        /// 처치 보상(3차 초안 4-6). 소수 기댓값은 정수 부분 + 남은 몫 확률로 하나 더(예: 멧돼지 골드 1.5개 = 1개 + 50%).
        /// 정예: 장비 2개(PromoteCommon: 일반이면 고급으로 올리고 옵션 1줄), 강화석 round(3 × 배율), 골드 round(15 × 배율)을 무더기로 나눔.
        /// context는 순위 보정·가진 전설에 쓴다. null이면 보통 규칙.
        /// </summary>
        public static LootBundle RollKill(KillSource source, int floor, IRandom rng, GearRollContext context)
        {
            var bundle = new LootBundle();
            int f = FloorScaling.Clamp(floor);
            if (source == KillSource.Elite)
            {
                var elite = Promoted(context);
                for (int i = 0; i < EliteGearCount; i++) bundle.Gear.Add(RollGear(f, rng, Grade.Common, elite));
                bundle.Stones = ScaleByFloor(3, f);
                SplitGold(bundle, ScaleByFloor(15, f), f);
                return bundle;
            }
            var row = Row(source);
            int gearCount = RollCount(row.GearPermille, rng);
            int stoneExpected = (int)(((long)row.StoneBasePermille * FloorMultiplierPermille(f) + 500) / 1000);
            bundle.Stones = RollCount(stoneExpected, rng);
            int piles = RollCount(row.GoldPilePermille, rng);
            for (int i = 0; i < gearCount; i++) bundle.Gear.Add(RollGear(f, rng, Grade.Common, context));
            int pileValue = GoldPileValue(f);
            for (int i = 0; i < piles; i++) bundle.GoldPiles.Add(pileValue);
            return bundle;
        }

        /// <summary>처치 보상(보통 규칙, context 없음).</summary>
        public static LootBundle RollKill(KillSource source, int floor, IRandom rng) => RollKill(source, floor, rng, null);

        /// <summary>context를 복사해 OnlyParts만 바꾼다(부르는 쪽 context는 고치지 않음).</summary>
        static GearRollContext With(GearRollContext context, GearPart[] onlyParts) => new GearRollContext
        {
            BoostedParts = context?.BoostedParts,
            OnlyParts = onlyParts,
            OwnedLegendaries = context?.OwnedLegendaries,
            PromoteCommon = context != null && context.PromoteCommon,
        };

        /// <summary>context를 복사해 PromoteCommon을 켠다(정예).</summary>
        static GearRollContext Promoted(GearRollContext context) => new GearRollContext
        {
            BoostedParts = context?.BoostedParts,
            OnlyParts = context?.OnlyParts,
            OwnedLegendaries = context?.OwnedLegendaries,
            PromoteCommon = true,
        };

        /// <summary>기댓값(‰)을 개수로: 정수 부분 + 남은 ‰ 확률로 하나 더. 늘 난수 1번을 쓴다(순서 고정).</summary>
        public static int RollCount(int expectedPermille, IRandom rng)
        {
            int e = Math.Max(0, expectedPermille);
            int whole = e / 1000;
            int frac = e % 1000;
            return whole + (rng.NextInt(0, 1000) < frac ? 1 : 0);
        }

        /// <summary>골드 합계를 무더기(하나 약 round(3 × 배율))로 나눈다. 최대 6개, 나머지는 앞 무더기부터 1씩.</summary>
        public static void SplitGold(LootBundle bundle, int total, int floor)
        {
            if (total <= 0) return;
            int pile = Math.Max(1, GoldPileValue(floor));
            int n = Math.Max(1, Math.Min(MaxGoldPiles, (total + pile / 2) / pile));
            int each = total / n;
            int extra = total % n;
            for (int i = 0; i < n; i++) bundle.GoldPiles.Add(each + (i < extra ? 1 : 0));
        }

        /// <summary>궤짝 시드용 문자열 해시(FNV-1a 32비트). 실행마다 같은 값(string.GetHashCode는 보장하지 않음).</summary>
        public static uint StableHash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (text != null)
                    foreach (char c in text)
                    {
                        h ^= c;
                        h *= 16777619u;
                    }
                return h;
            }
        }

        /// <summary>궤짝 난수 시드 = (원정 번호, id 해시). 같은 원정의 같은 궤짝이면 같은 결과.</summary>
        public static ulong ChestSeed(int expedition, string id) => ((ulong)(uint)expedition << 32) | StableHash(id);
    }
}
