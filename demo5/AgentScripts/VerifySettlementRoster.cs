using System;
using System.Linq;
using System.Threading.Tasks;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
public static class VerifySettlementRoster {
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static async Task<string> Run(){
  var roster=Object.FindAnyObjectByType<SettlementController>().Roster;
  var state=new CampaignState(roster.Candidates.Select(c=>new Adventurer(c.DisplayName,c.TraitTitle,c.Description,c.Health,c.Aim,c.BagCapacity)).ToArray(),true);
  state.Toggle(0);state.Toggle(1);state.ConfirmParty();state.Settle(0);
  for(int i=2;i<roster.Candidates.Length;i++)state.Chosen.Add(i);
  PartySelectionSession.Clear();PartySelectionSession.Selected.AddRange(roster.Candidates.Select(c=>c.Id));PartySelectionSession.Pending=state;
  SceneManager.LoadScene("Settlement");await Task.Delay(650);
  var c=Object.FindAnyObjectByType<SettlementController>();Canvas.ForceUpdateCanvases();
  Check(c.Members.Count(m=>m.gameObject.activeSelf)==4,"First page count");
  Check(!c.Previous.interactable&&c.Next.interactable,"First page arrows");
  Check(((RectTransform)c.Next.transform).rect.size==new Vector2(44,52),"Arrow size");
  var first=c.Members[0].transform.position; var cardCorners=new Vector3[4];var noticeCorners=new Vector3[4];((RectTransform)c.Members[0].transform).GetWorldCorners(cardCorners);((RectTransform)c.Main.transform.Find("ArrivalNotice")).GetWorldCorners(noticeCorners);Check(Mathf.Abs(cardCorners[0].y-noticeCorners[0].y)<.1f,"Footer baseline mismatch");
  c.Next.onClick.Invoke();await Task.Delay(100);Canvas.ForceUpdateCanvases();
  Check(c.Members.Count(m=>m.gameObject.activeSelf)==2,"Last page count");
  Check(c.Previous.interactable&&!c.Next.interactable,"Last page arrows");
  Check(Vector3.Distance(first,c.Members[0].transform.position)<.1f,"Cards shifted between pages");
  Check(c.Next.GetComponent<CanvasGroup>().alpha<.3f,"Disabled arrow fade");
  c.Members[0].Button.onClick.Invoke();Check(c.PopupTitle.text==roster.Candidates[4].DisplayName,"Wrong member callback");c.Close();
  c.Members[1].BagButton.onClick.Invoke();Check(c.InventoryPanel.MemberIndex==5,"Wrong bag callback");c.InventoryPanel.Close();
  c.Previous.onClick.Invoke();await Task.Delay(100);
  Check(c.RosterPage.text=="1–4 / 6명","Page indicator");
  return "PASS: six-member fixture, four/two pages, stable alignment, 44x52 arrows, boundary fade, correct member and bag callbacks. Runtime fixture only.";
 }
}

