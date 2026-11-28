const { app, BrowserWindow, ipcMain, session, net } = require('electron');
const fs = require('node:fs/promises');
const path = require('node:path');
const { fileURLToPath } = require('node:url');
const socket = require('node:net');
const readline = require('node:readline');

const argumentsByName = new Map(process.argv.filter(arg => arg.startsWith('--') && arg.includes('='))
  .map(arg => [arg.slice(2, arg.indexOf('=')), arg.slice(arg.indexOf('=') + 1)]));
const pipeName = argumentsByName.get('pipe');
const profile = argumentsByName.get('profile');
const cacheRoot = argumentsByName.get('cache');
if (!/^PeakAudio-[a-f\d]{32}$/.test(pipeName) || !profile || !cacheRoot) {
  app.exit(2);
  return;
}

const maximumBytes = 64 * 1024 * 1024;
const pending = new Map();
let window;
let shuttingDown = false;
app.setPath('userData', profile);
app.disableHardwareAcceleration();
app.commandLine.appendSwitch('autoplay-policy', 'no-user-gesture-required');
app.commandLine.appendSwitch('disable-renderer-backgrounding');
const transport = socket.createConnection('\\\\.\\pipe\\' + pipeName);

function shutdown() {
  if (shuttingDown) return;
  shuttingDown = true;
  for (const request of pending.values()) request.abort();
  transport.destroy();
  app.exit();
}
function reply(data) {
  if (transport.writable) {
    // Clock reports are replaceable. Never queue them behind a slow reader.
    if (data.type === 'sample' && transport.writableLength > 4096) return;
    transport.write(JSON.stringify(data) + '\n');
  }
}
transport.on('error', shutdown);
transport.on('close', shutdown);

function validateRemote(url) {
  if (url.protocol !== 'https:' || url.username || url.password)
    throw new Error('音频地址必须使用 HTTPS');
}
async function download(address, signal) {
  const url = new URL(address);
  if (url.protocol === 'file:') {
    const root = await fs.realpath(cacheRoot);
    const file = await fs.realpath(fileURLToPath(url));
    const relative = path.relative(root, file);
    if (relative.startsWith('..') || path.isAbsolute(relative) || !/\.mp3$/i.test(file))
      throw new Error('音频文件不在播放器缓存中');
    const info = await fs.stat(file);
    if (!info.isFile() || info.size > maximumBytes) throw new Error('音频文件超出缓存限制');
    return fs.readFile(file, { signal });
  }

  validateRemote(url);
  return new Promise((resolve, reject) => {
    const request = net.request({ url: url.href, redirect: 'manual', credentials: 'omit' });
    let redirects = 0;
    let total = 0;
    const parts = [];
    const cancel = () => fail(new Error('下载音频已取消'));
    function fail(error) {
      signal.removeEventListener('abort', cancel);
      reject(error);
      request.abort();
    }
    if (signal.aborted) {
      cancel();
      return;
    }
    signal.addEventListener('abort', cancel, { once: true });
    request.on('error', fail);
    request.on('redirect', (_status, _method, location) => {
      try {
        if (++redirects > 8) throw new Error('音频重定向次数过多');
        validateRemote(new URL(location));
        request.followRedirect();
      } catch (error) {
        fail(error);
      }
    });
    request.on('response', response => {
      if (response.statusCode < 200 || response.statusCode >= 300) {
        fail(new Error('下载音频失败（HTTP ' + response.statusCode + '）'));
        return;
      }
      const contentLength = response.headers['content-length'];
      if (Number(contentLength?.[0]) > maximumBytes) {
        fail(new Error('单首音频超过 64 MiB 缓存上限'));
        return;
      }
      response.on('error', fail);
      response.on('data', value => {
        total += value.length;
        if (total > maximumBytes) fail(new Error('单首音频超过 64 MiB 缓存上限'));
        else parts.push(value);
      });
      response.on('end', () => {
        signal.removeEventListener('abort', cancel);
        resolve(Buffer.concat(parts, total));
      });
    });
    request.end();
  });
}

