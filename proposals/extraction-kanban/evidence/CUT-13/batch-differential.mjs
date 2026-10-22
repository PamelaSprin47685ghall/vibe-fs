// CUT-13 SyncDelegate sub-loop differential: the hand-written SyncDelegateStoreOps batch admission and
// observed-batch derivation (base dist) vs the candidate (wanxiangli/delegate-batch behind the same
// SyncDelegateCallStore members). Both stores run the same seeded random operation sequences —
// Admit (ungrouped, valid, empty, duplicate-order and foreign-current batches over two ProviderRuns),
// ReleaseAdmission, ObserveProviderToolCall and TryObservedBatch over 2 scopes x 2 roles — and every
// answer (admission tag, admitted invocation order, rejection text, observed batch) is compared.
// Usage: node batch-differential.mjs <base-root> <cand-root> [sequences]
const [baseRoot, candRoot, n = '20000'] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`, fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return {
    store: await import(`${d}/Execution/Delegation/SyncDelegate/Store.js`),
    model: await import(`${d}/Execution/Delegation/SyncDelegate/Model.js`),
    role: await import(`${d}/Execution/Session/SyncDelegateRole.js`),
    id: await import(`${d}/Foundation/Identity.js`),
    list: await import(`${fl}/List.js`),
  }
}
const base = await load(baseRoot), cand = await load(candRoot)
let seed = 13
const rnd = (k) => { // mulberry32
  seed = (seed + 0x6d2b79f5) | 0
  let x = Math.imul(seed ^ (seed >>> 15), 1 | seed)
  x = (x + Math.imul(x ^ (x >>> 7), 61 | x)) ^ x
  return ((x ^ (x >>> 14)) >>> 0) % k
}
const pick = (xs) => xs[rnd(xs.length)]
const calls = ['a', 'b', 'c']
const orders = [[], ['a'], ['a', 'b'], ['b', 'a'], ['a', 'b', 'c'], ['a', 'a'], ['c', 'b']]

function genOp() {
  const scope = pick(['s1', 's2']), role = pick([2, 3]), r = rnd(10)
  if (r < 6) {
    const batch = rnd(4) === 0 ? null : { run: pick(['r1', 'r2']), order: pick(orders), current: pick(calls) }
    return { op: 'admit', scope, role, batch }
  }
  if (r < 7) return { op: 'release', scope, role }
  if (r < 9) return { op: 'observe', owner: scope, run: pick(['r1', 'r2']), role, call: pick(calls) }
  return { op: 'tryObserved', owner: scope, run: pick(['r1', 'r2']), role, call: pick(calls) }
}

function runner(m) {
  const S = m.store, store = S.SyncDelegateCallStore_$ctor()
  const tcId = (s) => m.id.ToolCallIdModule_create(s)
  const role = (t) => new m.role.SyncDelegateRole(t, [])
  let counter = 0
  return (o) => {
    switch (o.op) {
      case 'admit': {
        const batch = o.batch && new m.model.SyncDelegateBatch(m.id.ProviderRunIdentityModule_create(o.batch.run), m.list.ofArray(o.batch.order.map(tcId)), tcId(o.batch.current))
        const charge = `inv${counter++}`
        const inv = new S.SyncDelegateInvocation(m.id.SessionIdModule_create(o.scope), m.model.ReuseScopeIdModule_create(o.scope), role(o.role), charge, undefined, () => null, undefined, undefined, () => false, batch ?? undefined, null, undefined)
        const a = S.SyncDelegateCallStore__Admit_Z310155D6(store, inv)
        if (a.tag === 1) return ['Ready', [...a.fields[0]].map((i) => i.Charge)]
        return a.tag === 0 ? ['Waiting'] : ['Rejected', a.fields[0]]
      }
      case 'release':
        S.SyncDelegateCallStore__ReleaseAdmission_58A5FAC5(store, m.model.ReuseScopeIdModule_create(o.scope), role(o.role))
        return ['released']
      case 'observe':
        S.SyncDelegateCallStore__ObserveProviderToolCall_Z3C9F7939(store, m.id.SessionIdModule_create(o.owner), m.id.ProviderRunIdentityModule_create(o.run), role(o.role), tcId(o.call))
        return ['observed']
      case 'tryObserved': {
        const b = S.SyncDelegateCallStore__TryObservedBatch_Z3C9F7939(store, m.id.SessionIdModule_create(o.owner), m.id.ProviderRunIdentityModule_create(o.run), role(o.role), tcId(o.call))
        return b == null ? ['none'] : [m.id.ProviderRunIdentityModule_value(b.ProviderRun), [...b.CallOrder].map((c) => c.fields[0]), b.CurrentCall.fields[0]]
      }
    }
  }
}

let compared = 0, differences = 0
const tally = {}
const samples = []
for (let s = 0; s < Number(n); s++) {
  const rb = runner(base), rc = runner(cand)
  for (let k = 0; k < 12; k++) {
    const o = genOp()
    const x = JSON.stringify(rb(o)), y = JSON.stringify(rc(o))
    compared++
    const [tag, detail] = JSON.parse(x)
    const label = tag === 'Rejected' ? `Rejected: ${detail}` : tag === 'Ready' ? `Ready x${detail.length}` : tag
    tally[label] = (tally[label] ?? 0) + 1
    if (x !== y) { differences++; if (samples.length < 5) samples.push({ sequence: s, step: k, op: o, base: x, cand: y }) }
  }
}
console.log(JSON.stringify({ sequences: Number(n), compared, differences, outcomes: tally, samples }, null, 2))
process.exit(differences === 0 ? 0 : 1)
