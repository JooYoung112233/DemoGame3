using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class SearchDropPreview:MonoBehaviour {
  public RectTransform Content;public SearchDropRow RowPrefab;public Text Summary,Note;
  public readonly List<SearchDropRow> Rows=new List<SearchDropRow>();
  public void Refresh(ExpeditionArrivalPanel arrival,int site,int pace,int duty){
   foreach(var row in Rows){row.gameObject.SetActive(false);Destroy(row.gameObject);}Rows.Clear();
   var state=arrival.Loot.State(site);int bonus=arrival.Loot.BonusFor(site,duty),remaining=(state.Required>0?state.Required:pace+1)-state.Progress;
   Summary.text="남은 "+remaining+"턴 · "+remaining*arrival.Rooms.MinutesPerTurn+"분\n턴당 소음 +"+arrival.Loot.NoiseFor(pace,duty)+" · 발견 보정 "+(pace==0?"−15":pace==2?"+15":"0")+"%p"+(bonus>0?(duty==2?" / 조명 +":" / 협력 +")+bonus+"%p":"");
   foreach(var drop in arrival.Loot.Sites[site].Drops){var item=arrival.Inventory.Items.FirstOrDefault(i=>i.Id==drop.Id);var row=Instantiate(RowPrefab,Content);row.Icon.sprite=item?.Icon;row.Name.text=item?.Name??drop.Id;row.Quantity.text="×"+drop.Count;row.Chance.text=ExpeditionLootPanel.ChanceFor(drop,pace,bonus)+"%";Rows.Add(row);}
   Note.text=state.Progress>0?"진행 중 · 수색 방식과 협력 보정 유지":"각 물건을 개별 판정 · 완료 시 결과 확인";
   Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
  }
 }
}
