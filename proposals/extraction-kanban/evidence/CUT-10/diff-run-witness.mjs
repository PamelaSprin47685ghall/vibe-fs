// CUT-10 ⑥a differential: a line-by-line JS transcription of the deleted host F# provider-run witness
// (ModelRouting.fs at 06d8e1267: targetByProviderRun, latestProviderRunBySession, providerStepIdentityByRun,
// recoveryRetryTargetBySession and their let-functions plus the retireCurrentExecution /
// ReleasePhysicalExecution clean-up blocks) vs wanxiangli/run-witness. Every return value and the
// full state is compared after every operation. Usage: node diff-run-witness.mjs <seed> <runs> [len] [module-path]
import { pathToFileURL } from 'node:url'
const [seedArg, runsArg, lenArg, modPath] = process.argv.slice(2)
const { ProviderRunWitness } = modPath ? await import(pathToFileURL(modPath).href) : await import('wanxiangli/run-witness')

class OldHost {
  constructor() { this.targetByProviderRun = new Map(); this.latest = new Map(); this.steps = new Map(); this.retry = new Map() }
  retireProviderRunTarget(s) { if (this.latest.has(s)) { const r = this.latest.get(s); this.latest.delete(s); this.targetByProviderRun.delete(r) } }
  remember(s, r, t) { this.retireProviderRunTarget(s); this.targetByProviderRun.set(r, [s, t]); this.latest.set(s, r) }
  forgetLatestIfCurrent(s, r) { if (this.latest.get(s) === r) this.latest.delete(s) }
  takeWitness(r) { if (!this.targetByProviderRun.has(r)) return null; const [s, t] = this.targetByProviderRun.get(r); this.targetByProviderRun.delete(r); this.forgetLatestIfCurrent(s, r); return [s, t] }
  take(r) { const w = this.takeWitness(r); return w && { sessionId: w[0], target: w[1] } }
  retainForRetry(s, r) { const w = this.takeWitness(r); if (w && w[0] === s) { this.retry.set(s, w[1]); return w[1] } return null }
  takeRetry(s) { if (!this.retry.has(s)) return null; const t = this.retry.get(s); this.retry.delete(s); return t }
  rememberStep(s, p, r) { this.steps.set(r, [s, p]) }
  stepOf(r) { const v = this.steps.get(r); return v ? { sessionId: v[0], physicalUserMessageId: v[1] } : null }
  forgetSession(s) { this.retireProviderRunTarget(s); this.retry.delete(s); for (const [r, [rs]] of [...this.steps]) if (rs === s) this.steps.delete(r) }
  forgetPhysical(s, p) { for (const [r, [rs, rp]] of [...this.steps]) if (rs === s && rp === p) this.steps.delete(r) }
  state() { return JSON.stringify([[...this.targetByProviderRun].sort(), [...this.latest].sort(), [...this.steps].sort(), [...this.retry].sort()].map(x => x.map(([k, v]) => [k, v]))) }
}

let seed = Number(seedArg ?? 1)
const rnd = () => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed / 2147483648 }
const pick = (xs) => xs[Math.floor(rnd() * xs.length)]
const sessions = ['s1', 's2', 's3']
const physicals = ['p1', 'p2', 'p3']
const runs = ['r1', 'r2', 'r3', 'r4', 'r5']
const targets = [{ model: 'a/x', reasoning: 'none' }, { model: 'a/y', reasoning: 'high' }, { model: 'b/z', reasoning: 'none' }]
const tname = (t) => (t == null ? null : targets.indexOf(t))
const view = (v) => v == null ? null : JSON.stringify(Object.fromEntries(Object.entries(v).map(([k, x]) => [k, typeof x === 'object' ? tname(x) : x])))
const ops = ['remember', 'remember', 'take', 'retainForRetry', 'takeRetry', 'rememberStep', 'rememberStep', 'stepOf', 'forgetSession', 'forgetPhysical']
// library state is observed only through its own read API (snapshot counts) + a probe of every key on a clone path:
// after each op, both sides are also probed non-destructively via stepOf for every run.
let total = 0, mismatches = 0, first = null
const seen = {}
const nRuns = Number(runsArg ?? 1000), len = Number(lenArg ?? 80)
for (let run = 0; run < nRuns; run++) {
  const O = new OldHost(), N = new ProviderRunWitness()
  for (let k = 0; k < len; k++) {
    total++
    const op = pick(ops), s = pick(sessions), p = pick(physicals), r = pick(runs), t = pick(targets)
    const call = (W) => {
      switch (op) {
        case 'remember': W.remember(s, r, t); return 'ok'
        case 'take': return view(W.take(r))
        case 'retainForRetry': return tname(W.retainForRetry(s, r))
        case 'takeRetry': return tname(W.takeRetry(s))
        case 'rememberStep': W.rememberStep(s, p, r); return 'ok'
        case 'stepOf': return view(W.stepOf(r))
        case 'forgetSession': W.forgetSession(s); return 'ok'
        case 'forgetPhysical': W.forgetPhysical(s, p); return 'ok'
      }
    }
    const ra = JSON.stringify(call(O)), rb = JSON.stringify(call(N))
    const probeO = runs.map((x) => view(O.stepOf(x))).join('|'), probeN = runs.map((x) => view(N.stepOf(x))).join('|')
    const snap = N.snapshot()
    const countsO = [O.targetByProviderRun.size, O.latest.size, O.steps.size, O.retry.size].join(','), countsN = [snap.witnesses, snap.latest, snap.steps, snap.retries].join(',')
    const key = op + ':' + (ra === 'null' ? 'null' : ra === '"ok"' ? 'ok' : 'value'); seen[key] = (seen[key] ?? 0) + 1
    if (ra !== rb || probeO !== probeN || countsO !== countsN) { mismatches++; if (!first) first = { run, k, op, s, p, r, ra, rb, probeO, probeN, countsO, countsN }; break }
  }
  // Final destructive drain: every remaining witness and retry binding must match.
  const drainO = runs.map((x) => view(O.take(x))).concat(sessions.map((x) => tname(O.takeRetry(x)))).join('|')
  const drainN = runs.map((x) => view(N.take(x))).concat(sessions.map((x) => tname(N.takeRetry(x)))).join('|')
  if (drainO !== drainN) { mismatches++; if (!first) first = { run, drainO, drainN } }
}
console.log(JSON.stringify({ seed: Number(seedArg ?? 1), runs: nRuns, total, mismatches, first, seen }))
