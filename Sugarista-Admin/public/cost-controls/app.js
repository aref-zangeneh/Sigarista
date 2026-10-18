
(function(){
"use strict";
const SEED = window.SUGARISTA_COST_SEED;
const clone = x => JSON.parse(JSON.stringify(x));
const ICONS = {
  overview:'<path d="M3 13h8V3H3zM13 21h8V11h-8zM3 21h8v-6H3zM13 3v6h8V3z"/>',
  inputs:'<path d="M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6"/>',
  costs:'<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM23 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/>',
  capital:'<path d="M21 12V7H5a2 2 0 0 1 0-4h14v4M3 5v14a2 2 0 0 0 2 2h16v-5M18 12a2 2 0 0 0 0 4h4v-4z"/>',
  menu:'<path d="M17 8h1a4 4 0 1 1 0 8h-1M3 8h14v9a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4zM6 2v2M10 2v2M14 2v2"/>',
  cup:'<path d="M17 8h1a4 4 0 1 1 0 8h-1M3 8h14v9a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4zM6 2v2M10 2v2M14 2v2"/>',
  mats:'<path d="M21 8l-9-5-9 5 9 5 9-5zM3 8v8l9 5 9-5V8M12 13v8"/>',
  breakeven:'<path d="M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20zM12 18a6 6 0 1 0 0-12 6 6 0 0 0 0 12zM12 14a2 2 0 1 0 0-4 2 2 0 0 0 0 4z"/>',
  bank:'<path d="M3 21h18M5 21V10M19 21V10M9 21V10M15 21V10M2 10l10-7 10 7z"/>',
  fx:'<path d="M9 14a6 6 0 1 0 0-12 6 6 0 0 0 0 12zM18.1 10.4A6 6 0 1 1 10.4 18.1"/>',
  save:'<path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2zM17 21v-8H7v8M7 3v5h8"/>',
  refresh:'<path d="M23 4v6h-6M1 20v-6h6M3.5 9a9 9 0 0 1 14.9-3.4L23 10M1 14l4.6 4.4A9 9 0 0 0 20.5 15"/>',
  profit:'<path d="M3 17l6-6 4 4 8-8M15 7h6v6"/>',
  sales:'<path d="M6 2L3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4zM3 6h18M16 10a4 4 0 0 1-8 0"/>',
  pie:'<path d="M21.2 15.9A10 10 0 1 1 8 2.8M22 12A10 10 0 0 0 12 2v10z"/>',
  plus:'<path d="M12 5v14M5 12h14"/>'
};
const ic = (k, sw = 1.8) => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICONS[k] || ""}</svg>`;
const VIEWS = [
  ["overview", "نمای کلی", "وضعیت کسب‌وکار در یک نگاه"],
  ["inputs", "ورودی‌ها و نرخ‌ها", "نرخ ارز و طلا، مالیات، کمیسیون، سیاست قیمت"],
  ["costs", "پرسنل و هزینه‌ها", "حقوق شرکا و نیروها، هزینه‌های ثابت و ضریب قیمت‌گذاری"],
  ["capital", "خواب سرمایه", "سرمایه‌ی تزریق‌شده به ریال، دلار، یورو و طلا"],
  ["menu", "منو و کاست کنترل", "قیمت، فروش و بهای تمام‌شده‌ی هر آیتم"],
  ["mats", "مواد اولیه", "قیمت خرید و ضایعات"],
  ["breakeven", "سربه‌سر و سوددهی", "چند فروش در روز لازم است"],
  ["bank", "بانک و صندوق", "مقایسه با سپرده و صندوق سرمایه‌گذاری"],
  ["fx", "دلار، یورو، طلا", "حفظ ارزش سرمایه در برابر ارز و طلا"]
];

/* ---------- formatting ---------- */
const FA = s => String(s).replace(/[0-9]/g, d => "۰۱۲۳۴۵۶۷۸۹"[d]);
const EN = s => String(s).replace(/[۰-۹]/g, d => "۰۱۲۳۴۵۶۷۸۹".indexOf(d)).replace(/[٠-٩]/g, d => "٠١٢٣٤٥٦٧٨٩".indexOf(d));
const grp = (n, dec = 0) => isFinite(n) ? FA(Number(n).toLocaleString("en-US", {maximumFractionDigits: dec, minimumFractionDigits: dec})) : "—";
function T(n){ if (!isFinite(n)) return "—"; const a = Math.abs(n), s = n < 0 ? "−" : "";
  if (a >= 1e9) return s + FA((a/1e9).toFixed(2)) + " میلیارد";
  if (a >= 1e6) return s + FA((a/1e6).toFixed(1)) + " میلیون";
  return s + grp(a); }
const pct = (x, d = 1) => isFinite(x) ? FA((x*100).toFixed(d)) + "٪" : "—";
const grpD = v => grp(v, Math.abs(v) > 0 && Math.abs(v) < 100 ? 1 : 0);
const esc = s => String(s == null ? "" : s).replace(/[&<>"]/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c]));
const $ = id => document.getElementById(id);
const monthTxt = m => m ? "ماه " + FA(m) : "بیش از ۶۰ ماه";

/* ---------- state ---------- */
let REAL = null;
const normUnit = u => ({gr:["گرم",1,"گرم"], kg:["کیلو",1000,"گرم"], ml:["میلی‌لیتر",1,"میلی‌لیتر"], l:["لیتر",1000,"میلی‌لیتر"], pieces:["عدد",1,"عدد"], oz:["اونس",1,"اونس"], tsp:["قاشق چای‌خوری",1,"قاشق چای‌خوری"], tbsp:["قاشق غذاخوری",1,"قاشق غذاخوری"], cup:["پیمانه",1,"پیمانه"]}[u] || [u || "عدد", 1, u || "عدد"]);
const KIND_DRINK = /چای|قهوه|نوشیدنی|موکتل|شیک|لاته|کاپوچینو|آمریکانو|ماکیاتو|ماچا|دمنوش|لیموناد|موهیتو/;

function ingestReal(data){
  const ingredients = ((data && data.ingredients) || []).filter(x => x && x.id);
  const items = ((data && data.menuItems) || []).filter(x => x && x.id);
  if (!items.length){ REAL = null; return; }
  const mats = ingredients.length ? ingredients.map((ing, i) => {
    const u = normUnit(ing.unit);
    return {rid: ing.id, code: "M" + String(i + 1).padStart(2, "0"), name: ing.name || ing.englishName, cat: "مواد خرید", unit: u[0], pack: u[1], base: u[2], price: Math.max(0, +ing.unitCost || 0), waste: .03};
  }) : clone(SEED.mats);
  const byIng = {}; mats.forEach(m => byIng[m.rid || m.code] = m);
  const menu = items.map((x, i) => {
    const kind = KIND_DRINK.test(x.category || "") ? "نوشیدنی" : "خوراکی";
    return {rid: x.id, code: "R" + String(i + 1).padStart(2, "0"), name: x.persianName || x.englishName, cat: x.category || "سایر", line: "—",
      w: kind === "نوشیدنی" ? 1.2 : .5, tm: kind === "نوشیدنی" ? .3 : .25,
      daily: Math.round((+x.dailyAvg || (+x.soldLast30 || 0) / 30) * 10) / 10,
      rec: (x.recipe || []).filter(r => byIng[r.ingredientId]).map(r => [r.ingredientId, +r.quantity || 0]),
      price: Math.max(0, +x.defaultPrice || 0), kind, est: false};
  });
  const cats = [];
  menu.forEach(m => { if (cats.indexOf(m.cat) < 0) cats.push(m.cat); });
  REAL = {mats, menu, cats: cats.length ? cats : ["سایر"]};
}

function applyEditsTo(st){
  for (const [rid, e] of Object.entries(st.matsEdits || {})){
    const m = st.mats.find(x => (x.rid || x.code) === rid); if (m) Object.assign(m, e);
  }
  for (const [rid, e] of Object.entries(st.menuEdits || {})){
    const m = st.menu.find(x => (x.rid || x.code) === rid); if (!m) continue;
    const recEdit = e.rec; Object.assign(m, ...Object.entries(e).filter(([k]) => k !== "rec").map(([k, v]) => ({[k]: v})));
    if (recEdit) m.rec = m.rec.map(line => [line[0], recEdit[line[0]] !== undefined ? recEdit[line[0]] : line[1]]);
  }
  return st;
}
function defaults(){
  const d = clone(SEED);
  const st = {v: 3, savedAt: 0, params: d.params, staff: d.staff, lines: d.lines, inj: d.inj, sug: d.sug, rateMode: "manual", pmode: "menu", vol: 1, matsEdits: {}, menuEdits: {}};
  if (REAL){ st.mats = clone(REAL.mats); st.menu = clone(REAL.menu); }
  else { st.mats = d.mats; st.menu = d.menu; }
  return st;
}
function applySavedState(saved){
  const st = defaults();
  if (!saved) return st;
  if (saved.params) st.params = saved.params;
  if (saved.staff) st.staff = saved.staff;
  if (saved.lines) st.lines = saved.lines;
  if (saved.inj) st.inj = saved.inj;
  if (saved.sug) st.sug = saved.sug;
  if (saved.rateMode) st.rateMode = saved.rateMode;
  if (saved.pmode) st.pmode = saved.pmode;
  if (saved.vol) st.vol = saved.vol;
  st.savedAt = saved.savedAt || 0;
  st.matsEdits = saved.matsEdits || {};
  st.menuEdits = saved.menuEdits || {};
  return applyEditsTo(st);
}
function noteEdit(path){
  const mo = path.match(/^mats\.(\d+)\.(name|pack|price|waste)$/);
  if (mo){ const mt = S.mats[+mo[1]]; if (mt && (mt.rid || mt.code)){ const key = mt.rid || mt.code; S.matsEdits[key] = Object.assign({}, S.matsEdits[key] || {}, {[mo[2]]: mt[mo[2]]}); } return; }
  const it = path.match(/^menu\.(\d+)\.(price|daily|tm|w)$/);
  if (it){ const m = S.menu[+it[1]]; if (m && (m.rid || m.code)){ const key = m.rid || m.code; S.menuEdits[key] = Object.assign({}, S.menuEdits[key] || {}, {[it[2]]: m[it[2]]}); } return; }
  const rc = path.match(/^menu\.(\d+)\.rec\.(\d+)\.1$/);
  if (rc){ const m = S.menu[+rc[1]]; const line = m && m.rec[+rc[2]];
    if (line && (m.rid || m.code)){ const key = m.rid || m.code; const cur = S.menuEdits[key] = Object.assign({}, S.menuEdits[key] || {}); cur.rec = Object.assign({}, cur.rec || {}, {[line[0]]: line[1]}); } }
}
const menuCats = () => { const r = []; S.menu.forEach(m => { if (r.indexOf(m.cat) < 0) r.push(m.cat); }); return r; };
let S = defaults();
let dirty = false;
const UI = {view: "overview", cat: "همه", sel: null, live: null, liveMsg: "", readOnly: false, saving: false, resetArm: 0};
const getP = path => path.split(".").reduce((o, k) => o == null ? o : o[k], S);
function setP(path, v){ const ks = path.split("."); let o = S; for (let i = 0; i < ks.length - 1; i++) o = o[ks[i]]; o[ks[ks.length - 1]] = v; }

/* ---------- model ---------- */
let M = {};
function rates(){ const p = S.params, L = UI.live, live = S.rateMode === "live" && L;
  return {usd: live && L.usd > 0 ? L.usd : p.usd, eur: live && L.eur > 0 ? L.eur : p.eur, gold: live && L.gold > 0 ? L.gold : p.gold,
    src: live ? L.src : "ورود دستی"}; }
function compute(){
  const p = S.params, r = rates();
  const ded = p.online*p.comm + p.disc + p.pos;
  const mm = {}; for (const m of S.mats) mm[m.rid || m.code] = Object.assign({}, m, {eff: m.pack > 0 ? m.price/m.pack/(1 - Math.min(+m.waste || 0, .95)) : 0});
  const bufR = Math.pow(1 + p.infl, p.buffer) - 1;
  const coffeeCodes = {};
  for (const m of S.mats){ const nm = m.name || ""; if (nm.indexOf("قهوه") >= 0 || nm.indexOf("نسکافه") >= 0) coffeeCodes[m.rid || m.code] = 1; }
  const it0 = S.menu.map(m => { let mat = 0, cof = 0;
    for (const [c, q] of m.rec){ const v = mm[c] ? mm[c].eff*q : 0; mat += v; if (coffeeCodes[c]) cof += v; }
    return Object.assign({}, m, {mat, cof, buf: mat*bufR, hid: mat*p.hidden}); });
  const staffT = S.staff.reduce((a, x) => a + (+x.sal || 0)*(+x.cnt || 0)*(1 + (+x.ins || 0) + (+x.acc || 0))*(x.on ? 1 : 0), 0);
  const deposit = +S.sug[0].v || 0, preopen = S.sug.slice(2, 6).reduce((a, b) => a + (+b.v || 0), 0);
  const lines = S.lines.map((l, i) => { let v = +l.v || 0;
    if (l.calc === "staff") v = staffT; else if (l.calc === "capc") v = deposit*p.bank/12; else if (l.calc === "preopen") v = p.amort > 0 ? preopen/p.amort : 0;
    return Object.assign({}, l, {i, v}); });
  const otherOp = lines.filter(l => l.t === "عملیاتی" && l.calc !== "cont").reduce((a, l) => a + l.v, 0);
  const cont = otherOp*SEED.cont_rate; lines.forEach(l => { if (l.calc === "cont") l.v = cont; });
  const op = otherOp + cont, nc = lines.filter(l => l.t === "غیرنقدی").reduce((a, l) => a + l.v, 0);
  const capc = lines.filter(l => l.t === "هزینه سرمایه").reduce((a, l) => a + l.v, 0);
  const acc = op + nc, priceBase = acc + (p.capcost ? capc : 0);
  let units = 0, matM = 0, wU = 0; for (const m of it0){ const u = (+m.daily || 0)*p.days; units += u; matM += u*m.mat; wU += u*m.w; }
  const K = matM ? priceBase/matM : 0, perUnit = units ? priceBase/units : 0, perW = wU ? priceBase/wU : 0;
  const costCoef = 1 + bufR + p.hidden + K, den0 = 1 - p.margin - ded, finalCoef = den0 > 0 ? costCoef/den0*(1 + p.vat) : 0;
  const items = it0.map(m => {
    const ovh = p.method == 2 ? m.mat*K : p.method == 3 ? perUnit : m.w*perW;
    const full = m.mat + m.buf + m.hid + ovh, den = 1 - m.tm - ded, base = den > 0 ? full/den : 0;
    const sug = Math.ceil(base*(1 + p.vat)/p.round)*p.round, price = S.pmode === "model" ? sug : +m.price || 0;
    const net = price/(1 + p.vat), netrev = net*(1 - ded), profit = netrev - full, pm = net ? profit/net : 0, fc = net ? m.mat/net : 0;
    const lim = m.kind === "خوراکی" ? p.fc_food : p.fc_drink;
    const status = pm < 0 ? "زیان‌ده" : fc > lim ? "فود کاست بالا" : pm < m.tm - .02 ? "زیر هدف" : "مطلوب";
    const mu = (+m.daily || 0)*p.days;
    return Object.assign({}, m, {ovh, full, sug, price, net, netrev, profit, pm, fc, lim, status, cmU: netrev - m.mat - m.buf - m.hid,
      cofShare: price ? m.cof/price : 0, mu, mProfit: mu*profit, mRev: mu*netrev}); });
  const CM = {ded, mm, bufR, staffT, lines, cont, op, nc, capc, acc, priceBase, units, matM, K, perUnit, costCoef, finalCoef, items};
  M = {p, r, CM};
  M.ops = ops(1); M.o = ops(S.vol);
  M.cap = capital(); M.ser = series(M.cap.tot.t); M.fxs = fxModel(); M.health = health(); M.cats = catModel();
}
function ops(mult){
  const p = S.params, CM = M.CM; let units = 0, rev = 0, vc = 0, gross = 0, matCost = 0;
  for (const m of CM.items){ const u = (+m.daily || 0)*p.days*mult; units += u; rev += u*m.netrev; gross += u*m.price; vc += u*(m.mat + m.buf + m.hid); matCost += u*m.mat; }
  const cm = rev - vc, fix = CM.acc, cmr = rev ? cm/rev : 0, avg = units ? rev/units : 0;
  const be = cmr > 0 ? fix/cmr : Infinity, beDay = avg && cmr > 0 ? be/p.days/avg : Infinity;
  const tgtDay = avg && cmr > 0 ? (fix + p.target)/cmr/p.days/avg : Infinity;
  const econDay = avg && cmr > 0 ? (fix + CM.capc)/cmr/p.days/avg : Infinity;
  return {units, daily: units/p.days, rev, gross, vc, cm, fix, cmr, avg, profit: cm - fix, be, beDay, tgtDay, econDay,
    mos: rev && isFinite(be) ? (rev - be)/rev : -1, fc: gross ? matCost/(gross/(1 + p.vat)) : 0, pm: rev ? (cm - fix)/(rev/(1 - CM.ded)) : 0};
}
function capital(){
  const r = M.r, p = S.params;
  const rows = S.inj.map(x => { const amt = +x.amt || 0, u0 = +x.usd || 0, e0 = +x.eur || 0, g0 = +x.gold || 0;
    const t = x.cur === "ریال" ? amt/10 : x.cur === "دلار" ? amt*u0 : x.cur === "یورو" ? amt*e0 : x.cur === "طلا" ? amt*g0 : amt;
    const u = u0 ? t/u0 : 0, e = e0 ? t/e0 : 0, g = g0 ? t/g0 : 0;
    return Object.assign({}, x, {t, u, e, g, tu: u*r.usd, te: e*r.eur, tg: g*r.gold}); });
  const sum = k => rows.reduce((a, b) => a + b[k], 0);
  const tot = {t: sum("t"), u: sum("u"), e: sum("e"), g: sum("g"), tu: sum("tu"), te: sum("te"), tg: sum("tg")};
  const vcM = M.ops.vc;
  const sug = S.sug.map(s => Object.assign({}, s, {v: s.calc === "inv" ? vcM*20/30 : s.calc === "wc" ? M.CM.op*3 : +s.v || 0}));
  const sub = sug.reduce((a, b) => a + b.v, 0), cont = sub*.1, sugTot = sub + cont;
  const fxShare = sub ? (sug[1].v + sug[6].v)/sub : 0;
  const adj = sugTot*(1 + fxShare*(r.usd/(p.usd_base || r.usd) - 1));
  const byWho = {}; rows.forEach(x => byWho[x.who || "—"] = (byWho[x.who || "—"] || 0) + x.t);
  return {rows, tot, sug, sub, cont, sugTot, fxShare, adj, gap: tot.t - adj, byWho};
}
function series(C){
  const p = S.params, o = M.ops, out = []; let cum = 0;
  for (let m = 1; m <= 60; m++){
    const ramp = m === 1 ? p.ramp1 : m === 2 ? p.ramp2 : m === 3 ? p.ramp3 : 1, g = Math.pow(1 + p.growth, m - 1);
    const rev = o.rev*ramp*g, cost = (o.vc*ramp + o.fix)*g, pr = rev - cost; cum += pr;
    out.push({m, rev, cost, p: pr, cafe: cum, bank: C*p.bank/12*m, fund: C*(Math.pow(1 + p.fund, m) - 1), cash: cum + M.CM.nc*m});
  }
  const first = f => { const r = out.find(f); return r ? r.m : null; };
  return {out, kpi: {profitM: first(r => r.p > 0), recover: first(r => r.cafe > 0), bank: first(r => r.cafe >= r.bank), fund: first(r => r.cafe >= r.fund), payback: first(r => C > 0 && r.cash >= C)}};
}
const fxStatus = chg => Math.abs(chg) <= S.params.fxband ? ["سربه‌سر", "p-warn"] : chg > 0 ? ["سودده", "p-ok"] : ["ضررده", "p-bad"];
function fxModel(){
  const c = M.cap, em = Math.max(1, Math.min(60, S.params.evalmonth)), cum = M.ser.out[em - 1].cafe, W = c.tot.t + cum, r = M.r;
  const rows = [["تومان", c.tot.t, 1, "تومان", 0], ["دلار", c.tot.u, r.usd, "$", 0], ["یورو", c.tot.e, r.eur, "€", 0], ["طلای ۱۸", c.tot.g, r.gold, "گرم", 1]]
    .map(([n, init, rate, u, d]) => { const now = W/rate, chg = init ? now/init - 1 : 0; return {n, init, rate, u, d, now, chg, st: fxStatus(chg), be: init ? W/init : 0}; });
  return {em, cum, W, rows};
}
function lin(x, a, b){ return Math.max(0, Math.min(1, (x - a)/(b - a))); }
function health(){
  const o = M.ops, c = M.cap;
  const ratio = isFinite(o.beDay) && o.beDay > 0 ? o.daily/o.beDay : 0;
  const parts = [
    ["فروش در برابر سربه‌سر", lin(ratio, .8, 1.5), FA(ratio.toFixed(2)) + "×"],
    ["حاشیه سود عملیاتی", lin(o.pm, -.05, .2), pct(o.pm, 0)],
    ["فود کاست میانگین", 1 - lin(o.fc, .3, .5), pct(o.fc, 0)],
    ["پوشش سرمایه‌ی لازم", lin(c.adj ? c.tot.t/c.adj : 0, .7, 1), pct(c.adj ? c.tot.t/c.adj : 0, 0)]];
  const score = Math.round(parts.reduce((a, x) => a + x[1]*25, 0));
  const lbl = score >= 75 ? ["سالم", "p-ok"] : score >= 50 ? ["قابل قبول، با ریسک", "p-warn"] : score >= 25 ? ["نیاز به اصلاح", "p-bad"] : ["بحرانی", "p-bad"];
  return {score, lbl, parts};
}
function catModel(){
  const by = {};
  for (const m of M.CM.items){ const k = m.cat; const b = by[k] || (by[k] = {cat: k, n: 0, daily: 0, rev: 0, profit: 0, mat: 0, net: 0, loss: 0});
    b.n++; b.daily += +m.daily || 0; b.rev += m.mRev; b.profit += m.mProfit; b.mat += m.mu*m.mat; b.net += m.mu*m.net; if (m.status === "زیان‌ده") b.loss++; }
  return menuCats().map(c => by[c]).filter(Boolean).map(b => Object.assign(b, {fc: b.net ? b.mat/b.net : 0, pm: b.net ? b.profit/b.net : 0}));
}

/* ---------- charts ---------- */
function niceMax(v){ if (v <= 0) return 1; const p = Math.pow(10, Math.floor(Math.log10(v))), n = v/p; return (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10)*p; }
const short = v => T(v).replace(" میلیارد", "B").replace(" میلیون", "M");
function axes(H, lo, hi, L, R, Tp, B, W){
  const y = v => Tp + (H - Tp - B)*(1 - (v - lo)/(hi - lo)); let s = "";
  for (let i = 0; i <= 4; i++){ const v = lo + (hi - lo)*i/4;
    s += `<line x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}" stroke="var(--line)"/><text x="${L - 8}" y="${y(v) + 4}" text-anchor="end">${short(v)}</text>`; }
  if (lo < 0) s += `<line x1="${L}" x2="${W - R}" y1="${y(0)}" y2="${y(0)}" stroke="var(--muted)" stroke-width="1.2"/>`;
  return {y, s};
}
function range(vals){ let lo = Math.min(0, ...vals), hi = Math.max(0, ...vals); hi = niceMax(hi); if (lo < 0) lo = -niceMax(-lo); if (hi === lo) hi = lo + 1; return [lo, hi]; }
function comboChart(svg, labels, barsV, lines){
  const W = 720, H = +svg.getAttribute("viewBox").split(" ")[3], L = 70, R = 14, Tp = 12, B = 28;
  const [lo, hi] = range(barsV.concat(...lines.map(l => l.v)));
  const {y, s: ax} = axes(H, lo, hi, L, R, Tp, B, W); let s = ax;
  const n = labels.length, bw = (W - L - R)/n, x = i => L + bw*(i + .5);
  barsV.forEach((v, i) => { const y0 = y(0), y1 = y(v); s += `<rect x="${x(i) - bw*.28}" y="${Math.min(y0, y1)}" width="${bw*.56}" height="${Math.max(1, Math.abs(y1 - y0))}" rx="3" fill="${v >= 0 ? "var(--k3)" : "var(--k6)"}"/>`; });
  for (const l of lines){ const d = l.v.map((v, i) => (i ? "L" : "M") + x(i).toFixed(1) + " " + y(v).toFixed(1)).join(" ");
    s += `<path d="${d}" fill="none" stroke="${l.c}" stroke-width="2.4" stroke-linejoin="round" stroke-linecap="round"/>`;
    s += `<circle cx="${x(n - 1)}" cy="${y(l.v[n - 1])}" r="3.5" fill="${l.c}"/>`; }
  labels.forEach((t, i) => s += `<text x="${x(i)}" y="${H - 8}" text-anchor="middle">${t}</text>`);
  svg.innerHTML = s;
}
function lineChart(svg, xs, lines, opt = {}){
  const W = 720, H = +svg.getAttribute("viewBox").split(" ")[3], L = 70, R = 16, Tp = 20, B = 28;
  const [lo, hi] = range([].concat(...lines.map(l => l.v)).filter(isFinite));
  const {y, s: ax} = axes(H, lo, hi, L, R, Tp, B, W); let s = ax;
  const x = i => L + (W - L - R)*(i/(xs.length - 1));
  const step = opt.xStep || Math.ceil(xs.length/8);
  xs.forEach((v, i) => { if (i % step === 0 || i === xs.length - 1) s += `<text x="${x(i)}" y="${H - 8}" text-anchor="middle">${FA(v)}</text>`; });
  for (const l of lines){ const d = l.v.map((v, i) => (i ? "L" : "M") + x(i).toFixed(1) + " " + y(v).toFixed(1)).join(" ");
    if (l.area) s += `<path d="${d} L${x(l.v.length - 1)} ${y(Math.max(lo, 0))} L${x(0)} ${y(Math.max(lo, 0))}Z" fill="${l.c}" opacity=".10"/>`;
    s += `<path d="${d}" fill="none" stroke="${l.c}" stroke-width="${l.w || 2.4}" stroke-linejoin="round" ${l.dash ? 'stroke-dasharray="5 5"' : ""}/>`;
    const i = l.v.length - 1; s += `<circle cx="${x(i)}" cy="${y(l.v[i])}" r="3.5" fill="${l.c}"/>`; }
  (opt.marks || []).forEach((mk, j) => { if (!isFinite(mk.i)) return; const xi = Math.min(Math.max(x(mk.i), L), W - R);
    s += `<line x1="${xi}" x2="${xi}" y1="${Tp}" y2="${H - B}" stroke="${mk.c}" stroke-dasharray="3 4"/><text x="${xi}" y="${10 + j*11}" text-anchor="middle" style="fill:${mk.c};font-weight:700">${mk.t}</text>`; });
  svg.innerHTML = s;
}
function donut(parts, center, sub){
  const tot = parts.reduce((a, b) => a + Math.max(0, b.v), 0) || 1, R = 60, r = 42, cx = 75, cy = 75; let a0 = -Math.PI/2, s = "";
  for (const p of parts){ const a1 = a0 + 2*Math.PI*Math.max(0, p.v)/tot; if (a1 - a0 < 1e-4){ continue; }
    const big = a1 - a0 > Math.PI ? 1 : 0, P = (a, rr) => [cx + rr*Math.cos(a), cy + rr*Math.sin(a)];
    const [x1, y1] = P(a0, R), [x2, y2] = P(a1 - .01, R), [x3, y3] = P(a1 - .01, r), [x4, y4] = P(a0, r);
    s += `<path d="M${x1} ${y1}A${R} ${R} 0 ${big} 1 ${x2} ${y2}L${x3} ${y3}A${r} ${r} 0 ${big} 0 ${x4} ${y4}Z" fill="${p.c}"/>`; a0 = a1; }
  s += `<text x="${cx}" y="${cy}" text-anchor="middle" style="fill:var(--ink);font-weight:800;font-size:15px">${center}</text><text x="${cx}" y="${cy + 16}" text-anchor="middle">${sub}</text>`;
  return `<svg viewBox="0 0 150 150" role="img" aria-label="${esc(sub)}">${s}</svg>`;
}
function gauge(score){
  const cx = 130, cy = 120, n = 36; let s = "";
  for (let i = 0; i < n; i++){ const a = Math.PI + Math.PI*i/(n - 1), on = i/(n - 1) <= score/100;
    const x1 = cx + 86*Math.cos(a), y1 = cy + 86*Math.sin(a), x2 = cx + 108*Math.cos(a), y2 = cy + 108*Math.sin(a);
    const col = on ? (score >= 75 ? "var(--k1)" : score >= 50 ? "var(--k4)" : "var(--k5)") : "var(--line)";
    s += `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" stroke="${col}" stroke-width="5" stroke-linecap="round"/>`; }
  s += `<text x="${cx}" y="${cy - 14}" text-anchor="middle" style="fill:var(--ink);font-size:34px;font-weight:800">${FA(score)}</text><text x="${cx}" y="${cy + 6}" text-anchor="middle" style="font-size:12px">از ۱۰۰</text>`;
  return `<svg viewBox="0 0 260 130" role="img" aria-label="شاخص سلامت ${FA(score)} از ۱۰۰">${s}</svg>`;
}

