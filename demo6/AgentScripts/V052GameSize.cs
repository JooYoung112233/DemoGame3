using System;using System.Reflection;using UnityEngine;using UnityEditor;
public static class V052GameSize {
 const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
 public static object Set720()=>Set(1280,720);
 public static object Set1080()=>Set(1920,1080);
 static object Set(int width,int height){
  var assembly=typeof(Editor).Assembly;var type=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);
  var index=type.GetProperty("selectedSizeIndex",All);if(!SessionState.GetBool("V052.size.saved",false)){SessionState.SetInt("V052.size.index",(int)index.GetValue(view));SessionState.SetBool("V052.size.saved",true);}
  var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",All).GetValue(null);
  var groupType=assembly.GetType("UnityEditor.GameViewSizeGroupType");var groupId=sizesType.GetProperty("currentGroupType",All).GetValue(sizes);var group=sizesType.GetMethod("GetGroup",All).Invoke(sizes,new[]{groupId});var groupClass=group.GetType();
  string label="V052 temporary "+width+"x"+height;int builtin=(int)groupClass.GetMethod("GetBuiltinCount",All).Invoke(group,null),custom=(int)groupClass.GetMethod("GetCustomCount",All).Invoke(group,null);int selected=-1;
  for(int i=0;i<builtin+custom;i++){var size=groupClass.GetMethod("GetGameViewSize",All).Invoke(group,new object[]{i});var st=size.GetType();if((string)st.GetProperty("baseText",All).GetValue(size)==label){selected=i;break;}}
  if(selected<0){var st=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");var size=Activator.CreateInstance(st,All,null,new object[]{Enum.Parse(kind,"FixedResolution"),width,height,label},null);groupClass.GetMethod("AddCustomSize",All).Invoke(group,new[]{size});selected=builtin+custom;}
  index.SetValue(view,selected);view.Repaint();return new{width,height,selected,method="Temporary real Game View fixed resolution; no input injection"};
 }
 public static object Restore(){var assembly=typeof(Editor).Assembly;var type=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);if(SessionState.GetBool("V052.size.saved",false)){type.GetProperty("selectedSizeIndex",All).SetValue(view,SessionState.GetInt("V052.size.index",0));SessionState.EraseBool("V052.size.saved");}
  var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance",All).GetValue(null);var group=sizesType.GetMethod("GetGroup",All).Invoke(sizes,new[]{sizesType.GetProperty("currentGroupType",All).GetValue(sizes)});var g=group.GetType();int builtin=(int)g.GetMethod("GetBuiltinCount",All).Invoke(group,null),custom=(int)g.GetMethod("GetCustomCount",All).Invoke(group,null);
  for(int i=custom-1;i>=0;i--){var size=g.GetMethod("GetGameViewSize",All).Invoke(group,new object[]{builtin+i});if(((string)size.GetType().GetProperty("baseText",All).GetValue(size)).StartsWith("V052 temporary "))g.GetMethod("RemoveCustomSize",All).Invoke(group,new object[]{i});}view.Repaint();return "Restored original Game View size and removed temporary sizes";
 }
}
