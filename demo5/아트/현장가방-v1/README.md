# 현장 개인 가방

기존 발견물 분배 화면과 동일한 종이·패널·물건 아이콘을 재사용했다. 새 원화 생성은 없다.

- `원본/`, `개별-PNG/`: 실제 텍스처 원본 픽셀 보존.
- `PSD/field-bags-layout.psd`: 패널, 대원 카드, 실루엣, 아이콘, 버튼별 실제 분리 레이어.
- `layout.json`: Unity 실제 화면의 좌표와 색상.
- `sources.json`: 원본 경로, 크기, 복사 파일 매핑.

PSD는 3840×2160 배치 편집용이며 기존 텍스처를 확대 배치한 것이다. 고해상도 디테일 복원본은 아니다. 텍스트는 이미지에 굽지 않고 `ExpeditionBagPanel.prefab`에서 수정한다. 배경은 기존 `Assets/Art/ExpeditionArrival` 원본을 사용한다.

재생성: Play에서 현장 가방을 연 후 `ExportFieldBagLayout.Run`, `tools/prepare_field_bags.py` 순서. PNG 합성 미리보기는 그림 레이어만이며 실제 게임 화면은 `Assets/Screenshots/Settlement/field-bags.png`다.
