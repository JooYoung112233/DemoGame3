using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Applies only visitor trade layout. Run after the everyday item catalog builder.
public static class BuildEverydayTradeUI
{
    const string Folder="Assets/Prefabs/Settlement/";
    [Serializable] sealed class NameMeasure
    {
        public string surface,id,name;
        public int font;
        public float width,height,preferredWidth,preferredHeight;
        public bool fits;
    }
    static void Place(Transform transform,float x,float y,float width,float height)
    {
        var rect=(RectTransform)transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);
    }
    static RectTransform Node(Transform parent,string name,float x,float y,float width,float height)
    {
        var old=parent.Find(name);var rect=old?(RectTransform)old:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        if(!old)rect.SetParent(parent,false);Place(rect,x,y,width,height);return rect;
    }
    static Text Label(RectTransform rect,Font font,string value,int size,Color color)
    {
        var text=rect.GetComponent<Text>()??rect.gameObject.AddComponent<Text>();
        text.font=font;text.fontSize=size;text.text=value;text.color=color;text.alignment=TextAnchor.MiddleCenter;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        text.resizeTextForBestFit=false;text.raycastTarget=false;return text;
    }
    static Button Arrow(Transform parent,string name,float x,Sprite paper,Font font,string caption)
    {
        var rect=Node(parent,name,x,424,92,48);var image=rect.GetComponent<Image>()??rect.gameObject.AddComponent<Image>();
        image.sprite=paper;image.color=Color.white;image.raycastTarget=true;
        var button=rect.GetComponent<Button>()??rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        Label(Node(rect,"Label",0,0,92,48),font,caption,23,new Color(.045f,.065f,.06f));return button;
    }
    static InventorySlot[] Stock(SettlementVisitorPanel panel,bool ours)
    {
        var source=ours?panel.OurSlots:panel.TheirSlots;
        if(source==null||source.Length<12)throw new InvalidOperationException("Visitor trade needs the existing twelve item slots.");
        var parent=source[0].transform.parent;var font=panel.GiveDetails.font;
        for(int i=0;i<source.Length;i++)
        {
            var slot=source[i];slot.gameObject.SetActive(i<12);
            if(PrefabUtility.IsPartOfPrefabInstance(slot.gameObject))PrefabUtility.RecordPrefabInstancePropertyModifications(slot.gameObject);
            if(i>=12)continue;
            // Three dense rows leave pagination above the existing detail paper.
            Place(slot.transform,38+(i%4)*183,84+(i/4)*112,174,104);
            // InventorySlot's paper is its root Image; preserve the root's cell geometry.
            var paper=slot.Paper.rectTransform;
            if(paper!=slot.transform){paper.anchorMin=Vector2.zero;paper.anchorMax=Vector2.one;paper.offsetMin=paper.offsetMax=Vector2.zero;}
            Place(slot.Icon.transform,49,4,76,62);slot.Icon.preserveAspect=true;
            Place(slot.Count.transform,128,2,38,34);slot.Count.fontSize=22;slot.Count.alignment=TextAnchor.MiddleRight;
            slot.Count.resizeTextForBestFit=true;slot.Count.resizeTextMinSize=12;slot.Count.resizeTextMaxSize=22;
            slot.Count.horizontalOverflow=HorizontalWrapMode.Wrap;slot.Count.verticalOverflow=VerticalWrapMode.Truncate;
            Place(slot.Label.transform,8,69,158,32);slot.Label.fontSize=21;
            slot.Label.horizontalOverflow=HorizontalWrapMode.Wrap;slot.Label.verticalOverflow=VerticalWrapMode.Truncate;slot.Label.resizeTextForBestFit=false;
            foreach(var component in slot.GetComponentsInChildren<Component>(true))
                if(component&&PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        var footer=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");
        var previous=Arrow(parent,"PreviousStockPage",38,footer,font,"‹ 이전");
        var next=Arrow(parent,"NextStockPage",678,footer,font,"다음 ›");
        var label=Label(Node(parent,"StockPage",144,424,520,48),font,"물품 1–12 / 22종 · 1 / 2쪽",22,new Color(.95f,.92f,.82f));
        if(ours){panel.OurPreviousPage=previous;panel.OurNextPage=next;panel.OurPageLabel=label;}
        else{panel.TheirPreviousPage=previous;panel.TheirNextPage=next;panel.TheirPageLabel=label;}
        return source.Take(12).ToArray();
    }
    static void Apply(SettlementVisitorPanel panel)
    {
        panel.OurSlots=Stock(panel,true);panel.TheirSlots=Stock(panel,false);
        // Two fixed lines: departure time, then demand and the exact preferred buying price.
        panel.Status.fontSize=22;panel.Status.resizeTextForBestFit=false;
        panel.Status.horizontalOverflow=HorizontalWrapMode.Wrap;panel.Status.verticalOverflow=VerticalWrapMode.Truncate;
        if(PrefabUtility.IsPartOfPrefabInstance(panel))PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
        if(PrefabUtility.IsPartOfPrefabInstance(panel.Status))PrefabUtility.RecordPrefabInstancePropertyModifications(panel.Status);
    }
    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        foreach(string name in new[]{"VisitorPanel","SettlementScreen"})
        {
            string path=Folder+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var panel in root.GetComponentsInChildren<SettlementVisitorPanel>(true))Apply(panel);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();return Validate();
    }
    public static string Validate()
    {
        foreach(string name in new[]{"VisitorPanel","SettlementScreen"})
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab");
            foreach(var panel in root.GetComponentsInChildren<SettlementVisitorPanel>(true))
            {
                if(panel.OurSlots.Length!=12||panel.TheirSlots.Length!=12||!panel.OurPreviousPage||!panel.OurNextPage||!panel.TheirPreviousPage||!panel.TheirNextPage||!panel.OurPageLabel||!panel.TheirPageLabel)
                    throw new InvalidOperationException("Incomplete paged trade references: "+name);
                foreach(var slot in panel.OurSlots.Concat(panel.TheirSlots))
                {
                    var icon=CellRect(slot.Icon.rectTransform);var count=CellRect(slot.Count.rectTransform);var label=CellRect(slot.Label.rectTransform);
                    if(icon.Overlaps(count)||icon.Overlaps(label)||count.Overlaps(label)||Mathf.Abs(icon.center.x-87)>.1f||label.yMax>104||icon.yMax>104||count.yMax>104)
                        throw new InvalidOperationException("Trade icon/count/name are not separated inside the cell: "+name+" / "+slot.name);
                }
                var catalog=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"SettlementScreen.prefab").GetComponent<SettlementController>();
                foreach(var good in panel.Goods)
                {
                    var item=catalog.CraftPanel.Materials.First(m=>m.Id==good.Id);var prototype=panel.OurSlots[0].Label;
                    using(var generator=new TextGenerator())
                    {
                        float height=generator.GetPreferredHeight(item.Name,prototype.GetGenerationSettings(prototype.rectTransform.rect.size))/prototype.pixelsPerUnit;
                        if(height>prototype.rectTransform.rect.height+.5f)throw new InvalidOperationException("Trade name exceeds its single line: "+item.Name);
                    }
                }
            }
        }
        return "PASS: visitor trade preserves its frame, shows twelve positive-stock goods per page, has explicit page controls/counts, centred 76×62 icons and separate single-line names. "+ValidateNames()+" Native runtime inspection still required.";
    }
    static Rect CellRect(RectTransform rect)=>new Rect(rect.anchoredPosition.x,-rect.anchoredPosition.y,rect.rect.width,rect.rect.height);
    public static string ValidateNames()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"SettlementScreen.prefab").GetComponent<SettlementController>();
        var measures=new List<NameMeasure>();
        foreach(var item in catalog.InventoryPanel.Items)
            Measure(measures,"inventory",item.Id,item.Name,catalog.InventoryPanel.SlotPrefab.Label);
        foreach(var item in catalog.VisitorPanel.Goods)
        {
            string name=catalog.CraftPanel.Materials.First(m=>m.Id==item.Id).Name;
            Measure(measures,"trade-our",item.Id,name,catalog.VisitorPanel.OurSlots[0].Label);
            Measure(measures,"trade-visitor",item.Id,name,catalog.VisitorPanel.TheirSlots[0].Label);
        }
        foreach(var recipe in catalog.CraftPanel.Recipes)
            Measure(measures,"craft-recipe",recipe.Id,recipe.Name,catalog.CraftPanel.RecipePrefab.Label);
        foreach(var meal in catalog.CookingPanel.Meals)
            Measure(measures,"cooking-recipe",meal.Id,meal.Name,catalog.CookingPanel.RecipePrefab.Label);
        var newCraftIds=new[]{"water-fuel","bandage-alcohol","flashlight-parts","nails-lubricant"};
        var newCookingIds=new[]{"warm-fuel","stew-fuel"};
        foreach(var recipe in catalog.CraftPanel.Recipes.Where(r=>newCraftIds.Contains(r.Id)))
            Measure(measures,"craft-description",recipe.Id,recipe.Description,catalog.CraftPanel.DetailDescription);
        foreach(var meal in catalog.CookingPanel.Meals.Where(m=>newCookingIds.Contains(m.Id)))
            Measure(measures,"cooking-description",meal.Id,meal.Description,catalog.CookingPanel.Description);
        // Ingredient names are displayed beside recipes too; use their actual row prototype.
        foreach(var material in catalog.CraftPanel.Materials)
            Measure(measures,"craft-material",material.Id,material.Name,catalog.CraftPanel.CostPrefab.Label);
        foreach(var ingredient in catalog.CookingPanel.Ingredients)
            Measure(measures,"cooking-ingredient",ingredient.Id,ingredient.Name,catalog.CookingPanel.CostPrefab.Label);
        var failures=measures.Where(m=>!m.fits).ToArray();
        int descriptionCount=measures.Count(m=>m.surface=="craft-description"||m.surface=="cooking-description");
        string path=Path.GetFullPath("아트/리소스검토/everyday-labels-static.json");
        File.WriteAllText(path,Newtonsoft.Json.JsonConvert.SerializeObject(new{utc=DateTime.UtcNow.ToString("O"),result=failures.Length==0&&descriptionCount==6?"PASS":"FAIL",inventoryCount=catalog.InventoryPanel.Items.Length,tradeCount=catalog.VisitorPanel.Goods.Length,descriptionCount,measures,failures},Newtonsoft.Json.Formatting.Indented));
        if(descriptionCount!=6)throw new InvalidOperationException("Expected all six everyday recipe descriptions; run BuildEverydayRecipes first. "+path);
        if(failures.Length>0)throw new InvalidOperationException("Label fit review failed: "+path+"\n"+string.Join("\n",failures.Select(m=>m.surface+" / "+m.id+" '"+m.name+"' font "+m.font+": preferred "+m.preferredWidth+"×"+m.preferredHeight+" in "+m.width+"×"+m.height)));
        return "All "+catalog.InventoryPanel.Items.Length+" inventory and "+catalog.VisitorPanel.Goods.Length+" trade names, "+catalog.CraftPanel.Recipes.Length+" craft and "+catalog.CookingPanel.Meals.Length+" cooking row names plus ingredient labels and six new recipe descriptions fit their existing font/rect; "+path;
    }
    static void Measure(List<NameMeasure> output,string surface,string id,string value,Text prototype)
    {
        var measure=new NameMeasure{surface=surface,id=id,name=value,font=prototype.fontSize,width=prototype.rectTransform.rect.width,height=prototype.rectTransform.rect.height};
        // Fixed-font labels and recipe descriptions retain their font and wrapping configuration.
        var settings=prototype.GetGenerationSettings(new Vector2(measure.width,0));
        settings.resizeTextForBestFit=false;settings.verticalOverflow=VerticalWrapMode.Overflow;
        using(var generator=new TextGenerator())
        {
            measure.preferredHeight=generator.GetPreferredHeight(value??"",settings)/prototype.pixelsPerUnit;
            settings.generationExtents=Vector2.zero;
            measure.preferredWidth=generator.GetPreferredWidth(value??"",settings)/prototype.pixelsPerUnit;
        }
        measure.fits=measure.preferredHeight<=measure.height+2&&(prototype.horizontalOverflow!=HorizontalWrapMode.Overflow||measure.preferredWidth<=measure.width+2);
        output.Add(measure);
    }
}
