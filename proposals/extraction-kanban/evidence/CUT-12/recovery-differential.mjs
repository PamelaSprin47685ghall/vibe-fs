// CUT-12 restart-recovery differential: the hand-written ChatExecutionRecovery.decide (base dist)
// vs the candidate (wanxiangli/chat-execution `recover` behind the same F# signature).
// Every reachable phase x every provider observation (same/other key, same/other run) x every
// resource observation (same/other key) x every persistence commitment x no policy / every
// failure-policy resolution (matching and non-matching authorization/key).
// Usage: node recovery-differential.mjs <base-root> <cand-root>
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`
  return {
    id: await import(`${d}/Foundation/Identity.js`),
    pi: await import(`${d}/Participant/Persona/Identity.js`),
    origin: await import(`${d}/Interaction/Authority/Origin.js`),
    seed: await import(`${d}/Interaction/Authority/IdentitySeed.js`),
    rk: await import(`${d}/Participant/Provider/Attempt/RequestKind.js`),
    facts: await import(`${d}/Execution/Session/ChatExecution/Facts.js`),
    proj: await import(`${d}/Execution/Session/ChatExecution/Projection.js`),
    rec: await import(`${d}/Execution/Session/ChatExecution/Recovery.js`),
    fm: await import(`${d}/Execution/Failure/Model.js`),
    fd: await import(`${d}/Execution/Failure/Decision.js`),
  }
}
const ok = (r) => { if (r.tag !== 0) throw new Error('setup'); return r.fields[0] }
const norm = (v) => {
  if (v === null || v === undefined) return null
  if (typeof v !== 'object') return v
  if (Array.isArray(v)) return v.map(norm)
  if (typeof v.cases === 'function' && 'tag' in v) return { [v.cases()[v.tag]]: norm(v.fields) }
  if (typeof v[Symbol.iterator] === 'function') return [...v].map(norm)
  const o = {}
  for (const k of Object.keys(v)) o[k] = norm(v[k])
  return o
}
const run = (m) => {
  const S = m.id.SessionIdModule_create('s1'), P = m.id.PhysicalUserMessageIdModule_create('p1')
  const R = (v) => m.id.LogicalRunIdModule_create(v), A = m.id.AuthorityRootUserMessageIdModule_create('m1')
  const Human = new m.origin.PromptRootAuthorityKind(0, [])
  const origin = new m.origin.PromptOrigin(0, [Human])
  const seed = new m.seed.PromptIdentitySeed(0, [ok(m.pi.ParticipantIdentityModule_resolveAtRoot('manager'))])
  const a1 = new m.facts.AcceptedChatExecutionEvidence(S, R('run-1'), A, Human, seed, P, origin)
  const a2 = new m.facts.AcceptedChatExecutionEvidence(S, R('run-2'), A, Human, seed, P, origin)
  const pr = (v) => m.id.ProviderRunIdentityModule_create(v)
  const WM = new m.rk.ProviderRequestKind(0, []), RP = new m.rk.ProviderRequestKind(1, [])
  const St = (a, r, k) => new m.facts.ProviderStartedEvidence(a, pr(r), k)
  const s11 = St(a1, 'r1', WM), s12 = St(a1, 'r2', WM), s1k = St(a1, 'r1', RP), s21 = St(a2, 'r1', WM)
  const D = [0, 1, 2, 3].map((t) => new m.facts.ChatExecutionTerminalDisposition(t, []))
  const O = [0, 1, 2].map((t) => new m.proj.PreStartOutcome(t, []))
  const key = new m.facts.ChatExecutionKey(S, P)
  const other = new m.facts.ChatExecutionKey(S, m.id.PhysicalUserMessageIdModule_create('p2'))
  const CS = m.proj.ChatExecutionState
  const states = [['A', new CS(0, [a1])], ['S', new CS(1, [s11])], ['Bo0', new CS(2, [a1, O[0]])], ['Bo2', new CS(2, [a1, O[2]])], ['Ed0', new CS(3, [s11, D[0]])], ['Ed3', new CS(3, [s11, D[3]])]]
  const PO = m.rec.ProviderPhysicalObservation
  const providers = [['missing', new PO(0, [])], ['ambiguous', new PO(1, [])], ['absent', new PO(2, [key])], ['absent-other', new PO(2, [other])]]
  for (const [sn, s] of [['s11', s11], ['s12', s12], ['s1k', s1k], ['s21', s21]]) {
    providers.push([`alive ${sn}`, new PO(3, [s])])
    for (const i of [0, 2]) providers.push([`terminal ${sn} d${i}`, new PO(4, [s, D[i]])])
  }
  const RO = m.rec.PhysicalResourceObservation
  const resources = []
  for (let t = 0; t < 4; t++) resources.push([`r${t}`, new RO(t, [key])])
  resources.push(['held-other', new RO(1, [other])])
  const persist = [0, 1, 2, 3].map((t) => [`c${t}`, new m.fm.PersistenceCommitment(t, [])])
  const auth = (run, r, k) => m.fm.ProviderRecoveryAuthorization_Create_603CB2F1(m.fm.ProviderRecoveryDecisionId_Create_Z721C83C5('d'), R(run), pr(r), k)
  const FR = m.fd.ExecutionFailureResolution
  const dec = (res) => new m.rec.RecoveryPolicyEvidence(1, [new m.fd.ExecutionFailureDecision(res, new m.fm.BreakerDecision(0, []), new m.fm.CapacitySettlement(0, []), new m.fm.FatalityDecision(0, []))])
  const policies = [
    ['none', new m.rec.RecoveryPolicyEvidence(0, [])],
    ['preserve', dec(new FR(0, []))],
    ['await', dec(new FR(1, [key]))],
    ['retry-match', dec(new FR(2, [auth('run-1', 'r1', WM)]))],
    ['retry-run', dec(new FR(2, [auth('run-1', 'r2', WM)]))],
    ['retry-kind', dec(new FR(2, [auth('run-1', 'r1', RP)]))],
    ['retry-logical', dec(new FR(2, [auth('run-2', 'r1', WM)]))],
    ['term-pre', dec(new FR(3, [key, D[1]]))],
    ['term-started', dec(new FR(4, [key, D[3]]))],
    ['term-started-other', dec(new FR(4, [other, D[3]]))],
  ]
  const out = []
  for (const [sl, st] of states) for (const [pl, po] of providers) for (const [rl, ro] of resources) for (const [cl, c] of persist) for (const [yl, y] of policies) {
    const ev = new m.rec.ChatExecutionRecoveryEvidence(st, po, ro, c, y)
    let r
    try { r = norm(m.rec.ChatExecutionRecovery_decide(ev)) } catch (e) { r = { throw: String(e.message) } }
    out.push([`${sl} | ${pl} | ${rl} | ${cl} | ${yl}`, r])
  }
  return out
}
const b = run(await load(baseRoot)), c = run(await load(candRoot))
let diffs = 0
const tally = {}
for (let i = 0; i < b.length; i++) {
  const sb = JSON.stringify(b[i][1]), sc = JSON.stringify(c[i][1])
  const top = Object.keys(b[i][1])[0]
  const sub = top === 'Ignore' ? Object.keys(b[i][1].Ignore[1])[0] : top === 'MarkManualIntervention' ? Object.keys(b[i][1].MarkManualIntervention[0].InterventionReason)[0] : top === 'ReconcilePhysical' ? Object.keys(b[i][1].ReconcilePhysical[0])[0] : ''
  tally[`${top} ${sub}`] = (tally[`${top} ${sub}`] ?? 0) + 1
  if (sb !== sc) { diffs++; if (diffs <= 10) console.log('DIFF', b[i][0], '\n  base', sb, '\n  cand', sc) }
}
console.log(JSON.stringify({ comparisons: b.length, differences: diffs, outcomes: tally }, null, 1))
process.exit(diffs === 0 ? 0 : 1)
