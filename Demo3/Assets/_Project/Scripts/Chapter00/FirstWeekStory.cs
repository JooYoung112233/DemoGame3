using System;
using System.Linq;
using Live49.Core;
using UnityEngine;
namespace Live49.Chapter00
{
    [Serializable] public class WeekLine {public string speaker,text,art,effect;}
    [Serializable] public class WeekEvent {public string id,title;public WeekLine[] lines;}
    [Serializable] public class WeekScript
    {
        public WeekEvent[] events;
        static WeekScript _data;
        public static WeekEvent Find(string id)
        {
            if(_data==null)_data=JsonUtility.FromJson<WeekScript>(Resources.Load<TextAsset>("Live49/FirstWeek/story").text);
            return _data.events.FirstOrDefault(e=>e.id==id);
        }
    }
    public static class FirstWeekStory
    {
        public static bool Pencils(JourneyState s)=>s.Has("pencils_owned")||s.Count("colored_pencils")>0||s.Count("pencils")>0||s.Has("pencils_delivered");
        static bool At(JourneyState s,string id)=>RegionExploration.Outside(s)&&RegionExploration.PlayerPlace(s)==id;
        public static string Action(JourneyState s)
        {
            if(s.day<=0||RegionTravel.Current(s)!=RegionExploration.RegionId)return null;
            if(!string.IsNullOrEmpty(s.weekEvent))return s.weekEvent;
            return AvailableAction(s);
        }
        static string AvailableAction(JourneyState s)
        {
            if(!RegionExploration.Outside(s))
            {
                if(RegionExploration.Completed(s,"L1")&&!s.Has("drawing_requested")&&s.Has("first_meal_shared"))return "request";
                if(s.Has("drawing_requested")&&Pencils(s)&&!s.Has("pencils_delivered"))return "deliver";
                if(s.Has("pencils_delivered")&&!s.Has(RegionTravel.Drawing))return "draw";
                if(s.Has("first_photo_taken")&&!s.Has(RegionTravel.Photo))return "album";
            }
            if(At(s,"L3")&&!Pencils(s))return "pencils";
            if(At(s,"L2")&&s.Has(RegionTravel.Drawing))
            {
                if(!s.Has("sejin_met"))return "meet";
                if(!s.Has("toolbag_requested"))return "accept";
                if(s.Has("toolbag_collected")&&!s.Has("camera_received"))return "handover";
            }
            if(At(s,"L4"))
            {
                if(s.Has("toolbag_requested")&&!s.Has("toolbag_collected"))return "bag";
                if(!s.Has("toolbag_requested")&&!s.Has("toolbag_seen"))return "noticebag";
            }
            if(At(s,"L6")&&s.Has("camera_received"))
            {
                if(!s.Has("first_photo_taken"))return "shoot";
                if(!s.Has(RegionTravel.Photo))return "album";
            }
            return null;
        }
        public static string Goal(JourneyState s)
        {
            if(s.day<=0||RegionTravel.Current(s)!=RegionExploration.RegionId||!RegionExploration.Completed(s,"L1"))return null;
            if(!s.Has("drawing_requested"))return s.Has("first_meal_shared")?"캠핑카에서 소이와 책 펼쳐 보기":"캠핑카 식탁에서 물과 식량 나누기";
            if(!Pencils(s))return "잡화점에서 색연필 챙기기";
            if(!s.Has("pencils_delivered"))return "캠핑카에서 색연필 꺼내 놓기";
            if(!s.Has(RegionTravel.Drawing))return "소이와 첫 그림 완성하고 함께 보기";
            if(!s.Has("sejin_met"))return "주유소에서 길을 아는 사람 찾아보기";
            if(!s.Has("toolbag_requested"))return "주유소에서 공구 가방 부탁 확인하기";
            if(!s.Has("toolbag_collected"))return "수리점 작업대 아래의 가방 가져오기";
            if(!s.Has("camera_received"))return "주유소로 돌아가 가방 전달하기";
            if(!s.Has("first_photo_taken"))return "강변 쉼터에서 첫 사진 찍기";
            if(!s.Has(RegionTravel.Photo))return "첫 사진을 앨범에 넣고 함께 확인하기";
            return null;
        }
        public static string Label(string id)=>id=="request"?"소이와 책 펼쳐 보기":id=="pencils"?"진열대의 색연필 챙기기":id=="deliver"?"책 곁에 색연필 꺼내 놓기":id=="draw"?"소이와 첫 그림 그리기":id=="meet"?"차를 손보는 사람에게 길 묻기":id=="accept"?"세진의 공구 가방 부탁 맡기":id=="noticebag"?"작업대 아래 가방 살펴보기":id=="bag"?"부탁받은 공구 가방 가져가기":id=="handover"?"세진에게 가방 전하고 카메라 받기":id=="shoot"?"강변에서 첫 사진 찍기":"첫 사진을 앨범에서 확인하기";
        public static bool Begin(JourneyState s,string id)
        {
            if(!string.IsNullOrEmpty(s.weekEvent)||Action(s)!=id)return false;
            s.weekEvent=id;s.weekLine=0;return true;
        }
        public static void ConfirmLine(JourneyState s)
        {
            var e=WeekScript.Find(s.weekEvent);if(e==null||s.weekLine<0||s.weekLine>=e.lines.Length)return;
            string effect=e.lines[s.weekLine].effect;
            if(effect=="reveal_sejin")s.Set("name.sejin");
            if(effect=="exchange_camera"&&!s.Has("camera_exchanged"))
            {
                if(!s.Spend(("sejin_toolbag",1)))throw new InvalidOperationException("공구 가방 전달 상태를 확인해주세요.");
                if(s.Count("camera")==0)s.Add("camera","즉석카메라",1,2,"세진에게 받은 여행용 카메라.");
                if(s.Count("tutorial_film")==0)s.Add("tutorial_film","첫 촬영 필름",1,2,"첫 촬영을 위해 보관한 필름.");
                s.Set("camera_exchanged");
            }
            if(effect=="take_photo"&&!s.Has("first_photo_taken"))
            {
                if(s.Count("tutorial_film")==0)s.Add("tutorial_film","첫 촬영 필름",1,2);
                s.Spend(("tutorial_film",1));s.Add("first_travel_photo","첫 여행 사진",1,2,"강변에서 직접 찍은 첫 풍경 사진.");s.Set("first_photo_taken");s.minutes+=10;
            }
            s.weekLine++;
        }
        public static bool Finish(JourneyState s)
        {
            var e=WeekScript.Find(s.weekEvent);if(e==null||s.weekLine!=e.lines.Length)return false;
            switch(e.id)
            {
                case "request":s.Set("drawing_requested");break;
                case "pencils":if(!Pencils(s))s.Add("colored_pencils","색연필",1,2,"첫 그림을 위한 도구. 책 곁에서 계속 사용해요.");s.Set("pencils_owned");s.minutes+=5;break;
                case "deliver":s.Set("pencils_delivered");break;
                case "draw":if(!s.Has(RegionTravel.Drawing))s.Add("first_drawing","첫 바다 그림",1,2,"함께 보고 싶은 풍경을 그린 첫 그림.");s.Set(RegionTravel.Drawing);s.minutes+=20;break;
                case "meet":s.Set("sejin_met");s.Set(RegionTravel.Route);s.minutes+=10;break;
                case "accept":s.Set("toolbag_requested");Reveal(s,"L4");break;
                case "noticebag":s.Set("toolbag_seen");break;
                case "bag":if(!s.Has("toolbag_collected"))s.Add("sejin_toolbag","세진의 공구 가방",1,2,"주유소로 전할 부탁받은 가방.");s.Set("toolbag_collected");s.minutes+=5;break;
                case "handover":s.Set("camera_received");Reveal(s,"L6");break;
                case "album":s.Set(RegionTravel.Photo);break;
            }
            s.weekEvent="";s.weekLine=0;return true;
        }
        static void Reveal(JourneyState s,string id){bool fresh=!RegionExploration.Discovered(s,id);s.Set("story.discovered."+id);if(fresh)JourneyDayLog.Found(s,new[]{id});}
        public static bool Valid(JourneyState s)
        {
            if(string.IsNullOrEmpty(s.weekEvent))return s.weekLine==0;
            var e=WeekScript.Find(s.weekEvent);
            if(s.day<=0||e==null||s.weekLine<0||s.weekLine>e.lines.Length||RegionTravel.Current(s)!=RegionExploration.RegionId)return false;
            bool captured=s.weekEvent=="shoot"&&s.weekLine>=2&&At(s,"L6")&&s.Has("camera_received")&&s.Has("first_photo_taken")&&!s.Has(RegionTravel.Photo);
            bool legacyRequest=s.weekEvent=="request"&&!RegionExploration.Outside(s)&&RegionExploration.Completed(s,"L1")&&!s.Has("drawing_requested");
            if(AvailableAction(s)!=s.weekEvent&&!captured&&!legacyRequest)return false;
            if(s.weekEvent=="handover")return s.weekLine<2?!s.Has("camera_exchanged")&&s.Count("sejin_toolbag")>0:s.Has("camera_exchanged")&&s.Count("camera")>0;
            if(s.weekEvent=="shoot")return s.weekLine<2?!s.Has("first_photo_taken"):s.Has("first_photo_taken")&&s.Count("first_travel_photo")>0;
            return true;
        }
    }
}
