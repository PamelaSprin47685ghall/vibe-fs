import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'

const scores = ['PERFECT', 'REVISE', 'PERFECT', 'REVISE', 'PERFECT', 'PERFECT', 'REVISE', 'PERFECT']

const open = (state, snapshot = 'snapshot-1') =>
  relay.openIncumbency(state, 'road-1', 'inc-1', snapshot, 'authority-1')

test('WHAT[relay-assessment-007] assessment accepts latest workspace snapshot on submit', () => {
  const opened = open(relay.empty(), 'snapshot-2')
  const assessed = relay.assess(
    opened.state,
    'road-1',
    'inc-1',
    'assessment-new',
    'snapshot-latest',
    'authority-1',
    ...Array(8).fill('PERFECT'),
  )
  assert.equal(assessed.ok, true)
  const valid = relay.assess(
    assessed.state,
    'road-1',
    'inc-1',
    'assessment-new',
    'snapshot-latest',
    'authority-1',
    ...Array(8).fill('PERFECT'),
  )
  assert.equal(valid.ok, true)
})
