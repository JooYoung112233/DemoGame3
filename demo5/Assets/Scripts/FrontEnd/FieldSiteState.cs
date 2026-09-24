using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Demo5.FrontEnd
{
    // Provisional numbers for the site board (Inspector on ExpeditionSiteThreat).
    [Serializable] public sealed class FieldSiteRules
    {
        [Header("장소 시계 · 방문을 합쳐 세는 턴")]
        [Min(1)] public int TurnBudget = 24;
        [Tooltip("장소 시계가 이 턴 수를 지날 때마다 오래 머묾: 위험도 바닥 +1. 시계를 다 쓰면 바닥 3")]
        [Min(1)] public int LingerEvery = 8;
        [Range(0, 3)] public int LingerCap = 2;
        [Header("소음 → 위험도")]
        [Min(1)] public int GaugeSize = 4;
        [Tooltip("한 턴 소음이 이 값 이상이면 큰 소리: 그것이 그 방을 기억한다")]
        [Min(1)] public int LoudNoise = 3;
        [Tooltip("숨죽이기 한 턴이 게이지에서 빼는 값")]
        [Min(0)] public int HushRelief = 1;
        [Header("그것")]
        [Tooltip("목적지에 닿은 뒤 머무는 턴")]
        [Min(0)] public int StayTurns = 2;
        [Tooltip("교전에서 쫓아낸 뒤 판에서 사라지는 턴 (굴도 빔)")]
        [Min(0)] public int GoneTurns = 3;
    }

    public enum ResidentState { Den, Out, Staying, Gone }

    // What one listener heard at a door after a turn. Room: where the party stood; Turn: the site clock when heard.
    public readonly struct FieldDoorReport : IEquatable<FieldDoorReport>
    {
        public readonly int Door, Room, Turn, Next, Then; public readonly bool Present, Left; public readonly ResidentState Resident;
        public FieldDoorReport(int door, int room, int turn, int next, int then, bool present, bool left, ResidentState resident)
        { Door = door; Room = room; Turn = turn; Next = next; Then = then; Present = present; Left = left; Resident = resident; }
        // Its decided next step is into the party's room / the step after that is (if the party stays).
        public bool NextIn => Present && Next == Room;
        public bool ThenIn => Present && !NextIn && Then == Room;
        public bool Equals(FieldDoorReport o) => Door == o.Door && Room == o.Room && Turn == o.Turn && Next == o.Next && Then == o.Then && Present == o.Present && Left == o.Left && Resident == o.Resident;
        public override bool Equals(object o) => o is FieldDoorReport r && Equals(r);
        public override int GetHashCode() => (Door, Room, Turn, Next, Then, Present, Left, Resident).GetHashCode();
        public override string ToString() => $"door {Door} from {Room} t{Turn} present {Present} left {Left} {Resident} next {Next} then {Then}";
    }

    // The site as a small board of rooms: party position, time, noise and danger, and the one thing that lives here.
    // Its next step is always decided one turn ahead (the telegraph), exactly like a creature's marked strike in battle.
    public sealed class FieldSiteState
    {
        public const int Arcade = 0, Corridor = 1, Storage = 2, Den = 3, Nowhere = -1;
        public static readonly string[] RoomNames = { "오락실", "복도", "보관실", "관리실" };
        static readonly int[][] Links = { new[] { Corridor }, new[] { Arcade, Storage, Den }, new[] { Corridor }, new[] { Corridor } };

        public readonly FieldSiteRules Rules;
        public int PartyRoom { get; private set; }
        public int TurnsUsed { get; private set; }
        public int Gauge { get; private set; }
        public int Danger { get; private set; }
        public int Floor { get; private set; }
        public int Remembered { get; private set; } = Nowhere;
        public int ResidentRoom { get; private set; } = Den;
        public ResidentState Resident { get; private set; } = ResidentState.Den;
        public int Next { get; private set; } = Den;
        public bool Asleep { get; private set; }
        // What happened on the last turn, for the screen and the log.
        public bool Moved { get; private set; }
        public int MovedFrom { get; private set; } = Nowhere;
        public bool PassedBy { get; private set; }
        public bool Encounter { get; private set; }
        public bool Surprise { get; private set; }
        public int LastNoise { get; private set; }
        // This turn's noise made it remember the party's room.
        public bool Noted { get; private set; }
        // Met without either side moving: it was already in the room and heard the party.
        public bool Noticed { get; private set; }
        public int TurnsLeft => Math.Max(0, Rules.TurnBudget - TurnsUsed);
        public int NextLinger => TurnsUsed >= Rules.LingerEvery * Rules.LingerCap ? (TurnsUsed >= Rules.TurnBudget ? -1 : Rules.TurnBudget - TurnsUsed + 1) : Rules.LingerEvery - TurnsUsed % Rules.LingerEvery;
        public bool DenEmpty => ResidentRoom != Den;
        // The den stays empty through the next turn: it is out and not stepping home, or gone for more than one more turn.
        // The shelf is searched from inside the den, so a turn it comes home is not a gap.
        public bool DenStaysEmpty => DenEmpty && (Resident == ResidentState.Gone ? gone > 1 : Next != Den);
        int patrol = Nowhere, stay, gone; bool paused;
        readonly Func<int> roll;

        // carried: danger, gauge and memory left from an earlier visit (the site remembers).
        public FieldSiteState(FieldSiteRules rules, Func<int> random, bool asleep = false, int danger = 0, int gauge = 0, int remembered = Nowhere, int clock = 0)
        {
            Rules = rules ?? new FieldSiteRules(); roll = random ?? (() => 0); Asleep = asleep;
            Danger = Mathf.Clamp(danger, 0, 3); Gauge = Mathf.Clamp(gauge, 0, Rules.GaugeSize - 1); Remembered = remembered;
            TurnsUsed = Math.Max(0, clock); Floor = FloorAt(TurnsUsed); if (!Asleep) Danger = Math.Max(Danger, Floor);
            if (Asleep) Danger = Math.Min(Danger, 1);
            Next = Plan();
        }

        // Forecast copy: value fields only; Rules and roll are shared and never mutated. Keep it that way (no mutable reference fields).
        public FieldSiteState Copy() => (FieldSiteState)MemberwiseClone();

        public static bool Adjacent(int a, int b) => a >= 0 && b >= 0 && a < Links.Length && Links[a].Contains(b);
        // The room one step from 'from' toward 'to' (breadth-first over the four rooms).
        public static int StepToward(int from, int to)
        {
            if (from == to || from < 0 || to < 0) return from;
            var previous = new Dictionary<int, int> { { from, -1 } }; var queue = new Queue<int>(); queue.Enqueue(from);
            while (queue.Count > 0) { int r = queue.Dequeue(); foreach (int n in Links[r]) if (!previous.ContainsKey(n)) { previous[n] = r; queue.Enqueue(n); } }
            if (!previous.ContainsKey(to)) return from;
            int step = to; while (previous[step] != from) step = previous[step];
            return step;
        }

        // Where the party stands before the next turn resolves (a move commits here, then the turn ticks).
        public void MoveParty(int room) { if (room != PartyRoom) { partyFrom = PartyRoom; PartyRoom = room; } }
        int partyFrom = Nowhere;
        int FloorAt(int clock) => clock > Rules.TurnBudget ? 3 : Math.Min(Rules.LingerCap, clock / Rules.LingerEvery);

        // One party turn: its noise lands, time passes, then the thing makes the move it announced last turn.
        public void EndTurn(int noise, bool hushed)
        {
            Moved = PassedBy = Encounter = Surprise = Noted = Noticed = false; MovedFrom = Nowhere; LastNoise = Math.Max(0, noise);
            TurnsUsed++; int crossedFrom = partyFrom; partyFrom = Nowhere;
            AddNoise(LastNoise, PartyRoom);
            if (hushed) Gauge = Math.Max(0, Gauge - Rules.HushRelief);
            // 오래 머묾: the site clock raises the floor (danger never falls on site).
            Floor = FloorAt(TurnsUsed);
            Danger = Math.Min(Asleep ? 1 : 3, Math.Max(Danger, Floor));

            // The announced step.
            if (Resident == ResidentState.Gone)
            {
                if (--gone <= 0) { Resident = ResidentState.Den; ResidentRoom = Den; }
            }
            else if (Next != ResidentRoom)
            {
                MovedFrom = ResidentRoom; ResidentRoom = Next; Moved = true; paused = true;
                if (ResidentRoom == Den) { Resident = ResidentState.Den; patrol = Nowhere; }
                else Resident = ResidentState.Out;
            }
            else paused = false;

            if (Resident == ResidentState.Out && ResidentRoom == Target() && ResidentRoom != Den && Danger < 3)
            {
                // Arrived where the noise was: the memory is spent; it stays a while, then heads home.
                if (stay == 0) stay = Rules.StayTurns + 1;
                Resident = ResidentState.Staying; Remembered = Nowhere;
            }
            if (Resident == ResidentState.Staying && --stay <= 0) { stay = 0; Remembered = Nowhere; patrol = Den; Resident = ResidentState.Out; }

            // Meeting: it steps into the party's room, the party walked in on it, or both went through the same door opposite ways.
            bool crossed = Moved && crossedFrom >= 0 && MovedFrom == PartyRoom && ResidentRoom == crossedFrom;
            if (crossed && Resident != ResidentState.Gone) { Encounter = true; Surprise = false; ResidentRoom = PartyRoom; }
            else if (Resident != ResidentState.Gone && ResidentRoom == PartyRoom && ResidentRoom != Den)
            {
                if (hushed && Danger <= 2) PassedBy = true;
                else { Encounter = true; Surprise = !Moved && crossedFrom >= 0; Noticed = !Moved && crossedFrom < 0; }
            }
            Noted &= Remembered == PartyRoom;
            Next = Plan();
        }
        // A fight left without a win (retreat): its noise lands in the party's room without a turn passing; the next step is re-planned.
        public void Hear(int noise) { AddNoise(Math.Max(0, noise), PartyRoom); Next = Plan(); }
        void AddNoise(int amount, int room)
        {
            if (amount <= 0) return;
            Gauge += amount;
            while (Gauge >= Rules.GaugeSize) { Gauge -= Rules.GaugeSize; Danger = Math.Min(3, Danger + 1); }
            if (Asleep) Danger = Math.Min(Danger, 1);
            // Awake (위험도 1+) it remembers the latest loud room; asleep it does not.
            if (room >= 0 && amount >= Rules.LoudNoise && !Asleep && Danger >= 1) { Remembered = room; Noted = true; }
        }
        int Target()
        {
            if (Danger >= 3) return PartyRoom;
            if (patrol == Den) return Den;          // on its way home after staying
            if (Danger <= 1) return Den;
            // 위험도 2: only a remembered loud room draws it out; lingering alone does not empty the den.
            return Remembered >= 0 && Remembered != Den ? Remembered : Den;
        }
        // Next step, decided now and shown before the party acts again.
        int Plan()
        {
            if (Resident == ResidentState.Gone) return Nowhere;
            if (Asleep) return Den;
            if (Resident == ResidentState.Staying) return ResidentRoom;
            int target = Target();
            // At 위험도 2 it stops to listen after every step; when chasing it keeps coming.
            if (paused && Danger < 3 && ResidentRoom != Den) return ResidentRoom;
            return StepToward(ResidentRoom, target);
        }

        // Encounter outcomes.
        public void Hidden() { ResidentRoom = Den; Resident = ResidentState.Den; Remembered = Nowhere; patrol = Nowhere; stay = 0; paused = false; Next = Plan(); Encounter = false; }
        public void Driven() { ResidentRoom = Nowhere; Resident = ResidentState.Gone; gone = Rules.GoneTurns; Remembered = Nowhere; patrol = Nowhere; stay = 0; paused = false; Next = Plan(); Encounter = false; }

        // What the player can know: in the same room it is seen; a step into the party's room is always announced at the door.
        public bool Visible => Resident != ResidentState.Gone && ResidentRoom == PartyRoom && ResidentRoom != Den;
        public bool Incoming => Resident != ResidentState.Gone && Next == PartyRoom && ResidentRoom != PartyRoom;
        public bool Heard => Moved && (Adjacent(ResidentRoom, PartyRoom) || Adjacent(MovedFrom, PartyRoom)) && !Visible;
        // 문에 귀 대기: only the room behind the door. Present → its decided next step, and the step after if the party stays quiet
        // (a copy runs one silent turn). Absent → only whether it just left. Never changes this state.
        public FieldDoorReport ListenAt(int door)
        {
            bool near = Adjacent(PartyRoom, door);
            bool present = near && Resident != ResidentState.Gone && ResidentRoom == door;
            bool left = near && !present && Moved && MovedFrom == door;
            int next = present ? Next : Nowhere, then = Nowhere;
            if (present && Next != PartyRoom) { var q = Copy(); q.partyFrom = Nowhere; q.EndTurn(0, true); then = q.Next; }
            return new FieldDoorReport(door, PartyRoom, TurnsUsed, next, then, present, left, present ? Resident : ResidentState.Gone);
        }

        // One turn ahead, without changing anything: noise and danger after a planned action with this much noise.
        public (int gauge, int danger, bool loud) Preview(int noise, bool hushed)
        {
            // Same order as EndTurn: the noise lands (and may overflow), then hushing eases the gauge.
            int g = Gauge + Math.Max(0, noise), d = Danger;
            while (g >= Rules.GaugeSize) { g -= Rules.GaugeSize; d = Math.Min(3, d + 1); }
            if (hushed) g = Math.Max(0, g - Rules.HushRelief);
            if (!Asleep) d = Math.Max(d, FloorAt(TurnsUsed + 1));
            if (Asleep) d = Math.Min(d, 1);
            return (g, d, !Asleep && noise >= Rules.LoudNoise);
        }
    }
}
