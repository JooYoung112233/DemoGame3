using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using UnityEngine;using UnityEditor;using UnityEngine.InputSystem;using UnityEngine.Rendering.Universal;using Demo6.Game;using Newtonsoft.Json;using Object=UnityEngine.Object;
public static class V026PropsPlayCheck {
 const string Ev="E:/personalProject/Demo3/demo6/검증/오브젝트-잔여-v026/original-play";
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 static void Require(bool ok,string m){if(!ok)throw new Exception(m);}
 static void Call(object x,string n)=>x.GetType().GetMethod(n,F).Invoke(x,null);
 static void Save(string n,object x)=>File.WriteAllText(Ev+"/"+n,JsonConvert.SerializeObject(x,Formatting.Indented));
 static double Capture(Camera c,string name){var rt=RenderTexture.GetTemporary(800,600,24);var old=RenderTexture.active;var t=new Texture2D(800,600,TextureFormat.RGB24,false);try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,800,600),0,0);t.Apply();File.WriteAllBytes(Ev+"/"+name,t.EncodeToPNG());return t.GetPixels32().Average(p=>(p.r+p.g+p.b)/3.0);}finally{c.targetTexture=null;RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(t);}}
 public static object Run(){
  Require(Application.dataPath.Replace("\\","/")=="E:/personalProject/Demo3/demo6/Assets"&&EditorApplication.isPlaying&&DungeonRoot.Instance,"Original DungeonTest Play only");
  Require(!DungeonUi.ModalOpen&&!TimeScaleService.Paused,"Preserve active modal/pause");
  var inputBefore=InputSystem.devices.Select(d=>new{d.deviceId,d.native,d.enabled}).ToArray();Require(Keyboard.current!=null&&Keyboard.current.native&&Keyboard.current.enabled&&Mouse.current!=null&&Mouse.current.native&&Mouse.current.enabled,"Hardware input required");
  var renders=Object.FindObjectsByType<Renderer>().ToDictionary(r=>r,r=>r.enabled);
  var scripts=Object.FindObjectsByType<MonoBehaviour>().Where(b=>b.GetType().Namespace=="Demo6.Game").ToDictionary(b=>b,b=>b.enabled);
  var objects=new HashSet<GameObject>(Object.FindObjectsByType<GameObject>());
  var lights=Object.FindObjectsByType<Light2D>();var lightBefore=JsonConvert.SerializeObject(lights.Select(l=>new{l.name,l.enabled,l.intensity,color=l.color.ToString(),l.pointLightOuterRadius,pos=l.transform.position.ToString()}));
  var interaction=InteractionSystem.Instance;var current=interaction.Current;var held=typeof(InteractionSystem).GetField("_held",F).GetValue(interaction);var hurt=typeof(InteractionSystem).GetField("_hurtSeen",F).GetValue(interaction);
  bool paused=EditorApplication.isPaused;float volume=AudioListener.volume;var random=UnityEngine.Random.state;
  try{
   EditorApplication.isPaused=true;AudioListener.volume=0;foreach(var s in scripts.Keys)s.enabled=false;foreach(var r in renders.Keys)r.enabled=false;
   var center=(Vector3)PlayerController.Instance.Position;var staged=new List<GameObject>();var paths=new[]{"Assets/Resources/TopDownPilotV9/lantern.png","Assets/Resources/TopDownPilotV9/chain.png"};
   for(int i=0;i<paths.Length;i++){
    var donor=renders.Keys.OfType<SpriteRenderer>().FirstOrDefault(r=>r.sprite&&AssetDatabase.GetAssetPath(r.sprite)==paths[i]);Require(donor,"Actual applied prop renderer missing: "+paths[i]);
    var sr=new GameObject("Temporary applied prop sample").AddComponent<SpriteRenderer>();sr.sprite=donor.sprite;sr.sharedMaterial=donor.sharedMaterial;sr.color=donor.color;sr.sortingLayerID=donor.sortingLayerID;sr.sortingOrder=100;sr.transform.position=center+new Vector3(i==0?-2f:2f,.5f,0);sr.transform.localScale=donor.transform.lossyScale;staged.Add(sr.gameObject);
   }
   var wood=Chest.Create(null,null,(Vector2)center+new Vector2(-.6f,.4f),false);var iron=Chest.Create(null,null,(Vector2)center+new Vector2(.7f,.4f),true);staged.Add(wood.gameObject);staged.Add(iron.gameObject);
   foreach(var chest in new[]{wood,iron}){var glint=chest.transform.Find("Glint");if(glint)glint.gameObject.SetActive(false);Call(chest.GetComponent<YSort>(),"LateUpdate");}
   var cam=new GameObject("Temporary props camera").AddComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.orthographicSize=3;cam.transform.position=center+new Vector3(0,.3f,-10);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.04f,.04f,.04f);cam.cullingMask=~0;
   Require(!wood.Opened&&!iron.Opened&&wood.Available&&iron.Available,"Closed-state mismatch");double lit=Capture(cam,"props-closed-existing-light.png");
   foreach(var go in staged)go.transform.position+=Vector3.right*40;cam.transform.position+=Vector3.right*40;
   double dark=Capture(cam,"props-closed-outside-light.png");
   foreach(var go in staged)go.transform.position-=Vector3.right*40;cam.transform.position-=Vector3.right*40;
   Require(lit>dark+.1,"Actual existing light response missing");
   Call(interaction,"Update");Require(interaction.Current==wood,"Nearest temporary wood chest not selected");Call(interaction,"Use");Require(wood.Opened&&!wood.Available,"Wood did not open through interaction");
   Call(interaction,"Update");Require(interaction.Current==iron,"Nearest temporary iron chest not selected");Call(interaction,"Use");Require(iron.Opened&&!iron.Available,"Iron did not open through interaction");
   foreach(var chest in new[]{wood,iron}){Require(chest.transform.Find("Inside").GetComponent<SpriteRenderer>().enabled,"Inside not visible");Require(!chest.transform.Find("Lock").GetComponent<SpriteRenderer>().enabled,"Lock remains visible");Require(Mathf.Abs(chest.transform.Find("Lid").localPosition.y-.62f)<.001f,"Lid position mismatch");}
   var ignore=new HashSet<Renderer>(staged.SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)));
   foreach(var r in Object.FindObjectsByType<Renderer>())if(!ignore.Contains(r))r.enabled=false;
   Capture(cam,"props-opened-existing-light.png");int count=Object.FindObjectsByType<GameObject>().Length;wood.Interact();iron.Interact();Require(Object.FindObjectsByType<GameObject>().Length==count,"Repeat interaction spawned duplicate rewards");
   var inputAfter=InputSystem.devices.Select(d=>new{d.deviceId,d.native,d.enabled}).ToArray();Require(JsonConvert.SerializeObject(inputBefore)==JsonConvert.SerializeObject(inputAfter),"Input changed");
   var lightAfter=JsonConvert.SerializeObject(lights.Select(l=>new{l.name,l.enabled,l.intensity,color=l.color.ToString(),l.pointLightOuterRadius,pos=l.transform.position.ToString()}));Require(lightBefore==lightAfter,"Original lighting mutated");
   var result=new{success=true,method="Original DungeonTest Play; existing lighting unchanged; actual InteractionSystem selection + Use on temporary null-id chests; no input injection",lit,dark,lightCount=lights.Length,woodOpened=wood.Opened,ironOpened=iron.Opened,noDuplicateRewards=true,inputBefore,inputAfter};Save("props-play-result.json",result);return result;
  }catch(Exception e){File.WriteAllText(Ev+"/props-error.txt",e.ToString());throw;}
  finally{
   foreach(var go in Object.FindObjectsByType<GameObject>())if(!objects.Contains(go))Object.DestroyImmediate(go);
   foreach(var s in scripts)if(s.Key)s.Key.enabled=s.Value;foreach(var r in renders)if(r.Key)r.Key.enabled=r.Value;
   typeof(InteractionSystem).GetProperty("Current",F).SetValue(interaction,current);typeof(InteractionSystem).GetField("_held",F).SetValue(interaction,held);typeof(InteractionSystem).GetField("_hurtSeen",F).SetValue(interaction,hurt);
   AudioListener.volume=volume;UnityEngine.Random.state=random;EditorApplication.isPaused=paused;Save("props-cleanup.json",new{restored=true,EditorApplication.isPaused,virtualDevices=InputSystem.devices.Count(d=>!d.native)});
  }
 }
}
