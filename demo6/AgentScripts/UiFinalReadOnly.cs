using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using Demo6.Game;
public static class UiFinalReadOnly {
 public static object State(){
  var a=typeof(Editor).Assembly;var t=a.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(t).Cast<EditorWindow>().FirstOrDefault();
  var result=new {playing=EditorApplication.isPlaying,pausedEditor=EditorApplication.isPaused,compiling=EditorApplication.isCompiling,modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked,pausedGame=TimeScaleService.Paused,background=Application.runInBackground,screen=new{Screen.width,Screen.height},viewIndex=view?(int)t.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(view):-1,
   scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>new {SceneManager.GetSceneAt(i).path,SceneManager.GetSceneAt(i).isDirty}).ToArray(),
   root=DungeonRoot.Instance!=null,inventory=Inventory.Instance!=null,bagCount=Inventory.Instance?Inventory.Instance.BagCount:-1,equipped=Inventory.Instance?.Equipped?.DisplayName,
   eventCount=UnityEngine.Object.FindObjectsByType<LunchboxEvent>().Length,
   textures=new[]{"UI/Skin/panel","UI/Skin/slot","UI/Skin/button","UI/Items/wpn_longsword","UI/Items/wpn_greatsword","UI/Items/wpn_twinblades"}.Select(p=>{var tex=Resources.Load<Texture2D>(p);return new{path=p,loaded=tex!=null,width=tex?tex.width:0,height=tex?tex.height:0};}).ToArray()
  };
  File.WriteAllText("아트/UI-정리-v2/검수/final-followup-state.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
 }
}
