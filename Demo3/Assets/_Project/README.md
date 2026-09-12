# Live49 Unity 프로젝트 구성

이 폴더에는 실제 게임에 쓰는 것만 둔다. 원본·PSD·검수 이미지는 저장소의 `art/`, `design/`에 그대로 두고, 이곳의 PNG는 원본 파일명을 유지한 복사본이다. 수정은 원본에서 한 뒤 다시 복사한다.

## 폴더

| 폴더 | 내용 |
| --- | --- |
| `Scenes/` | `00_Title`(시작 씬), `01_Game`(게임 씬). Build Settings 순서도 같다 |
| `Title/` | 시작 화면 전용 배경·로고(`Art/`)와 버튼·음영(`UI/`) |
| `Shared/` | 여러 챕터가 함께 쓰는 것: 글꼴(`Fonts/`), 대화창·딤·진행 표시(`UI/`), 대화 초상(`Characters/Portraits/`), 캠핑카(`Locations/Camper/`) |
| `Chapters/Chapter00/` | 챕터 0 전용 원화(`Art/Memory`, `Art/Present`)와 대사·화자 데이터(`Data/`) |
| `Scripts/` | `Core`(씬 전환·입력·시간), `UI`, `Title`, `Dialogue`, `Chapter00`, `Editor` |
| `Shaders/` | 스테이지 이미지용 블러·얼굴 마스크·지지직 셰이더와 머티리얼 |

새 챕터는 `Chapters/ChapterNN/`과 `Scripts/ChapterNN/`을 추가하고, 게임 씬 `Stage/ChapterNN` 아래에 무대를 둔다.

## 씬 하이라키

- `[System]`: EventSystem, 씬 진행 컨트롤러(`TitleController`, `C0_OpeningDirector`)
- `[Camera]`: Main Camera(검정 레터박스)
- `[UI]`: 1920×1080 고정 `Viewport_1920x1080`. 원화(1672×941)는 `*Art_1672x941` 프레임 안에 원본 좌표로 배치하고 비율을 유지해 확대한다.

게임 씬 Viewport 순서: `Stage/Chapter00`(C0_PhotoWall → C0_MemoryPath → C0_Present) → `Portraits` → `CallOverlay` → `DialoguePanel` → `ScreenFade`.

2026-09-12 도입부 수정: 런타임 `C0_BookIntro`에서 첫 부름을 표시한다. 시작 직후 캠핑카 전경·사진 확대는 생략하고, 책 → “수혁아.” 입력 대기 → 발·손 회상 → 얼굴이 흐려진 벽 사진 → “아빠?” 입력 대기 → 현재 캠핑카 순서로 재생한다. 현재 복귀는 1.60초 디졸브와 1.40초 여백을 둔다. `ReadabilityGradient`가 부름과 일반 대사 하단에 별도 UI 딤을 보강한다. 원화 PNG는 수정하지 않는다.

검증: `VerifyOpeningReadability`로 시작 시 캠핑카 비노출, 첫 입력 후 회상, 회상 대사 7개, 사진·현재 복귀를 플레이 모드에서 확인했다. 1920×1080 캡처 검수 및 런타임 오류 0개 확인. 게임 씬만 직접 열어 시작하는 디버그 경로의 시각 검수는 별도다.

## 원본 위치

| 여기 | 저장소 원본 |
| --- | --- |
| `Title/Art/title-background-v1.png`, `logo-v1.png` | `art/title/` (배경은 교체 예정 임시본) |
| `Title/UI/01_readability_shade.png` | `design/ui/ch00-01/layers/C0-00-A-first-start/` |
| `Title/UI/title-button-*.png` | `design/ui/ch00-01/sprites/A/` |
| `Shared/Fonts/*` | `art/title/fonts/` (OFL 고지 동봉) |
| `Shared/UI/01_bottom_readability_gradient.png`, `03_advance_cue.png` | `design/ui/ch00-01/runtime/1920x1080/C0-01/` |
| `Shared/UI/01_dialogue_panel.png`, `02_nameplate.png` | `design/ui/ch00-01/runtime/1920x1080/C0-02/` |
| `Shared/Characters/Portraits/suhyeok-dialogue-native.png` | `art/chapter00-01/art-polish-v1/layers/` |
| `Shared/Characters/Portraits/soi-answer-soft-v3.png` | `art/chapter00/memory/dialogue-v1/` |
| `Shared/Locations/Camper/camper-clean-base-v3.png` | `art/chapter01/revision-v3/` |
| `Chapters/Chapter00/Art/Memory/*` | `art/chapter00/memory/dialogue-v1/` |
| `Chapters/Chapter00/Art/Present/C0-current-*.png` | `art/chapter00-01/art-polish-v1/layers/` |
| `Chapters/Chapter00/Data/C0_OpeningDialogue.json` | `design/ui/ch00-01/memory-dialogue.json` 정리본 |

