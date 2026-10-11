(() => {
  let settings = {}, assets = {}, clickEvent = null;
  const players = new Map();
  const keys = { start: 'startGameEffect', switch: 'switchGameEffect', back: 'backToAllEffect', click: 'click' };
  const url = path => /^(https?:|file:|data:|blob:)/i.test(path) ? path : 'file:///' + path.replace(/\\/g, '/');
  function stop() { players.forEach(audio => { audio.pause(); }); }
  function play(kind) {
    if (clickEvent) clickEvent.uiEffectHandled = true;
    if (settings.DisableEffects) return;
    const path = assets[keys[kind]];
    if (!path) return;
    const source = url(path);
    let audio = players.get(kind);
    if (!audio || audio.effectSource !== source) {
      if (audio) audio.pause();
      audio = new Audio(source);
      audio.effectSource = source;
      audio.preload = 'metadata';
      audio.volume = .45;
      players.set(kind, audio);
    }
    stop();
    try { audio.currentTime = 0; } catch (error) {}
    const result = audio.play();
    if (result) result.catch(() => {});
  }
  window.uiEffects = {
    configure(nextSettings, nextAssets) { settings = nextSettings || {}; assets = nextAssets || {}; if (settings.DisableEffects) stop(); },
    play
  };
  document.addEventListener('click', event => {
    clickEvent = event;
    const control = event.target.closest('button, select, input, summary, label, [role="button"]');
    queueMicrotask(() => {
      if (control && !control.disabled && !(control.tagName === 'LABEL' && control.control) && !event.uiEffectHandled) play('click');
      if (clickEvent === event) clickEvent = null;
    });
  }, true);
})();
