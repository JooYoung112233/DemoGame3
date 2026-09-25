using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;

public static class BuildCharacterUnlockUI
{
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode first.");
        foreach(string path in new[]{"Assets/Prefabs/Settlement/VisitorPanel.prefab","Assets/Prefabs/Settlement/CraftWorkPanel.prefab","Assets/Prefabs/Settlement/SettlementScreen.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var craft in root.GetComponentsInChildren<SettlementCraftPanel>(true))
                {
                    craft.SideRoomPlaces=3;
                    foreach(var recipe in craft.Recipes)
                        if(recipe.Id=="prepare-side-room")recipe.Description="추가 잠자리 3곳을 준비합니다.\n완료하면 총 6명이 함께 지낼 수 있습니다.";
                }
                foreach(var panel in root.GetComponentsInChildren<SettlementVisitorPanel>(true))
                {
                    var rect=panel.RecruitDetails.rectTransform;
                    rect.sizeDelta=new Vector2(rect.sizeDelta.x,260);
                    rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-130);
                    panel.RecruitDetails.fontSize=28;
                    var paper=rect.parent;
                    var divider=paper.Find("SectionDivider") as RectTransform;
                    if(divider)divider.anchoredPosition=new Vector2(divider.anchoredPosition.x,-390);
                    var housing=panel.RecruitHousing.rectTransform;
                    housing.anchoredPosition=new Vector2(housing.anchoredPosition.x,-402);
                    var icon=paper.Find("HousingIcon") as RectTransform;
                    if(icon)icon.anchoredPosition=new Vector2(icon.anchoredPosition.x,-414);
                    panel.Dialogue.fontSize=28;
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();return "Visitor milestone clues and complete character traits fitted to existing paper panels.";
    }
}
