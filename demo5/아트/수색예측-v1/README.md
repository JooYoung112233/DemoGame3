# 수색 예측 UI 편집 원본

기존 종이와 물자 아이콘을 재사용했다. `원본`과 `개별-PNG`는 원본 픽셀을 보존한다. `PSD/search-preview-layout.psd`는 종이/아이콘/행을 실제 레이어로 분리한 컴포넌트 배치용 원본이다. `layout.json`은 패널 내부 좌표, `sources.json`은 Unity 원본 경로와 크기다.

3840×2160 PSD는 배치 편집용으로 기존 텍스처를 확대했으며 디테일 복원본이 아니다. 문구와 수치는 이미지에 굽지 않고 Unity Text에서 수정한다. 스크롤 밖 행도 레이어에 포함되므로 Unity 표시 영역은 `SearchDropPreview.prefab`에서 조정한다.

Play에서 수색 화면을 열고 `ExportSearchPreviewLayout.Run` → `tools/prepare_search_preview.py` 순서로 재생성한다. 실제 합성 게임 화면은 `Assets/Screenshots/Settlement/search-preview.png`다.
