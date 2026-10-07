using System.Collections.Generic;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Owns only runtime-created terrain meshes/materials for this town instance.
    /// Imported sprites/textures and the reusable crop cache remain owned by their existing loaders.</summary>
    public sealed class TownGeneratedResourcesV058 : MonoBehaviour
    {
        readonly List<Object> owned=new List<Object>();

        public T Track<T>(T resource) where T : Object
        {
            if(resource&&!owned.Contains(resource))owned.Add(resource);
            return resource;
        }

        void OnDestroy()
        {
            for(int i=owned.Count-1;i>=0;i--)
            {
                var resource=owned[i];
                if(!resource)continue;
                if(Application.isPlaying)Destroy(resource);
                else DestroyImmediate(resource);
            }
            owned.Clear();
        }
    }
}
