from pathlib import Path
import json,base64,sys
ROOT=Path(__file__).resolve().parents[1]
images={key:'data:image/png;base64,'+base64.b64encode((ROOT/f'art/chapter01/focus-review/qa/{key}.png').read_bytes()).decode() for key in ['before','after']}
template=(ROOT/'design/chapter01/focus-comparison.template.html').read_text(encoding='utf-8')
html=template.replace('__IMAGES__',json.dumps(images))
assert len(html.encode())<1000000,len(html.encode())
out=Path(sys.argv[1])/'focus-comparison.html';out.write_text(html,encoding='utf-8');print(out)
