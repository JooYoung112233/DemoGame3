using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // One cell of the battle item grid (UI11). Cells past the bag capacity show a locked X.
    public sealed class BattleItemSlot : MonoBehaviour
    {
        public Button Button;
        public Image Border, Fill, Icon;
        public Text Count;
        public GameObject Lock;
        public Color BorderColor = new Color(.62f, .64f, .6f, .75f), SelectedBorder = new Color(1, .78f, .38f), FillColor = new Color(.07f, .1f, .1f, .92f), SelectedFill = new Color(.36f, .27f, .17f, .95f);
        public void Bind(Sprite icon, int count, bool selected, bool locked, bool usable)
        {
            Lock.SetActive(locked); Icon.enabled = icon && !locked; Icon.sprite = icon; Icon.color = usable ? Color.white : new Color(1, 1, 1, .45f);
            Count.text = count > 1 || (count == 1 && icon) ? count.ToString() : ""; Count.gameObject.SetActive(!locked && icon);
            Border.color = selected ? SelectedBorder : BorderColor; Fill.color = selected ? SelectedFill : FillColor;
            Button.interactable = !locked && icon;
        }
    }
}
