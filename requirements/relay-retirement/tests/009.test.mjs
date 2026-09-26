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

test('WHAT[relay-retirement-009] ToolRuntimeScope RetirementBlockersFor excludes fixed road DevOps PTY and agent resources', async () => {
  const scopeMod = await import('../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js')
  const evaluate = scopeMod.evaluateRetirementBlockers || (scopeMod.ToolRuntimeScopeSurface && scopeMod.ToolRuntimeScopeSurface.evaluateRetirementBlockers)
  assert.equal(typeof evaluate, 'function', 'evaluateRetirementBlockers must be exported on ToolRuntimeScope surface')

  const blockers = evaluate({
    managerSessionId: 'manager-road-1',
    devopsChildSessionId: 'devops-session-1',
    devopsPtys: ['devops-pty-1'],
    managerHasDevopsAgent: true,
  })

  assert.deepEqual(blockers, [], 'DevOps PTY and agent runs must not appear in Manager retirement blockers')
})

test('WHAT[relay-retirement-009] ToolRuntimeScope RetirementBlockersFor preserves incumbency-owned Engineer blocking resources', async () => {
  const scopeMod = await import('../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js')
  const evaluate = scopeMod.evaluateRetirementBlockers || (scopeMod.ToolRuntimeScopeSurface && scopeMod.ToolRuntimeScopeSurface.evaluateRetirementBlockers)
  assert.equal(typeof evaluate, 'function', 'evaluateRetirementBlockers must be exported on ToolRuntimeScope surface')

  const blockers = evaluate({
    managerSessionId: 'manager-road-1',
    devopsChildSessionId: 'devops-session-1',
    devopsPtys: ['devops-pty-1'],
    managerHasDevopsAgent: true,
    engineerChildSessionId: 'engineer-session-1',
    engineerPtys: ['eng-pty-1'],
    managerHasEngineerAgent: true,
  })

  assert.equal(blockers.length > 0, true, 'Engineer resources must block Manager retirement')
  assert.equal(blockers.some((b) => b.includes('devops')), false, 'DevOps resources must not be among the blockers')
  assert.equal(blockers.some((b) => b.includes('engineer-session-1')), true, 'Engineer child session must be among blockers')
  assert.equal(blockers.some((b) => b.includes('eng-pty-1')), true, 'Engineer PTY must be among blockers')
})


test('WHAT[relay-retirement-009] ToolRuntimeScope RetirementBlockersFor does not falsely exclude Engineer PTY whose name contains devops substring', async () => {
  const scopeMod = await import('../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js')
  const evaluate = scopeMod.evaluateRetirementBlockers || (scopeMod.ToolRuntimeScopeSurface && scopeMod.ToolRuntimeScopeSurface.evaluateRetirementBlockers)
  assert.equal(typeof evaluate, 'function', 'evaluateRetirementBlockers must be exported on ToolRuntimeScope surface')

  const blockers = evaluate({
    managerSessionId: 'manager-road-1',
    devopsChildSessionId: 'devops-session-1',
    devopsPtys: ['devops-pty-1'],
    managerHasDevopsAgent: true,
    engineerChildSessionId: 'engineer-session-1',
    engineerPtys: ['engineer-devops-migration-pty'],
    managerHasEngineerAgent: false,
  })

  assert.equal(blockers.length, 1, 'Engineer PTY containing devops substring must still block Manager retirement')
  assert.equal(blockers[0].includes('engineer-devops-migration-pty'), true, 'Blocker must be the Engineer PTY')
})
