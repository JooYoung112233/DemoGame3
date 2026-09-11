import fs from 'node:fs';
const root=new URL('../',import.meta.url),base='art/chapter00-01/narrative-art-v1/';
const read=p=>JSON.parse(fs.readFileSync(new URL(p,root),'utf8'));
const write=(p,v)=>fs.writeFileSync(new URL(p,root),JSON.stringify(v,null,2)+'\n');
const manifest=read(base+'manifest.json'),byId=new Map(manifest.states.map(s=>[s.id,s]));
const c0path='design/chapter00/continuation-v1/story.json',c0=read(c0path),baseline=read(base+'scene-baseline.json');
for(const key of Object.keys(baseline.c0_scenes))if(byId.has('C0-'+key))c0.scenes[key]=byId.get('C0-'+key).preview;
c0.art_revision={manifest:base+'manifest.json',guide:'docs/05-원화/CH00-01-NARRATIVE-ART-REVIEW.ko.md',source_scenes_preserved:true,objective_reveal_in_early_game:false};
write(c0path,c0);
const mapping={
 'art/chapter01/completion-v1/review/E03-HUB-rations-shared.png':byId.get('E03-rations').preview,
 'art/chapter01/first-drawing-v2/review/01-delivery.png':byId.get('E05-delivery').preview,
 'art/chapter01/first-drawing-v2/review/02-ready.png':byId.get('E06-ready').preview,
 'art/chapter01/first-drawing-v2/review/03-started.png':byId.get('E06-drawing').preview,
 'art/chapter01/first-drawing-v2/review/04-complete.png':byId.get('E06-complete').preview
};
const catpath='design/chapter01/event-quest-catalog-v2.json',cat=read(catpath);
for(const e of cat.events){
 if(!['E03','E05','E06'].includes(e.id))continue;
 e.previous_art_resources??=[...e.art_resources];e.art_resources=e.art_resources.map(p=>mapping[p]??p);
 e.art_revision_manifest=base+'manifest.json';e.art_status='revised-subjective-art-and-native-PSD-available; final resolution and Unity pending';
 if(e.id==='E06'){e.scene='수혁이 기존 책에 그리는 행동 → 소이와 함께 보는 체험 → 완성 페이지 확인. 소이는 옆에서 지켜보며 연필·페이지를 독립적으로 움직이지 않는다.';e.art='새 수혁 그리기 자세·새 소이 앉기와 기존 책/색연필/배경. 실제 상황 회수용 무소이 변형은 초반 비노출.';}
}
write(catpath,cat);
const covpath='design/chapter01/week01-art-coverage-v1.json',cov=read(covpath);
for(const e of cov.events)if(['E03','E05','E06'].includes(e.event)){
 e.previous_resources??=[...e.resources];e.resources=e.resources.map(p=>mapping[p]??p);
 e.previous_composition_ids??=e.composition_ids;e.composition_ids=[];
 e.composition_manifest=base+'manifest.json';e.revision_composition_ids=e.resources.filter(p=>p.startsWith(base)).map(p=>p.split('/').at(-1).replace('.png',''));
 e.status='narrative-art-revised-native-layers-supplied';
}
cov.art_revision_manifest=base+'manifest.json';write(covpath,cov);
const old=read('art/chapter01/first-drawing-v2/manifest.json');old.current_revision_manifest=base+'manifest.json';old.current_revision_note='Originals preserved. E05/E06 current scene references are in narrative-art-v1; Suhyeok draws, Soi watches. Objective reveal variant is ending-only.';write('art/chapter01/first-drawing-v2/manifest.json',old);
const d1=read('design/ui/day01-v1/story.json');d1.art_revision={manifest:base+'manifest.json',note:'Clean scene art revised; existing UI-baked review screenshots remain older layout references.'};write('design/ui/day01-v1/story.json',d1);
for(const p of [...Object.values(c0.scenes),...cat.events.flatMap(e=>e.art_resources)])if(!fs.existsSync(new URL(p,root)))throw Error('Missing '+p);
if(Object.values(c0.scenes).some(p=>p.includes('-truth.png')))throw Error('Objective reveal exposed early');
console.log(JSON.stringify({current_c0_scenes:10,first_week_events_updated:3,objective_variants_not_exposed:true}));
