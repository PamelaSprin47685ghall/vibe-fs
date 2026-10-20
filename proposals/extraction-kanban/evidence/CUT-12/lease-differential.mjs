// CUT-12 sub-loop 3 differential: the hand-written AttachmentLeaseCore.ensure lease resolution
// (base dist) vs the candidate (wanxiangli/attachment-lease `resolve`/`abortOnFailure` behind the
// same F# signature). Every restore hint (none / x) x every Host child list of up to three children
// over ids {x, y} with agent {a, b, absent} and title {t, u, absent} x every success/failure of
// ListChildren, CreateChild, Close, Link and AbortChild x four AttachmentKinds. Compared: the
// returned Result (lease or error text) and the exact sequence of operations performed.
// Usage: node lease-differential.mjs <base-root> <cand-root>
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`, fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return {
    core: await import(`${d}/Execution/Session/Attachment/AttachmentLeaseCore.js`),
    own: await import(`${d}/Execution/Session/Ownership.js`),
    id: await import(`${d}/Foundation/Identity.js`),
    list: await import(`${fl}/List.js`),
    res: await import(`${fl}/Result.js`),
  }
}
const base = await load(baseRoot), cand = await load(candRoot)

const ids = ['x', 'y'], agents = ['a', 'b', undefined], titles = ['t', 'u', undefined]
const oneChild = []
for (const id of ids) for (const agent of agents) for (const title of titles) oneChild.push({ id, agent, title })
const childLists = [[]]
for (const c of oneChild) childLists.push([c])
for (const c of oneChild) for (const d of oneChild) childLists.push([c, d])
// three children only with duplicate hinted ids (the ambiguity edge)
for (const c of oneChild.filter((c) => c.id === 'x')) childLists.push([c, { id: 'x', agent: 'a', title: 't' }, { id: 'y', agent: 'a', title: 't' }])

const kinds = (m) => [m.own.AttachmentKind.Companion, m.own.AttachmentKind.SyncCoder, m.own.AttachmentKind.SyncDevOps, new m.own.AttachmentKind(5, ['tx1'])]
const failSets = []
for (let mask = 0; mask < 32; mask++) failSets.push({ list: !!(mask & 1), create: !!(mask & 2), close: !!(mask & 4), link: !!(mask & 8), abort: !!(mask & 16) })

const run = async (m, hint, children, kindIndex, fail) => {
  const trace = []
  const R = m.res.FSharpResult$2
  const ok = (v) => Promise.resolve(new R(0, [v])), err = (e) => Promise.resolve(new R(1, [e]))
  const ops = new m.core.AttachmentLeaseCore_Operations(
    (owner) => { trace.push(['list', owner]); return fail.list ? err('list-failed') : ok(m.list.ofArray(children.map((c) => new m.core.AttachmentLeaseCore_Child(c.id, c.agent, c.title)))) },
    (owner, agent, dir) => { trace.push(['create', owner, agent, dir ?? null]); return fail.create ? err('create-failed') : ok('fresh') },
    (child) => { trace.push(['abort', child]); return fail.abort ? err('abort-failed') : ok(undefined) },
    (owner, child, agent) => { trace.push(['link', owner, child, agent]); return fail.link ? err('link-failed') : ok(undefined) },
    (owner) => { trace.push(['close', owner]); return fail.close ? err('close-failed') : ok(undefined) },
  )
  const spec = new m.core.AttachmentLeaseCore_Spec(kinds(m)[kindIndex], 'a', 't', 'dir', hint)
  const r = await m.core.AttachmentLeaseCore_ensure(ops, m.id.SessionIdModule_create('owner'), spec)
  const result = r.tag === 0 ? { ok: { id: r.fields[0].SessionId, origin: r.fields[0].Origin.cases()[r.fields[0].Origin.tag], agent: r.fields[0].Agent } } : { error: r.fields[0] }
  return JSON.stringify({ result, trace })
}

let comparisons = 0, differences = 0
const outcomes = {}
for (const hint of [undefined, 'x']) for (const children of childLists) for (let k = 0; k < 4; k++) for (const fail of failSets) {
  const b = await run(base, hint, children, k, fail), c = await run(cand, hint, children, k, fail)
  comparisons++
  if (b !== c) { differences++; if (differences <= 3) console.error('DIFF', JSON.stringify({ hint, children, k, fail }), '\n base', b, '\n cand', c) }
  const o = JSON.parse(b).result
  const key = o.ok ? `ok ${o.ok.origin}` : `error ${o.error.split(' ')[0]}`
  outcomes[key] = (outcomes[key] ?? 0) + 1
}
console.log(JSON.stringify({ comparisons, differences, outcomes }, null, 1))
