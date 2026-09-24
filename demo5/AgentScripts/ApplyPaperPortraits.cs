using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Character images only: card paper, item icons, text and layout remain untouched.
public static class ApplyPaperPortraits
{
    const string Folder="Assets/Prefabs";
    const string MaterialPath="Assets/Settings/PaperPortrait.mat";
    static readonly HashSet<string> PortraitNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {"Portrait","ActorPortrait","TargetPortrait","MemberPortrait","RecruitPortrait","ProfilePortrait","DetailPortrait"};
    static string[] Paths()=>AssetDatabase.FindAssets("t:Prefab",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath)
        .OrderBy(p=>AssetDatabase.GetDependencies(p,false).Length).ThenBy(p=>p,StringComparer.Ordinal).ToArray();

    static Dictionary<Image,string> Select(GameObject root)
    {
        var selected=new Dictionary<Image,string>();
        foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if(!component)continue;
            var serialized=new SerializedObject(component);var property=serialized.GetIterator();
            while(property.Next(true))
            {
                if(property.propertyType!=SerializedPropertyType.ObjectReference||property.propertyPath.IndexOf("Portrait",StringComparison.OrdinalIgnoreCase)<0)continue;
                if(property.objectReferenceValue is Image image&&image.transform.IsChildOf(root.transform))selected[image]="serialized portrait";
            }
        }
        foreach(var image in root.GetComponentsInChildren<Image>(true))
        {
            if(selected.ContainsKey(image))continue;
            if(image.GetComponent<BattlePortraitFit>()){selected[image]="dynamic battle portrait";continue;}
            if(PortraitNames.Contains(image.name)){selected[image]="named portrait";continue;}
            var sprite=image.overrideSprite?image.overrideSprite:image.sprite;
            string path=sprite?AssetDatabase.GetAssetPath(sprite):"";
            string filename=Path.GetFileNameWithoutExtension(path);
            bool portrait=path.StartsWith("Assets/Art/PartySelection/portrait-",StringComparison.Ordinal);
            bool creature=path.StartsWith("Assets/Art/Creatures/",StringComparison.Ordinal);
            bool body=path.StartsWith("Assets/Art/Tokens/",StringComparison.Ordinal)&&
                (filename=="infected-body"||filename=="infected-body-v4"||filename=="scout-body"||filename=="sentry-body"||filename.StartsWith("party-",StringComparison.Ordinal)&&filename.EndsWith("-body",StringComparison.Ordinal));
            if(portrait||creature||body)selected[image]="character sprite";
        }
        return selected;
    }

    static string Category(string path)
    {
        string name=Path.GetFileNameWithoutExtension(path);
        if(name.IndexOf("Battle",StringComparison.OrdinalIgnoreCase)>=0)return "battle / dynamic enemies";
        if(name.IndexOf("Visitor",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Recruit",StringComparison.OrdinalIgnoreCase)>=0)return "visitor / recruitment";
        if(name.IndexOf("Bag",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Inventory",StringComparison.OrdinalIgnoreCase)>=0)return "inventory / transfer";
        if(name.IndexOf("Work",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Craft",StringComparison.OrdinalIgnoreCase)>=0||name.IndexOf("Cooking",StringComparison.OrdinalIgnoreCase)>=0)return "work / cooking";
        if(name.IndexOf("Return",StringComparison.OrdinalIgnoreCase)>=0)return "return summary";
        return "party / exploration / assembled screen";
    }

    public static string Audit()=>Process(false);
    public static string Run()=>Process(true);
    static string Process(bool apply)
    {
        // Only isolated prefab contents are edited; never instantiate into or alter the user's live scene.
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(apply&&!material)throw new InvalidOperationException("Run ApplySharedPaperStandeeStyle first: "+MaterialPath);
        var categories=new Dictionary<string,int>();var reasons=new Dictionary<string,int>();var report=new List<string>();
        int changed=0,images=0;
        foreach(var path in Paths())
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var selected=Select(root);if(selected.Count==0)continue;
                bool dirty=false;
                foreach(var item in selected)
                {
                    var image=item.Key;images++;string category=Category(path);
                    categories[category]=categories.TryGetValue(category,out int count)?count+1:1;
                    reasons[item.Value]=reasons.TryGetValue(item.Value,out count)?count+1:1;
                    if(!apply)continue;
                    var style=image.GetComponent<PaperPortraitStyle>();
                    if(!style){style=image.gameObject.AddComponent<PaperPortraitStyle>();dirty=true;}
                    if(style.StyleMaterial!=material||image.material!=material){style.Configure(material);dirty=true;}
                    if(dirty){EditorUtility.SetDirty(style);EditorUtility.SetDirty(image);}
                }
                if(dirty){PrefabUtility.SaveAsPrefabAsset(root,path);changed++;}
                report.Add(Path.GetFileName(path)+": "+selected.Count);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        if(apply)AssetDatabase.SaveAssets();
        return (apply?"Applied":"Audit")+": "+images+" portrait instances in "+report.Count+" prefabs; changed "+changed+" assets.\n"+
            string.Join("\n",categories.Select(x=>x.Key+": "+x.Value))+"\nSelectors: "+string.Join(", ",reasons.Select(x=>x.Key+" "+x.Value))+"\n"+string.Join("\n",report);
    }

    public static string Verify()
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);int count=0;
        if(!material)throw new InvalidOperationException("Portrait ink material missing.");
        foreach(var path in Paths())
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var selected=Select(root);
            foreach(var image in root.GetComponentsInChildren<Image>(true))
            {
                var style=image.GetComponent<PaperPortraitStyle>();
                if(selected.ContainsKey(image))
                {
                    if(!style||style.StyleMaterial!=material||image.material!=material)throw new InvalidOperationException("Unstyled portrait: "+path+" / "+image.name);
                    count++;
                }
                else if(style||image.material==material)throw new InvalidOperationException("Non-character image was styled: "+path+" / "+image.name);
            }
        }
        return "PASS: "+count+" selected portraits styled; no item/icon/paper image styled. Dynamic battle portraits included by component reference and portrait mesh fit.";
    }
}
