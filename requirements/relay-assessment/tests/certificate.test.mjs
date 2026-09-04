import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

test('WHAT[ASSESS-005] WHAT[ASSESS-006] all-ten assessment creates an exact-bound certificate and permanently removes work/review phase', () => {
  const opened = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1', 'ExistingWorld')
  const assessed = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill(10),
  )
  assert.equal(assessed.ok, true)
  assert.equal(relay.view(assessed.state, 'road-1').phase, 'PerfectAwaitingRetirement')
  assert.deepEqual(relay.certificate(assessed.state, 'road-1'), {
    assessmentId: 'assessment-1',
    snapshotId: 'snapshot-1',
    authorityRevision: 'authority-1',
    valid: true,
  })
  assert.deepEqual(
    relay.assess(
      assessed.state,
      'road-1',
      'inc-1',
      'assessment-2',
      'snapshot-1',
      'authority-1',
      ...Array(8).fill(10),
    ),
    { ok: false, error: 'AssessmentAlreadySubmitted' },
  )
})

test('WHAT[RELAY-008] certificate invalidation is explicit and never reactivates its assessor', () => {
  const opened = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1', 'ExistingWorld')
  const assessed = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill(10),
  )
  const invalidated = relay.invalidateCertificate(assessed.state, 'road-1', 'WorkspaceChanged')
  assert.equal(invalidated.ok, true)
  assert.equal(relay.certificate(invalidated.state, 'road-1').valid, false)
  assert.equal(relay.view(invalidated.state, 'road-1').activeIncumbency, 'inc-1')
})

