# 색연필 묶음 · 분리 원화와 발견 장면

내장 image_gen으로 낡은 종이 케이스의 색연필 6개를 제작했다. [사용 프롬프트](colored-pencils-v1.prompt.txt)와 생성 원본을 보관한다. [E05 콘티](../../../docs/03-콘티/챕터1/CH01-E05-PENCILS-DISCOVERY.ko.md)에 장소·퀘스트·획득 전후를 연결했다.

- [실제 투명 PNG](colored-pencils-cutout-native-v1.png): 캔버스 1774×887, 물체 가시 영역 1687×579. 생성 결과가 체크무늬 RGB여서 사용자 승인 범위 안에서 원본 색상을 유지하고 알파만 분리했다.
- [원본 크기 소품 PSD](colored-pencils-native-v1.psd): 생성 원본 숨김 + 실제 투명 소품. 연필 6개와 케이스가 각각 분리된 구조는 아니다.
- [배치 PSD](L3-discovery-placement-v1.psd): 고정 배경·접촉 그림자·소품 3레이어. 소품 축소본은 장면 배치용이며 큰 편집 원본은 별도 보존한다.
- [발견 전 합성](review/L3-pencils-available.png) / [획득 후 합성](review/L3-pencils-collected.png). 배경을 재생성하지 않았고 색연필과 그림자만 전환한다.
- [좌표·파일 manifest](manifest.json), [파일 검사](validation.json), [어두운/밝은 바탕 알파 검수](review/alpha-check.jpg).

오른쪽 진열대 상판 안에 90×31px로 배치했다. 바깥 그림자는 원본 알파에서 만든 별도 접촉 그림자다. 색연필의 내부 음영은 소품에 포함된다. 큰 투명 PNG를 쓰면 색연필 자체 확대에는 여유가 있지만, 배경은 여전히 1672×941로 4K 원본 보강이 필요하다.

현재 제공한 것은 정지 상태 원화와 콘티다. 집는 손·캠핑카 전달 자세·실제 UI·하이라이트·획득 로직·소리는 후속이다. PSD 파일 재열기 검사는 했으며 Photoshop 앱/Unity에서 확인한 것은 아니다.
