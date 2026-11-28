const { ipcRenderer } = require('electron');

const buffers = new Map();
const decoding = new Set();
const emit = message => ipcRenderer.send('audio-state', message);
let context, gain, filter, pan, dry, echo, delay, feedback, monitor, leftMeter, rightMeter;
let current = 0, token = 0, revision = 0, lastCycle = 0;
let next = [];
let voice, anchor = 0, startTime = 0, rate = 1, repeat = false, ended = false;
let mix = { gain: 0, pan: 0, cutoff: 22000, wet: 0, rate: 1 };
let wantsPlayback = false, announceStart = false, resuming;
let voiceStartedAt = 0, lastClockAt = 0, lastClock = 0, missingSince = 0;
let lastAudible = 0, repairs = 0, stableSince = 0;
const waveform = new Float32Array(256);

function initialize() {
  if (context) return;
  context = new AudioContext({ latencyHint: 'playback' });
  filter = context.createBiquadFilter();
  filter.type = 'lowpass';
  pan = context.createStereoPanner();
  gain = context.createGain();
  dry = context.createGain();
  echo = context.createGain();
  gain.gain.value = mix.gain;
  delay = context.createDelay(0.2);
  feedback = context.createGain();
  delay.delayTime.value = mix.delay ?? 0.1;
  feedback.gain.value = mix.feedback ?? 0.42;
  dry.gain.value = 1 - mix.wet;
  echo.gain.value = mix.wet * (1 - feedback.gain.value);
  filter.connect(pan).connect(dry).connect(gain).connect(context.destination);
  pan.connect(delay).connect(echo).connect(gain);
  delay.connect(feedback).connect(delay);
  monitor = context.createChannelSplitter(2);
  leftMeter = context.createAnalyser();
  rightMeter = context.createAnalyser();
  leftMeter.fftSize = rightMeter.fftSize = waveform.length;
  monitor.connect(leftMeter, 0);
  monitor.connect(rightMeter, 1);
  applyMix();
}

function applyMix() {
  if (!context) return;
  const now = context.currentTime;
  gain.gain.setTargetAtTime(mix.gain, now, 0.025);
  pan.pan.setTargetAtTime(mix.pan, now, 0.025);
  filter.frequency.setTargetAtTime(Math.min(mix.cutoff, context.sampleRate * 0.49), now, 0.025);
  const feedbackLevel = Math.min(0.7, Math.max(0, mix.feedback ?? 0.42));
  delay.delayTime.setTargetAtTime(Math.min(0.19, Math.max(0.025, mix.delay ?? 0.1)), now, 0.055);
  feedback.gain.setTargetAtTime(feedbackLevel, now, 0.055);
  dry.gain.setTargetAtTime(1 - mix.wet, now, 0.025);
  echo.gain.setTargetAtTime(mix.wet * (1 - feedbackLevel), now, 0.025);
  if (rate !== mix.rate) {
    anchor = rawPosition();
    startTime = now;
    rate = mix.rate;
    if (voice) voice.playbackRate.setValueAtTime(rate, now);
  }
}

function duration() { return buffers.get(current)?.duration ?? 0; }
function rawPosition(time = context?.currentTime ?? 0) {
  return voice ? anchor + Math.max(0, time - startTime) * rate : anchor;
}
function position(time) {
  const length = duration();
  const raw = rawPosition(time);
  return repeat && length > 0 ? raw % length : Math.min(raw, length);
}
function report(type = 'sample') {
  if (!current) return;
  const length = duration();
  const clock = context.getOutputTimestamp().contextTime || context.currentTime;
  if (repeat && voice && length > 0) {
    const cycle = Math.floor(rawPosition(clock) / length);
    if (cycle > lastCycle) {
      revision += cycle - lastCycle;
      lastCycle = cycle;
    }
  }
  emit({ type, id: current, token, revision, playing: !!voice && context.state === 'running',
    rate, position: Math.round(position(clock) * 1000), duration: Math.round(length * 1000) });
}
function detach() {
  if (!voice) return;
  const old = voice;
  voice = null;
  old.onended = null;
  old.stop();
  old.disconnect();
}

function resetOutputCheck() {
  voiceStartedAt = lastClockAt = performance.now();
  lastClock = context?.getOutputTimestamp().contextTime ?? 0;
  missingSince = stableSince = 0;
  lastAudible = position();
}

function resumeContext() {
  if (resuming === context) return;
  const output = context;
  resuming = output;
  output.resume().then(() => {
    if (context === output && wantsPlayback) startVoice();
  }).catch(() => {
    // The clock watchdog can rebuild a suspended or closed device context.
  }).finally(() => {
    if (resuming === output) resuming = undefined;
  });
}

