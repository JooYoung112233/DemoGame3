# 귀환 정산 UI 편집 원본

기존 승인 종이 자산을 재사용한 화면이다.

- `PSD/return-layout.psd`: 딤, 본문 종이, 경과 시간, 대원 초상/체력 막대, 아이템 아이콘/행, 하단 버튼의 분리 레이어 24개(2명·4종 미리보기 상태). 좌표·알파·합성 검증 완료.
- `원본/`, `개별-PNG/`: 기존 자산의 동일 복사본. 실제 원본 크기와 출처는 `sources.json`에 기록한다.
- 3840×2160은 배치 편집 캔버스이며 작은 원본 이미지의 디테일을 복원한 것이 아니다.
- 글자·수치·화살표는 Unity 프리팹에서 편집한다. `return-art-preview.png`는 글자 없는 이미지 조립본이다.
- 실제 Unity 캡처: `Assets/Screenshots/Settlement/return-cards.png`.

재생성은 실제 귀환 패널을 연 Play 상태에서 `ExportExpeditionReturnLayout.Run`과 `tools/prepare_return.py`를 사용한다. 편집 모드에서는 동적 목록을 제외한 기본 프리팹만 출력한다.
