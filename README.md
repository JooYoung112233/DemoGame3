# Live49 · 마지막 여름

**이미지 구분:** [게임제작 폴더](게임제작/README.md)에서 인게임 레이어 / 참고용 합성 / 편집 원본을 따로 확인합니다.

**자료 찾기:** [문서 폴더](docs/README.md) · [원화 찾기](art/README.md) · [실행 데이터·시안](design/README.md) · [도구 사용](tools/README.md)

**다음 제작 시작점:** [Unity 전환 인계·승인 방향·남은 작업](docs/00-제작관리/NEXT-UNITY-WORK.ko.md). 기획과 독립 시안 확장은 여기서 멈추고 Unity 실제 플레이에서 다듬습니다. 이미지·PSD·폰트는 Git LFS를 사용하므로 새 환경에서 `git lfs pull`을 실행하세요.

집필·구상: [사용자 수정용 대사집](docs/06-대사/DIALOGUE-BOOK-CH00-01.ko.md) · [미니게임·음악·효과음 공동 구상](docs/07-음악과미니게임/MINIGAME-AND-AUDIO-WORKSHOP.ko.md). 대사집의 수정 칸에서 문장을 다듬고, 음악은 은은한 생활감과 고유 주제 선율을 중심으로 검토합니다.

2026-09-11 최신 원화: [챕터 0~1 전체 보완 기록](docs/05-원화/ART-POLISH-2026-09-11.ko.md) · [사용 장면·PSD·레이어](art/chapter00-01/art-polish-v1/scene-manifest.json) · [대표 장면 미리보기](art/chapter00-01/art-polish-v1/review/overview.jpg). 보완 원본 17개·장면 60개를 정리했으며, 최종 확대용 원본 해상도 한계는 별도 기록합니다.

다음 검토: [챕터 0~첫 주 추가 작업·전수 검수 결과](docs/00-제작관리/CH00-WEEK01-OVERNIGHT-REVIEW.ko.md).

최신: [챕터 1 첫 주 원화 전체 검수](docs/05-원화/CH01-WEEK01-ART-REVIEW.ko.md) · [25개 사건 원화 연결표](design/chapter01/week01-art-coverage-v1.json) · [생활~출발 콘티](docs/03-콘티/챕터1/CH01-E15-25-LIVING-DEPARTURE-STORYBOARD.ko.md)

따뜻하고 쓸쓸한 아포칼립스 속에서 수혁과 소이가 여행하는 2D 게임의 기획·원화 준비 저장소입니다.

현재는 원화와 제작 기준을 정리하는 단계입니다. 목표 엔진은 Unity 6.6, 렌더링 구성은 URP 2D Renderer이며 Unity 프로젝트는 아직 만들지 않았습니다.

사용자 지정 [원화 원본·납품 규칙](docs/05-원화/ART-ASSET-RULES.ko.md): 이름표 등 UI는 Unity 단계에서 다루고, 원화는 수정·확대를 고려한 큰 원본과 분리 레이어를 우선합니다. 가능하면 실제 레이어가 있는 PSD도 함께 준비합니다.

## 첫 플레이 범위

챕터 0은 [개연성과 볼륨을 보강한 연결 초안](docs/03-콘티/챕터0/CH00-CONTINUITY-AND-VOLUME.ko.md)을 추가했습니다. `node tools/serve-opening-motion.cjs 8793` 실행 후 `http://127.0.0.1:8793`에서 시작하면 기존 회상·현재 대화 뒤 첫 선택부터 취침·1일 차 아침까지 이어 볼 수 있습니다. 신규 대사는 검토안이며 원화·음향·Unity 최종 구현 완료와 구분합니다.

최신 [1일 차 낮 콘티](docs/03-콘티/챕터1/CH01-DAY01-NOON-STORYBOARD.ko.md)는 물 필요 → 지도에서 편의점 선택 → 탐색 방식·확률 선택 → 계산대 대피 쪽지 → 가게 바로 앞 캠핑카 복귀 → 물·식량 분배 → 고장 난 요리대 암시 → 소이의 색연필 부탁으로 이어집니다. 책은 챕터 0에서 챙긴 것을 쓰며 색연필 선확보를 인정합니다. 일반 대화는 배경 블러·좌우 초상·하단 대화창을 사용합니다.

전체 49일은 내부 개발용 5챕터로 나누는 초안을 작성했습니다. 현재 목표는 [챕터 0부터 챕터 1 첫 주까지 상세 콘티](docs/00-제작관리/CH00-WEEK01-STORYBOARD-PROGRESS.ko.md)이며, 우선 장면별 원화·UI를 검토합니다. [낮 UI 검토 파일](design/ui/day01-v1/index.html)은 `node tools/serve-day01-review.cjs`로 연 뒤 `http://127.0.0.1:8792`에서 볼 수 있습니다. 고정 화면 사례를 연결한 검토판이며 실제 미니게임·재고·Unity 실행 구현은 아닙니다. 걷기 애니메이션과 자유 이동은 현재 제작 범위에 포함하지 않습니다.

## 시각 기준

현재 합성 배경과 행동 자세는 [챕터 1 장면 수정 v3](art/chapter01/revision-v3/README.ko.md)를 기준으로 합니다. 아래 캠핑카 v1~v3는 별도로 보존한 초기 콘셉트 버전입니다.

높은 시점에서 가로로 보이는 아담한 캠핑카, 따뜻한 실내와 차가운 폐허의 대비를 유지합니다. 원본의 생활감과 은은한 입체감을 살리면서 불규칙한 잔질감을 줄입니다.

