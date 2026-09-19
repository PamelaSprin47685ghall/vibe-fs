import assert from 'node:assert/strict'
import test from 'node:test'
import * as retirement from '../../../dist/Mission/Relay/Retirement/Surface.js'



test('WHAT[relay-retirement-009] fixed devops and road-level resources do not block Continue retirement and transfer cleanly', () => {
  if (typeof retirement.decideWithRoadResources === 'function') {
    const roadResources = [
      { id: 'fixed-devops-1', kind: 'FixedDevOps', roadId: 'road-1' },
      { id: 'devops-pty-1', kind: 'Pty', owner: 'fixed-devops-1' },
    ]
    const decision = retirement.decideWithRoadResources(
      [],
      roadResources,
      { assessed: true, openObligations: 1, testsPassing: false, dirty: false, unmerged: false },
    )
    assert.deepEqual(decision, { decision: 'Retire', outcome: 'Continue' })
  } else {
    assert.fail('retirement.decideWithRoadResources is not yet implemented')
  }
})

test('WHAT[relay-retirement-009] live incumbency-owned child tasks block retirement while devops processes persist across terms', () => {
  if (typeof retirement.decideWithRoadResources === 'function') {
    const incumbencyResources = [
      { id: 'engineer-child-1', kind: 'ChildAgent', owner: 'inc-1' },
    ]
    const roadResources = [
      { id: 'fixed-devops-1', kind: 'FixedDevOps', roadId: 'road-1' },
    ]
    const decision = retirement.decideWithRoadResources(
      incumbencyResources,
      roadResources,
      { assessed: true, openObligations: 0, testsPassing: true, dirty: false, unmerged: false },
    )
    assert.deepEqual(decision, {
      decision: 'BlockedByResources',
      blockers: incumbencyResources,
    })
  } else {
    assert.fail('retirement.decideWithRoadResources is not yet implemented')
  }
})
