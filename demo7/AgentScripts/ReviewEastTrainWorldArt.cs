using System.Threading.Tasks;
using EastTrain;
using UnityEngine;
public static class ReviewEastTrainWorldArt
{
    static EastTrainDemo Demo => Object.FindAnyObjectByType<EastTrainDemo>();
    static void Place(float x, float y)
    {
        var d=Demo; d.Driving=d.Repairing=d.Outside=false; d.Carry=0; d.PlayerX=x;d.PlayerY=y;d.ResetZoom();
        d.State=new TrainState { Distance=30,Fuel=40,Integrity=1,Brake=true,Throttle=.3f };
    }
    public static async Task<string> Drive()
    {
        Place(6.6f,.78f);var d=Demo;d.State.Brake=false;d.State.Throttle=.45f;d.Interact();
        await Task.Delay(4000);return "Travel review without persistent text.";
    }
    public static async Task<string> Interior()
    {
        Place(-2.9f,-1.65f);Demo.State.Vent=true;
        await Task.Delay(3000);return "Interior review after nearby hint expires.";
    }
    public static async Task<string> Repair()
    {
        Place(EastTrainDemo.RepairStationX,-1.65f);Demo.State.Integrity=.4f;Demo.State.Throttle=0;Demo.Interact();
        await Task.Delay(1500);return "Active repair review with temporary context hint.";
    }
}
