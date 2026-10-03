using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using Demo6.Game;

// Read-only project audit; PNGs below are inspection captures of existing code-generated sprites.
public static class InspectTopDownResources
{
    public static object Capture()
    {
        string output = Path.GetFullPath("ResourceCleanupLogs/2026-10-03/pixels");
        Directory.CreateDirectory(output);
        var sprites = new[] {TopDownSprites.PlayerBody, TopDownSprites.Longsword,
            TopDownSprites.Greatsword, TopDownSprites.Twinblade, TopDownSprites.Rat,
            TopDownSprites.Boar, TopDownSprites.Archer};
        var info = sprites.Select((s,i) => {
            var rt = RenderTexture.GetTemporary(s.texture.width, s.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Texture2D copy = null;
            try {
                Graphics.Blit(s.texture, rt);
                RenderTexture.active = rt;
                copy = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); copy.Apply();
                File.WriteAllBytes(Path.Combine(output,i.ToString("00")+".png"),copy.EncodeToPNG());
            } finally {
                RenderTexture.active=previous;
                if(copy) UnityEngine.Object.DestroyImmediate(copy);
                RenderTexture.ReleaseTemporary(rt);
            }
            return new {s.name, width=s.texture.width, height=s.texture.height, s.pixelsPerUnit,
                filter=s.texture.filterMode.ToString(), asset=AssetDatabase.GetAssetPath(s)};
        }).ToArray();
        var asset="Assets/Data/Combat/CombatArtSet.asset";
        var art=AssetDatabase.LoadAssetAtPath<CombatArtSet>(asset);
        var value = new {
            capturedUtc=DateTime.UtcNow.ToString("o"), playing=EditorApplication.isPlaying,
            compiling=EditorApplication.isCompiling, topDownEnabled=TopDownView.Enabled,
            topDownActive=TopDownView.Active, sprites=info,
            scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i)).Select(s=>new{s.path,s.isDirty}).ToArray(),
            artDirty=art&&EditorUtility.IsDirty(art),
            dependencies=new[]{"Assets/Scenes/DungeonTest.unity","Assets/Scenes/CombatTest.unity",asset}.Select(p=>new{path=p,assets=AssetDatabase.GetDependencies(p,true)}).ToArray()
        };
        var json=Newtonsoft.Json.JsonConvert.SerializeObject(value,Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText("ResourceCleanupLogs/2026-10-03/unity-before.json",json);
        return json;
    }
}
