'use strict';
const runtime = require('./test-runtime.cjs'); runtime.prepare();
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict');
const { chromium } = runtime.playwright();
const output = path.join(runtime.verificationRoot, 'latest');
const fixtureImage = 'data:image/png;base64,' + fs.readFileSync(path.join(__dirname, 'fixtures', 'StudioUR.png')).toString('base64');

async function installFixture(page) {
  await page.route(/^https?:\/\//, route => route.abort());
  await page.addInitScript(({ image }) => {
    let listener, session, battle, battleNumber = 0;
    const skills = [['strike', 'attack', 1, 0], ['guard', 'guard', 1, 0], ['mend', 'heal', 3, 2], ['burst', 'attack', 5, 2], ['focus', 'focus', 8, 1], ['pulse', 'teamHeal', 12, 3], ['nova', 'attack', 18, 4]]
      .map(([Id, Kind, Level, Energy]) => ({ Id, Kind, Level, Energy }));
    const roster = ['Akari', 'Rin', 'Yuki'].map((Name, index) => ({ CardId: 'HT-ARENA-QA-' + index, CharacterId: 500 + index, Name, Series: 'Offline arena fixture', Rarity: 'SSR', RarityColor: '#e8bd75', Orientation: 'portrait' }));
    const profiles = roster.map(row => ({ ...row, Level: 20, Xp: 1, NextLevelXp: 200, Effort: 30, Attack: 40, Defense: 20, Hp: 200, Bond: 1, Busy: false, Skills: skills, EquippedSkills: skills.map(x => x.Id) }));
    const wallet = { Gems: 6000, FreeCredits: 5, PaidCredits: 0, PackPrice: 160, LimitedPackPrice: 180, MaxFreeCredits: 5, ServerNow: new Date().toISOString(), NextFreeAt: new Date(Date.now() + 1200000).toISOString() };
    const creative = { Albums: [], Cosmetics: [], Dyes: [], InventoryItems: [], NextStudioSequence: 1 };
    const gameplay = { Profiles: profiles, Routes: [], Expeditions: [], Inventory: [], Fight: { Stage: 1, MaxStage: 20, DailyRemaining: 3, EnemyName: 'Training sentinel', EnemyHp: 800, EnemyAttack: 10, EnemyDefense: 8 } };
    const clone = value => JSON.parse(JSON.stringify(value));
    const snapshot = () => clone({ creative, gameplay: { ...gameplay, Battle: battle }, roster, wallet, serverNow: new Date().toISOString() });
    const reply = msg => setTimeout(() => listener({ data: clone({ session, ...msg, wallet }) }), 5);
    window.arenaQa = {
      calls: [], audio: [], audioStops: [], rejectAudio: false, effects: [], hpChanges: [], requestDelay: 0, finishNext: false,
      snapshot, getBattle: () => clone(battle),
      settings(settings) { reply({ type: 'state', state: { Settings: { Language: 'en', ThemeMode: 'dark', AccentColor: '#8fb6fb', ...settings } } }); },
      async handle(msg) {
        this.calls.push({ ...msg, at: performance.now() });
        if (msg.type === 'hytcgLifestyleImage') return { imageData: image };
        if (msg.action === 'profile') return { card: roster.find(row => row.CardId === msg.cardId), profile: profiles.find(row => row.CardId === msg.cardId) };
        if (msg.action === 'roster') return { roster, profiles, rosterTotal: roster.length, hasMore: false };
        if (msg.action === 'fight') battle = {
          Id: 'BA-ARENA-QA-' + (++battleNumber), Stage: 1, Turn: 1, Actor: 0, Complete: false, Win: false,
          EnemyName: 'Training sentinel', EnemyHp: 800, EnemyMaxHp: 800,
          Team: msg.cardIds.map((CardId, index) => ({ CardId, Name: roster.find(row => row.CardId === CardId).Name, Hp: 145 + index * 10, MaxHp: 200, Attack: 40, Defense: 20, Energy: 6, Skills: skills.map(x => x.Id), Shield: false, Focus: 0 })), Events: []
        };
        if (msg.action === 'battleTurn') {
          await new Promise(resolve => setTimeout(resolve, this.requestDelay));
          const actor = battle.Team[battle.Actor], kind = skills.find(x => x.Id === msg.skillId).Kind;
          const event = (target, type, amount, hp) => ({ ActorId: actor.CardId, TargetId: target, Kind: type, SkillId: msg.skillId, Amount: amount, HpAfter: hp });
          battle.Events = [];
          if (kind === 'attack') { battle.EnemyHp = this.finishNext ? 0 : battle.EnemyHp - 20; battle.Events.push(event('enemy', 'attack', 20, battle.EnemyHp)); }
          if (kind === 'heal') { const target = battle.Team.find(row => row.CardId === msg.targetId); target.Hp = Math.min(target.MaxHp, target.Hp + 25); battle.Events.push(event(target.CardId, 'heal', 25, target.Hp)); }
          if (kind === 'teamHeal') for (const target of battle.Team) { target.Hp = Math.min(target.MaxHp, target.Hp + 15); battle.Events.push(event(target.CardId, 'heal', 15, target.Hp)); }
          if (kind === 'guard') { actor.Shield = true; battle.Events.push(event(actor.CardId, 'guard', 0, actor.Hp)); }
          if (kind === 'focus') { actor.Focus = 1; battle.Events.push(event(actor.CardId, 'focus', 0, actor.Hp)); }
          if (this.finishNext) {
            this.finishNext = false; battle.Complete = true; battle.Win = true;
            gameplay.Fight.LastResult = { Kind: 'fight', Title: 'Victory', Win: true, Stage: 1, Rounds: battle.Turn, GemsAwarded: 9, XpAwarded: 22, CardIds: battle.Team.map(row => row.CardId), CombatLog: ['Victory'] };
          } else {
            actor.Hp -= 5; actor.Shield = false;
            battle.Events.push({ ActorId: 'enemy', TargetId: actor.CardId, Kind: 'attack', SkillId: 'strike', Amount: 5, HpAfter: actor.Hp });
          }
          battle.Actor = (battle.Actor + 1) % battle.Team.length; battle.Turn++;
        }
        if (msg.action === 'battleForfeit') battle.Complete = true;
        return snapshot();
      }
    };
    HTMLMediaElement.prototype.play = function () {
      arenaQa.audio.push({ source: this.currentSrc || this.src, volume: this.volume, at: performance.now() });
      return arenaQa.rejectAudio ? Promise.reject(new Error('Offline fixture denied audio')) : Promise.resolve();
    };
    HTMLMediaElement.prototype.pause = function () { arenaQa.audioStops.push({ source: this.currentSrc || this.src, at: performance.now() }); };
    window.chrome = { webview: {
      addEventListener(type, callback) { if (type === 'message') listener = callback; },
      postMessage(msg) {
        session = msg.session || session;
        if (msg.type === 'hytcgInit') {
          reply({ type: 'state', state: { Settings: { Language: 'en', ThemeMode: 'dark', AccentColor: '#8fb6fb', DisableEffects: false } } });
          reply({ type: 'hytcgConfig', config: { Concurrency: 2, DropRates: [{ Name: 'SSR', Color: '#e8bd75', Weight: 100 }] } });
        }
        if (msg.type === 'hytcgCollection') reply({ ...msg, page: { Items: [], Total: 3, HasMore: false } });
        if (msg.type === 'hytcgWallet' || msg.type === 'hytcgDiscoveryState') reply({ ...msg, banners: [], missions: [] });
        if (msg.type === 'hytcgLifestyle' || msg.type === 'hytcgLifestyleImage') arenaQa.handle(msg).then(value => reply({ ...msg, ...value })).catch(error => reply({ ...msg, error: error.message }));
      }
    } };
  }, { image: fixtureImage });
}

(async () => {
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let checks = 0;
  const results = [], errors = [], rejected = [], csp = [];
  const check = (condition, label) => { assert(condition, label); checks++; results.push(label); };
  try {
    // Decode the supplied MP3s under the real panel CSP, before playback is
    // stubbed for the deterministic battle fixture. This never plays sound.
    const audioPage = await browser.newPage();
    await audioPage.route(/^https?:\/\//, route => route.abort());
    await audioPage.addInitScript(() => {
      window.chrome = { webview: { addEventListener() {}, postMessage() {} } };
    });
    await audioPage.goto('file:///' + path.join(runtime.projectRoot, 'ui', 'hytcg.html').replaceAll('\\', '/'));
    const decodedAudio = await audioPage.evaluate(async () => Promise.all(
      ['attack effect.mp3', 'burst effect.mp3', 'healing effect.mp3'].map(file => new Promise(resolve => {
        const audio = new Audio(new URL('../assets/hytcg/' + encodeURIComponent(file), document.baseURI));
        audio.preload = 'auto'; audio.muted = true;
        const finish = error => { clearTimeout(timer); const duration = audio.duration; audio.removeAttribute('src'); audio.load(); resolve({ file, duration, error }); };
        const timer = setTimeout(() => finish('decode timeout'), 10000);
        audio.addEventListener('canplay', () => finish(null), { once: true });
        audio.addEventListener('error', () => finish('media error ' + audio.error?.code), { once: true });
        audio.load();
      }))
    ));
    for (const row of decodedAudio) check(!row.error && row.duration > 0, row.file + ' decodes under the panel CSP: ' + (row.error || row.duration.toFixed(2) + ' seconds'));
    await audioPage.close();
    const page = await browser.newPage({ viewport: { width: 1100, height: 900 } });
    page.on('pageerror', error => errors.push(error.message));
    page.on('console', message => { if (message.type() === 'error' && /Content Security Policy/.test(message.text())) csp.push(message.text()); });
    await installFixture(page);
    await page.goto('file:///' + path.join(runtime.projectRoot, 'ui', 'hytcg.html').replaceAll('\\', '/'));
    await page.waitForFunction(() => window.HyTcgLifestyle && window.HyTcgArenaEffects && tcg.config);
    await page.evaluate(() => {
      window.addEventListener('unhandledrejection', event => arenaQa.unhandled = [...(arenaQa.unhandled || []), String(event.reason)]);
      const rememberEffect = node => {
        const effect = node.matches?.('.arenaFx') ? node : node.querySelector?.('.arenaFx');
        if (!effect || arenaQa.effects.some(row => row.node === effect)) return;
        arenaQa.effects.push({ node: effect, kind: effect.dataset.kind, skill: effect.dataset.skill, started: performance.now(), ended: null, field: effect.closest('.arenaBattlefield'), hp: [...document.querySelectorAll('[data-arena-hp]')].map(row => [row.dataset.arenaHp, row.textContent]) });
      };
      const observer = new MutationObserver(records => {
        for (const record of records) {
          for (const node of record.addedNodes) if (node.nodeType === 1) rememberEffect(node);
          for (const effect of arenaQa.effects) if (effect.ended === null && !effect.node.isConnected) effect.ended = performance.now();
          const hp = record.target.nodeType === 1 ? record.target.closest?.('[data-arena-hp]') : record.target.parentElement?.closest('[data-arena-hp]');
          if (hp) arenaQa.hpChanges.push({ id: hp.dataset.arenaHp, text: hp.textContent, at: performance.now() });
        }
      });
      observer.observe(document.getElementById('lifestyleContent'), { subtree: true, childList: true, characterData: true });
      arenaQa.observer = observer;
    });
    await page.click('[data-life-view="arena"]');
    for (let index = 0; index < 3; index++) {
      await page.locator('[data-life-team-add="arena"]').first().click();
      await page.click('[data-life-pick="HT-ARENA-QA-' + index + '"]');
    }
    await page.click('[data-life-action="fight"]');
    await page.waitForSelector('.arenaBattlefield');
    check(await page.locator('.arenaFighter').count() === 3, 'three local fixture cards mount without any account access');
    const idle = () => page.waitForFunction(() => { const command = document.querySelector('[data-adventure-action="battleSkill"]'); return command && !command.disabled && !document.querySelector('.arenaFx'); }, null, { timeout: 20000 });
    const use = async skill => {
      await idle();
      await page.click('[data-adventure-action="battleSkill"][data-skill="' + skill + '"]');
      if (skill === 'mend') await page.locator('[data-adventure-action="battleTarget"]').first().click();
    };

    const beforeAttack = await page.locator('[data-arena-hp="enemy"]').textContent();
    const callsBefore = await page.evaluate(() => arenaQa.calls.filter(row => row.action === 'battleTurn').length);
    await use('strike');
    await page.waitForSelector('.arenaFx[data-kind="attack"]');
    await page.evaluate(() => arenaQa.currentField = document.querySelector('.arenaBattlefield'));
    check(await page.locator('[data-arena-hp="enemy"]').textContent() === beforeAttack, 'enemy HP stays at previous value until the attack impact');
    check(await page.locator('.arenaSkill:enabled').count() === 0, 'all commands locked during visible attack');
    check(await page.locator('[data-adventure-action="battleForfeit"]').isDisabled(), 'forfeit locked during visible attack');
    check(await page.locator('.arenaFxProjectile,.arenaFxSlash').count() > 0, 'attack renders a projectile or slash rather than only a text label');
    check(await page.locator('.arenaFx').evaluate(node => { const bounds = node.getBoundingClientRect(); return bounds.width > 10 && bounds.height > 10 && getComputedStyle(node).visibility !== 'hidden'; }), 'attack effect occupies visible battlefield coordinates');
    await page.evaluate(() => {
      document.querySelector('[data-adventure-action="battleSkill"][data-skill="strike"]').dispatchEvent(new MouseEvent('click', { bubbles: true }));
      HyTcgLifestyle.applySnapshot(arenaQa.snapshot());
    });
    check(await page.evaluate(() => arenaQa.currentField === document.querySelector('.arenaBattlefield') && arenaQa.currentField.isConnected), 'background native snapshot retains the animated battlefield');
    await page.screenshot({ path: path.join(output, 'arena-attack-charge.png') });
    await page.waitForFunction(() => document.querySelector('[data-arena-hp="enemy"]')?.textContent === '780 / 800');
    check(await page.locator('.arenaFx').count() > 0, 'attack HP changes while the impact effect remains visible');
    await page.waitForTimeout(70);
    await page.screenshot({ path: path.join(output, 'arena-attack.png') });
    await idle();
    check(await page.evaluate(() => arenaQa.calls.filter(row => row.action === 'battleTurn').length) === callsBefore + 1, 'extra click during attack does not issue a duplicate turn');
    const attackEffects = await page.evaluate(() => arenaQa.effects.filter(row => row.kind === 'attack').map(row => ({ duration: row.ended - row.started, kind: row.kind, skill: row.skill })));
    check(await page.locator('#lifestyleContent').getAttribute('aria-busy') === 'false', 'completed action clears the busy state');
    check(!await page.locator('#lifestyleRefresh').isDisabled(), 'completed action unlocks refresh');
    check(attackEffects.length >= 2, 'player strike and enemy retaliation each mount an attack animation');
    check(attackEffects.every(row => row.duration >= 850), 'attack and retaliation remain visible for at least 850 milliseconds');
    check(await page.locator('[data-arena-hp="HT-ARENA-QA-0"]').textContent() === '140 / 200', 'enemy impact updates the correct teammate HP');

    for (const [skill, kind, selector, screenshot] of [
      ['mend', 'heal', '.arenaFxHealRing,.arenaFxHealCross', 'arena-heal.png'],
      ['guard', 'guard', '.arenaFxShield,.arenaFxShieldRing', 'arena-defense.png'],
      ['focus', 'focus', '.arenaFxBuffOrbit,.arenaFxBuffStar', 'arena-buff.png'],
      ['burst', 'attack', '.arenaFxProjectile.burst', 'arena-burst.png'],
      ['nova', 'attack', '.arenaFxProjectile.nova', 'arena-nova.png']
    ]) {
      const start = await page.evaluate(() => arenaQa.effects.length);
      await use(skill);
      await page.waitForSelector('.arenaFx[data-kind="' + kind + '"]');
      check(await page.locator(selector).count() > 0, skill + ' has its own mounted visual primitives');
      check(await page.locator('.arenaSkill:enabled').count() === 0, skill + ' keeps commands locked');
      await page.waitForTimeout(180);
      await page.screenshot({ path: path.join(output, screenshot) });
      if (skill === 'guard') {
        await page.waitForSelector('.arenaFxShield.blocking');
        check(true, 'the enemy impact visibly strikes the defense shield');
      }
      await idle();
      const effect = await page.evaluate(({ start, kind }) => { const row = arenaQa.effects.slice(start).find(row => row.kind === kind); return row && { duration: row.ended - row.started, skill: row.skill }; }, { start, kind });
      check(effect && effect.duration >= 850, skill + ' remains animated for a perceptible duration');
      check(effect.skill === skill, skill + ' uses the selected skill identity');
    }

    const pulseStart = await page.evaluate(() => arenaQa.effects.length);
    await use('pulse'); await idle();
    check(await page.evaluate(start => arenaQa.effects.slice(start).filter(row => row.kind === 'heal').length, pulseStart) === 3, 'team healing animates each of the three affected cards');
    const audio = await page.evaluate(() => arenaQa.audio);
    check(audio.some(row => /attack%20effect\.mp3|attack effect\.mp3/i.test(row.source)), 'strike uses the provided attack sound file');
    check(audio.some(row => /healing%20effect\.mp3|healing effect\.mp3/i.test(row.source)), 'heal and buff use the provided healing sound file');
    check(audio.some(row => /burst%20effect\.mp3|burst effect\.mp3/i.test(row.source)), 'burst, guard or nova uses the provided burst sound file');
    check(audio.every(row => /^file:/.test(row.source)), 'arena sounds are local files');

    await page.evaluate(() => arenaQa.rejectAudio = true);
    const rejectBefore = await page.evaluate(() => arenaQa.calls.filter(row => row.action === 'battleTurn').length);
    await use('strike'); await idle();
    check(await page.evaluate(() => arenaQa.calls.filter(row => row.action === 'battleTurn').length) === rejectBefore + 1, 'rejected audio still completes the selected turn');
    rejected.push(...await page.evaluate(() => arenaQa.unhandled || []));
    check(rejected.length === 0, 'rejected audio does not leak an unhandled promise rejection');
    await page.evaluate(() => { arenaQa.rejectAudio = false; arenaQa.settings({ DisableEffects: true }); });
    await page.waitForTimeout(30);
    const mutedStart = await page.evaluate(() => arenaQa.audio.length);
    await use('strike'); await idle();
    check(await page.evaluate(() => arenaQa.audio.length) === mutedStart, 'launcher DisableEffects mutes arena sound');
    check(await page.evaluate(() => arenaQa.effects.at(-2)?.kind) === 'attack', 'muting sound retains the attack animation');
    await page.evaluate(() => arenaQa.settings({ DisableEffects: false })); await page.waitForTimeout(30);

    await use('strike'); await page.waitForSelector('.arenaFx');
    const activeMuteStart = await page.evaluate(() => arenaQa.audioStops.length);
    await page.evaluate(() => arenaQa.settings({ DisableEffects: true }));
    await page.waitForFunction(start => arenaQa.audioStops.length > start, activeMuteStart);
    check(true, 'disabling effects pauses a sound already playing');
    check(await page.locator('.arenaFx').count() > 0, 'disabling sound during a turn keeps the mounted animation');
    await idle();
    await page.evaluate(() => arenaQa.settings({ DisableEffects: false })); await page.waitForTimeout(30);

    await use('strike'); await page.waitForSelector('.arenaFx');
    const cancelStart = await page.evaluate(() => arenaQa.audioStops.length);
    await page.click('#boosterTab');
    await page.waitForFunction(() => !document.querySelector('.arenaFx'));
    check(true, 'leaving arena cancels all mounted battle effects');
    check(await page.evaluate(() => arenaQa.audioStops.length) > cancelStart, 'leaving arena pauses its battle sound');
    await page.click('[data-life-view="arena"]'); await idle();
    check(await page.locator('.arenaSkill:enabled').count() > 0, 'returning to arena restores unlocked commands');

    await use('strike'); await page.waitForSelector('.arenaFx');
    await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')); });
    await page.waitForFunction(() => !document.querySelector('.arenaFx'));
    check(true, 'document hiding removes active battle effects');
    await page.evaluate(() => { delete document.hidden; document.dispatchEvent(new Event('visibilitychange')); });
    await page.click('[data-life-view="arena"]'); await idle();

    await page.emulateMedia({ reducedMotion: 'reduce' });
    const reducedStart = await page.evaluate(() => arenaQa.effects.length);
    await use('guard'); await page.waitForSelector('.arenaFx[data-kind="guard"]');
    check(await page.locator('.arenaFxShield').count() > 0, 'reduced motion retains the defense visual cue');
    await idle();
    const reduced = await page.evaluate(start => arenaQa.effects.slice(start).map(row => row.ended - row.started), reducedStart);
    check(reduced.every(duration => duration > 100 && duration < 700), 'reduced motion uses short visible effects');
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await page.setViewportSize({ width: 375, height: 850 });
    await page.evaluate(() => HyTcgLifestyle.applySnapshot(arenaQa.snapshot()));
    check(await page.locator('#lifestyleContent').evaluate(node => node.scrollWidth <= node.clientWidth + 2), 'arena remains within a 375 pixel panel');
    await use('strike'); await page.waitForSelector('.arenaFx'); await page.waitForTimeout(180);
    await page.screenshot({ path: path.join(output, 'arena-mobile.png') }); await idle();

    await page.evaluate(() => arenaQa.finishNext = true);
    await use('strike'); await page.waitForSelector('.arenaFx');
    check(await page.locator('.arenaOutcome').count() === 0, 'victory result waits for the final strike animation');
    await page.waitForSelector('.arenaOutcome');
    check(await page.locator('.arenaFx').count() === 0, 'victory appears only after the strike effect ends');
    await page.click('[data-adventure-action="arenaNew"]');
    await page.click('[data-life-action="fight"]'); await idle();

    await page.evaluate(() => arenaQa.requestDelay = 250);
    await use('strike');
    await page.waitForFunction(() => document.querySelector('.arenaSkill')?.disabled);
    await page.evaluate(() => HyTcgLifestyle.reset());
    await page.waitForTimeout(1400);
    check(await page.locator('.arenaFx').count() === 0 && await page.locator('#lifestyleContent > *').count() === 0, 'account reset ignores delayed turn response and leaves no animation');
    check(errors.length === 0, 'no page errors: ' + errors.join('; '));
    check(csp.length === 0, 'no CSP failures: ' + csp.join('; '));
    const metrics = await page.evaluate(() => arenaQa.effects.map(row => ({ kind: row.kind, skill: row.skill, durationMs: Math.round((row.ended ?? performance.now()) - row.started) })));
    fs.writeFileSync(path.join(output, 'arena-animation.json'), JSON.stringify({ Result: 'PASS', Checks: checks, Assertions: results, Effects: metrics, AudioCalls: audio.length, Errors: errors, CspErrors: csp, ProductionDataAccessed: false, ExternalApiRequests: false }, null, 2));
    console.log('PASS arena animation ' + checks + ' checks');
  } finally { await browser.close(); }
})().catch(error => { console.error(error.stack); process.exitCode = 1; });
