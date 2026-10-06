using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Demo8;

public static class VerifyCampaignInput
{
    static async Task Frames()
    {
        int frame = Time.frameCount;
        for (int i = 0; i < 100 && (i < 5 || Time.frameCount < frame + 4); i++) { EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint(); EditorApplication.QueuePlayerLoopUpdate(); await Task.Delay(80); }
    }
    static async Task Click(float x, float y)
    {
        float s = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
        Vector2 pos = new Vector2((Screen.width - 1440 * s) / 2 + x * s, (Screen.height - 900 * s) / 2 + y * s);
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(pos.x, Screen.height - pos.y), buttons = 1 });
        await Frames();
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = new Vector2(pos.x, Screen.height - pos.y), buttons = 0 });
        await Frames();
    }
    static void Check(bool ok, string description, List<string> results)
    { if (!ok) throw new Exception("INPUT FAIL: " + description); results.Add(description); }
    public static async Task<string> Run()
    {
        var view = UnityEngine.Object.FindAnyObjectByType<CampaignView>();
        if (view == null) throw new Exception("Start campaign Play mode first");
        var results = new List<string>();
        var gameView = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        gameView.Show(); gameView.Focus();
        var oldKeyboard = Keyboard.current; var oldMouse = Mouse.current;
        var keyboard = InputSystem.AddDevice<Keyboard>("CampaignTestKeys"); var mouse = InputSystem.AddDevice<Mouse>("CampaignTestMouse");
        var oldBehavior = InputSystem.settings.backgroundBehavior;
        var oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        try
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.EnableDevice(keyboard); InputSystem.EnableDevice(mouse); keyboard.MakeCurrent(); mouse.MakeCurrent();
            view.NewCampaign(); view.FocusArmy();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = Vector2.zero }); await Frames();
            await Click(1250, 450);
            Check(view.state.provinces[0].farm == 1, "Farm button builds facility", results);
            await Click(1250, 711);
            Check(view.state.ArmyOf(0).infantry == 200, "Recruit button recruits infantry", results);
            // Click the rendered map label rather than calling Select.
            var screen = view.mapCamera.WorldToScreenPoint(CampaignView.Position(1) + new Vector3(0, .1f, -.72f));
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            float dx = (screen.x - (Screen.width - 1440 * scale) / 2) / scale;
            float dy = (Screen.height - screen.y - (Screen.height - 900 * scale) / 2) / scale;
            await Click(dx, dy + 20);
            Check(view.selected == 1, "Map label selects target province", results);
            await Click(390, 670);
            Check(view.confirmBattle, "Attack button opens battle confirmation", results);
            int turn = view.state.turn;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); await Task.Delay(130);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Task.Delay(130);
            Check(view.state.turn == turn, "Space cannot end turn beneath battle modal", results);
            await Click(820, 590);
            Check(!view.confirmBattle && view.state.provinces[1].owner == -1 && view.state.ArmyOf(0).actions == 2, "Cancel battle preserves state and action", results);
            await Click(390, 670); await Click(570, 590);
            Check(view.state.provinces[1].owner == 0 && !view.confirmBattle, "Confirm battle conquers selected territory", results);
            Check(view.Marching, "Soldier formation starts physical march after order", results);
            await Task.Delay(300);
            for (int i = 0; i < 80 && view.Marching; i++) await Task.Delay(50);
            Check(!view.Marching, "Formation arrives at conquered region", results);
            await Click(1190, 600);
            Check(view.state.provinces[1].order == 60, "Relief button stabilizes occupation", results);
            await Click(970, 670);
            Check(view.state.turn == turn + 1, "End-turn button advances campaign", results);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); await Task.Delay(130);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Task.Delay(130);
            Check(view.state.turn == turn + 2, "Space shortcut advances exactly one turn", results);
            await Click(920, 42);
            Check(view.domestic, "Domestic affairs opens from world map", results);
            await Click(540, 285);
            Check(view.state.factions[0].tax == 2, "Domestic tax policy button applies high tax", results);
            await Click(430, 406);
            Check(view.state.provinces[1].focus == 1, "Domestic city focus button changes production", results);
            await Click(702, 285);
            Check(view.state.factions[0].agriculture == 1, "Research button completes agricultural research", results);
            await Click(1350, 42); Check(view.confirmRestart, "New campaign asks before discarding progress", results);
            await Click(580, 520);
            Check(view.state.turn == 1 && view.state.Territory(0) == 2 && !view.confirmRestart, "New campaign confirmation resets campaign", results);
            var cameraPosition = view.mapCamera.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D)); await Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames();
            Check(view.mapCamera.transform.position.x > cameraPosition.x, "WASD pans the world map", results);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home)); await Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames();
            Check(view.mapCamera.orthographicSize <= 6.1f, "Home approaches the selected soldier formation", results);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); await Frames();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Frames();
            Check(view.domestic, "Tab switches to domestic affairs", results);
            Directory.CreateDirectory("Verification"); File.WriteAllLines("Verification/input.txt", results);
            return "PASS " + results.Count + " real game-view input checks";
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); oldKeyboard?.MakeCurrent(); oldMouse?.MakeCurrent();
            InputSystem.settings.backgroundBehavior = oldBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
            view.NewCampaign();
            view.notice = "청하에 군대가 있습니다. 풍림을 선택하여 첫 출정을 준비하세요.";
        }
    }
}
