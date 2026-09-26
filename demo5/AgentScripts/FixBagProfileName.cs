using UnityEditor;
using UnityEngine;
using Demo5.FrontEnd;
// 가방 창 대원 이름 칸 (2026-09-26): BuildTabbedFieldBags (2026-09-24) made ProfileName 48px high for a 34pt line that lays out at 50px
// (펀플로 생존자, at every canvas scale), so every text-bounds check over the bag window stops on it (VerifyFieldBags.Flow,
// VerifyPopupFrames.Bags, VerifyLifeSupplies.Flow, VerifyCooking.Flow). The box grows 1px up and 1px down: the middle-aligned name
// stays where it is. Idempotent (running it again changes nothing); play mode stopped.
public static class FixBagProfileName
{
    const string Path = "Assets/Prefabs/Settlement/ExpeditionBagPanel.prefab";
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return "Stop play mode first.";
        var go = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var r = go.GetComponent<ExpeditionBagPanel>().ProfileName.rectTransform;
            string before = r.anchoredPosition + " " + r.sizeDelta;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(252, -266); r.sizeDelta = new Vector2(354, 50);
            PrefabUtility.SaveAsPrefabAsset(go, Path); AssetDatabase.SaveAssets();
            return "ProfileName " + before + " → " + r.anchoredPosition + " " + r.sizeDelta;
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }
}
