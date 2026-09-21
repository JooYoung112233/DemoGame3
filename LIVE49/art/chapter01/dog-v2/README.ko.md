# 별똥이 개별 원화와 돌봄 상태 v2

> 후속 자세·소품과 현재 제작 범위는 [completion-v1](../completion-v1/README.ko.md) 및 [전체 검수](../../../docs/05-원화/CH01-WEEK01-ART-REVIEW.ko.md)를 따른다. 이 묶음 자체의 제작 범위와 기존 원본은 보존한다.

기존 [외형 시트](../dog/byeolddongi-poses-source-v1.png)의 갈색·크림색 무늬, 한쪽 접힌 귀, 짧게 말린 꼬리를 유지한 개별 신규 생성본이다. 기존 작은 추출본을 확대하지 않았다. 내장 image_gen 사용, 각 요청은 `sources/*.prompt.txt`에 보관했다.

| 원화 | 원본 캔버스 | 물체 범위(알파 16 이상) | 분리 PNG / PSD |
| --- | --- | --- | --- |
| 경계하며 서기 | 1254×1254 | 1017×1150 | [PNG](layers/alert-native.png) · [PSD](psd/alert-native.psd) |
| 안심하고 앉기 | 1254×1254 | 919×1151 | [PNG](layers/sit-native.png) · [PSD](psd/sit-native.psd) |
| 엎드려 쉬기 | 1448×1086 | 1313×683 | [PNG](layers/rest-native.png) · [PSD](psd/rest-native.psd) |
| 물이 든 법랑 그릇 | 1536×1024 | 1408×823 | [PNG](layers/bowl-native.png) · [PSD](psd/bowl-native.psd) |
| 접은 깔개 | 1448×1086 | 1363×777 | [PNG](layers/mat-native.png) · [PSD](psd/mat-native.psd) |

각 원본 PSD는 숨긴 생성 원본과 보이는 분리본을 보관한다. 강아지 관절·털 무늬까지 독립 레이어인 것은 아니다. 강아지는 생성 알파를 유지했고, 그릇 외곽 번짐과 깔개의 가짜 체크무늬는 사용자 허용 범위의 로컬 알파 처리로 제거했다. 원본 RGB와 원본 PNG는 유지한다. 물은 그릇 내부에 함께 그려져 있어 빈 그릇 상태는 별도 제작이 필요하다.

## 사건 상태

[모아 보기](review/story-states-contact.jpg) · [정확한 좌표·레이어 manifest](manifest.json) · [검증 기록](validation.json) · [E12~14 콘티](../../../docs/03-콘티/챕터1/CH01-E12-14-DOG-STORYBOARD.ko.md)

1. [L5 경계](review/01-L5-wary.png): 강아지와 접촉 그림자.
2. [L5 물그릇 놓기](review/02-L5-water-placed.png): 같은 강아지 자리 유지, 그릇과 그릇 그림자 추가.
3. [L5 가까이 앉기](review/03-L5-relaxed.png): 물그릇 유지, 다음 정지 자세로 교체. 걸어오는 애니메이션 아님.
4. [HUB 자리 마련](review/04-HUB-place-ready.png): 기존 러그 위 접은 깔개, 별도 접촉 그림자.
5. [HUB 휴식](review/05-HUB-rest.png): 같은 깔개 위 엎드린 강아지와 접촉 그림자.

각 상태의 실제 레이어 PSD는 동일 이름으로 `psd/`에 있다. 고정 배경을 다시 그리거나 기존 런타임 참조를 일괄 교체하지 않았다. 배경은 1672×941, 새 개별 강아지도 요청한 2048 수준보다 작은 원본이다. 최대 확대용 최종 마스터로 확정하지 않는다. 파일 리더로 PSD를 재검증했으며 Photoshop 앱·Unity 검수는 후속이다.
