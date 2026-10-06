using System;
using System.Linq;
using System.Collections.Generic;
using Demo5.NightRun;
using UnityEngine;

namespace Demo5.FrontEnd
{
    [Serializable] public sealed class SavedCount { public string Id; public int Count; }
    [Serializable] public sealed class SavedMember
    {
        public string Id, Name, Role, Description;
        public int Health, Maximum, Aim, Capacity;
        public SavedCount[] Bag;
    }
    [Serializable] public sealed class SavedRest { public string MemberId, Name; public int Minutes, Recovery; }
    [Serializable] public sealed class SavedCraft { public string MemberId, RecipeId; public int Quantity, Minutes; public SavedCount[] Reserved; }
    [Serializable] public sealed class SavedCooking { public string MemberId, RecipeId, OutputId; public int Quantity, Minutes, OutputCount; public SavedCount[] Reserved; }
    [Serializable] public sealed class SavedSearch
    {
        public string Id;
        public int Progress, Required, Pace, Bonus, Duty;
        public bool Complete, Opened;
        public SavedCount[] Loot;
    }
    [Serializable] public sealed class SavedReportMember { public string Id; public int Before, After, Maximum; }
    [Serializable] public sealed class SavedReportItem { public string Id; public int Before, After; }
    [Serializable] public sealed class SavedReport
    {
        public string Destination, Body;
        public int Minutes;
        public SavedReportMember[] Members;
        public SavedReportItem[] Items;
    }
    // Id = destination id ("mall"); Remembered = "" or a stable room id; the record's presence means the site has been visited.
    // ResidentLessonShown: the one-time battle lesson for the site's resident; a missing key (older saves) reads as false, so no version bump.
    [Serializable] public sealed class SavedSiteBoard { public string Id, Remembered; public int Danger, Gauge; public bool IntroShown; public bool ResidentLessonShown; }
    [Serializable] public sealed class CampaignSaveData
    {
        public int Version = CampaignPersistence.CurrentVersion;
        public string SavedUtc, HomeId;
        // v14 dropped the retired supplies counter; JsonUtility skips the old "Supplies" key of older saves.
        public int Day, Minute, Ammo;
        public int IntroductionStep; public SavedDevelopment Development;
        public SavedOpeningChapter Opening;
        public SavedTutorialNarrative TutorialNarrative = new SavedTutorialNarrative();
        public SavedMissingPerson MissingPerson = new SavedMissingPerson();
        public SavedMember[] Members;
        public SavedCount[] Materials;
        public SavedRest[] Rest;
        public SavedCraft[] Craft;
        public SavedCooking[] Cooking;
        public SavedVisitor Visitor;
        public bool BedRepaired, BenchImproved, CookerImproved, CorridorVisited;
        public bool SideRoomConnected, SideRoomReady, StorageUnlocked, StorageVisited;
        public string[] Inspected;
        public SavedSearch[] Searches;
        public SavedSiteBoard[] SiteBoards;
        public SavedNpcStory Doyun=new SavedNpcStory();
        public string[] UnlockedCharacters=Array.Empty<string>();
        public SavedReport ReturnReport;
        public string[] Activity;
    }

