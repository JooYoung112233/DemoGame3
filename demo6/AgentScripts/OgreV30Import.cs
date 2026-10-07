using System;using UnityEditor;using UnityEngine;
public static class OgreV30Import {
 public static object Run(){if(EditorApplication.isPlaying)throw new Exception("Import in Edit mode only");AssetDatabase.Refresh();
  foreach(var n in new[]{"slam","dust","sweep","roar","rock","lower_body_styled","torso_clean_styled","torso_underlap_styled"}){
   string path="Assets/Resources/"+(n.EndsWith("styled")?"OgreApproved/":"OgreVfxV30/")+n+".png";
   var t=AssetImporter.GetAtPath(path) as TextureImporter;if(!t)throw new Exception(path);
   t.textureType=TextureImporterType.Default;t.maxTextureSize=4096;t.textureCompression=TextureImporterCompression.Uncompressed;t.mipmapEnabled=false;t.alphaIsTransparency=true;t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;t.SaveAndReimport();
  }return new{imported=8};}
}
