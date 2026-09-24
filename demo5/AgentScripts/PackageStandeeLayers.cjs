#!/usr/bin/env node
'use strict';

// Packaging only: copy native Unity PNG samples into a PSD container. No pixels
// are recolored, resized, extracted, or composited by this script.
// Run only after RenderStandeeReview.ExportLayers has completed.
// Usage: node AgentScripts/PackageStandeeLayers.cjs [manifest.json]

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
let PNG;
try { ({ PNG } = require('pngjs')); }
catch { ({ PNG } = require('C:/Users/power/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/pngjs')); }

const projectRoot = path.resolve(__dirname, '..');
const outputDirectory = path.join(projectRoot, '아트', '리소스검토');
const defaultManifest = path.join(outputDirectory, 'standee-common-layer-exports.json');
const channelIds = [0, 1, 2, -1];
const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

function be16(value, signed = false) {
  const b = Buffer.alloc(2);
  if (signed) b.writeInt16BE(value); else b.writeUInt16BE(value);
  return b;
}
function be32(value, signed = false) {
  const b = Buffer.alloc(4);
  if (signed) b.writeInt32BE(value); else b.writeUInt32BE(value);
  return b;
}
function checkedPath(value) {
  assert.equal(typeof value, 'string', 'A required native export path is missing.');
  const resolved = path.resolve(projectRoot, value);
  assert(fs.statSync(resolved).isFile(), `Not a file: ${resolved}`);
  return resolved;
}
function readPng(value, expectedWidth, expectedHeight) {
  const file = checkedPath(value);
  const bytes = fs.readFileSync(file);
  assert(bytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])), `Not PNG: ${file}`);
  assert.equal(bytes[24], 8, `Expected native 8-bit PNG, no bit-depth conversion: ${file}`);
  assert.equal(bytes[25], 6, `Expected native RGBA PNG, no channel conversion: ${file}`);
  const png = PNG.sync.read(bytes, { checkCRC: true });
  assert.equal(png.width, expectedWidth, `Canvas width differs: ${file}`);
  assert.equal(png.height, expectedHeight, `Canvas height differs: ${file}`);
  assert.equal(png.data.length, expectedWidth * expectedHeight * 4);
  return { file, bytes, data: png.data, width: png.width, height: png.height, sha256: sha256(bytes) };
}

// Photoshop PackBits: 0..127 literal n+1, 129..255 repetition 257-n.
// Encoding each row separately prevents runs from crossing row boundaries.
function packBits(row) {
  const output = [];
  let i = 0;
  while (i < row.length) {
    let run = 1;
    while (run < 128 && i + run < row.length && row[i + run] === row[i]) run++;
    if (run >= 3) {
      output.push(257 - run, row[i]);
      i += run;
      continue;
    }
    const start = i;
    i += run;
    while (i < row.length && i - start < 128) {
      run = 1;
      while (run < 128 && i + run < row.length && row[i + run] === row[i]) run++;
      if (run >= 3) break;
      i += Math.min(run, 128 - (i - start));
    }
    output.push(i - start - 1);
    for (let j = start; j < i; j++) output.push(row[j]);
  }
  return Buffer.from(output);
}

function unpackBits(bytes, expectedLength) {
  const output = Buffer.alloc(expectedLength);
  let i = 0, at = 0;
  while (i < bytes.length) {
    const header = bytes.readInt8(i++);
    if (header >= 0) {
      const count = header + 1;
      assert(i + count <= bytes.length && at + count <= output.length, 'Invalid literal PackBits packet.');
      bytes.copy(output, at, i, i + count);
      i += count; at += count;
    } else if (header !== -128) {
      const count = 1 - header;
      assert(i < bytes.length && at + count <= output.length, 'Invalid repeated PackBits packet.');
      output.fill(bytes[i++], at, at + count);
      at += count;
    }
  }
  assert.equal(at, expectedLength, 'Expanded row length differs.');
  return output;
}

function encodeChannel(image, rgbaChannel) {
  const rows = [];
  const counts = Buffer.alloc(image.height * 2);
  const row = Buffer.alloc(image.width);
  for (let y = 0; y < image.height; y++) {
    const source = y * image.width * 4 + rgbaChannel;
    for (let x = 0; x < image.width; x++) row[x] = image.data[source + x * 4];
    const encoded = packBits(row);
    assert(encoded.length <= 65535, 'PSD v1 row-length limit exceeded.');
    counts.writeUInt16BE(encoded.length, y * 2);
    rows.push(encoded);
  }
  return { counts, rows: Buffer.concat(rows) };
}

