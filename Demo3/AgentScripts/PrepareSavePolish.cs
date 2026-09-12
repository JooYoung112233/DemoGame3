using System.IO;
using Live49.Core;
using UnityEngine;
public static class PrepareSavePolish
{
    public static string Run()
    {
        string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Screenshots/SavePolish"));
        Directory.CreateDirectory(folder);
        if(SaveSystem.Current!=null)File.WriteAllText(Path.Combine(folder,"live-before.json"),JsonUtility.ToJson(SaveSystem.Current));
        File.WriteAllText(Path.Combine(folder,"real-path.txt"),SaveSystem.SlotPath);
        File.WriteAllText(Path.Combine(folder,"real-before.txt"),File.Exists(SaveSystem.SlotPath)?File.ReadAllText(SaveSystem.SlotPath):"");
        return "Live progress captured without changing saved files.";
    }
}
