# Replay the actual PawnFacing.cs with minimal Unity API stubs; no Editor state changes.
$facingStubs = @'
using System;
namespace UnityEngine {
 public class Object { public static implicit operator bool(Object o)=>o!=null; }
 public class MonoBehaviour:Object { public Transform transform=new Transform(); public T GetComponent<T>() where T:class=>null; }
 public class Transform:Object { public Vector3 position; }
 public class SpriteRenderer:Object { public bool flipX; }
 public class MinAttribute:Attribute {public MinAttribute(float x){} }
 public class RangeAttribute:Attribute {public RangeAttribute(float x,float y){} }
 public struct Vector3 {public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} public float sqrMagnitude=>x*x+y*y+z*z; public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z); }
 public static class Mathf {public static float Abs(float v)=>Math.Abs(v);public static float Max(float a,float b)=>Math.Max(a,b);public static int RoundToInt(float v)=>(int)Math.Round(v);}
}
namespace UnityEngine.Rendering {public class SortingGroup:UnityEngine.Object {public int sortingOrder;}}
'@
$facingSource = Get-Content (Join-Path $PSScriptRoot '../Assets/Scripts/FrontEnd/PawnFacing.cs') -Raw
$facingSource = $facingSource.Replace('using UnityEngine;', '').Replace('using UnityEngine.Rendering;', '')
$facingTests = @'
public static class FacingReplay {
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);}
 public static string Run(){
  var f=new Demo5.FrontEnd.PawnFacing {Body=new UnityEngine.SpriteRenderer()};
  f.ResetTracking();
  for(int i=0;i<600;i++)f.ObservePosition(new UnityEngine.Vector3(i%2==0?-.006f:0,0,0));
  Check(!f.Body.flipX,"Idle jitter flipped");
  f.ResetTracking();
  for(int i=1;i<=20;i++)f.ObservePosition(new UnityEngine.Vector3(-i*.002f,i*.04f,0));
  Check(!f.Body.flipX,"Vertical drift flipped");
  foreach(int frames in new[]{15,30,60,144}){
   f.Face(true);
   for(int i=1;i<=frames;i++)f.ObservePosition(new UnityEngine.Vector3(-.3f*i/frames,0,0));
   Check(f.Body.flipX,"Left travel failed at "+frames);
   for(int i=1;i<=frames;i++)f.ObservePosition(new UnityEngine.Vector3(-.3f+.3f*i/frames,0,0));
   Check(!f.Body.flipX,"Right travel failed at "+frames);
  }
  f.Face(true);f.ObservePosition(new UnityEngine.Vector3(-.08f,0,0));
  f.ObservePosition(new UnityEngine.Vector3(-.08f,0,0));
  f.ObservePosition(new UnityEngine.Vector3(-.16f,0,0));
  Check(!f.Body.flipX,"Stopped fragments accumulated");
  f.Face(true);f.ObservePosition(new UnityEngine.Vector3(-3,0,0));
  Check(!f.Body.flipX,"Teleport flipped");
  f.ArtworkFacesRight=false;f.Face(false);
  Check(!f.Body.flipX,"Left facing artwork incorrect");
  f.ObservePosition(new UnityEngine.Vector3(.2f,0,0));
  Check(f.Body.flipX,"Mirrored artwork turn failed");
  f.Face(true);f.ObservePosition(new UnityEngine.Vector3(-.08f,0,0));f.ResetTracking();
  f.ObservePosition(new UnityEngine.Vector3(-.08f,0,0));
  Check(f.Body.flipX,"Reset kept stale travel");
  return "PASS: idle jitter, vertical drift, left/right at 15/30/60/144 samples, stops, teleport, artwork direction, reset. Actual source compiled with Unity API stubs; not an Editor integration test.";
 }
}
'@
Add-Type -TypeDefinition ("using System; using UnityEngine; using UnityEngine.Rendering;`n" + $facingStubs.Replace('using System;', '') + $facingSource + $facingTests)
[FacingReplay]::Run()
