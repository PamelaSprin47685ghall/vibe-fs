import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const scores = [10, 8, 10, 7, 10, 10, 9, 10]

test('WHAT[ASSESS-002] second assessment in one incumbency is rejected without overwriting the first', () => {
  const opened = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1', 'ExistingWorld')
  const assessed = relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-1', ...scores)
  assert.equal(assessed.ok, true)

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

test('WHAT[ASSESS-004] low-score assessment atomically records obligations and grants work ownership', () => {
  const opened = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1', 'ExistingWorld')
  const assessed = relay.assess(opened.state, 'road-1', 'inc-1', 'assessment-1', 'snapshot-1', 'authority-1', ...scores)
  assert.equal(assessed.ok, true)
  assert.deepEqual(relay.view(assessed.state, 'road-1'), {
    activeIncumbency: 'inc-1',
    phase: 'WorkOwned',
    source: 'ExistingWorld',
    retired: [],
  })
  assert.deepEqual(relay.obligations(assessed.state, 'road-1'), [
    'simplicity',
    'granularity',
    'caller_ergonomics',
  ])
})

test('WHAT[ASSESS-003] assessment binds exact execution identity and rejects mismatched authority or incumbency', () => {
  const opened = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1', 'ExistingWorld')
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
  const opened = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-2', 'authority-1', 'ExistingWorld')
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

