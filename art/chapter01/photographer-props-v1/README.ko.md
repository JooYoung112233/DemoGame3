# 사진기사 사건 소품 v1

> 후속 자세·소품과 현재 제작 범위는 [completion-v1](../completion-v1/README.ko.md) 및 [전체 검수](../../../docs/05-원화/CH01-WEEK01-ART-REVIEW.ko.md)를 따른다. 이 묶음 자체의 제작 범위와 기존 원본은 보존한다.

E08 공구 가방 회수 → E09 카메라·필름 인수 → E11 첫 촬영에 쓰는 원화 시안. [상세 콘티](../../../docs/03-콘티/챕터1/CH01-E07-11-PHOTOGRAPHER-STORYBOARD.ko.md)와 함께 사용한다.

| 자산 | 생성 원본 | 투명 PNG | 실제 캔버스 |
| --- | --- | --- | --- |
| 공구 가방 | [원본](sources/toolbag-source.png) | [분리본](layers/toolbag-native.png) | 1774×887 |
| 즉석카메라 | [원본](sources/camera-source.png) | [분리본](layers/camera-native.png) | 1254×1254 |
| 필름 포장 | [원본](sources/film-source.png) | [분리본](layers/film-native.png) | 1536×1024 |

각 프롬프트는 `sources/*.prompt.txt`에 보관했다. 카메라와 가방은 생성 원본 알파를 유지했다. 필름은 외곽의 밝은 번짐만 폴리곤 알파로 잘랐으며 RGB를 다시 그리지 않았다. 표기 치수는 여백을 포함한 캔버스이고 실제 물체의 범위는 [manifest](manifest.json)를 따른다.

[원본 크기 PSD](psd/photographer-props-native.psd)는 2790×2141 캔버스에 세 물체를 각각 독립 레이어로 보관한다. 작은 시트에서 추출한 원본이 아니며 각 PNG의 픽셀 크기를 그대로 유지했다. 카메라 내부 부품이나 가방 손잡이가 따로 분리된 것은 아니다.

## 수리점 배치

[회수 전](review/L4-toolbag-available.png) · [회수 후](review/L4-toolbag-collected.png) · [접지 원본 크기 확대 보기](review/toolbag-contact-native.png) · [배치 PSD](psd/L4-toolbag-placement.psd)

고정 L4 배경에 가방을 `(742,306,130,49)`, 접촉 그림자를 `(737,348,140,14)`로 배치했다. 좌표 기준은 1672×941 원본이다. 가방은 선반 바닥에 놓이며 회수하면 가방과 그림자만 함께 숨긴다. 배치 PSD는 배경·그림자·가방 3레이어다. 실제 Unity 좌표 변환·선택 영역은 개발 단계에 맞춘다.

갈색 바탕 알파 검수와 선반 접지 합성을 확인했다. [검증 기록](validation.json)은 PSD의 레이어·합성 및 회수 전후 픽셀 범위를 검사한 결과다. Photoshop 앱과 Unity에서의 확인은 아직 하지 않았다.

세진 인물·차량, 건네고 받는 자세, 촬영 자세, 첫 사진·앨범 상태는 미제작이다. 카메라 화면을 이 원화만으로 완성했다고 취급하지 않는다.
