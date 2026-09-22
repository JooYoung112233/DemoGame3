from pathlib import Path
R=Path('demo5/Assets/Scripts/FrontEnd')
p=R/'ExpeditionSearchPanel.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('Notice.text="배정만 저장합니다. 턴 진행·획득은 다음 단계입니다."','Notice.text="완료 전에 멈춰도 수색도는 유지됩니다."')
s=s.replace('  void Refresh(){for(int i=0;i<3;i++){','  void Refresh(){var progress=arrival.Loot.State(site);if(progress.Progress>0)Pace=progress.Pace;for(int i=0;i<3;i++){Paces[i].interactable=progress.Progress==0;')
s=s.replace('   Choose.interactable=Worker!=null&&Worker.Health>0;','   Cost.text="수색도 "+progress.Progress+" / "+(progress.Required>0?progress.Required:Pace+1)+"턴  ·  누적 소음 "+arrival.Rooms.Noise+"\\n"+(Duty==0?"동료가 함께하면 발견 확률 +10%p":"망보기 동료가 있으면 턴당 소음 -1");Choose.GetComponentInChildren<Text>().text=progress.Progress>0?"다음 1턴 진행":"수색 시작";Choose.interactable=Worker!=null&&Worker.Health>0;')
start=s.index('  void Ask()');end=s.index('  public void Dismiss()',start)
s=s[:start]+'''  void Ask(){if(!IsOpen||!Choose.interactable||Review.activeSelf)return;ReviewBody.text=Title.text+"  ·  "+new[]{"빠른 수색","보통 수색","정밀 수색"}[Pace]+"\\n\\n담당  "+Worker.Name+"\\n동료  "+(Duty==0?(arrival.Participants.Count>1?"함께 수색":"혼자 수색"):"망보기")+"\\n이번 진행  1턴\\n\\n완료 시 발견물을 확인합니다. 중단해도 진행도는 유지됩니다.";Confirm.GetComponentInChildren<Text>().text="1턴 진행";Review.SetActive(true);Workspace.interactable=Workspace.blocksRaycasts=false;}
  void Save(){if(!IsOpen||!Review.activeSelf||Worker==null||Worker.Health<=0)return;Dismiss();if(!arrival.Loot.Advance(site,Pace,Worker,Duty)){Refresh();return;}Assignments[site]=new Assignment{ObjectIndex=site,Worker=Worker,Pace=Pace,Duty=Duty};if(arrival.Loot.State(site).Complete){Close();arrival.Loot.Open(site);}else{Refresh();arrival.Status.text=Title.text+" · 수색 중\\n진행도 유지 · 다음 턴 대기";}}
''' +s[end:];p.write_text(s,encoding='utf-8')
p=R/'ExpeditionLootPanel.cs';s=p.read_text(encoding='utf-8-sig').replace('public int Progress,Required,Pace;','public int Progress,Required,Pace,Bonus;')
s=s.replace('int pace,Adventurer worker)', 'int pace,Adventurer worker,int duty=0)').replace('s.Required=pace+1;','s.Required=pace+1;s.Bonus=duty==0&&arrival.Participants.Count(p=>p.Health>0)>1?10:0;').replace('SpendSearchTurn(3-s.Pace)','SpendSearchTurn(Math.Max(0,3-s.Pace-(duty==1&&arrival.Participants.Count(p=>p.Health>0)>1?1:0)))').replace('drop.Chance+(s.Pace-1)*15','drop.Chance+(s.Pace-1)*15+s.Bonus');p.write_text(s,encoding='utf-8')
