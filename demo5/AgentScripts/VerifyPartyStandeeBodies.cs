using System;
using System.Linq;
using System.Reflection;
using Demo5.FrontEnd;
using Demo5.NightRun;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Non-playing verification: inactive disposable fixture, no live campaign or scene changes.
public static class VerifyPartyStandeeBodies
{
    static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static float Height(PartyRoster roster,SpriteRenderer body)=>roster.VisiblePixelsFor(body.sprite).height*Mathf.Abs(body.transform.localScale.y)/body.sprite.pixelsPerUnit;
    static float NormalizedHeight(PartyRoster roster,SpriteRenderer body)=>Height(roster,body)/roster.BodyScaleFor(body.sprite);
    static float Foot(PartyRoster roster,SpriteRenderer body)=>body.transform.localPosition.y+(roster.VisiblePixelsFor(body.sprite).y-body.sprite.pivot.y)*body.transform.localScale.y/body.sprite.pixelsPerUnit;
    static float Center(PartyRoster roster,SpriteRenderer body)=>body.transform.localPosition.x+(roster.VisiblePixelsFor(body.sprite).center.x-body.sprite.pivot.x)*body.transform.localScale.x*(body.flipX?-1:1)/body.sprite.pixelsPerUnit;

    public static string Run()
    {
        var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
        var expected=new[]{"scout","medic","cook","mechanic","guard","researcher"};
        Check(roster&&roster.Candidates!=null,"Roster is missing.");
        var candidates=expected.Select(id=>roster.Candidates.Single(c=>c.Id==id)).ToArray();
        Check(candidates.All(c=>c.Body&&c.Portrait&&c.BodyVisiblePixels.width>0&&c.BodyVisiblePixels.height>0),"Every actor needs portrait, body and alpha bounds.");
        Check(candidates.Select(c=>c.Body).Distinct().Count()==6,"Actors still reuse another ID's full body.");
        var scout=candidates[0].Body;var medic=candidates[1].Body;
        Check(roster.BodyFor("unknown",scout,medic)==scout,"Unknown ID fallback changed.");
        Check(roster.BodyFor(null,scout,medic)==scout,"Missing ID fallback changed.");
        var legacy=ScriptableObject.CreateInstance<PartyRoster>();
        var root=new GameObject("Party body verification");root.SetActive(false);
        var savedIds=PartySelectionSession.Selected.ToArray();
        try
        {
            legacy.Candidates=new[]{new PartyCandidate{Id="medic"}};
            Check(legacy.BodyFor("medic",scout,medic)==medic,"Older assets must preserve medic fallback.");
            Check(legacy.BodyFor("medic",scout,null)==scout,"Missing medic fallback must remain visible.");
            Check(legacy.BodyScaleFor(null)==1&&legacy.BodyScaleFor(scout)==1,"Missing and unknown bodies must keep the default display scale.");
            var pawnAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/FieldPawn.prefab");
            Check(pawnAsset,"Field pawn prefab is missing.");
            var pawn=Object.Instantiate(pawnAsset,root.transform);
            var body=pawn.transform.Find("Body").GetComponent<SpriteRenderer>();
            float height=NormalizedHeight(roster,body),foot=Foot(roster,body),rightCenter=Center(roster,body);
            foreach(var candidate in candidates)
            {
                roster.ApplyBody(body,candidate.Id,scout,medic);
                Check(body.sprite==candidate.Body,"Wrong field body for "+candidate.Id);
                Check(Mathf.Abs(NormalizedHeight(roster,body)-height)<.0001f,"Actor height does not preserve its configured display scale: "+candidate.Id);
                Check(Mathf.Abs(Foot(roster,body)-foot)<.0001f,"Actor feet moved off base: "+candidate.Id);
                Check(Mathf.Abs(Center(roster,body)-rightCenter)<.0001f,"Changing a right-facing actor shifts its horizontal center.");
                var position=body.transform.localPosition;var scale=body.transform.localScale;
                roster.ApplyBody(body,candidate.Id,scout,medic);
                Check(body.transform.localPosition==position&&body.transform.localScale==scale,"Refresh causes body drift.");
            }
            body.flipX=true;float center=Center(roster,body);
            foreach(var candidate in candidates)
            {
                roster.ApplyBody(body,candidate.Id,scout,medic);
                Check(body.flipX&&Mathf.Abs(Center(roster,body)-center)<.0001f,"Changing a left-facing actor shifts its horizontal center.");
                Check(Mathf.Abs(Foot(roster,body)-foot)<.0001f,"Left-facing actor feet moved off base.");
                Check(Mathf.Abs(NormalizedHeight(roster,body)-height)<.0001f,"Left-facing actor lost its display scale.");
            }
            VerifyScaledSwaps(roster,pawnAsset,root.transform,expected);
            var owner=root.AddComponent<SettlementController>();owner.Roster=roster;owner.ScoutBody=scout;owner.MedicBody=medic;
            owner.StandeeObjects=new GameObject[candidates.Length];owner.StandeeBodies=new SpriteRenderer[candidates.Length];
            for(int i=0;i<candidates.Length;i++)
            {
                var standee=Object.Instantiate(pawnAsset,root.transform);owner.StandeeObjects[i]=standee;
                owner.StandeeBodies[i]=standee.transform.Find("Body").GetComponent<SpriteRenderer>();
            }
            var campaign=new CampaignState(candidates.Select(c=>new Adventurer(c.DisplayName,c.RoleTitle,c.Description,c.Health,c.Aim,c.BagCapacity)).ToArray(),true);
            campaign.Toggle(0);campaign.Toggle(1);campaign.ConfirmParty();campaign.Settle(0);
            for(int i=2;i<candidates.Length;i++)campaign.Chosen.Add(i);
            typeof(SettlementController).GetProperty("Campaign").SetValue(owner,campaign);
            PartySelectionSession.Selected.Clear();PartySelectionSession.Selected.AddRange(expected);
            typeof(SettlementController).GetMethod("RefreshStandees",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,null);
            for(int i=0;i<candidates.Length;i++)
            {
                Check(owner.StandeeBodies[i].sprite==candidates[i].Body,"Settlement refresh reused another actor: "+expected[i]);
                Check(Mathf.Abs(NormalizedHeight(roster,owner.StandeeBodies[i])-height)<.0001f,"Settlement refresh lost the display scale: "+expected[i]);
            }
            var battle=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Settlement/ExpeditionBattlePanel.prefab").GetComponent<ExpeditionBattlePanel>();
            foreach(var candidate in candidates)Check(battle.VisibleBounds.Any(b=>b.Sprite==candidate.Body&&b.Pixels==candidate.BodyVisiblePixels),"Battle alpha bounds missing for "+candidate.Id);
        }
        finally
        {
            PartySelectionSession.Selected.Clear();PartySelectionSession.Selected.AddRange(savedIds);
            Object.DestroyImmediate(root);Object.DestroyImmediate(legacy);
        }
        return "PASS: six distinct ID bodies, unknown/legacy scale fallback, normalized alpha height, mixed 0.75–3.25 display scales without accumulation, facing/center/feet preservation, repeated refresh without drift, settlement scale propagation, and matching battle bounds. No live campaign changed.";
    }

