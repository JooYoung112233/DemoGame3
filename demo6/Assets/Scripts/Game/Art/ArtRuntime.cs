using UnityEngine;

namespace Demo6.Game
{
    public enum ArtMode
    {
        /// <summary>도형과 코드 효과음(그림이 오기 전 기본).</summary>
        Shapes,
        /// <summary>프로젝트의 CombatArtSet 에셋에 연결된 그림·소리. 빈 칸은 도형으로 대신한다.</summary>
        Project,
        /// <summary>코드로 만든 시험용 그림. 교체 자리와 타격 프레임 맞춤을 확인하는 용도.</summary>
        Placeholder,
    }

    /// <summary>지금 쓰는 그림 묶음을 고르고, 그림 피격 번쩍임 재질을 준다.</summary>
    public static class ArtRuntime
    {
        static CombatArtSet _placeholder;
        static Material _runtimeFlash;

        public static ArtMode Mode { get; set; }

        public static CombatArtSet Active
        {
            get
            {
                switch (Mode)
                {
                    case ArtMode.Project:
                        var dungeon = DungeonRoot.Instance;
                        if (dungeon) return dungeon.ProjectArt;
                        var town = TownRoot.Instance;
                        if (town) return town.ProjectArt;
                        var root = CombatTestRoot.Instance;
                        return root ? root.ProjectArt : null;
                    case ArtMode.Placeholder:
                        if (!_placeholder) _placeholder = PlaceholderArt.Build();
                        return _placeholder;
                    default:
                        return null;
                }
            }
        }

        public static Material FlashMaterial
        {
            get
            {
                var set = Active;
                if (set && set.flashMaterial) return set.flashMaterial;
                if (!_runtimeFlash)
                {
                    var shader = Shader.Find("Demo6/SpriteFlash");
                    if (shader) _runtimeFlash = new Material(shader) { name = "SpriteFlash (실행 중)" };
                }
                return _runtimeFlash;
            }
        }

        public static void ResetStatics() => Mode = ArtMode.Shapes;

        /// <summary>그림 렌더러로 바꾼다. 원래 재질은 처음 한 번 기억해 둔다.</summary>
        public static void UseArtMaterial(SpriteRenderer renderer, SpriteFlash flash, ref Material original)
        {
            if (!original) original = renderer.sharedMaterial;
            var mat = FlashMaterial;
            if (mat && renderer.sharedMaterial != mat) renderer.sharedMaterial = mat;
            if (flash) flash.useShader = mat;
        }

        public static void UseShapeMaterial(SpriteRenderer renderer, SpriteFlash flash, Material original)
        {
            if (original && renderer.sharedMaterial != original) renderer.sharedMaterial = original;
            if (flash) flash.useShader = false;
        }

        public static Sprite Frame(SpriteClip clip, int index)
        {
            if (clip == null || !clip.Has || index < 0) return null;
            return clip.frames[Mathf.Min(index, clip.frames.Length - 1)];
        }
    }
}
