using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class ConnectStorageRoom {
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop and preserve dirty scene");
  // A re-run would truncate Sites/Objects to 8 (dropping the den shelf) and restore the old storage tables.
  var current=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();
  if(current.ArrivalPanel.Loot.Sites.Length>8)throw new Exception("Superseded: den site exists; loot tables live in BuildBalance1.cs");
  AssetDatabase.Refresh();var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Art/ExpeditionArrival/storage.png");importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=100;importer.maxTextureSize=4096;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
  const string path="Assets/Prefabs/Settlement/SettlementScreen.prefab";var g=PrefabUtility.LoadPrefabContents(path);
  try {
   var c=g.GetComponent<SettlementController>();var a=c.ArrivalPanel;var n=a.Rooms;n.Storage=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ExpeditionArrival/storage.png");
   var old=a.Main.transform.Find("StorageHotspots");if(old)Object.DestroyImmediate(old.gameObject);
   var container=new GameObject("StorageHotspots",typeof(RectTransform));container.transform.SetParent(a.Main.transform,false);var cr=container.GetComponent<RectTransform>();cr.anchorMin=cr.anchorMax=cr.pivot=new Vector2(0,1);cr.sizeDelta=new Vector2(1920,1080);cr.anchoredPosition=Vector2.zero;
   var back=Object.Instantiate(n.CorridorBack,container.transform);back.name="StorageBack";back.transform.Find("Caption/Text").GetComponent<Text>().text="복도로";n.StorageBack=back;n.StorageHotspots=container;container.SetActive(false);
   var buttons=a.Objects.Take(6).ToList();var names=a.ObjectNames.Take(6).ToList();var descriptions=a.ObjectDescriptions.Take(6).ToList();var icons=a.Search.ObjectIcons.Take(6).ToList();
   string[] titles={"보관실 선반","보관실 자재"};string[] bodies={"남겨진 통조림과 물을\n선반에서 찾아봅니다.","쌓인 판자와 공구함에서\n쓸 만한 자재를 회수합니다."};
   string[] itemIds={"can","wood"};
   for(int i=0;i<2;i++){
    old=a.Main.transform.Find("StorageSearch_"+i);if(old)Object.DestroyImmediate(old.gameObject);
    var b=Object.Instantiate(a.Objects[0],a.Main.transform);b.name="StorageSearch_"+i;((RectTransform)b.transform).anchoredPosition=new Vector2(i==0?1225:1645,-370);
    var icon=c.InventoryPanel.Items.First(x=>x.Id==itemIds[i]).Icon;b.transform.Find("Icon").GetComponent<Image>().sprite=icon;b.transform.Find("Caption/Text").GetComponent<Text>().text=titles[i];b.gameObject.SetActive(false);
    buttons.Add(b);names.Add(titles[i]);descriptions.Add(bodies[i]);icons.Add(icon);
   }
   a.Objects=buttons.ToArray();a.ObjectNames=names.ToArray();a.ObjectDescriptions=descriptions.ToArray();a.Search.ObjectIcons=icons.ToArray();
   var sites=a.Loot.Sites.Take(6).ToList();sites.Add(new ExpeditionLootPanel.Site{Room=2,Drops=new[]{Drop("can",2,85),Drop("water",2,80)}});sites.Add(new ExpeditionLootPanel.Site{Room=2,Drops=new[]{Drop("wood",3,90),Drop("rope",2,80),Drop("nails",2,75)}});a.Loot.Sites=sites.ToArray();
   n.RouteLabel.fontSize=24;
   old=n.LockedDoor.transform.Find("DoorStatus");if(old)Object.DestroyImmediate(old.gameObject);var status=Object.Instantiate(a.Objects[0].transform.Find("SearchStatus").gameObject,n.LockedDoor.transform);status.name="DoorStatus";n.StorageDoorStatus=status.GetComponentInChildren<Text>();n.StorageDoorStatus.fontSize=20;((RectTransform)status.transform).sizeDelta=new Vector2(210,38);
   PrefabUtility.SaveAsPrefabAsset(g,path);
  }finally{PrefabUtility.UnloadPrefabContents(g);}
  AssetDatabase.SaveAssets();return "Storage room background, return exit and two search sites linked.";
 }
 static ExpeditionLootPanel.Drop Drop(string id,int count,int chance)=>new ExpeditionLootPanel.Drop{Id=id,Count=count,Chance=chance};
}
