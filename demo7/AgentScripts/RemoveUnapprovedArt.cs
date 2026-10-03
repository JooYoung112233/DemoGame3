using System;
using UnityEditor;

// User requested removal of generated art. Delete importer-owned assets through Unity.
public static class RemoveUnapprovedArt
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop Play mode first");
        var paths = new[] { "Assets/Art", "Assets/Screenshots", "Assets/Resources/EastTrainArtSet.asset",
            "Assets/Resources/EastTrainWorldArtSet.asset", "Assets/Resources/EastTrainCrewArtSet.asset" };
        foreach (var path in paths)
            if (AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null)
                if (!AssetDatabase.DeleteAsset(path)) throw new Exception("Could not remove " + path);
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        return "Removed generated Unity textures, captures and three art-set assets.";
    }
}
