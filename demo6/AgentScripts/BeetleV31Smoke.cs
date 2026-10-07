using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Demo6.Core.Combat;
using Demo6.Game;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using Newtonsoft.Json;
using Object=UnityEngine.Object;

public static class BeetleV31Smoke
{
    const string Ev="E:/personalProject/Demo3/demo6/검증/돌갑충-예고-v031";
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static void Need(bool ok,string why){if(!ok)throw new Exception(why);}
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    static void Write(string name,object data)=>File.WriteAllText(Ev+"/"+name,JsonConvert.SerializeObject(data,Formatting.Indented));
    public static async Task<object> Run()
    {
        Need(EditorApplication.isPlaying&&CombatTestRoot.Instance,"CombatTest Play required");
        var behaviours=Object.FindObjectsByType<MonoBehaviour>().Where(b=>b.GetType().Namespace=="Demo6.Game").ToDictionary(b=>b,b=>b.enabled);
        var bodies=Object.FindObjectsByType<Rigidbody2D>().ToDictionary(b=>b,b=>b.simulated);
        var paused=EditorApplication.isPaused;var scale=Time.timeScale;var audio=AudioListener.volume;var random=UnityEngine.Random.state;var hitStop=Tuning.HitStopEnabled;
        BoarBrain boar=null;GameObject wall=null;List<Sprite> sprites=null;GameObject pool=null;int impacts=0,dust=0;bool hidden=false,resourceClean=false;
        try
        {
            foreach(var b in behaviours.Keys)b.enabled=false;foreach(var b in bodies.Keys)b.simulated=false;EditorApplication.isPaused=false;Time.timeScale=1;AudioListener.volume=0;Tuning.HitStopEnabled=false;
            boar=(BoarBrain)EnemySpawner.Create(MonsterKind.Boar,2,new Vector2(100,0));boar.NoReward=true;
            typeof(Enemy).GetField("Facing",F).SetValue(boar,Vector2.right);
            var rig=TopDownEnemyRig.Create(boar);rig.Apply();rig.Drive(.025f,AssetDatabase.LoadAssetAtPath<CombatArtSet>("Assets/Data/Combat/CombatArtSet.asset").topDown);
            var fx=boar.GetComponent<BeetlePatternVfx>();Need(fx,"Fx missing");
            sprites=new List<Sprite>((List<Sprite>)typeof(BeetlePatternVfx).GetField("owned",F).GetValue(fx));pool=(GameObject)typeof(BeetlePatternVfx).GetField("poolRoot",F).GetValue(fx);
            wall=new GameObject("v031 smoke wall");wall.layer=Layers.Wall;wall.transform.position=new Vector3(103.2f,0,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(.6f,3);
            Call(boar,"BeginChargeTelegraph",Vector2.right,.15f);
            var until=DateTime.UtcNow.AddSeconds(6);
            while((fx.WallImpactCount==0||fx.ActiveCount>0)&&DateTime.UtcNow<until)await Task.Delay(20);
            impacts=fx.WallImpactCount;dust=fx.DustCount;Need(impacts==1&&dust>0&&boar.Broken&&fx.ActiveCount==0,"Normal Update/FixedUpdate/LateUpdate collision effect/expiration failed");
            fx.enabled=false;hidden=boar.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name.StartsWith("Beetle toe")).All(r=>!r.enabled);Need(hidden&&fx.ActiveCount==0,"Disable cleanup failed");
            Call(boar,"OnInterrupted");Object.Destroy(boar.gameObject);boar=null;await Task.Delay(60);
            resourceClean=!pool&&sprites.All(s=>!s)&&Resources.Load<Texture2D>("OgreVfxV30/dust")&&Resources.Load<Texture2D>("BeetleV31/body");Need(resourceClean,"Owned effects/sprites were not released");
            var data=new{success=true,method="Normal Unity Update, FixedUpdate and collision callbacks, no scripted physics steps; hit-stop temporarily off because original time service is frozen",impacts,dust,allEffectsExpiredNaturally=true,disabledToesHidden=hidden,ownedSpriteCount=sprites.Count,ownedSpritesAndPoolReleased=resourceClean,sharedTexturesRetained=true,input=InputSystem.devices.Select(d=>new{d.deviceId,d.native,d.enabled}).ToArray()};Write("lifecycle.json",data);return data;
        }
        finally
        {
            if(boar){Call(boar,"OnInterrupted");Object.Destroy(boar.gameObject);}if(wall)Object.Destroy(wall);
            foreach(var t in Object.FindObjectsByType<Telegraph>())if(t.Origin.x>99&&t.Origin.x<104)t.Cancel();
            foreach(var p in behaviours)if(p.Key)p.Key.enabled=p.Value;foreach(var p in bodies)if(p.Key)p.Key.simulated=p.Value;
            Tuning.HitStopEnabled=hitStop;Time.timeScale=scale;AudioListener.volume=audio;UnityEngine.Random.state=random;EditorApplication.isPaused=paused;
        }
    }
    public static object GameView()
    {
        Need(EditorApplication.isPlaying&&CombatTestRoot.Instance,"CombatTest Play required");var main=Camera.main;var root=new GameObject("v031 temporary capture");var list=new List<Telegraph>();bool dark=Telegraph.AboveDark;RenderTexture rt=null;Texture2D image=null;
        try
        {
            Telegraph.AboveDark=false;Vector2 center=main.transform.position;
            list.Add(Telegraph.Circle(center+new Vector2(-5,2.3f),2,1));list.Add(Telegraph.HalfDisc(center+new Vector2(1,2.3f),Vector2.right,3,1));list.Add(Telegraph.Rect(center+new Vector2(-4,-3.5f),Vector2.right,7.55f,1.2f,1));
            foreach(var t in list){t.transform.SetParent(root.transform,true);t.Drive(.7f);}
            var cameraGo=new GameObject("v031 capture camera");cameraGo.transform.SetParent(root.transform,false);var cam=cameraGo.AddComponent<Camera>();cam.CopyFrom(main);cam.enabled=false;cam.transform.position=main.transform.position;cam.transform.rotation=main.transform.rotation;
            int w=main.pixelWidth,h=main.pixelHeight;rt=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32);cam.targetTexture=rt;image=new Texture2D(w,h,TextureFormat.RGB24,false);cam.Render();var old=RenderTexture.active;
            try{RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,w,h),0,0);image.Apply();File.WriteAllBytes(Ev+"/telegraphs-original-game-view.png",image.EncodeToPNG());}finally{RenderTexture.active=old;}
            var probe=Telegraph.Circle(center,2,1);list.Add(probe);probe.transform.SetParent(root.transform,true);probe.Drive(.2f);var edge=probe.transform.Find("Inner rim").GetComponent<SpriteRenderer>();float low=edge.color.a;probe.Drive(.99f);float high=edge.color.a;Need(high>low&&high<=.73f,"Single pre-impact contrast ramp failed");
            Telegraph.AboveDark=true;var darkProbe=Telegraph.HalfDisc(center,Vector2.right,3,1);list.Add(darkProbe);darkProbe.transform.SetParent(root.transform,true);
            Need(darkProbe.transform.Find("Outline").GetComponent<SpriteRenderer>().sortingOrder==14500&&darkProbe.transform.Find("Inner rim").GetComponent<SpriteRenderer>().sortingOrder==14501&&darkProbe.transform.Find("Fill").GetComponent<SpriteRenderer>().sortingOrder==-59,"Darkness ordering changed");
            var data=new{success=true,width=w,height=h,gamePixelsPerUnit=h/(main.orthographicSize*2),rimAlphaEarly=low,rimAlphaBeforeImpact=high,aboveDarkOrderPassed=true,scene="Original CombatTest existing floor/lighting; temporary warning visuals only; no saved scene change"};Write("game-view.json",data);return data;
        }
        finally{foreach(var t in list)if(t)t.Cancel();if(root){var c=root.GetComponentInChildren<Camera>();if(c)c.targetTexture=null;Object.Destroy(root);}if(rt){rt.Release();Object.Destroy(rt);}if(image)Object.Destroy(image);Telegraph.AboveDark=dark;}
    }
}
