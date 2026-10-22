// CUT-11 differential (sub-scopes 1 and 4: ledger law and run-boundary reset): the host claim ledger (base dist: hand-written scope/key/sequence,
// settlement edits and occasion admission) vs the candidate dist (wanxiangli/dispatch behind
// typed projections). Same seeded random op sequences on both; after every op the whole
// projection and every query are compared. Pure functions; no effects.
// Usage: node differential.mjs <base-root> <cand-root> [sequences] [steps]
import assert from 'node:assert/strict'
import { createHash } from 'node:crypto'
const [baseRoot, candRoot, seqArg, stepArg] = process.argv.slice(2)
const SEQUENCES = Number(seqArg ?? 3000), STEPS = Number(stepArg ?? 40)
const load = async (root) => {
  const d = `${root}/dist`
  const fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return {
    id: await import(`${d}/Foundation/Identity.js`),
    pi: await import(`${d}/Participant/Persona/Identity.js`),
    origin: await import(`${d}/Interaction/Authority/Origin.js`),
    model: ((r) => new Proxy(r, { get: (t, k) => t[k] ?? t[`PromptAuthority_${String(k)}`] }))(await import(`${d}/Interaction/Authority/Model.js`)),
    run: ((r) => new Proxy(r, { get: (t, k) => t[k] ?? t[`PromptAuthorityRun_${String(k)}`] }))(await import(`${d}/Interaction/Authority/Run.js`)),
    map: await import(`${fl}/Map.js`),
  }
}
const base = await load(baseRoot), cand = await load(candRoot)
const ok = (r) => { if (r.tag !== 0) throw new Error('setup: ' + JSON.stringify(r)); return r.fields[0] }
const sha = (t) => createHash('sha256').update(t).digest('hex')

const world = (m) => {
  const S = (v) => m.id.SessionIdModule_create(v), R = (v) => m.id.LogicalRunIdModule_create(v)
  const A = (v) => m.id.AuthorityRootUserMessageIdModule_create(v), P = (v) => m.id.PhysicalUserMessageIdModule_create(v)
  const K = (v) => m.id.PromptKeyModule_create(v), T = (v) => m.id.TransportReceiptModule_create(v)
  const Q = (v) => m.id.RepairRequestIdModule_create(v), PR = (v) => m.id.ProviderRunIdentityModule_create(v)
  const C = (tag) => new m.origin.PromptContinuationKind(tag, [])
  const Human = new m.origin.PromptRootAuthorityKind(0, []), Agent = new m.origin.PromptRootAuthorityKind(1, [])
  const origins = [new m.origin.PromptOrigin(0, [Human]), new m.origin.PromptOrigin(0, [Agent]), ...[0, 1, 2, 3, 4, 5].map((t) => new m.origin.PromptOrigin(1, [C(t)])), new m.origin.PromptOrigin(2, []), new m.origin.PromptOrigin(3, [])]
  const ident = ok(m.pi.ParticipantIdentityModule_resolveAtRoot('manager'))
  const profiles = ['s1', 's2'].flatMap((s) => ['run-1', 'run-2'].map((r) => ok(m.model.createAuthorityExecutionProfile(S(s), R(r), A(`root-${r}`), Human, ident))))
  return { S, R, A, P, K, T, Q, PR, C, origins, profiles }
}
const wb = world(base), wc = world(cand)

let seed = 0x2545f491
const rnd = (n) => { seed ^= seed << 13; seed >>>= 0; seed ^= seed >>> 17; seed ^= seed << 5; seed >>>= 0; return seed % n }
const norm = (v) => JSON.parse(JSON.stringify(v ?? null))
const attempt = (f) => { try { return { value: norm(f()) } } catch (e) { return { threw: String(e?.message ?? e) } } }