![캠핑카 분위기 기준](art/concepts/camper-interior-v2.png)

| 파일 | 상태 |
| --- | --- |
| [캠핑카 v2](art/concepts/camper-interior-v2.png) | 현재 선호하는 분위기와 구도 기준. 최종 게임 자산은 아님 |
| [캠핑카 v3](art/concepts/camper-interior-v3-base.png) | 조명 분리와 질감 단순화 실험. 최종 화풍으로 채택하지 않음 |
| [캠핑카 v1](art/concepts/camper-interior-v1.png) | 초기 탐색안. 원본과 구도가 달라 보관용 |

## 제작 문서

- [챕터 1 연속 제작 현황](docs/00-제작관리/CHAPTER01-PRODUCTION.ko.md)
- [챕터 1 전체 점검 결과](docs/00-제작관리/CHAPTER01-REVIEW.ko.md)
- [챕터 1 통합 리소스·미리보기 사용법](art/chapter01/README.ko.md)
- [챕터 1 사건별 리소스 연결표](design/chapter01/RESOURCE-CUES.ko.md)
- [챕터 1 고정 베이스·교체 레이어 제작](art/chapter01/layers/README.ko.md)
- [최신 상호작용 방향: 탐색 위험·캠핑카 확장·편의점](docs/08-UI연출/INTERACTION-DIRECTION.ko.md)
- [조사 하이라이트·이름·미니게임 시제품](design/interaction/README.ko.md)
- [첫 3일 리소스 목록과 1차 제작물](docs/05-원화/RESOURCE-LIST-CH01.ko.md)
- [챕터별 제작 계획 v0.1](docs/00-제작관리/DEVELOPMENT-PLAN.ko.md)
- [원화 공간 일관성 기준](art/CONTINUITY.ko.md)
- [캠핑카 내부 첫 확정 후보](art/scenes/01-camper/README.ko.md)
- [원화 제작 목록](docs/05-원화/ART-PLAN.ko.md)
- [게임 적용 기준](art/concepts/IMPLEMENTATION-NOTES.ko.md)

생성 원화 옆의 `.prompt.txt`에 내장 이미지 생성 도구에서 사용한 프롬프트가 있습니다. 챕터 1은 공통 베이스 3개와 분리 PNG를 조합한 6개 장면·33개 상태의 1차 미리보기까지 연결했습니다. 배치·가림과 작은 화면의 상태 전환을 검사했으며, 확대 선명도 보정·최종 화풍 정리·Unity 적용은 후속 작업입니다. 고정 배경 조명에 포즈를 맞추며 실시간 캐릭터 조명은 필수 범위가 아닙니다.

챕터 1은 [첫날 낮 서사 검토판](design/ui/day01-v1/story.html)과 [첫 주 서사 연결표](docs/03-콘티/챕터1/CH01-FIRST-WEEK-STORY.ko.md)로 이어집니다. 기존 UI 이미지 위에 사건 목적·연출 의도와 조기 귀환, 쪽지 열람, 색연필 선확보 분기를 연결했습니다. 대사 퇴고와 2~7일 상세 컷은 후속입니다.

챕터 1의 [이벤트 25개·퀘스트 21개 연결표](docs/03-콘티/챕터1/CHAPTER01-EVENT-QUEST-DRAFT.ko.md)와 [장소별 배경 제작표](docs/05-원화/CH01-LOCATION-ART-PLAN.ko.md)를 정리했습니다. 개발 참고 원본은 [event-quest-catalog-v2.json](design/chapter01/event-quest-catalog-v2.json)이며 아직 Unity 실행 데이터는 아닙니다.

L3 [잡화점 빈 베이스 v1](art/chapter01/general-store-v1/README.ko.md)을 제작했습니다. 1672×941 생성 원본과 가림용 3레이어 PSD를 보관했으며 4K 목표는 미달입니다. 색연필·천·메모 등 획득 소품은 후속 분리 제작합니다.

[E05 색연필 발견 콘티](docs/03-콘티/챕터1/CH01-E05-PENCILS-DISCOVERY.ko.md)와 [투명 소품·획득 전후 원화](art/chapter01/colored-pencils-v1/README.ko.md)를 추가했습니다. 고정 잡화점 배경에 별도 색연필과 그림자를 조합하며, 큰 소품 PSD와 배치 PSD를 분리 보관합니다.

[E05 전달~첫 그림 원화](art/chapter01/first-drawing-v2/README.ko.md)와 [상세 콘티](docs/03-콘티/챕터1/CH01-E06-FIRST-DRAWING.ko.md)를 추가했습니다. [주유소·수리점·쉼터·고갯길 배경 비교](art/chapter01/location-review-v1/new-location-bases-contact.jpg)도 보관합니다. 신규 배경 다섯 곳은 모두 1672×941 빈 베이스 시안이며 최종 확대 원본·NPC/차량/사건 소품은 후속입니다.

- [세진 부탁부터 첫 촬영까지 상세 콘티](docs/03-콘티/챕터1/CH01-E07-11-PHOTOGRAPHER-STORYBOARD.ko.md) · [공구 가방·카메라·필름 원본/PSD](art/chapter01/photographer-props-v1/README.ko.md)

- [별똥이 개별 원화·돌봄 콘티](art/chapter01/dog-v2/README.ko.md) · [세진·차량 시안](art/chapter01/sejin-v1/README.ko.md) · [첫 촬영·사진·앨범 원화](art/chapter01/first-photo-v1/README.ko.md)
