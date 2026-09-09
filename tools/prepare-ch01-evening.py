from pathlib import Path
import json,zipfile,xml.etree.ElementTree as ET,sys
from PIL import Image
from prepare_ch01_shared import save_pack,layer,state
ROOT=Path(__file__).resolve().parents[1]
base=json.loads((ROOT/'design/interaction/camper-seated-v1.json').read_text(encoding='utf-8'))
actors=base['actors'];father=[a['id'] for a in actors if a['pose']=='suhyeok-seated'];soi=[a['id'] for a in actors if a['pose']=='soi-seated']
slots=[layer('book','소이의 스케치북','art/chapter01/layers/pages/sketchbook-blank-v1.png',[904,454,102,73],40),
 layer('journal','블랙 저널','art/chapter01/shared-props/black-journal-v1.png',[872,476,67,45],40),
 layer('medicine','약병','art/chapter01/shared-props/medicine-bottle-v1.png',[990,470,22,38],40),
 layer('mug','머그컵','art/chapter01/layers/sprites/mug-source-v1.png',[1010,480,53,52],40),
 layer('dog','별똥이','art/chapter01/dog/byeolddongi-rest-v1.png',[722,643,63,37],40)]
cfg=dict(id='evening',title='돌아온 저녁',canvas=base['canvas'],base=base['base'],slots=slots,actors=actors,foreground=base['foreground'],presets=[
 state('no-pencils','빈 페이지',['book']+father+soi,[.575,.44,2.9],'오늘은 못 찾았어.','수혁',['book']),
 state('promise','소이의 대답',['book']+father+soi,[.575,.44,2.9],'그럼 오늘은 얘기로 그리자. 아빠가 본 거.','소이'),
 state('journal','하루의 기록',['journal','mug']+father,[.55,.51,3],'빈손으로 돌아왔지만, 오늘의 이야기는 남았다.','수혁',['journal']),
 state('medicine','쉬어 갈 때',['medicine','mug']+father,[.585,.49,3],'오늘은 여기까지 하자.','수혁'),
 state('rest','잠들기 전',['dog']+father+soi,[.60,.45,2],'밖의 소리가 조금 멀어졌다.',''),
 state('next','다음 목적지',['book','dog']+father+soi,[.575,.44,2.9],'그 바다는, 어느 쪽에 있어?','소이')],notes=['미획득 분기 대사와 저널은 새 초안. 원문 대사로 표기하지 않는다.','휴식은 현재 앉기 포즈를 재사용한다. 잠든 인물 전신을 새로 만들지 않는다.','약 이름·용량·치료 효과를 설정하지 않는다.'])
save_pack(cfg)

# Optional path supplied explicitly: copy ONLY the user's Live49 first letter.
if len(sys.argv)>1:
    source=Path(sys.argv[1])
    with zipfile.ZipFile(source) as z:doc=ET.fromstring(z.read('word/document.xml'))
    ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
    paragraphs=[''.join(p.itertext()) for p in doc.findall('.//w:body/w:p',ns)]
    start=next(i for i,p in enumerate(paragraphs) if p.strip()=='내 사랑하는 수혁 씨에게.')
    end=next(i for i in range(start,len(paragraphs)) if paragraphs[i].strip()=='- 당신의 서연')
    text=paragraphs[start:end+1]
    assert len(text)==8,(len(text),text)
    letter=dict(id='seoyeon-letter-01',title='첫 번째 편지 · 담담한 부탁',author='서연',provenance='사용자 최초 기획서 아포칼립스 · Live49 · 편지 1',verbatim=True,paragraphs=text)
    (ROOT/'design/chapter01/first-letter-text.json').write_text(json.dumps(letter,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('Evening pack saved; first-letter source copied if supplied.')