서연 초상은 회상 대화에서 표시하지 않기로 했으므로 가져오지 않았다.

2026-09-12 대사 진행 안내: `DialogueAdvanceCue`가 부름·일반 대화에 `계속 ▶`만 표시한다. 클릭·Space 문구는 제거했다. 출력 완료 후 0.35초 여유를 두고 0.22초 페이드인하며 화살표만 1.4초 주기로 3px 이동한다. 출력 중·장면 전환에서는 숨긴다. 현재 마지막 질문에는 탐색 진입 안내로 계속 표시를 제공한다. 진행 입력 뒤에도 0.22초 안내 퇴장과 0.18초 여백을 둔다. 입력 방식은 기존 클릭·Space를 유지한다.

2026-09-12 깜빡임·속도 검수: 같은 장면의 대사창과 딤은 유지하고 본문만 0.16초에 걸쳐 교체한다. 장면 전환에서는 대사창 퇴장을 기다린 뒤 새 이름을 숨겨진 상태에서 지정하므로 이전 이름표가 재노출되지 않는다. 회상·현재 디졸브는 이전 그림의 불투명도를 유지한 채 다음 그림을 덮어 중간 밝기 저하를 방지한다. 나레이션·인물 대사 사이 배경 블러는 3.5px로 유지한다. 기본 글자 간격은 0.075초, 회상 내부 전환은 1초와 도착 후 1.6초 여백을 사용한다. 사진의 의도된 짧은 지지직 연출은 유지한다.

검증: `AgentScripts/AuditOpeningPacing.cs`에서 실제 글자 출력 속도로 책부터 현재 마지막 대사까지 10개 대사를 재생했다. 대사 사이 대사창 유지, 재등장 이름표, 전환 중 이전 그림 불투명도, 현재 배경 블러, 빠른 중복 입력 차단과 화자 좌우를 확인했다. 플레이 모드 오류 0개. `Screenshots/PacingAudit/report.txt`와 1920×1080 캡처 7장에 기록했다. 검수 범위는 현재 구현된 챕터 0 도입부이며 독립 빌드 검수는 별도다.

2026-09-12 이름표 좌우 배치: 현재 화자 이름표 하나를 수혁은 왼쪽, 소이·서연은 오른쪽에 표시한다. 대사창 양쪽 가장자리에서 동일하게 110px 안쪽에 둔다(1920×1080 기준). 위치 판정은 표시 이름 대신 내부 `speakerId`를 사용하므로 미공개 이름 `?`도 올바른 쪽에 나온다. 나레이션에서는 이름표를 숨긴다. `DialogueView.rightSpeakerIds`에서 오른쪽 화자 목록을 편집할 수 있다. 실제 회상·현재 대사 재생으로 좌우 배치, 소이 이름 공개, 서연 `?`, 나레이션 숨김을 검증했다.

## 공통 HUD와 Esc 메뉴 · 2026-09-12

최신 연속 연결: `C0Journey`·`ContinuationGraph`가 기존 후속 콘티를 실제 챕터 0 종료까지 이어 재생한다. `JourneyState`가 준비·조사·기록·가방·날짜·위치를 유지한다. `ActivityPanel`의 요리/기록, 편의점 첫 이동·왕복·보장 물품 회수, 하루 마치기와 자동 저장, Esc 수동 저장·불러오기 및 타이틀 이어하기를 연결했다. 새 씬은 없다. `VerifyJourney` 전체 흐름 검수와 `ReviewJourneyScreens` 가독성 검수가 통과했다. 잠정 비용·방송/사진 대사 초안·요리대 수리 해금 등 잔여 범위는 저장소의 `docs/08-UI연출/UNITY-LIFE-LOOP.ko.md`를 따른다. 아래 단계별 미연결 설명은 당시 기록이다.

2단계 가방·인물 상태: `BagPanel`과 `BagContents`를 추가했다. 실제 캠핑카 HUD의 가방 버튼에서 같은 캔버스의 패널을 열며 카테고리·6종 페이지·선택 물품 설명·수량, 수혁·소이 각각 허기·수분·체력·스태미너를 표시한다. `ConfigureBag`로 데이터를 공급한다. 현재 챕터 0에는 확정된 초기 재고·생존 수치가 없어 확인 전 상태를 사용한다. 물자 지급·소비·회복·저장은 별도이며 임시 검수 수치는 실제 게임에 남기지 않았다. `OpenBagForReview`, `VerifyBagPanel`로 실제 게임 진입, 목록 갱신·페이지·개별 최대치·미확인 상태·입력·정지 복원을 확인했다. `Screenshots/BagReview/`에 실제 초기 화면과 명시적인 검수 데이터 화면을 구분해 기록한다.

