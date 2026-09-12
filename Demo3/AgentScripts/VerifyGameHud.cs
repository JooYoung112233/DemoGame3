using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Live49.Core;
using Live49.Title;
using Live49.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class VerifyGameHud
{
    static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "../Screenshots/HudReview"));
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static async Task Wait(Func<bool> ready, int milliseconds = 15000)
    {
        var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (!ready()) { if (DateTime.UtcNow > end) throw new Exception("HUD wait timed out."); await Task.Delay(16); }
    }
    static async Task Shot(string file)
    {
        Directory.CreateDirectory(Folder); ScreenCapture.CaptureScreenshot(Path.Combine(Folder, file)); await Task.Delay(350);
    }
    static Button Button(string name) => UnityEngine.Object.FindObjectsByType<Button>().Single(b => b.name == name);
    static async Task Escape(Keyboard keyboard)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
        await Task.Delay(250);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        await Task.Delay(220);
    }
    public static async Task<string> Exploration()
    {
        var hud = GameHud.Instance;
        Check(hud != null && hud.IsExploring, "Gameplay exploration HUD missing.");
        var kb = InputSystem.AddDevice<Keyboard>("HudAuditKeyboard");
        try
        {
            await Shot("01-camper-hud.png");
            int dispatched = 0;
            hud.BindAction(GameHud.ActionId.Map, () => dispatched++);
            Button("Action_Map").onClick.Invoke();
            Check(dispatched == 1, "Map route not dispatched.");
            hud.BindAction(GameHud.ActionId.Map, null);
            Time.timeScale = .75f;
            await Escape(kb);
            Check(hud.IsPaused && Time.timeScale == 0 && AudioListener.pause, "Escape did not pause game/audio.");
            FreshInput.SimulateAdvance();
            Check(!FreshInput.TryAdvance(), "Menu input leaked into dialogue.");
            await Shot("02-pause-menu.png");
            Button("Button_설정").onClick.Invoke();
            await Task.Delay(200);
            Check(hud.Settings.IsOpen && hud.IsPaused, "Settings not open over paused game.");
            float oldVolume = GamePreferences.Volume;
            hud.Settings.Adjust(0, -1);
            Check(GamePreferences.Volume < oldVolume || oldVolume == 0, "Settings volume not applied.");
            GamePreferences.Volume = oldVolume;
            await Shot("03-settings.png");
            await Escape(kb);
            Check(!hud.Settings.IsOpen && hud.IsPaused && Time.timeScale == 0, "Escape closed two menu levels.");
            Button("Button_게임 종료").onClick.Invoke();
            await Task.Delay(150);
            Check(UnityEngine.Object.FindObjectsByType<TMP_Text>().Any(t => t.text.Contains("진행은 저장되지")), "Exit lacks unsaved-progress confirmation.");
            await Shot("04-exit-confirmation.png");
            await Escape(kb);
            Check(hud.IsPaused && Button("Button_재개") != null, "Exit cancel did not return to menu.");
            // The native UI submit path used by Enter resumes the selected button.
            ExecuteEvents.Execute(Button("Button_재개").gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            await Wait(() => !hud.IsPaused);
            Check(Mathf.Approximately(Time.timeScale, .75f) && !AudioListener.pause, "Resume did not restore prior clock/audio.");
            Time.timeScale = 1;
            await Escape(kb);
            Button("Button_타이틀로").onClick.Invoke();
            Button("Button_타이틀로").onClick.Invoke();
            await Wait(() => UnityEngine.Object.FindAnyObjectByType<TitleController>() != null && GameHud.Instance == null);
            Check(Time.timeScale == 1 && !GamePause.IsPaused && !AudioListener.pause, "Title return leaked pause state.");
            File.WriteAllText(Path.Combine(Folder, "preview-report.txt"), "PASS: four HUD actions; Map handler routing; actual InputSystem Escape; time/audio pause; story-input gate; Settings adjustment and one-level Esc; exit warning and cancellation; UI submit Resume restores previous 0.75 scale; title return removes HUD and restores clock. Four 1920x1080 screenshots.\n");
            return "HUD preview, nested Esc, settings, resume, exit cancel, title cleanup passed.";
        }
        finally { if (kb.added) InputSystem.RemoveDevice(kb); }
    }
    public static async Task<string> Story()
    {
        var start = UnityEngine.Object.FindObjectsByType<TitleMenuItem>().Single(i => i.Id == "start");
        start.Owner.Confirm(start);
        await Wait(() => GameHud.Instance != null && !SceneFlow.OpeningHandoffPending);
        var hud = GameHud.Instance;
        Check(!hud.IsExploring, "Exploration HUD intrudes on story.");
        var call = UnityEngine.Object.FindObjectsByType<Typewriter>().Single(t => t.name == "CallText");
        await Wait(() => call.IsTyping);
        hud.OpenPause();
        var tmp = call.GetComponent<TMP_Text>();
        int visible = tmp.maxVisibleCharacters;
        float time = Time.time;
        FreshInput.SimulateAdvance();
        await Task.Delay(650);
        Check(visible == tmp.maxVisibleCharacters && Mathf.Approximately(time, Time.time), "Story typing progressed while paused.");
        hud.Resume(); await Wait(() => !hud.IsPaused);
        await Wait(() => !call.IsTyping);
        Check(UnityEngine.Object.FindObjectsByType<CanvasGroup>().Single(g => g.name == "C0_BookIntro").alpha == 1, "Paused input advanced to next shot.");
        await Shot("05-story-resumed.png");
        hud.OpenPause(); await Task.Delay(200);
        hud.RequestExit(false); hud.ConfirmExit();
        await Wait(() => GameHud.Instance == null && UnityEngine.Object.FindAnyObjectByType<TitleController>() != null);
        File.WriteAllText(Path.Combine(Folder, "story-report.txt"), "PASS: Title-to-game integration; exploration HUD hidden during dialogue; pause freezes actual typewriter/time; queued story input discarded; resume completes same line without skipping the book; return to title destroys persistent HUD.\n");
        return "Real opening pause/resume and title lifecycle passed.";
    }

    public static async Task<string> CaptureFinal()
    {
        var hud = GameHud.Instance;
        Check(hud != null && hud.IsExploring, "Run in an active gameplay exploration context.");
        await Shot("01-camper-hud.png");
        hud.OpenPause();
        await Wait(() => UnityEngine.Object.FindObjectsByType<CanvasGroup>().Single(g => g.name == "PauseOverlay").alpha >= .999f);
        await Task.Delay(200);
        await Shot("02-pause-menu.png");
        hud.RequestExit(true); await Task.Delay(200);
        await Shot("04-exit-confirmation.png");
        hud.HandleEscape(); hud.Resume();
        await Wait(() => !hud.IsPaused);
        Check(Time.timeScale == 1 && !AudioListener.pause, "Final capture left gameplay paused.");
        return "Final HUD and stronger pause-card contrast captured; review scene is running and unpaused.";
    }

    public static async Task<string> DayEnd()
    {
        var hud = GameHud.Instance;
        Check(hud != null && hud.IsExploring, "Run in an active gameplay exploration context.");
        if (hud.IsPaused) { hud.Resume(); await Wait(() => !hud.IsPaused); }
        int finished = 0;
        string blocked = "소이와 이야기를 나눈 뒤에\n하루를 마칠 수 있어요.";
        hud.ConfigureDayEnd("소이와 이야기 나누기", () => blocked, () => finished++);
        // CLI tests run with the Game view unfocused. Use a temporary settings clone, never change the project asset.
        var originalInputSettings = InputSystem.settings;
        // InputManager destroys its temporary default settings when replaced; preserve a clone for restoration.
        var restoreInputSettings = originalInputSettings.hideFlags == HideFlags.HideAndDontSave
            ? UnityEngine.Object.Instantiate(originalInputSettings) : originalInputSettings;
        var testInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
        InputSystem.settings = testInputSettings;
        testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var kb = InputSystem.AddDevice<Keyboard>("DayEndAuditKeyboard");
        try
        {
            await Shot("06-day-end-hud.png");
            Button("EndDayButton").onClick.Invoke();
            await Task.Delay(300);
            Check(hud.IsPaused && !Button("Button_하루 마치기").interactable, "Blocked day can be confirmed.");
            hud.ConfirmDayEnd(); Check(finished == 0, "Blocked callback ran.");
            await Shot("07-day-end-blocked.png");
            await Escape(kb);
            Check(!hud.IsPaused && Time.timeScale == 1, "Day-end Escape did not return directly to camper. keyboardEnabled=" + kb.enabled + "; current=" + (Keyboard.current == kb) + "; focused=" + Application.isFocused + "; settingsOpen=" + hud.Settings.IsOpen + "; frame=" + Time.frameCount);
            blocked = null;
            hud.ConfigureDayEnd("오늘 할 일을 모두 마쳤어요.", () => blocked, () => finished++);
            hud.RequestDayEnd(); await Task.Delay(300);
            Check(Button("Button_하루 마치기").interactable, "Ready day is disabled.");
            Check(EventSystem.current.currentSelectedGameObject.name == "Button_더 둘러보기", "Repeated submit would auto-confirm the day.");
            await Shot("08-day-end-confirmation.png");
            blocked = "아직 마치지 않은 일이 있어요.";
            hud.ConfirmDayEnd(); Check(finished == 0, "Prerequisites were not rechecked.");
            hud.HandleEscape(); await Wait(() => !hud.IsPaused);
            blocked = null; hud.RequestDayEnd(); await Task.Delay(230);
            hud.ConfirmDayEnd(); hud.ConfirmDayEnd();
            await Wait(() => finished == 1);
            Check(!hud.IsPaused && Time.timeScale == 1 && !AudioListener.pause, "Day transition callback ran while paused.");
            hud.RequestDayEnd(); await Task.Delay(150);
            Check(!Button("Button_하루 마치기").interactable, "Same day can dispatch twice without a new binding.");
            hud.HandleEscape(); await Wait(() => !hud.IsPaused);
            Check(finished == 1, "Day completion dispatched twice.");
            hud.ConfigureDayEnd("소이와 이야기 나누기", () => "소이와 이야기를 나눈 뒤에\n하루를 마칠 수 있어요.", null);
            File.WriteAllText(Path.Combine(Folder, "day-end-report.txt"), "PASS: HUD entry; unmet prerequisite explanation and disabled confirmation; actual Escape cancels to camper; cancel selected by default; condition rechecked at confirmation; ready callback exactly once after clock/audio restored; no unbound repeat; screenshots of HUD, blocked and ready states. Test callbacks only; actual next-day story remains unimplemented.\n");
            return "Day-end UI, availability guard, cancellation and single completion dispatch passed. Preview restored.";
        }
        finally
        {
            if (kb.added) InputSystem.RemoveDevice(kb);
            InputSystem.settings = restoreInputSettings;
            if (testInputSettings != null) UnityEngine.Object.Destroy(testInputSettings);
        }
    }

    public static string RestoreDefaultTestFocus()
    {
        // Restore the two default focus fields after a failed test cleanup of Unity's temporary settings.
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
        return "Temporary input settings restored to the original default focus behavior.";
    }
}
