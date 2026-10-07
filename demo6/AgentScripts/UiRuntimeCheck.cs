using System;using System.Linq;using System.Reflection;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using Demo6.Game;using UnityEngine.InputSystem;
public static class UiRuntimeCheck {
 public static async Task<object> Run(){
  int before=Time.frameCount;float time=Time.time;
  var t=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var v=Resources.FindObjectsOfTypeAll(t).Cast<EditorWindow>().First();v.Focus();
  await Task.Delay(400);
  return new {pause=EditorApplication.isPaused,playing=EditorApplication.isPlaying,focused=Application.isFocused,background=Application.runInBackground,scale=Time.timeScale,before,after=Time.frameCount,time,timeAfter=Time.time,inv=Inventory.Instance!=null,down=PlayerController.Instance.IsDown,modal=DungeonUi.Modal,keyboard=Keyboard.current.enabled,keys=Keyboard.current.iKey.ReadValue(),mouse=Mouse.current.enabled};
 }
 public static object OpenBag(){DungeonUi.Close(DungeonUi.Modal);return DungeonUi.TryOpen("bag");}
}
