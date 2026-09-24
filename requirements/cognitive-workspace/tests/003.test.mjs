import assert from 'node:assert/strict'
import test from 'node:test'
import * as fold from '../../../dist/Participant/Cognition/FoldSurface.js'

// cognitive-workspace-004/005/006: the commit algebra. A replayed call must converge
// to its frozen result, a conflicting input for the same call is an identity
// conflict, and the ordinal must advance exactly once per commit.
const commit = (overrides = {}) => ({
  ownerKey: 'ses-1\u001finc-1',
  sessionId: 'ses-1',
  incumbencyId: 'inc-1',
  toolCallId: 'call-1',
  ordinal: 1,
  inputDigest: 'digest:1',
  predecessorOrdinal: null,
  snapshotRef: 'blob-1',
  snapshotDigest: 'digest-blob-1',
  rendererVersion: '1',
  ...overrides,
})

test('WHAT[cognitive-workspace-003] the same call and input folds to one change', () => {
  const result = fold.CognitiveFactFold_fold(null, { case: 'AssumePhaseCommitted', fields: commit() })
  assert.equal(result.ok, true)
  assert.equal(result.value.length, 1)
  assert.equal(result.value[0].projection.ordinal, 1)
})

test('WHAT[cognitive-workspace-003] the same call with a different input is an identity conflict', () => {
  const first = fold.CognitiveFactFold_fold(null, { case: 'AssumePhaseCommitted', fields: commit() })
  assert.equal(first.ok, true)
  const state = first.value[0].projection

  const second = fold.CognitiveFactFold_fold(state, {
    case: 'AssumePhaseCommitted',
    fields: commit({ inputDigest: 'digest:different' }),
  })

  assert.equal(second.ok, false)
  assert.match(second.error, /committed two different inputs/)
})

test('WHAT[cognitive-workspace-003] the same call and input replayed is idempotent', () => {
  // A replayed line from a crash between append and fold must converge to the same
  // state, not advance the ordinal a second time.
  const first = fold.CognitiveFactFold_fold(null, { case: 'AssumePhaseCommitted', fields: commit() })
  const state = first.value[0].projection
  const replay = fold.CognitiveFactFold_fold(state, { case: 'AssumePhaseCommitted', fields: commit() })

  assert.equal(replay.ok, true, replay.error ?? '')
  assert.equal(replay.value[0].projection.ordinal, 1)
})

test('WHAT[cognitive-workspace-003] a non-successor ordinal is refused', () => {
  const result = fold.CognitiveFactFold_fold(null, {
    case: 'AssumePhaseCommitted',
    fields: commit({ ordinal: 3, predecessorOrdinal: 1 }),
  })
  assert.equal(result.ok, false)
  assert.match(result.error, /is not the successor/)
})

test('WHAT[cognitive-workspace-003] the first commit has no predecessor and ordinal one', () => {
  const result = fold.CognitiveFactFold_fold(null, { case: 'AssumePhaseCommitted', fields: commit() })
  assert.equal(result.ok, true)
  assert.equal(result.value[0].projection.ordinal, 1)
})

test('WHAT[cognitive-workspace-003] a second commit advances exactly once', () => {
  const first = fold.CognitiveFactFold_fold(null, { case: 'AssumePhaseCommitted', fields: commit() })
  const second = fold.CognitiveFactFold_fold(first.value[0].projection, {
    case: 'AssumePhaseCommitted',
    fields: commit({ toolCallId: 'call-2', ordinal: 2, predecessorOrdinal: 1, inputDigest: 'digest:2' }),
  })
  assert.equal(second.ok, true, second.error ?? '')
  assert.equal(second.value[0].projection.ordinal, 2)
})

test('WHAT[cognitive-workspace-003] a phase boundary is the turn, not an array index', () => {
  // The fold carries no provider message index at all: the boundary lives in the
  // trace, so nothing here can be mistaken for a position in the provider's array.
  const result = fold.CognitiveFactFold_fold(null, { case: 'AssumePhaseCommitted', fields: commit() })
  assert.ok(!Object.prototype.hasOwnProperty.call(result.value[0], 'cutoffExclusive'))
  assert.ok(!Object.prototype.hasOwnProperty.call(result.value[0], 'messageIndex'))
})

test('WHAT[cognitive-workspace-003] the projection grants no capability and no completion', () => {
  const result = fold.CognitiveFactFold_fold(null, { case: 'AssumePhaseCommitted', fields: commit() })
  const projection = result.value[0].projection
  for (const forbidden of ['permission', 'role', 'complete', 'quality', 'retired', 'testPassed']) {
    assert.ok(
      !Object.prototype.hasOwnProperty.call(projection, forbidden),
      `a workspace projection must never carry ${forbidden}`,
    )
  }
})
