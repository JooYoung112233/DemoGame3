# 세진과 정비 차량 · 기본 시안 v1

> 후속 자세·소품과 현재 제작 범위는 [completion-v1](../completion-v1/README.ko.md) 및 [전체 검수](../../../docs/05-원화/CH01-WEEK01-ART-REVIEW.ko.md)를 따른다. 이 묶음 자체의 제작 범위와 기존 원본은 보존한다.

내장 image_gen으로 세진 기본 서기와 별도 차량을 제작했다. **외형은 검토용 제안이며 확정 인물 설정으로 취급하지 않는다.** 수혁과 너무 닮았던 첫 후보는 `sources/sejin-standing-candidate-v1.png`에 보존하고 사용하지 않는다. 짧은 머리로 수정한 v2를 현재 합성에 사용했다.

- [세진 PNG](layers/sejin-standing-native.png) · [PSD](psd/sejin-standing-native.psd): 1086×1448 원본, 실제 인물 범위 553×1384. 목표 인물 높이 1536~2048에 미달한다. 가짜 체크무늬를 알파로 제거했으며 RGB는 유지했다.
- [정비 차량 PNG](layers/van-native.png) · [PSD](psd/van-native.psd): 1448×1086 원본, 실제 차량 범위 1399×970. 생성 알파 유지. 보닛·바퀴가 각각 움직이는 레이어는 아니다.
- [주유소 합성](review/L2-sejin-meeting.png) · [접지 확인](review/meeting-contact-native.png) · [장면 PSD](psd/L2-sejin-meeting.psd).

고정 주유소 베이스에 차량·차량과 바퀴 접촉 그림자·세진·발 접촉 그림자·작업대의 기존 카메라를 별도 레이어로 놓았다. 배경 자체를 변경하지 않았다. 카메라는 기존 사진기사 소품 PNG를 작게 배치한 것이며 그 축소본을 원본으로 취급하지 않는다.

[manifest](manifest.json)에 좌표와 원본 범위를 기록했다. [검증 기록](../../../design/chapter01/sejin-first-photo-validation.json)은 파일 레이어와 합성 검사다. 프롬프트는 `sources/*.prompt.txt`, 생성 원본은 `sources/*.png`에 남겼다.

짧은 회색기 머리·올리브 겉옷과 작은 승합차는 시각적 제안이지 나이·차종·과거사의 확정이 아니다. 대사별 표정, 대화용 큰 초상, 정비 동작과 카메라 건네기 자세는 후속이다. 이름 공개는 실제 소개/호명 시점에 Unity에서 처리한다.
