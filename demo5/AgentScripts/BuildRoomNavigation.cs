using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildRoomNavigation {
 const string P="Assets/Prefabs/Settlement/",A="Assets/Art/PartySelection/",E="Assets/Art/ExpeditionPlan/";static Font font;
 static RectTransform R(string n,Transform p,float x,float y,float w,float h){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
 static Image I(string n,Transform p,float x,float y,float w,float h,string path=null){var im=R(n,p,x,y,w,h).gameObject.AddComponent<Image>();if(path!=null)im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.raycastTarget=false;return im;}
 static Text T(string n,Transform p,float x,float y,float w,float h,string s,int size=30,bool light=false){var t=R(n,p,x,y,w,h).gameObject.AddComponent<Text>();t.font=font;t.fontStyle=FontStyle.Normal;t.fontSize=size;t.text=s;t.color=light?new Color(.96f,.93f,.85f):new Color(.045f,.065f,.06f);t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;}
 static Button B(string n,Transform p,float x,float y,float w,float h,string label,int size=32){var im=I(n,p,x,y,w,h,A+"footer-paper.png");im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;T("Label",im.transform,6,0,w-12,h,label,size).alignment=TextAnchor.MiddleCenter;return b;}
 static GameObject Save(GameObject g,string name){var asset=PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");Object.DestroyImmediate(g);return asset;}
 static Sprite S(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(E+name+".png");
 public static string Build(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene changes");
  font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");AssetDatabase.Refresh();string art="Assets/Art/ExpeditionArrival/corridor.png";var imp=(TextureImporter)AssetImporter.GetAtPath(art);imp.textureType=TextureImporterType.Sprite;imp.spritePixelsPerUnit=100;imp.maxTextureSize=4096;imp.mipmapEnabled=false;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
  var room=R("CorridorHotspots",null,0,0,1920,1080);var hotspot=AssetDatabase.LoadAssetAtPath<GameObject>(P+"FacilityHotspot.prefab");
  string[] names={"오락실로","잠긴 철문","닫힌 문"};Vector2[] points={new Vector2(80,384),new Vector2(1140,355),new Vector2(1655,400)};
  for(int i=0;i<3;i++){var g=(GameObject)PrefabUtility.InstantiatePrefab(hotspot,room);g.name="Door_"+i;((RectTransform)g.transform).anchoredPosition=new Vector2(points[i].x,-points[i].y);g.transform.Find("Icon").GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/icon-"+(i==0?"exit":"storage")+".png");g.transform.Find("Caption/Text").GetComponent<Text>().text=names[i];}
  var doors=Save(room.gameObject,"CorridorHotspots");
  var root=PrefabUtility.LoadPrefabContents(P+"ExpeditionArrivalPanel.prefab");
  try{
   var c=root.GetComponent<ExpeditionArrivalPanel>();var nav=root.GetComponent<ExpeditionRoomNavigation>()??root.AddComponent<ExpeditionRoomNavigation>();c.Rooms=nav;nav.Arcade=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/ExpeditionArrival/arcade-clean.png");nav.Corridor=AssetDatabase.LoadAssetAtPath<Sprite>(art);
   var main=root.transform.Find("Main");var old=main.Find("CorridorHotspots");if(old)Object.DestroyImmediate(old.gameObject);var d=(GameObject)PrefabUtility.InstantiatePrefab(doors,main);nav.CorridorHotspots=d;nav.CorridorBack=d.transform.Find("Door_0").GetComponent<Button>();nav.LockedDoor=d.transform.Find("Door_1").GetComponent<Button>();nav.OfficeDoor=d.transform.Find("Door_2").GetComponent<Button>();d.SetActive(false);
   foreach(string n in new[]{"TurnPaper","TurnLabel","RoutePaper","RouteLabel"}){old=main.Find(n);if(old)Object.DestroyImmediate(old.gameObject);}
   I("TurnPaper",main,80,162,204,62,A+"count-paper.png");nav.TurnLabel=T("TurnLabel",main,100,162,164,62,"탐험 0턴",29);
   I("RoutePaper",main,308,162,455,62,A+"footer-paper.png");nav.RouteLabel=T("RouteLabel",main,328,162,415,62,"● 오락실 ─ 복도 · 미방문",26);
   c.Objects[3].transform.Find("Caption/Text").GetComponent<Text>().text="복도로";c.ObjectNames[3]="복도로";c.ObjectDescriptions[3]="오락실 안쪽 복도로 이어지는 문입니다.";
   PrefabUtility.SaveAsPrefabAsset(root,P+"ExpeditionArrivalPanel.prefab");
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  var scene=EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");var owner=Object.FindAnyObjectByType<SettlementController>();var navInstance=owner.ArrivalPanel.Rooms;navInstance.Background=owner.ArrivalPanel.World.transform.Find("Arcade").GetComponent<SpriteRenderer>();PrefabUtility.RecordPrefabInstancePropertyModifications(navInstance);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();return "Two rooms, reusable corridor hotspots, turn HUD and scene background reference saved.";
 }
}
