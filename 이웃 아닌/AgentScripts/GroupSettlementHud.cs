using System;using System.Linq;using UnityEngine;using UnityEngine.UI;using UnityEditor;using UnityEditor.SceneManagement;using Demo5.FrontEnd;
public static class GroupSettlementHud {
 static void R(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static void Label(Button b,int size,float inset=14){var t=b.GetComponentInChildren<Text>();t.fontSize=size;t.alignment=TextAnchor.MiddleCenter;var r=t.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(inset,0);r.offsetMax=new Vector2(-14,0);}
 // 2026-09-25: the supplies slot was retired (BuildRetireSupplies). Once the ResourceHud has 2 slots (no Value_0), keep it at 1430,24,278,72:
 // right edge 1708, 16px before the menu at 1724. The old 3-slot 1298,24,410,72 applies only to a HUD that still has Value_0.
 static bool SuppliesRetired(){var hud=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/ResourceHud.prefab");return hud&&!hud.transform.Find("Value_0");}
 public static string Run(){if(EditorApplication.isPlaying)throw new Exception("Stop isolated editor first");if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Active scene has unsaved edits; preserve them first.");bool retired=SuppliesRetired();const string path="Assets/Prefabs/Settlement/SettlementScreen.prefab";var root=PrefabUtility.LoadPrefabContents(path);try{var c=root.GetComponent<SettlementController>();R(c.Location,240,24,430,42);c.Location.fontSize=29;R(c.Advance,240,80,220,48);Label(c.Advance,26,48);
 if(retired)R(c.Main.transform.Find("ResourceHud"),1430,24,278,72);else R(c.Main.transform.Find("ResourceHud"),1298,24,410,72);R(c.GameMenu.OpenButton,1724,24,168,72);Label(c.GameMenu.OpenButton,28);
 R(c.VisitorPanel.OpenButton,1414,696,414,56);Label(c.VisitorPanel.OpenButton,24);
 R(c.CraftPanel.HousingButton,1414,766,264,56);Label(c.CraftPanel.HousingButton,22);
 R(c.Journal,1694,766,134,56);Label(c.Journal,24,42);foreach(var icon in c.Journal.GetComponentsInChildren<Image>())if(icon.transform!=c.Journal.transform)R(icon,10,15,26,26);
 PrefabUtility.SaveAsPrefabAsset(root,path);
 }finally{PrefabUtility.UnloadPrefabContents(root);}AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Grouped HUD: time/place left, resources ("+(retired?"2 slots · 1430,24,278,72":"3 slots · 1298,24,410,72")+")/menu right; visitor/housing/journal next to residents. Facility/map hotspots and gameplay unchanged.";}
}
