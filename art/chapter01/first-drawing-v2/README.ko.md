# 색연필 전달과 첫 그림 · 원화 v2

내장 image_gen으로 **수혁 전달 자세 1개와 바다 그림 1개**를 제작했다. 생성 원본과 프롬프트는 [sources](sources)에 보관한다. [상세 콘티](../../../docs/03-콘티/챕터1/CH01-E06-FIRST-DRAWING.ko.md), [자산 manifest](manifest.json), [파일 검증](validation.json)을 함께 사용한다.

| 장면 | 전체 합성 | 배치 PSD |
| --- | --- | --- |
| 색연필을 놓은 직후 | [PNG](review/01-delivery.png) | [PSD](psd/01-delivery-placement.psd) |
| 첫 그림 준비 | [PNG](review/02-ready.png) | [PSD](psd/02-ready-placement.psd) |
| 그림 시작 | [PNG](review/03-started.png) | [PSD](psd/03-started-placement.psd) |
| 완성 그림 확인 | [PNG](review/04-complete.png) | [PSD](psd/04-complete-placement.psd) |

[수혁 원본 PSD](psd/suhyeok-place-pencils-native.psd) · [그림 원본 PSD](psd/sea-drawing-states-native.psd) · [기존 책+그림 상태 PSD](psd/book-page-states-native.psd)

실제 원본: 수혁 1086×1448, 그림 1254×1254, 기존 책 1536×1024, 배경 1672×941. 인물·배경은 목표 해상도 미달을 유지하며 단순 확대본으로 대체하지 않았다. 장면 배치 PSD의 작은 인물·책은 위 원본 PSD/PNG와 별도로 보관한다.

수혁 생성 결과의 체크무늬는 사용자 승인 범위에서 알파만 분리했고 원래 RGB를 유지했다. 그림은 실제 생성 알파를 유지한다. 손은 인물을 식탁 위에서 가리기 위한 복사 레이어로, 가려진 몸을 재구성한 전신 원화는 아니다. 책의 낱장·개별 연필도 독립적으로 움직이는 구조가 아니다.

페이지 그림과 전달 자세 외의 조용히 앉기·소이 그리기 자세는 기존 시안을 재사용한다. 바다는 앞으로 가 보고 싶은 곳이라는 내용의 초안이며 대사집 확정 전이다. 큰 책 합성은 원화 검수용으로, 인게임 팝업 연출 확정이 아니다.

PSD의 합성·채널·레이어·위치와 식탁 바깥 배경 유지 여부를 파일 리더로 검사했다. Photoshop 앱이나 Unity에서 실행 검증한 것은 아니다.