function layerRecord(layer, width, height) {
  const channels = channelIds.map((id, index) => {
    const encoded = encodeChannel(layer.image, index);
    return { id, data: Buffer.concat([be16(1), encoded.counts, encoded.rows]) };
  });
  const name = Buffer.from(layer.name, 'ascii');
  assert(name.length < 256 && name.toString('ascii') === layer.name, 'Use short ASCII PSD layer names.');
  const pascal = Buffer.concat([Buffer.from([name.length]), name, Buffer.alloc((4 - (name.length + 1) % 4) % 4)]);
  const extra = Buffer.concat([be32(0), be32(0), pascal]);
  const record = Buffer.concat([
    be32(0, true), be32(0, true), be32(height, true), be32(width, true), be16(4),
    ...channels.map(c => Buffer.concat([be16(c.id, true), be32(c.data.length)])),
    Buffer.from('8BIMnorm', 'ascii'), Buffer.from([255, 0, 0, 0]), be32(extra.length), extra
  ]);
  return { record, channels };
}

function writePsd(layers, composite) {
  const { width, height } = composite;
  assert(width > 0 && height > 0 && width <= 30000 && height <= 30000, 'PSD v1 canvas limit exceeded.');
  const records = layers.map(layer => layerRecord(layer, width, height));
  let layerInfo = Buffer.concat([
    be16(-layers.length, true), // Negative count denotes merged-image transparency.
    ...records.map(r => r.record),
    ...records.flatMap(r => r.channels.map(c => c.data))
  ]);
  if (layerInfo.length % 2) layerInfo = Buffer.concat([layerInfo, Buffer.alloc(1)]);
  const layerMaskSection = Buffer.concat([be32(layerInfo.length), layerInfo, be32(0)]);
  const merged = [0, 1, 2, 3].map(channel => encodeChannel(composite, channel));
  const header = Buffer.concat([
    Buffer.from('8BPS'), be16(1), Buffer.alloc(6), be16(4), be32(height), be32(width), be16(8), be16(3)
  ]);
  return Buffer.concat([
    header, be32(0), be32(0), be32(layerMaskSection.length), layerMaskSection,
    be16(1), ...merged.map(c => c.counts), ...merged.map(c => c.rows)
  ]);
}

class Reader {
  constructor(bytes, offset = 0) { this.bytes = bytes; this.offset = offset; }
  take(n) {
    assert(n >= 0 && this.offset + n <= this.bytes.length, 'PSD read outside file bounds.');
    const result = this.bytes.subarray(this.offset, this.offset + n);
    this.offset += n;
    return result;
  }
  u16() { return this.take(2).readUInt16BE(0); }
  i16() { return this.take(2).readInt16BE(0); }
  u32() { return this.take(4).readUInt32BE(0); }
  i32() { return this.take(4).readInt32BE(0); }
  text(n) { return this.take(n).toString('ascii'); }
}

function decodeChannel(bytes, width, height, rgba, rgbaChannel) {
  const reader = new Reader(bytes);
  assert.equal(reader.u16(), 1, 'Expected PSD RLE compression.');
  const counts = Array.from({ length: height }, () => reader.u16());
  for (let y = 0; y < height; y++) {
    const row = unpackBits(reader.take(counts[y]), width);
    const target = y * width * 4 + rgbaChannel;
    for (let x = 0; x < width; x++) rgba[target + x * 4] = row[x];
  }
  assert.equal(reader.offset, bytes.length, 'Extra bytes in layer channel.');
}

