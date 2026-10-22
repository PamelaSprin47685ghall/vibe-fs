// CUT-09 sub-scope 1 differential: the host-side root registration / exact closure / origin precedence
// (base dist) vs the wanxiangli/authority-backed candidate dist. Pure projections only; no effects.
// Usage: node differential.mjs <base-root> <cand-root>
import assert from 'node:assert/strict'
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`
  return {
    id: await import(`${d}/Foundation/Identity.js`),
    pi: await import(`${d}/Participant/Persona/Identity.js`),
    origin: await import(`${d}/Interaction/Authority/Origin.js`),
    model: await import(`${d}/Interaction/Authority/Model.js`),
    // Adding the AuthorityLaw module to Run.fs makes Fable prefix the PromptAuthorityRun exports; resolve both spellings.
    facts: await import(`${d}/Interaction/Authority/Facts.js`),
    fold: await import(`${d}/Interaction/Authority/Fold.js`),
    run: ((r) => new Proxy(r, { get: (t, k) => t[k] ?? t[`PromptAuthorityRun_${String(k)}`] }))(await import(`${d}/Interaction/Authority/Run.js`)),
  }
}
const base = await load(baseRoot), cand = await load(candRoot)
const ok = (r) => { if (r.tag !== 0) throw new Error('setup: ' + JSON.stringify(r)); return r.fields[0] }

// A deterministic universe built separately in each implementation from the same inputs.
const universe = (m) => {
  const S = (v) => m.id.SessionIdModule_create(v), R = (v) => m.id.LogicalRunIdModule_create(v)
  const A = (v) => m.id.AuthorityRootUserMessageIdModule_create(v), P = (v) => m.id.PhysicalUserMessageIdModule_create(v)
  const K = (v) => m.id.PromptKeyModule_create(v)
  const Human = new m.origin.PromptRootAuthorityKind(0, []), Agent = new m.origin.PromptRootAuthorityKind(1, [])
  const ident = (name) => ok(m.pi.ParticipantIdentityModule_resolveAtRoot(name))
  const profiles = []
  for (const s of ['s1', 's2']) for (const r of ['run-1', 'run-2']) for (const a of ['m1', 'm2'])
    for (const [kind, kl] of [[Human, 'H'], [Agent, 'A']]) for (const name of ['manager', 'engineer']) {
      const p = m.model.createAuthorityExecutionProfile(S(s), R(r), A(a), kind, ident(name))
      if (p.tag === 0) profiles.push({ label: `${s}/${r}/${a}/${kl}/${name}`, p: p.fields[0] })
    }
  // Inherited seeds: a child of each manager root.
  for (const owner of profiles.filter((x) => x.label.endsWith('/manager')).slice(0, 4)) {
    for (const childName of ['engineer', 'devops', 'manager']) {
      const seed = m.model.issueInheritedIdentitySeed(childName, owner.p)
      if (seed.tag !== 0) continue
      for (const childRun of ['run-c', 'run-d'])
        for (const [kind, kl] of [[Human, 'H'], [Agent, 'A']]) {
          const p = m.model.createAuthorityExecutionProfileFromSeed(S('s3'), R(childRun), A('mc'), kind, seed.fields[0])
          if (p.tag === 0) profiles.push({ label: `${childName}-child-of(${owner.label})/${childRun}/${kl}`, p: p.fields[0] })
        }
    }
  }
  const projections = [{ label: 'empty', p: m.model.empty }]
  for (const x of profiles) {
    const active = ok(m.run.registerAuthority(x.p, m.model.empty))
    projections.push({ label: `active ${x.label}`, p: active })
    const closed = m.run.closeAuthority(m.model.AuthorityExecutionProfile__get_LogicalRunId(x.p), m.model.AuthorityExecutionProfile__get_AuthorityRootUserMessageId(x.p), active)
    projections.push({ label: `closed ${x.label}`, p: ok(closed) })
    // A pending continuation claim and an accepted continuation on the active run.
    const join = new m.origin.PromptContinuationKind(1, []), human = new m.origin.PromptContinuationKind(4, [])
    let withClaims = m.run.registerClaim(m.run.claimContinuation(K('k-pending'), m.model.AuthorityExecutionProfile__get_SessionId(x.p), join, x.p, 'd1'), active)
    withClaims = m.run.registerClaim(m.run.claimContinuation(K('k-accepted'), m.model.AuthorityExecutionProfile__get_SessionId(x.p), human, x.p, 'd2'), withClaims)
    withClaims = m.run.acceptClaim(K('k-accepted'), P('phys-accepted'), withClaims)
    projections.push({ label: `claims ${x.label}`, p: withClaims })
  }
  return { profiles, projections, ids: { R, A, P, K } }
}
const ub = universe(base), uc = universe(cand)
console.log(ub.profiles.map((x) => x.label).join(" | "))
assert.equal(ub.profiles.length, uc.profiles.length)
assert.equal(ub.projections.length, uc.projections.length)

let compared = 0
const tally = {}
const diffs = []
const norm = (v) => JSON.parse(JSON.stringify(v ?? null))
const same = (k, f) => {
  let b, c, be, ce
  try { b = norm(f(base, ub)) } catch (e) { be = String(e?.message ?? e) }
  try { c = norm(f(cand, uc)) } catch (e) { ce = String(e?.message ?? e) }
  compared++
  { const fam = k.split(' ')[0]; const kind = be ? 'threw' : Array.isArray(b) ? (b[0] === 'Error' ? `Error:${JSON.stringify(b[1]).slice(0, 48)}` : b[0] === 'Ok' && Array.isArray(b[1]) && b[1].length === 0 ? 'Ok[]' : String(b[0])) : JSON.stringify(b).slice(0, 48); (tally[fam] ??= new Set()).add(kind) }
  try { assert.deepEqual({ value: c, threw: ce }, { value: b, threw: be }) }
  catch { diffs.push({ k, base: be ? { threw: be } : b, cand: ce ? { threw: ce } : c }) }
}
const n = ub.projections.length, np = ub.profiles.length
for (let j = 0; j < n; j++) {
  for (let i = 0; i < np; i++) {
    same(`resolveAuthorityProfile p${i} j${j}`, (m, u) => m.run.resolveAuthorityProfile(u.profiles[i].p, u.projections[j].p))
    same(`registerAuthority p${i} j${j}`, (m, u) => m.run.registerAuthority(u.profiles[i].p, u.projections[j].p))
    same(`closeAuthority p${i} j${j}`, (m, u) => m.run.closeAuthority(m.model.AuthorityExecutionProfile__get_LogicalRunId(u.profiles[i].p), m.model.AuthorityExecutionProfile__get_AuthorityRootUserMessageId(u.profiles[i].p), u.projections[j].p))
    same(`closeChildWork p${i} j${j}`, (m, u) => m.run.closeCompletedAgentOwnerChildWork(m.model.AuthorityExecutionProfile__get_LogicalRunId(u.profiles[i].p), m.model.AuthorityExecutionProfile__get_AuthorityRootUserMessageId(u.profiles[i].p), u.projections[j].p))
  }
  for (const [run, root] of [['run-1', 'm2'], ['run-x', 'm1'], ['', '']])
    same(`closeAuthority ${run}/${root} j${j}`, (m, u) => m.run.closeAuthority(u.ids.R(run), u.ids.A(root), u.projections[j].p))
  for (const phys of ['phys-accepted', 'phys-other'])
    for (const key of [null, 'k-pending', 'k-accepted', 'k-unknown'])
      for (const compaction of [false, true])
        same(`resolveKnownOrigin ${phys} ${key} ${compaction} j${j}`, (m, u) =>
          m.run.resolveKnownOrigin(u.ids.P(phys), key == null ? undefined : u.ids.K(key), compaction, u.projections[j].p))
}
// Durable replay of AuthorityLogicalRunClosed through the public fold (Fold.fs routes its verdict through the same law).
const closedFact = (m, sid, run, root) => new m.facts.PromptFactCases(5, [new m.facts.AuthorityLogicalRunClosedPayload(sid, run, root, 'HumanRoot', 'w')])
for (let j = 0; j < n; j++) {
  const runs = [...new Set(ub.profiles.map((x) => `${base.id.LogicalRunIdModule_value(base.model.AuthorityExecutionProfile__get_LogicalRunId(x.p))}|${base.id.AuthorityRootUserMessageIdModule_value(base.model.AuthorityExecutionProfile__get_AuthorityRootUserMessageId(x.p))}`)), 'run-x|m1', '|']
  for (const sid of ['s1', 's2', 's3']) for (const rr of runs) {
    const [run, root] = rr.split('|')
    same(`fold AuthorityLogicalRunClosed ${sid} ${rr} j${j}`, (m, u) =>
      m.fold.PromptFactFold_fold(() => u.projections[j].p, 0, closedFact(m, m.id.SessionIdModule_create(sid), u.ids.R(run), u.ids.A(root))))
  }
  same(`fold AuthorityLogicalRunClosed absent-session j${j}`, (m, u) =>
    m.fold.PromptFactFold_fold(() => undefined, 0, closedFact(m, m.id.SessionIdModule_create('s1'), u.ids.R('run-1'), u.ids.A('m1'))))
}
console.log(`profiles ${np}; projections ${n}; compared ${compared}; differing ${diffs.length}`)
console.log('base outcome kinds per family:', JSON.stringify(Object.fromEntries(Object.entries(tally).map(([k, v]) => [k, [...v].sort()]))))
for (const d of diffs.slice(0, 10)) console.log(JSON.stringify(d).slice(0, 800))
