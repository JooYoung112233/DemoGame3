using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;
namespace Demo5.FrontEnd
{
    // Inspector-authored crops remove transparent padding without changing source artwork.
    [RequireComponent(typeof(Image))]
    public sealed class BattlePortraitFit : BaseMeshEffect
    {
        [Serializable] public struct Crop { public Sprite Sprite; public Rect Area; }
        public Crop[] Crops;
        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive()) return;
            var image = (Image)graphic;
            var sprite = image.overrideSprite ? image.overrideSprite : image.sprite;
            if (!sprite) return;
            Rect crop = new Rect(0, 0, 1, 1);
            if (Crops != null) foreach (var entry in Crops) if (entry.Sprite == sprite) { crop = entry.Area; break; }
            if (crop.width <= 0 || crop.height <= 0) return;
            var r = image.rectTransform.rect;
            float aspect = sprite.rect.width * crop.width / (sprite.rect.height * crop.height);
            float w = Mathf.Min(r.width, r.height * aspect), h = w / aspect;
            var mid = r.center; var uv = DataUtility.GetOuterUV(sprite);
            // OuterUV already excludes Unity's trimmed transparent border.
            var trimmed = sprite.textureRect; var offset = sprite.textureRectOffset;
            float u0 = Mathf.LerpUnclamped(uv.x, uv.z, (crop.xMin * sprite.rect.width - offset.x) / trimmed.width);
            float u1 = Mathf.LerpUnclamped(uv.x, uv.z, (crop.xMax * sprite.rect.width - offset.x) / trimmed.width);
            float v0 = Mathf.LerpUnclamped(uv.y, uv.w, (crop.yMin * sprite.rect.height - offset.y) / trimmed.height);
            float v1 = Mathf.LerpUnclamped(uv.y, uv.w, (crop.yMax * sprite.rect.height - offset.y) / trimmed.height);
            mesh.Clear();
            mesh.AddVert(new Vector3(mid.x-w/2, mid.y-h/2), image.color, new Vector2(u0,v0));
            mesh.AddVert(new Vector3(mid.x-w/2, mid.y+h/2), image.color, new Vector2(u0,v1));
            mesh.AddVert(new Vector3(mid.x+w/2, mid.y+h/2), image.color, new Vector2(u1,v1));
            mesh.AddVert(new Vector3(mid.x+w/2, mid.y-h/2), image.color, new Vector2(u1,v0));
            mesh.AddTriangle(0,1,2); mesh.AddTriangle(0,2,3);
        }
    }
}
