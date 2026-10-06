using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
public static class AlignSettlementCraft
{
    const string P="Assets/Prefabs/Settlement/";
    static void Edit(string name,Action<GameObject> action){var g=PrefabUtility.LoadPrefabContents(P+name+".prefab");try{action(g);PrefabUtility.SaveAsPrefabAsset(g,P+name+".prefab");}finally{PrefabUtility.UnloadPrefabContents(g);}}
    static RectTransform Rect(Transform root,string path,float x,float y,float w,float h){var r=(RectTransform)root.Find(path);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static void Text(Transform root,string path,float x,float y,float w,float h,TextAnchor alignment){Rect(root,path,x,y,w,h).GetComponent<Text>().alignment=alignment;}
    public static string Apply(){
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve unsaved scene changes");
        Edit("CraftRecipeRow",g=>{var t=g.transform;Rect(t,"Icon",14,12,54,48);Text(t,"Name",83,0,213,72,TextAnchor.MiddleLeft);Rect(t,"Selected",303,22,28,28);});
        Edit("CraftWorkerRow",g=>{var t=g.transform;Rect(t,"Portrait",14,7.5f,49,54);Text(t,"Name",78,0,181,69,TextAnchor.MiddleLeft);Text(t,"State",258,0,92,69,TextAnchor.MiddleCenter);Rect(t,"Selected",363,20.5f,28,28);});
        Edit("CraftCostRow",g=>{var t=g.transform;Rect(t,"Icon",12,6,48,40);Text(t,"Name",80,0,324,52,TextAnchor.MiddleLeft);Text(t,"Count",418,0,143,52,TextAnchor.MiddleRight);});
        Edit("CraftOrderRow",g=>{var t=g.transform;Rect(t,"Icon",14,27,58,58);Text(t,"Title",87,9,407,43,TextAnchor.MiddleLeft);Text(t,"Member",87,52,218,40,TextAnchor.MiddleLeft);Text(t,"Status",309,52,188,40,TextAnchor.MiddleRight);Rect(t,"Cancel",522,28.5f,92,61);});
        Edit("CraftWorkPanel",g=>{
            var t=g.transform;const string w="Workspace/Workshop/",o="Workspace/OrdersPaper/";
            Text(t,w+"Heading",10,-22,210,73,TextAnchor.MiddleCenter);
            for(int i=0;i<3;i++){Rect(t,w+"Category_"+i+"/Icon",17,15,42,42);Text(t,w+"Category_"+i+"/Label",66,0,86,72,TextAnchor.MiddleCenter);}
            Text(t,w+"ItemTitle",781,70,270,58,TextAnchor.MiddleLeft);
            Text(t,w+"Description",781,142,276,74,TextAnchor.UpperLeft);
            Text(t,w+"QuantityLabel",781,220,276,38,TextAnchor.MiddleLeft);
            Rect(t,w+"Minus",770,265,60,52);Text(t,w+"Minus/Label",0,0,60,52,TextAnchor.MiddleCenter);
            Rect(t,w+"Plus",997,265,60,52);Text(t,w+"Plus/Label",0,0,60,52,TextAnchor.MiddleCenter);
            Text(t,w+"Quantity",830,265,167,52,TextAnchor.MiddleCenter);
            Text(t,w+"WorkerHeading",34,351,408,51,TextAnchor.MiddleLeft);
            Text(t,w+"CostHeading",480,351,260,51,TextAnchor.MiddleLeft);
            Text(t,w+"CostColumns",793,351,263,51,TextAnchor.MiddleRight);
            Text(t,w+"Duration",36,649,408,37,TextAnchor.MiddleLeft);
            Text(t,w+"StartWork/Label",12,0,553,73,TextAnchor.MiddleCenter);
            Text(t,o+"Title",12,-20,248,70,TextAnchor.MiddleCenter);
            Text(t,o+"Count",554,-20,102,70,TextAnchor.MiddleRight);
            Text(t,o+"Hint",30,378,630,56,TextAnchor.MiddleLeft);
            Text(t,"CancelConfirmation/Paper/Title",40,27,670,60,TextAnchor.MiddleLeft);
        });
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Only UI/text geometry and alignment updated in five existing craft prefabs; fonts, artwork, data and gameplay preserved.";
    }
}