/* ---------- inputs ---------- */
function fmtIn(v, kind){ if (v == null || v === "") return "";
  if (kind === "pct") return FA(+(v*100).toFixed(2)); if (kind === "num") return grp(v); if (kind === "dec") return FA(v); return v; }
const fid = path => "f-" + path.replace(/[^A-Za-z0-9]/g, "-");
function inp(path, kind = "num", cls = "", label = "", sfx = ""){
  return `<input class="in ${kind} ${cls}" id="${fid(path)}${sfx}" data-k="${path}" data-kind="${kind}" value="${esc(fmtIn(getP(path), kind))}" inputmode="${kind === "text" ? "text" : "decimal"}"${label ? ` aria-label="${esc(label)}"` : ""}${UI.readOnly ? " readonly" : ""}>`;
}
function sel(path, opts, cls = ""){ const v = getP(path);
  return `<select class="in ${cls}" id="${fid(path)}" data-k="${path}" data-kind="sel"${UI.readOnly ? " disabled" : ""}>${opts.map(([val, t]) => `<option value="${esc(val)}"${String(val) === String(v) ? " selected" : ""}>${t}</option>`).join("")}</select>`; }
function chk(path){ return `<input type="checkbox" id="${fid(path)}" data-k="${path}" data-kind="chk"${getP(path) ? " checked" : ""}${UI.readOnly ? " disabled" : ""}>`; }
function parseIn(el){
  const kind = el.dataset.kind;
  if (kind === "chk") return el.checked ? 1 : 0;
  if (kind === "sel") return isNaN(+el.value) ? el.value : +el.value;
  if (kind === "text") return el.value.trim();
  const s = EN(el.value).replace(/[,٬،\s]/g, "").replace(/[^\d.\-]/g, "");
  const n = parseFloat(s); if (!isFinite(n)) return undefined;
  return kind === "pct" ? n/100 : n;
}
document.addEventListener("change", e => {
  const el = e.target, k = el.dataset && el.dataset.k; if (!k || UI.readOnly) return;
  const v = parseIn(el);
  if (v === undefined){ el.value = fmtIn(getP(k), el.dataset.kind); toast("عدد معتبر وارد کنید."); return; }
  setP(k, v); noteEdit(k);
  if (k === "params.usd" || k === "params.eur" || k === "params.gold") S.rateMode = "manual";
  if (el.dataset.kind !== "chk" && el.dataset.kind !== "sel") el.value = fmtIn(v, el.dataset.kind);
  document.querySelectorAll("[data-k]").forEach(o => { if (o !== el && o.dataset.k === k && o.dataset.kind !== "chk" && o.dataset.kind !== "sel") o.value = fmtIn(v, o.dataset.kind); });
  changed(el.dataset.rebuild === "1");
});
function changed(rebuild){ dirty = true; compute(); if (rebuild) showView(UI.view, true); else outputs(); }

