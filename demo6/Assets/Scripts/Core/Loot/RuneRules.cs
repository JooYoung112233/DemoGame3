using System.Collections.Generic;
using Demo6.Core.Combat;
using Demo6.Core.Random;

namespace Demo6.Core.Loot
{
    /// <summary>룬이 떨어지는 곳(기획/세-무기-우클릭-소켓-1차.md 6-3 표의 줄).</summary>
    public enum RuneSource
    {
        Rat,
        Archer,
        Boar,
        Elite,
        NestClear,
        WoodChest,
        IronChest,
        Safe,
        BossFirst,
        BossRepeat,
    }

    /// <summary>
    /// 룬 홈(소켓)·룬 규칙(6-1·6-3·6-5). 장비·강화석·골드와 따로 흐르는 난수로 굴려 기존 결과·시험이 바뀌지 않는다.
    /// 계약 단계 값(확정). 꾸러미 ⑥ 소켓·룬이 LootBundle.Runes·AddTo(bundle, source, rng)를 더하고 시험한다(RuneRulesTests).
    /// </summary>
    public static class RuneRules
    {
        /// <summary>종류마다 주머니 최대.</summary>
        public const int PouchCap = 99;
        /// <summary>무기 홈 최대(영웅·전설).</summary>
        public const int MaxSockets = 2;
        /// <summary>처치 룬 흐름(시각 씨앗 + 이 흐름, Inventory 처치 보상 23과 겹치지 않음).</summary>
        public const ulong KillStream = 29;
        /// <summary>궤짝·금고 룬 흐름(궤짝 씨앗 + 이 흐름).</summary>
        public const ulong ChestStream = 43;
        /// <summary>보스 다시 처치 룬 흐름(보스 씨앗 + 이 흐름).</summary>
        public const ulong BossStream = 47;

        /// <summary>
        /// 룬 홈 칸 수 = 부위 + 등급(종류 무관, 저장하지 않고 매번 계산): 무기 일반·고급·희귀 1, 영웅·전설 2, 방어구·장신구 0.
        /// </summary>
        public static int SocketCount(GearPart part, Grade grade)
        {
            if (part != GearPart.Weapon) return 0;
            return grade >= Grade.Epic ? 2 : 1;
        }

        /// <summary>떨어질 확률(‰, 층 배율 없음). 오우거 첫 처치는 확정(1000).</summary>
        public static int DropPermille(RuneSource source)
        {
            switch (source)
            {
                case RuneSource.Rat: return 3;
                case RuneSource.Archer: return 15;
                case RuneSource.Boar: return 25;
                case RuneSource.Elite: return 150;
                case RuneSource.NestClear: return 80;
                case RuneSource.WoodChest: return 15;
                case RuneSource.IronChest: return 100;
                case RuneSource.Safe: return 500;
                case RuneSource.BossFirst: return 1000;
                case RuneSource.BossRepeat: return 300;
                default: return 0;
            }
        }

        /// <summary>
        /// 한 번 굴린다. 난수는 늘 2번(나올지 → 어느 룬인지) 써서 순서가 고정이다. 안 나오면 null, 나오면 룬 id.
        /// </summary>
        public static string Roll(RuneSource source, IRandom rng)
        {
            bool drop = rng.NextInt(0, 1000) < DropPermille(source);
            int total = 0;
            foreach (var r in RuneTable.All) total += r.Weight > 0 ? r.Weight : 0;
            int pick = rng.NextInt(0, total > 0 ? total : 1);
            if (!drop || total <= 0) return null;
            foreach (var r in RuneTable.All)
            {
                int w = r.Weight > 0 ? r.Weight : 0;
                if (pick < w) return r.Id;
                pick -= w;
            }
            return null;
        }

        /// <summary>
        /// 이 장비에 이 룬을 끼울 수 있는가(6-5): 무기만, 아는 룬만, 빈 홈이 있고, 같은 종류 룬은 한 무기에 하나만.
        /// inserted = 이미 낀 룬 id(없으면 null).
        /// </summary>
        public static bool CanInsert(GearPart part, Grade grade, IReadOnlyList<string> inserted, string runeId)
        {
            if (part != GearPart.Weapon || RuneTable.Get(runeId) == null) return false;
            int count = inserted != null ? inserted.Count : 0;
            if (count >= SocketCount(part, grade)) return false;
            for (int i = 0; i < count; i++)
                if (inserted[i] == runeId) return false;
            return true;
        }

