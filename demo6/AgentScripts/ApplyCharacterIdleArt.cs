using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Demo6.Game;

public static class ApplyCharacterIdleArt
{
    const string Source = "아트/검사-오른쪽-기본자세-테스트-v1/swordsman-east-idle-test-v1.png";
    const string TexturePath = "Assets/Art/Combat/Player/swordsman-east-idle-test-v1.png";
    const string ArtPath = "Assets/Data/Combat/CombatArtSet.asset";
    const string ScenePath = "Assets/Scenes/DungeonTest.unity";

    public static string Apply()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Apply only while stopped and compiled.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Preserving unsaved scene: " + SceneManager.GetSceneAt(i).path);
        var art = AssetDatabase.LoadAssetAtPath<CombatArtSet>(ArtPath);
        if (!art || EditorUtility.IsDirty(art))
            throw new InvalidOperationException("Missing or unsaved CombatArtSet; no changes applied.");
        if (art.player.idle.Has && AssetDatabase.GetAssetPath(art.player.idle.frames[0]) != TexturePath)
            throw new InvalidOperationException("A different idle was connected during work; preserving it.");

        Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));
        if (!File.Exists(TexturePath)) File.Copy(Source, TexturePath, false);
        else if (!File.ReadAllBytes(Source).SequenceEqual(File.ReadAllBytes(TexturePath)))
            throw new InvalidOperationException("Different texture exists; preserving it.");
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 96f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(76f / 208f, (192f - 173f) / 192f);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.userData = "Static character art test, one frame. Source: " + Source + "; feet=(76,173) top-left; hand=(113,117).";
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexturePath);
        if (!sprite) throw new InvalidOperationException("Sprite import failed.");
        Undo.RecordObject(art, "Connect static swordsman art test");
        art.player.idle.frames = new[] { sprite };
        art.player.idle.fps = 1f;
        art.player.idle.keyFrameNumber = 0;
        art.player.scale = 1f;
        art.player.offset = Vector2.zero;
        art.player.previewIdleForMissingClips = true;
        EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssetIfDirty(art);

        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<DungeonRoot>(true)).ToArray();
            if (roots.Length != 1) throw new InvalidOperationException("Expected one DungeonRoot.");
            var target = roots[0];
            Undo.RecordObject(target, "Connect dungeon character art");
            var so = new SerializedObject(target);
            so.FindProperty("projectArt").objectReferenceValue = art;
            so.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Dungeon scene save failed.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
        return $"APPLIED {TexturePath}; {sprite.rect.width}x{sprite.rect.height}; PPU={sprite.pixelsPerUnit}; pivot={sprite.pivot}; Point/Uncompressed; idle1 + missing-clip preview; {ScenePath}";
    }
}
