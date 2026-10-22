import assert from 'node:assert/strict'
import test from 'node:test'
import {
  agentIdOf,
  childTurn,
  forkCall,
  joinCall,
  observeTerminals,
  physicalUserMessageOf,
  resumeCall,
  workLifecycleOf,
  withRealFork,
} from './support/real-fork.mjs'

test('WHAT[delegation-006] actual accepted fork returns placement and resume reuses the same child without waiting for completion', async () => {
  const owner = 'owner-placement-binding'
  await withRealFork(owner, async ({ hooks, createdIds, runtime }) => {
    const terminals = await observeTerminals(runtime)
    try {
      // The fork returns its placement while the child's work is still running.
      const first = await forkCall(hooks, owner, 'Ada', 'FIRST-CHARGE')
      assert.match(first, /Ada/)
      assert.equal(createdIds.length, 1)
      const child = createdIds[0]
      const agentId = agentIdOf(first)
      const firstRoot = physicalUserMessageOf(runtime, child).id
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, firstRoot), 'Active')

      await childTurn(hooks, runtime, child, `run-${child}-1`, 'FIRST PROSE', 'FIRST-RESULT', terminals.awaitRun)
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, firstRoot), 'CompletedAwaitingJoin')

      // The resume reuses the same physical child and also returns while its work runs.
      const next = await resumeCall(hooks, owner, 'Ada', 'NEXT-CHARGE')
      assert.match(next, /Ada/)
      assert.equal(createdIds.length, 1, 'the resume must reuse the same physical child')
      assert.equal(agentIdOf(next), agentId)
      const nextRoot = physicalUserMessageOf(runtime, child).id
      assert.notEqual(nextRoot, firstRoot, 'the resumed work roots a new physical input')
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, nextRoot), 'Active')

      await childTurn(hooks, runtime, child, `run-${child}-2`, 'NEXT PROSE', 'NEXT-RESULT', terminals.awaitRun)
      assert.equal(workLifecycleOf(runtime, owner, agentId, child, nextRoot), 'CompletedAwaitingJoin')

      const joined = await joinCall(hooks, owner)
      assert.match(joined, /FIRST-RESULT/)
      assert.match(joined, /NEXT-RESULT/)
    } finally {
      terminals.dispose()
    }
  })
})

test.todo('WHAT[delegation-006] actual resume preserves the complete execution and model binding through replacement and recovery (GAP-153; GAP-129)')
