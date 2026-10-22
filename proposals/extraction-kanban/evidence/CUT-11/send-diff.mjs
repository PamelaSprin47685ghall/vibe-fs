// CUT-11 sub-scope 2 differential: base vs candidate private send-answer handling in
// Interaction/Dispatch/Send.js. Each module is loaded from a temp copy with one appended export
// line (removed right after import); nothing else changes. For every Host SendOutcome shape and
// every stub behaviour (persist/accept/abandon succeed or fail, confirmation waiter present or
// absent) the calls made on the ledger ports, their arguments and the returned Result are compared,
// and continuationAttemptOutcome is compared on every (outcome, Ok/Error) pair.
// The detached classifier is a closure inside a member and is not reachable this way; it is covered
// by the dispatch-protocol suite (see mutants.out).
// Usage: node send-diff.mjs <base-root> <cand-root>
import assert from 'node:assert/strict'
import { readFileSync, writeFileSync, rmSync } from 'node:fs'
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`, src = `${d}/Interaction/Dispatch/Send.js`, tmp = `${d}/Interaction/Dispatch/.Send.cut11-diff.js`
  writeFileSync(tmp, readFileSync(src, 'utf8') + '\nexport { awaitPhysicalAwareSend as __await, continuationAttemptOutcome as __attempt };\n')
  try {
    return {
      send: await import(tmp),
      out: await import(`${d}/Foundation/Outcome.js`),
      id: await import(`${d}/Foundation/Identity.js`),
      res: await import(`${d}/fable_modules/fable-library-js.5.13.0/Result.js`),
    }
  } finally { rmSync(tmp) }
}
const B = await load(baseRoot), C = await load(candRoot)
const norm = (v) => JSON.parse(JSON.stringify(v ?? null))
const outcomes = (m) => [
  new m.out.Outcome_SendOutcome(0, [m.id.TransportReceiptModule_create('accepted-1')]),
  new m.out.Outcome_SendOutcome(1, [m.id.PhysicalUserMessageIdModule_create('phys-1')]),
  new m.out.Outcome_SendOutcome(2, ['retry later']),
  new m.out.Outcome_SendOutcome(3, ['who knows']),
  new m.out.Outcome_SendOutcome(4, ['fatal refusal']),
]
const Ok = (m, v) => new m.res.FSharpResult$2(0, [v]), Err = (m, e) => new m.res.FSharpResult$2(1, [e])
const run = async (m, oi, persistOk, acceptOk, abandonOk, waiter) => {
  const log = []
  const persist = async (fact) => { log.push(['persist', norm(fact)]); return persistOk ? Ok(m, undefined) : Err(m, 'persist failed') }
  const accept = async (p) => { log.push(['accept', norm(p)]); return acceptOk ? Ok(m, undefined) : Err(m, 'accept failed') }
  const abandon = async (reason, error) => { log.push(['abandon', norm(reason), error]); return abandonOk ? Ok(m, undefined) : Err(m, 'abandon failed') }
  const key = m.id.PromptKeyModule_create('K'), sid = m.id.SessionIdModule_create('s1')
  const w = waiter === 'none' ? undefined : Promise.resolve(waiter === 'confirmed' ? 'confirmed' : undefined)
  let result
  try { result = norm(await m.send.__await(key, sid, persist, accept, abandon, Promise.resolve(outcomes(m)[oi]), w)) } catch (e) { result = { threw: String(e?.message ?? e) } }
  return { log, result }
}
let compared = 0
const diffs = []
for (let oi = 0; oi < 5; oi++) for (const p of [true, false]) for (const a of [true, false]) for (const ab of [true, false]) for (const w of ['none', 'empty']) {
  const b = await run(B, oi, p, a, ab, w), c = await run(C, oi, p, a, ab, w)
  compared++
  try { assert.deepEqual(c, b) } catch { diffs.push({ oi, p, a, ab, w, base: b, cand: c }) }
}
for (let oi = 0; oi < 5; oi++) for (const ok of [true, false]) {
  const r = (m) => norm(m.send.__attempt(outcomes(m)[oi], ok ? Ok(m, m.id.PromptKeyModule_create('K')) : Err(m, 'e')))
  compared++
  try { assert.deepEqual(r(C), r(B)) } catch { diffs.push({ attempt: oi, ok, base: r(B), cand: r(C) }) }
}
console.log(JSON.stringify({ compared, differences: diffs.length }, null, 1))
if (diffs.length) { console.log(JSON.stringify(diffs.slice(0, 3), null, 1)); process.exit(1) }
