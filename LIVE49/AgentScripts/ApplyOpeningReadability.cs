using System;
using System.Linq;
using Live49.Chapter00;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyOpeningReadability
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Exit play mode first.");
        const string path = "Assets/_Project/Scenes/01_Game.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool wasOpen = scene.isLoaded;
        if (wasOpen && scene.isDirty) throw new Exception("Game scene has unsaved changes; preserve them before applying.");
        if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<C0OpeningDirector>(true)).Single();
            var so = new SerializedObject(director);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Title/Art/title-background-v1.png");
            if (sprite == null) throw new Exception("Book sprite missing.");
            so.FindProperty("openingBook").objectReferenceValue = sprite;
            // Only timing fields change; all existing scene objects and asset assignments are preserved.
            var defaults = new C0OpeningTimings();
            foreach (var field in typeof(C0OpeningTimings).GetFields())
                if (field.FieldType == typeof(float)) so.FindProperty("timings." + field.Name).floatValue = (float)field.GetValue(defaults);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed.");
            return "Book shot wired; revised opening pacing applied; existing scene preserved.";
        }
        finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
    }
}
