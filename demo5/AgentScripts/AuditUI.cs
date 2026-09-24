using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using UnityEngine;using UnityEngine.UI;using UnityEngine.EventSystems;using UnityEngine.SceneManagement;using Demo5.FrontEnd;
using Demo5.NightRun;using Object=UnityEngine.Object;
public static class AuditUI {
 const string Dir="Assets/Screenshots/UIAudit";
 [Serializable] public class Node {public string path,text,type,issue;public float x,y,w,h,font,preferredH,preferredW;public bool interactable;}
 [Serializable] public class Shot {public string name,scene;public int width,height;public Node[] nodes;}
 static SettlementController C=>Object.FindAnyObjectByType<SettlementController>();
 static string PathOf(Transform t){string s=t.name;while(t.parent){t=t.parent;s=t.name+"/"+s;}return s;}
 static bool Visible(Component c){if(!c.gameObject.activeInHierarchy)return false;foreach(var g in c.GetComponentsInParent<CanvasGroup>())if(g.alpha<.01f)return false;return true;}
 static Rect RectOf(RectTransform r){var canvas=r.GetComponentInParent<Canvas>();var cam=canvas?canvas.worldCamera:null;var v=new Vector3[4];r.GetWorldCorners(v);var p=v.Select(x=>RectTransformUtility.WorldToScreenPoint(cam,x)).ToArray();return Rect.MinMaxRect(p.Min(x=>x.x),p.Min(x=>x.y),p.Max(x=>x.x),p.Max(x=>x.y));}
 static bool InViewport(Component c,Rect rect){if(!rect.Overlaps(new Rect(0,0,Screen.width,Screen.height)))return false;foreach(var m in c.GetComponentsInParent<RectMask2D>())if(m.enabled&&!rect.Overlaps(RectOf(m.rectTransform)))return false;foreach(var m in c.GetComponentsInParent<Mask>())if(m.enabled&&!rect.Overlaps(RectOf((RectTransform)m.transform)))return false;return true;}
 public static async Task<string> Snapshot(string name){await Task.Delay(250);Canvas.ForceUpdateCanvases();var rows=new List<Node>();foreach(var t in Object.FindObjectsByType<Text>()){if(!Visible(t)||string.IsNullOrEmpty(t.text))continue;var r=RectOf(t.rectTransform);if(!InViewport(t,r))continue;var n=new Node{type="text",path=PathOf(t.transform),text=t.text,x=r.x/Screen.width*1920,y=(Screen.height-r.yMax)/Screen.height*1080,w=r.width/Screen.width*1920,h=r.height/Screen.height*1080,font=t.fontSize,preferredH=t.preferredHeight,preferredW=t.preferredWidth};if(!t.resizeTextForBestFit&&t.preferredHeight>t.rectTransform.rect.height+2)n.issue="height";if(t.horizontalOverflow==HorizontalWrapMode.Overflow&&t.preferredWidth>t.rectTransform.rect.width+3)n.issue+=" width";if((r.x<-.5f||r.y<-.5f||r.xMax>Screen.width+.5f||r.yMax>Screen.height+.5f)&&!t.GetComponentInParent<ScrollRect>())n.issue+=" screen";rows.Add(n);}
 foreach(var b in Object.FindObjectsByType<Button>()){if(!Visible(b))continue;var r=RectOf((RectTransform)b.transform);if(!InViewport(b,r))continue;var n=new Node{type="button",path=PathOf(b.transform),text=string.Join(" ",b.GetComponentsInChildren<Text>().Select(t=>t.text)),interactable=b.IsInteractable(),x=r.x/Screen.width*1920,y=(Screen.height-r.yMax)/Screen.height*1080,w=r.width/Screen.width*1920,h=r.height/Screen.height*1080};if(n.interactable&&r.width>0){var e=new PointerEventData(EventSystem.current){position=r.center};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(e,hits);if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=b)n.issue="center blocked";}rows.Add(n);}
 Directory.CreateDirectory(Dir);File.WriteAllLines(Dir+"/"+name+".tsv",new[]{"type\tissue\tx\ty\tw\th\tfont\tinteractable\tpath\ttext"}.Concat(rows.Select(n=>string.Join("\t",n.type,n.issue,n.x,n.y,n.w,n.h,n.font,n.interactable,n.path,(n.text??"").Replace("\n"," / ").Replace("\t"," ")))));File.WriteAllText(Dir+"/"+name+".json",JsonUtility.ToJson(new Shot{name=name,scene=SceneManager.GetActiveScene().name,width=Screen.width,height=Screen.height,nodes=rows.ToArray()},true));string file=Dir+"/"+name+".png";ScreenCapture.CaptureScreenshot(file);await Task.Delay(550);return name+": "+rows.Count+" visible nodes, "+rows.Count(n=>!string.IsNullOrEmpty(n.issue))+" candidates; screenshot="+File.Exists(file);}
 static void Tap(Button b){if(!b||!b.IsActive()||!b.IsInteractable())throw new Exception("Unavailable button "+(b?b.name:"null"));b.onClick.Invoke();}
 static async Task LoadFixture(){var c=C;var catalog=c?c:UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/SettlementScreen.prefab").GetComponent<SettlementController>();CampaignSaveStore.TestDirectory=System.IO.Path.GetFullPath("Temp/OpeningChapterVerification");try{var saved=CampaignSaveStore.Read(0,catalog);if(!saved.CanLoad)throw new Exception(saved.Error);CampaignPersistence.Prepare(saved.Data,catalog);}finally{CampaignSaveStore.TestDirectory=System.IO.Path.GetFullPath("Temp/UIAuditSlots");}SceneManager.LoadScene("Settlement");await Task.Delay(700);}
 public static async Task<string> Step(string key){var t=Object.FindAnyObjectByType<TitleMenuController>();switch(key){
 case "01-title":CampaignSaveStore.TestDirectory=System.IO.Path.GetFullPath("Temp/UIAuditSlots");SceneManager.LoadScene("StartMenu");await Task.Delay(700);break;
 case "02-title-settings":t.OpenSettings();break;
 case "03-title-load-empty":t.CancelSettings();t.LoadButton.onClick.Invoke();break;
 case "04-title-quit-review":Tap(t.LoadClose);Tap(t.ExitButton);break;
 case "05-party-empty":Tap(t.ConfirmCancel);Tap(t.NewGameButton);await Task.Delay(700);break;
 case "06-party-selected":var p=Object.FindAnyObjectByType<PartySelectionController>();Tap(p.Cards[0].Button);Tap(p.Cards[2].Button);break;
 case "07-home":Tap(Object.FindAnyObjectByType<PartySelectionController>().Continue);await Task.Delay(700);break;
 case "08-opening-hud":var h=Object.FindAnyObjectByType<HomeSelectionController>();Tap(h.Cards[0].Button);Tap(h.Continue);await Task.Delay(700);break;
 case "09-settlement-hud":await LoadFixture();break;
 case "10-inventory":C.InventoryPanel.Open();break;
 case "11-craft":C.InventoryPanel.Close();C.CraftPanel.Open();break;
 case "12-rest":C.CraftPanel.Close();C.WorkPanel.Open();break;
 case "13-cooking":C.WorkPanel.Close();C.Development.State.Cooker=true;C.CookingPanel.Open();break;
 case "14-time":C.CookingPanel.Close();C.TimePanel.Open();break;
 case "15-housing":C.TimePanel.Close();Tap(C.CraftPanel.HousingButton);break;
 case "16-visitor":C.CraftPanel.Close();C.VisitorPanel.Open();break;
 case "17-trade":C.VisitorPanel.OpenTrade();break;
 case "18-records":C.VisitorPanel.Close();C.Opening.OpenRecords();break;
 case "19-menu":C.Opening.Close();C.GameMenu.Open();break;
 case "20-save-empty":Tap(C.GameMenu.Save);break;
 case "21-settings":C.GameMenu.SavePanel.Close();Tap(C.GameMenu.Settings);break;
 case "22-title-review":C.GameMenu.SettingsDialog.CancelChanges();Tap(C.GameMenu.Title);break;
 case "23-plan":Tap(C.GameMenu.Cancel);C.GameMenu.Close();C.ExpeditionPanel.Open();break;
 case "24-packing":C.ExpeditionPanel.Close();C.PackingPanel.Open(C.Campaign.Party.ToArray(),C.ExpeditionPanel.Destinations.First(d=>d.Id=="mall"),()=>{});break;
 case "25-field":C.PackingPanel.Close();if(!C.ArrivalPanel.Begin(C.Campaign.Party.ToArray(),C.ExpeditionPanel.Destinations.First(d=>d.Id=="mall")))throw new Exception("Departure failed");await Task.Delay(900);break;
 case "26-search":C.ArrivalPanel.Search.Open(0);break;
 case "27-search-review":Tap(C.ArrivalPanel.Search.Cards[0].Button);Tap(C.ArrivalPanel.Search.Choose);break;
 case "28-loot":Tap(C.ArrivalPanel.Search.Confirm);break;
 case "28b-loot":C.ArrivalPanel.Search.Close();C.ArrivalPanel.Loot.Open(0);break;
 case "29-leave-loot":Tap(C.ArrivalPanel.Loot.Back);break;
 case "30-bag":if(C.ArrivalPanel.Loot.LeaveReview.activeSelf)Tap(C.ArrivalPanel.Loot.LeaveConfirm);C.ArrivalPanel.FieldBags.Open(0);break;
 case "31-return-review":C.ArrivalPanel.FieldBags.Close();Tap(C.ArrivalPanel.Return);break;
 case "32-return":Tap(C.ArrivalPanel.ReturnConfirm);break;
 case "33-record-settlement":C.ReturnPanel.Close();C.Opening.OpenRecords();C.Opening.SelectRecord(0);break;
 case "34-record-management":C.Opening.SelectRecord(1);break;
 case "35-night":await LoadFixture();CampaignPersistence.Prepare(JsonUtility.FromJson<CampaignSaveData>(File.ReadAllText("Temp/NightEventBeforeChoice.json")),C);SceneManager.LoadScene("Settlement");await Task.Delay(700);Tap(C.Introduction.Action);break;
 case "36-night-result":Tap(C.Opening.NightListen);break;
 case "37-bag-six":await LoadFixture();C.ArrivalPanel.Begin(C.Campaign.Party.ToArray(),C.ExpeditionPanel.Destinations.First(d=>d.Id=="mall"));await Task.Delay(900);var a=C.ArrivalPanel;var people=a.Participants.ToList();for(int i=people.Count;i<6;i++){var d=C.Roster.Candidates.First(x=>!people.Any(q=>q.Name==x.DisplayName));people.Add(new Adventurer(d.DisplayName,d.RoleTitle,d.TraitDescription,d.Health,d.Aim,d.BagCapacity));var card=Object.Instantiate(a.MemberPrefab,a.MemberContent);card.Portrait.sprite=d.Portrait;a.Cards.Add(card);}typeof(ExpeditionArrivalPanel).GetField("people",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(a,people.ToArray());C.InventoryPanel.TransferField(people[0],"bandage",2,true);a.FieldBags.Open(0);Tap(a.FieldBags.LeftRows.First(r=>r.Label.text=="붕대").Button);break;
 case "38-bag-scroll-bottom":C.ArrivalPanel.FieldBags.RightMembers.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=0;break;
 case "39-encounter":await LoadFixture();C.ArrivalPanel.Begin(C.Campaign.Party.ToArray(),C.ExpeditionPanel.Destinations.First(d=>d.Id=="mall"));await Task.Delay(900);var e=C.ArrivalPanel.Encounter;e.BaseChance=100;e.MaximumChance=100;e.NoiseThreshold=0;C.ArrivalPanel.Rooms.SpendSearchTurn(0);e.AfterSearch(0);C.ArrivalPanel.Rooms.SpendSearchTurn(0);e.AfterSearch(0);break;
 case "40-encounter-review":Tap(C.ArrivalPanel.Encounter.Wait);break;
 case "41-battle":C.ArrivalPanel.Encounter.CancelChoice();C.InventoryPanel.TransferField(C.ArrivalPanel.Participants[0],"bandage",2,true);Tap(C.ArrivalPanel.Encounter.Fight);await Task.Delay(1500);break;
 case "42-battle-items":Tap(C.ArrivalPanel.Encounter.Battle.Items);await Task.Delay(600);break;
 case "43-battle-retreat-review":C.ArrivalPanel.Encounter.Battle.CloseItems();Tap(C.ArrivalPanel.Encounter.Battle.Retreat);break;
 case "44-battle-result":Tap(C.ArrivalPanel.Encounter.Battle.RetreatConfirm);await Task.Delay(3500);break;
 case "45-fresh-search":await LoadFixture();C.ArrivalPanel.Begin(C.Campaign.Party.ToArray(),C.ExpeditionPanel.Destinations.First(d=>d.Id=="mall"));await Task.Delay(900);var s=C.ArrivalPanel.Loot.State(0);s.Progress=s.Required=0;s.Complete=s.Opened=false;s.Loot.Clear();C.ArrivalPanel.Search.Open(0);Tap(C.ArrivalPanel.Search.Cards[0].Button);break;
 case "46-loot-filled":C.ArrivalPanel.Search.Close();var ls=C.ArrivalPanel.Loot.State(0);ls.Complete=ls.Opened=true;ls.Progress=ls.Required=2;ls.Loot["wood"]=10;ls.Loot["metal"]=8;ls.Loot["cloth"]=4;C.ArrivalPanel.Loot.Open(0);Tap(C.ArrivalPanel.Loot.FieldRows[0].Button);break;
 case "47-leave-filled":Tap(C.ArrivalPanel.Loot.Back);break;
 case "48-loot-full-bag":C.ArrivalPanel.Loot.Dismiss();var person=C.ArrivalPanel.Participants[0];foreach(var id in new[]{"bandage","ration","water"})C.InventoryPanel.TransferField(person,id,2,true);C.ArrivalPanel.Loot.Rebuild();Tap(C.ArrivalPanel.Loot.FieldRows[0].Button);break;
 case "49-return-filled":Tap(C.ArrivalPanel.Loot.Back);Tap(C.ArrivalPanel.Loot.LeaveConfirm);Tap(C.ArrivalPanel.Return);Tap(C.ArrivalPanel.ReturnConfirm);break;
 case "50-save-filled":await LoadFixture();C.GameMenu.Open();if(!CampaignSaveStore.Write(0,CampaignPersistence.Capture(C),C,out var error))throw new Exception(error);Tap(C.GameMenu.Save);break;
 case "51-rest-selected":C.GameMenu.SavePanel.Close();C.GameMenu.Close();C.WorkPanel.Open();Tap(C.WorkPanel.Rows[1].Button);break;
 case "52-six-bag-last-transfer":await Step("37-bag-six");C.ArrivalPanel.FieldBags.RightMembers.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=0;await Task.Delay(250);var dest=C.ArrivalPanel.FieldBags.RightCards.Last().Button;var rt=(RectTransform)dest.transform;var pe=new PointerEventData(EventSystem.current){position=RectOf(rt).center,button=PointerEventData.InputButton.Left};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pe,hits);if(hits.Count==0||hits[0].gameObject.GetComponentInParent<Button>()!=dest)throw new Exception("Last recipient raycast blocked");ExecuteEvents.Execute(dest.gameObject,pe,ExecuteEvents.pointerClickHandler);if(C.InventoryPanel.CountFor(C.ArrivalPanel.Participants[5],"bandage")!=1)throw new Exception("Last recipient transfer failed");break;
 case "53-battle-icon-audit":await Step("39-encounter");await Step("41-battle");await Step("42-battle-items");break;
 case "54-inventory-quantity":await LoadFixture();C.InventoryPanel.Open();Tap(C.InventoryPanel.StockRows[0].Button);Tap(C.InventoryPanel.ToBag);break;
 case "55-trade-review":C.InventoryPanel.Close();C.VisitorPanel.Open();C.VisitorPanel.OpenTrade();Tap(C.VisitorPanel.Offer);break;
 case "56-recruitment":C.VisitorPanel.CancelReview();C.VisitorPanel.OpenRecruit();break;
 case "57-packing-review":C.VisitorPanel.Close();C.PackingPanel.Open(C.Campaign.Party.ToArray(),C.ExpeditionPanel.Destinations.First(d=>d.Id=="mall"),()=>{});Tap(C.PackingPanel.Ready);break;
 case "58-save-overwrite-review":C.PackingPanel.Close();C.GameMenu.Open();Tap(C.GameMenu.Save);Tap(C.GameMenu.SavePanel.Slots[0]);Tap(C.GameMenu.SavePanel.Action);break;
 case "59-craft-queued":await LoadFixture();foreach(var m in C.CraftPanel.Materials)m.Initial=20;C.CraftPanel.Open();Tap(C.CraftPanel.RecipeRows[0].Button);Tap(C.CraftPanel.WorkerRows[0].Button);Tap(C.CraftPanel.Confirm);C.CraftPanel.Open();break;
 case "60-craft-cancel":Tap(C.CraftPanel.OrderRows[0].Cancel);break;
 default:throw new Exception("Unknown step "+key);
 }return await Snapshot(key);}
}
