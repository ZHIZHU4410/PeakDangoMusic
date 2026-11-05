const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../Desktop/CdpProgressReader.cs'), 'utf8');
const expression = source.match(/internal const string Expression = """\s*([\s\S]*?)\s*""";/)?.[1];
assert.ok(expression, 'CDP expression exists');

function clock(text, parent = {}) {
  return { textContent: text, parentElement: parent, childElementCount: 0, getClientRects: () => [1] };
}
function evaluate(nodes, { state = 'playing', cache = null, title = 'Example', artist = 'Artist' } = {}) {
  const context = {
    location: { protocol: 'orpheus:' },
    window: { __dangoReadOnlyRequire: cache },
    navigator: { mediaSession: { metadata: { title, artist }, playbackState: state } },
    document: { title: `${title} - ${artist}`, querySelectorAll: selector => selector === 'audio' ? [] : nodes },
  };
  return vm.runInNewContext(expression, context, { timeout: 1000 });
}

const parent = {};
const split = evaluate([clock('1:23', parent), clock('3:45', parent)]);
assert.equal(split.source, 'timer');
assert.equal(split.positionMs, 83000);
assert.equal(split.durationMs, 225000);
assert.equal(split.playing, true);
assert.equal(evaluate([clock('1:23 / 3:45')]).positionMs, 83000);
const grandparent = {};
assert.equal(evaluate([clock('1:23', { parentElement: grandparent }),
  clock('3:45', { parentElement: grandparent })]).durationMs, 225000);
assert.equal(evaluate([clock('1:23', {}), clock('3:45', {})]), null);
assert.equal(evaluate([clock('1:23', parent), clock('3:45', parent), clock('0:42')]), null);
assert.equal(evaluate([clock('1:23', parent), clock('3:45', parent)], { state: 'none' }), null);

const p = { curPlaying: { track: { name: 'Example', artists: [{ name: 'Artist' }] }, resourceId: 123 },
  playId: 123, playingState: 2, resourceDuration: 225 };
const q = { playId: 123, current: 83 };
const cache = { c: {
  8: { exports: { a: { getStore: () => ({ playing: p }) } } },
  148: { exports: { b: () => q } },
  7: { exports: { k: { Pause: 1, Playing: 2, Stop: 0 } } },
} };
const legacy = evaluate([], { cache });
assert.equal(legacy.source, 'native-cache');
assert.equal(legacy.songId, 123);
assert.equal(legacy.positionMs, 83000);
console.log('CDP expression: legacy, combined, split and ambiguous cases passed');
