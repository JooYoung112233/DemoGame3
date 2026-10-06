using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class ExportExpeditionReturnLayout
{
 public static string Run()
 {
  bool live=EditorApplication.isPlaying;
  var root=live?UnityEngine.Object.FindAnyObjectByType<SettlementController>().ReturnPanel.View:PrefabUtility.LoadPrefabContents("Assets/Prefabs/Settlement/ExpeditionReturnPanel.prefab");
  try
  {
   var c=root.GetComponent<ExpeditionReturnPanel>();var frame=(RectTransform)root.transform;
   var groups=new Dictionary<string,object>();
   foreach(var pair in new[]{("return",c.View)})
   {
    var records=new List<object>();
    foreach(var im in pair.Item2.GetComponentsInChildren<Image>(true))
    {
     var rect=im.rectTransform;var p=frame.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin,rect.rect.yMax,0)));
     records.Add(new{name=pair.Item1+"-"+records.Count.ToString("00")+"-"+im.name,source=im.sprite?AssetDatabase.GetAssetPath(im.sprite):null,x=p.x,y=-p.y,w=rect.rect.width*(im.type==Image.Type.Filled&&im.fillMethod==Image.FillMethod.Horizontal?im.fillAmount:1),h=rect.rect.height,color=new[]{im.color.r,im.color.g,im.color.b,im.color.a},aspect=im.preserveAspect});
    }
    groups[pair.Item1]=records;
   }
   string output="아트/귀환정산-v1/layout.json";Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonConvert.SerializeObject(groups,Formatting.Indented));
   return output;
  }
  finally{if(!live)PrefabUtility.UnloadPrefabContents(root);}
 }
}
