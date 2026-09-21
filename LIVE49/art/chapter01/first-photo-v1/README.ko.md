# 첫 촬영과 앨범 원화 · 사진 화풍 v2

> 후속 자세·소품과 현재 제작 범위는 [completion-v1](../completion-v1/README.ko.md) 및 [전체 검수](../../../docs/05-원화/CH01-WEEK01-ART-REVIEW.ko.md)를 따른다. 이 묶음 자체의 제작 범위와 기존 원본은 보존한다.

E11/Q10을 위한 원화 시안. 수혁 촬영 자세, 풍경 사진, 빈 앨범을 내장 image_gen으로 각각 생성했다. 프롬프트·원본은 `sources/`에 보관한다. [manifest](manifest.json)에 치수·배치·사용 상태를 기록했다.

| 원화 | 원본 | 분리/납품 |
| --- | --- | --- |
| 수혁 촬영 자세 | 1086×1448, 인물 범위 529×1398 | [PNG](layers/suhyeok-shoot-native.png) · [PSD](psd/suhyeok-shoot-native.psd) |
| 첫 강변 사진 내용 | 1448×1086 | [PNG](layers/riverside-photo-native.png) · [PSD](psd/riverside-photo-native.psd) |
| 빈 앨범 | 1448×1086, 물체 범위 1378×826 | [PNG](layers/album-blank-native.png) · [PSD](psd/album-blank-native.psd) |

촬영 자세는 기존 수혁의 머리·옷을 유지한 새로운 뒤쪽 3/4 자세다. 카메라와 두 손은 함께 그려졌고, 별도의 가림용 복사 레이어를 제공한다. 카메라를 지우면 가려진 손이 복원되는 관절 분리 원본은 아니다.

[강변 촬영 합성](review/L6-first-shoot.png) · [접지 확인](review/shoot-contact-native.png) · [장면 PSD](psd/L6-first-shoot.psd). 고정 L6 베이스에 촬영 자세와 두 발의 그림자를 얹었다.

[사진 등록 전](review/album-before-first-photo.png) · [첫 사진 등록 후](review/album-first-photo.png) · [상태 PSD](psd/album-states-native.psd). 빈 앨범과 사진 삽입 레이어가 분리되어 같은 앨범을 유지한 채 사진을 바꿀 수 있다. 삽입본은 474×356 검토용 축소본이며, 사진 내용의 1448×1086 원본을 별도로 보존한다. [사진 인화 시안](review/first-print-native.png)과 [내용·테두리 PSD](psd/first-print-native.psd)는 원본 사진을 축소하지 않고 간단한 종이색 테두리를 별도 합성했다. 테두리가 생성된 종이 디테일이라고 보고하지 않는다.

첫 사진은 같은 강변의 다리·물·식생만 담은 **구도 제안**이다. 사용자가 확정한 최종 사진이나 소이 존재의 객관적 증거가 아니다. 기존 가족사진은 변경하지 않았다. 빈 앨범은 검토용 원화이며 새 물건 획득 퀘스트·인물 소지품 설정을 자동으로 추가하지 않는다. UI 버튼·글자·상태 동작은 포함하지 않는다.

알파와 합성·PSD는 [파일 검사 기록](../../../design/chapter01/sejin-first-photo-validation.json)을 따른다. Photoshop 앱·Unity 검수는 후속이다. 인물 크기는 목표에 미달하고, 배경도 기존 1672×941이므로 확대 연출의 최종 해상도가 완성된 것은 아니다.

## 사진 화풍 수정 v2

사용자가 첫 사진 화풍을 수정해 달라고 요청하여 [새 생성 원본](sources/riverside-photo-style-source-v2.png)을 제작했다. 기존 다리·난간·강 구도를 유지하면서 수풀·바닥의 촘촘한 묘사를 줄이고 넓은 색면으로 정리한 검토안이다. 현재 사진/인화/앨범 PSD는 이 원본을 참조한다. [선택 기록](photo-selection.json), [프롬프트](sources/riverside-photo-style-source-v2.prompt.txt)에 변경 근거를 남겼다. 기존 원본과 `previous-style-v1/`의 앨범·PSD는 보존한다. 수정본에 대한 사용자 승인이 완료된 상태는 아니다.
