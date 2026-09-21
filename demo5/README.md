# demo5 — Night Expedition

> 그래픽 최신 기준: `아트/승인시안/폐상가-보드게임-원본.png`를 그대로 따른다. 아래 쿼터뷰 계획은 사용자가 정정했으므로 폐기한다. 현재 코드의 마름모 보드는 승인 그래픽이 아니다. 이후 실행 파일 빌드는 생략하고 Unity Editor에서 작업한다.

Unity 6.6 (`6000.6.0f1`)로 만든 야간 수색·턴제 전투 첫 프로토타입.

## 플레이

- Windows: `Builds/Windows/Demo5.exe` 실행. 실행 파일 옆 Data, MonoBleedingEdge, DLL 폴더/파일이 함께 있어야 한다. 빌드 산출물은 Git 제외.
- Unity: `Assets/Scenes/NightExpedition.unity`를 열고 Play.
- 원정 시작 → 오른쪽 대원 선택 → 하단 행동 선택 → 보드 칸 클릭.
- 방어·철수는 행동 버튼을 누르면 즉시 실행. 적 턴 진행으로 두 대원의 AP를 회복한다.
- 두 선반을 탐색하고 두 대원이 왼쪽 아래 출구로 돌아오면 임무 성공. 조기 철수도 가능.

## 반영된 방향

말 이동을 사용하는 소규모 턴제 전투, 탐색 소음과 증원, 조명 소진, 미끼, 명중/빗나감 확률. 엄폐는 드럼통·박스 옆에서 상대 사격 명중률을 20%p 줄이는 간단한 효과다. 감염자 근접 공격에는 엄폐가 적용되지 않는다.

현재는 하나의 임무를 기능 검증하는 단계다. 배경·토큰은 임시 도형이며 거점/다중 장소/저장/최종 원화/효과음은 아직 없다. 이름과 역할도 임시다. LIVE49 자산을 사용하지 않는다.

최신 확정 방향: 모험가 2명 선택 → 정착지 선택 → 정착지 생활과 외부 원정의 순환. 화면은 승인된 쿼터뷰 보드/디오라마 구도로 개발한다. 걷기·공격 프레임 애니메이션은 만들지 않으며, 고정된 말의 위치 변경과 짧은 타격·명중 결과 표시를 사용한다. 이 시작 흐름과 쿼터뷰는 다음 구현 대상이며 현재 실행 파일에는 아직 적용되지 않았다.

## 구조

- `Assets/Scripts/NightRun`: 상태·규칙과 uGUI 화면 코드.
- `Assets/Prefabs/NightRun`: Expedition, Panel, ActionButton, BoardTile, SurvivorToken, PropToken.
- `AgentScripts`: Unity Pipeline에서 실행하는 씬 생성·테스트·Windows 빌드 스크립트.
- `기획/개발방향과-첫-프로토타입.md`: 합의된 방향, 세부 규칙, 후속 작업.
- `기획/프로토타입-검증.md`: 이번 검증 결과와 범위.

UI는 공통 프리팹 기반으로 런타임 조립한다. 현재 화면 위치·색·글자는 NightRunView 코드에서 지정하며, 완전한 Inspector 편집형 UI는 후속 개선 대상이다. 한글 폰트는 실행 OS에 의존한다.

## 재현용 명령

실행 중인 demo5 Editor와 Pipeline 패키지가 필요하다. 프로젝트 경로는 로컬 위치에 맞게 바꾼다.

```powershell
unity command run_script --file AgentScripts/VerifyPrototype.cs --entry VerifyPrototype.Rules --project-path D:\Demo3\demo5
unity command editor_play --project-path D:\Demo3\demo5
unity command run_script --file AgentScripts/VerifyPrototype.cs --entry VerifyPrototype.PlayThrough --project-path D:\Demo3\demo5
unity command editor_stop --project-path D:\Demo3\demo5
unity command run_script --file AgentScripts/BuildPlayer.cs --entry BuildPlayer.Windows --project-path D:\Demo3\demo5
```

긴 빌드는 Pipeline 응답 제한 시간을 넘겨도 에디터에서 계속될 수 있다. 같은 빌드를 즉시 재시도하지 말고 `BuildReport.GetLatestReport().summary.result`와 Editor 로그를 확인한다.
