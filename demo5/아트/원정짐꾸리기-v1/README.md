# 원정 짐 꾸리기 UI03
승인 기준: ../UI전체시안-v1/03-원정짐꾸리기-v2.png.
기존 승인 종이·배경·아이콘을 재사용한다. 새 그림 생성 없음. 3840×2160 PSD의 배경/패널/제목/카드/슬롯/버튼이 개별 레이어이며 글자와 수치는 Unity에서 편집한다. 원본 텍스처 PNG와 단일 요소 PSD도 보존했다. 큰 배치 PSD는 기존 픽셀 확대 배치이며 고해상도 디테일 복원이 아니다.
아이템 그림 원본: ../개별인벤토리-v1 및 Assets/Art/Inventory. 대원 원본: ../모험가선택-v1 및 Assets/Art/PartySelection. 새 PSD는 기존 인물·아이콘 원본을 대체하지 않는다.
시안과 차이: 최신 규칙대로 뒤로 버튼 좌측 하단. 실제 데이터의 2명·가방 3종 사용. 현재 아이템은 보급품/탄약/재료이므로 물/치료/도구 수량을 꾸며내지 않는다. 체크리스트도 실제 합계로 표시한다. 빈 16칸을 가짜로 그리지 않는다.
Unity 프리팹: ExpeditionPackingPanel, ExpeditionPackingReview, PackingItemSlot. 대원 카드는 ExpeditionMemberCard 공유. 출발 동작은 후속 단계이며 이번 화면은 준비 내역 확인까지다.
