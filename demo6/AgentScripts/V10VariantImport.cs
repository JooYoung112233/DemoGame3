using System;using System.IO;using UnityEngine;using UnityEditor;
public static class V10VariantImport {
 public static object Run(){if(!Application.dataPath.Contains("topdown-v10-review-isolated")||EditorApplication.isPlaying)throw new Exception("Owned isolated Edit only");
 Directory.CreateDirectory("Assets/Resources/TopDownV10");for(int n=1;n<=2;n++){string file="Assets/Resources/TopDownV10/floor_variant_"+n+".png";File.Copy("E:/personalProject/Demo3/demo6/아트/정수리-질감수정-v10/runtime-PNG/floor_variant_"+n+".png",file,true);AssetDatabase.ImportAsset(file,ImportAssetOptions.ForceSynchronousImport);var i=(TextureImporter)AssetImporter.GetAtPath(file);i.textureType=TextureImporterType.Sprite;i.spriteImportMode=SpriteImportMode.Single;i.spritePixelsPerUnit=58.75f;i.mipmapEnabled=true;i.filterMode=FilterMode.Trilinear;i.textureCompression=TextureImporterCompression.Uncompressed;i.maxTextureSize=2048;i.SaveAndReimport();}return "Two native floor variants imported"; }
}
