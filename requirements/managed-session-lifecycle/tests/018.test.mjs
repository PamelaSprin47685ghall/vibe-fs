import assert from 'node:assert/strict'
import test from 'node:test'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'

const schemaNode = (kind, extra = {}) => ({
  kind, ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
  int: () => schemaNode(`${kind}-int`, extra),
  nonnegative: () => schemaNode(`${kind}-nonnegative`, extra),
})
const toolModule = {
  tool: { schema: {
    string: () => schemaNode('string'), number: () => schemaNode('number'),
    enum: (values) => schemaNode('enum', { values }),
    array: (inner) => schemaNode('array', { inner }),
  } },
}

test('WHAT[managed-session-lifecycle-018] actual tool runtime detach preserves durable Active child without physical abort', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-process-detach-'))
  const owner = 'manager-process-detach'
  const runtime = await forkTool.createRuntime(directory, [{ sessionId: owner, agent: 'manager' }])
  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'SURVIVE-PLUGIN-RELOAD')
    await forkTool.awaitPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
    await forkTool.detachToolRuntime(runtime)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')
    assert.equal(forkTool.abortCount(runtime), 0)
    assert.equal(forkTool.childCount(runtime), 1)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test.todo('WHAT[managed-session-lifecycle-018] real TurnAborted, retry, Fission and unknown stops preserve durable handles and permit re-enlist after process restart (GAP-133)')
test.todo('WHAT[managed-session-lifecycle-018] authorized logical cancellation alone abandons the durable child and waits for held physical cleanup (GAP-133)')

test.todo('WHAT[managed-session-lifecycle-018] plugin shutdown waits for already admitted provider transforms and terminal callbacks without logical cancellation (GAP-133)')
