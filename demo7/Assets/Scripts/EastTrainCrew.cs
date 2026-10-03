using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EastTrain
{
    // Crew orders own movement and work; the original fuel/heat/damage simulation is shared.
    public sealed partial class EastTrainDemo
    {
        public enum CrewJob { Idle, Move, Drive, Fuel, Cool, Repair, Collect, Shovel, Return }
        [Serializable] public sealed class CrewMember
        {
            public float X, Destination, WorkTime;
            public bool Outside;
            public int Carry, Stage;
            public CrewJob Job, AfterReturn;
            [NonSerialized] public Transform Body, Cargo, Target;
            [NonSerialized] public SpriteRenderer Image;
        }
        public readonly List<CrewMember> Crew = new List<CrewMember>();
        public int SelectedCrew { get; private set; }
        public float CrewZoom = 10.8f;
        public bool CrewMode => crewMode;
        bool crewMode;
        EastTrainWorldArtSet crewArt;
        Transform crewTrain, selectionMark, crewHintRoot, crewFire, crewSteam, crewLamp, crewDamage;
        readonly List<Transform> crewSmoke = new List<Transform>();
        TextMesh crewHint;
        float crewHintTime, crewPan;
        string crewMessage;
        int repairingCrew = -1;
        const float CrewFloor = -1.65f, CrewDoor = 15.5f, CrewStove = -10.7f, CrewStock = -13.5f, CrewBench = 3.3f, CrewDriver = 13.5f;
        public float OverviewSize => Mathf.Max(7.6f, 19.5f / Mathf.Max(.7f, cam.aspect));
        public bool CrewHintVisible => crewHintRoot != null && crewHintRoot.gameObject.activeSelf;

        Transform CrewArt(Transform parent, string key, float x, float y, float w, float h, int order, bool fit = false)
        {
            var part = crewArt.Find(key);
            if (part.Texture == null) return null;
            var sprite = Sprite.Create(part.Texture, part.Pixels, Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            var t = Root(key, parent); t.localPosition = new Vector3(x, y, 0);
            var r = t.gameObject.AddComponent<SpriteRenderer>(); r.sprite = sprite; r.sortingOrder = order;
            t.localScale = fit ? Vector3.one * Mathf.Min(w / sprite.bounds.size.x, h / sprite.bounds.size.y)
                : new Vector3(w / sprite.bounds.size.x, h / sprite.bounds.size.y, 1);
            return t;
        }
        void BuildCrewMode()
        {
            crewArt = Resources.Load<EastTrainWorldArtSet>("EastTrainCrewArtSet");
            if (crewArt == null) return;
            crewMode = true; train.gameObject.SetActive(false); person.gameObject.SetActive(false);
            hintBoard.gameObject.SetActive(false);
            crewTrain = Root("Three connected single-level carriages", world);
            CrewArt(crewTrain, "warm-workshop", -10.5f, -.7f, 10.7f, 4.2f, 10);
            CrewArt(crewTrain, "warm-engine", 0, -.7f, 10.7f, 4.2f, 10);
            CrewArt(crewTrain, "warm-cab", 10.5f, -.7f, 10.7f, 4.2f, 10);
            for (int i = 0; i < 2; i++) Box("Walk-through gangway", crewTrain, -5.25f + i * 10.5f, -1.63f, .65f, .12f, "#947151", 11);
            var newHorizon = crewArt.Find("warm-snow");
            if (newHorizon.Texture != null)
            {
                horizonArt.gameObject.SetActive(false);
                horizonArt = Root("Soft painted snow panorama", world);
                for (int i = -1; i <= 1; i++) CrewArt(horizonArt, "warm-snow", i * 84, 4, 84.05f, 28, -95);
            }
            crewFire = Art(crewTrain, "Live furnace flame", "fire", CrewStove, -.62f, .4f, .36f, 21);
            crewSteam = Art(crewTrain, "Live cooling steam", "smoke", .4f, .6f, 1.2f, 1.6f, 24);
            crewLamp = Art(crewTrain, "Driving headlight", "glow", 16, -.4f, 3.8f, 2.3f, 25);
            crewDamage = Art(crewTrain, "Loose plate", "metal-strip", 2.5f, -1.55f, 1.1f, .22f, 30);
            for (int i = 0; i < 12; i++) crewSmoke.Add(Art(crewTrain, "Chimney smoke", "smoke", 0, 0, 1, 1, 25));
            timingTool.SetParent(crewTrain, false); timingTool.localPosition = new Vector3(CrewBench, .8f, 0);
            foreach (var r in timingTool.GetComponentsInChildren<Renderer>(true)) r.sortingOrder += 40;
            foreach (var bolt in bolts) { bolt.SetParent(crewTrain, false); bolt.localPosition = new Vector3(CrewBench - .3f + bolts.IndexOf(bolt) * .3f, .08f, 0); }
            for (int i = 0; i < 3; i++)
            {
                var c = new CrewMember { X = -12 + i * 11, Job = CrewJob.Idle };
                c.Body = Root("Crew " + (i + 1), world);
                var art = CrewArt(c.Body, "crew-" + (i + 1), 0, .71f, 1.1f, 1.42f, 45, true);
                c.Image = art.GetComponent<SpriteRenderer>();
                c.Cargo = Art(c.Body, "Carried fuel", "logs", .38f, .65f, .65f, .45f, 48, true);
                Crew.Add(c);
            }
            selectionMark = Shape("Selected crew footlight", world, 0, 0, .8f, .09f, C("#FFE8A5"), 44, true);
            crewHintRoot = Root("Temporary crew instruction", world);
            crewHint = Words("", crewHintRoot, 0, 0, .22f, "#FFEFCC", 80);
            Box("Hint backing", crewHintRoot, 0, 0, 12, .55f, "#293E4D", 79);
            snowBank.position = new Vector3(TrainState.SnowStop + 18, 0, 0);
            CrewZoom = OverviewSize; cam.orthographicSize = CrewZoom;
            cam.transform.position = new Vector3(1, .4f, -10);
            CrewNotice("사람 클릭 → 설비·장작 우클릭으로 지시 · 휠 확대 · TAB 도움말", 7);
            UpdateCrewPresentation(0);
        }
        public void SelectCrew(int index)
        {
            SelectedCrew = Mathf.Clamp(index, 0, Crew.Count - 1); crewPan = 0;
            CrewNotice("승무원 " + (SelectedCrew + 1) + " · " + JobName(Crew[SelectedCrew].Job) + " · 우클릭으로 지시");
        }
        static string JobName(CrewJob job)
        {
            switch (job) { case CrewJob.Drive:return "운전"; case CrewJob.Fuel:return "연료 보충"; case CrewJob.Cool:return "기관 냉각";
                case CrewJob.Repair:return "수리"; case CrewJob.Collect:return "연료 수집"; case CrewJob.Shovel:return "제설";
                case CrewJob.Return:return "열차 복귀"; case CrewJob.Move:return "이동"; default:return "대기"; }
        }
        void CrewNotice(string message, float seconds = 2.8f) { crewMessage = message; crewHintTime = seconds; LastAction = message; }
        public void SetCrewZoom(float wheel)
        {
            if (Mathf.Abs(wheel) < .01f) return;
            float steps = Mathf.Abs(wheel) < 10 ? wheel : wheel / 120;
            CrewZoom = Mathf.Clamp(CrewZoom - steps * 2, 3.2f, Mathf.Max(16, OverviewSize));
            crewPan = 0;
        }
        public void OrderCrew(int index, CrewJob job, float target = 0, Transform pickup = null)
        {
            if (index < 0 || index >= Crew.Count || State.Arrived) return;
            var c = Crew[index];
            if (repairingCrew == index) { Repairing = false; repairingCrew = -1; }
            // A station has one worker. Reassignment releases the previous worker cleanly.
            if (job == CrewJob.Drive || job == CrewJob.Fuel || job == CrewJob.Cool || job == CrewJob.Repair)
                for (int i = 0; i < Crew.Count; i++) if (i != index && (Crew[i].Job == job || Crew[i].AfterReturn == job))
                { Crew[i].Job = Crew[i].Outside ? CrewJob.Return : CrewJob.Idle; Crew[i].AfterReturn = CrewJob.Idle;
                  if (repairingCrew == i) { Repairing = false; repairingCrew = -1; } }
            c.WorkTime = 0; c.Stage = 0; c.Target = pickup; c.Destination = target;
            if (job == CrewJob.Repair) c.Destination = target < -5 ? -7.5f : CrewBench;
            if (c.Outside) { c.Job = CrewJob.Return; c.AfterReturn = job; }
            else { c.Job = job; c.AfterReturn = CrewJob.Idle; }
            if (job == CrewJob.Collect || job == CrewJob.Shovel || c.Outside) State.Brake = true;
            if (job == CrewJob.Drive) CrewZoom = Mathf.Max(CrewZoom, OverviewSize);
            CrewNotice("승무원 " + (index + 1) + " → " + JobName(job)); sound.Cue();
        }
        void CrewClick(Vector2 point, bool select)
        {
            if (select)
            {
                float best = 1.4f; int found = -1;
                for (int i = 0; i < Crew.Count; i++)
                {
                    float distance = Vector2.Distance(point, Crew[i].Body.position + Vector3.up * .7f);
                    if (distance < best) { best = distance; found = i; }
                }
                if (found >= 0) SelectCrew(found);
                return;
            }
            float x = point.x - State.Distance;
            if (x >= -15.7f && x <= 15.7f && point.y > -2.15f && point.y < 2)
            {
                CrewJob job = x < -8 ? CrewJob.Fuel : x < -5.2f ? CrewJob.Repair : x < 1.4f ? CrewJob.Cool : x < 5.25f ? CrewJob.Repair : x < 11.5f ? CrewJob.Move : CrewJob.Drive;
                OrderCrew(SelectedCrew, job, Mathf.Clamp(x, -15, 15)); return;
            }
            if (State.Blocked && Mathf.Abs(point.x - snowBank.position.x) < 3)
            { OrderCrew(SelectedCrew, CrewJob.Shovel, snowBank.position.x); return; }
            foreach (var pickup in pickups)
                if (!pickup.Inside && pickup.Root != null && Vector2.Distance(point, pickup.Root.position) < 1.7f)
                {
                    bool claimed = Crew.Exists(c => c.Target == pickup.Root && (c.Job == CrewJob.Collect || c.AfterReturn == CrewJob.Collect));
                    if (claimed) { CrewNotice("다른 승무원이 수집하러 가고 있다"); return; }
                    OrderCrew(SelectedCrew, CrewJob.Collect, pickup.Root.position.x, pickup.Root); return;
                }
            if (point.y < -1.5f) OrderCrew(SelectedCrew, CrewJob.Collect, Mathf.Clamp(point.x, State.Distance - 32, State.Distance + 40));
        }
        void UpdateCrewMode(float dt)
        {
            var key = Keyboard.current; var mouse = Mouse.current;
            if (mouse != null)
            {
                SetCrewZoom(mouse.scroll.ReadValue().y);
                if (mouse.middleButton.wasPressedThisFrame) { CrewZoom = OverviewSize; crewPan = 0; }
                Vector2 point = cam.ScreenToWorldPoint(mouse.position.ReadValue());
                if (mouse.leftButton.wasPressedThisFrame) CrewClick(point, true);
                if (mouse.rightButton.wasPressedThisFrame) CrewClick(point, false);
            }
            if (key != null)
            {
                if (key.digit1Key.wasPressedThisFrame) SelectCrew(0);
                if (key.digit2Key.wasPressedThisFrame) SelectCrew(1);
                if (key.digit3Key.wasPressedThisFrame) SelectCrew(2);
                if (key.escapeKey.wasPressedThisFrame) OrderCrew(SelectedCrew, CrewJob.Idle);
                if (key.rKey.wasPressedThisFrame && (key.leftCtrlKey.isPressed || key.rightCtrlKey.isPressed))
                { UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); return; }
                if (key.spaceKey.wasPressedThisFrame)
                {
                    if (Repairing && repairTimer >= 0) CrewRepairPress();
                    else if (!Repairing) { State.Brake = !State.Brake; CrewNotice(State.Brake ? "열차 제동" : "제동 해제"); }
                }
                if (Driving) State.Throttle = Mathf.Clamp01(State.Throttle + ((key.wKey.isPressed ? 1 : 0) - (key.sKey.isPressed ? 1 : 0)) * dt * .5f);
                crewPan = Mathf.Clamp(crewPan + ((key.dKey.isPressed ? 1 : 0) - (key.aKey.isPressed ? 1 : 0)) * dt * 12, -25, 25);
            }
            StepCrew(dt); UpdateCrewPresentation(dt);
        }
        public void CrewRepairPress()
        {
            if (!Repairing || repairTimer < 0) return;
            bool ok = State.Repair(RepairPhase); sound.Hit(ok); repairTimer = -.65f; RepairPhase = 0;
            CrewNotice(ok ? "수리 성공" : "타이밍 실패 · 초록 구간에서 SPACE");
            if (State.Integrity >= .99f) FinishCrewRepair();
        }
        void FinishCrewRepair()
        {
            if (repairingCrew >= 0) Crew[repairingCrew].Job = CrewJob.Idle;
            repairingCrew = -1; Repairing = false;
        }
        bool CrewWalk(CrewMember c, float target, float dt)
        {
            float before = c.X; c.X = Mathf.MoveTowards(c.X, target, dt * 6.5f * State.WalkMultiplier);
            float delta = c.X - before;
            if (Mathf.Abs(delta) > .001f) c.Image.flipX = delta < 0;
            c.Body.localRotation = Quaternion.Euler(0, 0, Mathf.Abs(delta) > .001f ? Mathf.Sin(clock * 15) * 2 : 0);
            return Mathf.Abs(c.X - target) < .08f;
        }
        bool CrewExit(CrewMember c, float dt)
        {
            State.Brake = true;
            if (State.Speed > .1f) return false;
            if (c.Outside) return true;
            if (!CrewWalk(c, CrewDoor, dt)) return false;
            c.Outside = true; c.X += State.Distance; return true;
        }
        void Deposit(CrewMember c)
        {
            if (c.Carry == 1) State.Coal++; else if (c.Carry == 2) State.Scrap++;
            c.Carry = 0;
        }
        void ReturnCrew(CrewMember c, float dt)
        {
            State.Brake = true;
            if (c.Outside)
            {
                if (!CrewWalk(c, State.Distance + CrewDoor, dt)) return;
                c.Outside = false; c.X = CrewDoor;
            }
            if (c.Carry != 0 && !CrewWalk(c, CrewStock, dt)) return;
            Deposit(c); c.Job = c.AfterReturn; c.AfterReturn = CrewJob.Idle; c.Stage = 0; c.WorkTime = 0;
        }
        public void StepCrew(float dt)
        {
            clock += dt;
            if (!State.Arrived)
            for (int i = 0; i < Crew.Count; i++)
            {
                var c = Crew[i];
                switch (c.Job)
                {
                    case CrewJob.Move:
                        if (CrewWalk(c, c.Destination, dt)) c.Job = CrewJob.Idle; break;
                    case CrewJob.Drive:
                        if (CrewWalk(c, CrewDriver, dt) && c.Stage == 0)
                        { c.Stage = 1; State.Throttle = .42f; State.Brake = false; CrewNotice("기관사 배치 · W/S 출력 · SPACE 제동"); }
                        break;
                    case CrewJob.Fuel:
                        if (c.Carry == 0)
                        {
                            if (!CrewWalk(c, CrewStock, dt) || State.Fuel > 70 || State.Fire) break;
                            if (State.Coal > 0) { State.Coal--; c.Carry = 1; }
                            else if (State.Scrap > 0) { State.Scrap--; c.Carry = 2; }
                        }
                        else if (CrewWalk(c, CrewStove, dt))
                        { c.WorkTime += dt; if (c.WorkTime >= 1 && State.LoadFuel(c.Carry == 1)) { c.Carry = 0; c.WorkTime = 0; sound.Hit(true); } }
                        break;
                    case CrewJob.Cool:
                        if (CrewWalk(c, -.8f, dt))
                        { if (State.Fire) { c.WorkTime += dt; if (c.WorkTime > 1.5f) { State.Extinguish(); c.WorkTime = 0; } }
                          State.Vent = State.Fire || State.Heat > 65 || (State.Vent && State.Heat > 38); }
                        break;
                    case CrewJob.Repair:
                        if (State.Integrity >= .99f) { c.Job = CrewJob.Idle; break; }
                        if (CrewWalk(c, c.Destination, dt) && repairingCrew < 0)
                        { repairingCrew = i; Repairing = true; repairTimer = -.7f;
                          timingTool.localPosition = new Vector3(c.Destination, .8f, 0);
                          foreach (var bolt in bolts) bolt.localPosition = new Vector3(c.Destination - .3f + bolts.IndexOf(bolt) * .3f, .08f, 0);
                          CrewNotice("수리 · 초록 구간에서 SPACE", 4); }
                        break;
                    case CrewJob.Collect:
                        if (!CrewExit(c, dt)) break;
                        if (!CrewWalk(c, c.Target != null ? c.Target.position.x : c.Destination, dt)) break;
                        c.WorkTime += dt;
                        if (c.WorkTime < (c.Target != null ? 1 : 3)) break;
                        c.Carry = 2;
                        if (c.Target != null)
                        { for (int p = pickups.Count - 1; p >= 0; p--) if (pickups[p].Root == c.Target) { c.Carry = pickups[p].Kind; pickups.RemoveAt(p); break; }
                          Destroy(c.Target.gameObject); c.Target = null; }
                        c.Job = CrewJob.Return; c.AfterReturn = CrewJob.Idle; c.WorkTime = 0; sound.Hit(true); break;
                    case CrewJob.Shovel:
                        if (!State.Blocked) { c.Job = c.Outside ? CrewJob.Return : CrewJob.Idle; break; }
                        if (CrewExit(c, dt) && CrewWalk(c, snowBank.position.x - 1, dt)) State.Shovel(dt);
                        break;
                    case CrewJob.Return: ReturnCrew(c, dt); break;
                }
            }
            Driving = Crew.Exists(c => c.Job == CrewJob.Drive && c.Stage == 1);
            if (!Driving || Crew.Exists(c => c.Outside || c.Job == CrewJob.Collect || c.Job == CrewJob.Shovel || c.Job == CrewJob.Return)) State.Brake = true;
            if (Repairing)
            {
                repairTimer += dt; RepairPhase = Mathf.Clamp01(repairTimer / 1.65f);
                if (repairTimer > 1.65f) CrewRepairPress();
            }
            State.Tick(dt);
            if (State.Integrity < previousIntegrity - .3f) CrewNotice("철판이 떨어졌다 · 작업대에 수리 담당 배치", 4);
            previousIntegrity = State.Integrity;
            if (State.Fire && !previousFire) { sound.Hit(false); CrewNotice("엔진 과열 · 냉각 담당이 필요하다", 4); }
            previousFire = State.Fire;
            if (State.Blocked && !snowAnnounced) { snowAnnounced = true; CrewNotice("눈에 막혔다 · 앞쪽 눈더미 우클릭으로 제설 지시", 5); }
            if (State.Arrived && !arrivedAnnounced)
            { arrivedAnnounced = true; FinishCrewRepair(); foreach (var c in Crew) c.Job = CrewJob.Idle; Driving = false; CrewNotice("동부 차량기지 도착 · 프롤로그 완료", 6); sound.Radio(); }
            radioTimer -= dt;
            if (radioTimer <= 0 && !State.Arrived) { radioTimer = 42; sound.Radio(); CrewNotice("무전: 따뜻한 곳이 있습니다. 계속 동쪽으로 오십시오.", 4); }
            crewHintTime = Mathf.Max(0, crewHintTime - dt);
        }
        void UpdateCrewPresentation(float dt)
        {
            crewTrain.position = new Vector3(State.Distance, 0, 0);
            float zoom = State.Speed > .15f ? Mathf.Max(CrewZoom, OverviewSize) : CrewZoom;
            float blend = 1 - Mathf.Exp(-22 * dt);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, zoom, blend);
            var selected = Crew[SelectedCrew];
            float close = Mathf.InverseLerp(OverviewSize, 4, zoom);
            float focus = Mathf.Lerp(State.Distance + 1.5f, selected.Outside ? selected.X : State.Distance + selected.X, close) + crewPan;
            cam.transform.position = Vector3.Lerp(cam.transform.position, new Vector3(focus, .2f, -10), blend);
            if (horizonArt != null) horizonArt.position = new Vector3(cam.transform.position.x * .87f, 0, 0);
            foreach (var c in Crew)
            {
                c.Body.position = new Vector3(c.Outside ? c.X : State.Distance + c.X, c.Outside ? -2.65f : CrewFloor, 0);
                c.Cargo.gameObject.SetActive(c.Carry != 0);
                bool work = c.Job != CrewJob.Idle && c.Job != CrewJob.Move && c.Job != CrewJob.Return;
                c.Image.transform.localPosition = new Vector3(0, .71f + (work ? Mathf.Sin(clock * 7) * .018f : 0), 0);
            }
            selectionMark.position = selected.Body.position + Vector3.up * .04f;
            crewFire.gameObject.SetActive(State.Fuel > 0); crewFire.localScale = new Vector3(crewFire.localScale.x, crewFire.localScale.x * (.8f + Mathf.Sin(clock * 16) * .1f), 1);
            crewSteam.gameObject.SetActive(State.Vent || State.Fire);
            crewSteam.GetComponent<SpriteRenderer>().color = State.Fire ? new Color(1, .38f, .16f, .9f) : new Color(1, 1, 1, .55f);
            crewLamp.GetComponent<SpriteRenderer>().color = new Color(1, .8f, .4f, Driving && !State.Brake ? .8f : .25f);
            crewDamage.gameObject.SetActive(State.Integrity < .9f); crewDamage.localRotation = Quaternion.Euler(0, 0, -20 + Mathf.Sin(clock * 4) * 4);
            foreach (var t in crewSmoke)
            {
                float p = Mathf.Repeat(clock * .18f + crewSmoke.IndexOf(t) / 12f, 1);
                t.gameObject.SetActive(State.Fuel > 0); t.localPosition = new Vector3(13.8f - p * (4 + State.Speed), 1.8f + p * 3.5f, 0);
                t.localScale = Vector3.one * (.025f + p * .10f); t.GetComponent<SpriteRenderer>().color = new Color(.42f, .47f, .55f, (1 - p) * .65f);
            }
            timingTool.gameObject.SetActive(Repairing); needle.localRotation = Quaternion.Euler(0, 0, -RepairPhase * 360);
            foreach (var bolt in bolts) { bolt.gameObject.SetActive(Repairing); bolt.GetComponent<SpriteRenderer>().color = State.Integrity >= (bolts.IndexOf(bolt) + 1) * .32f ? C("#A3C79D") : C("#8D5A4B"); }
            snowBank.gameObject.SetActive(State.Snow > 0); snowBank.localScale = new Vector3(1, Mathf.Max(.03f, State.Snow), 1);
            bool help = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            crewHintRoot.gameObject.SetActive(help || crewHintTime > 0);
            crewHint.text = help ? "1–3 / 클릭 선택 · 우클릭 작업 · 휠 줌 · A/D 시점 · W/S 출력 · SPACE 제동/수리 · ESC 취소" : crewMessage;
            crewHintRoot.position = cam.ViewportToWorldPoint(new Vector3(.5f, .9f, 10));
            crewHintRoot.localScale = Vector3.one * (cam.orthographicSize / 10);
            crewHint.characterSize = .22f * .28f;
            crewHintRoot.Find("Hint backing").localScale = new Vector3(Mathf.Max(8, crewHint.text.Length * .17f), .6f, 1);
            foreach (var flake in flakes)
            {
                var p = flake.localPosition; p.x -= dt * (1.4f + State.Speed * .25f); p.y -= dt * 1.5f;
                if (p.y < -9) p.y = 12; if (p.x < -25) p.x = 25; flake.localPosition = p;
            }
            if (flakes.Count > 0) flakes[0].parent.position = new Vector3(cam.transform.position.x, 0, 0);
            sound.UpdateEngine(State);
        }
    }
}
