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
  recognition.onend();
  assert.equal(element('start').textContent, 'Start listening', 'fatal error did not stop UI');
  assert.match(element('status').textContent, /browser speech service could not connect/i);
  assert.equal(timers.length, 0, 'network error triggered an automatic retry');

  element('mode').value = 'browser';
  element('mode').listeners.change();
  assert.equal(element('mode').value, 'browser', 'mode selector reset browser mode');

  element('manual').value = 'spawn a Ford';
  element('manual-form').listeners.submit({ preventDefault() {} });
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(requests.length, 1, 'manual fallback did not submit');
  assert.equal(JSON.parse(requests[0].options.body).text, 'spawn a Ford');
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
  console.log('Browser voice page behavior tests passed.');
})().catch(error => { console.error(error); process.exitCode = 1; });
