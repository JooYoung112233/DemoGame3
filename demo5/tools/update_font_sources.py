from pathlib import Path
font='Assets/funflow_font/TWD-AS_FUNFLOW SURVIVOR_Font/펀플로 생존자.ttf'
for p in Path('demo5/AgentScripts').glob('*.cs'):
 s=p.read_text(encoding='utf-8-sig');n=s.replace('Assets/Art/FrontEnd/Fonts/Gaegu/Gaegu-Bold.ttf',font).replace('Assets/Art/FrontEnd/Fonts/Gaegu/Gaegu-Regular.ttf',font)
 if n!=s:
  n=n.replace('FontStyle.Bold','FontStyle.Normal').replace('Gaegu Bold','FUNFLOW Survivor');p.write_text(n,encoding='utf-8')
p=Path('demo5/Assets/Scripts/NightRun/NightRunView.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('font=Font.CreateDynamicFontFromOSFont','font=FontOverride?FontOverride:Font.CreateDynamicFontFromOSFont');s=s.replace('        void Awake()', '        public Font FontOverride;\n        void Awake()');p.write_text(s,encoding='utf-8')
p=Path('demo5/AgentScripts/ApplyFunflowFont.cs');s=p.read_text(encoding='utf-8-sig');s=s.replace('int count=0;foreach(var t', 'int count=0;foreach(var v in root.GetComponentsInChildren<Demo5.FrontEnd.TitleViewport>(true)){if(v.FontOverride!=font){v.FontOverride=font;EditorUtility.SetDirty(v);count++;}}foreach(var v in root.GetComponentsInChildren<Demo5.NightRun.NightRunView>(true)){if(v.FontOverride!=font){v.FontOverride=font;EditorUtility.SetDirty(v);count++;}}foreach(var t');p.write_text(s,encoding='utf-8')
