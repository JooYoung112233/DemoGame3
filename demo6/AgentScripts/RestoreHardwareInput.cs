using System;using System.Linq;using UnityEditor;using UnityEngine;using UnityEngine.InputSystem;
public static class RestoreHardwareInput {
 public static object Run(){
  if(Application.dataPath.Replace("\\","/")!="E:/personalProject/Demo3/demo6/Assets")throw new Exception("Original only");
  bool playing=EditorApplication.isPlaying,paused=EditorApplication.isPaused;
  var stale=InputSystem.devices.Where(d=>!d.native&&(d.name=="V10ReviewKeyboard"||d.name=="V10ReviewMouse")).ToArray();
  var removed=stale.Select(d=>new{d.deviceId,d.name}).ToArray();foreach(var d in stale)InputSystem.RemoveDevice(d);
  var keyboard=InputSystem.devices.OfType<Keyboard>().FirstOrDefault(d=>d.native);var mouse=InputSystem.devices.OfType<Mouse>().FirstOrDefault(d=>d.native);
  if(keyboard!=null){InputSystem.EnableDevice(keyboard);keyboard.MakeCurrent();}if(mouse!=null){InputSystem.EnableDevice(mouse);mouse.MakeCurrent();}
  var view=Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w=>w.GetType().FullName=="UnityEditor.GameView");if(view)view.Focus();
  return new{removed,keyboard=Keyboard.current?.name,keyboardNative=Keyboard.current?.native,keyboardEnabled=Keyboard.current?.enabled,mouse=Mouse.current?.name,mouseNative=Mouse.current?.native,mouseEnabled=Mouse.current?.enabled,playStatePreserved=playing==EditorApplication.isPlaying,pauseStatePreserved=paused==EditorApplication.isPaused};
 }
}
