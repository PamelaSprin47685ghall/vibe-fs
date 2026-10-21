// CUT-28 sub-scope ② differential: the hand-written Fission topology and settlement laws in
// Execution/Fission/Model.fs (base dist) vs the candidate (same F# signatures over
// wanxiangli/fission-group). Exhaustive over small domains: every prompt list of length <= 4 over
// five texts (blank/whitespace included); routing for lane counts 0..4 x broadcast/lane -1..5;
// mergeOrder/finalLane for -1..6; successor for counts 0..5 x index -1..5 x every closed subset of
// 0..4; both settlements for every observation; convergence for lane counts 0..4 x every bundle key
// set x every delivery set for 0..2 pre-Fission completions (delivery lane count = group lane count).
// The convergence answers for a delivery built for a *different* lane count are reported separately
// (the candidate deliberately refuses those; see cards/CUT-28.md).
// Usage: node topology-differential.mjs <base-root> <cand-root>
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`, fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return { m: await import(`${d}/Execution/Fission/Model.js`), list: await import(`${fl}/List.js`), map: await import(`${fl}/Map.js`), set: await import(`${fl}/Set.js`) }
}
const base = await load(baseRoot), cand = await load(candRoot)
const norm = (v) => {
  if (v === null || v === undefined) return null
  if (typeof v !== 'object') return v
  if (Array.isArray(v)) return v.map(norm)
  if (typeof v.cases === 'function' && 'tag' in v) return { [v.cases()[v.tag]]: norm(v.fields) }
  if (typeof v[Symbol.iterator] === 'function') return [...v].map(norm)
  const o = {}
  for (const k of Object.keys(v)) o[k] = norm(v[k])
  return o
}
let compared = 0, differences = 0, mismatchedCountCompared = 0, mismatchedCountDifferences = 0
const counts = {}, samples = []
const cmp = (kind, f, bucket = 'main') => {
  let x, y
  try { x = JSON.stringify(norm(f(base))) } catch (e) { x = 'throw ' + e.message }
  try { y = JSON.stringify(norm(f(cand))) } catch (e) { y = 'throw ' + e.message }
  counts[kind] = (counts[kind] ?? 0) + 1
  if (bucket === 'main') { compared++; if (x !== y) { differences++; if (samples.length < 6) samples.push({ kind, base: x, cand: y }) } }
  else { mismatchedCountCompared++; if (x !== y) mismatchedCountDifferences++ }
}
const texts = ['a', ' ', '', '\t\n', 'x y']
const lists = [[]]
for (let len = 1; len <= 4; len++) { const prev = lists.filter((l) => l.length === len - 1); for (const p of prev) for (const t of texts) lists.push([...p, t]) }
for (const l of lists) cmp('parse', (b) => b.m.FissionPrompt_parse(b.list.ofArray(l)))
for (let n = 0; n <= 4; n++) {
  cmp('targets', (b) => b.m.FissionCompletionRouting_targets(n, new b.m.FissionCompletionAffinity(0, [])))
  for (let i = -1; i <= 5; i++) cmp('targets', (b) => b.m.FissionCompletionRouting_targets(n, b.m.FissionCompletionAffinityModule_lane(i)))
}
for (let n = -1; n <= 6; n++) { cmp('mergeOrder', (b) => b.m.FissionRing_mergeOrder(n)); cmp('finalLane', (b) => b.m.FissionRing_finalLane(n)) }
const subsets = (k) => Array.from({ length: 1 << k }, (_, mask) => Array.from({ length: k }, (_, i) => i).filter((i) => mask & (1 << i)))
for (let n = 0; n <= 5; n++) for (let i = -1; i <= 5; i++) for (const closed of subsets(5)) cmp('successor', (b) => b.m.FissionRing_successor(n, i, b.list.ofArray(closed)))
for (let tag = 0; tag < 6; tag++) {
  const obs = (b) => new b.m.FissionSettlementObservation(tag, tag === 4 ? ['aborted by user'] : [])
  cmp('decideLane', (b) => b.m.FissionSettlement_decideLane(obs(b)))
  cmp('decideTakeover', (b) => b.m.FissionSettlement_decideTakeover(obs(b)))
}
const bundleOf = (b, keys) => keys.reduce((acc, k) => b.m.FissionWorkBundleModule_add(k, `v${k}`, acc).fields[0], b.m.FissionWorkBundleModule_empty)
const deliveryOf = (b, dc, marks) => marks.reduce((acc, [id, lane]) => { const r = b.m.FissionDeliveryModule_mark(id, lane, acc); return r.tag === 0 ? r.fields[0] : acc }, b.m.FissionDeliveryModule_empty(dc))
for (let n = 0; n <= 4; n++) {
  const keySets = subsets(Math.max(n, 1) + 1)
  for (const pre of [[], ['c1'], ['c1', 'c2']]) {
    for (const keys of keySets) {
      for (const d1 of subsets(Math.min(n, 4))) for (const d2 of (pre.length > 1 ? subsets(Math.min(n, 3)) : [[]])) {
        const marks = [...d1.map((l) => ['c1', l]), ...d2.map((l) => ['c2', l])]
        cmp('ready', (b) => b.m.FissionConvergence_ready(n, b.list.ofArray(pre), bundleOf(b, keys), deliveryOf(b, n, marks)))
        cmp('ready-mismatched-count', (b) => b.m.FissionConvergence_ready(n, b.list.ofArray(pre), bundleOf(b, keys), deliveryOf(b, Math.max(0, n - 1), marks)), 'mismatched')
      }
    }
  }
}
console.log(JSON.stringify({ compared, differences, counts, samples, mismatchedLaneCount: { compared: mismatchedCountCompared, differences: mismatchedCountDifferences } }, null, 2))
process.exit(differences === 0 ? 0 : 1)
