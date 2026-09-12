using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class RemoveHudPreview
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop play mode before removing the review scene.");
        const string previewPath = "Assets/_Project/Scenes/02_HudPreview.unity";
        const string titlePath = "Assets/_Project/Scenes/00_Title.unity";
        var title = SceneManager.GetSceneByPath(titlePath);
        if (!title.isLoaded) title = EditorSceneManager.OpenScene(titlePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(title);
        var preview = SceneManager.GetSceneByPath(previewPath);
        if (preview.isLoaded) EditorSceneManager.CloseScene(preview, true);
        // The user requested removal of this exact disposable scene and its exclusive bootstrap.
        foreach (var path in new[] { previewPath, "Assets/_Project/Scripts/UI/GameHudPreview.cs" })
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)) && !AssetDatabase.DeleteAsset(path))
                throw new Exception("Could not delete " + path);
        AssetDatabase.Refresh();
        return "Removed HUD review scene and bootstrap through AssetDatabase. Title is active. Build scenes: "
            + string.Join(", ", EditorBuildSettings.scenes.Select(s => s.path));
    }
    public static string Verify()
    {
        var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
        if (scenes.Length != 2 || scenes.Any(p => p.Contains("Preview"))) throw new Exception("Unexpected scene list.");
        foreach (var path in scenes)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.isLoaded;
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                int missing = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.Transform>(true))
                    .Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if (missing != 0) throw new Exception(path + " has missing scripts: " + missing);
            }
            finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
        }
        return "Only 00_Title and 01_Game remain in _Project/Scenes; both have zero missing scripts; active=" + SceneManager.GetActiveScene().name;
    }
}
