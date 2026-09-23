# 시간 진행 UI 편집 원본

기존 승인 종이 자산을 재사용했다. 새로운 배경/인물 원화 생성은 없다.

- `원본/`, `개별-PNG/`: 기존 PNG의 동일 복사본. 실제 원본 해상도와 출처는 `sources.json`에 기록한다.
- `PSD/time-layout.psd`: 딤, 본문 종이, 4개 선택 버튼, 양쪽 하단 버튼, 스크롤 영역의 13개 분리 레이어(작업 카드 2개와 아이콘 포함). 레이어 좌표·이름·알파·합성을 다시 읽어 검증했다.
- 3840×2160은 편집용 배치 캔버스다. 기존 저해상도 종이 이미지의 확대는 고해상도 디테일 복원이 아니다.
- 텍스트와 수치는 이미지에 넣지 않았다. 실제 표시/글씨/스크롤 화살표는 `SettlementTimePanel.prefab`에서 편집한다.
- `time-art-preview.png`는 글자 없는 합성 원본 미리보기이며 실제 Unity 캡처는 `Assets/Screenshots/Settlement/time-cards.png`다.

재생성: `ExportSettlementTimeLayout.Run`으로 프리팹 배치를 출력한 뒤 `tools/prepare_time.py` 실행.

동적 작업 카드까지 포함하려면 Play에서 시간 진행 창을 연 뒤 배치를 출력한다.
