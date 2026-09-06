import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

test('WHAT[ASSESS-008] iteration phase separates assess work and finish before and after review', () => {
  const opened = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1')
  assert.equal(opened.ok, true)
  assert.equal(relay.view(opened.state, 'road-1').phase, 'AuditPending')
  assert.equal(relay.certificate(opened.state, 'road-1'), null)
  assert.equal(relay.retirement(opened.state, 'road-1'), null)

  const repaired = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-repair',
    'snapshot-1',
    'authority-1',
    'REVISE', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT',
  )
  assert.equal(repaired.ok, true)
  assert.equal(relay.view(repaired.state, 'road-1').phase, 'WorkOwned')
  assert.equal(relay.certificate(repaired.state, 'road-1'), null)

  const continued = relay.retireContinue(repaired.state, 'road-1', 'inc-1', 'ret-repair', 'run-1', 'tool-1', 'snapshot-1')
  assert.equal(continued.ok, true)
  assert.equal(relay.retirement(continued.state, 'road-1').outcome, 'Continue')
  const next = relay.openIncumbency(continued.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(next.ok, true)
  assert.equal(relay.view(next.state, 'road-1').phase, 'AuditPending')

  const finished = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-finish',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill('PERFECT'),
  )
  assert.equal(finished.ok, true)
  assert.equal(relay.view(finished.state, 'road-1').phase, 'PerfectAwaitingRetirement')
  assert.equal(relay.certificate(finished.state, 'road-1').valid, true)

  const accepted = relay.retireAccepted(
    finished.state,
    'road-1',
    'inc-1',
    'ret-finish',
    'run-1',
    'tool-1',
    'certificate:assessment-finish',
    'snapshot-1',
  )
  assert.equal(accepted.ok, true)
  assert.equal(relay.retirement(accepted.state, 'road-1').outcome, 'Accepted')
  assert.equal(relay.view(accepted.state, 'road-1').activeIncumbency, null)

  const blocked = relay.openIncumbency(accepted.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(blocked.ok, false)

  const invalidated = relay.invalidateCertificate(accepted.state, 'road-1', 'WorkspaceChanged')
  assert.equal(invalidated.ok, true)
  assert.equal(relay.certificate(invalidated.state, 'road-1').valid, false)

  const reopened = relay.openIncumbency(invalidated.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(reopened.ok, true)
  assert.equal(relay.view(reopened.state, 'road-1').phase, 'AuditPending')
})
