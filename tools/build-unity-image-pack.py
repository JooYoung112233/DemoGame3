"""Export current approved scene layers separately from authoring and review files."""
from pathlib import Path
import json,shutil,hashlib
R=Path(__file__).resolve().parents[1];OUT=R/'게임제작';rows=[]
def read(p):return json.loads((R/p).read_text(encoding='utf-8-sig'))
seen=set()
def put(src,category,usage):
 key=(src,category)
 if key in seen:return
 seen.add(key);p=R/src
 if not p.is_file():raise FileNotFoundError(src)
 dest=OUT/category/src
 dest.parent.mkdir(parents=True,exist_ok=True)
 if not dest.exists() or hashlib.sha256(dest.read_bytes()).digest()!=hashlib.sha256(p.read_bytes()).digest():shutil.copy2(p,dest)
 rows.append({'source':src,'file':dest.relative_to(R).as_posix(),'usage':usage,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
scenes=read('art/chapter00-01/art-polish-v1/scene-manifest.json')
for id,p in scenes['backgrounds'].items():put(p,'01-인게임이미지','고정 배경 '+id)
for scene in scenes['scenes']:
 for layer in scene['layers']:put(layer['path'],'01-인게임이미지',scene['id']+' / '+layer['id']+' / 표시 조건은 원본 매니페스트 확인')
 put(scene['review'],'02-참고용이미지',scene['id']+' 합성 검수 / 인게임 레이어 대체 금지')
 put(scene['psd'],'03-편집원본',scene['id']+' 편집 PSD')
for asset in read('art/chapter00-01/art-polish-v1/native-manifest.json')['assets']:
 put(asset['native'],'01-인게임이미지',asset['id']+' 승인 인물 원본 / 장면 크기와 표시 조건 확인')
 put(asset['source'],'03-편집원본',asset['id']+' 생성 원본')
 put(asset['psd'],'03-편집원본',asset['id']+' 편집 PSD')
# Other historical images stay in the authoring archive, not silently promoted to runtime.
image_ext={'.png','.jpg','.jpeg','.webp','.psd'}
selected={x['source'] for x in rows}
remaining=[p.relative_to(R).as_posix() for p in (R/'art').rglob('*') if p.is_file() and p.suffix.lower() in image_ext and p.relative_to(R).as_posix() not in selected and not any(x in p.parts for x in ['qa','scene-qa','gallery-qa'])]
data={'status':'current-scene-export-not-complete-unity-assets','source_manifest':'art/chapter00-01/art-polish-v1/scene-manifest.json','files':rows,'not_exported':remaining,'note':'미수록은 미사용 확정이 아님. 시작 회상/UI/소품의 추가 연결은 최신 콘티와 대조. review 이미지를 분리 레이어 대신 사용하지 않음.'}
(OUT/'이미지분류표.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
for category,meaning in [('01-인게임이미지','현재 장면용 배경·인물·분리 레이어'),('02-참고용이미지','레이어를 합친 검수 장면. 게임 임포트 대상 아님'),('03-편집원본','PSD와 수정용 생성 원본. 게임 임포트 대상 아님')]:
 files=[x for x in rows if '/'+category+'/' in x['file']]
 (OUT/category/'README.md').write_text('# '+category+'\n\n'+meaning+'\n\n총 '+str(len(files))+'개. 원본 경로와 SHA-256은 상위 이미지분류표.json을 확인합니다. 분리 레이어는 위치·가림 순서·표시 조건과 함께 사용합니다. 이 폴더의 이름은 최종 해상도 검수 완료를 의미하지 않습니다.\n',encoding='utf-8')
print(json.dumps({'exported':len(rows),'remaining_source_images':len(remaining),'counts':{c:sum('/'+c+'/' in x['file'] for x in rows) for c in ['01-인게임이미지','02-참고용이미지','03-편집원본']}},ensure_ascii=False))
