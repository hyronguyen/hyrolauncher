const testRuntime = require('./test-runtime.cjs'); testRuntime.prepare();
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { chromium } = require('./test-runtime.cjs').playwright();

// This fixture never opens a booster or touches the production wallet/cards folder.
const out = path.resolve('output/verification/hytcg-hover');
fs.mkdirSync(out, { recursive: true });
const rates = [
  ['Common', 65, '#aebccc'], ['Uncommon', 22, '#6fd6b0'], ['Rare', 7.3, '#7ab9f9'],
  ['SR', 4, '#bf96ef'], ['SSR', 1.1, '#f5c879'], ['UR', .6, '#f6c6ff']
].map(([Name, Weight, Color]) => ({ Name, Weight, Color }));
const image = 'data:image/png;base64,' + fs.readFileSync('assets/default-logo.png').toString('base64');
const cards = rates.map((rate, index) => ({
  CharacterId: 950000 + index, Name: 'Hover fixture ' + rate.Name, Series: 'Local visual verification',
  Gender: 'Unknown', Rarity: rate.Name, RarityColor: rate.Color, ImageSource: index % 2 ? 'zerochan' : 'acdb',
  ArtworkPage: index % 2 ? 'https://www.zerochan.net/3085577' : '',
  CardId: 'HT-20261008-HOVER' + String(index).padStart(7, '0'),
  Metadata: { desc: 'Local test image. No real card was opened, saved, or dissolved.' }
}));
const high = rarity => ['SR', 'SSR', 'UR'].includes(rarity);

