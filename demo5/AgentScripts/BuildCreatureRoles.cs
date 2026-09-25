using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;

// Run after BuildCreatureBattle/BuildBalance1. Changes only role, balance and encounter eligibility.
// Existing art, dimensions, sounds, Attack/Gait, depths and cover behaviour are deliberately preserved.
public static class BuildCreatureRoles
{
    const string Path = "Assets/Data/BattleCreatures.asset";
    sealed class Spec
    {
        public string Id, Trait, Counter;
        public CreatureRole Role;
        public CreatureAttack Attack;
        public bool SuppressReinforcements;
        public int Hp, Damage, Armor, Front, Shot, Evasion, Hit = 70, Tier = 1, Weight = 2;
    }
    static readonly Spec[] Specs =
    {
        new Spec { Id = "01-door-bearer", Role = CreatureRole.Defender, Attack = CreatureAttack.Shove, Hp = 8, Damage = 1, Front = 1,
            Trait = "정면 방어 +1 · 뒤의 적을 엄폐", Counter = "옆 줄에서 공격하면 정면 방어를 피합니다" },
        new Spec { Id = "02-listener", Role = CreatureRole.Ranged, Attack = CreatureAttack.Listen, Hp = 6, Damage = 2, Hit = 75, Tier = 0, Weight = 0, SuppressReinforcements = true,
            Trait = "사격한 칸을 추적 · 조용하면 근접 공격", Counter = "총을 쏜 뒤 표시된 칸에서 이동하세요" },
        new Spec { Id = "03-under-table", Role = CreatureRole.Assault, Attack = CreatureAttack.Pounce, Hp = 5, Damage = 2, Weight = 3,
            Trait = "가까운 칸 도약 · 방어 없음", Counter = "표시된 칸에서 벗어나고 착지 후 공격하세요" },
        new Spec { Id = "04-seam-hound", Role = CreatureRole.Assault, Attack = CreatureAttack.Charge, Hp = 4, Damage = 3, Weight = 3,
            Trait = "빠른 돌진 · 공격 뒤 한 턴 빈틈", Counter = "옆 줄로 피한 뒤 빈틈을 공격하세요" },
        new Spec { Id = "05-laundry", Role = CreatureRole.Disruptor, Attack = CreatureAttack.Veil, Hp = 5, Damage = 1, Shot = 1, Tier = 2,
            Trait = "한 줄 시야 가림 · 사격 피해 -1", Counter = "천이 펼쳐진 줄에서 벗어나세요" },
        new Spec { Id = "06-meter-keeper", Role = CreatureRole.Ranged, Attack = CreatureAttack.Wire, Hp = 4, Damage = 2, Tier = 2,
            Trait = "중열부터 인접한 두 칸 감전", Counter = "연결된 두 칸에서 벗어나고 가까이 붙으세요" },
        new Spec { Id = "07-moth-nest", Role = CreatureRole.Disruptor, Attack = CreatureAttack.Swarm, Hp = 4, Damage = 1, Evasion = 20,
            Trait = "붙은 대원까지 공격 · 사격 명중 -20%p", Counter = "대원 사이를 벌리세요 · 근접에는 회피 없음" },
        new Spec { Id = "08-puddle", Role = CreatureRole.Disruptor, Attack = CreatureAttack.Grab, Hp = 6, Damage = 1, Shot = 1, Tier = 2,
            Trait = "다음 이동 묶음 · 사격 피해 -1", Counter = "물결이 번진 칸을 피하세요 · 묶이면 방어" },
        new Spec { Id = "09-stairback", Role = CreatureRole.Defender, Attack = CreatureAttack.Collapse, Hp = 8, Damage = 2, Armor = 1, Tier = 3, Weight = 1,
            Trait = "방어 1 · 무너진 뒤 방어 0", Counter = "앞의 두 칸을 비우고 무너진 뒤 공격하세요" },
        new Spec { Id = "10-twin-coat", Role = CreatureRole.Assault, Attack = CreatureAttack.Twin, Hp = 6, Damage = 2, Tier = 2,
            Trait = "두 칸 공격 · 바로 뒤도 노림", Counter = "한 칸 후퇴만 하지 말고 두 표시를 확인하세요" },
        new Spec { Id = "11-root-receiver", Role = CreatureRole.Support, Attack = CreatureAttack.Broadcast, Hp = 4, Damage = 0, Tier = 3, Weight = 1,
            Trait = "이동·직접 피해 없음 · 방송 소음 +3", Counter = "소음으로 증원을 부르기 전에 쓰러뜨리세요" },
        new Spec { Id = "12-bellied-cart", Role = CreatureRole.Assault, Attack = CreatureAttack.Ram, Hp = 6, Damage = 2, Tier = 3, Weight = 1,
            Trait = "한 줄 밀침 · 부딪히면 피해 +1", Counter = "옆 줄로 피하세요 · 방어하면 밀리지 않습니다" },
    };

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before applying creature roles.");
        var roster = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>(Path);
        if (!roster) throw new InvalidOperationException("Missing " + Path);
        // Validate the complete roster before changing any entry, so a missing type cannot leave a half-written balance.
        foreach (var s in Specs)
        {
            var matches = roster.Creatures.Where(c => c != null && c.Id == s.Id).ToArray();
            if (matches.Length != 1 || matches[0].Attack != s.Attack)
                throw new InvalidOperationException("Expected one unchanged attack for " + s.Id);
        }
        Undo.RecordObject(roster, "Apply creature roles and regional balance");
        foreach (var s in Specs)
        {
            var c = roster.Find(s.Id);
            c.Role = s.Role; c.Health = s.Hp; c.Damage = s.Damage;
            c.Armor = s.Armor; c.FrontArmor = s.Front; c.ShotArmor = s.Shot; c.ShotEvasion = s.Evasion; c.HitChance = s.Hit;
            c.MinRegionTier = s.Tier; c.Weight = s.Weight; c.Trait = s.Trait; c.Counter = s.Counter;
            c.SuppressReinforcements = s.SuppressReinforcements;
        }
        EditorUtility.SetDirty(roster); AssetDatabase.SaveAssets();
        return Validate();
    }

    public static string Validate()
    {
        var roster = AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>(Path);
        if (!roster) throw new InvalidOperationException("Missing " + Path);
        var lines = new List<string>();
        foreach (var s in Specs)
        {
            var c = roster.Find(s.Id);
            if (c == null || c.Role != s.Role || c.Health != s.Hp || c.Damage != s.Damage || c.Armor != s.Armor || c.FrontArmor != s.Front
                || c.ShotArmor != s.Shot || c.ShotEvasion != s.Evasion || c.HitChance != s.Hit || c.MinRegionTier != s.Tier || c.Weight != s.Weight
                || c.Trait != s.Trait || c.Counter != s.Counter || c.Attack != s.Attack || c.SuppressReinforcements != s.SuppressReinforcements)
                throw new InvalidOperationException("Creature balance readback mismatch: " + s.Id);
            lines.Add(c.Name + " · " + c.RoleName + " · HP " + c.Health + " · 피해 " + c.Damage + " · 지역 " + c.MinRegionTier);
        }
        if (roster.Pool.Any() || roster.Lineup(3, () => 0).Count != 0)
            throw new InvalidOperationException("The opening site must use the infected fallback, not random creatures.");
        return "PASS · 12 creature profiles; opening pool empty\n" + string.Join("\n", lines);
    }
}
