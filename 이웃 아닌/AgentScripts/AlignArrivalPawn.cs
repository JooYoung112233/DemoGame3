using UnityEngine;
using UnityEditor;
using Demo5.FrontEnd;
public static class AlignArrivalPawn{
 public static string Apply(){string path="Assets/Prefabs/Settlement/FieldPawn.prefab";var p=PrefabUtility.LoadPrefabContents(path);try{p.transform.Find("Body").localPosition=new Vector3(0,.04f,0);PrefabUtility.SaveAsPrefabAsset(p,path);}finally{PrefabUtility.UnloadPrefabContents(p);}foreach(var c in Object.FindObjectsByType<ExpeditionArrivalPanel>(FindObjectsInactive.Include)){if(c.PawnRoot)foreach(Transform pawn in c.PawnRoot)pawn.Find("Body").localPosition=new Vector3(0,.04f,0);}AssetDatabase.SaveAssets();return "Aligned bottom-pivot standees to bases in prefab and live preview.";}
}
