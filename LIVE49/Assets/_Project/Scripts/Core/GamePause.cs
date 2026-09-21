using UnityEngine;

namespace Live49.Core
{
    // Shared story-input gate. The UI owner also preserves/restores the previous time scale.
    public static class GamePause
    {
        public static bool IsPaused { get; internal set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => IsPaused = false;
    }
}
