using System.Collections.Generic;

namespace Demo6.Core.Loot
{
    /// <summary>룬 효과 깃발(나중 룬은 줄 하나 + 깃발 하나로 늘린다, 6-8).</summary>
    public enum RuneEffect
    {
        None,
        /// <summary>버팀(슈퍼아머): 대검 기 모으기·놓아 베기 동안 맞아도 끊기지 않고 밀리지 않는다(피해는 받음).</summary>
        SuperArmor,
    }

    /// <summary>룬 한 종류(기획/세-무기-우클릭-소켓-1차.md 6-2). 등급·굴림이 없어 같은 룬은 다 같다(개수로 쌓음). 능력치 0.</summary>
    public sealed class RuneDef
    {
        public readonly string Id;
        /// <summary>화면 이름(예: '버팀 룬').</summary>
        public readonly string Name;
        /// <summary>짧은 이름(가방 제목 줄 '룬: 버팀 ×2').</summary>
        public readonly string ShortName;
        /// <summary>효과 한 줄(카드).</summary>
        public readonly string EffectLine;
        /// <summary>색 0xRRGGBB(등급색·골드와 겹치지 않음).</summary>
        public readonly int Rgb;
        /// <summary>어느 룬이 나올지 고르는 가중치(RuneRules.Roll 두 번째 난수).</summary>
        public readonly int Weight;
        public readonly RuneEffect Effect;

        public RuneDef(string id, string name, string shortName, string effectLine, int rgb, int weight, RuneEffect effect)
        {
            Id = id;
            Name = name;
            ShortName = shortName;
            EffectLine = effectLine;
            Rgb = rgb;
            Weight = weight;
            Effect = effect;
        }
    }

    /// <summary>룬 표(6-2). 계약 단계 값(확정). 게임 안 말은 '소켓' 대신 '룬 홈'.</summary>
    public static class RuneTable
    {
        public const string SuperArmorId = "rune_superarmor";

        public static readonly RuneDef SuperArmor = new RuneDef(
            SuperArmorId, "버팀 룬", "버팀",
            "대검 기 모으기·놓아 베기 동안 맞아도 끊기지 않고 밀리지 않는다(피해는 받는다)",
            0x5FD6C8, 1000, RuneEffect.SuperArmor);

        /// <summary>모든 룬(id 차례). 차례를 바꾸지 않는다(굴림 결과가 바뀜).</summary>
        public static readonly RuneDef[] All = { SuperArmor };

        /// <summary>id의 룬. 모르는 id·null이면 null.</summary>
        public static RuneDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var r in All)
                if (r.Id == id) return r;
            return null;
        }

        /// <summary>id 목록에 그 효과 룬이 있는가.</summary>
        public static bool Has(IReadOnlyList<string> runes, RuneEffect effect)
        {
            if (runes == null) return false;
            for (int i = 0; i < runes.Count; i++)
            {
                var r = Get(runes[i]);
                if (r != null && r.Effect == effect) return true;
            }
            return false;
        }
    }
}
