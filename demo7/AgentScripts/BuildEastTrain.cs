using EastTrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildEastTrain
{
    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 8.1f;
        camera.transform.position = new Vector3(0, 1.3f, -10); camera.gameObject.AddComponent<AudioListener>();
        new GameObject("East Train Prototype").AddComponent<EastTrainDemo>();
        PlayerSettings.productName = "East Train";
        PlayerSettings.runInBackground = true;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/EastTrain.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/EastTrain.unity", true) };
        AssetDatabase.SaveAssets();
        return "EastTrain scene saved; play to construct the procedural cutaway locomotive.";
    }
}