/* ---------- views ---------- */
const V = {};
V.overview = {
  html: () => `
  <section class="kpis" id="o-kpis" aria-label="شاخص‌های کلیدی"></section>
  <div class="row r-2-1">
    <div class="card"><div class="ch"><h3>پیش‌بینی ۱۲ ماه اول</h3><div class="legend"><span><i style="background:var(--k1)"></i>درآمد خالص</span><span><i style="background:var(--k5)"></i>کل هزینه</span><span><i style="background:var(--k3)"></i>سود ماه</span></div></div>
      <svg class="chart" id="ch12" viewBox="0 0 720 260" role="img" aria-label="درآمد، هزینه و سود ۱۲ ماه اول"></svg>
      <p class="note">با رمپ‌آپ ماه‌های اول و رشد ماهانه‌ی ${pct(S.params.growth, 0)}. قیمت‌ها: ${S.pmode === "model" ? "پیشنهاد مدل" : "قیمت منو"}.</p></div>
    <div class="card"><div class="ch"><h3>شاخص سلامت کسب‌وکار</h3><span id="o-hlbl"></span></div><div class="gauge" id="o-health"></div></div>
  </div>
  <div class="row r-3">
    <div class="card"><div class="ch"><h3>هشدارها و اقدام‌ها</h3></div><div class="alerts" id="o-alerts"></div></div>
    <div class="card"><div class="ch"><h3>ساختار هزینه‌ی ثابت ماهانه</h3></div><div class="donutwrap" id="o-cost"></div></div>
    <div class="card"><div class="ch"><h3>وضعیت آیتم‌های منو</h3></div><div id="o-mstat"></div></div>
  </div>
  <div class="row r-3">
    <div class="card"><div class="ch"><h3>بیشترین سود ماهانه</h3><p>سود خالص × فروش ماه</p></div><div class="lst" id="o-top"></div></div>
    <div class="card"><div class="ch"><h3>بیشترین زیان ماهانه</h3><p>آیتم‌هایی که باید بازبینی شوند</p></div><div class="lst" id="o-bot"></div></div>
    <div class="card"><div class="ch"><h3>در برابر بازارهای جایگزین</h3></div><div class="lst" id="o-alt"></div></div>
  </div>
  <div class="card"><div class="ch"><h3>عملکرد دسته‌های منو</h3><p>ماهانه، با فروش پیش‌بینی</p></div><div class="tbl"><table id="o-cats"></table></div></div>`,
  out(){
    const o = M.ops, c = M.cap, s = M.ser, h = M.health, CM = M.CM, p = S.params;
    const ratio = isFinite(o.beDay) ? o.daily/o.beDay - 1 : -1;
    const dlt = (v, good, txt) => `<span class="d ${good ? "up" : "dn"}">${good ? "▲" : "▼"} ${txt}</span>`;
    const kp = (cls, icon, lb, v, sub, d) => `<div class="kpi"><div class="hd"><div class="ib ${cls}">${ic(icon)}</div><div class="lb">${lb}</div></div><div class="v">${v}</div><div class="s"><span>${sub}</span>${d}</div></div>`;
    $("o-kpis").innerHTML = [
      kp("n", "profit", "سود عملیاتی ماهانه", T(o.profit), "حاشیه " + pct(o.pm, 1), dlt(0, o.profit >= 0, o.profit >= 0 ? "سودده" : "ضررده")),
      kp("g", "sales", "فروش روزانه", grpD(o.daily) + ' <small class="muted">فروش</small>', "سربه‌سر " + (isFinite(o.beDay) ? FA(Math.ceil(o.beDay)) : "—"), dlt(0, ratio >= 0, pct(ratio, 0))),
      kp("n", "pie", "درآمد خالص ماهانه", T(o.rev), "ناخالص " + T(o.gross), ""),
      kp("r", "cup", "فود کاست میانگین", pct(o.fc, 1), "سقف نوشیدنی " + pct(p.fc_drink, 0), dlt(0, o.fc <= p.fc_drink + .05, o.fc <= p.fc_drink + .05 ? "قابل قبول" : "بالا")),
      kp("g", "capital", c.gap < 0 ? "کسری سرمایه" : "مازاد سرمایه", T(Math.abs(c.gap)), "پیشنهادی " + T(c.adj), dlt(0, c.gap >= 0, pct(c.adj ? c.tot.t/c.adj : 0, 0) + " پوشش")),
      kp("r", "bank", "رسیدن به سود بانک", monthTxt(s.kpi.bank), "صندوق: " + monthTxt(s.kpi.fund), "")].join("");
    const y12 = s.out.slice(0, 12);
    comboChart($("ch12"), y12.map(r => FA(r.m)), y12.map(r => r.p), [{v: y12.map(r => r.rev), c: "var(--k1)"}, {v: y12.map(r => r.cost), c: "var(--k5)"}]);
    $("o-hlbl").innerHTML = `<span class="pill ${h.lbl[1]}">${h.lbl[0]}</span>`;
    $("o-health").innerHTML = gauge(h.score) + `<div class="hb">${h.parts.map(([n, v, t]) => `<div><span>${n}</span><span class="bar"><i style="width:${v*100}%;background:${v >= .7 ? "var(--k2)" : v >= .4 ? "var(--k4)" : "var(--k5)"}"></i></span><em>${t}</em></div>`).join("")}</div>`;
    $("o-alerts").innerHTML = alerts().slice(0, 6).map(a => `<div class="al ${a.s}"><span class="dot"></span><div><b>${a.t}</b><span>${a.d}</span></div>${a.go ? `<button type="button" data-act="goto:${a.go}">${a.btn || "بررسی"}</button>` : ""}</div>`).join("");
    const g = {}; CM.lines.forEach(l => { if (l.t === "هزینه سرمایه" && !p.capcost) return; g[l.g] = (g[l.g] || 0) + l.v; });
    const arr = Object.entries(g).sort((a, b) => b[1] - a[1]), top = arr.slice(0, 5), rest = arr.slice(5).reduce((a, b) => a + b[1], 0);
    const cols = ["var(--k1)", "var(--k2)", "var(--k3)", "var(--k4)", "var(--k5)", "var(--k6)"];
    const parts = top.map(([k, v], i) => ({n: k, v, c: cols[i]})); if (rest > 0) parts.push({n: "سایر", v: rest, c: cols[5]});
    const tot = parts.reduce((a, b) => a + b.v, 0);
    $("o-cost").innerHTML = donut(parts, T(tot).replace(" میلیون", "M").replace(" میلیارد", "B"), "در ماه") +
      `<div class="lg-rows">${parts.map(x => `<div><i style="background:${x.c}"></i><span>${x.n}</span><b>${T(x.v)}</b><em>${pct(x.v/tot, 0)}</em></div>`).join("")}</div>`;
    const st = ["مطلوب", "زیر هدف", "فود کاست بالا", "زیان‌ده"], stc = {"مطلوب": "var(--k2)", "زیر هدف": "var(--k4)", "فود کاست بالا": "var(--k6)", "زیان‌ده": "var(--k5)"};
    const cnt = st.map(k => CM.items.filter(m => m.status === k).length), n = CM.items.length;
    $("o-mstat").innerHTML = `<div class="stackbar">${st.map((k, i) => `<div style="width:${100*cnt[i]/n}%;background:${stc[k]}" title="${k}"></div>`).join("")}</div>
      <div class="lg-rows" style="margin-top:10px">${st.map((k, i) => `<div><i style="background:${stc[k]}"></i><span>${k}</span><b>${FA(cnt[i])} قلم</b><em>${pct(cnt[i]/n, 0)}</em></div>`).join("")}</div>
      <div class="statgrid" style="margin-top:6px"><div class="stat"><span>میانگین حاشیه سود</span><b>${pct(o.pm, 1)}</b></div><div class="stat"><span>ضریب نهایی قیمت</span><b>${FA(CM.finalCoef.toFixed(2))}×</b></div></div>
      <button class="btn" type="button" data-act="goto:menu">مشاهده‌ی منو</button>`;
    const sorted = CM.items.slice().sort((a, b) => b.mProfit - a.mProfit);
    const row = (m, bad) => `<button type="button" class="li" data-act="item:${m.code}"><span class="av" style="background:${bad ? "var(--rose-t)" : "var(--navy-t)"};color:${bad ? "var(--rose)" : "var(--k1)"}">${esc(m.code.slice(0, 3))}</span><span style="min-width:0"><b>${esc(m.name)}</b><span>${esc(m.cat)} · ${FA(m.daily)} در روز · واحد ${grp(m.profit)}</span></span><span class="amt" style="color:${m.mProfit < 0 ? "var(--bad)" : "var(--ok)"}">${T(m.mProfit)}</span></button>`;
    $("o-top").innerHTML = sorted.slice(0, 5).map(m => row(m, false)).join("");
    const losers = sorted.slice().reverse().filter(m => m.mProfit < 0).slice(0, 5);
    $("o-bot").innerHTML = losers.length ? losers.map(m => row(m, true)).join("") : '<p class="small muted">آیتم زیان‌دهی نیست.</p>';
    const fx = M.fxs.rows;
    const alt = [["سپرده بانکی " + pct(p.bank, 0), s.kpi.bank ? "رسیدن در " + monthTxt(s.kpi.bank) : "تا ۶۰ ماه نمی‌رسد", s.kpi.bank && s.kpi.bank <= 24 ? "p-ok" : s.kpi.bank ? "p-warn" : "p-bad", s.kpi.bank ? "قابل رقابت" : "عقب"],
      ["صندوق " + pct(p.fund, 0) + " ماهانه", s.kpi.fund ? "رسیدن در " + monthTxt(s.kpi.fund) : "تا ۶۰ ماه نمی‌رسد", s.kpi.fund ? "p-warn" : "p-bad", s.kpi.fund ? "دیر" : "عقب"]]
      .concat(fx.slice(1).map(f => [f.n + " (ماه " + FA(M.fxs.em) + ")", "تغییر " + pct(f.chg, 1), f.st[1], f.st[0]]));
    $("o-alt").innerHTML = alt.map(a => `<button type="button" class="li" data-act="goto:${a[0].startsWith("سپرده") || a[0].startsWith("صندوق") ? "bank" : "fx"}"><span class="av" style="background:var(--gold-t);color:var(--gold)">${ic(a[0].startsWith("سپرده") || a[0].startsWith("صندوق") ? "bank" : "fx")}</span><span><b>${a[0]}</b><span>${a[1]}</span></span><span class="pill ${a[2]}">${a[3]}</span></button>`).join("");
    let t = "<thead><tr><th>دسته</th><th>اقلام</th><th>فروش روزانه</th><th>درآمد خالص</th><th>سود خالص</th><th>فود کاست</th><th>حاشیه سود</th><th>وضعیت</th></tr></thead><tbody>";
    for (const b of M.cats){ const stt = b.pm < 0 ? ["زیان‌ده", "p-bad"] : b.pm < .1 ? ["ضعیف", "p-warn"] : ["خوب", "p-ok"];
      t += `<tr><td>${b.cat}</td><td class="n">${FA(b.n)}</td><td class="n">${FA(b.daily)}</td><td class="n">${T(b.rev)}</td><td class="n" style="color:${b.profit < 0 ? "var(--bad)" : "inherit"}">${T(b.profit)}</td><td class="n">${pct(b.fc, 0)}</td><td class="n">${pct(b.pm, 0)}</td><td><span class="pill ${stt[1]}">${stt[0]}</span>${b.loss ? ` <span class="small muted">${FA(b.loss)} زیان‌ده</span>` : ""}</td></tr>`; }
    $("o-cats").innerHTML = t + "</tbody>";
  }
};
function alerts(){
  const o = M.ops, c = M.cap, CM = M.CM, s = M.ser, A = [];
  if (o.profit < 0) A.push({s: "bad", t: "سود عملیاتی منفی است", d: `ماهانه ${T(o.profit)} با فروش و قیمت فعلی.`, go: "breakeven"});
  if (!isFinite(o.beDay) || o.daily < o.beDay) A.push({s: "bad", t: "فروش پیش‌بینی زیر نقطه‌ی سربه‌سر است", d: `${grpD(o.daily)} فروش در روز در برابر ${isFinite(o.beDay) ? FA(Math.ceil(o.beDay)) : "—"} لازم.`, go: "breakeven"});
  else if (o.mos < .2) A.push({s: "warn", t: "حاشیه‌ی ایمنی کم است", d: `فقط ${pct(o.mos, 0)} بالاتر از سربه‌سر.`, go: "breakeven"});
  const loss = CM.items.filter(m => m.status === "زیان‌ده");
  if (loss.length) A.push({s: "bad", t: `${FA(loss.length)} قلم زیر بهای تمام‌شده فروخته می‌شود`, d: loss.slice(0, 3).map(m => m.name).join("، ") + (loss.length > 3 ? " و …" : ""), go: "menu", btn: "اصلاح قیمت"});
  const hi = CM.items.filter(m => m.fc > m.lim);
  if (hi.length) A.push({s: "warn", t: `فود کاست ${FA(hi.length)} قلم بالاتر از سقف است`, d: "بهای مواد یا مقدار رسپی را بازبینی کنید.", go: "mats", btn: "مواد اولیه"});
  if (c.gap < 0) A.push({s: c.gap < -.15*c.adj ? "bad" : "warn", t: "کسری سرمایه‌ی راه‌اندازی", d: `${T(-c.gap)} کمتر از خواب سرمایه‌ی پیشنهادی.`, go: "capital"});
  if (!s.kpi.bank || s.kpi.bank > 24) A.push({s: "warn", t: "سود کافه دیرتر از سپرده‌ی بانکی می‌رسد", d: s.kpi.bank ? `رسیدن در ${monthTxt(s.kpi.bank)}.` : "در ۶۰ ماه نمی‌رسد.", go: "bank"});
  const missingRec = CM.items.filter(m2 => !m2.rec || !m2.rec.length);
  if (missingRec.length) A.push({s: "warn", t: `${FA(missingRec.length)} آیتم بدون رسپی`, d: "در بخش «دستور پخت» ادمین، رسپی هر آیتم را ثبت کنید.", go: "menu", btn: "منو"});
  const zeroPriced = CM.items.filter(m2 => m2.rec && m2.rec.length && m2.mat === 0);
  if (zeroPriced.length) A.push({s: "warn", t: `${FA(zeroPriced.length)} آیتم با ماده‌ی بی‌قیمت`, d: "بهای واحد مواد را در بخش «مواد اولیه» وارد کنید.", go: "mats", btn: "مواد اولیه"});
  A.push({s: "info", t: "نرخ دلار و طلا دستی است", d: "در بخش ورودی‌ها به‌روزرسانی کنید.", go: "inputs", btn: "نرخ‌ها"});
  if (!REAL) A.push({s: "info", t: "داده‌ی زنده‌ی سایت خوانده نشد", d: "اعداد نمونه نمایش داده می‌شود؛ صفحه را دوباره باز کنید.", go: null});
  if (dirty) A.unshift({s: "info", t: "تغییرات ذخیره‌نشده دارید", d: "فقط روی همین دستگاه نگه داشته شده.", go: null});
  const order = {bad: 0, warn: 1, info: 2}; return A.sort((a, b) => order[a.s] - order[b.s]);
}

