using System;
using System.Collections.Generic;
using EastTrain;
using UnityEngine;

public static class VerifyEastTrain
{
    static readonly List<string> passed = new List<string>();
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); passed.Add(name); }
    static void Advance(TrainState state, float seconds) { for (int i = 0; i < seconds * 50; i++) state.Tick(.02f); }

    public static string Rules()
    {
        passed.Clear();
        var s = new TrainState { Throttle = 1, Brake = false, Fuel = 30 };
        Advance(s, 2); Check(s.Distance == 0, "Broken engine cannot drive");
        Check(!s.Repair(.2f) && s.Integrity == 0, "Early timing fails without negative integrity");
        Check(s.Repair(.62f) && s.Repair(.82f) && s.Repair(.7f) && s.Integrity == 1, "Three successful timing checks repair engine");
        Advance(s, 2); Check(s.Speed > 0 && s.Fuel < 30, "Manual throttle drives and consumes fuel");
        s.Brake = true; Advance(s, 3); Check(s.Speed == 0, "Brake stops the train");
        var damaged = new TrainState { Fuel = 40, Integrity = .4f, Throttle = .5f, Brake = false };
        var healthy = new TrainState { Fuel = 40, Integrity = 1, Throttle = .5f, Brake = false };
        Advance(damaged, 5); Advance(healthy, 5);
        Check(damaged.Fuel < healthy.Fuel && damaged.Speed < healthy.Speed, "Hull damage reduces fuel efficiency and top speed");
        s = new TrainState { Heat = 99, Fuel = 30, Throttle = 1, Integrity = 1, Brake = false };
        Advance(s, 1); Check(s.Fire && s.Throttle == 0 && !s.LoadFuel(true), "Overheat ignites and cuts power; cannot refuel a fire");
        s.Extinguish(); Check(!s.Fire && s.Vent && s.Heat == 65, "Extinguisher recovers engine");
        Advance(s, 2); Check(s.Heat < 65, "Open cooling valve cools engine");
        s = new TrainState { Fuel = .001f, Speed = 4, Integrity = 1, Brake = false, Throttle = .5f };
        Advance(s, 5); Check(s.Fuel == 0 && s.Speed == 0, "Fuel exhaustion stops train");
        Check(s.LoadFuel(false), "Salvage accepted after fuel exhaustion"); Advance(s, 2);
        Check(s.Speed > 0, "Salvage allows departure again");
        s = new TrainState { Distance = 144, Speed = 6, Fuel = 50, Throttle = 1, Brake = false, Integrity = 1 };
        Advance(s, 1); Check(s.Distance == TrainState.SnowStop && s.Blocked && s.Speed == 0, "Snow barrier cannot be crossed by momentum");
        s.Shovel(5.1f); Check(s.Snow == 0, "Shovelling clears obstacle"); Advance(s, 2);
        Check(s.Distance > TrainState.SnowStop, "Cleared track permits travel");
        s = new TrainState { Distance = 219, Speed = 5, Fuel = 50, Throttle = .5f, Brake = false, Integrity = 1, Snow = 0 };
        Advance(s, 1); Check(s.PlateEvent && s.Integrity < .5f, "Route triggers loose plate event");
        s.Integrity = 1; Advance(s, 1); Check(s.Integrity == 1, "Repaired plate does not immediately fall again");
        s = new TrainState { Fuel = 0, Warmth = 1 }; Advance(s, 5);
        Check(s.WalkMultiplier < 1 && s.Warmth == 0, "No heating slows movement without a negative temperature");
        s = new TrainState { Fuel = 28, Integrity = 1, Brake = false, Throttle = .55f };
        int repairs = 0, refills = 0;
        for (int i = 0; i < 20000 && !s.Arrived; i++)
        {
            s.Tick(.02f);
            if (s.Blocked) s.Shovel(.02f);
            if (s.Heat > 80) s.Vent = true;
            if (s.Heat < 35) s.Vent = false;
            if (s.Fuel < 2) { s.LoadFuel(true); refills++; }
            if (s.Integrity < .4f) { s.Repair(.7f); s.Repair(.7f); repairs++; }
        }
        Check(s.Arrived && s.Distance == TrainState.Destination && s.Speed == 0 && repairs == 1 && refills > 0, "Entire prologue route can complete with refuelling, snow and plate repair");
        float end = s.Distance; Advance(s, 5); Check(s.Distance == end, "Arrival remains stopped");
        return string.Join("\n", passed) + "\nPASS " + passed.Count + " checks; route refills=" + refills;
    }

    public static string Interactions()
    {
        passed.Clear();
        var d = UnityEngine.Object.FindAnyObjectByType<EastTrainDemo>();
        if (d == null || !Application.isPlaying) throw new Exception("Enter Play mode first");
        d.State = new TrainState(); d.Outside = d.Driving = d.Repairing = false; d.Carry = 0;
        d.PlayerY = -1.65f; d.PlayerX = -7.5f; d.Interact();
        Check(d.Carry == 1 && d.State.Coal == 0 && d.State.Fuel == 0, "Take a physical fuel item without filling furnace remotely");
        d.Drop(); Check(d.Carry == 0 && d.State.Coal == 0, "Dropped item stays on floor rather than teleporting into stock");
        d.Interact(); Check(d.Carry == 1, "Dropped floor item can be picked up again");
        d.PlayerX = -5.2f; d.Interact(); Check(d.Carry == 0 && d.State.Fuel == 28, "Carry fuel to furnace and insert");
        d.PlayerX = EastTrainDemo.RepairStationX; d.Interact(); Check(d.Repairing, "Repair only starts at workbench");
        d.ResolveRepair(.72f); d.ResolveRepair(.72f); d.ResolveRepair(.72f);
        Check(!d.Repairing && d.State.Integrity == 1, "Timing successes finish repair interaction");
        d.PlayerX = 3.2f; for (int i = 0; i < 75; i++) d.MoveTraveller(0, 1, .02f);
        Check(d.PlayerY > .7f, "Ladder reaches elevated driving cabin");
        d.PlayerX = 6.6f; d.Interact(); Check(d.Driving, "Driver takes control at physical lever");
        d.Driving = false; d.PlayerX = 3.2f; for (int i = 0; i < 75; i++) d.MoveTraveller(0, -1, .02f);
        Check(d.PlayerY < -1.6f, "Ladder returns to engine room");
        d.PlayerX = 8.5f; d.State.Speed = 2; d.Interact(); Check(!d.Outside, "Cannot leave a moving train");
        d.State.Speed = 0; d.Interact(); Check(d.Outside && d.State.Brake, "Stopped train allows safe exit");
        d.PlayerX = 18; d.Interact(); Check(d.Carry == 2, "Pick up external salvage");
        d.PlayerX = d.State.Distance + 9.7f; d.Interact(); Check(!d.Outside && d.Carry == 2, "Reboard while carrying salvage");
        d.PlayerX = -5.2f; d.Interact(); Check(d.Carry == 0 && d.State.Fuel == 37, "External salvage burns in furnace");
        d.State.Fire = true; d.PlayerX = -1.35f; d.Interact(); Check(!d.State.Fire && d.State.Vent, "Physical cooling valve extinguishes engine");
        return string.Join("\n", passed) + "\nPASS " + passed.Count + " interaction checks";
    }
}
