import assert from 'node:assert/strict'
import test from 'node:test'
import {ToolRuntimeScopeSurface} from '../../../dist/OpenCode/Tools/ToolRuntimeScopeSurface.js'

const evaluate = scenario => ToolRuntimeScopeSurface.evaluateRetirementBlockers({
  managerSessionId: 'manager-road-1',
  devopsChildSessionId: 'devops-session-1',
  devopsPtys: ['devops-pty-1'],
  managerHasDevopsAgent: true,
  ...scenario,
})

test('WHAT[relay-retirement-009] real scope tracking excludes fixed DevOps work from Manager retirement blockers', () => {
  assert.deepEqual(evaluate({}), [])
})

test('WHAT[relay-retirement-009] real scope tracking still blocks an incumbent Engineer and its PTY', () => {
  const blockers = evaluate({engineerChildSessionId: 'engineer-session-1', engineerPtys: ['engineer-pty-1'], managerHasEngineerAgent: true})
  assert.ok(blockers.some(value => value.includes('engineer-session-1')))
  assert.ok(blockers.some(value => value.includes('engineer-pty-1')))
  assert.equal(blockers.some(value => value.includes('devops')), false)
})

test('WHAT[relay-retirement-009] a PTY name containing devops does not change its actual Engineer ownership', () => {
  const blockers = evaluate({engineerChildSessionId: 'engineer-session-1', engineerPtys: ['engineer-devops-migration-pty'], managerHasEngineerAgent: false})
  assert.equal(blockers.length, 1)
  assert.ok(blockers[0].includes('engineer-devops-migration-pty'))
})

test.todo('WHAT[relay-retirement-009] actual Continue transfers fixed DevOps work and processes while Accepted or exceptional termination physically closes them')
