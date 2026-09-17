import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const open = (state) => relay.openIncumbency(state, 'road-1', 'inc-1', 'snapshot-1', 'authority-1')

test('WHAT[RELAY-008] authority update invalidates a perfect certificate without restoring work ownership', () => {
  const first = open(relay.empty())
  const perfect = relay.assess(
    first.state,
    'road-1',
    'inc-1',
    'assessment-perfect',
    'snapshot-1',
    'authority-1',
    ...Array(8).fill('PERFECT'),
  )
  assert.equal(perfect.ok, true)
  assert.equal(relay.certificate(perfect.state, 'road-1').valid, true)

  const revised = relay.advanceAuthority(
    perfect.state,
    'road-1',
    'inc-1',
    'authority-1',
    'authority-2',
    'physical-authority-2',
    'snapshot-2',
  )
  assert.equal(revised.ok, true)
  assert.equal(relay.certificate(revised.state, 'road-1').valid, false)
  assert.equal(relay.view(revised.state, 'road-1').phase, 'PerfectAwaitingRetirement')
})
