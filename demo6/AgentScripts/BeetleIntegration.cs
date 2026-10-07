using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo6.Core.Combat;
using Demo6.Game;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Newtonsoft.Json;

public static class BeetleIntegration
{
    const string Root="E:/personalProject/Demo3/demo6";
    const string Evidence=Root+"/검증/돌갑충-큰색면-v028";
    const string Asset="Assets/Art/TopDown/Enemies/td_stone_beetle_v028.png";
    public static object FinalState()
    {
        var shader=Shader.Find("Demo6/SpriteFlash");
        var errors=ShaderUtil.GetShaderMessages(shader).Where(m=>m.severity.ToString()=="Error").Select(m=>m.message).ToArray();
        var data=new{EditorApplication.isPlaying,EditorApplication.isPaused,EditorApplication.isCompiling,scene=SceneManager.GetActiveScene().path,SceneManager.GetActiveScene().isDirty,
            shaderErrors=errors,devices=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray(),virtualDevices=InputSystem.devices.Count(d=>!d.native)};
        File.WriteAllText(Evidence+"/final-state.json",JsonConvert.SerializeObject(data,Formatting.Indented));return data;
    }
    public static async Task<object> AutomaticSmoke()
    {
        if(!EditorApplication.isPlaying||!CombatTestRoot.Instance)throw new Exception("CombatTest required");
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        bool sound=Tuning.Sound;Tuning.Sound=false;OgreBrain ogre=null;BoarBrain beetle=null;
        try
        {
            ogre=(OgreBrain)EnemySpawner.Create(MonsterKind.Ogre,2,new Vector2(100,0));
            beetle=(BoarBrain)EnemySpawner.Create(MonsterKind.Boar,2,new Vector2(110,0));
            ogre.NoReward=beetle.NoReward=true;
            var look=ogre.GetComponentInChildren<OgreLook>();var rig=ogre.GetComponentInChildren<OgreArtRig>();
            float chewBefore=(float)typeof(OgreLook).GetField("_chewAt",flags).GetValue(look);
            var owned=((System.Collections.IEnumerable)typeof(OgreArtRig).GetField("ownedSprites",flags).GetValue(rig)).Cast<Sprite>().ToArray();
            var ring=(Texture2D)typeof(OgreArtRig).GetField("ringTexture",flags).GetValue(rig);
            await Task.Delay(1800);
            float chewAfter=(float)typeof(OgreLook).GetField("_chewAt",flags).GetValue(look);
            var legs=beetle.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Leg").ToArray();
            bool correct=look.enabled&&rig.enabled&&!ogre.GetComponent<SpriteFlash>().target.enabled&&chewAfter>chewBefore
                &&AssetDatabase.GetAssetPath(beetle.GetComponent<SpriteFlash>().target.sprite)==Asset
                &&legs.Length==4&&legs.All(r=>!r.enabled);
            if(!correct)throw new Exception("Automatic visual update failed");
            ogre.Remove();beetle.Remove();await Task.Delay(100);
            bool disposed=owned.All(s=>!s)&&!ring;
            if(!disposed)throw new Exception("Generated ogre sprites not disposed");
            var data=new{success=true,automaticLateUpdate=true,chewScheduleAdvanced=true,extraFeetVisible=0,ownedOgreSpritesDisposed=owned.Length,ringTextureDisposed=disposed,virtualDevices=InputSystem.devices.Count(d=>!d.native)};
            File.WriteAllText(Evidence+"/automatic-smoke.json",JsonConvert.SerializeObject(data,Formatting.Indented));return data;
        }
        finally{if(ogre)ogre.Remove();if(beetle)beetle.Remove();Tuning.Sound=sound;}
    }
    public static object Baseline()
    {
        if(Application.dataPath.Replace("\\","/")!=Root+"/Assets")throw new Exception("Original project only");
        var s=TopDownSprites.Boar;
        var rt=RenderTexture.GetTemporary(s.texture.width,s.texture.height,0,RenderTextureFormat.ARGB32);
        var active=RenderTexture.active;var copy=new Texture2D(s.texture.width,s.texture.height,TextureFormat.RGBA32,false);
        Color32[] p;
        try{Graphics.Blit(s.texture,rt);RenderTexture.active=rt;copy.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);copy.Apply();p=copy.GetPixels32();}
        finally{RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(copy);}
        int minX=s.texture.width,minY=s.texture.height,maxX=0,maxY=0;
        for(int y=0;y<s.texture.height;y++)for(int x=0;x<s.texture.width;x++)if(p[y*s.texture.width+x].a>0){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
        float length=(maxX-minX+1)/s.pixelsPerUnit*MonsterRule.Of(MonsterKind.Boar).Diameter;
        var data=new{EditorApplication.isPlaying,EditorApplication.isPaused,scene=SceneManager.GetActiveScene().path,SceneManager.GetActiveScene().isDirty,
            sourceBoar=new{s.name,s.pixelsPerUnit,minX,minY,maxX,maxY,length,diameter=MonsterRule.Of(MonsterKind.Boar).Diameter},
            devices=InputSystem.devices.Select(d=>new{d.deviceId,d.name,d.native,d.enabled}).ToArray()};
        File.WriteAllText(Evidence+"/editor-before.json",JsonConvert.SerializeObject(data,Formatting.Indented));return data;
    }
    public static object Import()
    {
        AssetDatabase.ImportAsset(Asset,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(Asset);
        importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spritePivot=new Vector2(.5f,.5f);importer.spritePixelsPerUnit=660;
        importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.isReadable=true;importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;importer.filterMode=FilterMode.Bilinear;
        importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        return new{guid=AssetDatabase.AssetPathToGUID(Asset),importer.spritePixelsPerUnit,sprite=(bool)AssetDatabase.LoadAssetAtPath<Sprite>(Asset)};
    }
    public static object VerifyImport()
    {
        AssetDatabase.ImportAsset("Assets/Data/Combat/CombatArtSet.asset",ImportAssetOptions.ForceSynchronousImport);
        var set=AssetDatabase.LoadAssetAtPath<CombatArtSet>("Assets/Data/Combat/CombatArtSet.asset");var s=set.topDown.boar.body;
        if(!s||!set.topDown.boar.feetInBody)throw new Exception("Beetle slot not connected");
        var data=new{body=AssetDatabase.GetAssetPath(s),s.pixelsPerUnit,width=s.texture.width,height=s.texture.height,set.topDown.boar.scale,set.topDown.boar.feetInBody};
        File.WriteAllText(Evidence+"/import.json",JsonConvert.SerializeObject(data,Formatting.Indented));return data;
    }
}
