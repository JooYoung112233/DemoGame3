using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
public static class GroundPaintedFacilities {
 static void Shadow(Transform parent,string name,float x,float y,float width,float depth){
  var old=parent.Find(name);if(old)Object.DestroyImmediate(old.gameObject);
  const int count=40;var vertices=new Vector3[count+1];var colors=new Color[count+1];var triangles=new int[count*3];
  vertices[0]=new Vector3((x-960)/100,(540-y)/100,0);colors[0]=new Color(.025f,.035f,.035f,.42f);
  for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count;vertices[i+1]=vertices[0]+new Vector3(Mathf.Cos(a)*width/200,Mathf.Sin(a)*depth/200,0);colors[i+1]=new Color(.025f,.035f,.035f,0);triangles[i*3]=0;triangles[i*3+1]=i+1;triangles[i*3+2]=(i+1)%count+1;}
  var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();
  string path="Assets/Art/Settlement/Geometry/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,path);
  var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/SettlementProp.mat");r.sortingOrder=-20;
 }
 public static string Run(){const string path="Assets/Prefabs/Settlement/SettlementWorld.prefab";var root=PrefabUtility.LoadPrefabContents(path);try{var w=root.GetComponentInChildren<SettlementDevelopmentWorld>(true);Shadow(w.ResearchTable.transform,"ResearchContactShadow",1124,764,246,32);Shadow(w.StorageCrates.transform,"StorageContactShadow",838,791,240,24);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Separate editable soft contact shadows added below both facility sprites.";}
}


