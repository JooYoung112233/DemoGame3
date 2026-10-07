'use strict';
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const {analyze, inspect, watch} = require('./check.cjs');
const root = path.resolve(__dirname, '../..');
const reportDir = path.join(root, '검증/topdown-quality');
const fixture = path.join(reportDir, 'text-fixture');
const ysort = 'public int baseOrder = 1000;';
const source = n => 'const int OutlineOrder = -60; const int AboveDarkOrder = ' + n + ';\n' +
  't._outline = NewChild(go.transform, "Outline", sprite, color, AboveDark ? AboveDarkOrder : OutlineOrder);\n' +
  'sr.sortingOrder = order;';
async function main() {
  const cases = [];
  assert(analyze(source(14500), ysort).findings.some(f => f.level === 'risk')); cases.push('known high-order branch detected');
  assert.equal(analyze(source(-60), ysort).findings.length, 0); cases.push('low-order fixture has no known static risk');
  assert(analyze(source(-60).replace('AboveDarkOrder :', 'ComputeOrder() :'), ysort).findings.some(f => f.level === 'unknown')); cases.push('dynamic expression remains unknown');
  assert(analyze('sr.sortingOrder = ComputeOrder();', ysort).findings.some(f => f.level === 'unknown')); cases.push('changed creation pattern remains unknown');
  assert(analyze(source(-60) + '\nsr.sortingLayerID = custom;', ysort).findings.some(f => f.level === 'unknown')); cases.push('layer override requires review');
  assert(analyze(source(-60) + '\nsr.sortingOrder = 9000;', ysort).findings.some(f => f.level === 'unknown')); cases.push('additional sorting write remains unknown');
  assert(analyze(source(-60) + '\nt._edge = NewChild(\nparent, name, sprite, color, 9000);', ysort).findings.some(f => f.level === 'unknown')); cases.push('partially unparsed creation remains unknown');
  const telegraph = path.join(fixture, 'Assets/Scripts/Game/Combat/Telegraph.cs');
  fs.mkdirSync(path.dirname(telegraph), {recursive:true});
  fs.mkdirSync(path.join(fixture, 'Assets/Scripts/Game/Infra'), {recursive:true});
  fs.mkdirSync(path.join(fixture, '기획'), {recursive:true});
  fs.writeFileSync(telegraph, source(-60));
  fs.writeFileSync(path.join(fixture, 'Assets/Scripts/Game/Infra/YSort.cs'), ysort);
  fs.writeFileSync(path.join(fixture, 'AGENTS.md'), 'TOPDOWN-QUALITY-RULES.ko.md');
  fs.writeFileSync(path.join(fixture, '기획/TOPDOWN-QUALITY-RULES.ko.md'), 'Text fixture only; not a Unity project.');
  const events = []; let close, timeout;
  try {
    await new Promise((resolve, reject) => {
      timeout = setTimeout(() => reject(new Error('No filesystem hook event within 8 seconds')), 8000);
      close = watch(fixture, event => {
        events.push(event);
        if (event.kind === 'watch-change') {
          try { assert.equal(event.result.exitCode, 1); resolve(); } catch (error) { reject(error); }
        }
        if (event.kind === 'watch-error') reject(new Error(event.error));
      }, 50);
      fs.writeFileSync(telegraph, source(14500));
    });
    cases.push('fs.watch change automatically invoked inspect and detected introduced risk');
  } finally { clearTimeout(timeout); if (close) close(); }
  const originalConnection = [];
  const closeOriginal = watch(root, event => originalConnection.push(event));
  closeOriginal();
  assert(originalConnection.some(event => event.kind === 'watch-ready' && event.watcherCount === 3));
  cases.push('original demo6 Assets/ProjectSettings/art watchers connected and then closed without changes');
  const report = {time:new Date().toISOString(), selfTests:'passed', cases, connectionEvidence:events, originalProjectConnection:originalConnection, originalProjectScan:inspect(root), unityTouched:false, visualReview:'not performed; Unity owned by another session', persistentBackgroundHook:false};
  fs.writeFileSync(path.join(reportDir, 'verification.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(report, null, 2));
}
main().catch(error => {console.error(error.stack); process.exitCode = 1;});
