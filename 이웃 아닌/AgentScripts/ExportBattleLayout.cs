using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Demo5.FrontEnd;
public static class ExportBattleLayout
{
 public static string Run()
 {
  var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Settlement/ExpeditionBattlePanel.prefab");
  try
  {
   var c=root.GetComponent<ExpeditionBattlePanel>();var frame=(RectTransform)root.transform;
   var groups=new Dictionary<string,object>();
   foreach(var pair in new[]{("battle",c.Workspace.gameObject),("result",c.Result),("retreat",c.RetreatReview)})
   {
    var records=new List<object>();
    foreach(var im in pair.Item2.GetComponentsInChildren<Image>(true))
    {
     var rect=im.rectTransform;var p=frame.InverseTransformPoint(rect.TransformPoint(new Vector3(rect.rect.xMin,rect.rect.yMax,0)));
     records.Add(new{name=pair.Item1+"-"+records.Count.ToString("00")+"-"+im.name,source=im.sprite?AssetDatabase.GetAssetPath(im.sprite):null,x=p.x,y=-p.y,w=rect.rect.width,h=rect.rect.height,color=new[]{im.color.r,im.color.g,im.color.b,im.color.a},aspect=im.preserveAspect});
    }
    groups[pair.Item1]=records;
   }
   string output="아트/분리진형전투-v1/layout.json";Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonConvert.SerializeObject(groups,Formatting.Indented));
   return output;
  }
  finally{PrefabUtility.UnloadPrefabContents(root);}
 }
}
