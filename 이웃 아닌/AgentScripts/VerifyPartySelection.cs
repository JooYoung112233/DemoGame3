using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class VerifyPartySelection
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static PartySelectionController View()=>Object.FindAnyObjectByType<PartySelectionController>();
    static void Click(Button b)
    {
        Require(b.IsActive()&&b.IsInteractable(),"Unavailable: "+b.name);Canvas.ForceUpdateCanvases();
        var rt=(RectTransform)b.transform;var canvas=b.GetComponentInParent<Canvas>();
        var p=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rt.TransformPoint(rt.rect.center)),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Require(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked: "+b.name);
        ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    public static async Task<string> Flow()
    {
        Require(EditorApplication.isPlaying,"Enter Play first");var c=View();Require(c,"Open PartySelection");
        PartySelectionSession.Clear();c.Refresh();Require(c.SelectedCount==0&&!c.Continue.interactable,"Initial state");
        Click(c.Cards[0].Button);Click(c.Cards[2].Button);Require(c.SelectedCount==2&&c.Continue.interactable&&c.Cards[0].Check.activeSelf&&c.Cards[2].Check.activeSelf&&c.Cards[0].SelectedBorder.activeSelf&&c.Cards[2].SelectedBorder.activeSelf&&c.SelectionCount.text=="2 / 2","Two selected visual state");
        Click(c.Cards[1].Button);Require(c.SelectedCount==2&&!c.Cards[1].Check.activeSelf,"Third must be rejected");
        Click(c.Cards[0].Button);Require(c.SelectedCount==1&&!c.Continue.interactable&&!c.Cards[0].SelectedBorder.activeSelf,"Deselect");
        var original=c.Roster;var expanded=ScriptableObject.CreateInstance<PartyRoster>();
        expanded.Candidates=original.Candidates.Concat(new[]{new PartyCandidate{Id="extra",DisplayName="Extra"}}).ToArray();
        try{c.Roster=expanded;c.Refresh();Require(c.NextPage.interactable,"Extra page missing");Click(c.NextPage);Require(c.Page==1&&c.Cards[0].CandidateId=="extra"&&!c.Cards[1].gameObject.activeSelf,"Page binding");Click(c.Previous);Require(c.Page==0&&c.Cards[2].Check.activeSelf,"Selection must persist between pages");}
        finally{c.Roster=original;c.Refresh();Object.Destroy(expanded);}
        PartySelectionSession.Clear();c.Refresh();Click(c.Cards[4].Button);Click(c.Cards[5].Button);
        Click(c.Continue);await Task.Delay(800);
        var game=Object.FindAnyObjectByType<HomeSelectionController>();Require(game&&game.Campaign.Stage==JourneyStage.HomeChoice,"Destination stage");
        Require(game.Campaign.Candidates.Length==6&&game.Campaign.Chosen.SequenceEqual(new[]{4,5}),"Roster/selection lost");
        Require(game.Campaign.Party.Last().BagCapacity==original.Candidates[5].BagCapacity,"Bag stat lost");
        foreach(var pair in game.Campaign.Party.Zip(new[]{original.Candidates[4],original.Candidates[5]},(actual,expected)=>new{actual,expected}))
            Require(pair.actual.Name==pair.expected.DisplayName&&pair.actual.MaxHealth==pair.expected.Health&&pair.actual.Aim==pair.expected.Aim,"Next screen party name/health/aim mismatch");
        Click(Object.FindObjectsByType<Button>().Single(b=>b.name=="BackToParty"));await Task.Delay(700);
        c=View();Require(c&&c.SelectedCount==2&&c.Cards[4].Check.activeSelf,"Return selection lost");
        Click(c.Back);await Task.Delay(700);var title=Object.FindAnyObjectByType<TitleMenuController>();Require(title&&title.NewGameScene=="PartySelection","Title route");
        Click(title.NewGameButton);
        for(int i=0;i<40&&SceneManager.GetActiveScene().name!="PartySelection";i++)await Task.Delay(100);
        await Task.Delay(150);c=View();Require(c&&c.SelectedCount==0&&!c.Continue.interactable,"New Game must clear draft");
        Click(c.Cards[0].Button);Click(c.Cards[2].Button);
        return "PASS: raycast clicks, exactly two, deselect, reject third, 7-candidate pagination, selected IDs 4/5+stats transferred, return preserves draft, title New Game resets draft. Screenshot state: scout+medic.";
    }
    public static string Open(){Require(!EditorApplication.isPlaying,"Stop first");EditorSceneManager.OpenScene("Assets/Scenes/PartySelection.unity");return "Party selection open.";}
    public static string Details()
    {
        var c=View();Require(c&&c.Details,"Details missing");PartySelectionSession.Clear();c.Refresh();
        Require(!c.transform.Find("SelectionHeader/StepPaper"),"02 badge remains");
        foreach(var card in c.Cards){
            ExecuteEvents.Execute(card.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
            Require(c.FocusedCandidateId==card.CandidateId&&c.SelectedCount==0,"Hover selected a candidate or wrong preview");
            var data=c.Roster.Candidates.Single(p=>p.Id==card.CandidateId);
            Require(c.Details.Name.text==data.DisplayName&&c.Details.TraitTitle.text==data.TraitTitle&&c.Details.Characteristics.text==data.Characteristics,"Details binding");
            Canvas.ForceUpdateCanvases();
            foreach(var t in c.Details.GetComponentsInChildren<Text>())Require(t.preferredHeight<=t.rectTransform.rect.height+1,"Overflow: "+data.Id+"/"+t.name);
            Require(card.Description.preferredHeight<=card.Description.rectTransform.rect.height+1,"Card description overflow: "+data.Id);
        }
        ExecuteEvents.Execute(c.Cards[1].gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.selectHandler);
        Require(c.FocusedCandidateId==c.Cards[1].CandidateId&&c.SelectedCount==0,"Keyboard preview");
        Click(c.Cards[0].Button);Click(c.Cards[2].Button);Click(c.Cards[4].Button);
        Require(c.SelectedCount==2&&!c.Cards[4].Check.activeSelf&&c.Details.Name.text==c.Roster.Candidates[4].DisplayName,"Full party preview");
        c.Preview(c.Cards[2].CandidateId);Require(c.Details.SelectionState.text.Contains("선택됨"),"Selection status");
        return "PASS: six hover previews without selection, keyboard focus, detail data and text bounds, reject third while previewing, selected state. Capture shows medic details.";
    }
    public static string RefreshArtwork()
    {
        System.IO.File.Copy("아트/모험가선택-v1/개별-PNG/arrow-paper.png","Assets/Art/PartySelection/arrow-paper.png",true);
        AssetDatabase.ImportAsset("Assets/Art/PartySelection/arrow-paper.png",ImportAssetOptions.ForceUpdate);
        return "Arrow paper refreshed; layout unchanged.";
    }
}
