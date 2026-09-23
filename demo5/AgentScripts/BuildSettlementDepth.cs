using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildSettlementDepth
{
 const string Folder="Assets/Art/Settlement/Geometry";
 static Material mat;
 static Vector3 V(float x,float y)=>new Vector3(x,y,0);
 static GameObject Shape(Transform parent,string name,Color color,int order,params Vector3[] points){
  var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);
  var mesh=new Mesh{name=name};mesh.vertices=points;mesh.colors=points.Select(p=>color.linear).ToArray();var tris=new List<int>();for(int i=1;i<points.Length-1;i++){tris.Add(0);tris.Add(i);tris.Add(i+1);}mesh.triangles=tris.ToArray();mesh.RecalculateBounds();
  string path=Folder+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,path);
  g.GetComponent<MeshFilter>().sharedMesh=mesh;var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=mat;r.sortingOrder=order;return g;
 }
 static void Box(Transform parent,string id,float x,float y,float w,float h,float depth,int order,Color top){
  Shape(parent,id+"Shadow",new Color(.015f,.023f,.025f,.38f),order-1,V(x-.08f,y-.10f),V(x+w+.38f,y-.26f),V(x+w+.55f,y-.50f),V(x+.05f,y-.35f));
  Shape(parent,id+"Front",new Color(top.r*.60f,top.g*.60f,top.b*.60f,1),order,V(x,y),V(x+w,y),V(x+w,y+h),V(x,y+h));
  Shape(parent,id+"Side",new Color(top.r*.46f,top.g*.46f,top.b*.46f,1),order+1,V(x+w,y),V(x+w+.22f,y+depth),V(x+w+.22f,y+h+depth),V(x+w,y+h));
  Shape(parent,id+"Top",top,order+2,V(x,y+h),V(x+w,y+h),V(x+w+.22f,y+h+depth),V(x+.22f,y+h+depth));
 }
 static GameObject Group(Transform parent,string name,int order){var g=new GameObject(name,typeof(SortingGroup));g.transform.SetParent(parent,false);g.GetComponent<SortingGroup>().sortingOrder=order;return g;}
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop Play/preserve scene");
  if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Art/Settlement","Geometry");
  mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/SettlementProp.mat");if(!mat){mat=new Material(Shader.Find("Demo5/SettlementProp"));AssetDatabase.CreateAsset(mat,"Assets/Settings/SettlementProp.mat");}
  const string path="Assets/Prefabs/Settlement/SettlementWorld.prefab";var root=PrefabUtility.LoadPrefabContents(path);
  try{
   var old=root.transform.Find("DepthAndGrowth");if(old)Object.DestroyImmediate(old.gameObject);
   var g=new GameObject("DepthAndGrowth");g.transform.SetParent(root.transform,false);var world=g.AddComponent<SettlementDevelopmentWorld>();
   var desk=Group(g.transform,"ResearchTable",675);world.ResearchTable=desk;
   var brown=new Color(.24f,.225f,.17f,1);Box(desk.transform,"ResearchLegL",.7f,-2.22f,.12f,.54f,.13f,0,brown);Box(desk.transform,"ResearchLegR",2.2f,-2.22f,.12f,.54f,.13f,0,brown);
   Box(desk.transform,"ResearchWorktop",.60f,-1.75f,1.86f,.12f,.56f,10,new Color(.35f,.315f,.23f,1));
   Shape(desk.transform,"ResearchPaper",new Color(.60f,.60f,.48f,1),14,V(.93f,-1.60f),V(1.67f,-1.60f),V(1.86f,-1.18f),V(1.12f,-1.18f));
   for(int i=0;i<3;i++)Shape(desk.transform,"ResearchNote"+i,new Color(.20f,.27f,.25f,1),15,V(1.13f,-1.31f-i*.065f),V(1.62f,-1.31f-i*.065f),V(1.63f,-1.325f-i*.065f),V(1.14f,-1.325f-i*.065f));
   Box(desk.transform,"ResearchBook",1.95f,-1.46f,.29f,.10f,.21f,15,new Color(.18f,.26f,.27f,1));
   var plan=Group(g.transform,"ResearchPlan",200);world.ResearchPlan=plan;
   Shape(plan.transform,"ResearchFloorSheet",new Color(.17f,.23f,.23f,.75f),0,V(.65f,-2.16f),V(2.40f,-2.16f),V(2.64f,-1.61f),V(.89f,-1.61f));
   var crates=Group(g.transform,"StorageCrates",735);world.StorageCrates=crates;
   Box(crates.transform,"StockCrateA",-2.30f,-2.68f,1.05f,.45f,.37f,0,new Color(.30f,.28f,.20f,1));
   Box(crates.transform,"StockCrateB",-1.16f,-2.57f,.74f,.40f,.32f,10,new Color(.25f,.28f,.23f,1));
   Shape(crates.transform,"StockStrap",new Color(.12f,.16f,.16f,1),20,V(-1.85f,-2.68f),V(-1.75f,-2.68f),V(-1.75f,-2.23f),V(-1.85f,-2.23f));
   var shelter=root.GetComponentsInChildren<SpriteRenderer>(true).First(s=>s.name=="Shelter");
   Occluder(g.transform,shelter,"NearRightPillar",new Rect(1289,471,130,304),785);
   Occluder(g.transform,shelter,"NearLeftPillar",new Rect(109,191,71,445),690);
   PrefabUtility.SaveAsPrefabAsset(root,path);
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  const string screen="Assets/Prefabs/Settlement/SettlementScreen.prefab";root=PrefabUtility.LoadPrefabContents(screen);try{var c=root.GetComponent<SettlementController>();var r=(RectTransform)c.Cabinet.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(751,-713);r.sizeDelta=new Vector2(82,82);PrefabUtility.SaveAsPrefabAsset(root,screen);}finally{PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");
  return "Native editable research table and salvage crates, build-state visibility, foreground column occlusion; storage and research now use middle/front floor.";
 }
 static void Occluder(Transform parent,SpriteRenderer original,string name,Rect pixels,int order){
  var g=new GameObject(name,typeof(SpriteRenderer));g.transform.SetParent(parent,false);g.transform.position=original.transform.position;g.transform.rotation=original.transform.rotation;g.transform.localScale=original.transform.lossyScale;
  var r=g.GetComponent<SpriteRenderer>();r.sprite=original.sprite;r.sharedMaterial=original.sharedMaterial;r.sortingOrder=order;r.color=original.color;
  var crop=g.AddComponent<ForegroundOccluder>();crop.Source=original.sprite;crop.SourcePixels=pixels;
 }
}
