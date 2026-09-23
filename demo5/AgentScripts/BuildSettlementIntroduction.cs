using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;

public static class BuildSettlementIntroduction
{
    public static string Run()
    {
        if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Preserve dirty scene/stop Play first");
        AssetDatabase.Refresh();
        var shader=Shader.Find("Demo5/RuinedRoom");
        if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Room shader failed: "+string.Join(";",ShaderUtil.GetShaderMessages(shader).Select(m=>m.message)));
        const string materialPath="Assets/Settings/RuinedRoom.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,materialPath);}else material.shader=shader;
        material.SetFloat("_Exposure",.83f);material.SetFloat("_Saturation",.62f);material.SetFloat("_Vignette",.52f);material.SetFloat("_Grain",.065f);material.SetFloat("_Dampness",.24f);EditorUtility.SetDirty(material);
        Edit("Assets/Prefabs/Settlement/SettlementScreen.prefab",g=>{
            var c=g.GetComponent<SettlementController>();var intro=g.GetComponent<SettlementIntroduction>();if(!intro)intro=g.AddComponent<SettlementIntroduction>();c.Introduction=intro;
            var old=c.Main.transform.Find("IntroductionAction");if(old)Object.DestroyImmediate(old.gameObject);
            var button=new GameObject("IntroductionAction",typeof(RectTransform),typeof(Image),typeof(Button));button.transform.SetParent(c.Main.transform,false);
            var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(30,-790);rect.sizeDelta=new Vector2(554,76);
            var image=button.GetComponent<Image>();image.sprite=c.Advance.GetComponent<Image>().sprite;image.color=new Color(.82f,.78f,.57f);intro.Action=button.GetComponent<Button>();intro.Action.targetGraphic=image;
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(button.transform,false);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(16,6);label.rectTransform.offsetMax=new Vector2(-16,-6);label.font=c.NoticeTitle.font;label.fontSize=28;label.alignment=TextAnchor.MiddleCenter;label.color=new Color(.06f,.08f,.07f);label.raycastTarget=false;label.text="주변 확인 · 10분";intro.ActionLabel=label;
            var notice=(RectTransform)c.NoticeBody.transform.parent;notice.anchoredPosition=new Vector2(notice.anchoredPosition.x,-894);notice.sizeDelta=new Vector2(notice.sizeDelta.x,156);
            c.NoticeBody.fontSize=24;c.NoticeBody.rectTransform.sizeDelta=new Vector2(460,88);
        });
        Edit("Assets/Prefabs/Settlement/SettlementWorld.prefab",g=>{
            var background=g.GetComponentsInChildren<SpriteRenderer>(true).First(s=>s.name=="Shelter");background.sharedMaterial=material;
            foreach(var l in g.GetComponentsInChildren<Light2D>(true)){
                if(l.name=="Ambient"){l.intensity=.38f;l.color=new Color(.74f,.83f,1);}
                if(l.name=="WallLamp"){l.intensity=1.5f;l.pointLightOuterRadius=3.3f;}
                if(l.name=="ExitLamp"){l.intensity=1.1f;l.pointLightOuterRadius=2.6f;}
            }
        });
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");
        var arcade=Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(s=>s.name=="Arcade");
        if(arcade){arcade.sharedMaterial=material;PrefabUtility.RecordPrefabInstancePropertyModifications(arcade);}
        EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Saved progressive introduction UI and ruined-room shader/light treatment; shader compiled without errors.";
    }
    static void Edit(string path,Action<GameObject> edit){var g=PrefabUtility.LoadPrefabContents(path);try{edit(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
}
