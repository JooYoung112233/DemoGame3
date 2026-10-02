using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 시험장 IMGUI 공통(기준 높이 1080으로 크기를 맞춤). 정식 화면은 Unity 개발 단계(uGUI)에서 다시 만든다.
    /// 창(큰 지도, 말뚝 메뉴, 사건 고르기, 스킬)은 한 번에 하나만 열고, 여는 동안 시간을 멈추고 공격 입력을 막는다.
    /// </summary>
    public static class DungeonUi
    {
        public const float RefHeight = 1080f;

        public static float Scale => Mathf.Max(0.1f, Screen.height / RefHeight);
        /// <summary>기준 좌표계의 화면 너비.</summary>
        public static float Width => Screen.width / Scale;
        public static float Height => RefHeight;

        public static GUIStyle Title { get; private set; }
        public static GUIStyle Label { get; private set; }
        public static GUIStyle Small { get; private set; }
        public static GUIStyle Bold { get; private set; }
        public static GUIStyle Center { get; private set; }
        public static GUIStyle BigCenter { get; private set; }

        /// <summary>지금 열린 창 이름(없으면 null).</summary>
        public static string Modal { get; private set; }
        public static bool ModalOpen => Modal != null;

        static bool _ready;
        static bool _pausedByModal;

        public static void ResetStatics()
        {
            Modal = null;
            _pausedByModal = false;
            PlayerInputReader.Blocked = false;
        }

        /// <summary>OnGUI 맨 앞에서 부른다. 글꼴 크기·배율을 맞춘다.</summary>
        public static void Begin()
        {
            if (!_ready)
            {
                _ready = true;
                Title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, wordWrap = true };
                Label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
                Small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                Bold = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, wordWrap = true };
                Center = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                BigCenter = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                GUI.skin.button.fontSize = 15;
                GUI.skin.toggle.fontSize = 14;
            }
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        }

        /// <summary>창을 연다. 다른 창이 열려 있으면 false. 여는 동안 시간을 멈춘다.</summary>
        public static bool TryOpen(string name)
        {
            if (Modal != null && Modal != name) return false;
            if (Modal == name) return true;
            Modal = name;
            PlayerInputReader.Blocked = true;
            if (!TimeScaleService.Paused)
            {
                TimeScaleService.Paused = true;
                _pausedByModal = true;
            }
            return true;
        }

        public static void Close(string name)
        {
            if (Modal != name) return;
            Modal = null;
            PlayerInputReader.Blocked = false;
            if (_pausedByModal) TimeScaleService.Paused = false;
            _pausedByModal = false;
        }

        public static void Box(Rect r, float alpha = 0.82f)
        {
            var prev = GUI.color;
            GUI.color = new Color(0.07f, 0.06f, 0.05f, alpha);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void Fill(Rect r, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void Bar(Rect r, float fraction, Color fill, string text = null)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.6f));
            Fill(new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * Mathf.Clamp01(fraction), r.height - 2f), fill);
            if (!string.IsNullOrEmpty(text))
            {
                var prev = GUI.color;
                GUI.color = Color.white;
                GUI.Label(r, text, Center);
                GUI.color = prev;
            }
        }

        /// <summary>테두리만 그린다(큰 지도의 '안 간 출구' 칸 등).</summary>
        public static void Outline(Rect r, Color color, float thickness = 1f)
        {
            Fill(new Rect(r.x, r.y, r.width, thickness), color);
            Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), color);
            Fill(new Rect(r.x, r.y + thickness, thickness, r.height - thickness * 2f), color);
            Fill(new Rect(r.xMax - thickness, r.y + thickness, thickness, r.height - thickness * 2f), color);
        }

        /// <summary>초 → "m:ss".</summary>
        public static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>월드 좌표 → 기준 GUI 좌표(카메라가 없으면 null).</summary>
        public static Vector2? WorldToGui(Vector2 world)
        {
            var cam = Camera.main;
            if (!cam) return null;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return null;
            return new Vector2(sp.x, Screen.height - sp.y) / Scale;
        }

        /// <summary>등급색(2차 6-2): 일반 회색, 고급 초록, 희귀 파랑, 영웅 보라, 전설 주황.</summary>
        public static Color GradeColor(int grade)
        {
            switch (grade)
            {
                case 1: return new Color32(0x4C, 0xAF, 0x50, 0xFF);
                case 2: return new Color32(0x2F, 0x80, 0xED, 0xFF);
                case 3: return new Color32(0x9B, 0x51, 0xE0, 0xFF);
                case 4: return new Color32(0xF2, 0x99, 0x4A, 0xFF);
                default: return new Color32(0x9E, 0x9E, 0x9E, 0xFF);
            }
        }
    }
}
