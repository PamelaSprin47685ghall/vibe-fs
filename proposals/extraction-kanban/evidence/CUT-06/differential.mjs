// Pure decision double-run: the retired in-host table (base build) and the
// wanxiangli/delivery-backed composer (candidate build) receive the same
// random message sequence; every result and every thrown error must match.
import assert from 'node:assert/strict'
const root = process.argv[2]
const js = `${root}/dist/Repository/Programming/Js`
const oldM = await import(`${js}/DeliveredValues.base.js`)
const newM = await import(`${js}/DeliveredValues.js`)
const { ProviderRunIdentityModule_create: run, ToolCallIdModule_create: call } = await import(`${root}/dist/Foundation/Identity.js`)
const ledger = await import(`${js}/Anchor/AnchorLedger.js`)
const { some } = await import(`${root}/node_modules/@fable-org/fable-library-js/Option.js`)
let seed = Number(process.argv[3] ?? 1)
const rand = n => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed % n }
const sessions = ['s1', 's2', 's3']
const runs = ['r1', 'r2']
const proses = [undefined, 'I did the work and ran the tests.', 'summary', '   ', 'I did the work and ran the tests. More.']
const viewOf = () => {
  switch (rand(5)) {
    case 0: return undefined
    case 1: return { prefixUntil: 0, thenMap: {} }
    case 2: return { prefixUntil: 0, thenMap: { 0: 'final report' } }
    case 3: return { prefixUntil: rand(3), thenMap: { 0: `note ${rand(3)}` } }
    default: return { bogus: true }
  }
}
const show = value => JSON.stringify(value, (k, v) => (v && typeof v === 'object' && 'fields' in v && 'tag' in v) ? { tag: v.tag, fields: v.fields } : v)
const outcome = (f) => { try { return { ok: show(f()) } } catch (e) { return { err: String(e.message) } } }
const opt = v => v === undefined ? undefined : some(v)
let steps = 0, throws = 0, nonEmpty = 0, rendered = 0
for (let round = 0; round < 4000; round += 1) {
  const s = sessions[rand(3)]
  if (rand(25) === 0) ledger.remember(s, [[opt(`m${rand(4)}`), `message ${rand(4)}`]])
  const view = viewOf(), r = run(runs[rand(2)]), c = call(`c${rand(3)}`), prose = proses[rand(proses.length)]
  const ops = [
    m => m.recordView(s, opt(view)),
    m => m.recordFencedView(s, r, c, opt(view)),
    m => m.take(s),
    m => m.peek(s),
    m => m.peekCached(s),
    m => m.peekView(s),
    m => m.peekFence(s),
    m => [...m.alreadyReturned(s)].sort(),
    m => m.tryTakeRenderedWith(s, prose),
    m => m.tryTakeRenderedFor(s, r, prose),
    m => m.tryTakeRendered(s),
    m => m.tryPeekRendered(s),
    m => m.tryPeekRenderedWith(s, prose),
    m => m.clear(s),
    m => m.clearReturned(s),
  ]
  const op = ops[rand(ops.length)]
  const a = outcome(() => op(oldM)), b = outcome(() => op(newM))
  assert.deepEqual(b, a, `divergence at step ${round} op ${op}`)
  if (a.ok && a.ok.includes("[[returned]]")) rendered += 1
  steps += 1; if (a.err) throws += 1; else if (a.ok && a.ok !== 'null' && a.ok !== undefined && a.ok !== '[]') nonEmpty += 1
}
console.log(JSON.stringify({ rendered, seed: process.argv[3] ?? 1, steps, throws, nonEmpty }))
