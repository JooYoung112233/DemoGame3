using System;
using System.Collections.Generic;
using Demo6.Core.Town;
using UnityEngine;
using UnityEngine.Rendering;

namespace Demo6.Game
{
    /// <summary>Independent, editable modules traced against the approved 1536 x 1024 village.
    /// Reference pixels are placement coordinates only; the reference bitmap is never loaded by the game.</summary>
    public static class TownModularArtV058
    {
        [Serializable] public sealed class Layout { public PartGroup[] groups; public Contact[] contacts; public Surface[] surfaces; }
        [Serializable] public sealed class Surface { public string id,sprite;public int order;public float textureSpan,brightness;public Vector2[] points; }
        [Serializable] public sealed class PartGroup
        {
            public string id, assembly, role; public float anchorX, anchorY; public int order;
            public Part[] parts;
        }
        [Serializable] public sealed class Part
        {
            public string sprite, color; public float x, y, width, height, rotation;
            public int order; public bool unlit; public float[] crop;
        }
        [Serializable] public sealed class Contact
        {
            public string id; public float x, y, width, height, rotation;
        }
        static readonly Dictionary<string, Sprite> Crops = new Dictionary<string, Sprite>();
        public static Vector2 FromPixel(float x, float y) { var p=TownLayout.Pixel(x,y);return new Vector2(p.X,p.Y); }
        public static int Depth(float pixelY) => WorldProps.SortY(FromPixel(0,pixelY).y,50);

        public static void BuildAll(Transform parent, List<SpriteRenderer> cores, List<SpriteRenderer> glows)
        {
            var source=Resources.Load<TextAsset>("TownArtV058/assembly");
            if(!source) throw new InvalidOperationException("TownArtV058/assembly is missing; refusing to display the rejected town layout.");
            var layout=JsonUtility.FromJson<Layout>(source.text);
            var assemblies=new Dictionary<string, Transform>();
            var resources=parent.gameObject.GetComponent<TownGeneratedResourcesV058>();
            if(!resources)resources=parent.gameObject.AddComponent<TownGeneratedResourcesV058>();
            var terrainMaterials=new Dictionary<Texture, Material>();
            var terrain=new GameObject("00 Terrain - independent textured mesh chunks").transform;terrain.SetParent(parent,false);
            foreach(var surface in layout.surfaces)Ground(terrain,surface,resources,terrainMaterials);
            foreach(var group in layout.groups)
            {
                if(!assemblies.TryGetValue(group.assembly,out var assembly))
                {
                    assembly=new GameObject(group.assembly).transform; assembly.SetParent(parent,false);
                    assemblies.Add(group.assembly,assembly);
                }
                var go=new GameObject(group.id);go.transform.SetParent(assembly,false);
                var sorting=go.AddComponent<SortingGroup>();
                sorting.sortingOrder=group.role=="ground"?group.order:Depth(group.anchorY);
                foreach(var part in group.parts) Place(go.transform,part);
            }
            var contacts=new GameObject("Ground contacts - roofs and canopies are not solid").transform;
            contacts.SetParent(parent,false);
            foreach(var box in TownLayout.Solids())
                WorldProps.SolidBox(contacts,box.Name,new Vector2(box.Center.X,box.Center.Y),new Vector2(box.Width,box.Height));
            foreach(var c in layout.contacts)
            {
                var col=WorldProps.SolidBox(contacts,c.id,FromPixel(c.x,c.y),new Vector2(c.width,c.height)/TownLayout.WorldPixelsPerUnit);
                col.transform.localRotation=Quaternion.Euler(0,0,-c.rotation);
            }
            // Keep the existing progress-driven 12-lamp contract, now mounted on the actual winch beam.
            var lamps=new GameObject("Winch progress lamps (12)").transform;lamps.SetParent(assemblies["08 Mine"],false);
            lamps.gameObject.AddComponent<SortingGroup>().sortingOrder=Depth(182)+1;
            for(int i=0;i<TownLayout.LampCount;i++)
            {
                var p=TownLayout.LampPos(i);var at=new Vector2(p.X,p.Y);
                glows.Add(WorldProps.Shape(lamps,"Lamp glow "+i,at,new Vector2(.21f,.21f)*TownLayout.MapScale,WorldProps.SoftDot,new Color(1f,.82f,.6f,.22f),0,true));
                cores.Add(WorldProps.Shape(lamps,"Lamp "+i,at,new Vector2(.075f,.075f)*TownLayout.MapScale,ShapeSprites.Circle,Palette.TorchLight,1,true));
            }
            TownNpcIdleV057.Attach(parent,"elder",FromPixel(374,406));
            TownNpcIdleV057.Attach(parent,"merchant",FromPixel(1170,770));
            TownEnvironmentV059.Build(parent,layout);
            if(Application.isPlaying)parent.gameObject.AddComponent<TownActorSortingV058>();
        }

