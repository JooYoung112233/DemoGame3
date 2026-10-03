using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>
    /// 실행 중 칸 바닥·벽 덩이마다 따로 만든 그림(ShapeSprites.DirtFloor·StoneWall·RoughRock)을 물체와 함께 지운다.
    /// 판자벽·금 간 벽이 부서져 물체가 지워지면 그 그림도 같이 풀린다(기획/다크판타지-분위기-1차.md '땅·벽').
    /// </summary>
    public sealed class RuntimeAssetOwner : MonoBehaviour
    {
        readonly List<Object> _owned = new List<Object>(2);

        /// <summary>그림과 그 텍스처를 go가 지워질 때 함께 지우게 맡긴다.</summary>
        public static void Own(GameObject go, Sprite sprite)
        {
            if (!go || !sprite) return;
            var owner = go.GetComponent<RuntimeAssetOwner>();
            if (!owner) owner = go.AddComponent<RuntimeAssetOwner>();
            owner._owned.Add(sprite);
            if (sprite.texture) owner._owned.Add(sprite.texture);
        }

        void OnDestroy()
        {
            for (int i = 0; i < _owned.Count; i++)
                if (_owned[i]) Destroy(_owned[i]);
            _owned.Clear();
        }
    }
}
