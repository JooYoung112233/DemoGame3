using System.Linq;
using UnityEngine;
using UnityEngine.UI;
public static class InspectFont {public static string Run(){return string.Join("\n",Object.FindObjectsByType<Text>().Where(t=>t.preferredHeight>t.rectTransform.rect.height+1).Select(t=>t.name+" size="+t.fontSize+" preferred="+t.preferredHeight+" rect="+t.rectTransform.rect.height+" text="+t.text));}}

