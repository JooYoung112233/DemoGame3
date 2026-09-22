using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyPawnMotion
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Click(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);}

 public static async Task<string> Record(){
 var c=Object.FindAnyObjectByType<SettlementController>();var m=Object.FindAnyObjectByType<SettlementPawnMotion>();var p=c.CraftPanel;
 int minute=c.Campaign.MinuteOfDay,supply=c.Campaign.Supplies;var home=m.Pawns[0].position;var other=m.Pawns[1].position;
 p.Close();Click(c.Workbench);await Task.Delay(180);Click(p.RecipeRows[1].Button);Click(p.WorkerRows[0].Button);
 Click(p.RecipeRows[2].Button);Check(p.WorkerRows[0].Check.gameObject.activeSelf,"Worker lost on item change");
 Click(p.Tabs[1]);await Task.Delay(150);Check(p.WorkerRows[0].Check.gameObject.activeSelf,"Worker lost on category change");
 Click(p.Tabs[0]);await Task.Delay(150);Click(p.RecipeRows[1].Button);Check(p.Confirm.interactable,"Retained worker cannot confirm");
 string dir="Assets/Screenshots/Movement/work-frames";Directory.CreateDirectory(dir);var cam=Camera.main;var rt=new RenderTexture(960,540,24);var tex=new Texture2D(960,540,TextureFormat.RGB24,false);var previous=cam.targetTexture;var active=RenderTexture.active;var stamps=new List<string>();var clock=System.Diagnostics.Stopwatch.StartNew();bool started=false;
 try{
  for(int i=0;i<56;i++){
   if(i==5){Click(p.Confirm);started=true;}
   Canvas.ForceUpdateCanvases();cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,960,540),0,0);tex.Apply();File.WriteAllBytes(dir+"/frame-"+i.ToString("000")+".png",tex.EncodeToPNG());stamps.Add(clock.ElapsedMilliseconds.ToString());cam.targetTexture=previous;RenderTexture.active=active;await Task.Delay(70);
  }
 }finally{cam.targetTexture=previous;RenderTexture.active=active;Object.Destroy(rt);Object.Destroy(tex);}
 File.WriteAllLines(dir+"/times.txt",stamps);Check(started&&!m.IsMoving,"Movement did not finish");Check(Vector3.Distance(m.Bases[0].position,m.WorkPositions[0].position)<.02f,"Foot not at work anchor");Check(m.Pawns[1].position==other,"Other member moved");Check(c.Main.GetComponentsInChildren<SettlementWorkBubble>().First(x=>x.MemberIndex==0).Visual.activeSelf,"Work bubble absent");Check(c.Campaign.MinuteOfDay==minute&&c.Campaign.Supplies==supply,"Presentation charged cost");
 Click(c.Workbench);await Task.Delay(120);p.OrderRows[0].Cancel.onClick.Invoke();p.CancelYes.onClick.Invoke();p.Close();await Task.Delay(3500);Check(Vector3.Distance(m.Pawns[0].position,home)<.01f&&!m.IsMoving&&!m.WorkBadges[0].gameObject.activeSelf,"Cancel did not return home");
 return "PASS: real UI assignment, pawn + base move together, exact work anchor, other pawn stationary, badge, no extra time/resource cost, cancellation returns home. 56 recorded frames.";
 }
}
