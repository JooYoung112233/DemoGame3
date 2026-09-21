# 조사 하이라이트 리소스

기존 배경을 수정하지 않고 물체의 조사 영역을 별도 도형으로 표시한다. 이름과 행동 안내는 별도 UI 텍스트다.

## 파일

- mart-drawer-highlight.svg: 걸린 서랍
- mart-food-highlight.svg: 식품 선반
- mart-medicine-highlight.svg: 구급 물품장
- mart-pencils-highlight.svg: 문구 선반
- camper-stove-highlight.svg: 작은 주방
- camper-sketchbook-highlight.svg: 소이의 스케치북

6개 SVG는 1672×941 배경에 맞춘 투명 벡터 오버레이다. 물체 내부의 정밀 픽셀 실루엣 마스크가 아니라 수동으로 지정한 조사 영역의 외곽이다. 소품 원화에 하이라이트나 글자를 굽지 않았다.

위치와 표시 이름은 [hotspots-v1.json](../../../design/interaction/hotspots-v1.json)에 저장했다. 왼쪽 위가 원점이며, 최종 배경이 바뀌면 좌표도 다시 검증해야 한다. Unity에서는 이 좌표를 화면 UI/클릭 영역에 옮기거나 별도 마스크로 변환해야 한다. 이 파일만으로 Unity 구현이 완료되는 것은 아니다.

미리보기에서는 대기 상태의 작은 점, 커서/키보드 포커스의 금빛 외곽과 이름, 선택 상태 유지, 완료 상태의 이름 변경을 사용한다. 발견 전에는 장소 이름을 표시하고 발견 후에는 물건 이름을 표시한다.
