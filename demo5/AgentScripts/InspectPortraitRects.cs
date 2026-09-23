using UnityEngine;
using UnityEditor;
using UnityEngine.Sprites;
public static class InspectPortraitRects {public static string Run(){var s=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PartySelection/portrait-scout.png");return "rect "+s.rect+" texrect "+s.textureRect+" UV "+DataUtility.GetOuterUV(s)+" texture "+s.texture.width+"x"+s.texture.height;}}
