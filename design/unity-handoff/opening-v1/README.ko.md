# Unity 인계 고정 보관본 · opening-v1

2026-09-10 현재 대화의 선택·콘티·이미지·PSD·원본 경로 기록을 복사한 보관본이다. 향후 원본이 바뀌어도 이 폴더는 그대로 두고 다음 버전으로 보관한다. 원본 작업은 `D:/Demo3/art`, `D:/Demo3/design`, `D:/Demo3/docs`에서 계속한다.

## 개발 시 먼저 열기

1. [시작 흐름·확정 사항](files/docs/UNITY-OPENING-HANDOFF.ko.md)
2. [장면별 이미지·원화 경로·PSD·좌표·전환 조건](files/docs/CH00-01-STORYBOARD-ASSET-MAP.ko.md)
3. [기계 판독 장면·자산 목록](files/design/unity-handoff/storyboard-assets-v1.json)
4. [UI 선택 기록](files/design/ui/ch00-01/selection.json)

## 선택한 화면

![시작 A](files/design/ui/ch00-01/review/C0-00-A-first-start.jpg)

![첫 부름·하단 딤](files/design/ui/ch00-01/review/C0-01-call.jpg)

[시작 PSD](files/design/ui/ch00-01/psd/C0-00-A-first-start.psd) · [첫 부름 PSD](files/design/ui/ch00-01/psd/C0-01-call.psd) · [대화 기준 이미지](files/design/dialogue/dialogue-layout-v1.png)

기본 해상도는 1920×1080이고 UI PSD는 3840×2160 편집 원본이다. A 화풍과 하단 딤 구성은 채택됐다. 사실적인 손 배경은 임시 검수용이다. 대화용 표정·행동 원화는 대사집 이후 제작한다.

`files/` 아래는 원본 저장소의 상대 경로를 유지한다. [보관 파일 목록·SHA-256](manifest.json)에서 원본 경로와 복사 경로를 대조할 수 있다. 보관 대상으로 등록된 파일을 전부 복사하고 해시 일치를 확인했다. 저장소 전체 백업이나 Unity 실행 프로젝트는 아니다. 문서가 언급하는 후속·외부 자료는 원 저장소를 함께 참조한다.
