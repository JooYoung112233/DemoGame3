using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;
public static class VerifySettlementWork
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static void Click(Button b){Check(b.IsActive()&&b.IsInteractable(),"Unavailable "+b.name);Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(b.GetComponentInParent<Canvas>().worldCamera,r.TransformPoint(r.rect.center)),button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==b,"Blocked "+b.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);}
    public static async Task<string> Preview(){var c=Object.FindAnyObjectByType<SettlementController>();Click(c.Bed);await Task.Delay(200);Check(c.WorkPanel.Rows.Count==2&&!c.WorkPanel.Confirm.interactable,"Initial selection gate");Click(c.WorkPanel.Rows[0].Button);await Task.Delay(100);return "Rest panel preview ready.";}
    public static async Task<string> Flow(){
        var c=Object.FindAnyObjectByType<SettlementController>();var p=c.WorkPanel;int supplies=c.Campaign.Supplies,ammo=c.Campaign.Ammo;Check(p.IsOpen&&!c.Main.interactable&&!c.Main.blocksRaycasts,"Modal isolation");
        Click(p.Sleep);Check(p.Duration.text.Contains("120"),"Sleep duration");Click(p.ShortRest);Check(p.Duration.text.Contains("30"),"Short duration");
        Click(p.Rows[1].Button);Check(p.DetailName.text==c.Campaign.Party.Last().Name,"Selected detail");
        Canvas.ForceUpdateCanvases();foreach(var t in p.View.GetComponentsInChildren<Text>())Check(t.preferredHeight<=t.rectTransform.rect.height+1,"Overflow "+t.name+": "+t.preferredHeight+" > "+t.rectTransform.rect.height);
        Click(p.CloseButton);Check(!p.IsOpen&&p.Orders.Count==0&&c.Main.interactable,"Cancel must not register");
        Click(c.Bed);await Task.Delay(150);Click(p.Rows[0].Button);Click(p.Confirm);await Task.Delay(100);Check(!p.IsOpen&&p.Orders.Count==1&&c.Members[0].Status.text=="예약","Registration failed");
        Canvas.ForceUpdateCanvases();Check(c.NoticeBody.preferredHeight<=c.NoticeBody.rectTransform.rect.height+1,"Arrival message overflow");
        Click(c.Bed);await Task.Delay(150);Check(!p.Rows[0].Button.interactable&&!p.Confirm.interactable,"Duplicate assignment gate");Click(p.Rows[1].Button);
        Check(c.Campaign.Supplies==supplies&&c.Campaign.Ammo==ammo&&c.Clock.text.EndsWith("09:00"),"Registration advanced time/resources");
        p.Close();return "PASS: actual two-member rows, modal blocking, type/detail selection, text bounds, cancel without mutation, registration and duplicate gate, no time/resource changes.";
    }
    public static async Task<string> LargerRoster(){
        var c=Object.FindAnyObjectByType<SettlementController>();var p=c.WorkPanel;var original=c.Campaign.Chosen.ToArray();
        try{
            foreach(int i in Enumerable.Range(0,c.Campaign.Candidates.Length))if(!c.Campaign.Chosen.Contains(i))c.Campaign.Chosen.Add(i);
            Click(c.Bed);await Task.Delay(200);Check(p.Rows.Count==6,"Must create all 6 rows");Canvas.ForceUpdateCanvases();Check(p.Content.rect.height>p.Scroll.viewport.rect.height,"Missing overflow content");p.Scroll.verticalNormalizedPosition=0;await Task.Delay(200);Click(p.Rows[5].Button);Check(p.DetailName.text==c.Campaign.Party.Last().Name,"Last scrolled row inaccessible");return "PASS: 6-member roster scroll reaches and selects final row.";
        }finally{p.Close();c.Campaign.Chosen.Clear();c.Campaign.Chosen.AddRange(original);}
    }
}
