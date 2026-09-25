using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;

// Additive pass: keep existing inventory, starting supplies, room layout and completed search records.
// Run after legacy loot/inventory builders, before BuildEverydayRecipes and BuildEverydayTradeUI.
public static class BuildEverydayGoods
{
    const string P = "Assets/Prefabs/Settlement/", Art = "Assets/Art/EverydayGoods/";
    sealed class Spec
    {
        public string Id, Name, Description;
        public int Value, Stock = 2, Category = 3;
    }
    static readonly Spec[] Specs = {
        new Spec { Id="fuel",Name="연료통",Value=4,Description="조리·정수용 연료\n목재 대신 사용" },
        new Spec { Id="lubricant",Name="윤활유",Value=3,Description="공구 연구 후 못 가공\n작업대 제작 재료" },
        new Spec { Id="battery",Name="건전지",Value=3,Description="손전등 제작 재료\n공구 연구 후 사용" },
        new Spec { Id="tobacco",Name="담배",Value=3,Description="방문자 교환용 기호품\n선호 시 교환 가치 +1" },
        new Spec { Id="coffee",Name="커피",Value=3,Category=1,Description="방문자 교환용 기호품\n선호 시 교환 가치 +1" },
        new Spec { Id="alcohol",Name="소독 알코올",Value=4,Description="붕대 제작용 소독제\n작업대 · 천과 함께" },
        new Spec { Id="electrical-parts",Name="전기 부품",Value=4,Description="손전등 제작 재료\n공구 연구 후 사용" },
        new Spec { Id="weapon-parts",Name="무기 부품",Value=6,Stock=1,Description="방문자 교환용 부품\n교환 가치 6" }
    };
    static readonly (int site,string id,int count,int chance)[] Drops = {
        (1,"tobacco",1,60),(1,"coffee",1,65),(2,"lubricant",1,75),
        (4,"battery",1,80),(4,"electrical-parts",1,70),(5,"fuel",1,75),
        (6,"alcohol",1,65),(8,"weapon-parts",1,80)
    };
    static bool IsNew(string id) => Specs.Any(s=>s.Id==id);
    static Sprite Icon(string id) => AssetDatabase.LoadAssetAtPath<Sprite>(Art+id+".png");

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        foreach(var s in Specs) if(!File.Exists(Art+s.Id+".png")) throw new FileNotFoundException("Missing independent item icon",Art+s.Id+".png");
        AssetDatabase.Refresh();
        foreach(var s in Specs) Import(s.Id);
        foreach(var name in new[]{"InventoryPanel","CraftWorkPanel","VisitorPanel","ExpeditionArrivalPanel","SettlementScreen"})
        {
            string path=P+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var inv in root.GetComponentsInChildren<SettlementInventoryPanel>(true)) Inventory(inv);
                foreach(var craft in root.GetComponentsInChildren<SettlementCraftPanel>(true)) Materials(craft);
                foreach(var visitor in root.GetComponentsInChildren<SettlementVisitorPanel>(true)) Goods(visitor);
                foreach(var loot in root.GetComponentsInChildren<ExpeditionLootPanel>(true)) Loot(loot);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        return Validate();
    }
    static void Import(string id)
    {
        string path=Art+id+".png";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType=TextureImporterType.Sprite;imp.spriteImportMode=SpriteImportMode.Single;
        imp.alphaIsTransparency=true;imp.mipmapEnabled=false;imp.npotScale=TextureImporterNPOTScale.None;
        imp.maxTextureSize=2048;imp.filterMode=FilterMode.Bilinear;imp.wrapMode=TextureWrapMode.Clamp;
        var settings=new TextureImporterSettings();imp.ReadTextureSettings(settings);
        settings.spriteAlignment=(int)SpriteAlignment.Center;settings.spriteMeshType=SpriteMeshType.FullRect;
        imp.SetTextureSettings(settings);imp.SaveAndReimport();
        if(!Icon(id)) throw new InvalidOperationException("Icon was not imported as a sprite: "+id);
    }
    static void Inventory(SettlementInventoryPanel inv)
    {
        var list=inv.Items.ToList();
        foreach(var s in Specs)
        {
            var item=list.FirstOrDefault(i=>i.Id==s.Id);
            if(item==null){item=new SettlementInventoryPanel.Item{Id=s.Id};list.Add(item);}
            item.Name=s.Name;item.Description=s.Description;item.Icon=Icon(s.Id);item.Category=s.Category;
            item.Recovery=0;item.FieldUsable=false;item.UseVerb="";
        }
        inv.Items=list.ToArray();EditorUtility.SetDirty(inv);
    }
    static void Materials(SettlementCraftPanel craft)
    {
        var list=craft.Materials.ToList();
        foreach(var s in Specs)
        {
            var m=list.FirstOrDefault(x=>x.Id==s.Id);
            if(m==null){m=new SettlementCraftPanel.Material{Id=s.Id,Initial=0};list.Add(m);}
            m.Name=s.Name;m.Icon=Icon(s.Id);
        }
        craft.Materials=list.ToArray();EditorUtility.SetDirty(craft);
    }
    static void Goods(SettlementVisitorPanel visitor)
    {
        var list=visitor.Goods.ToList();
        foreach(var s in Specs)
        {
            var good=list.FirstOrDefault(x=>x.Id==s.Id);
            if(good==null){good=new SettlementVisitorPanel.Good{Id=s.Id};list.Add(good);}
            good.Value=s.Value;good.Initial=s.Stock;
        }
        visitor.Goods=list.ToArray();EditorUtility.SetDirty(visitor);
    }
    static void Loot(ExpeditionLootPanel loot)
    {
        // Some old standalone loot prefabs do not contain all nine sites. Never create rooms here.
        if(loot.Sites==null||loot.Sites.Length!=9) throw new InvalidOperationException("Expected current nine-site mall layout.");
        foreach(var grouping in Drops.GroupBy(d=>d.site))
        {
            var site=loot.Sites[grouping.Key];
            var list=site.Drops.Where(d=>!IsNew(d.Id)).ToList();
            foreach(var d in grouping) list.Add(new ExpeditionLootPanel.Drop{Id=d.id,Count=d.count,Chance=d.chance});
            site.Drops=list.ToArray();
        }
        EditorUtility.SetDirty(loot);
    }
    public static string Validate()
    {
        var c=AssetDatabase.LoadAssetAtPath<GameObject>(P+"SettlementScreen.prefab").GetComponent<SettlementController>();
        foreach(var s in Specs)
        {
            var item=c.InventoryPanel.Items.Single(x=>x.Id==s.Id);
            var material=c.CraftPanel.Materials.Single(x=>x.Id==s.Id);
            var good=c.VisitorPanel.Goods.Single(x=>x.Id==s.Id);
            if(!item.Icon||item.Icon!=material.Icon||item.Icon!=Icon(s.Id)||item.Recovery!=0||material.Initial!=0||good.Initial<1)
                throw new Exception("Item registry differs: "+s.Id);
            if(!c.ArrivalPanel.Loot.Sites.Any(x=>x.Drops.Any(d=>d.Id==s.Id&&d.Count>0&&d.Chance>0)))
                throw new Exception("No scavenging source: "+s.Id);
        }
        if(c.ArrivalPanel.Loot.Sites[0].Drops.Any(d=>IsNew(d.Id))) throw new Exception("Tutorial crate altered.");
        if(c.InventoryPanel.Items.GroupBy(x=>x.Id).Any(g=>g.Count()!=1)||c.CraftPanel.Materials.GroupBy(x=>x.Id).Any(g=>g.Count()!=1)||c.VisitorPanel.Goods.GroupBy(x=>x.Id).Any(g=>g.Count()!=1))
            throw new Exception("Duplicate goods IDs.");
        return "PASS: 8 independent item sprites, inventory/material/trade registries, no starting stock, all field sources, unchanged tutorial crate, no duplicate IDs.";
    }
    public static object Inspect()
    {
        var c=AssetDatabase.LoadAssetAtPath<GameObject>(P+"SettlementScreen.prefab").GetComponent<SettlementController>();
        var slots=new[]{c.InventoryPanel.SlotPrefab,c.ArrivalPanel.Loot.SlotPrefab};
        return new { Slots=slots.Select(s=>new {s.name,Label=s.Label.rectTransform.rect.ToString(),Font=s.Label.fontSize,Min=s.Label.resizeTextMinSize,Max=s.Label.resizeTextMaxSize,Fit=s.Label.resizeTextForBestFit,Icon=s.Icon.rectTransform.rect.ToString()}).ToArray(),
            Description=c.InventoryPanel.Description.rectTransform.rect.ToString(),DescriptionFont=c.InventoryPanel.Description.fontSize };
    }
}
