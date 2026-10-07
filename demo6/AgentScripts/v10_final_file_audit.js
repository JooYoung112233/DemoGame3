const fs = require('fs');
const crypto = require('crypto');
const dir = '검증/정수리-질감수정-v10';
const applied = JSON.parse(fs.readFileSync(dir + '/applied-files.json', 'utf8'));
const hash = p => crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const own = new Set(applied.files.map(x => x.path));
const expected = new Map([
  ...JSON.parse(fs.readFileSync(dir + '/initial-files.json', 'utf8')).files,
  ...JSON.parse(fs.readFileSync(dir + '/synced-concurrent-files.json', 'utf8')),
].map(x => [x.path, x.sha256]));
const concurrent = [...expected]
  .filter(([p, v]) => !own.has(p) && fs.existsSync(p) && hash(p) !== v)
  .map(([p]) => p);
const actual = {
  time: new Date().toISOString(),
  appliedFiles: applied.files.length,
  allAppliedHashesMatch: applied.files.every(x => fs.existsSync(x.path) && hash(x.path) === x.after),
  concurrentFilesChangedSinceCheckpoint: concurrent,
  compileErrors: fs.readFileSync(dir + '/original-v10-startup.log', 'utf8').split('\n')
    .filter(line => /error CS\d+|NullReferenceException|IndexOutOfRangeException|MissingReferenceException/.test(line)),
};
fs.writeFileSync(dir + '/final-file-audit.json', JSON.stringify(actual, null, 2));
console.log(JSON.stringify(actual));
