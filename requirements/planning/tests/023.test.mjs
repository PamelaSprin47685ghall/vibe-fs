import assert from 'node:assert/strict'
import test from 'node:test'
import * as prefix from '../../../dist/Context/Prefix/Surface.js'
import * as surface from '../../../dist/Mission/Planning/Surface.js'

test('WHAT[planning-023] tenure reanchor event shape carries session, epochs, work and incumbency', () => {
  assert.equal(typeof prefix.applyTenureReanchor, 'function')
  assert.equal(typeof prefix.isTenureReanchored, 'function')
  assert.equal(typeof prefix.reanchoredTenures, 'function')
})

test('WHAT[planning-023] duplicate incumbencyId is rejected on second tenure reanchor', () => {
  const empty = prefix.empty
  const first = prefix.applyTenureReanchor({ previousEpoch: 0n, nextEpoch: 1n, incumbencyId: 'inc-1' }, empty)
  assert.equal(first.ok, true)
  const second = prefix.applyTenureReanchor({ previousEpoch: 1n, nextEpoch: 2n, incumbencyId: 'inc-1' }, first.value)
  assert.equal(second.ok, false)
  assert.equal(second.error, 'TenureAlreadyReanchored')
})

test('WHAT[planning-023] reanchorRequested is true on fresh handover', () => {
  const rawMessages = [{ id: 'u1', role: 'user', content: 'hi' }]
  const tenure = {
    workId: '',
    incumbencyId: '',
    stage: 'S1',
    openingCursor: 0n,
    previousRange: null,
    isFreshHandover: true,
  }
  const result = surface.PlanningSurface.assembleTenureMessages(rawMessages, tenure, () => '')
  assert.equal(result.reanchorRequested, true)
})
