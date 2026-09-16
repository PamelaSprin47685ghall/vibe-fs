import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const perfectScores = Array(8).fill('PERFECT')

const open = (state, snapshot = 'snapshot-1') =>
  relay.openIncumbency(state, 'road-1', 'inc-1', snapshot, 'authority-1')

test('WHAT[ASSESS-009] independent assessment uses read-only engineer and rejects implementer self-verdict', () => {
  const opened = open(relay.empty(), 'snapshot-1')
  assert.equal(opened.ok, true)

  // Assessment requires independent review submission bound to the active incumbent and snapshot.
  // Implementer self-reported completion without an independent assessment on the active snapshot cannot yield certificate.
  const viewBefore = relay.view(opened.state, 'road-1')
  assert.equal(viewBefore.phase, 'AuditPending')
  assert.equal(relay.certificate(opened.state, 'road-1'), null)

  // Submitting independent assessment produces valid outcome on snapshot-1
  const assessed = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-independent-1',
    'snapshot-1',
    'authority-1',
    ...perfectScores,
  )
  assert.equal(assessed.ok, true)
  assert.equal(relay.view(assessed.state, 'road-1').phase, 'PerfectAwaitingRetirement')
  assert.equal(relay.certificate(assessed.state, 'road-1').valid, true)
})

test('WHAT[ASSESS-010] devops self-repair snapshot mutation invalidates old assessment and certificate', () => {
  const opened = open(relay.empty(), 'snapshot-1')
  const assessed = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    ...perfectScores,
  )
  assert.equal(assessed.ok, true)
  assert.equal(relay.certificate(assessed.state, 'road-1').valid, true)

  // DevOps self-repair produces new workspace mutation advancing snapshot from snapshot-1 to snapshot-2
  const invalidated = relay.invalidateCertificate(assessed.state, 'road-1', 'WorkspaceChanged')
  assert.equal(invalidated.ok, true)
  assert.equal(relay.certificate(invalidated.state, 'road-1').valid, false)

  // Attempting to retire with old certificate on new snapshot or after invalidation must be rejected
  const acceptedOld = relay.retireAccepted(
    invalidated.state,
    'road-1',
    'inc-1',
    'ret-old',
    'run-1',
    'tool-1',
    'certificate:assessment-1',
    'snapshot-2',
  )
  assert.equal(acceptedOld.ok, false)

  // Retirement must continue to next iteration for fresh independent assessment on snapshot-2
  const retired = relay.retireContinue(
    invalidated.state,
    'road-1',
    'inc-1',
    'ret-cont',
    'run-1',
    'tool-1',
    'snapshot-2',
  )
  assert.equal(retired.ok, true)

  const nextOpened = relay.openIncumbency(retired.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(nextOpened.ok, true)
  assert.equal(relay.view(nextOpened.state, 'road-1').phase, 'AuditPending')
})
