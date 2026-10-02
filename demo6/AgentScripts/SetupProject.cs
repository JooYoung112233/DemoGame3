using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run once on the fresh template via Unity Pipeline; keep outside Assets.
public static class SetupProject
{
    public static string Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before setup.");

        foreach (var name in new[] { "Scripts", "Prefabs", "Art", "Audio", "Data" })
            if (!AssetDatabase.IsValidFolder("Assets/" + name))
                AssetDatabase.CreateFolder("Assets", name);

        const string main = "Assets/Scenes/Main.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(main) == null)
        {
            var error = AssetDatabase.MoveAsset("Assets/Scenes/SampleScene.unity", main);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }

        var scene = EditorSceneManager.OpenScene(main, OpenSceneMode.Single);
        var camera = Camera.main;
        if (camera == null || !camera.orthographic)
            throw new InvalidOperationException("Expected the template's orthographic Main Camera.");

        PlayerSettings.companyName = "PersonalProject";
        PlayerSettings.productName = "Demo6";
        PlayerSettings.bundleVersion = "0.1.0";
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        EditorSettings.serializationMode = SerializationMode.ForceText;
        UnityEditor.VersionControlSettings.mode = "Visible Meta Files";
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(main, true) };
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Demo6 ready: Main scene, 2D camera, URP template, 1920x1080 window, asset folders, text serialization.";
    }
}
