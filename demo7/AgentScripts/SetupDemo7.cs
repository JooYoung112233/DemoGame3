using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupDemo7
{
    public static string Run()
    {
        foreach (var name in new[] { "Scripts", "Prefabs", "Art", "Audio", "Data" })
            if (!AssetDatabase.IsValidFolder("Assets/" + name))
                AssetDatabase.CreateFolder("Assets", name);

        const string main = "Assets/Scenes/Main.unity";
        const string sample = "Assets/Scenes/SampleScene.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(main) == null)
        {
            var error = AssetDatabase.MoveAsset(sample, main);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        var scene = EditorSceneManager.OpenScene(main);
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        PlayerSettings.productName = "Demo7";
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(main, true) };
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Demo7 ready: " + scene.path + "; camera=" + (Camera.main != null) + "; orthographic=" + (Camera.main != null && Camera.main.orthographic);
    }
}
