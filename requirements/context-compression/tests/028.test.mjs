import assert from 'node:assert/strict'
import test from 'node:test'
import * as checkpointWindow from '../../../dist/Context/Prefix/Surface.js'

const turnStarts = (n) => Array.from({ length: n }, (_, index) => index + 1)

const cutoffOf = (k, n) => {
  const decision = checkpointWindow.desiredCutoff(k, turnStarts(n))
  return decision.kind === 'KeepFrom' ? decision.cutoffExclusive : null
}

const append = (window, callId, retainCheckpoints) => {
  const result = checkpointWindow.appendCheckpoint(callId, retainCheckpoints, window)
  assert.equal(result.ok, true, result.error ?? '')
  return result.value
}

test('WHAT[context-compression-028] no todowrite checkpoint yields no cutoff', () => {
  for (const k of [1, 2, 5]) assert.equal(cutoffOf(k, 0), null)
})

test('WHAT[context-compression-028] K=1 compresses directly before the current todowrite', () => {
  assert.equal(cutoffOf(1, 1), 1)
  assert.equal(cutoffOf(1, 2), 2)
  assert.equal(cutoffOf(1, 3), 3)
  assert.equal(cutoffOf(1, 4), 4)
})

test('WHAT[context-compression-028] K=2 compresses before the previous todowrite', () => {
  assert.equal(cutoffOf(2, 1), 1)
  assert.equal(cutoffOf(2, 2), 1)
  assert.equal(cutoffOf(2, 3), 2)
  assert.equal(cutoffOf(2, 4), 3)
})

test('WHAT[context-compression-028] two checkpoints inside one turn share their boundary', () => {
  const decision = checkpointWindow.desiredCutoff(2, [1, 4, 4, 7])
  assert.equal(decision.kind, 'KeepFrom')
  assert.equal(decision.cutoffExclusive, 4)
})

test('WHAT[context-compression-028] retainCheckpoints must be positive', () => {
  assert.equal(checkpointWindow.validateK(0).ok, false)
  assert.equal(checkpointWindow.validateK(-1).ok, false)
  assert.match(checkpointWindow.validateK(0).error, /positive integer/)
  assert.equal(checkpointWindow.validateK(1).ok, true)
})

test('WHAT[context-compression-028] each todowrite chooses its own K without erasing unreplaced checkpoints', () => {
  let window = []
  window = append(window, 't1', 4)
  window = append(window, 't2', 1)

  assert.deepEqual(
    window.map((item) => item.callId),
    ['t1', 't2'],
    'K=1 expresses a desire; it does not erase checkpoint evidence before rebase commits',
  )

  window = append(window, 't3', 3)
  const decision = checkpointWindow.desiredCutoffOfWindow(window, [1, 2, 3])
  assert.equal(decision.kind, 'KeepFrom')
  assert.equal(decision.cutoffExclusive, 1, 'T3 K=3 may still keep T1 because no rebase crossed it')
})

test('WHAT[context-compression-028] committed cutoff prunes old checkpoints so a later huge K cannot restore LWR', () => {
  let window = []
  window = append(window, 't1', 4)
  window = append(window, 't2', 1)
  window = append(window, 't3', 3)

  window = checkpointWindow.pruneCheckpointWindow(window, [1, 2, 3], 2)
  assert.deepEqual(window.map((item) => item.callId), ['t2', 't3'])

  window = append(window, 't4', 100000)
  const decision = checkpointWindow.desiredCutoffOfWindow(window, [2, 3, 4])
  assert.equal(decision.kind, 'KeepFrom')
  assert.equal(decision.cutoffExclusive, 2, 'a large K stops extra compression but cannot return to T1')
})

test('WHAT[context-compression-028] duplicate call identity is idempotent only for the same K', () => {
  const first = append([], 'same-call', 2)
  const replay = checkpointWindow.appendCheckpoint('same-call', 2, first)
  assert.equal(replay.ok, true)
  assert.deepEqual(replay.value, first)

  const conflict = checkpointWindow.appendCheckpoint('same-call', 3, first)
  assert.equal(conflict.ok, false)
  assert.match(conflict.error, /two retainCheckpoints values/)
})

test('WHAT[context-compression-028] an unaddressable selected checkpoint proves no boundary', () => {
  let window = []
  window = append(window, 'old', 2)
  window = append(window, 'live', 2)

  const missing = checkpointWindow.desiredCutoffOfWindow(window, [null, 42])
  assert.equal(missing.kind, 'NoPhases')

  const addressable = checkpointWindow.desiredCutoffOfWindow(window, [7, 42])
  assert.equal(addressable.kind, 'KeepFrom')
  assert.equal(addressable.cutoffExclusive, 7)
})
