using System.Collections.Generic;
using Demo6.Core.Town;
using UnityEngine;

namespace Demo6.Game
{
    /// <summary>Approved modular art, assembled in the existing town. Open cutaway rooms; no new quest/interior system.</summary>
    public static class TownApprovedArtV057
    {
        const float Ppu = 128f;
        // Match the standing player part band (+41..56) without changing player/combat sorting.
        internal static int SurfaceOrder(float y, int offset = 0) => WorldProps.SortY(y, 48 + offset);
        static readonly Dictionary<string, Sprite> Crops = new Dictionary<string, Sprite>();
        public static bool Available => Module("wall-straight") && Module("floor-wood") && Module("doorway-wide-open");
        static Sprite Module(string id) => Resources.Load<Sprite>("TownArtV057/modules/" + id);
        public static bool IsRoom(TownBox b) => b.Name == TownLayout.Smithy.Name || b.Name == TownLayout.Tavern.Name || b.Name == TownLayout.House.Name;

        // Assembly-board wall centre lines: smith 9P x 7P, shop 12P x 9P.
        // Fit inside the previous footprint, preserving the courtyard-facing entry centre.
        static Rect RoomArea(TownBox b)
        {
            float left=b.XMin+.18f,right=b.XMax-.18f,bottom=b.YMin+.18f,top=b.YMax-.18f;
            if(b.Name==TownLayout.Smithy.Name) bottom=top-(right-left)*7f/9f;
            if(b.Name==TownLayout.Tavern.Name)
            {
                float width=(top-bottom)*12f/9f;
                left=b.Center.X-width*.5f;right=b.Center.X+width*.5f;
            }
            return Rect.MinMaxRect(left,bottom,right,top);
        }
        static float ModuleScale(TownBox b,Rect area) => area.width/(b.Name==TownLayout.Smithy.Name?9f:12f)*Ppu/64f;
        static Vector2 At(Rect r,float x,float y) => new Vector2(Mathf.Lerp(r.xMin,r.xMax,x),Mathf.Lerp(r.yMin,r.yMax,y));

        public static bool TryBuild(Transform parent, TownBox b)
        {
            if (!Available || !IsRoom(b)) return false;
            var root = new GameObject("Approved modules " + b.Name).transform;
            root.SetParent(parent, false);
            var area=RoomArea(b);
            float left=area.xMin,right=area.xMax,bottom=area.yMin,top=area.yMax;
            float scale=ModuleScale(b,area),unit=scale*64f/Ppu;
            float cx = b.Center.X;
            bool northEntry = b.Name != TownLayout.Smithy.Name;
            float entryY = northEntry ? top : bottom;
            float backY = northEntry ? bottom : top;
            Tile(root, "floor-wood", area, new Rect(93,94,325,324), -985,scale);
            Wall(root, new Vector2(left, backY), new Vector2(right, backY),scale);
            Wall(root, new Vector2(left, bottom), new Vector2(left, top),scale);
            Wall(root, new Vector2(right, bottom), new Vector2(right, top),scale);
            // Join the outside of the two painted jambs, not the clear passage.
            float west=cx-(northEntry?196f:203f)*scale/Ppu;
            float east=cx+(northEntry?203f:196f)*scale/Ppu;
            Wall(root, new Vector2(left, entryY), new Vector2(west, entryY),scale);
            Wall(root, new Vector2(east, entryY), new Vector2(right, entryY),scale);
            Place(root, "doorway-wide-open", new Vector2(cx,entryY), new Vector2(259,207), scale,northEntry?180:0,SurfaceOrder(entryY,2));
            foreach (float x in new[]{left,right}) foreach (float y in new[]{bottom,top})
                Place(root,"wall-joint-cap",new Vector2(x,y),new Vector2(256,256),scale*.64f,0,SurfaceOrder(y,1));
            // The approved local interior assembly has no roof strip.
            // Exterior roofs await their own approved modules; do not invent a remnant here.
            if (b.Name == TownLayout.Tavern.Name)
            {
                // Rotate the entire south-entry study 180 degrees for the existing north entry.
                Prop(root,"shop-counter",At(area,.5f,1f/3f),3.285f*unit,365,180);
                Prop(root,"shop-display",At(area,.76875f,.13056f),3.237f*unit,893,180);
                Prop(root,"storage-crates",At(area,.18333f,.15278f),2.178f*unit,792,180);
                TownNpcIdleV057.Attach(root,"merchant",At(area,.5f,.13889f));
            }
            else if (b.Name == TownLayout.Smithy.Name)
            {
                Prop(root,"storage-crates",At(area,73f/360f,220f/280f),2.376f*unit,792,0);
                Prop(root,"shop-counter",At(area,255f/360f,218f/280f),2.7375f*unit,365,0);
            }
            else
            {
                Prop(root,"storage-crates",new Vector2(right-1f,bottom+.8f),1f,792,0);
                TownNpcIdleV057.Attach(root,"elder",new Vector2(cx,top-1.4f));
            }
            return true;
        }

        public static bool TryAnvil(Transform parent)
        {
            if (!Available || !Module("smith-anvil")) return false;
            var p = TownLayout.Anvil.Pos;
            Prop(parent,"smith-anvil",new Vector2(p.X,p.Y),1.25f,331,0);
            return true;
        }

