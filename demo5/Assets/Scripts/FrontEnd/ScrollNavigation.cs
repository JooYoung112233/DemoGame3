using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
namespace Demo5.FrontEnd
{
 public sealed class ScrollNavigation:MonoBehaviour
 {
  public ScrollRect Scroll;
  public Button Up,Down;
  float lastPosition,settledAt;
  void Awake(){Up.onClick.AddListener(()=>Move(1));Down.onClick.AddListener(()=>Move(-1));}
  void LateUpdate(){if(!Scroll||!Scroll.content||!Scroll.viewport)return;float excess=Scroll.content.rect.height-Scroll.viewport.rect.height;bool overflow=excess>2;Up.gameObject.SetActive(overflow);Down.gameObject.SetActive(overflow);Up.interactable=overflow&&Scroll.verticalNormalizedPosition<.999f;Down.interactable=overflow&&Scroll.verticalNormalizedPosition>.001f;if(Mathf.Abs(lastPosition-Scroll.content.anchoredPosition.y)>.1f){lastPosition=Scroll.content.anchoredPosition.y;settledAt=Time.unscaledTime;}if(overflow&&Time.unscaledTime-settledAt>.16f&&!(Mouse.current?.leftButton.isPressed??false))Snap();}
  float Step(){var layout=Scroll.content.GetComponent<VerticalLayoutGroup>();return layout&&Scroll.content.childCount>0?((RectTransform)Scroll.content.GetChild(0)).rect.height+layout.spacing:0;}
  void Snap(){float step=Step(),excess=Scroll.content.rect.height-Scroll.viewport.rect.height;if(step<=0||excess<=2)return;float offset=(1-Scroll.verticalNormalizedPosition)*excess;if(offset<1||offset>excess-1)return;Scroll.StopMovement();Scroll.verticalNormalizedPosition=1-Mathf.Clamp(Mathf.Round(offset/step)*step,0,excess)/excess;}
  void Move(int direction){Canvas.ForceUpdateCanvases();float excess=Scroll.content.rect.height-Scroll.viewport.rect.height;if(excess<=2)return;Scroll.StopMovement();float step=Step();float distance=step>0?Mathf.Max(1,Mathf.Floor(Scroll.viewport.rect.height/step))*step:Scroll.viewport.rect.height*.8f;Scroll.verticalNormalizedPosition=Mathf.Clamp01(Scroll.verticalNormalizedPosition+direction*distance/excess);Snap();}
 }
}
