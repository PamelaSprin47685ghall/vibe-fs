import assert from 'node:assert/strict'
import test from 'node:test'
import { deliverTwice } from './support/contract.mjs'

const base = {
  sequence: ['S4'],
  reviewed: true,
  confirmed: false,
  reviewOutcome: 'Stayed',
}

test('WHAT[work-006] S4 deliver confirms first and ends only on another call', () => {
  const first = deliverTwice(base, { toolCallId: 'c1', providerRunId: 'r1', head: 3 })
  assert.equal(first.finished, false)
  assert.equal(first.written[0].type, 'ConfirmationCommitted')
  const replay = deliverTwice(
    { ...base, confirmed: true, confirmedToolCallId: 'c1', confirmedProviderRunId: 'r1' },
    { toolCallId: 'c1', providerRunId: 'r1', head: 4 },
  )
  assert.equal(replay.finished, false)
  assert.equal(replay.replay, true)
  const second = deliverTwice(
    { ...base, confirmed: true, confirmedToolCallId: 'c1', confirmedProviderRunId: 'r1', reviewOutcome: 'Promoted' },
    { toolCallId: 'c2', providerRunId: 'r1', head: 5 },
  )
  assert.equal(second.finished, true)
  assert.equal(second.outcome, 'Delivered')
  assert.equal('retirementCursor' in second.written[0], false)
  assert.equal(second.written.some((event) => event.type === 'ConfirmationCommitted'), false)
})
