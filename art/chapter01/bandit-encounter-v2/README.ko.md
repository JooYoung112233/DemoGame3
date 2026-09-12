# 밴딧 인상 수정 v2

2026-09-13. 사용자의 ‘너무 착하게 보인다’는 피드백을 반영한 두 인물 수정본이다.

- 남자: 내려간 눈썹, 좁힌 눈매, 굳은 입과 턱, 힘을 준 제지 손짓.
- 여자: 경계하는 눈매, 굳은 입, 긴장한 손과 어깨.
- 기존 의상·머리·인물 비율·왼쪽을 보는 방향과 골목 배경을 유지했다. 원화 v1은 별도 보존했다.

내장 image_gen으로 각각 기존 인물 원화를 편집했다. [최종 생성 지시문](prompts.json)에 참조 경로와 편집 범위를 기록했다. 출력된 체크무늬는 사용자의 기존 배경 제거 승인에 따라 로컬에서 알파만 변경해 제거했다. RGB는 v2 생성 원본과 동일하다.

## 파일

- `sources/`: 생성 원본 두 인물, 변경 없이 복사한 v1 골목 배경.
- `layers/bandit-lead-native.png`, `layers/bandit-lookout-native.png`: 실제 투명 RGBA 인물 PNG.
- `layers/*-contact-shadow.png`: 별도 접지 그림자.
- `psd/`: 원본 배경, 인물별 2개 레이어 PSD, 원본 크기 편집 마스터, 배경·두 인물·두 그림자가 분리된 5개 레이어 배치 PSD.
- [합성 검수본](review/first-encounter.png), [배치와 원본 치수](manifest.json), [29개 파일 검증 결과](validation.json).

PSD의 인물은 생성 원본과 투명 인물로 분리했으며 얼굴·팔다리·옷 내부 레이어는 분리하지 않았다. 배치 PSD에 축소된 인물만 보관한 것이 아니라 native PNG와 native PSD도 보존했다. 스크립트가 PSD 레이어 채널·가시성·합성 픽셀을 검증했으며 Photoshop 앱 검수는 아니다.

배경은 1672×941, 인물 원본 캔버스는 1024×1536이다. 실제 인물 크기는 남자 711×1504, 여자 533×1494이다. 확대용 원본 목표에는 미달하며 보간 확대하지 않았다.

재현: `tools/prepare-bandit-art-v1.py --version v2`. Unity 반입 스크립트 `Demo3/AgentScripts/ImportBanditArt.cs`의 원본 경로도 v2로 갱신했다. 인게임 리소스는 `Demo3/Assets/_Project/Resources/Live49/Encounters/Bandit/`이다. 이벤트 로직·대사·씬은 이번 수정 범위에 포함하지 않았다.
