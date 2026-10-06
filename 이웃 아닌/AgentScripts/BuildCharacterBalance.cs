using System;
using System.Linq;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;

// Inspector data only. Art, display scales, roster order and saved campaigns are untouched.
public static class BuildCharacterBalance
{
    public static string Run()
    {
        var roster = AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        if (!roster || roster.Candidates == null) throw new InvalidOperationException("Party roster is missing.");
        Configure(roster);
        EditorUtility.SetDirty(roster);
        AssetDatabase.SaveAssets();
        return "Updated six human profiles: 5–8 HP, distinct bags/aim and effective traits. Only scout/medic start unlocked; four conditional recruits. Art/scales and save slots untouched.";
    }

    // Public so rule tests can create the same profile data without changing the asset.
    public static void Configure(PartyRoster roster)
    {
        PartyCandidate Set(string id, int health, int bag, int aim, string role, string title, string trait, string combat, string habits,
            CharacterUnlockRule unlock, int order, string hint, HumanTraits traits)
        {
            var p = roster.Candidates.Single(c => c.Id == id);
            p.Health = health; p.BagCapacity = bag; p.Aim = aim;
            p.CombatRole = role; p.TraitTitle = title; p.TraitDescription = trait; p.CombatTraitSummary = combat; p.Characteristics = habits;
            p.AvailableAtStart = unlock == CharacterUnlockRule.OpeningPair;
            p.UnlockRule = unlock; p.UnlockOrder = order; p.UnlockHint = hint;
            p.CookingTimePercent = p.CraftTimePercent = p.ResearchTimePercent = 100;
            p.Traits = traits;
            return p;
        }
        Set("scout", 6, 4, 0, "기동", "발 빠른 대응",
            "이동한 차례 명중 +10%p\n사격·근접 모두 적용",
            "이동 후 명중 +10%p\n사격·근접 모두 적용",
            "습관 · 낯선 방에서도 출구부터 본다.\n성격 · 기다리기보다 먼저 움직인다.\n운용 · 적의 예고 칸을 피해 움직이고 공격한다.",
            CharacterUnlockRule.OpeningPair, 0, "처음부터 함께하는 대원", new HumanTraits { MoveHitBonus = 10 });
        Set("medic", 6, 3, -5, "치료", "침착한 처치",
            "사용하는 붕대 회복 +1\n전투에서는 동료 처치에도 적용",
            "사용한 붕대 회복 +1\n사격 명중 −5%p",
            "습관 · 대답보다 손 떨림을 먼저 살핀다.\n약점 · 사격 명중 −5%p.\n운용 · 붕대를 자신의 가방에 챙겨 동료를 돕는다.",
            CharacterUnlockRule.OpeningPair, 1, "처음부터 함께하는 대원", new HumanTraits { BandageRecoveryBonus = 1 });
        var mechanic = Set("mechanic", 6, 5, -5, "돌파", "쓸모를 찾는 손",
            "일반 제작 시간 −20% · 연구 제외\n장갑이 남은 적에게 근접 피해 +1",
            "장갑 적 근접 피해 +1\n사격 명중 −5%p",
            "습관 · 망가진 물건의 나사부터 챙긴다.\n약점 · 사격 명중 −5%p.\n운용 · 큰 가방에 공구·재료를 나눠 담는다.",
            CharacterUnlockRule.RestoredWorkshop, 2, "작업대를 복구하면 만날 기회가 생깁니다.", new HumanTraits { ArmoredMeleeBonus = 1 });
        mechanic.CraftTimePercent = 80;
        var cook = Set("cook", 7, 4, -10, "보급", "한 끼의 힘",
            "허용된 요리 시간 −20%\n직접 먹는 휴대식량·식사 회복 +1",
            "직접 먹는 식량 회복 +1\n사격 명중 −10%p",
            "습관 · 먹기 전에 사람 수부터 센다.\n약점 · 사격 명중 −10%p.\n운용 · 식량으로 자신의 체력을 관리한다.",
            CharacterUnlockRule.RestoredKitchen, 3, "조리대를 복구하면 만날 기회가 생깁니다.", new HumanTraits { FoodRecoveryBonus = 1 });
        cook.CookingTimePercent = 80;
        Set("guard", 8, 2, 0, "버팀", "문을 지키는 사람",
            "방어 중 받는 피해 추가 −1\n물기와 예고 공격 모두 적용",
            "방어 중 피해 추가 −1\n가방은 2칸",
            "습관 · 마지막 사람이 들어온 뒤 문을 닫는다.\n약점 · 개인 가방이 2칸으로 작다.\n운용 · 피할 수 없는 공격에 방어로 버틴다.",
            CharacterUnlockRule.PreparedHousing, 4, "추가 잠자리를 준비하면 만날 기회가 생깁니다.", new HumanTraits { GuardDamageReduction = 1 });
        var researcher = Set("researcher", 5, 3, 5, "분석", "빈틈을 읽는 기록",
            "연구 시간 −30%\n빈틈 상태인 적에게 피해 +1",
            "빈틈 적 피해 +1\n사격 명중 +5%p",
            "습관 · 어제와 달라진 자리를 표시한다.\n약점 · 체력 5칸으로 오래 버티기 어렵다.\n운용 · 큰 공격을 피한 뒤 생긴 빈틈을 노린다.",
            CharacterUnlockRule.SharedRecords, 5, "연구와 기록을 이어가면 만날 기회가 생깁니다.", new HumanTraits { StaggerDamageBonus = 1 });
        researcher.ResearchTimePercent = 70;
    }
}
