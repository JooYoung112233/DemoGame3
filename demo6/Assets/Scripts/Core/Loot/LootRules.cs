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
        public readonly List<WeaponItem> Gear = new List<WeaponItem>();
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
    /// 궤짝·처치 보상 규칙(3차 초안 2-5 찾을 거리 표, 4-6 처치 보상 표). M0b 장비는 무기 3종만(종류는 1/3씩).
    /// 배율 = 1 + 0.15 × (층 − 1)은 ‰ 정수(1000 + 150 × (층 − 1))로 계산한다.
    /// </summary>
    public static class LootRules
    {
        /// <summary>쇠 궤짝 Param: 희귀 이상 무기 보장(1층 H, 2차 7-4 첫 상자 보장을 옮김).</summary>
        public const string RareWeaponParam = "rare-weapon";
        /// <summary>쇠 궤짝 Param: 영웅 이상 보장(3층 첫 쇠 궤짝. M0b에는 없지만 규칙은 둔다).</summary>
        public const string EpicParam = "epic";

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

        /// <summary>무기 하나: 등급(층 표, min 이상) → 종류(1/3씩) → 굴림(900~1100‰) 순서로 난수 3번.</summary>
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
        /// 궤짝 보상(3차 초안 2-5). 아이템 레벨 = 층(M0b는 1층뿐이라 '최고 도달 층 − 1' 규칙은 쓰지 않는다).
        /// 나무: 장비 10%, 강화석 25%로 1개, 골드 4 + 2 × 층, 10%는 쥐 궤짝(보상은 그대로).
        /// 쇠: 장비 1개 확정(Param "rare-weapon"이면 희귀 이상), 강화석 max(1, 층 − 2), 골드 6 + 3 × 층.
        /// </summary>
        public static LootBundle RollChest(bool iron, int floor, string param, IRandom rng)
        {
            var bundle = new LootBundle();
            int f = FloorScaling.Clamp(floor);
            if (iron)
            {
                var min = param == RareWeaponParam ? Grade.Rare : param == EpicParam ? Grade.Epic : Grade.Common;
                bundle.Gear.Add(RollWeapon(f, rng, min));
                bundle.Stones = IronStones(f);
                SplitGold(bundle, IronGold(f), f);
                return bundle;
            }
            bool gear = rng.NextInt(0, 1000) < WoodGearPermille;
            bool stone = rng.NextInt(0, 1000) < WoodStonePermille;
            bundle.RatChest = rng.NextInt(0, 1000) < WoodRatPermille;
            if (gear) bundle.Gear.Add(RollWeapon(f, rng));
            bundle.Stones = stone ? 1 : 0;
            SplitGold(bundle, WoodGold(f), f);
            return bundle;
        }

        /// <summary>
        /// 광업소 금고(3차 초안 2-5): 장비 2, 강화석 2 + 아이템 레벨 ÷ 2(내림), 골드 15 + 6 × 아이템 레벨.
        /// M0b에서는 열쇠가 없어 열리지 않지만 금고(D)가 같은 규칙을 쓰도록 둔다.
        /// </summary>
        public static LootBundle RollSafe(int itemLevel, IRandom rng)
        {
            var bundle = new LootBundle();
            int lv = FloorScaling.Clamp(itemLevel);
            for (int i = 0; i < 2; i++) bundle.Gear.Add(RollWeapon(lv, rng));
            bundle.Stones = 2 + lv / 2;
            SplitGold(bundle, 15 + 6 * lv, lv);
            return bundle;
        }

        /// <summary>
        /// 처치 보상(3차 초안 4-6). 소수 기댓값은 정수 부분 + 남은 몫 확률로 하나 더(예: 멧돼지 골드 1.5개 = 1개 + 50%).
        /// 정예: 장비 2개(일반이면 고급), 강화석 round(3 × 배율), 골드 round(15 × 배율)을 무더기로 나눔.
        /// </summary>
        public static LootBundle RollKill(KillSource source, int floor, IRandom rng)
        {
            var bundle = new LootBundle();
            int f = FloorScaling.Clamp(floor);
            if (source == KillSource.Elite)
            {
                for (int i = 0; i < EliteGearCount; i++)
                {
                    var w = RollWeapon(f, rng);
                    if (w.Grade == Grade.Common) w = new WeaponItem(w.WeaponId, Grade.Uncommon, w.ItemLevel, w.RollPermille);
                    bundle.Gear.Add(w);
                }
                bundle.Stones = ScaleByFloor(3, f);
                SplitGold(bundle, ScaleByFloor(15, f), f);
                return bundle;
            }
            var row = Row(source);
            int gearCount = RollCount(row.GearPermille, rng);
            int stoneExpected = (int)(((long)row.StoneBasePermille * FloorMultiplierPermille(f) + 500) / 1000);
            bundle.Stones = RollCount(stoneExpected, rng);
            int piles = RollCount(row.GoldPilePermille, rng);
            for (int i = 0; i < gearCount; i++) bundle.Gear.Add(RollWeapon(f, rng));
            int pileValue = GoldPileValue(f);
            for (int i = 0; i < piles; i++) bundle.GoldPiles.Add(pileValue);
            return bundle;
        }

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
