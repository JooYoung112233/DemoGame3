using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class StandardizePopupFrames {
 const string P="Assets/Prefabs/Settlement/";
 static void R(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static GameObject heading;
 static void Footer(Button b,Transform parent,float x,float width){b.transform.SetParent(parent,false);R(b,x,974,width,76);var label=b.transform.Find("Label").GetComponent<Text>();R(label,12,0,width-24,76);label.alignment=TextAnchor.MiddleCenter;label.fontSize=34;}
 static void Dim(GameObject g){var t=g.transform.Find("InputBlocker");if(!t)t=g.transform.Find("Dim");t.GetComponent<Image>().color=new Color(0,0,0,.97f);}
 static void Edit(string name,string title,Action<GameObject> extra){string path=P+name+".prefab";var g=PrefabUtility.LoadPrefabContents(path);try{var old=g.transform.Find("PopupHeading");if(old)Object.DestroyImmediate(old.gameObject);var h=(GameObject)PrefabUtility.InstantiatePrefab(heading,g.transform);h.name="PopupHeading";h.transform.SetSiblingIndex(1);h.GetComponentInChildren<Text>().text=title;PrefabUtility.RecordPrefabInstancePropertyModifications(h.GetComponentInChildren<Text>());if(!g.GetComponent<PopupBackgroundHud>())g.AddComponent<PopupBackgroundHud>();extra(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
 public static string Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop first");
  var h=new GameObject("PopupHeading",typeof(RectTransform),typeof(Image));R(h.transform,80,32,540,72);h.GetComponent<Image>().sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/footer-paper.png");h.GetComponent<Image>().raycastTarget=false;
  var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(h.transform,false);R(label.transform,24,0,492,72);var text=label.GetComponent<Text>();text.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf");text.fontSize=34;text.color=new Color(.045f,.065f,.06f);text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;heading=PrefabUtility.SaveAsPrefabAsset(h,P+"PopupHeading.prefab");Object.DestroyImmediate(h);
  Edit("ExpeditionSearchPanel","탐험 · 수색",g=>{});
  Edit("ExpeditionBagPanel","탐험 · 개인 가방",g=>{var c=g.GetComponent<ExpeditionBagPanel>();R(c.Back,80,974,410,76);R(c.Transfer,1442,974,410,76);g.transform.Find("Dim").GetComponent<Image>().color=new Color(0,0,0,.97f);});
  // Loot uses the return report's 3-button footer: 돌아가기 | 선택 가져오기 | 모두 담기 (TakeAll is created by BuildDeferredUI).
  Edit("ExpeditionLootPanel","탐험 · 발견물 정리",g=>{var c=g.GetComponent<ExpeditionLootPanel>();R(c.Back,80,974,320,76);R(c.Transfer,1178,974,320,76);if(c.TakeAll)R(c.TakeAll,1532,974,320,76);foreach(var b in new[]{c.Back,c.Transfer,c.TakeAll}){if(!b)continue;var txt=b.GetComponentInChildren<Text>(true);R(txt,8,0,304,76);txt.alignment=TextAnchor.MiddleCenter;}g.transform.Find("Dim").GetComponent<Image>().color=new Color(0,0,0,.97f);});
  Edit("SettlementTimePanel","정착지 · 시간 진행",g=>{var c=g.GetComponent<SettlementTimePanel>();R(c.CloseButton,80,974,320,76);R(c.Confirm,1532,974,320,76);g.transform.Find("Dim").GetComponent<Image>().color=new Color(0,0,0,.97f);});
  Edit("ExpeditionReturnPanel","원정 · 귀환 보고",g=>{var c=g.GetComponent<ExpeditionReturnPanel>();R(c.Back,80,974,320,76);R(c.InspectBags,1178,974,320,76);R(c.StoreAll,1532,974,320,76);g.transform.Find("Dim").GetComponent<Image>().color=new Color(0,0,0,.97f);});
  Edit("InventoryPanel","정착지 · 공용 창고",g=>{var c=g.GetComponent<SettlementInventoryPanel>();Dim(g);Footer(c.CloseButton,c.Workspace.transform,80,410);var w=c.Workspace.transform;foreach(var n in new[]{"ClockPaper","LocationPaper","HeaderClock","HeaderLocation"}){var t=w.Find(n);if(t)t.gameObject.SetActive(false);}for(int i=0;i<c.MemberCards.Length;i++)R(c.MemberCards[i],660+i*184,65,175,158);R(c.PrevMember,624,108,32,64);R(c.NextMember,1796,108,32,64);foreach(var b in new[]{c.PrevMember,c.NextMember})R(b.transform.Find("Label"),0,0,32,64);var notice=w.Find("NoticePaper");if(notice)R(notice,776,974,1076,76);R(c.Notice,800,980,1028,64);});
  Edit("CraftWorkPanel","정착지 · 제작대",g=>{var c=g.GetComponent<SettlementCraftPanel>();Dim(g);Footer(c.CloseButton,c.Workspace.transform,80,410);Footer(c.Confirm,c.Workspace.transform,1442,410);c.CloseButton.GetComponent<Image>().color=Color.white;var label=c.CloseButton.transform.Find("Label").GetComponent<Text>();label.text="돌아가기";label.color=new Color(.045f,.065f,.06f);R(g.transform.Find("Workspace/Workshop"),80,218,1090,690);R(g.transform.Find("Workspace/OrdersPaper"),1190,470,660,440);});
  Edit("RestWorkPanel","정착지 · 휴식",g=>{var c=g.GetComponent<SettlementWorkPanel>();Dim(g);Footer(c.CloseButton,g.transform,80,410);c.CloseButton.GetComponent<Image>().color=Color.white;c.CloseButton.transform.Find("Label").GetComponent<Text>().text="돌아가기";c.CloseButton.transform.Find("Label").GetComponent<Text>().color=new Color(.045f,.065f,.06f);Footer(c.Confirm,g.transform,1442,410);R(g.transform.Find("TaskPaper"),80,130,660,818);R(g.transform.Find("SelectedMember"),1432,530,420,390);});
  AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Shared popup frames applied to eight noncombat screens.";
 }
}
