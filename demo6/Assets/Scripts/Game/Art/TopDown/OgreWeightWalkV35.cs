using UnityEngine;

namespace Demo6.Game
{
    // Presentation only. A footfall drives weight, torso, then opposite shoulder/head.
    // Contact positions are world anchors; no velocity, AI, hitbox or timer is written.
    public sealed class OgreWeightWalkV35
    {
        public const float Stride=1.85f, Stance=.64f;
        readonly Vector2[] plant=new Vector2[2], lift=new Vector2[2];
        readonly bool[] planted=new bool[2], lifted=new bool[2];
        Vector2 previous,direction=Vector2.down;
        bool ready;
        // At half-stance, both the supporting foot and the mid-swing foot start at Home.
        float cycle=Stance*.5f,amount;
        public Vector2 Left {get;private set;}
        public Vector2 Right {get;private set;}
        public bool LeftPlanted {get{return planted[0];}}
        public bool RightPlanted {get{return planted[1];}}
        public float Amount {get{return amount;}}
        public float Cycle {get{return cycle;}}
        float Phase {get{return cycle*Mathf.PI*2;}}
        public float WeightShift {get{return Mathf.Sin(Phase-.10f)*.085f*amount;}}
        public float TorsoTurn {get{return Mathf.Sin(Phase-.32f)*5.5f*amount;}}
        public float ShoulderTurn {get{return Mathf.Sin(Phase-.60f)*7.0f*amount;}}
        public float ClubFollow {get{return -Mathf.Sin(Phase-.92f)*2.2f*amount;}}
        public float HeadTurn {get{return Mathf.Sin(Phase-1.02f)*2.0f*amount;}}
        public static Vector2 Home(int i){return new Vector2(i==0?-97f/160f:97f/160f,-180f/160f);}
        static Vector2 Turn(Vector2 p,float angle){float r=angle*Mathf.Deg2Rad;return new Vector2(p.x*Mathf.Cos(r)-p.y*Mathf.Sin(r),p.x*Mathf.Sin(r)+p.y*Mathf.Cos(r));}
        public void Reset(Vector2 position)
        {
            previous=position;ready=true;cycle=Stance*.5f;amount=0;
            for(int i=0;i<2;i++){planted[i]=false;lifted[i]=false;}
            Left=Home(0);Right=Home(1);
        }
        public void Drive(Vector2 position,float angle,float dt,bool locomotion,bool moving)
        {
            if(!ready||(position-previous).sqrMagnitude>4f)Reset(position);
            if(dt<=0)return; // Hit-stop must not advance phase, releases or contact history.
            Vector2 delta=position-previous;previous=position;
            if(!locomotion){amount=Mathf.MoveTowards(amount,0,dt*8);planted[0]=planted[1]=false;lifted[0]=lifted[1]=false;return;}
            float distance=delta.magnitude;bool travel=moving&&distance>.00001f;
            amount=Mathf.MoveTowards(amount,travel?1:0,dt*(travel?5:6));
            if(travel){direction=delta/distance;cycle=Mathf.Repeat(cycle+distance/Stride,1);}
            Vector2 local=Turn(direction,-angle);
            Left=Foot(0,cycle,position,angle,local,travel);
            Right=Foot(1,Mathf.Repeat(cycle+.5f,1),position,angle,local,travel);
        }
        Vector2 Foot(int i,float phase,Vector2 position,float angle,Vector2 local,bool travel)
        {
            Vector2 home=Home(i);float half=Stride*Stance*.5f;
            bool contact=travel&&phase<Stance;Vector2 result;
            if(contact)
            {
                if(!planted[i])plant[i]=position+Turn(home+local*(half-Stride*phase),angle);
                result=Turn(plant[i]-position,-angle);
            }
            else if(travel)
            {
                if(planted[i]){lift[i]=plant[i];lifted[i]=true;}
                float u=Mathf.Clamp01((phase-Stance)/(1-Stance));float k=u*u*(3-2*u);
                Vector2 landing=position+Turn(home+local*half,angle);
                if(!lifted[i]){lift[i]=position+Turn(home-local*half,angle);lifted[i]=true;}
                result=Turn(Vector2.Lerp(lift[i],landing,k)-position,-angle);
            }
            else
            {
                // Stopping plants both feet where they finished; the upper body settles above.
                result=i==0?Left:Right;
                lifted[i]=false;
            }
            planted[i]=contact;return result;
        }
    }
}