// Independent PSD container parsing and channel decoding validates every byte,
// including transparent pixels. It does not merely check dimensions or thumbnails.
function verifyPsd(bytes, layers, composite) {
  const reader = new Reader(bytes);
  assert.equal(reader.text(4), '8BPS');
  assert.equal(reader.u16(), 1);
  assert(reader.take(6).equals(Buffer.alloc(6)));
  assert.equal(reader.u16(), 4);
  assert.equal(reader.u32(), composite.height);
  assert.equal(reader.u32(), composite.width);
  assert.equal(reader.u16(), 8);
  assert.equal(reader.u16(), 3);
  reader.take(reader.u32()); // Color mode data.
  reader.take(reader.u32()); // Image resources.
  const sectionLength = reader.u32();
  const sectionEnd = reader.offset + sectionLength;
  const layerInfoLength = reader.u32();
  const layerInfoEnd = reader.offset + layerInfoLength;
  assert.equal(reader.i16(), -layers.length, 'Merged transparency flag or layer count differs.');
  const records = layers.map(expected => {
    assert.equal(reader.i32(), 0);
    assert.equal(reader.i32(), 0);
    assert.equal(reader.i32(), composite.height);
    assert.equal(reader.i32(), composite.width);
    assert.equal(reader.u16(), 4);
    const channels = Array.from({ length: 4 }, () => ({ id: reader.i16(), size: reader.u32() }));
    assert.deepEqual(channels.map(c => c.id), channelIds);
    assert.equal(reader.text(8), '8BIMnorm');
    assert(reader.take(4).equals(Buffer.from([255, 0, 0, 0])));
    const extraLength = reader.u32();
    const extraEnd = reader.offset + extraLength;
    reader.take(reader.u32()); // Layer mask.
    reader.take(reader.u32()); // Blending ranges.
    const nameLength = reader.take(1)[0];
    assert.equal(reader.text(nameLength), expected.name);
    reader.take((4 - (nameLength + 1) % 4) % 4);
    assert.equal(reader.offset, extraEnd);
    return { expected, channels };
  });
  for (const record of records) {
    const decoded = Buffer.alloc(composite.width * composite.height * 4);
    for (const channel of record.channels) {
      decodeChannel(reader.take(channel.size), composite.width, composite.height, decoded, channel.id === -1 ? 3 : channel.id);
    }
    assert(decoded.equals(record.expected.image.data), `Layer RGBA bytes differ: ${record.expected.name}`);
  }
  const remainingLayerPadding = layerInfoEnd - reader.offset;
  assert(remainingLayerPadding === 0 || remainingLayerPadding === 1);
  assert(reader.take(remainingLayerPadding).every(v => v === 0));
  assert.equal(reader.u32(), 0, 'Unexpected global layer mask.');
  assert.equal(reader.offset, sectionEnd);
  assert.equal(reader.u16(), 1);
  const countTable = Array.from({ length: 4 * composite.height }, () => reader.u16());
  const decodedComposite = Buffer.alloc(composite.width * composite.height * 4);
  for (let channel = 0; channel < 4; channel++) {
    for (let y = 0; y < composite.height; y++) {
      const row = unpackBits(reader.take(countTable[channel * composite.height + y]), composite.width);
      const target = y * composite.width * 4 + channel;
      for (let x = 0; x < composite.width; x++) decodedComposite[target + x * 4] = row[x];
    }
  }
  assert.equal(reader.offset, bytes.length, 'Trailing PSD bytes not validated.');
  assert(decodedComposite.equals(composite.data), 'Merged RGBA differs from native Unity composite PNG.');
}

function validateCodec() {
  // Meaningful boundary cases for the compact PSD writer, using numeric data only.
  const fixtures = [Buffer.alloc(0), Buffer.from([1]), Buffer.from([1, 1]), Buffer.from([1, 1, 1])];
  for (const width of [127, 128, 129, 255, 256, 257, 1024]) {
    fixtures.push(Buffer.alloc(width, 7));
    fixtures.push(Buffer.from(Array.from({ length: width }, (_, i) => i % 251)));
    fixtures.push(Buffer.from(Array.from({ length: width }, (_, i) => Math.floor(i / 2) % 256)));
    fixtures.push(Buffer.from(Array.from({ length: width }, (_, i) => Math.floor(i / 3) % 256)));
  }
  for (const original of fixtures) assert(unpackBits(packBits(original), original.length).equals(original));
  return fixtures.length;
}

