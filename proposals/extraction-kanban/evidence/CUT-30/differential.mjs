// Old-vs-new LoopDetector differential (CUT-30).
// Usage (repo root, after a build): copy the base build's compiled
// dist/Execution/Session/LoopDetector.js to dist/Execution/Session/LoopDetector.base.js,
// then `node proposals/extraction-kanban/evidence/CUT-30/differential.mjs [seed] [rounds]`.
import * as Old from '../../../../dist/Execution/Session/LoopDetector.base.js'
import * as New from '../../../../dist/Execution/Session/LoopDetector.js'

let seed = Number(process.argv[2] ?? 20261020) >>> 0
const rounds = Number(process.argv[3] ?? 400)
const rand = () => ((seed = (seed * 1664525 + 1013904223) >>> 0) / 2 ** 32)
const words = ['the', 'loop', 'again', '重复', '退化', 'token', '\n', '  ', 'x', '{', '}', '😀', 'abc123', 'λ']
const text = () => {
  const kind = rand()
  if (kind < 0.1) return ''
  const n = 1 + Math.floor(rand() * 60)
  if (kind < 0.35) return words[Math.floor(rand() * words.length)].repeat(n)
  if (kind < 0.6) return Array.from({ length: n }, () => String.fromCodePoint(32 + Math.floor(rand() * 20000))).join('')
  return Array.from({ length: n }, () => words[Math.floor(rand() * words.length)]).join(' ')
}
const view = (e) => JSON.stringify([e.State.tag ?? e.State.name ?? String(e.State), e.IsAnomalous, e.WeightedDistinctTokenCount, e.Step])
const close = (a, b) => Math.abs(a - b) <= 1e-9 * Math.max(1, Math.abs(a))
let compared = 0, exact = 0, mismatches = 0
for (let r = 0; r < rounds; r++) {
  let o = Old.create(), n = New.create()
  const steps = 1 + Math.floor(rand() * 40)
  for (let s = 0; s < steps; s++) {
    if (rand() < 0.03) { o = Old.create(); n = New.create(); continue }
    const t = text()
    const [o2, oe] = Old.pushText(o, t)
    const [n2, ne] = New.pushText(n, t)
    o = o2; n = n2; compared++
    const same = view(oe) === view(ne)
    const tracked = Old.trackedTokenCount(o) === New.trackedTokenCount(n)
    const ev = view(Old.evaluate(o)) === view(New.evaluate(n))
    if (same && tracked && ev) { exact++; continue }
    const near = oe.Step === ne.Step && close(oe.WeightedDistinctTokenCount, ne.WeightedDistinctTokenCount)
    if (!near || !tracked || String(oe.State) !== String(ne.State)) { mismatches++; if (mismatches < 5) console.error('MISMATCH', r, s, view(oe), view(ne)) }
  }
}
for (const x of [Old.MinimumWeightedDistinctCount, Old.MaximumWeightedDistinctCount, Old.MinimumWeightedDistinctCount - 1e-9, Old.MaximumWeightedDistinctCount + 1e-9, 0, -1, 1e9, NaN, Infinity, -Infinity, Old.NormalWeightedDistinctCount])
  if (String(Old.classify(x)) !== String(New.classify(x))) { mismatches++; console.error('CLASSIFY MISMATCH', x) }
console.log(JSON.stringify({ rounds, compared, exact, mismatches }))
process.exit(mismatches ? 1 : 0)
