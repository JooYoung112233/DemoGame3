using System.Linq;
using UnityEngine;
namespace Demo5.FrontEnd {
 // Presentation only: moving a piece never advances time or completes a job.
 public sealed class SettlementPawnMotion:MonoBehaviour {
  public Transform[] Pawns,Bodies,Bases,WorkBadges;
  public Transform[] WorkPositions;
  [Min(.1f)] public float Speed=2.4f;
  [Range(0,8)] public float LeanDegrees=2.5f;
  public float AisleY=-1.15f;
  public bool MovingAt(int index)=>moving!=null&&index>=0&&index<moving.Length&&moving[index];
  public bool IsMoving=>moving!=null&&moving.Any(x=>x);
  SettlementController owner;
  Vector3[] home,footOffset,start,target,via;
  Quaternion[] rotations;
  bool[] assigned,moving,secondLeg;
  float[] progress;
  void Start(){
   owner=FindAnyObjectByType<SettlementController>();
   int n=Pawns.Length;home=new Vector3[n];footOffset=new Vector3[n];start=new Vector3[n];target=new Vector3[n];via=new Vector3[n];rotations=new Quaternion[n];assigned=new bool[n];moving=new bool[n];secondLeg=new bool[n];progress=new float[n];
   for(int i=0;i<n;i++){home[i]=Pawns[i].position;footOffset[i]=Bases[i].position-Pawns[i].position;rotations[i]=Bodies[i].localRotation;WorkBadges[i].gameObject.SetActive(false);}
  }
  void Update(){
   if(!owner||owner.Campaign==null||home==null)return;
   var people=owner.Campaign.Party.ToArray();
   for(int i=0;i<Pawns.Length&&i<people.Length;i++){
    bool work=owner.CraftPanel&&owner.CraftPanel.IsAssigned(people[i]);
    if(work!=assigned[i]){
     assigned[i]=work;WorkBadges[i].gameObject.SetActive(false);
     target[i]=work?WorkPositions[i%WorkPositions.Length].position-footOffset[i]:home[i];
     // Keep to the open aisle, then approach the front of the workbench.
     var feet=target[i]+footOffset[i];via[i]=new Vector3(feet.x,AisleY,feet.z)-footOffset[i];
     start[i]=Pawns[i].position;progress[i]=0;secondLeg[i]=false;moving[i]=true;
    }
    if(!moving[i]){Bodies[i].localRotation=rotations[i]*Quaternion.Euler(0,0,assigned[i]?Mathf.Sin(Time.time*3.1f+i)*.85f:0);continue;}
    var end=secondLeg[i]?target[i]:via[i];float distance=Vector3.Distance(start[i],end);
    progress[i]+=Time.deltaTime*Speed/Mathf.Max(.01f,distance);
    float t=Mathf.Clamp01(progress[i]),ease=t*t*(3-2*t);
    Pawns[i].position=Vector3.Lerp(start[i],end,ease);
    float direction=Mathf.Abs(end.x-start[i].x)>.01f?Mathf.Sign(end.x-start[i].x):1;
    Bodies[i].localRotation=rotations[i]*Quaternion.Euler(0,0,-direction*LeanDegrees*Mathf.Sin(t*Mathf.PI));
    if(t>=1){Bodies[i].localRotation=rotations[i];if(!secondLeg[i]){secondLeg[i]=true;start[i]=Pawns[i].position;progress[i]=0;}else{moving[i]=false;WorkBadges[i].gameObject.SetActive(assigned[i]);}}
   }
  }
 }
}