function main() {
  assert(fs.statSync(outputDirectory).isDirectory(), 'The existing art review directory is missing.');
  const manifestPath = checkedPath(process.argv[2] || defaultManifest);
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  assert(Array.isArray(manifest.layers) && manifest.layers.length > 0, 'Manifest contains no export entries.');
  const uniqueIds = new Set();
  // Validate every path before producing any PSD so a missing native render cannot
  // silently turn into a synthesized, resized, or incomplete output.
  for (const entry of manifest.layers) {
    assert(/^[a-z0-9][a-z0-9_-]*$/i.test(entry.id), `Unsafe or empty actor id: ${entry.id}`);
    assert(!uniqueIds.has(entry.id), `Duplicate actor id: ${entry.id}`);
    uniqueIds.add(entry.id);
    assert(Number.isInteger(entry.exportWidth) && Number.isInteger(entry.exportHeight));
    for (const key of ['source', 'ink', 'paper', 'sharedBase', 'composite']) checkedPath(entry[key]);
  }
  const codecCases = validateCodec();
  const results = [];
  for (const entry of manifest.layers) {
    const originalSource = checkedPath(entry.source);
    const sourceBytes = fs.readFileSync(originalSource);
    const sourceHashBefore = sha256(sourceBytes);
    assert(sourceBytes.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10])), 'Original source is not PNG.');
    const load = key => readPng(entry[key], entry.exportWidth, entry.exportHeight);
    const layers = [
      { name: 'Body Ink', image: load('ink') },
      { name: 'Paper Rim', image: load('paper') },
      { name: 'Shared Base', image: load('sharedBase') }
    ];
    const composite = load('composite');
    const psd = writePsd(layers, composite);
    verifyPsd(psd, layers, composite);
    const destination = path.join(outputDirectory, `standee-${entry.id}-native-layers.psd`);
    const temporary = destination + '.tmp';
    try {
      fs.writeFileSync(temporary, psd);
      const written = fs.readFileSync(temporary);
      assert(written.equals(psd), 'Written PSD bytes differ from validated container.');
      fs.renameSync(temporary, destination);
    } finally {
      if (fs.existsSync(temporary)) fs.unlinkSync(temporary);
    }
    assert.equal(sha256(fs.readFileSync(originalSource)), sourceHashBefore, 'Original source PNG changed during packaging.');
    for (const image of [...layers.map(l => l.image), composite]) {
      assert.equal(sha256(fs.readFileSync(image.file)), image.sha256, 'Native export PNG changed during packaging.');
    }
    results.push({
      id: entry.id, psd: destination, width: composite.width, height: composite.height,
      bytes: psd.length, sha256: sha256(psd), compression: 'PSD v1 PackBits RLE per channel row',
      independentLayers: layers.map(l => ({ name: l.name, png: l.image.file, sha256: l.image.sha256 })),
      mergedPng: composite.file, mergedPngSha256: composite.sha256,
      originalSource, originalSourceSha256: sourceHashBefore,
      originalSourceDimensions: [sourceBytes.readUInt32BE(16), sourceBytes.readUInt32BE(20)],
      sourceSpriteDimensions: [entry.sourceWidth, entry.sourceHeight],
      sourceSpriteRect: entry.sourceRectPixels || null,
      importedTextureDimensions: [entry.sourceTextureWidth || null, entry.sourceTextureHeight || null],
      validation: 'PSD header, layer names/order, all four RGBA channels of each layer, and merged RGBA roundtrip exactly match unchanged PNGs.',
      note: 'Native Unity component renders in an editable PSD container. Original source PNG remains separate and unchanged. Padding is not new detail; no enlargement, detail restoration, recoloring, or background removal is performed by packaging.'
    });
    process.stdout.write(`Packed ${entry.id}: ${psd.length} bytes; 3 layers + merged RGBA verified.\n`);
  }
  const reportName = process.argv[3] || 'standee-common-psd-validation.json';
  assert(/^[a-z0-9][a-z0-9_-]*\.json$/i.test(reportName), 'Unsafe report name.');
  const reportPath = path.join(outputDirectory, reportName);
  const report = {
    generatedAt: new Date().toISOString(), tool: 'PackageStandeeLayers.cjs', manifest: manifestPath,
    codecBoundaryCasesPassed: codecCases, count: results.length,
    totalPsdBytes: results.reduce((sum, item) => sum + item.bytes, 0),
    sourcePixelsModified: false, mergedImageCompositedByPackager: false, results
  };
  fs.writeFileSync(reportPath, JSON.stringify(report, null, 2) + '\n', 'utf8');
  process.stdout.write(JSON.stringify({ count: report.count, totalPsdBytes: report.totalPsdBytes, report: reportPath }) + '\n');
}

if (require.main === module) {
  try { main(); }
  catch (error) { console.error(error.stack || error.message); process.exitCode = 1; }
}

module.exports = { packBits, unpackBits, writePsd, verifyPsd, validateCodec };
