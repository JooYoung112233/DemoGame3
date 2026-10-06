using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace Demo5.FrontEnd {
 public sealed partial class SettlementOpeningChapter {
  public GameObject NightView, NightChoices;
  public Text NightTitle, NightBody, NightResult, NightBackLabel;
  public Button NightListen, NightLook, NightBack;
  public bool NightAvailable=>State.Enabled&&State.Complete&&State.NightChoice==0&&owner&&owner.Campaign!=null&&(State.NightHeard||owner.Campaign.MinuteOfDay>=1200||owner.Campaign.MinuteOfDay<360);
  void InitializeNight(){if(!NightView)return;NightView.SetActive(false);NightBack.onClick.AddListener(Close);NightListen.onClick.AddListener(()=>ResolveNight(1));NightLook.onClick.AddListener(()=>ResolveNight(2));}
  void OpenNight(){if(!NightAvailable||IsOpen||!NightView)return;State.NightHeard=true;returnFocus=EventSystem.current?.currentSelectedGameObject;NightTitle.text="문밖에서, 세 번";NightBody.text="문 너머에서 두드리는 소리가 세 번 들렸다.\n잠시 뒤, 같은 간격으로 다시 세 번.\n\n관리표의 문장이 떠오른다.\n<color=#713C29>‘문 너머에서 세 번 두드리면, 먼저 대답하지 말 것.’</color>\n\n문은 닫혀 있다. 어떻게 확인할까?";NightResult.text="선택할 때만 시간이 흐릅니다. 진행 중인 작업도 함께 진행됩니다.";NightChoices.SetActive(true);NightListen.interactable=NightLook.interactable=true;NightBackLabel.text="나중에 결정하기";owner.Main.interactable=owner.Main.blocksRaycasts=false;NightView.SetActive(true);EventSystem.current?.SetSelectedGameObject(NightListen.gameObject);}
  public void ResolveNight(int choice){if(!NightView||!NightView.activeSelf||!State.NightHeard||State.NightChoice!=0||(choice!=1&&choice!=2))return;int minutes=choice==1?15:30;State.NightChoice=choice;NightListen.interactable=NightLook.interactable=false;if(!owner.Campaign.AdvanceSettlementTime(minutes)){State.NightChoice=0;NightListen.interactable=NightLook.interactable=true;return;}string clue=choice==1?"세 번의 두드림은 매번 같은 간격이었다. 사람의 대답을 기다리는 듯했다.":"가린 창 너머에는 아무도 없었다. 문 앞의 먼지에 발자국 하나 남아 있지 않았다.";owner.ActivityLog.Add("밤의 기록 · "+(choice==1?"대답 없이 귀 기울였다":"불빛을 가리고 창으로 살폈다")+" · "+minutes+"분 경과. "+clue);NightChoices.SetActive(false);NightTitle.text=choice==1?"대답하지 않은 밤":"빈 문 앞";NightBody.text=clue+"\n\n누구도 이름을 부르지 않았다.\n한동안 기다리자 소리는 멎었다.\n\n이유는 아직 알 수 없다. 알아낸 사실을 기록했다.";NightResult.text="<color=#4D5145>기록한 단서</color>\n"+(choice==1?"일정하게 반복되는 두드림":"먼지 위에 발자국이 없음")+"\n\n시간 +"+minutes+"분 · 물품 소모 없음\n진행 중인 작업에도 같은 시간이 반영됩니다.";NightBackLabel.text="정착지로 돌아가기";owner.NoticeTitle.text="밤의 기록을 남겼습니다";owner.NoticeBody.text="문을 열지 않고 상황을 확인했습니다.\n정착지 생활을 이어갈 수 있습니다.";EventSystem.current?.SetSelectedGameObject(NightBack.gameObject);}
 }
}
