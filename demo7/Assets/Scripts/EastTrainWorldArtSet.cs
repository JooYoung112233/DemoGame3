using UnityEngine;

namespace EastTrain
{
    [CreateAssetMenu(menuName = "East Train/World Art Set")]
    public sealed class EastTrainWorldArtSet : ScriptableObject
    {
        [System.Serializable]
        public struct Entry { public string Name; public EastTrainArtSet.Part Image; }
        public Entry[] Entries;
        public EastTrainArtSet.Part Find(string name)
        {
            if (Entries != null) foreach (var entry in Entries) if (entry.Name == name) return entry.Image;
            return default;
        }
    }
}
