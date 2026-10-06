using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class ConnectCorridorSearch
{
 public static string Run()
 {
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop and preserve dirty scene");
  const string path="Assets/Prefabs/Settlement/SettlementScreen.prefab";
  var root=PrefabUtility.LoadPrefabContents(path);
  try {
   var a=root.GetComponent<SettlementController>().ArrivalPanel;
   // A re-run would truncate Sites/Objects to 6 (dropping the storage and den sites) and restore the old corridor tables.
   if(a.Loot.Sites.Length>6)throw new Exception("Superseded: storage/den sites exist; loot tables live in BuildBalance1.cs");
   var buttons=a.Objects.Take(4).ToList();
   var names=a.ObjectNames.Take(4).ToList();var descriptions=a.ObjectDescriptions.Take(4).ToList();
   string[] titles={"복도 배전함","복도 보관 상자"};
   string[] bodies={"지렛대로 덮개를 열어\n고철과 못을 회수합니다.","도구 없이 상자를 열어\n생활 물자를 살펴봅니다."};
   var icons=a.Search.ObjectIcons.Take(3).Concat(new Sprite[]{null,AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ExpeditionPlan/parts.png"),a.Search.ObjectIcons[0]}).ToArray();
   for(int i=0;i<2;i++) {
    var old=a.Main.transform.Find("CorridorSearch_"+i);if(old)Object.DestroyImmediate(old.gameObject);
    var b=Object.Instantiate(a.Objects[0],a.Main.transform);b.name="CorridorSearch_"+i;
    var r=(RectTransform)b.transform;r.anchoredPosition=new Vector2(i==0?235:1390,i==0?-280:-395);
    b.transform.Find("Caption/Text").GetComponent<Text>().text=titles[i];
    b.transform.Find("Icon").GetComponent<Image>().sprite=icons[i+4];
    b.gameObject.SetActive(false);buttons.Add(b);names.Add(titles[i]);descriptions.Add(bodies[i]);
   }
   a.Objects=buttons.ToArray();a.ObjectNames=names.ToArray();a.ObjectDescriptions=descriptions.ToArray();a.Search.ObjectIcons=icons;
   var sites=a.Loot.Sites.Take(3).ToList();foreach(var s in sites)s.Room=0;
   sites.Add(new ExpeditionLootPanel.Site{Room=-1,Drops=Array.Empty<ExpeditionLootPanel.Drop>()});
   sites.Add(new ExpeditionLootPanel.Site{Room=1,RequiredTool="prybar",Drops=new[]{Drop("scrap",2,85),Drop("nails",2,80)}});
   sites.Add(new ExpeditionLootPanel.Site{Room=1,Drops=new[]{Drop("raw-water",2,85),Drop("cloth",2,75),Drop("can",1,60)}});
   a.Loot.Sites=sites.ToArray();
   PrefabUtility.SaveAsPrefabAsset(root,path);
  } finally {PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();return "Two corridor sites connected; existing site IDs and door preserved.";
 }
 static ExpeditionLootPanel.Drop Drop(string id,int count,int chance)=>new ExpeditionLootPanel.Drop{Id=id,Count=count,Chance=chance};
}
