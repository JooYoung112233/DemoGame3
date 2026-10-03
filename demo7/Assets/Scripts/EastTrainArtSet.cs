using UnityEngine;

namespace EastTrain
{
    [CreateAssetMenu(menuName = "East Train/Art Set")]
    public sealed class EastTrainArtSet : ScriptableObject
    {
        [System.Serializable]
        public struct Part { public Texture2D Texture; public Rect Pixels; }
        public Part Furnace, Engine, Workbench;
    }
}
