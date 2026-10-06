using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;

public static class PolishSettlement
{
    const string P="Assets/Prefabs/Settlement/";
    static void Edit(string name,Action<GameObject> change){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{change(g);PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
    static RectTransform Rect(Transform p,string path,float x,float y,float w,float h){var r=(RectTransform)p.Find(path);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static void Texts(GameObject g){foreach(var t in g.GetComponentsInChildren<Text>(true)){t.fontStyle=FontStyle.Bold;t.color=new Color(.045f,.065f,.06f);t.resizeTextForBestFit=false;}}
    static void Size(Transform p,string path,int size){p.Find(path).GetComponent<Text>().fontSize=size;}
    static void FitIcon(Image im,float cx,float cy,float visibleSize){
        var path=AssetDatabase.GetAssetPath(im.sprite.texture);var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(path));var px=tex.GetPixels32();int minX=tex.width,minY=tex.height,maxX=0,maxY=0;
        for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)if(px[y*tex.width+x].a>64){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
        float s=visibleSize/Math.Max(maxX-minX+1,maxY-minY+1);var r=im.rectTransform;r.sizeDelta=new Vector2(tex.width*s,tex.height*s);r.anchoredPosition=new Vector2(cx-(minX+maxX+1)*.5f*s,-cy+(tex.height-(minY+maxY+1)*.5f)*s);im.preserveAspect=true;Object.DestroyImmediate(tex);
    }
    static SpriteRenderer Layer(SpriteRenderer source,string name,float dy,float sx,float sy,Color color,int order){
        var old=source.transform.parent.Find(name);if(old)Object.DestroyImmediate(old.gameObject);var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(source.transform.parent,false);go.transform.position=source.transform.position+new Vector3(0,dy,0);go.transform.localScale=new Vector3(source.transform.localScale.x*sx,source.transform.localScale.y*sy,1);var r=go.GetComponent<SpriteRenderer>();r.sprite=source.sprite;r.sharedMaterial=source.sharedMaterial;r.color=color;r.sortingOrder=order;return r;
    }
    // STALE since 2026-09-25: the ResourceHud has 2 slots (Icon_Ammo/Value_Ammo, Icon_Party/Value_Party; BuildRetireSupplies) and Apply loops Value_0..2; do not rerun.
    static void RefuseAfterSuppliesRetired(){var hud=AssetDatabase.LoadAssetAtPath<GameObject>(P+"ResourceHud.prefab");if(hud&&!hud.transform.Find("Value_0"))throw new Exception("Superseded: supplies retired 2026-09-25 (BuildRetireSupplies). PolishSettlement expects the old 3-slot ResourceHud; do not rerun.");}
    public static string Apply(){
        if(EditorApplication.isPlaying)throw new Exception("Stop first");RefuseAfterSuppliesRetired();for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene changes");
        Edit("DayPanel",g=>{Texts(g);Size(g.transform,"Clock",40);Rect(g.transform,"Clock",24,8,166,98);});
        Edit("AdvanceButton",g=>{Texts(g);Size(g.transform,"Label",34);});
        Edit("ResourceHud",g=>{Texts(g);for(int i=0;i<3;i++){Size(g.transform,"Value_"+i,38);g.transform.Find("Value_"+i).GetComponent<Text>().alignment=TextAnchor.MiddleLeft;Rect(g.transform,"Value_"+i,62+i*116,8,52,62);Rect(g.transform,"Icon_"+i,18+i*116,21,36,36);}});
        Edit("JournalButton",g=>{Texts(g);Size(g.transform,"Label",30);});
        Edit("ArrivalNotice",g=>{Texts(g);Size(g.transform,"Title",38);Size(g.transform,"Body",30);Rect(g.transform,"Title",84,8,445,46);Rect(g.transform,"Body",84,57,445,48);});
        Edit("MemberCard",g=>{Texts(g);Size(g.transform,"Name",31);Size(g.transform,"Status",32);Rect(g.transform,"Name",12,10,140,44);Rect(g.transform,"Status",105,90,82,46);g.transform.Find("Status").GetComponent<Text>().alignment=TextAnchor.MiddleCenter;Rect(g.transform,"HealthTrack",108,151,76,18);Rect(g.transform,"HealthTrack/Health",2,2,72,14);});
        Edit("InformationPopup",g=>{Texts(g);Size(g.transform,"Paper/Title",46);Size(g.transform,"Paper/Body",34);Size(g.transform,"Paper/Close/Label",34);});
        Edit("FacilityHotspot",g=>{Texts(g);((RectTransform)g.transform).sizeDelta=new Vector2(84,84);Rect(g.transform,"Icon",20,20,44,44);Rect(g.transform,"Caption",-58,90,200,54);Rect(g.transform,"Caption/Text",6,0,188,54);Size(g.transform,"Caption/Text",30);var o=g.GetComponent<Outline>()??g.AddComponent<Outline>();o.effectColor=new Color(.08f,.095f,.09f,.95f);o.effectDistance=new Vector2(1.5f,-1.5f);});
        Edit("SettlementScreen",g=>{
            var t=g.transform;var c=g.GetComponent<SettlementController>();c.Location.fontSize=34;c.Location.fontStyle=FontStyle.Bold;var outline=c.Location.GetComponent<Outline>()??c.Location.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.02f,.04f,.045f,.9f);outline.effectDistance=new Vector2(1,-1);
            string[] keys={"bed","water","work","storage","exit"};float[] xs={483,818,1185,1458,1768},ys={221,251,209,137,270};
            for(int i=0;i<5;i++){var h=t.Find("Main/Facility_"+keys[i]);Rect(t,"Main/Facility_"+keys[i],xs[i],ys[i],84,84);FitIcon(h.Find("Icon").GetComponent<Image>(),42,42,42);}
        });
        Edit("SettlementWorld",g=>{
            for(int i=0;i<2;i++){var pawn=g.transform.Find("Standee_"+i);var b=pawn.Find("Base").GetComponent<SpriteRenderer>();b.transform.position=new Vector3((815+i*175-960)/100f,(540-(662+i*18))/100f,0);b.transform.localScale=new Vector3(1f/b.sprite.bounds.size.x,.30f/b.sprite.bounds.size.y,1);b.color=i==0?new Color(.74f,.84f,.87f):new Color(.84f,.8f,.63f);b.sortingOrder=12+i*5;
                Layer(b,"ContactShadow",-.085f,1.16f,1.2f,new Color(.025f,.04f,.045f,.28f),10+i*5);
                Layer(b,"BaseEdge",-.045f,1.03f,1.02f,new Color(.24f,.27f,.25f),11+i*5);
                pawn.Find("Body").GetComponent<SpriteRenderer>().sortingOrder=13+i*5;
            }
        });
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Readability/grounding polish saved to component prefabs; no gameplay changes.";
    }
    public static string RefreshIcons(){
        foreach(var file in Directory.GetFiles("아트/정착지첫화면-v1/개별-PNG","icon-*.png")){var path="Assets/Art/Settlement/"+Path.GetFileName(file);File.Copy(file,path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);}
        return "Clean icon alpha imported.";
    }
}
