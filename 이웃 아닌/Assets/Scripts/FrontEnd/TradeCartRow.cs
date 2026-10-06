using System;
using UnityEngine;
using UnityEngine.UI;

namespace Demo5.FrontEnd
{
    /// <summary>A single editable or read-only line in a barter offer.</summary>
    public sealed class TradeCartRow : MonoBehaviour
    {
        public Image Icon;
        public Text NameLabel,CountLabel,ValueLabel;
        public Button Minus,Plus;
        public string ItemId { get; private set; }
        public bool Ours { get; private set; }

        public void Bind(string id,bool ours,Sprite icon,string name,int count,int value,bool canAdd,Action remove,Action add)
        {
            ItemId=id;Ours=ours;
            if(Icon){Icon.sprite=icon;Icon.enabled=icon;}
            if(NameLabel)NameLabel.text=name;
            if(CountLabel)CountLabel.text=count+"개";
            if(ValueLabel)ValueLabel.text="가치 "+((long)count*value);
            if(Minus){Minus.onClick.RemoveAllListeners();Minus.interactable=remove!=null;if(remove!=null)Minus.onClick.AddListener(()=>remove());}
            if(Plus){Plus.onClick.RemoveAllListeners();Plus.interactable=canAdd&&add!=null;if(add!=null)Plus.onClick.AddListener(()=>add());}
        }
    }
}
