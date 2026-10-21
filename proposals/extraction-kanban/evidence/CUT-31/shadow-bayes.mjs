// CUT-31 ② shadow: the old in-repo Bayes exact refiner (a dist that still has Plugins/Bayes/Exact)
// against wanxiangli/bayes on the same seeded inputs. Usage: node shadow-bayes.mjs <old-dist> <bayes-entry> <rounds> <seed>
// Expected, declared divergences only: DuplicateHypothesisKey names the repeated key (old: the first key),
// and priorOnly enforces the hypothesis-set contract (old: accepted 1 hypothesis / blank / duplicate keys).
import { join } from 'node:path'
const [oldDist, entry, roundsArg = '50000', seedArg = '31'] = process.argv.slice(2)
const Old = await import(join(oldDist, 'Sphinx/V2/Plugins/Bayes/Surface.js'))
const fl = join(oldDist, 'fable_modules/fable-library-js.5.13.0')
const { toArray } = await import(join(fl, 'List.js'))
const FMap = await import(join(fl, 'Map.js'))
const { BayesLaw } = await import(entry)
const law = new BayesLaw()
let s = Number(seedArg) >>> 0
const rnd = () => { s = (s + 0x6D2B79F5) >>> 0; let t = s; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296 }
const int = (n) => Math.floor(rnd() * n)
const pick = (xs) => xs[int(xs.length)]
const keysPool = ['h1', 'h2', 'h3', 'h4', 'h5']
const prior = () => (rnd() < 0.03 ? pick([-1, NaN, Infinity]) : rnd() < 0.1 ? 0 : rnd() * 3)
const lik = () => (rnd() < 0.02 ? pick([1.5, -0.1, NaN]) : rnd() < 0.05 ? 1e-300 * rnd() : rnd() < 0.05 ? 0 : rnd())
function sample() {
  const n = rnd() < 0.05 ? int(2) : 2 + int(4)
  const hs = Array.from({ length: n }, (_, i) => ({ Key: rnd() < 0.02 ? ' ' : rnd() < 0.03 ? pick(keysPool) : keysPool[i], Prior: prior() }))
  const fs = Array.from({ length: int(7) }, () => {
    const ls = hs.filter(() => rnd() > 0.02).map(h => [h.Key, lik()])
    if (rnd() < 0.02) ls.push(['zz', 0.5])
    return { ObservationId: pick(['o1', 'o2', 'o3', 'o4']), DependencyKey: rnd() < 0.02 ? '' : pick(['g1', 'g2', 'g3']), Likelihoods: ls, Qualified: rnd() > 0.15 }
  })
  return { hs, fs }
}
const oldView = (r) => {
  if (r.tag !== 0) { const f = r.fields[0]; return { ok: false, fault: { case: f.name, fields: [...f.fields] } } }
  const p = r.fields[0]
  return { ok: true, posterior: { probabilities: Object.fromEntries(FMap.toArray(p.Probabilities)), logPartition: p.LogPartition, usedObservations: toArray(p.UsedObservations), dropped: toArray(p.Dropped).map(d => [...d]) } }
}
const newFault = (f) => ({ case: f.kind, fields: Object.entries(f).filter(([k]) => k !== 'kind').map(([, v]) => v) })
const same = (a, b) => JSON.stringify(a, (k, v) => (typeof v === 'number' && !Number.isFinite(v) ? String(v) : v)) === JSON.stringify(b, (k, v) => (typeof v === 'number' && !Number.isFinite(v) ? String(v) : v))
const tally = { infer: { cases: 0, ok: 0, faults: {}, differences: 0, declared: 0 }, priorOnly: { cases: 0, ok: 0, differences: 0, declared: 0 } }
const firstDiff = []
for (let r = 0; r < Number(roundsArg); r++) {
  const { hs, fs } = sample()
  const oldHs = Old.listOfItems(hs.map(h => ({ ...h })))
  const oldFs = Old.listOfItems(fs.map(f => ({ ...f, Likelihoods: Old.stringFloatMapOf(f.Likelihoods) })))
  const newHs = hs.map(h => ({ key: h.Key, prior: h.Prior }))
  const newFs = fs.map(f => ({ observationId: f.ObservationId, dependencyKey: f.DependencyKey, likelihoods: Object.fromEntries(f.Likelihoods), qualified: f.Qualified }))
  for (const op of ['infer', 'priorOnly']) {
    const a = oldView(op === 'infer' ? Old.infer(oldHs, oldFs) : Old.priorOnly(oldHs))
    const nr = op === 'infer' ? law.infer(newHs, newFs) : law.priorOnly(newHs)
    const b = nr.ok ? { ok: true, posterior: nr.posterior } : { ok: false, fault: newFault(nr.fault) }
    const t = tally[op]; t.cases++
    if (b.ok) t.ok++; else if (t.faults) t.faults[b.fault.case] = (t.faults[b.fault.case] || 0) + 1
    if (same(a, b)) continue
    const keys = hs.map(h => h.Key), dup = keys.length !== new Set(keys).size
    const declared = (op === 'infer' && !a.ok && !b.ok && a.fault.case === 'DuplicateHypothesisKey' && b.fault.case === 'DuplicateHypothesisKey')
      || (op === 'priorOnly' && !b.ok && ['TooFewHypotheses', 'BlankHypothesisKey', 'DuplicateHypothesisKey'].includes(b.fault.case) && (keys.length < 2 || dup || keys.some(k => !k.trim())))
    if (declared) t.declared++; else { t.differences++; if (firstDiff.length < 3) firstDiff.push({ op, hs, fs, a, b }) }
  }
}
console.log(JSON.stringify(tally))
for (const d of firstDiff) console.log('DIFF', JSON.stringify(d))
