// CUT-10 differential: old host ExecutionAdmissionQueue (argv[2] = master-built dist root)
// vs new host adapter over wanxiangli/admission-queue (argv[3] = candidate dist root).
// Observes every return value, counters, snapshots and first settlement of every waiter.
import { pathToFileURL } from 'node:url'
const [oldDist, newDist, seedArg, runsArg, boundArg] = process.argv.slice(2)
const imp = (p) => import(pathToFileURL(p).href)
async function load(root) {
  const Q = await imp(`${root}/OpenCode/Host/ModelCapacity/Queue.js`)
  const M = await imp(`${root}/OpenCode/Host/ModelCapacity/Model.js`)
  const T = await imp(`${root}/fable_modules/fable-library-js.5.13.0/Task.js`)
  const R = await imp(`${root}/Foundation/Roles.js`)
  const P = T.TaskCompletionSource.prototype
  if (!P.__patched) {
    const sr = P.SetResult, se = P.SetException
    P.SetResult = function (v) { if (!this.__settled) this.__settled = { r: v }; return sr.call(this, v) }
    P.SetException = function (e) { if (!this.__settled) this.__settled = { e: String(e?.message ?? e) }; this.task.catch(() => {}); return se.call(this, e) }
    P.__patched = true
  }
  return { Q, M, R }
}
const O = await load(oldDist), N = await load(newDist)
let seed = Number(seedArg ?? 1)
const rnd = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 }
const pick = (xs) => xs[Math.floor(rnd() * xs.length)]
const sessions = Array.from({ length: Number(process.env.SESSIONS ?? 5) }, (_, i) => 's' + (i + 1))
const physicals = ['p1', 'p2', 'p3']
const tagName = (u) => u.cases()[u.tag]
function mk(L) {
  const owner = {}
  const counters = L.M.CapacityTransitionCounters_$ctor()
  const q = L.Q.ExecutionAdmissionQueue_$ctor_51D6D074(owner, counters)
  return { L, q, counters, nodes: [], owner }
}
let CUR = null
const nodeView = (L, n) => n == null ? null : [CUR ? CUR.nodes.indexOf(n) : -2, L.M.ExecutionAdmissionQueueNode__get_SessionId(n), L.M.ExecutionAdmissionQueueNode__get_PhysicalUserMessageId(n)]
const acq = (L, a) => a.tag === 1 ? ['Queued', nodeView(L, a.fields[0])] : [tagName(a)]
const demandView = (L, d) => d == null ? null : [Number(d.Sequence), d.SessionId, d.PhysicalUserMessageId, d.Participant]
const settled = (L, n) => { const s = L.M.ExecutionAdmissionQueueNode__get_Completion(n).__settled; return s == null ? null : s.e ? ['E', s.e] : acq(L, s.r) }
const ctr = (c) => [String(c.duplicate), String(c.stale), String(c.conflict)]
function step(S, op, args) {
  const { L, q } = S
  CUR = S
  const Q = L.Q
  switch (op) {
    case 'enqueue': {
      const r = Q.ExecutionAdmissionQueue__Enqueue_Z158A34EC(q, args.s, args.p, L.R.Role.Engineer ?? null, args.a, null, undefined, undefined)
      if (r.tag === 1 && !S.nodes.includes(r.fields[0])) S.nodes.push(r.fields[0])
      return acq(L, r)
    }
    case 'admit': {
      const n = args.i < 0 ? L.M.ExecutionAdmissionQueueNode_Create_47D5EDC4({}, S.nodes[0] ? L.M.ExecutionAdmissionQueueNode__get_NodeId(S.nodes[0]) : L.M.QueueNodeIdModule_initial, S.nodes[0] ? L.M.ExecutionAdmissionQueueNode__get_Fence(S.nodes[0]) : L.M.QueueFenceModule_initial, args.s, args.p, null) : S.nodes[args.i % Math.max(1, S.nodes.length)]
      if (n == null) return 'none'
      return tagName(Q.ExecutionAdmissionQueue__Admit_Z5B820D31(q, n, { lease: true }))
    }
    case 'take': {
      const n = S.nodes[args.i % Math.max(1, S.nodes.length)]
      if (n == null) return 'none'
      return demandView(L, Q.ExecutionAdmissionQueue__TryTake_63764E19(q, n))
    }
    case 'cancel': return tagName(Q.ExecutionAdmissionQueue__CancelSession_Z721C83C5(q, args.s))
    case 'cancelExec': return tagName(Q.ExecutionAdmissionQueue__CancelExecution_Z384F8060(q, args.s, args.p))
    case 'supersede': return tagName(Q.ExecutionAdmissionQueue__SupersedeSession_Z721C83C5(q, args.s))
    case 'fail': Q.ExecutionAdmissionQueue__Fail_229D3F39(q, new Error('boom')); return 'ok'
    case 'current': return demandView(L, Q.ExecutionAdmissionQueue__TryCurrent_Z721C83C5(q, args.s))
    case 'contains': return Q.ExecutionAdmissionQueue__ContainsSession_Z721C83C5(q, args.s)
  }
}
function observe(S) {
  const { L, q } = S
  CUR = S
  return JSON.stringify([
    L.Q.ExecutionAdmissionQueue__get_Count(q),
    L.Q.ExecutionAdmissionQueue__Snapshot(q).map((d) => demandView(L, d)),
    ctr(S.counters),
    S.nodes.map((n) => settled(L, n)),
  ])
}
let total = 0, mismatches = 0, first = null
const seen = {}
const ops = [...(process.env.HEAVY ? Array(12).fill('enqueue') : []), 'enqueue', 'enqueue', 'enqueue', 'enqueue', 'admit', 'admit', 'take', 'cancel', 'cancelExec', 'supersede', 'current', 'contains', 'fail']
const runs = Number(runsArg ?? 2000), len = Number(process.env.LEN ?? 60)
for (let run = 0; run < runs; run++) {
  const a = mk(O), b = mk(N)
  for (let k = 0; k < len; k++) {
    total++
    let op = pick(ops)
    if (op === 'fail' && (process.env.HEAVY || rnd() < 0.8)) op = 'contains'
    const args = { s: pick(sessions), p: pick(physicals), a: pick(['alice', 'bob']), i: rnd() < 0.1 ? -1 : Math.floor(rnd() * 100) }
    let ra, rb
    try { ra = JSON.stringify(step(a, op, args)) } catch (e) { ra = 'throw ' + e.message }
    try { rb = JSON.stringify(step(b, op, args)) } catch (e) { rb = 'throw ' + e.message }
    const key = op + ':' + (ra.startsWith('[') ? JSON.parse(ra)[0] : ra.slice(0, 20)); seen[key] = (seen[key] ?? 0) + 1
    const oa = observe(a), ob = observe(b)
    if (ra !== rb || oa !== ob) { mismatches++; if (!first) first = { run, k, op, args, ra, rb, oa, ob }; break }
  }
}
console.log(JSON.stringify({ seed: Number(seedArg ?? 1), runs, total, mismatches, first, seen }))
