using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
public static class ApplyFunflowFont {
 public const string FontPath="Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf";
 static int Apply(GameObject root,Font font){int count=0;foreach(var v in root.GetComponentsInChildren<Demo5.FrontEnd.TitleViewport>(true)){if(v.FontOverride!=font){v.FontOverride=font;EditorUtility.SetDirty(v);count++;}}foreach(var v in root.GetComponentsInChildren<Demo5.NightRun.NightRunView>(true)){if(v.FontOverride!=font){v.FontOverride=font;EditorUtility.SetDirty(v);count++;}}foreach(var t in root.GetComponentsInChildren<Text>(true)){var before=t.font;int size=t.fontSize;var style=t.fontStyle;t.font=font;t.fontStyle=FontStyle.Normal;string original=t.text;if(t.name=="Hint" && Mathf.Abs(t.rectTransform.rect.height-54)<1)t.text="선택 전에는 시간이 흐르지 않습니다.\n숨어 기다리기 · 1턴 / 이탈 확률 65%";if(t.name=="Body" && Mathf.Abs(t.rectTransform.rect.height-300)<1)t.text="체력  3 / 3\n상태  대기\n\n길을 읽는 눈\n낯선 장소에서도 작은 흔적을 놓치지 않습니다.\n수색할 곳과 돌아올 길을 먼저 살핍니다.";if(string.IsNullOrEmpty(t.text))t.text=t.name.IndexOf("Clock",StringComparison.OrdinalIgnoreCase)>=0?"DAY 1\n09:00":"가나다";while(t.fontSize>16&&t.preferredHeight>t.rectTransform.rect.height+0.5f)t.fontSize--;t.text=original;if(before!=font||size!=t.fontSize||style!=t.fontStyle){count++;EditorUtility.SetDirty(t);}}return count;}
 public static string Run(){if(EditorApplication.isPlaying)throw new Exception("Stop first");for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene");AssetDatabase.Refresh();var font=AssetDatabase.LoadAssetAtPath<Font>(FontPath);if(!font)throw new Exception("Font missing");int texts=0,prefabs=0;
 foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath)){var root=PrefabUtility.LoadPrefabContents(path);try{int n=Apply(root,font);if(n>0){PrefabUtility.SaveAsPrefabAsset(root,path);texts+=n;prefabs++;}}finally{PrefabUtility.UnloadPrefabContents(root);}}
 foreach(var path in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath)){var scene=EditorSceneManager.OpenScene(path);foreach(var root in scene.GetRootGameObjects())texts+=Apply(root,font);EditorSceneManager.SaveScene(scene);}
 AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/Settlement.unity");font.RequestCharactersInTexture("가나다수색생존자DAY0123456789←→×−·?%",32,FontStyle.Normal);string missing=new string("가나다수색생존자DAY0123456789←→×−·?%".Where(c=>!font.HasCharacter(c)).ToArray());return "Updated "+texts+" texts in "+prefabs+" prefabs and all game scenes. Missing sample characters: "+missing;
 }
}



