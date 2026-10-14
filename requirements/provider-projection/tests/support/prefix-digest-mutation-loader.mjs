// Node hands `source` either as a string or as bytes depending on version and
// format. Decode once so the mutation operates on real text, and re-encode on the
// way out only when the loader gave us bytes.
const decodeSource = (source) =>
  typeof source === 'string' ? source : new TextDecoder().decode(source)

const reencodeSource = (template, mutated) =>
  typeof template === 'string' ? mutated : new TextEncoder().encode(mutated)

// The loader hook can run more than once for the same module (loader thread and
// main thread). Apply the mutation exactly once; later passes see the mutated
// text and pass it through.
const mutatedUrls = new Set()

// CTX-029 (requirements/context-compression/WHAT.md): a Blogger frame whose
// covered interval crosses the desired phase boundary must not advance unless a
// verifiable complete-material subset ends at that boundary. The former
// digest-wiring mutants produced the same non-advance outcome as the correct
// behaviour, so they could no longer form a counterexample. This mutation injects
// the opposite defect instead — the frozen subset is taken past the boundary and
// both boundary refusals are disabled — so the production test's own non-advance
// assertion must catch it.
const editsFor = (url) => {
  if (url.endsWith('/dist/Context/Prefix/Wire.js')) {
    return [
      [
        'const frames = BlogProjection_framesThroughCutoff(PrefixProbeSelection_limit(window$, coverableCutoff, requestCutoff), blog);',
        'const frames = BlogProjection_framesThroughCutoff(coverableCutoff, blog);',
      ],
    ]
  }
  if (url.endsWith('/dist/Context/Prefix/ProbeSelection.js')) {
    return [
      ['else if (materialCutoff > limit) {', 'else if (false) {'],
      ['else if ((desired = (window$.fields[0] | 0), materialCutoff > desired)) {', 'else if (false) {'],
      // The production-side proof is the other refusal this scenario can hit:
      // if the Companion claim and X's current prefix disagree (or the boundary
      // is unprovable for this retry), CTX-011 step 5 fails closed before any
      // candidate exists. Leaving it armed would let the proof absorb the
      // mutant's boundary bypass, so the mutant must disable this refusal too —
      // the reproduced defect is exactly "advance on an unproven boundary".
      ['if (recomputedFrontier !== coveredDigest) {', 'if (false) {'],
    ]
  }
  return []
}

export async function load(url, context, nextLoad) {
  const loaded = await nextLoad(url, context)
  if (process.env.WANXIANGSHU_PREFIX_DIGEST_MUTATION !== 'crossing') return loaded
  if (mutatedUrls.has(url)) return loaded
  const edits = editsFor(url)
  if (edits.length === 0) return loaded
  let source = decodeSource(loaded.source)
  for (const [expression, replacement] of edits) {
    if (source.split(expression).length !== 2) {
      throw new Error(`production expression must occur exactly once: ${expression}`)
    }
    source = source.replace(expression, () => replacement)
  }
  mutatedUrls.add(url)
  return { ...loaded, source: reencodeSource(loaded.source, source) }
}
