# 전투 UI·바닥 원근·체력 칸 표시 보정 (2026-09-24)

- 오락실 전투 배경 arcade-unlit-v1의 바닥 타일 선에 맞춰 소실점과 격자 폭·앞쪽 끝을 보정. 18칸 프리팹 모서리와 말 위치 계산에 같은 좌표 적용.
- 조준/호버 정보창에서 체력·명중률·방어력 문구가 잘리던 높이 수정. 하단 안내띠 위에 제한하고 좌우 중 살아 있는 말과 덜 겹치는 위치 선택.
- 전투 머리 위, 하단 카드, 조준 예상 피해, 아이템 대상, 정착지 대원, 원정 선택/도착, 휴식, 귀환의 체력 막대를 1 HP = 1칸으로 통일. 숫자도 유지. 가방 용량과 소음 게이지는 별도.
- 예상 손실 칸은 황금색, 이미 소진한 칸은 어두운색.
- 재적용: AgentScripts/FixBattleReadability.cs (Unity API로 두 프리팹만 수정).
- 검증: Unity 컴파일 오류 0. VerifyFieldBattle의 Enter/BeginShowcase/AimFlow 통과. VerifyBattleReadability.Slots의 빈 체력/최대/부분/예상 손실/최대 0 통과.
- 실제 1920×1080 Game View 검수: Assets/Screenshots/Battle/ui-floor-health-fixed.png. 비전투 화면별 스크린샷 재검수는 이번 범위에서 수행하지 않음.
- 바닥 보정은 현재 사용 중인 오락실 배경 기준. 서로 다른 구도의 전투 배경 추가 시 해당 배경에 별도 보정 필요.
