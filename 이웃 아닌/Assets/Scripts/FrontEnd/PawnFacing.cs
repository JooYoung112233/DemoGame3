using UnityEngine;
using UnityEngine.Rendering;
namespace Demo5.FrontEnd
{
    public sealed class PawnFacing:MonoBehaviour
    {
        public SpriteRenderer Body;
        public Transform Foot;
        public bool ArtworkFacesRight=true;
        public float MotionThreshold=.0005f,TeleportDistance=2f;
        [Min(.01f)] public float TurnDistance=.12f;
        [Range(0,1)] public float HorizontalIntentRatio=.35f;
        // Added to the depth order (0 = by the feet only): a tie-break for pawns standing on the same line, or a carried copy drawn on top.
        [Tooltip("발 높이로 정한 그리기 순서에 더하는 값 (0 = 발 높이만 · 같은 줄에 선 말의 순서, 들고 가는 실루엣)")] public int SortingBias;
        Vector3 previous;
        float pendingTravel;
        int pendingDirection;
        SortingGroup group;
        void OnEnable(){ResetTracking();group=GetComponent<SortingGroup>();}
        public void Face(bool right)
        {
            if(Body)Body.flipX=right!=ArtworkFacesRight;
            ResetTracking();
        }
        public void ResetTracking(){previous=transform.position;ClearPending();}
        void ClearPending(){pendingTravel=0;pendingDirection=0;}
        // Accumulate deliberate horizontal travel rather than flipping on every frame.
        // Public for deterministic motion replay without entering or changing a scene.
        public void ObservePosition(Vector3 position)
        {
            var delta=position-previous;previous=position;
            if(!Body||delta.sqrMagnitude>=TeleportDistance*TeleportDistance)
            {ClearPending();return;}
            float horizontal=Mathf.Abs(delta.x);
            if(horizontal<=MotionThreshold||horizontal<Mathf.Abs(delta.y)*HorizontalIntentRatio)
            {ClearPending();return;}
            bool right=delta.x>0;
            if(right==(Body.flipX!=ArtworkFacesRight)){ClearPending();return;}
            int direction=right?1:-1;
            if(direction!=pendingDirection){pendingDirection=direction;pendingTravel=0;}
            pendingTravel+=horizontal;
            if(pendingTravel>=Mathf.Max(.01f,TurnDistance))
            {
                Body.flipX=right!=ArtworkFacesRight;
                ClearPending();
            }
        }
        void LateUpdate()
        {
            ObservePosition(transform.position);
            if(group&&Foot)group.sortingOrder=1000-Mathf.RoundToInt((Foot.position.y+5.4f)*100)+SortingBias;
        }
    }
}
