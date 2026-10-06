using System;

namespace Demo5.FrontEnd
{
    // A story identity is separate from the recruit roster. No display name or rewards are stored here.
    [Serializable]
    public sealed class SavedNpcStory
    {
        // 0: before observation, 1: observed, 2: name learned, 3: first conversation completed.
        public int Stage;
        // 0: unchosen, 1: respected distance, 2: explained the known entrance route.
        public int Choice;
        // Stage 0: page 0; stage 1: pages 0/1; stage 2: page 2; stage 3: page 3.
        public int Page;
        public int VisitCount, MetVisit;
        // PendingDialogue is only the stage-1/page-0 observation waiting to open.
        public bool Returned, Reunited, PendingDialogue;
    }
}
