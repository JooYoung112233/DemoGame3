using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using Demo5.FrontEnd;
public static class VerifyCookingUI {
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static async Task Tap(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var cam=b.GetComponentInParent<Canvas>().worldCamera;cam?.Render();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(cam,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);await Task.Delay(80);}
 static void Bounds(SettlementCookingPanel p){Canvas.ForceUpdateCanvases();foreach(var t in p.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow "+t.name+" "+t.preferredHeight+"/"+t.rectTransform.rect.height);Check(((RectTransform)p.Back.transform).rect.size==((RectTransform)p.Confirm.transform).rect.size,"Unequal footer sizes");Check(Mathf.Abs(p.Back.GetComponent<RectTransform>().anchoredPosition.y-p.Confirm.GetComponent<RectTransform>().anchoredPosition.y)<1,"Footer baseline");}
 public static async Task<string> Flow(){var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>();var p=c.CookingPanel;int time=c.Campaign.MinuteOfDay,supply=c.Campaign.Supplies;var stock=string.Join(";",c.CraftPanel.Materials.Select(m=>m.Id+":"+m.Initial));
  await Tap(c.Stock);Check(p.IsOpen&&!c.InventoryPanel.IsOpen&&c.Main.alpha==0&&!c.Main.blocksRaycasts,"Stock routing/modal gate");Check(!p.Confirm.interactable,"Missing worker allowed");await Tap(p.WorkerRows[0].Button);await Tap(p.RecipeRows[1].Button);Check(p.SelectedWorker==0,"Recipe lost worker");Bounds(p);await Tap(p.Plus);Check(!p.Confirm.interactable&&p.ConfirmLabel.text=="시안 재료 부족","Insufficient fuel accepted");await Tap(p.Minus);await Tap(p.Confirm);Check(p.PlanTitle.text=="준비 내용 확인됨"&&!p.Confirm.interactable,"Review confirmation");Bounds(p);
  await Tap(p.Tabs[1]);Check(p.SelectedWorker==0&&p.BatchCount==1&&p.ResultTitle.text=="원정 도시락","Ration tab");Bounds(p);await Tap(p.Tabs[2]);Check(p.Duration.text.Contains("0분"),"Convenience time");Bounds(p);await Tap(p.Back);Check(c.Main.alpha==1&&c.Main.blocksRaycasts,"HUD restore");await Tap(c.Cabinet);Check(c.InventoryPanel.IsOpen&&!p.IsOpen,"Warehouse changed");await Tap(c.InventoryPanel.CloseButton);await Tap(c.Stock);await Tap(p.WorkerRows[0].Button);await Tap(p.Plus);Bounds(p);
  Check(c.Campaign.MinuteOfDay==time&&c.Campaign.Supplies==supply&&stock==string.Join(";",c.CraftPanel.Materials.Select(m=>m.Id+":"+m.Initial))&&!c.IsAssigned(c.Campaign.Party.First()),"Preview changed campaign state");return "PASS stock/warehouse separation, tabs, worker retention, quantities, shortage, confirmation, modal/close, bounds, no campaign mutation. Preview stock only.";
 }
 [Serializable] public class Layer {public string name,path;public float x,y,w,h;public float[] color;public bool aspect;}
 [Serializable] public class Layout {public int width=1920,height=1080;public List<Layer> layers=new List<Layer>();}
 public static string Export(){var c=UnityEngine.Object.FindAnyObjectByType<SettlementController>().CookingPanel;Canvas.ForceUpdateCanvases();var layout=new Layout();foreach(var im in c.GetComponentsInChildren<Image>()){if(!im.enabled)continue;var corners=new Vector3[4];im.rectTransform.GetWorldCorners(corners);var a=c.transform.InverseTransformPoint(corners[1]);var b=c.transform.InverseTransformPoint(corners[3]);layout.layers.Add(new Layer{name=im.name,path=im.sprite?AssetDatabase.GetAssetPath(im.sprite):"",x=a.x,y=-a.y,w=b.x-a.x,h=a.y-b.y,aspect=im.preserveAspect,color=new[]{im.color.r,im.color.g,im.color.b,im.color.a}});}System.IO.Directory.CreateDirectory("아트/식량준비-v1");System.IO.File.WriteAllText("아트/식량준비-v1/layout.json",Newtonsoft.Json.JsonConvert.SerializeObject(layout,Newtonsoft.Json.Formatting.Indented));return layout.layers.Count+" editable image layers exported (text remains in prefab).";}
}
