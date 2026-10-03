using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 던전 시험장 IMGUI 공통(기준 높이 900으로 크기를 맞춤). 정식 화면은 Unity 개발 단계(uGUI)에서 다시 만든다.
    /// 창(큰 지도, 말뚝 메뉴, 사건 고르기, 스킬)은 한 번에 하나만 열고, 여는 동안 시간을 멈추고 공격 입력을 막는다.
    /// 다크 판타지 1차(기획/다크판타지-분위기-1차.md '화면 연출'): 틀은 검은 쇠·뼈색, 제목·큰 글자는 바탕체 계열 OS 글꼴(없으면 기본),
    /// 단추는 어두운 쇠판. 기본 스킨을 복제한 던전 스킨을 Begin에서 그 OnGUI에만 씌우므로 전투 시험장 화면은 바뀌지 않는다.
    /// 철제·가죽 그림은 UI/Skin PNG를 사용한다. 구슬·띠·비네트와 버튼 상태는 처음 한 번 만들고 재사용한다.
    /// </summary>
    public static class DungeonUi
    {
        public const float RefHeight = 900f;

        public static float Scale => Mathf.Max(0.1f, Screen.height / RefHeight);
        /// <summary>기준 좌표계의 화면 너비.</summary>
        public static float Width => Screen.width / Scale;
        public static float Height => RefHeight;

        // ── 색(다크 판타지 1차 '화면 연출') ───────────────────────

        /// <summary>뼈색: 기본 글·테두리.</summary>
        public static readonly Color Bone = new Color(0.9f, 0.86f, 0.77f, 1f);
        /// <summary>바랜 뼈색: 보조 글.</summary>
        public static readonly Color BoneDim = new Color(0.69f, 0.66f, 0.60f, 1f);
        /// <summary>횃불 호박색: 강조(제목, 레벨, 키).</summary>
        public static readonly Color Ember = new Color(0.9f, 0.7f, 0.4f, 1f);
        /// <summary>검은 쇠 바탕.</summary>
        public static readonly Color IronFill = new Color(0.047f, 0.051f, 0.059f, 1f);
        /// <summary>쇠 테.</summary>
        public static readonly Color IronEdge = new Color(0.30f, 0.29f, 0.27f, 1f);
        /// <summary>짙은 피색 범위(#4A0606~#8A1010, 등급색 빨강·주황과 헷갈리지 않게).</summary>
        public static readonly Color BloodDark = new Color32(0x4A, 0x06, 0x06, 0xFF);
        public static readonly Color Blood = new Color32(0x8A, 0x10, 0x10, 0xFF);
        /// <summary>막힌 이유 등 경고 글(녹 빛 붉은색, 피·등급색과 다름).</summary>
        public static readonly Color Rust = new Color(0.82f, 0.48f, 0.38f, 1f);
        /// <summary>창 제목색(바랜 호박).</summary>
        public static readonly Color TitleColor = new Color(0.88f, 0.77f, 0.57f, 1f);

        public static GUIStyle Title { get; private set; }
        public static GUIStyle Label { get; private set; }
        public static GUIStyle Small { get; private set; }
        public static GUIStyle Bold { get; private set; }
        public static GUIStyle Center { get; private set; }
        public static GUIStyle BigCenter { get; private set; }
        static GUIStyle _closeGlyph;
        /// <summary>층 이름 카드·쓰러짐 같은 아주 큰 바탕체 글(가운데, 글자색은 GUI.color로).</summary>
        public static GUIStyle Display { get; private set; }
        /// <summary>레벨업 띠 같은 큰 바탕체 글(가운데).</summary>
        public static GUIStyle Banner { get; private set; }
        /// <summary>음울한 한 줄(바탕체 기울임, 가운데).</summary>
        public static GUIStyle Subtitle { get; private set; }
        /// <summary>화면 아래 알림 한 줄(바탕체, 가운데, 줄바꿈 없음).</summary>
        public static GUIStyle Toast { get; private set; }
        /// <summary>조용한 알림(새 칸 이름 등, 바탕체 기울임).</summary>
        public static GUIStyle ToastQuiet { get; private set; }

        /// <summary>지금 열린 창 이름(없으면 null).</summary>
        public static string Modal { get; private set; }
        public static bool ModalOpen => Modal != null;

        /// <summary>바탕체 계열 후보(기준 문서 '화면 연출'). 앞에서부터 설치된 것을 쓴다.</summary>
        static readonly string[] SerifNames = { "Batang", "바탕", "Noto Serif KR", "Gungsuh", "궁서", "Nanum Myeongjo", "NanumMyeongjo", "나눔명조" };

        static bool _ready;
        static bool _pausedByModal;
        static bool _serifTried;
        static Font _serif;
        static Font _bodyFont;
        static GUISkin _skin;
        static Texture2D _orbFill;
        static Texture2D _orbGlass;
        static Texture2D _orbRing;
        static Texture2D _strip;
        static Texture2D _vignette;
        static Texture2D _btnNormal;
        static Texture2D _btnHover;
        static Texture2D _btnActive;

        /// <summary>
        /// 플레이 시작마다 부른다(DungeonRoot). 창 상태를 비우고 글꼴 스타일을 다시 만들게 한다.
        /// 텍스처·스킨·글꼴은 아직 살아 있으면 그대로 다시 쓰고, 지워졌으면(Unity null) 다시 만든다.
        /// </summary>
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void HookReload() => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseCached;
#endif

        /// <summary>스크립트를 다시 불러오기 전에 만든 텍스처·스킨·글꼴을 지운다(DontSave라 저절로 지워지지 않는다).</summary>
        static void ReleaseCached()
        {
            DestroyCached(ref _orbFill);
            DestroyCached(ref _orbGlass);
            DestroyCached(ref _orbRing);
            DestroyCached(ref _strip);
            DestroyCached(ref _vignette);
            DestroyCached(ref _btnNormal);
            DestroyCached(ref _btnHover);
            DestroyCached(ref _btnActive);
            DestroyCached(ref _skin);
            DestroyCached(ref _serif);
            DestroyCached(ref _bodyFont);
            _ready = false;
            _serifTried = false;
        }

        static void DestroyCached<T>(ref T obj) where T : Object
        {
            if (obj) Object.DestroyImmediate(obj);
            obj = null;
        }

        public static void ResetStatics()
        {
            Modal = null;
            _pausedByModal = false;
            PlayerInputReader.Blocked = false;
            _ready = false;
            _serifTried = false;
        }

        /// <summary>바탕체 계열 OS 글꼴. 설치된 것이 없으면 null(기본 글꼴).</summary>
        public static Font Serif
        {
            get
            {
                if (_serif) return _serif;
                if (_serifTried) return null;
                _serifTried = true;
                _serif = LoadSerif();
                return _serif;
            }
        }

        static Font LoadSerif()
        {
            string[] installed;
            try
            {
                installed = Font.GetOSInstalledFontNames();
            }
            catch
            {
                return null;
            }
            if (installed == null || installed.Length == 0) return null;
            var found = new List<string>();
            foreach (var want in SerifNames)
            {
                foreach (var have in installed)
                {
                    if (!string.Equals(want, have, System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (!found.Contains(have)) found.Add(have);
                    break;
                }
            }
            if (found.Count == 0) return null;
            var font = Font.CreateDynamicFontFromOSFont(found.ToArray(), 32);
            if (font) font.hideFlags = HideFlags.DontSave;
            return font;
        }

        /// <summary>OnGUI 맨 앞에서 부른다. 던전 스킨을 씌우고 글꼴 크기·배율을 맞춘다.</summary>
        public static void Begin()
        {
            if (!_ready || !_skin) Build();
            if (GUI.skin != _skin) GUI.skin = _skin;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
        }

        /// <summary>던전 스킨·스타일을 만든다(플레이마다 한 번). 반드시 OnGUI 안에서 부른다.</summary>
        static void Build()
        {
            _ready = true;
            if (!_skin)
            {
                // 이번 OnGUI의 기본 스킨을 복제한다. 기본 스킨 자체는 건드리지 않는다(전투 시험장 화면 그대로).
                _skin = UnityEngine.Object.Instantiate(GUI.skin);
                _skin.name = "Dungeon skin";
                _skin.hideFlags = HideFlags.DontSave;
            }
            EnsureTextures();
            var serif = Serif;
            if (!_bodyFont) {
                _bodyFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Noto Sans KR", "Arial" }, 20);
                if (_bodyFont) _bodyFont.hideFlags = HideFlags.DontSave;
            }
            if (_bodyFont) _skin.font = _bodyFont;
            var label = _skin.label;

            Title = new GUIStyle(label) { fontSize = 25, fontStyle = FontStyle.Bold, wordWrap = true, font = serif };
            SetTextColor(Title, TitleColor);
            Label = new GUIStyle(label) { fontSize = 18, wordWrap = true };
            Small = new GUIStyle(label) { fontSize = 16, wordWrap = true };
            Bold = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = true };
            Center = new GUIStyle(label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _closeGlyph = new GUIStyle(label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal, padding = new RectOffset(), contentOffset = new Vector2(0,-1), wordWrap = false };
            SetTextColor(_closeGlyph, Bone);
            BigCenter = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, font = serif };
            SetTextColor(BigCenter, Color.white);
            Display = new GUIStyle(label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, font = serif };
            SetTextColor(Display, Color.white);
            Banner = new GUIStyle(label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, font = serif };
            SetTextColor(Banner, Color.white);
            Subtitle = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, font = serif };
            SetTextColor(Subtitle, Color.white);
            Toast = new GUIStyle(label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, font = serif };
            SetTextColor(Toast, Color.white);
            ToastQuiet = new GUIStyle(label) { fontSize = 18, fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, font = serif };
            SetTextColor(ToastQuiet, Color.white);

            // 단추: 어두운 쇠판 + 뼈색 글. 말뚝·사건·쪽지·가방·스킬 창 단추가 모두 이 모양을 쓴다.
            var b = _skin.button;
            b.fontSize = 18;
            b.padding = new RectOffset(12, 12, 6, 6);
            b.wordWrap = true;
            b.clipping = TextClipping.Clip;
            b.border = new RectOffset(16, 16, 16, 16);
            b.normal.background = _btnNormal;
            b.normal.textColor = Bone;
            b.hover.background = _btnHover;
            b.hover.textColor = new Color(0.97f, 0.9f, 0.76f, 1f);
            b.active.background = _btnActive;
            b.active.textColor = Ember;
            b.focused.background = _btnHover;
            b.focused.textColor = Ember;
            b.onNormal.background = _btnActive;
            b.onNormal.textColor = Ember;
            b.onHover.background = _btnActive;
            b.onHover.textColor = Ember;
            b.onActive.background = _btnActive;
            b.onActive.textColor = Ember;
            _skin.toggle.fontSize = 17;
            _skin.label.fontSize = 18;
        }

        static void SetTextColor(GUIStyle s, Color c)
        {
            s.normal.textColor = c;
            s.hover.textColor = c;
            s.active.textColor = c;
            s.focused.textColor = c;
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

        // ── 그리기 ───────────────────────────────────────────

        /// <summary>칠한 철제 테와 가죽 바탕. 큰 창은 32px 모서리, 작은 카드는 높이에 맞춰 얇게 쓴다.</summary>
        public static void Box(Rect r, float alpha = 0.82f)
        {
            if (alpha <= 0.002f) return;
            float corner = Mathf.Min(32f, r.height * .18f);
            Fill(r, new Color(.018f,.018f,.021f,alpha));
            UiSkinArt.NineSlice(r, UiSkinArt.Panel, corner, new Color(.74f,.73f,.71f,alpha), .22f);
        }

        /// <summary>정보 카드와 안내의 화면 여백. 입력을 받는 투명 패널을 추가하지 않는다.</summary>
        public static Rect KeepOnScreen(Rect rect, float bottomReserve = 0f)
        {
            const float margin = 16f;
            rect.width = Mathf.Min(rect.width, Width - margin * 2f);
            rect.height = Mathf.Min(rect.height, Height - margin * 2f - bottomReserve);
            rect.x = Mathf.Clamp(rect.x, margin, Width - margin - rect.width);
            rect.y = Mathf.Clamp(rect.y, margin, Height - margin - bottomReserve - rect.height);
            return rect;
        }

        public static void Slot(Rect rect, bool selected = false)
        {
            Fill(rect, new Color(.028f, .026f, .025f, 1f));
            UiSkinArt.NineSlice(rect, UiSkinArt.Socket, Mathf.Min(14f,rect.height*.1f), Color.white);
            if(selected) UiSkinArt.Selection(rect);
        }

        /// <summary>닫을 수 있는 창의 공통 닫기. 기능 처리는 각 창의 기존 닫기 경로를 쓴다.</summary>
        public static Rect CloseButtonRect(Rect panel) => new Rect(panel.xMax - 60f, panel.y + 14f, 40f, 40f);
        public static bool CloseButton(Rect panel, string tooltip = "닫기 · Esc")
        {
            var r = CloseButtonRect(panel);
            var color = GUI.color;
            GUI.color = Color.white;
            bool clicked = GUI.Button(r, new GUIContent("", tooltip), GUIStyle.none);
            bool hover = r.Contains(Event.current.mousePosition);
            UiSkinArt.NineSlice(r, UiSkinArt.Panel, 7f, new Color(.9f,.55f,.5f,1f));
            Fill(new Rect(r.x+4,r.y+4,r.width-8,r.height-8),hover?new Color(.48f,.08f,.09f,.96f):new Color(.30f,.045f,.055f,.96f));
            GUI.Label(r, "×", _closeGlyph);
            GUI.color = color;
            return clicked;
        }

        public static void Fill(Rect r, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>한 줄 표시 칸은 말줄임하고 전체 이름을 툴팁에 보존한다. 상세 이름은 별도로 줄바꿈한다.</summary>
        public static string FitLine(string text, GUIStyle style, float width)
        {
            if(string.IsNullOrEmpty(text)||style.CalcSize(new GUIContent(text)).x<=width)return text;
            int lo=0,hi=text.Length;
            while(lo<hi){int mid=(lo+hi+1)/2;if(style.CalcSize(new GUIContent(text.Substring(0,mid)+"…")).x<=width)lo=mid;else hi=mid-1;}
            return text.Substring(0,lo)+"…";
        }

        public static void CompactLabel(Rect r,string text,GUIStyle style)
            => GUI.Label(r,new GUIContent(FitLine(text,style,r.width),text),style);

        /// <summary>막대: 검은 홈 + 채움 + 윗면 옅은 광 + 쇠 테. 글이 있으면 뼈색 그림자 글로 가운데에.</summary>
        public static void Bar(Rect r, float fraction, Color fill, string text = null)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.78f));
            float w = (r.width - 2f) * Mathf.Clamp01(fraction);
            if (w > 0f)
            {
                var inner = new Rect(r.x + 1f, r.y + 1f, w, r.height - 2f);
                Fill(inner, fill);
                if (r.height >= 8f) Fill(new Rect(inner.x, inner.y, inner.width, Mathf.Max(1f, inner.height * 0.3f)), new Color(1f, 1f, 1f, 0.08f));
            }
            Outline(r, IronEdge, 1f);
            if (!string.IsNullOrEmpty(text)) ShadowLabel(r, text, Center, Bone);
        }

        /// <summary>테두리만 그린다(큰 지도의 '안 간 출구' 칸 등).</summary>
        public static void Outline(Rect r, Color color, float thickness = 1f)
        {
            var prev = GUI.color;
            OutlineRaw(r, color, thickness);
            GUI.color = prev;
        }

        static void OutlineRaw(Rect r, Color color, float thickness)
        {
            var tex = Texture2D.whiteTexture;
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), tex);
            GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), tex);
            GUI.DrawTexture(new Rect(r.x, r.y + thickness, thickness, r.height - thickness * 2f), tex);
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.y + thickness, thickness, r.height - thickness * 2f), tex);
        }

        /// <summary>양 끝이 흐려지는 띠(알림·층 이름 카드·레벨업 띠·장식 선). 색은 color(알파 포함).</summary>
        public static void Strip(Rect r, Color color)
        {
            if (color.a <= 0.002f) return;
            EnsureTextures();
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, _strip, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        /// <summary>가장자리로 갈수록 짙어지는 비네트(쓰러짐 화면 등). 색은 color(알파 포함).</summary>
        public static void Vignette(Rect r, Color color)
        {
            if (color.a <= 0.002f) return;
            EnsureTextures();
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, _vignette, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        /// <summary>검은 그림자를 깐 글. 글자색은 color(스타일 글자색은 흰색이라 GUI.color가 그대로 보인다).</summary>
        public static void ShadowLabel(Rect r, string text, GUIStyle style, Color color, float shadow = 0.85f)
        {
            if (string.IsNullOrEmpty(text) || color.a <= 0.002f) return;
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, color.a * shadow);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
            GUI.color = prev;
        }

        /// <summary>
        /// 디아블로식 둥근 생명 구슬(짙은 피색, 기준 문서 '화면 연출'): 빈 구슬 바탕 → 채운 만큼 아래에서부터 피색 → 수면 선 → 유리 광 → 쇠 고리.
        /// r은 구슬 자체(정사각형), 쇠 고리는 둘레로 6%씩 더 크게 그린다. brightness는 채움 밝기(낮은 체력 맥동 등).
        /// </summary>
        public static void Orb(Rect r, float fraction, float brightness = 1f)
        {
            EnsureTextures();
            var prev = GUI.color;
            GUI.color = new Color(0.17f, 0.13f, 0.13f, 1f);
            GUI.DrawTexture(r, _orbFill);
            float f = Mathf.Clamp01(fraction);
            if (f > 0.001f)
            {
                GUI.color = new Color(brightness, brightness, brightness, 1f);
                var dst = new Rect(r.x, r.y + r.height * (1f - f), r.width, r.height * f);
                GUI.DrawTextureWithTexCoords(dst, _orbFill, new Rect(0f, 0f, 1f, f), true);
                if (f < 0.995f)
                {
                    // 수면: 차오른 높이에서 원의 현을 따라 가는 옅은 선.
                    float yy = (0.5f - f) * 2f;
                    float half = Mathf.Sqrt(Mathf.Max(0f, 1f - yy * yy)) * r.width * 0.5f * 0.94f;
                    float cy = r.y + r.height * (1f - f);
                    GUI.color = new Color(0.86f, 0.3f, 0.24f, 0.45f * Mathf.Clamp01(brightness));
                    GUI.DrawTexture(new Rect(r.center.x - half, cy - 0.75f, half * 2f, 1.5f), Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;
            GUI.DrawTexture(r, _orbGlass);
            float pad = r.width * 0.06f;
            GUI.DrawTexture(new Rect(r.x - pad, r.y - pad, r.width + pad * 2f, r.height + pad * 2f), _orbRing);
            GUI.color = prev;
        }

        /// <summary>작은 물약 병(가득 = 피색, 빈 병 = 검게).</summary>
        public static void Flask(Rect r, bool full)
        {
            EnsureTextures();
            var prev = GUI.color;
            float body = Mathf.Min(r.width, r.height * 0.72f);
            var b = new Rect(r.center.x - body * 0.5f, r.yMax - body, body, body);
            var neck = new Rect(r.center.x - body * 0.17f, b.y - r.height * 0.22f + 1f, body * 0.34f, r.height * 0.24f);
            Fill(neck, new Color(0.26f, 0.24f, 0.22f, 1f));
            Fill(new Rect(neck.x - 1f, neck.y - 2f, neck.width + 2f, 3f), new Color(0.4f, 0.3f, 0.19f, 1f));
            GUI.color = full ? Color.white : new Color(0.14f, 0.12f, 0.12f, 1f);
            GUI.DrawTexture(b, _orbFill);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(b, _orbGlass);
            GUI.color = prev;
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

        /// <summary>등급색(2차 6-2): 일반 회색, 고급 초록, 희귀 파랑, 영웅 보라, 전설 주황. 장비에만 쓴다(값은 그대로).</summary>
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

        // ── 절차 텍스처(처음 한 번) ──────────────────────────────

        static void EnsureTextures()
        {
            if (!_orbFill) _orbFill = BuildOrbFill();
            if (!_orbGlass) _orbGlass = BuildOrbGlass();
            if (!_orbRing) _orbRing = BuildOrbRing();
            if (!_strip) _strip = BuildStrip();
            if (!_vignette) _vignette = BuildVignette();
            if (!_btnNormal) _btnNormal = UiSkinArt.Button(.88f) ?? BuildButton(new Color(0.12f, 0.125f, 0.135f), new Color(0.065f, 0.07f, 0.08f), IronEdge);
            if (!_btnHover) _btnHover = UiSkinArt.Button(1.4f) ?? BuildButton(new Color(0.19f, 0.185f, 0.17f), new Color(0.11f, 0.11f, 0.115f), new Color(0.5f, 0.45f, 0.37f));
            if (!_btnActive) _btnActive = UiSkinArt.Button(.60f) ?? BuildButton(new Color(0.05f, 0.045f, 0.04f), new Color(0.08f, 0.07f, 0.06f), new Color(0.6f, 0.44f, 0.24f));
        }

        static Texture2D NewTex(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
        }

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>구슬 채움: 짙은 피색(#3A0404 가장자리 → #8A1010 왼쪽 위 빛), 느린 소용돌이 결. 원 밖은 투명.</summary>
        static Texture2D BuildOrbFill()
        {
            const int N = 128;
            var tex = NewTex(N, N, "Life orb fill");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N - 0.5f;
                float v = (y + 0.5f) / N - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v);
                float edge = Mathf.Clamp01((0.5f - d) * N);
                float lx = u + 0.13f;
                float ly = v - 0.15f;
                float light = Mathf.Clamp01(1f - Mathf.Sqrt(lx * lx + ly * ly) * 1.55f);
                float swirl = 0.5f + 0.5f * Mathf.Sin(u * 21f + Mathf.Sin(v * 13f) * 2.2f) * Mathf.Cos(v * 17f - u * 5f);
                float k = Mathf.Clamp01(light * 0.85f + swirl * 0.16f);
                // #3A0404 → #8A1010
                float r = Mathf.Lerp(0.227f, 0.541f, k);
                float g = Mathf.Lerp(0.016f, 0.063f, k);
                float b = Mathf.Lerp(0.016f, 0.063f, k);
                px[y * N + x] = new Color(r, g, b, edge);
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>구슬 유리: 가장자리 안쪽 그림자 + 왼쪽 위 흰 광 + 아래 작은 반사.</summary>
        static Texture2D BuildOrbGlass()
        {
            const int N = 128;
            var tex = NewTex(N, N, "Life orb glass");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N - 0.5f;
                float v = (y + 0.5f) / N - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v);
                float edge = Mathf.Clamp01((0.5f - d) * N);
                float rim = Smooth(0.33f, 0.5f, d) * 0.72f;
                float hx = u + 0.13f;
                float hy = v - 0.21f;
                float hd = Mathf.Sqrt(hx * hx + hy * hy * 2.4f);
                float hl = Mathf.Clamp01(1f - hd / 0.15f);
                hl = hl * hl * 0.42f;
                float bx = u - 0.04f;
                float by = v + 0.34f;
                float bd = Mathf.Sqrt(bx * bx * 0.5f + by * by * 7f);
                float bl = Mathf.Clamp01(1f - bd / 0.11f) * 0.13f;
                float white = Mathf.Clamp01(hl + bl);
                float a = white + rim * (1f - white);
                float c = a > 0.0001f ? white / a : 0f;
                px[y * N + x] = new Color(c, c * 0.97f, c * 0.93f, a * edge);
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>구슬 쇠 고리: 위가 밝은 쇠 결 + 가운데 바랜 뼈색 선 + 못 넷. 구슬보다 12% 큰 판에 그린다(안쪽 반지름 0.43).</summary>
        static Texture2D BuildOrbRing()
        {
            const int N = 160;
            const float Inner = 0.43f;
            const float Mid = (Inner + 0.5f) * 0.5f;
            var tex = NewTex(N, N, "Life orb ring");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (x + 0.5f) / N - 0.5f;
                float v = (y + 0.5f) / N - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.Clamp01((d - Inner) * N) * Mathf.Clamp01((0.5f - d) * N);
                if (a <= 0f)
                {
                    px[y * N + x] = new Color(0f, 0f, 0f, 0f);
                    continue;
                }
                float t = (d - Inner) / (0.5f - Inner);
                float top = 0.5f + 0.5f * (v / Mathf.Max(d, 0.001f));
                float shade = 0.07f + 0.11f * top + 0.05f * Mathf.Sin(t * Mathf.PI);
                float r = shade * 1.06f;
                float g = shade;
                float b = shade * 0.9f;
                float line = Mathf.Clamp01(1.2f - Mathf.Abs(d - Mid) * N);
                r = Mathf.Lerp(r, 0.5f, line * 0.55f);
                g = Mathf.Lerp(g, 0.45f, line * 0.55f);
                b = Mathf.Lerp(b, 0.37f, line * 0.55f);
                // 못 넷(45°, 135°, 225°, 315°).
                for (int k = 0; k < 4; k++)
                {
                    float ang = (45f + 90f * k) * Mathf.Deg2Rad;
                    float sx = u - Mathf.Cos(ang) * Mid;
                    float sy = v - Mathf.Sin(ang) * Mid;
                    float sd = Mathf.Sqrt(sx * sx + sy * sy);
                    if (sd > 0.022f) continue;
                    float stud = 1f - sd / 0.022f;
                    r = Mathf.Lerp(r, 0.6f, stud);
                    g = Mathf.Lerp(g, 0.54f, stud);
                    b = Mathf.Lerp(b, 0.45f, stud);
                }
                px[y * N + x] = new Color(r, g, b, a);
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>가로 띠: 흰색, 양 끝 22%에서 부드럽게 사라진다(GUI.color로 물들여 쓴다).</summary>
        static Texture2D BuildStrip()
        {
            const int W = 64;
            const int H = 4;
            var tex = NewTex(W, H, "Fade strip");
            var px = new Color[W * H];
            for (int x = 0; x < W; x++)
            {
                float xn = (x + 0.5f) / W;
                float a = Smooth(0f, 0.22f, xn) * Smooth(1f, 0.78f, xn);
                for (int y = 0; y < H; y++) px[y * W + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>비네트: 가운데 투명, 가장자리로 갈수록 짙다(흰색, GUI.color로 물들인다).</summary>
        static Texture2D BuildVignette()
        {
            const int N = 64;
            var tex = NewTex(N, N, "Vignette");
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = ((x + 0.5f) / N - 0.5f) * 2f;
                float v = ((y + 0.5f) / N - 0.5f) * 2f;
                float d = Mathf.Sqrt(u * u + v * v);
                px[y * N + x] = new Color(1f, 1f, 1f, Smooth(0.3f, 1.25f, d));
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        /// <summary>단추 판(8×8, 테 3px로 늘려 씀): 검은 바깥 1px, 테 색 1px, 안은 위→아래 어두워지는 쇠.</summary>
        static Texture2D BuildButton(Color top, Color bottom, Color edge)
        {
            const int N = 8;
            var tex = NewTex(N, N, "Dungeon button");
            tex.filterMode = FilterMode.Point;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int ring = Mathf.Min(Mathf.Min(x, N - 1 - x), Mathf.Min(y, N - 1 - y));
                Color c;
                if (ring == 0) c = new Color(0f, 0f, 0f, 1f);
                else if (ring == 1) c = edge;
                else c = Color.Lerp(bottom, top, (y - 2f) / (N - 5f));
                c.a = 1f;
                px[y * N + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
