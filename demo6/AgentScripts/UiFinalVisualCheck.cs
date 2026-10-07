using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using Demo6.Game;
public static class UiFinalVisualCheck {
 const string Root="아트/UI-정리-v2/검수";
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 public static async Task<object> Run(){
  if(!EditorApplication.isPlaying||!DungeonRoot.Instance)throw new Exception("A running DungeonTest session is required");
  if(DungeonUi.ModalOpen)throw new Exception("An existing user modal is open; left untouched");
  var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty,background=Application.runInBackground;
  bool paused=TimeScaleService.Paused,blocked=PlayerInputReader.Blocked;
  var a=typeof(Editor).Assembly;var t=a.GetType("UnityEditor.GameView");var view=Resources.FindObjectsOfTypeAll(t).Cast<EditorWindow>().First();
  var selected=t.GetProperty("selectedSizeIndex",Flags);int originalIndex=(int)selected.GetValue(view);
  var sizes=a.GetType("UnityEditor.GameViewSizes");var instance=typeof(ScriptableSingleton<>).MakeGenericType(sizes).GetProperty("instance").GetValue(null);
  var group=sizes.GetMethod("GetGroup").Invoke(instance,new object[]{GameViewSizeGroupType.Standalone});
  var sizeType=a.GetType("UnityEditor.GameViewSize");var enumType=a.GetType("UnityEditor.GameViewSizeType");
  var added=new List<object>();var captures=new List<object>();
  var lunch=UnityEngine.Object.FindObjectsByType<LunchboxEvent>().First();var open=typeof(LunchboxEvent).GetField("_open",Flags);bool wasOpen=(bool)open.GetValue(lunch);
  int bagBefore=Inventory.Instance.BagCount;var gearBefore=Inventory.Instance.Equipped;
  try{
   Application.runInBackground=true;
   foreach(var size in new[]{new[]{1280,720},new[]{1920,1080}}){
    object entry=Activator.CreateInstance(sizeType,new object[]{Enum.ToObject(enumType,1),size[0],size[1],"UI Final Follow-up "+size[0]+"x"+size[1]});
    group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{entry});added.Add(entry);int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);selected.SetValue(view,count-1);view.Repaint();await Task.Delay(350);
    DungeonUi.TryOpen(LunchboxEvent.ModalName);open.SetValue(lunch,true);await Task.Delay(150);
    string file=Path.GetFullPath(Root+"/"+size[1]+"-event-final.png");ScreenCapture.CaptureScreenshot(file);await Task.Delay(500);
    captures.Add(new{file,width=Screen.width,height=Screen.height,modal=DungeonUi.Modal,footerTextHeight=DungeonUi.Small.CalcHeight(new GUIContent("Esc: 고르지 않고 닫기"),520f),footerLabelHeight=28,footerBottomInset=24});
    open.SetValue(lunch,false);DungeonUi.Close(LunchboxEvent.ModalName);
    DungeonUi.TryOpen(Inventory.BagWindow);await Task.Delay(150);
    file=Path.GetFullPath(Root+"/"+size[1]+"-restarted-bag.png");ScreenCapture.CaptureScreenshot(file);await Task.Delay(500);
    captures.Add(new{file,width=Screen.width,height=Screen.height,modal=DungeonUi.Modal,bagCount=Inventory.Instance.BagCount,equipped=Inventory.Instance.Equipped.DisplayName});
    DungeonUi.Close(Inventory.BagWindow);
   }
  }finally{
   open.SetValue(lunch,wasOpen);DungeonUi.Close(DungeonUi.Modal);TimeScaleService.Paused=paused;PlayerInputReader.Blocked=blocked;Application.runInBackground=background;
   selected.SetValue(view,originalIndex);
   foreach(var field in group.GetType().GetFields(Flags)){if(!(field.GetValue(group) is IList list)||list.IsReadOnly)continue;for(int i=list.Count-1;i>=0;i--)if(added.Contains(list[i]))list.RemoveAt(i);}
   view.Repaint();
  }
  var result=new{captures,pointerClickTest="unavailable: no supported desktop pointer tool in current tool catalog or Unity Pipeline CLI",scene=scene.path,sceneDirtyBefore=dirty,sceneDirtyAfter=scene.isDirty,playingRestored=EditorApplication.isPlaying,viewIndexRestored=(int)selected.GetValue(view),backgroundRestored=Application.runInBackground,modal=DungeonUi.Modal,pausedRestored=TimeScaleService.Paused,blockedRestored=PlayerInputReader.Blocked,bagUnchanged=bagBefore==Inventory.Instance.BagCount,equipmentUnchanged=ReferenceEquals(gearBefore,Inventory.Instance.Equipped)};
  File.WriteAllText(Root+"/final-followup-visual.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));return result;
 }
}