        public static void RoomSolids(Transform parent, TownBox b)
        {
            var area=RoomArea(b);float scale=ModuleScale(b,area);
            float l=area.xMin,r=area.xMax,d=area.yMin,u=area.yMax,c=b.Center.X;
            float thickness=67f*scale/Ppu,gap=134f*scale/Ppu;
            bool northEntry=b.Name!=TownLayout.Smithy.Name;float entry=northEntry?u:d,back=northEntry?d:u;
            Solid(parent,b.Name+" back",new Vector2((l+r)*.5f,back),new Vector2(r-l,thickness));
            Solid(parent,b.Name+" west",new Vector2(l,(d+u)*.5f),new Vector2(thickness,u-d));
            Solid(parent,b.Name+" east",new Vector2(r,(d+u)*.5f),new Vector2(thickness,u-d));
            Solid(parent,b.Name+" entry west",new Vector2((l+c-gap*.5f)*.5f,entry),new Vector2(c-gap*.5f-l,thickness));
            Solid(parent,b.Name+" entry east",new Vector2((c+gap*.5f+r)*.5f,entry),new Vector2(r-c-gap*.5f,thickness));
            // The open south-facing leaf projects onto the ground to the right of the passage.
            float leafScale=gap/1.2f;
            Solid(parent,b.Name+" open door leaf",new Vector2(c+(northEntry?-.69f:.69f)*leafScale,entry+(northEntry?.54f:-.54f)*leafScale),new Vector2(.12f,1.02f)*leafScale);
        }

        static void Solid(Transform parent,string id,Vector2 pos,Vector2 size)
        {
            var go=new GameObject("Module contact "+id);go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.layer=Layers.Wall;
            go.AddComponent<BoxCollider2D>().size=size;
        }

        static void Wall(Transform parent,Vector2 a,Vector2 b,float scale)
        {
            Vector2 d=b-a;float len=d.magnitude;if(len<.01f)return;
            float angle=Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg;
            // Fixed native thickness. Only crop the longitudinal end; never shrink both axes.
            for(float covered=0;covered<len-.001f;)
            {
                float remaining=(len-covered)*Ppu/scale;
                string id=remaining<=161f?"wall-short":"wall-straight";
                int sourceX=id=="wall-short"?178:51,maxWidth=id=="wall-short"?161:409;
                int width=Mathf.Min(maxWidth,Mathf.CeilToInt(remaining));
                var original=Module(id);var key=id+":span:"+width;
                if(!Crops.TryGetValue(key,out var sp)||!sp)
                {
                    sp=Sprite.Create(original.texture,new Rect(sourceX,original.texture.height-300,width,88),new Vector2(0,44.5f/88f),Ppu,0,SpriteMeshType.FullRect);
                    sp.name=key;Crops[key]=sp;
                }
                var p=a+d.normalized*covered;
                var go=new GameObject(id+" span");go.transform.SetParent(parent,false);go.transform.localPosition=p;
                go.transform.localRotation=Quaternion.Euler(0,0,angle);go.transform.localScale=Vector3.one*scale;
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sp;sr.sortingOrder=SurfaceOrder((p+d.normalized*(width*scale/Ppu*.5f)).y);
                if(covered>0)Place(parent,"wall-joint-cap",p,new Vector2(256,256),scale*.64f,0,SurfaceOrder(p.y,1));
                covered+=width*scale/Ppu;
            }
        }

        static SpriteRenderer Place(Transform parent,string id,Vector2 anchorWorld,Vector2 anchorPixel,float scale,float angle,int order)
        {
            var sp=Module(id);if(!sp)return null;
            Vector2 offset=new Vector2(anchorPixel.x-sp.rect.width*.5f,sp.rect.height*.5f-anchorPixel.y)/Ppu;
            var go=new GameObject(id);go.transform.SetParent(parent,false);
            go.transform.localPosition=anchorWorld-WorldProps.Rotate(offset*scale,angle);
            go.transform.localRotation=Quaternion.Euler(0,0,angle);go.transform.localScale=Vector3.one*scale;
            var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sp;sr.sortingOrder=order;return sr;
        }

        static void Prop(Transform parent,string id,Vector2 pos,float width,float visiblePixels,float angle)
        {
            var s=Module(id);if(!s)return;
            Place(parent,id,pos,new Vector2(s.rect.width*.5f,s.rect.height*.5f),width*Ppu/visiblePixels,angle,SurfaceOrder(pos.y));
            if(id=="shop-counter"||id=="shop-display"||id=="storage-crates")
            {
                float ratio=id=="shop-counter"?144f/365f:id=="shop-display"?283f/893f:388f/792f;
                Solid(parent,id,pos,new Vector2(width*.88f,width*ratio*.8f));
            }
        }

        static void Tile(Transform parent,string id,Rect area,Rect topLeftPixels,int order,float scale)
        {
            var original=Module(id);if(!original)return;
            int fullW=(int)topLeftPixels.width,fullH=(int)topLeftPixels.height;
            // Match the study's 196px stepping at .625 native scale (overlap panels).
            float tileW=313.6f*scale/Ppu,tileH=313.6f*scale/Ppu;
            for(float y=area.yMin;y<area.yMax-.001f;y+=tileH)
            for(float x=area.xMin;x<area.xMax-.001f;x+=tileW)
            {
                int w=Mathf.Min(fullW,Mathf.CeilToInt((area.xMax-x)*Ppu/scale));
                int h=Mathf.Min(fullH,Mathf.CeilToInt((area.yMax-y)*Ppu/scale));
                string key=id+":"+w+":"+h;
                if(!Crops.TryGetValue(key,out var sp)||!sp)
                {
                    var crop=new Rect(topLeftPixels.x,original.texture.height-topLeftPixels.y-h,w,h);
                    sp=Sprite.Create(original.texture,crop,Vector2.zero,Ppu,0,SpriteMeshType.FullRect);sp.name=key;Crops[key]=sp;
                }
                var go=new GameObject("Panel "+id);go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(x,y,0);
                go.transform.localScale=Vector3.one*scale;
                var sr=go.AddComponent<SpriteRenderer>();sr.sprite=sp;sr.sortingOrder=order;
            }
        }
    }
}
