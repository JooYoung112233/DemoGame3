# 챕터 0~1 배경·인물 조화 검토

> **후속 승인:** 사용자가 [주택 통합 시안](../../art/chapter00-01/scene-integration-v1/review/house-integrated.png)을 보고 “좋다”라고 승인했다. 후속 작업은 인물의 크기·시점·주변광·접지를 실제 장면에서 함께 맞추는 방향을 따른다. 독립 캐릭터/그림자 분리와 다른 장면 적용은 아직 후속 작업이다.

> **최신 피드백:** 아래 배경 조화 수정 후에도 사용자가 인물과 배경이 동떨어져 있다고 지적했다. 8종 배경의 제작 완료와 합성 파일 검증을 시각적 조화 해결로 보고하지 않는다. [주택 장면의 인물·공간 통합 시안](../../art/chapter00-01/scene-integration-v1/README.ko.md)에서 크기·조명·시점·접지를 함께 재검토했다. 새 시안은 배경을 포함한 국소 재작화로, 독립 스프라이트 교체본이나 전 장소 적용 완료가 아니다.

> **후속 완료 · 2026-09-11:** “다른 장소들도 작업해줘” 요청에 따라 주유소·잡화점·수리점·주택·강변·언덕길의 배경 6종과 이벤트 합성 17개를 추가했다. [나머지 장소 수정본·PNG·PSD·프롬프트](../../art/chapter00-01/background-harmony-v2/README.ko.md), [정확한 배경/장면 매핑](../../art/chapter00-01/background-harmony-v2/manifest.json)을 참조한다. 앞선 2곳과 합쳐 총 8종의 배경 수정본이 있다. 아래는 먼저 제작한 캠핑카·편의점 기록이다. 전체 배경 원본은 여전히 1672×941이며 Unity 활성 참조 일괄 교체나 최종 해상도 충족을 뜻하지 않는다.

2026-09-11 사용자 요청으로 캠핑카와 편의점 배경의 강한 윤곽·디지털 질감과 부드러운 인물 원화 사이 차이를 조정했다. 기존 인물 설정, 자세, 가구 배치, 소품 좌표를 기준으로 두고 배경의 채색 마감을 바꿨다.

현재 별도 검토 자산은 [background-harmony-v1](../../art/chapter00-01/background-harmony-v1/README.ko.md)에 있다. **캠핑카 v1 / 편의점 v2**가 이번 검토본이며 기존 활성 콘티를 일괄 교체한 상태가 아니다. 편의점 v1은 과도한 밝기 때문에 이력으로 남겼다.

| 연출·장면 | 합성 PNG | 실제 레이어 PSD |
| --- | --- | --- |
| C0 현재 대화 | [PNG](../../art/chapter00-01/background-harmony-v1/review/C0-current.png) | [PSD](../../art/chapter00-01/background-harmony-v1/psd/C0-current.psd) |
| C0 정전 대화 | [PNG](../../art/chapter00-01/background-harmony-v1/review/C0-outage.png) | [PSD](../../art/chapter00-01/background-harmony-v1/psd/C0-outage.psd) |
| E06 수혁이 그림, 소이가 지켜봄 | [PNG](../../art/chapter00-01/background-harmony-v1/review/E06-drawing.png) | [PSD](../../art/chapter00-01/background-harmony-v1/psd/E06-drawing.psd) |
| E06 엔딩 대비본 · 초반 표시 금지 | [PNG](../../art/chapter00-01/background-harmony-v1/review/E06-drawing-truth.png) | [PSD](../../art/chapter00-01/background-harmony-v1/psd/E06-drawing-truth.psd) |
| 편의점 문구 선반 조사 | [PNG](../../art/chapter00-01/background-harmony-v1/review/L1-stationery.png) | [PSD](../../art/chapter00-01/background-harmony-v1/psd/L1-stationery.psd) |
| 편의점 색연필 회수 후 | [PNG](../../art/chapter00-01/background-harmony-v1/review/L1-pencils-taken.png) | [PSD](../../art/chapter00-01/background-harmony-v1/psd/L1-pencils-taken.psd) |

원본 배경은 1672×941로 3840px 제작 목표에 미달한다. 확대 보간을 새 디테일로 취급하지 않는다. 가구는 평면 배경 안에 있으며 실제 분리한 인물·소품·가림만 레이어 편집이 가능하다. 기존 전원 상태의 조명 비율을 새 배경에 적용해 정전 파생본을 만들었다. 원화 비교, 소품 좌표, 알파·PSD 재읽기 결과는 묶음의 README와 manifest/validation 파일에서 확인한다.
