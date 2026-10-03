using UnityEngine;
using UnityEngine.UI;

namespace EastTrain
{
    public sealed partial class EastTrainDemo
    {
        public bool PrototypeUI = true;
        Canvas prototypeCanvas;
        Text hudMode, hudValues, hudControls, hudContext, hudStartTitle, hudStartHelp;
        RectTransform hudStartPanel;
        Transform startTarget;
        bool firstDeparture;
        public string FirstDepartureTitle { get; private set; }
        public string FirstDepartureHelp { get; private set; }
        public bool CanQuickStart => PrototypeUI && !firstDeparture && State.Distance < .1f;
        public bool PrototypeHudVisible => prototypeCanvas != null && prototypeCanvas.gameObject.activeSelf;

        RectTransform HudPanel(string name, Vector2 anchor, Vector2 pivot, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(prototypeCanvas.transform, false);
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = offset; rect.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = new Color(.06f, .10f, .14f, .88f); image.raycastTarget = false;
            return rect;
        }
        Text HudText(RectTransform parent, string name, float top, float height, int size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(14, -top - height); rect.offsetMax = new Vector2(-14, -top);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size;
            text.color = new Color(.94f, .95f, .92f); text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        void BuildPrototypeHud()
        {
            var go = new GameObject("Prototype HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            prototypeCanvas = go.GetComponent<Canvas>(); prototypeCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            prototypeCanvas.worldCamera = cam; prototypeCanvas.planeDistance = 1;
            prototypeCanvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var status = HudPanel("Train status", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), new Vector2(410, 192));
            hudMode = HudText(status, "Control mode", 10, 29, 21); hudMode.fontStyle = FontStyle.Bold;
            hudValues = HudText(status, "Live train values", 45, 140, 17);
            var controls = HudPanel("Controls", Vector2.one, Vector2.one, new Vector2(-16, -16), new Vector2(330, 184));
            hudControls = HudText(controls, "Keyboard guide", 12, 165, 16);
            var nearby = HudPanel("Current action", new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(850, 70));
            hudContext = HudText(nearby, "Action guidance", 11, 53, 18); hudContext.alignment = TextAnchor.MiddleCenter;
            hudStartPanel = HudPanel("First departure guide", new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -220), new Vector2(410, 148));
            hudStartTitle = HudText(hudStartPanel, "Next objective", 10, 27, 19); hudStartTitle.color = C("#FFE0A1");
            hudStartHelp = HudText(hudStartPanel, "First departure steps", 43, 101, 17);
            startTarget = Root("Next station marker", world);
            Line("Arrow left", startTarget, new Vector2(-.2f, .2f), Vector2.zero, .07f, "#FFE398", 80);
            Line("Arrow right", startTarget, new Vector2(.2f, .2f), Vector2.zero, .07f, "#FFE398", 80);
        }
        public void QuickStartPrototype()
        {
            if (!CanQuickStart) return;
            State = new TrainState { Fuel = 28, Coal = 1, Integrity = 1, Throttle = .45f, Brake = false };
            Carry = 0; Outside = Repairing = climbing = false; Driving = true;
            PlayerX = 6.05f; PlayerY = .78f; facing = 1; previousIntegrity = 1;
            ResetZoom(); LastAction = "출발 준비 완료 · W/S 출력, SPACE 제동, E 운전석 나가기";
            sound.Cue();
        }
        void UpdateFirstDepartureGuide()
        {
            if (State.Distance > 2) firstDeparture = true;
            bool visible = PrototypeUI && !firstDeparture && !State.Arrived;
            hudStartPanel.gameObject.SetActive(visible); startTarget.gameObject.SetActive(visible && !Outside);
            if (!visible) return;
            float targetX = -7.5f, targetY = .6f;
            if (State.Fuel <= 0)
            {
                if (Carry > 0)
                { FirstDepartureTitle = "첫 출발 2/4 · 화실에 연료 넣기"; FirstDepartureHelp = "D로 오른쪽 둥근 화실까지 이동\n화실 앞에서 E를 눌러 연료를 넣으세요."; targetX = -5.2f; targetY = 1.25f; }
                else if (State.Coal + State.Scrap > 0)
                { FirstDepartureTitle = "첫 출발 1/4 · 연료 집기"; FirstDepartureHelp = "노란 화살표가 가리키는 적재 선반에서 E\n연료를 든 뒤 오른쪽 화실로 가져가세요."; }
                else
                { FirstDepartureTitle = "연료가 없습니다 · 바깥에서 수집"; FirstDepartureHelp = "열차 끝 문에서 E로 내리세요.\n밖의 장작에서 E, 또는 E를 3초 눌러 땔감 수집."; targetX = 8.5f; }
            }
            else if (State.Integrity < .99f)
            {
                FirstDepartureTitle = "첫 출발 3/4 · 고장 난 열차 수리";
                FirstDepartureHelp = Repairing ? $"바늘이 초록 구간에 오면 SPACE!\n현재 수리 {State.Integrity * 100:0}% · 성공 3번이면 완료됩니다."
                    : "D로 오른쪽 작업대까지 이동한 뒤 E\n수리가 시작되면 초록 구간에서 SPACE를 누르세요.";
                targetX = RepairStationX; targetY = 1.1f;
            }
            else
            {
                FirstDepartureTitle = "첫 출발 4/4 · 운전석에서 출발";
                if (Driving) FirstDepartureHelp = State.Brake ? "SPACE로 제동을 풀고 W로 출력을 올리세요.\n출력이 절반 정도면 천천히 출발합니다."
                    : "W로 출력 증가 · S로 감소 · SPACE로 제동\nE를 누르면 운전석에서 나와 기관실로 갈 수 있습니다.";
                else if (PlayerY < 0) FirstDepartureHelp = "사다리 앞으로 이동한 뒤 W를 눌러 올라가세요.\n위층 오른쪽 운전석에서 E를 누르세요.";
                else FirstDepartureHelp = "D로 위층 오른쪽 운전석까지 이동 → E\nSPACE로 제동 해제 → W로 출력 올리기";
                targetX = PlayerY < 0 && !Driving ? 3.2f : 6.6f; targetY = PlayerY < 0 && !Driving ? 1.3f : 3.2f;
            }
            hudStartTitle.text = FirstDepartureTitle;
            hudStartHelp.text = FirstDepartureHelp + (CanQuickStart ? "\nF5 · 준비를 건너뛰고 바로 출발" : "");
            startTarget.position = new Vector3(State.Distance + targetX, targetY + Mathf.Sin(clock * 4) * .09f, 0);
        }
        void UpdatePrototypeHud(string context)
        {
            if (prototypeCanvas == null) return;
            prototypeCanvas.gameObject.SetActive(PrototypeUI);
            UpdateFirstDepartureGuide();
            if (!PrototypeUI) return;
            hudMode.text = State.Arrived ? "차량기지 도착" : Repairing ? "수리 중" : Driving ? "운전 중" : Outside ? "열차 밖 · 탐색 중" : "열차 내부 · 이동 중";
            hudMode.color = State.Fire ? C("#FF8870") : Driving ? C("#A8E4B5") : C("#FFE0A1");
            hudValues.text = $"속도 {State.Speed:0.0}   |   출력 {State.Throttle * 100:0}%   |   제동 {(State.Brake ? "ON" : "OFF")}\n"
                + $"연료 {State.Fuel:0}/100   |   온도 {State.Heat:0}°   |   난방 {State.Warmth:0}%\n"
                + $"차체 {State.Integrity * 100:0}%   |   냉각 {(State.Vent ? "열림" : "닫힘")}   |   {(State.Fire ? "화재 발생!" : "화재 없음")}\n"
                + $"석탄 {State.Coal}   ·   잡동사니 {State.Scrap}   ·   손: {(Carry == 1 ? "석탄" : Carry == 2 ? "땔감" : "비어 있음")}\n"
                + $"이동 {State.Distance:0} / {TrainState.Destination:0}   ·   {DriveStatus}";
            hudControls.text = "프로토타입 조작\n"
                + (Driving ? "W/S 출력 조절 · SPACE 제동\nE / ESC 운전대 놓기\n" : Repairing ? "SPACE 초록 구간에서 수리\nE / ESC 수리 중단\n" : "A/D 이동 · W/S 사다리\nE 사용 · G 연료 내려놓기\n")
                + "밖에서 E 길게: 제설 / 땔감\n휠: 커서 중심 줌 · 휠 클릭: 추적\nCtrl+R: 처음부터";
            hudContext.text = State.Arrived ? "폐역 → 동부 차량기지 · 프롤로그 완료"
                : Repairing ? $"초록 구간에서 SPACE  ·  수리 {State.Integrity * 100:0}%  ·  바늘 {RepairPhase * 100:0}%"
                : string.IsNullOrEmpty(context) ? (Outside ? "장작을 찾거나 열차 문으로 돌아가세요" : "설비 가까이 이동하면 사용할 수 있습니다") : context;
        }
    }
}
