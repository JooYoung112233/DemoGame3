using System.IO;
using Demo5.NightRun;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class CreateVisitResources
{
    public static string Build()
    {
        if(EditorApplication.isPlaying)return "Stop play mode first";
        const string dir="Assets/Prefabs/Exploration";Directory.CreateDirectory(dir);AssetDatabase.Refresh();
        var tile=new GameObject("FormationTile",typeof(RectTransform),typeof(FormationTile));tile.GetComponent<RectTransform>().sizeDelta=new Vector2(102,48);tile.GetComponent<FormationTile>().raycastTarget=false;
        var tilePrefab=PrefabUtility.SaveAsPrefabAsset(tile,dir+"/FormationTile.prefab");Object.DestroyImmediate(tile);
        var paper=new GameObject("PaperGrain",typeof(RectTransform),typeof(PaperGrain));paper.GetComponent<RectTransform>().sizeDelta=new Vector2(278,85);paper.GetComponent<PaperGrain>().raycastTarget=false;
        var paperPrefab=PrefabUtility.SaveAsPrefabAsset(paper,dir+"/PaperGrain.prefab");Object.DestroyImmediate(paper);
        var view=Object.FindAnyObjectByType<NightRunView>();view.FormationTilePrefab=tilePrefab;view.PaperGrainPrefab=paperPrefab;EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(view.gameObject.scene);EditorSceneManager.SaveScene(view.gameObject.scene);AssetDatabase.SaveAssets();return "Created and connected FormationTile / PaperGrain prefabs";
    }
}
