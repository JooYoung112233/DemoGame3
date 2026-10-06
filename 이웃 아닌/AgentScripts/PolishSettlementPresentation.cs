using System;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class PolishSettlementPresentation
{
    public static string Run()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
            throw new Exception("Stop play and preserve unsaved scene changes first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");
        var camera = Camera.main;
        if (!camera.GetComponent<WorldViewport>()) camera.gameObject.AddComponent<WorldViewport>();
        camera.GetComponent<WorldViewport>().Fit();
        EditorSceneManager.SaveScene(scene);

        const string path = "Assets/Prefabs/Settlement/SettlementWorld.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var light in root.GetComponentsInChildren<Light2D>(true))
            {
                if (light.name == "Ambient") { light.intensity = .64f; light.color = new Color(.84f,.9f,1f); }
                if (light.name == "WallLamp") { light.intensity = 1.05f; light.pointLightOuterRadius = 4.1f; light.pointLightInnerRadius = .35f; light.color = new Color(1f,.77f,.47f); }
                if (light.name == "ExitLamp") { light.intensity = .8f; light.pointLightOuterRadius = 3.1f; light.pointLightInnerRadius = .25f; light.color = new Color(1f,.84f,.62f); }
            }
            var motion = root.GetComponentInChildren<SettlementPawnMotion>(true);
            if (motion) { motion.Speed = 1.9f; motion.LeanDegrees = 1.4f; }
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        return "Saved fitted world camera, cool ambient/warm interior lights, gentler aisle movement.";
    }
}
