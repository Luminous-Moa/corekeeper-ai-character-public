// node run.js -> en-pre.json, missing.json ; translates the 100 cases (../../100-ko/cases.json) with tr2 + dict-base + dict-add (10k) + dict-add100
const fs = require('fs'), path = require('path');
const rd = f => JSON.parse(fs.readFileSync(path.join(__dirname, f), 'utf8'));
const opt = f => fs.existsSync(path.join(__dirname, f)) ? rd(f) : {};
fs.writeFileSync(path.join(__dirname, 'dict.json'), JSON.stringify({ ...rd('dict-base.json'), ...opt('dict-add.json'), ...opt('dict-add100.json') }));
const tr = require('./tr2');
const C = JSON.parse(fs.readFileSync(path.join(__dirname, '..', '..', '100-ko', 'cases.json'), 'utf8'));
const out = []; let hk = 0, he = 0;
for (const c of C) {
  const x = { id: c.id, input: c.text, opts: c.cands.map(o => ({ value: o.value, desc: o.description })) };
  const e = tr.translateRequest(x);
  out.push({ id: c.id, input: e.input, choices: e.opts.map(o => ({ value: o.value, description: o.desc })) });
  hk += ((x.input + x.opts.map(o => o.desc).join('')).match(tr.HG) || []).length;
  he += ((e.input + e.opts.map(o => o.desc).join('')).match(tr.HG) || []).length;
}
fs.writeFileSync(path.join(__dirname, 'en-pre.json'), JSON.stringify(out, null, 1));
const M = Object.entries(tr.MISS).map(([k, v]) => ({ key: k, slots: (k.match(/\{\}/g) || []).length, n: v.n, ex: v.ex, ctx: v.ctx === v.ex ? undefined : v.ctx })).sort((a, b) => b.n - a.n);
fs.writeFileSync(path.join(__dirname, 'missing.json'), JSON.stringify(M, null, 1));
console.log('dict', Object.keys(tr.DICT).length, 'missing keys', M.length, 'leftover hangul', he, '/', hk, (he / hk * 100).toFixed(2) + '%');
