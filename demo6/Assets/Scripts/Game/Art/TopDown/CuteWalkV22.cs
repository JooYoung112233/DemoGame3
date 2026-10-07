using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Visual-only gait. Distance drives cadence; visible toes remain close to the hips.</summary>
    public sealed class CuteWalkV22
    {
        public const float Stride = 1.45f;
        public const float Stance = .58f;
        Vector2 _last, _direction = Vector2.right;
        readonly Vector2[] _plants = new Vector2[2];
        readonly Vector2[] _lifts = new Vector2[2];
        readonly Vector2[] _contacts = new Vector2[2];
        readonly bool[] _planted = new bool[2];
        bool _ready;
        float _cycle, _amount;
        float _rootScale=1f;
        public Vector2 Left { get; private set; }
        public Vector2 Right { get; private set; }
        public float Amount => _amount;
        float Phase => _cycle*Mathf.PI*2f;
        // Readable weight transfer in the fixed overhead camera; contact history keeps the existing cadence.
        public float BodyTurn => Mathf.Sin(Phase-.25f)*4.2f*_amount;
        float BodyShift => Mathf.Sin(Phase-.08f)*.040f*_amount;
        public Vector2 BodyOffset => Lean+new Vector2(0,BodyShift);
        // The head follows the weight transfer slightly later, without scaling or bouncing.
        public float HeadTurn => Mathf.Sin(Phase-.90f)*2.3f*_amount-BodyTurn;
        public float HeadShift => Mathf.Sin(Phase-.65f)*.026f*_amount-BodyShift;
        public float ShoulderTurn => Mathf.Sin(Phase-.55f)*7.0f*_amount;
        public float HandTurn => -Mathf.Sin(Phase-.80f)*3.6f*_amount;
        public Vector2 Lean { get; private set; }
        public bool LeftPlanted => _planted[0];
        public bool RightPlanted => _planted[1];

        public void Reset(Vector2 position)
        {
            _last=position; _ready=true; _cycle=.25f; _amount=0;
            _planted[0]=_planted[1]=false; Lean=Vector2.zero;
            _lifts[0]=_lifts[1]=Vector2.zero;
            Left=_contacts[0]=new Vector2(.285f,.19f); Right=_contacts[1]=new Vector2(.285f,-.19f);
        }

        public void Drive(Vector2 position, float angle, float dt, bool locomotion, bool moving, float rootScale=1f)
        {
            _rootScale=Mathf.Max(.01f,rootScale);
            if(!_ready || (position-_last).sqrMagnitude>4f) Reset(position);
            Vector2 delta=position-_last; _last=position;
            // Actions never inherit gait deformation or a planted foot from walking.
            if(!locomotion) { Reset(position); return; }
            if(dt<=0) return;
            float distance=delta.magnitude;
            bool travel=moving && distance>.00001f;
            float speed=travel?distance/dt:0;
            float k=1f-Mathf.Exp(-18f*dt);
            _amount=Mathf.MoveTowards(_amount,travel?1f:0f,dt*(travel?10f:8f));
            if(travel) {
                _direction=delta/distance;
                _cycle=Mathf.Repeat(_cycle+distance/(Stride*_rootScale),1f);
            }
            Vector2 localDirection=TopDownCanvas.Rotate(_direction,-angle);
            Lean=Vector2.Lerp(Lean,localDirection*Mathf.Min(speed*.002f,.009f),k);
            _contacts[0]=Foot(0,.19f,_cycle,position,angle,localDirection,travel);
            _contacts[1]=Foot(1,-.19f,Mathf.Repeat(_cycle+.5f,1f),position,angle,localDirection,travel);
            Left=VisibleFoot(_contacts[0],.19f,localDirection);
            Right=VisibleFoot(_contacts[1],-.19f,localDirection);
        }

        Vector2 VisibleFoot(Vector2 contact,float side,Vector2 direction)
        {
            var hip=new Vector2(.285f,side);
            // Keep the real traveled-distance phase, but show only a short fore/aft step.
            // Strafing must not spread the toes along local Y, away from their attached legs.
            float step=Mathf.Clamp(Vector2.Dot(contact-hip,direction)*.22f,-.09f,.09f);
            return TopDownCanvas.Rotate(hip+new Vector2(step,0),BodyTurn)+BodyOffset;
        }

        Vector2 Foot(int index,float side,float phase,Vector2 position,float angle,Vector2 direction,bool travel)
        {
            var home=new Vector2(.285f,side);
            float half=Stride*Stance*.5f;
            bool stance=travel && phase<Stance;
            Vector2 result;
            if(stance) {
                if(!_planted[index]) {
                    var contact=home+direction*(half-Stride*phase);
                    _plants[index]=position+TopDownCanvas.Rotate(contact*_rootScale,angle);
                }
                result=TopDownCanvas.Rotate(_plants[index]-position,-angle)/_rootScale;
            } else {
                float u=Mathf.Clamp01((phase-Stance)/(1f-Stance));
                // Hermite recovery: no sharp reversal at either end; most travel is hidden under the body.
                float recover=u*u*(3f-2f*u);
                if(_planted[index]) _lifts[index]=_plants[index];
                if(travel && _lifts[index]!=Vector2.zero){
                    Vector2 landing=position+TopDownCanvas.Rotate((home+direction*half)*_rootScale,angle);
                    result=TopDownCanvas.Rotate(Vector2.Lerp(_lifts[index],landing,recover)-position,-angle)/_rootScale;
                } else result=home+direction*Mathf.Lerp(-half,half,recover);
                if(!travel) result=Vector2.Lerp(home,_contacts[index],_amount);
            }
            _planted[index]=stance;
            return result;
        }
    }
}
