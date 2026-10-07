namespace Demo6.Core.Dungeon
{
    /// <summary>
    /// 웅크리기(앉기, 2026-10-04 결정 ③ — 사용자 원문 "넣는대신 걷기나 앉기도 구현해야할듯? 앉으면 시야 좁아지고?").
    /// 키 C로 켜고 끈다(구르기·공격·스킬을 쓰면 일어선다). 웅크리면 느리고 조용해져 잠든 무리를 덜 깨우고, 대신 시야가 좁아진다.
    /// 기습 처형(ExecutionRule.Ambush)은 웅크린 채 들키지 않은 적의 등 뒤 ±60°(BackstabRule.IsBehind)에서 친 근접 첫 타에만 걸린다.
    /// 숫자는 가안이다. Tuning(Crouch*)이 이 값을 기본값으로 쓰고 시험 패널에서 바꾼다.
    /// 계약(꾸러미 ①이 값과 시험을, 꾸러미 ⑤가 PlayerController 상태를, ①이 시야·소음을, ⑦이 몸 그림을, ⑧이 Enemy 감지를 맡는다).
    /// </summary>
    public static class CrouchRules
    {
        /// <summary>걸음 배율(× 0.55). 아는 길 덮어쓰기는 쓰지 않고 장비 걸음에 곱한다.</summary>
        public const float MoveScale = 0.55f;
        /// <summary>
        /// 발소리·소음 반경 배율(× 0.3): 발소리 음량, 큰 소리(곡괭이·광맥·금고) 반경 12 → 3.6, 잠든 적의 등 뒤 감지(듣기)는 몸 사이 거리에 곱한다
        /// (HearCenterDistance: 굴쥐 3 → 1.36, 멧돼지 3 → 1.57).
        /// </summary>
        public const float NoiseScale = 0.3f;
        /// <summary>시야 부채꼴 반각 65° → 40°.</summary>
        public const float ConeHalfAngle = 40f;
        /// <summary>시야 반경 14 → 10.</summary>
        public const float ViewRadius = 10f;
        /// <summary>몸 둘레 늘 보는 반경 2.5 → 2.0.</summary>
        public const float NearRadius = 2.0f;
        /// <summary>시야·몸 그림이 웅크림으로 바뀌는 시간(초, 그림만 부드럽게).</summary>
        public const float BlendSeconds = 0.15f;
        /// <summary>정수리 몸 그림: 크기 × 0.9, 밝기 × 0.75(코드 임시).</summary>
        public const float BodyScale = 0.9f;
        public const float BodyBrightness = 0.75f;
        /// <summary>HUD 한 줄 글.</summary>
        public const string HudLabel = "웅크림";

        /// <summary>소음 배율: 웅크리면 noiseScale(기본 0.3), 아니면 1.</summary>
        public static float NoiseFactor(bool crouching, float noiseScale = NoiseScale) => crouching ? noiseScale : 1f;

        /// <summary>듣는 반경 = 바탕 반경 × 소음 배율(큰 소리 12 → 3.6). 몸 둘레에서 재는 감지는 HearCenterDistance를 쓴다.</summary>
        public static float HearRadius(float baseRadius, bool crouching, float noiseScale = NoiseScale) => baseRadius * NoiseFactor(crouching, noiseScale);

        /// <summary>
        /// 잠든 적이 등 뒤 소리로 알아채는 중심 거리: 몸이 닿는 거리(contact = 두 반지름 합)는 그대로 두고 그 바깥(몸 사이 틈)만 소음 배율로 줄인다.
        /// = contact + (바탕 − contact) × 배율. 배율 1이면 바탕 그대로(서 있으면 예전과 같음). 바탕 3, 웅크림 0.3이면 굴쥐(0.65) 1.355, 궁수(0.7) 1.39,
        /// 멧돼지(0.95) 1.565, 오우거(1.6, 바탕 3) 2.02. 중심 거리에 곱하면(0.9) 몸이 닿아도 못 듣는 적이 생겨 '덜 깸'이 '절대 안 깸'이 된다.
        /// 문서 4-1 '숨죽인 걸음'(등 뒤 3 → 1.5)과 비슷한 값이다. 바탕이 contact보다 작으면 바탕 그대로.
        /// </summary>
        public static float HearCenterDistance(float baseDistance, float contact, float noiseScale)
        {
            if (baseDistance <= contact || noiseScale >= 1f) return baseDistance;
            float scale = noiseScale < 0f ? 0f : noiseScale;
            return contact + (baseDistance - contact) * scale;
        }

        /// <summary>웅크림 섞기 한 걸음(unscaled 초 dt): 0(서 있음) ↔ 1(웅크림)을 BlendSeconds(0.15초)에 걸쳐 오간다.</summary>
        public static float StepBlend(float blend, bool crouching, float dt)
        {
            float step = BlendSeconds > 0f ? dt / BlendSeconds : 1f;
            if (dt <= 0f) step = 0f;
            float next = crouching ? blend + step : blend - step;
            return next < 0f ? 0f : next > 1f ? 1f : next;
        }

        /// <summary>섞기(0~1)에 맞춘 값: 서 있을 때 값에서 웅크린 값으로(시야 반각·반경·몸 둘레 공용).</summary>
        public static float Blend(float standing, float crouched, float blend)
        {
            float t = blend < 0f ? 0f : blend > 1f ? 1f : blend;
            return standing + (crouched - standing) * t;
        }
    }
}
