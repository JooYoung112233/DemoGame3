using System;
using UnityEngine;

namespace Demo6.Game
{
    // Explicit asset references keep approved files at their original GUIDs and paths.
    public sealed class ApprovedWorldCatalogV043 : ScriptableObject
    {
        [Serializable] public sealed class Entry { public string id; public Sprite sprite; public Material material; }
        public Entry[] entries;
        public Entry Find(string id)
        {
            if (entries != null) foreach (var item in entries) if (item.id == id) return item;
            return null;
        }
    }
}
