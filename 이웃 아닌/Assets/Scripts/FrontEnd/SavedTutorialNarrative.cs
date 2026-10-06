using System;

namespace Demo5.FrontEnd
{
    // Dialogue progress only. Advancing a line never spends time or grants a resource.
    [Serializable] public sealed class SavedTutorialNarrative
    {
        public const int BeatCount = 11;
        public const int AllSeen = (1 << BeatCount) - 1;
        public int SeenMask;
        public int PendingBeat = -1;
        public int LineIndex;
        // Optional v18 field; old campaigns keep their existing guided flow.
        public bool Skipped;

        public SavedTutorialNarrative Clone() => new SavedTutorialNarrative {
            SeenMask = SeenMask, PendingBeat = PendingBeat, LineIndex = LineIndex, Skipped = Skipped
        };
    }
}
