// CUT-10 differential: old host BorrowingCapacity (argv[2] = master-built dist root) vs the new host
// adapter over wanxiangli/borrowing (argv[3] = candidate dist root). Pure capacity decisions: no model
// request, no session, no I/O. Compares every return value, thrown message, promise settlement,
// scheduler `running` snapshot and the full invariant snapshot after every operation.
import { pathToFileURL } from 'node:url'
const [oldDist, newDist, seedArg, runsArg] = process.argv.slice(2)
const imp = (p) => import(pathToFileURL(p).href)
const fn = (mod, prefix) => { const k = Object.keys(mod).find((n) => n === prefix || n.startsWith(prefix + '_')); if (!k) throw new Error('missing ' + prefix); return mod[k] }
async function load(root, legacy) {
  const B = await imp(`${root}/OpenCode/Host/ModelCapacity/Borrowing.js`)
  const M = await imp(`${root}/OpenCode/Host/ModelCapacity/Model.js`)
  const S = await imp(`${root}/fable_modules/fable-library-js.5.13.0/Set.js`)
  const L = legacy ? await imp(`${root}/OpenCode/Host/ModelCapacity/Ledger.js`) : null
  const cmp = { Compare: (a, b) => (a < b ? -1 : a > b ? 1 : 0) }
  const ctor = Object.keys(B).find((n) => n.startsWith('BorrowingCapacity$1_$ctor'))
  const m = (name) => fn(B, 'BorrowingCapacity$1__' + name)
  return {
    target: (model, reasoning) => new M.ModelRoutingTarget(model, reasoning),
    make: (providerOf, same) => legacy ? B[ctor](fn(L, 'CapacityLedger$1_$ctor')(), providerOf, same) : B[ctor](providerOf, same),
    set: (xs) => S.ofArray(xs, cmp),
    credit: (c) => String(M.CapacityCreditIdModule_value(c)),
    RouteFresh: m('RouteFresh'), ReserveFresh: m('ReserveFresh'), AdoptReservation: m('AdoptReservation'),
    ContinueExecution: m('ContinueExecution'), ReleaseSession: m('ReleaseSession'), ReleasePhysical: m('ReleasePhysical'),
    EnterStep: m('EnterStep'), EndStep: m('EndStep'), SuppressStep: m('SuppressStep'), ExactCredit: m('ExactCredit'),
    InvariantSnapshot: m('InvariantSnapshot'), Snapshot: m('Snapshot'), Fail: m('Fail'),
  }
}
import { existsSync } from 'node:fs'
const legacyOf = (root) => existsSync(`${root}/OpenCode/Host/ModelCapacity/Ledger.js`)
const O = await load(oldDist, legacyOf(oldDist)), N = await load(newDist, legacyOf(newDist))
let seed = Number(seedArg ?? 1)
const rnd = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 }
const pick = (xs) => xs[Math.floor(rnd() * xs.length)]
const sessions = ['s1', 's2', 's3', 's4']
const physicals = ['p1', 'p2', 'p3']
const models = [['a/x', 'none'], ['a/x', 'high'], ['a/y', 'none'], ['b/z', 'none']]
const runs = ['r1', 'r2', 'r3', 'r4']
const json = (v) => JSON.stringify(v, (k, x) => (typeof x === 'bigint' ? String(x) : x))
const tagName = (u) => u.cases()[u.tag]
const view = (t) => (t == null ? null : [t.Model, t.Reasoning])
function policy(L, spec) {
  // spec: {caps: {a: n, b: m}, prefs: [index...]} — pure function of running.
  return (running) => {
    const used = {}
    for (const t of running) { const p = t.Model.slice(0, t.Model.indexOf('/')); used[p] = (used[p] ?? 0) + 1 }
    for (const i of spec.prefs) { const [m, r] = models[i]; const p = m.slice(0, m.indexOf('/')); if ((used[p] ?? 0) < spec.caps[p]) return L.target(m, r) }
    return undefined
  }
}
function mk(L) {
  const providerOf = (t) => t.Model.slice(0, t.Model.indexOf('/'))
  const same = (a, b) => a.Model === b.Model && a.Reasoning === b.Reasoning
  return { L, c: L.make(providerOf, same), promises: [] }
}
function track(S, p) {
  const rec = { s: 'pending' }
  p.then(() => { rec.s = 'resolved' }, (e) => { rec.s = 'rejected:' + (e?.message ?? String(e)) })
  S.promises.push(rec)
  return 'promise#' + (S.promises.length - 1)
}
function step(S, op, a) {
  const { L, c } = S
  const tgt = L.target(...models[a.ti])
  switch (op) {
    case 'route': return view(L.RouteFresh(c, a.s, a.old ?? undefined, a.p, a.lender ?? undefined, policy(L, a.spec), a.retire))
    case 'reserve': return view(L.ReserveFresh(c, a.s, a.lender ?? undefined, policy(L, a.spec)))
    case 'adopt': L.AdoptReservation(c, a.s, a.p, tgt); return 'ok'
    case 'continue': return view(L.ContinueExecution(c, a.s, a.old ?? 'p0', a.p, tgt))
    case 'releaseSession': return tagName(L.ReleaseSession(c, a.s))
    case 'releasePhysical': return tagName(L.ReleasePhysical(c, a.s, a.p))
    case 'enter': {
      const spec = a.spec
      const tryOrdinary = (running) => policy(L, spec)(running) !== undefined
      return track(S, L.EnterStep(c, a.s, a.p, tgt, L.set(a.fence), tryOrdinary, a.key ?? undefined))
    }
    case 'end': L.EndStep(c, a.s, a.p, a.run); return 'ok'
    case 'suppress': L.SuppressStep(c, a.s, a.p); return 'ok'
    case 'credit': return L.credit(L.ExactCredit(c, a.s, a.p))
    case 'fail': L.Fail(c, new Error('boom')); return 'ok'
  }
}
const observe = (S) => json([S.L.Snapshot(S.c).map(view), S.L.InvariantSnapshot(S.c), S.promises.map((p) => p.s)])
const ops = ['route', 'route', 'route', 'reserve', 'adopt', 'continue', 'releaseSession', 'releasePhysical', 'releasePhysical',
  'enter', 'enter', 'enter', 'enter', 'end', 'end', 'suppress', 'credit', 'credit', 'fail']
