// node check.js -> fact check of tr/kop-pre.json against ../100-ko/cases.json:
// numbers and ASCII tokens (multiset) survive in KO' (input and each choice), choice values/order equal.
const fs = require('fs'), path = require('path');
const KO = JSON.parse(fs.readFileSync(path.join(__dirname, '..', '100-ko', 'cases.json'), 'utf8'));
const KP = JSON.parse(fs.readFileSync(path.join(__dirname, 'tr', 'kop-pre.json'), 'utf8'));
const TOK = /[-+]?[A-Za-z0-9_][A-Za-z0-9_:.,\-+#]*[A-Za-z0-9_%]|[-+]?[A-Za-z0-9_]/g;
const NUM = /\d+(?:\.\d+)?/g;
const bag = s => { const m = {}; for (const t of (s.match(NUM) || [])) m['#' + t] = (m['#' + t] || 0) + 1;
  for (const t of (s.match(TOK) || [])) if (/[A-Za-z]/.test(t)) m['@' + t] = (m['@' + t] || 0) + 1; return m; };
let bad = [], nTok = 0, extra = 0;
for (const k of KO) {
  const e = KP.find(x => x.id === k.id);
  if (!e) { bad.push(k.id + ': missing'); continue; }
  if (JSON.stringify(k.cands.map(c => c.value)) !== JSON.stringify(e.choices.map(c => c.value))) bad.push(k.id + ': choice values/order differ');
  const pairs = [['input', k.text, e.input], ...k.cands.map((c, i) => [c.value, c.description, e.choices[i].description])];
  for (const [nm, ko, kp] of pairs) {
    const a = bag(ko), b = bag(kp);
    for (const [t, n] of Object.entries(a)) { nTok += n; if ((b[t] || 0) !== n) bad.push(`${k.id} ${nm}: token '${t}' ko ${n} kop ${b[t] || 0}`); }
    for (const [t, n] of Object.entries(b)) if (!(t in a)) { extra += n; bad.push(`${k.id} ${nm}: extra token '${t}' kop ${n}`); }
  }
}
console.log('tokens checked', nTok, 'extra', extra);
console.log(bad.length ? bad.slice(0, 60).join('\n') + (bad.length > 60 ? `\n... ${bad.length} total` : '') : 'OK');