V.inputs = {
  html(){
    const f = (lbl, path, kind, unit) => `<div class="fld"><label for="${fid(path)}">${lbl}</label><div class="ln">${inp(path, kind)}<span class="u">${unit || ""}</span></div></div>`;
    const card = (t, body, p) => `<div class="card"><div class="ch"><h3>${t}</h3>${p ? `<p>${p}</p>` : ""}</div><div class="fields">${body}</div></div>`;
    return card("نرخ‌های مرجع", `
      <div class="fld"><label for="${fid("rateMode")}">منبع نرخ</label>${sel("rateMode", [["manual", "ورود دستی"]])}</div>
      ${f("دلار آزاد", "params.usd", "num", "تومان")}${f("یورو", "params.eur", "num", "تومان")}${f("طلای ۱۸ عیار، هر گرم", "params.gold", "num", "تومان")}
      ${f("دلار مبنای برآورد هزینه‌ها", "params.usd_base", "num", "تومان")}${f("تاریخ نرخ دستی", "params.date", "text")}
      <div class="fld"><label>وضعیت نرخ</label><div class="calc" id="i-live"></div></div>`,
      "ویرایش هر نرخ، محاسبه‌ی کل مدل را به‌روز می‌کند.")
    + card("بازارهای جایگزین و رشد", f("سود سالانه‌ی سپرده بانکی", "params.bank", "pct", "٪") + f("بازده ماهانه‌ی صندوق", "params.fund", "pct", "٪") + f("رشد ماهانه‌ی سود اسمی", "params.growth", "pct", "٪") + f("ماه ارزیابی ارزی", "params.evalmonth", "dec", "ماه") + f("بازه‌ی سربه‌سر ارزی", "params.fxband", "pct", "٪"))
    + card("فروش، کسورات و مالیات", f("مالیات بر ارزش افزوده", "params.vat", "pct", "٪") + f("سهم فروش آنلاین", "params.online", "pct", "٪") + f("کمیسیون پلتفرم", "params.comm", "pct", "٪") + f("تخفیف و پروموشن", "params.disc", "pct", "٪") + f("کارمزد کارتخوان", "params.pos", "pct", "٪") + f("روز کاری در ماه", "params.days", "dec", "روز") + f("سود هدف ماهانه", "params.target", "num", "تومان"))
    + card("سیاست قیمت‌گذاری", f("حاشیه سود هدف پیش‌فرض", "params.margin", "pct", "٪") + f("گرد کردن قیمت به مضرب", "params.round", "num", "تومان") + f("سقف فود کاست نوشیدنی", "params.fc_drink", "pct", "٪") + f("سقف فود کاست خوراکی", "params.fc_food", "pct", "٪") + f("تورم ماهانه‌ی مواد", "params.infl", "pct", "٪") + f("ماه‌های ثبات قیمت منو", "params.buffer", "dec", "ماه") + f("هزینه‌ی نهان مواد", "params.hidden", "pct", "٪")
      + `<div class="fld"><label for="${fid("params.method")}">روش تسهیم سربار</label>${sel("params.method", [[1, "زمان آماده‌سازی"], [2, "ضریب بهای مواد"], [3, "مساوی هر فروش"]])}</div>
         <div class="fld"><label for="${fid("params.capcost")}">هزینه‌ی فرصت ودیعه در قیمت</label><div class="ln">${chk("params.capcost")}<span class="u">لحاظ شود</span></div></div>`)
    + card("شروع کار", f("فروش ماه اول", "params.ramp1", "pct", "٪ ظرفیت") + f("فروش ماه دوم", "params.ramp2", "pct", "٪") + f("فروش ماه سوم", "params.ramp3", "pct", "٪") + f("دوره‌ی سرشکن پیش‌راه‌اندازی", "params.amort", "dec", "ماه"));
  },
  out(){ $("i-live").textContent = "دلار " + grp(S.params.usd) + " · یورو " + grp(S.params.eur) + " · طلا " + grp(S.params.gold) + (S.params.date ? " (تاریخ نرخ: " + S.params.date + ")" : "") + " — ورود دستی"; }
};