let total = 0, mismatches = 0, first = null
const seen = {}
const nRuns = Number(runsArg ?? 500), len = Number(process.env.LEN ?? 60)
for (let run = 0; run < nRuns; run++) {
  const A = mk(O), B = mk(N)
  for (let k = 0; k < len; k++) {
    total++
    let op = pick(ops)
    if (op === 'fail' && rnd() < 0.85) op = 'end'
    const a = {
      s: pick(sessions), p: pick(physicals), old: rnd() < 0.5 ? pick(physicals) : null,
      lender: rnd() < 0.5 ? pick(sessions) : null, retire: rnd() < 0.2 ? false : (rnd() < 0.5 ? true : undefined),
      ti: Math.floor(rnd() * models.length),
      spec: { caps: { a: 1 + Math.floor(rnd() * 3), b: 1 + Math.floor(rnd() * 2) }, prefs: [0, 1, 2, 3].sort(() => rnd() - 0.5) },
      fence: runs.filter(() => rnd() < 0.4), key: rnd() < 0.8 ? pick(['k1', 'k2', 'k3', '']) : null, run: pick(runs),
    }
    let ra, rb
    try { ra = json(step(A, op, a)) } catch (e) { ra = 'throw ' + e.message }
    try { rb = json(step(B, op, a)) } catch (e) { rb = 'throw ' + e.message }
    await new Promise((r) => setImmediate(r))
    const key = op + ':' + (ra.startsWith('throw') ? 'throw' : ra.startsWith('"promise') ? 'promise' : ra === 'null' ? 'null' : 'value')
    seen[key] = (seen[key] ?? 0) + 1
    const oa = observe(A), ob = observe(B)
    if (ra !== rb || oa !== ob) { mismatches++; if (!first) first = { run, k, op, a, ra, rb, oa: oa.slice(0, 1500), ob: ob.slice(0, 1500) }; break }
  }
}
const states = {}
console.log(JSON.stringify({ seed: Number(seedArg ?? 1), runs: nRuns, total, mismatches, first, seen }))
