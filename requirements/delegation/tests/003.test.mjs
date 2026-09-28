import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import test from 'node:test'
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

test('WHAT[delegation-003] FORK_TOOL_requires_calling_and_resume_rejects_calling', async () => {
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
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[delegation-003] FORK_TOOL_manager_resume_dispatches_to_bound_fixed_devops_directly', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-resume-'))
  const owner = 'manager-devops-resume'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    await forkTool.injectAcceptedAssessment(runtime, owner)
    const resumed = forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-FIRST-CHARGE',
    )
    await waitForPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    const result = await resumed
    assert.match(result, /devops/)
    assert.match(result, /carries this charge now|现已接下这项托付/i)

    assert.equal(await forkTool.settle(runtime, owner, 'DEVOPS-FIRST-ANSWER', 'devops-run-1'), true)
    const joined = await forkTool.executeJoin(runtime, owner)
    assert.match(joined, /DEVOPS-FIRST-ANSWER/)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[delegation-003] explicit runtime reopen preserves the companion DevOps route for a new charge', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-restart-'))
  const owner = 'manager-devops-restart'
  const runtime1 = await forkTool.createRuntime(directory, ownerDescriptor(owner))
  let firstDisposed = false

  try {
    await forkTool.injectAcceptedAssessment(runtime1, owner)
    const first = forkTool.executeManagerResume(
      runtime1,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-FIRST-CHARGE',
    )
    await waitForPromptCount(runtime1, 1)
    assert.equal(forkTool.acceptPrompt(runtime1, 0), true)
    const resumed1 = await first
    assert.match(resumed1, /devops/)
    assert.match(resumed1, /carries this charge now|现已接下这项托付/i)

    assert.equal(await forkTool.settle(runtime1, owner, 'DEVOPS-FIRST-ANSWER', 'devops-run-1'), true)
    const joined1 = await forkTool.executeJoin(runtime1, owner)
    assert.match(joined1, /DEVOPS-FIRST-ANSWER/)

    // Explicit same-process reopen of the durable journal, not an OS crash.
    forkTool.disposeRuntime(runtime1)
    firstDisposed = true
    const runtime2 = await forkTool.createRuntime(directory, ownerDescriptor(owner))

    try {
      // Horizon on fresh runtime is cleared (current process handles empty)
      const horizonView = await forkTool.executeHorizon(runtime2, owner)
      assert.ok(!horizonView.includes('DEVOPS-FIRST-CHARGE'))

      // Companion DevOps is NOT cleaned up, and its state is normalized to accept new charges
      const second = forkTool.executeManagerResume(
        runtime2,
        toolModule,
        owner,
        '',
        'devops',
        'DEVOPS-RESTART-CHARGE',
      )
      await waitForPromptCount(runtime2, 1)
      assert.equal(forkTool.acceptPrompt(runtime2, 0), true)
      const resumed2 = await second
      assert.match(resumed2, /devops/)
      assert.match(resumed2, /carries this charge now|现已接下这项托付/i)

      assert.equal(await forkTool.settle(runtime2, owner, 'DEVOPS-SECOND-ANSWER', 'devops-run-2'), true)
      const joined2 = await forkTool.executeJoin(runtime2, owner)
      assert.match(joined2, /DEVOPS-SECOND-ANSWER/)
    } finally {
      forkTool.disposeRuntime(runtime2)
    }
  } finally {
    if (!firstDisposed) forkTool.disposeRuntime(runtime1)
    rmSync(directory, { recursive: true, force: true })
  }
})

test('WHAT[delegation-003] companion devops is preserved and not abandoned when parent session cancels child work', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'wxs-mgr-devops-cancel-'))
  const owner = 'manager-devops-cancel'
  const runtime = await forkTool.createRuntime(directory, ownerDescriptor(owner))

  try {
    await forkTool.injectAcceptedAssessment(runtime, owner)
    const first = forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-CHARGE-BEFORE-CANCEL',
    )
    await waitForPromptCount(runtime, 1)
    assert.equal(forkTool.acceptPrompt(runtime, 0), true)
    const resumed = await first
    assert.match(resumed, /devops/)
    assert.match(resumed, /carries this charge now|现已接下这项托付/i)

    // Trigger cancelOwnerChildren
    await forkTool.cancelOwnerChildren(runtime, owner)

    // DevOps handle must remain Active (not Abandoned)
    const lifecycle = forkTool.durableLifecycleByname(runtime, owner, 'devops')
    assert.notEqual(lifecycle, 'Abandoned', 'Companion devops must not be abandoned on parent cancel')

    // Verify DevOps is still intact and ready to accept new assignments
    const second = forkTool.executeManagerResume(
      runtime,
      toolModule,
      owner,
      '',
      'devops',
      'DEVOPS-CHARGE-AFTER-CANCEL',
    )
    await waitForPromptCount(runtime, 2)
    assert.equal(forkTool.acceptPrompt(runtime, 1), true)
    const resumedAfterCancel = await second
    assert.match(resumedAfterCancel, /devops/)
    assert.match(resumedAfterCancel, /carries this charge now|现已接下这项托付/i)
  } finally {
    forkTool.disposeRuntime(runtime)
    rmSync(directory, { recursive: true, force: true })
  }
})
