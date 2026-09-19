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

test('WHAT[delegation-006] FORK_continuation_reuses_bound_managed_agent_and_does_not_rebind_tier', () => {
  const result = fork.reuseBinding('Ada', 'inspector', 'coder', 'deep', 'continue the charge')
  assert.equal(result.ok, true)
  assert.equal(result.byname, 'Ada')
  assert.equal(result.managedAgent, 'inspector')
  assert.equal(result.requestedAgent, 'coder')
  assert.equal(result.tier, 'deep')
  assert.equal(result.authorityTransferred, false)
  assert.equal(Object.hasOwn(result, 'agentId'), false)
})
