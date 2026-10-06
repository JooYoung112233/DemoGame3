using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    public sealed partial class ExpeditionBattlePanel
    {
        [Header("전투 역할 · 기존 상세 카드")]
        public BattleRoleBadge ActorRoleBadge, TargetRoleBadge;
        public Text ActorRoleLabel, TargetRoleLabel, ActorTactic, TargetTactic;

        void RefreshRoleDetails(int actor, int target)
        {
            RoleDetails(actor,ActorRoleBadge,ActorRoleLabel,ActorTactic,false);
            RoleDetails(target,TargetRoleBadge,TargetRoleLabel,TargetTactic,true);
            // The actor's passive guard reduction and item recovery must match the rule engine.
            if (actor>=0 && actor<State.Units.Count && !State.Units[actor].Enemy)
                Describe(Guard,"Description",string.Format(GuardDescription,Rules.GuardHitPenalty,State.GuardReductionFor(actor,true)));
        }
        void RoleDetails(int index,BattleRoleBadge badge,Text label,Text tactic,bool target)
        {
            bool exists=index>=0 && index<State.Units.Count;
            if(badge)badge.gameObject.SetActive(exists && State.Units[index].Enemy);
            if(!exists)
            {
                if(label)label.text="";
                if(tactic)tactic.text="";
                return;
            }
            var unit=State.Units[index];
            if(unit.Enemy)
            {
                var role=CreatureRoles.For(unit.Creature);
                if(badge)badge.SetRole(role);
                // Only the selected player's attack has a meaningful directional/ranged armor value.
                string defense=target && !Busy && State.Current!=null && !State.Current.Enemy
                    ? " · 방어 " + State.ArmorOf(index,Ranged)
                    : "";
                if(label)label.text=CreatureRoles.Name(role)+defense;
                if(tactic)tactic.text=CreatureTactic(unit.Creature);
            }
            else
            {
                var owner=arrival?arrival.GetComponentInParent<SettlementController>(true):null;
                var candidate=owner && owner.Roster ? owner.Roster.Candidates.FirstOrDefault(c=>c!=null && c.DisplayName==unit.Name):null;
                if(label)label.text=candidate?.CombatRole??"대원";
                if(tactic)tactic.text=candidate?.CombatTraitSummary??"";
            }
        }
        public static string CreatureTactic(BattleCreature creature)
        {
            if(creature==null)return CreatureRoles.Summary(CreatureRole.Melee);
            // Two explicit lines keep conditional attacks (especially the listener) distinct from constant range.
            string summary=!string.IsNullOrEmpty(creature.Trait)?creature.Trait:creature.RoleSummary;
            return summary+(string.IsNullOrEmpty(creature.Counter)?"":"\n"+creature.Counter);
        }
    }
}
