using System;using System.IO;using UnityEngine;using UnityEditor;
public static class UiSkinImport {
 public static object Run(){
  if(EditorApplication.isPlaying)throw new Exception("Stop Play before import");
  AssetDatabase.Refresh();
  int count=0;
  foreach(string folder in new[]{"Assets/Resources/UI/Items","Assets/Resources/UI/Skin"})foreach(var file in Directory.GetFiles(folder,"*.png")){
   string p=file.Replace('\\','/');var ti=(TextureImporter)AssetImporter.GetAtPath(p);
   ti.textureType=TextureImporterType.Default;ti.alphaIsTransparency=true;ti.mipmapEnabled=false;
   ti.filterMode=FilterMode.Bilinear;ti.wrapMode=TextureWrapMode.Clamp;ti.npotScale=TextureImporterNPOTScale.None;
   ti.textureCompression=TextureImporterCompression.Uncompressed;ti.maxTextureSize=2048;
   ti.isReadable=p.EndsWith("/button.png");ti.SaveAndReimport();count++;
  }
  return "Imported "+count+" UI PNGs (uncompressed, bilinear, no mipmaps)";
 }
}
