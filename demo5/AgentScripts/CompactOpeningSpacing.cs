using UnityEngine;using UnityEngine.UI;using UnityEditor;using Demo5.FrontEnd;
public static class CompactOpeningSpacing {
 static void Rect(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static void Apply(Transform t){Rect(t.Find("Paper"),530,260,860,560);Rect(t.Find("EventTitle"),574,294,772,64);Rect(t.Find("EventBody"),574,370,772,400);t.Find("EventBody").GetComponent<Text>().lineSpacing=.86f;}
 public static string Run(){const string path="Assets/Prefabs/Settlement/OpeningChapterPanel.prefab";var root=PrefabUtility.LoadPrefabContents(path);try{Apply(root.transform);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}var c=Object.FindAnyObjectByType<SettlementController>();Apply(c.Opening.View.transform);Canvas.ForceUpdateCanvases();return "Body: "+c.Opening.EventBody.preferredHeight+" / "+c.Opening.EventBody.rectTransform.rect.height;}}
