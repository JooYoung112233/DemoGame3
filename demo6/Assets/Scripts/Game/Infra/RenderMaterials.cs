using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 3차 초안 2-4 '손맛 효과도 빛을 무시한다': 예고·불꽃·파편·검기·화살·빛기둥·정예 테두리는 빛을 받지 않는 재질로 그린다.
    /// 2D 렌더러의 기본 스프라이트 재질은 빛을 받는다(어둠 속에서 몸·벽이 가려짐). 빛이 없는 전투 시험장에서는 둘이 같아 보인다.
    /// </summary>
    public static class RenderMaterials
    {
        const string UnlitShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";

        static Material _unlit;
        static bool _searched;

        /// <summary>던전 시험장이 에셋으로 넣어 주면 그것을 쓴다(빌드에서 셰이더가 빠지지 않게).</summary>
        public static void SetUnlit(Material material)
        {
            if (material) _unlit = material;
        }

        public static Material Unlit
        {
            get
            {
                if (_unlit || _searched) return _unlit;
                _searched = true;
                var shader = Shader.Find(UnlitShader);
                if (shader) _unlit = new Material(shader) { name = "Sprite-Unlit (runtime)" };
                return _unlit;
            }
        }

        /// <summary>빛을 무시하게 바꾼다. 재질을 찾지 못하면 그대로 둔다.</summary>
        public static void MakeUnlit(SpriteRenderer sr)
        {
            if (!sr) return;
            var m = Unlit;
            if (m) sr.sharedMaterial = m;
        }

        public static void ResetStatics()
        {
            _searched = false;
        }
    }
}
