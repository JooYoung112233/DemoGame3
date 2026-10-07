using System;using System.IO;using System.Reflection;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using Demo6.Game;
public static class UiPointerVerification {
 const string Root="아트/UI-정리-v2/검수";
 public static object Prepare(){
  if(!EditorApplication.isPlaying||!Inventory.Instance||DungeonUi.ModalOpen)throw new Exception("A running session with no open modal is required");
  if(GameObject.Find("UI pointer verification state"))throw new Exception("Previous pointer check has not been restored");
  var original=InputSystem.settings;var flags=original.hideFlags;original.hideFlags=HideFlags.DontSave;
  var clone=UnityEngine.Object.Instantiate(original);clone.hideFlags=HideFlags.DontSave;
  var go=new GameObject("UI pointer verification state"){hideFlags=HideFlags.HideAndDontSave};var backup=go.AddComponent<UiPointerSettingsBackup>();backup.Original=original;backup.Clone=clone;backup.Flags=flags;backup.Background=Application.runInBackground;
  InputSystem.settings=clone;clone.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;clone.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  Application.runInBackground=true;InputSystem.EnableDevice(Mouse.current);InputSystem.EnableDevice(Keyboard.current);
  return State("prepared");
 }
 public static object State(string label){
  var inv=Inventory.Instance;var selected=inv?typeof(Inventory).GetField("_selected",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(inv) as Demo6.Core.Loot.WeaponItem:null;
  var result=new{label,playing=EditorApplication.isPlaying,modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked,paused=TimeScaleService.Paused,width=Screen.width,height=Screen.height,mouseEnabled=Mouse.current.enabled,keyboardEnabled=Keyboard.current.enabled,mousePosition=Mouse.current.position.ReadValue().ToString(),leftDown=Mouse.current.leftButton.isPressed,selected=selected?.DisplayName,equipped=inv?.Equipped?.DisplayName,count=inv?inv.BagCount:-1};
  File.AppendAllText(Root+"/supported-pointer-states.jsonl",Newtonsoft.Json.JsonConvert.SerializeObject(result)+"\n");return result;
 }
 public static object Restore(){
  var go=GameObject.Find("UI pointer verification state");if(!go)throw new Exception("No pointer check to restore");var backup=Array.Find(go.GetComponents<Component>(),c=>c&&c.GetType().Name=="UiPointerSettingsBackup");var type=backup.GetType();
  var original=(InputSettings)type.GetField("Original").GetValue(backup);var clone=(InputSettings)type.GetField("Clone").GetValue(backup);
  if(!original)throw new Exception("Original input settings no longer alive; do not substitute defaults");
  InputSystem.settings=original;original.hideFlags=(HideFlags)type.GetField("Flags").GetValue(backup);if(clone)UnityEngine.Object.DestroyImmediate(clone);Application.runInBackground=(bool)type.GetField("Background").GetValue(backup);UnityEngine.Object.DestroyImmediate(go);
  // Cleanup only, not evidence of a pointer click.
  DungeonUi.Close(DungeonUi.Modal);
  return State("settings restored");
 }
}
public class UiPointerSettingsBackup:MonoBehaviour { public InputSettings Original,Clone; public HideFlags Flags;public bool Background; }
