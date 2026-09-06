import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const scores = [10, 8, 10, 7, 10, 10, 9, 10]

const open = (state, snapshot = 'snapshot-1') =>
  relay.openIncumbency(state, 'road-1', 'inc-1', snapshot, 'authority-1')

test('WHAT[ASSESS-002] second assessment in one iteration is rejected without overwriting the first', () => {
  const opened = open(relay.empty())
  const assessed = relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-1', ...scores)
  assert.equal(assessed.ok, true)

  const replayed = relay.assess(
    assessed.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...scores,
  )
  assert.equal(replayed.ok, true)

  const conflicted = relay.assess(
    assessed.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill(10),
  )
  assert.deepEqual(conflicted, { ok: false, error: 'AssessmentReplayConflict' })

  const second = relay.assess(
    assessed.state,
    'road-1',
    'inc-1',
    'assessment-2',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill(10),
  )
  assert.deepEqual(second, { ok: false, error: 'AssessmentAlreadySubmitted' })
})

test('WHAT[ASSESS-002] cross-iteration replay of another iteration assessment is rejected', () => {
  const opened = open(relay.empty())
  const assessed = relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-1', ...scores)
  assert.equal(assessed.ok, true)
  const retired = relay.retireContinue(assessed.state, 'road-1', 'inc-1', 'ret-1', 'run-1', 'tool-1', 'snapshot-1')
  assert.equal(retired.ok, true)
  const next = relay.openIncumbency(retired.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(next.ok, true)

  const replayed = relay.assess(
    next.state,
    'road-1',
    'inc-2',
    'assessment-1',
    'snapshot-2',
    'authority-1',
    ...scores,
  )
  assert.deepEqual(replayed, { ok: false, error: 'AssessmentReplayConflict' })
})

test('WHAT[ASSESS-004] low-score assessment atomically records obligations and grants work ownership', () => {
  const opened = open(relay.empty())
  const assessed = relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-1', ...scores)
  assert.equal(assessed.ok, true)
  assert.deepEqual(relay.view(assessed.state, 'road-1'), {
    activeIncumbency: 'inc-1',
    phase: 'WorkOwned',
    retired: [],
  })
  assert.equal(relay.certificate(assessed.state, 'road-1'), null)
})

test('WHAT[ASSESS-003] assessment binds exact execution identity and rejects mismatched authority or iteration', () => {
  const opened = open(relay.empty())
  assert.deepEqual(
    relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-X', ...scores),
    { ok: false, error: 'AuthorityRevisionStale' },
  )
  assert.deepEqual(
    relay.assess(opened.state, 'road-1', 'inc-X', 'assessment-1', 'snapshot-1', 'authority-1', ...scores),
    { ok: false, error: 'IncumbencyNotActive' },
  )
})

test('WHAT[ASSESS-007] stale snapshot does not consume the one semantic assessment slot', () => {
  const opened = open(relay.empty(), 'snapshot-2')
  const stale = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-stale',
    'snapshot-old',
    'authority-1',
    ...Array(8).fill(10),
  )
  assert.deepEqual(stale, { ok: false, error: 'AuditSnapshotStale' })
  const valid = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-valid',
    'snapshot-2',
    'authority-1',
    ...Array(8).fill(10),
  )
  assert.equal(valid.ok, true)
})
