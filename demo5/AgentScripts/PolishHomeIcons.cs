using System;
using System.IO;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class PolishHomeIcons
{
    const string P="Assets/Prefabs/HomeSelection/";
    static string Art(string n)=>"Assets/Art/HomeSelection/"+n+".png";
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h){var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static Image Icon(string name,Transform parent,float x,float y,float w,float h,string path){var im=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Image>();im.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);im.preserveAspect=true;im.raycastTarget=false;return im;}
    static void Edit(string name,Action<GameObject> edit){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{edit(g);PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
    static void Remove(Transform root,string name){var t=root.Find(name);if(t)Object.DestroyImmediate(t.gameObject);}
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unsaved scene edits first");
        foreach(var f in Directory.GetFiles("아트/정착지선택-v1/개별-PNG","icon-*.png")){string path="Assets/Art/HomeSelection/"+Path.GetFileName(f);File.Copy(f,path,true);AssetDatabase.ImportAsset(path);var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.SaveAndReimport();}
        string[] icons={"icon-supplies","icon-ammo","icon-recovery"};
        var stat=Rect("ResourceIconValue",null,0,0,102,48);Icon("Icon",stat,0,8,32,32,Art(icons[0]));
        var text=Rect("Value",stat,43,0,54,48).gameObject.AddComponent<Text>();text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/FrontEnd/Fonts/Gaegu/Gaegu-Bold.ttf");text.fontSize=32;text.alignment=TextAnchor.MiddleLeft;text.color=new Color(.06f,.095f,.095f);text.text="0";text.raycastTarget=false;
        var item=PrefabUtility.SaveAsPrefabAsset(stat.gameObject,P+"ResourceIconValue.prefab");Object.DestroyImmediate(stat.gameObject);
        Edit("HomeCandidateCard",g=>{
            var c=g.GetComponent<HomeCandidateCard>();var values=new Text[3];
            for(int i=0;i<3;i++){
                Remove(g.transform,"StatLabel_"+i);Remove(g.transform,"StatValue_"+i);Remove(g.transform,"Resource_"+i);
                var row=(GameObject)PrefabUtility.InstantiatePrefab(item,g.transform);row.name="Resource_"+i;((RectTransform)row.transform).anchoredPosition=new Vector2(22+i*102,-223);row.transform.Find("Icon").GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art(icons[i]));values[i]=row.transform.Find("Value").GetComponent<Text>();
            }
            c.Supplies=values[0];c.Ammo=values[1];c.Recovery=values[2];
        });
        Edit("HomeDetails",g=>{
            Remove(g.transform,"ResourceIcons");var rows=Rect("ResourceIcons",g.transform,1220,107,30,114);
            for(int i=0;i<3;i++)Icon(icons[i],rows,0,i*42,30,30,Art(icons[i]));rows.gameObject.SetActive(false);
            var textRect=(RectTransform)g.transform.Find("Resources");textRect.anchoredPosition=new Vector2(1270,-103);textRect.sizeDelta=new Vector2(430,193);
            Remove(g.transform,"PartyIcon");Icon("PartyIcon",g.transform,42,27,34,34,"Assets/Art/PartySelection/icon-party.png");((RectTransform)g.transform.Find("PartyLabel")).anchoredPosition=new Vector2(92,-24);
        });
        Edit("StartHereButton",g=>{
            ((RectTransform)g.transform).sizeDelta=new Vector2(410,78);Remove(g.transform,"LocationIcon");Icon("LocationIcon",g.transform,27,18,40,42,Art("icon-location"));
            var label=(RectTransform)g.transform.Find("Label");label.anchoredPosition=new Vector2(82,0);label.sizeDelta=new Vector2(302,78);
        });
        Edit("BackButton",g=>{
            Remove(g.transform,"BackIcon");Icon("BackIcon",g.transform,22,22,26,34,"Assets/Art/PartySelection/icon-left.png");var label=(RectTransform)g.transform.Find("Label");label.anchoredPosition=new Vector2(65,0);label.sizeDelta=new Vector2(290,78);
        });
        Edit("HomeSelectionScreen",g=>{
            var c=g.GetComponent<HomeSelectionController>();var r=(RectTransform)c.Continue.transform;r.anchoredPosition=new Vector2(1450,-944);r.sizeDelta=new Vector2(410,78);c.ResourceIcons=g.transform.Find("HomeDetails/ResourceIcons").gameObject;
        });
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/HomeSelection.unity");return "Resource icons+numbers, summary icons and party icon, location-pin start button at bottom right; editable IconValue prefab saved.";
    }
}
