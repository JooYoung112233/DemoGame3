using GamePlayer = Demo6.Game.PlayerController;
using System;using System.IO;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Demo6.Game;using Newtonsoft.Json;using Object=UnityEngine.Object;
public static class V056FxReview {
 const string Out="검증/v056-fx";const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static void Capture(Camera c,string name){var old=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1920,1080,24);var t=new Texture2D(1920,1080,TextureFormat.RGB24,false);try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,1920,1080),0,0);t.Apply();File.WriteAllBytes(Out+"/"+name+".png",t.EncodeToPNG());}finally{c.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(t);}}
 public static object Run(){if(!Application.isPlaying||!GamePlayer.Instance)throw new Exception("Original game required");var p=GamePlayer.Instance;var camera=Camera.main;var cp=camera.transform.position;var size=camera.orthographicSize;bool paused=EditorApplication.isPaused,sparks=Tuning.HitSparks;var initial=new HashSet<GameObject>(Object.FindObjectsByType<GameObject>());var random=UnityEngine.Random.state;var checks=new List<object>();var errors=new List<string>();Application.LogCallback log=(s,t,l)=>{if(l==LogType.Error||l==LogType.Exception)errors.Add(s);};Application.logMessageReceived+=log;
 try{EditorApplication.isPaused=true;Tuning.HitSparks=true;camera.transform.position=new Vector3(p.Position.x,p.Position.y,cp.z);
 foreach(var mode in new[]{"wall-visible","wall-under-body","blocked-visible"}){
 foreach(var oldFx in Object.FindObjectsByType<ImportedFxV056>())Object.DestroyImmediate(oldFx.gameObject);
 var at=p.Position+(mode=="wall-under-body"?Vector2.zero:Vector2.right*1.1f);int before=ImportedFxV056.SpawnCount,beforeParticles=ImportedFxV056.EmittedCount;var rngBefore=UnityEngine.Random.state;
 if(mode.StartsWith("wall"))HitEffects.WallDust(at,Vector2.right,6);else HitEffects.OnBlocked(at,Vector2.left);
 var fx=Object.FindFirstObjectByType<ImportedFxV056>();if(!fx)throw new Exception("FX production hook did not spawn");var systems=fx.GetComponentsInChildren<ParticleSystem>();
 var counts=systems.Select(ps=>new{name=ps.name,count=ps.particleCount,sorting=ps.GetComponent<ParticleSystemRenderer>().sortingOrder,material=ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.name}).ToArray();
 for(int i=0;i<10;i++){if(i>0)foreach(var ps in systems)ps.Simulate(1f/30f,false,false,true);Capture(camera,mode+"-"+i.ToString("00"));}
 var particles=new List<object>();foreach(var ps in systems){var data=new ParticleSystem.Particle[64];int n=ps.GetParticles(data);particles.Add(new{name=ps.name,alive=n,z=data.Take(n).Select(x=>x.position.z).ToArray()});}
 camera.orthographicSize=2.2f;Capture(camera,mode+"-detail-end");camera.orthographicSize=size;
 foreach(var ps in systems)ps.Simulate(.6f,false,false,true);
 checks.Add(new{mode,spawnDelta=ImportedFxV056.SpawnCount-before,particleBudget=ImportedFxV056.EmittedCount-beforeParticles,counts,particles,expired=systems.All(x=>x.particleCount==0),gameplayRandomUnchanged=JsonUtility.ToJson(rngBefore)==JsonUtility.ToJson(UnityEngine.Random.state)});
 }
 }finally{foreach(var go in Object.FindObjectsByType<GameObject>())if(go&&!initial.Contains(go))Object.DestroyImmediate(go);camera.transform.position=cp;camera.orthographicSize=size;Tuning.HitSparks=sparks;UnityEngine.Random.state=random;EditorApplication.isPaused=paused;Application.logMessageReceived-=log;}
 var result=new{utc=DateTime.UtcNow.ToString("O"),method="Production HitEffects hooks in original scene/camera/light, synthetic contact cues and particle clock samples. No physical input. Restored camera/pause/Tuning/Random; removed all fixture objects.",checks,errors};File.WriteAllText(Out+"/runtime.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;}
}
