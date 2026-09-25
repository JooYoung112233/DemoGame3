using System;
using Demo5.NightRun;
using UnityEngine;
namespace Demo5.FrontEnd
{
    [Serializable] public sealed class PartyCandidate
    {
        public string Id,DisplayName;
        public bool AvailableAtStart;
        public CharacterUnlockRule UnlockRule;
        public string UnlockHint;
        public int UnlockOrder;
        public string RoleTitle;
        public string CombatRole;
        [TextArea(1,2)] public string CombatTraitSummary;
        [TextArea] public string FirstLine,WorkLine,RestLine;
        [TextArea] public string Description;
        public string TraitTitle;
        [TextArea(2,5)] public string TraitDescription;
        [TextArea(2,5)] public string Characteristics;
        [Min(1)] public int BagCapacity=3,Health=3;
        public int Aim;
        [Range(1,100)] public int CookingTimePercent=100, CraftTimePercent=100;
        [Range(1,100)] public int ResearchTimePercent=100;
        public HumanTraits Traits = new HumanTraits();
        public Sprite Portrait;
        public Sprite Body;
        [Min(.1f)] public float BodyScale=1;
        // Editor-authored alpha bounds, relative to Body.rect; no readable texture is needed in a Player.
        public Rect BodyVisiblePixels;

        public Adventurer CreateAdventurer()
        {
            var member = new Adventurer(DisplayName, string.IsNullOrEmpty(RoleTitle) ? DisplayName : RoleTitle, Description, Health, Aim, BagCapacity);
            ApplyTraits(member);
            return member;
        }
        // Save loading preserves the saved numeric state and reattaches current ID capabilities.
        public void ApplyTraits(Adventurer member) { if (member != null) member.ApplyTraits(Traits); }
        public int WorkTimePercent(bool cooking, bool research = false)
        {
            int value = cooking ? CookingTimePercent : research ? ResearchTimePercent : CraftTimePercent;
            return value <= 0 ? 100 : Math.Max(1, Math.Min(100, value));
        }
    }
    [CreateAssetMenu(menuName="Demo5/Party roster")]
    public sealed class PartyRoster : ScriptableObject
    {
        public PartyCandidate[] Candidates;

        public Sprite BodyFor(string id,Sprite scoutFallback,Sprite medicFallback)
        {
            var candidate=Array.Find(Candidates??Array.Empty<PartyCandidate>(),c=>c!=null&&c.Id==id);
            if(candidate!=null&&candidate.Body)return candidate.Body;
            return id=="medic"&&medicFallback?medicFallback:scoutFallback;
        }

        public Rect VisiblePixelsFor(Sprite sprite)
        {
            if(!sprite)return default;
            var candidate=Array.Find(Candidates??Array.Empty<PartyCandidate>(),c=>c!=null&&c.Body==sprite&&c.BodyVisiblePixels.width>0&&c.BodyVisiblePixels.height>0);
            return candidate!=null?candidate.BodyVisiblePixels:new Rect(0,0,sprite.rect.width,sprite.rect.height);
        }

        public float BodyScaleFor(Sprite sprite)
        {
            if(!sprite)return 1;
            var candidate=Array.Find(Candidates??Array.Empty<PartyCandidate>(),c=>c!=null&&c.Body==sprite);
            float scale=candidate?.BodyScale??1;
            return scale>0&&!float.IsInfinity(scale)?scale:1;
        }

        public void ApplyBody(SpriteRenderer target,string id,Sprite scoutFallback,Sprite medicFallback)
        {
            if(!target)return;
            var next=BodyFor(id,scoutFallback,medicFallback);
            if(!next||target.sprite==next)return;
            var previous=target.sprite;
            if(previous)
            {
                // A wide source canvas must not shrink the next actor or move its feet off the shared base.
                var oldBounds=VisiblePixelsFor(previous);var nextBounds=VisiblePixelsFor(next);
                var position=target.transform.localPosition;var oldScale=target.transform.localScale;
                // Normalize the previous actor first so swapping scaled bodies never compounds their size.
                float height=oldBounds.height*Mathf.Abs(oldScale.y)/previous.pixelsPerUnit/BodyScaleFor(previous)*BodyScaleFor(next);
                float facing=target.flipX?-1:1;
                float center=position.x+(oldBounds.center.x-previous.pivot.x)*oldScale.x*facing/previous.pixelsPerUnit;
                float oldBottom=target.flipY?previous.pivot.y-oldBounds.yMax:oldBounds.y-previous.pivot.y;
                float foot=position.y+oldBottom*oldScale.y/previous.pixelsPerUnit;
                float scale=height*next.pixelsPerUnit/nextBounds.height;
                float xScale=scale*(oldScale.x<0?-1:1);
                float nextBottom=target.flipY?next.pivot.y-nextBounds.yMax:nextBounds.y-next.pivot.y;
                target.transform.localScale=new Vector3(xScale,scale,oldScale.z);
                target.transform.localPosition=new Vector3(center-(nextBounds.center.x-next.pivot.x)*xScale*facing/next.pixelsPerUnit,
                    foot-nextBottom*scale/next.pixelsPerUnit,position.z);
            }
            target.sprite=next;
        }
    }
}
