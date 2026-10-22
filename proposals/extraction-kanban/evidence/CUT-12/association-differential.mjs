// CUT-12 sub-loop 1 differential: the hand-written SessionAssociationProjection.linkSatellite
// (base dist) vs the candidate (wanxiangli/association `link` behind the same F# signature).
// Every association map over three sessions (each absent / Work with or without a parent /
// Companion of one of three owners) x every main/satellite among four ids x parent proposed or not.
// Usage: node association-differential.mjs <base-root> <cand-root>
const [baseRoot, candRoot] = process.argv.slice(2)
const load = async (root) => {
  const d = `${root}/dist`, fl = `${d}/fable_modules/fable-library-js.5.13.0`
  return {
    id: await import(`${d}/Foundation/Identity.js`),
    a: await import(`${d}/Execution/Session/Association.js`),
    map: await import(`${fl}/Map.js`),
  }
}
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
  const S = (v) => m.id.SessionIdModule_create(v)
  const ids = ['a', 'b', 'c']
  const options = (sid) => [
    null,
    new m.a.SessionAssociation(S(sid), new m.a.ManagedSessionKind(0, []), undefined),
    new m.a.SessionAssociation(S(sid), new m.a.ManagedSessionKind(0, []), S('p')),
    ...['a', 'b', 'z'].filter((o) => o !== sid).map((o) =>
      new m.a.SessionAssociation(S(sid), new m.a.ManagedSessionKind(1, [S(o), new m.a.SatelliteKind(0, [])]), S(o))),
  ]
  const maps = [[]]
  for (const sid of ids) {
    const next = []
    for (const partial of maps) for (const e of options(sid)) next.push(e ? [...partial, e] : partial)
    maps.splice(0, maps.length, ...next)
  }
  const out = []
  for (const entries of maps) {
    let cur = m.a.SessionAssociationProjection_empty
    for (const e of entries) cur = m.map.add(e.SessionId, e, cur)
    for (const main of ['a', 'b', 'c', 'n']) for (const sat of ['a', 'b', 'c', 'n']) for (const parent of [undefined, S('q')]) {
      let r
      try {
        const res = m.a.SessionAssociationProjection_link(S(main), S(sat), parent, cur)
        r = res.tag === 0 ? { Ok: norm([...res.fields[0]].map(([, v]) => v)) } : { Error: norm(res.fields[0]), text: m.a.SessionAssociationProjection_describe(res.fields[0]) }
      } catch (e) { r = { throw: String(e.message) } }
      out.push([`${JSON.stringify(norm(entries))} | link ${main}->${sat} parent=${parent ? 'q' : '-'}`, r])
    }
  }
  return out
}
const b = run(await load(baseRoot)), c = run(await load(candRoot))
let diffs = 0
const tally = {}
for (let i = 0; i < b.length; i++) {
  const sb = JSON.stringify(b[i][1]), sc = JSON.stringify(c[i][1])
  const k = b[i][1].Ok ? 'Ok' : b[i][1].Error ? Object.keys(b[i][1].Error)[0] : 'throw'
  tally[k] = (tally[k] ?? 0) + 1
  if (sb !== sc) { diffs++; if (diffs <= 10) console.log('DIFF', b[i][0], '\n  base', sb, '\n  cand', sc) }
}
console.log(JSON.stringify({ comparisons: b.length, differences: diffs, outcomes: tally }, null, 1))
process.exit(diffs === 0 ? 0 : 1)
