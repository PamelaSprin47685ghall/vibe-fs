import assert from 'node:assert/strict'
import test from 'node:test'
import * as checkpointWindow from '../../../dist/Context/Prefix/Surface.js'

const turnStarts = (n) => Array.from({ length: n }, (_, index) => index + 1)

const cutoffOf = (n) => {
  const decision = checkpointWindow.desiredCutoff(turnStarts(n))
  return decision.kind === 'KeepFrom' ? decision.cutoffExclusive : null
}

const append = (window, callId) => checkpointWindow.appendCheckpoint(callId, window)

test('WHAT[context-compression-028] no todowrite checkpoint yields no cutoff', () => {
  assert.equal(cutoffOf(0), null)
})

test('WHAT[context-compression-028] fixed K=3 folds directly before the third-newest checkpoint', () => {
  assert.equal(cutoffOf(1), 1)
  assert.equal(cutoffOf(2), 1)
  assert.equal(cutoffOf(3), 1)
  assert.equal(cutoffOf(4), 2)
  assert.equal(cutoffOf(5), 3)
  assert.equal(cutoffOf(6), 4)
})

test('WHAT[context-compression-028] two checkpoints inside one turn share their boundary', () => {
  const decision = checkpointWindow.desiredCutoff([1, 4, 4, 7])
  assert.equal(decision.kind, 'KeepFrom')
  assert.equal(decision.cutoffExclusive, 4)
})

test('WHAT[context-compression-028] each todowrite adds its checkpoint without erasing unreplaced checkpoints', () => {
  let window = []
  window = append(window, 't1')
  window = append(window, 't2')

  assert.deepEqual(
    window.map((item) => item.callId),
    ['t1', 't2'],
    'appending a checkpoint does not erase older checkpoint evidence before rebase commits',
  )

  window = append(window, 't3')
  const decision = checkpointWindow.desiredCutoffOfWindow(window, [1, 2, 3])
  assert.equal(decision.kind, 'KeepFrom')
  assert.equal(decision.cutoffExclusive, 1, 'with three checkpoints the window keeps all three from the oldest boundary')
})

test('WHAT[context-compression-028] committed cutoff prunes old checkpoints so a later checkpoint cannot restore LWR', () => {
  let window = []
  window = append(window, 't1')
  window = append(window, 't2')
  window = append(window, 't3')

  window = checkpointWindow.pruneCheckpointWindow(window, [1, 2, 3], 2)
  assert.deepEqual(window.map((item) => item.callId), ['t2', 't3'])

  window = append(window, 't4')
  const decision = checkpointWindow.desiredCutoffOfWindow(window, [2, 3, 4])
  assert.equal(decision.kind, 'KeepFrom')
  assert.equal(decision.cutoffExclusive, 2, 'the window cannot return to the pruned T1')
})

test('WHAT[context-compression-028] duplicate call identity is idempotent', () => {
  const first = append([], 'same-call')
  const replay = checkpointWindow.appendCheckpoint('same-call', first)
  assert.deepEqual(replay, first)
})

test('WHAT[context-compression-028] an unaddressable selected checkpoint proves no boundary', () => {
  let window = []
  window = append(window, 'old')
  window = append(window, 'live')

  const missing = checkpointWindow.desiredCutoffOfWindow(window, [null, 42])
  assert.equal(missing.kind, 'NoPhases')

  const addressable = checkpointWindow.desiredCutoffOfWindow(window, [7, 42])
  assert.equal(addressable.kind, 'KeepFrom')
  assert.equal(addressable.cutoffExclusive, 7)
})
