using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd
{
    // View references of the battle item drawer (UI11). ExpeditionBattlePanel fills it; layout lives in the prefab.
    public sealed class BattleItemDrawer : MonoBehaviour
    {
        public Text Title, DetailName, DetailDescription, TargetTitle, TargetNote, Message;
        public Image DetailIcon;
        public RectTransform Slots, Chips;
        public GridLayoutGroup ChipGrid;
        public BattleItemSlot SlotPrefab;
        public BattleTargetChip ChipPrefab;
        public Button Use, Cancel;
        [Min(1)] public int SlotCount = 12;
    }
}
