using System;
using System.Reflection;
using EastTrain;
using UnityEngine;
using UnityEngine.UI;

public static class VerifyPrototypeHud
{
    public static string Run()
    {
        var d = UnityEngine.Object.FindAnyObjectByType<EastTrainDemo>();
        if (d == null || !Application.isPlaying) throw new Exception("Play mode required");
        int checks = 0;
        Action<bool,string> check = (ok,message) => { if (!ok) throw new Exception(message); checks++; };
        var camera = typeof(EastTrainDemo).GetMethod("UpdateCamera", BindingFlags.Instance | BindingFlags.NonPublic);
        var context = typeof(EastTrainDemo).GetMethod("UpdateContextPresentation", BindingFlags.Instance | BindingFlags.NonPublic);
        d.State = new TrainState(); d.Driving = d.Outside = d.Repairing = false;
        d.PlayerX = -7.5f; d.PlayerY = -1.65f; d.ResetZoom();
        d.AdjustZoom(-120); check(Mathf.Abs(d.InteriorZoom - 5.8f) < .01f, "Wheel notch must change zoom by two units");
        d.ResetZoom(); d.AdjustZoom(-1); check(Mathf.Abs(d.InteriorZoom - 5.8f) < .01f, "Normalized wheel ticks must match Windows ticks");
        Camera.main.orthographicSize = 3.8f;
        for (int i=0;i<10;i++) camera.Invoke(d,new object[]{.016f});
        check(Mathf.Abs(Camera.main.orthographicSize - 5.8f) < .05f, "Zoom has not settled within 160 ms");
        var anchorScreen=new Vector2(Camera.main.pixelWidth*.75f,Camera.main.pixelHeight*.4f);
        var anchorBefore=Camera.main.ScreenToWorldPoint(new Vector3(anchorScreen.x,anchorScreen.y,10));
        d.InteriorZoom=3.5f; d.FocusZoomAtScreenPoint(anchorScreen);
        for(int i=0;i<20;i++) camera.Invoke(d,new object[]{.016f});
        var anchorAfter=Camera.main.ScreenToWorldPoint(new Vector3(anchorScreen.x,anchorScreen.y,10));
        check(Vector2.Distance(anchorBefore,anchorAfter)<.01f,"Mouse anchor moved during zoom");
        float focusBefore=Camera.main.transform.position.x;d.PlayerX+=4;
        for(int i=0;i<20;i++) camera.Invoke(d,new object[]{.016f});
        check(Mathf.Abs(Camera.main.transform.position.x-focusBefore)<.02f,"Walking pulled cursor zoom back to player");
        d.ResetZoom(); d.InteriorZoom=14;
        for(int i=0;i<30;i++) camera.Invoke(d,new object[]{.016f});
        var rear=Camera.main.WorldToViewportPoint(new Vector3(-9.5f,0,0));
        var front=Camera.main.WorldToViewportPoint(new Vector3(10,0,0));
        check(rear.x>0 && front.x<1,"Wide stopped view must show whole train");
        d.Driving=true; d.State.Speed=3; d.ResetZoom();
        for(int i=0;i<30;i++) camera.Invoke(d,new object[]{.016f});
        check(Camera.main.WorldToViewportPoint(new Vector3(22,-2.4f,0)).x<1,"Journey zoom clips forward fuel");
        d.PrototypeUI=true; d.State.Fuel=53; d.State.Heat=76; d.Carry=2;
        context.Invoke(d,new object[]{100f});
        check(d.PrototypeHudVisible,"HUD must remain visible after hint timer expires");
        var values=GameObject.Find("Live train values").GetComponent<Text>().text;
        check(values.Contains("53/100") && values.Contains("76°") && values.Contains("땔감"),"Live resource values missing");
        check(GameObject.Find("Control mode").GetComponent<Text>().text=="운전 중","Driving mode not explicit");
        d.Driving=false; d.Repairing=true; context.Invoke(d,new object[]{.02f});
        check(GameObject.Find("Action guidance").GetComponent<Text>().text.Contains("SPACE"),"Repair timing help missing");
        d.Repairing=false; d.Carry=0; d.State=new TrainState(); d.ResetZoom(); context.Invoke(d,new object[]{.02f});
        context.Invoke(d,new object[]{.02f});
        check(d.FirstDepartureTitle.Contains("1/4"),"Initial fuel objective missing");
        d.Carry=1;context.Invoke(d,new object[]{.02f});check(d.FirstDepartureTitle.Contains("2/4"),"Carry-to-furnace objective missing");
        d.State.Fuel=28;d.Carry=0;context.Invoke(d,new object[]{.02f});check(d.FirstDepartureTitle.Contains("3/4"),"Repair objective missing");
        d.State.Integrity=1;context.Invoke(d,new object[]{.02f});check(d.FirstDepartureTitle.Contains("4/4"),"Driving objective missing");
        d.State=new TrainState();d.QuickStartPrototype();
        check(d.Driving && d.State.Fuel>0 && d.State.Integrity==1 && !d.State.Brake && d.State.Throttle>0,"Quick start remains blocked");
        d.State.Tick(1);check(d.State.Distance>0,"Quick start does not move");
        d.State=new TrainState();d.Driving=false;d.PlayerX=-7.4f;d.PlayerY=-1.65f;d.ResetZoom();
        context.Invoke(d,new object[]{.02f});
        return "PASS " + checks + " checks: fast cursor-anchored zoom, follow reset, full-train and forward view, persistent HUD, first-departure steps and quick start.";
    }
}
