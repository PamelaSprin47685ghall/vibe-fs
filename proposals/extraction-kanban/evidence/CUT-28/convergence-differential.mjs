// CUT-28 convergence differential: the hand-written host convergence checks that the hosts used
// before this cut (OpenCode/Host.fs allLaneWorkPresent && allSharedCompletionsDelivered, and
// OpenCode/Tool.fs readyExceptForRetirement) vs FissionProjection.convergenceReady (the one
// wanxiangli/fission-group convergenceReady), on every group reachable through the candidate fold.
// Same seeded fact generator as fission-differential.mjs.
// Usage: node convergence-differential.mjs <cand-root> [sequences]
const [candRoot, n = '20000'] = process.argv.slice(2)
const d = `${candRoot}/dist`, fl = `${d}/fable_modules/fable-library-js.5.13.0`
const cand = {
  p: await import(`${d}/Execution/Fission/Projection.js`),
  f: await import(`${d}/Execution/Fission/Facts.js`),
  id: await import(`${d}/Foundation/Identity.js`),
  list: await import(`${fl}/List.js`),
}
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
const pairs = (m) => [...m].map(([k, v]) => [norm(k), v])
const sameSet = (xs, n) => xs.length === n && xs.every((x, i) => x === i)
function oldHost(g) {
  const n = g.LaneCount
  const work = pairs(g.LaneWork).length === n && pairs(g.LaneProviderRuns).length === n
  const captured = new Set(pairs(g.CapturedCompletions).map(([k]) => k))
  const deliveries = new Map(pairs(g.CompletionDeliveries).map(([k, v]) => [k, [...v].sort((a, b) => a - b)]))
  const shared = [...g.PreFissionCompletionIds].every((id) => captured.has(id) && sameSet(deliveries.get(id) ?? [], n))
  return work && shared
}
function oldTool(g) {
  const n = g.LaneCount
  const captured = new Set(pairs(g.CapturedCompletions).map(([k]) => k))
  const deliveries = new Map(pairs(g.CompletionDeliveries).map(([k, v]) => [k, [...v].sort((a, b) => a - b)]))
  return pairs(g.LaneWork).length === n && [...g.PreFissionCompletionIds].every((id) => captured.has(id) && sameSet(deliveries.get(id) ?? [], n))
}
let compared = 0, differences = 0, ready = 0
const samples = []
for (let s = 0; s < Number(n); s++) {
  let st = cand.p.FissionProjection_empty
  const cfg = { count: pick([2, 2, 3]), pre: pick([[], ['c1'], ['c1']]) }
  for (let k = 0; k < 24; k++) {
    const r = cand.p.FissionProjection_fold(st, build(cand, genFact(cfg, k)))
    if (r.tag === 0) st = r.fields[0]
    for (const [, g] of st.Groups) {
      const now = cand.p.FissionProjection_convergenceReady(g), h = oldHost(g), t = oldTool(g)
      compared++; if (now) ready++
      if (now !== h || now !== t) { differences++; if (samples.length < 5) samples.push({ sequence: s, step: k, now, host: h, tool: t, group: JSON.stringify(norm(g)).slice(0, 400) }) }
    }
  }
}
console.log(JSON.stringify({ sequences: Number(n), compared, ready, differences, samples }, null, 2))
process.exit(differences === 0 && ready > 0 ? 0 : 1)
