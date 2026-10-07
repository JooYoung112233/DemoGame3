'use strict';
// Read-only source guard. This does not execute Unity or validate rendered pixels.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const ROOT = path.resolve(__dirname, '../..');
const INPUTS = {
  telegraph: 'Assets/Scripts/Game/Combat/Telegraph.cs',
  ysort: 'Assets/Scripts/Game/Infra/YSort.cs',
  rules: '기획/TOPDOWN-QUALITY-RULES.ko.md',
  agents: 'AGENTS.md'
};
function stripComments(s) { return s.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/[^\r\n]*/g, ''); }
function values(expression, constants) {
  const branches = expression.split('?');
  const terms = branches.length === 1 ? branches : branches.slice(1).join('?').split(':');
  if (branches.length > 2 || terms.some(x => /[?:]/.test(x))) return null;
  const result = [];
  for (let term of terms) {
    term = term.trim();
    const m = /^(-?\d+|[A-Za-z_]\w*)(?:\s*([+-])\s*(\d+))?$/.exec(term);
    if (!m) return null;
    const n = /^-?\d+$/.test(m[1]) ? Number(m[1]) : constants[m[1]];
    if (!Number.isFinite(n)) return null;
    result.push(n + (m[2] === '-' ? -1 : 1) * Number(m[3] || 0));
  }
  return result;
}
function analyze(telegraph, ysort) {
  const t = stripComments(telegraph), y = stripComments(ysort);
  const base = /\bbaseOrder\s*=\s*(-?\d+)\s*;/.exec(y);
  const findings = [], orders = [];
  const constants = Object.fromEntries([...t.matchAll(/\bconst\s+int\s+(\w+)\s*=\s*(-?\d+)\s*;/g)].map(m => [m[1], Number(m[2])]));
  if (!base) findings.push({level:'unknown', code:'YSORT_BASE_UNRESOLVED'});
  if (/\bsortingLayer(?:ID|Name)\s*=|\bSortingGroup\b/.test(t)) findings.push({level:'unknown', code:'LAYER_OR_GROUP_REVIEW_REQUIRED'});
  const calls = [...t.matchAll(/\bNewChild\s*\(([^;\n]+)\)\s*;/g)];
  const callCount = [...t.matchAll(/=\s*NewChild\s*\(/g)].length;
  const sortWrites = [...t.matchAll(/\bsortingOrder\s*=/g)].length;
  if (!calls.length || calls.length !== callCount || sortWrites !== 1 || !/\bsortingOrder\s*=\s*order\s*;/.test(t)) findings.push({level:'unknown', code:'TELEGRAPH_CREATION_PATTERN_CHANGED'});
  for (const m of calls) {
    const expression = m[1].slice(m[1].lastIndexOf(',') + 1).trim();
    const resolved = values(expression, constants);
    orders.push({expression, values:resolved});
    if (!resolved) findings.push({level:'unknown', code:'ORDER_EXPRESSION_UNRESOLVED', expression});
    else if (base && resolved.some(n => n >= Number(base[1]))) findings.push({level:'risk', code:'GROUND_ORDER_REACHES_ACTOR_BASE', expression, values:resolved, actorBase:Number(base[1])});
  }
  return {actorBase:base ? Number(base[1]) : null, orders, findings};
}
function inspect(root = ROOT) {
  const inputs = {}, sources = {}, findings = [];
  for (const [key, relative] of Object.entries(INPUTS)) {
    try {
      const bytes = fs.readFileSync(path.join(root, relative));
      sources[key] = bytes.toString('utf8');
      inputs[key] = {path:relative, sha256:crypto.createHash('sha256').update(bytes).digest('hex')};
    } catch (error) { findings.push({level:'unknown', code:'INPUT_UNREADABLE', path:relative, error:error.code}); }
  }
  const analysis = sources.telegraph && sources.ysort ? analyze(sources.telegraph, sources.ysort) : {orders:[], findings:[]};
  findings.push(...analysis.findings);
  if (sources.agents && !sources.agents.includes('TOPDOWN-QUALITY-RULES.ko.md')) findings.push({level:'unknown', code:'RULE_LINK_MISSING'});
  const exitCode = findings.some(f => f.level === 'unknown') ? 2 : findings.length ? 1 : 0;
  return {time:new Date().toISOString(), kind:'topdown-source-check', scope:'known Telegraph source paths only; not runtime rendering', visualReview:'required', exitCode, inputs, actorBase:analysis.actorBase, orders:analysis.orders, findings};
}
function watch(root = ROOT, emit = x => console.log(JSON.stringify(x)), debounceMs = 200) {
  const watchers = [], pending = new Set(); let timer;
  const relevant = /\.(cs|shader|asset|unity|prefab|meta|png|aseprite|ase|json|mat|anim|controller|psd)$/i;
  try {
    for (const relative of ['Assets', 'ProjectSettings', '아트']) {
      const dir = path.join(root, relative);
      if (!fs.existsSync(dir)) continue;
      const watcher = fs.watch(dir, {recursive:true}, (event, filename) => {
        if (filename && !relevant.test(filename.toString())) return;
        pending.add(path.join(relative, filename ? filename.toString() : '*'));
        clearTimeout(timer);
        timer = setTimeout(() => {
          const changed = [...pending]; pending.clear();
          emit({kind:'watch-change', changed, result:inspect(root)});
        }, debounceMs);
      });
      watcher.on('error', error => emit({kind:'watch-error', directory:relative, error:error.message, visualReview:'required'}));
      watchers.push(watcher);
    }
    if (!watchers.length) throw new Error('No project input directories could be watched.');
    emit({kind:'watch-ready', root, watcherCount:watchers.length, lifetime:'this process only', result:inspect(root)});
  } catch (error) { watchers.forEach(w => w.close()); throw error; }
  return () => { clearTimeout(timer); watchers.forEach(w => w.close()); };
}
if (require.main === module) {
  const args = process.argv.slice(2);
  if (args.some(x => x !== '--watch')) { console.error('Usage: node AgentScripts/topdown-quality/check.cjs [--watch]'); process.exitCode = 2; }
  else if (args.includes('--watch')) {
    try { const close = watch(); process.on('SIGINT', () => {close(); process.exit(0);}); process.on('SIGTERM', () => {close(); process.exit(0);}); }
    catch (error) { console.error(error.message); process.exitCode = 2; }
  } else { const result = inspect(); console.log(JSON.stringify(result, null, 2)); process.exitCode = result.exitCode; }
}
module.exports = {analyze, inspect, watch};
