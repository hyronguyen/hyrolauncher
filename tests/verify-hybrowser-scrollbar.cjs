const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const source = fs.readFileSync('WebLauncher.cs', 'utf8');
const script = source.match(/private const string BrowserScrollbarScript = @"([^\r\n]*)";/)[1];
new vm.Script(script);
for (const early of [false, true]) {
  const styles = [];
  const parent = { appendChild: style => styles.push(style) };
  let ready;
  const document = {
    head: early ? null : parent,
    documentElement: early ? null : parent,
    getElementById: id => styles.find(style => style.id === id),
    createElement: name => { assert.equal(name, 'style'); return {}; },
    addEventListener: (event, callback, options) => {
      assert.equal(event, 'DOMContentLoaded');
      assert.equal(options.once, true);
      ready = callback;
    }
  };
  vm.runInNewContext(script, { document });
  if (early) { document.documentElement = parent; ready(); }
  vm.runInNewContext(script, { document });
  assert.equal(styles.length, 1);
  assert.match(styles[0].textContent, /scrollbar-width:thin/);
  assert.doesNotMatch(styles[0].textContent, /overflow\s*:\s*hidden/);
}
console.log('PASS: page scrollbar script parses, handles early DOM, installs once, and preserves scrolling.');
