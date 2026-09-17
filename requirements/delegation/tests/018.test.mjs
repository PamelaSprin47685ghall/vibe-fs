import assert from 'node:assert/strict'
import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
import * as fork from '../../../dist/Execution/Delegation/Fork/Surface.js'
import * as forkTool from '../../../dist/Execution/Delegation/Fork/OpenCode/ToolSurface.js'

const schemaNode = (kind, extra = {}) => ({
  kind,
  ...extra,
  describe: () => schemaNode(`${kind}-described`, extra),
  optional: () => schemaNode(`${kind}-optional`, extra),
  int: () => schemaNode(`${kind}-int`, extra),
  nonnegative: () => schemaNode(`${kind}-nonnegative`, extra),
})

const toolModule = {
  tool: {
    schema: {
      string: () => schemaNode('string'),
      number: () => schemaNode('number'),
      enum: (values) => schemaNode('enum', { values }),
      array: (inner) => schemaNode('array', { inner }),
    },
  },
}

const waitForPromptCount = (runtime, count) => forkTool.awaitPromptCount(runtime, count)

const ownerDescriptor = (sessionId) => [{ sessionId, agent: 'manager' }]

test('WHAT[MANAGED-SESSION-018] FORK_TOOL_process_detach_preserves_durable_active_child_for_restart', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-process-detach-'))
  const owner = 'manager-process-detach'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const placed = forkTool.executeManagerFork(runtime, toolModule, owner, 'engineer', 'Ada', 'SURVIVE-PLUGIN-RELOAD')
    await waitForPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    assert.match(await placed, /Ada/)
    assert.equal(forkTool.durableLifecycleByname(runtime, owner, 'Ada'), 'Active')

    await forkTool.detachToolRuntime(runtime)

    assert.equal(
      forkTool.durableLifecycleByname(runtime, owner, 'Ada'),
      'Active',
      'process/plugin shutdown has no authority to manufacture ParentCancelled',
    )
    assert.equal(forkTool.abortCount(runtime), 0, 'process/plugin detach must not call Host AbortSession for live child agents')
    assert.equal(forkTool.childCount(runtime), 1)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})
