// CUT-14 ② end-to-end differential: the pre-cut host surfaces (old dist snapshot) against the post-cut host
// surfaces (new dist, whose Projection/FailureBudget now adapt wanxiangli/failure-budget). Same random op
// sequences as diff-budget.mjs, driven through ProviderFailureSurface.{budget, providerFailureProjection} on both sides.
// usage: node diff-host.mjs <seed> <runs> <old ProviderFailureSurface.js> <new ProviderFailureSurface.js>
import { pathToFileURL } from 'node:url'
const [seedArg, runsArg, oldPath, newPath] = process.argv.slice(2)
const A = await import(pathToFileURL(oldPath).href)
const B = await import(pathToFileURL(newPath).href)
let s = Number(seedArg) >>> 0 || 1
const rnd = () => ((s = (Math.imul(s ^ (s >>> 15), 0x2c1b3c6d) + 0x9e3779b9) >>> 0) / 2 ** 32)
const pick = xs => xs[Math.floor(rnd() * xs.length)]
const runs = ['lr-a', 'lr-b', ''], roots = ['root-1', 'root-2'], sessions = ['s1', 's2', '', 'a\u001fb']
const J = JSON.stringify
const read = (X, h) => X.providerFailureProjection.read(h)
let mismatches = 0, first = null, steps = 0
const seen = {}
const bump = k => (seen[k] = (seen[k] ?? 0) + 1)
for (let run = 0; run < Number(runsArg) && !first; run++) {
  const lr = pick(runs), root = pick(roots)
  let ha = A.providerFailureProjection.forAuthority(lr, root), hb = B.providerFailureProjection.forAuthority(lr, root)
  const pool = 4 + Math.floor(rnd() * 60), len = 1 + Math.floor(rnd() * 80)
  for (let i = 0; i < len && !first; i++) {
    steps++
    const r = rnd(); let o, n, op
    if (r < 0.7) {
      op = 'applyFailure'
      const ident = { session: pick(sessions), run: rnd() < 0.93 ? lr : pick(runs), root: rnd() < 0.93 ? root : pick(roots), attempt: `p${Math.floor(rnd() * pool)}` }
      const cur = read(A, ha).failures
      const count = rnd() < 0.85 ? cur + 1 : pick([cur, cur + 2, 0, cur + 1, Math.max(0, cur - 1)])
      const ra = A.providerFailureProjection.applyFailure(ident, count, ha), rb = B.providerFailureProjection.applyFailure(ident, count, hb)
      o = ra.ok ? { ok: true, v: read(A, ra.value) } : { ok: false, e: ra.error }
      n = rb.ok ? { ok: true, v: read(B, rb.value) } : { ok: false, e: rb.error }
      bump(o.ok ? 'advanced' : o.e)
      if (ra.ok) ha = ra.value
      if (rb.ok) hb = rb.value
      if (A.budget.dedupeKey(ident) !== B.budget.dedupeKey(ident)) { o = { key: A.budget.dedupeKey(ident) }; n = { key: B.budget.dedupeKey(ident) } }
    } else if (r < 0.715) {
      op = 'applyExhausted'; ha = A.providerFailureProjection.applyExhausted(ha); hb = B.providerFailureProjection.applyExhausted(hb); o = read(A, ha); n = read(B, hb); bump(op)
    } else if (r < 0.80) {
      op = 'recordSuccess'; ha = A.providerFailureProjection.recordSuccess(ha); hb = B.providerFailureProjection.recordSuccess(hb); o = read(A, ha); n = read(B, hb); bump(op)
    } else {
      op = 'mayRetry'; const limit = pick([0, 1, 2, 3, 5, 12, 12, 12, 40]); const c = Math.floor(rnd() * 20)
      o = [A.providerFailureProjection.mayRetry(limit, ha), A.budget.verdict(limit, { failures: c })]
      n = [B.providerFailureProjection.mayRetry(limit, hb), B.budget.verdict(limit, { failures: c })]
      bump(o[0] ? 'mayRetry:true' : 'mayRetry:false')
    }
    if (J(o) !== J(n)) { mismatches++; first ??= { run, i, op, old: o, new: n } }
  }
}
console.log(J({ seed: Number(seedArg), runs: Number(runsArg), steps, mismatches, first, seen }))
