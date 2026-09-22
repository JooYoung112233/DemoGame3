using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class PolishPartyTypography
{
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before changing prefabs");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unsaved scene changes first");
        var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/FrontEnd/Fonts/Gaegu/Gaegu-Bold.ttf");
        foreach(var name in new[]{"SelectionHeader","CandidateCard","CandidateDetails","NavigationButton","ContinueButton","PartySelectionScreen"})
        {
            string path="Assets/Prefabs/PartySelection/"+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try{
                foreach(var t in root.GetComponentsInChildren<Text>(true)){t.font=font;t.fontStyle=FontStyle.Normal;}
                if(name=="CandidateDetails"){
                    foreach(var n in new[]{"TraitDescription","Characteristics"}){var t=root.transform.Find(n).GetComponent<Text>();t.fontSize=30;t.lineSpacing=1.12f;}
                    root.transform.Find("SelectionState").GetComponent<Text>().fontSize=24;
                }
                if(name=="CandidateCard"){
                    var t=root.transform.Find("Description").GetComponent<Text>();t.fontSize=22;t.lineSpacing=1.02f;t.rectTransform.anchoredPosition=new Vector2(10,-233);t.rectTransform.sizeDelta=new Vector2(172,62);
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/PartySelection.unity");
        return "All party selection text uses Gaegu Bold; detail body 30px, card descriptions 22px. Layout and imagery preserved.";
    }
}
