using System.Globalization;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 세계 물체(벽·등잔·말뚝·광맥·사건·이야기 물건) 공통 도움 함수.
    /// 그림·소리 리소스는 다음 단계라 지금은 도형으로 그린다(계약서 'M0b에서 빼는 것').
    /// 기본 스프라이트는 빛을 받아 어둠 속에서 가려지고(3차 초안 2-4), 불꽃·반짝임·흰 등불·파편만 빛을 무시한다.
    /// </summary>
    public static class WorldProps
    {
        /// <summary>바닥 그림(분필·먼지 줄·계단) 정렬: 바닥(-1000) 위, 벽(-900) 아래.</summary>
        public const int FloorDecalOrder = -950;
        /// <summary>벽 위에 겹쳐 그리는 금·자물쇠(벽 -900 바로 위).</summary>
        public const int WallDetailOrder = -899;

        /// <summary>그림 자원 캐시(ShapeSprites와 같은 방식, 상태가 아니라 비울 필요 없음).</summary>
        static Sprite _softDot;

        public static DungeonState State => DungeonRoot.Instance ? DungeonRoot.Instance.State : null;

        public static int Floor => DungeonRoot.Instance ? DungeonRoot.Instance.Floor : 1;

        public static bool HasPickaxe
        {
            get
            {
                var s = State;
                return s != null && s.HasPickaxe;
            }
        }

        public static bool HasKey
        {
            get
            {
                var s = State;
                return s != null && s.HasKey;
            }
        }

        /// <summary>물체 뿌리(던전 루트 아래). 위치는 월드 좌표.</summary>
        public static GameObject Root(string name, Vector2 pos)
        {
            var go = new GameObject(name);
            var parent = DungeonRoot.Instance ? DungeonRoot.Instance.transform : null;
            if (parent) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go;
        }

        /// <summary>탑다운 앞뒤 정렬(YSort와 같은 식): 아래(앞)에 있을수록 위에 그린다.</summary>
        public static int SortY(float worldY, int offset = 0) => 1000 - Mathf.RoundToInt(worldY * 20f) + offset;

        /// <summary>도형 하나를 자식으로 붙인다. unlit이면 빛을 무시한다(불꽃·반짝임·흰 등불).</summary>
        public static SpriteRenderer Shape(Transform parent, string name, Vector2 local, Vector2 size, Sprite sprite, Color color, int order, bool unlit, float rotationDeg = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            if (unlit) RenderMaterials.MakeUnlit(sr);
            return sr;
        }

        /// <summary>두 점을 잇는 가는 줄(금·분필 획). a, b는 parent 기준 좌표.</summary>
        public static SpriteRenderer Stroke(Transform parent, string name, Vector2 a, Vector2 b, float thickness, Color color, int order, bool unlit)
        {
            Vector2 d = b - a;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            return Shape(parent, name, (a + b) * 0.5f, new Vector2(Mathf.Max(0.01f, d.magnitude), thickness), ShapeSprites.Square, color, order, unlit, angle);
        }

        /// <summary>Wall 레이어 충돌 상자(광맥 바위·금고·말뚝 기둥). 몸이 지나가지 못하게만 한다.</summary>
        public static BoxCollider2D SolidBox(Transform parent, string name, Vector2 local, Vector2 size)
        {
            var go = new GameObject(name);
            go.layer = Layers.Wall;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var col = go.AddComponent<BoxCollider2D>();
            // 말뚝·광맥·금고 같은 작은 물체는 걸음은 막되 시야는 가리지 않는다(좀보이드의 가구처럼).
            VisionSystem.RegisterSeeThrough(col);
            col.size = size;
            return col;
        }

        /// <summary>가장자리가 부드러운 점(불빛 무리·반짝임·흰 등불). 지름 1유닛.</summary>
        public static Sprite SoftDot
        {
            get
            {
                if (_softDot) return _softDot;
                const int size = 64;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v));
                    float a = (1f - d) * (1f - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                _softDot = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
                return _softDot;
            }
        }

        public static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>자리 표시 Param의 각도(도). 읽지 못하면 fallback.</summary>
        public static float ParseAngle(string text, float fallback)
        {
            if (!string.IsNullOrEmpty(text) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float deg)) return deg;
            return fallback;
        }

        /// <summary>플레이어와의 거리(플레이어가 없으면 무한).</summary>
        public static float PlayerDistance(Vector2 pos)
        {
            var player = PlayerController.Instance;
            return player ? (player.Position - pos).magnitude : float.PositiveInfinity;
        }

        /// <summary>문틈의 긴 쪽 방향(폭 4 방향)과 두께 방향.</summary>
        public static Vector2 DoorAxis(DungeonEdge edge) => edge.DoorSize.x >= edge.DoorSize.y ? Vector2.right : Vector2.up;

        public static Vector2 DoorNormal(DungeonEdge edge) => edge.DoorSize.x >= edge.DoorSize.y ? Vector2.up : Vector2.right;

        /// <summary>문 기준 좌표(u = 긴 쪽, v = 두께 쪽) → 문 가운데 기준 좌표.</summary>
        public static Vector2 DoorLocal(DungeonEdge edge, float u, float v) => DoorAxis(edge) * u + DoorNormal(edge) * v;
    }
}
