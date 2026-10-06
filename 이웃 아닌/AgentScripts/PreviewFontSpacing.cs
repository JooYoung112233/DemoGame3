using UnityEngine;
using UnityEngine.UI;
public static class PreviewFontSpacing {public static string Run(){foreach(var t in Object.FindObjectsByType<Text>(FindObjectsInactive.Include))t.lineSpacing=.8f;return "Spacing preview";}}