`GameHud`는 게임 씬 사이에 유지되는 공통 UI다. 실제 챕터 0에서 Esc로 재개·설정·타이틀로·게임 종료 메뉴를 사용할 수 있다. 대화 중 생활 HUD는 숨긴다. 메뉴는 게임 시간과 오디오를 멈추며 설정에서 Esc는 메뉴로 한 단계만 돌아간다. 타이틀·종료는 미저장 진행 확인 후 실행한다.

별도 HUD 프리뷰 씬과 전용 생성·실행 스크립트는 사용자 요청으로 제거했다. `_Project/Scenes`에는 `00_Title`과 `01_Game`만 유지한다. HUD·지도·하루 마치기는 재사용 UI 코드로 보존하며 실제 게임의 탐색 시작 시 `SetExplorationContext`, 대화 시작 시 `SetNarrativeMode`로 전환한다. 지도 초기 데이터는 `FirstRegionMap.Create()`에 분리했다.

1단계 캠핑카 탐색 연결: 마지막 질문을 읽고 진행 입력을 누르면 대화창과 인물 컷인이 사라지고 배경이 선명해지며 HUD가 열린다. `CamperInteractions`는 소이·주방·침대·스케치북의 클릭 영역과 호버 이름을 제공한다. 소이의 두 답변 분기와 이후 스케치북 대화는 기존 콘티 문구를 사용한다. 각 대화 후 탐색과 목표를 복원한다. 주방은 이용 조건을 안내하고 침대는 하루 마치기 조건창으로 연결한다. 라디오 이후 이야기·실제 이동·요리·다음 날·저장은 후속 단계다. 현재 진행 상태는 실행 중 메모리에만 유지된다. `VerifyCamperExploration`과 `Screenshots/CamperExploration/`에서 실제 게임 씬의 진행·입력·취소·복귀를 검수한다.

후속 지도 UI: 새 씬 없이 같은 HUD의 지도 버튼에서 `MapPanel`을 연다. 종이 지도와 목적지 카드, 미발견 장소 숨김, 선택 경로 표시, 출발 확인/취소와 Esc 복귀를 구현했다. 지도 중 생활 HUD는 숨긴다. `ConfigureMap(places, blockedReason, travel)`과 `Discover(id)`로 데이터·상태를 연결한다. 실제 이동·비용·저장은 별도이며 개략 도로 좌표는 시안이다. `VerifyMapPanel.Run` 및 `Screenshots/MapReview/`에서 검수했다.

`AgentScripts/VerifyGameHud.cs`에서 실제 Esc 입력, 설정 왕복, 대사 정지·재개, 종료 취소와 타이틀 복귀를 검수했다. 화면과 보고서는 `Screenshots/HudReview/`에 있다.

후속 HUD 요청으로 생활 버튼 위에 `하루 마치기`와 오늘의 마무리 확인창을 추가했다. `ConfigureDayEnd(summary, blockedReason, finish)`가 현재 조건과 완료 처리를 받는다. 남은 일이 있으면 사유를 표시하고 확정을 막는다. 취소/Esc는 캠핑카로 돌아가며 완료 콜백은 정지를 해제한 뒤 한 번만 실행한다. 시안에서는 소이와의 대화가 남은 상태이고, 실제 소등·다음 날 스토리 연결은 별도다. `VerifyGameHud.DayEnd`와 `Screenshots/HudReview/day-end-report.txt`에서 검수한다.

## 타이틀 설정 UI · 2026-09-11

타이틀의 설정 버튼이 `SettingsPanel`을 연다. 기존 1920×1080 Viewport 안에서 런타임에 구성하며, 배경·로고·시작 연출은 그대로 사용한다.

- 전체 음량: 5% 단위, `AudioListener.volume`에 즉시 적용.
- 대사 출력 속도: 0.5~2배, 실제 `Typewriter`에 적용. 별도 예시 문장으로 미리보기.
- 대사 즉시 출력: 다음 대사부터 전체 문장 표시.
- 마우스 버튼, 위/아래 항목 이동, 좌/우 조절, Enter/Space 선택, Esc 닫기. 닫는 프레임의 입력은 타이틀에 중복 전달하지 않는다.
- 설정은 `Live49.*` PlayerPrefs 키로 보관하며 닫을 때 저장한다. 게임 진행 저장과 별개다.

CLI 검증: `AgentScripts/VerifySettingsUI.cs`의 `VerifySettingsUI.Run`은 플레이 모드에서 타이틀의 포인터 클릭 이벤트, 음량 반영, 속도·즉시 출력 변경, 닫기·설정 보관을 확인하고 원래 값을 복원한다. 컴파일 오류 없음과 1920×1080 게임 캡처를 확인했다. 실제 하드웨어 키보드 입력 및 독립 빌드 재실행 검증은 아직 수행하지 않았다.
