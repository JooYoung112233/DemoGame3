const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const model=require('./navigation-source.cjs');
const root=__dirname,artRoot=path.resolve(root,'../../아트/UI전체시안-v1');
const manifest=JSON.parse(fs.readFileSync(path.join(artRoot,'manifest.json'),'utf8'));
const ids=new Set(model.nodes.map(n=>n.id));
if(ids.size!==model.nodes.length)throw Error('Duplicate node ID');
if(new Set(model.routes.map(r=>r.id)).size!==model.routes.length)throw Error('Duplicate button ID');
for(const r of model.routes){if(!ids.has(r.from)||!ids.has(r.to))throw Error('Broken route '+r.id);for(const k of ['button','effect','time','guard','cancel','mode'])if(!r[k])throw Error('Missing '+k+' '+r.id);}
for(const n of model.nodes){
 if(n.art){const a=manifest.find(x=>x.id===n.art);if(!a||!fs.existsSync(path.join(artRoot,a.file)))throw Error('Missing art '+n.id);n.image='../../아트/UI전체시안-v1/'+a.file;}
 if(!['RETURN','QUIT'].includes(n.id)&&!model.routes.some(r=>r.from===n.id))throw Error('Dead end '+n.id);
}
for(const a of manifest)if(!model.nodes.some(n=>n.art===a.id))throw Error('Unmapped art '+a.id);
const diagrams=[
 ['전체 진행',`flowchart LR
 S00["20 시작"] -->|새 여정| S01["20 모험가 2명"]
 S01 -->|다음| S02["20 정착지"]
 S02 -->|여정 시작| H00["04 거점"]
 H00 -->|출입구| D00["02 목적지·원정대"]
 D00 -->|짐 꾸리기| D02["03 개인별 휴대품"]
 D02 -->|출발 확인| E00["탐험 · 현재 방"]
 E00 -->|출구에서 귀환| R00["12 귀환 정산"]
 R00 -->|입고 또는 개인 보관| H00
 E00 -->|위험 조우| E08["09 교전·회피"]
 E08 -->|교전| B00["10 분리 진형 전투"]
 B00 -->|결과 확인 · 생존| E00
 H00 -->|하루 마무리| H08["15 야간 배정"]
 H08 -->|밤 보내기 확인| H09["16 하루 결과"]
 H09 -->|확인| H00`],
 ['거점 오브젝트와 팝업',`flowchart TD
 H00["H00 거점"] -->|보관함| H01["H01 창고·개인 가방"]
 H00 -->|대원 카드| H02["H02 대원 상세"]
 H02 -->|가방| H01
 H02 -->|치료| H03["H03 생활 작업"]
 H00 -->|침대·조리대·정수시설| H03
 H00 -->|작업대| H04["H04 제작·수리"]
 H03 -->|담당자 선택| P05["P05 다인원 선택"]
 H04 -->|담당자 선택| P05
 P05 -->|확인 · 시간 미소모| Return["호출 작업 초안"]
 H03 -->|작업 시작 · 등록만| H00
 H04 -->|작업 시작 · 등록만| H00
 H00 -->|진행 작업| H05["H05 작업 목록"]
 H05 -->|중단| P02["P02 반환·소모 확인"]
 P02 -->|확인 또는 취소| H05
 H00 -->|시간 진행 확인| Advance["거점 작업 시간 갱신"]
 Advance --> H00`],
 ['원정 준비와 소유권',`flowchart LR
 H00["거점 출입구"] --> D00["D00 목적지"]
 D00 -->|원정대 선택| D01["D01 참가 대원"]
 D01 -->|짐 꾸리기| D02["D02 가방별 휴대품"]
 D02 -->|아이템 슬롯| P00["P00 아이템 상세"]
 P00 -->|이동·나누기| P01["P01 수량"]
 P01 -->|확인 · 실제 이전| D02
 D02 -->|출발 준비 완료| D03["D03 최종 확인"]
 D03 -->|취소 · 시간 미소모| D02
 D03 -->|출발| E10["E10 왕로 시간 1회"]
 E10 --> E00["E00 현재 방"]
 D02 -->|뒤로 · 옮긴 물품 유지| D01`],
 ['방 연결과 탐험 턴',`flowchart TD
 E00["E00 현재 방"] -->|지도| E01["E01 연결 지도"]
 E01 -->|인접 문 보기| E02["E02 문·이동 계획"]
 E00 -->|문 클릭| E02
 E02 -->|이동 배정 · 시간 미소모| E04["E04 이번 턴 계획"]
 E00 -->|수색물 클릭| E03["E03 방식·담당자·지원"]
 E03 -->|행동 배정 · 시간 미소모| E04
 E04 -->|턴 진행 · 1턴 확정| E05["E05 처리 잠금·검증·결과 저장"]
 E05 -->|긴급 조우 우선| E08["E08 조우"]
 E05 -->|조우 없고 발견물 있음| E06["E06 발견물 배분"]
 E05 -->|작업 계속 또는 이동 완료| E00
 E06 -->|완료 · 잔여물 남김 확인| E00
 E08 -->|해결 후 미배분 물품 있음| E06
 E08 -->|해결 후 물품 없음| E00`],
 ['전투와 귀환',`flowchart TD
 E08["09 조우"] -->|교전| B00["10 전투"]
 B00 -->|공격·위치 변경| B01["대상 미리보기"]
 B01 -->|취소 · 비용 없음| B00
 B01 -->|실행 · 비용 확정| B00
 B00 -->|아이템| B02["11 소지품·대상"]
 B02 -->|사용 · 비용 확정| B00
 B00 -->|승리·도주·패배| B03["전투 결과"]
 B03 -->|전원 행동 불능| B04["여정 종료"]
 B03 -->|생존 · 대기 발견물 우선| E00["현재 방"]
 E00 -->|출구 아닌 곳에서 귀환| E01["연결 지도 · 출구 안내"]
 E00 -->|출구에서 귀환| E09["귀환 확인"]
 E09 -->|귀환 출발| E10["귀로 시간 1회"]
 E10 --> R00["12 귀환 정산"]
 R00 -->|입고 또는 개인 보관| H00["거점"]`],
 ['방문자·야간·기록',`flowchart LR
 H00["거점"] -->|방문자| H06["대화·선택"]
 H06 -->|거래| H07["13 물물교환"]
 H07 -->|수량| P01["수량 초안"]
 P01 --> H07
 H07 -->|교환 제안| P02["최종 확인"]
 P02 -->|수락·재검증 후 이전| H07
 H00 -->|하루 마무리| H08["15 역할 배정"]
 H08 -->|밤 보내기 확인| H09["16 결과"]
 H09 -->|부상 알림| H02["14 대원 상세"]
 H09 -->|확인| H00
 H00 -->|기록| C00["17 기록·목표"]
 H06 -->|단서| C00`],
 ['공통 팝업과 복귀',`flowchart TD
 Source["호출 화면 · 맥락 저장"] -->|아이템| P00["상세"]
 P00 -->|이동| P01["수량"]
 P01 -->|취소| P00
 P01 -->|확인 성공| Return["호출 화면 갱신·복귀"]
 P00 -->|버리기| P02["확인창"]
 P02 -->|취소| P00
 P02 -->|확인·재검증| Return
 P01 -->|조건 변경·부족| P03["오류 · 비용 미차감"]
 P03 -->|닫기| P01
 Source -->|메뉴·Esc| C01["19 일시정지"]
 C01 -->|설정| C02["설정 초안"]
 C02 -->|적용 또는 취소| C01
 C01 -->|저장| C03["저장 슬롯"]
 C01 -->|불러오기| C04["불러오기 확인"]
 C04 -->|파일 검증 성공| Restore["저장 phase 복원"]
 C01 -->|계속하기| Return`]
];
const esc=s=>String(s).replaceAll('|',' / ').replaceAll('\n',' ');
let md=`# 인게임 화면·팝업·버튼 연결 명세\n\n작성: ${model.date} · 화면/상태 ${model.nodes.length}개 · 버튼/자동 전이 ${model.routes.length}개.\n\n[클릭형 연결도](index.html) · [상세 규칙·확인창 분기·검증 시나리오](규칙과검토사항.md)\n\n이 문서는 구현 전 명세다. 현재 이미지 문구보다 최신 턴제 방향을 우선한다. 연결도는 핵심 경로 요약이며 아래 버튼 표가 세부 연결을 설명한다. 조건에 따른 분기는 effect/guard와 규칙 문서를 함께 따른다.\n\n## 화면 목록\n\n| ID | 이름 | 종류 | 시안 | 상태 | 역할 |\n| --- | --- | --- | --- | --- | --- |\n`;
for(const n of model.nodes)md+=`| ${n.id} | ${n.title} | ${n.type} | ${n.image?'['+n.art+']('+encodeURI(n.image)+')':'—'} | ${n.status} | ${n.purpose} |\n`;
for(const [title,diagram] of diagrams)md+=`\n## ${title}\n\n\x60\x60\x60mermaid\n${diagram}\n\x60\x60\x60\n`;
for(const n of model.nodes){const rows=model.routes.filter(r=>r.from===n.id);if(!rows.length)continue;
 md+=`\n## ${n.id} · ${n.title}\n\n${n.purpose}\n\n| 버튼 ID | 누르는 대상 / 발생 조건 | 연결 | 동작 | 시간 | 조건 / 실패 | 취소 / 복귀 |\n| --- | --- | --- | --- | --- | --- | --- |\n`;
 for(const r of rows)md+=`| ${r.id} | ${esc(r.button)} | ${r.to} · ${r.mode} | ${esc(r.effect)} | ${esc(r.time)} | ${esc(r.guard)} | ${esc(r.cancel)} |\n`;
}
fs.writeFileSync(path.join(root,'버튼연결명세.md'),md);
fs.writeFileSync(path.join(root,'navigation.json'),JSON.stringify(model,null,2)+'\n');
const template=fs.readFileSync(path.join(root,'viewer-template.html'),'utf8');
const html=template.replace('/*NAVIGATION_DATA*/',JSON.stringify(model).replaceAll('<','\\u003c'));
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];new vm.Script(script);
if(html.includes('/*NAVIGATION_DATA*/'))throw Error('Unresolved placeholder');
fs.writeFileSync(path.join(root,'index.html'),html);
console.log(JSON.stringify({nodes:model.nodes.length,transitions:model.routes.length,artReferences:manifest.length,diagrams:diagrams.length,checks:['unique IDs','all route targets exist','all 20 art references exist','no unintended terminal node','viewer JavaScript syntax']}));
