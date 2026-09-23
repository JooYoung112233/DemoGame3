using System.Linq;
using UnityEngine;
using UnityEditor;
using Demo5.FrontEnd;
public static class InspectBagLayout {public static string Run(){var b=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/ExpeditionBagPanel.prefab").GetComponent<ExpeditionBagPanel>();return "memberPrefab="+AssetDatabase.GetAssetPath(b.MemberPrefab)+" slot="+AssetDatabase.GetAssetPath(b.SlotPrefab)+"\n"+string.Join("\n",b.GetComponentsInChildren<RectTransform>(true).Select(r=>r.name+" parent="+r.parent?.name+" pos="+r.anchoredPosition+" size="+r.sizeDelta));}}