async function state(locator) {
  return locator.evaluate(element => {
    const surface = element.querySelector('.cardSurface');
    const rect = element.getBoundingClientRect();
    return {
      active: element.classList.contains('isInteracting'),
      transform: surface ? getComputedStyle(surface).transform : 'missing-surface',
      shine: Number(getComputedStyle(element.querySelector('.cardShine')).opacity),
      glare: Number(getComputedStyle(element.querySelector('.cardGlare')).opacity),
      rect: { x: rect.x, y: rect.y, width: rect.width, height: rect.height },
      rotateX: element.style.getPropertyValue('--rotate-x'),
      rotateY: element.style.getPropertyValue('--rotate-y')
    };
  });
}
function sameRect(actual, expected, context) {
  for (const key of ['x', 'y', 'width', 'height']) {
    assert(Math.abs(actual[key] - expected[key]) < .3, `${context}: stable hit area ${key} changed (${actual[key]} vs ${expected[key]})`);
  }
}
async function leave(page, locator, rect) {
  await page.mouse.move(rect.x - 8, rect.y + rect.height * .45);
  await page.waitForTimeout(440);
  const result = await state(locator);
  assert.equal(result.active, false, 'pointer leave resets interaction');
  assert.equal(result.rotateX, '', 'pointer leave clears x rotation');
  assert.equal(result.rotateY, '', 'pointer leave clears y rotation');
  assert.equal(result.shine, 0, 'pointer leave hides foil');
}
async function hoverAcross(page, locator, rarity, context) {
  await locator.scrollIntoViewIfNeeded();
  const before = await state(locator);
  assert.notEqual(before.transform, 'missing-surface', `${context}: card needs an inner .cardSurface`);
  const transforms = [];
  for (const [x, y] of [[.13, .17], [.87, .20], [.82, .83], [.18, .84]]) {
    await page.mouse.move(before.rect.x + before.rect.width * x, before.rect.y + before.rect.height * y, { steps: 5 });
    await page.waitForTimeout(180);
    const result = await state(locator);
    assert.equal(result.active, true, `${context}: interaction active`);
    assert.match(result.transform, /^matrix3d\(/, `${context}: inner surface must rotate in 3D`);
    assert.equal(result.shine > 0, high(rarity), `${context}: foil is restricted to SR/SSR/UR`);
    assert(result.shine<=.22 && result.glare<=.17, `${context}: transparent foil/glare must not wash out artwork`);
    sameRect(result.rect, before.rect, context);
    transforms.push(result.transform);
  }
  assert(new Set(transforms).size >= 3, `${context}: moving the mouse changes rotation`);
  await leave(page, locator, before.rect);
  return { rarity, context, transforms, stableHitArea: before.rect };
}

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const checks = [], errors = [], requests = [];
  try {
    const context = await browser.newContext({ viewport: { width: 1000, height: 820 } });
    const page = await context.newPage();
    page.on('pageerror', error => errors.push(error.message));
    await page.route(/^https?:\/\//, route => { requests.push(route.request().url()); return route.abort(); });
    await page.addInitScript(({ rates }) => {
      let listener, session;
      const wallet = {
        FreeCredits: 0, PaidCredits: 0, Gems: 0, MaxFreeCredits: 5, IntervalSeconds: 1200, PackPrice: 160,
        SalvageValues: { Common: 1, Uncommon: 3, Rare: 8, SR: 20, SSR: 60, UR: 160 }
      };
      const snapshot = () => ({ ...wallet, ServerNow: new Date().toISOString(), NextFreeAt: new Date(Date.now() + 1200000).toISOString() });
      const reply = data => setTimeout(() => listener({ data: { session, ...data, wallet: snapshot() } }), 5);
      window.hoverQa = { messages: [], items: [] };
      window.chrome = window.chrome || {};
      window.chrome.webview = {
        addEventListener: (_, callback) => listener = callback,
        postMessage: msg => {
          hoverQa.messages.push(msg); session = msg.session;
          if (msg.type === 'ready') reply({ type: 'state', state: { Settings: { Language: 'en', ThemeMode: 'dark', AccentColor: '#b995ef' } } });
          if (msg.type === 'hytcgInit') reply({ type: 'hytcgConfig', config: { DropRates: rates, Concurrency: 2, MaxCharacterAttempts: 15 }, cardsDirectory: 'cards', recoveries: [] });
          if (msg.type === 'hytcgWallet') reply({ type: 'hytcgWallet', requestId: msg.requestId });
          if (msg.type === 'hytcgCollection') reply({ type: 'hytcgCollection', requestId: msg.requestId, page: { Items: hoverQa.items, Total: hoverQa.items.length, HasMore: false } });
        }
      };
    }, { rates });
    const url = 'file:///' + path.resolve('ui/hytcg.html').replaceAll('\\', '/') + '?id=builtin.hytcg';
    await page.goto(url);
    await page.waitForFunction(() => tcg.config && tcg.wallet);
    await page.waitForTimeout(70);
    const pngs = await page.evaluate(async ({ cards, image }) => {
      const pngs = [];
      for (const card of cards) pngs.push(await HyTcgRenderer.render(card, image));
      tcg.slots = cards.map((card, index) => ({ card, png: pngs[index], status: 'ready', saved: false }));
      renderSlots();
      return pngs;
    }, { cards, image });
    const items = cards.map((card, index) => {
      const file = path.join(out, 'fixture-' + card.Rarity + '.png');
      fs.writeFileSync(file, Buffer.from(pngs[index].split(',')[1], 'base64'));
      return { Card: card, ImagePath: file, CanDissolve: false, SalvageValue: 0, IsLegacy: false };
    });
    await page.evaluate(items => hoverQa.items = items, items);
    assert.equal(await page.locator('#cardEffectsButton').getAttribute('aria-pressed'), 'true', 'interactive effects default on');

    // All six rarities must tilt; only high rarity cards may show foil.
    for (let index = 0; index < cards.length; index++) {
      await page.evaluate(index => detail(index, false), index);
      assert.equal(await page.locator('#cardEffectsButton').isVisible(), true, 'effects control is discoverable in card detail');
      const locator = page.locator('#cardDetail [data-tilt]');
      checks.push(await hoverAcross(page, locator, cards[index].Rarity, 'detail-' + cards[index].Rarity));
      const rect = (await state(locator)).rect;
      // A move arriving without pointerover must re-establish the current card.
      await locator.evaluate((element, { x, y }) => {
        element.querySelector('img').dispatchEvent(new PointerEvent('pointermove', { bubbles: true, pointerType: 'mouse', clientX: x, clientY: y }));
      }, { x: rect.x + rect.width * .8, y: rect.y + rect.height * .25 });
      await page.waitForTimeout(180);
      assert.equal((await state(locator)).active, true, 'late pointermove activates the card');
      // Switching image / overlay children must not drop or stick the active card.
      await locator.evaluate(element => {
        const image = element.querySelector('img'), glare = element.querySelector('.cardGlare');
        image.dispatchEvent(new PointerEvent('pointerout', { bubbles: true, pointerType: 'mouse', relatedTarget: glare }));
        glare.dispatchEvent(new PointerEvent('pointerover', { bubbles: true, pointerType: 'mouse', relatedTarget: image }));
      });
      assert.equal((await state(locator)).active, true, 'movement among card children preserves interaction');
      await page.screenshot({ path: path.join(out, 'detail-' + cards[index].Rarity + '.png') });
      await leave(page, locator, rect);
      // Outside chrome at the top edge releases tilt even when the transformed
      // surface protrudes beyond the stable hit area.
      await page.mouse.move(rect.x + rect.width * .5, rect.y + rect.height * .15);
      await page.waitForTimeout(160);
      await page.mouse.move(rect.x + rect.width * .5, rect.y - 6);
      await page.waitForTimeout(320);
      assert.equal((await state(locator)).active, false, 'outside edge chrome releases tilt');
      await page.click('[data-close="cardDialog"]');
    }

    // Collection uses precisely the same hit area / surface interaction.
    await page.evaluate(() => showView('collection'));
    await page.waitForFunction(() => tcg.collection.length === 6);
    assert.equal(await page.locator('.walletBar').isVisible(),false,'collection reserves space for cards');
    assert(await page.locator('#tcgScroll').evaluate(el=>el.clientHeight>400),'collection uses the remaining panel height');
    const patterns=await page.locator('#collectionGrid [data-rarity]').evaluateAll(elements=>elements.filter(el=>['SR','SSR','UR'].includes(el.dataset.rarity)).map(el=>getComputedStyle(el.querySelector('.cardShine')).backgroundImage));
    assert.equal(new Set(patterns).size,3,'SR/SSR/UR use distinct holo patterns');
    await page.selectOption('#collectionSort','name');await page.waitForFunction(()=>hoverQa.messages.some(msg=>msg.type==='hytcgCollection'&&msg.sort==='name'));
    await page.click('#resetFilters');await page.waitForFunction(()=>tcg.collection.length===6&&document.getElementById('collectionSort').value==='newest');
    await page.click('#expandPanel');assert(await page.evaluate(()=>hoverQa.messages.some(msg=>msg.type==='hytcgPanelSize')),'expand panel uses native bridge');
    for(const [width,height,theme] of [[375,650,'dark'],[520,740,'dark'],[1120,820,'dark'],[1120,820,'light']]) {
      await page.setViewportSize({width,height});await page.evaluate(theme=>bindTheme({ThemeMode:theme,Language:'vi',AccentColor:'#b995ef'}),theme);
      assert.equal(await page.locator('#tcgScroll').evaluate(el=>el.scrollWidth>el.clientWidth),false,'collection controls do not overflow');
      await page.screenshot({path:path.join(out,`collection-${width}-${theme}.png`)});
    }
    await page.setViewportSize({width:1000,height:820});await page.evaluate(()=>bindTheme({ThemeMode:'dark',Language:'en',AccentColor:'#b995ef'}));
    for (let index = 0; index < cards.length; index++) {
      checks.push(await hoverAcross(page, page.locator('#collectionGrid [data-tilt]').nth(index), cards[index].Rarity, 'collection-' + cards[index].Rarity));
    }
    await page.evaluate(() => detail(4, true));
    await page.emulateMedia({ reducedMotion: 'reduce' });
    checks.push(await hoverAcross(page, page.locator('#cardDetail [data-tilt]'), 'SSR', 'reduced-motion-direct-hover'));
    await page.screenshot({ path: path.join(out, 'reduced-motion-SSR.png') });
    await page.click('[data-close="cardDialog"]');

    // A compact plugin window still exposes the switch and supports hover.
    await page.setViewportSize({ width: 480, height: 730 });
    await page.evaluate(() => detail(5, true));
    assert.equal(await page.locator('#cardEffectsButton').isVisible(), true);
    checks.push(await hoverAcross(page, page.locator('#cardDetail [data-tilt]'), 'UR', 'compact-detail-UR'));
    await page.screenshot({ path: path.join(out, 'compact-detail-UR.png') });
    await page.click('#cardEffectsButton');
    assert.equal(await page.locator('#cardEffectsButton').getAttribute('aria-pressed'), 'false');
    const persisted = await page.evaluate(() => localStorage.getItem('hytcg-card-effects'));
    assert(persisted !== null, 'toggle saves a local preference');
    const disabledCard = page.locator('#cardDetail [data-tilt]'), disabledRect = (await state(disabledCard)).rect;
    await page.mouse.move(disabledRect.x + disabledRect.width * .8, disabledRect.y + disabledRect.height * .2);
    await page.waitForTimeout(220);
    const disabled = await state(disabledCard);
    assert.equal(disabled.active, false, 'effects off prevents interaction');
    assert.equal(disabled.shine, 0, 'effects off hides foil');
    assert(['none', 'matrix(1, 0, 0, 1, 0, 0)'].includes(disabled.transform), 'effects off prevents 3D rotation');
    await page.click('[data-close="cardDialog"]');
    await page.reload();
    await page.waitForFunction(() => tcg.config && tcg.wallet);
    assert.equal(await page.locator('#cardEffectsButton').getAttribute('aria-pressed'), 'false', 'effects off survives reload');
    assert.equal(await page.evaluate(() => localStorage.getItem('hytcg-card-effects')), persisted);
    assert.equal(requests.length, 0, 'renderer sent no remote network requests');
    assert.deepEqual(errors, [], 'no browser errors');
    const mutations = await page.evaluate(() => hoverQa.messages.filter(msg => ['hytcgReserve', 'hytcgOpen', 'hytcgSave', 'hytcgBuyPull', 'hytcgDissolve'].includes(msg.type)));
    assert.deepEqual(mutations, [], 'fixture never spent credits or changed real cards');
    // Verify the launcher shelf uses the actual inline SVG and inherits accent.
    const launcher=fs.readFileSync('ui/index.html','utf8'),svg=fs.readFileSync('assets/icons/hytcg.svg','utf8');
    const functions=['iconHtml','pluginIconHtml'].map(name=>launcher.match(new RegExp('    function '+name+'\\([^]*?\\r?\\n    \\}'))[0]).join('\n');
    const iconPage=await browser.newPage();await iconPage.setContent('<style>'+launcher.match(/<style>([\s\S]*?)<\/style>/)[1]+'</style>');
    const iconResult=await iconPage.evaluate(async({functions,svg})=>{
      const assets={svgHyTcg:svg};eval(functions);const button=document.createElement('button');button.className='pluginBtn';button.innerHTML=pluginIconHtml({Type:'hytcg'});document.body.append(button);
      const results=[];for(const accent of ['#43cce9','#c49bf7']){document.documentElement.style.setProperty('--accent',accent);await new Promise(resolve=>setTimeout(resolve,280));results.push({count:button.querySelectorAll('svg').length,stroke:getComputedStyle(button.querySelector('svg path')).stroke,width:button.querySelector('svg').getBoundingClientRect().width});}return results;
    },{functions,svg});
    assert.deepEqual(iconResult.map(result=>result.count),[1,1]);assert.deepEqual(iconResult.map(result=>result.stroke),['rgb(67, 204, 233)','rgb(196, 155, 247)']);assert(iconResult.every(result=>result.width>=18),'launcher SVG is visible');await iconPage.screenshot({path:path.join(out,'launcher-svg.png')});await iconPage.close();
    fs.writeFileSync(path.join(out, 'report.json'), JSON.stringify({ passed: true, checks, networkRequests: requests.length, productionMutations: mutations.length, errors }, null, 2));
    console.log('PASS: all 6 rarity cards tilt across the full card in detail and collection; stable hit area, SR+ foil, clean pointer reset, late mousemove, reduced-motion direct hover, compact layout, persisted effects switch; no network or production mutations.');
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
