using System.Linq;
using UnityEngine;
using UnityEditor;
using Demo5.FrontEnd;
public static class InspectVisitor
{
 public static string Run(){var v=Object.FindAnyObjectByType<SettlementVisitorPanel>();var s=v.OurSlots[1];var p=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/InventorySlot.prefab").GetComponent<InventorySlot>();return "label="+s.Label.text+" enabled="+s.Label.enabled+" rect="+s.Label.rectTransform.rect+" preferred="+s.Label.preferredHeight+" font="+s.Label.font.name+" prefabpaper="+AssetDatabase.GetAssetPath(p.Paper.sprite)+" color="+p.Paper.color+" icon="+p.Icon.rectTransform.rect+" name="+p.Label.rectTransform.rect;}
}
