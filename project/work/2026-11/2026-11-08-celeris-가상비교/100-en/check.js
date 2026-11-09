// node check.js -> fact check of tr/en-pre.json against ../100-ko/cases.json:
// numbers and Korean-side ASCII tokens (multiset) survive in English (input and each choice), choice values/order equal, no Hangul.
const fs = require('fs'), path = require('path');
const KO = JSON.parse(fs.readFileSync(path.join(__dirname, '..', '100-ko', 'cases.json'), 'utf8'));
const EN = JSON.parse(fs.readFileSync(path.join(__dirname, 'tr', 'en-pre.json'), 'utf8'));
const TOK = /[-+]?[A-Za-z0-9_][A-Za-z0-9_:.,\-+#]*[A-Za-z0-9_%]|[-+]?[A-Za-z0-9_]/g;
const NUM = /\d+(?:\.\d+)?/g;
const esc = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const bag = s => { const m = {}; for (const t of (s.match(NUM) || [])) m['#' + t] = (m['#' + t] || 0) + 1;
  for (const t of (s.match(TOK) || [])) if (/[A-Za-z]/.test(t)) for (const p of t.split(/[^A-Za-z_]+/).filter(x => x.length > 1)) m['@' + p] = (m['@' + p] || 0) + 1; return m; };
let bad = [], nTok = 0;
for (const k of KO) {
  const e = EN.find(x => x.id === k.id);
  if (!e) { bad.push(k.id + ': missing'); continue; }
  if (/[가-힣]/.test(e.input + e.choices.map(c => c.description).join(''))) bad.push(k.id + ': Hangul left');
  if (JSON.stringify(k.cands.map(c => c.value)) !== JSON.stringify(e.choices.map(c => c.value))) bad.push(k.id + ': choice values/order differ');
  const pairs = [['input', k.text, e.input], ...k.cands.map((c, i) => [c.value, c.description, e.choices[i].description])];
  for (const [nm, ko, en] of pairs) {
    const a = bag(ko), b = bag(en);
    for (const w of Object.keys(a)) if (w[0] == '@') b[w] = (en.match(new RegExp(esc(w.slice(1)), 'g')) || []).length;
    for (const [t, n] of Object.entries(a)) { nTok += n; if ((b[t] || 0) < n) bad.push(`${k.id} ${nm}: token '${t}' ko ${n} en ${b[t] || 0}`); }
  }
}
console.log('korean-side tokens checked', nTok); console.log(bad.length ? bad.slice(0, 60).join('\n') + (bad.length > 60 ? `\n... ${bad.length} total` : '') : 'OK');
