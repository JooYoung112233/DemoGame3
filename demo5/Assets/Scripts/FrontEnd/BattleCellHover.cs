using UnityEngine;
using UnityEngine.EventSystems;
namespace Demo5.FrontEnd
{
    // Forwards pointer hover on a formation cell so the panel can preview moves and targets.
    public sealed class BattleCellHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        ExpeditionBattlePanel panel; int index;
        public void Bind(ExpeditionBattlePanel owner, int cell) { panel = owner; index = cell; }
        public void OnPointerEnter(PointerEventData e) { if (panel) panel.Hover(index, true); }
        public void OnPointerExit(PointerEventData e) { if (panel) panel.Hover(index, false); }
    }
}
