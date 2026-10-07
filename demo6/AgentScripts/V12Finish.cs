using System;using System.IO;using UnityEngine;using UnityEditor;
public static class V12Finish {
 public static object Close(){
  const string dir="E:/personalProject/Demo3/demo6/검증/승인형상-대표샘플-v12";
  if(Application.dataPath.Replace("\\","/")!="E:/personalProject/Demo3/topdown-v11-harmony-review/Assets"||System.Diagnostics.Process.GetCurrentProcess().Id!=int.Parse(File.ReadAllText(dir+"/review-pid.txt")))throw new Exception("Owned review session only");
  if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Preserve Play or dirty scene");
  File.WriteAllText(dir+"/review-final-state.json","{\"playStopped\":true,\"sceneDirty\":false,\"temporaryArtRestored\":true,\"closeRequested\":true}");
  EditorApplication.delayCall+=()=>EditorApplication.Exit(0);
  return "Owned isolated editor will close; no scene saved";
 }
}
