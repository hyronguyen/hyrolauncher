const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs = require('fs');
const vm = require('vm');
const assert = require('assert');
const tasks = [], created = [], plays = [];
let click;
const context = {
  Audio: class {
    constructor(source) { this.src = encodeURI(source); created.push(this); }
    pause() {}
    play() { plays.push(this.effectSource); return Promise.resolve(); }
  },
  queueMicrotask: callback => tasks.push(callback),
  document: { addEventListener(name, handler) { if (name === 'click') click = handler; } }
};
context.window = context;
vm.createContext(context);
vm.runInContext(fs.readFileSync('ui/ui-effects.js', 'utf8'), context);
const settings = {};
const assets = { click: 'D:/UI VFX/click.mp3', startGameEffect: 'D:/UI VFX/start_game.mp3', switchGameEffect: 'D:/UI VFX/switch_to_game.mp3', backToAllEffect: 'D:/UI VFX/back_to_all_game.mp3' };
context.uiEffects.configure(settings, assets);
const control = { tagName: 'BUTTON' };
const event = () => ({ target: { closest: () => control } });
const flush = () => { while (tasks.length) tasks.shift()(); };
click(event()); context.uiEffects.play('start'); flush();
assert.equal(plays.length, 1, 'Start must not also play generic click');
assert(plays[0].endsWith('start_game.mp3'));
click(event()); context.uiEffects.play('switch'); flush();
assert(plays.at(-1).endsWith('switch_to_game.mp3'));
click(event()); context.uiEffects.play('back'); flush();
assert(plays.at(-1).endsWith('back_to_all_game.mp3'));
click(event()); flush();
assert(plays.at(-1).endsWith('click.mp3'));
assert.equal(created.length, 4);
context.uiEffects.play('start');
assert.equal(created.length, 4, 'Audio players must be reused even when paths have spaces');
settings.DisableEffects = true;
context.uiEffects.configure(settings, assets);
const before = plays.length;
click(event()); context.uiEffects.play('start'); flush();
assert.equal(plays.length, before, 'Disable effects must mute all 4 sounds');
for (const file of ['ui/index.html', 'ui/plugin.html']) new vm.Script(fs.readFileSync(file, 'utf8').match(/<script>([\s\S]*?)<\/script>/)[1]);
console.log('PASS: 4 sound mappings, no double click, reuse audio, Disable effects, UI syntax');
