import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'

// context-compression-029: the actual cutoff is the window's desire clamped by what the
// frozen material proves. Two boundaries are involved and they are not the same number:
// the Companion's digest is a claim about ITS cutoff, while the snapshot must record the
// prefix the probe actually replaces.

const select = (overrides) =>
  prefix.select({
    session: 'ses-029',
    committedEpoch: 0,
    committedSnapshot: null,
    coveredDigest: 'claim-at-frontier',
    requestStartCutoff: 50,
    frozenRef: 'blob-frozen',
    frozenDigest: 'frozen-digest',
    recomputeDigest: () => 'claim-at-frontier',
    ...overrides,
  })

test('WHAT[context-compression-029] the window bounds a probe even when coverage is ahead', () => {
  // The Blogger has summarised further than the window allows to fold. Folding to its
  // frontier would replace phases the window keeps raw, so the material stops at the
  // window boundary and the probe records that boundary.
  const result = select({
    phaseBoundary: 2,
    coverableCutoff: 5,
    materialCutoff: 2,
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.equal(result.cutoff, 2, 'folded only up to the window boundary')
})

test('WHAT[context-compression-029] material past the window boundary is refused', () => {
  const result = select({
    phaseBoundary: 2,
    coverableCutoff: 5,
    materialCutoff: 4,
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'BeyondPhaseBoundary')
  assert.match(result.message, /folds turns the window keeps raw below 2/)
})

test('WHAT[context-compression-029] coverage behind the window still bounds the fold', () => {
  // The window would allow folding to 9, but the Blogger only proved 3. The actual
  // cutoff never moves past the evidence.
  const result = select({
    phaseBoundary: 9,
    coverableCutoff: 3,
    materialCutoff: 3,
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.equal(result.cutoff, 3)
})

test('WHAT[context-compression-029] failure recovery may exceed the window', () => {
  // No window was spoken for this attempt: after a real WorkMain failure the probe is
  // bounded by proven coverage alone, which is the clause's explicit exception.
  const result = select({
    phaseBoundary: null,
    coverableCutoff: 5,
    materialCutoff: 5,
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.equal(result.cutoff, 5)
})

test('WHAT[context-compression-029] material the request may not cover is refused', () => {
  // The request is answering turn 4; material that claims turns up to 5 would replace
  // the message being answered.
  const result = select({
    phaseBoundary: null,
    coverableCutoff: 5,
    materialCutoff: 5,
    requestStartCutoff: 4,
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'MaterialBeyondBoundary')
  assert.match(result.message, /reaches past the covered boundary 4/)
})

test('WHAT[context-compression-029] a probe never retreats below the committed cutoff', () => {
  const result = select({
    phaseBoundary: 2,
    coverableCutoff: 9,
    materialCutoff: 2,
    committedSnapshot: prefix.snapshot({
      ref: 'blob-committed',
      frozenDigest: 'committed-frozen',
      cutoff: 6,
      prefixDigest: 'committed-prefix',
      sealRoot: 'committed-seal',
      syntheticId: 'committed-synthetic',
    }),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'WouldRetreat')
  assert.match(result.message, /2 is behind the committed 6/)
})

test('WHAT[context-compression-029] the coverage claim is proven at its own cutoff', () => {
  // One number is the Companion's claim, the other is the prefix this probe replaces.
  // Confusing them would either fold unproven turns or record a digest that does not
  // describe the snapshot.
  const asked = []

  const result = select({
    phaseBoundary: 2,
    coverableCutoff: 6,
    materialCutoff: 2,
    coveredDigest: 'claim-at-6',
    recomputeDigest: (cutoff) => {
      asked.push(cutoff)
      return cutoff === 6 ? 'claim-at-6' : 'digest-at-2'
    },
  })

  assert.equal(result.ok, true, result.ok ? '' : result.message)
  assert.deepEqual(asked, [6, 2], 'prove the claim at the frontier, then hash the boundary the snapshot claims')
  assert.equal(result.candidate.cutoff, 2)
  assert.equal(result.candidate.prefixDigest, 'digest-at-2')
})

test('WHAT[context-compression-029] a claim that does not match fails closed', () => {
  const result = select({
    phaseBoundary: 2,
    coverableCutoff: 6,
    materialCutoff: 2,
    coveredDigest: 'claim-at-6',
    recomputeDigest: (cutoff) => (cutoff === 6 ? 'renumbered' : 'digest-at-2'),
  })

  assert.equal(result.ok, false)
  assert.equal(result.error, 'CutoffProofFailed')
})

