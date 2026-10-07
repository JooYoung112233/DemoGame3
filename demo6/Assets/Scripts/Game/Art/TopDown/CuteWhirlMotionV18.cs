using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Visual-only poses for the existing 0.6s / three-hit right-click skill.</summary>
    public static class CuteWhirlMotionV18
    {
        public struct Pose
        {
            public float Arm, Shoulder, Body, Lean, Cape;
            public float Head, LeftShoulder, LeftHand;
            public Pose(float arm, float shoulder, float body, float lean, float cape)
            { Arm=arm; Shoulder=shoulder; Body=body; Lean=lean; Cape=cape; Head=0;LeftShoulder=0;LeftHand=0; }
        }

        // Anticipate before 0.1, cut across 0.1/0.3/0.5, then close the pose by 0.6.
        // An elbow does not windmill three complete revolutions around a fixed torso.
        static readonly float[] Times = { 0f, .055f, .14f, .22f, .36f, .42f, .54f, .60f };
        static readonly Pose[] Keys = {
            new Pose(0,0,0,0,0),
            new Pose(-55,-14,-6,-.024f,-.35f),
            new Pose(80,20,7,.024f,1),
            new Pose(76,18,5,.008f,.25f),
            new Pose(-72,-18,-7,-.020f,-1),
            new Pose(-68,-17,-5,-.008f,-.25f),
            new Pose(90,20,7,.024f,1),
            new Pose(0,0,0,0,0)
        };

        public static Pose Sample(float time, float duration, bool wholeBody = false)
        {
            float t = Mathf.Clamp01(time / Mathf.Max(.001f,duration)) * .6f;
            var pose=At(t);
            if(!wholeBody||t<=0f||t>=.6f)return pose;
            // Hips/chest lead the sword; the head follows later at a smaller angle.
            // The free arm counterbalances rather than copying the sword arm.
            float envelope=Mathf.SmoothStep(0,1,t/.035f)*(1-Mathf.SmoothStep(0,1,(t-.55f)/.05f));
            var lead=At(Mathf.Min(.6f,t+.020f));
            var shoulder=At(Mathf.Min(.6f,t+.014f));
            var late=At(Mathf.Max(0,t-.024f));
            pose.Body=lead.Body*2.5f*envelope;
            pose.Lean=lead.Lean*1.8f*envelope;
            pose.Shoulder=shoulder.Shoulder*envelope;
            pose.Head=(late.Body*1.05f-pose.Body)*envelope;
            pose.LeftShoulder=(-late.Shoulder*.65f)*envelope;
            pose.LeftHand=(-At(Mathf.Max(0,t-.038f)).Arm*.32f)*envelope;
            return pose;
        }

        static Pose At(float t)
        {
            for(int i=1;i<Times.Length;i++) if(t<=Times[i]) {
                float u=Mathf.SmoothStep(0f,1f,(t-Times[i-1])/(Times[i]-Times[i-1]));
                var a=Keys[i-1];var b=Keys[i];
                return new Pose(Mathf.Lerp(a.Arm,b.Arm,u),Mathf.Lerp(a.Shoulder,b.Shoulder,u),
                    Mathf.Lerp(a.Body,b.Body,u),Mathf.Lerp(a.Lean,b.Lean,u),Mathf.Lerp(a.Cape,b.Cape,u));
            }
            return default;
        }
    }
}
