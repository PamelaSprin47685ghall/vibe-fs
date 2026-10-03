import assert from 'node:assert/strict'
import test from 'node:test'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'

for (const kind of ['Failed', 'Aborted']) {
  test('WHAT[delegation-027] root affinity also rejects old ' + kind + ' without a prepared handoff', async () => {
    const owner = 'unprepared-affinity-' + kind
    await withForkRuntime(owner, async runtime => {
      const started = forkTool.startUnprepared(runtime, owner, 'WORK-WITHOUT-HANDOFF')
      await forkTool.awaitPromptCount(runtime, 1)
      assert.equal(forkTool.acceptPrompt(runtime, 0), true)
      assert.equal((await started).ok, true)
      const before = forkTool.workSnapshot(runtime, owner)
      assert.equal(before.length, 1)
      await forkTool.emitStopForRoot(runtime, owner, 'another-work-root', kind)
      assert.deepEqual(forkTool.workSnapshot(runtime, owner), before)
      assert.equal(await forkTool.settle(runtime, owner, 'CURRENT-WORK-ANSWER', 'current-provider'), true)
      const result = await forkTool.executeJoin(runtime, owner)
      assert.match(result, /CURRENT-WORK-ANSWER/)
      assert.doesNotMatch(result, /old work stop/)
    })
  })
}
import { toolModule, withForkRuntime } from './support/fork-runtime.mjs'

test('WHAT[delegation-027] an active road rejects a new charge and becomes reusable after completion before join consumption', async () => {
  const owner = 'owner-busy-road'
  await withForkRuntime(owner, async runtime => {
    const first = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'FIRST')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await first, /Ada/)
    const rejected = await forkTool.executeManagerResume(runtime, toolModule, owner, '', 'Ada', 'SECOND-WHILE-BUSY')
    assert.doesNotMatch(rejected, /carries this charge now|现已接下这项托付/)
    assert.equal(forkTool.promptCount(runtime), 1)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
    assert.equal(await forkTool.settle(runtime, owner, 'FIRST-DONE', 'first-run'), true)
    const next = forkTool.executeManagerResume(runtime, toolModule, owner, '', 'Ada', 'SECOND-AFTER-COMPLETION')
    await forkTool.awaitPromptCount(runtime, 2)
    assert.equal(forkTool.acceptPrompt(runtime, 1), true)
    assert.match(await next, /carries this charge now|现已接下这项托付/)
    assert.equal(forkTool.childCount(runtime), 1)
    assert.equal(await forkTool.settle(runtime, owner, 'SECOND-DONE', 'second-run'), true)
  })
})
