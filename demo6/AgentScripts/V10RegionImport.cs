using System;using System.IO;using UnityEngine;using UnityEditor;
public static class V10RegionImport {
 public static object Run(){if(!Application.dataPath.Contains("topdown-v10-review-isolated")||EditorApplication.isPlaying)throw new Exception("Owned isolated Edit only");
 Directory.CreateDirectory("Assets/Resources/TopDownV10");const string file="Assets/Resources/TopDownV10/rock_field.png";
 File.Copy("E:/personalProject/Demo3/demo6/아트/정수리-질감수정-v10/generated/rock-field.png",file,true);AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceSynchronousImport);
 var i=(TextureImporter)AssetImporter.GetAtPath(file);i.textureType=TextureImporterType.Default;i.isReadable=true;i.mipmapEnabled=true;i.filterMode=FilterMode.Trilinear;i.textureCompression=TextureImporterCompression.Uncompressed;i.maxTextureSize=2048;i.SaveAndReimport();return "Rock field imported; readable native source retained"; }
}
