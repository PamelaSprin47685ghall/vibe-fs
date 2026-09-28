import assert from 'node:assert/strict'
import test from 'node:test'
import * as recovery from '../../../dist/Execution/Session/Recovery/Surface.js'

test('WHAT[crash-reconciliation-013] all finite outcome triples respect priority independent of order', () => {
  const outcomes = ['NoRecoveryRequired', 'Recovered', 'Waiting', 'Blocked']
  assert.equal(recovery.combine([]), 'NoRecoveryRequired')
  for (const first of outcomes) for (const second of outcomes) for (const third of outcomes) {
    const values = [first, second, third]
    const expected = values.includes('Blocked') ? 'Blocked' : values.includes('Waiting') ? 'Waiting' : values.includes('Recovered') ? 'Recovered' : 'NoRecoveryRequired'
    assert.equal(recovery.combine(values), expected)
    assert.equal(recovery.combine([...values].reverse()), expected)
  }
})

test('WHAT[crash-reconciliation-013] actual family authorization follows blocked then waiting then ready', () => {
  assert.equal(recovery.authorize('root', 9, [{ session: 'child', state: 'Blocked' }]).state, 'FamilyBlocked')
  assert.equal(recovery.authorize('root', 9, [{ session: 'child', state: 'Waiting' }, { session: 'other', state: 'NoRecoveryRequired' }]).state, 'FamilyWaiting')
  const ready = recovery.authorize('root', 9, [{ session: 'child', state: 'Recovered' }])
  assert.equal(ready.state, 'FamilyReady')
  assert.equal(ready.root, 'root')
  assert.equal(ready.sequence, 9)
})