let compared = 0, ops = 0
const boundaries = {}
const tally = {}, diffs = []
const check = (label, f) => {
  const b = attempt(() => f(base, wb, pb)), c = attempt(() => f(cand, wc, pc))
  compared++
  try { assert.deepEqual(c, b) } catch { if (diffs.length < 50) diffs.push({ label, base: b, cand: c }); else diffs.length++ }
}
let pb, pc
const keys = ['k0', 'k1', 'k2', 'k3', 'k4', 'k5']
const digests = ['d1', 'd2', 'gate\u001fprun-1', 'q1\u001fprun-1\u001frepair']
for (let s = 0; s < SEQUENCES; s++) {
  pb = base.model.empty; pc = cand.model.empty
  for (let step = 0; step < STEPS; step++) {
    const op = rnd(8), key = keys[rnd(keys.length)], prof = rnd(4), oi = rnd(10), dg = digests[rnd(digests.length)], phys = `p${rnd(6)}`, rc = `accepted-${rnd(3)}`
    const mk = (m, w) => {
      const p = w.profiles[prof]
      const claim = m.run.claimContinuation(w.K(key), m.model.AuthorityExecutionProfile__get_SessionId(p), w.C(rnd2), p, dg)
      if (oi < 6) return claim
      // other origins / a root claim without a logical run: reshape the template
      return Object.assign(Object.create(Object.getPrototypeOf(claim)), claim, { Origin: w.origins[oi === 9 ? 1 : oi], LogicalRunId: oi === 9 ? undefined : claim.LogicalRunId, AuthorityRootUserMessageId: oi === 9 ? undefined : claim.AuthorityRootUserMessageId })
    }
    const rnd2 = rnd(6)
    const name = ['register', 'submit', 'accept', 'abandon', 'register', 'registerAuthority', 'close', 'question'][op]
    tally[name] = (tally[name] ?? 0) + 1
    const apply = (m, w, p) => {
      if (name === 'register') return m.run.registerClaim(mk(m, w), p)
      if (name === 'submit') return m.run.submitClaim(w.K(key), w.T(rc), p)
      if (name === 'accept') return m.run.acceptClaim(w.K(key), w.P(phys), p)
      if (name === 'abandon') return m.run.abandonClaim(w.K(key), p)
      // an open user question (opaque placeholder value): only the run-boundary reset may clear it
      if (name === 'question') return Object.assign(Object.create(Object.getPrototypeOf(p)), p, { UserQuestions: m.map.add(`tc-${rnd2}`, { open: key }, p.UserQuestions) })
      // run boundaries: a new root or an exact closure resets the run-scoped dispatch resources
      const pf = w.profiles[prof]
      const r = name === 'registerAuthority'
        ? m.run.registerAuthority(pf, p)
        : m.run.closeAuthority(m.model.AuthorityExecutionProfile__get_LogicalRunId(pf), m.model.AuthorityExecutionProfile__get_AuthorityRootUserMessageId(pf), p)
      if (r.tag === 0 && m === base) boundaries[name] = (boundaries[name] ?? 0) + 1
      return r.tag === 0 ? r.fields[0] : p
    }
    const nb = attempt(() => (pb = apply(base, wb, pb))), nc = attempt(() => (pc = apply(cand, wc, pc)))
    ops++; compared++
    try { assert.deepEqual(nc, nb) } catch { if (diffs.length < 50) diffs.push({ label: `op ${name}`, s, step, base: nb, cand: nc }); else diffs.length++ }
    // queries on the resulting projection
    for (let q = 0; q < 3; q++) {
      const pi = rnd(4), oj = rnd(10), dj = digests[rnd(digests.length)], ck = rnd(6), seq = 1 + rnd(4), noRun = rnd(3) === 0
      check('scope+next', (m, w, p) => {
        const pf = w.profiles[pi]
        const sid = m.model.AuthorityExecutionProfile__get_SessionId(pf), run = noRun ? undefined : m.model.AuthorityExecutionProfile__get_LogicalRunId(pf)
        const scope = m.model.claimScopeDigest(sid, run, w.origins[oj], dj)
        return { scope, next: m.model.nextClaimSequence(scope, p), key: m.model.derivePromptKey(sha, sid, run, noRun ? undefined : m.model.AuthorityExecutionProfile__get_AuthorityRootUserMessageId(pf), w.origins[oj], dj, seq) }
      })
      check('gate', (m, w, p) => {
        const pf = w.profiles[pi]
        const sid = m.model.AuthorityExecutionProfile__get_SessionId(pf), run = m.model.AuthorityExecutionProfile__get_LogicalRunId(pf)
        return { admitted: m.model.gateNudgeAlreadyAdmitted(sid, run, w.C(ck), 'gate', w.PR('prun-1'), p), physical: m.model.gateNudgeAcceptedPhysical(sid, w.C(ck), 'gate', w.PR('prun-1'), p) }
      })
      check('questions', (m, w, p) => [...p.UserQuestions].length)
      check('repair', (m, w, p) => {
        const pf = w.profiles[pi]
        return m.model.repairAlreadyClaimed(m.model.AuthorityExecutionProfile__get_SessionId(pf), m.model.AuthorityExecutionProfile__get_LogicalRunId(pf), w.Q('q1'), w.PR('prun-1'), 'repair', p)
      })
    }
  }
}
const stats = { sequences: SEQUENCES, steps: STEPS, ops, compared, differences: diffs.length, opMix: tally, acceptedBoundaries: boundaries,
  finalShape: { pending: [...pb.PendingClaims].length, occasions: [...pb.AcceptedDispatches].length, landings: [...pb.PhysicalLandings].length } }
console.log(JSON.stringify(stats, null, 1))
if (diffs.length) { console.log(JSON.stringify(diffs.slice(0, 3), null, 1)); process.exit(1) }
