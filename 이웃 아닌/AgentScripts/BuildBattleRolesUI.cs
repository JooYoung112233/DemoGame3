using System;
using System.Collections.Generic;
using System.Linq;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

// Run after the character/creature data builders and other battle layout builders.
// Adds editable role glyphs/text to existing prefabs. Does not open/save a scene or alter combat data.
public static class BuildBattleRolesUI
{
    const string P="Assets/Prefabs/Settlement/";
    static readonly Color Ink=new Color(.045f,.065f,.06f),Cream=new Color(.95f,.92f,.82f);
    static Font font;
    static RectTransform Node(Transform parent,string name)
    {
        var old=parent.Find(name);
        if(old)return (RectTransform)old;
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;
    }
    static void Place(Transform transform,float x,float y,float w,float h)
    {
        var r=(RectTransform)transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);
        r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);
    }
    static Text Text(Transform parent,string name,float x,float y,float w,float h,int size,string value="")
    {
        var rect=Node(parent,name);Place(rect,x,y,w,h);
        var text=rect.GetComponent<Text>()??rect.gameObject.AddComponent<Text>();
        text.font=font;text.fontSize=size;text.color=Cream;text.alignment=TextAnchor.MiddleLeft;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
        text.resizeTextForBestFit=false;text.lineSpacing=1;text.raycastTarget=false;text.text=value;return text;
    }
    static BattleRoleBadge Badge(Transform parent,string name,float x,float y,float size,Color color)
    {
            var rect=Node(parent,name);Place(rect,x,y,size,size);
            if(!rect.GetComponent<CanvasRenderer>())rect.gameObject.AddComponent<CanvasRenderer>();
        var badge=rect.GetComponent<BattleRoleBadge>()??rect.gameObject.AddComponent<BattleRoleBadge>();
        badge.color=color;badge.raycastTarget=false;badge.SetRole(CreatureRole.Melee);return badge;
    }
    static void Edit(string name,Action<GameObject> edit)
    {
        string path=P+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
        try{edit(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }

    public static string Run()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before editing battle prefabs.");
        font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");
        if(!font)throw new InvalidOperationException("Approved game font is missing.");
        Edit("BattlePawnHud",root=>
        {
            var hud=root.GetComponent<BattlePawnHud>();
            var header=Node(root.transform,"RoleNameLine");
            header.anchorMin=header.anchorMax=new Vector2(.5f,0);header.pivot=new Vector2(.5f,0);
            header.anchoredPosition=new Vector2(0,21);header.sizeDelta=new Vector2(132,30);
            var layout=header.GetComponent<HorizontalLayoutGroup>()??header.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing=4;layout.padding=new RectOffset();layout.childAlignment=TextAnchor.MiddleCenter;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
            hud.RoleBadge=Badge(header,"RoleBadge",0,0,24,Cream);hud.RoleBadge.transform.SetAsFirstSibling();
            var iconSize=hud.RoleBadge.GetComponent<LayoutElement>()??hud.RoleBadge.gameObject.AddComponent<LayoutElement>();
            iconSize.minWidth=iconSize.preferredWidth=24;iconSize.minHeight=iconSize.preferredHeight=24;
            var outline=hud.RoleBadge.GetComponent<Outline>()??hud.RoleBadge.gameObject.AddComponent<Outline>();
            outline.effectColor=new Color(Ink.r,Ink.g,Ink.b,.92f);outline.effectDistance=new Vector2(1,-1);
            hud.Name.transform.SetParent(header,false);hud.Name.transform.SetAsLastSibling();
            var nameSize=hud.Name.GetComponent<LayoutElement>()??hud.Name.gameObject.AddComponent<LayoutElement>();
            nameSize.minWidth=0;nameSize.preferredWidth=104;nameSize.preferredHeight=30;
            hud.Name.fontSize=19;hud.Name.alignment=TextAnchor.MiddleCenter;hud.Name.horizontalOverflow=HorizontalWrapMode.Overflow;
            hud.RoleBadge.gameObject.SetActive(false);
        });
        foreach(var name in new[]{"BattleTurnCard","BattleOrderCard"})Edit(name,root=>
        {
            var card=root.GetComponent<BattleTurnCard>();bool order=name=="BattleOrderCard";
            // Preserve the portrait centre. A left gutter holds the glyph without covering the head or name.
            Place(card.Portrait.transform,20,order?8:12,order?62:56,order?72:56);
            if(order)
            {
                var portrait=card.Portrait.rectTransform;
                portrait.anchorMin=new Vector2(.15f,.33f);portrait.anchorMax=new Vector2(.85f,.8f);
                portrait.offsetMin=portrait.offsetMax=Vector2.zero;card.Portrait.preserveAspect=true;
            }
            card.RoleBadge=Badge(root.transform,"RoleBadge",1,6,order?14:18,Ink);
            if(!order)
            {
                var size=root.GetComponent<LayoutElement>()??root.AddComponent<LayoutElement>();
                size.minWidth=88;size.preferredWidth=96;size.flexibleWidth=0;
                size.minHeight=size.preferredHeight=108;size.flexibleHeight=0;
                var paper=card.Paper.rectTransform;paper.anchorMin=Vector2.zero;paper.anchorMax=Vector2.one;paper.offsetMin=paper.offsetMax=Vector2.zero;
                var portrait=card.Portrait.rectTransform;portrait.anchorMin=portrait.anchorMax=new Vector2(.5f,1);portrait.pivot=new Vector2(.5f,1);
                portrait.anchoredPosition=new Vector2(0,-12);portrait.sizeDelta=new Vector2(52,56);card.Portrait.preserveAspect=true;
                Place(card.RoleBadge.transform,1,8,16,16);
                var label=card.Label.rectTransform;label.anchorMin=new Vector2(0,1);label.anchorMax=Vector2.one;label.pivot=new Vector2(.5f,1);
                label.anchoredPosition=new Vector2(0,-73);label.sizeDelta=new Vector2(-8,30);
                card.Label.fontSize=17;card.Label.alignment=TextAnchor.MiddleCenter;
                card.Label.horizontalOverflow=HorizontalWrapMode.Wrap;card.Label.verticalOverflow=VerticalWrapMode.Truncate;card.Label.resizeTextForBestFit=false;
            }
            card.RoleBadge.gameObject.SetActive(false);
        });
        Edit("BattleUnitDetails",root=>Details(root.transform,false));
        Edit("BattleTargetDetails",root=>Details(root.transform,true));
        Edit("ExpeditionBattlePanel",root=>
        {
            var panel=root.GetComponent<ExpeditionBattlePanel>();
            TurnLayout(panel);
            var actor=panel.ActorPortrait.transform.parent;var target=panel.TargetPortrait.transform.parent;
            // Reapply to the real nested cards as old layout overrides can mask changes in their source prefab.
            Details(actor,false);Details(target,true);
            panel.ActorRoleBadge=actor.Find("RoleBadge").GetComponent<BattleRoleBadge>();
            panel.ActorRoleLabel=actor.Find("RoleLabel").GetComponent<Text>();panel.ActorTactic=actor.Find("Tactic").GetComponent<Text>();
            panel.TargetRoleBadge=target.Find("RoleBadge").GetComponent<BattleRoleBadge>();
            panel.TargetRoleLabel=target.Find("RoleLabel").GetComponent<Text>();panel.TargetTactic=target.Find("Tactic").GetComponent<Text>();
        });
        AssetDatabase.SaveAssets();return Validate();
    }

    static void TurnLayout(ExpeditionBattlePanel panel)
    {
        var content=panel.TurnContent;var viewport=(RectTransform)content.parent;
        var layout=content.GetComponent<HorizontalLayoutGroup>();
        layout.spacing=6;layout.padding=new RectOffset();layout.childAlignment=TextAnchor.MiddleCenter;
        layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=false;
        layout.childScaleWidth=layout.childScaleHeight=false;
        // Equal priority combines this viewport minimum with the LayoutGroup's minimum.
        // Nine cards fit; a larger reinforcement queue can still grow and use the existing scroll.
        var minimum=content.GetComponent<LayoutElement>()??content.gameObject.AddComponent<LayoutElement>();
        minimum.layoutPriority=0;minimum.minWidth=viewport.rect.width;minimum.preferredWidth=minimum.flexibleWidth=-1;
        var fitter=content.GetComponent<ContentSizeFitter>();fitter.horizontalFit=ContentSizeFitter.FitMode.MinSize;fitter.verticalFit=ContentSizeFitter.FitMode.Unconstrained;
        content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,.5f);content.anchoredPosition=Vector2.zero;
        content.sizeDelta=new Vector2(viewport.rect.width,108);
    }

    static void Details(Transform root,bool target)
    {
        Place(root.Find("PortraitPaper"),20,12,150,172);Place(root.Find("Portrait"),35,32,120,136);
        var info=root.Find("Info").GetComponent<Text>();
        var health=root.Find("Health");var healthBack=root.Find("HealthBackground");
        if(target)
        {
            var name=root.Find("Name").GetComponent<Text>();Place(name.transform,196,8,354,36);
            name.fontSize=24;name.horizontalOverflow=HorizontalWrapMode.Wrap;name.verticalOverflow=VerticalWrapMode.Truncate;
            Badge(root,"RoleBadge",196,46,24,Cream);
            Text(root,"RoleLabel",228,44,322,30,20,"근접형 · 방어 0");
            Place(info.transform,196,77,354,64);info.fontSize=21;
            Place(healthBack,196,145,354,15);Place(health,198,147,350,11);
            var chance=root.Find("Chance").GetComponent<Text>();Place(chance.transform,196,159,354,33);chance.fontSize=22;
        }
        else
        {
            var badge=Badge(root,"RoleBadge",196,15,24,Cream);badge.gameObject.SetActive(false);
            Text(root,"RoleLabel",228,12,322,30,20,"대원");
            Place(info.transform,196,44,354,104);info.fontSize=23;
            Place(healthBack,196,154,354,15);Place(health,198,156,350,11);
        }
        info.horizontalOverflow=HorizontalWrapMode.Wrap;info.verticalOverflow=VerticalWrapMode.Truncate;info.resizeTextForBestFit=false;
        // Two lines span the whole card below its portrait. This keeps 19–25 character counters readable.
        Text(root,"Tactic",20,194,542,54,18);
    }

    public static string Validate()
    {
        int checkedBadges=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{P.TrimEnd('/')}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach(var badge in prefab.GetComponentsInChildren<BattleRoleBadge>(true))
            {
                Require(badge.GetComponent<CanvasRenderer>(),"Role glyph is missing CanvasRenderer: "+path+" / "+AnimationUtility.CalculateTransformPath(badge.transform,prefab.transform));
                checkedBadges++;
            }
        }
        Require(checkedBadges>0,"No role glyphs were found in settlement prefabs.");
        var panel=AssetDatabase.LoadAssetAtPath<GameObject>(P+"ExpeditionBattlePanel.prefab").GetComponent<ExpeditionBattlePanel>();
        Require(panel.ActorRoleBadge&&panel.ActorRoleLabel&&panel.ActorTactic&&panel.TargetRoleBadge&&panel.TargetRoleLabel&&panel.TargetTactic,"Missing detail role references.");
        foreach(var path in new[]{"BattleTurnCard","BattleOrderCard"})
        {
            var card=AssetDatabase.LoadAssetAtPath<GameObject>(P+path+".prefab").GetComponent<BattleTurnCard>();
            Require(card.RoleBadge&&!card.RoleBadge.raycastTarget,"Missing/nondecorative turn role glyph: "+path);
            var a=card.Portrait.rectTransform;var b=card.RoleBadge.rectTransform;
            if(path=="BattleTurnCard")
            {
                float portraitLeft=card.GetComponent<LayoutElement>().minWidth*.5f-a.rect.width*.5f;
                Require(b.anchoredPosition.x+b.rect.width<=portraitLeft,"Turn role glyph overlaps portrait: "+path);
            }
            else
            {
                float portraitTop=((RectTransform)card.transform).rect.height*(1-a.anchorMax.y)-a.offsetMax.y;
                Require(-b.anchoredPosition.y+b.rect.height<=portraitTop,"Order glyph overlaps portrait vertically: "+path);
            }
        }
        var turnSize=panel.TurnPrefab.GetComponent<LayoutElement>();var turnLayout=panel.TurnContent.GetComponent<HorizontalLayoutGroup>();
        Require(turnSize&&turnLayout.childControlWidth,"Turn cards do not use their layout widths.");
        Require(turnSize.minWidth*9+turnLayout.spacing*8+turnLayout.padding.horizontal<=((RectTransform)panel.TurnContent.parent).rect.width,"Nine turn cards cannot fit inside their viewport.");
        var hud=AssetDatabase.LoadAssetAtPath<GameObject>(P+"BattlePawnHud.prefab").GetComponent<BattlePawnHud>();
        Require(hud.RoleBadge&&hud.Name.transform.parent==hud.RoleBadge.transform.parent,"Head role/name group is not wired.");
        var creatureRoster=AssetDatabase.LoadAssetAtPath<BattleCreatureRoster>("Assets/Data/BattleCreatures.asset");
        var party=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        var failures=new List<string>();
        foreach(var creature in creatureRoster.Creatures)Fits(panel.TargetTactic,ExpeditionBattlePanel.CreatureTactic(creature),creature.Name,failures);
        foreach(var candidate in party.Candidates)Fits(panel.ActorTactic,candidate.CombatTraitSummary,candidate.DisplayName,failures);
        Require(failures.Count==0,string.Join("\n",failures));
        return "PASS · CanvasRenderer present on all "+checkedBadges+" role badge instances (including inactive/nested prefabs); six editable role glyphs; overhead/turn/item-order badges; role and tactical text in both existing detail cards; all 12 creature/6 human summaries fit. Native in-game capture remains required.";
    }
    static void Fits(Text prototype,string value,string name,List<string> failures)
    {
        var settings=prototype.GetGenerationSettings(prototype.rectTransform.rect.size);
        using(var generator=new TextGenerator())
        {
            float height=generator.GetPreferredHeight(value??"",settings)/prototype.pixelsPerUnit;
            if(height>prototype.rectTransform.rect.height+.5f)failures.Add(name+" tactical summary exceeds card: "+height+" > "+prototype.rectTransform.rect.height);
        }
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
}
