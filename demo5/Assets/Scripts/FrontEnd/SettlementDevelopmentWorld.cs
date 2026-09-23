using UnityEngine;
namespace Demo5.FrontEnd {
 public sealed class SettlementDevelopmentWorld:MonoBehaviour {
  public GameObject ResearchTable,StorageCrates,ResearchPlan;
  SettlementController owner;
  void Start(){owner=FindAnyObjectByType<SettlementController>();}
  void LateUpdate(){if(!owner||!owner.Development)return;var s=owner.Development.State;ResearchTable.SetActive(s.Research);StorageCrates.SetActive(s.Warehouse>0);ResearchPlan.SetActive(!s.Research&&owner.Introduction.Allows(5));}
 }
}
