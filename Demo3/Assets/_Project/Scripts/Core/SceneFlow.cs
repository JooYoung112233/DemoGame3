using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live49.Core
{
    public static class SceneNames
    {
        public const string Title = "00_Title";
        public const string Game = "01_Game";
    }

    // Title → Game handoff: Game loads additively under the title so the book shot can dissolve into the camper photo.
    public static class SceneFlow
    {
        public static bool OpeningHandoffPending { get; private set; }
        public static bool TitleReadyForHandoff { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            OpeningHandoffPending = false;
            TitleReadyForHandoff = false;
        }

        public static AsyncOperation LoadGameUnderTitle()
        {
            OpeningHandoffPending = true;
            TitleReadyForHandoff = false;
            return SceneManager.LoadSceneAsync(SceneNames.Game, LoadSceneMode.Additive);
        }

        public static void MarkTitleReady() => TitleReadyForHandoff = true;

        public static void FinishTitleHandoff()
        {
            OpeningHandoffPending = false;
            TitleReadyForHandoff = false;

            var game = SceneManager.GetSceneByName(SceneNames.Game);
            if (game.IsValid())
                SceneManager.SetActiveScene(game);

            var title = SceneManager.GetSceneByName(SceneNames.Title);
            if (title.isLoaded)
                SceneManager.UnloadSceneAsync(title);
        }
    }
}
