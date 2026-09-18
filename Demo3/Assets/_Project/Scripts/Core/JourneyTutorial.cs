using System;
using System.Linq;
using Live49.Chapter00;
namespace Live49.Core
{
    public sealed class TutorialStep
    {
        public string Id,Title,Detail;
        public int Chapter;
        public TutorialStep(string id,string title,string detail,int chapter){Id=id;Title=title;Detail=detail;Chapter=chapter;}
    }
    // Progress comes from gameplay milestones, never from clicking through help pages.
    public static class JourneyTutorial
    {
        public static readonly string[] Chapters={"첫 밤의 준비","물과 식량","식탁과 첫 그림","길에서 만난 사람","첫 사진과 앨범","다음 지역으로"};
        public static readonly string[] Basics={
            "대화는 화면의 계속 표기가 나타나면 이어갈 수 있어요. ESC로 멈추고, 설정에서 대사 속도를 조절할 수 있어요.",
            "캠핑카 안에서 지도 이동을 선택하면 차량이 이동해요. 밖에서 지도 이동을 선택하면 걸어가며 캠핑카는 원래 자리에 남아요. 장소를 살펴보면 다음 길이 열려요.",
            "캠핑카의 식탁에서 물과 식량을 나눈 뒤 책을 펼쳐요. 이미 챙긴 도구는 다시 구하지 않아도 돼요. 첫 그림은 다음 지역으로 가기 전에 완성해요.",
            "첫 그림 뒤 길 정보를 알아보고 부탁을 따라가요. 이야기용 가방과 도구는 일반 식량처럼 소비하지 않아요. 대화 도중 나가도 자동 저장된 대사부터 이어져요.",
            "카메라와 첫 촬영 필름을 받은 뒤 풍경을 촬영해요. 촬영과 앨범 확인은 별도 단계예요. 첫 필름은 보장되며 이후 사진은 선택 활동이에요.",
            "첫 그림·첫 사진·길 정보와 고갯길 탐색을 마친 뒤 캠핑카로 출발해요. 저녁 기록·요리·강아지 동행·골목 조우는 필수 출발 조건이 아니에요."
        };
        static TutorialStep Step(string id,string title,string detail,int chapter)=>new TutorialStep(id,title,detail,chapter);
        static bool Home(JourneyState s)=>!RegionExploration.Outside(s);
        static string HomeName(JourneyState s)=>s.location=="camper"?"출발 지점의 캠핑카":RegionExploration.ParkingName(s)+" 캠핑카";
        static TutorialStep AtHome(JourneyState s,string id,string title,string detail,int chapter)
            =>Home(s)?Step(id,title,detail,chapter):Step("return."+id,HomeName(s)+"로 돌아가기","주변 행동의 ‘캠핑카로 돌아가기’를 선택해요. 걸어온 장소로 캠핑카가 따라오지는 않아요.\n\n돌아온 뒤 · "+detail,chapter);
        static string Reachable(JourneyState s,string target)
        {
            if(RegionExploration.Discovered(s,target))return target;
            var site=RegionExploration.Find(target);
            return site?.Parents.Where(p=>!RegionExploration.Completed(s,p)).Select(p=>Reachable(s,p)).FirstOrDefault(p=>p!=null);
        }
        static TutorialStep Inspect(JourneyState s,string site,int chapter)
        {
            var points=PlaceInspection.Points(site);
            int index=Array.FindIndex(points,p=>!PlaceInspection.Checked(s,site,Array.IndexOf(points,p)));
            return Step("inspect."+site,index>=0?points[index].Label:"주변 탐색 마무리","이 장소의 두 확인 지점을 살펴보면 연결된 길이 열려요. 들르기만 하거나 이야기 물품만 챙긴 것은 탐색 완료와 달라요.",chapter);
        }
        static TutorialStep Go(JourneyState s,string target,string id,string title,string detail,int chapter)
        {
            string next=Reachable(s,target);
            if(next==null)return Step("map", "지도에서 주변 길 확인하기","아직 연결되지 않은 길의 주변 장소를 먼저 살펴봐요.",chapter);
            string name=RegionExploration.Find(next).ShortName;
            if(RegionExploration.PlayerPlace(s)!=next)
            {
                if(Home(s)&&s.fuel==0&&s.location!="camper")return Step("walk."+next,"캠핑카에서 내려 걸어가기","연료가 없어도 도보 탐색은 가능해요. 먼저 내린 뒤 지도에서 "+name+"을 선택해요.",chapter);
                return Step("go."+next,"지도에서 "+name+" 선택하기",Home(s)?"캠핑카 안에서 출발하면 차량이 이동해요. 도착하면 내려서 둘러봐요.":"밖에서 출발하면 걸어가요. 캠핑카는 "+RegionExploration.ParkingName(s)+"에 그대로 있어요.",chapter);
            }
            if(Home(s))return Step("leave."+next,"캠핑카에서 내려 "+name+" 둘러보기","주차만으로 탐색이 끝나지는 않아요. ‘캠핑카에서 내려…’를 선택해요.",chapter);
            return next!=target?Inspect(s,next,chapter):Step(id,title,detail,chapter);
        }
        public static bool[] Completed(JourneyState s)=>new[]{s!=null&&s.day>0,s!=null&&RegionExploration.Completed(s,"L1"),s!=null&&s.Has(RegionTravel.Drawing),s!=null&&s.Has("camera_received"),s!=null&&s.Has(RegionTravel.Photo),s!=null&&s.Has(RegionTravel.NextRegion+".opened")};
        public static TutorialStep Current(JourneyState s)
        {
            if(s==null||s.day==0)return Step("opening","첫 밤의 준비 이어가기","대화와 주변 인물·스케치북·라디오를 따라 준비해요. 밤의 기록을 남기면 첫날 아침이 시작돼요.\n\nESC에서 설정·저장·안내를 다시 볼 수 있어요.",0);
            if(SearchSession.Active(s))return Step("search","진행 중인 탐색 마치기","원형 판정은 예고 뒤 잠깐 나타나요. 가운데 판정 버튼이나 스페이스로 확인해요. ESC로 멈출 수 있고, 중간에 챙긴 물자는 그만두어도 남아요. 불러오면 진행 중 탐색은 종료되고 확보한 물자만 유지돼요.",1);
            if(BanditEncounter.Active(s))return Step("bandit",s.bandit.stage==0?"골목에서 지나갈 방법 선택하기":s.bandit.stage==1?"골목 조우 결과 확인하기":"캠핑카 귀환 확인 마치기","물자가 없어도 시간을 써서 우회할 수 있어요. 선택과 결과는 자동 저장돼요. 사건을 마치면 원래 목표로 돌아가요.",Math.Max(1,Array.FindIndex(Completed(s),done=>!done)));
            if(!string.IsNullOrEmpty(s.weekEvent))return Step("story."+s.weekEvent,"진행 중인 이야기 이어보기","지금 대사를 확인하면 다음 단계로 이어져요. 읽던 위치는 자동 저장돼요.",s.Has("camera_received")?4:s.Has(RegionTravel.Drawing)?3:2);
            if(CampLife.Cooking(s))return Step("cooking","주방에서 멈춘 조리 이어가기","요리 창을 다시 열고 ‘이어서 조리’를 선택해요. 조리가 끝나면 식사가 가방에 들어가요.",2);
            if(CampLife.Heating(s))return Step("heating","식탁에서 간편식 가열 마치기","생활의 식탁 탭을 열어 가열을 이어가요. 창을 닫으면 가열도 멈춰요.",2);
            if(Home(s)&&s.location=="camper"&&s.fuel==0)return Step("origin.fuel","출발 지점에서 비상 연료 찾기","‘식탁 · 설비 · 오늘의 한 장’ → 전력에서 ‘출발 지점 주변에서 비상 연료 찾기’를 선택해요. 60분을 써 연료 2를 확보한 뒤 지도에서 출발해요.",1);
            if(!RegionExploration.Completed(s,"L1"))return Go(s,"L1","supplies",s.Has("water_checked")?"식품 선반에서 식량 챙기기":"냉장고에서 생수 챙기기","물과 식량을 각각 살펴보고 회수해요. 두 가지를 마치면 주유소와 잡화점으로 가는 길이 열려요.",1);
            if(!s.Has("first_meal_shared")&&!s.Has("drawing_requested")&&!s.Has(RegionTravel.Drawing))
            {
                if(s.Count("water")==0||(s.Count("packaged_food")==0&&s.Count("meal")==0))return Go(s,"L1","extra.food","주변 물자에서 식수·식량 추가 수색","‘주변 물자와 생활 흔적’에서 ‘남은 식수와 식량 찾기’를 선택해요. 첫 식사를 위한 최소 물자는 다시 확보할 수 있어요.",2);
                return AtHome(s,"meal","식탁에서 물과 식량 나누기","‘식탁 · 설비 · 오늘의 한 장’ → 식탁에서 식사를 선택해요. 생수 1개와 식사 또는 포장 식량 1개를 사용해요.",2);
            }
            if(!s.Has("drawing_requested")&&!s.Has(RegionTravel.Drawing))return AtHome(s,"book","소이와 책 펼쳐 보기","캠핑카의 ‘소이와 책 펼쳐 보기’를 선택해 첫 그림 준비를 시작해요.",2);
            if(!s.Has(RegionTravel.Drawing))
            {
                if(!FirstWeekStory.Pencils(s))return Go(s,"L3","pencils","진열대의 색연필 챙기기","주변 행동에서 ‘진열대의 색연필 챙기기’를 선택해요. 두 탐색 지점 확인과 도구 회수는 별도 행동이에요.",2);
                return AtHome(s,s.Has("pencils_delivered")?"draw":"deliver",s.Has("pencils_delivered")?"첫 그림을 완성해 함께 보기":"책 곁에 색연필 꺼내 놓기","캠핑카의 그림 행동을 선택하고 마지막 반응까지 확인해요. 이미 가진 색연필은 그대로 사용해요.",2);
            }
            if(!s.Has("camera_received"))
            {
                if(!s.Has("sejin_met"))return Go(s,"L2","meet","차를 손보는 사람에게 길 묻기","주유소에서 길을 물으며 이야기를 이어가요. 연료 보충을 먼저 할 필요는 없어요.",3);
                if(!s.Has("toolbag_requested"))return Go(s,"L2","accept","공구 가방 부탁 확인하기","길을 알려 준 사람의 부탁을 확인하면 가방이 있는 수리점의 위치를 알 수 있어요.",3);
                if(!s.Has("toolbag_collected"))return Go(s,"L4","bag","부탁받은 공구 가방 가져가기","작업대 아래 가방을 회수해요. 주변 두 곳 살펴보기는 지도 해금을 위한 별도 탐색이에요.",3);
                return Go(s,"L2","handover","공구 가방 전달하기","부탁한 사람에게 가방을 전하고 전달 장면을 끝까지 확인해요.",3);
            }
            if(!s.Has("first_photo_taken"))return Go(s,"L6","photo","강변에서 첫 사진 찍기","첫 촬영용 필름은 보장돼요. 촬영 뒤 앨범 확인까지 이어가요.",4);
            if(!s.Has(RegionTravel.Photo))return Home(s)||RegionExploration.PlayerPlace(s)=="L6"?Step("album","첫 사진을 앨범에서 확인하기","사진 촬영만으로 안내가 끝나지는 않아요. ‘첫 사진을 앨범에서 확인하기’를 선택해요.",4):AtHome(s,"album","첫 사진을 앨범에서 확인하기","캠핑카의 앨범 행동을 선택해요. 사진과 필름이 다시 소모되지는 않아요.",4);
            if(s.Has(RegionTravel.NextRegion+".opened"))return Step("complete","첫 여정 안내 완료","기본 탐색과 그림·사진 안내를 마쳤어요. 이후의 요리·기록·동행은 원하는 때 이어갈 수 있어요.",6);
            if(!s.Has(RegionTravel.Route))return Go(s,"L2","route","주유소에서 다음 길 정보 확인","길을 알려 주는 이야기를 끝까지 확인해요.",5);
            if(!RegionExploration.Completed(s,"L7"))
            {
                var inspect=Inspect(s,"L7",5);
                return Go(s,"L7",inspect.Id,inspect.Title,inspect.Detail,5);
            }
            if(s.fuel==0)return Go(s,"L2","fuel","주변 물자에서 비상 연료 확보","‘주변 물자와 생활 흔적’ → ‘도보로 비상 연료 확보’를 선택해요. 확보한 뒤 실제 주차한 캠핑카로 돌아와요.",5);
            if(!Home(s))return AtHome(s,"depart","캠핑카로 돌아와 출발 준비","캠핑카를 고갯길로 옮긴 뒤 지역 이동을 선택해요.",5);
            if(s.location!="L7")return Step("park.gate","캠핑카를 고갯길로 옮기기","캠핑카 안에서 지도 → 고갯길을 선택해 차량으로 이동해요. 도보 도착은 출발 주차 조건을 채우지 않아요.",5);
            return Step("depart","지도에서 다음 지역으로 출발하기","지도 상단의 지역 선택에서 ‘고개 너머’를 골라요. 차량 이동에 연료 1과 40분이 필요해요.",5);
        }
    }
}
