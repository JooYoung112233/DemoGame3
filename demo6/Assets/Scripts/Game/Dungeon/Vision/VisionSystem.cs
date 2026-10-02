using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 좀보이드식 시야(사용자 요청 2026-10-03 "범위 빛 밖이 보이네, 좀보이드 같은 것처럼 처리해 줘").
    /// ① 벽이 시야를 가린다: 플레이어에서 광선을 쏴 시야 다각형을 만든다(벽 면은 0.45 들어가 보이게).
    ///    고른 간격 광선에 더해 벽·기둥 모서리를 정확히 겨냥한 광선을 쏴, 걸을 때 그림자 경계가 떨리지 않게 한다.
    /// ② 바라보는 쪽(커서) 부채꼴 130°·14유닛 + 몸 둘레 2.5유닛은 늘 본다. 부채꼴 옆면은 10°에 걸쳐 부드럽게 줄어든다.
    /// ③ 안 가 본 곳은 완전히 검게(기억 안개), 가 봤지만 지금 안 보이는 곳은 어둡게 덮는다(기억으로만 보이는 지형).
    ///    기억 안개는 0.25유닛 칸에 '봤다'를 적고, 바뀐 곳만 5×5로 흐려 올려 경계가 계단처럼 보이지 않게 한다.
    /// ④ 시야 밖 적은 그리지 않는다. 시야 안이어도 빛(등잔 흐린 반경·켠 벽 등잔) 밖이면 몸 대신 눈 두 점만(3차 초안 2-4).
    /// 예고·빛기둥·투사체는 지워지지 않고 시야 밖에서는 어둡게만 보인다(공정 규칙).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class VisionSystem : MonoBehaviour
    {
        public const float ViewRadius = 14f;
        public const float NearRadius = 2.5f;
        public const float ConeHalfAngle = 65f;
        /// <summary>부채꼴 옆면이 14 → 2.5로 줄어드는 각도 폭(칼로 자른 경계 대신 부드럽게).</summary>
        const float ConeSoft = 10f;
        /// <summary>벽에 닿은 광선을 벽 안으로 더 들여 벽 면이 보이게(벽 두께 1, 기둥 1.2보다 작게).</summary>
        const float WallReveal = 0.45f;
        const float ConeStep = 0.6f;
        const float RearStep = 3f;
        /// <summary>모서리 광선을 모서리 양옆으로 이만큼 비켜 쏜다(도).</summary>
        const float CornerNudge = 0.05f;
        const float Feather = 0.9f;
        const float VeilAlpha = 0.62f;
        const float FarRadius = 45f;
        /// <summary>기억 안개 칸 크기.</summary>
        const float MemoryCell = 0.25f;
        /// <summary>기억 안개 흐림 반경(칸). 2 = 5×5.</summary>
        const int BlurRadius = 2;
        const float TurnSpeed = 540f;
        /// <summary>어둡게 덮기: 바닥·벽·물체·예고·효과 위, 눈빛(20000) 아래.</summary>
        const int VeilOrder = 5000;
        /// <summary>기억 안개: 안 가 본 곳을 덮는 맨 위 판.</summary>
        const int FogOrder = 14000;

        public static VisionSystem Instance { get; private set; }

        /// <summary>시험 패널: 끄면 전부 보인다(안개·덮개·적 숨기기 없음).</summary>
        public bool VisionOn { get; set; } = true;
        /// <summary>시험 패널: 끄면 360°로 14유닛까지 본다.</summary>
        public bool ConeOn { get; set; } = true;
        public Vector2 Origin => _origin;
        public float LookDeg => _lookDeg;

        /// <summary>시야를 가리지 않는 작은 물체 충돌체(말뚝·광맥·금고). 벽 레이어지만 좀보이드의 가구처럼 시야는 통과시킨다.</summary>
        static readonly HashSet<Collider2D> SeeThrough = new HashSet<Collider2D>();

        readonly List<float> _rayAngle = new List<float>(1024);
        readonly List<float> _rayDist = new List<float>(1024);
        readonly List<float> _sample = new List<float>(1024);
        readonly List<RaycastHit2D> _hits = new List<RaycastHit2D>(8);
        readonly List<Renderer> _renderers = new List<Renderer>(16);
        readonly List<Vector2> _litLamps = new List<Vector2>(8);
        readonly List<Vector2> _corners = new List<Vector2>(1024);
        readonly List<Collider2D> _wallCols = new List<Collider2D>(512);
        readonly List<Vector3> _verts = new List<Vector3>(4096);
        readonly List<Color32> _colors = new List<Color32>(4096);
        readonly List<int> _tris = new List<int>(16384);

        ContactFilter2D _wallFilter;
        Vector2 _origin;
        float _lookDeg;
        bool _hasLook;
        bool _cornersDirty = true;
        Rect _worldBounds;

        Mesh _veilMesh;
        MeshRenderer _veilRenderer;

        Texture2D _fogTex;
        Sprite _fogSpriteAsset;
        /// <summary>칸마다 봤는가(0/1).</summary>
        byte[] _seen;
        /// <summary>올리는 안개 알파(흐린 결과, 255 = 검음).</summary>
        byte[] _fogAlpha;
        int[] _rowSum;
        SpriteRenderer _fogSprite;
        Vector2 _fogMin;
        int _fogW;
        int _fogH;
        int _dirtyMinX, _dirtyMinY, _dirtyMaxX, _dirtyMaxY;
        bool _fogDirty;

        public static void RegisterSeeThrough(Collider2D col)
        {
            if (col) SeeThrough.Add(col);
        }

        public static void ResetStatics() => SeeThrough.Clear();

        void Awake()
        {
            Instance = this;
            _wallFilter = new ContactFilter2D { useTriggers = false };
            _wallFilter.SetLayerMask(Layers.WallMask);

            var veil = new GameObject("Vision veil");
            veil.transform.SetParent(transform, false);
            _veilMesh = new Mesh { name = "Vision veil" };
            _veilMesh.MarkDynamic();
            veil.AddComponent<MeshFilter>().sharedMesh = _veilMesh;
            _veilRenderer = veil.AddComponent<MeshRenderer>();
            _veilRenderer.sharedMaterial = RenderMaterials.Unlit;
            _veilRenderer.sortingOrder = VeilOrder;
            // 판자벽·금 간 벽이 열리면 모서리가 바뀐다.
            DungeonEvents.EdgeOpened += OnEdgeOpened;
        }

        void OnDestroy()
        {
            DungeonEvents.EdgeOpened -= OnEdgeOpened;
            if (Instance == this) Instance = null;
            if (_veilMesh) Destroy(_veilMesh);
            if (_fogSpriteAsset) Destroy(_fogSpriteAsset);
            if (_fogTex) Destroy(_fogTex);
        }

        void OnEdgeOpened(DungeonEdge edge) => _cornersDirty = true;

        /// <summary>DungeonRoot가 지도를 만든 뒤 부른다: 지도 전체를 덮는 기억 안개 판을 만든다.</summary>
        public void Init(Rect worldBounds)
        {
            _worldBounds = worldBounds;
            _fogMin = worldBounds.min - Vector2.one;
            _fogW = Mathf.CeilToInt((worldBounds.width + 2f) / MemoryCell);
            _fogH = Mathf.CeilToInt((worldBounds.height + 2f) / MemoryCell);
            // 알파만 쓰는 1바이트 판(검은색으로 칠해 그린다).
            _fogTex = new Texture2D(_fogW, _fogH, TextureFormat.Alpha8, false)
            {
                name = "Vision memory",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            _seen = new byte[_fogW * _fogH];
            _fogAlpha = new byte[_fogW * _fogH];
            _rowSum = new int[_fogW * _fogH];
            for (int i = 0; i < _fogAlpha.Length; i++) _fogAlpha[i] = 255;
            _fogTex.SetPixelData(_fogAlpha, 0);
            _fogTex.Apply(false);
            var go = new GameObject("Vision memory fog");
            go.transform.SetParent(transform, false);
            go.transform.position = _fogMin;
            _fogSprite = go.AddComponent<SpriteRenderer>();
            _fogSpriteAsset = Sprite.Create(_fogTex, new Rect(0, 0, _fogW, _fogH), Vector2.zero, 1f / MemoryCell);
            _fogSprite.sprite = _fogSpriteAsset;
            _fogSprite.color = Color.black;
            _fogSprite.sortingOrder = FogOrder;
            RenderMaterials.MakeUnlit(_fogSprite);
            _cornersDirty = true;
        }

        /// <summary>시야 다각형 안인가(pad만큼 넉넉하게). 시야를 끄면 늘 참.</summary>
        public bool IsVisible(Vector2 p, float pad = 0f)
        {
            if (!VisionOn || _rayAngle.Count < 3) return true;
            Vector2 to = p - _origin;
            float d = to.magnitude;
            if (d <= pad) return true;
            return d - pad <= LimitAt(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
        }

        /// <summary>그 방향 시야 다각형 경계까지 거리(이웃 광선 사이를 직선으로 이음).</summary>
        float LimitAt(float angleDeg)
        {
            float start = _rayAngle[0];
            float rel = Mathf.Repeat(angleDeg - start, 360f) + start;
            int hi = _rayAngle.BinarySearch(rel);
            if (hi >= 0) return _rayDist[hi];
            hi = ~hi;
            int lo = hi - 1;
            float aLo, aHi, dLo, dHi;
            if (hi >= _rayAngle.Count)
            {
                aLo = _rayAngle[_rayAngle.Count - 1];
                dLo = _rayDist[_rayAngle.Count - 1];
                aHi = _rayAngle[0] + 360f;
                dHi = _rayDist[0];
            }
            else
            {
                aLo = _rayAngle[lo];
                dLo = _rayDist[lo];
                aHi = _rayAngle[hi];
                dHi = _rayDist[hi];
            }
            float t = aHi - aLo > 0.0001f ? (rel - aLo) / (aHi - aLo) : 0f;
            return Mathf.Lerp(dLo, dHi, t);
        }

        void LateUpdate()
        {
            var player = PlayerController.Instance;
            if (!player) return;
            bool on = VisionOn;
            if (_veilRenderer) _veilRenderer.enabled = on;
            if (_fogSprite) _fogSprite.enabled = on;
            if (!on)
            {
                ShowAllEnemies();
                return;
            }
            if (_cornersDirty) CollectCorners();
            // 그림·등잔·카메라가 따르는 보간된 위치(물리 위치를 쓰면 50Hz로 끊겨 움직인다).
            _origin = player.transform.position;
            if (!TimeScaleService.Paused) UpdateLook(player);
            BuildRays();
            MarkMemory();
            BuildVeil();
            if (_fogDirty) UploadFog();
            UpdateEnemies();
        }

        /// <summary>바라보는 쪽 = 커서 방향(커서가 몸 가까이면 몸이 향한 쪽). 너무 튀지 않게 초당 540°까지만 돈다.</summary>
        void UpdateLook(PlayerController player)
        {
            Vector2 dir = player.FacingDirection;
            var cam = Camera.main;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (cam && mouse != null)
            {
                Vector3 sp = mouse.position.ReadValue();
                sp.z = -cam.transform.position.z;
                Vector2 toCursor = (Vector2)cam.ScreenToWorldPoint(sp) - _origin;
                if (toCursor.sqrMagnitude > 0.36f) dir = toCursor;
            }
            if (dir.sqrMagnitude < 0.0001f) return;
            float target = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (!_hasLook)
            {
                _lookDeg = target;
                _hasLook = true;
                return;
            }
            _lookDeg = Mathf.MoveTowardsAngle(_lookDeg, target, TurnSpeed * Time.unscaledDeltaTime);
        }

        /// <summary>시야를 막는 벽·기둥·문 막이의 모서리(축 정렬 상자 네 귀). 벽이 열리면 다시 모은다.</summary>
        void CollectCorners()
        {
            _cornersDirty = false;
            _corners.Clear();
            _wallCols.Clear();
            var area = _worldBounds;
            Physics2D.OverlapArea(area.min - Vector2.one * 2f, area.max + Vector2.one * 2f, _wallFilter, _wallCols);
            foreach (var col in _wallCols)
            {
                if (!col || SeeThrough.Contains(col)) continue;
                var b = col.bounds;
                _corners.Add(new Vector2(b.min.x, b.min.y));
                _corners.Add(new Vector2(b.min.x, b.max.y));
                _corners.Add(new Vector2(b.max.x, b.min.y));
                _corners.Add(new Vector2(b.max.x, b.max.y));
            }
        }

        /// <summary>그 방향 최대 시야 거리: 부채꼴 안 14, 밖 2.5, 옆면 10°에 걸쳐 부드럽게.</summary>
        float MaxRadius(float angleDeg)
        {
            if (!ConeOn) return ViewRadius;
            float off = Mathf.Abs(Mathf.DeltaAngle(angleDeg, _lookDeg));
            float t = Mathf.Clamp01((off - ConeHalfAngle) / ConeSoft);
            return Mathf.Lerp(ViewRadius, NearRadius, t * t * (3f - 2f * t));
        }

        void BuildRays()
        {
            _sample.Clear();
            float look = _lookDeg;
            float start = look - 180f;
            float wide = ConeHalfAngle + ConeSoft;
            for (float a = start; a < start + 360f - 0.001f;)
            {
                _sample.Add(a);
                bool inCone = !ConeOn || Mathf.Abs(Mathf.DeltaAngle(a, look)) <= wide;
                a += inCone ? ConeStep : RearStep;
            }
            // 모서리 광선: 모서리 바로 앞뒤로 쏴 그림자 경계를 정확히 잡는다.
            float reach = ViewRadius + 1f;
            foreach (var c in _corners)
            {
                Vector2 to = c - _origin;
                float d2 = to.sqrMagnitude;
                if (d2 > reach * reach || d2 < 0.0001f) continue;
                float a = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                if (Mathf.Sqrt(d2) > MaxRadius(a) + 1f) continue;
                float rel = Mathf.Repeat(a - start, 360f) + start;
                _sample.Add(rel - CornerNudge);
                _sample.Add(rel + CornerNudge);
            }
            _sample.Sort();
            _rayAngle.Clear();
            _rayDist.Clear();
            float last = float.NegativeInfinity;
            foreach (float a in _sample)
            {
                if (a - last < 0.01f || a >= start + 360f || a < start) continue;
                last = a;
                Ray(a, MaxRadius(a));
            }
        }

        void Ray(float angleDeg, float maxR)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            float d = maxR;
            int n = Physics2D.Raycast(_origin, dir, _wallFilter, _hits, maxR);
            for (int i = 0; i < n; i++)
            {
                var h = _hits[i];
                if (!h.collider || SeeThrough.Contains(h.collider)) continue;
                d = Mathf.Min(d, h.distance + WallReveal);
            }
            _rayAngle.Add(angleDeg);
            _rayDist.Add(Mathf.Min(maxR, d));
        }

        /// <summary>시야에 든 칸을 '봤다'로 적는다(흐림은 올릴 때).</summary>
        void MarkMemory()
        {
            if (_seen == null) return;
            float step = MemoryCell * 0.7f;
            for (int i = 0; i < _rayAngle.Count; i++)
            {
                float r = _rayAngle[i] * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
                float len = _rayDist[i];
                for (float t = 0f; t <= len; t += step) See(_origin + dir * t);
                See(_origin + dir * len);
            }
        }

        void See(Vector2 p)
        {
            int x = Mathf.FloorToInt((p.x - _fogMin.x) / MemoryCell);
            int y = Mathf.FloorToInt((p.y - _fogMin.y) / MemoryCell);
            if (x < 0 || y < 0 || x >= _fogW || y >= _fogH) return;
            int i = y * _fogW + x;
            if (_seen[i] != 0) return;
            _seen[i] = 1;
            if (!_fogDirty)
            {
                _fogDirty = true;
                _dirtyMinX = _dirtyMaxX = x;
                _dirtyMinY = _dirtyMaxY = y;
                return;
            }
            if (x < _dirtyMinX) _dirtyMinX = x;
            if (x > _dirtyMaxX) _dirtyMaxX = x;
            if (y < _dirtyMinY) _dirtyMinY = y;
            if (y > _dirtyMaxY) _dirtyMaxY = y;
        }

        /// <summary>바뀐 구역(+흐림 반경)만 5×5 상자로 흐려 알파를 다시 계산하고 판을 올린다.</summary>
        void UploadFog()
        {
            _fogDirty = false;
            int k = BlurRadius;
            int x0 = Mathf.Max(0, _dirtyMinX - k), x1 = Mathf.Min(_fogW - 1, _dirtyMaxX + k);
            int y0 = Mathf.Max(0, _dirtyMinY - k), y1 = Mathf.Min(_fogH - 1, _dirtyMaxY + k);
            // 가로 합(세로로 k칸 더 넓게).
            int ry0 = Mathf.Max(0, y0 - k), ry1 = Mathf.Min(_fogH - 1, y1 + k);
            for (int y = ry0; y <= ry1; y++)
            {
                int row = y * _fogW;
                for (int x = x0; x <= x1; x++)
                {
                    int s = 0;
                    int a = Mathf.Max(0, x - k), b = Mathf.Min(_fogW - 1, x + k);
                    for (int xx = a; xx <= b; xx++) s += _seen[row + xx];
                    _rowSum[row + x] = s;
                }
            }
            float norm = 1f / ((2 * k + 1) * (2 * k + 1));
            for (int y = y0; y <= y1; y++)
            {
                int a = Mathf.Max(ry0, y - k), b = Mathf.Min(ry1, y + k);
                for (int x = x0; x <= x1; x++)
                {
                    int s = 0;
                    for (int yy = a; yy <= b; yy++) s += _rowSum[yy * _fogW + x];
                    float v = Mathf.Clamp01(s * norm * 1.6f);
                    v = v * v * (3f - 2f * v);
                    _fogAlpha[y * _fogW + x] = (byte)Mathf.RoundToInt((1f - v) * 255f);
                }
            }
            _fogTex.SetPixelData(_fogAlpha, 0);
            _fogTex.Apply(false);
        }

        /// <summary>시야 다각형 바깥(가장자리 0.9유닛은 점점 짙게)을 덮는 고리 모양 판.</summary>
        void BuildVeil()
        {
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
            int n = _rayAngle.Count;
            if (n < 3)
            {
                _veilMesh.Clear();
                return;
            }
            byte veilA = (byte)Mathf.RoundToInt(VeilAlpha * 255f);
            var clear = new Color32(0, 0, 0, 0);
            var dark = new Color32(0, 0, 0, veilA);
            for (int i = 0; i < n; i++)
            {
                float r = _rayAngle[i] * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
                float d = _rayDist[i];
                _verts.Add(_origin + dir * d);
                _verts.Add(_origin + dir * (d + Feather));
                _verts.Add(_origin + dir * FarRadius);
                _colors.Add(clear);
                _colors.Add(dark);
                _colors.Add(dark);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a0 = i * 3, a1 = a0 + 1, a2 = a0 + 2;
                int b0 = j * 3, b1 = b0 + 1, b2 = b0 + 2;
                _tris.Add(a0); _tris.Add(b1); _tris.Add(b0);
                _tris.Add(a0); _tris.Add(a1); _tris.Add(b1);
                _tris.Add(a1); _tris.Add(b2); _tris.Add(b1);
                _tris.Add(a1); _tris.Add(a2); _tris.Add(b2);
            }
            _veilMesh.Clear();
            _veilMesh.SetVertices(_verts);
            _veilMesh.SetColors(_colors);
            _veilMesh.SetTriangles(_tris, 0);
            _veilMesh.bounds = new Bounds(new Vector3(_origin.x, _origin.y, 0f), new Vector3(FarRadius * 2f, FarRadius * 2f, 10f));
        }

        /// <summary>시야 밖 적은 그리지 않고, 시야 안이라도 빛 밖이면 숨긴다(눈 두 점은 DarkVision이 VisionInSight로 띄움).</summary>
        void UpdateEnemies()
        {
            var lighting = DungeonLighting.Instance;
            var player = PlayerController.Instance;
            float lampR = lighting ? lighting.DimRadius : DungeonLighting.LampDim;
            bool dark = !lighting || lighting.DarknessOn;
            CollectLitLamps();
            foreach (var e in Enemy.All)
            {
                if (!e) continue;
                bool inSight = IsVisible(e.Position, e.Radius);
                bool lit = !dark || IsLit(e.Position, player ? (Vector2)player.transform.position : _origin, lampR);
                e.VisionInSight = inSight;
                bool hidden = !inSight || !lit;
                // 쓰러지는 몸(처치 날림, 스스로 빛남 2-4 ②)은 시야 안이거나 보이던 적이면 끝까지 보인다.
                if (e.Dead) hidden = e.VisionHidden && !inSight;
                SetHidden(e, hidden);
            }
        }

        bool IsLit(Vector2 p, Vector2 player, float lampR)
        {
            if ((p - player).sqrMagnitude <= lampR * lampR) return true;
            float wallR = DungeonLighting.WallLampDim;
            foreach (var l in _litLamps)
                if ((p - l).sqrMagnitude <= wallR * wallR && !Blocked(l, p)) return true;
            return false;
        }

        /// <summary>두 점 사이를 벽이 막는가(시야를 가리지 않는 작은 물체는 무시). 벽 등잔 빛도 벽에 그림자가 진다.</summary>
        bool Blocked(Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 0.001f) return false;
            int n = Physics2D.Raycast(from, d / len, _wallFilter, _hits, len);
            for (int i = 0; i < n; i++)
                if (_hits[i].collider && !SeeThrough.Contains(_hits[i].collider)) return true;
            return false;
        }

        void CollectLitLamps()
        {
            _litLamps.Clear();
            foreach (var it in Interactable.All)
                if (it is WallLamp lamp && lamp && lamp.Lit) _litLamps.Add(lamp.Position);
        }

        /// <summary>
        /// 몸을 숨기거나 보인다. 숨겨도 시야 안이면 빛을 무시하는 표식(정예 흰 테두리, 멧돼지 기절 고리)은 남긴다(3차 초안 2-4 공정 규칙).
        /// </summary>
        void SetHidden(Enemy e, bool hidden)
        {
            if (e.VisionHidden == hidden && !hidden) return;
            e.VisionHidden = hidden;
            _renderers.Clear();
            e.GetComponentsInChildren(true, _renderers);
            var unlit = RenderMaterials.Unlit;
            bool keepMarkers = hidden && e.VisionInSight && !e.Dead;
            foreach (var r in _renderers)
            {
                if (!r) continue;
                bool off = hidden && !(keepMarkers && unlit && r.sharedMaterial == unlit);
                if (r.forceRenderingOff != off) r.forceRenderingOff = off;
            }
        }

        void ShowAllEnemies()
        {
            foreach (var e in Enemy.All)
            {
                if (!e) continue;
                e.VisionInSight = true;
                if (!e.VisionHidden) continue;
                e.VisionHidden = false;
                _renderers.Clear();
                e.GetComponentsInChildren(true, _renderers);
                foreach (var r in _renderers)
                    if (r) r.forceRenderingOff = false;
            }
        }
    }
}
