import assert from 'node:assert/strict'
import test from 'node:test'
import * as forkTool from '../../../dist/Execution/Delegation/ForkToolSurface.js'

test('WHAT[DELEG-006] FORK_continuation_reuses_bound_managed_agent_and_does_not_rebind_tier', () => {
  const sess = forkTool.createSession('agent-bound-1')
  const resumed = forkTool.resumeSession(sess, { byname: 'agent-bound-1' })
  assert.equal(resumed.boundAgentId, 'agent-bound-1')
  assert.equal(resumed.tierRebound, false)
})
