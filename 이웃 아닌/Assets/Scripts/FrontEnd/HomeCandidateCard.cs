using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    public sealed class HomeCandidateCard:MonoBehaviour
    {
        public Button Button;
        public Text Name,Supplies,Ammo,Recovery,Description;
        public GameObject SelectedBorder,Check;
        public void Bind(HomeSite site,bool selected,UnityEngine.Events.UnityAction onClick)
        {
            Name.text=site.Name;Supplies.text="?";Ammo.text="?";Recovery.text="+"+site.Recovery;
            Description.text=site.Description;SelectedBorder.SetActive(selected);Check.SetActive(selected);
            Button.onClick.RemoveAllListeners();Button.onClick.AddListener(onClick);
        }
    }
}
