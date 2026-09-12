using System.Linq;
using Live49.UI;
using TMPro;
using UnityEngine;
public static class InspectCityMap
{
    public static string Run()
    {
        Canvas.ForceUpdateCanvases();
        return string.Join("\n",GameHud.Instance.Map.GetComponentsInChildren<TMP_Text>().Where(t=>t.isTextOverflowing).Select(t=>t.text+" | "+t.rectTransform.rect.size+" | font "+t.fontSize+" preferred "+t.preferredHeight));
    }
}
