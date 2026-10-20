// CUT-09-a differential: the host-side ChatAdmissionIntent.resolve (base dist, hand-written entrance
// precedence) vs the candidate dist (wanxiangli/authority `admit` behind a typed projection).
// Pure function over DecodedMessage x DurableSnapshot; no effects.
// Usage: node differential.mjs <base-root> <cand-root> [<mutant-label>]
import assert from 'node:assert/strict'
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`
  const fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return {
    id: await import(`${d}/Foundation/Identity.js`),
    pi: await import(`${d}/Participant/Persona/Identity.js`),
    origin: await import(`${d}/Interaction/Authority/Origin.js`),
    model: await import(`${d}/Interaction/Authority/Model.js`),
    run: ((r) => new Proxy(r, { get: (t, k) => t[k] ?? t[`PromptAuthorityRun_${String(k)}`] }))(await import(`${d}/Interaction/Authority/Run.js`)),
    facts: await import(`${d}/Execution/Session/ChatExecution/Facts.js`),
    intent: await import(`${d}/OpenCode/Host/ChatAdmission/Intent.js`),
    map: await import(`${fl}/Map.js`),
  }
}
const base = await load(baseRoot), cand = await load(candRoot)
const ok = (r) => { if (r.tag !== 0) throw new Error('setup: ' + JSON.stringify(r)); return r.fields[0] }

const universe = (m) => {
  const S = (v) => m.id.SessionIdModule_create(v), R = (v) => m.id.LogicalRunIdModule_create(v)
  const A = (v) => m.id.AuthorityRootUserMessageIdModule_create(v), P = (v) => m.id.PhysicalUserMessageIdModule_create(v)
  const K = (v) => m.id.PromptKeyModule_create(v)
  const Human = new m.origin.PromptRootAuthorityKind(0, []), Agent = new m.origin.PromptRootAuthorityKind(1, [])
  const O = {
    human: new m.origin.PromptOrigin(0, [Human]), agent: new m.origin.PromptOrigin(0, [Agent]),
    join: new m.origin.PromptOrigin(1, [new m.origin.PromptContinuationKind(1, [])]),
    humanMsg: new m.origin.PromptOrigin(1, [new m.origin.PromptContinuationKind(4, [])]),
    hostInternal: new m.origin.PromptOrigin(2, []), unknown: new m.origin.PromptOrigin(3, []),
  }
  const ident = (name) => ok(m.pi.ParticipantIdentityModule_resolveAtRoot(name))
  const profiles = []
  for (const s of ['s1', 's2']) for (const [kind, kl] of [[Human, 'H'], [Agent, 'A']]) for (const name of ['manager', 'engineer']) {
    const p = m.model.createAuthorityExecutionProfile(S(s), R('run-1'), A('m1'), kind, ident(name))
    if (p.tag === 0) profiles.push({ label: `${s}/${kl}/${name}`, p: p.fields[0] })
  }
  for (const owner of profiles.filter((x) => x.label.endsWith('/manager')).slice(0, 2))
    for (const childName of ['engineer', 'devops'])
      for (const [kind, kl] of [[Human, 'H'], [Agent, 'A']]) {
        const seed = m.model.issueInheritedIdentitySeed(childName, owner.p)
        if (seed.tag !== 0) continue
        const p = m.model.createAuthorityExecutionProfileFromSeed(S('s3'), R('run-c'), A('mc'), kind, seed.fields[0])
        if (p.tag === 0) profiles.push({ label: `${childName}-child-of(${owner.label})/${kl}`, p: p.fields[0] })
      }
  const withClaim = (proj, key, claim) => Object.assign(Object.create(Object.getPrototypeOf(proj)), proj, { PendingClaims: m.map.add(key, claim, proj.PendingClaims) })
  const projections = [{ label: 'empty', p: m.model.empty }]
  for (const x of profiles) {
    const sid = m.model.AuthorityExecutionProfile__get_SessionId(x.p)
    const active = ok(m.run.registerAuthority(x.p, m.model.empty))
    projections.push({ label: `active ${x.label}`, p: active })
    projections.push({ label: `closed ${x.label}`, p: ok(m.run.closeAuthority(m.model.AuthorityExecutionProfile__get_LogicalRunId(x.p), m.model.AuthorityExecutionProfile__get_AuthorityRootUserMessageId(x.p), active)) })
    const join = new m.origin.PromptContinuationKind(1, []), human = new m.origin.PromptContinuationKind(4, [])
    let c = m.run.registerClaim(m.run.claimContinuation(K('k-pending'), sid, join, x.p, 'd1'), active)
    c = m.run.registerClaim(m.run.claimContinuation(K('k-accepted'), sid, human, x.p, 'd2'), c)
    c = m.run.acceptClaim(K('k-accepted'), P('phys-accepted'), c)
    // An AgentOwnerRoot claim that lands (PhysicalLandings without a continuation mapping).
    const tpl0 = m.map.tryFind(K('k-pending'), c.PendingClaims)
    c = withClaim(c, K('k-root'), Object.assign(Object.create(Object.getPrototypeOf(tpl0)), tpl0, { Origin: O.agent, PromptKey: K('k-root'), PayloadDigest: 'd3' }))
    const pendingRoot = c
    c = m.run.acceptClaim(K('k-root'), P('phys-landed'), c)
    projections.push({ label: `claims ${x.label}`, p: c })
    projections.push({ label: `pending-root ${x.label}`, p: pendingRoot })
    // Claims whose own origin is not admissible / a claim recorded under another session.
    const template = m.map.tryFind(K('k-pending'), c.PendingClaims)
    for (const [ol, o] of Object.entries(O)) {
      const forged = Object.assign(Object.create(Object.getPrototypeOf(template)), template, { Origin: o, PromptKey: K(`k-${ol}`) })
      c = withClaim(c, K(`k-${ol}`), forged)
    }
    const other = Object.assign(Object.create(Object.getPrototypeOf(template)), template, { SessionId: S('s9'), PromptKey: K('k-other-session') })
    c = withClaim(c, K('k-other-session'), other)
    projections.push({ label: `forged ${x.label}`, p: c })
  }
  const accepted = [{ label: 'none', e: undefined }]
  for (const x of profiles) for (const [ol, o] of [['human', O.human], ['agent', O.agent], ['humanMsg', O.humanMsg]]) for (const phys of ['phys-root', 'phys-accepted'])
    accepted.push({ label: `${ol}/${phys}/${x.label}`, e: new m.facts.AcceptedChatExecutionEvidence(m.model.AuthorityExecutionProfile__get_SessionId(x.p), R('run-1'), A('m1'), m.model.AuthorityExecutionProfile__get_AuthorityKind(x.p), m.model.AuthorityExecutionProfile__get_IdentitySeed(x.p), P(phys), o) })
  const carriers = [undefined, new m.intent.IdentityCarrierError(0, []), new m.intent.IdentityCarrierError(1, []), new m.intent.IdentityCarrierError(2, [])]
  return { projections, accepted, carriers, S, P, K }
}
const ub = universe(base), uc = universe(cand)
assert.equal(ub.projections.length, uc.projections.length)
assert.equal(ub.accepted.length, uc.accepted.length)

const sessions = [undefined, 's1', 's2', 's3']
const physicals = [undefined, 'phys-root', 'phys-accepted', 'phys-landed', 'phys-new']
const agents = [undefined, 'manager', 'engineer', 'devops', 'not-a-participant', '']
const keys = [undefined, 'k-pending', 'k-accepted', 'k-root', 'k-unknown', 'k-human', 'k-agent', 'k-join', 'k-humanMsg', 'k-hostInternal', 'k-other-session']
const hosts = [[false, false], [true, false], [false, true]]

const norm = (v) => JSON.parse(JSON.stringify(v ?? null))
const run = (m, u, pi, ei, ci, s, ph, ag, k, h) => {
  const msg = new m.intent.DecodedMessage(u.carriers[ci], s && u.S(s), ph && u.P(ph), ag, k && u.K(k), h[0], h[1], undefined)
  const snap = new m.intent.DurableSnapshot(pi < 0 ? undefined : u.projections[pi].p, u.accepted[ei].e)
  return m.intent.resolve(msg, snap)
}
let compared = 0
const tally = {}, diffs = []
const caseName = (d) => d?.tag === undefined ? 'threw' : `${d.tag}${d.fields?.[0]?.tag !== undefined ? '.' + d.fields[0].tag : ''}`
for (let pi = -1; pi < ub.projections.length; pi++) for (let ei = 0; ei < ub.accepted.length; ei++) for (let ci = 0; ci < 4; ci++)
  for (const s of sessions) for (const ph of physicals) for (const ag of agents) for (const k of keys) for (const h of hosts) {
    if (k && ei > 1) continue // accepted evidence only enters the PromptKey-free replay rule; keep two shapes with a key
    if (ci > 0 && (ag || k || h[0] || h[1])) continue // a malformed carrier short-circuits; sample it on the plain shape
    let b, c, be, ce, braw
    try { braw = run(base, ub, pi, ei, ci, s, ph, ag, k, h); b = norm(braw) } catch (e) { be = String(e?.message ?? e) }
    try { c = norm(run(cand, uc, pi, ei, ci, s, ph, ag, k, h)) } catch (e) { ce = String(e?.message ?? e) }
    compared++
    const cn = be ? 'threw' : caseName(braw); tally[cn] = (tally[cn] ?? 0) + 1
    try { assert.deepEqual({ value: c, threw: ce }, { value: b, threw: be }) }
    catch { if (diffs.length < 2000) diffs.push({ at: { pi, ei, ci, s, ph, ag, k, h }, base: be ? { threw: be } : b, cand: ce ? { threw: ce } : c }) ; else diffs.length++ }
  }
console.log(JSON.stringify({ compared, differences: diffs.length, decisionTagCoverage: tally }, null, 1))
if (diffs.length) { console.log(JSON.stringify(diffs.slice(0, 3), null, 1)); process.exit(1) }
