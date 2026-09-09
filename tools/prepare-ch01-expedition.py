from pathlib import Path
import json
from PIL import Image
from asset_cutout import gray_backdrop_cut,polygon_cut,inspect_asset
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'art/chapter01/letter'
src=Image.open(OUT/'letter-props-source-v1.png')
records=[]
for name,rect in [('pink-envelope',[195,275,595,375]),('pink-stationery',[890,75,610,780])]:
    asset,actual=gray_backdrop_cut(src,rect)
    asset.save(OUT/(name+'-v1.png'));inspect_asset(asset,OUT/'qa',name)
    records.append({'id':name,'asset':f'art/chapter01/letter/{name}-v1.png','source':'art/chapter01/letter/letter-props-source-v1.png','sourceRect':actual,'size':list(asset.size)})
(OUT/'manifest-v1.json').write_text(json.dumps(records,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

# Keep the existing refrigerator's tiny bottle design. Resolution work is deferred.
src=Image.open(ROOT/'art/scenes/02-mart/mart-interior-v1.png')
rect=[795,168,17,32]
bottle=polygon_cut(src,rect,[(801,169),(808,169),(808,174),(811,177),(811,195),(808,199),(799,198),(796,195),(796,178),(800,175)])
bottle.save(ROOT/'art/chapter01/store/drink-bottle-v1.png')
inspect_asset(bottle,ROOT/'art/chapter01/store/qa','drink-bottle')
for name,rect,poly in [
 ('pencil-tin',[1266,460,89,49],[(1271,463),(1335,462),(1349,468),(1353,491),(1348,504),(1276,506),(1269,497),(1267,478)]),
 ('medicine',[1096,207,26,44],[(1105,208),(1114,208),(1116,213),(1114,218),(1120,221),(1120,246),(1116,249),(1101,248),(1098,244),(1099,221),(1105,216)]),
 ('food-can',[565,132,29,44],[(571,135),(582,133),(590,137),(592,144),(591,170),(586,174),(572,174),(566,170),(566,143)])]:
    cut=polygon_cut(src,rect,poly);cut.save(ROOT/f'art/chapter01/store/{name}-v1.png')
    inspect_asset(cut,ROOT/'art/chapter01/store/qa',name)

def layer(id,name,asset,rect,order=30):return dict(id=id,name=name,asset=asset,rect=rect,order=order)
def state(id,label,visible,focus,line,speaker='수혁',active=None):
    return dict(id=id,label=label,visible=visible,focus=focus,line=line,speaker=speaker,active=active or [])
def save(cfg):
    (ROOT/'design/chapter01'/f"{cfg['id']}.json").write_text(json.dumps(cfg,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    for s in cfg['presets']:
        canvas=Image.open(ROOT/cfg['base']).convert('RGBA')
        for l in sorted(cfg['slots'],key=lambda l:l['order']):
            if l['id'] in s['visible']:
                im=Image.open(ROOT/l['asset']).convert('RGBA').resize(tuple(l['rect'][2:]),Image.Resampling.LANCZOS)
                canvas.alpha_composite(im,tuple(l['rect'][:2]))
        canvas.save(ROOT/'art/chapter01/scene-qa'/f"{cfg['id']}-{s['id']}.png")

common='art/chapter01/shared-props/'
slots=[layer('can-a','통조림','art/chapter01/store/food-can-v1.png',[565,132,29,44]),
       layer('can-b','통조림','art/chapter01/store/food-can-v1.png',[608,192,29,44]),
       layer('medicine','갈색 약병','art/chapter01/store/medicine-v1.png',[1096,207,26,44]),
       layer('pencils','색연필 상자','art/chapter01/store/pencil-tin-v1.png',[1266,460,89,49]),
       layer('drink-a','음료 병','art/chapter01/store/drink-bottle-v1.png',[795,168,17,32]),
       layer('drink-b','음료 병','art/chapter01/store/drink-bottle-v1.png',[872,168,17,32]),
       layer('investigate','수혁 · 조사','art/chapter01/extra-poses/suhyeok-investigate-v1.png',[1055,325,220,281],40)]
loot=[l['id'] for l in slots if l['id']!='investigate']
save(dict(id='store',title='편의점에서',canvas=dict(width=1672,height=941),base='art/chapter01/store/store-clean-base-v1.png',slots=slots,actors=[],presets=[
 state('enter','들어가기',loot,[.5,.5,1],'아직 남아 있는 게 있을까.','수혁',['fridge','stationery']),
 state('fridge','냉장고',loot,[.515,.225,3.1],'유리 너머, 몇 병이 보인다.','', ['drink-a']),
 state('recovered','한 병 꺼낸 뒤',[x for x in loot if x!='drink-a'],[.515,.225,3.1],'덜컥. 선반이 생각보다 크게 울렸다.',''),
 state('noise','입구의 인기척',[x for x in loot if x!='drink-a'],[.50,.75,2.4],'…밖에서 난 소리인가.','수혁'),
 state('stationery','문구 선반',loot+['investigate'],[.70,.50,2.3],'색연필. 소이가 좋아하겠지.','수혁',['pencils']),
 state('pencils-taken','상자를 챙긴 뒤',[x for x in loot if x!='pencils']+['investigate'],[.70,.50,2.3],'이제 돌아가자.','수혁')],
 hotspots=[dict(id='fridge',name='냉장고',rect=[778,143,161,148]),dict(id='stationery',name='문구 선반',rect=[1247,443,117,81])],
 notes=['상태 선택용 원화 시안. 실제 획득 판정·재고·소음 확률은 연결하지 않았다.','회수용 병은 기존 원화 크기 그대로 추출했다.','약품 종류·용량은 원문에 없으므로 시각적 소품으로만 둔다.']))

save(dict(id='letter',title='서랍에 남은 편지',canvas=dict(width=1672,height=941),base='art/chapter01/letter/old-house-clean-base-v1.png',slots=[
 layer('envelope','분홍색 봉투','art/chapter01/letter/pink-envelope-v1.png',[1049,396,85,28]),
 layer('investigate','수혁 · 발견','art/chapter01/extra-poses/suhyeok-investigate-v1.png',[936,316,144,184],40)],actors=[],presets=[
 state('discover','발견 전',['envelope'],[.5,.5,1],'서랍 안에, 익숙한 색이 남아 있었다.','',['envelope']),
 state('reach','서랍 앞에서',['envelope','investigate'],[.635,.445,2.7],'…서연아.','수혁',['envelope']),
 state('taken','꺼낸 뒤',['investigate'],[.635,.445,2.7],'빛바랜 분홍색 편지지. 익숙하고 따뜻한 향기가 희미하게 남아있다.',''),
 state('read','첫 편지 읽기',[],[.635,.445,2.7],'내 사랑하는 수혁 씨에게.','서연')],
 notes=['낡은 집 내부는 원문에 배치가 없어 새로 제안한 고정 베이스다.','발견·수혁의 짧은 반응은 새 대본 초안. 편지 본문은 사용자 최초 기획서 원문.']))
print('Letter props, store/letter state packs and 10 composites saved.')
