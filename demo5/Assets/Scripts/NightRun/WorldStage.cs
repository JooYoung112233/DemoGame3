using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Demo5.NightRun
{
    public sealed class WorldStage : MonoBehaviour
    {
        public Sprite Shelter, Arcade, ScoutBody, SentryBody, InfectedBody, WhiteBase;
        public Material LitMaterial;
        public GameObject StandeePrefab;
        public Sprite SupplyCrate;
        public Color[] BaseColors={new Color(.28f,.58f,.74f),new Color(.84f,.67f,.27f),new Color(.57f,.69f,.46f),new Color(.72f,.46f,.4f)};
        public Light2D Ambient, ShelterLamp, ExitLamp;
        public bool LightingEnabled = true;
        SpriteRenderer backdrop;
        readonly GameObject[] pawns=new GameObject[6];
        readonly SpriteRenderer[] bodies=new SpriteRenderer[6],bases=new SpriteRenderer[6];
        readonly SpriteRenderer[] markers=new SpriteRenderer[16],crates=new SpriteRenderer[4];
        public Color ColorFor(int id)=>BaseColors[id%BaseColors.Length];
        public static readonly Color[] TeamColors={new Color(.28f,.58f,.74f),new Color(.84f,.67f,.27f),new Color(.57f,.69f,.46f),new Color(.72f,.46f,.4f)};
        public static Vector2 CellPoint(int x,int y){float t=y/3f;return new Vector2(Mathf.Lerp(605,550,t)+x*Mathf.Lerp(175,205,t),370+y*80);}
        public static Vector3 WorldPoint(float x,float y)=>new Vector3((x-960)/100,(540-y)/100,0);
        public static Vector2 BattlePoint(int side,int depth,int lane)=>new Vector2((side==0?760:1160)+(side==0?-1:1)*depth*114-lane*13,390+lane*83);
        public void ShowVisit(CampaignState campaign,ExplorationRun visit)
        {
            Ensure();backdrop.sprite=Arcade;backdrop.transform.localScale=new Vector3(19.2f/Arcade.bounds.size.x,10.8f/Arcade.bounds.size.y,1);
            foreach(var p in pawns)p.SetActive(false);foreach(var m in markers)m.gameObject.SetActive(false);foreach(var c in crates)c.gameObject.SetActive(false);
            bool combat=visit.Phase==VisitPhase.Combat;
            for(int i=0;i<2;i++)if(visit.Run.Squad[i].Health>0){var p=visit.Run.Squad[i];Pawn(i,BodyFor(campaign.Chosen[i]),ColorFor(campaign.Chosen[i]),combat?BattlePoint(0,p.X,p.Y):new Vector2(550+i*155,570+i*35),.8f);}
            if(combat)for(int i=0;i<visit.Foes.Count;i++){var p=visit.Foes[i];if(p.Health>0)Pawn(i+2,InfectedBody,new Color(.65f,.23f,.2f),BattlePoint(1,p.X,p.Y),.8f);}
            SetLighting(LightingEnabled,true,Mathf.Max(30,100-visit.Minutes/2));
        }
        void Ensure()
        {
            if(backdrop!=null)return;
            backdrop=MakeSprite("NeutralBackground",transform,Shelter,0);
            for(int i=0;i<6;i++)
            {
                pawns[i]=Instantiate(StandeePrefab,transform);pawns[i].name="Standee_"+i;
                bases[i]=pawns[i].transform.Find("WhiteBase").GetComponent<SpriteRenderer>();
                bases[i].transform.localScale=new Vector3(.82f/WhiteBase.bounds.size.x,.28f/WhiteBase.bounds.size.y,1);
                bodies[i]=pawns[i].transform.Find("Silhouette").GetComponent<SpriteRenderer>();
            }
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                int i=y*4+x;markers[i]=MakeSprite("FloorMarker_"+i,transform,WhiteBase,1);var p=CellPoint(x,y);markers[i].transform.position=WorldPoint(p.x,p.y);
                markers[i].transform.localScale=new Vector3(1.5f/WhiteBase.bounds.size.x,.4f/WhiteBase.bounds.size.y,1);markers[i].color=new Color(.35f,.85f,.65f,.38f);
            }
            for(int i=0;i<4;i++){crates[i]=MakeSprite("SupplyProp_"+i,transform,SupplyCrate,2);}
        }
        SpriteRenderer MakeSprite(string name,Transform parent,Sprite sprite,int order)
        {var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent,false);var r=go.GetComponent<SpriteRenderer>();r.sprite=sprite;r.sharedMaterial=LitMaterial;r.sortingOrder=order;return r;}
        public Sprite BodyFor(int candidate)=>candidate%2==0?ScoutBody:SentryBody;
        void Pawn(int i,Sprite body,Color color,Vector2 point,float scale=1)
        {
            pawns[i].SetActive(true);pawns[i].transform.position=WorldPoint(point.x,point.y);pawns[i].transform.localScale=Vector3.one*scale;
            bases[i].color=color;bodies[i].sprite=body;bodies[i].transform.localScale=Vector3.one*(1.7f/body.bounds.size.y);
            bodies[i].transform.localPosition=new Vector3(0,.56f,0);bases[i].sortingOrder=10+Mathf.RoundToInt(point.y);bodies[i].sortingOrder=bases[i].sortingOrder+1;
        }
        public void Show(CampaignState campaign,RunState run,int selected=0,Order order=Order.Move)
        {
            Ensure();bool battle=campaign.Stage==JourneyStage.Expedition;
            backdrop.sprite=battle?Arcade:Shelter;backdrop.transform.localScale=new Vector3(19.2f/backdrop.sprite.bounds.size.x,10.8f/backdrop.sprite.bounds.size.y,1);
            foreach(var p in pawns)p.SetActive(false);
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)markers[y*4+x].gameObject.SetActive(battle&&run.Phase==RunPhase.Expedition&&run.Validate(selected,order,x,y).Length==0);
            for(int i=0;i<4;i++)
            {
                crates[i].gameObject.SetActive(battle);int j=i%2;int x=i<2?RunState.Caches[j,0]:RunState.CoverProps[j,0],y=i<2?RunState.Caches[j,1]:RunState.CoverProps[j,1];var p=CellPoint(x,y);
                crates[i].transform.position=WorldPoint(p.x,p.y-27);crates[i].transform.localScale=Vector3.one*(1.1f/SupplyCrate.bounds.size.x);crates[i].sortingOrder=10+Mathf.RoundToInt(p.y);
            }
            if(battle)
            {
                for(int i=0;i<2;i++)if(run.Squad[i].Active){var p=run.Squad[i];Pawn(i,BodyFor(campaign.Chosen[i]),ColorFor(campaign.Chosen[i]),CellPoint(p.X,p.Y),.72f+p.Y*.04f);}
                for(int i=0;i<run.Enemies.Count&&i<4;i++)if(run.Enemies[i].Active){var p=run.Enemies[i];Pawn(i+2,InfectedBody,new Color(.7f,.22f,.2f),CellPoint(p.X,p.Y),.72f+p.Y*.04f);}
            }
            else if(campaign.Stage==JourneyStage.Settlement)
                for(int i=0;i<campaign.Chosen.Count;i++){int id=campaign.Chosen[i];if(campaign.Candidates[id].Health>0)Pawn(i,BodyFor(id),ColorFor(id),new Vector2(650+i*180,620));}
            SetLighting(LightingEnabled,battle,run.Light);
        }
        public void SetLighting(bool enabled,bool battle=false,int light=100)
        {
            LightingEnabled=enabled;
            if(Ambient!=null){Ambient.intensity=enabled?(battle?Mathf.Lerp(.32f,.68f,light/100f):.68f):1;Ambient.color=Color.white;}
            if(ShelterLamp!=null){ShelterLamp.enabled=enabled&&!battle;ShelterLamp.intensity=.85f;}
            if(ExitLamp!=null){ExitLamp.enabled=enabled;ExitLamp.intensity=.7f;}
        }
    }
}
