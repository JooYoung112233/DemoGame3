namespace Demo6.Core.Loot
{
    /// <summary>장비 부위 7개(장비 문서 4-1). 반지는 부위 하나에 자리가 둘이다.</summary>
    public enum GearPart
    {
        /// <summary>무기(장검·대검·쌍검).</summary>
        Weapon,
        /// <summary>갑옷(몸 그림 통째).</summary>
        Armor,
        /// <summary>투구(머리 위 덧그림).</summary>
        Helm,
        /// <summary>장갑(주먹).</summary>
        Gloves,
        /// <summary>장화.</summary>
        Boots,
        /// <summary>반지(자리 둘).</summary>
        Ring,
        /// <summary>목걸이.</summary>
        Amulet,
    }

    /// <summary>장착 자리 8개(장비 문서 4-1). 꾸러미 글의 eq.* 줄 순서와 같다.</summary>
    public enum GearSlot
    {
        Weapon,
        Armor,
        Helm,
        Gloves,
        Boots,
        Ring1,
        Ring2,
        Amulet,
    }

    /// <summary>드랍·상점·시뮬레이션 묶음 3개(2차 3칸이 묶음으로 남음, 장비 문서 4-1).</summary>
    public enum GearGroup
    {
        /// <summary>무기.</summary>
        Weapon,
        /// <summary>방어구(갑옷·투구·장갑·장화).</summary>
        Armor,
        /// <summary>장신구(반지·목걸이).</summary>
        Accessory,
    }

    /// <summary>방어구 무게(장비 문서 4-2). 무기·장신구는 None.</summary>
    public enum ArmorWeight
    {
        None,
        /// <summary>가죽: 이동 +.</summary>
        Light,
        /// <summary>사슬: 체력이 가장 많음.</summary>
        Medium,
        /// <summary>판금: 방어가 가장 높음, 이동 −.</summary>
        Heavy,
    }

    /// <summary>
    /// 부위·자리·묶음 규칙표(장비 문서 4-1, 8-1, 8-4, 8-5). 숫자는 모두 ‰ 정수다.
    /// 아래 값과 함수 이름, enum 차례는 꾸러미 글·능력치 출처·그림 칸이 쓰므로 바꾸지 않는다.
    /// </summary>
    public static class GearSlots
    {
        public const int PartCount = 7;
        public const int SlotCount = 8;
        public const int GroupCount = 3;

        /// <summary>자리 8개(차례대로).</summary>
        public static readonly GearSlot[] All =
        {
            GearSlot.Weapon, GearSlot.Armor, GearSlot.Helm, GearSlot.Gloves, GearSlot.Boots, GearSlot.Ring1, GearSlot.Ring2, GearSlot.Amulet,
        };

        /// <summary>부위 7개(차례대로).</summary>
        public static readonly GearPart[] Parts =
        {
            GearPart.Weapon, GearPart.Armor, GearPart.Helm, GearPart.Gloves, GearPart.Boots, GearPart.Ring, GearPart.Amulet,
        };

        static readonly string[] PartNames = { "무기", "갑옷", "투구", "장갑", "장화", "반지", "목걸이" };
        static readonly string[] SlotNames = { "무기", "갑옷", "투구", "장갑", "장화", "반지 1", "반지 2", "목걸이" };
        /// <summary>꾸러미 글 키(eq.{키}).</summary>
        static readonly string[] SlotKeys = { "weapon", "armor", "helm", "gloves", "boots", "ring1", "ring2", "amulet" };

        /// <summary>4-1 기본 능력치 몫(‰): 갑옷 500, 투구 200, 장갑·장화 150. 무기·장신구는 0(따로 셈).</summary>
        static readonly int[] BaseShare = { 0, 500, 200, 150, 150, 0, 0 };
        /// <summary>8-4 강화 비용 배율(‰): 무기 1000, 갑옷 500, 투구 200, 장갑·장화 150, 반지 300(자리마다), 목걸이 400.</summary>
        static readonly int[] EnhanceCost = { 1000, 500, 200, 150, 150, 300, 400 };
        /// <summary>8-5 비교 문턱(‰): 무기 30, 갑옷 20, 투구·장갑·장화·반지·목걸이 10.</summary>
        static readonly int[] CompareThreshold = { 30, 20, 10, 10, 10, 10, 10 };
        /// <summary>8-1 부위 비율(‰, 합 1000): 무기 340 · 갑옷 105 · 투구 75 · 장갑 75 · 장화 75 · 반지 180(두 자리 몫) · 목걸이 150.</summary>
        static readonly int[] PartDrop = { 340, 105, 75, 75, 75, 180, 150 };
        /// <summary>8-1 묶음 비율(‰): 무기 340 · 방어구 330 · 장신구 330(2차 slotPermille 그대로).</summary>
        static readonly int[] GroupDrop = { 340, 330, 330 };

        public static GearPart PartOf(GearSlot slot)
        {
            switch (slot)
            {
                case GearSlot.Weapon: return GearPart.Weapon;
                case GearSlot.Armor: return GearPart.Armor;
                case GearSlot.Helm: return GearPart.Helm;
                case GearSlot.Gloves: return GearPart.Gloves;
                case GearSlot.Boots: return GearPart.Boots;
                case GearSlot.Ring1:
                case GearSlot.Ring2: return GearPart.Ring;
                default: return GearPart.Amulet;
            }
        }

        /// <summary>부위의 첫 자리(반지 → 반지 1).</summary>
        public static GearSlot FirstSlotOf(GearPart part)
        {
            switch (part)
            {
                case GearPart.Weapon: return GearSlot.Weapon;
                case GearPart.Armor: return GearSlot.Armor;
                case GearPart.Helm: return GearSlot.Helm;
                case GearPart.Gloves: return GearSlot.Gloves;
                case GearPart.Boots: return GearSlot.Boots;
                case GearPart.Ring: return GearSlot.Ring1;
                default: return GearSlot.Amulet;
            }
        }

        /// <summary>그 자리에 이 부위를 낄 수 있는가(반지는 반지 1·2 모두).</summary>
        public static bool Accepts(GearSlot slot, GearPart part) => PartOf(slot) == part;

        public static GearGroup GroupOf(GearPart part)
        {
            switch (part)
            {
                case GearPart.Weapon: return GearGroup.Weapon;
                case GearPart.Ring:
                case GearPart.Amulet: return GearGroup.Accessory;
                default: return GearGroup.Armor;
            }
        }

        /// <summary>그 묶음의 부위(부위 차례, 새 배열). 상점 묶음 칸·시뮬레이션 묶음 판단(8-3)에 쓴다.</summary>
        public static GearPart[] PartsOf(GearGroup group)
        {
            var list = new System.Collections.Generic.List<GearPart>(4);
            foreach (var part in Parts)
                if (GroupOf(part) == group) list.Add(part);
            return list.ToArray();
        }

        /// <summary>벗어서 비울 수 있는 자리(반지 1·2·목걸이). 무기·갑옷·투구·장갑·장화는 바꾸기만 한다(4-1).</summary>
        public static bool CanUnequip(GearSlot slot) => slot == GearSlot.Ring1 || slot == GearSlot.Ring2 || slot == GearSlot.Amulet;

        /// <summary>반지 자리의 다른 쪽(Shift+G). 반지 자리가 아니면 그대로.</summary>
        public static GearSlot OtherRing(GearSlot slot) =>
            slot == GearSlot.Ring1 ? GearSlot.Ring2 : slot == GearSlot.Ring2 ? GearSlot.Ring1 : slot;

        public static string Name(GearPart part) => PartNames[(int)part];
        public static string SlotName(GearSlot slot) => SlotNames[(int)slot];
        /// <summary>꾸러미 글 키(weapon, armor, helm, gloves, boots, ring1, ring2, amulet).</summary>
        public static string Key(GearSlot slot) => SlotKeys[(int)slot];

        /// <summary>키로 자리 찾기. 모르면 false.</summary>
        public static bool TryParseKey(string key, out GearSlot slot)
        {
            for (int i = 0; i < SlotKeys.Length; i++)
                if (SlotKeys[i] == key)
                {
                    slot = (GearSlot)i;
                    return true;
                }
            slot = GearSlot.Weapon;
            return false;
        }

        public static int BaseSharePermille(GearPart part) => BaseShare[(int)part];
        public static int EnhanceCostPermille(GearPart part) => EnhanceCost[(int)part];
        public static int CompareThresholdPermille(GearPart part) => CompareThreshold[(int)part];
        /// <summary>전체에서 이 부위가 나올 비율(‰, 7부위 합 1000).</summary>
        public static int PartDropPermille(GearPart part) => PartDrop[(int)part];
        /// <summary>묶음 비율(‰, 3묶음 합 1000).</summary>
        public static int GroupDropPermille(GearGroup group) => GroupDrop[(int)group];
    }
}
