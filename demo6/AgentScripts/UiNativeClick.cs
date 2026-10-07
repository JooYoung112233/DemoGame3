using System;using System.Diagnostics;using System.Linq;using System.Reflection;using System.Runtime.InteropServices;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using Demo6.Game;
public static class UiNativeClick {
 [StructLayout(LayoutKind.Sequential)] public struct Point { public int x,y; }
 [DllImport("user32.dll")] static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 public static async Task<object> Click(float x,float y){
  var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
  var t=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(t).Cast<EditorWindow>().First();
  var target=(Rect)t.GetProperty("targetInParent",flags).GetValue(view);var oldWindow=GetForegroundWindow();GetCursorPos(out var original);
  view.Focus();SetForegroundWindow(Process.GetCurrentProcess().MainWindowHandle);await Task.Delay(250);
  float dpi=EditorGUIUtility.pixelsPerPoint;
  int sx=Mathf.RoundToInt((view.position.x+target.x+x/Screen.width*target.width)*dpi);
  int sy=Mathf.RoundToInt((view.position.y+target.y+y/Screen.height*target.height)*dpi);
  try {
   SetCursorPos(sx,sy);await Task.Delay(180);
   var observed=Mouse.current.position.ReadValue();
   // Correct the dock/tab offset from the actual input device before clicking.
   sx+=Mathf.RoundToInt((x-observed.x)*target.width/Screen.width*dpi);
   sy+=Mathf.RoundToInt(((Screen.height-y)-observed.y)*-target.height/Screen.height*dpi);
   SetCursorPos(sx,sy);await Task.Delay(150);
   var at=Mouse.current.position.ReadValue();
   mouse_event(2,0,0,0,UIntPtr.Zero);await Task.Delay(100);mouse_event(4,0,0,0,UIntPtr.Zero);await Task.Delay(200);
   var inv=Inventory.Instance;var selected=typeof(Inventory).GetField("_selected",flags).GetValue(inv) as Demo6.Core.Loot.WeaponItem;
   return new {x,y,observed=observed.ToString(),at=at.ToString(),screen=new{sx,sy,dpi},selected=selected?.DisplayName,equipped=inv.Equipped.DisplayName,modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked};
  }finally{mouse_event(4,0,0,0,UIntPtr.Zero);SetCursorPos(original.x,original.y);SetForegroundWindow(oldWindow);}
 }
}
