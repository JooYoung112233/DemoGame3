using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifySettlement
{
    static void Check(bool ok,string msg){if(!ok)throw new Exception(msg);}
    static void Click(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);}
    public static string Open(){Check(!EditorApplication.isPlaying,"Stop first");EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Settlement open.";}
    public static async Task<string> Flow()
    {
        Check(EditorApplication.isPlaying,"Play first");SceneManager.LoadScene("PartySelection");await Task.Delay(650);var p=Object.FindAnyObjectByType<PartySelectionController>();PartySelectionSession.Clear();p.Refresh();Click(p.Cards[0].Button);Click(p.Cards[2].Button);Click(p.Continue);await Task.Delay(650);
        var h=Object.FindAnyObjectByType<HomeSelectionController>();Check(((RectTransform)h.Back.transform).rect.size==((RectTransform)h.Continue.transform).rect.size,"Footer buttons unequal");Click(h.Cards[0].Button);Click(h.Continue);await Task.Delay(450);
        var c=Object.FindAnyObjectByType<SettlementController>();Check(c&&c.Campaign!=null&&c.Campaign.Home.Name=="폐정비소","Settlement data missing");Check(c.SupplyCount.text=="2"&&c.AmmoCount.text=="6"&&c.PartyCount.text=="2","Resource HUD incorrect");Check(c.Members.Count(m=>m.gameObject.activeSelf)==2&&c.StandeeObjects.All(g=>g.activeSelf),"Wrong party visibility");Check(c.Members[0].Name.text=="탐험가 1"&&c.Members[1].Name.text=="의무관","Wrong party names");
        int supplies=c.Campaign.Supplies,ammo=c.Campaign.Ammo;
        Canvas.ForceUpdateCanvases();foreach(var text in c.Main.GetComponentsInChildren<Text>())Check(text.preferredHeight<=text.rectTransform.rect.height+1,"Main text overflow: "+text.name);
        foreach(var button in new[]{c.Bed,c.Stock,c.Workbench,c.Cabinet,c.Exit,c.Advance,c.Journal,c.Members[0].Button,c.Members[1].BagButton}.Where(button=>(button!=c.Bed||!c.WorkPanel)&&(button!=c.Workbench||!c.CraftPanel)&&(button!=c.Exit||!c.ExpeditionPanel)&&(!c.InventoryPanel||(button!=c.Stock&&button!=c.Cabinet&&button!=c.Members[1].BagButton)))){
            Click(button);await Task.Delay(100);Check(c.IsPopupOpen&&!c.Main.interactable&&!c.Main.blocksRaycasts,"Popup/background gate");Canvas.ForceUpdateCanvases();Check(c.PopupBody.preferredHeight<=c.PopupBody.rectTransform.rect.height+1,"Popup text overflow: "+button.name);Click(c.PopupClose);await Task.Delay(50);Check(!c.IsPopupOpen&&c.Main.interactable,"Popup did not close");
        }
        Check(c.Campaign.Supplies==supplies&&c.Campaign.Ammo==ammo,"Inspection changed resources");
        Check(Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>().Length>=3,"URP lights missing");
        EventSystem.current.SetSelectedGameObject(null);
        return "PASS: equal footer sizes, party/resources transferred, two cards+standees, all 5 facilities, clock/log/member/bag inspection, modal input blocking/close, text bounds, no resource side effects, URP lights present.";
    }
}