// Chromium decodes WAV/OGG/MP3 directly. Convert AIFF PCM to a WAV container
// so the previous AIFF playback path remains available in the new backend.
function makeDecodable(bytes) {
  if (bytes.length < 12 || bytes.toString('ascii', 0, 4) !== 'FORM') return bytes;
  const kind = bytes.toString('ascii', 8, 12);
  if (kind !== 'AIFF' && kind !== 'AIFC') return bytes;
  const end = Math.min(bytes.length, 8 + bytes.readUInt32BE(4));
  let channels, frames, bits, rate, encoding, pcm;
  for (let at = 12; at + 8 <= end;) {
    const name = bytes.toString('ascii', at, at + 4);
    const size = bytes.readUInt32BE(at + 4);
    const start = at + 8, finish = start + size;
    if (finish > end) break;
    if (name === 'COMM' && size >= (kind === 'AIFF' ? 18 : 22)) {
      channels = bytes.readUInt16BE(start);
      frames = bytes.readUInt32BE(start + 2);
      bits = bytes.readUInt16BE(start + 6);
      const exponent = bytes.readUInt16BE(start + 8);
      rate = Math.round(Number(bytes.readBigUInt64BE(start + 10)) * 2 ** (exponent - 16383 - 63));
      encoding = kind === 'AIFF' ? 'NONE' : bytes.toString('ascii', start + 18, start + 22);
    } else if (name === 'SSND' && size >= 8) {
      const offset = start + 8 + bytes.readUInt32BE(start);
      if (offset <= finish) pcm = bytes.subarray(offset, finish);
    }
    at = finish + (size & 1);
  }
  const floating = encoding === 'fl32' || encoding === 'FL32' || encoding === 'fl64' || encoding === 'FL64';
  const length = channels * (bits / 8) * frames;
  if (!pcm || !channels || channels > 8 || !frames || !(rate >= 8000 && rate <= 192000) ||
      length > pcm.length || !(floating ? [32, 64] : [8, 16, 24, 32]).includes(bits) ||
      (!floating && !['NONE', 'twos', 'sowt'].includes(encoding)))
    throw new Error('AIFF 音频格式无法解码');
  const size = 44 + length + (length & 1);
  const wave = Buffer.alloc(size);
  wave.write('RIFF');
  wave.writeUInt32LE(size - 8, 4);
  wave.write('WAVEfmt ', 8);
  wave.writeUInt32LE(16, 16);
  wave.writeUInt16LE(floating ? 3 : 1, 20);
  wave.writeUInt16LE(channels, 22);
  wave.writeUInt32LE(rate, 24);
  const frameSize = channels * bits / 8;
  wave.writeUInt32LE(rate * frameSize, 28);
  wave.writeUInt16LE(frameSize, 32);
  wave.writeUInt16LE(bits, 34);
  wave.write('data', 36);
  wave.writeUInt32LE(length, 40);
  pcm.copy(wave, 44, 0, length);
  const payload = wave.subarray(44, 44 + length);
  if (bits === 8) {
    for (let i = 0; i < payload.length; i++) payload[i] ^= 128;
  } else if (encoding !== 'sowt') {
    if (bits === 16) payload.swap16();
    if (bits === 32) payload.swap32();
    if (bits === 64) payload.swap64();
    if (bits === 24) {
      for (let i = 0; i < payload.length; i += 3) {
        const first = payload[i];
        payload[i] = payload[i + 2];
        payload[i + 2] = first;
      }
    }
  }
  return wave;
}

async function load({ id, url }) {
  const request = new AbortController();
  pending.set(id, request);
  const timer = setTimeout(() => request.abort('timeout'), 45000);
  try {
    const bytes = makeDecodable(await download(url, request.signal));
    if (pending.get(id) === request)
      window.webContents.send('command', { type: 'decode', id, bytes });
  } catch (error) {
    if (pending.get(id) === request)
      reply({ type: 'loadError', id, error: request.signal.aborted ? '下载音频超时' :
        (/^[\u4e00-\u9fff]/.test(error.message) ? error.message : '无法下载音频') });
  } finally {
    clearTimeout(timer);
    if (pending.get(id) === request) pending.delete(id);
  }
}
function command(line) {
  if (line.length > 32768) return;
  let message;
  try { message = JSON.parse(line); } catch { return; }
  if (message.type === 'load') {
    load(message);
    return;
  }
  if (message.type === 'drop') {
    const request = pending.get(message.id);
    pending.delete(message.id);
    request?.abort();
  }
  window.webContents.send('command', message);
}
app.on('window-all-closed', shutdown);
app.whenReady().then(async () => {
  session.defaultSession.setPermissionRequestHandler((_sender, _permission, callback) => callback(false));
  window = new BrowserWindow({
    show: false,
    width: 64,
    height: 64,
    webPreferences: { preload: path.join(__dirname, 'preload.cjs'), backgroundThrottling: false,
      nodeIntegration: false, contextIsolation: true, sandbox: true }
  });
  window.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  window.webContents.on('will-navigate', event => event.preventDefault());
  window.webContents.on('render-process-gone', shutdown);
  ipcMain.on('audio-state', (event, data) => {
    if (event.sender === window.webContents) reply(data);
  });
  await window.loadURL('data:text/html,<!doctype html><title>Muonium Audio</title>');
  reply({ type: 'ready' });
  readline.createInterface({ input: transport, crlfDelay: Infinity }).on('line', command).on('close', shutdown);
}).catch(shutdown);
