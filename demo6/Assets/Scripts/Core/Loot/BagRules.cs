using System;

namespace Demo6.Core.Loot
{
    /// <summary>장비가 가방 쪽으로 오는 길(재화 쓸 곳 1차 5-3).</summary>
    public enum PickupReason
    {
        /// <summary>F로 직접 줍기.</summary>
        Manual,
        /// <summary>저절로 줍기(15 밖으로 멀어짐).</summary>
        Auto,
        /// <summary>층을 떠날 때 거두기(검토 1차 Q3).</summary>
        Sweep,
        /// <summary>G로 바꿔 껴 벗은 장비.</summary>
        Swap,
    }

    /// <summary>판정 결과(재화 쓸 곳 1차 5-3).</summary>
    public enum PickupAction
    {
        /// <summary>가방 칸에 넣는다.</summary>
        Take,
        /// <summary>넘침 칸에 넣는다(가방 칸은 찼다).</summary>
        TakeOverflow,
        /// <summary>그 자리에서 강화석으로 분해한다.</summary>
        Salvage,
        /// <summary>바닥에 남긴다(F 막힘이면 알림, 저절로 줍기면 조용히).</summary>
        Leave,
        /// <summary>남기되 떠나기 전에 한 번 묻는다(넘침도 가득인 희귀·영웅).</summary>
        Ask,
        /// <summary>발밑에 내려놓는다(넘침도 가득일 때 벗은 장비).</summary>
        DropAtFeet,
    }

    /// <summary>
    /// 가방 칸·넘침·줍기 판정(재화 쓸 곳 1차 5-1·5-3, 2차 7-7, 장비 문서 8-3). 순수 규칙. 시험: BagRulesTests.
    /// 칸 = 20 + 5 × 산 가방 물건 수(0~2) → 20·25·30. 넘침 끝(HardCap) = 칸 + 6. '가득' = 개수 ≥ 칸(Inventory.BagFull과 같은 뜻).
    /// 넘침 칸에 들어가는 길은 G로 바꿔 껴 벗은 장비, 반지·목걸이 벗기, 떠날 때 거두기뿐이다.
    /// ㉡(기본 켬): 끼어도 나아지지 않는 일반 +0은 저절로 줍기·떠날 때 거두기에서 그 자리에서 분해. ㉠: 저절로 줍기가 칸 − 3에서 멈춤(F1 손잡이).
    /// </summary>
    public static class BagRules
    {
        public const int BaseCapacity = 20, StepSlots = 5, MaxUpgrades = 2, OverflowSlots = 6, AutoReserveSlots = 3, NearlyFullMargin = 4;
        /// <summary>저절로 줍기 거리(LootDrop.cs의 15f 숫자와 같아야 한다. LootDrop은 고치지 않으므로 숫자를 맞춰 둔다).</summary>
        public const float AutoPickupDistance = 15f;
        /// <summary>㉡ 기본값(켬, 5-2).</summary>
        public const bool AutoSalvageCommonDefault = true;

        /// <summary>가방 칸 = 20 + 5 × 늘린 수(0~2로 자름).</summary>
        public static int Capacity(int upgrades) => BaseCapacity + StepSlots * Math.Max(0, Math.Min(MaxUpgrades, upgrades));

        /// <summary>넘침 끝 = 칸 + 6(26·31·36).</summary>
        public static int HardCap(int capacity) => capacity + OverflowSlots;

        /// <summary>거의 참 = 개수 ≥ 칸 − 4(20칸이면 16). 권양기 창 덧줄(5-4).</summary>
        public static bool NearlyFull(int count, int capacity) => count >= capacity - NearlyFullMargin;

        /// <summary>㉡ 대상: 켬이면서 일반·강화 +0·끼어도 나아지지 않음(5-2).</summary>
        public static bool AutoSalvageTarget(Grade grade, int enhance, bool improves, bool autoSalvageCommon) =>
            autoSalvageCommon && grade == Grade.Common && enhance <= 0 && !improves;

        /// <summary>
        /// 줍기 판정 한 곳(5-3 표). count = 지금 가방 장비 수(넘침 포함), capacity = 가방 칸, autoReserve = ㉠ 여유 칸(0 또는 3).
        /// Manual(F): 칸이 남으면 넣음, 아니면 남김(막힘 알림은 부르는 쪽).
        /// Auto(저절로): 희귀 이상 남김 → ㉡ 대상 분해 → 개수 &lt; 칸 − 여유면 넣음 → 아니면 남김(조용히).
        /// Sweep(떠날 때): ㉡ 대상 분해 → 칸이 남으면 넣음 → 전설 넘침 칸(늘 얻음) → 일반·고급 분해 → 넘침이 남으면 넘침 칸 → 아니면 묻기.
        /// Swap(벗은 장비): 칸이 남으면 넣음 → 넘침이 남으면 넘침 칸 → 아니면 발밑에.
        /// </summary>
        public static PickupAction Decide(PickupReason reason, Grade grade, int enhance, bool improves,
                                          int count, int capacity, bool autoSalvageCommon, int autoReserve)
        {
            bool target = AutoSalvageTarget(grade, enhance, improves, autoSalvageCommon);
            switch (reason)
            {
                case PickupReason.Manual:
                    return count < capacity ? PickupAction.Take : PickupAction.Leave;
                case PickupReason.Auto:
                    if (grade >= Grade.Rare) return PickupAction.Leave;
                    if (target) return PickupAction.Salvage;
                    return count < capacity - Math.Max(0, autoReserve) ? PickupAction.Take : PickupAction.Leave;
                case PickupReason.Sweep:
                    if (target) return PickupAction.Salvage;
                    if (count < capacity) return PickupAction.Take;
                    if (grade >= Grade.Legendary) return PickupAction.TakeOverflow;
                    if (grade <= Grade.Uncommon) return PickupAction.Salvage;
                    return count < HardCap(capacity) ? PickupAction.TakeOverflow : PickupAction.Ask;
                case PickupReason.Swap:
                    if (count < capacity) return PickupAction.Take;
                    return count < HardCap(capacity) ? PickupAction.TakeOverflow : PickupAction.DropAtFeet;
                default:
                    return PickupAction.Leave;
            }
        }
    }
}
