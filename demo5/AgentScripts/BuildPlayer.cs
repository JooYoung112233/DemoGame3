using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BuildPlayer
{
    public static string Windows()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/NightExpedition.unity" },
            locationPathName = "Builds/Windows/Demo5.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Build failed: " + report.summary.result + ", errors " + report.summary.totalErrors);
        return "Windows build succeeded: Builds/Windows/Demo5.exe, " + report.summary.totalSize + " bytes";
    }
}