V.costs = {
  html(){
    let st = `<thead><tr><th>سمت</th><th>نام</th><th>حقوق ماهانه</th><th>تعداد</th><th>بیمه ٪</th><th>ذخایر ٪</th><th>فعال</th><th>جمع ماهانه</th><th></th></tr></thead><tbody>`;
    S.staff.forEach((x, i) => { st += `<tr><td>${inp(`staff.${i}.role`, "text")}</td><td>${inp(`staff.${i}.name`, "text")}</td><td>${inp(`staff.${i}.sal`, "num")}</td><td>${inp(`staff.${i}.cnt`, "dec", "w-s")}</td><td>${inp(`staff.${i}.ins`, "pct", "w-s")}</td><td>${inp(`staff.${i}.acc`, "pct", "w-s")}</td><td>${chk(`staff.${i}.on`)}</td><td class="n" id="c-st-${i}"></td><td><button class="rm" type="button" data-act="rmstaff:${i}" aria-label="حذف">×</button></td></tr>`; });
    st += `<tr class="tot"><td colspan="7">جمع هزینه‌ی پرسنل فعال</td><td class="n" id="c-st-tot"></td><td></td></tr></tbody>`;
    let ln = `<thead><tr><th>گروه</th><th>شرح</th><th>مبلغ ماهانه</th><th>نوع</th><th>ضریب × مواد</th><th>سهم هر فروش</th><th>٪ درآمد</th></tr></thead><tbody>`;
    S.lines.forEach((l, i) => { ln += `<tr><td>${esc(l.g)}</td><td>${esc(l.d)}</td><td>${l.calc ? `<span class="calc" id="c-lv-${i}"></span>` : inp(`lines.${i}.v`, "num")}</td><td><span class="pill ${l.t === "عملیاتی" ? "p-info" : l.t === "غیرنقدی" ? "p-warn" : "p-bad"}">${esc(l.t)}</span></td><td class="n" id="c-lk-${i}"></td><td class="n" id="c-lu-${i}"></td><td class="n" id="c-lr-${i}"></td></tr>`; });
    ln += `<tr class="tot"><td colspan="2">جمع هزینه‌ی ثابت مبنای قیمت</td><td class="n" id="c-l-tot"></td><td></td><td class="n" id="c-l-k"></td><td class="n" id="c-l-u"></td><td class="n" id="c-l-r"></td></tr></tbody>`;
    return `<div class="card"><div class="ch"><h3>پرسنل و شرکا</h3><button class="btn" type="button" data-act="addstaff">${ic("plus")} افزودن نیرو</button></div>
      <p class="note">حقوق نیما و آرمین جدا از سهم سودشان است. برای سنجش اثر استخدام، تیک «فعال» را بزنید یا بردارید.</p>
      <div class="tbl"><table>${st}</table></div></div>
      <div class="card"><div class="ch"><h3>ضرایب قیمت‌گذاری</h3><p>قیمت منو = بهای مواد × ضریب نهایی</p></div><div class="statgrid" id="c-coef"></div></div>
      <div class="card"><div class="ch"><h3>هزینه‌های ثابت ماهانه</h3><p>خانه‌های خاکستری محاسبه‌ای‌اند: حقوق از جدول بالا، ودیعه از خواب سرمایه.</p></div><div class="tbl"><table>${ln}</table></div></div>`;
  },
  out(){
    const CM = M.CM, o = M.ops, p = S.params;
    S.staff.forEach((x, i) => { const el = $("c-st-" + i); if (el) el.textContent = x.on ? grp((+x.sal || 0)*(+x.cnt || 0)*(1 + (+x.ins || 0) + (+x.acc || 0))) : "—"; });
    $("c-st-tot").textContent = grp(CM.staffT);
    let tot = 0;
    CM.lines.forEach((l, i) => { const inc = !(l.t === "هزینه سرمایه" && !p.capcost); const v = inc ? l.v : 0; tot += v;
      const lv = $("c-lv-" + i); if (lv) lv.textContent = grp(l.v) + (inc ? "" : " (لحاظ نمی‌شود)");
      $("c-lk-" + i).textContent = CM.matM ? FA((v/CM.matM).toFixed(3)) : "—"; $("c-lu-" + i).textContent = CM.units ? grp(v/CM.units) : "—"; $("c-lr-" + i).textContent = pct(o.rev ? v/o.rev : 0); });
    $("c-l-tot").textContent = grp(tot); $("c-l-k").textContent = FA(CM.K.toFixed(3)); $("c-l-u").textContent = grp(CM.perUnit); $("c-l-r").textContent = pct(o.rev ? tot/o.rev : 0);
    $("c-coef").innerHTML = [["هزینه‌ی پرسنل", T(CM.staffT)], ["هزینه‌ی ثابت حسابداری", T(CM.acc)], ["هزینه‌ی ثابت مبنای قیمت", T(CM.priceBase)], ["ضریب بافر تورم", FA(CM.bufR.toFixed(3))], ["ضریب هزینه‌ی نهان", FA((+p.hidden).toFixed(3))], ["ضریب سربار ثابت", FA(CM.K.toFixed(3))], ["ضریب بهای تمام‌شده", FA(CM.costCoef.toFixed(3))], ["کسورات از قیمت", pct(CM.ded)], ["ضریب نهایی قیمت منو", `<span style="color:var(--rose)">${FA(CM.finalCoef.toFixed(3))}×</span>`], ["نمونه: مواد ۱۰۰ هزار تومانی", grp(100000*CM.finalCoef)]]
      .map(([k, v]) => `<div class="stat"><span>${k}</span><b>${v}</b></div>`).join("");
  }
};

