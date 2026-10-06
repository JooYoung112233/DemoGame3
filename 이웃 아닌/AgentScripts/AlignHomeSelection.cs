using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class AlignHomeSelection {
 static void Rect(Transform t,float x,float y,float w,float h){var r=(RectTransform)t;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static void TextAt(Text t,float x,float y,float w,float h,int size,TextAnchor align=TextAnchor.MiddleLeft){Rect(t.transform,x,y,w,h);t.fontSize=size;t.alignment=align;t.lineSpacing=1;t.resizeTextForBestFit=false;}
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop and preserve dirty scene");
  const string path="Assets/Prefabs/HomeSelection/HomeSelectionScreen.prefab";var g=PrefabUtility.LoadPrefabContents(path);
  try {
   var c=g.GetComponent<HomeSelectionController>();var header=g.transform.Find("HomeHeader");Rect(header,80,60,360,229);
   TextAt(header.Find("Heading").GetComponent<Text>(),28,32,304,64,40);
   var instruction=header.Find("Instruction").GetComponent<Text>();instruction.text="머물 정착지를\n선택하세요.";TextAt(instruction,30,130,172,78,24);
   for(int i=0;i<c.Cards.Length;i++){
    var card=c.Cards[i];Rect(card.transform,480+480*i,60,400,380);Rect(card.transform.Find("LocationArt"),16,16,368,156);
    TextAt(card.Name,24,180,352,58,34);TextAt(card.Description,24,302,352,60,22);
    Rect(card.SelectedBorder.transform,0,0,400,380);Rect(card.Check.transform,340,14,44,46);
    for(int j=0;j<3;j++){
     var row=card.transform.Find("Resource_"+j);Rect(row,24+120*j,242,112,48);
     var icon=row.Find("Icon").GetComponent<Image>();var r=icon.rectTransform;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(16,-24);r.sizeDelta=new Vector2(30,30);
     TextAt(row.Find("Value").GetComponent<Text>(),40,0,72,48,30);
    }
   }
   TextAt(c.Hint,480,465,1360,54,28);
   var details=g.transform.Find("HomeDetails");Rect(details,80,550,1760,320);
   TextAt(details.Find("PartyLabel").GetComponent<Text>(),86,26,334,46,28);Rect(details.Find("PartyIcon"),40,35,30,30);
   for(int i=0;i<2;i++){
    var r=c.PartyPortraits[i].rectTransform;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(126+204*i,-156);r.sizeDelta=new Vector2(132,116);
    TextAt(c.PartyNames[i],40+204*i,224,172,44,26,TextAnchor.MiddleCenter);
   }
   TextAt(c.LocationName,530,26,600,50,34);TextAt(c.LocationDescription,530,108,600,148,28,TextAnchor.UpperLeft);
   TextAt(details.Find("ResourceTitle").GetComponent<Text>(),1220,26,490,50,28);
   TextAt(c.Resources,1220,108,490,156,28,TextAnchor.UpperLeft);
   var icons=c.ResourceIcons.transform;Rect(icons,1220,100,490,168);c.ResourceValues=new Text[3];
   string[] iconNames={"icon-supplies","icon-ammo","icon-recovery"};
   for(int i=0;i<3;i++){
    var icon=icons.Find(iconNames[i]).GetComponent<Image>();var r=icon.rectTransform;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition=new Vector2(16,-24-52*i);r.sizeDelta=new Vector2(30,30);
    var old=icons.Find("Value_"+i);if(old)Object.DestroyImmediate(old.gameObject);
    var t=Object.Instantiate(c.Resources,icons);t.name="Value_"+i;t.gameObject.SetActive(true);TextAt(t,50,52*i,440,48,28);t.text="";c.ResourceValues[i]=t;
   }
   foreach(Transform t in details)if(t.name=="Divider"){var r=(RectTransform)t;r.anchoredPosition=new Vector2(r.anchoredPosition.x,-32);r.sizeDelta=new Vector2(2,256);}
   Rect(c.Back.transform,80,974,410,76);Rect(c.Continue.transform,1430,974,410,76);
   TextAt(c.Back.GetComponentInChildren<Text>(),60,0,326,76,30,TextAnchor.MiddleCenter);TextAt(c.Continue.GetComponentInChildren<Text>(),76,0,310,76,30,TextAnchor.MiddleCenter);
   PrefabUtility.SaveAsPrefabAsset(g,path);
  }finally{PrefabUtility.UnloadPrefabContents(g);}
  AssetDatabase.SaveAssets();return "Header safe area, equal cards, inset checks, resource rows, centered party portraits and shared footer margins saved.";
 }
}
