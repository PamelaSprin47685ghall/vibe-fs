// CUT-19-b shadow differential. (1) ProjectionCutTail payload codec: the old host codec — the exact
// Thoth.Json encode/decode calls IntegratorEngine.fs made, imported from a base dist — against
// wanxiangli/quarantine encodeCut/decodeCut over fixed-seed random payloads (wire bytes and decode
// verdicts). (2) Quarantine ledger: a reference model of the old Map<RuleFaultKey, RuleFault>
// (record-keyed, add overwrites, remove no-op) against the library ledger over random op sequences,
// including persistence of every older snapshot. Pure: no files, no store.
// usage: node shadow-quarantine.mjs <base-dist> [rounds]   (run from the vibe-fs worktree root)
import { join, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'
import { RuleQuarantine } from 'wanxiangli/quarantine'
const [baseDist, roundsArg] = process.argv.slice(2)
const mod = p => import(pathToFileURL(join(resolve(baseDist), p)).href)
const Enc = await mod('fable_modules/Thoth.Json.10.5.1/Encode.fs.js')
const Dec = await mod('fable_modules/Thoth.Json.10.5.1/Decode.fs.js')
const Res = await mod('fable_modules/fable-library-js.5.13.0/Result.js')
const q = new RuleQuarantine()
const oldEncode = c => Enc.object([['rule', c.rule], ['failed_event_id', c.failedEventId], ['reason', c.reason], ['reset_json', c.resetJson]])
const oldDecode = (eventType, payload) => {
  if (eventType !== 'ProjectionCutTail') return null
  const r = Res.Result_ToOption(Dec.fromValue('$', (path, v) => Dec.object(get => ({
    rule: get.Required.Field('rule', Dec.string), failedEventId: get.Required.Field('failed_event_id', Dec.string),
    reason: get.Required.Field('reason', Dec.string), resetJson: get.Required.Field('reset_json', Dec.string) }), path, v), payload))
  return r === undefined ? null : r
}
let seed = 1902
const random = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 }
const pick = l => l[Math.floor(random() * l.length)]
const texts = ['', 'Jobs', 'a/b', '😀', '缓存', '"q"', '{"a":1}', '\n', ' ', 'x'.repeat(40)]
const values = [...texts, 1, 0, true, null, undefined, [], {}, ['x']]
const types = ['ProjectionCutTail', 'ProjectionCutTail', 'JobRequested', 'projectioncuttail', '']
const keys = ['rule', 'failed_event_id', 'reason', 'reset_json']
const rounds = Number(roundsArg ?? 3000)
let codecCases = 0, codecEqual = 0, decodeCases = 0, decodeEqual = 0, cuts = 0; const diffs = []
const norm = d => d === null ? null : JSON.stringify({ rule: d.rule, failedEventId: d.failedEventId, reason: d.reason, resetJson: d.resetJson })
for (let r = 0; r < rounds; r++) {
  const c = { rule: pick(texts), failedEventId: pick(texts), reason: pick(texts), resetJson: pick(texts) }
  codecCases++; const a = JSON.stringify(oldEncode(c)), b = JSON.stringify(q.encodeCut(c))
  if (a === b) codecEqual++; else diffs.push({ what: 'encode', c, a, b })
  let payload
  const shape = random()
  if (shape < 0.5) payload = q.encodeCut(c)
  else if (shape < 0.9) { payload = {}; for (const k of keys) if (random() < 0.85) payload[k] = random() < 0.8 ? pick(texts) : pick(values); if (random() < 0.2) payload.extra = pick(values) }
  else payload = pick(values)
  const t = pick(types)
  decodeCases++; const da = norm(oldDecode(t, payload)), db = norm(q.decodeCut(t, payload))
  if (da !== null) cuts++
  if (da === db) decodeEqual++; else diffs.push({ what: 'decode', t, payload, da, db })
}
// ledger
let ledgerOps = 0, ledgerEqual = 0
for (let r = 0; r < rounds; r++) {
  const model = new Map(); let ledger = q.empty(); const snaps = []
  for (let s = Math.floor(random() * 12); s >= 0; s--) {
    const rule = pick(['Jobs', 'Plans', 'a', 'a/b']), scope = pick(['global', 'job/a', 'b', 'b/c', ''])
    const k = JSON.stringify([rule, scope]); const op = random()
    if (op < 0.45) { const f = { failedEventId: pick(texts), reason: pick(texts) }; model.set(k, f); ledger = q.quarantine(ledger, rule, scope, f.failedEventId, f.reason) }
    else if (op < 0.75) { model.delete(k); ledger = q.release(ledger, rule, scope) }
    snaps.push([new Map(model), ledger])
    for (const [m, l] of snaps) for (const rr of ['Jobs', 'Plans', 'a', 'a/b']) for (const ss of ['global', 'job/a', 'b', 'b/c', '']) {
      ledgerOps++
      const want = m.get(JSON.stringify([rr, ss])) ?? null, got = q.fault(l, rr, ss)
      const ok = (want === null ? got === null : got !== null && got.failedEventId === want.failedEventId && got.reason === want.reason) && q.isQuarantined(l, rr, ss) === (want !== null) && q.size(l) === m.size
      if (ok) ledgerEqual++; else if (diffs.length < 50) diffs.push({ what: 'ledger', rr, ss, want, got })
    }
  }
}
console.log(JSON.stringify({ rounds, encode: { cases: codecCases, equal: codecEqual }, decode: { cases: decodeCases, equal: decodeEqual, validCuts: cuts }, ledger: { checks: ledgerOps, equal: ledgerEqual }, diffs: diffs.slice(0, 10) }, null, 1))
