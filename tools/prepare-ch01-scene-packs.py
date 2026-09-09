from pathlib import Path
import json
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'design/chapter01';OUT.mkdir(exist_ok=True,parents=True)
QA=ROOT/'art/chapter01/scene-qa';QA.mkdir(exist_ok=True)
seated=json.loads((ROOT/'design/interaction/camper-seated-v1.json').read_text(encoding='utf-8'))
actors=seated['actors']
father=[a['id'] for a in actors if a['pose']=='suhyeok-seated']
soi=[a['id'] for a in actors if a['pose']=='soi-seated']
foreground=seated['foreground']
def layer(id,asset,rect,order=40,name=None):return {'id':id,'name':name or id,'asset':asset,'rect':rect,'order':order,'interactive':False}
def pack(id,title,layers,presets,zoom,foreground_order=30):
    cfg={'id':id,'title':title,'canvas':{'width':1672,'height':941},'base':seated['base'],'slots':layers,'actors':actors,'foreground':dict(foreground,order=foreground_order),'presets':presets,'zoom':zoom,'status':'resource placement prototype; no minigame or date rules'}
    (OUT/(id+'.json')).write_text(json.dumps(cfg,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    all_layers=layers+actors+[cfg['foreground']]
    for state in presets:
        canvas=Image.open(ROOT/cfg['base']).convert('RGBA')
        for l in sorted(all_layers,key=lambda x:x['order']):
            if l.get('alwaysVisible') or l['id'] in state['visible']:
                sprite=Image.open(ROOT/l['asset']).convert('RGBA').resize(tuple(l['rect'][2:]),Image.Resampling.LANCZOS)
                canvas.alpha_composite(sprite,tuple(l['rect'][:2]))
        canvas.save(QA/(id+'-'+state['id']+'.png'))
    return cfg
props=[
 layer('pot','art/chapter01/kitchen/soup-pot-v1.png',[723,306,75,55],35,'수프 냄비'),
 layer('can','art/chapter01/shared-props/food-can-v1.png',[621,328,25,27],35,'통조림'),
 layer('cook','art/chapter01/extra-poses/suhyeok-cook-v1.png',[740,245,130,262],40,'수혁 · 요리'),
 layer('soup-father','art/chapter01/shared-props/soup-bowl-v1.png',[872,469,58,46],46,'수혁의 수프'),
 layer('soup-soi','art/chapter01/shared-props/soup-bowl-v1.png',[988,469,50,40],46,'소이의 수프')]
pack('kitchen','주방과 저녁 식사',props,[
 {'id':'prepare','label':'준비','visible':['can']+soi,'active':['can'],'line':'남은 걸로 따뜻하게 해 보자.','speaker':'수혁','description':'식재료를 꺼내 놓은 주방'},
 {'id':'cook','label':'요리 자세','visible':['can','pot','cook']+soi,'active':[],'line':'냄새 좋다.','speaker':'소이','description':'고정 주방 자리의 요리 포즈 · 미니게임 규칙은 보류'},
 {'id':'meal','label':'식사','visible':['soup-father','soup-soi']+father+soi,'active':[],'line':'아빠 것도 식기 전에 먹어.','speaker':'소이','description':'같은 식탁에서 함께 먹는 저녁'}],{'scale':2,'originX':47,'originY':45},foreground_order=44)
dogs=[
 layer('dog-sit','art/chapter01/dog/byeolddongi-sit-v1.png',[735,610,52,70],45,'별똥이'),
 layer('dog-alert','art/chapter01/dog/byeolddongi-alert-v1.png',[730,610,57,70],45,'별똥이 · 인기척'),
 layer('dog-rest','art/chapter01/dog/byeolddongi-rest-v1.png',[722,643,63,37],45,'별똥이 · 쉬기'),
 layer('dog-outside','art/chapter01/dog/byeolddongi-sit-v1.png',[810,805,52,70],45,'처음 만난 강아지'),
 layer('meet-dog','art/chapter01/extra-poses/suhyeok-investigate-v1.png',[701,736,120,153],45,'수혁 · 손 내밀기')]
pack('byeolddongi','별똥이의 캠핑카 자리',dogs,[
 {'id':'meet','label':'첫 만남','visible':['dog-outside']+soi,'active':['dog-outside'],'focus':[.49,.83,2.5],'line':'아빠, 저기. 혼자 있나 봐.','speaker':'소이','description':'캠핑카 계단 밖에서 마주치는 작은 유기견'},
 {'id':'approach','label':'손 내밀기','visible':['dog-outside','meet-dog']+soi,'active':[],'focus':[.48,.84,2.5],'line':'괜찮아. 천천히 와.','speaker':'수혁','description':'걷기 없이 고정 조사 포즈를 재사용한 첫 교감'},
 {'id':'sit','label':'기다리기','visible':['dog-sit']+father+soi,'active':[],'line':'이제 네 자리도 생겼네.','speaker':'소이','description':'합류 후 러그에서 두 사람을 바라보는 별똥이'},
 {'id':'alert','label':'인기척','visible':['dog-alert']+father+soi,'active':[],'line':'왜 그래, 별똥아?','speaker':'수혁','description':'별똥이의 반응이 주변을 살펴볼 계기가 된다'},
 {'id':'rest','label':'쉬기','visible':['dog-rest']+father+soi,'active':[],'line':'작은 발소리도 조용해졌다.','speaker':'','description':'같은 자리에서 편하게 쉬는 자세'}],{'scale':2.2,'originX':47,'originY':67})
print('Kitchen and dog packs, 8 scene composites. Dialogue is new draft text.')
