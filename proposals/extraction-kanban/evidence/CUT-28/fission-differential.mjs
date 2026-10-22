// CUT-28 differential: the hand-written FissionProjection.fold (base dist) vs the candidate
// (wanxiangli/fission-group judging behind the same fold). Seeded random fact sequences over
// 2 groups x 2 owners x lane counts 1..3, biased toward the happy path so groups reach takeover
// and convergence, with replays, conflicting values, wrong lanes/sessions, unknown groups and
// late terminals mixed in. Every step compares the whole Result (rejection, or the full state).
// Usage: node fission-differential.mjs <base-root> <cand-root> [sequences]
const [baseRoot, candRoot, n = '20000'] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`, fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return {
    p: await import(`${d}/Execution/Fission/Projection.js`),
    f: await import(`${d}/Execution/Fission/Facts.js`),
    id: await import(`${d}/Foundation/Identity.js`),
    list: await import(`${fl}/List.js`),
  }
}
const base = await load(baseRoot), cand = await load(candRoot)
let seed = 28
const rnd = (k) => { seed = (seed + 0x6d2b79f5) | 0; let x = Math.imul(seed ^ (seed >>> 15), 1 | seed); x = (x + Math.imul(x ^ (x >>> 7), 61 | x)) ^ x; return ((x ^ (x >>> 14)) >>> 0) % k }
const pick = (xs) => xs[rnd(xs.length)]
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
// abstract fact -> concrete fact for one build
function build(m, a) {
  const S = (s) => m.id.SessionIdModule_create(s), R = (s) => m.id.BlobRefModule_create(s), D = (s) => m.id.BlobDigestModule_create(s)
  const P = (s) => m.id.ProviderRunIdentityModule_create(s), L = (xs) => m.list.ofArray(xs)
  const F = (tag, x) => new m.f.FissionFactCases(tag, [x])
  switch (a.k) {
    case 'admitted': return F(0, { GroupId: a.g, OwnerSessionId: S(a.o), ParentSessionId: a.parent ? S(a.parent) : undefined, OriginToolCallId: m.id.ToolCallIdModule_create(a.call), LaneCount: a.count, LaneSessions: L(a.lanes.map(S)), LanePrompts: L(a.prompts), OwnerViewRef: R(a.view), OwnerViewDigest: D(a.view + '#'), PreFissionCompletionIds: L(a.pre) })
    case 'lane': return F(1, { GroupId: a.g, OwnerSessionId: S(a.o), LaneIndex: a.i, LaneSessionId: S(a.s), ProviderRun: P(a.run), ViewRef: R(a.v), ViewDigest: D(a.v + '#') })
    case 'captured': return F(2, { GroupId: a.g, OwnerSessionId: S(a.o), CompletionId: a.c, PayloadRef: R(a.v), PayloadDigest: D(a.v + '#') })
    case 'delivered': return F(3, { GroupId: a.g, OwnerSessionId: S(a.o), CompletionId: a.c, LaneIndex: a.i })
    case 'affinity': return F(4, { GroupId: a.g, OwnerSessionId: S(a.o), ExternalId: a.x, LaneIndex: a.i })
    case 'claimed': return F(5, { GroupId: a.g, OwnerSessionId: S(a.o), LaneIndex: a.i, LaneSessionId: S(a.s), PromptKey: m.id.PromptKeyModule_create(a.key), AggregateViewRef: R(a.v), AggregateViewDigest: D(a.v + '#') })
    case 'started': return F(6, { GroupId: a.g, OwnerSessionId: S(a.o), LaneIndex: a.i, LaneSessionId: S(a.s), PhysicalUserMessageId: m.id.PhysicalUserMessageIdModule_create(a.key), AggregateViewRef: R(a.v), AggregateViewDigest: D(a.v + '#') })
    case 'converged': return F(7, { GroupId: a.g, OwnerSessionId: S(a.o), TerminalLaneSessionId: S(a.s), TerminalProviderRun: P(a.run), AggregateViewRef: R(a.v), AggregateViewDigest: D(a.v + '#') })
    case 'failed': return F(8, { GroupId: a.g, OwnerSessionId: S(a.o), Reason: a.reason })
  }
}
const groups = { g1: { o: 'o1' }, g2: { o: 'o2' } }
function genFact(cfg, k) {
  const g = rnd(10) < 8 ? 'g1' : pick(['g2', 'gX']), o = groups[g]?.o ?? 'o1', count = rnd(10) < 8 ? cfg.count : pick([1, 2, 3]), lane = (i) => `${g}-l${i}`
  const i = rnd(10) < 8 ? rnd(cfg.count) : pick([-1, 2, 3]), sess = rnd(10) === 0 ? 'stranger' : lane(Math.max(0, i))
  const v = rnd(8) ? 'v1' : 'v2', c = rnd(8) ? 'c1' : 'c2'
  if (k === 0 && rnd(5)) return { k: 'admitted', g: 'g1', o: 'o1', parent: 'p', call: 't1', count: cfg.count, lanes: Array.from({ length: cfg.count }, (_, q) => `g1-l${q}`), prompts: Array.from({ length: cfg.count }, (_, q) => `prompt${q}`), view: 'ov', pre: cfg.pre }
  switch (rnd(14)) {
    case 0: case 1: {
      const lanes = Array.from({ length: count + (rnd(8) === 0 ? 1 : 0) }, (_, k) => lane(k))
      return { k: 'admitted', g, o: rnd(8) === 0 ? 'o2' : o, parent: 'p', call: 't1', count, lanes, prompts: lanes.map((_, k) => `prompt${k}`).slice(0, count + (rnd(10) === 0 ? -1 : 0)), view: pick(['ov', 'ov', 'ov2']), pre: rnd(4) ? cfg.pre : pick([[], ['c1'], ['c1', 'c2']]) }
    }
    case 2: case 3: return { k: 'lane', g, o, i, s: sess, run: rnd(8) ? 'r1' : 'r2', v }
    case 4: return { k: 'captured', g, o, c, v }
    case 5: case 6: return { k: 'delivered', g, o, c, i }
    case 7: return { k: 'affinity', g, o, x: pick(['x1', 'x2']), i }
    case 8: return { k: rnd(2) ? 'claimed' : 'started', g, o, i: rnd(4) ? 0 : i, s: rnd(4) ? lane(0) : sess, key: rnd(4) ? 'k1' : 'k2', v }
    case 9: case 10: case 11: case 12: return { k: 'converged', g, o, s: rnd(4) ? lane(0) : pick([lane(1), 'stranger']), run: rnd(4) ? 'r1' : 'r2', v }
    default: return { k: 'failed', g, o, reason: pick(['boom', 'other']) }
  }
}
let compared = 0, differences = 0
const tally = {}, samples = []
for (let s = 0; s < Number(n); s++) {
  let sb = base.p.FissionProjection_empty, sc = cand.p.FissionProjection_empty
  const cfg = { count: pick([2, 2, 3]), pre: pick([[], ['c1'], ['c1']]) }
  for (let k = 0; k < 24; k++) {
    const a = genFact(cfg, k)
    const rb = base.p.FissionProjection_fold(sb, build(base, a)), rc = cand.p.FissionProjection_fold(sc, build(cand, a))
    const xb = JSON.stringify(norm(rb)), xc = JSON.stringify(norm(rc))
    compared++
    const label = `${a.k} ${rb.tag === 0 ? 'Ok' : 'Error ' + rb.fields[0].cases()[rb.fields[0].tag]}`
    tally[label] = (tally[label] ?? 0) + 1
    if (xb !== xc) { differences++; if (samples.length < 5) samples.push({ sequence: s, step: k, fact: a, base: xb.slice(0, 400), cand: xc.slice(0, 400) }) }
    if (rb.tag === 0) sb = rb.fields[0]
    if (rc.tag === 0) sc = rc.fields[0]
  }
}
console.log(JSON.stringify({ sequences: Number(n), compared, differences, outcomes: tally, samples }, null, 2))
process.exit(differences === 0 ? 0 : 1)
