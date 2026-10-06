using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class VerifyResultFrame {
 public static string Run(){var b=UnityEngine.Object.FindAnyObjectByType<ExpeditionBattlePanel>(FindObjectsInactive.Include);if(!b.Result.activeSelf)throw new Exception("Result not open");
 var heading=(RectTransform)b.Result.transform.Find("PopupHeading");var button=(RectTransform)b.ResultContinue.transform;
 if(heading.anchoredPosition!=new Vector2(80,-32)||button.anchoredPosition!=new Vector2(80,-974)||button.sizeDelta.y!=76)throw new Exception("Frame coordinates");
 if(b.Workspace.alpha!=0)throw new Exception("Background HUD visible");b.Result.SetActive(false);if(b.Workspace.alpha!=1)throw new Exception("HUD restore failed");b.Result.SetActive(true);if(b.Workspace.alpha!=0)throw new Exception("HUD re-hide failed");
 var summary=b.Result.GetComponent<BattleResultSummary>();var texts=summary.Used.GetComponentsInChildren<Text>().Select(t=>t.text).ToArray();if(!texts.Contains("붕대")||!texts.Any(t=>t=="−1"))throw new Exception("Missing actual consumed bandage");
 foreach(var scroll in summary.GetComponentsInChildren<ScrollRect>(true)){Canvas.ForceUpdateCanvases();if(scroll.verticalScrollbar&&scroll.verticalScrollbar.gameObject.activeSelf){var r=scroll.verticalScrollbar.handleRect;var parent=(RectTransform)r.parent;if(r.rect.height>parent.rect.height+.1f)throw new Exception("Scrollbar outside track");}}
 return "PASS: shared heading (80,32), footer (80,974 h76), HUD hide/restore, actual consumed bandage row, bounded scroll handles.";
 }
}
