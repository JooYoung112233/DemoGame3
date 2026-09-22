using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifyHomeSelection
{
    public static string Open(){Check(!EditorApplication.isPlaying,"Stop first");UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/HomeSelection.unity");return "HomeSelection open.";}
    static void Check(bool value,string msg){if(!value)throw new Exception(msg);}
    static void Click(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var canvas=b.GetComponentInParent<Canvas>();var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name);ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);}
    static async Task<HomeSelectionController> Enter()
    {
        SceneManager.LoadScene("PartySelection");await Task.Delay(300);var p=Object.FindAnyObjectByType<PartySelectionController>();PartySelectionSession.Clear();p.Refresh();Click(p.Cards[0].Button);Click(p.Cards[2].Button);Click(p.Continue);await Task.Delay(300);return Object.FindAnyObjectByType<HomeSelectionController>();
    }
    public static async Task<string> Flow()
    {
        Check(EditorApplication.isPlaying,"Enter Play first");
        var direct=Object.FindAnyObjectByType<HomeSelectionController>();Check(direct&&!direct.Continue.interactable&&direct.Campaign==null,"Direct scene must require party");
        Click(direct.Cards[1].Button);Check(!direct.Continue.interactable,"Must not silently create party");
        for(int index=0;index<3;index++){
            var c=await Enter();Check(c&&c.Campaign.Chosen.SequenceEqual(new[]{0,2})&&!c.Continue.interactable,"Party transfer/initial gate");
            Check(c.PartyNames.Select(t=>t.text).SequenceEqual(c.Campaign.Party.Select(p=>p.Name)),"Party labels");
            Click(c.Cards[(index+1)%3].Button);Click(c.Cards[index].Button);
            Check(c.SelectedIndex==index&&c.Cards.Count(card=>card.SelectedBorder.activeSelf)==1&&c.Cards[index].Check.activeSelf,"Single selected card");
            var site=c.Campaign.Sites[index];Check(c.Resources.text.Contains(site.Ammo.ToString())&&c.LocationName.text==site.Name,"Summary");
            Canvas.ForceUpdateCanvases();foreach(var t in c.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Text overflow: "+t.name);
            Click(c.Continue);await Task.Delay(400);var game=Object.FindAnyObjectByType<SettlementController>();
            Check(game&&game.Campaign.Stage==JourneyStage.Settlement&&game.Campaign.Home.Name==site.Name,"Home destination");
            Check(game.Campaign.Supplies==site.Supplies&&game.Campaign.Ammo==site.Ammo&&game.Campaign.Home.Recovery==site.Recovery,"Start resources mismatch");
            Check(game.Campaign.Chosen.SequenceEqual(new[]{0,2}),"Party lost on settlement");
        }
        var last=await Enter();Click(last.Back);await Task.Delay(300);var party=Object.FindAnyObjectByType<PartySelectionController>();Check(party.SelectedCount==2&&party.Cards[0].Check.activeSelf&&party.Cards[2].Check.activeSelf,"Return selection lost");
        Click(party.Continue);await Task.Delay(300);Click(Object.FindAnyObjectByType<HomeSelectionController>().Cards[0].Button);
        return "PASS: no-party guard, two-person transfer, all 3 site buttons via raycast, one selected/check, text bounds, all start-resource values and party preserved in settlement, back navigation. Screenshot ready: garage selected.";
    }
}
