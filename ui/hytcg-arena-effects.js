'use strict';

/* Local, finite Arena actions. The native battle remains the source of truth. */
window.HyTcgArenaEffects = (() => {
  const sessions = new Set(), sounds = new Set();
  const field = (value, key, fallback = '') => value?.[key] ?? value?.[key[0].toLowerCase() + key.slice(1)] ?? fallback;
  const clamp = (value, minimum, maximum) => Math.max(minimum, Math.min(maximum, value));
  const svg = body => '<svg viewBox="0 0 200 240" aria-hidden="true" focusable="false">' + body + '</svg>';
  const shieldArt = svg('<path d="M100 17 174 46v77c0 45-36 76-74 101-38-25-74-56-74-101V46Z" fill="currentColor" fill-opacity=".11" stroke="currentColor" stroke-width="6"/><path d="m100 34 56 23v66c0 32-26 60-56 80-30-20-56-48-56-80V57Z" fill="none" stroke="currentColor" stroke-opacity=".62" stroke-width="2"/><path d="m100 58 14 37 40 14-40 14-14 38-14-38-40-14 40-14Z" fill="currentColor" fill-opacity=".92"/><path d="M100 25v18M100 200v15M33 61l13 6M154 67l13-6" stroke="white" stroke-opacity=".9" stroke-width="3"/>');
  const slashArt = '<svg viewBox="0 0 200 200" aria-hidden="true" focusable="false"><path d="M19 181C44 92 87 47 183 17c-70 43-111 88-164 164Z" fill="currentColor"/><path d="M46 181C70 104 112 65 180 43c-47 34-85 74-134 138Z" fill="white" fill-opacity=".85"/></svg>';
  const starArt = '<svg viewBox="0 0 100 100" aria-hidden="true" focusable="false"><path d="m50 3 13 33 34 14-34 13-13 34-14-34L3 50l33-14Z" fill="currentColor"/><path d="m50 21 8 22 21 7-21 8-8 21-7-21-22-8 22-7Z" fill="white" fill-opacity=".75"/></svg>';
  function artOf(unit) { return unit?.querySelector('.lifeMiniCard') || unit?.querySelector(':scope > svg') || unit; }
  function anchor(unit, battlefield) {
    const bounds = battlefield.getBoundingClientRect(), rect = artOf(unit)?.getBoundingClientRect();
    if (!rect) return { x: bounds.width / 2, y: bounds.height / 2, width: 120, height: 150 };
    return { x: rect.left - bounds.left - battlefield.clientLeft + rect.width / 2,
      y: rect.top - bounds.top - battlefield.clientTop + rect.height / 2, width: rect.width, height: rect.height };
  }
  function setGuard(unit, enabled) {
    if (!unit) return;
    unit.dataset.arenaFxGuarded = enabled ? 'true' : 'false';
    let aura = unit.querySelector(':scope > .arenaFxGuardAura');
    if (!enabled) { aura?.remove(); return; }
    if (!aura) {
      aura = document.createElement('i'); aura.className = 'arenaFxGuardAura';
      aura.setAttribute('aria-hidden', 'true'); aura.innerHTML = shieldArt; unit.append(aura);
    }
  }
  function syncGuards(battlefield, guardIds = []) {
    const ids = new Set(guardIds.map(String));
    battlefield?.querySelectorAll('[data-arena-unit]').forEach(unit => setGuard(unit, ids.has(unit.dataset.arenaUnit)));
  }
  function stopSound(sound) {
    if (!sound || sound.stopped) return;
    sound.stopped = true;
    clearTimeout(sound.timer); sound.audio.pause();
    sound.audio.removeAttribute('src'); sound.audio.load(); sounds.delete(sound);
  }
  function soundFor(session, prefs, kind, skill) {
    if (!prefs?.enabled || prefs.muted) return;
    const volume = clamp(Number.isFinite(Number(prefs.volume)) ? Number(prefs.volume) : .6, 0, 1);
    if (!volume) return;
    const file = kind === 'heal' || kind === 'focus' ? 'healing effect.mp3' : kind === 'guard' || skill === 'burst' || skill === 'nova' ? 'burst effect.mp3' : 'attack effect.mp3';
    try {
      const base = new URL(prefs.baseUrl || '../assets/hytcg/', document.baseURI);
      const url = new URL(encodeURIComponent(file), base);
      if (!['http:', 'https:', 'file:'].includes(url.protocol) || url.origin !== new URL(document.baseURI).origin) return;
      const audio = new Audio(url.href); audio.preload = 'auto'; audio.volume = volume * (kind === 'guard' ? .62 : 1);
      const record = { audio, battlefield: session.battlefield, timer: 0 };
      sounds.add(record); record.timer = setTimeout(() => stopSound(record), 5000);
      audio.addEventListener('ended', () => stopSound(record), { once: true });
      audio.addEventListener('error', () => stopSound(record), { once: true });
      Promise.resolve(audio.play()).catch(() => stopSound(record));
    } catch { /* A sound failure never suppresses the visual action. */ }
  }
  function createSession(battlefield, kind, skill) {
    const layer = document.createElement('div'); layer.className = 'arenaFx';
    layer.dataset.kind = kind; layer.dataset.skill = skill; layer.setAttribute('aria-hidden', 'true');
    battlefield.append(layer);
    const session = { battlefield, layer, animations: [], waits: new Set(), cancelled: false, completed: false };
    sessions.add(session);
    session.wait = milliseconds => new Promise(resolve => {
      if (session.cancelled) { resolve(false); return; }
      const pending = { timer: 0, resolve };
      pending.timer = setTimeout(() => { session.waits.delete(pending); resolve(!session.cancelled); }, milliseconds);
      session.waits.add(pending);
    });
    session.animate = (node, frames, timing) => {
      if (session.cancelled || !node?.animate) return null;
      try {
        const animation = node.animate(frames, Object.assign({ easing: 'ease-out', fill: 'both' }, timing));
        animation.finished.catch(() => {}); session.animations.push(animation); return animation;
      } catch { return null; }
    };
    session.node = (name, position, width, height = width, content = '') => {
      const node = document.createElement('i'); node.className = name;
      node.style.left = position.x + 'px'; node.style.top = position.y + 'px';
      node.style.width = width + 'px'; node.style.height = height + 'px';
      if (content) node.innerHTML = content; layer.append(node); return node;
    };
    session.dispose = () => {
      for (const pending of session.waits) { clearTimeout(pending.timer); pending.resolve(false); }
      session.waits.clear(); session.animations.forEach(animation => animation.cancel()); session.animations.length = 0;
      layer.remove(); sessions.delete(session);
    };
    return session;
  }
  function ring(session, name, position, size, delay = 0, color = '') {
    const node = session.node(name, position, size);
    if (color) node.style.color = color;
    session.animate(node, [{ opacity: 0, transform: 'translate(-50%,-50%) scale(.15)' },
      { opacity: 1, transform: 'translate(-50%,-50%) scale(.65)', offset: .22 },
      { opacity: 0, transform: 'translate(-50%,-50%) scale(1.2)' }], { duration: 620, delay });
    return node;
  }
  function particles(session, center, count, color, heal = false, delay = 0) {
    for (let i = 0; i < count; i++) {
      const angle = i * Math.PI * 2 / count, distance = 38 + (i % 3) * 15;
      const x = Math.cos(angle) * distance, y = Math.sin(angle) * distance;
      const start = heal ? { x: center.x + x * .65, y: center.y + 58 } : center;
      const node = session.node('arenaFxSpark' + (heal ? ' healing' : ''), start, i % 3 === 0 ? 8 : 5);
      node.style.color = color;
      session.animate(node, [{ opacity: 0, transform: 'translate(-50%,-50%) scale(.4)' },
        { opacity: 1, transform: 'translate(-50%,-50%) scale(1)', offset: .18 },
        { opacity: 0, transform: 'translate(calc(-50% + ' + (heal ? x * .25 : x) + 'px),calc(-50% + ' + (heal ? -105 - i % 3 * 18 : y) + 'px)) scale(.2)' }],
      { duration: heal ? 920 : 560, delay: delay + i % 4 * 38 });
    }
  }
  function hit(session, target, point, skill, blocked) {
    const big = skill === 'burst' || skill === 'nova', color = blocked ? '#7ceaff' : skill === 'nova' ? '#d8b3ff' : big ? '#ffb861' : '#f4f3ff';
    if (blocked) {
      const shield = session.node('arenaFxShield blocking', point, 125, 150, shieldArt);
      session.animate(shield, [{ opacity: 0, transform: 'translate(-50%,-50%) scale(.7)' },
        { opacity: 1, transform: 'translate(-50%,-50%) scale(1.1)', offset: .22 },
        { opacity: .8, transform: 'translate(calc(-50% - 6px),-50%) scale(1)', offset: .4 },
        { opacity: 1, transform: 'translate(calc(-50% + 5px),-50%) scale(1)', offset: .55 },
        { opacity: 0, transform: 'translate(-50%,-50%) scale(1.17)' }], { duration: 600 });
      setGuard(target, false);
    } else {
      const slash = session.node('arenaFxSlash', point, big ? 175 : 135, big ? 175 : 135, slashArt);
      slash.style.color = color;
      session.animate(slash, [{ opacity: 0, transform: 'translate(-50%,-50%) rotate(-15deg) scale(.45)' },
        { opacity: 1, transform: 'translate(-50%,-50%) rotate(10deg) scale(1)', offset: .28 },
        { opacity: 0, transform: 'translate(-50%,-50%) rotate(25deg) scale(1.15)' }], { duration: 420 });
      const art = artOf(target);
      session.animate(art, [{ transform: 'translate(0,0)', opacity: 1 }, { transform: 'translate(-9px,3px)', opacity: .4, offset: .15 },
        { transform: 'translate(8px,-2px)', opacity: 1, offset: .32 }, { transform: 'translate(-5px,0)', opacity: .65, offset: .5 },
        { transform: 'translate(3px,0)', opacity: 1, offset: .68 }, { transform: 'translate(0,0)', opacity: 1 }], { duration: 400 });
    }
    ring(session, 'arenaFxImpact', point, big ? 190 : 140, 0, color);
    particles(session, point, skill === 'nova' ? 18 : big ? 14 : 10, color);
    if (skill === 'nova') ring(session, 'arenaFxNovaRing', point, 250, 85, '#ad92ff');
  }
  async function attack(session, options, source, destination, skill) {
    const dx = destination.x - source.x, dy = destination.y - source.y, distance = Math.max(1, Math.hypot(dx, dy));
    const big = skill === 'burst' || skill === 'nova', duration = big ? 1350 : 1100, impactAt = big ? 520 : 440;
    const color = skill === 'nova' ? '#ceb0ff' : big ? '#ffb36c' : '#a4e8ff';
    const charge = ring(session, 'arenaFxCharge', source, big ? 115 : 75, 0, color);
    if (skill === 'nova') particles(session, source, 12, color, false);
    session.animate(artOf(options.actor), [{ transform: 'translate(0,0) scale(1)' },
      { transform: 'translate(' + (-dx / distance * 9) + 'px,' + (-dy / distance * 9) + 'px) scale(.97)', offset: .18 },
      { transform: 'translate(' + (dx / distance * 42) + 'px,' + (dy / distance * 35) + 'px) scale(1.06)', offset: .5 },
      { transform: 'translate(0,0) scale(1)' }], { duration: impactAt + 220, easing: 'cubic-bezier(.22,.65,.3,1)' });
    const projectile = session.node('arenaFxProjectile ' + (big ? 'burst' : 'strike') + (skill === 'nova' ? ' nova' : ''), source, big ? 56 : 30);
    projectile.style.color = color; projectile.style.setProperty('--arena-flight-angle', Math.atan2(dy, dx) * 180 / Math.PI + 'deg');
    session.animate(projectile, [{ opacity: 0, transform: 'translate(-50%,-50%) scale(.25)' },
      { opacity: 1, transform: 'translate(-50%,-50%) scale(1)', offset: .22 },
      { opacity: 1, transform: 'translate(calc(-50% + ' + dx + 'px),calc(-50% + ' + dy + 'px)) scale(' + (big ? 1.25 : .85) + ')', offset: .96 },
      { opacity: 0, transform: 'translate(calc(-50% + ' + dx + 'px),calc(-50% + ' + dy + 'px)) scale(1.7)' }], { duration: impactAt - 80, delay: 80, easing: 'cubic-bezier(.5,.02,.7,.55)' });
    charge.dataset.variant = skill;
    if (!await session.wait(impactAt)) return;
    const blocked = !!options.blocked || options.target?.dataset.arenaFxGuarded === 'true';
    hit(session, options.target, destination, skill, blocked); impact(options, session);
    await session.wait(duration - impactAt);
  }
  async function heal(session, options, destination) {
    const cone = session.node('arenaFxHealLight', { x: destination.x, y: destination.y + 35 }, 140, 240);
    session.animate(cone, [{ opacity: 0, transform: 'translate(-50%,-50%) scaleY(.15)' },
      { opacity: .88, transform: 'translate(-50%,-50%) scaleY(1)', offset: .25 },
      { opacity: 0, transform: 'translate(-50%,-65%) scaleY(1.1)' }], { duration: 1120 });
    for (let i = 0; i < 3; i++) {
      const point = { x: destination.x, y: destination.y + 52 - i * 15 };
      const ringNode = session.node('arenaFxHealRing', point, 140, 44);
      session.animate(ringNode, [{ opacity: 0, transform: 'translate(-50%,-50%) scale(.25)' },
        { opacity: .95, transform: 'translate(-50%,-50%) scale(1)', offset: .25 },
        { opacity: 0, transform: 'translate(-50%,calc(-50% - 75px)) scale(.72)' }], { duration: 880, delay: i * 100 });
    }
    particles(session, destination, 14, '#a1ffd1', true, 60);
    const cross = session.node('arenaFxHealCross', { x: destination.x, y: destination.y - 12 }, 48);
    session.animate(cross, [{ opacity: 0, transform: 'translate(-50%,-50%) scale(.4)' },
      { opacity: 1, transform: 'translate(-50%,-50%) scale(1.15)', offset: .25 },
      { opacity: 0, transform: 'translate(-50%,calc(-50% - 42px)) scale(.75)' }], { duration: 870, delay: 160 });
    session.animate(artOf(options.target), [{ transform: 'translateY(0)' }, { transform: 'translateY(-9px)', offset: .4 }, { transform: 'translateY(0)' }], { duration: 1050 });
    if (!await session.wait(380)) return; impact(options, session); await session.wait(850);
  }
  async function buff(session, options, destination) {
    ring(session, 'arenaFxBuffRing', destination, 165, 0, '#e9bdff');
    for (let i = 0; i < 3; i++) {
      const orbit = session.node('arenaFxBuffOrbit', destination, 145, 65);
      orbit.style.setProperty('--arena-orbit-angle', i * 60 + 'deg');
      session.animate(orbit, [{ opacity: 0, transform: 'translate(-50%,-50%) rotate(' + (i * 60 - 70) + 'deg) scale(.3)' },
        { opacity: 1, transform: 'translate(-50%,-50%) rotate(' + (i * 60) + 'deg) scale(1)', offset: .28 },
        { opacity: 0, transform: 'translate(-50%,-50%) rotate(' + (i * 60 + 95) + 'deg) scale(1.1)' }], { duration: 1120, delay: i * 45 });
    }
    const star = session.node('arenaFxBuffStar', { x: destination.x, y: destination.y - 15 }, 75, 75, starArt);
    session.animate(star, [{ opacity: 0, transform: 'translate(-50%,-50%) rotate(-45deg) scale(.2)' },
      { opacity: 1, transform: 'translate(-50%,-50%) rotate(0deg) scale(1)', offset: .3 },
      { opacity: 0, transform: 'translate(-50%,calc(-50% - 35px)) rotate(45deg) scale(.65)' }], { duration: 1200 });
    particles(session, destination, 12, '#ffe0a7', true);
    if (!await session.wait(400)) return; impact(options, session); await session.wait(850);
  }
  async function guard(session, options, destination) {
    const size = clamp(destination.height * 1.25, 140, 205), shield = session.node('arenaFxShield', destination, size * .83, size, shieldArt);
    session.animate(shield, [{ opacity: 0, transform: 'translate(-50%,-50%) rotateY(75deg) scale(.35)' },
      { opacity: 1, transform: 'translate(-50%,-50%) rotateY(0deg) scale(1.08)', offset: .34 },
      { opacity: 1, transform: 'translate(-50%,-50%) scale(1)', offset: .55 },
      { opacity: 0, transform: 'translate(-50%,-50%) scale(.94)' }], { duration: 1140 });
    ring(session, 'arenaFxShieldRing', destination, 200, 180, '#8ce5ff');
    particles(session, destination, 10, '#a1eaff', false, 240);
    if (!await session.wait(390)) return; setGuard(options.target || options.actor, true); impact(options, session); await session.wait(800);
  }
  function impact(options, session) {
    if (session.cancelled || session.impacted) return; session.impacted = true;
    try { Promise.resolve(options.onImpact?.()).catch(() => {}); } catch { /* Renderer callbacks cannot strand cleanup. */ }
  }
  async function reduced(session, options, destination, kind) {
    const isGuard = kind === 'guard' || options.blocked, name = isGuard ? 'arenaFxShield reduced' : kind === 'heal' ? 'arenaFxHealCross reduced' : kind === 'focus' ? 'arenaFxBuffStar reduced' : 'arenaFxImpact reduced';
    const content = isGuard ? shieldArt : kind === 'focus' ? starArt : '';
    const node = session.node(name, destination, isGuard ? 105 : 75, isGuard ? 126 : 75, content);
    session.animate(node, [{ opacity: 0 }, { opacity: .95, offset: .22 }, { opacity: .95, offset: .6 }, { opacity: 0 }], { duration: 360 });
    if (!await session.wait(140)) return;
    if (kind === 'guard') setGuard(options.target || options.actor, true);
    else if (kind === 'attack' && (options.blocked || options.target?.dataset.arenaFxGuarded === 'true')) setGuard(options.target, false);
    impact(options, session); await session.wait(220);
  }
  async function play(options = {}) {
    const battlefield = options.battlefield;
    if (!battlefield?.isConnected || document.hidden) return { cancelled: true };
    cancel(battlefield);
    const kind = String(field(options.event, 'Kind', 'attack')), skill = String(field(options.event, 'SkillId', kind));
    const session = createSession(battlefield, kind, skill), start = performance.now();
    const source = anchor(options.actor || options.target, battlefield), destination = anchor(options.target || options.actor, battlefield);
    const reducedMotion = options.reducedMotion ?? matchMedia('(prefers-reduced-motion: reduce)').matches;
    soundFor(session, options.sound, kind, skill);
    try {
      if (reducedMotion) await reduced(session, options, destination, kind);
      else if (kind === 'attack') await attack(session, options, source, destination, skill);
      else if (kind === 'heal' || kind === 'teamHeal') await heal(session, options, destination);
      else if (kind === 'guard' || kind === 'defense') await guard(session, options, destination);
      else await buff(session, options, destination);
      session.completed = true;
      return { cancelled: session.cancelled, kind, skill, duration: Math.round(performance.now() - start) };
    } finally { session.dispose(); }
  }
  function cancel(battlefield) {
    for (const session of [...sessions]) if (!battlefield || session.battlefield === battlefield || battlefield.contains(session.battlefield)) {
      session.cancelled = true; session.dispose();
    }
    for (const sound of [...sounds]) if (!battlefield || sound.battlefield === battlefield || battlefield.contains(sound.battlefield)) stopSound(sound);
  }
  function clear(root) {
    cancel(root);
    (root || document).querySelectorAll('[data-arena-fx-guarded]').forEach(unit => setGuard(unit, false));
  }
  function stopAudio(battlefield) {
    for (const sound of [...sounds]) if (!battlefield || sound.battlefield === battlefield || battlefield.contains(sound.battlefield)) stopSound(sound);
  }
  document.addEventListener('visibilitychange', () => { if (document.hidden) clear(); });
  window.addEventListener('pagehide', () => clear());
  return { play, cancel, clear, stopAudio, setGuard, syncGuards };
})();
