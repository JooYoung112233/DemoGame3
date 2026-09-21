using System;
using System.Linq;
using Live49.Core;
using UnityEngine;

namespace Live49.Chapter00
{
    [Serializable] public sealed class StoryLine { public string speaker,text; }
    [Serializable] public sealed class StoryEffect { public string key,value; }
    [Serializable] public sealed class StoryChoice { public string label,target;public string[] requires,unless;public StoryEffect[] effects; }
    [Serializable] public sealed class StoryNode
    {
        public string id,title,scene,next;public StoryLine[] lines;public StoryEffect[] effects;public StoryChoice[] choices;
    }
    [Serializable] public sealed class StoryScene { public string id,path; }
    [Serializable] public sealed class ContinuationGraph
    {
        public StoryNode[] nodes;public StoryScene[] scenes;
        public static ContinuationGraph Load()=>JsonUtility.FromJson<ContinuationGraph>(Resources.Load<TextAsset>("Live49/continuation").text);
        public StoryNode Node(string id)=>nodes.First(n=>n.id==id);
        public StoryChoice[] Choices(StoryNode node,JourneyState state)=>node.choices.Where(c=>c.requires.All(state.Has)&&c.unless.All(f=>!state.Has(f))).ToArray();
        public string Resolve(string id,JourneyState state)=>id=="prepare"&&state.Has("water_packed")&&state.Has("blanket_packed")?"prepared":id=="inspect"&&state.Has("switch_checked")&&state.Has("panel_checked")?"small_light":id;
        public static void Effects(StoryEffect[] effects,JourneyState state)
        {
            foreach(var e in effects)
            {
                state.Set(e.key,e.value);
                if(e.key=="journal_first_line")state.journal=e.value;
                if(e.key=="book_packed"&&state.Count("sketchbook")==0)state.Add("sketchbook","스케치북",1,2,"모서리가 닳은 스케치북. 아직 그릴 수 있는 빈 페이지가 남아 있어요.");
            }
        }
    }
}
