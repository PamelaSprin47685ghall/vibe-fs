// CUT-14 ② differential: the pre-cut host failure budget + projection (compiled F# behind
// dist/Participant/Provider/Attempt/Fallback/ProviderFailureSurface.js `budget` / `providerFailureProjection`,
// run as-is from a pre-cut dist snapshot) against wanxiangli/failure-budget, over random op sequences.
// usage: node diff-budget.mjs <seed> <runs> <old ProviderFailureSurface.js> <library ProviderFailureBudget.js> [mutant]
import { pathToFileURL } from 'node:url'
const [seedArg, runsArg, oldPath, libPath, mutant] = process.argv.slice(2)
const old = await import(pathToFileURL(oldPath).href)
const { ProviderFailureBudget } = await import(pathToFileURL(libPath).href)
const lib = new ProviderFailureBudget()
const M = {
  noDedupe: (op, a) => op === 'applyFailure' ? lib.applyFailure(a[0], a[1], { ...a[2], recentFailureKeys: [] }) : null,
  window31: (op, a) => op === 'applyFailure' ? (r => r.ok ? { ok: true, state: { ...r.state, recentFailureKeys: r.state.recentFailureKeys.slice(0, 31) } } : r)(lib.applyFailure(...a)) : null,
  skipAllowed: (op, a) => op === 'applyFailure' && a[1] === a[2].consecutiveFailureCount + 2 ? lib.applyFailure(a[0], a[1] - 1, a[2]) : null,
  exhaustedOrder: (op, a) => op === 'applyFailure' && a[2].exhausted && a[2].recentFailureKeys.includes(lib.dedupeKey(a[0])) ? { ok: false, rejection: 'AlreadyObserved' } : null,
  successKeepsKeys: (op, a) => op === 'recordSuccess' ? { ...lib.recordSuccess(a[0]), recentFailureKeys: a[0].recentFailureKeys } : null,
  verdictGt: (op, a) => op === 'mayRetry' ? !a[1].exhausted && a[1].consecutiveFailureCount <= a[0] : null,
  rootIgnored: (op, a) => op === 'applyFailure' && a[0].logicalRun === a[2].logicalRun && a[0].authorityRoot !== a[2].authorityRoot ? lib.applyFailure({ ...a[0], authorityRoot: a[2].authorityRoot }, a[1], a[2]) : null,
}
const call = (op, ...a) => (mutant && M[mutant](op, a)) ?? lib[op](...a)

let s = Number(seedArg) >>> 0 || 1
const rnd = () => ((s = (Math.imul(s ^ (s >>> 15), 0x2c1b3c6d) + 0x9e3779b9) >>> 0) / 2 ** 32)
const pick = xs => xs[Math.floor(rnd() * xs.length)]
const runs = ['lr-a', 'lr-b', '']
const roots = ['root-1', 'root-2']
const sessions = ['s1', 's2', '', 'a\u001fb']
const attempts = n => `p${Math.floor(rnd() * n)}`

const viewOld = h => { const r = old.providerFailureProjection.read(h); return { logicalRun: r.logicalRun, authorityRoot: r.authorityRoot, failures: r.failures, dedupeKeys: r.dedupeKeys, exhausted: r.exhausted } }
const viewLib = st => ({ logicalRun: st.logicalRun, authorityRoot: st.authorityRoot, failures: st.consecutiveFailureCount, dedupeKeys: st.recentFailureKeys.length, exhausted: st.exhausted })
const J = JSON.stringify

let mismatches = 0, first = null, steps = 0
const seen = {}
const bump = k => (seen[k] = (seen[k] ?? 0) + 1)
for (let run = 0; run < Number(runsArg); run++) {
  const lr = pick(runs), root = pick(roots)
  let h = old.providerFailureProjection.forAuthority(lr, root)
  let st = call('forAuthority', lr, root)
  const pool = 4 + Math.floor(rnd() * 60)
  const len = 1 + Math.floor(rnd() * 80)
  for (let i = 0; i < len && !first; i++) {
    steps++
    const r = rnd()
    let o, n, op
    if (r < 0.7) {
      op = 'applyFailure'
      const ident = { sessionId: pick(sessions), logicalRun: rnd() < 0.93 ? lr : pick(runs), authorityRoot: rnd() < 0.93 ? root : pick(roots), providerRun: attempts(pool) }
      const cur = st.consecutiveFailureCount
      const count = rnd() < 0.85 ? cur + 1 : pick([cur, cur + 2, 0, cur + 1, Math.max(0, cur - 1)])
      const oldIdent = { session: ident.sessionId, run: ident.logicalRun, root: ident.authorityRoot, attempt: ident.providerRun }
      const ro = old.providerFailureProjection.applyFailure(oldIdent, count, h)
      const rl = call('applyFailure', ident, count, st)
      o = ro.ok ? { ok: true, v: viewOld(ro.value) } : { ok: false, e: ro.error }
      n = rl.ok ? { ok: true, v: viewLib(rl.state) } : { ok: false, e: rl.rejection }
      bump(o.ok ? 'advanced' : o.e)
      if (ro.ok) h = ro.value
      if (rl.ok) st = rl.state
      // key format equality on every applied identity
      if (old.budget.dedupeKey(oldIdent) !== lib.dedupeKey(ident)) { o = { key: old.budget.dedupeKey(oldIdent) }; n = { key: lib.dedupeKey(ident) } }
    } else if (r < 0.715) {
      op = 'applyExhausted'; h = old.providerFailureProjection.applyExhausted(h); st = call('applyExhausted', st); o = viewOld(h); n = viewLib(st); bump(op)
    } else if (r < 0.80) {
      op = 'recordSuccess'; h = old.providerFailureProjection.recordSuccess(h); st = call('recordSuccess', st); o = viewOld(h); n = viewLib(st); bump(op)
    } else {
      op = 'mayRetry'; const limit = pick([0, 1, 2, 3, 5, 12, 12, 12, 40])
      o = old.providerFailureProjection.mayRetry(limit, h); n = call('mayRetry', limit, st)
      const c = Math.floor(rnd() * 20)
      o = [o, old.budget.verdict(limit, { failures: c })]; n = [n, lib.verdict(limit, c)]
      bump(o[0] ? 'mayRetry:true' : 'mayRetry:false')
    }
    if (J(o) !== J(n)) { mismatches++; first ??= { run, i, op, old: o, lib: n } }
  }
  if (first && mutant) break
}
console.log(J({ seed: Number(seedArg), runs: Number(runsArg), steps, mutant: mutant ?? null, mismatches, first, seen }))
