using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Run after the four new body sprites have been generated/imported.
// Changes references/bounds only; leaves portraits, candidate balance, and battle rules intact.
public static class WirePartyStandeeBodies
{
    public static readonly string[] Ids={"scout","medic","cook","mechanic","guard","researcher"};
    public static string PathFor(string id)
    {
        if(id=="scout"||id=="medic")return "Assets/Art/Settlement/standee-"+id+".png";
        string refined="Assets/Art/Tokens/party-"+id+"-body-v3.png";
        if(id!="guard"&&id!="researcher"&&File.Exists(refined))return refined;
        string revised="Assets/Art/Tokens/party-"+id+"-body-v2.png";
        return File.Exists(revised)?revised:"Assets/Art/Tokens/party-"+id+"-body.png";
    }

    public static string RevisedPeople()
    {
        foreach(string id in new[]{"cook","mechanic","guard","researcher"})
            if(!File.Exists(PathFor(id)))throw new InvalidOperationException("Missing selected body "+id);
        string report=Run();
        var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        foreach(var c in roster.Candidates)c.BodyScale=1;
        EditorUtility.SetDirty(roster);AssetDatabase.SaveAssets();
        return report+"\nDisplay heights matched to the original scout/medic after user review; original sources preserved, no body clipping.";
    }

    public static string Run()
    {
        var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        if(!roster)throw new InvalidOperationException("PartyRoster asset is missing.");
        var planned=new List<(PartyCandidate candidate,Sprite body,Rect pixels)>();
        // Validate every source before changing any catalog data.
        foreach(var id in Ids)
        {
            var candidate=roster.Candidates.Single(c=>c.Id==id);
            var body=AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(id));
            if(!body)throw new InvalidOperationException("Body sprite not imported: "+PathFor(id));
            // The shared world shader removes the old painted paper rim from these two sources.
            // Cache the remaining ink bounds so old and new actors use the same visible-height rule.
            var pixels=ReadAlphaBounds(body,id=="scout"||id=="medic");
            planned.Add((candidate,body,pixels));
        }
        foreach(var entry in planned)
        {
            entry.candidate.Body=entry.body;
            entry.candidate.BodyVisiblePixels=entry.pixels;
        }
        EditorUtility.SetDirty(roster);
        const string battlePath="Assets/Prefabs/Settlement/ExpeditionBattlePanel.prefab";
        var prefab=PrefabUtility.LoadPrefabContents(battlePath);
        try
        {
            var battle=prefab.GetComponent<ExpeditionBattlePanel>();
            var bounds=(battle.VisibleBounds??Array.Empty<ExpeditionBattlePanel.PawnBounds>()).Where(x=>x!=null).ToList();
            foreach(var entry in planned)
            {
                var known=bounds.FirstOrDefault(x=>x.Sprite==entry.body);
                if(known==null){known=new ExpeditionBattlePanel.PawnBounds{Sprite=entry.body};bounds.Add(known);}
                known.Pixels=entry.pixels;
            }
            battle.VisibleBounds=bounds.ToArray();
            PrefabUtility.SaveAsPrefabAsset(prefab,battlePath);
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        AssetDatabase.SaveAssets();
        return "Wired six character IDs to individual bodies and cached alpha bounds; preserved portraits and battle rules.\n"+
            string.Join("\n",planned.Select(p=>p.candidate.Id+" -> "+AssetDatabase.GetAssetPath(p.body)+" | alpha "+p.pixels));
    }

    public static Rect ReadAlphaBounds(Sprite sprite,bool removeLegacyPaper=false,float paperCutoff=.38f)
    {
        string path=AssetDatabase.GetAssetPath(sprite);
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try
        {
            string absolute=Path.Combine(Directory.GetParent(Application.dataPath).FullName,path);
            if(!ImageConversion.LoadImage(source,File.ReadAllBytes(absolute),false))throw new InvalidOperationException("Could not read PNG: "+path);
            float sx=(float)source.width/sprite.texture.width,sy=(float)source.height/sprite.texture.height;
            var area=sprite.rect;
            int left=Mathf.RoundToInt(area.xMin*sx),bottom=Mathf.RoundToInt(area.yMin*sy);
            int right=Mathf.RoundToInt(area.xMax*sx),top=Mathf.RoundToInt(area.yMax*sy);
            var colors=source.GetPixels32();int minX=right,minY=top,maxX=left-1,maxY=bottom-1,clear=0;
            for(int y=bottom;y<top;y++)for(int x=left;x<right;x++)
            {
                var pixel=colors[y*source.width+x];float alpha=pixel.a;if(pixel.a<32)clear++;
                if(removeLegacyPaper)
                {
                    float brightness=Mathf.Max(pixel.r,Mathf.Max(pixel.g,pixel.b))/255f;
                    float t=Mathf.Clamp01((brightness-(paperCutoff-.035f))/.07f);
                    alpha*=1-t*t*(3-2*t);
                }
                if(alpha<32)continue;
                minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
            }
            if(maxX<minX||clear==0)throw new InvalidOperationException("Body needs visible ink and real transparent pixels: "+path);
            return new Rect((minX-left)/sx,(minY-bottom)/sy,(maxX-minX+1)/sx,(maxY-minY+1)/sy);
        }
        finally{Object.DestroyImmediate(source);}
    }
}