V.capital = {
  html(){
    const cur = [["ریال", "ریال"], ["تومان", "تومان"], ["دلار", "دلار"], ["یورو", "یورو"], ["طلا", "طلا (گرم)"]];
    let t = `<thead><tr><th>تاریخ</th><th>شریک</th><th>شرح</th><th>ارز</th><th>مبلغ / مقدار</th><th>دلار روز واریز</th><th>یورو روز واریز</th><th>طلا روز واریز</th><th>معادل تومان</th><th>امروز اگر دلار</th><th>امروز اگر یورو</th><th>امروز اگر طلا</th><th></th></tr></thead><tbody>`;
    S.inj.forEach((x, i) => { t += `<tr><td>${inp(`inj.${i}.date`, "text", "w-s")}</td><td>${inp(`inj.${i}.who`, "text", "w-s")}</td><td>${inp(`inj.${i}.desc`, "text")}</td><td>${sel(`inj.${i}.cur`, cur)}</td><td>${inp(`inj.${i}.amt`, "num")}</td><td>${inp(`inj.${i}.usd`, "num")}</td><td>${inp(`inj.${i}.eur`, "num")}</td><td>${inp(`inj.${i}.gold`, "num")}</td><td class="n" id="k-t-${i}"></td><td class="n" id="k-u-${i}"></td><td class="n" id="k-e-${i}"></td><td class="n" id="k-g-${i}"></td><td><button class="rm" type="button" data-act="rminj:${i}" aria-label="حذف">×</button></td></tr>`; });
    t += `<tr class="tot"><td colspan="8" id="k-who"></td><td class="n" id="k-tt"></td><td class="n" id="k-tu"></td><td class="n" id="k-te"></td><td class="n" id="k-tg"></td><td></td></tr></tbody>`;
    let sg = "";
    S.sug.forEach((s_, i) => { sg += `<div class="fld"><label for="${fid(`sug.${i}.v`)}">${esc(s_.label)}</label>${s_.calc ? `<div class="calc" id="k-s-${i}"></div>` : inp(`sug.${i}.v`, "num")}</div>`; });
    return `<div class="card"><div class="ch"><h3>سرمایه‌های تزریق‌شده</h3><button class="btn" type="button" data-act="addinj">${ic("plus")} افزودن واریز</button></div>
      <p class="note">ارز «ریال» به تومان تبدیل می‌شود (÷۱۰). برای دلار، یورو و طلا مقدار و نرخ همان روز را وارد کنید.</p>
      <div class="tbl"><table>${t}</table></div></div>
      <div class="row r-2"><div class="card"><div class="ch"><h3>خواب سرمایه‌ی پیشنهادی</h3><p>موجودی انبار و سرمایه‌ی در گردش محاسبه‌ای‌اند.</p></div><div class="fields">${sg}</div><div class="statgrid" id="k-sum"></div></div>
      <div class="card"><div class="ch"><h3>سرمایه‌ی واقعی در برابر پیشنهادی</h3></div><div id="k-bars"></div></div></div>`;
  },
  out(){
    const c = M.cap, r = M.r;
    c.rows.forEach((x, i) => { $("k-t-" + i).textContent = grp(x.t); $("k-u-" + i).textContent = grp(x.tu); $("k-e-" + i).textContent = grp(x.te); $("k-g-" + i).textContent = grp(x.tg); });
    $("k-who").textContent = "جمع · " + Object.entries(c.byWho).map(([k, v]) => `${k} ${pct(c.tot.t ? v/c.tot.t : 0, 0)}`).join(" · ");
    $("k-tt").textContent = grp(c.tot.t); $("k-tu").textContent = grp(c.tot.tu); $("k-te").textContent = grp(c.tot.te); $("k-tg").textContent = grp(c.tot.tg);
    c.sug.forEach((s_, i) => { const el = $("k-s-" + i); if (el) el.textContent = grp(s_.v) + " تومان"; });
    $("k-sum").innerHTML = [["جمع اقلام", T(c.sub)], ["ذخیره‌ی احتیاط ۱۰٪", T(c.cont)], ["پیشنهادی به نرخ مبنا", T(c.sugTot)], ["پیشنهادی به نرخ روز دلار", `<span style="color:var(--rose)">${T(c.adj)}</span>`], ["سهم وابسته به دلار", pct(c.fxShare, 0)], [c.gap < 0 ? "کسری" : "مازاد", `<span style="color:${c.gap < 0 ? "var(--bad)" : "var(--ok)"}">${T(Math.abs(c.gap))}</span>`], ["معادل دلار", grp(c.adj/r.usd) + " $"], ["معادل طلا", grp(c.adj/r.gold) + " گرم"]]
      .map(([k, v]) => `<div class="stat"><span>${k}</span><b>${v}</b></div>`).join("");
    const mx = Math.max(c.tot.t, c.adj, c.tot.tu, c.tot.te, c.tot.tg) || 1;
    const bar = (l, v, col) => `<div style="display:flex;flex-direction:column;gap:4px"><div style="display:flex;justify-content:space-between;font-size:.85rem"><span>${l}</span><b class="num">${T(v)}</b></div><div style="height:12px;background:var(--navy-t2);border-radius:6px;overflow:hidden"><div style="height:100%;width:${100*v/mx}%;background:${col};border-radius:6px"></div></div></div>`;
    $("k-bars").innerHTML = `<div style="display:flex;flex-direction:column;gap:12px">${bar("سرمایه‌ی تزریق‌شده (اسمی)", c.tot.t, "var(--k2)")}${bar("خواب سرمایه‌ی پیشنهادی به نرخ روز", c.adj, "var(--k5)")}${bar("اگر دلار نگه داشته می‌شد", c.tot.tu, "var(--k3)")}${bar("اگر یورو نگه داشته می‌شد", c.tot.te, "var(--k1)")}${bar("اگر طلا نگه داشته می‌شد", c.tot.tg, "var(--k4)")}</div>`;
  }
};

const STC = {"مطلوب": "p-ok", "زیر هدف": "p-warn", "فود کاست بالا": "p-bad", "زیان‌ده": "p-bad"};
V.menu = {
  html(){
    const i = Math.max(0, S.menu.findIndex(m => m.code === UI.sel)); UI.sel = S.menu[i].code; const m = S.menu[i];
    const cats = ["همه"].concat(menuCats(), ["زیان‌ده", "فود کاست بالا"]);
    let rec = `<thead><tr><th>ماده</th><th>مقدار</th><th>واحد</th><th>بهای واحد</th><th>بهای سطر</th></tr></thead><tbody>`;
    m.rec.forEach((r_, j) => { const mt = S.mats.find(x => (x.rid || x.code) === r_[0]) || {name: r_[0], base: ""};
      rec += `<tr><td>${esc(mt.name)}</td><td>${inp(`menu.${i}.rec.${j}.1`, "dec", "w-s")}</td><td>${esc(mt.base)}</td><td class="n" id="m-ru-${j}"></td><td class="n" id="m-rc-${j}"></td></tr>`; });
    rec += `<tr class="tot"><td colspan="4">بهای مواد مستقیم</td><td class="n" id="m-rt"></td></tr></tbody>`;
    return `<div class="ctrl"><span class="small">قیمت مبنای محاسبه:</span><div class="seg" role="group"><button type="button" data-act="pmode:menu" class="${S.pmode === "menu" ? "on" : ""}">قیمت منو</button><button type="button" data-act="pmode:model" class="${S.pmode === "model" ? "on" : ""}">پیشنهاد مدل</button></div><span class="small muted">پیشنهاد مدل = بهای تمام‌شده ÷ (۱ − حاشیه هدف − کسورات) + مالیات</span></div>
    <section class="mpanel" aria-label="کاست کنترل آیتم‌ها">
      <div><h2>آیتم‌های منو</h2><div class="filters">${cats.map(c => `<button type="button" class="${c === UI.cat ? "on" : ""}" data-act="cat:${c}">${c}</button>`).join("")}</div><div class="mlist" id="m-list"></div></div>
      <div class="detail">
        <div class="ch"><div><span class="small muted">${esc(m.code)} · ${esc(m.cat)}${m.line !== "—" ? " · لاین " + esc(m.line) : ""} · رسپی ثبت‌شده در ادمین${m.est ? " · قیمت برآوردی" : ""}</span><h2>${esc(m.name)}</h2></div><span id="m-st"></span></div>
        <div class="fields">
          <div class="fld"><label for="${fid(`menu.${i}.price`)}">قیمت منو (تومان)</label>${inp(`menu.${i}.price`, "num")}</div>
          <div class="fld"><label for="${fid(`menu.${i}.daily`)}">فروش روزانه</label>${inp(`menu.${i}.daily`, "dec")}</div>
          <div class="fld"><label for="${fid(`menu.${i}.tm`)}">حاشیه سود هدف ٪</label>${inp(`menu.${i}.tm`, "pct")}</div>
          <div class="fld"><label for="${fid(`menu.${i}.w`)}">ضریب زمان آماده‌سازی</label>${inp(`menu.${i}.w`, "dec")}</div>
        </div>
        <div class="dgrid" id="m-boxes"></div>
        <div><h3 style="margin-bottom:8px">قیمت از کجا می‌آید</h3><div class="stack" id="m-stack"></div><div class="legend" id="m-leg" style="margin-top:8px"></div></div>
        <div class="row r-2"><div class="wf" id="m-wf"></div><div class="tbl"><table>${rec}</table></div></div>
      </div>
    </section>
    <div class="card"><div class="ch"><h3>جدول کامل منو</h3><p>قیمت و فروش روزانه را مستقیم ویرایش کنید.</p></div><div class="tbl" style="max-height:620px"><table id="m-table"></table></div></div>`;
  },
  out(){
    const CM = M.CM, items = CM.items, m = items.find(x => x.code === UI.sel) || items[0];
    const vis = items.filter(x => UI.cat === "همه" || x.cat === UI.cat || x.status === UI.cat);
    $("m-list").innerHTML = vis.length ? vis.map(x => `<button type="button" class="mi ${x.code === UI.sel ? "on" : ""}" data-act="item:${x.code}"><span><span class="nm">${esc(x.name)}</span><br><span class="meta">${esc(x.cat)} · فود کاست ${pct(x.fc, 0)}</span></span><span style="text-align:left"><span class="pr">${grp(x.price)}</span><br><span class="pill ${STC[x.status]}">${x.status}</span></span></button>`).join("") : '<p class="small muted">آیتمی در این دسته نیست.</p>';
    $("m-st").innerHTML = `<span class="pill ${STC[m.status]}">${m.status}</span>`;
    const diff = m.sug - (+S.menu.find(x => x.code === m.code).price || 0);
    $("m-boxes").innerHTML = [["بهای تمام‌شده", grp(m.full)], ["پیشنهاد مدل", grp(m.sug)], ["پیشنهاد − قیمت منو", `<span style="color:${diff > 0 ? "var(--bad)" : "var(--ok)"}">${diff > 0 ? "+" : ""}${grp(diff)}</span>`], ["سود خالص واحد", `<span style="color:${m.profit < 0 ? "var(--bad)" : "inherit"}">${grp(m.profit)}</span>`], ["فود کاست / سقف", pct(m.fc, 0) + " / " + pct(m.lim, 0)], ["سهم قهوه از قیمت", pct(m.cofShare, 0)]]
      .map(([k, v]) => `<div class="dbox"><span>${k}</span><b>${v}</b></div>`).join("");
    const ded = m.net - m.netrev, vat = m.price - m.net;
    const parts = [["مواد مستقیم", m.mat, "var(--k3)"], ["تورم و هزینه‌ی نهان", m.buf + m.hid, "var(--k4)"], ["سربار ثابت", m.ovh, "var(--k2)"], ["سود خالص", Math.max(0, m.profit), "var(--k1)"], ["کمیسیون و تخفیف", ded, "var(--k6)"], ["ارزش افزوده", vat, "var(--k5)"]];
    const sum = parts.reduce((a, b) => a + Math.max(b[1], 0), 0) || 1;
    $("m-stack").innerHTML = parts.map(p => `<div title="${p[0]}" style="width:${100*Math.max(p[1], 0)/sum}%;background:${p[2]}"></div>`).join("");
    $("m-leg").innerHTML = parts.map(p => `<span><i style="background:${p[2]}"></i>${p[0]}</span>`).join("");
    $("m-wf").innerHTML = `<div><span>مواد مستقیم</span><b>${grp(m.mat)}</b></div><div><span>+ بافر تورم</span><b>${grp(m.buf)}</b></div><div><span>+ هزینه‌ی نهان</span><b>${grp(m.hid)}</b></div><div><span>+ سربار ثابت</span><b>${grp(m.ovh)}</b></div><div class="t"><span>بهای تمام‌شده</span><b>${grp(m.full)}</b></div><div><span>+ سود خالص</span><b>${grp(m.profit)}</b></div><div><span>+ کمیسیون و تخفیف</span><b>${grp(ded)}</b></div><div><span>+ ارزش افزوده</span><b>${grp(vat)}</b></div><div class="t"><span>قیمت</span><b>${grp(m.price)}</b></div>`;
    const raw = S.menu.find(x => x.code === m.code);
    raw.rec.forEach((r_, j) => { const mt = CM.mm[r_[0]]; const u = $("m-ru-" + j), c = $("m-rc-" + j); if (u) u.textContent = mt ? grp(mt.eff, mt.eff < 100 ? 1 : 0) : "—"; if (c) c.textContent = mt ? grp(mt.eff*r_[1]) : "—"; });
    $("m-rt").textContent = grp(m.mat);
    const tb = $("m-table");
    if (!tb.dataset.built){ tb.dataset.built = "1";
      let t = "<thead><tr><th>کد</th><th>آیتم</th><th>دسته</th><th>قیمت منو</th><th>فروش روزانه</th><th>بهای مواد</th><th>بهای تمام‌شده</th><th>پیشنهاد مدل</th><th>فود کاست</th><th>حاشیه سود</th><th>سود ماهانه</th><th>وضعیت</th></tr></thead><tbody>";
      S.menu.forEach((x, i) => { t += `<tr id="mt-${i}"><td>${esc(x.code)}</td><td>${esc(x.name)}</td><td>${esc(x.cat)}</td><td>${inp(`menu.${i}.price`, "num", "", x.name, "-t")}</td><td>${inp(`menu.${i}.daily`, "dec", "w-s", x.name, "-t")}</td><td class="n" id="mt-m-${i}"></td><td class="n" id="mt-f-${i}"></td><td class="n" id="mt-s-${i}"></td><td class="n" id="mt-c-${i}"></td><td class="n" id="mt-p-${i}"></td><td class="n" id="mt-r-${i}"></td><td id="mt-x-${i}"></td></tr>`; });
      tb.innerHTML = t + "</tbody>"; }
    items.forEach((x, i) => { $("mt-m-" + i).textContent = grp(x.mat); $("mt-f-" + i).textContent = grp(x.full); $("mt-s-" + i).textContent = grp(x.sug); $("mt-c-" + i).textContent = pct(x.fc, 0);
      const pe = $("mt-p-" + i); pe.textContent = pct(x.pm, 0); pe.style.color = x.pm < 0 ? "var(--bad)" : ""; $("mt-r-" + i).textContent = T(x.mProfit); $("mt-x-" + i).innerHTML = `<span class="pill ${STC[x.status]}">${x.status}</span>`;
      $("mt-" + i).className = x.code === UI.sel ? "sel" : "";
      });
  }
};

