using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
using Object=UnityEngine.Object;
public static class BuildSettlementDevelopment
{
 const string P="Assets/Prefabs/Settlement/";
 static void Edit(string name,Action<GameObject> edit){string path=P+name+".prefab";var g=PrefabUtility.LoadPrefabContents(path);try{edit(g);PrefabUtility.SaveAsPrefabAsset(g,path);}finally{PrefabUtility.UnloadPrefabContents(g);}}
 static void Place(Component c,float x,float y,float w,float h){var r=(RectTransform)c.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
 static void Profiles(){
  var roster=AssetDatabase.LoadAssetAtPath<PartyRoster>("Assets/Data/PartyRoster.asset");
  string[][] profiles={
   new[]{"scout","윤서진","전직 택배기사","길을 기억하는\n성급한 길잡이","길을 읽는 사람","가방 3칸 · 체력 3\n지름길과 출입구를 먼저 찾는다.","습관 · 출구부터 확인한다.\n약점 · 기다리는 데 서툴다.","돌아올 길부터 보자.","금방 끝내고 길을 보자.","깨우는 건 잊지 마."},
   new[]{"mechanic","강기섭","수리기사","못 하나도 챙기는\n무뚝뚝한 수리꾼","손에 밴 수리","제작 시간 −20%\n가방 4칸 · 체력 3","습관 · 나사를 주머니에 모은다.\n약점 · 고장 난 걸 못 버린다.","고칠 수 있으면 고쳐야지.","이 나사도 쓸 데가 있어.","공구는 거기 놔둬."},
   new[]{"medic","한해인","방문 간병인","남부터 살피는\n조용한 간병인","침착한 돌봄","가방 3칸 · 체력 4\n말보다 안색을 먼저 살핀다.","습관 · 동료의 손을 살핀다.\n약점 · 자기 피로를 숨긴다.","다친 데부터 보여줘요.","서두르다 다치지 마요.","조금만 눈을 붙일게요."},
   new[]{"cook","오정은","급식 조리원","한 끼를 챙기는\n든든한 조리원","익숙한 손놀림","따뜻한 식사·스튜 조리 −20%\n가방 4칸 · 체력 4","습관 · 인원부터 세어본다.\n약점 · 남기는 음식을 못 본다.","일단 뭘 좀 먹고 얘기해.","빈속으로 일하지 마.","불 껐는지만 봐줘."},
   new[]{"researcher","백소담","기록 관리사","이상한 흔적을\n적어두는 기록가","지워지지 않는 기록","가방 3칸 · 체력 3\n어제와 달라진 흔적을 적는다.","습관 · 날짜와 위치를 적는다.\n약점 · 기록에 몰두하면 둔해진다.","어제도 이랬는지 적어둘게.","순서를 적어두면 돼.","이 줄만 적고 쉴게."},
   new[]{"guard","임태오","야간 경비원","문을 다시 보는\n잠 못 드는 경비","마지막 확인","가방 4칸 · 체력 4\n사람이 모두 돌아왔는지 센다.","습관 · 잠금장치를 두 번 본다.\n약점 · 작은 소리에도 잠이 깬다.","다 들어왔지? 문 닫는다.","끝나면 내가 확인할게.","소리 나면 바로 깨워."}
  };
  foreach(var a in profiles){var c=roster.Candidates.First(x=>x.Id==a[0]);c.DisplayName=a[1];c.RoleTitle=a[2];c.Description=a[3];c.TraitTitle=a[4];c.TraitDescription=a[5];c.Characteristics=a[6];c.FirstLine=a[7];c.WorkLine=a[8];c.RestLine=a[9];}
  EditorUtility.SetDirty(roster);
 }
 static void Recipes(SettlementCraftPanel c){
  var rows=c.Recipes.Where(r=>!r.Id.StartsWith("build-")&&!r.Id.StartsWith("research-")&&r.Id!="expand-stock").ToList();
  Action<string,string,string,int,int,string[],int[]> add=(id,name,desc,cat,min,ids,counts)=>rows.Add(new SettlementCraftPanel.Recipe{Id=id,Name=name,Description=desc,Category=cat,Minutes=min,Icon=c.Recipes.First(r=>r.Id=="upgrade-bench").Icon,Costs=ids.Select((x,i)=>new SettlementCraftPanel.Cost{MaterialId=x,Count=counts[i]}).ToArray()});
  add("build-stock","창고 복구","임시 보관 12 → 창고 40개\n회수한 물자를 정리할 선반을 만듭니다.",1,40,new[]{"wood","scrap"},new[]{2,1});
  add("build-bench","작업대 복구","기본 제작 해금\n회수한 목재와 고철로 작업면을 고칩니다.",1,60,new[]{"wood","scrap"},new[]{3,2});
  add("build-bed","잠자리 복구","짧은 휴식 60 → 30분 · 수면 해금\n망가진 침상과 침구를 손봅니다.",1,50,new[]{"wood","cloth"},new[]{2,2});
  add("build-cooker","조리대 복구","식사·정수 준비 해금\n먼저 작업대를 복구해야 합니다.",1,60,new[]{"wood","scrap"},new[]{2,2});
  add("build-research","연구대 설치","공구·생활·보관 연구 해금\n회수품을 분해하고 시험하는 자리입니다.",1,70,new[]{"wood","scrap","cloth"},new[]{3,2,1});
  add("research-tools","공구 연구","작업대 Lv.2 · 옆방 확장 해금\n회수한 고철로 결합 구조를 시험합니다.",2,90,new[]{"scrap","wood"},new[]{3,1});
  add("research-comfort","생활 연구","침대·조리대 Lv.2 해금\n침구와 열 보존 방식을 시험합니다.",2,90,new[]{"cloth","scrap"},new[]{2,2});
  add("research-storage","보관 연구","창고 Lv.2 해금\n적재 방식과 묶는 방법을 시험합니다.",2,75,new[]{"rope","wood"},new[]{1,2});
  add("expand-stock","창고 확장","창고 40 → 80개\n창고 복구와 보관 연구가 필요합니다.",1,90,new[]{"wood","rope"},new[]{4,2});
  c.Recipes=rows.OrderBy(r=>r.Category).ThenBy(r=>r.Id.StartsWith("build-")?0:r.Id.StartsWith("research-")?1:2).ToArray();
  c.Tabs[0].GetComponentInChildren<Text>().text="제작";c.Tabs[1].GetComponentInChildren<Text>().text="복구";c.Tabs[2].GetComponentInChildren<Text>().text="연구";
 }
 static void Pawns(GameObject root,Material shadow){
  foreach(var body in root.GetComponentsInChildren<SpriteRenderer>(true).Where(s=>s.name=="Body").ToArray()){
   var parent=body.transform.parent;var foot=parent.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(s=>s.name=="Base");if(!foot)continue;
   var face=parent.GetComponent<PawnFacing>();if(!face)face=parent.gameObject.AddComponent<PawnFacing>();face.Body=body;face.Foot=foot.transform;
   var group=parent.GetComponent<SortingGroup>();if(!group)group=parent.gameObject.AddComponent<SortingGroup>();group.sortingOrder=1000-Mathf.RoundToInt((foot.transform.position.y+5.4f)*100);
   var cast=parent.GetComponent<PawnGroundShadow>();if(!cast)cast=parent.gameObject.AddComponent<PawnGroundShadow>();cast.Body=body;cast.Base=foot;cast.ShadowMaterial=shadow;
  }
 }
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop Play and preserve scene first");
  Profiles();Edit("CraftWorkPanel",g=>Recipes(g.GetComponent<SettlementCraftPanel>()));
  Edit("SettlementScreen",g=>{
   var c=g.GetComponent<SettlementController>();var d=g.GetComponent<SettlementDevelopment>();if(!d)d=g.AddComponent<SettlementDevelopment>();c.Development=d;
   var old=c.Main.transform.Find("DevelopmentOpen");if(old)Object.DestroyImmediate(old.gameObject);
   d.OpenButton=Object.Instantiate(c.Introduction.Action,c.Main.transform);d.OpenButton.name="DevelopmentOpen";d.OpenButton.onClick=new Button.ButtonClickedEvent();d.OpenLabel=d.OpenButton.GetComponentInChildren<Text>();d.OpenLabel.text="시설 복구 · 연구";Place(d.OpenButton,30,790,554,76);
   old=c.Main.transform.Find("ResearchHotspot");if(old)Object.DestroyImmediate(old.gameObject);
   d.ResearchButton=Object.Instantiate(c.Workbench,c.Main.transform);d.ResearchButton.name="ResearchHotspot";d.ResearchButton.onClick=new Button.ButtonClickedEvent();Place(d.ResearchButton,1075,590,82,82);
   foreach(var t in d.ResearchButton.GetComponentsInChildren<Text>(true))t.text="연구대";
   foreach(var i in d.ResearchButton.GetComponentsInChildren<Image>(true))if(i.name=="Icon")i.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Settlement/icon-journal.png");
   var body=c.PopupBody;body.fontSize=28;
   var sites=c.ArrivalPanel.Loot.Sites;
   sites[0].RequiredTool="";sites[0].Drops=new[]{Drop("wood",10),Drop("scrap",8),Drop("cloth",4)};
   sites[1].RequiredTool="";sites[1].Drops=new[]{Drop("food",4),Drop("water",4),Drop("scrap",5)};
   sites[5].Drops=new[]{Drop("wood",14),Drop("cloth",8),Drop("scrap",10),Drop("raw-water",4)};
  });
  var shader=Shader.Find("Demo5/GroundShadow");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Shadow shader error");
  var shadow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/GroundShadow.mat");if(!shadow){shadow=new Material(shader);AssetDatabase.CreateAsset(shadow,"Assets/Settings/GroundShadow.mat");}
  Edit("SettlementWorld",g=>Pawns(g,shadow));Edit("FieldPawn",g=>Pawns(g,shadow));
  AssetDatabase.SaveAssets();var scene=EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");EditorSceneManager.SaveScene(scene);
  return "Saved six named profiles; 5 restoration + 3 research + warehouse expansion projects; early guaranteed salvage; pawn facing and floor shadows.";
 }
 static ExpeditionLootPanel.Drop Drop(string id,int n)=>new ExpeditionLootPanel.Drop{Id=id,Count=n,Chance=100};
}
