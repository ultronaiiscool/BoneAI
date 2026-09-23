const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');

const html = fs.readFileSync('assets/browser-voice.html', 'utf8');
const start = html.indexOf('<script>') + '<script>'.length;
const end = html.indexOf('</script>', start);
assert(start >= '<script>'.length && end > start, 'embedded script missing');
const source = html.slice(start, end)
  .replace('__TOKEN_JSON__', JSON.stringify('test-token'))
  .replace('__WAKE_JSON__', JSON.stringify('Hey BoneAI'));
new vm.Script(source);

const elements = new Map();
function element(id) {
  if (!elements.has(id)) elements.set(id, {
    value: id === 'mode' ? 'auto' : '', textContent: '', disabled: false,
    listeners: {}, addEventListener(type, listener) { this.listeners[type] = listener; },
    querySelector() { return { disabled: false }; }
  });
  return elements.get(id);
}
class SpeechRecognition {
  constructor() { SpeechRecognition.last = this; }
  start() { this.onstart?.(); }
  abort() { this.onend?.(); }
  static async available() { return 'available'; }
  static async install() { return true; }
}
SpeechRecognition.prototype.processLocally = false;
const timers = [];
const requests = [];
const context = vm.createContext({
  window: { SpeechRecognition }, document: { getElementById: element },
  setTimeout(callback) { timers.push(callback); return timers.length; },
  clearTimeout() {},
  fetch: async (url, options) => { requests.push({ url, options }); return { status: 202 }; },
  Date, console
});
vm.runInContext(source, context);

(async () => {
  await element('start').listeners.click();
  assert.equal(SpeechRecognition.last.processLocally, true, 'local mode not enabled');
  assert.equal(element('start').textContent, 'Stop listening');
  const recognition = SpeechRecognition.last;
  recognition.onerror({ error: 'network' });
  assert.equal(element('start').textContent, 'Stop listening', 'local failure did not keep automatic fallback active');
  assert.equal(timers.length, 1, 'local failure did not schedule browser fallback');
  timers.shift()();
  assert.notEqual(SpeechRecognition.last, recognition, 'browser fallback did not start');
  assert.equal(SpeechRecognition.last.processLocally, false, 'fallback still used local mode');
  SpeechRecognition.last.onerror({ error: 'network' });
  assert.equal(element('start').textContent, 'Start listening', 'fatal error did not stop UI');
  assert.match(element('status').textContent, /browser speech service could not connect/i);
  assert.equal(timers.length, 0, 'network error triggered an automatic retry without another provider');

  element('mode').value = 'browser';
  element('mode').listeners.change();
  assert.equal(element('mode').value, 'browser', 'mode selector reset browser mode');

  element('manual').value = 'spawn a Ford';
  element('manual-form').listeners.submit({ preventDefault() {} });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(requests.filter(item => item.url.startsWith('/prompt?')).length, 1, 'manual fallback did not submit');
  assert.equal(JSON.parse(requests.find(item => item.url.startsWith('/prompt?')).options.body).text, 'spawn a Ford');
  assert.equal(element('manual').value, '', 'manual field was not cleared');

  const fallbackElements = new Map();
  function fallbackElement(id) {
    if (!fallbackElements.has(id)) fallbackElements.set(id, {
      value: id === 'mode' ? 'auto' : '', textContent: '', disabled: false,
      listeners: {}, addEventListener(type, listener) { this.listeners[type] = listener; },
      querySelector() { return { disabled: false }; }
    });
    return fallbackElements.get(id);
  }
  class LocalUnavailable extends SpeechRecognition {
    static async available() { return 'unavailable'; }
  }
  const fallbackContext = vm.createContext({
    window: { SpeechRecognition: LocalUnavailable }, document: { getElementById: fallbackElement },
    setTimeout, clearTimeout, fetch: async () => ({ status: 202 }), Date, console
  });
  vm.runInContext(source, fallbackContext);
  await fallbackElement('start').listeners.click();
  assert.notEqual(LocalUnavailable.last.processLocally, true, 'automatic fallback did not use browser service');
  assert.equal(fallbackElement('start').textContent, 'Stop listening');
  LocalUnavailable.last.onerror({ error: 'network' });
  assert.equal(fallbackElement('start').textContent, 'Start listening');

  let microphoneRequests = 0;
  const cloudElements = new Map();
  function cloudElement(id) {
    if (!cloudElements.has(id)) cloudElements.set(id, {
      value: id === 'mode' ? 'auto' : '', textContent: '', disabled: false,
      listeners: {}, addEventListener(type, listener) { this.listeners[type] = listener; },
      querySelector() { return { disabled: false }; }
    });
    return cloudElements.get(id);
  }
  class FakeAudioContext {
    sampleRate = 48000;
    destination = {};
    createMediaStreamSource() { return { connect() {}, disconnect() {} }; }
    createScriptProcessor() { return { connect() {}, disconnect() {} }; }
    close() {}
  }
  const cloudContext = vm.createContext({
    window: { SpeechRecognition: LocalUnavailable, AudioContext: FakeAudioContext },
    navigator: { mediaDevices: { async getUserMedia() { microphoneRequests++; return { getTracks: () => [{ stop() {} }] }; } } },
    document: { getElementById: cloudElement },
    setTimeout, clearTimeout, Date, console,
    fetch: async url => url.startsWith('/capabilities?')
      ? { ok: true, async json() { return { groq: true, cloudflare: true }; } }
      : { status: 202 }
  });
  vm.runInContext(source, cloudContext);
  await new Promise(resolve => setImmediate(resolve));
  await cloudElement('start').listeners.click();
  LocalUnavailable.last.onerror({ error: 'network' });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(microphoneRequests, 1, 'network error did not start configured web fallback');
  assert.match(cloudElement('status').textContent, /groq.*cloudflare/i, 'web fallback order is not visible');
  console.log('Browser voice page behavior tests passed.');
})().catch(error => { console.error(error); process.exitCode = 1; });
