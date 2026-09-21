# 1일 차 낮 · 장면별 UI 검토 묶음

기본 1920×1080. UI는 3840×2160에 직접 제작했으며 기존 A의 크림색 활자·얇은 선·어두운 갈색 패널을 따른다. 배경 원본은 그대로 보존한다.

`node tools/serve-day01-review.cjs` 실행 후 [검토판](http://127.0.0.1:8792/design/ui/day01-v1/index.html)을 연다. 장면 메뉴나 화면 위 버튼으로 시안을 고른다. 편의점에서는 냉장고·식품 선반·생활용품 상자·문구대 각각의 물자 목록을 볼 수 있다.

- `manifest.json`: 화면 순서·PNG·PSD·논리 배치·레이어 종류.
- `strings.ko.json`: 분리 문구. 최종 대사집이 아니라 시안.
- `psd/`: 실제 UI 레이어. 배경·소품 원본을 UI PSD에 합치지 않았다.
- `layers/`: 2배 크기 개별 PNG. 게임 문자는 `runtime-text` PNG 대신 문자열과 폰트로 처리한다.
- `runtime/*-surfaces.png`: 텍스트 없는 1920×1080 UI 표면.
- `runtime/*-review-text.png`: 문구가 합쳐진 UI 참고본, 실행 텍스트 원본 아님.
- `review/`: 기존 그림과 합친 검토 JPG. 원화 편집 원본으로 쓰지 않는다.
- `validation.json`: PSD 재열기·각 레이어 RGBA·배치·원본 알파 검사.

계산대 종이의 실제 원본과 레이어 PSD는 `art/chapter01/day01-v1/`, 기존 책 분리 원본은 `art/chapter00/book-insert-v1/`에 있다. 책 페이지가 낱장으로 분리된 것은 아니다. PSD 글자는 래스터이며 Photoshop 라이브 텍스트가 아니다.

콘티 뷰어는 고정 사례이며 확률·시간·분배·미니게임을 실제 계산하거나 저장하지 않는다. 클릭 가능한 영역은 다음 UI 시안을 여는 링크다. 숫자·지명·대사는 검토안. 소리는 아직 실제 파일이 없다. Unity와 Photoshop 앱에서의 동작 확인은 남아 있다.

상세 연출·입력·분기·남은 제작은 [1일 차 낮 콘티](../../../docs/03-콘티/챕터1/CH01-DAY01-NOON-STORYBOARD.ko.md)를 따른다. 재생성: `tools/build-day01-review.py`. 실제 레이어와 원본을 다시 검증하므로 완료 후 `validation.json`도 함께 보관한다.

## 첫날 서사 검토 추가

`node tools/serve-opening-motion.cjs 8793` 실행 후 [서사 검토판](http://127.0.0.1:8793/design/ui/day01-v1/story.html)을 연다. `story.json`에 장면 목적·연출·대사 의도와 분기를 기록했다. 기존 JPG를 재사용하고 실제 물자·확률은 계산하지 않는다. [첫 주 서사 연결표](../../../docs/03-콘티/챕터1/CH01-FIRST-WEEK-STORY.ko.md)에 이후 사건과 미제작 자산을 기록했다.

`tools/check-day01-story.mjs`로 355개 도달 상태와 두 종료 분기, 조기 귀환의 확보 상태 유지, 선택 탐색 생략, 쪽지 열람별 반응, 색연필 선확보 경로를 검증했다. 브라우저 오류는 없었으며 Unity 검증은 아니다.
