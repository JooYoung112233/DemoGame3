using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // Ally card for item targeting and the before/after treatment preview (UI11).
    public sealed class BattleTargetChip : MonoBehaviour
    {
        public Button Button;
        public Image Paper, Portrait, HealthFill;
        public Text Name, Health;
        public Color Normal = new Color(.95f, .92f, .82f), Selected = new Color(1, .78f, .38f), Disabled = new Color(.62f, .62f, .58f);
        public void Bind(Sprite portrait, string label, int health, int maximum, bool selected, bool enabled)
        {
            Portrait.sprite = portrait; Portrait.enabled = portrait; Name.text = label; Health.text = health + " / " + maximum;
            SegmentedHealthGraphic.Set(HealthFill,health,maximum);
            Paper.color = !enabled ? Disabled : selected ? Selected : Normal;
            if (Button) Button.interactable = enabled;
        }
    }
}
