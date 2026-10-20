// CUT-13 sub-loop 1 differential: the hand-written HandleProjection lifecycle transitions (base
// dist) vs the candidate (wanxiangli/handle-lifecycle behind the same F# signatures).
// Handle cells: absent / Active / CompletedAwaitingJoin(every completion) / Abandoned(every reason)
// / Retired x complete(every completion) / abandon(every reason) / retire / rejectFalseCompletion
// (every expected ref x digest). Work cells: absent / every phase x legacy or not x
// completeWork(every completion) / abandonWork / voidWork / consumeWork(every consumption id x
// every expected completion). Compared: the whole Result (rejection, or the full projection).
// Usage: node handle-differential.mjs <base-root> <cand-root>
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`, fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return {
    lp: await import(`${d}/Execution/Delegation/LinkageProjection.js`),
    facts: await import(`${d}/Execution/Delegation/Facts.js`),
    id: await import(`${d}/Foundation/Identity.js`),
    roles: await import(`${d}/Foundation/Roles.js`),
    map: await import(`${fl}/Map.js`),
    set: await import(`${fl}/Set.js`),
    util: await import(`${fl}/Util.js`),
  }
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

// Abstract universe, materialized per implementation.
const kinds = ['Terminal', 'SendFailure', 'Cancelled']
const refs = [null, 'r1', 'r2'], digests = [null, 'd1', 'd2']
const completions = []
for (const kind of kinds) for (const ref of refs) for (const digest of digests) completions.push({ kind, ref, digest })
const reasons = [0, 1, 2]
const phases = [null, { p: 'Active' }, ...completions.map((c) => ({ p: 'Completed', c })), ...reasons.map((r) => ({ p: 'Abandoned', r })), { p: 'Retired' }]

const mk = (m) => {
  const cmp = { Compare: (x, y) => m.util.compare(x, y) | 0 }
  const handle = new m.id.HandleId(0, [m.id.AgentHandleIdModule_create('h')])
  const child = m.id.SessionIdModule_create('child')
  const work = new m.facts.HandleWorkId(handle, child, m.id.AuthorityRootUserMessageIdModule_create('root'))
  const completion = (c) => new m.lp.HandleCompletion(new m.facts.HandleCompletionKind(kinds.indexOf(c.kind), []),
    c.ref == null ? undefined : m.id.BlobRefModule_create(c.ref), c.digest == null ? undefined : m.id.BlobDigestModule_create(c.digest))
  const lifecycle = (ph) => ph.p === 'Active' ? m.lp.HandleLifecycle.Active
    : ph.p === 'Completed' ? new m.lp.HandleLifecycle(1, [completion(ph.c)])
    : ph.p === 'Abandoned' ? new m.lp.HandleLifecycle(2, [new m.facts.HandleAbandonReason(ph.r, [])])
    : m.lp.HandleLifecycle.Retired
  const role = m.roles.Role[Object.keys(m.roles.Role).find((k) => m.roles.Role[k] instanceof m.roles.Role)] ?? new m.roles.Role(0, [])
  const record = (ph) => new m.lp.HandleRecord(handle, child, 'agent', 'by', role, new m.facts.HandleOwnership(0, []), lifecycle(ph), 0,
    ph.p === 'Completed' ? completion(ph.c) : undefined, undefined)
  const projection = (handlePhase, workPhase, legacy) => new m.lp.AgentLinkageProjection(
    m.map.ofArray(handlePhase ? [[handle, record(handlePhase)]] : [], cmp), 1,
    m.map.ofArray(workPhase ? [[work, new m.lp.HandleWork(work, m.id.LogicalRunIdModule_create('run'), lifecycle(workPhase),
      workPhase.p === 'Completed' ? completion(workPhase.c) : undefined, undefined)]] : [], cmp),
    m.set.ofArray(legacy ? [handle] : [], cmp))
  return { handle, work, completion, projection, reason: (r) => new m.facts.HandleAbandonReason(r, []),
    ref: (r) => m.id.BlobRefModule_create(r), digest: (d) => m.id.BlobDigestModule_create(d) }
}
const B = mk(base), C = mk(cand)
const show = (r) => JSON.stringify(r.tag === 0 ? { ok: norm(r.fields[0]) } : { error: norm(r.fields[0]) })

const cases = []
for (const ph of phases) {
  for (const c of completions) cases.push(['complete', (m, X) => m.lp.HandleProjection_complete(X.handle, X.completion(c), X.projection(ph, null, false))])
  for (const r of reasons) cases.push(['abandon', (m, X) => m.lp.HandleProjection_abandon(X.handle, X.reason(r), X.projection(ph, null, false))])
  cases.push(['retire', (m, X) => m.lp.HandleProjection_retire(X.handle, X.projection(ph, null, true))])
  for (const er of ['r1', 'r2']) for (const ed of ['d1', 'd2'])
    cases.push(['rejectFalseCompletion', (m, X) => m.lp.HandleProjection_rejectFalseCompletion(X.handle, X.ref(er), X.digest(ed), X.projection(ph, null, false))])
  for (const legacy of [false, true]) {
    for (const c of completions) cases.push(['completeWork', (m, X) => m.lp.HandleProjection_completeWork(X.work, X.completion(c), X.projection({ p: 'Active' }, ph, legacy))])
    for (const r of reasons) cases.push(['abandonWork', (m, X) => m.lp.HandleProjection_abandonWork(X.work, X.reason(r), X.projection({ p: 'Active' }, ph, legacy))])
    cases.push(['voidWork', (m, X) => m.lp.HandleProjection_voidWork(X.work, X.projection({ p: 'Active' }, ph, legacy))])
    for (const id of ['', '  ', 'k']) for (const c of completions)
      cases.push(['consumeWork', (m, X) => m.lp.HandleProjection_consumeWork(X.work, id, X.completion(c), X.projection({ p: 'Active' }, ph, legacy))])
  }
}
let comparisons = 0, differences = 0
const outcomes = {}
for (const [name, f] of cases) {
  const b = show(f(base, B)), c = show(f(cand, C))
  comparisons++
  if (b !== c) { differences++; if (differences <= 3) console.error('DIFF', name, '\n base', b.slice(0, 400), '\n cand', c.slice(0, 400)) }
  const o = JSON.parse(b)
  const key = `${name} ${o.ok ? 'ok' : Object.keys(o.error)[0]}`
  outcomes[key] = (outcomes[key] ?? 0) + 1
}
console.log(JSON.stringify({ comparisons, differences, outcomes }, null, 1))
