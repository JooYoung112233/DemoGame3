using Demo6.Core.Dungeon;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 계단(3차 초안 2-3 S, 매판 새 탐험 1차 5장 단계 4). F로 DungeonRoot.Descend: 같은 원정 그대로 체력·물약을 들고 아래층 승강장으로 장면을 다시 불러온다.
    /// 아래층이 시험판에 없고 계단 아래가 오우거 굴이면(2층) 같은 원정으로 굴 장면에 내려간다(프롬프트 '굴로 내려가기'). 둘 다 없으면 Descend가 막혔다는 글만 띄운다.
    /// 오른쪽으로 내려가는 계단 단을 바닥 그림으로 그린다(빛을 받음).
    /// 계단 밑 기척(1-2층 탐험 맛 1차 4-11, 나 단계): 아래가 오우거 굴이면(DescendsToDen) 이번 원정에 오우거를 잡지 않은 동안,
    /// 플레이어가 RumbleRange 안에 있으면 10~16초(게임 시간)마다 돌 씹는 소리(OgreSounds Chew)를 작게 낸다. 가까울수록 크게(바로 옆 0.3, 거리 끝 0.12), 음높이 0.8.
    /// 그 장면에서 처음 들릴 때 한 번 알림(ExploreText.DenRumbleFirst, 이 물체의 필드로 셈 — 장면이 바뀌면 저절로 풀림).
    /// 멈춤·창·쓰러짐·소리 끔(Tuning.Sound)·기척 끔(LurkSounds.Enabled) 동안은 내지 않는다(읽기만). 새 소리 파일 없음(지금 있는 합성음).
    /// </summary>
    public sealed class Stairs : Interactable
    {
        const int Steps = 5;
        const float Width = 2.6f;
        const float Depth = 2.4f;

        static readonly Color Rim = new Color(0.36f, 0.32f, 0.28f);
        static readonly Color StepTop = new Color(0.46f, 0.41f, 0.36f);
        static readonly Color Hole = new Color(0.05f, 0.045f, 0.04f);

        // ── 1-2층 탐험 맛 1차 4-11 계단 밑 기척 ──
        /// <summary>돌 씹는 소리가 들리는 거리(계단 가운데에서, 유닛).</summary>
        const float RumbleRange = 18f;
        /// <summary>소리 사이(초, 게임 시간).</summary>
        const float RumbleGapMin = 10f;
        const float RumbleGapMax = 16f;
        /// <summary>거리 안에 들어선 뒤 첫 소리까지(초). 지난 소리에서 RumbleGapMin은 지난 뒤에 낸다(거리 끝을 오가도 잦아지지 않게).</summary>
        const float RumbleFirstMin = 1f;
        const float RumbleFirstMax = 3f;
        /// <summary>음량: 바로 옆 0.3 → 거리 끝 0.12(OgreSounds가 0.1 칸으로 굽는다).</summary>
        const float RumbleNear = 0.3f;
        const float RumbleFar = 0.12f;
        const float RumblePitch = 0.8f;

        string _prompt = "아래로 내려가기";
        /// <summary>계단 아래가 오우거 굴인가(Create 때 DungeonRoot.DescendsToDen).</summary>
        bool _denBelow;
        /// <summary>다음 소리 게임 시각(거리 밖이면 -1).</summary>
        float _rumbleAt = -1f;
        float _rumbleLast = -99f;
        System.Random _rumbleRng;

        /// <summary>이 장면에서 낸 계단 밑 소리 수(확인용).</summary>
        public int RumblesPlayed { get; private set; }
        /// <summary>이 장면에서 첫 알림을 띄웠는가(확인용, 장면에 한 번).</summary>
        public bool RumbleAnnounced { get; private set; }

        public override string Prompt => _prompt;
        public override float Range => 2.2f;

        public static Stairs Create(DungeonCell cell, CellFeature f, Vector2 pos)
        {
            var go = WorldProps.Root("Stairs " + f.Id, pos);
            var stairs = go.AddComponent<Stairs>();
            // 계단 아래가 오우거 굴이면(2층, 아래층이 시험판에 없음) '굴로 내려가기'.
            var root = DungeonRoot.Instance;
            stairs._prompt = root && root.DescendsToDen ? OgreDen.StairsPrompt : (WorldProps.Floor + 1) + "층으로 내려가기";
            stairs._denBelow = root && root.DescendsToDen;
            stairs.Build();
            return stairs;
        }

        /// <summary>계단 밑 기척(4-11): 거리 안에 있으면 10~16초마다 돌 씹는 소리, 장면 첫 소리에 알림 한 번.</summary>
        void Update()
        {
            if (!_denBelow) return;
            var root = DungeonRoot.Instance;
            var player = PlayerController.Instance;
            if (!root || !player || !root.Playing || player.IsDown || TimeScaleService.Paused || DungeonUi.ModalOpen) return;
            float dist = (player.Position - Position).magnitude;
            // 거리 밖·소리 끔·기척 끔이면 시계를 내려놓는다(다시 들어서면 처음처럼 조금 뒤 첫 소리).
            if (dist > RumbleRange || !Tuning.Sound || !LurkSounds.Enabled)
            {
                _rumbleAt = -1f;
                return;
            }
            float now = Time.time;
            if (_rumbleAt < 0f)
            {
                _rumbleAt = Mathf.Max(now + RumbleRandom(RumbleFirstMin, RumbleFirstMax), _rumbleLast + RumbleGapMin);
                return;
            }
            if (now < _rumbleAt) return;
            _rumbleAt = now + RumbleRandom(RumbleGapMin, RumbleGapMax);
            // 이번 원정에 오우거를 잡았으면 내지 않는다(굴에서 올라와도 같은 원정이면 조용).
            if (BossLedger.KilledThisExpedition(ProfileCarry.Ensure(), OgreDen.BossId, root.Expedition)) return;
            _rumbleLast = now;
            float volume = Mathf.Lerp(RumbleNear, RumbleFar, Mathf.Clamp01(dist / RumbleRange));
            OgreSounds.Play(OgreSound.Chew, volume, RumblePitch);
            RumblesPlayed++;
            if (RumbleAnnounced) return;
            RumbleAnnounced = true;
            DungeonEvents.Say(ExploreText.DenRumbleFirst);
        }

        /// <summary>전역 Random 순서를 건드리지 않게 자기 난수로 고른다(LurkSounds와 같은 까닭).</summary>
        float RumbleRandom(float min, float max)
        {
            // Unity 6.6에서 GetInstanceID는 컴파일 오류(CS0619)라 물체 이름(계단 자리 id)을 섞는다.
            if (_rumbleRng == null) _rumbleRng = new System.Random(unchecked((int)(System.DateTime.Now.Ticks & 0x7fffffff) ^ name.GetHashCode()));
            return min + (float)_rumbleRng.NextDouble() * (max - min);
        }

        void Build()
        {
            int order = WorldProps.FloorDecalOrder;
            WorldProps.Shape(transform, "Rim", Vector2.zero, new Vector2(Width + 0.3f, Depth + 0.3f), ShapeSprites.Square, Rim, order, false);
            WorldProps.Shape(transform, "Hole", Vector2.zero, new Vector2(Width, Depth), ShapeSprites.Square, Hole, order + 1, false);
            // 왼쪽(들어오는 쪽)에서 오른쪽으로 갈수록 낮고 어두운 단.
            float stepW = Width / Steps;
            for (int i = 0; i < Steps; i++)
            {
                float k = i / (float)(Steps - 1);
                var c = Color.Lerp(StepTop, Hole, k * 0.85f);
                float x = -Width * 0.5f + stepW * (i + 0.5f);
                WorldProps.Shape(transform, "Step", new Vector2(x, 0f), new Vector2(stepW * 0.82f, Depth - 0.1f), ShapeSprites.Square, c, order + 2 + i, false);
            }
        }

        public override void Interact()
        {
            var root = DungeonRoot.Instance;
            if (root) root.Descend();
        }
    }
}
