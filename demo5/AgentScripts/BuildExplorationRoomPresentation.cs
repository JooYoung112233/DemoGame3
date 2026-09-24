using System;
using Demo5.FrontEnd;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Prefab-only presentation wiring. Keeps the actual painted background, scene overrides and game data intact.
public static class BuildExplorationRoomPresentation
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before wiring the world prefab.");
        const string path = "Assets/Prefabs/Settlement/ExpeditionWorld.prefab";
        var world = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var presentation = world.GetComponent<ExplorationRoomPresentation>() ?? world.AddComponent<ExplorationRoomPresentation>();
            presentation.Ambient = world.transform.Find("Ambient")?.GetComponent<Light2D>();
            presentation.EntranceLight = world.transform.Find("EntranceLight")?.GetComponent<Light2D>();
            presentation.PawnRoot = world.transform.Find("Pawns");
            if (!presentation.Ambient || !presentation.EntranceLight || !presentation.PawnRoot)
                throw new InvalidOperationException("Existing expedition light/pawn hierarchy is incomplete.");
            // Defaults are populated only when the component is first added. Subsequent runs preserve Inspector tuning.
            presentation.ApplyRoom(0);
            EditorUtility.SetDirty(presentation);
            PrefabUtility.SaveAsPrefabAsset(world, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(world); }
        AssetDatabase.SaveAssets();
        return "Wired room-specific lighting, matching pawn shadows and directional door formations in ExpeditionWorld.prefab. Backgrounds, viewport fitting, combat rules and save data unchanged.";
    }
}
