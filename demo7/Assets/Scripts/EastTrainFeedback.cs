using UnityEngine;

namespace EastTrain
{
    public sealed partial class EastTrainDemo
    {
        Transform leftBoot, rightBoot, arm, glove, driveBeacon, controlBoard, hintBoard;
        Transform engineRotor, cylinderRod, furnaceGlow, headlight, brakeLamp, steamCross;
        TextMesh drivingTitle, drivingStatus, engineStatus, coolingStatus, furnaceStatus, repairStatus, hintText;
        SpriteRenderer fireGlowRenderer;
        readonly System.Collections.Generic.List<Transform> embers = new System.Collections.Generic.List<Transform>();
        float moveInput, gait, enginePhase, fuelFlash, zoomVelocity;
        Vector2 cylinderOrigin = new Vector2(-2.6f, -.75f);
        public float InteriorZoom = 3.8f, JourneyZoom = 9.6f, OutsideZoom = 5.3f;
        public bool JourneyView => Driving || State.Speed > .15f;
        public string ViewMode => State.Arrived ? "arrival" : JourneyView ? "driving" : Outside ? "outside" : Repairing ? "repair" : "interior";
        public void AdjustZoom(float wheelDelta)
        {
            if (Mathf.Abs(wheelDelta) < .01f) return;
            float change = Mathf.Clamp(wheelDelta, -480, 480) * .0035f;
            if (JourneyView) JourneyZoom = Mathf.Clamp(JourneyZoom - change, 9.6f, 13);
            else if (Outside) OutsideZoom = Mathf.Clamp(OutsideZoom - change, 3.5f, 9);
            else InteriorZoom = Mathf.Clamp(InteriorZoom - change, 3, 7);
        }
        public void ResetZoom() { InteriorZoom = 3.8f; JourneyZoom = 9.6f; OutsideZoom = 5.3f; }
        public string DriveStatus
        {
            get
            {
                if (State.Fire) return "화재 · 냉각 밸브에서 진화";
                if (State.Blocked) return "눈길 막힘 · 내려서 제설";
                if (State.Integrity <= .05f) return "시동 불가 · 아래층 수리 필요";
                if (State.Fuel <= 0) return "연료 없음 · 화실에 연료 투입";
                if (State.Brake) return "제동 중 · SPACE로 브레이크 해제";
                if (State.Throttle < .02f) return "출력 없음 · W로 출력 올리기";
                if (State.Speed < .2f) return "출발 중";
                return "주행 중 · 출력 " + Mathf.RoundToInt(State.Throttle * 100) + "%" + (State.Vent ? " · 냉각 중" : "");
            }
        }
        void BuildFeedback()
        {
            arm = Box("Working arm", person, .25f, .72f, .55f, .15f, "#B77A4F", 49);
            glove = Circle("Gloved hand", person, .45f, .68f, .18f, "#DED1AC", 50);
            Box("Coat seam", person, .08f, .58f, .035f, .5f, "#8B573F", 47);
            Box("Coat hem", person, 0, .29f, .55f, .12f, "#8C553D", 46);
            Circle("Hood shadow", person, .14f, 1.11f, .29f, "#785A45", 46);
            Box("Face detail", person, .2f, 1.11f, .16f, .17f, "#E3CB9F", 47);
            Box("Eye", person, .245f, 1.14f, .035f, .035f, "#293B42", 48);

            controlBoard = Root("Driving mode marker", train); controlBoard.localPosition = new Vector3(5.3f, 4.85f, 0);
            Box("Mode sign border", controlBoard, 0, 0, 7.3f, 1.8f, "#C3B68C", 70);
            Box("Mode sign backing", controlBoard, 0, 0, 7.2f, 1.7f, "#253B42", 71);
            drivingTitle = Words("조종 중 · W/S 출력 조절", controlBoard, 0, .51f, .25f, "#FFE1A0", 72);
            drivingStatus = Words("", controlBoard, 0, .02f, .2f, "#EDF3E3", 72);
            Words("E / ESC · 운전대 놓기", controlBoard, 0, -.5f, .17f, "#B1C8C3", 72);
            Line("Control marker lead", train, new Vector2(6.6f, 3.6f), new Vector2(6.6f, 3.9f), .04f, "#F4D395", 69);
            driveBeacon = Circle("Occupied driving station", train, 6.6f, 3.48f, .28f, "#F1CC7E", 55);

            hintBoard = Root("Nearby interaction", world);
            Box("Hint backing", hintBoard, 0, 0, 3.8f, .42f, "#263D45", 69);
            hintText = Words("", hintBoard, 0, 0, .12f, "#F2DDAE", 70);

            engineRotor = Root("Flywheel spokes", train); engineRotor.localPosition = new Vector3(-1.8f, -.35f, 0);
            for (int i = 0; i < 5; i++)
            {
                var spoke = Box("Flywheel spoke", engineRotor, 0, 0, 1.45f, .055f, "#AFB2A0", 20);
                spoke.localRotation = Quaternion.Euler(0, 0, i * 36);
            }
            Circle("Flywheel center", engineRotor, 0, 0, .23f, "#D9BD81", 22);
            Box("Cylinder casing", train, -2.9f, -.75f, .55f, .8f, "#73868A", 19);
            cylinderRod = Box("Reciprocating rod", train, -2.6f, -.75f, .65f, .12f, "#D1C5A6", 23);
            engineStatus = Words("기관 정지", train, -1.7f, -1.18f, .13f, "#B4C5C4", 38);
            coolingStatus = Words("닫힘", train, -.25f, .76f, .095f, "#E0C8A1", 38);
            furnaceStatus = Words("불 꺼짐", train, -5.2f, -1.22f, .12f, "#E9C495", 40);
            repairStatus = Words("수리 필요", train, RepairStationX + 1.35f, -1.3f, .11f, "#ECAF83", 38);
            steamCross = Root("Valve wheel handle", train); steamCross.localPosition = new Vector3(-.25f, .35f, 0);
            Box("Valve horizontal", steamCross, 0, 0, .48f, .065f, "#E7CEAA", 25);
            Box("Valve vertical", steamCross, 0, 0, .065f, .48f, "#E7CEAA", 25);

            furnaceGlow = Shape("Furnace light spill", train, -5.2f, -.38f, 2.5f, 2.4f, new Color(1, .53f, .14f, .1f), 26, true);
            fireGlowRenderer = furnaceGlow.GetComponent<SpriteRenderer>();
            for (int i = 0; i < 10; i++) embers.Add(Circle("Rising furnace ember", train, -5.2f, -.4f, .055f, "#FFD181", 27));
            for (int i = 0; i < 5; i++)
            {
                Box("Furnace rib", train, -6.22f + i * .5f, -1.35f, .05f, .3f, "#A68966", 22);
                Circle("Furnace bolt", train, -6.18f + i * .48f, .77f, .075f, "#C9B08C", 23);
            }
            headlight = Shape("Headlight beam", train, 11, -.2f, 4, 1.3f, new Color(1, .85f, .53f, .12f), 9, true);
            Circle("Headlamp housing", train, 9.1f, .3f, .5f, "#42575D", 25);
            Circle("Headlamp lens", train, 9.13f, .3f, .32f, "#F8D997", 26);
            brakeLamp = Circle("Brake lamp", train, 7.9f, 1.2f, .2f, "#C1644D", 26);
            // Small material details distinguish machine parts at the closer camera distance.
            for (int i = 0; i < 20; i++)
            {
                float x = -8.5f + i * .85f;
                Box("Floor board seam", train, x, -1.72f, .025f, .09f, "#655E4C", 25);
                Circle("Roof fastener", train, x, 2.03f, .06f, "#C4AF8A", 26);
            }
            for (int i = 0; i < 7; i++)
                Box("Steam pipe coupling", train, -4.65f + i, 1.5f, .12f, .29f, "#8A765B", 26);
        }

