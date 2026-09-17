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

test('WHAT[DELEG-003] FORK_road_with_calling_is_independent_and_omitted_calling_continues_byname', () => {
  const independent = fork.chooseRoad('Manager', 'Ada', 'inspect the retry path')
  assert.equal(independent.ok, true)
  assert.equal(independent.road, 'Independent')
  assert.equal(independent.byname, 'Ada')
  assert.equal(independent.charge, 'inspect the retry path')
  assert.equal(independent.authorityTransferred, false)

  const continuation = fork.chooseRoad('', 'Ada', 'continue the retry path')
  assert.equal(continuation.ok, true)
  assert.equal(continuation.road, 'Continuation')
  assert.equal(continuation.byname, 'Ada')
  assert.equal(continuation.calling, null)
})

test('WHAT[DELEG-003] FORK_TOOL_requires_calling_and_resume_rejects_calling', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-fork-split-'))
  const owner = 'manager-fork-split'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    const blankCalling = await forkTool.executeManagerFork(
      runtime,
      toolModule,
      owner,
      '',
      'Ada',
      'FORK-NEEDS-CALLING',
    )
    assert.match(blankCalling, /calling is required|必须携带 calling/i)
    assert.equal(forkTool.childCount(runtime), 0, 'blank calling must not place any child')

    const rejectedCalling = await forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      'engineer',
      'Ada',
      'RESUME-REJECTS-CALLING',
    )
    assert.match(rejectedCalling, /never calls a new one|从不叫起新人/i)
    assert.match(rejectedCalling, /use fork|用 fork/i)
    assert.equal(forkTool.childCount(runtime), 0, 'resume must not place any child')
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})

test('WHAT[DELEG-003] FORK_TOOL_manager_resume_dispatches_to_bound_fixed_devops_directly', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-resume-'))
  const owner = 'manager-devops-resume'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    setTimeout(() => {
      forkTool.acceptPrompt(runtime, 0)
    }, 50)

    const resumed = forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-FIRST-CHARGE',
    )
    const result = await resumed
    assert.match(result, /devops/)
    assert.match(result, /carries this charge now|现已接下这项托付/i)

    assert.equal(await forkTool.settle(runtime, owner, 'DEVOPS-FIRST-ANSWER', 'devops-run-1'), true)
    const joined = await forkTool.executeJoin(runtime, owner)
    assert.match(joined, /DEVOPS-FIRST-ANSWER/)
  } finally {
    forkTool.disposeRuntime(runtime)
  }
})
