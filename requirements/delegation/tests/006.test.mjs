import assert from 'node:assert/strict'
import test from 'node:test'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { toolModule, withForkRuntime } from './support/fork-runtime.mjs'

test('WHAT[delegation-006] actual accepted fork returns placement and resume reuses the same child without waiting for completion', async () => {
  const owner = 'owner-placement-binding'
  await withForkRuntime(owner, async runtime => {
    const first = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'FIRST-CHARGE')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    const child = forkTool.child(runtime)
    assert.match(await first, /Ada/)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
    assert.equal(await forkTool.settle(runtime, owner, 'FIRST-RESULT', 'run-first'), true)
    const next = forkTool.executeManagerResume(runtime, toolModule, owner, '', 'Ada', 'NEXT-CHARGE')
    await forkTool.awaitPromptCount(runtime, 2)
    assert.equal(forkTool.acceptPrompt(runtime, 1), true)
    assert.equal(forkTool.child(runtime), child)
    assert.equal(forkTool.childCount(runtime), 1)
    assert.match(await next, /Ada/)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
    assert.equal(await forkTool.settle(runtime, owner, 'NEXT-RESULT', 'run-next'), true)
  })
})

test.todo('WHAT[delegation-006] actual resume preserves the complete execution and model binding through replacement and recovery (GAP-153; GAP-129)')
