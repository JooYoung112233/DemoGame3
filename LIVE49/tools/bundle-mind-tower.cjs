const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
let html = fs.readFileSync(path.join(root, 'design/mind-tower/index.html'), 'utf8');
const game = fs.readFileSync(path.join(root, 'design/mind-tower/game.js'), 'utf8');
html = html.replace('<script src="game.js"></script>', () => `<script>${game}</script>`);
if (html.includes('src="game.js"')) throw new Error('External script remains');
for (const match of html.matchAll(/<script>([\s\S]*?)<\/script>/g)) new vm.Script(match[1]);
for (const target of ['preview-output/mind-tower/Live49-mind-tower.html', '.tmp/mind-tower-site/dist/index.html']) {
  const dest = path.join(root, target);
  fs.mkdirSync(path.dirname(dest), {recursive: true});
  fs.writeFileSync(dest, html);
}
console.log('Standalone HTML built; both scripts passed syntax validation.');
