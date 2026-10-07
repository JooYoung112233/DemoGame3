using System;using System.IO;using System.Linq;using System.Reflection;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using Demo6.Game;
public static class UiGuiEventCheck {
 public static async Task<object> Click(float x,float y){
  var go=new GameObject("Temporary IMGUI mouse event check");var driver=go.AddComponent<UiGuiMouseDriver>();
  driver.Target=Inventory.Instance;driver.At=new Vector2(x,y);
  var before=Inventory.Instance.Equipped;int count=Inventory.Instance.BagCount;
  await Task.Delay(600);
  var inv=Inventory.Instance;var selected=typeof(Inventory).GetField("_selected",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(inv) as Demo6.Core.Loot.WeaponItem;
  var result=new{x,y,driver.Phase,driver.DownControl,driver.UpControl,driver.Error,selected=selected?.DisplayName,equipped=inv.Equipped.DisplayName,previous=before.DisplayName,oldInBag=inv.Bag.Contains(before),countBefore=count,countAfter=inv.BagCount,modal=DungeonUi.Modal,blocked=PlayerInputReader.Blocked};
  UnityEngine.Object.DestroyImmediate(go);File.AppendAllText("아트/UI-정리-v2/검수/imgui-mouse-events.jsonl",Newtonsoft.Json.JsonConvert.SerializeObject(result)+"\n");return result;
 }
}
public class UiGuiMouseDriver:MonoBehaviour {
 public MonoBehaviour Target;public Vector2 At;public int Phase;public int DownControl,UpControl;public string Error;
 void OnGUI(){
  if(Phase>=2||!Target||Event.current.type!=EventType.Repaint)return;
  var saved=new Event(Event.current);var matrix=GUI.matrix;int depth=GUI.depth;
  try{GUI.matrix=Matrix4x4.identity;Event.current.type=Phase==0?EventType.MouseDown:EventType.MouseUp;Event.current.button=0;Event.current.mousePosition=At;
   Target.GetType().GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(Target,null);
   if(Phase==0)DownControl=GUIUtility.hotControl;else UpControl=GUIUtility.hotControl;
  }catch(Exception ex){Error=ex.ToString();}finally{Phase++;Event.current=saved;GUI.matrix=matrix;GUI.depth=depth;}
 }
}
