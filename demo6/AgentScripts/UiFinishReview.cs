using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using UnityEngine.InputSystem;using Demo6.Game;
public static class UiFinishReview {
 public static object Run(){
  DungeonUi.Close(DungeonUi.Modal);TimeScaleService.Paused=false;Application.runInBackground=false;
  var previous=InputSystem.settings;
  if(string.IsNullOrEmpty(AssetDatabase.GetAssetPath(previous))){
   previous.hideFlags=HideFlags.DontSave;
   var defaults=ScriptableObject.CreateInstance<InputSettings>();defaults.hideFlags=HideFlags.HideAndDontSave;InputSystem.settings=defaults;
   UnityEngine.Object.DestroyImmediate(previous);
  }
  var a=typeof(Editor).Assembly;var t=a.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(t).Cast<EditorWindow>().First();
  t.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,6);
  var sizes=a.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizes);var instance=singleton.GetProperty("instance").GetValue(null);
  var group=sizes.GetMethod("GetGroup").Invoke(instance,new object[]{GameViewSizeGroupType.Standalone});
  var removed=new List<string>();
  foreach(var field in group.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)){
   if(!(field.GetValue(group) is IList list)||list.IsReadOnly)continue;
   for(int i=list.Count-1;i>=0;i--){var entry=list[i];if(entry==null)continue;
    var prop=entry.GetType().GetProperty("baseText",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
    string name=prop?.GetValue(entry) as string;
    if(name!=null&&name.StartsWith("UI Review ")){list.RemoveAt(i);removed.Add(name);}
   }
  }
  view.Repaint();
  var result=new{modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked,background=Application.runInBackground,inputSettingsAsset=AssetDatabase.GetAssetPath(InputSystem.settings),inputBackground=InputSystem.settings.backgroundBehavior.ToString(),inputEditor=InputSystem.settings.editorInputBehaviorInPlayMode.ToString(),resolutionIndex=6,removedCustomResolutions=removed,scene=SceneManager.GetActiveScene().path,sceneDirty=SceneManager.GetActiveScene().isDirty};
  File.WriteAllText("아트/UI-정리-v2/검수/environment-restored.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
 }
 public static object ConsoleStatus(){
  var a=typeof(Editor).Assembly;var t=a.GetType("UnityEditor.LogEntries");var f=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
  object[] args={0,0,0};t.GetMethod("GetCountsByType",f).Invoke(null,args);
  return new{errors=args[0],warnings=args[1],logs=args[2]};
 }
}
