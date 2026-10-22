// CUT-13 restart sub-loop differential: the hand-written ChildRecovery.resolveChild (base dist) vs
// the candidate (wanxiangli/handle-lifecycle `recoverChild` behind the same F# signature).
// Every durable evidence (Unknown / Active / CompletedAwaitingJoin / Abandoned x 3 reasons / Retired)
// x every snapshot (Missing / Active / Terminal completed|failed x empty|non-empty body / Unreadable)
// x every ordered observation list of length <= 3 over the six Host observation kinds.
// Usage: node recovery-differential.mjs <base-root> <cand-root>
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`, fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return {
    cr: await import(`${d}/Execution/Delegation/Fork/ChildRecovery.js`),
    facts: await import(`${d}/Execution/Delegation/Facts.js`),
    id: await import(`${d}/Foundation/Identity.js`),
    list: await import(`${fl}/List.js`),
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
const durables = [['Unknown'], ['Active'], ['Completed'], ['Abandoned', 0], ['Abandoned', 1], ['Abandoned', 2], ['Retired']]
const snapshots = [['Missing'], ['Active'], ['Terminal', 'completed', 'body'], ['Terminal', 'completed', ''], ['Terminal', 'failed', 'oops'], ['Terminal', 'failed', ''], ['Unreadable', 'io']]
const kinds = [0, 1, 2, 3, 4, 5]
const lists = [[]]
for (const a of kinds) { lists.push([a]); for (const b of kinds) { lists.push([a, b]); for (const c of kinds) lists.push([a, b, c]) } }

const mk = (m) => {
  const handle = new m.id.HandleId(0, [m.id.AgentHandleIdModule_create('h')])
  const child = m.id.SessionIdModule_create('child')
  const proven = (k, body) => (k === 'completed' ? m.cr.TerminalEvidenceModule_completed : m.cr.TerminalEvidenceModule_failed)('h', handle, child, body)
  const durable = ([k, r]) => {
    const D = m.cr.DurableHandleEvidence
    if (k === 'Unknown') return new D(0, [])
    if (k === 'Active') return new D(1, [])
    if (k === 'Completed') return new D(2, [m.cr.JoinableCompletionModule_tryFromProvenTerminal(proven('completed', 'durable-body')).fields[0]])
    if (k === 'Abandoned') return new D(3, [new m.facts.HandleAbandonReason(r, [])])
    return new D(4, [])
  }
  const snapshot = ([k, a, b]) => {
    const S = m.cr.ChildSnapshotEvidence
    if (k === 'Missing') return new S(0, [])
    if (k === 'Active') return new S(1, [])
    if (k === 'Terminal') return new S(2, [proven(a, b)])
    return new S(3, [a])
  }
  const observation = (t) => new m.cr.HostObservation(t, t === 0 ? ['raw abort'] : [])
  return (d, s, os) => JSON.stringify(norm(m.cr.resolveChild(durable(d), snapshot(s), m.list.ofArray(os.map(observation)))))
}
const B = mk(base), C = mk(cand)
let comparisons = 0, differences = 0
const outcomes = {}
for (const d of durables) for (const s of snapshots) for (const os of lists) {
  const b = B(d, s, os), c = C(d, s, os)
  comparisons++
  if (b !== c) { differences++; if (differences <= 3) console.error('DIFF', JSON.stringify({ d, s, os }), '\n base', b, '\n cand', c) }
  const key = Object.keys(JSON.parse(b))[0]
  outcomes[key] = (outcomes[key] ?? 0) + 1
}
console.log(JSON.stringify({ comparisons, differences, outcomes }, null, 1))
