using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Demo6.Game;

public static class InspectCharacterArt
{
    public static object Status()
    {
        var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i));
        var art = AssetDatabase.LoadAssetAtPath<CombatArtSet>("Assets/Data/Combat/CombatArtSet.asset");
        return Newtonsoft.Json.JsonConvert.SerializeObject(new {
            playing = EditorApplication.isPlaying,
            paused = EditorApplication.isPaused,
            compiling = EditorApplication.isCompiling,
            scenes = scenes.Select(s => new { s.name, s.path, s.isDirty, s.isLoaded }).ToArray(),
            artDirty = art && EditorUtility.IsDirty(art),
            artPresent = art != null,
            artFrames = art == null ? 0 : art.player.idle.frames.Length,
            playerScale = art == null ? 0 : art.player.scale,
            playerOffset = art == null ? "" : art.player.offset.ToString(),
            selection = Selection.activeObject ? Selection.activeObject.name : "",
            roots = Object.FindObjectsByType<CombatTestRoot>().Select(r => new { r.name, scene = r.gameObject.scene.path, art = r.ProjectArt ? AssetDatabase.GetAssetPath(r.ProjectArt) : "" }).ToArray(),
            projectMode = ArtRuntime.Mode.ToString(),
        });
    }
}
