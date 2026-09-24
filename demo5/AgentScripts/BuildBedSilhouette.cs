using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;

// Editable vector selection geometry, not replacement furniture artwork.
// Points use a 4x inspection of source pixels (crop origin 300,245).
// Keep source shelter-unlit.png at its original 1672x941 resolution.
public static class BuildBedSilhouette {
 static Vector2[] Trace(params float[] xy) => Enumerable.Range(0,xy.Length/2)
  .Select(i=>new Vector2(300+xy[i*2]/4f,245+xy[i*2+1]/4f)).ToArray();
 static Vector2[][] Shapes() => new[] {
  Trace(270,147,269,134,282,107,296,85,303,61,314,52,346,49,383,45,420,44,465,46,511,50,552,56,583,64,595,74,600,85,594,116,594,122,626,122,637,127,641,140,639,193,631,214,630,244,634,286,633,318,629,344,618,345,615,309,613,279,599,335,582,387,563,441,542,481,534,487,528,480,519,453,510,466,506,510,503,558,498,565,479,565,475,560,474,508,473,442,114,442,109,491,107,551,104,565,84,566,80,560,78,478,74,444,67,465,59,475,49,474,44,466,47,422,51,388,61,367,81,344,114,296,143,248,170,207,190,175,205,166,245,160,254,129,270,125),
  Trace(833,144,830,135,838,112,850,83,850,69,859,64,856,58,878,48,926,44,979,40,1030,39,1090,44,1130,51,1158,59,1171,71,1174,87,1170,119,1199,119,1209,124,1213,142,1218,190,1224,234,1221,251,1215,258,1218,313,1217,354,1206,360,1198,340,1196,329,1191,410,1186,477,1182,489,1174,491,1165,465,1158,448,1152,455,1151,558,1147,565,1129,565,1125,560,1126,438,716,438,716,490,714,559,708,565,692,565,688,559,689,449,682,441,677,478,670,491,662,490,657,476,660,421,665,383,676,358,702,312,729,264,754,212,769,181,782,169,797,164,803,135,811,123,834,120)
 };
 static bool Inside(Vector2 q,Vector2[] p) {
  bool inside=false;
  for(int i=0,j=p.Length-1;i<p.Length;j=i++)
   if((p[i].y>q.y)!=(p[j].y>q.y)&&q.x<(p[j].x-p[i].x)*(q.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)inside=!inside;
  return inside;
 }
 static float Distance(Vector2 q,Vector2[] p) {
  float d=float.MaxValue;
  for(int i=0,j=p.Length-1;i<p.Length;j=i++) {
   var v=p[i]-p[j];var a=q-p[j];
   var t=Mathf.Clamp01(Vector2.Dot(a,v)/v.sqrMagnitude);
   d=Mathf.Min(d,(a-v*t).sqrMagnitude);
  }
  return Mathf.Sqrt(d);
 }
 public static string Run() {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before rebuilding the prefab.");
  var shapes=Shapes();var all=shapes.SelectMany(p=>p).ToArray();
  int x=Mathf.FloorToInt(all.Min(p=>p.x))-5,y=Mathf.FloorToInt(all.Min(p=>p.y))-5;
  int w=Mathf.CeilToInt(all.Max(p=>p.x))+6-x,h=Mathf.CeilToInt(all.Max(p=>p.y))+6-y;
  const string folder="Assets/Art/Settlement/SelectionMasks";
  Directory.CreateDirectory(folder);
  const string maskPath=folder+"/Beds.asset";
  var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
  if(!texture){texture=new Texture2D(w,h,TextureFormat.RGBA32,false,true);AssetDatabase.CreateAsset(texture,maskPath);}
  else texture.Reinitialize(w,h,TextureFormat.RGBA32,false);
  texture.name="Beds — original silhouette distance mask";texture.filterMode=FilterMode.Bilinear;texture.wrapMode=TextureWrapMode.Clamp;
  var pixels=new Color32[w*h];
  for(int yy=0;yy<h;yy++)for(int xx=0;xx<w;xx++) {
   var q=new Vector2(x+xx+.5f,y+h-yy-.5f);
   float d=shapes.Min(p=>Distance(q,p));bool inside=shapes.Any(p=>Inside(q,p));
   float signed=inside?d:-d;
   byte red=(byte)Mathf.RoundToInt(Mathf.Clamp01(.5f+signed/32)*255);
   byte alpha=(byte)Mathf.RoundToInt(Mathf.Clamp01(.5f+signed)*255);
   pixels[yy*w+xx]=new Color32(red,red,red,alpha);
  }
  texture.SetPixels32(pixels);texture.Apply(false,false);EditorUtility.SetDirty(texture);
  const string matPath="Assets/Settings/SettlementSilhouette.mat";
  var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
  if(!mat){mat=new Material(Shader.Find("Demo5/SettlementSilhouette"));AssetDatabase.CreateAsset(mat,matPath);}
  const string prefabPath="Assets/Prefabs/Settlement/SettlementScreen.prefab";
  var root=PrefabUtility.LoadPrefabContents(prefabPath);
  try {
   var c=root.GetComponent<SettlementController>();var f=c.Bed.GetComponent<SettlementFacilityFocus>();
   var r=(RectTransform)c.Bed.transform;
   var scale=new Vector2(1920f/1672,1080f/941);
   r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
   r.anchoredPosition=new Vector2(x*scale.x,-y*scale.y);r.sizeDelta=new Vector2(w*scale.x,h*scale.y);
   f.Paths=shapes.Select(p=>new SettlementFacilityFocus.Path { Points=p.Select(v=>Vector2.Scale(v-new Vector2(x,y),scale)).ToArray() }).ToArray();
   f.SilhouetteMask=texture;f.SilhouetteMaterial=mat;f.HoverOpacity=.88f;
   if(f.Help)f.Help.text="시설 위에 마우스 · Alt: 모든 시설 강조";
   PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
  } finally {PrefabUtility.UnloadPrefabContents(root);}
  AssetDatabase.SaveAssets();
  EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");
  return "Beds use original-pixel silhouette geometry ("+all.Length+" points), "+w+"x"+h+" signed distance mask, no background or furniture art replacement.";
 }
}