    // Data is captured independently of visible UI. References are resolved by stable content IDs.
    public static class CampaignPersistence
    {
        public const int CurrentVersion=22;
        public static readonly string[] HomeIds = { "garage", "house", "clinic" };
        public static readonly string[] SiteIds = { "mall.arcade.crate", "mall.arcade.table", "mall.arcade.machine", "mall.arcade.door", "mall.corridor.powerbox", "mall.corridor.crate", "mall.storage.shelf", "mall.storage.materials", "mall.office.shelf" };
        public static readonly string[] SiteBoardIds = { "mall" };
        // Index = FieldSiteState.Arcade..Den (0..3)
        public static readonly string[] SiteRoomIds = { "mall.arcade", "mall.corridor", "mall.storage", "mall.office" };
        public static CampaignSaveData Pending { get; private set; }
        public static readonly string[] FoodIds={"food","water","can","meal","ration"};
        public static readonly string[] LifeIds={"raw-water","bandage"};
        public static readonly string[] EverydayIds={"fuel","lubricant","battery","tobacco","coffee","alcohol","electrical-parts","weapon-parts"};
        static SavedCount[] WithEverydayCounts(SavedCount[] counts,IEnumerable<string> registered)
        {
            if(counts==null)return null; // Let validation reject a missing container, rather than repairing it.
            return counts.Concat(EverydayIds.Where(id=>registered.Contains(id)&&!counts.Any(c=>c!=null&&c.Id==id))
                .Select(id=>new SavedCount{Id=id,Count=0})).ToArray();
        }
        static SavedVisitor WithEverydayVisitorStock(SavedVisitor visitor,SettlementController catalog)
        {
            if(visitor==null||visitor.VisitDay==0)return visitor;
            return new SavedVisitor{VisitDay=visitor.VisitDay,Dismissed=visitor.Dismissed,Recruited=visitor.Recruited,RecruitId=visitor.RecruitId,
                Stock=WithEverydayCounts(visitor.Stock,catalog.VisitorPanel.Goods.Select(g=>g.Id))};
        }
        // Items removed from the game (supplies: 2026-09-25, v14). Older saves drop them before the first Validate, which rejects unknown ids.
        static readonly string[] RetiredItemIds={"supplies"};
        static SavedCount[] WithoutRetired(SavedCount[] rows)=>rows?.Where(r=>r==null||!RetiredItemIds.Contains(r.Id)).ToArray();
        // The value is discarded, not converted: it had no use and the warehouse never counted it. Null arrays are left for Validate to report.
        static void DropRetiredItems(CampaignSaveData d)
        {
            if(d==null||d.Version>=14)return;
            if(d.Members!=null)foreach(var p in d.Members)if(p!=null)p.Bag=WithoutRetired(p.Bag);
            if(d.Searches!=null)foreach(var r in d.Searches)if(r!=null)r.Loot=WithoutRetired(r.Loot);
            if(d.ReturnReport?.Items!=null)d.ReturnReport.Items=d.ReturnReport.Items.Where(i=>i==null||!RetiredItemIds.Contains(i.Id)).ToArray();
        }
        public static void Upgrade(CampaignSaveData data,SettlementController catalog)
        {
            // Unity serializes a null inline class as a default-valued object.
            // Only that exact empty shape represents "no previous expedition".
            var report=data?.ReturnReport;
            if(report!=null && string.IsNullOrEmpty(report.Destination) && string.IsNullOrEmpty(report.Body) && report.Minutes==0 && (report.Members==null || report.Members.Length==0) && (report.Items==null || report.Items.Length==0))data.ReturnReport=null;
            DropRetiredItems(data);
            Validate(data,catalog);
            if(data.Version==1){
                data.Materials=data.Materials.Concat(FoodIds.Where(id=>!data.Materials.Any(m=>m.Id==id)).Select(id=>new SavedCount{Id=id,Count=0})).ToArray();
                data.Cooking=Array.Empty<SavedCooking>();data.Version=2;
            }
            if(data.Version==2){
                data.Materials=data.Materials.Concat(LifeIds.Where(id=>!data.Materials.Any(m=>m.Id==id)).Select(id=>new SavedCount{Id=id,Count=0})).ToArray();
                data.Version=3;
            }
            if(data.Version==3){data.Visitor=new SavedVisitor();data.Version=4;}
            if(data.Version==4){data.CookerImproved=false;data.Version=5;}
            if(data.Version==5){data.SideRoomConnected=false;data.SideRoomReady=false;data.Version=6;}
            if(data.Version==6){data.Visitor.Recruited=false;data.Visitor.RecruitId=data.Visitor.VisitDay>0?catalog.Roster.Candidates.FirstOrDefault(p=>!data.Members.Any(m=>m.Id==p.Id))?.Id:null;data.Version=7;}
            if(data.Version==7){data.StorageUnlocked=data.StorageVisited=false;data.Version=8;}
            if(data.Version==8){data.IntroductionStep=10;data.Version=9;}
            if(data.Version==9){
                int step=data.IntroductionStep;
                data.Development=new SavedDevelopment{Bed=step>=3,Workbench=step>=6,Cooker=step>=8,Warehouse=step>=5?1:0,Tools=data.BenchImproved,Comfort=data.BedRepaired||data.CookerImproved};
                if(step>=5&&step<10)data.IntroductionStep=data.ReturnReport!=null?10:5;
                data.Version=10;
            }
            if(data.Version==10){data.Opening=new SavedOpeningChapter();data.Version=11;}
            if(data.Version==11){data.Opening.NightHeard=false;data.Opening.NightChoice=0;data.Version=12;}
            if(data.Version==12){
                // v12 kept the site board in memory only: derive the visit from what was saved; danger, noise and memory start clean (what a v12 load already did).
                bool visited=data.CorridorVisited||data.Inspected.Length>0||data.Searches.Length>0||data.ReturnReport!=null||data.Opening.FirstReturn;
                // The office shelf can only be inspected/searched while the board is awake (ExpeditionSiteThreat.CanSearchSite, OpenDen), and the intro pops before any click.
                bool awake=data.Inspected.Contains("mall.office.shelf")||data.Searches.Any(r=>r.Id=="mall.office.shelf");
                data.SiteBoards=visited?new[]{new SavedSiteBoard{Id="mall",Remembered="",IntroShown=awake}}:Array.Empty<SavedSiteBoard>();
                data.Version=13;
            }
            // v13 → v14: the supplies item and counter were retired; DropRetiredItems already stripped them above.
            if(data.Version==13){data.Version=14;}
            // v14 has no NPC history. Never infer a meeting or name from an ordinary site visit.
            if(data.Version==14){data.Doyun=new SavedNpcStory();data.Version=15;}
            if(data.Version==15)
            {
                UpgradeCharacterBalance(data,catalog.Roster);
                // People already met or recruited stay available; do not relock old campaigns.
                data.UnlockedCharacters=catalog.Roster.Candidates.Where(p=>p.AvailableAtStart).Select(p=>p.Id)
                    .Concat(data.Members.Select(p=>p.Id))
                    .Concat(string.IsNullOrEmpty(data.Visitor.RecruitId)?Array.Empty<string>():new[]{data.Visitor.RecruitId})
                    .Distinct().OrderBy(id=>id,StringComparer.Ordinal).ToArray();
                data.Version=16;
            }
            if(data.Version==16)
            {
                // New goods start empty. Never refill an existing visitor or reroll completed searches.
                data.Materials=WithEverydayCounts(data.Materials,catalog.CraftPanel.Materials.Select(m=>m.Id));
                data.Visitor=WithEverydayVisitorStock(data.Visitor,catalog);
                data.Version=17;
            }
            if(data.Version==17)
            {
                // Derive dialogue context only; never replay old actions, grant items or advance time.
                data.TutorialNarrative=TutorialNarrativeForProgress(data);
                data.Version=18;
            }
            if(data.Version==18){data.MissingPerson=new SavedMissingPerson();data.Version=19;}
            if(data.Version==19){data.MissingPerson.RouteKnown=false;data.MissingPerson.MetMiran=false;data.MissingPerson.MiranQuestions=0;data.Version=20;}
            if(data.Version==20){data.MissingPerson.MetGeumrye=false;data.MissingPerson.ReunionComplete=false;data.Version=21;}
            if(data.Version==21){data.MissingPerson.NewsShared=false;data.MissingPerson.NewsMessengerId=null;data.MissingPerson.NewsLine=0;data.Version=22;}
            string[] legacyNames={"탐험가 1","정비공","의무관","요리사","연구자","경비원"};
            foreach(var member in data.Members){var p=catalog.Roster.Candidates.First(x=>x.Id==member.Id);if(legacyNames.Contains(member.Name)){member.Name=p.DisplayName;member.Role=p.RoleTitle;member.Description=p.Description;}}
            Validate(data,catalog);
        }
        public static string MemberId(SettlementController owner, Adventurer member)
        {
            int index = Array.IndexOf(owner.Campaign.Party.ToArray(), member);
            if (index < 0 || index >= PartySelectionSession.Selected.Count) throw new InvalidOperationException("대원 식별자를 확인할 수 없습니다.");
            return PartySelectionSession.Selected[index];
        }
        static void UpgradeCharacterBalance(CampaignSaveData data,PartyRoster roster)
        {
            foreach(var member in data.Members)
            {
                var candidate=roster.Candidates.First(c=>c.Id==member.Id);
                int oldHealth,oldCapacity;
                switch(member.Id)
                {
                    case "scout": oldHealth=3;oldCapacity=3;break;
                    case "medic": oldHealth=4;oldCapacity=3;break;
                    case "mechanic": oldHealth=3;oldCapacity=4;break;
                    case "cook": oldHealth=4;oldCapacity=4;break;
                    case "researcher": oldHealth=3;oldCapacity=3;break;
                    case "guard": oldHealth=4;oldCapacity=4;break;
                    default: continue;
                }
                if(member.Maximum==oldHealth)
                {
                    int missing=member.Maximum-member.Health;
                    member.Health=member.Health==0?0:Math.Max(1,candidate.Health-missing);
                    member.Maximum=candidate.Health;
                }
                if(member.Aim==0)member.Aim=candidate.Aim;
                // Keep all carried stacks when an old guard's bag was larger. This legacy
                // capacity is saved for that person; newly recruited guards use the new limit.
                if(member.Capacity==oldCapacity)member.Capacity=Math.Max(candidate.BagCapacity,member.Bag.Count(i=>i.Count>0));
            }
        }
        public static SavedCount[] Counts(IEnumerable<KeyValuePair<string, int>> values) => values.Where(x => x.Value != 0).OrderBy(x => x.Key).Select(x => new SavedCount { Id = x.Key, Count = x.Value }).ToArray();
        static SavedTutorialNarrative TutorialNarrativeForProgress(CampaignSaveData data)
        {
            if(data.Opening==null||!data.Opening.Enabled||data.Opening.Complete)
                return new SavedTutorialNarrative { SeenMask=SavedTutorialNarrative.AllSeen };
            // 0 arrival, 1 inspected, 2 supplies found, 3 bed cleared, 4 rested;
            // then first return, workbench, clue, prybar, survey return, conclusion.
            int current=Math.Min(4,Math.Max(0,data.IntroductionStep));
            if(data.Opening.FirstReturn)
            {
                current=5;
                if(data.Development.Workbench)current=6;
                if(data.Opening.ClueRead)
                {
                    current=7;
                    if(data.StorageUnlocked||data.Materials.Any(m=>m.Id=="prybar"&&m.Count>0)||data.Members.Any(p=>p.Bag.Any(i=>i.Id=="prybar"&&i.Count>0)))current=8;
                }
                if(data.Opening.SurveyReturned)current=9;
            }
            return new SavedTutorialNarrative { SeenMask=(1<<current)-1 };
        }
        public static CampaignSaveData Capture(SettlementController c)
        {
            if (c.Campaign == null || c.Campaign.Stage != JourneyStage.Settlement || c.Campaign.IsFieldExpedition) throw new InvalidOperationException("정착지에 돌아온 뒤 저장할 수 있습니다.");
            var s = new CampaignSaveData {
                SavedUtc = DateTime.UtcNow.ToString("O"), HomeId = HomeIds[Array.IndexOf(c.Campaign.Sites, c.Campaign.Home)],
                Day = c.Campaign.Day, Minute = c.Campaign.MinuteOfDay, Ammo = c.Campaign.Ammo,
                IntroductionStep=c.Introduction?c.Introduction.Step:10, Development=c.Development?JsonUtility.FromJson<SavedDevelopment>(JsonUtility.ToJson(c.Development.State)):new SavedDevelopment{Bed=true,Workbench=true,Cooker=true,Warehouse=1},
                Opening=c.Opening?c.Opening.Export():new SavedOpeningChapter(), Doyun=NpcStory(c.ArrivalPanel),
                MissingPerson=c.MissingPerson?c.MissingPerson.Export():new SavedMissingPerson(),
                UnlockedCharacters=c.ExportCharacterUnlocks(),
                Members = c.Campaign.Party.Select(p => new SavedMember { Id = MemberId(c,p), Name=p.Name, Role=p.Role, Description=p.Description, Health=p.Health, Maximum=p.MaxHealth, Aim=p.Aim, Capacity=p.BagCapacity, Bag=Counts(c.InventoryPanel.Items.Select(i => new KeyValuePair<string,int>(i.Id,c.InventoryPanel.CountFor(p,i.Id)))) }).ToArray(),
                Materials = c.CraftPanel.Materials.Select(m => new SavedCount { Id=m.Id, Count=m.Initial }).ToArray(),
                Rest = c.WorkPanel.Orders.Select(o => new SavedRest { MemberId=MemberId(c,o.Member), Name=o.Name, Minutes=o.Minutes, Recovery=o.Recovery }).ToArray(),
                Craft = c.CraftPanel.Orders.Select(o => new SavedCraft { MemberId=MemberId(c,o.Member), RecipeId=o.Recipe.Id, Quantity=o.Quantity, Minutes=o.Minutes, Reserved=Counts(o.Reserved) }).ToArray(),
                Cooking = c.CookingPanel.Orders.Select(o => new SavedCooking { MemberId=MemberId(c,o.Member), RecipeId=o.Recipe.Id, Quantity=o.Quantity, Minutes=o.Minutes, OutputId=o.OutputId, OutputCount=o.OutputCount, Reserved=Counts(o.Reserved) }).ToArray(),
                Visitor=c.VisitorPanel.Export(),
                BedRepaired=c.CraftPanel.BedRepaired, BenchImproved=c.CraftPanel.BenchImproved, CookerImproved=c.CraftPanel.CookerImproved,
                SideRoomConnected=c.CraftPanel.SideRoomConnected, SideRoomReady=c.CraftPanel.SideRoomReady,
                CorridorVisited=c.ArrivalPanel.Rooms.CorridorVisited,StorageUnlocked=c.ArrivalPanel.Rooms.StorageUnlocked,StorageVisited=c.ArrivalPanel.Rooms.StorageVisited,
                Inspected=c.ArrivalPanel.Rooms.Inspected.OrderBy(i=>i).Select(i=>SiteIds[i]).ToArray(),
                Searches=c.ArrivalPanel.Loot.ExportSearches(), SiteBoards=Boards(c.ArrivalPanel.Threat), ReturnReport=c.ReturnPanel.ExportReport(), Activity=c.ActivityLog.TakeLast(100).ToArray()
            };
            var narrative=c.GetComponent<SettlementTutorialNarrative>();
            s.TutorialNarrative=narrative?narrative.Export():TutorialNarrativeForProgress(s);
            Validate(s,c);
            return s;
        }
        // The site board is optional everywhere (if(Threat) guards): no component, no record.
        static SavedSiteBoard[] Boards(ExpeditionSiteThreat t){var b=t?t.ExportSaved():null;return b==null?Array.Empty<SavedSiteBoard>():new[]{b};}
        static SavedNpcStory NpcStory(ExpeditionArrivalPanel arrival){var story=arrival?arrival.GetComponent<ExpeditionNpcStory>():null;return story?story.Export():new SavedNpcStory();}
        static bool DevelopmentDone(SavedDevelopment s,string id){switch(id){case "build-bed":return s.Bed;case "build-bench":return s.Workbench;case "build-cooker":return s.Cooker;case "build-stock":return s.Warehouse>=1;case "expand-stock":return s.Warehouse>=2;case "build-research":return s.Research;case "research-tools":return s.Tools;case "research-comfort":return s.Comfort;case "research-storage":return s.Storage;default:return false;}}
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static void CheckCounts(SavedCount[] rows, HashSet<string> known)
        {
            Require(rows != null && rows.Length <= 500, "물자 목록이 올바르지 않습니다.");
            var ids = new HashSet<string>();
            foreach (var r in rows) Require(r!=null && known.Contains(r.Id) && ids.Add(r.Id) && r.Count>=0 && r.Count<=1000000, "알 수 없거나 잘못된 물자 데이터입니다.");
        }
        public static void Validate(CampaignSaveData s, SettlementController catalog)
        {
            Require(s!=null, "저장 내용을 읽을 수 없습니다.");
            Require(s.Version>=1 && s.Version<=CurrentVersion, "지원하지 않는 저장 버전입니다.");
            if(s.Version>=18)ValidateTutorialNarrative(s.TutorialNarrative,catalog);
            if(s.Version>=19)Require(s.MissingPerson!=null&&(!s.MissingPerson.Discussed||s.MissingPerson.Found)&&s.MissingPerson.ReturnLine>=0&&s.MissingPerson.ReturnLine<3&&(s.MissingPerson.Found||s.MissingPerson.ReturnLine==0)&&(!s.MissingPerson.Discussed||s.MissingPerson.ReturnLine==0),"금례 수색 기록이 올바르지 않습니다.");
            if(s.Version>=20)Require((!s.MissingPerson.RouteKnown||s.MissingPerson.Discussed)&&(!s.MissingPerson.MetMiran||s.MissingPerson.RouteKnown)&&s.MissingPerson.MiranQuestions>=0&&s.MissingPerson.MiranQuestions<=3&&(s.MissingPerson.MiranQuestions==0||s.MissingPerson.MetMiran),"세탁소 방문 기록이 올바르지 않습니다.");
            if(s.Version>=21)Require((!s.MissingPerson.MetGeumrye||(s.MissingPerson.MetMiran&&(s.MissingPerson.MiranQuestions&1)!=0))&&(!s.MissingPerson.ReunionComplete||s.MissingPerson.MetGeumrye),"금례 재회 기록이 올바르지 않습니다.");
            if(s.Version>=22){var news=s.MissingPerson;bool pending=!string.IsNullOrEmpty(news.NewsMessengerId);Require(news.NewsLine>=0&&news.NewsLine<3&&(pending||news.NewsLine==0)&&(!news.NewsShared||news.MetGeumrye)&&(!pending||news.MetGeumrye&&!news.NewsShared&&!news.ReunionComplete&&news.NewsMessengerId!="medic"&&s.Members.Any(m=>m.Id==news.NewsMessengerId)),"금례 소식 전달 기록이 올바르지 않습니다.");}
            if(s.Version>=15)ValidateNpcStory(s.Doyun);
            if(s.Version>=12)Require(s.Opening!=null&&s.Opening.NightChoice>=0&&s.Opening.NightChoice<=2&&(!s.Opening.NightHeard||(s.Opening.Enabled&&s.Opening.Complete))&&(s.Opening.NightChoice==0||s.Opening.NightHeard),"밤 사건 기록이 올바르지 않습니다.");
            if(s.Version>=11)Require(s.Opening!=null&&(!s.Opening.ClueRead||s.Opening.FirstReturn)&&(!s.Opening.SurveyReturned||s.Opening.ClueRead)&&(!s.Opening.Complete||s.Opening.SurveyReturned),"첫 생활 진행 기록이 올바르지 않습니다.");
            if(s.Version>=10)Require(s.Development!=null&&s.Development.Warehouse>=0&&s.Development.Warehouse<=2,"시설 복구 상태가 올바르지 않습니다."); if(s.Version>=9)Require(s.IntroductionStep>=0&&s.IntroductionStep<=10,"도입 진행 상태가 올바르지 않습니다.");
            Require(DateTime.TryParse(s.SavedUtc,out _), "저장 날짜가 올바르지 않습니다.");
            Require(HomeIds.Contains(s.HomeId) && s.Day>=1 && s.Day<=100000 && s.Minute>=0 && s.Minute<1440 && s.Ammo>=0 && s.Ammo<=1000000, "시간 또는 정착지 정보가 올바르지 않습니다.");
            Require(s.Members!=null && s.Members.Length>=2 && s.Members.Length<=100, "대원 목록이 올바르지 않습니다.");
            if(s.Version>=16)
            {
                Require(s.UnlockedCharacters!=null&&s.UnlockedCharacters.Distinct().Count()==s.UnlockedCharacters.Length&&
                    s.UnlockedCharacters.All(id=>catalog.Roster.Candidates.Any(c=>c.Id==id)),"동료 해금 기록이 올바르지 않습니다.");
                Require(s.Members.All(m=>m!=null&&s.UnlockedCharacters.Contains(m.Id))&&
                    catalog.Roster.Candidates.Where(c=>c.AvailableAtStart).All(c=>s.UnlockedCharacters.Contains(c.Id)),"함께하는 동료의 해금 기록이 없습니다.");
                Require(string.IsNullOrEmpty(s.Visitor?.RecruitId)||s.UnlockedCharacters.Contains(s.Visitor.RecruitId),"방문한 동료의 해금 기록이 없습니다.");
            }
            var itemIds = new HashSet<string>(catalog.InventoryPanel.Items.Select(i=>i.Id));
            var materialIds = new HashSet<string>(catalog.CraftPanel.Materials.Select(i=>i.Id).Concat(catalog.CraftPanel.Recipes.Where(r=>r.Category==0).Select(r=>r.ProducedId)));
            // Old payloads still must contain every old good; only the newly introduced IDs may be absent.
            if(s.Version>=4)catalog.VisitorPanel.ValidateSaved(s.Version<17?WithEverydayVisitorStock(s.Visitor,catalog):s.Visitor,s.Day,s.Minute);
            var people = new HashSet<string>();
            foreach (var p in s.Members)
            {
                Require(p!=null && !string.IsNullOrWhiteSpace(p.Name) && catalog.Roster.Candidates.Any(c=>c.Id==p.Id) && people.Add(p.Id), "알 수 없거나 중복된 대원입니다.");
                Require(p.Maximum>0 && p.Maximum<=10000 && p.Health>=0 && p.Health<=p.Maximum && p.Capacity>0 && p.Capacity<=1000 && p.Aim>=-100 && p.Aim<=100, "대원 상태가 올바르지 않습니다.");
                CheckCounts(p.Bag,itemIds); Require(p.Bag.Count(i=>i.Count>0)<=p.Capacity, "가방 용량을 초과한 저장입니다.");
            }
            if(s.Version>=7)catalog.VisitorPanel.ValidateRecruitment(s.Visitor,s.Members,catalog.Roster);
            Require(s.Members.Sum(p=>p.Bag.Where(i=>i.Id=="ammo").Sum(i=>(long)i.Count))<=s.Ammo, "공용 물자와 가방 수량이 일치하지 않습니다.");
            CheckCounts(s.Materials,materialIds);
            Require(catalog.CraftPanel.Materials.Where(m=>(s.Version>=2 || !FoodIds.Contains(m.Id)) && (s.Version>=3 || !LifeIds.Contains(m.Id)) && (s.Version>=17 || !EverydayIds.Contains(m.Id))).All(m=>s.Materials.Any(x=>x.Id==m.Id)), "기본 물자 정보가 누락되었습니다.");
            Require(s.Rest!=null && s.Craft!=null && s.Rest.Length+s.Craft.Length<=people.Count, "작업 정보가 올바르지 않습니다.");
            Require(!s.SideRoomReady||s.SideRoomConnected,"거주 준비된 방의 연결 정보가 없습니다.");
            var assigned=new HashSet<string>();
            foreach(var r in s.Rest) Require(r!=null && people.Contains(r.MemberId) && assigned.Add(r.MemberId) && s.Members.First(p=>p.Id==r.MemberId).Health>0 && r.Minutes>0 && r.Minutes<=1000000 && r.Recovery>0 && r.Recovery<=10000 && (r.Name=="짧은 휴식" || r.Name=="수면"), "휴식 예약이 올바르지 않습니다.");
            var facilities=new HashSet<string>();
            foreach(var r in s.Craft)
            {
                Require(r!=null && people.Contains(r.MemberId) && assigned.Add(r.MemberId) && s.Members.First(p=>p.Id==r.MemberId).Health>0 && r.Minutes>0 && r.Minutes<=1000000 && r.Quantity>0 && r.Quantity<=99, "제작 예약이 올바르지 않습니다.");
                var recipe=catalog.CraftPanel.Recipes.FirstOrDefault(x=>x.Id==r.RecipeId); Require(recipe!=null, "제작법이 변경되어 불러올 수 없습니다.");
                if(!string.IsNullOrEmpty(recipe.UnlockProject))Require(s.Development!=null&&DevelopmentDone(s.Development,recipe.UnlockProject),"해금되지 않은 제작 예약입니다.");
                if(s.Version>=10&&recipe.Category!=0)Require(!DevelopmentDone(s.Development,r.RecipeId),"완료된 시설 작업이 중복되었습니다."); if(recipe.Category!=0) Require(r.Quantity==1 && facilities.Add(r.RecipeId) && !(r.RecipeId=="repair-bed" && s.BedRepaired) && !(r.RecipeId=="upgrade-bench" && s.BenchImproved) && !(r.RecipeId=="upgrade-cooker" && s.CookerImproved) && !(r.RecipeId=="open-side-room" && s.SideRoomConnected) && !(r.RecipeId=="prepare-side-room" && (s.SideRoomReady||!s.SideRoomConnected)), "시설 작업이 중복되었습니다.");
                CheckCounts(r.Reserved,materialIds);
                var expected=recipe.Costs.GroupBy(x=>x.MaterialId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Count)*r.Quantity);
                Require(r.Reserved.Length==expected.Count && r.Reserved.All(x=>expected.TryGetValue(x.Id,out int n)&&n==x.Count), "예약 재료가 제작법과 일치하지 않습니다.");
            }
            var cooking=s.Version==1?Array.Empty<SavedCooking>():s.Cooking;
            Require(cooking!=null && cooking.Length+s.Craft.Length+s.Rest.Length<=people.Count,"조리 예약이 올바르지 않습니다.");
            foreach(var r in cooking)
            {
                Require(r!=null && people.Contains(r.MemberId) && assigned.Add(r.MemberId) && s.Members.First(p=>p.Id==r.MemberId).Health>0 && r.Minutes>0 && r.Minutes<=1000000 && r.Quantity>0 && r.Quantity<=99,"조리 담당자 또는 시간이 올바르지 않습니다.");
                var recipe=catalog.CookingPanel.Meals.FirstOrDefault(m=>m.Id==r.RecipeId);
                Require(recipe!=null && r.OutputId==recipe.OutputId && r.OutputCount==recipe.Servings*r.Quantity && materialIds.Contains(r.OutputId),"조리 결과가 올바르지 않습니다.");
                if(!string.IsNullOrEmpty(recipe.UnlockProject))Require(s.Development!=null&&DevelopmentDone(s.Development,recipe.UnlockProject),"해금되지 않은 조리 예약입니다.");
                CheckCounts(r.Reserved,materialIds);
                var expected=recipe.Costs.GroupBy(x=>x.MaterialId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Count)*r.Quantity);
                Require(r.Reserved.Length==expected.Count && r.Reserved.All(x=>expected.TryGetValue(x.Id,out int n)&&n==x.Count),"조리 예약 재료가 일치하지 않습니다.");
            }
            foreach(var id in materialIds) Require(s.Craft.Sum(r=>r.Reserved.Where(x=>x.Id==id).Sum(x=>(long)x.Count))+cooking.Sum(r=>r.Reserved.Where(x=>x.Id==id).Sum(x=>(long)x.Count))<=(s.Materials.FirstOrDefault(x=>x.Id==id)?.Count??0), "예약 재료가 재고보다 많습니다.");
            Require(!s.StorageVisited||s.StorageUnlocked,"보관실 방문 기록이 올바르지 않습니다.");
            Require(s.Inspected!=null && s.Inspected.Distinct().Count()==s.Inspected.Length && s.Inspected.All(SiteIds.Contains), "장소 기록이 올바르지 않습니다.");
            Require(!s.Inspected.Any(id=>id.StartsWith("mall.storage."))||(s.Version>=8&&s.StorageUnlocked&&s.StorageVisited),"보관실 조사 기록이 올바르지 않습니다.");
            Require(s.Searches!=null && s.Searches.Length<=catalog.ArrivalPanel.Loot.Sites.Length, "수색 기록이 올바르지 않습니다.");
            var sites=new HashSet<string>();
            foreach(var r in s.Searches)
            {
                Require(r!=null && SiteIds.Contains(r.Id) && Array.IndexOf(SiteIds,r.Id)<catalog.ArrivalPanel.Loot.Sites.Length && catalog.ArrivalPanel.Loot.Sites[Array.IndexOf(SiteIds,r.Id)].Room>=0 && sites.Add(r.Id), "알 수 없는 수색 대상입니다.");
                Require(r.Pace>=0 && r.Pace<=2 && r.Duty>=0 && r.Duty<=2 && r.Bonus>=0 && r.Bonus<=100 && r.Progress>=0 && r.Required>=0 && r.Required<=3 && r.Progress<=r.Required, "수색 진행도가 올바르지 않습니다.");
                // 2026-09-25: no pace rule (Required comes from the object's turns and 함께); a started search needs at least one turn.
                Require(r.Progress==0 ? !r.Complete && r.Required==0 : r.Opened && r.Required>=1 && r.Complete==(r.Progress==r.Required), "수색 완료 상태가 올바르지 않습니다.");
                Require(!r.Id.StartsWith("mall.storage.")||(s.Version>=8&&s.StorageUnlocked&&s.StorageVisited),"보관실 해금 기록이 없습니다.");
                CheckCounts(r.Loot,itemIds); Require(r.Complete || r.Loot.All(x=>x.Count==0), "미완료 수색의 물자 정보가 올바르지 않습니다.");
            }
            if(s.Version>=13){
                // Static ids only; the gauge bound is loose (GaugeSize is a prefab rule and FieldSiteState clamps).
                Require(s.SiteBoards!=null&&s.SiteBoards.Length<=SiteBoardIds.Length&&s.SiteBoards.All(b=>b!=null)&&s.SiteBoards.Select(b=>b.Id).Distinct().Count()==s.SiteBoards.Length,"장소 상태 기록이 올바르지 않습니다.");
                foreach(var b in s.SiteBoards)Require(SiteBoardIds.Contains(b.Id)&&b.Danger>=0&&b.Danger<=3&&b.Gauge>=0&&b.Gauge<=100&&(string.IsNullOrEmpty(b.Remembered)||SiteRoomIds.Contains(b.Remembered)),"장소 상태 기록이 올바르지 않습니다.");
            }
            Require(s.Activity!=null && s.Activity.Length<=100 && s.Activity.All(x=>x!=null && x.Length<=2000), "활동 기록이 올바르지 않습니다.");
            if(s.ReturnReport!=null)
            {
                var r=s.ReturnReport;
                Require(!string.IsNullOrWhiteSpace(r.Destination) && r.Body!=null && r.Body.Length<=30000 && r.Minutes>=0 && r.Members!=null && r.Items!=null && r.Members.Length>0 && r.Members.Length<=people.Count && r.Items.Length<=itemIds.Count, "귀환 기록이 올바르지 않습니다.");
                var reportPeople=new HashSet<string>(); var reportItems=new HashSet<string>();
                // A report is a historical snapshot. v16 raises maximum health, but must not
                // rewrite the health or outcome shown in a previous expedition's report.
                foreach(var p in r.Members) Require(p!=null && people.Contains(p.Id) && reportPeople.Add(p.Id) && p.Maximum>0 &&
                    (p.Maximum==s.Members.First(x=>x.Id==p.Id).Maximum || s.Version>=16&&p.Maximum<s.Members.First(x=>x.Id==p.Id).Maximum) &&
                    p.Before>=0 && p.Before<=p.Maximum && p.After>=0 && p.After<=p.Maximum, "귀환 대원 기록이 올바르지 않습니다.");
                foreach(var i in r.Items) Require(i!=null && itemIds.Contains(i.Id) && reportItems.Add(i.Id) && i.Before>=0 && i.Before<=1000000 && i.After>=0 && i.After<=1000000, "귀환 물자 기록이 올바르지 않습니다.");
            }
        }
        static void ValidateNpcStory(SavedNpcStory story)
        {
            const string invalid="탐험 인물 기록이 올바르지 않습니다.";
            Require(story!=null,invalid);
            Require(story.Stage>=0&&story.Stage<=3&&story.Choice>=0&&story.Choice<=2&&story.Page>=0&&story.Page<=3,invalid);
            Require(story.Stage==0?story.Page==0:story.Stage==1?story.Page<=1:story.Page==story.Stage,invalid);
            Require(!story.PendingDialogue||(story.Stage==1&&story.Page==0),invalid);
            Require(story.VisitCount>=0&&story.VisitCount<=1000000&&story.MetVisit>=0&&story.MetVisit<=story.VisitCount,invalid);
            Require((story.Choice!=0)==(story.Stage==3),invalid);
            Require(story.Stage==0?story.MetVisit==0:story.MetVisit>=2,invalid);
            Require(!story.Returned||story.Stage==3,invalid);
            Require(!story.Reunited||story.Returned,invalid);
        }
        static void ValidateTutorialNarrative(SavedTutorialNarrative story,SettlementController catalog)
        {
            const string invalid="첫 정착 대화 기록이 올바르지 않습니다.";
            Require(story!=null,invalid);
            Require(story.SeenMask>=0&&story.SeenMask<=SavedTutorialNarrative.AllSeen,invalid);
            Require(story.PendingBeat>=-1&&story.PendingBeat<SavedTutorialNarrative.BeatCount&&story.LineIndex>=0&&story.LineIndex<=7,invalid);
            Require(story.PendingBeat<0?story.LineIndex==0:(story.SeenMask&(1<<story.PendingBeat))==0,invalid);
            var narrative=catalog.GetComponent<SettlementTutorialNarrative>();
            // The eight-line limit supports catalogs predating the narrative component.
            // A configured catalog must also contain the exact pending line; do not silently clamp it.
            if(story.PendingBeat>=0&&narrative)
            {
                Require(narrative.Beats!=null&&story.PendingBeat<narrative.Beats.Length,invalid);
                var beat=narrative.Beats[story.PendingBeat];
                Require(beat!=null&&beat.Lines!=null&&story.LineIndex<beat.Lines.Length,invalid);
            }
        }
        public static void Prepare(CampaignSaveData data, SettlementController catalog)
        {
            Upgrade(data,catalog);
            Validate(data,catalog);
            var campaign=CampaignState.RestoreSettled(data);
            int traitIndex=0;
            foreach(var person in campaign.Party)
            {
                string id=data.Members[traitIndex++].Id;
                catalog.Roster.Candidates.First(p=>p.Id==id).ApplyTraits(person);
            }
            PartySelectionSession.Clear();
            PartySelectionSession.Selected.AddRange(data.Members.Select(p=>p.Id));
            PartySelectionSession.Pending=campaign; Pending=data;
        }
        public static void ClearPending() { Pending=null; }
        public static void ApplyPending(SettlementController c)
        {
            var data=Pending; Pending=null; if(data==null)return;
            c.CraftPanel.RestoreSaved(data); c.WorkPanel.RestoreSaved(data); c.CookingPanel.RestoreSaved(data); c.InventoryPanel.RestoreSaved(data);
            c.ArrivalPanel.Loot.RestoreSaved(data); c.ArrivalPanel.Rooms.RestoreSaved(data); c.ReturnPanel.RestoreSaved(data);
            if(c.ArrivalPanel.Threat)c.ArrivalPanel.Threat.RestoreSaved(data.SiteBoards.FirstOrDefault(b=>b.Id==SiteBoardIds[0]));
            var story=c.ArrivalPanel.GetComponent<ExpeditionNpcStory>();if(story)story.Restore(data.Doyun??new SavedNpcStory());
            c.ActivityLog.Clear(); c.ActivityLog.AddRange(data.Activity);
            c.VisitorPanel.Restore(data.Visitor);
            if(c.Opening)c.Opening.State=JsonUtility.FromJson<SavedOpeningChapter>(JsonUtility.ToJson(data.Opening));
            if(c.Introduction)c.Introduction.Restore(data.IntroductionStep);if(c.Development)c.Development.State=JsonUtility.FromJson<SavedDevelopment>(JsonUtility.ToJson(data.Development));
            c.RestoreCharacterUnlocks(data.UnlockedCharacters);
            var narrative=c.GetComponent<SettlementTutorialNarrative>();if(narrative)narrative.Restore(data.TutorialNarrative);
            if(c.MissingPerson)c.MissingPerson.Restore(data.MissingPerson);
        }
        public static Adventurer Resolve(SettlementController c,string id) => c.Campaign.Party.ElementAt(PartySelectionSession.Selected.IndexOf(id));
    }
    public sealed partial class SettlementInventoryPanel
    {
        public void RestoreSaved(CampaignSaveData data) { bags.Clear(); foreach(var p in data.Members) bags[CampaignPersistence.Resolve(owner,p.Id)]=p.Bag.ToDictionary(x=>x.Id,x=>x.Count); }
    }
    public sealed partial class SettlementWorkPanel
    {
        public void RestoreSaved(CampaignSaveData data) { orders.Clear(); foreach(var r in data.Rest) orders.Add(new Order { Member=CampaignPersistence.Resolve(owner,r.MemberId), Name=r.Name, Minutes=r.Minutes, Recovery=r.Recovery }); }
    }
    public sealed partial class SettlementCraftPanel
    {
        public void RestoreSaved(CampaignSaveData data)
        {
            foreach(var m in data.Materials) { var material=Materials.FirstOrDefault(x=>x.Id==m.Id); if(material==null) { var recipe=Recipes.First(x=>x.ProducedId==m.Id); material=new Material { Id=m.Id, Name=recipe.Name, Icon=recipe.Icon }; Materials=Materials.Concat(new[]{material}).ToArray(); } material.Initial=m.Count; }
            BedRepaired=data.BedRepaired; BenchImproved=data.BenchImproved; CookerImproved=data.CookerImproved; SideRoomConnected=data.SideRoomConnected; SideRoomReady=data.SideRoomReady; orders.Clear();
            foreach(var r in data.Craft) orders.Add(new Order { Member=CampaignPersistence.Resolve(owner,r.MemberId), Recipe=Recipes.First(x=>x.Id==r.RecipeId), Quantity=r.Quantity, Minutes=r.Minutes, Reserved=r.Reserved.ToDictionary(x=>x.Id,x=>x.Count) });
        }
    }
    public sealed partial class ExpeditionLootPanel
    {
        public SavedSearch[] ExportSearches() => states.OrderBy(x=>x.Key).Select(x=>new SavedSearch { Id=CampaignPersistence.SiteIds[x.Key], Progress=x.Value.Progress, Required=x.Value.Required, Pace=x.Value.Pace, Bonus=x.Value.Bonus, Duty=x.Value.Duty, Complete=x.Value.Complete, Opened=x.Value.Opened, Loot=CampaignPersistence.Counts(x.Value.Loot) }).ToArray();
        public void RestoreSaved(CampaignSaveData data) { states.Clear(); foreach(var r in data.Searches) { var s=State(Array.IndexOf(CampaignPersistence.SiteIds,r.Id)); s.Progress=r.Progress;s.Required=r.Required;s.Pace=r.Pace;s.Bonus=r.Bonus;s.Duty=r.Duty;s.Complete=r.Complete;s.Opened=r.Opened;foreach(var item in r.Loot)s.Loot[item.Id]=item.Count; } }
    }
    public sealed partial class ExpeditionRoomNavigation
    {
        public void RestoreSaved(CampaignSaveData data) { inspected.Clear();foreach(var id in data.Inspected)inspected.Add(Array.IndexOf(CampaignPersistence.SiteIds,id));CorridorVisited=data.CorridorVisited;StorageUnlocked=data.StorageUnlocked;StorageVisited=data.StorageVisited; }
    }
    public sealed partial class ExpeditionSiteThreat
    {
        // What the site keeps between visits (End() already folded the last visit in); a visit in progress is never saved.
        public SavedSiteBoard ExportSaved() => !Visited ? null : new SavedSiteBoard { Id=CampaignPersistence.SiteBoardIds[0], Danger=carried.danger, Gauge=carried.gauge,
            Remembered=carried.remembered>=0&&carried.remembered<CampaignPersistence.SiteRoomIds.Length?CampaignPersistence.SiteRoomIds[carried.remembered]:"", IntroShown=introShown, ResidentLessonShown=residentLessonShown };
        public void RestoreSaved(SavedSiteBoard b)
        {
            State=null; introPending=false; Visited=b!=null; introShown=b!=null&&b.IntroShown; residentLessonShown=b!=null&&b.ResidentLessonShown;
            carried=b==null?(0,0,FieldSiteState.Nowhere,0):(b.Danger,b.Gauge,string.IsNullOrEmpty(b.Remembered)?FieldSiteState.Nowhere:Array.IndexOf(CampaignPersistence.SiteRoomIds,b.Remembered),0);
        }
    }
    public sealed partial class ExpeditionReturnPanel
    {
        public SavedReport ExportReport()
        {
            if(!HasReport)return null;
            return new SavedReport { Destination=destination, Body=report, Minutes=elapsedMinutes,
                Members=memberResults.Select((r,i)=>new SavedReportMember { Id=CampaignPersistence.MemberId(owner,party[i]), Before=r.Before, After=r.After, Maximum=r.Maximum }).ToArray(),
                Items=itemResults.Select(r=>new SavedReportItem { Id=r.Id, Before=r.Before, After=r.After }).ToArray() };
        }
        public void RestoreSaved(CampaignSaveData data)
        {
            memberResults.Clear();itemResults.Clear();HasReport=data.ReturnReport!=null;if(!HasReport)return;
            var r=data.ReturnReport;destination=r.Destination;report=r.Body;elapsedMinutes=r.Minutes;party=r.Members.Select(m=>CampaignPersistence.Resolve(owner,m.Id)).ToArray();
            foreach(var m in r.Members)memberResults.Add(new MemberResult { Name=CampaignPersistence.Resolve(owner,m.Id).Name, Portrait=owner.Roster.Candidates.First(x=>x.Id==m.Id).Portrait, Before=m.Before, After=m.After, Maximum=m.Maximum });
            foreach(var m in r.Items){var item=owner.InventoryPanel.Items.First(x=>x.Id==m.Id);itemResults.Add(new ItemResult { Id=m.Id, Name=item.Name, Icon=item.Icon, Before=m.Before, After=m.After });}
        }
    }
}

namespace Demo5.NightRun
{
    public sealed partial class CampaignState
    {
        public static CampaignState RestoreSettled(Demo5.FrontEnd.CampaignSaveData s)
        {
            var people=s.Members.Select(p=>new Adventurer(p.Name,p.Role,p.Description,p.Maximum,p.Aim,p.Capacity) { Health=p.Health }).ToArray();
            var c=new CampaignState(people,true);c.Chosen.AddRange(Enumerable.Range(0,people.Length));c.Home=c.Sites[Array.IndexOf(Demo5.FrontEnd.CampaignPersistence.HomeIds,s.HomeId)];
            c.Day=s.Day;c.MinuteOfDay=s.Minute;c.Ammo=s.Ammo;c.Stage=JourneyStage.Settlement;c.Message="저장한 생활을 이어갑니다.";return c;
        }
    }
}
