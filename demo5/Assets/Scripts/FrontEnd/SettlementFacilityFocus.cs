using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace Demo5.FrontEnd {
 // Baked scenery uses editable silhouette paths. Draw in the world, behind survivor pieces.
 public sealed class SettlementFacilityFocus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ICanvasRaycastFilter {
  [System.Serializable] public sealed class Path { public Vector2[] Points; }
  public GameObject Focus, Caption;
  public Button Button;
  public Path[] Paths, UnrestoredPaths; public bool UseUnrestored; bool renderedUnrestored; Path[] ActivePaths=>UseUnrestored&&UnrestoredPaths!=null&&UnrestoredPaths.Length>0?UnrestoredPaths:Paths;
  public Material ContourMaterial;
  // Optional baked distance mask, authored against the unchanged background pixels.
  public Texture2D SilhouetteMask;
  public Material SilhouetteMaterial;
  MeshRenderer maskRenderer;
  Mesh maskMesh;
  MaterialPropertyBlock maskProperties;
  readonly Vector3[] maskCorners = new Vector3[4];
  readonly Vector3[] maskVertices = new Vector3[4];
  float maskOpacity;
  bool HasMask => SilhouetteMask && SilhouetteMaterial && !UseUnrestored;
  void UpdateMask(Camera camera, RectTransform rect, bool visible) {
   if (!HasMask || !camera) { if (maskRenderer) maskRenderer.enabled = false; return; }
   if (!maskRenderer) {
    var go = new GameObject("Facility silhouette · " + FacilityName);
    maskRenderer = go.AddComponent<MeshRenderer>();
    maskRenderer.sharedMaterial = SilhouetteMaterial;
    maskRenderer.sortingOrder = 5;
    maskMesh = new Mesh { name = "Facility silhouette quad" };
    maskMesh.MarkDynamic();
    maskMesh.vertices = new Vector3[4];
    maskMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
    maskMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
    go.AddComponent<MeshFilter>().sharedMesh = maskMesh;
    maskProperties = new MaterialPropertyBlock();
   }
   maskOpacity = Mathf.MoveTowards(maskOpacity, visible ? HoverOpacity : 0, Time.unscaledDeltaTime * 8);
   maskRenderer.enabled = maskOpacity > .001f;
   if (!maskRenderer.enabled) return;
   rect.GetWorldCorners(maskCorners);
   for (int i = 0; i < 4; i++) {
    var screen = RectTransformUtility.WorldToScreenPoint(camera, maskCorners[i]);
    maskVertices[i] = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
   }
   maskMesh.vertices = maskVertices;
   maskMesh.RecalculateBounds();
   var left = RectTransformUtility.WorldToScreenPoint(camera, maskCorners[0]);
   var right = RectTransformUtility.WorldToScreenPoint(camera, maskCorners[3]);
   float referenceScale = Vector2.Distance(left, right) / SilhouetteMask.width / (Screen.height / 1080f);
   maskProperties.SetTexture("_MainTex", SilhouetteMask);
   maskProperties.SetFloat("_DistanceScale", referenceScale);
   maskProperties.SetColor("_Color", new Color(.86f, .83f, .69f, maskOpacity));
   maskRenderer.SetPropertyBlock(maskProperties);
  }
  public SpriteRenderer Artwork; MaterialPropertyBlock artworkProperties;
  bool HasArtwork=>Artwork&&Artwork.gameObject.activeInHierarchy&&!UseUnrestored;
  void TintArtwork(float amount){if(!Artwork)return;if(artworkProperties==null)artworkProperties=new MaterialPropertyBlock();Artwork.GetPropertyBlock(artworkProperties);artworkProperties.SetFloat("_Highlight",amount);artworkProperties.SetFloat("_Width",amount>.5f?8f:4f);Artwork.SetPropertyBlock(artworkProperties);}
  public Text Help;
  public string FacilityName;
  public float IdleOpacity=0f, HoverOpacity=.88f;
  [System.NonSerialized] public bool TutorialHighlighted;
  const string DefaultHelp="시설 위에 마우스 · Alt: 모든 시설 강조";
  bool hovered, selected;
  LineRenderer[] lines;
  Canvas canvas;
  public void OnPointerEnter(PointerEventData e){hovered=true;}
  public void OnPointerExit(PointerEventData e){hovered=false;ClearHelp();}
  public void OnSelect(BaseEventData e){selected=true;}
  public void OnDeselect(BaseEventData e){selected=false;ClearHelp();}
  void ClearHelp(){if(Help&&Help.text==FacilityName+" · 클릭하여 이용")Help.text=DefaultHelp;}
  void EnsureLines(){if(lines!=null&&renderedUnrestored==UseUnrestored)return;if(lines!=null)foreach(var line in lines)if(line)Destroy(line.gameObject);renderedUnrestored=UseUnrestored;canvas=GetComponentInParent<Canvas>();lines=new LineRenderer[ActivePaths==null?0:ActivePaths.Length];for(int i=0;i<lines.Length;i++){var g=new GameObject("Facility contour · "+FacilityName);var l=g.AddComponent<LineRenderer>();l.sharedMaterial=ContourMaterial;l.useWorldSpace=true;l.loop=true;l.positionCount=ActivePaths[i].Points.Length;l.numCornerVertices=3;l.numCapVertices=2;l.sortingOrder=5;lines[i]=l;}}
  void LateUpdate(){if(Focus)Focus.SetActive(false);if(Caption)Caption.SetActive(false);EnsureLines();bool ready=Button&&Button.IsActive()&&Button.IsInteractable();bool all=Keyboard.current!=null&&(Keyboard.current.leftAltKey.isPressed||Keyboard.current.rightAltKey.isPressed);bool active=ready&&(hovered||selected||TutorialHighlighted);TintArtwork(HasArtwork&&ready?(active||all?HoverOpacity:IdleOpacity):0);if(active&&Help)Help.text=FacilityName+" · 클릭하여 이용";else if(!ready)ClearHelp();var camera=canvas?canvas.worldCamera:null;if(!camera)return;var rect=(RectTransform)transform;UpdateMask(camera,rect,ready&&(active||all));float depth=-camera.transform.position.z;float pixelWorld=2*camera.orthographicSize/Screen.height;for(int i=0;i<lines.Length;i++){var l=lines[i];l.enabled=ready&&!HasArtwork&&!HasMask&&(active||all);if(!ready||HasArtwork)continue;var color=new Color(.87f,.83f,.64f,active||all?HoverOpacity:IdleOpacity);l.startColor=l.endColor=color;l.startWidth=l.endWidth=pixelWorld*(active||all?4f:2.4f);for(int j=0;j<ActivePaths[i].Points.Length;j++){var p=ActivePaths[i].Points[j];var screen=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(new Vector3(p.x,-p.y,0)));l.SetPosition(j,camera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,depth)));}}}
  public bool IsRaycastLocationValid(Vector2 screen,Camera eventCamera){if(HasMask){var r=(RectTransform)transform;RectTransformUtility.ScreenPointToLocalPointInRectangle(r,screen,eventCamera,out var localPoint);float u=(localPoint.x-r.rect.xMin)/r.rect.width,v=(localPoint.y-r.rect.yMin)/r.rect.height;return u>=0&&u<=1&&v>=0&&v<=1&&SilhouetteMask.GetPixelBilinear(u,v).a>.5f;}if(HasArtwork){var camera=eventCamera?eventCamera:Camera.main;if(!camera)return false;var world=camera.ScreenToWorldPoint(new Vector3(screen.x,screen.y,-camera.transform.position.z));var local=Artwork.transform.InverseTransformPoint(world);var bounds=Artwork.sprite.bounds;float u=(local.x-bounds.min.x)/bounds.size.x,v=(local.y-bounds.min.y)/bounds.size.y;if(u<0||u>1||v<0||v>1)return false;var sr=Artwork.sprite.textureRect;var texture=Artwork.sprite.texture;return texture.GetPixelBilinear((sr.x+u*sr.width)/texture.width,(sr.y+v*sr.height)/texture.height).a>.2f;}if(ActivePaths==null||ActivePaths.Length==0)return true;RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,screen,eventCamera,out var point);point.y=-point.y;foreach(var path in ActivePaths){bool inside=false;var p=path.Points;for(int i=0,j=p.Length-1;i<p.Length;j=i++){if((p[i].y>point.y)!=(p[j].y>point.y)&&point.x<(p[j].x-p[i].x)*(point.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)inside=!inside;}if(inside)return true;}return false;}
  void OnDisable(){maskOpacity=0;if(maskRenderer)maskRenderer.enabled=false;TintArtwork(0);hovered=selected=false;ClearHelp();if(Focus)Focus.SetActive(false);if(Caption)Caption.SetActive(false);if(lines!=null)foreach(var l in lines)if(l)l.enabled=false;}
  void OnDestroy(){if(maskRenderer)Destroy(maskRenderer.gameObject);if(maskMesh)Destroy(maskMesh);if(lines!=null)foreach(var l in lines)if(l)Destroy(l.gameObject);}
 }
}





