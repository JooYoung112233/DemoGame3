using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Demo8
{
    public class CampaignView : MonoBehaviour
    {
        public Campaign state;
        public int selected, selectedArmyId;
        public bool domestic;
        public Camera mapCamera;
        public Material surfaceMaterial;
        public string notice = "청하에 군대가 있습니다. 풍림을 선택하여 첫 출정을 준비하세요.";
        public bool confirmBattle, confirmRestart;
        bool pointerPending;
        bool mouseWasPressed;
        Vector2 pointer, pointerOffset;
        Rect? pointerClip;
        readonly List<GameObject> tiles = new List<GameObject>();
        readonly List<Material> materials = new List<Material>();
        readonly Dictionary<int, Transform> formations = new Dictionary<int, Transform>();
        Transform world;
        Font font;
        GUIStyle title, heading, body, small, button, mapLabel;
        Vector2 logScroll, cityScroll;
        float zoom = 17f;
        Vector3 cameraTarget = new Vector3(18.1f, 0, 8.25f);
        Army SelectedArmy => state.FindArmy(0, selectedArmyId) ?? state.ArmyOf(0);
        public bool Marching => state.armies.Any(a => formations.ContainsKey(a.id) && Vector3.Distance(formations[a.id].position, ArmyPosition(a)) > .12f);
        readonly Color ink = new Color(.045f, .075f, .09f);
        readonly Color panel = new Color(.075f, .115f, .125f);
        readonly Color gold = new Color(.9f, .76f, .46f);
        readonly Color muted = new Color(.56f, .65f, .65f);
        public static Color FactionColor(int owner) => owner == 0 ? new Color(.23f, .67f, .59f) : owner == 1 ? new Color(.77f, .33f, .30f) : owner == 2 ? new Color(.8f, .64f, .3f) : new Color(.38f, .46f, .43f);
        public static Vector3 Position(int id) => new Vector3(id % Campaign.Width * 3.81f + id / Campaign.Width * 1.905f, 0, id / Campaign.Width * 3.3f);
        static Vector3 ArmyPosition(Army a) => Position(a.province) + new Vector3(.25f, .15f, -.8f);
        public static string SavePath => Path.Combine(Application.persistentDataPath, "demo8-campaign-v2.json");
        void Awake()
        {
            Application.runInBackground = true;
            state = Campaign.New();
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Noto Sans CJK KR", "Arial" }, 18);
            CreateMap(); RefreshMap();
        }
        void OnDestroy()
        {
            foreach (var m in materials) if (m != null) Destroy(m);
            if (font != null) Destroy(font);
        }
        Material Mat(Color color)
        {
            var m = surfaceMaterial != null ? new Material(surfaceMaterial) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = color; m.SetFloat("_Smoothness", .12f); materials.Add(m); return m;
        }
        GameObject Shape(PrimitiveType type, string label, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type); go.name = label; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
            var collider = go.GetComponent<Collider>(); if (collider) Destroy(collider); return go;
        }
        Mesh Hex()
        {
            var mesh = new Mesh { name = "Province hex" };
            var vertices = new Vector3[14]; var triangles = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                float a = (30 + i * 60) * Mathf.Deg2Rad;
                vertices[i] = new Vector3(Mathf.Cos(a) * 2.13f, .1f, Mathf.Sin(a) * 2.13f);
                vertices[i + 6] = new Vector3(vertices[i].x, -.18f, vertices[i].z);
            }
            vertices[12] = new Vector3(0, .1f, 0); vertices[13] = new Vector3(0, -.18f, 0);
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                triangles.AddRange(new[] { 12, j, i, 13, i + 6, j + 6, i, j, j + 6, i, j + 6, i + 6 });
            }
            mesh.vertices = vertices; mesh.triangles = triangles.ToArray(); mesh.RecalculateNormals(); return mesh;
        }
        void CreateMap()
        {
            world = new GameObject("Campaign map").transform; world.SetParent(transform);
            var backdrop = new GameObject("Background camera"); backdrop.transform.SetParent(transform);
            var backdropCamera = backdrop.AddComponent<Camera>(); backdropCamera.depth = -10; backdropCamera.cullingMask = 0;
            backdropCamera.clearFlags = CameraClearFlags.SolidColor; backdropCamera.backgroundColor = ink;
            var goCamera = new GameObject("Campaign camera"); goCamera.transform.SetParent(transform);
            mapCamera = goCamera.AddComponent<Camera>(); mapCamera.orthographic = true; mapCamera.orthographicSize = zoom;
            goCamera.AddComponent<AudioListener>();
            UpdateCamera();
            mapCamera.clearFlags = CameraClearFlags.SolidColor; mapCamera.backgroundColor = ink;
            mapCamera.nearClipPlane = .1f; mapCamera.farClipPlane = 80;
            var stone = Mat(new Color(.66f, .63f, .49f)); var roof = Mat(new Color(.22f, .27f, .26f));
            var forest = Mat(new Color(.12f, .26f, .2f)); var mountain = Mat(new Color(.4f, .45f, .4f));
            var road = Mat(new Color(.46f, .51f, .43f));
            Shape(PrimitiveType.Cube, "Outer sea", new Vector3(18, -.52f, 8), new Vector3(160, .15f, 130), Mat(new Color(.08f, .2f, .24f)), world);
            Mesh hex = Hex();
            foreach (var p in state.provinces)
            {
                var tile = new GameObject("Region " + p.id + " " + p.name); tile.transform.SetParent(world); tile.transform.position = Position(p.id);
                tile.AddComponent<MeshFilter>().sharedMesh = hex;
                tile.AddComponent<MeshRenderer>().sharedMaterial = Mat(FactionColor(p.owner));
                tile.AddComponent<MeshCollider>().sharedMesh = hex; tile.AddComponent<ProvinceHit>().id = p.id; tiles.Add(tile);
                for (int n = 0; n < 3; n++)
                {
                    var pos = new Vector3((n - 1) * .42f, .34f, .12f);
                    Shape(PrimitiveType.Cube, "City building", pos, new Vector3(.35f, .46f, .45f), stone, tile.transform);
                    Shape(PrimitiveType.Cube, "Roof", pos + Vector3.up * .25f, new Vector3(.43f, .1f, .53f), roof, tile.transform);
                }
                Shape(PrimitiveType.Cube, "Keep", new Vector3(0, .65f, .6f), new Vector3(.48f, 1.1f, .48f), stone, tile.transform);
                for (int n = 0; n < 7; n++)
                {
                    float x = -1.5f + n * .45f;
                    if (p.terrain == 1) Shape(PrimitiveType.Capsule, "Forest", new Vector3(x, .45f, 1.05f), new Vector3(.30f, .4f + n % 2 * .1f, .32f), forest, tile.transform);
                    if (p.terrain == 2)
                    {
                        var rock = Shape(PrimitiveType.Cube, "Ridge", new Vector3(x, .40f + n % 2 * .2f, 1.05f), new Vector3(.6f, .65f + n % 2 * .25f, .65f), mountain, tile.transform);
                        rock.transform.localRotation = Quaternion.Euler(0, 30, 35);
                    }
                    if (p.terrain == 0) Shape(PrimitiveType.Cube, "Fields", new Vector3(x, .13f, 1.0f), new Vector3(.4f, .035f, .6f), road, tile.transform);
                }
            }
            foreach (var a in state.provinces)
                foreach (var b in state.provinces.Where(p => p.id > a.id && Campaign.Adjacent(a.id, p.id)))
                {
                    Vector3 start = Position(a.id) + Vector3.up * .12f, end = Position(b.id) + Vector3.up * .12f;
                    var path = Shape(PrimitiveType.Cube, "Road", (start + end) / 2, new Vector3(.055f, .025f, Vector3.Distance(start, end)), road, world);
                    path.transform.rotation = Quaternion.LookRotation(end - start);
                }
            var lightGo = new GameObject("Sun"); lightGo.transform.SetParent(transform); lightGo.transform.rotation = Quaternion.Euler(50, -35, 0);
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.shadows = LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.5f, .55f, .6f);
        }
        public void RefreshMap()
        {
            foreach (var p in state.provinces)
            {
                var ground = p.terrain == 2 ? new Color(.40f, .43f, .34f) : p.terrain == 1 ? new Color(.22f, .36f, .25f) : new Color(.38f, .43f, .26f);
                var color = Color.Lerp(ground, FactionColor(p.owner), p.owner < 0 ? .12f : .32f);
                if (p.id == selected) color = Color.Lerp(color, new Color(.94f, .85f, .58f), .4f);
                tiles[p.id].GetComponent<Renderer>().sharedMaterial.color = color;
            }
            if (flagMaterials == null || flagMaterials.Length != 3 || flagMaterials.Any(m => m == null)) flagMaterials = Enumerable.Range(0, 3).Select(i => Mat(FactionColor(i))).ToArray();
            foreach (int id in formations.Keys.Where(id => !state.armies.Any(a => a.id == id)).ToArray()) { Destroy(formations[id].gameObject); formations.Remove(id); }
            foreach (var a in state.armies)
            {
                if (formations.ContainsKey(a.id)) continue;
                var formation = new GameObject("Army " + a.id + " marching formation").transform;
                formation.SetParent(world); formation.position = ArmyPosition(a); formations[a.id] = formation;
                Shape(PrimitiveType.Cylinder, "Standard pole", new Vector3(0, .8f, 0), new Vector3(.04f, .7f, .04f), flagMaterials[a.owner], formation);
                Shape(PrimitiveType.Cube, "Banner", new Vector3(.2f, 1.25f, 0), new Vector3(.5f, .32f, .04f), flagMaterials[a.owner], formation);
                for (int n = 0; n < 16; n++)
                {
                    var soldier = new GameObject("Soldier " + n).transform; soldier.SetParent(formation, false);
                    soldier.localPosition = new Vector3((n % 4 - 1.5f) * .23f, 0, (n / 4 - 1.5f) * .26f);
                    Shape(PrimitiveType.Capsule, "Body", new Vector3(0, .23f, 0), new Vector3(.13f, .16f, .13f), flagMaterials[a.owner], soldier);
                    Shape(PrimitiveType.Sphere, "Head", new Vector3(0, .44f, 0), new Vector3(.105f, .105f, .105f), flagMaterials[a.owner], soldier);
                    Shape(PrimitiveType.Cylinder, "Spear", new Vector3(.12f, .38f, 0), new Vector3(.018f, .36f, .018f), flagMaterials[a.owner], soldier);
                }
            }
        }
        void UpdateCamera()
        {
            mapCamera.orthographicSize = zoom;
            mapCamera.transform.position = cameraTarget + new Vector3(0, 25, -21);
            mapCamera.transform.LookAt(cameraTarget);
        }
        public void FocusArmy()
        {
            var a = SelectedArmy; if (a == null) return;
            Select(a.province); cameraTarget = Position(a.province); zoom = 6; domestic = false; UpdateCamera();
        }
        public void Overview() { zoom = 17; cameraTarget = new Vector3(18.1f, 0, 8.25f); domestic = false; UpdateCamera(); }
        Material[] flagMaterials;
        float Scale => Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
        Vector2 Offset => new Vector2((Screen.width - 1440 * Scale) / 2, (Screen.height - 900 * Scale) / 2);
        Vector2 ToDesign(Vector2 bottomLeft) => new Vector2((bottomLeft.x - Offset.x) / Scale, (Screen.height - bottomLeft.y - Offset.y) / Scale);
        readonly Rect mapRect = new Rect(265, 175, 840, 510);
        void Update()
        {
            mapCamera.rect = new Rect((Offset.x + mapRect.x * Scale) / Screen.width, (Screen.height - Offset.y - mapRect.yMax * Scale) / Screen.height, mapRect.width * Scale / Screen.width, mapRect.height * Scale / Screen.height);
            mapCamera.enabled = !domestic;
            foreach (var a in state.armies)
            {
                if (!formations.TryGetValue(a.id, out var formation)) continue;
                Vector3 destination = ArmyPosition(a), delta = destination - formation.position;
                bool walking = delta.magnitude > .04f;
                if (walking) { formation.rotation = Quaternion.Slerp(formation.rotation, Quaternion.LookRotation(delta), Time.deltaTime * 8); formation.position = Vector3.MoveTowards(formation.position, destination, Time.deltaTime * 3.2f); }
                for (int n = 2; n < formation.childCount; n++)
                {
                    var soldier = formation.GetChild(n); var pos = soldier.localPosition;
                    pos.y = walking ? Mathf.Abs(Mathf.Sin(Time.time * 15 + n * .9f)) * .07f : 0; soldier.localPosition = pos;
                    soldier.localRotation = Quaternion.Euler(walking ? Mathf.Sin(Time.time * 15 + n) * 8 : 0, 0, 0);
                }
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                bool down = mouse.leftButton.isPressed;
                if (down && !mouseWasPressed) { pointer = ToDesign(mouse.position.ReadValue()); pointerPending = true; }
                mouseWasPressed = down;
            }
            if (mouse != null && !domestic && !confirmBattle && !confirmRestart && mapRect.Contains(ToDesign(mouse.position.ReadValue())))
            {
                zoom = Mathf.Clamp(zoom - mouse.scroll.ReadValue().y * .004f, 4, 23);
                if (mouse.middleButton.isPressed)
                { Vector2 drag = mouse.delta.ReadValue(); cameraTarget += new Vector3(-drag.x, 0, -drag.y) * zoom / 400; }
                if (mouse.leftButton.wasPressedThisFrame && Physics.Raycast(mapCamera.ScreenPointToRay(mouse.position.ReadValue()), out var hit))
                { var region = hit.collider.GetComponent<ProvinceHit>(); if (region != null) Select(region.id); }
            }
            var k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame) { confirmBattle = confirmRestart = false; }
            if (!confirmBattle && !confirmRestart)
            {
                if (k.spaceKey.wasPressedThisFrame && !state.Finished && !Marching) Act(state.EndTurn());
                if (k.f5Key.wasPressedThisFrame) Save();
                if (k.f9Key.wasPressedThisFrame) Load();
                if (k.homeKey.wasPressedThisFrame) FocusArmy();
                if (k.tabKey.wasPressedThisFrame) domestic = !domestic;
                if (!domestic)
                {
                    float x = (k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), z = (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0);
                    cameraTarget += new Vector3(x, 0, z) * Time.deltaTime * zoom;
                    cameraTarget.x = Mathf.Clamp(cameraTarget.x, -3, 40); cameraTarget.z = Mathf.Clamp(cameraTarget.z, -5, 22);
                }
            }
            UpdateCamera();
        }
        public void Select(int id) { selected = id; if (state.At(id)?.owner == 0) selectedArmyId = state.At(id).id; confirmBattle = false; RefreshMap(); }
        public void SnapArmies() { foreach (var a in state.armies) if (formations.TryGetValue(a.id, out var group)) group.position = ArmyPosition(a); }
        public void NewCampaign()
        {
            state = Campaign.New(); confirmBattle = confirmRestart = domestic = false; selectedArmyId = 0;
            pointerPending = false; Select(0); SnapArmies(); Overview();
            notice = "월드맵을 둘러보거나 Tab으로 내정을 여세요. Home으로 군단에 접근합니다.";
        }
        public void Act(string message) { notice = message; RefreshMap(); }
        public void Save()
        {
            try
            {
                string path = SavePath, temp = path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(temp, JsonUtility.ToJson(state, true));
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
                notice = "캠페인을 저장했습니다. F9로 이어할 수 있습니다.";
            }
            catch (Exception e) { notice = "저장 실패: " + e.Message; }
        }
        public void Load()
        {
            try
            {
                if (!File.Exists(SavePath)) { notice = "저장된 캠페인이 없습니다. F5로 먼저 저장하십시오."; return; }
                var loaded = JsonUtility.FromJson<Campaign>(File.ReadAllText(SavePath));
                if (loaded == null || !loaded.Valid()) { notice = "저장 형식이 올바르지 않습니다. 현재 게임을 유지합니다."; return; }
                state = loaded; confirmBattle = confirmRestart = false; Select(state.ArmyOf(0)?.province ?? state.provinces.Find(p => p.owner == 0)?.id ?? 0);
                SnapArmies();
                notice = state.turn + "턴 저장을 불러왔습니다.";
            }
            catch (Exception e) { notice = "불러오기 실패: " + e.Message; }
        }
        void Styles()
        {
            if (body != null) return;
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 17, wordWrap = true }; body.normal.textColor = new Color(.85f, .88f, .83f);
            small = new GUIStyle(body) { fontSize = 14 }; small.normal.textColor = muted;
            heading = new GUIStyle(body) { fontSize = 22, fontStyle = FontStyle.Bold };
            title = new GUIStyle(heading) { fontSize = 34 }; title.normal.textColor = gold;
            button = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            mapLabel = new GUIStyle(button) { fontSize = 15 };
        }
        void Fill(Rect r, Color color) { var old = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = old; }
        void Text(float x, float y, float w, float h, string text, GUIStyle style = null) => GUI.Label(new Rect(x, y, w, h), text, style ?? body);
        void Panel(float x, float y, float w, float h)
        { Fill(new Rect(x, y, w, h), panel); Fill(new Rect(x, y, w, 2), new Color(.26f, .33f, .32f)); }
        bool Button(float x, float y, float w, float h, string text, bool enabled = true, bool primary = false)
        {
            Rect r = new Rect(x, y, w, h); bool hover = Mouse.current != null && r.Contains(ToDesign(Mouse.current.position.ReadValue()) - pointerOffset);
            Fill(r, !enabled ? new Color(.12f, .16f, .17f) : primary ? new Color(.20f, .40f, .36f) : hover ? new Color(.25f, .32f, .31f) : new Color(.16f, .23f, .24f));
            Fill(new Rect(x, y + h - 2, w, 2), enabled ? primary ? gold : new Color(.35f, .43f, .41f) : new Color(.2f, .23f, .23f));
            var previous = GUI.color; GUI.color = enabled ? Color.white : new Color(.48f, .52f, .52f);
            bool old = GUI.enabled; GUI.enabled = old && enabled;
            GUI.Label(r, text, button);
            bool result = GUI.enabled && pointerPending && Event.current.type == EventType.Repaint && (!pointerClip.HasValue || pointerClip.Value.Contains(pointer)) && r.Contains(pointer - pointerOffset);
            if (result) pointerPending = false;
            GUI.enabled = old; GUI.color = previous; return result;
        }
        void OnGUI()
        {
            Styles(); var original = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(Offset.x, Offset.y, 0), Quaternion.identity, Vector3.one * Scale);
            // Leave the camera viewport uncovered while framing it with the interface.
            Fill(new Rect(0, 0, 1440, 166), ink); Fill(new Rect(0, 166, 265, 734), ink);
            Fill(new Rect(1105, 166, 335, 734), ink); Fill(new Rect(265, 685, 840, 215), ink);
            Text(22, 15, 700, 48, "청하연맹  /  변경의 군주", title);
            Text(24, 65, 1000, 25, "DEMO 08     ·     48개 지역  /  여러 군단  /  수도 보급망  /  내정과 국정", small);
            bool modal = confirmBattle || confirmRestart; GUI.enabled = !modal;
            if (Button(680, 24, 145, 39, "월드맵", true, !domestic)) domestic = false;
            if (Button(835, 24, 185, 39, "내정 / 국정  Tab", true, domestic)) domestic = true;
            if (Button(1040, 24, 112, 39, "저장  F5")) Save();
            if (Button(1162, 24, 112, 39, "불러오기 F9")) Load();
            if (Button(1284, 24, 132, 39, "새 캠페인")) confirmRestart = true;
            Panel(20, 102, 1396, 53);
            state.Economy(0, out int goldDelta, out int foodDelta);
            Text(36, 114, 140, 32, "제 " + state.turn + " 턴", heading);
            Text(180, 116, 270, 30, "금  " + state.factions[0].gold + "   (" + Campaign.Signed(goldDelta) + "/턴)");
            Text(460, 116, 290, 30, "식량  " + state.factions[0].food + "   (" + Campaign.Signed(foodDelta) + "/턴)");
            Text(790, 116, 220, 30, "영토  " + state.Territory(0) + " / 48");
            Text(1080, 116, 320, 30, "패권 유지  " + state.heldTurns + " / 3턴");
            DrawLeft(); if (domestic) DrawDomestic(); else DrawMapLabels(); DrawProvince(); DrawBottom();
            GUI.enabled = true;
            if (confirmBattle) DrawBattle();
            else if (confirmRestart) DrawRestart();
            else if (state.Finished)
            {
                Fill(new Rect(282, 355, 806, 146), new Color(.035f, .055f, .06f, .98f));
                Text(308, 380, 755, 74, state.outcome, heading);
                Text(308, 453, 740, 28, "상단의 새 캠페인으로 다시 시작하거나 저장을 불러오십시오.", small);
            }
            if (Event.current.type == EventType.Repaint) pointerPending = false;
            GUI.matrix = original;
        }
        void DrawLeft()
        {
            Panel(20, 175, 230, 210); Text(36, 190, 200, 34, "천하의 세력", heading);
            for (int i = 0; i < 3; i++)
            {
                Fill(new Rect(36, 243 + i * 43, 7, 25), FactionColor(i));
                Text(52, 242 + i * 43, 185, 30, state.factions[i].name + "   " + state.Territory(i));
            }
            Panel(20, 402, 230, 275); Text(36, 418, 195, 34, "군단 지휘", heading);
            var army = SelectedArmy;
            if (army == null) Text(36, 465, 195, 100, "군대가 없습니다.\n우리 도시에서 병력을 모집하여 재편성하십시오.");
            else
            {
                int row = 0;
                foreach (var a in state.armies.Where(a => a.owner == 0))
                {
                    if (Button(36, 458 + row * 31, 198, 27, "제" + (a.id + 1) + "군  " + state.provinces[a.province].name + "  " + a.Count, true, a.id == army.id)) { selectedArmyId = a.id; FocusArmy(); }
                    row++;
                }
                Text(36, 557, 198, 48, "보 " + army.infantry + " / 궁 " + army.archers + " / 기 " + army.cavalry + "\n행동 " + army.actions + " · " + (state.Supplied(0).Contains(army.province) ? "보급 연결" : "보급 단절"), small);
                if (Button(36, 608, 62, 26, "기동", !state.Finished, army.stance == 0)) Act(state.SetStance(army.id, 0));
                if (Button(104, 608, 62, 26, "진지", !state.Finished, army.stance == 1)) Act(state.SetStance(army.id, 1));
                if (Button(172, 608, 62, 26, "강습", !state.Finished, army.stance == 2)) Act(state.SetStance(army.id, 2));
                if (Button(36, 642, 198, 26, "군단으로 이동  Home")) FocusArmy();
            }
            Panel(20, 695, 230, 182); Text(36, 708, 200, 28, "승리 조건", heading);
            Text(36, 748, 198, 55, "12개 지역을 확보하고\n3턴 동안 유지하세요.");
            Text(36, 807, 198, 64, "WASD / 휠 드래그: 지도 이동\n휠: 확대 · Home: 선택 군단\nTab: 월드맵 / 내정", small);
        }
        void DrawMapLabels()
        {
            Text(280, 173, 590, 28, "월드맵  /  평지 이동 1 · 산지 2 · 전투 시 행동 종료", small);
            if (Button(965, 175, 125, 27, "전체 지도")) Overview();
            foreach (var p in state.provinces)
            {
                Vector3 point = mapCamera.WorldToScreenPoint(Position(p.id) + new Vector3(0, .1f, -.72f));
                Vector2 ui = ToDesign(point); if (ui.x < 310 || ui.x > 1060 || ui.y < 211 || ui.y > 606) continue;
                bool detail = zoom < 10;
                Rect rect = new Rect(ui.x - (detail ? 61 : 28), ui.y, detail ? 122 : 56, detail ? 42 : 24);
                Fill(rect, p.id == selected ? new Color(.23f, .29f, .24f, .98f) : new Color(.06f, .10f, .11f, .93f));
                Fill(new Rect(rect.x, rect.y, rect.width, 2), FactionColor(p.owner));
                string label = p.name + (detail && state.At(p.id) != null ? " [군]" : "") + (detail ? "\n" + Campaign.TerrainName(p.terrain) + " · 수비 " + p.garrison : "");
                GUI.Label(rect, label, mapLabel);
                if (GUI.enabled && pointerPending && Event.current.type == EventType.Repaint && rect.Contains(pointer)) { Select(p.id); pointerPending = false; }
                if (SelectedArmy != null && state.MoveError(0, p.id, SelectedArmy.id).Length == 0) Fill(new Rect(rect.x, rect.yMax, rect.width, 2), gold);
            }
            foreach (var army in state.armies)
            {
                if (!formations.TryGetValue(army.id, out var formation)) continue;
                Vector2 at = ToDesign(mapCamera.WorldToScreenPoint(formation.position + Vector3.up * 1.7f));
                if (at.x < 300 || at.x > 1070 || at.y < 216 || at.y > 605) continue;
                Fill(new Rect(at.x - 25, at.y - 20, 50, 20), FactionColor(army.owner));
                GUI.Label(new Rect(at.x - 25, at.y - 20, 50, 20), army.Count.ToString(), mapLabel);
            }
        }
        void DrawDomestic()
        {
            Panel(265, 175, 840, 466);
            var f = state.factions[0]; bool live = !state.Finished;
            Text(283, 191, 700, 36, "내정 · 국정 운영", heading);
            Text(283, 234, 350, 28, "세율  /  수입과 민심의 균형");
            string[] taxes = { "경감", "보통", "중과" };
            for (int i = 0; i < 3; i++) if (Button(283 + i * 108, 273, 100, 33, taxes[i], live, f.tax == i)) Act(state.SetTax(i));
            Text(283, 316, 343, 38, "경감: 수입 -20%, 치안 +5\n중과: 수입 +20%, 치안 -8 / 매 턴", small);
            int[] levels = { f.agriculture, f.commerce, f.logistics }; string[] techs = { "농법", "상업", "병참" };
            Text(635, 234, 455, 28, "국가 연구  /  매 턴 하나");
            for (int i = 0; i < 3; i++) if (Button(635 + i * 152, 273, 144, 33, techs[i] + " " + levels[i] + " / " + (300 + levels[i] * 150), live && levels[i] < 3 && f.researchedTurn != state.turn)) Act(state.Research(i));
            Text(635, 316, 455, 38, "농법: 식량 +15% / 상업: 금 +10% / 단계\n병참: 단절 손실 감소, 2단계에 행동력 +1", small);
            var p = state.provinces[selected];
            Text(283, 356, 700, 28, p.name + " 도시 방침 · 도로와 성벽");
            string[] focuses = { "균형", "농업", "상업", "군정" };
            for (int i = 0; i < 4; i++) if (Button(283 + i * 108, 392, 100, 31, focuses[i], live && p.owner == 0, p.focus == i)) Act(state.SetFocus(selected, i));
            if (Button(722, 392, 177, 31, "도로 " + p.road + " / 금" + (140 + p.road * 100), live && p.owner == 0 && p.road < 3)) Act(state.Build(selected, 3));
            if (Button(909, 392, 177, 31, "성벽 " + p.walls + " / 금" + (140 + p.walls * 100), live && p.owner == 0 && p.walls < 3)) Act(state.Build(selected, 4));
            Text(283, 434, 804, 36, "농업: 식량 +35% / 상업: 금 +30%, 치안 -3 / 군정: 치안 +5, 수비 +8\n도로: 산지 이동 1, 수입 증가 / 성벽: 수비대 방어 +20% / 단계", small);
            Text(283, 480, 804, 25, "도시              치안       수비        금 / 식량         방침            수도 보급", small);
            var cities = state.provinces.Where(x => x.owner == 0).ToList(); var supplied = state.Supplied(0);
            cityScroll = GUI.BeginScrollView(new Rect(280, 509, 807, 116), cityScroll, new Rect(0, 0, 778, Math.Max(115, cities.Count * 31)));
            pointerOffset = new Vector2(280, 509) - cityScroll;
            pointerClip = new Rect(280, 509, 790, 116);
            for (int i = 0; i < cities.Count; i++)
            {
                var city = cities[i];
                if (Button(0, i * 31, 115, 27, city.name, true, city.id == selected)) Select(city.id);
                Text(130, i * 31, 638, 29, city.order + "          " + city.garrison + "         " + state.GoldIncome(city) + " / " + state.FoodIncome(city) + "         " + focuses[city.focus] + "          " + (supplied.Contains(city.id) ? "연결" : "단절"), small);
            }
            GUI.EndScrollView();
            pointerOffset = Vector2.zero;
            pointerClip = null;
        }
        void DrawProvince()
        {
            var p = state.provinces[selected]; bool own = p.owner == 0 && !state.Finished;
            Panel(1120, 175, 296, 702); Fill(new Rect(1120, 175, 296, 4), FactionColor(p.owner));
            Text(1138, 191, 256, 40, p.name, title); Text(1138, 239, 256, 28, state.OwnerName(p.owner) + "  /  " + Campaign.TerrainName(p.terrain), small);
            Text(1138, 279, 256, 32, "치안 " + p.order + "    수비대 " + p.garrison);
            Fill(new Rect(1138, 316, 256, 6), new Color(.2f, .25f, .25f)); Fill(new Rect(1138, 316, 256 * p.order / 100f, 6), p.order < 40 ? FactionColor(1) : FactionColor(0));
            Text(1138, 338, 256, 49, "생산  금 " + state.GoldIncome(p) + " / 식량 " + state.FoodIncome(p) + "\n수비 유지비 별도 · 치안 0이면 반란", small);
            if (p.owner == 0)
            {
                Text(1138, 397, 256, 30, "도시 개발", heading);
                int[] levels = { p.farm, p.market, p.barracks }; string[] names = { "농지", "시장", "병영" };
                for (int i = 0; i < 3; i++)
                {
                    string label = names[i] + " " + levels[i] + " → " + (levels[i] < 3 ? (levels[i] + 1) + "   금 " + (140 + levels[i] * 100) : "최대");
                    if (Button(1138, 434 + i * 39, 256, 33, label, own && levels[i] < 3 && p.builtTurn != state.turn)) Act(state.Build(selected, i));
                }
                Text(1138, 554, 256, 23, "농지: 식량 +40 · 시장: 금 최대 +45", small);
                if (Button(1138, 585, 125, 34, "구휼  금60", own)) Act(state.Pacify(selected));
                if (Button(1269, 585, 125, 34, "수비 +30", own)) Act(state.Reinforce(selected));
                Text(1138, 624, 256, 23, "수비 보강: 금75 / 식량25", small);
                Text(1138, 659, 256, 28, "군대 모집", heading);
                string[] recruit = { "보병 +40    금100", "궁병 +40    금130", "기병 +20    금180" };
                for (int i = 0; i < 3; i++) if (Button(1138, 697 + i * 38, 256, 32, recruit[i], own)) Act(state.Recruit(selected, i));
                Text(1138, 818, 256, 44, "모집당 식량40 · 새 군단 추가 금150\n새 군단·궁병·기병: 병영 필요", small);
            }
            else
            {
                Text(1138, 406, 256, 32, "출정 정보", heading);
                var f = state.Preview(0, selected, SelectedArmy?.id ?? -1);
                Text(1138, 453, 256, 72, "우리 전력  " + f.attack + "\n적 방어력  " + f.defense);
                Text(1138, 540, 256, 100, "평야는 기병, 숲은 궁병에 유리합니다. 산지는 수비대의 방어력을 높입니다.");
                Text(1138, 659, 256, 95, "점령 직후 치안은 35입니다. 구휼·수비대·군정 방침으로 안정시키세요. 수도와 영토가 끊기면 군대 보급이 단절됩니다.", small);
                if (p.owner > 0)
                {
                    int left = Math.Max(0, state.factions[p.owner].ceasefireUntil - state.turn + 1);
                    if (Button(1138, 782, 256, 37, left > 0 ? "휴전 " + left + "턴 남음" : "3턴 휴전 교섭 / 금250", !state.Finished && left == 0)) Act(state.Truce(p.owner));
                }
            }
        }
        void DrawBottom()
        {
            string error = Marching ? "군대가 행군 중입니다." : state.MoveError(0, selected, SelectedArmy?.id ?? -1); bool own = state.provinces[selected].owner == 0;
            if (Button(280, 651, 248, 39, own ? "선택 지역으로 이동" : "출정 / 전투 예상 확인", error.Length == 0, true))
            { if (own) Act(state.Move(0, selected, SelectedArmy?.id ?? -1)); else confirmBattle = true; }
            if (Button(844, 651, 246, 39, "턴 종료   Space", !state.Finished && !Marching, true)) Act(state.EndTurn());
            Panel(265, 705, 840, 172);
            Text(282, 715, 804, 45, notice, body);
            Text(282, 763, 804, 22, error.Length > 0 ? error : "노란 밑줄: 진군 가능 · 수도 보급 단절: 공격 -30%, 매 턴 손실", small);
            logScroll = GUI.BeginScrollView(new Rect(282, 794, 803, 71), logScroll, new Rect(0, 0, 779, Math.Max(68, state.journal.Count * 26)));
            for (int i = 0; i < state.journal.Count; i++) GUI.Label(new Rect(0, i * 26, 768, 26), state.journal[state.journal.Count - 1 - i], small);
            GUI.EndScrollView();
        }
        void DrawBattle()
        {
            Fill(new Rect(0, 0, 1440, 900), new Color(0, 0, 0, .73f)); Panel(420, 232, 600, 420);
            var p = state.provinces[selected]; var f = state.Preview(0, selected, SelectedArmy?.id ?? -1);
            Text(450, 252, 540, 46, p.name + " 공략", title);
            Text(450, 319, 540, 32, "공격 전력 " + f.attack + "   /   방어 전력 " + f.defense, heading);
            Text(450, 370, 540, 70, (f.victory ? "승리 예상" : "패배 예상") + " · 병력 " + f.losses + "명 손실 예상\n지형·병종·수비대를 반영한 확정 자동 전투입니다.");
            Text(450, 455, 540, 85, "승리하면 이 지역을 점령합니다. 점령 직후 치안이 낮아 구휼과 주둔이 필요합니다. 패배하면 생존 병력은 출발지로 돌아갑니다.");
            if (Button(450, 570, 252, 47, "공격 실행", true, true)) { confirmBattle = false; Act(state.Move(0, selected, SelectedArmy?.id ?? -1)); }
            if (Button(720, 570, 270, 47, "취소  Esc")) confirmBattle = false;
        }
        void DrawRestart()
        {
            Fill(new Rect(0, 0, 1440, 900), new Color(0, 0, 0, .73f)); Panel(440, 310, 560, 260);
            Text(468, 333, 505, 42, "새 캠페인", title);
            Text(468, 399, 505, 62, "저장하지 않은 현재 진행을 끝내고 처음부터 시작합니다. 기존 저장 파일은 유지됩니다.");
            if (Button(468, 495, 238, 44, "처음부터 시작", true, true)) NewCampaign();
            if (Button(724, 495, 246, 44, "취소")) confirmRestart = false;
        }
    }
    public class ProvinceHit : MonoBehaviour { public int id; }
}
