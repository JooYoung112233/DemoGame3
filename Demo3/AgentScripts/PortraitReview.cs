using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Live49.Dialogue;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
public static class PortraitReview
{
    public static string Inspect()
    {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_Game.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
        try{
        var view=UnityEngine.Object.FindAnyObjectByType<DialogueView>(FindObjectsInactive.Include);
        return string.Join("\n",view.GetComponentsInChildren<Image>(true).Where(i=>i.sprite!=null).Select(i=>
        i.name+" sprite="+AssetDatabase.GetAssetPath(i.sprite)+" rect="+i.rectTransform.rect+" anchors="+i.rectTransform.anchorMin+"/"+i.rectTransform.anchorMax+" pivot="+i.rectTransform.pivot+" pos="+i.rectTransform.anchoredPosition+" scale="+i.transform.localScale+" parent="+i.transform.parent.name));
        }finally{UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
    }
}
