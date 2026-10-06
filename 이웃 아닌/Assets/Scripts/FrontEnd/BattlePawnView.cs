using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace Demo5.FrontEnd
{
    // Composes a battle standee from its cell (Home) and short-lived motion layers.
    // Only positions, tilt and tint change: no frame animation, per the minimal-motion direction.
    public sealed class BattlePawnView : MonoBehaviour
    {
        public int Unit, Depth, Lane;
        public bool Enemy;
        // Offset/Angle belong to the actor's own motion; Shove/Sway to hit reactions, so both can overlap.
        public Vector3 Home, Offset, Shove;
        public float Lift, Angle, Sway, Squash = 1, Alpha = 1, BasePulse, FlashAmount;
        public Color FlashColor = Color.white, BaseTint = Color.white;
        // Floating creatures hold their body above the base (the base stays on the floor).
        public float Hover;
        public Transform Tilt;
        public SpriteRenderer Body, Flash, Base;
        SpriteRenderer[] parts; Color[] partColors; Vector3[] partScales; int[] partOrders; Color bodyColor; int bodyOrder;
        SortingGroup group;
        // Held pose (a wind-up, a slump after a big move, a bound ally): targets the view eases toward, plus a tremble and a glow.
        float poseAngle, poseSquash = 1, poseLift, poseTremble, poseRate = 18, poseGlow;
        float shownAngle, shownSquash = 1, shownLift, shownTremble, shownGlow;
        Color poseColor = Color.white;
        // Per-standee phase so idle tremble and bob never march in step. Not UnityEngine.Random: battle rolls use it.
        float phase;
        public float Height { get; private set; } = 1.72f;
        public bool Posed => poseAngle != 0 || poseSquash != 1 || poseLift != 0 || poseTremble != 0 || poseGlow != 0;

        public void Setup(Material flashMaterial, float height)
        {
            Height = height; phase = (GetEntityId().GetHashCode() & 1023) * .0137f;
            Body = transform.Find("Body").GetComponent<SpriteRenderer>(); Base = transform.Find("Base").GetComponent<SpriteRenderer>();
            // The shared field pawn also serves exploration. Its walk facing, foot-Y sorting and projected shadow would
            // react to lunges and screen shake here, so the battle keeps the approved facing, lane order and no cast shadow.
            var facing = GetComponent<PawnFacing>(); if (facing) facing.enabled = false;
            var cast = GetComponent<PawnGroundShadow>(); if (cast) cast.enabled = false;
            var castMesh = transform.Find("CastShadow"); if (castMesh) castMesh.gameObject.SetActive(false);
            group = GetComponent<SortingGroup>(); Body.flipX = false; bodyOrder = Body.sortingOrder;
            Tilt = new GameObject("Tilt").transform; Tilt.SetParent(transform, false);
            Body.transform.SetParent(Tilt, false); bodyColor = Body.color;
            if (flashMaterial)
            {
                Flash = new GameObject("Flash", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>(); Flash.transform.SetParent(Tilt, false);
                Flash.transform.localPosition = Body.transform.localPosition; Flash.transform.localScale = Body.transform.localScale;
                Flash.sprite = Body.sprite; Flash.sharedMaterial = flashMaterial; Flash.color = Color.clear;
            }
            parts = GetComponentsInChildren<SpriteRenderer>(true).Where(r => r != Body && r != Flash).ToArray();
            partColors = parts.Select(p => p.color).ToArray(); partScales = parts.Select(p => p.transform.localScale).ToArray(); partOrders = parts.Select(p => p.sortingOrder).ToArray();
        }
        // Nearer lanes draw in front. Parts keep their prefab stacking; only a lane offset is added.
        public void SetSorting(int lane)
        {
            int order = 40 + lane * 8, offset = group ? 0 : order - bodyOrder;
            if (group) group.sortingOrder = order;
            for (int i = 0; i < parts.Length; i++) parts[i].sortingOrder = partOrders[i] + offset;
            Body.sortingOrder = bodyOrder + offset; if (Flash) Flash.sortingOrder = bodyOrder + offset + 1;
        }
        // angle in degrees (+ leans toward the other side of the board for an enemy), squash < 1 crouches wider,
        // tremble = degrees of shiver at rate (Hz-ish), glow = pulsing tint strength. snap skips the ease.
        public void SetPose(float angle, float squash, float lift, float tremble, float rate, float glow, Color glowColor, bool snap = false)
        {
            poseAngle = angle; poseSquash = squash; poseLift = lift; poseTremble = tremble; poseRate = rate; poseGlow = glow; poseColor = glowColor;
            if (snap) { shownAngle = angle; shownSquash = squash; shownLift = lift; shownTremble = tremble; shownGlow = glow; }
        }
        public void ClearPose(bool snap = false) => SetPose(0, 1, 0, 0, poseRate, 0, poseColor, snap);
        float PoseAngleNow => shownAngle + shownTremble * Mathf.Sin((Time.unscaledTime + phase) * poseRate);
        float Bob => Hover > 0 ? Mathf.Sin((Time.unscaledTime + phase) * 2.4f) * .025f : 0;
        public Vector3 Chest => transform.TransformPoint(Tilt.localPosition + Tilt.localRotation * new Vector3(0, (Height - Hover) * .52f, 0));
        public Vector3 Head => transform.position + new Vector3(0, Height + .08f, 0);
        void LateUpdate()
        {
            if (!Tilt) return;
            float ease = 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime);
            shownAngle = Mathf.Lerp(shownAngle, poseAngle, ease); shownSquash = Mathf.Lerp(shownSquash, poseSquash, ease); shownLift = Mathf.Lerp(shownLift, poseLift, ease);
            shownTremble = Mathf.Lerp(shownTremble, poseTremble, ease); shownGlow = Mathf.Lerp(shownGlow, poseGlow, ease);
            transform.localPosition = Home + Offset + Shove + new Vector3(0, Lift, 0);
            Tilt.localPosition = new Vector3(0, Hover + shownLift + Bob, 0);
            Tilt.localRotation = Quaternion.Euler(0, 0, Angle + Sway + PoseAngleNow * (Enemy ? 1 : -1));
            float squash = Squash * shownSquash;
            Tilt.localScale = new Vector3(1 + (1 - squash) * .6f, squash, 1);
            var c = bodyColor; c.a *= Alpha; Body.color = c;
            if (Flash)
            {
                float glow = shownGlow * (.55f + .45f * Mathf.Sin((Time.unscaledTime + phase) * 6));
                bool hit = FlashAmount >= glow;
                var f = hit ? FlashColor : poseColor; f.a *= Mathf.Clamp01(hit ? FlashAmount : glow) * Alpha; Flash.color = f; Flash.flipX = Body.flipX; Flash.flipY = Body.flipY;
            }
            for (int i = 0; i < parts.Length; i++)
            {
                var pc = parts[i] == Base ? BaseTint : partColors[i]; pc.a *= Alpha; parts[i].color = pc;
                if (parts[i] == Base || parts[i].name == "BaseBevel" || parts[i].name == "BaseNeck") parts[i].transform.localScale = partScales[i] * (1 + BasePulse);
            }
        }
    }
}
