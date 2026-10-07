const fs=require('fs');const ev='검증/돌갑충-예고-v031';
function edit(name,fn){let s=fs.readFileSync(ev+'/backup/'+name,'utf8');fs.writeFileSync(ev+'/candidate/'+name,fn(s));}
function replace(s,a,b){if(!s.includes(a))throw Error('Missing anchor: '+a);return s.replace(a,b);}
edit('TopDownEnemyRig.cs',s=>{
 s=replace(s,'bool _feetInBody;','bool _feetInBody;\n        BeetlePatternVfx _beetle; // v031 approved toe layers and cosmetic effects');
 s=replace(s,'Applied = false;','Applied = false;\n            if (_beetle) _beetle.Hide(); // v031 presentation');
 s=replace(s,'if (_enemy.Dead)\r\n            {','if (_enemy.Dead)\r\n            {\n                if (_beetle) _beetle.Hide(); // v031 full approved sprite for death/corpse');
 s=replace(s,'PlaceLegs(foot, order, alpha, feetInBody);','PlaceLegs(foot, order, alpha, feetInBody);\n            if (_enemy is BoarBrain beetle && _artBody && bodyArt.name == "td_stone_beetle_v028")\n            {\n                if (!_beetle) _beetle = BeetlePatternVfx.Attach(beetle, _body, bodyArt);\n                if (_beetle) _beetle.Pose(_phase, _stepAmp, dt);\n            }\n            else if (_beetle) _beetle.Hide();');
 return s;
});
edit('BoarBrain.cs',s=>{
 s=replace(s,'Sfx.Play(SfxKind.BoarCharge);','Sfx.Play(SfxKind.BoarCharge);\n                        GetComponent<BeetlePatternVfx>()?.StartCharge(); // v031 cosmetic cue');
 s=replace(s,'void EndCharge(bool hitWall)\n        {','void EndCharge(bool hitWall)\n        {\n            GetComponent<BeetlePatternVfx>()?.StopCharge(); // v031 cosmetic cue');
 s=replace(s,'if (_state == State.Charging && collision.gameObject.layer == Layers.Wall) EndCharge(true);','if (_state == State.Charging && collision.gameObject.layer == Layers.Wall)\n            {\n                if (collision.contactCount > 0) GetComponent<BeetlePatternVfx>()?.WallImpact(collision.GetContact(0).point, collision.GetContact(0).normal); // v031 actual contact, once before state changes\n                EndCharge(true);\n            }');
 s=replace(s,'protected override void OnBroken()\n        {','protected override void OnBroken()\n        {\n            GetComponent<BeetlePatternVfx>()?.StopCharge(); // v031 cosmetic cue');
 s=replace(s,'protected override void OnInterrupted()\n        {','protected override void OnInterrupted()\n        {\n            GetComponent<BeetlePatternVfx>()?.Clear(); // v031 cosmetic cue');
 return s;
});
edit('Telegraph.cs',s=>{
 s=replace(s,'static readonly Color OutlineLocked = new Color(0.85f, 0.1f, 0.1f, 0.32f);','static readonly Color OutlineLocked = new Color(.98f, .12f, .14f, 1f);');
 s=replace(s,'static readonly Color OutlineFlash = new Color(1f, 0.3f, 0.3f, 0.45f);','static readonly Color OutlineFlash = new Color(1f, .40f, .34f, 1f);\n        static readonly Color RimColor = new Color(.82f, .10f, .14f, 1f);\n        static readonly Color FillColor = new Color(1f, .18f, .22f, .90f);\n        static Sprite Mask(string shape, string part) => Resources.Load<Sprite>("TelegraphV31/" + shape + "-" + part);');
 s=replace(s,'Create("Telegraph(Circle)", ShapeSprites.Circle, ShapeSprites.Circle, AboveDark ? EdgeRingSprite(false) : null)','Create("Telegraph(Circle)", Mask("circle", "outline") ?? ShapeSprites.Circle, Mask("circle", "fill") ?? ShapeSprites.Circle, Mask("circle", "edge") ?? EdgeRingSprite(false))');
 s=replace(s,'Create("Telegraph(HalfDisc)", sprite, sprite, AboveDark ? EdgeRingSprite(true) : null)','Create("Telegraph(HalfDisc)", Mask("half", "outline") ?? sprite, Mask("half", "fill") ?? sprite, Mask("half", "edge") ?? EdgeRingSprite(true))');
 s=replace(s,'Create("Telegraph(Rect)", ShapeSprites.Square, ShapeSprites.Square, AboveDark ? ShapeSprites.Square : null)','Create("Telegraph(Rect)", Mask("rect", "outline") ?? ShapeSprites.Square, Mask("rect", "fill") ?? ShapeSprites.Square, ShapeSprites.Square)');
 s=replace(s,'t._shape = Shape.Rect;','t._shape = Shape.Rect;\n            if (t._outline.sprite.border.x > 0) t._outline.drawMode = SpriteDrawMode.Sliced;\n            if (t._fill.sprite.border.x > 0) t._fill.drawMode = SpriteDrawMode.Sliced;');
 s=replace(s,'outline, Palette.TelegraphOutline,','outline, RimColor,');
 s=replace(s,'fill, Palette.TelegraphFill,','fill, FillColor,');
 s=replace(s,'edge, EdgeColor, AboveDarkOrder + 1','edge, EdgeColor, AboveDark ? AboveDarkOrder + 1 : FillOrder + 1');
 s=replace(s,'if (_fill) _fill.color = Palette.TelegraphLocked;','if (_fill) _fill.color = new Color(1f, .12f, .16f, 1f);');
 s=s.replaceAll('if (_outline && _outline.sortingOrder == AboveDarkOrder)','if (_outline)');
 s=replace(s,'if (_fill) _fill.color = new Color(1f, 0.3f, 0.3f, 0.6f);','if (_fill) _fill.color = new Color(1f, .40f, .34f, 1f);');
 s=replace(s,'_outline.transform.localScale = new Vector3(_length, _width, 1f);','SizeRect(_outline, _length, _width);');
 s=replace(s,'_fill.transform.localScale = new Vector3(filled, _width, 1f);','SizeRect(_fill, filled, _width);');
 s=replace(s,'new Vector3(Mathf.Max(EdgeThick * 0.5f, filled - EdgeThick * 0.5f), 0f, 0f)','new Vector3(filled - Mathf.Min(EdgeThick, filled) * 0.5f, 0f, 0f)');
 s=replace(s,'new Vector3(EdgeThick, _width, 1f)','new Vector3(Mathf.Min(EdgeThick, filled), _width, 1f)');
 s=replace(s,'static Sprite HalfDiscSprite()','static void SizeRect(SpriteRenderer sr, float length, float width)\n        {\n            if (sr.drawMode == SpriteDrawMode.Sliced)\n            {\n                sr.transform.localScale = Vector3.one;\n                sr.size = new Vector2(length, width);\n            }\n            else sr.transform.localScale = new Vector3(length, width, 1f);\n        }\n\n        static Sprite HalfDiscSprite()');
 return s;
});
console.log('Candidate visual-only patches prepared outside Assets');
