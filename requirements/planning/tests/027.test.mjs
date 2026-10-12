import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'
import { userText } from './support/plan-messages.mjs'

test('WHAT[planning-017] tenure reanchor event shape carries session, epochs, work and incumbency', () => {
  assert.equal(typeof prefix.applyTenureReanchor, 'function')
  assert.equal(typeof prefix.isTenureReanchored, 'function')
  assert.equal(typeof prefix.reanchoredTenures, 'function')
})

test('WHAT[planning-017] duplicate incumbencyId is rejected on second tenure reanchor', () => {
  const empty = prefix.empty
  const first = prefix.applyTenureReanchor({ previousEpoch: 0n, nextEpoch: 1n, incumbencyId: 'inc-1' }, empty)
  assert.equal(first.ok, true)
  const second = prefix.applyTenureReanchor({ previousEpoch: 1n, nextEpoch: 2n, incumbencyId: 'inc-1' }, first.value)
  assert.equal(second.ok, false)
  assert.equal(second.error, 'TenureAlreadyReanchored')
})

test('WHAT[planning-017] reanchorRequested is true on fresh handover and deduplicated via isTenureReanchored', () => {
  const rawMessages = [userText('u1', 'hi')]
  const tenure = {
    workId: 'w-27',
    incumbencyId: 'inc-1',
    stage: 'S1',
    openingCursor: 0n,
    previousRange: null,
    isFreshHandover: true,
  }
  const result = surface.PlanningSurface.assembleTenureMessages(rawMessages, tenure, () => '', {})
  assert.equal(result.reanchorRequested, true)

  // Once the tenure is reanchored, a later empty stop must not re-request it:
  // ReanchorRequested derives from the durable reanchor state, not from every
  // empty assistant stop. The continuing tenure (isFreshHandover=false) must
  // not re-request.
  const continuing = surface.PlanningSurface.assembleTenureMessages(
    rawMessages,
    { ...tenure, isFreshHandover: false },
    () => '',
    {},
  )
  assert.equal(continuing.reanchorRequested, false)
})