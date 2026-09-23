using System;
using UnityEngine;
namespace Demo5.FrontEnd
{
    [Serializable] public sealed class PartyCandidate
    {
        public string Id,DisplayName;
        public string RoleTitle;
        [TextArea] public string FirstLine,WorkLine,RestLine;
        [TextArea] public string Description;
        public string TraitTitle;
        [TextArea(2,5)] public string TraitDescription;
        [TextArea(2,5)] public string Characteristics;
        [Min(1)] public int BagCapacity=3,Health=3;
        public int Aim;
        [Range(1,100)] public int CookingTimePercent=100, CraftTimePercent=100;
        public Sprite Portrait;
    }
    [CreateAssetMenu(menuName="Demo5/Party roster")]
    public sealed class PartyRoster : ScriptableObject { public PartyCandidate[] Candidates; }
}
