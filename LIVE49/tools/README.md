# 도구 사용 안내

도구 파일은 실행 시 상대 경로가 저장소 루트를 기준으로 잡혀 있어 현재 경로를 유지합니다. 기능별로 아래 순서로 찾습니다. 파일명 접두어는 역할을 나타냅니다.

| 목적 | 사용할 도구 |
| --- | --- |
| 탐색 시안 실행 | `serve-search-minigame-v2.cjs` |
| 요리 시안 실행 | `serve-cooking-minigame.cjs` |
| 시작 연출 실행 | `serve-opening-motion.cjs` |
| 첫날 시안 실행 | `serve-day01-review.cjs` |
| 탐색·요리 규칙 검증 | `test-search-minigame-v2.mjs`, `test-cooking-minigame.mjs` |
| C0 흐름 검증 | `test-ch00-continuation.mjs`, `test-speaker-identity.mjs` |
| 원화 최신 보완 검증 | `validate-art-polish-v1.py` |
| 원화 전체 감사 | `audit-current-art-20260911.py`, `audit-art-extra-proofs-20260911.py` |

`prepare-*`, `build-*`, `integrate-*`, `apply-*`는 생성·수정 도구이므로 검증 명령처럼 일괄 실행하지 않습니다. `test-*`, `check-*`, `audit-*`, `validate-*`는 개별 스크립트의 범위를 확인하고 실행합니다. 날짜·버전이 있는 것은 해당 작업 당시의 검증일 수 있습니다.

`build-dialogue-editing-book.py`는 최초 생성용입니다. 사용자가 수정한 대사집을 재생성하지 않습니다. 현재 본편에서 제외한 화덕 시안의 도구와 이전 탐색 도구는 비교 이력입니다.
