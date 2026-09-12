using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Live49.UI
{
    // A panel in the shared HUD, not a scene. Location discovery and travel are supplied by gameplay.
    public class MapPanel : MonoBehaviour
    {
        public sealed class Place
        {
            public string Id, Name, Description, Purpose;
            public Vector2 Point;
            public bool Discovered;
            public string Distance, TravelTime;
            public string MapLabel,PreviewResource,TravelLabel,ConfirmationText;
            public bool Completed,IsNew;
        }
        static readonly Color Cream = new Color32(242, 228, 196, 255);
        static readonly Color Muted = new Color32(185, 166, 135, 255);
        static readonly Color Gold = new Color32(180, 145, 89, 255);
        static readonly Color PaperInk = new Color32(221, 221, 199, 255);
        readonly Dictionary<string, Place> _places = new Dictionary<string, Place>();
        readonly Dictionary<string, Button> _markers = new Dictionary<string, Button>();
        TMP_FontAsset _font;
        RectTransform _paper;
        MapViewport _view;
        TMP_Text _zoomLabel;
        Button _zoomIn, _zoomOut, _recenter;
        Image _preview;
        TMP_Text _previewHint;
        TMP_Text _progressLabel;
        public MapViewport View => _view;
        TMP_Text _title, _description, _purpose, _distance, _travelTime, _availability, _actionLabel, _selectedLabel;
        Button _travel, _back, _close;
        MapPaperGraphic _drawing;
        CanvasGroup _group;
        Action _onClose;
        Action<string> _travelHandler;
        Func<string, string> _travelBlockReason;
        bool _confirming, _closing;
        Coroutine _fade;
        RectTransform _currentMarker;
        TMP_Text _currentLabel, _currentName;
        RectTransform _vehicleMarker;
        TMP_Text _parkingLabel;
        Vector2 _parkingPoint;
        Action<string> _onSeen;
        Coroutine _reveal;
        TMP_Text _regionTitle;
        Button _regions;
        Action _openRegions;
        public void ConfigureDiscovery(Action<string> seen){_onSeen=seen;}
        public bool IsOpen => gameObject.activeSelf;
        public string SelectedId { get; private set; }
        public bool IsConfirming => _confirming;
        public bool IsClosing => _closing;

        public static MapPanel Create(Transform parent, TMP_FontAsset font, Action close)
        {
            var root = new GameObject("RegionMapPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rt = (RectTransform)root.transform;
            rt.SetParent(parent, false); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color32(16, 21, 21, 252);
            var panel = root.AddComponent<MapPanel>();
            panel._font = font; panel._onClose = close; panel._group = root.GetComponent<CanvasGroup>();
            panel.Build(); root.SetActive(false); return panel;
        }

        void Build()
        {
            Label(transform, "우리의 여정  /  주변 탐색", 90, 54, 1200, 28, 18, Gold);
            _regionTitle=Label(transform, "주변 지도", 85, 91, 950, 74, 44, Cream);
            _regions=Button(transform,"MapRegions","지역 선택",1340,99,280,64,()=>_openRegions?.Invoke());
            Label(transform, "길을 살피고, 다음 머무를 곳을 정해요.", 90, 163, 1000, 34, 21, Muted);
            _close = Button(transform, "CloseMap", "닫기", 1680, 99, 150, 64, () => _onClose());
            var viewport=Box(transform, "CityMapViewport", 90, 220, 1220, 792, new Color32(57,62,57,255));
            viewport.GetComponent<Image>().raycastTarget=true;
            viewport.gameObject.AddComponent<RectMask2D>();
            _paper=Rect(viewport,"CityMapContent",0,0,1220,792);
            var texture=Resources.Load<Texture2D>("Live49/Maps/city-map-soft-v2");
            if(texture!=null)
            {
                var art=Rect(_paper,"CityMapArt",0,0,1220,792).gameObject.AddComponent<RawImage>();
                art.texture=texture;art.color=new Color(.84f,.84f,.82f,1);art.raycastTarget=false;
            }
            _drawing=Rect(_paper,"CityMapRoutes",0,0,1220,792).gameObject.AddComponent<MapPaperGraphic>();
            _drawing.OverlayOnly=texture!=null;_drawing.raycastTarget=false;
            _view=viewport.gameObject.AddComponent<MapViewport>();_view.Content=_paper;
            _view.Changed=UpdateAnnotations;
            var mapHeader=Box(viewport,"MapHeader",20,20,650,42,new Color32(22,30,29,237));
            _progressLabel=Label(mapHeader,"탐색 완료  0 / 10",14,0,220,42,17,PaperInk);
            _parkingLabel=Label(mapHeader,"캠핑카",252,0,380,42,17,new Color32(151,199,216,255));
            var compass=Box(viewport,"Compass",1144,20,56,70,new Color32(22,30,29,237));
            Label(compass,"N",0,0,56,31,19,Cream,TextAlignmentOptions.Center);
            Label(compass,"↑",0,29,56,32,28,Gold,TextAlignmentOptions.Center);
            var controls=Box(viewport,"MapControls",20,718,425,54,new Color32(22,30,29,247));
            _zoomOut=Button(controls,"MapZoomOut","−",4,4,46,46,()=>_view.StepZoom(1/1.25f));
            _zoomLabel=Label(controls,"100%",54,4,78,46,18,Cream,TextAlignmentOptions.Center);
            _zoomIn=Button(controls,"MapZoomIn","+",136,4,46,46,()=>_view.StepZoom(1.25f));
            _recenter=Button(controls,"MapRecenter","현재 위치",194,4,223,46,()=>{_view.StepZoom(1.35f/_view.Zoom);_view.Focus(_drawing.Current);});
            _recenter.GetComponentInChildren<TMP_Text>().fontSize=19;
            var legend=Box(viewport,"MapLegend",680,736,520,36,new Color32(22,30,29,237));
            Box(legend,"CurrentLegend",14,13,10,10,new Color32(140,196,166,255));
            Label(legend,"탐색 위치",34,0,130,36,16,PaperInk);
            Box(legend,"VehicleLegend",181,13,10,10,new Color32(151,199,216,255));
            Label(legend,"캠핑카",201,0,130,36,16,PaperInk);
            Box(legend,"SelectedLegend",348,13,10,10,Gold);
            Label(legend,"선택 장소",368,0,148,36,16,PaperInk);
            AddCurrentMarker(MapPaperGraphic.Camp);
            _vehicleMarker=Box(_paper,"CamperPosition",0,0,32,32,new Color32(48,82,91,255));
            var vehicleIcon=Rect(_vehicleMarker,"CamperIcon",3,3,26,26).gameObject.AddComponent<HudIcon>();
            vehicleIcon.Symbol=HudIcon.Kind.Camper;vehicleIcon.color=PaperInk;vehicleIcon.raycastTarget=false;
            var card = Box(transform, "DestinationCard", 1340, 220, 490, 792, new Color32(27, 32, 30, 255));
            Box(card, "TopRule", 0, 0, 490, 2, Gold);
            _selectedLabel = Label(card, "목적지 선택", 30, 27, 430, 28, 17, Gold);
            _title = Label(card, "어디로 가볼까요?", 28, 66, 434, 74, 32, Cream);
            var frame=Box(card,"PlacePreview",30,155,430,178,new Color32(41,48,43,255));
            frame.gameObject.AddComponent<RectMask2D>();
            _preview=Box(frame,"PlaceImage",0,-32,430,242,Color.white).GetComponent<Image>();
            _previewHint=Label(frame,"지도에서 장소를 선택해주세요.",20,0,390,178,20,Muted,TextAlignmentOptions.Center);
            _description = Label(card, "확인한 장소를 선택하면\n주변 정보와 이동 준비를 볼 수 있어요.", 30, 357, 430, 83, 22, Cream);
            Box(card, "Rule", 30, 455, 430, 1, new Color(Gold.r, Gold.g, Gold.b, .3f));
            Label(card, "거리", 30, 475, 186, 27, 16, Muted);
            Label(card, "이동 시간", 266, 475, 194, 27, 16, Muted);
            _distance = Label(card, "—", 30, 513, 186, 36, 26, Cream);
            _travelTime = Label(card, "—", 266, 513, 194, 36, 26, Cream);
            _purpose = Label(card, "", 30, 575, 430, 44, 20, Gold);
            _availability = Label(card, "", 30, 633, 430, 48, 19, Muted);
            _travel = Button(card, "MapTravel", "이곳으로 출발", 30, 696, 430, 58, TravelPressed);
            _actionLabel = _travel.GetComponentInChildren<TMP_Text>();
            _back = Button(card, "MapBack", "장소 다시 고르기", 252, 22, 208, 38, BackToSelection);
            _back.GetComponentInChildren<TMP_Text>().fontSize=17;
            _back.gameObject.SetActive(false);_travel.interactable = false;
            UpdateAnnotations();
        }

        public void SetPlaces(IEnumerable<Place> places)
        {
            StopReveal();
            foreach (var marker in _markers.Values) { marker.gameObject.SetActive(false); Destroy(marker.gameObject); }
            _markers.Clear(); _places.Clear();
            foreach (var place in places)
            {
                _places.Add(place.Id, place);
                var marker = Button(_paper, "MapPlace_" + place.Id, "", place.Point.x-22, place.Point.y-65, 44, 44, () => Select(place.Id));
                marker.GetComponent<Image>().color = Color.clear;
                var stamp = Box(marker.transform, "Stamp", 0, 0, 44, 44, new Color32(39,44,39,255));
                var icon = Rect(stamp, "PlaceIcon", 9, 9, 26, 26).gameObject.AddComponent<HudIcon>();
                icon.Symbol = place.Id=="L6"||place.Id=="L7"?HudIcon.Kind.Map:HudIcon.Kind.Store;
                icon.color = Cream; icon.raycastTarget = false;
                Box(marker.transform,"PinStem",21,44,2,21,Gold);
                Box(marker.transform, "NameBacking", 53, 1, 178, 42, new Color32(28,35,32,241)).GetComponent<Image>().raycastTarget=true;
                Label(marker.transform, (place.MapLabel??place.Name)+(place.Completed?" · 완료":""), 63, 1, 158, 42, 19, PaperInk);
                var badge=Box(marker.transform,"NewDiscovery",-9,-25,62,22,Gold);
                Label(badge,"새 발견",0,0,62,22,13,new Color32(24,30,27,255),TextAlignmentOptions.Center);
                badge.gameObject.SetActive(place.IsNew);
                marker.gameObject.AddComponent<CanvasGroup>();
                marker.gameObject.SetActive(place.Discovered);
                _markers.Add(place.Id, marker);
            }
            if (SelectedId != null && (!_places.ContainsKey(SelectedId) || !_places[SelectedId].Discovered)) SelectedId = null;
            Refresh();
        }
        void AddCurrentMarker(Vector2 point)
        {
            _currentMarker=Box(_paper,"CurrentPosition",point.x-17,point.y-17,34,34,new Color32(113,167,143,255));
            Label(_currentMarker,"●",0,0,34,34,20,new Color32(23,40,35,255),TextAlignmentOptions.Center);
            Box(_currentMarker,"CurrentNameBacking",-71,46,224,55,new Color32(24,36,32,245));
            _currentLabel=Label(_currentMarker,"현재 위치",-61,46,204,25,15,new Color32(157,207,177,255),TextAlignmentOptions.Center);
            _currentName=Label(_currentMarker,"캠핑카",-61,72,204,26,18,Cream,TextAlignmentOptions.Center);
        }
        public void SetCurrentLocation(string id)
        {
            SetLocations(id,id,false);
        }
        public void SetLocations(string vehicle,string person,bool outside)
        {
            _parkingPoint=Live49.Core.RegionExploration.ParkingPoint(vehicle);
            _drawing.Current=outside&&_places.TryGetValue(person,out var place)?place.Point:_parkingPoint;
            _currentMarker.gameObject.SetActive(outside);
            _currentMarker.Find("CurrentNameBacking").gameObject.SetActive(false);
            _currentLabel.gameObject.SetActive(false);_currentName.gameObject.SetActive(false);
            _parkingLabel.text="캠핑카 · "+(Live49.Core.RegionExploration.Find(vehicle)?.ShortName??"출발 지점")+(vehicle=="camper"?"":" 앞");
            _drawing.SetVerticesDirty();UpdateAnnotations();
        }
        void UpdateAnnotations()
        {
            if(_view==null)return;float z=_view.Zoom;
            foreach(var pair in _markers)
            {
                var rt=(RectTransform)pair.Value.transform;var p=_places[pair.Key].Point;
                rt.localScale=Vector3.one/z;rt.anchoredPosition=new Vector2(p.x-22/z,-p.y+65/z);
                var screenPoint=new Vector2(p.x*z+_view.Pan.x,p.y*z-_view.Pan.y);
                var rightLabel=new UnityEngine.Rect(screenPoint.x+31,screenPoint.y-64,178,42);
                bool left=rightLabel.xMax>MapPaperGraphic.Width-12||_places.Values.Any(other=>other.Discovered&&other.Id!=pair.Key&&rightLabel.Overlaps(new UnityEngine.Rect(other.Point.x*z+_view.Pan.x-26,other.Point.y*z-_view.Pan.y-69,52,52)));
                var backing=(RectTransform)rt.Find("NameBacking");
                backing.anchoredPosition=new Vector2(left?-187:53,-1);
                var name=rt.GetComponentInChildren<TMP_Text>();
                name.rectTransform.anchoredPosition=new Vector2(left?-177:63,-1);
            }
            if(_currentMarker!=null)
            {
                _currentMarker.localScale=Vector3.one/z;
                _currentMarker.anchoredPosition=new Vector2(_drawing.Current.x-17/z,-_drawing.Current.y+17/z);
            }
            if(_vehicleMarker!=null)
            {
                _vehicleMarker.localScale=Vector3.one/z;
                _vehicleMarker.anchoredPosition=new Vector2(_parkingPoint.x-16/z,-_parkingPoint.y+16/z);
                _vehicleMarker.SetAsLastSibling();
            }
            if(_zoomLabel!=null)_zoomLabel.text=Mathf.RoundToInt(z*100)+"%";
            if(_zoomIn!=null)_zoomIn.interactable=z<2.499f;
            if(_zoomOut!=null)_zoomOut.interactable=z>1.001f;
            if(_close!=null)WireNavigation();
        }
        public void ConfigureTravel(Func<string, string> blockedReason, Action<string> handler)
        { _travelBlockReason = blockedReason; _travelHandler = handler; if (_travel != null) Refresh(); }
        public void ConfigureRegion(string region,Action open)
        {
            bool next=region==Live49.Core.RegionTravel.NextRegion;_openRegions=open;
            _regionTitle.text=next?"고개 너머 · 진입 구간":"첫 동네 · 주변 지도";
            var art=_paper.Find("CityMapArt");if(art!=null)art.gameObject.SetActive(!next);
            _drawing.GateMap=next;_drawing.OverlayOnly=!next&&art!=null;_drawing.SetVerticesDirty();
        }
        public void Discover(string id)
        {
            if (!_places.TryGetValue(id, out var place)) return;
            place.Discovered = true; _markers[id].gameObject.SetActive(true); Refresh();
        }
        public void Select(string id)
        {
            if (_closing || !_places.TryGetValue(id, out var place) || !place.Discovered) return;
            StopReveal();
            if(place.IsNew){place.IsNew=false;_markers[id].transform.Find("NewDiscovery").gameObject.SetActive(false);_onSeen?.Invoke(id);}
            SelectedId = id; _confirming = false; Refresh(); _view.Focus(_places[id].Point);
        }
        string BlockReason()
        {
            if (SelectedId == null) return "지도에서 목적지를 선택해주세요.";
            var reason = _travelBlockReason?.Invoke(SelectedId);
            if (!string.IsNullOrWhiteSpace(reason)) return reason;
            return _travelHandler == null ? "지금은 출발할 수 없어요." : null;
        }
        void Refresh()
        {
            var visible = _places.Values.Where(p => p.Discovered).ToArray();
            _drawing.Destinations = visible.Select(p => p.Point).ToArray();
            _drawing.Selected = SelectedId != null ? _places[SelectedId].Point : (Vector2?)null;
            _drawing.SetVerticesDirty();
            foreach (var pair in _markers)
                pair.Value.transform.Find("Stamp").GetComponent<Image>().color = pair.Key == SelectedId ? new Color32(153,122,64,255) : _places[pair.Key].Completed?new Color32(64,102,84,255):new Color32(39,44,39,255);
            _progressLabel.text="탐색 완료  "+_places.Values.Count(p=>p.Completed)+" / "+_places.Count;
            var preview=SelectedId!=null?_places[SelectedId].PreviewResource:null;
            _preview.sprite=!string.IsNullOrEmpty(preview)?Resources.Load<Sprite>("Live49/Stages/"+preview):null;
            _preview.gameObject.SetActive(_preview.sprite!=null);
            _previewHint.gameObject.SetActive(_preview.sprite==null);
            _previewHint.text=SelectedId==null?"지도에서 장소를 선택해주세요.":"주변 지도에서 위치를 확인해요.";
            UpdateAnnotations();
            _back.gameObject.SetActive(_confirming);
            if (SelectedId == null)
            {
                _confirming = false; _back.gameObject.SetActive(false);
                _selectedLabel.text = "목적지 선택"; _title.text = "어디로 가볼까요?";
                _description.text = "지도에서 장소를 선택하면\n확인한 정보를 볼 수 있어요.";
                _distance.text = _travelTime.text = "—"; _purpose.text = _availability.text = string.Empty;
                _actionLabel.text = "이곳으로 출발"; _travel.interactable = false; WireNavigation(); return;
            }
            var place = _places[SelectedId];
            _selectedLabel.text = _confirming ? "출발 전 확인" : place.Completed?"탐색 완료":"아직 탐색하지 않은 곳";
            _title.text = place.Name;
            _description.text = _confirming ? place.ConfirmationText??"캠핑카를 타고 이동할까요?\n출발 전까지 취소할 수 있어요." : place.Description;
            _distance.text = string.IsNullOrWhiteSpace(place.Distance) ? "미확인" : place.Distance;
            _travelTime.text = string.IsNullOrWhiteSpace(place.TravelTime) ? "미확인" : place.TravelTime;
            _purpose.text = place.Purpose;
            string blocked = BlockReason();
            _availability.text = blocked ?? "준비가 되면 출발할 수 있어요.";
            _travel.interactable = blocked == null && !_closing;
            _actionLabel.text = _confirming ? "출발하기" : place.TravelLabel??"이곳으로 출발";
            WireNavigation();
        }
        void TravelPressed()
        {
            if (_closing || SelectedId == null) return;
            if (BlockReason() != null) { _confirming = false; Refresh(); return; }
            if (!_confirming) { _confirming = true; Refresh(); EventSystem.current?.SetSelectedGameObject(_back.gameObject); return; }
            var id = SelectedId; var handler = _travelHandler;
            _travelHandler = null; // Single dispatch; gameplay supplies the next travel context.
            _confirming = false; Refresh();
            handler(id);
        }
        void WireNavigation()
        {
            var buttons = _markers.Values.Where(b => b.gameObject.activeSelf).ToList();
            if (_travel.interactable) buttons.Add(_travel);
            if (_back.gameObject.activeSelf) buttons.Add(_back);
            if (_zoomOut!=null && _zoomOut.interactable) buttons.Add(_zoomOut);
            if (_zoomIn!=null && _zoomIn.interactable) buttons.Add(_zoomIn);
            if (_recenter!=null) buttons.Add(_recenter);
            buttons.Add(_close);
            if(_regions!=null)buttons.Add(_regions);
            for (int i = 0; i < buttons.Count; i++) buttons[i].navigation = new Navigation
            { mode = Navigation.Mode.Explicit, selectOnDown = buttons[(i + 1) % buttons.Count], selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count] };
        }
        void BackToSelection() { _confirming = false; Refresh(); }
        public void Escape() { if (_closing) return; if (_confirming) BackToSelection(); else _onClose(); }
        public void Open()
        {
            gameObject.SetActive(true); transform.SetAsLastSibling(); _view.ResetView(); _closing = false; _confirming = false; Refresh(); WireNavigation();
            EventSystem.current?.SetSelectedGameObject(_markers.Values.FirstOrDefault(b => b.gameObject.activeSelf)?.gameObject ?? _close.gameObject);
            _fade = StartCoroutine(Fade(0, 1, .22f));
            _reveal=StartCoroutine(RevealNewPlaces());
        }
        void StopReveal()
        {
            if(_reveal!=null){StopCoroutine(_reveal);_reveal=null;}
            foreach(var marker in _markers.Values){var g=marker.GetComponent<CanvasGroup>();if(g!=null){g.alpha=1;g.blocksRaycasts=true;}}
        }
        IEnumerator RevealNewPlaces()
        {
            var fresh=_places.Values.Where(p=>p.Discovered&&p.IsNew).ToArray();
            foreach(var place in fresh){var g=_markers[place.Id].GetComponent<CanvasGroup>();g.alpha=0;g.blocksRaycasts=false;}
            yield return new WaitForSecondsRealtime(.35f);
            foreach(var place in fresh)
            {
                var g=_markers[place.Id].GetComponent<CanvasGroup>();
                for(float t=0;t<.4f;t+=Time.unscaledDeltaTime){g.alpha=Mathf.SmoothStep(0,1,t/.4f);yield return null;}
                g.alpha=1;g.blocksRaycasts=true;yield return new WaitForSecondsRealtime(.35f);
            }
            _reveal=null;
        }
        public void Close(Action completed)
        {
            StopReveal();
            if (_closing) return; _closing = true;
            if (_fade != null) StopCoroutine(_fade);
            StartCoroutine(CloseRoutine(completed));
        }
        IEnumerator CloseRoutine(Action completed)
        {
            yield return Fade(_group.alpha, 0, .16f);
            gameObject.SetActive(false); _closing = false; completed();
        }
        IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            { _group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds)); yield return null; }
            _group.alpha = to;
        }

        RectTransform Rect(Transform p, string name, float x, float y, float w, float h)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rt.SetParent(p, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); return rt;
        }
        RectTransform Box(Transform p, string name, float x, float y, float w, float h, Color color)
        { var rt = Rect(p, name, x, y, w, h); var image = rt.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return rt; }
        TMP_Text Label(Transform p, string text, float x, float y, float w, float h, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var textUI = Rect(p, "Label_" + text, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            textUI.font = _font; textUI.text = text; textUI.fontSize = size; textUI.enableAutoSizing=true; textUI.fontSizeMin=Mathf.Max(12,size-3);textUI.fontSizeMax=size; textUI.color = color; textUI.alignment = align; textUI.raycastTarget = false; return textUI;
        }
        Button Button(Transform p, string name, string text, float x, float y, float w, float h, Action onClick)
        {
            var rt = Box(p, name, x, y, w, h, new Color(Gold.r, Gold.g, Gold.b, .24f));
            var b = rt.gameObject.AddComponent<Button>(); b.targetGraphic = rt.GetComponent<Image>(); b.targetGraphic.raycastTarget = true;
            var colors = b.colors; colors.highlightedColor = colors.selectedColor = new Color(1.4f, 1.3f, 1.1f); colors.fadeDuration = .12f; b.colors = colors;
            b.onClick.AddListener(() => { if (!_closing) onClick(); });
            if (text.Length > 0) Label(rt, text, 8, 0, w - 16, h, 25, Cream, TextAlignmentOptions.Center);
            return b;
        }
    }
}
