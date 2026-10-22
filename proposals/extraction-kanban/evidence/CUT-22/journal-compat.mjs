// CUT-22 journal compatibility: removing the always-None ConcernPlacement field from
// HostFactCases.PairProgrammingGuidelineAnchored must not change what decodes.
// Usage: node journal-compat.mjs <base-root-with-field> <cand-root-without-field>
import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
const [baseRoot, candRoot] = process.argv.slice(2)
const mods = async (root) => ({
  codec: await import(`${root}/dist/Persistence/Journal/FactCodec.js`),
  fact: await import(`${root}/dist/Composition/Durable/Fact.js`),
  host: await import(`${root}/dist/Host/Facts.js`),
  id: await import(`${root}/dist/Foundation/Identity.js`),
  list: await import(`${root}/dist/fable_modules/fable-library-js.5.13.0/List.js`),
  surface: await import(`${root}/dist/OpenCode/Host/PairProgrammingThoughtSurface.js`),
})
const base = await mods(baseRoot), cand = await mods(candRoot)
assert.equal(typeof base.host.ConcernPlacementBatch, 'function', 'base must still carry the field')
assert.equal(cand.host.ConcernPlacementBatch, undefined, 'candidate must not carry the field')

const guideline = (m, i, placement) => {
  const payload = {
    CallGap: m.id.TranscriptGap.Start,
    CallId: m.id.ToolCallIdModule_create(`call-${i}`),
    MarkerText: `marker ${i}`,
    Ordinal: BigInt(i),
    ResultGap: m.id.TranscriptGap.Start,
    SessionId: m.id.SessionIdModule_create('s1'),
  }
  if (placement !== undefined) payload.ConcernPlacement = placement
  return new m.fact.Fact(1, [new m.fact.AgentFact(6, [new m.host.HostFactCases(1, [payload])])])
}
let checked = 0
for (let i = 0; i < 50; i++) {
  const placements = [undefined, new base.host.ConcernPlacementBatch(base.list.ofArray([`g${i}`]), base.list.ofArray([`m${i}`, 'x']))]
  const candJson = cand.codec.serializeFact(guideline(cand, i))
  for (const p of placements) {
    const oldJson = base.codec.serializeFact(guideline(base, i, p))
    const decoded = cand.codec.deserializeFact(oldJson)
    assert.equal(decoded.tag, 0, `candidate failed to decode a base fact: ${oldJson} -> ${JSON.stringify(decoded)}`)
    assert.equal(cand.codec.serializeFact(decoded.fields[0]), candJson, 'decoded old fact must equal the candidate fact')
    checked++
  }
  const back = base.codec.deserializeFact(candJson)
  assert.equal(back.tag, 0, `base failed to decode a candidate fact: ${candJson}`)
  assert.equal(base.codec.serializeFact(back.fields[0]), base.codec.serializeFact(guideline(base, i)))
  checked++
}
// End-to-end: a journal written by base replays in the candidate with the same pair count.
const dir = mkdtempSync(join(tmpdir(), 'cut22-'))
try {
  const jb = await base.surface.createJournal(dir); assert.ok(jb.ok)
  for (let i = 1; i <= 5; i++) assert.ok((await base.surface.appendAnchoredPair(jb.journal, { session: 's1', ordinal: i, callId: `c${i}`, markerText: `m${i}`, callGapAfter: `after:a${i}`, resultGapAfter: `after:r${i}` })).ok)
  const basePairs = base.surface.pairCount(jb.journal, 's1'); base.surface.disposeJournal(jb.journal)
  const jc = await cand.surface.createJournal(dir); assert.ok(jc.ok, JSON.stringify(jc))
  const candPairs = cand.surface.pairCount(jc.journal, 's1'); cand.surface.disposeJournal(jc.journal)
  assert.equal(candPairs, basePairs)
  console.log(`codec cases ${checked} ok; journal replay base pairs ${basePairs} = candidate pairs ${candPairs}`)
} finally { rmSync(dir, { recursive: true, force: true }) }
