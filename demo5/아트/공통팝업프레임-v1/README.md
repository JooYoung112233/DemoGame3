# 공통 팝업 프레임

제목 종이와 하단 버튼을 개별 레이어로 분리한 배치 편집용 PSD 5종이다. 전체 화면을 합친 PSD가 아니다. 내부 패널·아이콘 원본은 기존 화면별 PSD를 함께 사용한다. 새 공통 제목/버튼 좌표는 이 폴더의 `layout.json`을 우선한다.

`원본/paper.png`와 `개별-PNG/paper.png`는 기존 `Assets/Art/PartySelection/footer-paper.png`의 원본 픽셀 복사다. PSD는 3840×2160 배치 캔버스이며 확대가 디테일 복원을 의미하지 않는다. 글자는 포함하지 않고 Unity 프리팹에서 편집한다. `tools/prepare_popup_chrome.py`로 재생성 및 레이어 재개방 검증한다.

창고·제작대·휴식의 공통 프레임도 추가하여 총 8종이다. inventory는 제목/돌아가기/하단 안내 종이, craft와 rest는 제목/돌아가기/확정 종이를 각각 분리한 PSD다. 내부 전체 화면 조립 원본은 아니며 최종 위치는 Unity 프리팹 기준이다.
