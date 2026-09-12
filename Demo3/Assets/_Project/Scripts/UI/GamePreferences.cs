using UnityEngine;

namespace Live49.UI
{
    public static class GamePreferences
    {
        public static float Volume { get => Mathf.Clamp01(PlayerPrefs.GetFloat("Live49.Volume", .8f)); set { PlayerPrefs.SetFloat("Live49.Volume", Mathf.Clamp01(value)); AudioListener.volume = Volume; } }
        public static float TextSpeed { get => Mathf.Clamp(PlayerPrefs.GetFloat("Live49.TextSpeed", 1f), .5f, 2f); set => PlayerPrefs.SetFloat("Live49.TextSpeed", Mathf.Clamp(value, .5f, 2f)); }
        public static bool InstantText { get => PlayerPrefs.GetInt("Live49.InstantText", 0) == 1; set => PlayerPrefs.SetInt("Live49.InstantText", value ? 1 : 0); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply() => AudioListener.volume = Volume;
        public static void Save() => PlayerPrefs.Save();
    }
}
