// CUT-12 differential: the hand-written ChatExecution phase law (base dist) vs the candidate dist
// (wanxiangli/chat-execution behind ChatExecutionPhaseLaw). Covers ChatExecutionFactFold.fold
// (every fact x every reachable phase x evidence equal/different/invalid x schema 1/2),
// ManagedChatProviderLifecycle.startWith/terminalWith and PreProviderSettlement.settleWith
// with an in-memory persistence that records every append. Pure; no journal, no host.
// Usage: node differential.mjs <base-root> <cand-root>
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`, fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return {
    id: await import(`${d}/Foundation/Identity.js`),
    pi: await import(`${d}/Participant/Persona/Identity.js`),
    origin: await import(`${d}/Interaction/Authority/Origin.js`),
    seed: await import(`${d}/Interaction/Authority/IdentitySeed.js`),
    rk: await import(`${d}/Participant/Provider/Attempt/RequestKind.js`),
    facts: await import(`${d}/Execution/Session/ChatExecution/Facts.js`),
    proj: await import(`${d}/Execution/Session/ChatExecution/Projection.js`),
    fold: await import(`${d}/Execution/Session/ChatExecution/Fold.js`),
    acc: await import(`${d}/Execution/Session/ChatExecution/Acceptance.js`),
    set: await import(`${d}/Execution/Session/ChatExecution/Settlement.js`),
    res: await import(`${fl}/Result.js`),
  }
}
const ok = (r) => { if (r.tag !== 0) throw new Error('setup: ' + JSON.stringify(r)); return r.fields[0] }

// Structural normal form of Fable values: unions by case name, records by fields.
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

const universe = (m) => {
  const S = m.id.SessionIdModule_create('s1'), P = m.id.PhysicalUserMessageIdModule_create('p1')
  const R = (v) => m.id.LogicalRunIdModule_create(v), A = m.id.AuthorityRootUserMessageIdModule_create('m1')
  const Human = new m.origin.PromptRootAuthorityKind(0, []), Agent = new m.origin.PromptRootAuthorityKind(1, [])
  const origin = new m.origin.PromptOrigin(0, [Human])
  const rootSeed = (name) => new m.seed.PromptIdentitySeed(0, [ok(m.pi.ParticipantIdentityModule_resolveAtRoot(name))])
  const E = (run, kind, seed) => new m.facts.AcceptedChatExecutionEvidence(S, R(run), A, kind, seed, P, origin)
  const accepted = {
    a1: E('run-1', Human, rootSeed('manager')),
    a2: E('run-2', Human, rootSeed('manager')),
    a3: E('run-1', Human, rootSeed('engineer')),
    bad: E('run-1', Agent, rootSeed('manager')), // AgentOwnerRoot with a root-selection seed: invalid
  }
  const run = (v) => m.id.ProviderRunIdentityModule_create(v)
  const WorkMain = new m.rk.ProviderRequestKind(0, []), Repair = new m.rk.ProviderRequestKind(1, [])
  const St = (a, r, k) => new m.facts.ProviderStartedEvidence(a, run(r), k)
  const started = {
    s11: St(accepted.a1, 'r1', WorkMain), s12: St(accepted.a1, 'r2', WorkMain), s1k: St(accepted.a1, 'r1', Repair),
    s21: St(accepted.a2, 'r1', WorkMain), sbad: St(accepted.bad, 'r1', WorkMain),
  }
  const D = [0, 1, 2, 3].map((t) => new m.facts.ChatExecutionTerminalDisposition(t, []))
  const O = [0, 1, 2].map((t) => new m.proj.PreStartOutcome(t, []))
  const key = new m.facts.ChatExecutionKey(S, P)
  const otherKey = new m.facts.ChatExecutionKey(S, m.id.PhysicalUserMessageIdModule_create('p2'))
  const CS = m.proj.ChatExecutionState
  const states = [{ label: 'none', s: undefined }]
  for (const [an, a] of Object.entries(accepted)) if (an !== 'bad') {
    states.push({ label: `Accepted ${an}`, s: new CS(0, [a]) })
    O.forEach((o, i) => states.push({ label: `EndedBeforeStart ${an} o${i}`, s: new CS(2, [a, o]) }))
  }
  for (const [sn, s] of Object.entries(started)) if (sn !== 'sbad') {
    states.push({ label: `Started ${sn}`, s: new CS(1, [s]) })
    D.forEach((d, i) => states.push({ label: `EndedAfterStart ${sn} d${i}`, s: new CS(3, [s, d]) }))
  }
  const TE = m.facts.ChatExecutionTerminalEvidence, FC = m.facts.ChatExecutionFactCases
  const facts = []
  for (const sv of [1, 2]) for (const k of [['key', key], ['otherKey', otherKey]]) {
    for (const [an, a] of Object.entries(accepted)) facts.push({ label: `Accepted ${an} v${sv} ${k[0]}`, f: new FC(0, [{ SchemaVersion: sv, Key: k[1], Evidence: a }]) })
    for (const [sn, s] of Object.entries(started)) facts.push({ label: `ProviderStarted ${sn} v${sv} ${k[0]}`, f: new FC(1, [{ SchemaVersion: sv, Key: k[1], Evidence: s }]) })
    D.forEach((d, i) => {
      for (const [an, a] of Object.entries(accepted)) facts.push({ label: `Terminal pre ${an} d${i} v${sv} ${k[0]}`, f: new FC(2, [{ SchemaVersion: sv, Key: k[1], Evidence: new TE(0, [a]), Disposition: d }]) })
      for (const [sn, s] of Object.entries(started)) facts.push({ label: `Terminal after ${sn} d${i} v${sv} ${k[0]}`, f: new FC(2, [{ SchemaVersion: sv, Key: k[1], Evidence: new TE(1, [s]), Disposition: d }]) })
    })
  }
  return { key, states, facts, accepted, started, D, WorkMain, Repair }
}

const run = async (m) => {
  const u = universe(m)
  const out = []
  const mapAdd = (await import(`${m.root}/dist/fable_modules/fable-library-js.5.13.0/Map.js`)).add
  const projOf = (state) => state === undefined ? m.proj.ChatExecutionProjection_empty
    : new m.proj.ChatExecutionProjectionState(mapAdd(u.key, state, m.proj.ChatExecutionProjection_empty.ByKey))
  // 1. fold
  for (const st of u.states) for (const f of u.facts) {
    let r
    try {
      const res = m.fold.fold(projOf(st.s), f.f)
      r = res.tag === 0 ? { Ok: norm([...res.fields[0].ByKey].map(([, v]) => v)) } : { Error: norm(res.fields[0]) }
    } catch (e) { r = { throw: String(e.message) } }
    out.push([`fold | ${st.label} | ${f.label}`, r])
  }
  // 2. provider lifecycle + 3. pre-provider settlement, against an in-memory persistence
  const persistence = (state, kind) => {
    const log = []
    let current = projOf(state)
    const read = (k) => m.proj.ChatExecutionProjection_byKey(k, current)
    const commit = (fact) => {
      const r = m.fold.fold(current, fact)
      if (r.tag === 0) current = r.fields[0]
      log.push(norm(fact))
      return Promise.resolve(new m.res.FSharpResult$2(0, [undefined]))
    }
    const p = kind === 'life'
      ? new m.acc.ManagedChatProviderLifecyclePersistence(read, (_s, fact) => commit(fact))
      : new m.set.PreProviderSettlementPersistence(read, (k, a, d) => commit(new m.facts.ChatExecutionFactCases(2, [{ SchemaVersion: 1, Key: k, Evidence: new m.facts.ChatExecutionTerminalEvidence(0, [a]), Disposition: d }])))
    return { p, log }
  }
  const settle = async (label, thunk, log) => {
    let r
    try { const res = await thunk(); r = res.tag === 0 ? { Ok: norm(res.fields[0]) } : { Error: norm(res.fields[0]) } } catch (e) { r = { throw: String(e.message) } }
    out.push([label, { r, appended: log }])
  }
  for (const st of u.states) {
    for (const [an, a] of Object.entries(u.accepted)) for (const rn of ['r1', 'r2']) for (const k of [u.WorkMain, u.Repair]) {
      const { p, log } = persistence(st.s, 'life')
      await settle(`start | ${st.label} | ${an} ${rn} ${k.tag}`, () => m.acc.ManagedChatProviderLifecycle_startWith(p, u.key, a, m.id.ProviderRunIdentityModule_create(rn), k), log)
    }
    for (const [sn, s] of Object.entries(u.started)) for (let i = 0; i < 4; i++) {
      const { p, log } = persistence(st.s, 'life')
      await settle(`terminal | ${st.label} | ${sn} d${i}`, () => m.acc.ManagedChatProviderLifecycle_terminalWith(p, u.key, s, u.D[i]), log)
    }
    for (const [an, a] of Object.entries(u.accepted)) for (let i = 0; i < 4; i++) {
      const { p, log } = persistence(st.s, 'settle')
      await settle(`settle | ${st.label} | ${an} d${i}`, () => m.set.PreProviderSettlement_settleWith(p, u.key, a, u.D[i]), log)
    }
  }
  return out
}

const base = await load(baseRoot); base.root = baseRoot
const cand = await load(candRoot); cand.root = candRoot
const b = await run(base), c = await run(cand)
if (b.length !== c.length) throw new Error(`case count differs ${b.length} vs ${c.length}`)
let diffs = 0
const tally = {}
for (let i = 0; i < b.length; i++) {
  const [lb, rb] = b[i], [lc, rc] = c[i]
  if (lb !== lc) throw new Error('label drift ' + lb + ' / ' + lc)
  const sb = JSON.stringify(rb), sc = JSON.stringify(rc)
  const fam = lb.split(' | ')[0]
  const outcome = Object.keys(rb.r ?? rb)[0] + (rb.appended ? `/appends=${rb.appended.length}` : '')
  tally[`${fam} ${outcome}`] = (tally[`${fam} ${outcome}`] ?? 0) + 1
  if (sb !== sc) { diffs++; if (diffs <= 10) console.log('DIFF', lb, '\n  base', sb, '\n  cand', sc) }
}
console.log(JSON.stringify({ comparisons: b.length, differences: diffs, outcomes: tally }, null, 1))
process.exit(diffs === 0 ? 0 : 1)
