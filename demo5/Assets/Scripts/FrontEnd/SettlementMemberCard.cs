using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    public sealed class SettlementMemberCard:MonoBehaviour
    {
        public Button Button,BagButton;
        public Image Portrait,HealthFill;
        public Text Name,Status;
        public void Bind(Adventurer member,Sprite portrait,UnityEngine.Events.UnityAction inspect,UnityEngine.Events.UnityAction bag)
        {
            Name.text=member.Name;Portrait.sprite=portrait;Status.text=member.Health>0?"대기":"회복 필요";
            HealthFill.fillAmount=(float)member.Health/member.MaxHealth;
            Button.onClick.RemoveAllListeners();Button.onClick.AddListener(inspect);
            BagButton.onClick.RemoveAllListeners();BagButton.onClick.AddListener(bag);
        }
    }
}
