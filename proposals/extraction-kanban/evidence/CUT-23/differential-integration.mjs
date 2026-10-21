/**
 * CUT-23 ②: Change integration law, old host code vs a candidate.
 * Usage: node differential-integration.mjs <base-dist> <candidate-dist> [cases]
 *
 * A. Pure laws through the public Change surface: classifyRebasedCandidate, classifyPublishClaim and
 *    worktreeReconciliationDecision over a seeded corpus drawn from a tiny alphabet (so equalities collide).
 * B. The whole OrchestratorProgram: every scripted observeRelayProgram scenario (fresh, rebase conflict,
 *    target moved, CAS miss, stale certificate, reentry ×12 …) on both dists, deep-compared.
 * C. Worktree sweep: OrchestratorSweep.sweepStaleArtifacts of both dists driven by the same fake GitPort
 *    (random worktree records incl. the main tree and detached trees, random manager branches, random
 *    active jobs, injected list/remove/delete failures); the exact removal/deletion call sequence and the
 *    result are compared.
 */
import path from 'node:path'
import { pathToFileURL } from 'node:url'

const [baseDist, candDist, n = '20000'] = process.argv.slice(2)
const imp = (dist, rel) => import(pathToFileURL(path.resolve(dist, rel)).href)
const load = async (dist) => ({
  change: await imp(dist, 'Change/Surface.js'),
  sweep: await imp(dist, 'Change/Host/WorktreeCleanup.js'),
  id: await imp(dist, 'Foundation/Identity.js'),
  list: await imp(dist, 'fable_modules/fable-library-js.5.13.0/List.js'),
  result: await imp(dist, 'fable_modules/fable-library-js.5.13.0/Result.js'),
})
const base = await load(baseDist)
const cand = await load(candDist)

let seed = 0x2323
const rnd = (k) => (((seed = (Math.imul(seed, 1103515245) + 12345) & 0x7fffffff) >>> 16) % k)
const pick = (xs) => xs[rnd(xs.length)]
const show = (r) => JSON.stringify(r, (k, v) => (v && typeof v === 'object' && 'fields' in v && 'tag' in v ? { tag: v.tag, fields: v.fields } : v))
const report = {}
const samples = []
const tally = (name, a, b, input) => {
  const t = (report[name] ??= { compared: 0, differing: 0 })
  t.compared++
  if (a !== b) { t.differing++; if (samples.length < 8) samples.push({ name, input, a, b }) }
}
const capture = (f) => { try { return show(f()) } catch (e) { return 'throw:' + e.message } }

// A. pure laws
const C = ['c1', 'c2', 't1', 't2']
for (let i = 0; i < Number(n); i++) {
  const head = rnd(5) === 0 ? null : pick(C)
  const r = pick(C), e = pick(C)
  tally('classifyRebasedCandidate', capture(() => base.change.classifyRebasedCandidate(head, r, e)), capture(() => cand.change.classifyRebasedCandidate(head, r, e)), { head, r, e })
  tally('classifyPublishClaim', capture(() => base.change.classifyPublishClaim(head, r, e)), capture(() => cand.change.classifyPublishClaim(head, r, e)), { head, r, e })
  const J = ['j1', 'j2'], P = ['/w/j1', '/w/j2'], I = ['manager/j1', 'manager/j2', 'main']
  const kind = pick(['NoDurableEffect', 'CreatedReceipt', 'RequestedConflict', 'RequestedEntries', 'RequestedQueryFailure'])
  const evidence = { kind, jobId: pick(J), path: pick(P) }
  if (kind === 'RequestedEntries') evidence.entries = Array.from({ length: rnd(4) }, () => ({ path: pick(P), identity: rnd(4) === 0 ? null : pick(I) }))
  if (kind === 'RequestedQueryFailure') evidence.error = pick(['boom', ''])
  const args = [pick(J), pick(I), pick(P), evidence]
  tally('worktreeReconciliationDecision', capture(() => base.change.worktreeReconciliationDecision(...args)), capture(() => cand.change.worktreeReconciliationDecision(...args)), args)
}

