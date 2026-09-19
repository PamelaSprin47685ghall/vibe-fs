import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const perfectScores = Array(8).fill('PERFECT')

const open = (state, snapshot = 'snapshot-1') =>
  relay.openIncumbency(state, 'road-1', 'inc-1', snapshot, 'authority-1')

test('WHAT[relay-assessment-009] independent assessment uses read-only engineer and rejects implementer self-verdict', () => {
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
