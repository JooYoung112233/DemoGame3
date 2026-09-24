using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Inactive disposable fixture: no scene reload, live save, balance change or actual travel.
public static class VerifyExplorationKnowledge
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static void Field(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    static void Property(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
    static void Call(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
    static GameObject Child(Transform root,string name,bool active=true){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(root,false);g.SetActive(active);return g;}
    static Text Label(Transform root,string name)=>Child(root,name).AddComponent<Text>();
    static string Summary(int unsearched,int progress=0,int remaining=0,int empty=0)=>"미수색 "+unsearched+" · 진행 "+progress+"\n물품 남음 "+remaining+" · 비어 있음 "+empty;

    public static string Run()
    {
        var root=new GameObject("Exploration knowledge verification");root.SetActive(false);
        try
        {
            var a=root.AddComponent<ExpeditionArrivalPanel>();var rooms=root.AddComponent<ExpeditionRoomNavigation>();
            var loot=root.AddComponent<ExpeditionLootPanel>();var search=root.AddComponent<ExpeditionSearchPanel>();
            var threat=root.AddComponent<ExpeditionSiteThreat>();var badges=root.AddComponent<ExpeditionSearchStatus>();
            a.View=Child(root.transform,"Arrival",false);a.Popup=Child(root.transform,"Popup",false);a.Main=Child(root.transform,"Main").AddComponent<CanvasGroup>();
            a.Status=Label(root.transform,"Status");a.PopupTitle=Label(root.transform,"PopupTitle");a.PopupBody=Label(root.transform,"PopupBody");
            a.ReturnConfirm=Child(root.transform,"Confirm").AddComponent<Button>();a.ReturnConfirm.gameObject.AddComponent<Text>();
            a.Rooms=rooms;a.Loot=loot;a.Search=search;a.Threat=threat;
            rooms.TurnLabel=Label(root.transform,"Turn");rooms.RouteLabel=Label(root.transform,"Route");
            search.View=Child(root.transform,"Search",false);loot.View=Child(root.transform,"Loot",false);
            Field(rooms,"owner",a);Field(loot,"arrival",a);
            badges.Arrival=a;badges.Labels=Array.Empty<Text>();
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>().ArrivalPanel;
            loot.Sites=source.Loot.Sites;threat.DenSite=source.Threat.DenSite;rooms.Storage=source.Rooms.Storage;
            int arcade=loot.Sites.Count(s=>s.Room==0),corridor=loot.Sites.Select((s,i)=>new{s,i}).Count(x=>x.s.Room==1&&x.i!=threat.DenSite),storage=loot.Sites.Count(s=>s.Room==2);
            string randomBefore=JsonUtility.ToJson(UnityEngine.Random.state);
            Check(loot.SearchSummary().Contains("아직 수색 기록")&&loot.ExportSearches().Length==0,"Fresh destination leaked unvisited search sites or created state.");
            Check(!Enumerable.Range(0,loot.Sites.Length).Any(loot.IsKnownSite),"Fresh destination knows a site.");
            a.View.SetActive(true);
            Check(loot.SearchSummary()==Summary(arcade),"Entering arcade revealed another room.");
            rooms.RestoreSaved(new CampaignSaveData{CorridorVisited=true,Inspected=Array.Empty<string>()});
            Property(rooms,"CurrentRoom",1);
            Check(loot.SearchSummary()==Summary(arcade+corridor)&&!loot.IsKnownSite(threat.DenSite),"Corridor revealed storage or hidden office shelf.");
            rooms.RestoreSaved(new CampaignSaveData{CorridorVisited=true,StorageUnlocked=true,Inspected=Array.Empty<string>()});
            Check(loot.SearchSummary()==Summary(arcade+corridor),"An unlocked but unvisited room leaked its contents.");
            rooms.RestoreSaved(new CampaignSaveData{CorridorVisited=true,StorageUnlocked=true,StorageVisited=true,Inspected=Array.Empty<string>()});
            Property(rooms,"CurrentRoom",0);Call(rooms,"RefreshLabels");
            Check(rooms.RouteLabel.text=="● 오락실 ─ 복도 ─ 보관실","Returning to arcade dropped a visited route.");
            Check(loot.SearchSummary()==Summary(arcade+corridor+storage),"Visited storage is not remembered.");
            var s=loot.State(4);s.Progress=1;s.Required=2;
            var depleted=loot.State(6);depleted.Progress=depleted.Required=1;depleted.Complete=true;
            Check(loot.RoomSearchSummary(0)==Summary(arcade),"Other-room searches polluted arcade status.");
            Check(loot.RoomSearchSummary(1)==Summary(corridor-1,1),"Current room progress was not shown.");
            rooms.MarkInspected(threat.DenSite);
            Check(loot.IsKnownSite(threat.DenSite),"Actually inspected shelf remains unknown.");
            Property(rooms,"CurrentRoom",1);Call(badges,"LateUpdate");
            Check(a.Status.text==Summary(corridor,1),"Live room status did not refresh.");
            s.Progress=2;s.Complete=true;s.Loot["scrap"]=1;Call(badges,"LateUpdate");
            Check(a.Status.text==Summary(corridor,0,1),"Completion after a spent turn left stale HUD status.");
            s.Loot.Clear();Call(badges,"LateUpdate");
            Check(a.Status.text==Summary(corridor,0,0,1),"Taking last item left stale HUD status.");
            Check(JsonUtility.ToJson(UnityEngine.Random.state)==randomBefore,"Reading knowledge rolled random rewards.");

            // The existing v13 fields remain sufficient to restore knowledge and depleted/progress states.
            var saved=new CampaignSaveData{CorridorVisited=true,StorageUnlocked=true,StorageVisited=true,Inspected=new[]{CampaignPersistence.SiteIds[threat.DenSite]},Searches=loot.ExportSearches()};
            string before=loot.SearchSummary();loot.RestoreSaved(saved);rooms.RestoreSaved(saved);a.View.SetActive(false);
            Check(loot.SearchSummary()==before,"Restore changed recorded knowledge.");
            threat.RestoreSaved(new SavedSiteBoard{Id="mall"});rooms.RestoreSaved(new CampaignSaveData{Inspected=Array.Empty<string>()});
            loot.RestoreSaved(new CampaignSaveData{Searches=Array.Empty<SavedSearch>()});
            Check(loot.SearchSummary()==Summary(arcade),"A visit without any search should remember only the arcade.");

            // A real CampaignState makes accidental time consumption on open/cancel detectable.
            var owner=root.AddComponent<SettlementController>();var inventory=root.AddComponent<SettlementInventoryPanel>();owner.InventoryPanel=inventory;
            inventory.Items=new[]{new SettlementInventoryPanel.Item{Id="prybar",Name="지렛대"}};Field(inventory,"owner",owner);Field(a,"owner",owner);
            var people=new[]{new Adventurer("검증 1","정찰","",3,5),new Adventurer("검증 2","의무","",4,5)};
            var campaign=new CampaignState(people,true);campaign.Toggle(0);campaign.Toggle(1);campaign.ConfirmParty();campaign.Settle(0);
            Check(campaign.BeginFieldExpedition("mall",people,20),"Could not start disposable expedition.");Property(owner,"Campaign",campaign);Field(a,"people",people);a.View.SetActive(true);
            int minute=campaign.MinuteOfDay,turns=rooms.Turns,noise=rooms.Noise;
            foreach(int room in new[]{0,1,2})
            {
                Property(rooms,"CurrentRoom",room);rooms.AskMove();
                Check(a.Popup.activeSelf&&a.PopupBody.text.Contains("1턴 · 10분")&&a.PopupBody.text.Contains("소음 없음"),"Ordinary door omitted exact time/noise cost.");
                if(room==2)Check(a.PopupTitle.text=="복도로 돌아갈까요?","Storage back door names the wrong destination.");
                a.ClosePopup();rooms.ConfirmMove();
                Check(rooms.PendingRoom==-1&&campaign.MinuteOfDay==minute&&rooms.Turns==turns&&rooms.Noise==noise&&!a.InTransit,"Open/cancel spent time/noise or allowed stale confirmation.");
            }
            Property(rooms,"CurrentRoom",1);rooms.AskStorage();
            Check(a.Popup.activeSelf&&!a.ReturnConfirm.gameObject.activeSelf&&a.PopupBody.text.Contains("지렛대"),"Missing tool should explain the lock without offering travel.");a.ClosePopup();
            var bags=(Dictionary<Adventurer,Dictionary<string,int>>)typeof(SettlementInventoryPanel).GetField("bags",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(inventory);
            bags[people[0]]=new Dictionary<string,int>{{"prybar",1}};rooms.AskStorage();
            Check(a.PopupBody.text.Contains("잠금 해제 1턴 + 이동 1턴 · 20분")&&a.PopupBody.text.Contains("소음 +2 · 도구 소모 없음"),"Locked storage omitted combined unlock/travel cost.");a.ClosePopup();
            Check(!rooms.StorageUnlocked&&inventory.CountFor(people[0],"prybar")==1,"Cancel opened lock or consumed tool.");
            rooms.RestoreSaved(new CampaignSaveData{CorridorVisited=true,StorageUnlocked=true,Inspected=Array.Empty<string>()});rooms.AskStorage();
            Check(a.PopupBody.text.Contains("1턴 · 10분")&&a.PopupBody.text.Contains("소음 없음")&&!a.PopupBody.text.Contains("방문한 방"),"Open unvisited storage cost/knowledge wording is wrong.");a.ClosePopup();
            Check(campaign.MinuteOfDay==minute&&rooms.Turns==turns&&rooms.Noise==noise,"Storage inspection/cancel spent a turn.");
            return "PASS: unvisited rooms and hidden shelf stay unknown; known routes and current-room search states survive restore; completion/pickup refresh HUD; no read-only random rolls; ordinary/storage cost wording and storage return destination; all open/cancel paths consume zero time/noise and preserve tools. No live scene or save changed.";
        }
        finally{Object.DestroyImmediate(root);}
    }
}
