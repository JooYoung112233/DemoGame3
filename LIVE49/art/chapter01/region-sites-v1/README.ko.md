# 첫 지역 현장 배경 L8 · L9 · L10

2026-09-13 제작. 기존 잡화점 배경의 시점·색감·뭉갠 그림 질감을 참고해 공영주차장, 작은 세탁소, 동네 약국을 제작했다. 인물·캠핑카·버튼·탐색 강조는 포함하지 않는다. 주차 위치와 이벤트 인물은 게임 상태에 따라 별도로 표시한다.

각 `L*-source.png`는 이미지 도구가 반환한 **1672×941 원본**이다. 요청한 3840px 배경 목표에는 미달한다. 확대나 디테일 복원 처리를 하지 않았으며 확대 연출용 최종 고해상도 납품으로 간주하지 않는다.

각 `L*-native.psd`는 원본 크기의 `fixed_background` 레이어 하나를 포함한다. 가구·벽·소품 내부가 분리된 PSD는 아니다. 그림 수정에 필요한 개별 소품 분리는 후속 제작 범위다. PNG 원본과 PSD 합성 픽셀, PSD 레이어 복원 픽셀 및 Unity 복사본 바이트가 일치함을 검사했다.

- 생성 지시: [prompts.json](prompts.json)
- 해상도·SHA-256·Unity 리소스: [manifest.json](manifest.json)
- PNG/PSD/Unity 동일성 검사 12개 통과: [validation.json](validation.json)
- 포장 재현: `tools/package-region-sites-v1.py`
- Unity 연결: `Core/RegionExploration.cs`의 각 장소 `Art`와 `Live49/Stages/region-L8`, `region-L9`, `region-L10`.

공영주차장 일반 탐색은 새 주차장 그림을 사용하고 밴딧 조우가 진행될 때만 기존 별도 골목 원화로 전환한다. 저장 슬롯도 같은 장소 리소스를 사용한다.
