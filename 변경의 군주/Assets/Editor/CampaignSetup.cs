using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo8;

public static class CampaignSetup
{
    [MenuItem("Demo8/캠페인 씬 만들기")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before creating the campaign scene.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var view = new GameObject("Demo8 Campaign").AddComponent<CampaignView>();
        Directory.CreateDirectory("Assets/Materials");
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CampaignSurface.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, "Assets/Materials/CampaignSurface.mat");
        }
        view.surfaceMaterial = material;
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Campaign.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Campaign.unity", true) };
        PlayerSettings.productName = "Demo8 - Border Lords";
        PlayerSettings.companyName = "DemoProjects";
        PlayerSettings.defaultScreenWidth = 1440;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
        Debug.Log("DEMO8_SETUP_OK");
    }
}
