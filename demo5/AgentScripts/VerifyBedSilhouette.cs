using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Demo5.FrontEnd;
using Demo5.NightRun;
using Object=UnityEngine.Object;
public static class VerifyBedSilhouette {
 static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static Vector2 ScreenPoint(SettlementFacilityFocus f,Vector2 source){
  var r=(RectTransform)f.transform;var p=new Vector2(source.x*1920/1672,source.y*1080/941);
  return RectTransformUtility.WorldToScreenPoint(f.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(p.x-r.anchoredPosition.x,-p.y-r.anchoredPosition.y,0));
 }
 static MeshRenderer Mask()=>Object.FindObjectsByType<MeshRenderer>().First(r=>r.name.StartsWith("Facility silhouette"));
 static async Task Capture(string name){Directory.CreateDirectory("Assets/Screenshots/BedSilhouette");ScreenCapture.CaptureScreenshot("Assets/Screenshots/BedSilhouette/"+name+".png");await Task.Delay(500);}
 public static async Task<string> Run(){
  var roster=C.Roster;CampaignPersistence.ClearPending();PartySelectionSession.Clear();
  var campaign=new CampaignState(roster.Candidates.Select(x=>new Adventurer(x.DisplayName,x.RoleTitle,x.Description,x.Health,x.Aim,x.BagCapacity)).ToArray(),true);
  campaign.Toggle(0);campaign.Toggle(1);campaign.ConfirmParty();campaign.Settle(0);
  PartySelectionSession.Selected.AddRange(new[]{"scout","mechanic"});PartySelectionSession.Pending=campaign;
  SceneManager.LoadScene("Settlement");await Task.Delay(600);
  var c=C;c.Opening.State.Enabled=false;c.Introduction.Restore(10);c.Development.State.Bed=true;
  await Task.Delay(200);EventSystem.current.SetSelectedGameObject(null);
  var f=c.Bed.GetComponent<SettlementFacilityFocus>();var cam=f.GetComponentInParent<Canvas>().worldCamera;
  Canvas.ForceUpdateCanvases();
  foreach(var p in new[]{new Vector2(380,300),new Vector2(401,269),new Vector2(540,300),new Vector2(323,382)})
   Check(f.IsRaycastLocationValid(ScreenPoint(f,p),cam),"Missed bed silhouette at "+p);
  foreach(var p in new[]{new Vector2(450,382),new Vector2(340,265),new Vector2(387,381)})
   Check(!f.IsRaycastLocationValid(ScreenPoint(f,p),cam),"Clickable empty space at "+p);
  f.OnPointerEnter(null);await Task.Delay(250);
  Check(Mask().enabled,"No hover mask");
  Check(!Object.FindObjectsByType<LineRenderer>().Any(l=>l.name=="Facility contour · 잠자리"&&l.enabled),"Old polygon still enabled");
  Check(Mask().sortingOrder<c.StandeeBodies.Min(s=>s.sortingOrder),"Mask covers survivors");
  await Capture("01-hover");
  f.OnPointerExit(null);await Task.Delay(250);Check(!Mask().enabled,"Hover remained after exit");await Capture("02-idle");
  InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.LeftAlt));await Task.Delay(250);Check(Mask().enabled,"Alt does not reveal mask");
  InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());await Task.Delay(250);Check(!Mask().enabled,"Alt mask remained");
  foreach(var p in new[]{new Vector2(380,300),new Vector2(540,300)}) {
   var e=new PointerEventData(EventSystem.current){position=ScreenPoint(f,p),button=PointerEventData.InputButton.Left};
   var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);
   Check(hits.Count>0&&hits[0].gameObject==c.Bed.gameObject,"Bed blocked by "+hits.FirstOrDefault().gameObject?.name);
   ExecuteEvents.Execute(c.Bed.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(250);Check(c.WorkPanel.IsOpen,"Bed click did not open rest");
   Check(!Mask().enabled,"Highlight visible through popup");c.WorkPanel.Close();EventSystem.current.SetSelectedGameObject(null);await Task.Delay(150);
  }
  f.OnPointerEnter(null);await Task.Delay(250);
  return "PASS original silhouette hit tests (pillow/mattress/leg), floor and crate excluded, both beds open rest, no polygon, hover exit, Alt press/release, popup hide and behind-character order. Screenshots captured at "+Screen.width+"x"+Screen.height;
 }
 public static async Task<string> CaptureHover(){AssetDatabase.ImportAsset("Assets/Shaders/SettlementSilhouette.shader",ImportAssetOptions.ForceUpdate);var shader=Shader.Find("Demo5/SettlementSilhouette");Check(!ShaderUtil.ShaderHasError(shader),"Silhouette shader error");var f=C.Bed.GetComponent<SettlementFacilityFocus>();EventSystem.current.SetSelectedGameObject(null);f.OnPointerEnter(null);await Task.Delay(300);await Capture("03-hover-final");return "Captured final hover; shader has no compilation errors.";}
 public static async Task<string> Resolutions(){
  const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
  var assembly=typeof(Editor).Assembly;var st=assembly.GetType("UnityEditor.GameViewSizes");
  var singleton=typeof(ScriptableSingleton<>).MakeGenericType(st);
  var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
  var group=st.GetMethod("GetGroup",flags).Invoke(sizes,new[]{st.GetProperty("currentGroupType",flags).GetValue(sizes)});
  var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
  var selected=view.GetType().GetProperty("selectedSizeIndex",flags);int original=(int)selected.GetValue(view);
  try {
   foreach(var wh in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,1024),new Vector2Int(2560,1080)}){
    var sizeType=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");
    int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null), index=-1;
    for(int i=0;i<count;i++){
     var size=group.GetType().GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});
     if((int)sizeType.GetProperty("width",flags).GetValue(size)==wh.x&&(int)sizeType.GetProperty("height",flags).GetValue(size)==wh.y){index=i;break;}
    }
    if(index<0){var size=Activator.CreateInstance(sizeType,flags,null,new object[]{Enum.ToObject(kind,1),wh.x,wh.y,"Review "+wh.x+"x"+wh.y},null);group.GetType().GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});index=count;}
    selected.SetValue(view,index);view.Repaint();await Task.Delay(350);Canvas.ForceUpdateCanvases();
    Check(Screen.width==wh.x&&Screen.height==wh.y,"Resolution did not update");
    var f=C.Bed.GetComponent<SettlementFacilityFocus>();f.OnPointerEnter(null);
    var shelter=Object.FindObjectsByType<SpriteRenderer>().First(r=>r.name=="Shelter");
    foreach(var p in new[]{new Vector2(380,300),new Vector2(540,300),new Vector2(323,382)}){
     var b=shelter.sprite.bounds;
     var local=new Vector3(b.min.x+b.size.x*p.x/1672,b.max.y-b.size.y*p.y/941,0);
     var art=Camera.main.WorldToScreenPoint(shelter.transform.TransformPoint(local));
     Check(Vector2.Distance(art,ScreenPoint(f,p))<1,"Mask drift from background at "+wh);
    }
    await Task.Delay(200);await Capture("resolution-"+wh.x+"x"+wh.y);
   }
  } finally {selected.SetValue(view,original);view.Repaint();}
  return "PASS mask/background registration within 1 pixel at 1920x1080, 1280x1024 and 2560x1080; previous Game View size restored.";
 }
}
