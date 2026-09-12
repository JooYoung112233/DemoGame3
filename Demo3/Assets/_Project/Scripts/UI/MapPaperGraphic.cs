using System;
using UnityEngine;
using UnityEngine.UI;

namespace Live49.UI
{
    // Editable game geography, not a surveyed map. Existing component name preserves references.
    [RequireComponent(typeof(CanvasRenderer))]
    public class MapPaperGraphic : MaskableGraphic
    {
        public const float Width=1220, Height=792;
        public static readonly Vector2 Camp=new Vector2(395,590);
        public static readonly Vector2 Store=new Vector2(734,395);
        public static readonly Vector2 StoreParking=new Vector2(765,437);
        public bool OverlayOnly;
        public bool GateMap;
        public Vector2[] Destinations=Array.Empty<Vector2>();
        public Vector2? Selected;
        public Vector2 Current=Camp;
        static Color C(byte r,byte g,byte b)=>new Color32(r,g,b,255);
        static readonly float[] Columns={95,260,420,680,900,1100};
        static readonly float[] Rows={280,460,630,820};
        static readonly Vector2[] River={new Vector2(-100,262),new Vector2(130,170),new Vector2(350,104),new Vector2(570,80),new Vector2(790,88),new Vector2(1040,145),new Vector2(1320,195)};
        struct Road {public Vector2 A,B;public float Width;public Road(Vector2 a,Vector2 b,float width){A=a;B=b;Width=width;}}
        static Road[] MakeRoads()
        {
            var roads=new System.Collections.Generic.List<Road>();
            foreach(float y in Rows)roads.Add(new Road(new Vector2(-30,y),new Vector2(1250,y),y==460?24:15));
            foreach(float x in Columns)roads.Add(new Road(new Vector2(x,230),new Vector2(x,850),x==420||x==900?22:12));
            for(int i=1;i<River.Length;i++)roads.Add(new Road(River[i-1]+Vector2.up*93,River[i]+Vector2.up*93,13));
            roads.Add(new Road(new Vector2(900,280),new Vector2(900,-50),20));
            roads.Add(new Road(new Vector2(680,280),new Vector2(680,150),12));
            foreach(float y in new[]{365f,545f,718f})
            {
                roads.Add(new Road(new Vector2(80,y-12),new Vector2(420,y+6),5));
                roads.Add(new Road(new Vector2(420,y+6),new Vector2(1100,y-7),5));
            }
            foreach(float x in new[]{180f,340f,550f,800f,1000f,1170f})roads.Add(new Road(new Vector2(x,280),new Vector2(x-16,792),5));
            return roads.ToArray();
        }
        static readonly Road[] Roads=MakeRoads();
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {var d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude));}
        static float RiverDistance(Vector2 p)
        {float d=10000;for(int i=1;i<River.Length;i++)d=Mathf.Min(d,SegmentDistance(p,River[i-1],River[i]));return d;}
        static bool Park(Vector2 p)=>((p-new Vector2(555,350))/new Vector2(98,55)).sqrMagnitude<1 || ((p-new Vector2(105,565))/new Vector2(92,60)).sqrMagnitude<1;
        static bool Reserved(Vector2 p)=>new Rect(698,357,90,85).Contains(p)||new Rect(366,562,93,52).Contains(p);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(GateMap)
            {
                Fill(vh,0,0,Width,Height,C(40,51,46));
                var road=new[]{new Vector2(-40,660),new Vector2(240,660),new Vector2(440,520),new Vector2(610,460),new Vector2(810,320),new Vector2(1250,180)};
                Stroke(vh,road,170,C(48,62,49));Stroke(vh,road,92,C(63,72,59));Stroke(vh,road,43,C(119,117,94));Stroke(vh,road,34,C(58,61,53));
                for(int i=1;i<road.Length;i++)Dashes(vh,road[i-1],road[i],2,C(172,150,93),12,17);
                Fill(vh,583,457,100,64,C(98,99,82));Outline(vh,592,466,82,43,2,C(155,150,114));
            }
            else if(!OverlayOnly)
            {
            Fill(vh,0,0,Width,Height,C(57,62,57));
            Fill(vh,436,301,224,132,C(59,76,61));Fill(vh,20,507,202,112,C(60,76,61));
            Stroke(vh,River,140,C(68,82,70));Stroke(vh,River,107,C(112,116,95));
            Stroke(vh,River,97,C(44,69,75));Stroke(vh,River,82,C(49,77,83));
            Stroke(vh,River,2,new Color(.58f,.70f,.69f,.17f));
            foreach(var road in Roads)Line(vh,road.A,road.B,road.Width+5,C(103,108,94));
            foreach(var road in Roads)Line(vh,road.A,road.B,road.Width,C(39,44,43));
            foreach(var road in Roads)if(road.Width>=20)Dashes(vh,road.A,road.B,1,C(133,126,93),10,10);
            Line(vh,new Vector2(885,1),new Vector2(885,227),2,C(167,161,133));
            Line(vh,new Vector2(915,1),new Vector2(915,227),2,C(167,161,133));
            var rng=new System.Random(4901);
            for(int y=12;y<790;y+=32)for(int x=12;x<1220;x+=35)
            {
                var p=new Vector2(x+rng.Next(-3,4),y+rng.Next(-3,4));
                float w=rng.Next(16,28),h=rng.Next(13,24),rise=rng.Next(3,8);
                if(RiverDistance(p)<76||Park(p)||Reserved(p))continue;
                bool blocked=false;
                foreach(var r in Roads)if(SegmentDistance(p,r.A,r.B)<r.Width*.5f+Mathf.Max(w,h)*.65f+3){blocked=true;break;}
                if(blocked)continue;
                byte tint=(byte)rng.Next(110,147);
                Building(vh,p.x-w*.5f,p.y-h*.5f,w,h,rise,C(tint,(byte)(tint+2),(byte)(tint-10)),rng.Next(0,3));
            }
            Building(vh,925,654,53,25,8,C(137,136,116),2);
            Building(vh,1020,661,50,42,9,C(119,126,119),1);
            Building(vh,461,659,65,25,7,C(131,132,113),2);
            Building(vh,707,372,55,43,7,C(164,154,120),1);
            Fill(vh,703,419,87,20,C(95,98,86));
            for(int x=708;x<790;x+=12)Line(vh,new Vector2(x,422),new Vector2(x,436),1,C(164,159,131));
            Fill(vh,373,575,73,29,C(86,90,78));
            for(int i=0;i<330;i++)
            {
                var p=new Vector2(rng.Next(8,1210),rng.Next(8,785));
                float river=RiverDistance(p);
                if(!Park(p)&&!(river>59&&river<75))continue;
                Circle(vh,p+new Vector2(3,4),5,C(34,43,37));Circle(vh,p,4.5f,C(83,104,76));Circle(vh,p-new Vector2(1,1),2.7f,C(97,114,84));
            }
            foreach(float x in Columns)foreach(float y in new[]{280f,460f,630f})
                for(int s=-7;s<=7;s+=4)Fill(vh,x+s,y-22,2,6,C(151,148,125));
            }
            if(Selected.HasValue)
            {
                var p=Selected.Value;Outline(vh,p.x-35,p.y-31,70,62,2.5f,C(229,193,107));
                if(Vector2.Distance(Current,p)>100)
                {
                    var route=Route(Current,p);Stroke(vh,route,7,C(37,39,33));Stroke(vh,route,3,C(228,190,103));
                    for(int i=1;i<route.Length;i++)Dashes(vh,route[i-1],route[i],1,C(255,223,151),3,19);
                }
            }
        }
        // Route along the shared street network; only the short property access legs leave roads.
        public static Vector2[] Route(Vector2 from,Vector2 to)
        {
            var points=new System.Collections.Generic.List<Vector2>();
            var edges=new System.Collections.Generic.List<Vector2Int>();
            for(int row=0;row<3;row++)for(int col=0;col<Columns.Length;col++)
            {
                int n=points.Count;points.Add(new Vector2(Columns[col],Rows[row]));
                if(col>0)edges.Add(new Vector2Int(n-1,n));
                if(row>0)edges.Add(new Vector2Int(n-Columns.Length,n));
            }
            var bank=new[]{new Vector2(420,214),new Vector2(680,174),new Vector2(900,205),new Vector2(1100,250)};
            for(int i=0;i<bank.Length;i++)
            {int n=points.Count;points.Add(bank[i]);edges.Add(new Vector2Int(i+2,n));if(i>0)edges.Add(new Vector2Int(n-1,n));}
            int startEdge=NearestEdge(from,points,edges,out var start),endEdge=NearestEdge(to,points,edges,out var end);
            int s=points.Count;points.Add(start);int t=points.Count;points.Add(end);
            var se=edges[startEdge];var te=edges[endEdge];
            edges.Add(new Vector2Int(s,se.x));edges.Add(new Vector2Int(s,se.y));edges.Add(new Vector2Int(t,te.x));edges.Add(new Vector2Int(t,te.y));
            if(startEdge==endEdge)edges.Add(new Vector2Int(s,t));
            var distances=new float[points.Count];var prev=new int[points.Count];var used=new bool[points.Count];
            for(int i=0;i<points.Count;i++){distances[i]=float.PositiveInfinity;prev[i]=-1;}distances[s]=0;
            for(int step=0;step<points.Count;step++)
            {
                int u=-1;for(int i=0;i<points.Count;i++)if(!used[i]&&(u<0||distances[i]<distances[u]))u=i;
                if(u<0||u==t)break;used[u]=true;
                foreach(var edge in edges)
                {
                    int v=edge.x==u?edge.y:edge.y==u?edge.x:-1;if(v<0)continue;
                    float d=distances[u]+Vector2.Distance(points[u],points[v]);
                    if(d<distances[v]){distances[v]=d;prev[v]=u;}
                }
            }
            var route=new System.Collections.Generic.List<Vector2>{to};
            for(int n=t;n>=0;n=prev[n]){route.Add(points[n]);if(n==s)break;}
            route.Add(from);route.Reverse();return route.ToArray();
        }
        static int NearestEdge(Vector2 point,System.Collections.Generic.List<Vector2> nodes,System.Collections.Generic.List<Vector2Int> edges,out Vector2 projection)
        {
            int best=0;float distance=float.PositiveInfinity;projection=point;
            for(int i=0;i<edges.Count;i++)
            {
                var a=nodes[edges[i].x];var d=nodes[edges[i].y]-a;
                var p=a+d*Mathf.Clamp01(Vector2.Dot(point-a,d)/d.sqrMagnitude);
                float length=(p-point).sqrMagnitude;if(length<distance){distance=length;best=i;projection=p;}
            }
            return best;
        }
        void Building(VertexHelper vh,float x,float y,float w,float h,float rise,Color roof,int detail)
        {
            Fill(vh,x+5,y+6,w+rise,h+rise,new Color(0,0,0,.24f));
            Fill(vh,x,y+rise,w,h,C(70,74,67));Fill(vh,x+rise,y,w,h,C(80,82,72));
            Fill(vh,x,y,w,h,roof);Outline(vh,x,y,w,h,.75f,C(164,161,139));
            if(detail==0)Fill(vh,x+3,y+3,5,5,C(84,91,83));
            else if(detail==1){Fill(vh,x+3,y+3,w-6,2,C(191,180,142));Fill(vh,x+w-9,y+h-9,5,5,C(81,88,83));}
            else Line(vh,new Vector2(x+2,y+h*.5f),new Vector2(x+w-2,y+h*.5f),1,C(174,171,145));
        }
        void Outline(VertexHelper v,float x,float y,float w,float h,float t,Color c)
        {Fill(v,x,y,w,t,c);Fill(v,x,y+h-t,w,t,c);Fill(v,x,y,t,h,c);Fill(v,x+w-t,y,t,h,c);}
        void Dashes(VertexHelper v,Vector2 a,Vector2 b,float width,Color c,float dash,float gap)
        {float length=Vector2.Distance(a,b);var d=(b-a).normalized;for(float t=0;t<length;t+=dash+gap)Line(v,a+d*t,a+d*Mathf.Min(t+dash,length),width,c);}
        void Stroke(VertexHelper v,Vector2[] points,float width,Color c)
        {for(int i=1;i<points.Length;i++)Line(v,points[i-1],points[i],width,c);for(int i=1;i<points.Length-1;i++)Circle(v,points[i],width*.5f,c);}
        void Circle(VertexHelper v,Vector2 p,float r,Color c)
        {for(int i=0;i<12;i++){float a=i*Mathf.PI/6,b=(i+1)*Mathf.PI/6;Triangle(v,p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*r,c);}}
        void Fill(VertexHelper v,float x,float y,float w,float h,Color c)=>Quad(v,new Vector2(x,y),new Vector2(x+w,y),new Vector2(x+w,y+h),new Vector2(x,y+h),c);
        void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color c)
        {if((b-a).sqrMagnitude<.001f)return;var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;Quad(v,a-n,a+n,b+n,b-n,c);}
        void Vertex(VertexHelper v,Vector2 p,Color c)=>v.AddVert(new Vector3(rectTransform.rect.xMin+p.x,rectTransform.rect.yMax-p.y),c,Vector2.zero);
        void Triangle(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Color color)
        {int s=v.currentVertCount;Vertex(v,a,color);Vertex(v,b,color);Vertex(v,c,color);v.AddTriangle(s,s+1,s+2);}
        void Quad(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {int s=v.currentVertCount;Vertex(v,a,color);Vertex(v,b,color);Vertex(v,c,color);Vertex(v,d,color);v.AddTriangle(s,s+1,s+2);v.AddTriangle(s,s+2,s+3);}
    }
}
