(function (global) {
  'use strict';
  var names = ['Common', 'Uncommon', 'Rare', 'SR', 'SSR', 'UR'];
  var colors = ['#9baec8', '#6ed4a5', '#6cabff', '#c093f4', '#efc975', '#ec91bf'];
  var cache = Object.create(null);
  // A file:// SVG image taints exported cards. These are the same local vectors,
  // loaded as data images so Canvas export stays safe without fetch or network.
  var vectors = {
    "Common":"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"180\" height=\"64\" viewBox=\"0 0 180 64\" role=\"img\" aria-label=\"Common rarity\"><defs><linearGradient id=\"metal\" x1=\"0\" y1=\"0\" x2=\".7\" y2=\"1\"><stop stop-color=\"#e9f0fb\"/><stop offset=\".34\" stop-color=\"#9baec8\"/><stop offset=\".57\" stop-color=\"#50627d\"/><stop offset=\".81\" stop-color=\"#9baec8\"/><stop offset=\"1\" stop-color=\"#e9f0fb\"/></linearGradient><linearGradient id=\"plaque\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop stop-color=\"#283346\"/><stop offset=\".48\" stop-color=\"#151d2b\"/><stop offset=\"1\" stop-color=\"#0e1420\"/></linearGradient><linearGradient id=\"sheen\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop stop-color=\"#ffffff\" stop-opacity=\".16\"/><stop offset=\".42\" stop-color=\"#ffffff\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"#ffffff\" stop-opacity=\".03\"/></linearGradient></defs><rect x=\"1\" y=\"1\" width=\"178\" height=\"62\" rx=\"12\" fill=\"#080d16\" stroke=\"url(#metal)\" stroke-width=\"2\"/><rect x=\"5\" y=\"5\" width=\"170\" height=\"54\" rx=\"8.5\" fill=\"url(#plaque)\" stroke=\"#9baec8\" stroke-opacity=\".32\"/><path d=\"M13 7h154\" stroke=\"#e9f0fb\" stroke-opacity=\".42\" stroke-linecap=\"round\"/><rect x=\"6\" y=\"6\" width=\"168\" height=\"52\" rx=\"8\" fill=\"url(#sheen)\"/><circle cx=\"31\" cy=\"32\" r=\"22\" fill=\"#50627d\" fill-opacity=\".13\" stroke=\"#9baec8\" stroke-opacity=\".18\"/><circle cx=\"31\" cy=\"32\" r=\"13.5\" fill=\"url(#metal)\" stroke=\"#edf3fb\" stroke-width=\"1\"/><circle cx=\"31\" cy=\"32\" r=\"8.5\" fill=\"#26354a\" stroke=\"#c4d2e5\" stroke-width=\"1\"/><circle cx=\"31\" cy=\"32\" r=\"4\" fill=\"#cad7e9\"/><path d=\"M24 23l3-2m8 22l3-2\" stroke=\"#f6f9ff\" stroke-width=\"1.4\" stroke-linecap=\"round\"/><path d=\"M60 16v32\" stroke=\"#9baec8\" stroke-opacity=\".34\"/><path d=\"M68 44h77\" stroke=\"#9baec8\" stroke-opacity=\".48\"/><path d=\"M82 48h48\" stroke=\"#9baec8\" stroke-opacity=\".24\"/><text x=\"113\" y=\"32\" fill=\"#e9f0fb\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"18\" font-weight=\"750\" text-anchor=\"middle\" dominant-baseline=\"central\" letter-spacing=\".15\">Common</text></svg>",
    "SR":"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"180\" height=\"64\" viewBox=\"0 0 180 64\" role=\"img\" aria-label=\"SR rarity\"><defs><linearGradient id=\"metal\" x1=\"0\" y1=\"0\" x2=\".7\" y2=\"1\"><stop stop-color=\"#f2e1ff\"/><stop offset=\".34\" stop-color=\"#c093f4\"/><stop offset=\".57\" stop-color=\"#7b46b3\"/><stop offset=\".81\" stop-color=\"#c093f4\"/><stop offset=\"1\" stop-color=\"#f2e1ff\"/></linearGradient><linearGradient id=\"plaque\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop stop-color=\"#283346\"/><stop offset=\".48\" stop-color=\"#151d2b\"/><stop offset=\"1\" stop-color=\"#0e1420\"/></linearGradient><linearGradient id=\"sheen\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop stop-color=\"#ffffff\" stop-opacity=\".16\"/><stop offset=\".42\" stop-color=\"#ffffff\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"#ffffff\" stop-opacity=\".03\"/></linearGradient></defs><rect x=\"1\" y=\"1\" width=\"178\" height=\"62\" rx=\"12\" fill=\"#080d16\" stroke=\"url(#metal)\" stroke-width=\"2\"/><rect x=\"5\" y=\"5\" width=\"170\" height=\"54\" rx=\"8.5\" fill=\"url(#plaque)\" stroke=\"#c093f4\" stroke-opacity=\".32\"/><path d=\"M13 7h154\" stroke=\"#f2e1ff\" stroke-opacity=\".42\" stroke-linecap=\"round\"/><rect x=\"6\" y=\"6\" width=\"168\" height=\"52\" rx=\"8\" fill=\"url(#sheen)\"/><circle cx=\"31\" cy=\"32\" r=\"22\" fill=\"#7b46b3\" fill-opacity=\".13\" stroke=\"#c093f4\" stroke-opacity=\".18\"/><path d=\"M31 14l5.3 11.2 12.2 1.5-8.9 8.5 2.4 12.1-11-5.9-11 5.9 2.4-12.1-8.9-8.5 12.2-1.5z\" fill=\"url(#metal)\" stroke=\"#efdaff\" stroke-width=\"1\"/><path d=\"M31 19l2.3 11 10.7-.8-8.8 6.1 3.4 10.1-7.6-7.3-7.6 7.3 3.4-10.1-8.8-6.1 10.7.8z\" fill=\"#f5e9ff\" fill-opacity=\".25\"/><path d=\"M31 25v12m-6-6h12\" stroke=\"#fff5ff\" stroke-width=\"1.1\" stroke-linecap=\"round\"/><path d=\"M60 16v32\" stroke=\"#c093f4\" stroke-opacity=\".34\"/><path d=\"M81 45h63\" stroke=\"#c093f4\" stroke-opacity=\".55\"/><path d=\"M108 45l5-4 5 4-5 4z\" fill=\"#c093f4\"/><text x=\"113\" y=\"32\" fill=\"#f2e1ff\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"25\" font-weight=\"750\" text-anchor=\"middle\" dominant-baseline=\"central\" letter-spacing=\"1.4\">SR</text></svg>",
    "Rare":"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"180\" height=\"64\" viewBox=\"0 0 180 64\" role=\"img\" aria-label=\"Rare rarity\"><defs><linearGradient id=\"metal\" x1=\"0\" y1=\"0\" x2=\".7\" y2=\"1\"><stop stop-color=\"#deeeff\"/><stop offset=\".34\" stop-color=\"#6cabff\"/><stop offset=\".57\" stop-color=\"#2d66b3\"/><stop offset=\".81\" stop-color=\"#6cabff\"/><stop offset=\"1\" stop-color=\"#deeeff\"/></linearGradient><linearGradient id=\"plaque\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop stop-color=\"#283346\"/><stop offset=\".48\" stop-color=\"#151d2b\"/><stop offset=\"1\" stop-color=\"#0e1420\"/></linearGradient><linearGradient id=\"sheen\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop stop-color=\"#ffffff\" stop-opacity=\".16\"/><stop offset=\".42\" stop-color=\"#ffffff\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"#ffffff\" stop-opacity=\".03\"/></linearGradient></defs><rect x=\"1\" y=\"1\" width=\"178\" height=\"62\" rx=\"12\" fill=\"#080d16\" stroke=\"url(#metal)\" stroke-width=\"2\"/><rect x=\"5\" y=\"5\" width=\"170\" height=\"54\" rx=\"8.5\" fill=\"url(#plaque)\" stroke=\"#6cabff\" stroke-opacity=\".32\"/><path d=\"M13 7h154\" stroke=\"#deeeff\" stroke-opacity=\".42\" stroke-linecap=\"round\"/><rect x=\"6\" y=\"6\" width=\"168\" height=\"52\" rx=\"8\" fill=\"url(#sheen)\"/><circle cx=\"31\" cy=\"32\" r=\"22\" fill=\"#2d66b3\" fill-opacity=\".13\" stroke=\"#6cabff\" stroke-opacity=\".18\"/><path d=\"M31 15l16 17-16 17-16-17z\" fill=\"url(#metal)\" stroke=\"#c6e2ff\" stroke-width=\"1\"/><path d=\"M31 15l-6 17 6 17 6-17z\" fill=\"#d6eaff\" fill-opacity=\".52\"/><path d=\"M15 32h32m-22 0h12\" stroke=\"#305c94\" stroke-width=\"1\"/><path d=\"M21 23l10-8 10 8\" fill=\"none\" stroke=\"#f3f9ff\" stroke-width=\"1.2\"/><path d=\"M60 16v32\" stroke=\"#6cabff\" stroke-opacity=\".34\"/><path d=\"M81 45h63\" stroke=\"#6cabff\" stroke-opacity=\".55\"/><path d=\"M110 42l3 3-3 3-3-3z\" fill=\"#6cabff\"/><text x=\"113\" y=\"32\" fill=\"#deeeff\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"23\" font-weight=\"750\" text-anchor=\"middle\" dominant-baseline=\"central\" letter-spacing=\".15\">Rare</text></svg>",
    "UR":"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"180\" height=\"64\" viewBox=\"0 0 180 64\" role=\"img\" aria-label=\"UR rarity\"><defs><linearGradient id=\"metal\" x1=\"0\" y1=\"0\" x2=\".7\" y2=\"1\"><stop stop-color=\"#ffe2f0\"/><stop offset=\".34\" stop-color=\"#ec91bf\"/><stop offset=\".57\" stop-color=\"#a23f76\"/><stop offset=\".81\" stop-color=\"#ec91bf\"/><stop offset=\"1\" stop-color=\"#ffe2f0\"/></linearGradient><linearGradient id=\"plaque\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop stop-color=\"#283346\"/><stop offset=\".48\" stop-color=\"#151d2b\"/><stop offset=\"1\" stop-color=\"#0e1420\"/></linearGradient><linearGradient id=\"sheen\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop stop-color=\"#ffffff\" stop-opacity=\".16\"/><stop offset=\".42\" stop-color=\"#ffffff\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"#ffffff\" stop-opacity=\".03\"/></linearGradient></defs><rect x=\"1\" y=\"1\" width=\"178\" height=\"62\" rx=\"12\" fill=\"#080d16\" stroke=\"url(#metal)\" stroke-width=\"2\"/><rect x=\"5\" y=\"5\" width=\"170\" height=\"54\" rx=\"8.5\" fill=\"url(#plaque)\" stroke=\"#ec91bf\" stroke-opacity=\".32\"/><path d=\"M13 7h154\" stroke=\"#ffe2f0\" stroke-opacity=\".42\" stroke-linecap=\"round\"/><rect x=\"6\" y=\"6\" width=\"168\" height=\"52\" rx=\"8\" fill=\"url(#sheen)\"/><circle cx=\"31\" cy=\"32\" r=\"22\" fill=\"#a23f76\" fill-opacity=\".13\" stroke=\"#ec91bf\" stroke-opacity=\".18\"/><path d=\"M31 10v8m0 28v8M9 32h7m30 0h7M15 16l6 6m20 20l6 6m0-32l-6 6m-20 20l-6 6\" stroke=\"#f6b2d6\" stroke-width=\"1.4\" stroke-linecap=\"round\"/><path d=\"M31 15l4.8 12.2L48 32l-12.2 4.8L31 49l-4.8-12.2L14 32l12.2-4.8z\" fill=\"url(#metal)\" stroke=\"#ffe4f2\" stroke-width=\"1\"/><path d=\"M31 21l2.8 8.2L42 32l-8.2 2.8L31 43l-2.8-8.2L20 32l8.2-2.8z\" fill=\"#ffe9f5\" fill-opacity=\".76\"/><circle cx=\"31\" cy=\"32\" r=\"2.5\" fill=\"#ffffff\"/><path d=\"M60 16v32\" stroke=\"#ec91bf\" stroke-opacity=\".34\"/><path d=\"M78 45h69\" stroke=\"#ec91bf\" stroke-opacity=\".65\"/><path d=\"M113 40l2 3 5 2-5 2-2 3-2-3-5-2 5-2z\" fill=\"#ec91bf\"/><text x=\"113\" y=\"32\" fill=\"#ffe2f0\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"25\" font-weight=\"750\" text-anchor=\"middle\" dominant-baseline=\"central\" letter-spacing=\"1.4\">UR</text></svg>",
    "Uncommon":"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"180\" height=\"64\" viewBox=\"0 0 180 64\" role=\"img\" aria-label=\"Uncommon rarity\"><defs><linearGradient id=\"metal\" x1=\"0\" y1=\"0\" x2=\".7\" y2=\"1\"><stop stop-color=\"#d5ffe9\"/><stop offset=\".34\" stop-color=\"#6ed4a5\"/><stop offset=\".57\" stop-color=\"#287b58\"/><stop offset=\".81\" stop-color=\"#6ed4a5\"/><stop offset=\"1\" stop-color=\"#d5ffe9\"/></linearGradient><linearGradient id=\"plaque\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop stop-color=\"#283346\"/><stop offset=\".48\" stop-color=\"#151d2b\"/><stop offset=\"1\" stop-color=\"#0e1420\"/></linearGradient><linearGradient id=\"sheen\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop stop-color=\"#ffffff\" stop-opacity=\".16\"/><stop offset=\".42\" stop-color=\"#ffffff\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"#ffffff\" stop-opacity=\".03\"/></linearGradient></defs><rect x=\"1\" y=\"1\" width=\"178\" height=\"62\" rx=\"12\" fill=\"#080d16\" stroke=\"url(#metal)\" stroke-width=\"2\"/><rect x=\"5\" y=\"5\" width=\"170\" height=\"54\" rx=\"8.5\" fill=\"url(#plaque)\" stroke=\"#6ed4a5\" stroke-opacity=\".32\"/><path d=\"M13 7h154\" stroke=\"#d5ffe9\" stroke-opacity=\".42\" stroke-linecap=\"round\"/><rect x=\"6\" y=\"6\" width=\"168\" height=\"52\" rx=\"8\" fill=\"url(#sheen)\"/><circle cx=\"31\" cy=\"32\" r=\"22\" fill=\"#287b58\" fill-opacity=\".13\" stroke=\"#6ed4a5\" stroke-opacity=\".18\"/><path d=\"M29 46C16 38 17 24 39 17c2 16-1 29-10 29z\" fill=\"url(#metal)\" stroke=\"#b7f8d5\" stroke-width=\"1\"/><path d=\"M25 43l11-19m-9 13l-4-6m8 0l5 1\" fill=\"none\" stroke=\"#154a36\" stroke-width=\"1.6\" stroke-linecap=\"round\"/><path d=\"M23 47c-5-4-9-10-9-17\" fill=\"none\" stroke=\"#6ed4a5\" stroke-width=\"1.2\" stroke-linecap=\"round\"/><path d=\"M60 16v32\" stroke=\"#6ed4a5\" stroke-opacity=\".34\"/><path d=\"M72 44h73\" stroke=\"#6ed4a5\" stroke-opacity=\".5\"/><path d=\"M82 48h51\" stroke=\"#6ed4a5\" stroke-opacity=\".25\"/><text x=\"113\" y=\"32\" fill=\"#d5ffe9\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"16.5\" font-weight=\"750\" text-anchor=\"middle\" dominant-baseline=\"central\" letter-spacing=\".15\">Uncommon</text></svg>",
    "SSR":"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"180\" height=\"64\" viewBox=\"0 0 180 64\" role=\"img\" aria-label=\"SSR rarity\"><defs><linearGradient id=\"metal\" x1=\"0\" y1=\"0\" x2=\".7\" y2=\"1\"><stop stop-color=\"#fff1c8\"/><stop offset=\".34\" stop-color=\"#efc975\"/><stop offset=\".57\" stop-color=\"#ab772f\"/><stop offset=\".81\" stop-color=\"#efc975\"/><stop offset=\"1\" stop-color=\"#fff1c8\"/></linearGradient><linearGradient id=\"plaque\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop stop-color=\"#283346\"/><stop offset=\".48\" stop-color=\"#151d2b\"/><stop offset=\"1\" stop-color=\"#0e1420\"/></linearGradient><linearGradient id=\"sheen\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\"><stop stop-color=\"#ffffff\" stop-opacity=\".16\"/><stop offset=\".42\" stop-color=\"#ffffff\" stop-opacity=\"0\"/><stop offset=\"1\" stop-color=\"#ffffff\" stop-opacity=\".03\"/></linearGradient></defs><rect x=\"1\" y=\"1\" width=\"178\" height=\"62\" rx=\"12\" fill=\"#080d16\" stroke=\"url(#metal)\" stroke-width=\"2\"/><rect x=\"5\" y=\"5\" width=\"170\" height=\"54\" rx=\"8.5\" fill=\"url(#plaque)\" stroke=\"#efc975\" stroke-opacity=\".32\"/><path d=\"M13 7h154\" stroke=\"#fff1c8\" stroke-opacity=\".42\" stroke-linecap=\"round\"/><rect x=\"6\" y=\"6\" width=\"168\" height=\"52\" rx=\"8\" fill=\"url(#sheen)\"/><circle cx=\"31\" cy=\"32\" r=\"22\" fill=\"#ab772f\" fill-opacity=\".13\" stroke=\"#efc975\" stroke-opacity=\".18\"/><path d=\"M18 25l6 5 7-13 7 13 6-5-3.5 18h-19z\" fill=\"url(#metal)\" stroke=\"#fff0be\" stroke-width=\"1\"/><path d=\"M21 43h20v4H21z\" fill=\"#dab15f\" stroke=\"#fff0be\" stroke-width=\".8\"/><path d=\"M14 29l-5-4 2 11 8 5m29-12l5-4-2 11-8 5\" fill=\"#efc975\" fill-opacity=\".62\" stroke=\"#f7dfa3\" stroke-width=\".7\"/><path d=\"M31 28l3 6-3 6-3-6z\" fill=\"#fff8df\"/><circle cx=\"18\" cy=\"23\" r=\"2\" fill=\"#fff0be\"/><circle cx=\"31\" cy=\"15\" r=\"2\" fill=\"#fff0be\"/><circle cx=\"44\" cy=\"23\" r=\"2\" fill=\"#fff0be\"/><path d=\"M60 16v32\" stroke=\"#efc975\" stroke-opacity=\".34\"/><path d=\"M78 45h69\" stroke=\"#efc975\" stroke-opacity=\".6\"/><path d=\"M104 45l3-3 6 3 6-3 3 3-9 4z\" fill=\"#efc975\"/><text x=\"113\" y=\"32\" fill=\"#fff1c8\" font-family=\"Segoe UI,Arial,sans-serif\" font-size=\"25\" font-weight=\"750\" text-anchor=\"middle\" dominant-baseline=\"central\" letter-spacing=\"1.4\">SSR</text></svg>"
  };
  var script = typeof document !== 'undefined' ? document.currentScript : null;
  var base = script && script.src ? script.src : (typeof document !== 'undefined' ? document.baseURI : '');
  function canonical(name) {
    var value = String(name == null ? '' : name).trim().toLowerCase();
    for (var i = 0; i < names.length; i++) if (names[i].toLowerCase() === value) return names[i];
    return 'Common';
  }
  function escape(value) {
    return String(value).replace(/[&<>"']/g, function (char) {
      return {'&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;'}[char];
    });
  }
  function url(name) {
    var path = 'rarities/' + canonical(name).toLowerCase() + '.svg';
    try { return new URL(path, base).href; } catch (_) { return path; }
  }
  function markup(name, extraClass) {
    var rarity = canonical(name);
    var className = 'rarityEmblem' + (extraClass ? ' ' + String(extraClass) : '');
    return '<img class="' + escape(className) + '" alt="' + rarity + '" src="' + escape(url(rarity)) + '" width="180" height="64" draggable="false" decoding="async">';
  }
  function load(name) {
    var rarity = canonical(name);
    if (!cache[rarity]) {
      cache[rarity] = new Promise(function (resolve) {
        var image;
        try { image = new global.Image(); } catch (_) { resolve(null); return; }
        image.onload = function () { image.onload = image.onerror = null; resolve(image); };
        image.onerror = function () { image.onload = image.onerror = null; resolve(null); };
        image.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(vectors[rarity]);
      });
    }
    return cache[rarity];
  }
  function fallback(ctx, rarity, box) {
    var color = colors[names.indexOf(rarity)], r = Math.min(12 * box.w / 180, box.h / 2);
    ctx.fillStyle = '#111925';
    ctx.strokeStyle = color;
    ctx.lineWidth = Math.max(1, box.w / 90);
    ctx.beginPath();
    ctx.moveTo(box.x + r, box.y);
    ctx.lineTo(box.x + box.w - r, box.y);
    ctx.quadraticCurveTo(box.x + box.w, box.y, box.x + box.w, box.y + r);
    ctx.lineTo(box.x + box.w, box.y + box.h - r);
    ctx.quadraticCurveTo(box.x + box.w, box.y + box.h, box.x + box.w - r, box.y + box.h);
    ctx.lineTo(box.x + r, box.y + box.h);
    ctx.quadraticCurveTo(box.x, box.y + box.h, box.x, box.y + box.h - r);
    ctx.lineTo(box.x, box.y + r);
    ctx.quadraticCurveTo(box.x, box.y, box.x + r, box.y);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    ctx.fillStyle = color;
    ctx.font = '700 ' + Math.max(8, box.h * .32) + 'px Segoe UI, Arial, sans-serif';
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText(rarity, box.x + box.w / 2, box.y + box.h / 2, box.w * .9);
  }
  async function draw(ctx, name, box) {
    if (!ctx || !box) return false;
    var bounds = {x:Number(box.x), y:Number(box.y), w:Number(box.w), h:Number(box.h)};
    if (!Number.isFinite(bounds.x) || !Number.isFinite(bounds.y) ||
        !Number.isFinite(bounds.w) || !Number.isFinite(bounds.h) || bounds.w <= 0 || bounds.h <= 0) return false;
    var rarity = canonical(name), image = await load(rarity);
    ctx.save();
    try {
      if (image) ctx.drawImage(image, bounds.x, bounds.y, bounds.w, bounds.h);
      else fallback(ctx, rarity, bounds);
    } catch (_) {
      fallback(ctx, rarity, bounds);
      image = null;
    } finally { ctx.restore(); }
    return !!image;
  }
  global.HyTcgRarity = Object.freeze({url:url, markup:markup, draw:draw});
})(window);

