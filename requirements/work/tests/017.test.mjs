import assert from 'node:assert/strict'
import test from 'node:test'
import * as lifecycle from '../../../dist/Mission/Lifecycle/Surface.js'

const surface = lifecycle.LifecycleSurface ?? lifecycle

test('WHAT[work-017] 同一期望权威的后到推进被拒绝，不得改成后到的 next，也不得因此退休或派工', () => {
  const workId = 'work-authority-conflict'
  const scout = surface.advance(workId, '', 'not-the-authority', 'probe-next')
  assert.equal(scout.decision, 'Refuse')
  assert.equal(scout.detail, 'advance expected mismatch')
  const expected = scout.authority
  assert.notEqual(expected, '')
  assert.notEqual(expected, 'not-the-authority')

  const kept = surface.advance(workId, '', expected, 'next-kept')
  assert.equal(kept.decision, 'Write')
  assert.equal(kept.detail, 'advanced')
  assert.equal(kept.names, 'AdvanceCommitted')
  assert.equal(kept.bytes.includes('next-kept'), true)
  assert.equal(kept.bytes.includes('next-late'), false)
  assert.equal(kept.names.includes('IncumbencyRetired'), false)
  assert.equal(kept.names.includes('DevOpsBound'), false)
  assert.notEqual(kept.decision, 'AppendDurable')

  const late = surface.advance(workId, kept.bytes, expected, 'next-late')
  assert.equal(late.decision, 'Refuse')
  assert.equal(late.detail, 'advance expected mismatch')
  assert.equal(late.authority, 'next-kept')
  assert.notEqual(late.authority, 'next-late')
  assert.equal(late.names, '')
  assert.equal(late.bytes, '')
  assert.notEqual(late.decision, 'Write')
  assert.notEqual(late.decision, kept.decision)
})
