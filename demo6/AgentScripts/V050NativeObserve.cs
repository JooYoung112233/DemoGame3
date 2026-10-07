using System;using System.IO;using System.Collections;using System.Collections.Generic;using System.Linq;using UnityEngine;using UnityEngine.InputSystem;using UnityEngine.InputSystem.LowLevel;using Demo6.Game;using Newtonsoft.Json;
public static class V050NativeObserve {
 public static object Start(){if(!Application.isPlaying)throw new Exception("Play required");var old=GameObject.Find("V050 input observation temporary");if(old)throw new Exception("Observer already active");var go=new GameObject("V050 input observation temporary");go.AddComponent<V050NativeObserver>();return "Observing native devices for 60 seconds. No injected input or device settings.";}
 public static object Stop(){var go=GameObject.Find("V050 input observation temporary");if(go){go.GetComponent<V050NativeObserver>().Finish();return "Saved and removed";}return "Already removed";}
}
public sealed class V050NativeObserver:MonoBehaviour {
 readonly List<object> samples=new List<object>();float until;string phase;bool recording;int events,downs,ups,frames;string Out="검증/v049-final";
 void Awake(){Directory.CreateDirectory(Out);until=Time.realtimeSinceStartup+60;InputSystem.onEvent+=OnEvent;}
 void OnEvent(InputEventPtr ev,InputDevice device){if(!(device is Mouse mouse)||!device.native)return;if(!ev.IsA<StateEvent>()&&!ev.IsA<DeltaStateEvent>())return;events++;if(mouse.rightButton.ReadValueFromEvent(ev,out float v)){if(v>.5f)downs++;else ups++;}}
 void LateUpdate(){var p=PlayerController.Instance;var a=TopDownView.PlayerRig?.FirstAttackArt;string now=p?p.ActPhase.ToString():"none";if(now!=phase){samples.Add(new{time=Time.time,frame=Time.frameCount,phase=now,clip=a?.ClipId,source="Passive observation; this script injects no input",rightHeld=Mouse.current?.rightButton.isPressed,nativeMouse=Mouse.current?.native,guard=p&&p.GuardActive});phase=now;if(now=="Raise"&&!recording){recording=true;StartCoroutine(CaptureTen());}}if(Time.realtimeSinceStartup>=until)Finish();}
 IEnumerator CaptureTen(){for(int i=0;i<10;i++){yield return new WaitForEndOfFrame();var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Out+"/native-guard-"+i.ToString("00")+".png",t.EncodeToPNG());Destroy(t);frames++;}}
 public void Finish(){File.WriteAllText(Out+"/native-observation.json",JsonConvert.SerializeObject(new{method="Passive Unity observation; no input injected by this observer",events,downs,ups,capturedFrames=frames,samples,devices=InputSystem.devices.Select(d=>new{d.name,d.native,d.enabled}).ToArray()},Formatting.Indented));DestroyImmediate(gameObject);}
 void OnDestroy(){InputSystem.onEvent-=OnEvent;}
}
