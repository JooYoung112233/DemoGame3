using System;
using System.Globalization;
using System.IO;
using System.Linq;
namespace Live49.Core
{
    public sealed class SaveSlotInfo
    {
        public int Index;
        public string Path,Error;
        public JourneyState State,Backup;
        public bool Exists;
        public bool Readable=>State!=null;
        public bool Recoverable=>State==null&&Backup!=null;
        public string Name=>Index==0?"자동 저장":"저장 슬롯 "+Index.ToString("00");
        public DateTimeOffset SavedAt=>SaveSlots.Timestamp(State??Backup);
    }
    public static class SaveSlots
    {
        public const int ManualCount=3;
        public static string PathFor(int index)
        {
            if(index<0||index>ManualCount)throw new ArgumentOutOfRangeException(nameof(index));
            return index==0?SaveSystem.SlotPath:Path.Combine(Path.GetDirectoryName(SaveSystem.SlotPath),Path.GetFileNameWithoutExtension(SaveSystem.SlotPath)+"-manual-"+index+".json");
        }
        public static SaveSlotInfo Read(int index)
        {
            var info=new SaveSlotInfo{Index=index,Path=PathFor(index)};info.Exists=File.Exists(info.Path);
            SaveSystem.TryRead(info.Path,out info.State,out info.Error);
            if(!info.Readable)SaveSystem.TryRead(info.Path+".bak",out info.Backup,out _);
            return info;
        }
        public static SaveSlotInfo[] All()=>Enumerable.Range(0,ManualCount+1).Select(Read).ToArray();
        public static SaveSlotInfo Latest()=>All().Where(s=>s.Readable).OrderByDescending(s=>s.SavedAt).ThenBy(s=>s.Index).FirstOrDefault();
        public static bool Save(int index,out string error)
        {
            if(index<=0||index>ManualCount){error="자동 저장은 진행 중에 기록돼요. 수동 슬롯을 선택해주세요.";return false;}
            if(!SaveSystem.CanSave){error="대화나 화면 이동을 마친 뒤 저장할 수 있어요.";return false;}
            return SaveSystem.Write(PathFor(index),SaveSystem.Current,out error);
        }
        public static DateTimeOffset Timestamp(JourneyState s)=>s!=null&&DateTimeOffset.TryParse(s.savedUtc,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var t)?t:DateTimeOffset.MinValue;
        public static string Stamp(JourneyState s)=>Timestamp(s)==DateTimeOffset.MinValue?"저장 시각 정보 없음":Timestamp(s).ToLocalTime().ToString("yyyy.MM.dd  HH:mm:ss");
        public static string Place(JourneyState s)=>s==null?"":s.day==0?"캠핑카 · 떠나기 전":RegionExploration.Outside(s)?RegionExploration.Find(RegionExploration.PlayerPlace(s))?.ShortName??"주변 탐색":"캠핑카 · "+RegionExploration.ParkingName(s);
        public static string Progress(JourneyState s)
        {
            if(s==null)return "";
            if(!string.IsNullOrEmpty(s.weekEvent))return Chapter00.WeekScript.Find(s.weekEvent)?.title+" · 이어서 읽기";
            if(CampLife.Cooking(s))return "냄비 조리 · 중간부터 재개";
            if(CampLife.Heating(s))return "전자레인지 · 가열 중";
            return s.day==0?"출발 준비":Chapter00.FirstWeekStory.Goal(s)??"탐색과 여행 이어가기";
        }
        public static string Art(JourneyState s)
        {
            if(s==null)return null;
            if(!string.IsNullOrEmpty(s.weekEvent))
            {var e=Chapter00.WeekScript.Find(s.weekEvent);if(e!=null&&e.lines.Length>0)return e.lines[Math.Min(s.weekLine,e.lines.Length-1)].art;}
            if(RegionExploration.Outside(s))return RegionExploration.Find(RegionExploration.PlayerPlace(s))?.Art;
            return s.Has("dog_place_ready")?"life-E14-HUB-rest":s.Has("life.meal."+s.day)?"life-E03-HUB-rations-shared":"journal";
        }
    }
}
