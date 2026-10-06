using System;
using System.Linq;
using UnityEditor;
using Demo5.FrontEnd;

// Resets ExpeditionBattlePanel > Rules in the prefab to the reviewed code defaults (FieldBattleRules initialisers).
// Serialized values do not follow later code-default changes, so run this after retuning defaults on purpose.
// It overwrites Inspector tuning of Rules; it does not touch anything else.
public static class SyncBattleRules
{
    const string Prefab = "Assets/Prefabs/Settlement/ExpeditionBattlePanel.prefab";
    static string Differences(FieldBattleRules current)
    {
        var defaults = new FieldBattleRules();
        return string.Join("; ", typeof(FieldBattleRules).GetFields().Where(f => !Equals(f.GetValue(current), f.GetValue(defaults)))
            .Select(f => f.Name + " " + f.GetValue(current) + " -> " + f.GetValue(defaults)));
    }
    public static string Report()
    {
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try { var d = Differences(root.GetComponent<ExpeditionBattlePanel>().Rules); return d.Length == 0 ? "Prefab rules equal code defaults." : "Differs: " + d; }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    public static string Apply()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop first");
        var root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var panel = root.GetComponent<ExpeditionBattlePanel>(); var changed = Differences(panel.Rules);
            panel.Rules = new FieldBattleRules(); PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            return changed.Length == 0 ? "Already equal." : "Synced: " + changed;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); AssetDatabase.SaveAssets(); }
    }
}
