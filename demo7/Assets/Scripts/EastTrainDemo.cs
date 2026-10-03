using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EastTrain
{
    public sealed partial class EastTrainDemo : MonoBehaviour
    {
        public const float RepairStationX = 5.6f;
        public TrainState State = new TrainState();
        public bool Outside, Driving, Repairing;
        public int Carry;
        public float PlayerX = -7.4f, PlayerY = -1.65f, RepairPhase;
        public string LastAction = "";
        Transform world, train, person, ladder, lever, needle, piston, radioLamp, smokeRoot, carried;
        Transform far, middle, ground, snowBank, flame, heatColumn, fuelColumn, gap, breath, coolingValve, steam, engineFire;
        Sprite square, disc; Font font; Camera cam; EastTrainAudio sound;
        readonly List<Transform> wheels = new List<Transform>();
        readonly List<Transform> flakes = new List<Transform>();
        readonly List<Transform> smoke = new List<Transform>();
        readonly List<Transform> stock = new List<Transform>();
        readonly List<Transform> bolts = new List<Transform>();
        readonly List<Pickup> pickups = new List<Pickup>();
        TextMesh radioWords, destinationWords;
        float clock, radioTimer = 2, radioVisible, repairTimer, feedback, gatherTime, facing = 1;
        float previousIntegrity; bool climbing, previousFire, snowAnnounced, arrivedAnnounced;
        Vector3 cameraVelocity;
        struct Pickup { public Transform Root; public int Kind; public bool Inside; }
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        void Awake()
        {
            Application.runInBackground = true;
            cam = Camera.main;
            if (cam == null) { cam = new GameObject("Main Camera").AddComponent<Camera>(); cam.tag = "MainCamera"; }
            cam.orthographic = true; cam.orthographicSize = 8.1f;
            cam.backgroundColor = C("#718C9C"); cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0, 1.3f, -10);
            if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[4096];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) pixels[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(32 - Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(32, 32))));
            tex.SetPixels(pixels); tex.Apply(); disc = Sprite.Create(tex, new Rect(0, 0, 64, 64), Vector2.one * .5f, 64);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 64);
            world = Root("East Train / procedural prototype", transform);
            BuildLandscape(); BuildTrain(); BuildPerson(); BuildFeedback(); BuildArtwork(); BuildWorldArtwork(); BuildContextPresentation();
            cam.orthographicSize = 3.8f;
            cam.transform.position = new Vector3(PlayerX, -.1f, -10);
            sound = gameObject.AddComponent<EastTrainAudio>(); sound.Initialize();
            previousIntegrity = State.Integrity;
            // Original solo-control design. Crew experiments remain inactive while art is undecided.
        }
        Transform Root(string name, Transform parent) { var o = new GameObject(name); o.transform.SetParent(parent, false); return o.transform; }
        Transform Shape(string name, Transform parent, float x, float y, float w, float h, Color color, int order, bool round = false)
        {
            var t = Root(name, parent); t.localPosition = new Vector3(x, y, 0); t.localScale = new Vector3(w, h, 1);
            var r = t.gameObject.AddComponent<SpriteRenderer>(); r.sprite = round ? disc : square; r.color = color; r.sortingOrder = order; return t;
        }
        Transform Box(string name, Transform p, float x, float y, float w, float h, string color, int order) => Shape(name, p, x, y, w, h, C(color), order);
        Transform Circle(string name, Transform p, float x, float y, float size, string color, int order) => Shape(name, p, x, y, size, size, C(color), order, true);
        Transform Line(string name, Transform p, Vector2 a, Vector2 b, float width, string color, int order)
        {
            var t = Box(name, p, (a.x + b.x) / 2, (a.y + b.y) / 2, Vector2.Distance(a, b), width, color, order);
            t.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg); return t;
        }
        TextMesh Words(string text, Transform p, float x, float y, float size, string color, int order = 35)
        {
            var t = Root("Painted / " + text.Replace('\n', ' '), p); t.localPosition = new Vector3(x, y, -.1f);
            var m = t.gameObject.AddComponent<TextMesh>(); m.text = text; m.font = font; m.fontSize = 64; m.characterSize = size * .28f;
            m.anchor = TextAnchor.MiddleCenter; m.alignment = TextAlignment.Center; m.color = C(color);
            var r = m.GetComponent<MeshRenderer>(); r.sharedMaterial = font.material; r.sortingOrder = order; return m;
        }
        void Mountain(Transform parent, float x, float y, float width, float height, string color, int order)
        {
            var t = Root("Distant mountain", parent); t.localPosition = new Vector3(x, y, 0);
            var mesh = new Mesh();
            mesh.vertices = new[] {
                new Vector3(-width * .6f, -height, 0), new Vector3(-width * .34f, -.15f * height, 0),
                new Vector3(-width * .2f, -.24f * height, 0), new Vector3(0, height * .55f, 0),
                new Vector3(width * .16f, .07f * height, 0), new Vector3(width * .3f, .16f * height, 0),
                new Vector3(width * .65f, -height, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6 };
            var colors = new Color[7]; for (int i = 0; i < 7; i++) colors[i] = C(color); mesh.colors = colors;
            mesh.RecalculateBounds(); t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = t.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")); renderer.sortingOrder = order;
        }
        void BuildLandscape()
        {
            far = Root("Far snow ridges", world); middle = Root("Wind swept foothills", world); ground = Root("Railway", world);
            Box("Sky", far, 0, 6, 180, 24, "#78929F", -100);
            Circle("Winter sun", far, 10, 8, 2.3f, "#BBC8C8", -95);
            for (int i = -6; i <= 16; i++)
            {
                Mountain(far, i * 12, 3.2f + Mathf.Sin(i * 2) * 1.4f, 14, 4.5f, "#91A7B1", -90);
                Shape("Snow dune", middle, i * 19, -.3f, 31, 5 + Mathf.Sin(i) * 2, C("#ADBFC7"), -80, true);
            }
            Box("Snowfield", world, 170, -7.4f, 430, 10, "#D0DADD", -70);
            Box("Far rail", ground, 170, -2.72f, 440, .09f, "#566876", -10);
            Box("Near rail", ground, 170, -2.94f, 440, .12f, "#394D5B", 2);
            for (int i = -25; i < 385; i++)
            {
                Box("Sleeper", ground, i * 1.1f, -2.92f, .36f, .15f, "#687B85", -9);
                if (i % 4 == 0) Shape("Ground ice", ground, i * 1.1f, -3.6f - Mathf.Abs(Mathf.Sin(i)) * 1.6f, 1.1f, .15f, C("#91A8B5"), 40, true);
            }
            BuildStation(-11.5f, false); BuildStation(TrainState.Destination + 15, true);
            for (int i = 0; i < 13; i++)
            {
                float x = 18 + i * 25;
                AddPickup(x, i % 3 == 0 ? 2 : 1);
                Box("Route post", ground, x + 4, -.5f, .09f, 4.2f, "#617380", -20);
                Box("East marker", ground, x + 4, .9f, 1.1f, .45f, "#CDD8DC", -19);
                Words("동 →", ground, x + 4, .9f, .13f, "#435B6B", -18);
            }
            snowBank = Root("Blocked track / shovel here", world); snowBank.position = new Vector3(TrainState.SnowStop + 11, 0, 0);
            Shape("Packed snow", snowBank, 0, -2, 5, 2.4f, C("#E4EBE9"), 8, true);
            Shape("Snow shadow", snowBank, 1, -2.45f, 4, .7f, C("#99B1BE"), 9, true);
            Box("Shovel handle", snowBank, 0, -.1f, .08f, 1.6f, "#8D7361", 12);
            Box("Shovel blade", snowBank, 0, -.8f, .38f, .44f, "#617A87", 12);
            Words("E 꾹 · 제설", snowBank, 0, 1.1f, .15f, "#364C5B", 15);
            var weather = Root("Snowfall", world);
            var random = new System.Random(21);
            for (int i = 0; i < 120; i++)
            {
                var t = Shape("Snow", weather, (float)random.NextDouble() * 36 - 18, (float)random.NextDouble() * 20 - 8, .035f, .12f, new Color(.9f, .96f, 1, .28f + (i % 3) * .12f), 65);
                t.rotation = Quaternion.Euler(0, 0, -30); flakes.Add(t);
            }
        }
        void BuildStation(float x, bool depot)
        {
            var s = Root(depot ? "Eastern depot" : "Abandoned station", world); s.position = new Vector3(x, 0, 0);
            Box("Station wall", s, 0, .2f, depot ? 13 : 8, 6, "#6C818A", -35);
            Box("Roof", s, 0, 3.3f, depot ? 15 : 10, .4f, "#D5DFDF", -34);
            Box("Station sign", s, 0, 1.9f, 5.8f, .9f, "#354D5C", -33);
            var sign = Words(depot ? "동부 차량기지" : "서쪽 폐역", s, 0, 1.9f, .3f, "#D2DACE", -32);
            if (depot) destinationWords = sign;
            Box("Door", s, 1, -.8f, 2, 3.2f, "#304753", -32);
            for (int i = -2; i <= 1; i++) Box("Abandoned window", s, i * 1.6f, .6f, 1, 1, depot ? "#B6A171" : "#3D5867", -32);
            Box("Lamp post", s, 4.8f, -.1f, .12f, 5.1f, "#374E59", -15);
            Circle("Lamp haze", s, 4.8f, 2.4f, 1.7f, "#C7BE9A", -16);
            Box("Lamp", s, 4.8f, 2.4f, .5f, .3f, "#FFF0B9", -14);
        }
        void AddPickup(float x, int kind, bool inside = false, float y = -2.4f)
        {
            var p = Root(kind == 1 ? "Coal crate" : "Burnable salvage", inside ? train : world); p.localPosition = new Vector3(x, y, 0);
            Box("Crate", p, 0, 0, .72f, .55f, kind == 1 ? "#725B47" : "#A18B69", 25);
            Line("Crate brace", p, new Vector2(-.32f, -.23f), new Vector2(.32f, .23f), .06f, "#D4B780", 26);
            Words("E", p, 0, .06f, .14f, "#F1D9A6", 27);
            pickups.Add(new Pickup { Root = p, Kind = kind, Inside = inside });
            if (worldArt != null) PaintPickup(p, kind);
        }
        void BuildTrain()
        {
            train = Root("Locomotive / cutaway", world);
            Box("Chassis", train, 0, -2.05f, 19, .55f, "#263C48", 10);
            Box("Outer shell", train, -.2f, .05f, 18.8f, 4.1f, "#715F54", 10);
            Box("Engine room shadow", train, -1.7f, -.25f, 15, 2.8f, "#293C42", 11);
            Box("Cab shell", train, 5.5f, 1.8f, 6.7f, 3.4f, "#8C6050", 11);
            Box("Cab cutaway", train, 5.5f, 1.85f, 5.9f, 2.55f, "#364348", 12);
            Box("Cab glass", train, 7.7f, 2.35f, 1.25f, 1.3f, "#94AFB3", 13);
            Box("Cab glass mullion", train, 7.7f, 2.35f, .07f, 1.3f, "#58676C", 14);
            Box("Lower floor", train, 0, -1.77f, 18.5f, .18f, "#A69372", 23);
            Box("Upper floor", train, 5.6f, .67f, 6.4f, .17f, "#AC9670", 23);
            Box("Snow on roof", train, -3.9f, 2.2f, 11.1f, .2f, "#D7E0DE", 25);
            Box("Snow on cab", train, 5.45f, 3.57f, 7.1f, .22f, "#DFE6E2", 25);
            for (int i = 0; i < 7; i++)
            {
                float x = -7.9f + i * 2.55f;
                var w = Root("Wheel", train); w.localPosition = new Vector3(x, -2.3f, 0);
                Circle("Wheel tire", w, 0, 0, 1.25f, "#263744", 16);
                Circle("Wheel hub", w, 0, 0, .92f, "#61717A", 17);
                for (int j = 0; j < 3; j++) { var spoke = Box("Spoke", w, 0, 0, .1f, .8f, "#2C414E", 18); spoke.localRotation = Quaternion.Euler(0, 0, j * 60); }
                Circle("Axle", w, 0, 0, .24f, "#BEAA84", 19); wheels.Add(w);
            }
            Line("Connecting rod", train, new Vector2(-7.9f, -2.3f), new Vector2(7.4f, -2.3f), .1f, "#A7A394", 20);
            Box("Front buffer", train, 9.4f, -1.8f, .8f, .3f, "#2C3E45", 24);
            var plow = Box("Snow plow", train, 9.7f, -2.25f, .28f, 1.2f, "#725F50", 22); plow.localRotation = Quaternion.Euler(0, 0, 25);
            // Pipes cross the room: each station is a physical place to visit.
            Line("Steam main", train, new Vector2(-5, 1.5f), new Vector2(2.8f, 1.5f), .19f, "#BD9161", 15);
            Line("Steam downpipe", train, new Vector2(2.8f, 1.5f), new Vector2(2.8f, -.8f), .17f, "#BD9161", 15);
            Line("Water return", train, new Vector2(-5, -1.4f), new Vector2(2, -1.4f), .11f, "#678D9A", 15);
            Box("Chimney", train, -4.4f, 3, .8f, 1.6f, "#36434A", 12);
            Box("Chimney cap", train, -4.4f, 3.83f, 1.1f, .2f, "#526269", 13);
            Box("Furnace", train, -5.2f, -.3f, 2.3f, 2.4f, "#67584B", 16);
            Circle("Firebox rim", train, -5.2f, -.4f, 1.6f, "#C49C68", 17);
            Circle("Firebox dark", train, -5.2f, -.4f, 1.32f, "#252B2E", 18);
            flame = Shape("Fire inside furnace", train, -5.2f, -.56f, .95f, .7f, C("#EFAC57"), 19, true);
            for (int i = 0; i < 5; i++) Box("Firebox grate", train, -5.67f + i * .235f, -.62f, .055f, .85f, "#473B32", 20);
            Words("화실 · E 넣기", train, -5.2f, 1.25f, .15f, "#E9D1A4");
            Box("Fuel sight glass", train, -3.75f, -.35f, .22f, 1.8f, "#162A35", 16);
            fuelColumn = Box("Fuel level", train, -3.75f, -.35f, .12f, 1.7f, "#F1B568", 17);
            Box("Coal shelf", train, -7.8f, -1.5f, 1.75f, .2f, "#887359", 17);
            for (int i = 0; i < 5; i++) stock.Add(Box("Stored fuel", train, -8.3f + (i % 3) * .42f, -1.1f + (i / 3) * .43f, .4f, .4f, "#8D7A60", 18));
            Words("연료 · E", train, -7.5f, .8f, .13f, "#CCBE9F");
            Circle("Engine flywheel", train, -1.8f, -.35f, 1.8f, "#5E7679", 17);
            Circle("Engine inner wheel", train, -1.8f, -.35f, 1.38f, "#253D47", 18);
            piston = Box("Moving piston", train, -1.8f, -.35f, 1.45f, .15f, "#A4A48C", 19);
            Box("Pump body", train, -.25f, -.4f, .55f, 1.4f, "#677E7F", 17);
            coolingValve = Circle("Cooling valve", train, -.25f, .35f, .58f, "#B45F4D", 20);
            steam = Shape("Cooling steam", train, -.25f, .9f, .9f, 1.1f, new Color(.82f, .9f, .91f, .3f), 29, true);
            engineFire = Shape("Engine fire", train, -1.6f, -.1f, 1.5f, 1.7f, C("#F77C43"), 30, true);
            Words("냉각 · E", train, -1.35f, 1.04f, .14f, "#D0C4A6");
            Box("Thermometer", train, .35f, .15f, .22f, 1.45f, "#1E2D34", 19);
            heatColumn = Box("Heat column", train, .35f, .15f, .12f, 1.25f, "#CE664E", 20);
            Box("Danger mark", train, .35f, .7f, .37f, .055f, "#E67A5D", 21);
            gap = Box("Detached side plate", train, RepairStationX, -.85f, 1.1f, 1.4f, "#748B99", 16);
            Box("Repair bench", train, RepairStationX, -.4f, 2.2f, .18f, "#A39377", 20);
            var gauge = Root("Mechanical timing tool", train); gauge.localPosition = new Vector3(RepairStationX, .1f, 0);
            Circle("Timing case", gauge, 0, 0, .95f, "#A58E66", 21);
            Circle("Timing face", gauge, 0, 0, .82f, "#243F47", 22);
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.Lerp(.62f, .82f, i / 10f) * Mathf.PI * 2;
                Circle("Safe timing sector", gauge, Mathf.Sin(a) * .34f, Mathf.Cos(a) * .34f, .11f, "#93C6A2", 23);
            }
            needle = Root("Repair timing needle", gauge);
            Box("Needle", needle, 0, .17f, .04f, .4f, "#F3D8A5", 24);
            Circle("Needle pin", gauge, 0, 0, .13f, "#D8C59A", 25);
            for (int i = 0; i < 3; i++) bolts.Add(Circle("Fastener", train, RepairStationX + .85f + i * .32f, -.06f, .15f, "#69514B", 24));
            Words("수리 작업대", train, RepairStationX + 1.25f, .43f, .11f, "#D7C9AA");
            ladder = Root("Ladder", train);
            for (int i = 0; i < 8; i++) Box("Rung", ladder, 3.2f, -1.5f + i * .34f, .65f, .055f, "#D0B784", 24);
            Box("Ladder rail", ladder, 2.85f, -.3f, .065f, 2.7f, "#8A7C63", 24);
            Box("Ladder rail", ladder, 3.55f, -.3f, .065f, 2.7f, "#8A7C63", 24);
            Words("W ↑  S ↓", train, 3.2f, 1.75f, .12f, "#CCC5A9");
            Box("Driving desk", train, 6.6f, 1.1f, 1.9f, .65f, "#78654D", 19);
            lever = Root("Throttle lever", train); lever.localPosition = new Vector3(6.6f, 1.3f, 0);
            Box("Lever arm", lever, 0, .38f, .07f, .8f, "#AFC0BB", 22);
            Circle("Lever grip", lever, 0, .8f, .23f, "#BD7154", 23);
            Words("운전 E\nW / S 출력 · SPACE 제동", train, 7.1f, 2.6f, .105f, "#D9CCAA");
            Box("Radio", train, 4.45f, 1.25f, .8f, .65f, "#77857F", 18);
            for (int i = 0; i < 5; i++) Box("Speaker slot", train, 4.2f + i * .09f, 1.25f, .035f, .4f, "#2E464E", 19);
            radioLamp = Circle("Radio signal", train, 4.7f, 1.38f, .13f, "#A8BD8D", 23);
            // A receiver paper strip is part of the prop, never a screen-space HUD.
            Box("Receiver paper", train, 4.3f, 2.4f, 2.25f, .7f, "#C6BB99", 18);
            radioWords = Words("동쪽으로 오십시오.\n따뜻한 곳이 있습니다.", train, 4.3f, 2.4f, .095f, "#394B4A", 20);
            Words("이스트 트레인 / 07", train, -.8f, 1.85f, .17f, "#D6C5A7");
            for (int i = 0; i < 4; i++)
            {
                float x = -7 + i * 3.8f;
                Shape("Warm lamp glow", train, x, .1f, 2.5f, 2.5f, new Color(.9f, .65f, .32f, .1f), 12, true);
                Box("Ceiling light", train, x, 1.26f, .35f, .13f, "#F7D28F", 27);
            }
            for (int i = 0; i < 32; i++) Circle("Hull rivet", train, -8.8f + i * .55f, -1.98f, .06f, "#D1B78B", 28);
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Door frame", train, side * 8.8f, -.35f, .15f, 2.8f, "#B19A75", 26);
                Words("E 승하차", train, side * 8.05f, 1.36f, .13f, "#D8C7A3");
            }
            smokeRoot = Root("Exhaust", train);
            for (int i = 0; i < 14; i++) smoke.Add(Shape("Smoke puff", smokeRoot, 0, 0, 1, 1, new Color(.2f, .27f, .3f, .4f), 30, true));
        }
        void BuildPerson()
        {
            person = Root("Traveller", world);
            Box("Coat", person, 0, .62f, .5f, .73f, "#C1774E", 44);
            Circle("Hood", person, 0, 1.12f, .55f, "#D09B67", 45);
            Box("Face", person, .13f, 1.1f, .22f, .22f, "#E1CAB0", 46);
            Box("Scarf", person, 0, .88f, .62f, .15f, "#DDC89C", 47);
            leftBoot = Box("Boot L", person, -.15f, .17f, .18f, .33f, "#283C45", 45);
            rightBoot = Box("Boot R", person, .15f, .17f, .18f, .33f, "#283C45", 45);
            Box("Backpack", person, -.3f, .67f, .23f, .45f, "#697267", 43);
            carried = Box("Carried fuel", person, .4f, .55f, .4f, .4f, "#927653", 48);
            breath = Shape("Cold breath", person, .55f, 1.1f, .5f, .14f, new Color(.9f, .95f, 1, .5f), 50, true);
        }
        void Update()
        {
            if (crewMode) { UpdateCrewMode(Mathf.Min(Time.deltaTime, .05f)); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f); clock += dt;
            moveInput = 0;
            if (Mouse.current != null)
            {
                AdjustZoom(Mouse.current.scroll.ReadValue().y);
                if (Mouse.current.middleButton.wasPressedThisFrame) ResetZoom();
            }
            var key = Keyboard.current;
            if (key != null)
            {
                if (key.f5Key.wasPressedThisFrame) QuickStartPrototype();
                if (key.rKey.wasPressedThisFrame && (key.leftCtrlKey.isPressed || key.rightCtrlKey.isPressed))
                { UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); return; }
                if (key.escapeKey.wasPressedThisFrame) { Driving = false; Repairing = false; }
                if (!State.Arrived)
                {
                    if (Repairing)
                    {
                        if (key.eKey.wasPressedThisFrame) Repairing = false;
                        else if (key.spaceKey.wasPressedThisFrame && repairTimer >= 0) FinishRepair();
                    }
                    else if (Driving)
                    {
                        State.Throttle = Mathf.Clamp01(State.Throttle + ((key.wKey.isPressed ? 1 : 0) - (key.sKey.isPressed ? 1 : 0)) * dt * .4f);
                        if (key.spaceKey.wasPressedThisFrame) { State.Brake = !State.Brake; sound.Hit(true); }
                        if (key.eKey.wasPressedThisFrame) { Driving = false; sound.Cue(); }
                    }
                    else
                    {
                        float axis = (key.dKey.isPressed || key.rightArrowKey.isPressed ? 1 : 0) - (key.aKey.isPressed || key.leftArrowKey.isPressed ? 1 : 0);
                        float vertical = (key.wKey.isPressed || key.upArrowKey.isPressed ? 1 : 0) - (key.sKey.isPressed || key.downArrowKey.isPressed ? 1 : 0);
                        MoveTraveller(axis, vertical, dt);
                        if (key.eKey.wasPressedThisFrame) Interact();
                        if (key.gKey.wasPressedThisFrame) Drop();
                        if (Outside && key.eKey.isPressed) OutsideWork(dt);
                        else gatherTime = 0;
                    }
                }
            }
            if (Repairing)
            {
                float before = repairTimer; repairTimer += dt;
                if (before < 0 && repairTimer >= 0) sound.Cue();
                RepairPhase = Mathf.Clamp01(repairTimer / 1.65f);
                if (repairTimer > 1.65f) FinishRepair();
            }
            State.Tick(dt);
            if (State.Integrity < previousIntegrity - .3f) { sound.Hit(false); LastAction = "덧댄 철판이 떨어졌다. 작업대에서 수리해야 한다."; }
            previousIntegrity = State.Integrity;
            if (State.Fire && !previousFire) { sound.Hit(false); LastAction = "엔진 화재. 냉각 밸브에서 E로 진화."; }
            previousFire = State.Fire;
            if (State.Blocked && !snowAnnounced) { snowAnnounced = true; State.Brake = true; LastAction = "눈에 막혔다. 앞문으로 내려 제설."; }
            if (State.Arrived && !arrivedAnnounced)
            {
                arrivedAnnounced = true; Driving = false; Repairing = false; sound.Radio();
                destinationWords.text = "차량기지 도착\n프롤로그 완료";
                radioWords.text = "잘 들립니다.\n동쪽으로, 계속 오십시오.";
            }
            radioTimer -= dt;
            if (radioTimer <= 0 && !State.Arrived) { sound.Radio(); radioTimer = 42; radioVisible = 8; }
            radioVisible = Mathf.Max(0, radioVisible - dt);
            feedback = Mathf.Max(0, feedback - dt);
            Present(dt);
        }

        public void MoveTraveller(float horizontal, float vertical, float dt)
        {
            if (Driving || Repairing || State.Arrived) return;
            moveInput = Mathf.Abs(horizontal) + Mathf.Abs(vertical);
            if (Mathf.Abs(horizontal) > .01f) facing = Mathf.Sign(horizontal);
            if (!Outside && Mathf.Abs(PlayerX - 3.2f) < .65f && Mathf.Abs(vertical) > .1f) climbing = true;
            if (climbing)
            {
                PlayerX = 3.2f; PlayerY = Mathf.Clamp(PlayerY + vertical * dt * 4.2f, -1.65f, .78f);
                if (PlayerY <= -1.65f || PlayerY >= .78f) climbing = false;
            }
            else
            {
                float speed = 6.2f * State.WalkMultiplier * (Carry > 0 ? .92f : 1);
                PlayerX += horizontal * dt * speed;
                if (Outside) PlayerX = Mathf.Clamp(PlayerX, State.Distance - 22, State.Distance + 24);
                else PlayerX = Mathf.Clamp(PlayerX, PlayerY > 0 ? 2.5f : -8.6f, 8.6f);
            }
        }

        public void Interact()
        {
            if (Driving || Repairing || climbing || State.Arrived) return;
            if (Outside)
            {
                float local = PlayerX - State.Distance;
                if (Mathf.Abs(Mathf.Abs(local) - 9.25f) < 1.15f && State.Speed < .1f)
                { Outside = false; PlayerX = local < 0 ? -8.4f : 8.4f; PlayerY = -1.65f; gatherTime = 0; return; }
                if (Carry == 0)
                    for (int i = 0; i < pickups.Count; i++)
                        if (!pickups[i].Inside && pickups[i].Root != null && Mathf.Abs(pickups[i].Root.position.x - PlayerX) < 1.1f)
                        { Carry = pickups[i].Kind; Destroy(pickups[i].Root.gameObject); pickups.RemoveAt(i); sound.Hit(true); return; }
                return;
            }
            if (Carry == 0)
                for (int i = 0; i < pickups.Count; i++)
                    if (pickups[i].Inside && pickups[i].Root != null && Mathf.Abs(pickups[i].Root.localPosition.x - PlayerX) < .8f && Mathf.Abs(pickups[i].Root.localPosition.y - PlayerY - .25f) < .3f)
                    { Carry = pickups[i].Kind; Destroy(pickups[i].Root.gameObject); pickups.RemoveAt(i); sound.Hit(true); return; }
            if (PlayerY > 0)
            {
                if (Mathf.Abs(PlayerX - 6.6f) < 1.35f)
                {
                    Driving = true; PlayerX = 6.05f; facing = 1; sound.Cue();
                    LastAction = "W/S 출력, SPACE 브레이크, E 자리 떠나기";
                }
                else if (Mathf.Abs(PlayerX - 4.45f) < .85f) { sound.Radio(); radioVisible = 8; }
                return;
            }
            if (Mathf.Abs(PlayerX) > 8.15f)
            {
                if (State.Speed > .1f) { sound.Hit(false); LastAction = "먼저 열차를 멈춰야 한다."; return; }
                Outside = true; State.Brake = true; PlayerX = State.Distance + Mathf.Sign(PlayerX) * 9.7f; PlayerY = -2.62f; return;
            }
            if (Mathf.Abs(PlayerX + 7.5f) < .85f)
            {
                if (Carry != 0) { if (Carry == 1) State.Coal++; else State.Scrap++; Carry = 0; }
                else if (State.Coal > 0) { State.Coal--; Carry = 1; }
                else if (State.Scrap > 0) { State.Scrap--; Carry = 2; }
                sound.Hit(true); return;
            }
            if (Mathf.Abs(PlayerX + 5.2f) < 1)
            {
                if (Carry > 0 && State.LoadFuel(Carry == 1)) { Carry = 0; fuelFlash = 1; sound.Hit(true); }
                else { sound.Hit(false); LastAction = Carry == 0 ? "연료를 손에 들고 화실로 가져오기" : "화실이 가득 찼거나 화재 중이다."; }
                return;
            }
            if (Mathf.Abs(PlayerX + 1.35f) < 1.25f)
            {
                if (State.Fire) State.Extinguish(); else State.Vent = !State.Vent;
                sound.Hit(true); return;
            }
            if (Mathf.Abs(PlayerX - RepairStationX) < 1.05f)
            {
                if (State.Integrity >= .99f) { sound.Hit(true); return; }
                Repairing = true; repairTimer = -.75f; RepairPhase = 0;
                LastAction = "바늘이 초록 구간에 들어오면 SPACE. E로 중단.";
            }
        }
        public bool ResolveRepair(float phase)
        {
            bool success = State.Repair(phase); sound.Hit(success); feedback = .35f;
            needle.GetChild(0).GetComponent<SpriteRenderer>().color = success ? C("#AEE0AF") : C("#F57C59");
            repairTimer = -.8f; RepairPhase = 0;
            if (State.Integrity >= .99f) Repairing = false;
            LastAction = success ? "수리 성공" : "타이밍 실패 · 다시 시도"; return success;
        }
        void FinishRepair() => ResolveRepair(RepairPhase);
        public void Drop()
        {
            if (Carry == 0) return;
            if (Outside) AddPickup(PlayerX + facing * .7f, Carry);
            else AddPickup(Mathf.Clamp(PlayerX + facing * .5f, -8.5f, 8.5f), Carry, true, PlayerY + .25f);
            Carry = 0;
        }
        void OutsideWork(float dt)
        {
            if (State.Blocked && Mathf.Abs(PlayerX - (TrainState.SnowStop + 11)) < 2.3f)
            {
                State.Shovel(dt); gatherTime += dt;
                if (gatherTime > .5f) { gatherTime = 0; sound.Hit(true); }
                return;
            }
            // Emergency tinder can always be dug out beside a stopped train.
            // Physical carrying limits this to one piece per trip.
            if (Carry == 0 && State.Speed < .1f && Mathf.Abs(PlayerX - State.Distance) >= 10.5f)
            {
                gatherTime += dt;
                if (gatherTime >= 3) { Carry = 2; gatherTime = 0; sound.Hit(true); LastAction = "눈 아래에서 땔감을 찾았다."; }
            }
        }
        void Present(float dt)
        {
            train.position = new Vector3(State.Distance, 0, 0);
            far.position = new Vector3(State.Distance * .86f, 0, 0);
            middle.position = new Vector3(State.Distance * .55f, 0, 0);
            person.position = new Vector3(Outside ? PlayerX : State.Distance + PlayerX, PlayerY, 0);
            person.localScale = new Vector3(facing, 1, 1);
            carried.gameObject.SetActive(Carry > 0);
            breath.gameObject.SetActive(Outside || State.Warmth < 25);
            breath.localScale = new Vector3(.25f + Mathf.PingPong(clock * .5f, .4f), .12f, 1);
            UpdateCamera(dt);
            foreach (var w in wheels) w.Rotate(0, 0, -State.Speed * dt * 90);
            enginePhase += dt * (State.EngineRunning ? 40 + State.Throttle * 260 : 0);
            piston.localRotation = Quaternion.Euler(0, 0, enginePhase);
            lever.localRotation = Quaternion.Euler(0, 0, 40 - State.Throttle * 80);
            lever.GetChild(1).GetComponent<SpriteRenderer>().color = State.Brake ? C("#BE594A") : C("#97C1A0");
            flame.gameObject.SetActive(State.Fuel > 0 || State.Fire);
            flame.localScale = new Vector3(.85f + Mathf.Sin(clock * 17) * .08f, State.Fire ? 2.4f : .35f + State.Throttle * .5f, 1);
            flame.GetComponent<SpriteRenderer>().color = State.Fire ? C("#FF6342") : C("#EDAD62");
            engineFire.gameObject.SetActive(State.Fire);
            engineFire.localScale = new Vector3(1.1f + Mathf.Sin(clock * 18) * .1f, 1.8f + Mathf.Sin(clock * 13) * .3f, 1);
            steam.gameObject.SetActive(State.Vent);
            steam.localScale = new Vector3(.5f + Mathf.PingPong(clock, .5f), .9f, 1);
            coolingValve.GetComponent<SpriteRenderer>().color = State.Vent ? C("#A0C4BC") : C("#B45F4D");
            SetColumn(fuelColumn, State.Fuel / 100, -.35f, 1.7f);
            SetColumn(heatColumn, State.Heat / 110, .15f, 1.25f);
            gap.gameObject.SetActive(State.Integrity < .9f);
            for (int i = 0; i < stock.Count; i++) stock[i].gameObject.SetActive(i < State.Coal + State.Scrap);
            for (int i = 0; i < bolts.Count; i++) bolts[i].GetComponent<SpriteRenderer>().color = State.Integrity >= (i + 1) * .32f ? C("#A3C79D") : C("#8D5A4B");
            needle.localRotation = Quaternion.Euler(0, 0, -RepairPhase * 360);
            if (feedback <= 0) needle.GetChild(0).GetComponent<SpriteRenderer>().color = Repairing ? C(worldArt != null ? "#28343B" : "#FFF0BD") : C("#8B978A");
            radioLamp.GetComponent<SpriteRenderer>().color = radioVisible > 0 && Mathf.Sin(clock * 12) > 0 ? C("#FFE2A0") : C("#617969");
            snowBank.localScale = new Vector3(1, Mathf.Max(.03f, State.Snow), 1);
            snowBank.gameObject.SetActive(State.Snow > 0);
            for (int i = 0; i < smoke.Count; i++)
            {
                float t = Mathf.Repeat(clock * .22f + i / (float)smoke.Count, 1);
                smoke[i].gameObject.SetActive(State.Fuel > 0 || State.Fire);
                smoke[i].localPosition = new Vector3(-4.4f - t * (3 + State.Speed), (worldArt != null ? 3.5f : 3.85f) + t * 3, 0);
                smoke[i].localScale = Vector3.one * (.4f + t * 1.5f);
                smoke[i].GetComponent<SpriteRenderer>().color = new Color(.22f, .28f, .32f, (1 - t) * (State.Fire ? .8f : .4f));
            }
            foreach (var flake in flakes)
            {
                var p = flake.localPosition; p.x -= dt * (1.4f + State.Speed * .25f); p.y -= dt * 1.5f;
                if (p.y < -8) p.y = 12;
                if (p.x < -19) p.x = 19;
                flake.localPosition = p;
            }
            if (flakes.Count > 0) flakes[0].parent.position = new Vector3(cam.transform.position.x, 0, 0);
            UpdateFeedback(dt);
            sound.UpdateEngine(State);
        }
        static void SetColumn(Transform column, float ratio, float center, float fullHeight)
        {
            float height = Mathf.Max(.01f, fullHeight * ratio);
            column.localScale = new Vector3(column.localScale.x, height, 1);
            column.localPosition = new Vector3(column.localPosition.x, center - fullHeight * .5f + height * .5f, 0);
        }
    }
}
