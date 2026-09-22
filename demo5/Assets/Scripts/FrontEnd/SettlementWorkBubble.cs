using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Demo5.FrontEnd {
 public sealed class SettlementWorkBubble:MonoBehaviour {
  public int MemberIndex;
  public GameObject Visual;
  public Text Label;
  public RectTransform ToolIcon;
  public Vector2 Offset=new Vector2(-105,72);
  SettlementController owner;SettlementPawnMotion motion;
  void Start(){owner=FindAnyObjectByType<SettlementController>();motion=FindAnyObjectByType<SettlementPawnMotion>();}
  void LateUpdate(){
   if(!owner||!motion||owner.Campaign==null)return;
   var person=owner.Campaign.Party.ElementAtOrDefault(MemberIndex);var order=owner.CraftPanel.Orders.FirstOrDefault(x=>x.Member==person);
   bool show=order!=null&&MemberIndex<motion.Bodies.Length&&!motion.MovingAt(MemberIndex)&&motion.gameObject.activeInHierarchy;
   Visual.SetActive(show);if(!show)return;
   var body=motion.Bodies[MemberIndex].GetComponent<SpriteRenderer>();var canvas=GetComponentInParent<Canvas>();var screen=RectTransformUtility.WorldToScreenPoint(Camera.main,new Vector3(body.bounds.center.x,body.bounds.max.y,0));
   RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent,screen,canvas.worldCamera,out var point);
   ((RectTransform)transform).anchoredPosition=point+Offset;
   Label.text=order.Recipe.Name+" ×"+order.Quantity+"\n예상 "+order.Minutes+"분";
   ToolIcon.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Time.time*4)*9);
   motion.WorkBadges[MemberIndex].gameObject.SetActive(false);
  }
 }
}
