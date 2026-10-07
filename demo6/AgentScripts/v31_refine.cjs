const fs=require('fs');const path='검증/돌갑충-예고-v031/candidate/Telegraph.cs';let s=fs.readFileSync(path,'utf8');
function r(a,b){if(!s.includes(a))throw Error(a);s=s.replace(a,b);}
r('SpriteRenderer _fill;','SpriteRenderer _fill;\n        SpriteRenderer _rim; // v031 thin inner rim, independent of the moving progress edge');
r('Mask("circle", "edge") ?? EdgeRingSprite(false))','Mask("circle", "edge") ?? EdgeRingSprite(false), Mask("circle", "rim"))');
r('Mask("half", "edge") ?? EdgeRingSprite(true))','Mask("half", "edge") ?? EdgeRingSprite(true), Mask("half", "rim"))');
r('"fill") ?? ShapeSprites.Square, ShapeSprites.Square)','"fill") ?? ShapeSprites.Square, ShapeSprites.Square, Mask("rect", "rim"))');
r('if (t._fill.sprite.border.x > 0) t._fill.drawMode = SpriteDrawMode.Sliced;','if (t._fill.sprite.border.x > 0) t._fill.drawMode = SpriteDrawMode.Sliced;\n            if (t._rim && t._rim.sprite.border.x > 0) t._rim.drawMode = SpriteDrawMode.Sliced;');
r('static Telegraph Create(string name, Sprite outline, Sprite fill, Sprite edge)','static Telegraph Create(string name, Sprite outline, Sprite fill, Sprite edge, Sprite rim)');
r('if (edge) t._edge = NewChild','if (rim) t._rim = NewChild(go.transform, "Inner rim", rim, Color.white, AboveDark ? AboveDarkOrder + 1 : FillOrder + 1);\n            if (edge) t._edge = NewChild');
r('float p = Progress;','float p = Progress;\n            RefreshColors(p);');
s=s.replaceAll('_outline.transform.localScale = Vector3.one * (_radius * 2f);','_outline.transform.localScale = Vector3.one * (_radius * 2f);\n                if (_rim) _rim.transform.localScale = _outline.transform.localScale;');
r('SizeRect(_outline, _length, _width);','SizeRect(_outline, _length, _width);\n            if (_rim) { _rim.transform.localPosition = _outline.transform.localPosition; SizeRect(_rim, _length, _width); }');
r('static void SizeRect(SpriteRenderer sr, float length, float width)','void RefreshColors(float progress)\n        {\n            bool flash = _resolveFlash >= 0f;\n            float imminent = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.84f, 1f, progress));\n            if (_outline) _outline.color = flash ? OutlineFlash : _locked ? OutlineLocked : Color.Lerp(RimColor, OutlineLocked, imminent * .45f);\n            if (_rim) _rim.color = flash ? new Color(1f, .58f, .46f, .92f) : new Color(1f, .39f, .32f, .48f + .24f * imminent);\n            if (_edge) _edge.color = flash ? EdgeFlash : _locked ? EdgeLocked : new Color(1f, .30f, .27f, .75f + .15f * imminent);\n        }\n\n        static void SizeRect(SpriteRenderer sr, float length, float width)');
fs.writeFileSync(path,s);console.log('Thin stationary rim and single pre-impact contrast ramp prepared.');
