using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    public sealed class BattleTurnCard : MonoBehaviour
    {
        public Image Paper, Portrait;
        public Text Label;
        public BattleRoleBadge RoleBadge;
        public void SetRole(bool enemy, BattleCreature creature)
        {
            if (!RoleBadge) return;
            RoleBadge.gameObject.SetActive(enemy);
            RoleBadge.SetRole(CreatureRoles.For(creature));
        }
    }
}
