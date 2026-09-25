'use strict';

// Format packaging only. The native PNG RGBA pixels are never changed.
// One whole item per layer; only fuel is initially visible in the shared canvas.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const { PNG } = require('C:/Users/power/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pngjs');
const { writePsd, verifyPsd, validateCodec } = require('../../AgentScripts/PackageStandeeLayers.cjs');
const root = path.resolve(__dirname, '../..');
const ids = ['fuel', 'lubricant', 'battery', 'tobacco', 'coffee', 'alcohol', 'electrical-parts', 'weapon-parts'];
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

function load(id) {
  const source = path.join(__dirname, `${id}-source.png`);
  const unity = path.join(root, 'Assets/Art/EverydayGoods', `${id}.png`);
  const bytes = fs.readFileSync(source);
  assert(bytes.equals(fs.readFileSync(unity)), `${id}: Unity copy differs from original`);
  assert.equal(bytes[24], 8);
  assert.equal(bytes[25], 6, `${id}: not true RGBA`);
  const image = PNG.sync.read(bytes, { checkCRC: true });
  let transparent = 0, opaque = 0, maxAlpha = 0;
  let left = image.width, top = image.height, right = -1, bottom = -1;
  for (let y = 0; y < image.height; y++) {
    for (let x = 0; x < image.width; x++) {
      const alpha = image.data[(y * image.width + x) * 4 + 3];
      if (alpha === 0) transparent++;
      if (alpha >= 250) opaque++;
      maxAlpha = Math.max(maxAlpha, alpha);
      if (alpha >= 32) {
        left = Math.min(left, x); top = Math.min(top, y);
        right = Math.max(right, x); bottom = Math.max(bottom, y);
      }
    }
  }
  assert(transparent > 0 && opaque > 0, `${id}: missing transparency or opaque item`);
  assert(left > 0 && top > 0 && right < image.width - 1 && bottom < image.height - 1, `${id}: body touches canvas edge`);
  assert(Math.max(right - left + 1, bottom - top + 1) >= 800, `${id}: native item smaller than requested`);
  const corners = [0, image.width - 1, image.width * (image.height - 1), image.width * image.height - 1];
  assert(corners.every(pixel => image.data[pixel * 4 + 3] === 0), `${id}: opaque canvas corner`);
  return {
    name: id, image,
    report: { id, source, unity, sha256: hash(bytes), width: image.width, height: image.height,
      mode: 'RGBA8', alphaZeroPixels: transparent, alphaAtLeast250Pixels: opaque, maxAlpha,
      visibleBoundsAlpha32: [left, top, right + 1, bottom + 1], sourceUnityByteIdentical: true,
      wholeObjectLayer: true, resized: false, backgroundRemovedProgrammatically: false }
  };
}

// Read structural offsets to set PSD's hidden-layer flag. Pixel channels stay intact.
function setLayerVisibility(bytes, expectedCount) {
  let at = 26;
  for (let section = 0; section < 2; section++) {
    const length = bytes.readUInt32BE(at); at += 4 + length;
  }
  at += 4; // Layer/mask section length.
  at += 4; // Layer info length.
  assert.equal(bytes.readInt16BE(at), -expectedCount); at += 2;
  const flags = [];
  for (let i = 0; i < expectedCount; i++) {
    at += 16;
    const channelCount = bytes.readUInt16BE(at); at += 2 + channelCount * 6;
    assert.equal(bytes.subarray(at, at + 8).toString('ascii'), '8BIMnorm'); at += 8;
    const flagOffset = at + 2;
    bytes[flagOffset] = i === 0 ? 0 : 2;
    flags.push({ name: ids[i], offset: flagOffset, visible: i === 0 });
    at += 4;
    const extraLength = bytes.readUInt32BE(at); at += 4 + extraLength;
  }
  return flags;
}

const codecCases = validateCodec();
const layers = ids.map(load);
assert(layers.every(layer => layer.image.width === 1254 && layer.image.height === 1254));
const composite = layers[0].image;
const psd = writePsd(layers, composite);
verifyPsd(psd, layers, composite);
const flags = setLayerVisibility(psd, layers.length);
const destination = path.join(__dirname, 'everyday-goods-native-layers.psd');
fs.writeFileSync(destination, psd);
const written = fs.readFileSync(destination);
assert(written.equals(psd));
const verificationCopy = Buffer.from(written);
for (const flag of flags) {
  assert.equal(written[flag.offset] & 2, flag.visible ? 0 : 2);
  verificationCopy[flag.offset] = 0;
}
// The shared verifier checks all R/G/B/A bytes in all layers and merged image.
// It expects visible flags, so only those structural flags are reset in the copy.
verifyPsd(verificationCopy, layers, composite);
for (const layer of layers) assert.equal(hash(fs.readFileSync(layer.report.source)), layer.report.sha256);
const report = {
  generatedAt: new Date().toISOString(), packagingOnly: true, sourcePixelsModified: false,
  allLayerRgbaRoundtripExact: true, mergedRgbaMatches: 'fuel-source.png',
  codecBoundaryCasesPassed: codecCases, pngCount: layers.length,
  psd: { path: destination, bytes: written.length, sha256: hash(written), width: 1254, height: 1254,
    layerCount: layers.length, layers: flags.map(({ name, visible }) => ({ name, visible })),
    limitation: 'Each whole item is an independent layer. Ink, paint, scratches and components within an item are not separately layered.' },
  assets: layers.map(layer => layer.report)
};
fs.writeFileSync(path.join(__dirname, 'validation.json'), JSON.stringify(report, null, 2) + '\n', 'utf8');
process.stdout.write(JSON.stringify({ count: layers.length, psd: destination, psdBytes: written.length, roundtrip: 'PASS' }) + '\n');
