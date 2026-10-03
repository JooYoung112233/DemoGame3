using System;
using System.Reflection;
using System.Threading.Tasks;
using EastTrain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class VerifyEastTrainCrew
{
    static int checks;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    static void Sim(EastTrainDemo d, float seconds) { for (int i=0; i<Mathf.CeilToInt(seconds/.02f); i++) d.StepCrew(.02f); }
    static void Reset(EastTrainDemo d)
    {
        for (int i=0;i<d.Crew.Count;i++)
        {
            var c=d.Crew[i]; c.Outside=false; c.Carry=0; d.OrderCrew(i,EastTrainDemo.CrewJob.Idle);
            c.X=-12+i*11; c.Stage=0; c.Target=null;
        }
        d.State=new TrainState(); d.Driving=false; d.Repairing=false;
    }
    public static string Rules()
    {
        var d=UnityEngine.Object.FindAnyObjectByType<EastTrainDemo>(); checks=0;
        Check(d != null && d.CrewMode && d.Crew.Count==3,"Three-crew mode missing");
        bool wasEnabled=d.enabled; d.enabled=false;
        try
        {
            Reset(d); d.OrderCrew(0,EastTrainDemo.CrewJob.Fuel); d.OrderCrew(1,EastTrainDemo.CrewJob.Repair); d.OrderCrew(2,EastTrainDemo.CrewJob.Drive);
            Sim(d,3);
            Check(d.Crew[0].Job==EastTrainDemo.CrewJob.Fuel && d.Crew[1].Job==EastTrainDemo.CrewJob.Repair && d.Crew[2].Job==EastTrainDemo.CrewJob.Drive,"Jobs did not run concurrently");
            Check(d.State.Coal==0 && d.State.Fuel>20,"Assigned stoker failed to carry and burn stock");
            Check(d.Repairing && d.Driving,"Repair and drive stations should both be occupied");
            Check(d.State.Distance==0,"Broken train should not move");
            for(int i=0;i<3;i++) { typeof(EastTrainDemo).GetField("repairTimer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(d,.8f); d.RepairPhase=.7f; d.CrewRepairPress(); }
            Check(d.State.Integrity>=.99f && !d.Repairing,"Three timing successes did not repair");
            Sim(d,2); Check(d.State.Distance>0 && d.State.Fuel<28,"Crew-driven train did not consume fuel/move");
            d.OrderCrew(2,EastTrainDemo.CrewJob.Idle); Sim(d,2);
            Check(d.State.Brake && d.State.Speed==0,"Train must brake when driver leaves");
            Reset(d); d.OrderCrew(0,EastTrainDemo.CrewJob.Drive); d.OrderCrew(1,EastTrainDemo.CrewJob.Drive);
            Check(d.Crew[0].Job==EastTrainDemo.CrewJob.Idle,"Exclusive driving station allowed two owners");
            Reset(d); d.State.Coal=0;
            var pickup=GameObject.Find("Burnable salvage"); Check(pickup!=null,"World pickup missing");
            d.OrderCrew(2,EastTrainDemo.CrewJob.Collect,pickup.transform.position.x,pickup.transform); Sim(d,18);
            Check(d.Crew[2].Job==EastTrainDemo.CrewJob.Idle && !d.Crew[2].Outside && d.Crew[2].Carry==0,"Collector failed round trip");
            bool stillRegistered=false;
            foreach(var entry in (System.Collections.IEnumerable)typeof(EastTrainDemo).GetField("pickups",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(d))
                if ((Transform)entry.GetType().GetField("Root").GetValue(entry)==pickup.transform) stillRegistered=true;
            Check(d.State.Scrap==1 && !stillRegistered,"Collection did not consume exactly one world pickup and deposit it");
            d.OrderCrew(0,EastTrainDemo.CrewJob.Fuel); Sim(d,6);
            Check(d.State.Scrap==0 && d.State.Fuel>5 && d.State.Fuel<10,"Salvage fuel value changed");
            Reset(d); d.State.Coal=0; d.OrderCrew(2,EastTrainDemo.CrewJob.Collect,26); Sim(d,20);
            Check(d.State.Scrap==1 && !d.Crew[2].Outside,"Emergency tinder fallback failed");
            Reset(d); d.State.Integrity=1; d.State.Fuel=30; d.OrderCrew(2,EastTrainDemo.CrewJob.Drive); Sim(d,3);
            d.OrderCrew(1,EastTrainDemo.CrewJob.Collect,d.State.Distance+23); Sim(d,2);
            Check(d.State.Brake && d.State.Speed==0,"Collection failed to stop train before exit");
            d.OrderCrew(1,EastTrainDemo.CrewJob.Idle); Sim(d,12);
            Check(!d.Crew[1].Outside && d.Crew[1].Job==EastTrainDemo.CrewJob.Idle,"Cancel outside failed safe return");
            Reset(d); d.State.Distance=TrainState.SnowStop; d.OrderCrew(2,EastTrainDemo.CrewJob.Shovel); Sim(d,20);
            Check(d.State.Snow<=0 && !d.Crew[2].Outside,"Shovel and return failed");
            Reset(d); d.State.Heat=105; d.State.Fire=true; d.State.Fuel=20; d.OrderCrew(1,EastTrainDemo.CrewJob.Cool); Sim(d,3);
            Check(!d.State.Fire && d.State.Heat<70,"Engineer did not extinguish fire");
            Reset(d); d.State.Fuel=0; d.State.Integrity=1; d.OrderCrew(2,EastTrainDemo.CrewJob.Drive); Sim(d,3);
            Check(d.State.Distance==0,"Fuel-less train moved");
            Reset(d); d.State.Distance=219.9f; d.State.Integrity=1; d.State.Fuel=50; d.State.Snow=0; d.OrderCrew(2,EastTrainDemo.CrewJob.Drive); Sim(d,3);
            Check(d.State.PlateEvent && d.State.Integrity<.5f,"Plate loss event regressed");
            d.State.Distance=TrainState.Destination-.1f; d.State.Integrity=1; Sim(d,2);
            Check(d.State.Arrived && d.State.Speed==0,"Prologue arrival regressed");
            return "PASS: " + checks + " crew simulation checks (concurrency, physical fuel route, timing repair, safe collection/return, exclusivity, emergency salvage, snow, fire, plate loss, arrival).";
        }
        finally { Reset(d); d.enabled=wasEnabled; }
    }
    static async Task KeyTap(Keyboard k, Key key)
    {
        InputSystem.QueueStateEvent(k,new KeyboardState(key)); await Task.Delay(80);
        InputSystem.QueueStateEvent(k,new KeyboardState()); await Task.Delay(80);
    }
    public static async Task<string> InputAndCamera()
    {
        var d=UnityEngine.Object.FindAnyObjectByType<EastTrainDemo>(); checks=0; Reset(d);
        var oldK=Keyboard.current; var oldM=Mouse.current;
        var k=InputSystem.AddDevice<Keyboard>(); var m=InputSystem.AddDevice<Mouse>(); k.MakeCurrent();m.MakeCurrent();
        try
        {
            await KeyTap(k,Key.Digit2); Check(d.SelectedCrew==1,"Number selection failed");
            var cam=Camera.main; var position=cam.WorldToScreenPoint(d.Crew[2].Body.position+Vector3.up*.7f);
            InputSystem.QueueStateEvent(m,new MouseState{position=position}.WithButton(MouseButton.Left)); await Task.Delay(100);
            InputSystem.QueueStateEvent(m,new MouseState{position=position}); await Task.Delay(100);
            Check(d.SelectedCrew==2,"Mouse crew selection failed");
            position=cam.WorldToScreenPoint(new Vector3(d.State.Distance+13.5f,-.6f,0));
            InputSystem.QueueStateEvent(m,new MouseState{position=position}.WithButton(MouseButton.Right)); await Task.Delay(100);
            InputSystem.QueueStateEvent(m,new MouseState{position=position}); await Task.Delay(800);
            Check(d.Crew[2].Job==EastTrainDemo.CrewJob.Drive && d.Driving,"Right click order failed");
            d.State.Brake=true; await KeyTap(k,Key.Space); Check(!d.State.Brake,"Space brake input failed");
            await KeyTap(k,Key.Escape); Check(d.Crew[2].Job==EastTrainDemo.CrewJob.Idle,"Escape cancel failed");
            d.CrewZoom=d.OverviewSize; await Task.Delay(250);
            float before=cam.orthographicSize;
            InputSystem.QueueStateEvent(m,new MouseState{scroll=new Vector2(0,120),position=position}); await Task.Delay(220);
            InputSystem.QueueStateEvent(m,new MouseState{position=position});
            Check(before-d.CrewZoom>1.9f,"Wheel step too small");
            Check(Mathf.Abs(cam.orthographicSize-d.CrewZoom)<.15f,"Zoom response slower than 220 ms");
            d.CrewZoom=d.OverviewSize; await Task.Delay(250);
            var rear=cam.WorldToViewportPoint(new Vector3(d.State.Distance-15.9f,0,0));
            var front=cam.WorldToViewportPoint(new Vector3(d.State.Distance+18,0,0));
            Check(rear.x>.02f && front.x<.98f,"Overview clips rear or forward fuel area");
            d.State.Fuel=40;d.State.Integrity=1;d.OrderCrew(2,EastTrainDemo.CrewJob.Drive);await Task.Delay(900);
            d.CrewZoom=3.2f;await Task.Delay(500);
            Check(cam.orthographicSize>=d.OverviewSize-.1f,"Moving train lost overview");
            d.State.Brake=true;d.OrderCrew(2,EastTrainDemo.CrewJob.Idle);await Task.Delay(7000);
            Check(!d.CrewHintVisible,"Permanent instruction text remains visible");
            InputSystem.QueueStateEvent(k,new KeyboardState(Key.Tab));await Task.Delay(100);
            Check(d.CrewHintVisible,"Tab help failed");
            InputSystem.QueueStateEvent(k,new KeyboardState());await Task.Delay(100);
            Check(!d.CrewHintVisible,"Tab release did not hide help");
            return "PASS: " + checks + " actual input/camera checks.";
        }
        finally { InputSystem.RemoveDevice(k);InputSystem.RemoveDevice(m);if(oldK!=null&&oldK.added)oldK.MakeCurrent();if(oldM!=null&&oldM.added)oldM.MakeCurrent();Reset(d); }
    }
}
