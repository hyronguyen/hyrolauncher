const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs = require('fs');
const vm = require('vm');
const assert = require('assert');
const layers = [];
let focused;
const field = () => ({ value: '', focus() { focused = this; } });
const document = {
  activeElement: { isConnected: true, focus() {} }, documentElement: { lang: 'en' },
  body: { dataset: {}, appendChild(layer) { layers.push(layer); } },
  createElement() {
    const controls = { message: {}, input: field(), form: {}, cancel: field(), submit: field() };
    return {
      controls, innerHTML: '',
      querySelector(selector) {
        if (selector === '.appDialogMessage') return controls.message;
        if (selector === 'input') return this.innerHTML.includes('appDialogInput') ? controls.input : null;
        if (selector === 'form') return controls.form;
        if (selector === '.appDialogCancel') return this.innerHTML.includes('appDialogCancel') ? controls.cancel : null;
        return controls.submit;
      },
      querySelectorAll() { return [controls.cancel, controls.submit]; },
      remove() { this.removed = true; }
    };
  }
};
const context = { document, Event: class { constructor(type) { this.type = type; } }, dispatchEvent() {} };
context.window = context;
vm.createContext(context);
vm.runInContext(fs.readFileSync('ui/dialogs.js', 'utf8'), context);
(async () => {
  let promise = context.appConfirm('Delete item?');
  assert.equal(focused, layers.at(-1).controls.cancel);
  layers.at(-1).controls.cancel.onclick();
  assert.equal(await promise, false);
  promise = context.appPrompt('New category');
  layers.at(-1).controls.input.value = '  RPG  ';
  layers.at(-1).controls.form.onsubmit({ preventDefault() {} });
  assert.equal(await promise, 'RPG');
  const first = context.appAlert('<script>untrusted</script>');
  const count = layers.length;
  const next = context.appConfirm('Proceed?');
  assert.equal(layers.length, count, 'Dialogs must queue');
  assert.equal(layers.at(-1).controls.message.textContent, '<script>untrusted</script>');
  layers.at(-1).controls.form.onsubmit({ preventDefault() {} });
  await first;
  assert.equal(layers.length, count + 1);
  layers.at(-1).onkeydown({ key: 'Escape', preventDefault() {} });
  assert.equal(await next, false);
  for (const file of ['ui/index.html', 'ui/plugin.html']) assert(!/\b(alert|confirm|prompt)\(/.test(fs.readFileSync(file, 'utf8')), 'Browser popup remains: ' + file);
  console.log('PASS: app confirmation cancel, prompt trim, queued dialogs, safe message rendering, Esc, no browser popups');
})().catch(error => { console.error(error); process.exitCode = 1; });