        static void Ground(Transform parent,Surface s,TownGeneratedResourcesV058 resources,Dictionary<Texture,Material> materials)
        {
            var texture=Resources.Load<Sprite>("TownArtV058/modules/"+s.sprite).texture;
            var vertices=new Vector3[s.points.Length];var uv=new Vector2[vertices.Length];var colors=new Color[vertices.Length];
            for(int i=0;i<vertices.Length;i++){vertices[i]=FromPixel(s.points[i].x,s.points[i].y);uv[i]=new Vector2(s.points[i].x,-s.points[i].y)/s.textureSpan;colors[i]=new Color(s.brightness,s.brightness,s.brightness,1);}
            // Ear clipping keeps the irregular shore and each path bend editable without a baked map bitmap.
            var remaining=new List<int>();float area=0;
            for(int i=0;i<vertices.Length;i++){remaining.Add(i);var a=vertices[i];var b=vertices[(i+1)%vertices.Length];area+=a.x*b.y-b.x*a.y;}
            if(area<0)remaining.Reverse();var triangles=new List<int>();int guard=vertices.Length*vertices.Length;
            float Cross(Vector3 a,Vector3 b,Vector3 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
            while(remaining.Count>2&&guard-->0)
            {
                bool clipped=false;
                for(int j=0;j<remaining.Count;j++)
                {
                    int a=remaining[(j+remaining.Count-1)%remaining.Count],b=remaining[j],c=remaining[(j+1)%remaining.Count];
                    if(Cross(vertices[a],vertices[b],vertices[c])<=.000001f)continue;
                    bool inside=false;
                    foreach(int k in remaining)if(k!=a&&k!=b&&k!=c&&Cross(vertices[a],vertices[b],vertices[k])>=0&&Cross(vertices[b],vertices[c],vertices[k])>=0&&Cross(vertices[c],vertices[a],vertices[k])>=0){inside=true;break;}
                    if(inside)continue;triangles.Add(a);triangles.Add(b);triangles.Add(c);remaining.RemoveAt(j);clipped=true;break;
                }
                if(!clipped)break;
            }
            if(triangles.Count!=(vertices.Length-2)*3)throw new InvalidOperationException("Invalid ground polygon "+s.id);
            var mesh=resources.Track(new Mesh{name=s.id});mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.triangles=triangles.ToArray();mesh.RecalculateBounds();
            var go=new GameObject(s.id);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sortingOrder=s.order;
            if(!materials.TryGetValue(texture,out var material))
            {
                var shader=Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");if(!shader)shader=Shader.Find("Sprites/Default");
                material=resources.Track(new Material(shader){name="Town terrain "+texture.name,mainTexture=texture});
                materials.Add(texture,material);
            }
            renderer.sharedMaterial=material;
        }

        static SpriteRenderer Place(Transform parent,Part p)
        {
            if(p.sprite=="__square"||p.sprite=="__circle")
            {
                var flat=new GameObject(p.sprite);flat.transform.SetParent(parent,false);
                flat.transform.localPosition=FromPixel(p.x+p.width*.5f,p.y+p.height*.5f);
                flat.transform.localScale=new Vector3(p.width/TownLayout.WorldPixelsPerUnit,p.height/TownLayout.WorldPixelsPerUnit,1);
                var surface=flat.AddComponent<SpriteRenderer>();surface.sprite=p.sprite=="__square"?ShapeSprites.Square:ShapeSprites.Circle;
                surface.sortingOrder=p.order;
                if(ColorUtility.TryParseHtmlString(p.color,out var tint))surface.color=tint;
                if(p.unlit)RenderMaterials.MakeUnlit(surface);
                return surface;
            }
            Sprite original=Resources.Load<Sprite>("TownArtV058/modules/"+p.sprite);
            if(!original) original=Resources.Load<Sprite>("TownArtV057/modules/"+p.sprite);
            if(!original) throw new InvalidOperationException("Missing village module: "+p.sprite);
            Sprite sprite=original;
            if(p.crop!=null && p.crop.Length==4)
            {
                string key=p.sprite+":"+string.Join(",",p.crop);
                if(!Crops.TryGetValue(key,out sprite)||!sprite)
                {
                    var r=new Rect(p.crop[0],original.texture.height-p.crop[1]-p.crop[3],p.crop[2],p.crop[3]);
                    sprite=Sprite.Create(original.texture,r,new Vector2(.5f,.5f),40f,0,SpriteMeshType.FullRect);
                    sprite.name=key;Crops[key]=sprite;
                }
            }
            float scale=p.width/(sprite.rect.width);
            float height=sprite.rect.height*scale;
            var go=new GameObject(p.sprite);go.transform.SetParent(parent,false);
            go.transform.localPosition=FromPixel(p.x+p.width*.5f,p.y+height*.5f);
            go.transform.localRotation=Quaternion.Euler(0,0,-p.rotation);
            go.transform.localScale=Vector3.one*(scale*sprite.pixelsPerUnit/TownLayout.WorldPixelsPerUnit);
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=p.order;
            if(!string.IsNullOrEmpty(p.color)&&ColorUtility.TryParseHtmlString(p.color,out var color))sr.color=color;
            if(p.unlit)RenderMaterials.MakeUnlit(sr);
            return sr;
        }
    }
}

