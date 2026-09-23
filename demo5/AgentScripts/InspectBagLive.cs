using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class InspectBagLive {public static string Run(){var b=Object.FindAnyObjectByType<ExpeditionBagPanel>(FindObjectsInactive.Include);return "left "+b.LeftMembers.anchoredPosition+" size "+b.LeftMembers.rect+" scale "+b.LeftMembers.lossyScale+"\n"+string.Join("\n",b.LeftCards.Select(c=>c.Name.text+" active "+c.gameObject.activeSelf+" pos "+((RectTransform)c.transform).anchoredPosition+" world "+c.transform.position))+"\nright "+b.RightMembers.anchoredPosition+" size "+b.RightMembers.rect;}}
