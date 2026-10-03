import assert from 'node:assert/strict'
import * as forkTool from '../../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'
import { toolModule, withForkRuntime } from './fork-runtime.mjs'

export { forkTool, withForkRuntime }

export async function admit(runtime, owner, count, charge) {
  const invocation = count === 1
    ? forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', charge)
    : forkTool.executeManagerResume(runtime, toolModule, owner, '', 'Ada', charge)
  await forkTool.awaitPromptCount(runtime, count)
  assert.equal(forkTool.acceptPrompt(runtime, count - 1), true)
  assert.match(await invocation, /Ada/)
  const active = forkTool.workSnapshot(runtime, owner).filter(work => work.lifecycle === 'Active')
  assert.equal(active.length, 1, 'one real accepted work is active on this road')
  return active[0]
}

export async function assertCold(runtime, directory, owner) {
  assert.deepEqual(await forkTool.coldWorkSnapshot(directory, owner), forkTool.workSnapshot(runtime, owner))
}
