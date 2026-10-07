using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    // Cosmetic, scene-owned particle pool. Uses a private sequence, never gameplay RNG.
    public sealed class ImportedFxV056 : MonoBehaviour
    {
        sealed class Entry { public GameObject go; public ParticleSystem[] systems; public int kind; public float age; }
        static ImportedFxV056 instance;
        static ImportedFxV056Catalog catalog;
        readonly List<Entry> entries = new List<Entry>();
        uint seed = 4936;
        public static int SpawnCount { get; private set; }
        public static int EmittedCount { get; private set; }
        public int ActiveCount { get { int n=0; foreach(var e in entries)if(e.go.activeSelf)n++;return n; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics(){instance=null;catalog=null;SpawnCount=0;EmittedCount=0;}
        static bool Ready()
        {
            if(!catalog)catalog=Resources.Load<ImportedFxV056Catalog>("ImportedFxV056/catalog");
            if(!catalog||!catalog.hitSparks||!catalog.groundDust||!catalog.stoneDebris)return false;
            if(!instance)instance=new GameObject("Imported cosmetic FX V056").AddComponent<ImportedFxV056>();
            return true;
        }
        public static bool Blocked(Vector2 at,Vector2 outward,int count)
        {
            if(!Ready())return false;
            instance.Emit(0,at,outward,Mathf.Clamp(count,1,8));return true;
        }
        public static bool WallDust(Vector2 at,Vector2 normal,int count)
        {
            if(count<=0||!Ready())return false;
            int stone=count/2,dust=count-stone;
            if(dust>0)instance.Emit(1,at,normal,dust);
            if(stone>0)instance.Emit(2,at,normal,stone);
            return true;
        }
        float Next(){seed=seed*1664525u+1013904223u;return (seed>>8)*(1f/16777216f);}
        Entry Take(int kind)
        {
            foreach(var e in entries)if(e.kind==kind&&!e.go.activeSelf)return e;
            int count=0;Entry oldest=null;
            foreach(var e in entries)if(e.kind==kind){count++;if(oldest==null||e.age>oldest.age)oldest=e;}
            if(count>=8){oldest.go.SetActive(false);return oldest;}
            // Instantiate under an inactive owner so playOnAwake cannot emit before setup.
            var staging=new GameObject("FX setup");staging.SetActive(false);
            var prefab=kind==0?catalog.hitSparks:kind==1?catalog.groundDust:catalog.stoneDebris;
            var go=Instantiate(prefab,staging.transform);go.SetActive(false);go.transform.SetParent(transform,false);Destroy(staging);
            var entry=new Entry{go=go,kind=kind,systems=go.GetComponentsInChildren<ParticleSystem>(true)};
            foreach(var ps in entry.systems)
            {
                var m=ps.main;m.playOnAwake=false;m.loop=false;m.useUnscaledTime=false;m.simulationSpace=ParticleSystemSimulationSpace.World;m.scalingMode=ParticleSystemScalingMode.Shape;m.gravityModifier=0;m.stopAction=ParticleSystemStopAction.None;
                var em=ps.emission;em.enabled=false;var shape=ps.shape;shape.enabled=false;
                var velocity=ps.velocityOverLifetime;velocity.enabled=false;var force=ps.forceOverLifetime;force.enabled=false;var noise=ps.noise;noise.enabled=false;
                var collision=ps.collision;collision.enabled=false;var lights=ps.lights;lights.enabled=false;var trails=ps.trails;trails.enabled=false;
                ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(4936+kind);
                ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            entries.Add(entry);return entry;
        }
        void Emit(int kind,Vector2 at,Vector2 normal,int count)
        {
            normal=normal.sqrMagnitude>.0001f?normal.normalized:Vector2.up;
            var e=Take(kind);e.age=0;e.go.transform.position=new Vector3(at.x,at.y,0);e.go.transform.rotation=Quaternion.identity;
            foreach(var ps in e.systems){ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var r=ps.GetComponent<ParticleSystemRenderer>();r.sortingLayerID=0;r.sortingOrder=kind==0?1008-Mathf.RoundToInt(at.y*20):-55;}
            e.go.SetActive(true);foreach(var ps in e.systems)ps.Play(false);
            var tangent=new Vector2(-normal.y,normal.x);
            for(int i=0;i<count;i++)
            {
                float spread=(count>1?Mathf.Lerp(-1.12f,1.12f,i/(float)(count-1)):0)+(Next()-.5f)*.12f;
                var velocity=(normal*Mathf.Cos(spread)+tangent*Mathf.Sin(spread))*(kind==0?1.5f:kind==1?.7f:1.25f);
                var pos=at+normal*(.05f+Next()*.025f)+tangent*((Next()-.5f)*.08f);
                var p=new ParticleSystem.EmitParams{position=new Vector3(pos.x,pos.y,0),velocity=new Vector3(velocity.x,velocity.y,0),startLifetime=kind==0?.12f+Next()*.08f:.22f+Next()*.10f,startSize=kind==0?.10f+Next()*.04f:kind==1?.28f+Next()*.12f:.09f+Next()*.07f,rotation=Next()*360};
                e.systems[i%e.systems.Length].Emit(p,1);EmittedCount++;
            }
            SpawnCount++;
        }
        void Update(){foreach(var e in entries)if(e.go.activeSelf){e.age+=Time.deltaTime;if(e.age>.55f){foreach(var ps in e.systems)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);e.go.SetActive(false);}}}
        void OnDestroy(){if(instance==this)instance=null;}
    }
}
