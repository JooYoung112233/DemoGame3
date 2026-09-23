# 전투 카드 UI 정렬 — 2026-09-24

- 상단 턴 카드 묶음을 전용 영역 중앙에 배치. 카드 상단 y20, 그림 영역 64×64, 이름 영역 88×30으로 정렬.
- 하단 아군/대상 초상 종이 150×185와 내부 그림 영역 120×131을 동일하게 맞춤. 보이는 잉크의 경계 기준으로 중앙 정렬하며 종이 상하 여백을 균등하게 유지.
- 원본 PNG를 변경하지 않고 BattlePortraitFit의 Inspector crop으로 투명 여백을 제외. 감염자 UI는 기존 그림의 상반신을 표시. Unity의 tight sprite UV 여백 제거도 반영해 이중 크롭 방지.
- 적용: AgentScripts/AlignBattleCards.cs. 4개 전투 프리팹 수정. 신규 감염자 원화는 미적용.
- 검수: 컴파일 성공, 실제 탐험→전투 진입, 1920×1080 Game View 확인. Assets/Screenshots/Battle/ui-card-alignment.png.
- 검수 중 동시 작업으로 생긴 FieldBattleState.Wire 분기의 List<int> 전달 오류는 pair.ToArray()로 최소 수정.
