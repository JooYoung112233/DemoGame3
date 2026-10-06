using UnityEngine;
namespace Demo5.FrontEnd {
 public sealed class SettlementDevelopmentWorld:MonoBehaviour {
  public GameObject ResearchTable,StorageCrates,ResearchPlan,TemporaryStock,WarehouseUpgrade;
  public SpriteRenderer ResearchArtwork,StockArtwork;
  SettlementController owner;
  void Start(){owner=FindAnyObjectByType<SettlementController>();if(owner){var research=owner.Development.ResearchButton.GetComponent<SettlementFacilityFocus>();if(research)research.Artwork=ResearchArtwork;var stock=owner.Cabinet.GetComponent<SettlementFacilityFocus>();if(stock)stock.Artwork=StockArtwork;}}
  void LateUpdate(){if(!owner||!owner.Development)return;var s=owner.Development.State;ResearchTable.SetActive(s.Research);StorageCrates.SetActive(s.Warehouse>0);ResearchPlan.SetActive(!s.Research&&owner.Introduction.Allows(5));if(TemporaryStock)TemporaryStock.SetActive(s.Warehouse==0&&owner.Introduction.Allows(4));if(WarehouseUpgrade)WarehouseUpgrade.SetActive(s.Warehouse>=2);var stock=owner.Cabinet.GetComponent<SettlementFacilityFocus>();if(stock){stock.UseUnrestored=s.Warehouse==0;stock.FacilityName=s.Warehouse==0?"임시 보관 자리":"창고 Lv."+s.Warehouse;}}
 }
}


