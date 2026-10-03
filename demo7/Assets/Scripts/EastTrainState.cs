using System;

namespace EastTrain
{
    // Deterministic simulation, independent of rendering and input.
    [Serializable]
    public sealed class TrainState
    {
        public const float SnowStop = 145f, Destination = 345f;
        public float Distance, Speed, Fuel, Heat = 18f, Warmth = 70f, Throttle;
        public float Integrity, Snow = 1f;
        public bool Brake = true, Vent, Fire, Arrived, PlateEvent;
        public int Coal = 1, Scrap;
        public bool Blocked => Snow > 0 && Distance >= SnowStop - .01f;
        public float WalkMultiplier => Warmth < 25 ? .85f : 1f;
        public bool EngineRunning => Fuel > 0 && Integrity > .05f && !Fire;

        public void Tick(float dt)
        {
            if (dt <= 0 || Arrived) return;
            bool burning = Fuel > 0;
            float power = burning && !Fire && Integrity > .05f ? Throttle : 0;
            if (burning) Fuel = Math.Max(0, Fuel - dt * (.07f + power * .46f) * (1 + (1 - Integrity) * .7f));
            Heat = Clamp(Heat + dt * (Fire ? 5 : power * 6 - 2.8f - (Vent ? 5 : 0)), 0, 110);
            if (Heat >= 100) { Fire = true; Throttle = 0; }
            Warmth = Clamp(Warmth + dt * (burning && !Vent ? .7f : -.7f), 0, 100);
            float target = Brake || Blocked || Fire || Fuel <= 0 ? 0 : power * 7 * (.4f + .6f * Integrity) * (Vent ? .55f : 1);
            Speed = Move(Speed, target, dt * (Brake || Blocked ? 4 : 1.3f));
            float next = Distance + Speed * dt;
            if (Snow > 0 && next >= SnowStop) { next = SnowStop; Speed = 0; }
            Distance = Math.Min(Destination, next);
            if (!PlateEvent && Distance >= 220) { PlateEvent = true; Integrity = Math.Max(0, Integrity - .65f); }
            if (Distance >= Destination) { Arrived = true; Speed = 0; Throttle = 0; Brake = true; }
        }

        public bool Feed()
        {
            if (Fuel > 85 || Fire) return false;
            if (Coal > 0) { Coal--; return LoadFuel(true); }
            if (Scrap > 0) { Scrap--; return LoadFuel(false); }
            return false;
        }
        public bool LoadFuel(bool coal)
        {
            if (Fuel > 85 || Fire) return false;
            Fuel = Math.Min(100, Fuel + (coal ? 28 : 9)); return true;
        }
        public bool Repair(float phase)
        {
            bool success = phase >= .62f && phase <= .82f;
            Integrity = Clamp(Integrity + (success ? .34f : -.08f), 0, 1);
            return success;
        }
        public bool Shovel(float dt)
        {
            if (!Blocked || Speed > .1f) return false;
            Snow = Math.Max(0, Snow - dt / 5f);
            return true;
        }
        public void Extinguish() { Fire = false; Heat = 65; Vent = true; Throttle = 0; }
        static float Clamp(float x, float a, float b) => Math.Max(a, Math.Min(b, x));
        static float Move(float a, float b, float step) => a < b ? Math.Min(a + step, b) : Math.Max(a - step, b);
    }
}
