using System;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;
public static class V11Setup {
 public static object Open(){
  if(Application.dataPath.Replace("\\","/")!="E:/personalProject/Demo3/topdown-v11-harmony-review/Assets")throw new Exception("Isolated project only");
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
  var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if(current.isDirty)throw new Exception("Preserve dirty scene");
  EditorSceneManager.OpenScene("Assets/Scenes/DungeonTest.unity");Application.runInBackground=true;
  return new{scene="DungeonTest",pid=System.Diagnostics.Process.GetCurrentProcess().Id};
 }
}
