using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 잔혹 소리를 내는 한 곳(기획/다크판타지-분위기-1차.md '소리'·'잔혹'): 젖은 철퍽(SfxKind.Blood)과 무거운 몸 쓰러짐(SfxKind.BodyFall).
    /// 두 종류와 Sfx.PlayScaled는 소리 작업자(B)가 Infra/Sfx.cs에 더했다. 이름이 바뀌면 이 파일만 고치면 된다.
    /// Sfx는 한 프레임에 같은 종류를 한 번만 내므로(회오리 다중 타격), 한 번의 타격에서 가장 큰 쪽 하나만 부른다.
    /// </summary>
    public static class GoreSfx
    {
        /// <summary>피 튀김 철퍽: size 0.4~1.4(타격 크기). 클수록 크고 조금 낮게.</summary>
        public static void Blood(float size)
        {
            float s = Mathf.Clamp(size, 0.4f, 1.4f);
            Sfx.PlayScaled(SfxKind.Blood, s, Mathf.Lerp(1.08f, 0.86f, Mathf.InverseLerp(0.4f, 1.4f, s)));
        }

        /// <summary>무거운 몸 쓰러짐: 멧돼지·정예 시체가 바닥에 닿을 때(날림이 끝난 순간).</summary>
        public static void BodyFall(bool elite)
        {
            Sfx.PlayScaled(SfxKind.BodyFall, elite ? 1.15f : 1f, elite ? 0.9f : 1f);
        }
    }
}
