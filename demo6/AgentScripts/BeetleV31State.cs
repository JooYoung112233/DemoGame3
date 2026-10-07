using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Demo6.Game;
using Newtonsoft.Json.Linq;

public static class BeetleV31State
{
    public static object Finish()
    {
        var root=CombatTestRoot.Instance;
        var data=new { state=ReadCurrent(), preset=root?root.CurrentPreset.ToString():null, floor=root?root.Floor:0, weapon=root?root.TestWeaponId:null, Time.captureDeltaTime, Time.fixedDeltaTime, Time.timeScale, Physics2D.simulationMode, Tuning.HitStopEnabled, Tuning.Invincible };
        File.WriteAllText(Evidence+"/final-state.json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));return data;
    }
    public static object RestoreHardware()
    {
        if(Keyboard.current==null||!Keyboard.current.native||Mouse.current==null||!Mouse.current.native)throw new Exception("Preserve unexpected input devices");
        if(!Keyboard.current.enabled)InputSystem.EnableDevice(Keyboard.current);
        if(!Mouse.current.enabled)InputSystem.EnableDevice(Mouse.current);
        var data=ReadCurrent();File.WriteAllText(Evidence+"/input-restored.json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));return data;
    }
    const string Root = "E:/personalProject/Demo3/demo6";
    const string Evidence = Root + "/검증/돌갑충-예고-v031";
    public static object ReadCurrent() => new { EditorApplication.isPlaying, EditorApplication.isPaused,
        scene = SceneManager.GetActiveScene().path, dirty = SceneManager.GetActiveScene().isDirty,
        combat = (bool)CombatTestRoot.Instance, dungeon = (bool)DungeonRoot.Instance,
        rootNames = SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name).ToArray(),
        devices = InputSystem.devices.Select(d => new { d.deviceId, d.name, d.enabled, d.native }).ToArray() };
    public static async Task<object> OpenCombatTest()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Play required");
        if (!CombatTestRoot.Instance)
        {
            var op = SceneManager.LoadSceneAsync("CombatTest");
            while (!op.isDone) await Task.Delay(10);
        }
        return new { scene = SceneManager.GetActiveScene().name, root = (bool)CombatTestRoot.Instance };
    }
    public static object SaveAndStop()
    {
        if (Application.dataPath.Replace("\\", "/") != Root + "/Assets") throw new Exception("Original project only");
        var scene = SceneManager.GetActiveScene();
        if (scene.name != "CombatTest" || scene.isDirty) throw new Exception("Preserve unexpected or dirty scene");
        if (Keyboard.current == null || !Keyboard.current.native || !Keyboard.current.enabled || Mouse.current == null || !Mouse.current.native || !Mouse.current.enabled) throw new Exception("Hardware input is not ready");
        var root = CombatTestRoot.Instance;
        var tuning = typeof(Tuning).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => !f.IsLiteral && (f.FieldType.IsPrimitive || f.FieldType.IsEnum || f.FieldType == typeof(string)))
            .ToDictionary(f => f.Name, f => f.GetValue(null));
        var data = new { EditorApplication.isPlaying, EditorApplication.isPaused, scene = scene.path, scene.isDirty,
            preset = root ? (int)root.CurrentPreset : 0, floor = root ? root.Floor : 1,
            weapon = root ? root.TestWeaponId : "", tuning,
            devices = InputSystem.devices.Select(d => new { d.deviceId, d.name, d.native, d.enabled }).ToArray() };
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence + "/editor-before.json", Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented));
        EditorApplication.isPlaying = false;
        return data;
    }
    public static object RestoreRuntime()
    {
        var before = JObject.Parse(File.ReadAllText(Evidence + "/editor-before.json"));
        var root = CombatTestRoot.Instance;
        if (!EditorApplication.isPlaying || !root) throw new Exception("CombatTest must be running");
        foreach (var item in (JObject)before["tuning"])
        {
            var field = typeof(Tuning).GetField(item.Key, BindingFlags.Public | BindingFlags.Static);
            if (field != null && !field.IsInitOnly) field.SetValue(null, item.Value.ToObject(field.FieldType));
        }
        root.SetFloor((int)before["floor"]);
        root.SetTestWeapon((string)before["weapon"]);
        root.ApplyPreset((CombatTestRoot.Preset)(int)before["preset"]);
        EditorApplication.isPaused = (bool)before["isPaused"];
        var game = Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().Name == "GameView");
        if (game) game.Focus();
        var data = new { EditorApplication.isPlaying, EditorApplication.isPaused, scene = SceneManager.GetActiveScene().path,
            preset = root.CurrentPreset.ToString(), root.Floor, root.TestWeaponId,
            devices = InputSystem.devices.Select(d => new { d.deviceId, d.name, d.native, d.enabled }).ToArray() };
        File.WriteAllText(Evidence + "/editor-restored.json", Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented));
        return data;
    }
}