function startVoice() {
  if (!wantsPlayback || !current || voice) return;
  if (context.state !== 'running') {
    resumeContext();
    return;
  }
  const source = context.createBufferSource();
  source.buffer = buffers.get(current);
  source.loop = repeat;
  source.playbackRate.value = rate;
  startTime = context.currentTime;
  voice = source;
  source.connect(filter);
  source.connect(monitor);
  source.onended = () => {
    if (voice !== source) return;
    voice = null;
    wantsPlayback = false;
    source.disconnect();
    anchor = duration();
    ended = true;
    next = next.filter(id => buffers.has(id));
    if (next.length > 0) {
      play(next.shift());
    } else {
      report('ended');
    }
  };
  source.start(startTime, anchor);
  resetOutputCheck();
  if (announceStart) {
    announceStart = false;
    report('started');
  }
}
function play(id) {
  if (!buffers.has(id)) throw new Error('曲目缓存已清除');
  detach();
  current = id;
  anchor = 0;
  ended = false;
  lastCycle = 0;
  revision++;
  wantsPlayback = announceStart = true;
  repairs = 0;
  resetOutputCheck();
  startVoice();
}
function seek(fraction) {
  if (!current) return;
  detach();
  anchor = duration() * Math.min(0.999, Math.max(0, fraction));
  ended = false;
  lastCycle = 0;
  revision++;
  repairs = 0;
  resetOutputCheck();
  startVoice();
  report();
}
function toggle() {
  if (!current) return;
  if (wantsPlayback) {
    anchor = position();
    wantsPlayback = false;
    detach();
    lastCycle = 0;
  } else {
    if (ended) {
      anchor = 0;
      revision++;
    }
    ended = false;
    wantsPlayback = true;
    repairs = 0;
    resetOutputCheck();
    startVoice();
  }
  report();
}
function setRepeat(value) {
  if (repeat === value) return;
  if (voice && repeat) {
    anchor = position();
    detach();
    repeat = false;
    lastCycle = 0;
    startVoice();
  } else {
    repeat = value;
    if (voice) voice.loop = repeat;
  }
}

function hasSignal(analyser) {
  analyser.getFloatTimeDomainData(waveform);
  return waveform.some(value => Math.abs(value) > 0.0002);
}

function expectsSignal(buffer, at) {
  const start = Math.max(0, Math.floor((at - 0.04) * buffer.sampleRate));
  const end = Math.min(buffer.length, start + Math.floor(0.065 * buffer.sampleRate));
  for (let channel = 0; channel < Math.min(2, buffer.numberOfChannels); channel++) {
    const source = buffer.getChannelData(channel);
    for (let i = start; i < end; i += 19)
      if (Math.abs(source[i]) > 0.002) return true;
  }
  return false;
}

function recoverOutput(resetDevice) {
  detach();
  anchor = Math.min(lastAudible, Math.max(0, duration() - 0.01));
  lastCycle = 0;
  revision++;
  if (++repairs > 3) {
    wantsPlayback = announceStart = false;
    emit({ type: 'playError', id: current, token, error: '音频设备没有输出，检查 Windows 播放设备后可重试' });
    report();
    return;
  }
  if (resetDevice) {
    const stale = context;
    context = undefined;
    stale.close().catch(() => {});
    initialize();
  }
  resetOutputCheck();
  startVoice();
}

function checkOutput() {
  if (!wantsPlayback || !current) return;
  const now = performance.now();
  const clock = context.getOutputTimestamp().contextTime;
  if (clock > lastClock) {
    lastClock = clock;
    lastClockAt = now;
  }
  if (now - lastClockAt > 1500) {
    recoverOutput(true);
    return;
  }
  if (context.state !== 'running') {
    resumeContext();
    return;
  }
  if (!voice) {
    startVoice();
    return;
  }
  if (now - voiceStartedAt < 300 || mix.gain < 0.001) return;

  if (hasSignal(leftMeter) || hasSignal(rightMeter)) {
    lastAudible = position(clock || context.currentTime);
    missingSince = 0;
    stableSince ||= now;
    if (now - stableSince > 900) repairs = 0;
  } else {
    stableSince = 0;
    if (!expectsSignal(voice.buffer, position())) {
      // Respect an intentionally silent intro or rest in the track.
      missingSince = 0;
      lastAudible = position(clock || context.currentTime);
      return;
    }
    missingSince ||= now;
    if (now - missingSince > 560) recoverOutput(false);
  }
}

async function decode(id, bytes) {
  decoding.add(id);
  try {
    initialize();
    const data = bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
    const buffer = await context.decodeAudioData(data);
    if (!decoding.has(id)) return;
    const cost = buffer.length * buffer.numberOfChannels * 4;
    if (!Number.isFinite(buffer.duration) || buffer.duration <= 0)
      throw new Error('音频没有可用的采样');
    buffers.set(id, buffer);
    emit({ type: 'loaded', id, bytes: cost });
  } catch (error) {
    if (decoding.has(id))
      emit({ type: 'loadError', id, error: error.name === 'EncodingError' ? '音频格式无法解码，请切换音质或音源' : error.message });
  } finally {
    decoding.delete(id);
  }
}

ipcRenderer.on('command', (_event, command) => {
  const { type, id } = command;
  try {
    switch (type) {
      case 'decode': decode(id, command.bytes); break;
      case 'drop': decoding.delete(id); if (current !== id) buffers.delete(id); break;
      case 'play':
        token = command.token;
        next = [];
        repeat = command.repeat;
        play(id);
        break;
      case 'stop':
        wantsPlayback = announceStart = false;
        detach();
        token = command.token;
        next = [];
        current = 0;
        anchor = 0;
        break;
      case 'next':
        if (command.token === token && command.current === current) next = command.ids;
        break;
      case 'repeat':
        if (command.token === token) setRepeat(command.repeat);
        break;
      case 'pause': if (command.token === token) toggle(); break;
      case 'seek': if (command.token === token) seek(command.fraction); break;
      case 'mix': mix = command; applyMix(); break;
    }
  } catch (error) {
    emit({ type: 'playError', id, token, error: error.message });
  }
});
setInterval(() => {
  try { checkOutput(); }
  catch (error) {
    wantsPlayback = announceStart = false;
    detach();
    emit({ type: 'playError', id: current, token, error: error.message });
  }
  report();
}, 60);