    static void VerifyScaledSwaps(PartyRoster source,GameObject pawnAsset,Transform parent,string[] ids)
    {
        var scaled=ScriptableObject.CreateInstance<PartyRoster>();
        try
        {
            // Separate candidates exercise unequal sizes without changing the catalog or original art.
            var scales=new[]{1f,1f,.75f,1.3f,2.1f,3.25f};
            scaled.Candidates=ids.Select((id,i)=>
            {
                var original=source.Candidates.Single(c=>c.Id==id);
                return new PartyCandidate{Id=id,Body=original.Body,BodyVisiblePixels=original.BodyVisiblePixels,BodyScale=scales[i]};
            }).ToArray();
            var scout=scaled.Candidates[0].Body;var medic=scaled.Candidates[1].Body;
            var body=Object.Instantiate(pawnAsset,parent).transform.Find("Body").GetComponent<SpriteRenderer>();
            float height=NormalizedHeight(scaled,body);
            for(int pass=0;pass<4;pass++)
            {
                body.flipX=(pass&1)!=0;
                var initialScale=body.transform.localScale;initialScale.x=Mathf.Abs(initialScale.x)*(pass<2?1:-1);body.transform.localScale=initialScale;
                float foot=Foot(scaled,body),center=Center(scaled,body);
                foreach(var id in (pass&1)==0?ids:ids.Reverse())
                {
                    scaled.ApplyBody(body,id,scout,medic);
                    Check(Mathf.Abs(NormalizedHeight(scaled,body)-height)<.0001f,"Mixed display scales accumulated or were capped: "+id);
                    Check(Mathf.Abs(Foot(scaled,body)-foot)<.0001f&&Mathf.Abs(Center(scaled,body)-center)<.0001f,"Mixed display scales shifted the feet or center: "+id);
                    Check(body.flipX==((pass&1)!=0)&&Mathf.Sign(body.transform.localScale.x)==(pass<2?1:-1),"Mixed display scales changed facing.");
                    var position=body.transform.localPosition;var size=body.transform.localScale;
                    scaled.ApplyBody(body,id,scout,medic);
                    Check(body.transform.localPosition==position&&body.transform.localScale==size,"Repeated scaled-body refresh drifts.");
                }
                scaled.ApplyBody(body,"unknown",scout,medic);
                Check(body.sprite==scout&&Mathf.Abs(Height(scaled,body)-height)<.0001f,"Unknown-ID fallback retained the previous actor's display scale.");
            }
            var candidate=scaled.Candidates[2];
            foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            {
                candidate.BodyScale=invalid;
                Check(scaled.BodyScaleFor(candidate.Body)==1,"An invalid display scale must fall back to one.");
            }
            candidate.BodyScale=8.5f;
            Check(scaled.BodyScaleFor(candidate.Body)==8.5f,"Positive display scales must not have an upper cap.");
        }
        finally{Object.DestroyImmediate(scaled);}
    }
}