V.mats = {
  html(){
    let t = `<thead><tr><th>کد</th><th>ماده</th><th>دسته</th><th>واحد خرید</th><th>مقدار بسته</th><th>واحد پایه</th><th>قیمت بسته (تومان)</th><th>ضایعات ٪</th><th>قیمت مؤثر واحد</th><th>استفاده در</th></tr></thead><tbody>`;
    S.mats.forEach((x, i) => { t += `<tr><td>${esc(x.code)}</td><td>${inp(`mats.${i}.name`, "text")}</td><td>${esc(x.cat)}</td><td>${esc(x.unit)}</td><td>${inp(`mats.${i}.pack`, "dec", "w-s")}</td><td>${esc(x.base)}</td><td>${inp(`mats.${i}.price`, "num")}</td><td>${inp(`mats.${i}.waste`, "pct", "w-s")}</td><td class="n" id="mx-e-${i}"></td><td class="n" id="mx-u-${i}"></td></tr>`; });
    return `<div class="card"><div class="ch"><h3>مواد اولیه و بسته‌بندی</h3><p>قیمت مؤثر = قیمت بسته ÷ مقدار ÷ (۱ − ضایعات). هر تغییر، بهای همه‌ی آیتم‌ها را به‌روز می‌کند.</p></div><div class="tbl" style="max-height:680px"><table>${t}</table></div></div>`;
  },
  out(){ const used = {}; S.menu.forEach(m => m.rec.forEach(([c]) => used[c] = (used[c] || 0) + 1));
    S.mats.forEach((x, i) => { const e = M.CM.mm[x.rid || x.code].eff; $("mx-e-" + i).textContent = grp(e, e < 100 ? 1 : 0); $("mx-u-" + i).textContent = FA(used[x.rid || x.code] || 0) + " قلم"; }); }
};

V.breakeven = {
  html: () => `<div class="card"><div class="ctrl">
      <label for="vol">حجم فروش نسبت به پیش‌بینی <input id="vol" type="range" min="20" max="250" step="5" value="${Math.round(S.vol*100)}"> <b class="num" id="b-vol"></b></label>
      <label for="${fid("params.target")}">سود هدف ماهانه ${inp("params.target", "num")}</label></div>
    <div class="statgrid" id="b-stats"></div>
    <svg class="chart" id="b-chart" viewBox="0 0 720 260" role="img" aria-label="سود ماهانه بر اساس فروش روزانه"></svg>
    <div class="legend"><span><i style="background:var(--k1)"></i>سود عملیاتی ماهانه</span><span><i style="background:var(--k5)"></i>سود هدف</span></div></div>
    <div class="card"><div class="ch"><h3>سناریوهای حجم فروش</h3></div><div class="tbl"><table id="b-scn"></table></div></div>`,
  out(){
    const o = M.o, p = S.params;
    $("b-vol").textContent = pct(S.vol, 0) + " · " + grpD(o.daily) + " فروش در روز";
    const D = v => isFinite(v) ? FA(Math.ceil(v)) : "—";
    $("b-stats").innerHTML = [["فروش روزانه", grpD(o.daily)], ["درآمد خالص ماهانه", T(o.rev)], ["هزینه‌ی ثابت ماهانه", T(o.fix)], ["سود عملیاتی ماهانه", `<span style="color:${o.profit >= 0 ? "var(--ok)" : "var(--bad)"}">${T(o.profit)}</span>`],
      ["سربه‌سر (فروش در روز)", D(o.beDay)], ["سربه‌سر با هزینه‌ی فرصت", D(o.econDay)], ["سوددهی هدف (فروش در روز)", D(o.tgtDay)], ["نسبت حاشیه‌ی مشارکت", pct(o.cmr)]].map(([k, v]) => `<div class="stat"><span>${k}</span><b>${v}</b></div>`).join("");
    const base = M.ops, xs = [], prof = [], maxD = Math.max(base.daily*2.2, isFinite(o.tgtDay) ? o.tgtDay*1.2 : 0, 10);
    for (let i = 0; i <= 20; i++){ const d = maxD*i/20; xs.push(Math.round(d)); prof.push(ops(base.daily ? d/base.daily : 0).profit); }
    const idx = d => 20*d/maxD;
    lineChart($("b-chart"), xs, [{v: prof, c: "var(--k1)", area: true}, {v: xs.map(() => p.target), c: "var(--k5)", dash: true, w: 1.6}],
      {xStep: 4, marks: [{i: idx(o.beDay), c: "var(--bad)", t: "سربه‌سر"}, {i: idx(o.tgtDay), c: "var(--rose)", t: "هدف"}, {i: idx(o.daily), c: "var(--k2)", t: "شما"}]});
    let t = "<thead><tr><th>سناریو</th><th>ضریب</th><th>فروش روزانه</th><th>درآمد خالص</th><th>سود عملیاتی</th><th>وضعیت</th></tr></thead><tbody>";
    for (const [n, k] of [["بسیار بدبینانه", .5], ["بدبینانه", .75], ["محتمل", 1], ["خوش‌بینانه", 1.25], ["بسیار خوش‌بینانه", 1.5]]){ const q = ops(k);
      const st = Math.abs(q.profit) <= .02*q.rev ? ["سربه‌سر", "p-warn"] : q.profit > 0 ? ["سودده", "p-ok"] : ["ضررده", "p-bad"];
      t += `<tr><td>${n}</td><td class="n">${pct(k, 0)}</td><td class="n">${grpD(q.daily)}</td><td class="n">${T(q.rev)}</td><td class="n">${T(q.profit)}</td><td><span class="pill ${st[1]}">${st[0]}</span></td></tr>`; }
    $("b-scn").innerHTML = t + "</tbody>";
  }
};

V.bank = {
  html: () => `<div class="card"><div class="ch"><h3>سود تجمعی: کافه، بانک، صندوق</h3><p id="k-p"></p></div><div class="statgrid" id="k-stats"></div>
    <svg class="chart" id="k-chart" viewBox="0 0 720 280" role="img" aria-label="سود تجمعی ۴۸ ماه"></svg>
    <div class="legend"><span><i style="background:var(--k1)"></i>کافه</span><span><i style="background:var(--k4)"></i>سپرده بانکی</span><span><i style="background:var(--k5)"></i>صندوق</span></div></div>`,
  out(){ const s = M.ser, c = M.cap, p = S.params, k = s.kpi, at = m => s.out[m - 1];
    $("k-p").textContent = `سرمایه ${T(c.tot.t)} · بانک ${pct(p.bank, 0)} سالانه (ساده) · صندوق ${pct(p.fund, 0)} ماهانه (مرکب)`;
    $("k-stats").innerHTML = [["اولین ماه سودده", monthTxt(k.profitM)], ["جبران زیان شروع", monthTxt(k.recover)], ["رسیدن به بانک", monthTxt(k.bank)], ["رسیدن به صندوق", monthTxt(k.fund)], ["بازگشت کامل سرمایه‌ی نقدی", monthTxt(k.payback)], ["ماه ۱۲: کافه − بانک", T(at(12).cafe - at(12).bank)], ["ماه ۳۶: کافه − صندوق", T(at(36).cafe - at(36).fund)]]
      .map(([a, b]) => `<div class="stat"><span>${a}</span><b>${b}</b></div>`).join("");
    const o = s.out.slice(0, 48);
    lineChart($("k-chart"), o.map(r => r.m), [{v: o.map(r => r.fund), c: "var(--k5)"}, {v: o.map(r => r.bank), c: "var(--k4)"}, {v: o.map(r => r.cafe), c: "var(--k1)", area: true, w: 3}], {xStep: 6}); }
};