        // ── 꾸러미 ⑥ 소켓·룬이 더한 것 ──

        /// <summary>
        /// 한 번 굴려(Roll, 난수 늘 2번) 나오면 묶음의 룬 목록 끝에 붙인다. 붙인 룬 id, 안 나오면 null.
        /// 장비·강화석·골드 난수와 따로 흐르는 rng를 넘긴다(처치 KillStream, 궤짝·금고 ChestStream, 보스 BossStream). bundle이 null이어도 난수는 2번 쓴다.
        /// </summary>
        public static string AddTo(LootBundle bundle, RuneSource source, IRandom rng)
        {
            string id = Roll(source, rng);
            if (id != null && bundle != null) bundle.Runes.Add(id);
            return id;
        }

        /// <summary>처치 보상 출처 → 룬 출처(굴쥐·궁수·멧돼지·정예·둥지 정리).</summary>
        public static RuneSource SourceOf(KillSource source)
        {
            switch (source)
            {
                case KillSource.Archer: return RuneSource.Archer;
                case KillSource.Boar: return RuneSource.Boar;
                case KillSource.NestClear: return RuneSource.NestClear;
                case KillSource.Elite: return RuneSource.Elite;
                default: return RuneSource.Rat;
            }
        }

        /// <summary>
        /// 룬 목록을 이 장비의 홈에 맞춘다(6-6 저장 읽기). 차례대로 CanInsert가 되는 룬만 kept에 담고, 아는 룬인데 들어가지 못한 것
        /// (홈이 모자람·같은 룬 두 번·무기가 아님)은 overflow(주머니로 보낼 것)에 담는다. 모르는 id·빈 글은 버린다. kept·overflow는 null이어도 된다.
        /// 끼운 수를 돌려준다.
        /// </summary>
        public static int Fit(GearPart part, Grade grade, IEnumerable<string> runes, List<string> kept, ICollection<string> overflow)
        {
            var inserted = kept ?? new List<string>(MaxSockets);
            int start = inserted.Count;
            if (runes == null) return 0;
            foreach (var id in runes)
            {
                if (RuneTable.Get(id) == null) continue;
                if (CanInsert(part, grade, inserted, id)) inserted.Add(id);
                else overflow?.Add(id);
            }
            return inserted.Count - start;
        }

        /// <summary>
        /// 주머니에 더한다(6-4: 종류마다 최대 PouchCap). 모르는 룬·0 이하는 더하지 않는다. 실제로 더한 개수를 돌려준다(가득 차면 0).
        /// </summary>
        public static int PouchAdd(IDictionary<string, int> pouch, string runeId, int count = 1)
        {
            if (pouch == null || count <= 0 || RuneTable.Get(runeId) == null) return 0;
            pouch.TryGetValue(runeId, out int have);
            if (have < 0) have = 0;
            int added = System.Math.Max(0, System.Math.Min(count, PouchCap - have));
            if (added > 0 || pouch.ContainsKey(runeId)) pouch[runeId] = have + added;
            return added;
        }

        /// <summary>주머니에서 하나 뺀다. 없으면 false. 0이 되면 줄을 지운다(같은 값이면 같은 글).</summary>
        public static bool PouchTake(IDictionary<string, int> pouch, string runeId)
        {
            if (pouch == null || runeId == null || !pouch.TryGetValue(runeId, out int have) || have <= 0) return false;
            if (have <= 1) pouch.Remove(runeId);
            else pouch[runeId] = have - 1;
            return true;
        }

        /// <summary>
        /// 이 룬이 이 무기에서 효과가 있는가(6-7, 카드 회색 줄 '이 무기에서는 효과 없음'). 버팀 룬은 대검(기 모으기)만,
        /// testAllActs(Tuning.SuperArmorAllActs)면 오른쪽 클릭 행동이 있는 세 무기 모두. 모르는 룬은 false.
        /// </summary>
        public static bool Works(string runeId, string weaponId, bool testAllActs = false)
        {
            var rune = RuneTable.Get(runeId);
            if (rune == null) return false;
            var kind = WeaponActRules.KindOf(weaponId);
            switch (rune.Effect)
            {
                case RuneEffect.SuperArmor:
                    return kind == WeaponActKind.Charge || (testAllActs && kind != WeaponActKind.None);
                default:
                    return false;
            }
        }
    }
}
