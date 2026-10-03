# 굴쥐 정수리 v1 — 2026-10-03

현재 게임에 연결한 첫 리소스. 기준은 기획/타격감-리소스-명세.md의 2026-10-03 2차 정수리 판이다. 위에서 곧게 내려다보며 오른쪽(+X)이 앞이다. 회색 갈색 몸, 검은 등선, 무채색에 가까운 귀·꼬리와 뾰족한 주둥이로 방향을 읽힌다. 다른 프로젝트의 그림은 사용하지 않았다.

## 원본과 게임 파일

| 용도 | 파일 | 크기 | 월드 PPU | 피벗 |
|---|---|---|---|---|
| 편집 원본 | td_rat_master_v001.aseprite / .psd | 448×256 | 512 | 중앙(224,128) |
| 몸 원본 | td_rat_body_v001.png | 448×256 | 512 | 중앙 |
| 처치 원본 | td_rat_dead_v001.png | 448×256 | 512 | 중앙 |
| 발 원본 | td_rat_foot_v001.png | 32×24 | 512 | 중앙 |
| 게임 몸·처치 | Assets/Art/TopDown/Enemies/td_rat_{body,dead}_v001.png | 224×128 | 256 | 중앙 |
| 게임 발 | Assets/Art/TopDown/Enemies/td_rat_foot_v001.png | 16×12 | 256 | 중앙 |

원본은 RatMineArt의 형태를 해당 해상도에서 직접 래스터화한 것이다. 작은 이미지를 늘린 원본이 아니다. 게임 PNG만 원본에서 50%로 줄였다. 투명 배경이며 바닥 그림자는 포함하지 않는다. Unity 임포트는 Bilinear, 무압축, mipmap 끔, scale=1이다.

원본에는 tail, torso, head, dead(숨김), GUIDE(숨김) 5개 실제 레이어가 있다. 꼬리·몸통·머리 PNG도 layers/에 보존했다. GUIDE는 충돌 지름 0.5유닛과 피벗을 표시한다. 처치 레이어는 독립 완성 그림이고 세부 부위까지 분리하지 않았다. 발은 별도 PNG다. PSD는 레이어 픽셀을 보존하는 무압축 PSD로 작성했으며 평면 PNG를 확장자만 바꾼 파일이 아니다.

Aseprite 1.3.18.6에서 레이어 원본을 배치 저장했다. 이 설치판은 PSD 읽기/쓰기를 지원하지 않는다. PSD는 Unity TextureImporter로 열어 448×256 크기와 투명 합성을 확인했다. 합성 PNG와 비교한 114,688픽셀 중 145픽셀에 최대 채널 차이 1/255가 있었고, 레이어 합성 반올림 수준이었다. Photoshop에서 직접 열고 편집하는 검증은 수행하지 않았다.

## 연결과 동작

Assets/Data/Combat/CombatArtSet.asset의 topDown.rat.body / foot / dead에 연결했다. DungeonTest와 CombatTest의 projectArt는 같은 묶음을 참조한다. 새로운 방향별·동작별 몸 프레임은 없다. 기존 TopDownEnemyRig가 몸 회전·호흡·보행 흔들림·준비 시 웅크림·공격 시 전진과 대각선 네 발을 움직인다. 처치 때 dead 그림으로 바꾸고 기존 날림·시체 시스템을 사용한다. 도형용 임시 경로에도 같은 굴쥐를 제공한다.

## 실제 플레이 검수

- Unity 6000.6.0f1, DungeonTest. 120표본/약 6.18초에서 Idle → Locomotion → Windup → Attack 관찰. 위치 24종, 발 배치 13종으로 실제 접근·발걸음 확인.
- 몸 Sprite 이름 td_rat_body_v001 확인. 회전하는 주둥이와 공격 방향, 전조 위의 실루엣을 실제 카메라 화면에서 확인.
- 실제 TakeHit로 1 피해, 이후 159 피해를 적용해 td_rat_dead_v001 및 시체 그림 전환 확인. 기존 굴쥐는 이 타격에서 Hit 상태로 전환하지 않았으므로 별도 피격 자세 완료를 주장하지 않는다.
- 시야 밖 적 11마리의 몸 렌더러 11개가 숨김 상태인 것을 확인.
- 검수용 무적/백그라운드 실행 플래그 복원, Play 종료. 진행 저장 파일·규칙 수치·AI·배치·충돌·보상·이동 속도는 수정하지 않았다.

증거: ../../굴쥐-갱도-정수리-v1/검수/rat-runtime-observation.json, rat-locomotion.png, rat-windup.png, rat-attack.png, rat-death.png, rat-corpse.png, visibility.json. 캡처는 실제 Camera.main 렌더이며 HUD는 포함하지 않는다. 구도와 밝기를 보정한 합성 목업이 아니다.

## 후속 검수

첫 플레이용 v1이다. 현재의 도형 기반 다른 몬스터와 어울리도록 단순한 덩어리로 만들었다. 장시간 플레이 가독성과 사용자 화풍 승인은 별도다. 일반 굴쥐만 실제 전투 검수했으며 정예 1.35배의 밀집 전투, CombatTest 직접 플레이는 미검수다. 머리·꼬리 독립 절차 동작, 방향별 셀 애니메이션은 추가하지 않았다.
