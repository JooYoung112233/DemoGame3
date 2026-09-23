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
  bool[] assigned,moving;
  int[] leg;
  float[] progress;
  void Start(){
   owner=FindAnyObjectByType<SettlementController>();
   int n=Pawns.Length;home=new Vector3[n];footOffset=new Vector3[n];start=new Vector3[n];target=new Vector3[n];via=new Vector3[n];rotations=new Quaternion[n];assigned=new bool[n];moving=new bool[n];leg=new int[n];progress=new float[n];
   for(int i=0;i<n;i++){home[i]=Pawns[i].position;footOffset[i]=Bases[i].position-Pawns[i].position;rotations[i]=Bodies[i].localRotation;WorkBadges[i].gameObject.SetActive(false);}
  }
  void Update(){
   if(!owner||owner.Campaign==null||home==null)return;
   var people=owner.Campaign.Party.ToArray();
   for(int i=0;i<Pawns.Length&&i<people.Length;i++){
    bool work=owner.IsAssigned(people[i]);
    if(work!=assigned[i]){
     assigned[i]=work;WorkBadges[i].gameObject.SetActive(false);
     target[i]=work?JobPosition(people[i],i)-footOffset[i]:home[i];
     // Keep to the open aisle, then approach the front of the workbench.
     var feet=target[i]+footOffset[i];via[i]=new Vector3(feet.x,AisleY,feet.z)-footOffset[i];
     start[i]=Pawns[i].position;progress[i]=0;leg[i]=0;moving[i]=true;
    }
    if(!moving[i]){Bodies[i].localRotation=rotations[i]*Quaternion.Euler(0,0,assigned[i]?Mathf.Sin(Time.time*3.1f+i)*.85f:0);continue;}
    // First leave the furniture row vertically, then traverse the aisle, then approach.
    var end=leg[i]==0?new Vector3(start[i].x,via[i].y,start[i].z):leg[i]==1?via[i]:target[i];float distance=Vector3.Distance(start[i],end);
    progress[i]+=Time.deltaTime*Speed/Mathf.Max(.01f,distance);
    float t=Mathf.Clamp01(progress[i]),ease=t*t*(3-2*t);
    Pawns[i].position=Vector3.Lerp(start[i],end,ease);
    float direction=Mathf.Abs(end.x-start[i].x)>.01f?Mathf.Sign(end.x-start[i].x):1;
    Bodies[i].localRotation=rotations[i]*Quaternion.Euler(0,0,-direction*LeanDegrees*Mathf.Sin(t*Mathf.PI));
    if(t>=1){Bodies[i].localRotation=rotations[i];if(leg[i]<2){leg[i]++;start[i]=Pawns[i].position;progress[i]=0;}else{moving[i]=false;WorkBadges[i].gameObject.SetActive(assigned[i]);}}
   }
  }
  Vector3 JobPosition(Demo5.NightRun.Adventurer person,int index){
   float offset=(index%2)*.7f;
   var craft=owner.CraftPanel.Orders.FirstOrDefault(o=>o.Member==person);
   string id=craft?.Recipe.Id??"";
   if(id.StartsWith("research-")||id=="build-research")return new Vector3(1.1f+offset,-2.4f,0);
   if(id=="build-stock"||id=="expand-stock")return new Vector3(-2.2f+offset,-2.9f,0);
   if(id=="build-bed"||id=="repair-bed"||owner.WorkPanel.Orders.Any(o=>o.Member==person))return new Vector3(-4.9f+offset,.22f,0);
   if(id=="build-cooker"||id=="upgrade-cooker"||owner.CookingPanel.IsAssigned(person))return new Vector3(5.1f+offset,.20f,0);
   return WorkPositions[index%WorkPositions.Length].position;
  }
 }
}