        void UpdateCamera(float dt)
        {
            float x, y, zoom;
            if (State.Arrived) { x = State.Distance + 7; y = 1.3f; zoom = 8.5f; }
            else if (JourneyView) { x = State.Distance + 6.5f; y = 1.45f; zoom = JourneyZoom; }
            else if (Outside) { x = PlayerX + facing * .8f; y = -.15f; zoom = OutsideZoom; }
            else { x = State.Distance + PlayerX + facing * .45f; y = PlayerY > 0 ? 1.75f : -.05f; zoom = InteriorZoom; }
            // Maintain at least 13 units of visible track beyond the front of the train.
            // This also applies while walking inside a moving train, after leaving the controls.
            if (JourneyView) zoom = Mathf.Max(zoom, 17 / Mathf.Max(.8f, cam.aspect));
            else if (State.Arrived) zoom = Mathf.Max(zoom, 11.4f / Mathf.Max(.8f, cam.aspect));
            cam.transform.position = Vector3.SmoothDamp(cam.transform.position, new Vector3(x, y, -10), ref cameraVelocity, .24f, Mathf.Infinity, dt);
            cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, zoom, ref zoomVelocity, .3f, Mathf.Infinity, dt);
        }

        void UpdateFeedback(float dt)
        {
            controlBoard.gameObject.SetActive(false);
            driveBeacon.gameObject.SetActive(Driving);
            drivingStatus.text = DriveStatus;
            drivingStatus.color = State.Speed > .2f && !State.Brake && !State.Fire ? C("#B3E0B6") : C("#FFD095");
            drivingTitle.text = "조종 중 · W/S 출력 " + Mathf.RoundToInt(State.Throttle * 100) + "%";
            engineRotor.localRotation = Quaternion.Euler(0, 0, enginePhase);
            cylinderRod.localPosition = cylinderOrigin + Vector2.right * (Mathf.Sin(enginePhase * Mathf.Deg2Rad) * .2f);
            engineStatus.text = State.Fire ? "화재!" : !State.EngineRunning ? "기관 정지" : State.Throttle > .03f ? "기관 가동" : "기관 대기";
            engineStatus.color = State.Fire ? C("#FF8961") : State.EngineRunning ? C("#B6DBAC") : C("#ADBCBA");
            coolingStatus.text = State.Vent ? "열림 · 냉각 중" : "닫힘";
            steamCross.localRotation = Quaternion.RotateTowards(steamCross.localRotation, Quaternion.Euler(0, 0, State.Vent ? 45 : 0), dt * 180);
            furnaceStatus.text = State.Fire ? "과열!" : State.Fuel > 0 ? "연소 중" : "불 꺼짐";
            repairStatus.text = Repairing ? "수리 중" : State.Integrity >= .99f ? "수리 완료" : "수리 필요";
            fuelFlash = Mathf.Max(0, fuelFlash - dt);
            fireGlowRenderer.color = new Color(1, .55f, .16f, State.Fuel > 0 ? .08f + .045f * Mathf.Sin(clock * 13) + fuelFlash * .2f : 0);
            for (int i = 0; i < embers.Count; i++)
            {
                float phase = Mathf.Repeat(clock * .7f + i * .11f, 1);
                embers[i].gameObject.SetActive(State.Fuel > 0);
                embers[i].localPosition = new Vector3(-5.2f + Mathf.Sin(i * 8 + phase * 4) * .35f, -.9f + phase * .95f, 0);
                embers[i].GetComponent<SpriteRenderer>().color = new Color(1, .65f + .3f * phase, .25f, 1 - phase);
            }
            brakeLamp.GetComponent<SpriteRenderer>().color = State.Brake ? C("#F37861") : C("#8ACCAB");
            headlight.GetComponent<SpriteRenderer>().color = new Color(1, .85f, .53f, State.EngineRunning ? .17f : .05f);
            UpdateTravellerPose(dt);
            UpdateContextPresentation(dt);
            bool atWorkbench = !Outside && PlayerY < 0 && Mathf.Abs(PlayerX - RepairStationX) < 1.05f;
            hintBoard.position = atWorkbench
                ? new Vector3(State.Distance + RepairStationX + 3.2f, -.55f, 0)
                : new Vector3(person.position.x, person.position.y + 1.78f, 0);
        }
        void UpdateTravellerPose(float dt)
        {
            bool walk = moveInput > .05f && !Driving && !Repairing;
            if (walk) gait += dt * (climbing ? 12 : 16);
            float step = walk ? Mathf.Sin(gait) : 0;
            leftBoot.localPosition = new Vector3(-.14f + step * .12f, .17f + Mathf.Max(0, step) * .11f, 0);
            rightBoot.localPosition = new Vector3(.14f - step * .12f, .17f + Mathf.Max(0, -step) * .11f, 0);
            leftBoot.localRotation = Quaternion.Euler(0, 0, step * 22);
            rightBoot.localRotation = Quaternion.Euler(0, 0, -step * 22);
            Vector2 shoulder = new Vector2(.1f, .79f), hand;
            if (Driving) hand = person.InverseTransformPoint(lever.TransformPoint(new Vector3(0, .8f, 0)));
            else if (climbing) hand = new Vector2(.3f, .97f + step * .17f);
            else if (Repairing) hand = new Vector2(.45f, .83f + Mathf.Sin(clock * 8) * .05f);
            else if (Carry > 0) hand = new Vector2(.48f, .63f);
            else hand = new Vector2(.17f + step * .19f, .41f);
            Vector2 center = (shoulder + hand) * .5f, delta = hand - shoulder;
            arm.localPosition = center; arm.localScale = new Vector3(delta.magnitude, .14f, 1);
            arm.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            glove.localPosition = hand;
            UpdatePaintedTraveller(walk, step);
        }
        string InteractionHint()
        {
            if (Driving || State.Arrived) return "";
            if (Repairing) return "수리 중 · 초록 구간에서 SPACE";
            if (climbing) return "W / S · 사다리 이동";
            if (Outside)
            {
                if (State.Blocked && Mathf.Abs(PlayerX - TrainState.SnowStop - 11) < 2.3f) return "E 길게 · 제설";
                if (Mathf.Abs(Mathf.Abs(PlayerX - State.Distance) - 9.25f) < 1.15f) return "E · 열차에 타기";
                if (Carry == 0) return gatherTime > 0 ? "땔감 찾는 중 · E 유지" : "E · 줍기 / 길게 눌러 땔감";
                return "G · 내려놓기";
            }
            if (PlayerY > 0) return Mathf.Abs(PlayerX - 6.6f) < 1.35f ? "E · 운전 시작" : Mathf.Abs(PlayerX - 4.45f) < .85f ? "E · 무전 듣기" : "";
            if (Mathf.Abs(PlayerX) > 8.15f) return State.Speed > .1f ? "주행 중 · 정차 후 하차" : "E · 열차에서 내리기";
            if (Mathf.Abs(PlayerX + 7.5f) < .85f) return Carry > 0 ? "E · 연료 적재" : "E · 연료 들기";
            if (Mathf.Abs(PlayerX + 5.2f) < 1) return Carry > 0 ? "E · 화실에 연료 투입" : "연료를 들고 와서 E";
            if (Mathf.Abs(PlayerX + 1.35f) < 1.25f) return State.Fire ? "E · 화재 진압" : State.Vent ? "E · 냉각 밸브 닫기" : "E · 냉각 밸브 열기";
            if (Mathf.Abs(PlayerX - RepairStationX) < 1.05f) return State.Integrity >= .99f ? "수리 완료" : "E · 수리 시작";
            if (Mathf.Abs(PlayerX - 3.2f) < .65f) return "W · 사다리 오르기";
            return "";
        }
    }
}
