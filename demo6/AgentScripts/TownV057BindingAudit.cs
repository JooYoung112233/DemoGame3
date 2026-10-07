using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using Newtonsoft.Json;
public static class TownV057BindingAudit
{
 public static object ReadOnly()
 {
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  var assets=Directory.GetFiles("Assets/Resources/TownArtV057/modules","*.png").Select(p=>{p=p.Replace('\\','/');var s=Resources.Load<Sprite>("TownArtV057/modules/"+Path.GetFileNameWithoutExtension(p));return new{path=p,guid=AssetDatabase.AssetPathToGUID(p),loadedSprite=s?s.name:null,loadedSpritePath=AssetDatabase.GetAssetPath(s),texturePath=s?AssetDatabase.GetAssetPath(s.texture):null,ppu=s?s.pixelsPerUnit:0};}).ToArray();
  var result=new{utc=DateTime.UtcNow,playing=Application.isPlaying,scene=scene.path,dirty=scene.isDirty,roots=scene.GetRootGameObjects().Select(x=>new{x.name,prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x)}).ToArray(),assets,method="Read-only original Editor scene and Resources bindings. No scene/Play/save/source mutation."};
  File.WriteAllText("검증/town-npc-v057/visual-audit/editor-bindings.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}
