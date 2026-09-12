using System;
using System.Linq;
using Live49.Title;
using Live49.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class VerifySettingsUI
{
    public static string Run()
    {
        var menu = UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(x => x.Id == "settings");
        if (!menu.Interactable) throw new Exception("Settings menu disabled");
        menu.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        var panel = UnityEngine.Object.FindAnyObjectByType<SettingsPanel>();
        if (panel == null || !panel.IsOpen) throw new Exception("Menu failed to open settings");
        float volume = GamePreferences.Volume;
        float speed = GamePreferences.TextSpeed;
        bool instant = GamePreferences.InstantText;
        try
        {
            var buttons = panel.GetComponentsInChildren<Button>();
            buttons.First(b => b.name == "Button_−").onClick.Invoke();
            if (Mathf.Abs(GamePreferences.Volume - Mathf.Clamp01(volume - .05f)) > .001f) throw new Exception("Volume button failed");
            if (Mathf.Abs(AudioListener.volume - GamePreferences.Volume) > .001f) throw new Exception("Volume not applied");
            panel.Adjust(1, speed < 2 ? 1 : -1);
            if (Mathf.Abs(GamePreferences.TextSpeed - speed) < .01f) throw new Exception("Text speed failed");
            buttons.Single(b => b.name == "Button_변경").onClick.Invoke();
            if (GamePreferences.InstantText == instant) throw new Exception("Instant toggle failed");
            float expected = GamePreferences.Volume;
            buttons.Single(b => b.name == "Button_돌아가기").onClick.Invoke();
            if (panel.IsOpen) throw new Exception("Close button failed");
            if (Mathf.Abs(PlayerPrefs.GetFloat("Live49.Volume") - expected) > .001f) throw new Exception("Preference not stored");
        }
        finally
        {
            GamePreferences.Volume = volume;
            GamePreferences.TextSpeed = speed;
            GamePreferences.InstantText = instant;
            GamePreferences.Save();
            panel.Open();
        }
        return "PASS: title pointer click, volume +/- and audio application, text speed, instant toggle, close, preferences; original settings restored; panel open for review.";
    }
}
