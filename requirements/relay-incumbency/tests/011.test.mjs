import assert from 'node:assert/strict'
import test from 'node:test'
import * as relay from '../../../dist/Mission/Relay/Surface.js'



test('WHAT[RELAY-011] accepted assignments and running processes preserve continuity across manager terms without record loss', () => {
  const first = relay.openIncumbency(relay.empty(), 'road-1', 'inc-1', 'snapshot-1', 'authority-1')
  assert.equal(first.ok, true)

  const assessed = relay.assess(
    first.state,
    'road-1',
    'inc-1',
    'assessment-1',
    'snapshot-1',
    'authority-1',
    'REVISE', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT', 'PERFECT',
  )
  assert.equal(assessed.ok, true)

  const retired = relay.retireContinue(assessed.state, 'road-1', 'inc-1', 'ret-1', 'run-1', 'tool-1', 'snapshot-1')
  assert.equal(retired.ok, true)

  const next = relay.openIncumbency(retired.state, 'road-1', 'inc-2', 'snapshot-2', 'authority-1')
  assert.equal(next.ok, true)

  if (typeof relay.roadDevOps === 'function') {
    const devops = relay.roadDevOps(next.state, 'road-1')
    assert.equal(devops.incumbentId, 'inc-2', 'control must transfer to next active incumbent')
  } else {
    assert.fail('relay.roadDevOps is not yet implemented')
  }
})
