using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Demo6.Game;using Newtonsoft.Json;using Object=UnityEngine.Object;using Player=Demo6.Game.PlayerController;
public static class TownV057AssemblyViews
{
 static float[] V(Vector3 v) => new[]{v.x,v.y,v.z};
 const string Out="검증/town-npc-v057/assembly-correction";
 static void Capture(Camera c,string name)
 {
  var old=c.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1920,1080,24);var t=new Texture2D(1920,1080,TextureFormat.RGB24,false);
  try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,1920,1080),0,0);t.Apply();File.WriteAllBytes(Out+"/"+name+".png",t.EncodeToPNG());}
  finally{c.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(t);}
 }
 public static object ReadOnlyCurrent()
 {
  if(!TownRoot.Instance||!Player.Instance)throw new Exception("Live Town required");
  string stamp=DateTime.UtcNow.ToString("HHmmss");var c=Camera.main;Capture(c,"current-"+stamp);
  var npcs=Object.FindObjectsByType<TownNpcIdleV057>().Select(n=>new{role=n.Role,position=new[]{n.transform.position.x,n.transform.position.y},frame=n.CurrentFrame,order=n.GetComponent<SpriteRenderer>().sortingOrder,scale=V(n.transform.lossyScale),sprite=n.GetComponent<SpriteRenderer>().sprite.name,bounds=V(n.GetComponent<SpriteRenderer>().bounds.size),shader=n.GetComponent<SpriteRenderer>().sharedMaterial.shader.name}).ToArray();
  var solids=Object.FindObjectsByType<BoxCollider2D>().Where(x=>x.enabled&&x.gameObject.layer==Layers.Wall).Select(x=>new{name=x.name,center=V(x.bounds.center),size=V(x.bounds.size)}).ToArray();
  var result=new{utc=DateTime.UtcNow.ToString("O"),mode="READ ONLY USER SESSION: no pause/pose/scene/input/profile mutation; camera target restored after capture",capture="current-"+stamp+".png",scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,camera=new{position=V(c.transform.position),size=c.orthographicSize,screen=new[]{Screen.width,Screen.height}},npcs,solids,residents=TownNpc.Residents.Select(x=>new{id=x.NpcId,position=V(x.Position)}).ToArray(),player=new{position=V(Player.Instance.Position),collider=V(Player.Instance.GetComponent<Collider2D>().bounds.size),sprites=Player.Instance.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.enabled&&x.sprite).Select(x=>new{name=x.name,size=V(x.bounds.size),order=x.sortingOrder,layer=x.sortingLayerName}).ToArray()},saveSession=GameSession.FromTitle};
  File.WriteAllText(Out+"/current-"+stamp+".json",JsonConvert.SerializeObject(result,Formatting.Indented));return new{capture=result.capture,npcs=npcs.Length,player=Player.Instance.Position,saveSession=GameSession.FromTitle};
 }
 public static object StartUnsavedReview()
 {
  if(!Application.isPlaying)throw new Exception("Play required");
  if(GameSession.FromTitle)throw new Exception("Do not replace a user save session");
  ProfileCarry.Ensure();return new{loaded=SceneTravel.Load(SceneTravel.TownPath),saveSession=GameSession.FromTitle};
 }
 public static object CaptureAll()
 {
  if(!TownRoot.Instance||!Player.Instance)throw new Exception("Live original town required");
  var c=Camera.main;var rig=TownRoot.Instance.CameraRig;var original=c.transform.position;bool enabled=rig&&rig.enabled;
  var data=new List<object>();try{
   if(rig)rig.enabled=false;
   foreach(var spot in new[]{new Vector2(42,41),new Vector2(36.25f,43),new Vector2(36.25f,33),new Vector2(47.75f,33)})
   {c.transform.position=new Vector3(spot.x,spot.y,original.z);string name="town-"+data.Count;Capture(c,name);data.Add(new{name,center=new[]{spot.x,spot.y},ortho=c.orthographicSize});}
  }finally{c.transform.position=original;if(rig)rig.enabled=enabled;}
  var npcs=Object.FindObjectsByType<TownNpcIdleV057>().Select(n=>new{role=n.Role,position=new[]{n.transform.position.x,n.transform.position.y},frame=n.CurrentFrame,sprite=n.GetComponent<SpriteRenderer>().sprite.name,ppu=n.GetComponent<SpriteRenderer>().sprite.pixelsPerUnit,order=n.GetComponent<SpriteRenderer>().sortingOrder}).ToArray();
  var result=new{utc=DateTime.UtcNow.ToString("O"),scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,method="Original Town camera renderer at 1920x1080, unchanged ortho/light; temporarily sampled four legal camera centers, restored afterwards; no native input",cameras=data,npcs,residents=TownNpc.Residents.Select(x=>new{id=x.NpcId,position=V(x.Position)}).ToArray(),playerPosition=V(Player.Instance.Position),playerBounds=Player.Instance.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.enabled&&x.sprite).Select(x=>new{name=x.name,size=V(x.bounds.size),order=x.sortingOrder,layer=x.sortingLayerName}).ToArray(),saveSession=GameSession.FromTitle};
  File.WriteAllText(Out+"/initial-runtime.json",JsonConvert.SerializeObject(result,Formatting.Indented));return new{npcCount=npcs.Length,captures=data.Count,saveSession=GameSession.FromTitle};
 }
}
