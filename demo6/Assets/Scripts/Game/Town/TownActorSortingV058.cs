using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Demo6.Game
{
    /// <summary>Town-only atomic actor sorting. Existing animation part ordering is left intact.
    /// Contact shadows escape their actor group through sortAtRoot and remain on the ground.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class TownActorSortingV058 : MonoBehaviour
    {
        readonly List<SortingGroup> actors=new List<SortingGroup>();
        readonly List<SortingGroup> owned=new List<SortingGroup>();
        readonly HashSet<SpriteRenderer> shadows=new HashSet<SpriteRenderer>();
        readonly Dictionary<SortingGroup,GroupState> borrowed=new Dictionary<SortingGroup,GroupState>();
        struct GroupState { public int order; public bool sortAtRoot; }
        int setupFrames;
        void LateUpdate()
        {
            if(setupFrames++<8)
            {
                if(PlayerController.Instance)Register(PlayerController.Instance.transform);
                foreach(var npc in TownNpc.Residents)if(npc)Register(npc.transform);
                foreach(var idle in GetComponentsInChildren<TownNpcIdleV057>())
                    if(!idle.GetComponentInParent<TownNpc>())Register(idle.transform);
                foreach(var actor in actors)
                foreach(var sr in actor.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if(!sr.name.ToLowerInvariant().Contains("shadow")||!shadows.Add(sr))continue;
                    var ground=sr.GetComponent<SortingGroup>();if(!ground){ground=sr.gameObject.AddComponent<SortingGroup>();owned.Add(ground);}else Remember(ground);
                    ground.sortAtRoot=true;ground.sortingOrder=-940;
                }
            }
            foreach(var actor in actors)if(actor)actor.sortingOrder=WorldProps.SortY(actor.transform.position.y,50);
        }
        void Register(Transform target)
        {
            var group=target.GetComponent<SortingGroup>();
            if(!group){group=target.gameObject.AddComponent<SortingGroup>();owned.Add(group);}else Remember(group);
            if(!actors.Contains(group))actors.Add(group);
        }
        void Remember(SortingGroup group)
        {
            if(owned.Contains(group)||borrowed.ContainsKey(group))return;
            borrowed.Add(group,new GroupState{order=group.sortingOrder,sortAtRoot=group.sortAtRoot});
        }
        void OnDestroy()
        {
            // Component lifetime is the town's lifetime. No project-wide sorting setting is changed.
            foreach(var entry in borrowed)if(entry.Key)
            {
                entry.Key.sortingOrder=entry.Value.order;
                entry.Key.sortAtRoot=entry.Value.sortAtRoot;
            }
            foreach(var group in owned)if(group)Destroy(group);
        }
    }
}