V.fx = {
  html: () => `<div class="card"><div class="ch"><h3>وضعیت سرمایه در ماه ارزیابی</h3><p id="x-p"></p></div><div class="fx" id="x-cards"></div></div>
    <div class="card"><div class="ch"><h3>اگر نرخ‌ها تغییر کنند</h3></div><div class="tbl"><table id="x-table"></table></div></div>`,
  out(){ const F = M.fxs;
    $("x-p").textContent = `دارایی در ماه ${FA(F.em)} = سرمایه‌ی اسمی ${T(M.cap.tot.t)} + سود تجمعی ${T(F.cum)} = ${T(F.W)}`;
    $("x-cards").innerHTML = F.rows.map(f => `<div class="fxc"><div class="ch"><h3>${f.n}</h3><span class="pill ${f.st[1]}">${f.st[0]}</span></div><div class="big">${pct(f.chg)}</div><div class="small muted">اولیه ${grp(f.init, f.d)} ${f.u} · امروز ${grp(f.now, f.d)} ${f.u}</div>${f.n !== "تومان" ? `<div class="small">نرخ سربه‌سر: <b class="num">${grp(f.be)}</b> (امروز ${grp(f.rate)})</div>` : ""}</div>`).join("");
    let h = "<thead><tr><th>تغییر نرخ</th><th>دلار</th><th>وضعیت</th><th>یورو</th><th>وضعیت</th><th>طلای ۱۸</th><th>وضعیت</th></tr></thead><tbody>";
    for (const d of [-.2, -.1, 0, .1, .2, .3, .5, .75, 1]){ h += `<tr${d === 0 ? ' class="sel"' : ""}><td class="n">${d > 0 ? "+" : ""}${pct(d, 0)}</td>`;
      for (const f of F.rows.slice(1)){ const r = f.rate*(1 + d), st = fxStatus(f.init ? F.W/r/f.init - 1 : 0); h += `<td class="n">${grp(r)}</td><td><span class="pill ${st[1]}">${st[0]}</span></td>`; }
      h += "</tr>"; }
    $("x-table").innerHTML = h + "</tbody>"; }
};

/* ---------- chrome ---------- */
function renderNav(){
  const b = (v, mobile) => `<button type="button" data-act="goto:${v[0]}" class="${UI.view === v[0] ? "on" : ""}"${UI.view === v[0] ? ' aria-current="page"' : ""}>${mobile ? "" : ic(v[0])}<span>${v[1]}</span></button>`;
  $("nav").innerHTML = VIEWS.map(v => b(v, false)).join(""); $("mnav").innerHTML = VIEWS.map(v => b(v, true)).join("");
  const on = $("mnav").querySelector(".on"); if (on && on.scrollIntoView) on.scrollIntoView({block: "nearest", inline: "center"});
}
function chrome(){
  const r = M.r;
  const st = UI.readOnly ? '<span class="tag">فقط‌خواندنی</span>' : UI.saving ? '<span class="tag">در حال ذخیره…</span>' : dirty ? '<span class="tag dirty">ذخیره‌نشده</span>' : '<span class="tag live">ذخیره‌شده</span>';
  $("rates").innerHTML = `<span class="chip">دلار <b>${grp(r.usd)}</b></span><span class="chip opt">یورو <b>${grp(r.eur)}</b></span><span class="chip opt">طلا <b>${grp(r.gold)}</b></span><span class="tag">${esc(r.src)}</span>${st}`
    + (UI.readOnly ? "" : `<button class="btn primary" type="button" data-act="save" ${dirty && !UI.saving ? "" : "disabled"}>${ic("save")} <span>ذخیره</span></button>`)
    + (UI.readOnly || !dirty ? "" : `<button class="btn ghost" type="button" data-act="reset" title="بازگشت به آخرین ذخیره‌ی سرور">بازگردانی</button>`);
  $("save-msg").textContent = dirty ? "تغییرات فعلاً فقط در همین صفحه اعمال شده‌اند؛ با «ذخیره برای همه» ثبت می‌شود." : (S.savedAt ? "آخرین ذخیره: " + FA(new Date(S.savedAt).toLocaleString("fa-IR")) : "عددها از منو، انبار و فروش واقعی سایت خوانده شده است.");
  const sb = $("save-side"); sb.disabled = !dirty || UI.saving || UI.readOnly; sb.hidden = UI.readOnly;
  $("reset-btn").textContent = UI.resetArm ? "مطمئنید؟ دوباره بزنید" : "بازگشت به آخرین ذخیره"; $("reset-btn").hidden = UI.readOnly;
}
function outputs(){ chrome(); const v = V[UI.view]; if (v && v.out) v.out(); }
function showView(id, keepScroll){
  if (!V[id]) id = "overview"; UI.view = id; const meta = VIEWS.find(v => v[0] === id);
  $("vtitle").textContent = meta[1]; $("vsub").textContent = meta[2];
  $("view").innerHTML = V[id].html(); renderNav(); outputs();
  if (!keepScroll) window.scrollTo({top: 0});
  try { history.replaceState(null, "", "#" + id); } catch (_) {}
}
let toastT = 0;
function toast(msg){ let t = document.querySelector(".toast"); if (!t){ t = document.createElement("div"); t.className = "toast"; t.setAttribute("role", "status"); document.body.appendChild(t); }
  t.textContent = msg; t.hidden = false; clearTimeout(toastT); toastT = setTimeout(() => t.hidden = true, 3200); }

document.addEventListener("click", e => {
  const b = e.target.closest("[data-act]"); if (!b) return; const [a, arg] = b.dataset.act.split(/:(.*)/s);
  if (a === "goto") showView(arg);
  else if (a === "item"){ UI.sel = arg; if (UI.view !== "menu") showView("menu"); else showView("menu", true); }
  else if (a === "cat"){ UI.cat = arg; showView("menu", true); }
  else if (a === "pmode"){ S.pmode = arg; changed(true); }
  else if (a === "save") save();
  else if (a === "reset"){ if (!UI.resetArm){ UI.resetArm = 1; chrome(); setTimeout(() => { UI.resetArm = 0; chrome(); }, 4000); return; }
    UI.resetArm = 0; refreshBundle(false); toast("به آخرین ذخیره برگشت."); }
  else if (UI.readOnly) return;
  else if (a === "addstaff"){ S.staff.push({role: "نیروی جدید", name: "—", sal: 25000000, cnt: 1, ins: .23, acc: .2, on: 1}); changed(true); }
  else if (a === "rmstaff"){ S.staff.splice(+arg, 1); changed(true); }
  else if (a === "addinj"){ const r = M.r; S.inj.push({date: "", who: "نیما", desc: "", cur: "ریال", amt: 0, usd: r.usd, eur: r.eur, gold: r.gold}); changed(true); }
  else if (a === "rminj"){ S.inj.splice(+arg, 1); changed(true); }
});
document.addEventListener("input", e => { if (e.target.id === "vol"){ S.vol = e.target.value/100; dirty = true; compute(); outputs(); } });

/* ---------- saving: persist the model state to the Sugarista API ---------- */
async function save(){
  if (UI.saving || UI.readOnly) return;
  UI.saving = true; chrome();
  const next = Object.assign({}, S, {savedAt: Date.now()});
  try {
    const res = await fetch("/api/admin/cost-dashboard/state", {method: "PUT", headers: {"Content-Type": "application/json", Authorization: "Bearer " + TOKEN}, body: JSON.stringify({state: JSON.stringify(next)})});
    if (res.ok){ const done = await res.json().catch(() => null); dirty = false; S.savedAt = done && done.savedAt ? done.savedAt : next.savedAt; toast("ذخیره شد."); }
    else if (res.status === 401){ toast("نشست ادمین منقضی شده؛ صفحه را دوباره باز کنید."); }
    else if (res.status === 403){ toast("دسترسی ذخیره‌سازی را ندارید؛ تغییرات فقط در همین صفحه می‌ماند."); }
    else { const err = await res.json().catch(() => null); toast("ذخیره انجام نشد" + (err && err.detail ? ": " + err.detail : (res.status ? " (کد " + res.status + ")" : "")) + "."); }
  } catch (e){ toast("ذخیره انجام نشد: " + (e && e.message ? e.message : "خطای شبکه")); }
  UI.saving = false; chrome();
}

/* ---------- real data from the Sugarista admin ---------- */
let TOKEN = null;
async function refreshBundle(user){
  if (!TOKEN){ UI.liveMsg = "در انتظار دسترسی‌های صفحه‌ی ادمین…"; outputs(); return; }
  UI.liveMsg = "در حال خواندن داده‌ی زنده‌ی سایت…"; outputs();
  try {
    const res = await fetch("/api/admin/cost-dashboard/bundle", {headers: {Authorization: "Bearer " + TOKEN}});
    if (res.status === 401){ UI.liveMsg = "نشست منقضی شده؛ صفحه‌ی ادمین را دوباره باز کنید."; outputs(); if (user) toast(UI.liveMsg); return; }
    if (!res.ok) throw new Error("HTTP " + res.status);
    const data = await res.json();
    ingestReal(data);
    let saved = null;
    if (data.savedState){ try { saved = JSON.parse(data.savedState); } catch (_) {} }
    S = saved && saved.v === 3 ? applySavedState(saved) : defaults();
    dirty = false;
    UI.liveMsg = "";
    if (S.menu[0]) UI.sel = S.menu[0].code;
    compute();
    showView(UI.view, true);
    if (user) toast("داده‌ی زنده‌ی سایت به‌روزرسانی شد.");
  } catch (_){ UI.liveMsg = "خواندن داده‌ی زنده‌ی سایت ناموفق بود؛ اعداد نمونه نمایش داده می‌شود."; outputs();
    if (user) toast(UI.liveMsg); }
}
window.addEventListener("message", e => { const d = e && e.data;
  if (d && d.type === "costdash:init" && d.token){ TOKEN = d.token; refreshBundle(true); } });

/* ---------- boot ---------- */
const sh = document.getElementById("shell").innerHTML.replace(/\{\{ICON:(\w+)\}\}/g, (_, k) => ic(k));
const mount = document.createElement("div"); mount.id = "mount"; mount.innerHTML = sh; document.getElementById("shell").after(mount);
UI.sel = S.menu[0].code;
compute();
const h0 = (location.hash || "").replace("#", "");
showView(VIEWS.some(v => v[0] === h0) ? h0 : "overview", true);
(async () => { try { if (window.parent && window.parent !== window) window.parent.postMessage({type: "costdash:ready"}, location.origin); } catch (_) {} })();
})();
