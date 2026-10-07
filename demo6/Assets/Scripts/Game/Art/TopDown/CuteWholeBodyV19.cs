using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Head and free-arm joints for the cute right-click motion only.</summary>
    public sealed class CuteWholeBodyV19
    {
        readonly SpriteRenderer _head, _shoulder, _hand;
        readonly MaterialPropertyBlock _flash = new MaterialPropertyBlock();
        float _headAngle, _shoulderAngle, _handAngle;
        public CuteWholeBodyV19(SpriteRenderer head,SpriteRenderer shoulder,SpriteRenderer hand)
        { _head=head;_shoulder=shoulder;_hand=hand;Hide(); }
        public void Hide(){_head.enabled=false;_shoulder.enabled=false;_hand.enabled=false;}
        public void Reset(){Hide();_headAngle=0;_shoulderAngle=0;_handAngle=0;}

        public void Place(CuteArtProfileV15 art,SpriteRenderer body,Vector2 scale,Vector2 offset,float bodyAngle,
            bool whirl,CuteWhirlMotionV18.Pose motion,PlayerPose pose,float dt,CuteWalkV22 gait=null)
        {
            // A short release is allowed in idle/move. Other actions get their own
            // neutral joints immediately, so a right-click pose cannot leak into them.
            bool release=pose==PlayerPose.Idle||pose==PlayerPose.Move;
            float k=1-Mathf.Exp(-55f*dt);
            if(!whirl&&!release){_headAngle=0;_shoulderAngle=0;_handAngle=0;}
            else {
                _headAngle=Mathf.LerpAngle(_headAngle,whirl?motion.Head:0,k);
                _shoulderAngle=Mathf.LerpAngle(_shoulderAngle,whirl?motion.LeftShoulder:0,k);
                _handAngle=Mathf.LerpAngle(_handAngle,whirl?motion.LeftHand:0,k);
            }
            body.GetPropertyBlock(_flash);
            // Crown center over the shoulder line: 26 native pixels back, pivots stay independent.
            Vector2 neck=Vector2.Scale(new Vector2(-.16875f,0),scale);
            Vector2 shoulder=Vector2.Scale(new Vector2(-.01875f,.36875f),scale);
            float walkShoulder=gait!=null?-gait.ShoulderTurn:0f;
            float walkHead=gait!=null?gait.HeadTurn:0f;
            Vector2 headOffset=gait!=null?TopDownCanvas.Rotate(new Vector2(0,gait.HeadShift),bodyAngle):Vector2.zero;
            Vector2 elbow=shoulder+TopDownCanvas.Rotate(Vector2.Scale(new Vector2(-.03125f,.16875f),scale),_shoulderAngle+walkShoulder);
            Put(_head,art.head,neck,bodyAngle+_headAngle+walkHead,bodyAngle,scale,offset+headOffset,body,1);
            Put(_shoulder,art.leftShoulder,shoulder,bodyAngle+_shoulderAngle+walkShoulder,bodyAngle,scale,offset,body,-1);
            Put(_hand,art.leftHand,elbow,bodyAngle+_shoulderAngle+walkShoulder+_handAngle+(gait!=null?gait.HandTurn:0f),bodyAngle,scale,offset,body,0);
        }

        void Put(SpriteRenderer sr,Sprite sprite,Vector2 root,float angle,float bodyAngle,Vector2 scale,Vector2 offset,SpriteRenderer body,int order)
        {
            sr.enabled=true;sr.sprite=sprite;sr.sharedMaterial=body.sharedMaterial;
            sr.color=body.color;sr.SetPropertyBlock(_flash);
            sr.sortingLayerID=body.sortingLayerID;sr.sortingOrder=body.sortingOrder+order;sr.forceRenderingOff=body.forceRenderingOff;
            sr.transform.localPosition=TopDownCanvas.Rotate(root,bodyAngle)+offset;
            sr.transform.localRotation=Quaternion.Euler(0,0,angle);
            sr.transform.localScale=new Vector3(scale.x,scale.y,1);
        }
    }
}
