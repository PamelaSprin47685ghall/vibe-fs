import assert from 'node:assert/strict'
import test from 'node:test'
import { deliverTwice } from './support/contract.mjs'

const base = {
  sequence: ['S4'],
  reviewed: true,
  confirmed: false,
  reviewOutcome: 'Promoted',
}

test('WHAT[work-006] S4 deliver completes in a single call without confirmation ritual', () => {
  // One genuine delivery intent completes directly
  const completed = deliverTwice(base, { toolCallId: 'c1', providerRunId: 'r1', head: 3 })
  assert.equal(completed.finished, true)
  assert.equal(completed.outcome, 'Delivered')
  assert.equal('retirementCursor' in completed.written[0], false)
  assert.equal(completed.written.map((event) => event.type).join(','), 'IncumbencyRetired')

  // Stayed review relays in a single call with successor opened
  const stayed = deliverTwice({ ...base, reviewOutcome: 'Stayed' }, { toolCallId: 'c2', providerRunId: 'r1', head: 4 })
  assert.equal(stayed.finished, true)
  assert.equal(stayed.outcome, 'Relayed')
  assert.equal(stayed.written.map((event) => event.type).join(','), 'IncumbencyRetired,IncumbencyOpened')
})