// B. the whole program, scripted
const scenarios = ['fresh', 'rebase-conflict', 'target-moved', 'cas-miss', 'rebase-reuse-old-cert', 'stale-certificate',
  'artifact-conflict', 'retired', 'cleanup-failed', 'cancelled-program', 'reentry-valid', 'reentry-wrong-snapshot',
  'reentry-missing-rebased', 'reentry-claim-snapshot-mismatch', 'reentry-claim-target-mismatch', 'reentry-candidate-moved',
  'reentry-landed-mismatch', 'reentry-gate-cancelled', 'reentry-published-append-failed', 'reentry-cleanup-failed',
  'reentry-published-unsettled', 'reentry-stale-claim-superseded']
const scenarioResults = {}
for (const s of scenarios) {
  const run = async (m) => { try { return show(await m.change.observeRelayProgram(s)) } catch (e) { return 'throw:' + e.message } }
  const a = await run(base), b = await run(cand)
  tally('observeRelayProgram', a, b, s)
  scenarioResults[s] = a === b ? 'identical' : 'DIFFERENT'
}

// C. sweep
const sweepOnce = async (m, world) => {
  const calls = []
  const ok = (v) => new m.result.FSharpResult$2(0, [v])
  const err = (e) => new m.result.FSharpResult$2(1, [e])
  const git = {
    ListWorktrees: async () => world.listFails ? err('list boom')
      : ok(m.list.ofArray(world.entries.map(([p, b]) => [m.id.WorktreePathModule_create(p), b === null ? undefined : m.id.WorktreeIdentityModule_create(b)]))),
    ListManagerBranches: async () => world.branchFails ? err('branch boom') : ok(m.list.ofArray(world.branches.map((b) => m.id.WorktreeIdentityModule_create(b)))),
    RemoveWorktree: async (p) => { const v = m.id.WorktreePathModule_value(p); calls.push('rm ' + v); return world.removeFails === v ? err('rm boom') : ok(undefined) },
    DeleteBranch: async (b) => { const v = m.id.WorktreeIdentityModule_value(b); calls.push('del ' + v); return world.deleteFails === v ? err('del boom') : ok(undefined) },
  }
  const jobs = m.list.ofArray(world.owned.map((o) => ({ WorktreeIdentity: m.id.WorktreeIdentityModule_create(o) })))
  let outcome
  try { outcome = show(await m.sweep.sweepStaleArtifacts(git, jobs)) } catch (e) { outcome = 'throw:' + e.message }
  return JSON.stringify({ calls, outcome })
}
const B = ['refs/heads/main', 'refs/heads/manager/j1', 'refs/heads/manager/j2', 'refs/heads/manager/j12', 'refs/heads/feature/x', 'manager/j1', null]
const S = ['manager/j1', 'manager/j2', 'manager/j12', 'refs/heads/manager/j2', 'main']
for (let i = 0; i < Math.max(2000, Number(n) / 10); i++) {
  const entries = Array.from({ length: rnd(5) }, (_, k) => [k === 0 ? '/repo' : '/repo/.wt/' + k, pick(B)])
  const world = {
    entries,
    branches: Array.from({ length: rnd(4) }, () => pick(S)),
    owned: Array.from({ length: rnd(3) }, () => pick(['manager/j1', 'manager/j2', 'manager/j', 'refs/heads/manager/j1'])),
    listFails: rnd(15) === 0, branchFails: rnd(15) === 0,
    removeFails: rnd(6) === 0 ? pick(entries.map(([p]) => p).concat(['none'])) : null,
    deleteFails: rnd(6) === 0 ? pick(S) : null,
  }
  tally('sweepStaleArtifacts', await sweepOnce(base, world), await sweepOnce(cand, world), world)
}

console.log(JSON.stringify({ report, scenarios: scenarioResults, samples }, null, 2))
process.exitCode = Object.values(report).some((t) => t.differing) ? 1 : 0
