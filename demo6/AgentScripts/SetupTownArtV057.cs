using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using Newtonsoft.Json;
public static class SetupTownArtV057
{
 public static object OpenTown()
 {
  if(EditorApplication.isPlaying)throw new Exception("Play must be stopped before scene change");
  for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unsaved scene");
  var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Town.unity");return new{scene=scene.path,dirty=scene.isDirty};
 }
 public static object Run()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play before importer setup");
  var paths=Directory.GetFiles("Assets/Resources/TownArtV057","*.png",SearchOption.AllDirectories);
  foreach(var raw in paths)
  {
   string path=raw.Replace('\\','/');var ti=AssetImporter.GetAtPath(path) as TextureImporter;if(!ti)throw new Exception("Missing importer "+path);
   bool npc=path.Contains("/npc/");ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;
   ti.spritePixelsPerUnit=npc?192f:128f;ti.spritePivot=npc?new Vector2(.5f,.453125f):new Vector2(.5f,.5f);
   var st=new TextureImporterSettings();ti.ReadTextureSettings(st);st.spriteAlignment=(int)SpriteAlignment.Custom;st.spritePivot=ti.spritePivot;st.spriteMeshType=SpriteMeshType.FullRect;ti.SetTextureSettings(st);
   ti.alphaIsTransparency=true;ti.mipmapEnabled=false;ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=2048;ti.SaveAndReimport();
  }
  var report=paths.Select(x=>{string p=x.Replace('\\','/');var s=AssetDatabase.LoadAssetAtPath<Sprite>(p);return new{path=p,size=new[]{s.rect.width,s.rect.height},ppu=s.pixelsPerUnit,pivot=new[]{s.pivot.x,s.pivot.y}};}).ToArray();
  File.WriteAllText("검증/town-npc-v057/import-settings.json",JsonConvert.SerializeObject(report,Formatting.Indented));return new{count=report.Length};
 }
}
