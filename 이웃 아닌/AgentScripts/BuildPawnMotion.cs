using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Demo5.FrontEnd;
public static class BuildPawnMotion{
 public static string Build(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");
  string path="Assets/Prefabs/Settlement/SettlementWorld.prefab";var g=PrefabUtility.LoadPrefabContents(path);
  try{
   var m=g.GetComponent<SettlementPawnMotion>()??g.AddComponent<SettlementPawnMotion>();m.Pawns=new Transform[2];m.Bodies=new Transform[2];m.Bases=new Transform[2];m.WorkBadges=new Transform[2];m.WorkPositions=new Transform[2];
   for(int i=0;i<2;i++){
    var pawn=g.transform.Find("Standee_"+i);m.Pawns[i]=pawn;m.Bodies[i]=pawn.Find("Body");m.Bases[i]=pawn.Find("Base");
    var old=pawn.Find("WorkBadge");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
    var badge=new GameObject("WorkBadge",typeof(SpriteRenderer));badge.transform.SetParent(pawn,false);badge.transform.position=m.Bases[i].position+new Vector3(0,2.5f,0);
    var sr=badge.GetComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/icon-work.png");sr.sharedMaterial=pawn.Find("Body").GetComponent<SpriteRenderer>().sharedMaterial;sr.sortingOrder=40;badge.transform.localScale=Vector3.one*(.4f/sr.sprite.bounds.size.y);sr.color=new Color(1,.84f,.44f);badge.SetActive(false);m.WorkBadges[i]=badge.transform;
    var stop=g.transform.Find("WorkbenchStop_"+i);if(!stop){stop=new GameObject("WorkbenchStop_"+i).transform;stop.SetParent(g.transform,false);}stop.localPosition=new Vector3(2.05f+i*1.15f,.3f,0);m.WorkPositions[i]=stop;
   }
   PrefabUtility.SaveAsPrefabAsset(g,path);
  }finally{PrefabUtility.UnloadPrefabContents(g);}
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Workbench movement and cancellation return wired; editable stops, aisle, speed and lean.";
 }
}
