using System;
using System.Linq;
using System.Collections.Generic;
namespace Demo5.NightRun
{
    public enum VisitPhase { Explore, Encounter, Combat, Result, Finished }
    public enum SearchPace { Quick, Normal, Thorough }
    public enum PartnerDuty { Search, Watch, Light }
    // Small, deliberately independent prototype rules. Time advances only on committed actions.
    public sealed class ExplorationRun
    {
        public readonly RunState Run;
        readonly Func<double> roll;
        public VisitPhase Phase { get; private set; } = VisitPhase.Explore;
        public readonly int[] Progress = new int[3], Stock = { 2, 2, 2 };
        public readonly string[] Places = { "물자 상자", "자판기 아래", "잠긴 듯한 보관함" };
        public int Minutes, Noise, Loot, Battery=100, Searches, Cooldown, Round=1;
        public int Ammo { get; private set; }
        public int Lanes { get; private set; } = 3;
        public int Actor { get; private set; }
        public bool Moved { get; private set; }
        public string Message { get; private set; } = "아직 조용하다. 수색할 곳을 선택하자. 첫 수색은 안전하다.";
        public readonly List<Piece> Foes = new List<Piece>();
        public bool HasFlashlight=true;
        public int FlashlightCarrier=1;
        public int Danger => Math.Min(65, 10+Noise*5+Minutes/5);
        public ExplorationRun(RunState run,Func<double> random=null){Run=run;Ammo=run.Ammo;roll=random??new Random().NextDouble;}
        public int Duration(SearchPace pace,int who,PartnerDuty duty)
        {
            int t=new[]{5,10,18}[(int)pace];string role=Run.Squad[who].Role;
            if((pace==SearchPace.Quick&&role=="정찰수")||(pace==SearchPace.Normal&&role=="운반꾼")||(pace==SearchPace.Thorough&&role=="사수"))t-=3;
            if(duty==PartnerDuty.Search&&Run.Squad[1-who].Health>0)t-=2;
            return Math.Max(3,t);
        }
        public bool CanLight(int who)=>HasFlashlight&&FlashlightCarrier==1-who&&Run.Squad[1-who].Health>0&&Battery>=5;
        public bool Search(int site,SearchPace pace,int who,PartnerDuty duty)
        {
            if(Phase!=VisitPhase.Explore||site<0||site>=3||who<0||who>1||Run.Squad[who].Health<=0||Progress[site]>=100)return false;
            if(duty==PartnerDuty.Light&&!CanLight(who)){Message="손전등을 가진 동료와 배터리가 필요하다.";return false;}
            int time=Duration(pace,who,duty);Minutes+=time;
            int gain=new[]{22,38,60}[(int)pace];if(duty==PartnerDuty.Light){gain+=15;Battery-=5;}
            Progress[site]=Math.Min(100,Progress[site]+gain);Noise+=(int)pace+1;
            bool found=Stock[site]>0&&(roll()<new[]{.45,.7,.9}[(int)pace]||Progress[site]==100);
            if(found){Stock[site]--;Loot++;}
            // Completion accounts for the remaining finite contents; no reroll farming.
            if(Progress[site]==100){Loot+=Stock[site];Stock[site]=0;}
            Message=Places[site]+" · "+time+"분 경과 / "+(found?"보급품 발견":"물건은 없지만 수색 진척 확보")+" · 수색도 "+Progress[site]+"%";
            Searches++;bool safe=Searches==1||Cooldown>0;if(Cooldown>0)Cooldown--;
            int guard=duty==PartnerDuty.Watch&&Run.Squad[1-who].Health>0?(Run.Squad[1-who].Role=="경비원"?22:15):0;
            if(!safe&&roll()<Math.Max(5,Danger-guard)/100.0){Phase=VisitPhase.Encounter;Message+="\n가까운 곳에서 끌리는 발소리. 감염자 둘이 다가온다.";}
            return true;
        }
        public bool ChangeLanes(int n){if(Phase!=VisitPhase.Explore||(n!=3&&n!=4))return false;Lanes=n;return true;}
        public bool PreviewEncounter(){if(Phase!=VisitPhase.Explore)return false;Phase=VisitPhase.Encounter;Message="전투 시연 · 감염자 둘을 발견했다.";return true;}
        public bool Avoid()
        {
            if(Phase!=VisitPhase.Encounter)return false;Minutes+=5;
            if(roll()<.7){Phase=VisitPhase.Explore;Cooldown=1;Message="숨을 죽이고 기다렸다. 적이 지나갔다. +5분";}else BeginCombat();return true;
        }
        public bool BeginCombat()
        {
            if(Phase!=VisitPhase.Encounter)return false;Phase=VisitPhase.Combat;Round=1;Foes.Clear();
            for(int i=0;i<2;i++){Run.Squad[i].X=0;Run.Squad[i].Y=i;Run.Squad[i].Guarding=false;Foes.Add(new Piece("감염자 "+(i+1),0,i){Health=3,MaxHealth=3});}
            Actor=Run.Squad.FindIndex(p=>p.Health>0);Moved=false;Message="아군 차례. 위치 변경 1회 + 행동 1회. 칸을 누르면 이동한다.";return true;
        }
        public bool Move(int depth,int lane)
        {
            if(Phase!=VisitPhase.Combat||Moved||depth<0||depth>2||lane<0||lane>=Lanes||Run.Squad.Any(p=>p.Health>0&&p.X==depth&&p.Y==lane))return false;
            var a=Run.Squad[Actor];if(Math.Abs(a.X-depth)+Math.Abs(a.Y-lane)!=1)return false;
            a.X=depth;a.Y=lane;Moved=true;Message="위치를 옮겼다. 공격 또는 방어를 선택하자.";return true;
        }
        public int HitChance(int enemy,bool ranged)
        {
            if(enemy<0||enemy>=Foes.Count)return 0;var a=Run.Squad[Actor];var e=Foes[enemy];
            if(!ranged&&a.X!=0)return 0;
            return Math.Max(20,Math.Min(95,(ranged?80+a.AimBonus-a.X*5:90)-Math.Abs(a.Y-e.Y)*5));
        }
        public bool Attack(int enemy,bool ranged)
        {
            if(Phase!=VisitPhase.Combat||enemy<0||enemy>=Foes.Count||Foes[enemy].Health<=0||(ranged&&Ammo==0)||(!ranged&&Run.Squad[Actor].X!=0))return false;
            int chance=HitChance(enemy,ranged);if(ranged){Ammo--;Noise+=2;}
            bool hit=roll()<chance/100.0;var e=Foes[enemy];if(hit)e.Health=Math.Max(0,e.Health-(ranged?3:2));
            Message=Run.Squad[Actor].Name+" → "+e.Name+" · "+chance+"% · "+(hit?"명중":"빗나감");Advance();return true;
        }
        public bool Guard(){if(Phase!=VisitPhase.Combat)return false;Run.Squad[Actor].Guarding=true;Message=Run.Squad[Actor].Name+" 방어";Advance();return true;}
        public bool Flee()
        {
            if(Phase!=VisitPhase.Combat)return false;Minutes+=5;
            if(roll()<.65){Phase=VisitPhase.Result;Message="전투에서 벗어났다. 수색 장소로 돌아간다.";Cooldown=2;}
            else{Message="도주 실패. 적이 추격한다.";EnemyTurn();Actor=Run.Squad.FindIndex(p=>p.Health>0);Moved=false;}return true;
        }
        void Advance()
        {
            if(Foes.All(p=>p.Health<=0)){Phase=VisitPhase.Result;Message+="\n적을 물리쳤다. 수색 진척은 유지된다.";Cooldown=2;return;}
            int next=Run.Squad.FindIndex(Actor+1,p=>p.Health>0);
            if(next<0){EnemyTurn();next=Run.Squad.FindIndex(p=>p.Health>0);}
            Actor=next;Moved=false;
        }
        void EnemyTurn()
        {
            foreach(var e in Foes.Where(p=>p.Health>0))
            {
                var target=Run.Squad.Where(p=>p.Health>0).OrderBy(p=>p.X*3+Math.Abs(e.Y-p.Y)).FirstOrDefault();if(target==null)break;
                if(roll()<.8){int damage=target.Guarding?1:2;target.Health=Math.Max(0,target.Health-damage);Message+="\n"+target.Name+" 피해 "+damage;}else Message+="\n감염자의 공격이 빗나갔다.";
            }
            foreach(var p in Run.Squad)p.Guarding=false;Round++;Minutes+=2;
            if(Run.Squad.All(p=>p.Health<=0)){Phase=VisitPhase.Result;Message="원정대가 쓰러졌다. 정착지로 돌아갈 사람이 없다.";}
        }
        public bool Resume(){if(Phase!=VisitPhase.Result)return false;if(Run.Squad.All(p=>p.Health<=0))return Return();Phase=VisitPhase.Explore;Message="주변이 조용해졌다. 수색을 이어가거나 귀환하자.";return true;}
        public bool Return(){if(Phase!=VisitPhase.Explore&&!(Phase==VisitPhase.Result&&Run.Squad.All(p=>p.Health<=0)))return false;Phase=VisitPhase.Finished;Run.CompleteVisit(Loot,Ammo);return true;}
    }
}
