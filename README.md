# DemoGame3

이 저장소는 같은 Git 저장소 안에서 Unity 프로젝트를 관리합니다.

## 프로젝트

- `LIVE49/`: 기존 Live49 원래 프로젝트입니다. 이전 루트 제작 자료와 기존 Unity 프로젝트를 이 폴더 안으로 모았습니다.
- `demo5/`: Unity 6000.6.0f1로 새로 만든 빈 2D 프로젝트입니다. 이후 신규 실험과 재구성 작업의 시작점으로 사용합니다.
- `demo6/`: Unity 6000.6.0f1의 Universal 2D 템플릿으로 만든 새 프로젝트입니다. 기본 씬은 `Assets/Scenes/Main.unity`입니다.

Unity Hub나 Unity CLI에서 프로젝트를 열 때는 저장소 루트가 아니라 `LIVE49/`, `demo5/`, `demo6/` 중 작업할 프로젝트를 선택합니다.

## Live49 자료

Live49에서 쓰던 기획, 원화, UI 시안, 도구, 제작 이미지 묶음은 모두 `LIVE49/` 내부에 있습니다.

- `LIVE49/docs/`: 기획, 콘티, 대사, 제작 관리 문서
- `LIVE49/art/`: 원화 원본과 검수 이미지
- `LIVE49/design/`: UI 시안, 실행 데이터, 브라우저 검토판
- `LIVE49/tools/`: 제작·검증 스크립트
- `LIVE49/게임제작/`: 인게임 이미지, 참고용 이미지, 편집 원본 정리본

자세한 시작점은 `LIVE49/docs/00-제작관리/NEXT-UNITY-WORK.ko.md`를 확인합니다. 이미지·PSD·폰트는 Git LFS를 사용하므로 새 환경에서는 `git lfs pull`을 실행합니다.

## Git 기준

Unity가 생성하는 `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, `Build/`, `Builds/` 등은 Git에 올리지 않습니다. 실제 프로젝트 파일인 `Assets/`, `Packages/`, `ProjectSettings/`는 각 Unity 프로젝트 폴더 안에서 추적합니다.
