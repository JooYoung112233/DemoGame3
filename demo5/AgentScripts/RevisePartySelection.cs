using System;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class RevisePartySelection
{
    const string Folder="Assets/Prefabs/PartySelection/";
    static Color Ink=new Color(.06f,.095f,.095f);
    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;
        r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    static Text Text(string name,Transform parent,float x,float y,float w,float h,string value,int size,bool bold=false)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<Text>();t.text=value;t.fontSize=size;t.color=Ink;t.raycastTarget=false;
        t.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/FrontEnd/Fonts/Gaegu/Gaegu-"+(bold?"Bold":"Regular")+".ttf");
        t.verticalOverflow=VerticalWrapMode.Truncate;t.lineSpacing=1.08f;return t;
    }
    static void Move(Transform root,string name,float x,float y){var r=(RectTransform)root.Find(name);r.anchoredPosition=new Vector2(x,-y);}
    public static string Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play first");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene changes; preserve first");
        var header=PrefabUtility.LoadPrefabContents(Folder+"SelectionHeader.prefab");
        try{
            var step=header.transform.Find("StepPaper");if(step)Object.DestroyImmediate(step.gameObject);
            Move(header.transform,"Heading",30,42);((RectTransform)header.transform.Find("Heading")).sizeDelta=new Vector2(390,67);
            PrefabUtility.SaveAsPrefabAsset(header,Folder+"SelectionHeader.prefab");
        }finally{PrefabUtility.UnloadPrefabContents(header);}
        var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        foreach(var c in roster.Candidates){if(!string.IsNullOrEmpty(c.TraitTitle))continue;
            switch(c.Id){
            case "scout":c.TraitTitle="길을 읽는 눈";c.TraitDescription="낯선 장소에서도 작은 흔적을 놓치지 않습니다.\n수색할 곳과 돌아올 길을 먼저 살핍니다.";c.Characteristics="호기심이 많고 주변을 세심하게 관찰합니다.\n앞장서서 길을 찾는 일을 좋아합니다.\n새로운 발견 앞에서는 신중함이 필요합니다.";break;
            case "mechanic":c.TraitTitle="고쳐 쓰는 손";c.TraitDescription="망가진 물건에서도 쓸 만한 부분을 찾습니다.\n도구를 챙겨 설비를 살피는 역할이 어울립니다.";c.Characteristics="말보다 손이 먼저 움직이는 실용적인 성격입니다.\n부품을 버리지 않고 차곡차곡 모읍니다.\n도구와 재료가 있어야 실력을 발휘합니다.";break;
            case "medic":c.TraitTitle="상처를 살피는 눈";c.TraitDescription="동료의 작은 이상도 눈여겨봅니다.\n부상자를 돌보고 회복을 돕는 역할입니다.";c.Characteristics="침착하고 다른 사람의 상태를 먼저 살핍니다.\n위험한 상황에서도 동료를 두고 가지 않습니다.\n의약품을 언제 사용할지 신중하게 판단합니다.";break;
            case "cook":c.TraitTitle="한 끼의 온기";c.TraitDescription="남은 식재료로 함께 먹을 한 끼를 고민합니다.\n정착지의 식사와 식량 정리에 어울립니다.";c.Characteristics="사소한 일상을 소중히 여기는 성격입니다.\n식재료의 상태와 남은 양을 자주 확인합니다.\n제대로 요리하려면 재료와 설비가 필요합니다.";break;
            case "researcher":c.TraitTitle="흔적을 잇는 기록";c.TraitDescription="메모와 낯선 물건에서 단서를 찾습니다.\n모은 정보를 정리하고 의미를 짚어 봅니다.";c.Characteristics="궁금한 것은 기록하며 차근차근 살펴봅니다.\n흩어진 정보의 연결을 찾는 일을 좋아합니다.\n조사에 몰두할 때는 동료의 경계가 필요합니다.";break;
            case "guard":c.TraitTitle="주변을 지키는 눈";c.TraitDescription="주변의 소리와 움직임을 경계합니다.\n동료가 수색하는 동안 위험을 살피는 역할입니다.";c.Characteristics="책임감이 강하고 먼저 주변을 확인합니다.\n돌아갈 길과 몸을 피할 곳을 눈여겨봅니다.\n혼자 버티기보다 동료와 호흡을 맞춥니다.";break;
            }
        }
        EditorUtility.SetDirty(roster);
        var panel=Rect("CandidateDetails",null,76,552,1704,340);var paper=panel.gameObject.AddComponent<Image>();paper.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/count-paper.png");paper.raycastTarget=false;
        var d=panel.gameObject.AddComponent<PartyCandidateDetails>();
        d.Name=Text("Name",panel,48,25,280,60,"탐험가 1",40,true);
        d.Portrait=Rect("Portrait",panel,48,92,160,125).gameObject.AddComponent<Image>();d.Portrait.preserveAspect=true;d.Portrait.raycastTarget=false;
        d.Stats=Text("Stats",panel,48,236,280,42,"",26);
        d.SelectionState=Text("SelectionState",panel,48,282,280,35,"",22);
        foreach(float x in new[]{342f,966f}){var line=Rect("Divider",panel,x,40,2,254).gameObject.AddComponent<Image>();line.color=new Color(.18f,.22f,.18f,.22f);line.raycastTarget=false;}
        Text("TraitLabel",panel,388,34,530,43,"특성",27,true);
        d.TraitTitle=Text("TraitTitle",panel,388,87,530,55,"",36,true);
        d.TraitDescription=Text("TraitDescription",panel,388,154,530,155,"",28);
        Text("CharacteristicsLabel",panel,1012,34,630,43,"특징",27,true);
        d.Characteristics=Text("Characteristics",panel,1012,96,630,209,"",28);
        d.Bind(roster.Candidates[0],false);
        var detailPrefab=PrefabUtility.SaveAsPrefabAsset(panel.gameObject,Folder+"CandidateDetails.prefab");Object.DestroyImmediate(panel.gameObject);
        var root=PrefabUtility.LoadPrefabContents(Folder+"PartySelectionScreen.prefab");
        try{
            Move(root.transform,"SelectionHeader",64,94);
            for(int i=0;i<6;i++)Move(root.transform,"Candidate_"+i,570+i*201,122);
            Move(root.transform,"PreviousPage",497,246);Move(root.transform,"NextPage",1797,246);
            Move(root.transform,"PageNumber",1080,447);Move(root.transform,"SelectionHint",570,480);
            Move(root.transform,"Back",76,944);Move(root.transform,"Continue",1450,944);
            var old=root.transform.Find("CandidateDetails");if(old)Object.DestroyImmediate(old.gameObject);
            var detail=(GameObject)PrefabUtility.InstantiatePrefab(detailPrefab,root.transform);
            root.GetComponent<PartySelectionController>().Details=detail.GetComponent<PartyCandidateDetails>();
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"PartySelectionScreen.prefab");
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/PartySelection.unity");
        return "Removed step 02; moved selection up 210px; added nested CandidateDetails prefab with editable roster traits.";
    }
}
