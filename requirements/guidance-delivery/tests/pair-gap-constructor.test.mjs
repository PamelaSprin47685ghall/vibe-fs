import assert from 'node:assert/strict'
import test from 'node:test'

import * as pair from '../../../dist/OpenCode/Host/PairProgrammingThoughtSurface.js'

test('WHAT[GD-011] PPT_gap_constructor_receives_the_same_address_exactly_twice_in_pair_order', () => {
  const trace = pair.gapConstructorTrace('message-address-17')

  assert.equal(trace.ok, true)
  assert.deepEqual(trace.inputs, ['message-address-17', 'message-address-17'])
  assert.equal(trace.left, 'after:message-address-17')
  assert.equal(trace.right, 'after:message-address-17')
})

test('WHAT[GD-011] PPT_gap_constructor_failure_propagates_from_the_first_call_and_stops_the_pair', () => {
  const trace = pair.gapConstructorFailureTrace('message-address-failure')

  assert.equal(trace.calls, 1)
  assert.equal(trace.error, 'gap-constructor-failed')
})
