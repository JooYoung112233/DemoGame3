using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Demo5.FrontEnd;
public static class PolishDevelopment
{
 public static string Run(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Stop Play/preserve scene");
  const string path="Assets/Prefabs/Settlement/CraftWorkPanel.prefab";var root=PrefabUtility.LoadPrefabContents(path);
  try{var c=root.GetComponent<SettlementCraftPanel>();c.DetailDescription.fontSize=25;c.ConfirmLabel.resizeTextForBestFit=true;c.ConfirmLabel.resizeTextMinSize=20;c.ConfirmLabel.resizeTextMaxSize=30;
   string[][] descriptions={new[]{"build-stock","보관 12 → 40개\n회수품 선반 복구"},new[]{"build-bench","기본 제작 해금\n목재·고철로 복구"},new[]{"build-bed","짧은 휴식 60 → 30분\n수면 해금"},new[]{"build-cooker","식량 조리 해금\n작업대 복구 필요"},new[]{"build-research","세 가지 연구 해금\n작업대 복구 필요"},new[]{"research-tools","작업대 Lv.2 해금\n옆방 확장 해금"},new[]{"research-comfort","침대·조리대 Lv.2\n개선 제작법 해금"},new[]{"research-storage","창고 확장 해금\n보관 방식 시험"},new[]{"expand-stock","보관 40 → 80개\n보관 연구 필요"}};
   foreach(var a in descriptions)c.Recipes.First(r=>r.Id==a[0]).Description=a[1]; c.Recipes.First(r=>r.Id=="flashlight").Description="동료의 수색을 지원\n가방에 넣어 사용";
   foreach(var t in root.GetComponentsInChildren<Text>(true)){if(t.text=="작업대")t.text="생활 작업";if(t.text=="정착지 · 제작대")t.text="정착지 · 생활 작업";}
   PrefabUtility.SaveAsPrefabAsset(root,path);
  }finally{PrefabUtility.UnloadPrefabContents(root);}
  var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/SettlementProp.mat");mat.SetColor("_Color",new Color(.30f,.33f,.36f,1));EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");return "Short two-line facility descriptions, adaptive action text, correct heading, darker prop tint saved";
 }
}
